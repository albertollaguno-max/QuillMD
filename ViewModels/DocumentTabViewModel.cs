using CommunityToolkit.Mvvm.ComponentModel;
using PlannamTypora.Models;

namespace PlannamTypora.ViewModels
{
    public partial class DocumentTabViewModel : ObservableObject
    {
        [ObservableProperty]
        private MarkdownDocument _document;

        [ObservableProperty]
        private string _content = string.Empty;

        [ObservableProperty]
        private string _tabTitle = "Sin título";

        [ObservableProperty]
        private bool _isDirty = false;

        public DocumentTabViewModel(MarkdownDocument document)
        {
            _document = document;
            _content = document.Content;
            UpdateTitle();
        }

        partial void OnContentChanged(string value)
        {
            Document.Content = value;
            IsDirty = true;
            UpdateTitle();
        }

        public void MarkSaved()
        {
            IsDirty = false;
            Document.IsDirty = false;
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            string name = Document.IsNewFile
                ? "Sin título"
                : System.IO.Path.GetFileNameWithoutExtension(Document.FilePath);
            TabTitle = IsDirty ? name + " ●" : name;
        }
    }
}
