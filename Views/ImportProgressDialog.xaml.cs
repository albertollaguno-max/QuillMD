using System.IO;
using System.Threading;
using System.Windows;

namespace QuillMD.Views
{
    public partial class ImportProgressDialog : Window
    {
        private readonly CancellationTokenSource _cts;
        private bool _autoClosing;

        public CancellationToken Token => _cts.Token;

        public ImportProgressDialog(string filePath, CancellationTokenSource cts)
        {
            InitializeComponent();
            _cts = cts;
            StatusText.Text = $"Convirtiendo {Path.GetFileName(filePath)}…";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            CancelButton.IsEnabled = false;
            StatusText.Text = "Cancelando…";
        }

        public void AutoClose()
        {
            _autoClosing = true;
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_autoClosing && !_cts.IsCancellationRequested)
                _cts.Cancel();
            base.OnClosing(e);
        }
    }
}
