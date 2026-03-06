namespace PlannamTypora.Models
{
    public class MarkdownDocument
    {
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsDirty { get; set; } = false;
        public DateTime LastModified { get; set; } = DateTime.Now;

        public bool IsNewFile => string.IsNullOrEmpty(FilePath);

        public string FileName => IsNewFile
            ? "Sin título"
            : System.IO.Path.GetFileNameWithoutExtension(FilePath);

        public string TabTitle => IsDirty ? FileName + " •" : FileName;
    }
}
