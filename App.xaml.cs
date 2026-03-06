using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace QuillMD
{
    public partial class App : Application
    {
        public static bool IsDarkTheme { get; private set; } = true;
        public static string? StartupFilePath { get; private set; }

        // Log path: same folder as the exe, easy to find
        public static readonly string LogPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "QuillMD_crash.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            // Hook all unhandled exception sources
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
                LogFatal("AppDomain.UnhandledException", ex.ExceptionObject as Exception);

            DispatcherUnhandledException += (s, ex) =>
            {
                LogFatal("DispatcherUnhandledException", ex.Exception);
                ex.Handled = true; // Keep app alive so user can see the message box
                MessageBox.Show(
                    $"Error inesperado:\n\n{ex.Exception.Message}\n\n" +
                    $"Tipo: {ex.Exception.GetType().FullName}\n\n" +
                    $"(Log guardado en: {LogPath})",
                    "Error — QuillMD",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            };

            TaskScheduler.UnobservedTaskException += (s, ex) =>
                LogFatal("TaskScheduler.UnobservedTaskException", ex.Exception);

            // Capture file path from command-line arguments
            if (e.Args.Length > 0 && File.Exists(e.Args[0]))
                StartupFilePath = e.Args[0];

            // Log startup
            Log($"=== QuillMD started === args=[{string.Join(", ", e.Args)}]");
            base.OnStartup(e);
        }

        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
            }
            catch { }
        }

        public static void LogFatal(string source, Exception? ex)
        {
            try
            {
                string msg = ex == null ? "(null exception)"
                    : $"{ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}";
                if (ex?.InnerException != null)
                    msg += $"\n--- Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";

                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] FATAL [{source}]\n{msg}\n\n");
            }
            catch { }
        }

        public static void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            ApplyTheme();
        }

        public static void ApplyTheme()
        {
            var dictionaries = Current.Resources.MergedDictionaries;
            dictionaries.Clear();

            string themeFile = IsDarkTheme ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
            dictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(themeFile, UriKind.Relative)
            });
        }
    }
}
