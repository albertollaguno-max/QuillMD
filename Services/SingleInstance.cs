using System.Collections.Generic;
using System.IO;

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
        public static event Action<IReadOnlyList<string>>? MessageReceived;

        public static SingleInstanceResult TryAcquire(string[] args, out IReadOnlyList<string> validatedArgs)
        {
            validatedArgs = args.Where(File.Exists).ToArray();
            // Skeleton: always behave as first instance until Task 3+ wire mutex and pipe.
            return SingleInstanceResult.FirstInstance;
        }

        public static void Release()
        {
            // Skeleton: no-op until Task 3 introduces the mutex.
        }

        // Helper used by App once Task 5 subscribes; kept here so it compiles in the skeleton.
        internal static void RaiseMessageReceived(IReadOnlyList<string> paths)
        {
            MessageReceived?.Invoke(paths);
        }
    }
}
