using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QuillMD.Services
{
    public static class ImportService
    {
        public static readonly IReadOnlyList<string> SupportedExtensions = new[]
        {
            ".pdf", ".docx", ".pptx", ".xlsx", ".xls",
            ".msg", ".epub", ".html", ".htm",
            ".csv", ".json", ".xml", ".zip"
        };

        public static string OpenFileDialogFilter =>
            "Documentos importables|" +
            string.Join(";", SupportedExtensions.Select(e => "*" + e)) +
            "|Todos los archivos (*.*)|*.*";

        public static bool IsImportable(string path) =>
            !string.IsNullOrEmpty(path) &&
            SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

        public static string SuggestedMarkdownPath(string originalPath) =>
            Path.ChangeExtension(originalPath, ".md");
    }
}
