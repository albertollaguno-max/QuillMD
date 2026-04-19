using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QuillMD.Services
{
    public static class ImportService
    {
        private static readonly HashSet<string> _supportedSet = new(
            new[]
            {
                ".pdf", ".docx", ".pptx", ".xlsx", ".xls",
                ".msg", ".epub", ".html", ".htm",
                ".csv", ".json", ".xml", ".zip"
            },
            StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<string> SupportedExtensions => _supportedSet;

        public static string OpenFileDialogFilter =>
            "Documentos importables|" +
            string.Join(";", _supportedSet.OrderBy(e => e).Select(e => "*" + e)) +
            "|Todos los archivos (*.*)|*.*";

        public static bool IsImportable(string path) =>
            !string.IsNullOrEmpty(path) &&
            _supportedSet.Contains(Path.GetExtension(path));

        public static string SuggestedMarkdownPath(string originalPath) =>
            Path.ChangeExtension(originalPath, ".md");
    }
}
