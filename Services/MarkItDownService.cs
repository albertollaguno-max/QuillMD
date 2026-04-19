using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QuillMD.Services
{
    public enum ConversionStatus
    {
        Success,
        ExecutableMissing,
        NonZeroExit,
        EmptyOutput,
        Cancelled,
        Timeout,
        Exception
    }

    public record ConversionResult(
        ConversionStatus Status,
        string Markdown,
        string ErrorMessage);

    public static class MarkItDownService
    {
        public const int DefaultTimeoutSeconds = 60;

        private static string ExecutablePath =>
            Path.Combine(AppContext.BaseDirectory, "markitdown", "markitdown.exe");

        public static async Task<ConversionResult> ConvertAsync(
            string inputPath,
            CancellationToken cancellationToken,
            int timeoutSeconds = DefaultTimeoutSeconds)
        {
            if (!File.Exists(ExecutablePath))
            {
                return new ConversionResult(
                    ConversionStatus.ExecutableMissing,
                    string.Empty,
                    $"No se encontró markitdown.exe en:\n{ExecutablePath}\n\nReinstala QuillMD.");
            }

            var psi = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            // Sin terminal, Python bajo Windows escribe stdout en cp1252 por defecto
            // y los acentos salen mal. Forzar UTF-8 end-to-end en el proceso hijo.
            psi.Environment["PYTHONUTF8"] = "1";
            psi.Environment["PYTHONIOENCODING"] = "utf-8";
            psi.ArgumentList.Add(inputPath);

            Process? process = null;
            try
            {
                process = Process.Start(psi);
                if (process == null)
                {
                    return new ConversionResult(
                        ConversionStatus.Exception,
                        string.Empty,
                        "No se pudo iniciar el proceso markitdown.exe");
                }

                using (process)
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                    var stdoutTask = process.StandardOutput.ReadToEndAsync();
                    var stderrTask = process.StandardError.ReadToEndAsync();

                    try
                    {
                        await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        TryKill(process);
                        if (cancellationToken.IsCancellationRequested)
                            return new ConversionResult(ConversionStatus.Cancelled, string.Empty, "Cancelado por el usuario.");
                        return new ConversionResult(ConversionStatus.Timeout, string.Empty,
                            $"La conversión superó {timeoutSeconds} s y se abortó.");
                    }

                    string[] outputs = await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
                    string stdout = outputs[0];
                    string stderr = outputs[1];

                    if (process.ExitCode != 0)
                    {
                        string preview = Truncate(stderr, 500);
                        return new ConversionResult(ConversionStatus.NonZeroExit, string.Empty,
                            $"markitdown.exe terminó con código {process.ExitCode}.\n\n{preview}");
                    }

                    if (string.IsNullOrWhiteSpace(stdout))
                    {
                        return new ConversionResult(ConversionStatus.EmptyOutput, string.Empty,
                            "El archivo no contiene contenido extraíble.");
                    }

                    return new ConversionResult(ConversionStatus.Success, stdout, string.Empty);
                }
            }
            catch (Exception ex)
            {
                TryKill(process);
                return new ConversionResult(ConversionStatus.Exception, string.Empty,
                    $"Error inesperado: {ex.Message}");
            }
        }

        private static void TryKill(Process? p)
        {
            try
            {
                if (p != null && !p.HasExited)
                    p.Kill(entireProcessTree: true);
            }
            catch { /* best effort */ }
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max) + "…");
    }
}
