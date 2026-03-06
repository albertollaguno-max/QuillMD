using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace PlannamTypora.Services
{
    public class FileService
    {
        private const int MaxRecentFiles = 10;
        private const string RecentFilesKey = "RecentFiles";

        public static string? OpenFile(string? initialDirectory = null)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Abrir archivo Markdown",
                Filter = "Archivos Markdown (*.md;*.markdown)|*.md;*.markdown|Archivos de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                FilterIndex = 1
            };

            if (initialDirectory != null && Directory.Exists(initialDirectory))
                dialog.InitialDirectory = initialDirectory;

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public static string? SaveFileAs(string? suggestedFileName = null)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Guardar archivo Markdown",
                Filter = "Archivos Markdown (*.md)|*.md|Archivos de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                FilterIndex = 1,
                DefaultExt = ".md"
            };

            if (!string.IsNullOrEmpty(suggestedFileName))
                dialog.FileName = suggestedFileName;

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public static string? ReadFile(string path)
        {
            try
            {
                return File.ReadAllText(path, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al leer el archivo:\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public static bool WriteFile(string path, string content)
        {
            try
            {
                File.WriteAllText(path, content, System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar el archivo:\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static List<string> LoadRecentFiles()
        {
            var result = new List<string>();
            try
            {
                string settingsPath = GetSettingsPath();
                if (File.Exists(settingsPath))
                {
                    var lines = File.ReadAllLines(settingsPath);
                    foreach (var line in lines)
                    {
                        if (File.Exists(line.Trim()))
                            result.Add(line.Trim());
                    }
                }
            }
            catch { }
            return result;
        }

        public static void AddRecentFile(string filePath, List<string> recentFiles)
        {
            recentFiles.Remove(filePath);
            recentFiles.Insert(0, filePath);
            while (recentFiles.Count > MaxRecentFiles)
                recentFiles.RemoveAt(recentFiles.Count - 1);
            SaveRecentFiles(recentFiles);
        }

        private static void SaveRecentFiles(List<string> recentFiles)
        {
            try
            {
                File.WriteAllLines(GetSettingsPath(), recentFiles);
            }
            catch { }
        }

        private static string GetSettingsPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "PlannamTypora");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "recent.txt");
        }

        public static string? ChooseFolder()
        {
            // Use a simple custom folder browser since WPF doesn't have a built-in one
            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar carpeta (elige cualquier archivo en la carpeta)",
                CheckFileExists = false,
                FileName = "Seleccionar carpeta",
                Filter = "Carpeta|*.none",
                ValidateNames = false
            };
            if (dialog.ShowDialog() == true)
            {
                return Path.GetDirectoryName(dialog.FileName);
            }
            return null;
        }
    }
}
