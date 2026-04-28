using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QuillMD.Services
{
    public enum SingleInstanceResult
    {
        FirstInstance,
        ForwardedToFirst,
        FirstUnreachable
    }

    public static class SingleInstance
    {
        private const string ProtocolHeader = "v1";

        private static Mutex? _mutex;
        private static CancellationTokenSource? _cts;
        private static Task? _serverTask;
        private static string? _pipeName;

        public static event Action<IReadOnlyList<string>>? MessageReceived;

        public static SingleInstanceResult TryAcquire(string[] args, out IReadOnlyList<string> validatedArgs)
        {
            validatedArgs = args.Where(File.Exists).ToArray();

            var sid = WindowsIdentity.GetCurrent().User?.Value ?? "anon";
            var mutexName = $@"Local\QuillMD-SingleInstance-{sid}";
            _pipeName = $"QuillMD-Pipe-{sid}";

            var mutex = new Mutex(initiallyOwned: true, mutexName, out bool createdNew);
            if (createdNew)
            {
                _mutex = mutex;
                _cts = new CancellationTokenSource();
                _serverTask = Task.Run(() => RunServerLoop(_pipeName, _cts.Token));
                return SingleInstanceResult.FirstInstance;
            }

            // Not the first instance — discard the unowned mutex (do not Release: not ours).
            mutex.Dispose();
            return TryForwardToFirstInstance(_pipeName!, validatedArgs);
        }

        public static void Release()
        {
            try { _cts?.Cancel(); } catch { }
            try { _serverTask?.Wait(500); } catch { }
            try { _mutex?.ReleaseMutex(); } catch { }
            try { _mutex?.Dispose(); } catch { }
            _cts = null;
            _serverTask = null;
            _mutex = null;
        }

        private static SingleInstanceResult TryForwardToFirstInstance(string pipeName, IReadOnlyList<string> paths)
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    serverName: ".",
                    pipeName: pipeName,
                    direction: PipeDirection.Out);

                client.Connect(2000); // ms

                var sb = new StringBuilder();
                sb.Append(ProtocolHeader).Append('\n');
                foreach (var p in paths)
                    sb.Append(p).Append('\n');

                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                client.Write(bytes, 0, bytes.Length);
                client.Flush();
                return SingleInstanceResult.ForwardedToFirst;
            }
            catch (TimeoutException)
            {
                return SingleInstanceResult.FirstUnreachable;
            }
            catch (IOException)
            {
                return SingleInstanceResult.FirstUnreachable;
            }
            catch (UnauthorizedAccessException)
            {
                return SingleInstanceResult.FirstUnreachable;
            }
        }

        private static async Task RunServerLoop(string pipeName, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.In,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var raw = await reader.ReadToEndAsync().ConfigureAwait(false);
                    var lines = raw.Split('\n', StringSplitOptions.None)
                                   .Select(l => l.TrimEnd('\r'))
                                   .ToArray();

                    if (lines.Length == 0 || lines[0] != ProtocolHeader)
                    {
                        // Unknown protocol — ignore and keep listening.
                        continue;
                    }

                    var paths = lines.Skip(1)
                                     .Where(l => !string.IsNullOrEmpty(l))
                                     .ToArray();
                    MessageReceived?.Invoke(paths);
                }
                catch (OperationCanceledException) { /* graceful shutdown */ }
                catch (Exception)
                {
                    // Swallow per-connection errors; keep the server alive.
                }
                finally
                {
                    server?.Dispose();
                }
            }
        }
    }
}
