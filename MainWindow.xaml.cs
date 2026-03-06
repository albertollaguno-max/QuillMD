using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Search;
using PlannamTypora.Models;
using PlannamTypora.Services;
using PlannamTypora.ViewModels;
using Markdig;
using System.Xml;

namespace PlannamTypora
{
    public partial class MainWindow : Window
    {
        // ─────────────────── State ───────────────────
        public ObservableCollection<TabModel> Tabs { get; } = new();
        public ObservableCollection<string> RecentFiles { get; } = new();
        public ObservableCollection<FileTreeItem> FileTreeItems { get; } = new();

        private TabModel? _activeTab;
        private string _currentViewMode = "Split"; // Editor, Preview, Split
        private string _openFolderPath = string.Empty;

        private System.Windows.Threading.DispatcherTimer _previewTimer;
        private bool _suppressTextChanged = false;

        // ─────────────────── Commands ───────────────────
        public ICommand NewTabCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SaveAsCommand { get; }
        public ICommand CloseTabCommand { get; }
        public ICommand ExportHtmlCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand ToggleFindCommand { get; }
        public ICommand ToggleReplaceCommand { get; }
        public ICommand CloseFindBarCommand { get; }
        public ICommand FormatBoldCommand { get; }
        public ICommand FormatItalicCommand { get; }
        public ICommand FormatStrikethroughCommand { get; }
        public ICommand FormatCodeCommand { get; }
        public ICommand FormatCodeBlockCommand { get; }
        public ICommand FormatLinkCommand { get; }
        public ICommand FormatImageCommand { get; }
        public ICommand FormatBlockquoteCommand { get; }
        public ICommand FormatHrCommand { get; }
        public IRelayCommand<string> FormatHeadingCommand { get; }
        public IRelayCommand<string> FormatListCommand { get; }
        public IRelayCommand<string> SetViewModeCommand { get; }
        public ICommand ToggleSidebarCommand { get; }
        public IRelayCommand<string> SetThemeCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand IncreaseFontCommand { get; }
        public ICommand DecreaseFontCommand { get; }
        public ICommand ResetFontCommand { get; }
        public ICommand NextTabCommand { get; }
        public ICommand PrevTabCommand { get; }
        public IRelayCommand<string> OpenRecentCommand { get; }
        public ICommand ShowAboutCommand { get; }
        public ICommand ShowMarkdownHelpCommand { get; }
        public ICommand ParagraphCommand { get; }
        public ICommand CopyAsMarkdownCommand { get; }
        public ICommand PastePlainTextCommand { get; }
        public ICommand FullscreenCommand { get; }
        public ICommand FocusModeCommand { get; }
        public ICommand InsertTableCommand { get; }

        public MainWindow()
        {
            DataContext = this;

            // Initialize commands
            NewTabCommand = new RelayCommand(() => NewTab());
            OpenFileCommand = new RelayCommand(OpenFile);
            OpenFolderCommand = new RelayCommand(OpenFolder);
            SaveCommand = new RelayCommand(Save);
            SaveAsCommand = new RelayCommand(SaveAs);
            CloseTabCommand = new RelayCommand(CloseActiveTab);
            ExportHtmlCommand = new RelayCommand(ExportHtml);
            ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
            ToggleFindCommand = new RelayCommand(() => ToggleFindBar(false));
            ToggleReplaceCommand = new RelayCommand(() => ToggleFindBar(true));
            CloseFindBarCommand = new RelayCommand(HideFindBar);
            FormatBoldCommand = new RelayCommand(() => WrapSelection("**", "**", "texto en negrita"));
            FormatItalicCommand = new RelayCommand(() => WrapSelection("*", "*", "texto en cursiva"));
            FormatStrikethroughCommand = new RelayCommand(() => WrapSelection("~~", "~~", "texto tachado"));
            FormatCodeCommand = new RelayCommand(() => WrapSelection("`", "`", "código"));
            FormatCodeBlockCommand = new RelayCommand(InsertCodeBlock);
            FormatLinkCommand = new RelayCommand(InsertLink);
            FormatImageCommand = new RelayCommand(InsertImage);
            FormatBlockquoteCommand = new RelayCommand(InsertBlockquote);
            FormatHrCommand = new RelayCommand(InsertHr);
            FormatHeadingCommand = new RelayCommand<string>(InsertHeading);
            FormatListCommand = new RelayCommand<string>(InsertList);
            SetViewModeCommand = new RelayCommand<string>(SetViewMode);
            ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
            SetThemeCommand = new RelayCommand<string>(t => SetTheme(t == "Dark"));
            ToggleThemeCommand = new RelayCommand(ToggleTheme);
            IncreaseFontCommand = new RelayCommand(() => AdjustFontSize(2));
            DecreaseFontCommand = new RelayCommand(() => AdjustFontSize(-2));
            ResetFontCommand = new RelayCommand(() => { Editor.FontSize = 14; });
            NextTabCommand = new RelayCommand(NextTab);
            PrevTabCommand = new RelayCommand(PrevTab);
            OpenRecentCommand = new RelayCommand<string>(OpenRecentFile);
            ShowAboutCommand = new RelayCommand(ShowAbout);
            ShowMarkdownHelpCommand = new RelayCommand(ShowMarkdownHelp);
            ParagraphCommand = new RelayCommand(ConvertToParagraph);
            CopyAsMarkdownCommand = new RelayCommand(CopyAsMarkdown);
            PastePlainTextCommand = new RelayCommand(PastePlainText);
            FullscreenCommand = new RelayCommand(ToggleFullscreen);
            FocusModeCommand = new RelayCommand(ToggleFocusMode);
            InsertTableCommand = new RelayCommand(InsertTable);

            InitializeComponent();

            // Preview debounce timer
            _previewTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _previewTimer.Tick += (s, e) =>
            {
                _previewTimer.Stop();
                RefreshPreview();
            };

            App.Log("LoadMarkdownHighlighting...");
            LoadMarkdownHighlighting();

            App.Log("Loading recent files...");
            var recent = FileService.LoadRecentFiles();
            foreach (var f in recent) RecentFiles.Add(f);
            App.Log($"Loaded {recent.Count} recent files");

            App.Log("Opening blank tab...");
            NewTab();
            App.Log("Blank tab opened");

            // Editor cursor tracking
            Editor.TextArea.Caret.PositionChanged += (s, e) => UpdateStatusBar();

            // Context menu: show/hide table options based on cursor position
            Editor.ContextMenu.Opened += (s, e) =>
            {
                if (ContextTableMenu != null)
                    ContextTableMenu.Visibility = TableEditHelper.IsInTable(Editor)
                        ? Visibility.Visible : Visibility.Collapsed;
            };

            // Table navigation: Tab/Shift+Tab in tables
            Editor.TextArea.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Tab && TableEditHelper.IsInTable(Editor))
                {
                    bool handled;
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                        handled = TableEditHelper.MoveToPrevCell(Editor);
                    else
                        handled = TableEditHelper.MoveToNextCell(Editor);
                    if (handled) e.Handled = true;
                }
                else if (e.Key == Key.Enter && TableEditHelper.IsInTable(Editor))
                {
                    // Enter at end of last row adds new row
                    var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
                    if (Editor.CaretOffset >= line.EndOffset - 1)
                    {
                        // Check if this is the last row of the table
                        int nextLineNum = line.LineNumber + 1;
                        bool isLastRow = nextLineNum > Editor.Document.LineCount;
                        if (!isLastRow)
                        {
                            var nextLine = Editor.Document.GetLineByNumber(nextLineNum);
                            string nextText = Editor.Document.GetText(nextLine.Offset, nextLine.Length).Trim();
                            isLastRow = !Regex.IsMatch(nextText, @"^\|.*\|$");
                        }
                        if (isLastRow)
                        {
                            TableEditHelper.AddRow(Editor);
                            e.Handled = true;
                        }
                    }
                }
            };

            // Load saved settings
            LoadSettings();

            Closing += MainWindow_Closing;

            // Initialize WebView2 for WYSIWYG mode
            InitializeWebView();
        }

        private bool _webViewReady = false;

        private async void InitializeWebView()
        {
            try
            {
                await PreviewWebView.EnsureCoreWebView2Async();
                _webViewReady = true;

                // Suppress default browser context menu (our HTML/JS menu handles it)
                PreviewWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                // Intercept link navigation – open in default browser instead
                PreviewWebView.CoreWebView2.NavigationStarting += (s, args) =>
                {
                    if (args.Uri != null && (args.Uri.StartsWith("http://") || args.Uri.StartsWith("https://")))
                    {
                        args.Cancel = true;
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(args.Uri) { UseShellExecute = true });
                    }
                };

                PreviewWebView.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    try
                    {
                        var json = System.Text.Json.JsonDocument.Parse(e.WebMessageAsJson);
                        string type = json.RootElement.GetProperty("type").GetString() ?? "";

                        if (type == "contentChanged")
                        {
                            string html = json.RootElement.GetProperty("html").GetString() ?? "";
                            string markdown = HtmlToMarkdown.Convert(html);

                            Dispatcher.Invoke(() =>
                            {
                                _suppressTextChanged = true;
                                Editor.Text = markdown;
                                _suppressTextChanged = false;

                                if (_activeTab != null)
                                {
                                    _activeTab.Document.Content = markdown;
                                    _activeTab.IsDirty = true;
                                    _activeTab.TabTitle = _activeTab.Document.FileName + " ●";
                                    UpdateTitle();
                                }
                            });
                        }
                        else if (type == "copyAsMarkdown")
                        {
                            string html = json.RootElement.GetProperty("html").GetString() ?? "";
                            string markdown = HtmlToMarkdown.Convert(html);
                            Dispatcher.Invoke(() => Clipboard.SetText(markdown));
                        }
                        else if (type == "copyToClipboard")
                        {
                            string text = json.RootElement.GetProperty("text").GetString() ?? "";
                            Dispatcher.Invoke(() => Clipboard.SetText(text));
                        }
                        else if (type == "openLink")
                        {
                            string url = json.RootElement.GetProperty("url").GetString() ?? "";
                            if (!string.IsNullOrEmpty(url) && (url.StartsWith("http://") || url.StartsWith("https://")))
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                            }
                        }
                        else if (type == "requestPaste")
                        {
                            Dispatcher.Invoke(async () =>
                            {
                                string text = Clipboard.GetText();
                                if (!string.IsNullOrEmpty(text) && _webViewReady && PreviewWebView.CoreWebView2 != null)
                                {
                                    string escaped = System.Text.Json.JsonSerializer.Serialize(text);
                                    await PreviewWebView.CoreWebView2.ExecuteScriptAsync(
                                        $"document.execCommand('insertText', false, {escaped}); notifyChange();");
                                }
                            });
                        }
                    }
                    catch (Exception ex) { App.LogFatal("WebMessageReceived", ex); }
                };

                App.Log("WebView2 initialized OK");

                // If WYSIWYG was the saved view mode, apply it now that WebView2 is ready
                if (_currentViewMode == "WYSIWYG")
                    SetViewMode("WYSIWYG");
            }
            catch (Exception ex)
            {
                App.LogFatal("InitializeWebView", ex);
            }
        }

        // ─────────────────── Highlighting ───────────────────
        private void LoadMarkdownHighlighting()
        {
            try
            {
                App.Log("  Getting manifest resource stream...");
                using var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("PlannamTypora.Services.MarkdownHighlighting.xshd");
                App.Log($"  Stream is {(stream == null ? "NULL" : "OK")}");
                if (stream != null)
                {
                    using var reader = new XmlTextReader(stream);
                    var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    HighlightingManager.Instance.RegisterHighlighting("Markdown", new[] { ".md", ".markdown" }, highlighting);
                    Editor.SyntaxHighlighting = highlighting;
                    App.Log("  Highlighting loaded OK");
                }
            }
            catch (Exception ex) 
            { 
                App.LogFatal("LoadMarkdownHighlighting", ex);
            }
        }

        // ─────────────────── Tab Management ───────────────────
        private void NewTab(string? filePath = null, string? content = null)
        {
            App.Log($"NewTab: filePath={filePath ?? "(new)"}"
);
            var doc = new MarkdownDocument
            {
                FilePath = filePath ?? string.Empty,
                Content = content ?? string.Empty
            };

            var tab = new TabModel
            {
                Document = doc,
                TabTitle = doc.FileName,
                IsActive = false
            };

            Tabs.Add(tab);
            ActivateTab(tab);
        }

        private void ActivateTab(TabModel tab)
        {
            foreach (var t in Tabs) t.IsActive = false;
            tab.IsActive = true;
            _activeTab = tab;

            _suppressTextChanged = true;
            Editor.Text = tab.Document.Content;
            _suppressTextChanged = false;

            UpdateTitle();
            UpdateStatusBar();
            RefreshPreview();

            // Update tab highlights via refresh
            var items = TabStrip.FindName("") as Panel;
        }

        private void CloseActiveTab()
        {
            if (_activeTab == null) return;
            CloseTab(_activeTab);
        }

        private void CloseTab(TabModel tab)
        {
            if (tab.IsDirty)
            {
                var result = MessageBox.Show(
                    $"¿Guardar los cambios en '{tab.TabTitle}'?",
                    "Cambios sin guardar",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel) return;
                if (result == MessageBoxResult.Yes)
                {
                    ActivateTab(tab);
                    Save();
                }
            }

            int index = Tabs.IndexOf(tab);
            Tabs.Remove(tab);

            if (Tabs.Count == 0)
            {
                NewTab();
            }
            else
            {
                int nextIndex = Math.Min(index, Tabs.Count - 1);
                ActivateTab(Tabs[nextIndex]);
            }
        }

        private void NextTab()
        {
            if (Tabs.Count < 2 || _activeTab == null) return;
            int idx = Tabs.IndexOf(_activeTab);
            ActivateTab(Tabs[(idx + 1) % Tabs.Count]);
        }

        private void PrevTab()
        {
            if (Tabs.Count < 2 || _activeTab == null) return;
            int idx = Tabs.IndexOf(_activeTab);
            ActivateTab(Tabs[(idx - 1 + Tabs.Count) % Tabs.Count]);
        }

        // ─────────────────── File Operations ───────────────────
        private void OpenFile()
        {
            App.Log("OpenFile: showing dialog");
            string? path = FileService.OpenFile(_openFolderPath);
            if (path == null) { App.Log("OpenFile: cancelled"); return; }

            App.Log($"OpenFile: selected={path}");
            // Check if already open
            var existing = Tabs.FirstOrDefault(t => t.Document.FilePath == path);
            if (existing != null) { ActivateTab(existing); return; }

            App.Log("OpenFile: reading file...");
            string? content = FileService.ReadFile(path);
            if (content == null) { App.Log("OpenFile: ReadFile returned null"); return; }

            App.Log($"OpenFile: content length={content.Length} chars, calling NewTab");
            NewTab(path, content);
            AddToRecent(path);
            App.Log("OpenFile: done");
        }

        private void OpenRecentFile(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!File.Exists(path))
            {
                MessageBox.Show($"El archivo ya no existe:\n{path}", "Archivo no encontrado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                RecentFiles.Remove(path);
                return;
            }
            var existing = Tabs.FirstOrDefault(t => t.Document.FilePath == path);
            if (existing != null) { ActivateTab(existing); return; }
            string? content = FileService.ReadFile(path);
            if (content != null)
            {
                NewTab(path, content);
                AddToRecent(path);
            }
        }

        private void OpenFolder()
        {
            string? folder = FileService.ChooseFolder();
            if (folder == null) return;
            _openFolderPath = folder;
            LoadFileTree(folder);
        }

        private void Save()
        {
            if (_activeTab == null) return;

            if (_activeTab.Document.IsNewFile)
            {
                SaveAs();
                return;
            }

            if (FileService.WriteFile(_activeTab.Document.FilePath, Editor.Text))
            {
                _activeTab.Document.Content = Editor.Text;
                _activeTab.IsDirty = false;
                _activeTab.TabTitle = _activeTab.Document.FileName;
                UpdateTitle();
            }
        }

        private void SaveAs()
        {
            if (_activeTab == null) return;
            string? path = FileService.SaveFileAs(_activeTab.Document.IsNewFile ? null : _activeTab.Document.FilePath);
            if (path == null) return;

            if (FileService.WriteFile(path, Editor.Text))
            {
                _activeTab.Document.FilePath = path;
                _activeTab.Document.Content = Editor.Text;
                _activeTab.IsDirty = false;
                _activeTab.TabTitle = _activeTab.Document.FileName;
                AddToRecent(path);
                UpdateTitle();
            }
        }

        private void ExportHtml()
        {
            if (_activeTab == null) return;
            var save = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exportar a HTML",
                Filter = "Archivo HTML (*.html)|*.html",
                FileName = _activeTab.Document.IsNewFile ? "documento" : _activeTab.Document.FileName
            };
            if (save.ShowDialog() != true) return;

            // Use WebPreviewBridge for consistent, styled HTML export
            string html = WebPreviewBridge.GenerateHtml(Editor.Text, App.IsDarkTheme, editable: false);
            File.WriteAllText(save.FileName, html, Encoding.UTF8);
            MessageBox.Show($"Exportado correctamente:\n{save.FileName}", "Exportar a HTML",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab == null) return;

            var save = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exportar a PDF",
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = _activeTab.Document.IsNewFile
                    ? "documento.pdf"
                    : System.IO.Path.ChangeExtension(_activeTab.Document.FileName, ".pdf")
            };
            if (save.ShowDialog() != true) return;

            try
            {
                // Ensure WebView2 is ready
                if (!_webViewReady || PreviewWebView.CoreWebView2 == null)
                {
                    await PreviewWebView.EnsureCoreWebView2Async();
                    _webViewReady = true;
                }

                // Render clean HTML (light theme, non-editable) for PDF
                string html = WebPreviewBridge.GenerateHtml(Editor.Text, isDark: false, editable: false);

                var coreWebView = PreviewWebView.CoreWebView2!;

                // Navigate and wait for completion
                var tcs = new TaskCompletionSource<bool>();
                void OnNav(object? s, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
                {
                    coreWebView.NavigationCompleted -= OnNav;
                    tcs.TrySetResult(args.IsSuccess);
                }
                coreWebView.NavigationCompleted += OnNav;
                coreWebView.NavigateToString(html);
                await tcs.Task;

                // Let rendering settle
                await Task.Delay(500);

                // Print to PDF
                await coreWebView.PrintToPdfAsync(save.FileName);

                // Restore WYSIWYG if it was active
                if (IsWysiwygMode) RefreshWysiwygPreview();

                MessageBox.Show($"PDF exportado correctamente:\n{save.FileName}", "Exportar PDF",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar PDF:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddToRecent(string path)
        {
            var list = RecentFiles.ToList();
            FileService.AddRecentFile(path, list);
            RecentFiles.Clear();
            foreach (var f in list) RecentFiles.Add(f);
        }

        // ─────────────────── File Tree ───────────────────
        private void LoadFileTree(string folder)
        {
            FileTreeItems.Clear();
            try
            {
                var root = BuildTreeItem(folder, true);
                if (root != null)
                    FileTreeItems.Add(root);
            }
            catch { }
        }

        private FileTreeItem? BuildTreeItem(string path, bool isRoot = false)
        {
            if (Directory.Exists(path))
            {
                var item = new FileTreeItem
                {
                    Name = isRoot ? Path.GetFileName(path) + " (carpeta)" : Path.GetFileName(path),
                    FullPath = path,
                    Icon = "📁",
                    IsDirectory = true
                };

                try
                {
                    foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
                    {
                        var child = BuildTreeItem(dir);
                        if (child != null) item.Children.Add(child);
                    }
                    foreach (var file in Directory.GetFiles(path, "*.md").Concat(Directory.GetFiles(path, "*.markdown")).OrderBy(f => f))
                    {
                        item.Children.Add(new FileTreeItem
                        {
                            Name = Path.GetFileName(file),
                            FullPath = file,
                            Icon = "📝",
                            IsDirectory = false
                        });
                    }
                }
                catch { }
                return item;
            }
            return null;
        }

        private void FileTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FileTree.SelectedItem is FileTreeItem item && !item.IsDirectory)
            {
                var existing = Tabs.FirstOrDefault(t => t.Document.FilePath == item.FullPath);
                if (existing != null) { ActivateTab(existing); return; }
                string? content = FileService.ReadFile(item.FullPath);
                if (content != null)
                {
                    NewTab(item.FullPath, content);
                    AddToRecent(item.FullPath);
                }
            }
        }

        // ─────────────────── Editor Events ───────────────────
        private void Editor_TextChanged(object? sender, EventArgs e)
        {
            if (_suppressTextChanged) return;
            if (_activeTab == null) return;

            _activeTab.Document.Content = Editor.Text;
            _activeTab.IsDirty = true;
            _activeTab.TabTitle = _activeTab.Document.FileName + " ●";
            UpdateStatusBar();

            // Debounced preview refresh
            _previewTimer.Stop();
            _previewTimer.Start();
        }

        private void RefreshPreview()
        {
            RefreshOutline();
            if (_currentViewMode == "Editor") return;
            if (_currentViewMode == "WYSIWYG")
            {
                RefreshWysiwygPreview();
                return;
            }
            try
            {
                App.Log("RefreshPreview: calling ToFlowDocument...");
                var flowDoc = MarkdownConverter.ToFlowDocument(Editor.Text, App.IsDarkTheme);
                App.Log("RefreshPreview: assigning document...");
                PreviewViewer.Document = flowDoc;
                App.Log("RefreshPreview: done");
            }
            catch (Exception ex)
            {
                App.LogFatal("RefreshPreview", ex);
            }
        }

        private void RefreshWysiwygPreview()
        {
            if (!_webViewReady) return;
            try
            {
                string html = WebPreviewBridge.GenerateHtml(Editor.Text, App.IsDarkTheme, editable: true);
                PreviewWebView.NavigateToString(html);
            }
            catch (Exception ex)
            {
                App.LogFatal("RefreshWysiwygPreview", ex);
            }
        }

        // ─────────────────── WYSIWYG Format Bridge ───────────────────
        private bool IsWysiwygMode => _currentViewMode == "WYSIWYG" && _webViewReady;

        private async void ExecuteWysiwygScript(string jsFunction)
        {
            if (!_webViewReady || PreviewWebView.CoreWebView2 == null) return;
            try
            {
                await PreviewWebView.CoreWebView2.ExecuteScriptAsync(jsFunction);
            }
            catch (Exception ex) { App.LogFatal("ExecuteWysiwygScript", ex); }
        }

        private void RefreshOutline()
        {
            try
            {
                OutlineTree.Items.Clear();
                string text = Editor.Text;
                var lines = text.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].TrimEnd('\r');
                    var match = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
                    if (!match.Success) continue;

                    int level = match.Groups[1].Value.Length;
                    string title = match.Groups[2].Value.Trim();
                    int lineNumber = i + 1;

                    var item = new TreeViewItem
                    {
                        Header = title,
                        Tag = lineNumber,
                        Padding = new Thickness(level * 12, 2, 4, 2),
                        FontSize = level <= 2 ? 13 : 12,
                        FontWeight = level == 1 ? FontWeights.SemiBold : FontWeights.Normal,
                        Foreground = (SolidColorBrush)FindResource("ForegroundBrush")
                    };

                    item.MouseDoubleClick += (s, e) =>
                    {
                        if (s is TreeViewItem ti && ti.Tag is int ln)
                        {
                            var docLine = Editor.Document.GetLineByNumber(ln);
                            Editor.ScrollToLine(ln);
                            SafeSetCaret(docLine.Offset);
                            Editor.Focus();
                            e.Handled = true;
                        }
                    };

                    OutlineTree.Items.Add(item);
                }
            }
            catch { }
        }

        // ─────────────────── View Mode ───────────────────
        private void SetViewMode(string? mode)
        {
            _currentViewMode = mode ?? "Split";

            // Hide WebView by default, show only in WYSIWYG
            PreviewWebView.Visibility = Visibility.Collapsed;

            switch (_currentViewMode)
            {
                case "Editor":
                    EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                    SplitterColumn.Width = new GridLength(0);
                    PreviewColumn.Width = new GridLength(0);
                    EditorPreviewSplitter.Visibility = Visibility.Collapsed;
                    PreviewViewer.Visibility = Visibility.Collapsed;
                    Editor.Visibility = Visibility.Visible;
                    Editor.Focus();
                    break;
                case "Preview":
                    EditorColumn.Width = new GridLength(0);
                    SplitterColumn.Width = new GridLength(0);
                    PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                    EditorPreviewSplitter.Visibility = Visibility.Collapsed;
                    Editor.Visibility = Visibility.Collapsed;
                    PreviewViewer.Visibility = Visibility.Visible;
                    RefreshPreview();
                    break;
                case "WYSIWYG":
                    EditorColumn.Width = new GridLength(0);
                    SplitterColumn.Width = new GridLength(0);
                    PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                    EditorPreviewSplitter.Visibility = Visibility.Collapsed;
                    Editor.Visibility = Visibility.Collapsed;
                    PreviewViewer.Visibility = Visibility.Collapsed;
                    PreviewWebView.Visibility = Visibility.Visible;
                    RefreshWysiwygPreview();
                    break;
                default: // Split
                    EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                    SplitterColumn.Width = new GridLength(4);
                    PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                    EditorPreviewSplitter.Visibility = Visibility.Visible;
                    Editor.Visibility = Visibility.Visible;
                    PreviewViewer.Visibility = Visibility.Visible;
                    Editor.Focus();
                    RefreshPreview();
                    break;
            }

            StatusViewMode.Text = _currentViewMode switch
            {
                "Editor" => "✏️ Editor",
                "Preview" => "👁 Preview",
                "WYSIWYG" => "W WYSIWYG",
                _ => "⬜ Split"
            };
        }

        private void ToggleSidebar()
        {
            if (SidebarBorder.Visibility == Visibility.Visible)
            {
                _savedSidebarWidth = SidebarColumn.Width.Value;
                SidebarBorder.Visibility = Visibility.Collapsed;
                SidebarSplitter.Visibility = Visibility.Collapsed;
                SidebarColumn.Width = new GridLength(0);
            }
            else
            {
                SidebarBorder.Visibility = Visibility.Visible;
                SidebarSplitter.Visibility = Visibility.Visible;
                SidebarColumn.Width = new GridLength(_savedSidebarWidth > 0 ? _savedSidebarWidth : 220);
            }
        }

        // ─────────────────── Theme ───────────────────
        private void SetTheme(bool dark)
        {
            if (App.IsDarkTheme == dark) return;
            App.ToggleTheme();
            UpdateThemeUI();
            RefreshPreview();
        }

        private void ToggleTheme()
        {
            App.ToggleTheme();
            UpdateThemeUI();
            RefreshPreview();
        }

        private void UpdateThemeUI()
        {
            StatusTheme.Text = App.IsDarkTheme ? "🌙 Oscuro" : "☀️ Claro";
            ThemeButton.Content = App.IsDarkTheme ? "🌙" : "☀️";

            // Update AvalonEdit colors
            Editor.Background = (SolidColorBrush)FindResource("EditorBackgroundBrush");
            Editor.Foreground = (SolidColorBrush)FindResource("EditorForegroundBrush");
        }

        // ─────────────────── Font size ───────────────────
        private void AdjustFontSize(double delta)
        {
            double newSize = Editor.FontSize + delta;
            if (newSize >= 8 && newSize <= 36)
                Editor.FontSize = newSize;
        }

        // ─────────────────── Markdown Formatting ───────────────────
        private void WrapSelection(string prefix, string suffix, string placeholder)
        {
            if (IsWysiwygMode)
            {
                // Route to WebView2 JS formatting
                string jsFunc = (prefix, suffix) switch
                {
                    ("**", "**") => "formatBold()",
                    ("*", "*") => "formatItalic()",
                    ("~~", "~~") => "formatStrike()",
                    ("`", "`") => "formatCode()",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(jsFunc)) ExecuteWysiwygScript(jsFunc);
                return;
            }

            var selection = Editor.SelectedText;
            string text = string.IsNullOrEmpty(selection) ? placeholder : selection;
            int start = Editor.SelectionStart;
            Editor.Document.Replace(start, Editor.SelectionLength, prefix + text + suffix);
            // Select the inner text
            Editor.Select(start + prefix.Length, text.Length);
            Editor.Focus();
        }

        private void ConvertToParagraph()
        {
            if (IsWysiwygMode) { ExecuteWysiwygScript("formatParagraph()"); return; }
            var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
            string currentLine = Editor.Document.GetText(line.Offset, line.Length);

            // Strip heading, list, blockquote prefixes
            string newLine = Regex.Replace(currentLine, @"^(#{1,6}\s+|>\s*|\d+\.\s+|- \s*|\* \s*)", "");

            if (newLine != currentLine)
            {
                Editor.Document.Replace(line.Offset, line.Length, newLine);
                SafeSetCaret(line.Offset + newLine.Length);
            }
        }

        private void InsertHeading(string? levelStr)
        {
            if (!int.TryParse(levelStr, out int level)) return;
            if (IsWysiwygMode) { ExecuteWysiwygScript($"formatHeading({level})"); return; }
            string prefix = new string('#', level) + " ";

            var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
            string currentLine = Editor.Document.GetText(line.Offset, line.Length);

            // Toggle: if already has heading, remove or change it
            var match = Regex.Match(currentLine, @"^(#{1,6})\s");
            string newLine;
            if (match.Success)
            {
                if (match.Groups[1].Length == level)
                    newLine = currentLine.Substring(match.Length); // Remove heading
                else
                    newLine = prefix + currentLine.Substring(match.Length);
            }
            else
            {
                newLine = prefix + currentLine;
            }

            Editor.Document.Replace(line.Offset, line.Length, newLine);
            SafeSetCaret(line.Offset + newLine.Length);
            Editor.Focus();
        }

        private void PrefixSelectedLines(string? prefix, bool numbered = false)
        {
            string selected = Editor.SelectedText;
            int selStart = Editor.SelectionStart;
            int selLen = Editor.SelectionLength;

            if (selLen > 0)
            {
                // Multi-line: prefix each line in the selection
                var lines = selected.Split('\n');
                var sb = new System.Text.StringBuilder();
                int num = 1;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (i > 0) sb.Append('\n');
                    string p = numbered ? $"{num++}. " : prefix!;
                    sb.Append(p);
                    sb.Append(lines[i]);
                }
                string result = sb.ToString();
                Editor.Document.Replace(selStart, selLen, result);
                Editor.Select(selStart, result.Length);
            }
            else
            {
                // No selection: prefix current line
                string p = numbered ? "1. " : prefix!;
                var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
                Editor.Document.Insert(line.Offset, p);
                SafeSetCaret(Editor.CaretOffset + p.Length);
            }
        }

        private void InsertList(string? type)
        {
            if (IsWysiwygMode)
            {
                ExecuteWysiwygScript(type == "ordered" ? "formatOrderedList()" : "formatUnorderedList()");
                return;
            }
            if (type == "ordered")
                PrefixSelectedLines(null, numbered: true);
            else
                PrefixSelectedLines("- ");
        }

        private void InsertBlockquote()
        {
            if (IsWysiwygMode) { ExecuteWysiwygScript("formatBlockquote()"); return; }
            PrefixSelectedLines("> ");
        }

        private void InsertLink()
        {
            if (IsWysiwygMode) { ExecuteWysiwygScript("insertLink()"); return; }
            string selected = Editor.SelectedText;
            string insertion;
            int selectStart, selectLen;

            if (!string.IsNullOrEmpty(selected))
            {
                insertion = $"[{selected}](https://)";
                selectStart = Editor.SelectionStart + selected.Length + 3;
                selectLen = 8;
            }
            else
            {
                insertion = "[texto del enlace](https://)";
                selectStart = Editor.SelectionStart + 1;
                selectLen = 17;
            }

            int start = Editor.SelectionStart;
            Editor.Document.Replace(start, Editor.SelectionLength, insertion);
            Editor.Select(selectStart, selectLen);
            Editor.Focus();
        }

        private void InsertImage()
        {
            if (IsWysiwygMode) { ExecuteWysiwygScript("insertImage()"); return; }
            string insertion = "![texto alternativo](ruta/a/imagen.png)";
            int start = Editor.SelectionStart;
            Editor.Document.Replace(start, Editor.SelectionLength, insertion);
            Editor.Select(start + 2, 18);
            Editor.Focus();
        }

        private void InsertCodeBlock()
        {
            if (IsWysiwygMode) { ExecuteWysiwygScript("formatCodeBlock()"); return; }
            string selected = Editor.SelectedText;
            int start = Editor.SelectionStart;

            // Ensure opening ``` is at the start of a line
            string prefix = "";
            if (start > 0 && Editor.Document.GetCharAt(start - 1) != '\n')
                prefix = "\n";

            // Ensure closing ``` is followed by a newline
            int end = start + Editor.SelectionLength;
            string suffix = "";
            if (end < Editor.Document.TextLength && Editor.Document.GetCharAt(end) != '\n')
                suffix = "\n";

            string code = string.IsNullOrEmpty(selected) ? "código aquí" : selected;
            string insertion = $"{prefix}```\n{code}\n```{suffix}";

            Editor.Document.Replace(start, Editor.SelectionLength, insertion);
            // Place caret inside the code block
            int codeStart = start + prefix.Length + 4; // after "```\n"
            Editor.Select(codeStart, code.Length);
            Editor.Focus();
        }

        private void InsertHr()
        {
            if (IsWysiwygMode) { ExecuteWysiwygScript("formatHr()"); return; }
            var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
            int insertOffset = line.EndOffset;
            string insertion = "\n\n---\n\n";
            Editor.Document.Insert(insertOffset, insertion);
            SafeSetCaret(insertOffset + insertion.Length);
            Editor.Focus();
        }

        // Clamps offset to valid document range before setting caret
        private void SafeSetCaret(int offset)
        {
            int clamped = Math.Max(0, Math.Min(offset, Editor.Document.TextLength));
            Editor.CaretOffset = clamped;
        }

        // ─────────────────── Insert: Table, Footnote, Math, TOC, TaskList ───────────────────
        private void InsertTable()
        {
            if (IsWysiwygMode) { ExecuteWysiwygScript("insertTable(3, 3)"); return; }
            int start = Editor.CaretOffset;
            string prefix = "";
            if (start > 0 && Editor.Document.GetCharAt(start - 1) != '\n')
                prefix = "\n";
            string table = TableEditHelper.GenerateTable(2, 3);
            string insertion = $"{prefix}{table}";
            Editor.Document.Insert(start, insertion);
            SafeSetCaret(start + prefix.Length + 2); // inside first header cell
            Editor.Focus();
        }

        private void InsertFootnote()
        {
            int pos = Editor.CaretOffset;
            // Find next available footnote number
            string text = Editor.Text;
            int num = 1;
            while (text.Contains($"[^{num}]")) num++;
            string marker = $"[^{num}]";
            string definition = $"\n\n[^{num}]: Texto de la nota al pie";

            Editor.Document.Insert(pos, marker);
            Editor.Document.Insert(Editor.Document.TextLength, definition);
            SafeSetCaret(pos + marker.Length);
            Editor.Focus();
        }

        private void InsertMathBlock()
        {
            int start = Editor.CaretOffset;
            string prefix = "";
            if (start > 0 && Editor.Document.GetCharAt(start - 1) != '\n')
                prefix = "\n";
            string insertion = $"{prefix}$$\nE = mc^2\n$$\n";
            Editor.Document.Insert(start, insertion);
            SafeSetCaret(start + prefix.Length + 3);
            Editor.Focus();
        }

        private void InsertTOC()
        {
            int pos = Editor.CaretOffset;
            Editor.Document.Insert(pos, "[TOC]\n\n");
            SafeSetCaret(pos + 6);
            Editor.Focus();
        }

        private void InsertTaskList()
        {
            if (IsWysiwygMode) { ExecuteWysiwygScript("formatTaskList()"); return; }
            PrefixSelectedLines("- [ ] ");
        }

        // ─────────────────── Clipboard Advanced ───────────────────
        private void CopyAsMarkdown()
        {
            string selected = Editor.SelectedText;
            if (!string.IsNullOrEmpty(selected))
                Clipboard.SetText(selected);
        }

        private void CopyAsHtml()
        {
            string selected = Editor.SelectedText;
            if (string.IsNullOrEmpty(selected)) return;
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            string html = Markdig.Markdown.ToHtml(selected, pipeline);
            Clipboard.SetText(html);
        }

        private void PastePlainText()
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText(TextDataFormat.UnicodeText);
                int start = Editor.SelectionStart;
                Editor.Document.Replace(start, Editor.SelectionLength, text);
                SafeSetCaret(start + text.Length);
            }
            Editor.Focus();
        }

        // ─────────────────── Visualization: Fullscreen, Focus, Typewriter ───────────────────
        private WindowState _previousWindowState;
        private WindowStyle _previousWindowStyle;
        private bool _isFullscreen = false;
        private bool _isFocusMode = false;
        private bool _isTypewriterMode = false;
        private double _savedSidebarWidth = 220;

        private void ToggleFullscreen()
        {
            _isFullscreen = !_isFullscreen;
            if (_isFullscreen)
            {
                _previousWindowState = WindowState;
                _previousWindowStyle = WindowStyle;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowStyle = _previousWindowStyle;
                WindowState = _previousWindowState;
            }
        }

        private void ToggleFocusMode()
        {
            _isFocusMode = !_isFocusMode;
            var vis = _isFocusMode ? Visibility.Collapsed : Visibility.Visible;
            MainMenu.Visibility = vis;
            MainToolbar.Visibility = vis;
            StatusBarMain.Visibility = vis;
            if (_isFocusMode)
            {
                _savedSidebarWidth = SidebarColumn.Width.Value;
                SidebarColumn.Width = new GridLength(0);
                SidebarBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                SidebarColumn.Width = new GridLength(_savedSidebarWidth > 0 ? _savedSidebarWidth : 220);
                SidebarBorder.Visibility = Visibility.Visible;
            }
        }

        private void ToggleTypewriterMode()
        {
            _isTypewriterMode = !_isTypewriterMode;
            if (_isTypewriterMode)
                Editor.TextArea.Caret.PositionChanged += TypewriterScroll;
            else
                Editor.TextArea.Caret.PositionChanged -= TypewriterScroll;
            if (MenuTypewriter != null)
                MenuTypewriter.Header = _isTypewriterMode ? "✓ Modo máquina de escribir" : "Modo máquina de escribir";
        }

        private void TypewriterScroll(object? sender, EventArgs e)
        {
            var textView = Editor.TextArea.TextView;
            var caretLine = Editor.TextArea.Caret.Line;
            var visualTop = textView.GetVisualTopByDocumentLine(caretLine);
            double viewportCenter = Editor.TextArea.TextView.ActualHeight / 2.0;
            Editor.ScrollToVerticalOffset(visualTop - viewportCenter + textView.DefaultLineHeight / 2.0);
        }

        // ─────────────────── Find & Replace ───────────────────
        private void ToggleFindBar(bool showReplace)
        {
            bool wasVisible = FindBar.Visibility == Visibility.Visible;

            if (wasVisible && !showReplace && ReplaceLabel.Visibility == Visibility.Collapsed)
            {
                HideFindBar();
                return;
            }

            FindBar.Visibility = Visibility.Visible;
            ReplaceLabel.Visibility = showReplace ? Visibility.Visible : Visibility.Collapsed;
            ReplaceTextBox.Visibility = showReplace ? Visibility.Visible : Visibility.Collapsed;
            ReplaceButtons.Visibility = showReplace ? Visibility.Visible : Visibility.Collapsed;

            FindTextBox.Focus();
            FindTextBox.SelectAll();
        }

        private void HideFindBar()
        {
            FindBar.Visibility = Visibility.Collapsed;
            Editor.Focus();
        }

        private void FindTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift) FindPrevious();
                else FindNext();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                HideFindBar();
            }
        }

        private void FindNext_Click(object sender, RoutedEventArgs e) => FindNext();
        private void FindPrev_Click(object sender, RoutedEventArgs e) => FindPrevious();

        private void FindNext()
        {
            string searchText = FindTextBox.Text;
            if (string.IsNullOrEmpty(searchText)) return;

            PerformSearch(searchText, forward: true);
        }

        private void FindPrevious()
        {
            string searchText = FindTextBox.Text;
            if (string.IsNullOrEmpty(searchText)) return;

            PerformSearch(searchText, forward: false);
        }

        private void PerformSearch(string searchText, bool forward)
        {
            string text = Editor.Text;
            bool caseSensitive = CaseSensitiveCheck.IsChecked == true;
            bool useRegex = RegexCheck.IsChecked == true;
            StringComparison comp = caseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            int startPos = forward
                ? Editor.SelectionStart + Editor.SelectionLength
                : Editor.SelectionStart - 1;

            int foundIdx = -1;
            int foundLen = searchText.Length;

            if (useRegex)
            {
                try
                {
                    var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    var rx = new Regex(searchText, options);
                    MatchCollection matches = rx.Matches(text);
                    Match? match = forward
                        ? matches.Cast<Match>().FirstOrDefault(m => m.Index >= startPos)
                          ?? matches.Cast<Match>().FirstOrDefault()
                        : matches.Cast<Match>().LastOrDefault(m => m.Index < Math.Max(0, startPos))
                          ?? matches.Cast<Match>().LastOrDefault();

                    if (match != null && match.Success)
                    {
                        foundIdx = match.Index;
                        foundLen = match.Length;
                    }
                }
                catch { FindResultLabel.Text = "Regex inválido"; return; }
            }
            else
            {
                if (forward)
                {
                    foundIdx = text.IndexOf(searchText, Math.Min(startPos, text.Length), comp);
                    if (foundIdx < 0) foundIdx = text.IndexOf(searchText, comp); // wrap
                }
                else
                {
                    int from = Math.Max(0, Math.Min(startPos, text.Length - 1));
                    foundIdx = text.LastIndexOf(searchText, from, comp);
                    if (foundIdx < 0) foundIdx = text.LastIndexOf(searchText, comp); // wrap
                }
            }

            if (foundIdx >= 0)
            {
                Editor.Select(foundIdx, foundLen);
                Editor.ScrollToLine(Editor.Document.GetLineByOffset(foundIdx).LineNumber);
                FindResultLabel.Text = string.Empty;
            }
            else
            {
                FindResultLabel.Text = "No encontrado";
            }
        }

        private void ReplaceOne_Click(object sender, RoutedEventArgs e)
        {
            if (Editor.SelectionLength > 0)
            {
                Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, ReplaceTextBox.Text);
            }
            FindNext();
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            string search = FindTextBox.Text;
            string replace = ReplaceTextBox.Text;
            if (string.IsNullOrEmpty(search)) return;

            StringComparison comp = CaseSensitiveCheck.IsChecked == true
                ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            string newText;
            int count;

            if (RegexCheck.IsChecked == true)
            {
                try
                {
                    var options = CaseSensitiveCheck.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase;
                    var rx = new Regex(search, options);
                    count = rx.Matches(Editor.Text).Count;
                    newText = rx.Replace(Editor.Text, replace);
                }
                catch { FindResultLabel.Text = "Regex inválido"; return; }
            }
            else
            {
                var sb = new StringBuilder();
                string text = Editor.Text;
                int lastIdx = 0;
                count = 0;
                int idx;
                while ((idx = text.IndexOf(search, lastIdx, comp)) >= 0)
                {
                    sb.Append(text, lastIdx, idx - lastIdx);
                    sb.Append(replace);
                    lastIdx = idx + search.Length;
                    count++;
                }
                sb.Append(text, lastIdx, text.Length - lastIdx);
                newText = sb.ToString();
            }

            if (count > 0)
            {
                Editor.Text = newText;
                FindResultLabel.Text = $"{count} reemplazos";
            }
            else
            {
                FindResultLabel.Text = "No encontrado";
            }
        }

        // ─────────────────── Tab Click Events ───────────────────
        private void Tab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is TabModel tab)
                ActivateTab(tab);
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is TabModel tab)
                CloseTab(tab);
        }

        // ─────────────────── Status Bar & Title ───────────────────
        private void UpdateTitle()
        {
            if (_activeTab == null) return;
            string dirty = _activeTab.IsDirty ? " ●" : string.Empty;
            string file = _activeTab.Document.IsNewFile
                ? "Sin título"
                : _activeTab.Document.FilePath;
            Title = $"{_activeTab.TabTitle}{dirty} — PlannamTypora";
            StatusFilePath.Content = file;
        }

        private void UpdateStatusBar()
        {
            string text = Editor.Text;
            int words = string.IsNullOrWhiteSpace(text)
                ? 0
                : text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            int lines = string.IsNullOrEmpty(text) ? 1 : Editor.Document.LineCount;
            int chars = text.Length;
            int ln = Editor.TextArea.Caret.Line;
            int col = Editor.TextArea.Caret.Column;

            StatusWords.Text = $"{words} palabras";
            StatusLines.Text = $"{lines} líneas";
            StatusChars.Text = $"{chars} caracteres";
            StatusCursor.Text = $"Ln {ln}, Col {col}";
        }

        // ─────────────────── About / Help ───────────────────
        private void ShowAbout()
        {
            MessageBox.Show(
                "PlannamTypora v1.0\n\nEditor Markdown WYSIWYG nativo para Windows.\n\nTecnología: C# WPF + AvalonEdit + Markdig\n\n© 2026 Plannam",
                "Acerca de PlannamTypora",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ShowMarkdownHelp()
        {
            string help = """
                # Referencia rápida de Markdown

                ## Encabezados
                # H1   ## H2   ### H3

                ## Énfasis
                **negrita**   *cursiva*   ~~tachado~~

                ## Listas
                - Ítem 1
                - Ítem 2
                  - Sub-ítem

                1. Ítem 1
                2. Ítem 2

                ## Código
                `código en línea`

                ```
                bloque de código
                ```

                ## Enlaces e imágenes
                [texto](https://url.com)
                ![alt](imagen.png)

                ## Citas
                > Texto de cita

                ## Tablas
                | Col 1 | Col 2 |
                |-------|-------|
                | A     | B     |

                ## Línea horizontal
                ---
                """;

            // Open help in a new tab
            var existing = Tabs.FirstOrDefault(t => t.TabTitle == "Referencia Markdown");
            if (existing != null) { ActivateTab(existing); return; }
            NewTab(null, help);
            _activeTab!.TabTitle = "Referencia Markdown";
        }

        // ─────────────────── Window Closing ───────────────────
        // ─────────────────── Settings persistence ───────────────────
        private void LoadSettings()
        {
            var s = SettingsService.Load();

            // Theme
            if (App.IsDarkTheme != s.IsDarkTheme) App.ToggleTheme();
            UpdateThemeUI();

            // View mode
            SetViewMode(s.ViewMode);

            // Font size
            Editor.FontSize = s.FontSize > 0 ? s.FontSize : 14;

            // Sidebar
            _savedSidebarWidth = s.SidebarWidth > 0 ? s.SidebarWidth : 220;
            if (s.SidebarVisible)
            {
                SidebarBorder.Visibility = Visibility.Visible;
                SidebarSplitter.Visibility = Visibility.Visible;
                SidebarColumn.Width = new GridLength(_savedSidebarWidth);
            }
            else
            {
                SidebarBorder.Visibility = Visibility.Collapsed;
                SidebarSplitter.Visibility = Visibility.Collapsed;
                SidebarColumn.Width = new GridLength(0);
            }

            // Window size/position
            if (s.WindowMaximized)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                Width = s.WindowWidth > 0 ? s.WindowWidth : 1200;
                Height = s.WindowHeight > 0 ? s.WindowHeight : 750;
                if (!double.IsNaN(s.WindowLeft) && !double.IsNaN(s.WindowTop))
                {
                    Left = s.WindowLeft;
                    Top = s.WindowTop;
                    WindowStartupLocation = WindowStartupLocation.Manual;
                }
            }

            // Status bar
            StatusBarMain.Visibility = s.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
            if (MenuStatusBar != null)
                MenuStatusBar.Header = s.ShowStatusBar ? "✓ Mostrar barra de estado" : "Mostrar barra de estado";

            // Always on top
            Topmost = s.AlwaysOnTop;
            if (MenuAlwaysOnTop != null)
                MenuAlwaysOnTop.Header = s.AlwaysOnTop ? "✓ Siempre encima" : "Siempre encima";
        }

        private void SaveSettings()
        {
            var s = new AppSettings
            {
                IsDarkTheme = App.IsDarkTheme,
                ViewMode = _currentViewMode,
                FontSize = Editor.FontSize,
                SidebarWidth = SidebarBorder.Visibility == Visibility.Visible
                    ? SidebarColumn.Width.Value : _savedSidebarWidth,
                SidebarVisible = SidebarBorder.Visibility == Visibility.Visible,
                WindowMaximized = WindowState == WindowState.Maximized,
                WindowWidth = RestoreBounds.Width > 0 ? RestoreBounds.Width : Width,
                WindowHeight = RestoreBounds.Height > 0 ? RestoreBounds.Height : Height,
                WindowLeft = RestoreBounds.Left,
                WindowTop = RestoreBounds.Top,
                ShowStatusBar = StatusBarMain.Visibility == Visibility.Visible,
                AlwaysOnTop = Topmost
            };
            SettingsService.Save(s);
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();

            var dirtyTabs = Tabs.Where(t => t.IsDirty).ToList();
            if (!dirtyTabs.Any()) return;

            string names = string.Join(", ", dirtyTabs.Select(t => t.Document.FileName));
            var result = MessageBox.Show(
                $"Hay cambios sin guardar en: {names}\n\n¿Guardar antes de salir?",
                "Cambios sin guardar",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                foreach (var tab in dirtyTabs)
                {
                    ActivateTab(tab);
                    Save();
                }
            }
        }
    }

    // ─────────────────── Helper Models ───────────────────
    public class TabModel : System.ComponentModel.INotifyPropertyChanged
    {
        private string _tabTitle = "Sin título";
        private bool _isActive;
        private bool _isDirty;

        public MarkdownDocument Document { get; set; } = new();

        public string TabTitle
        {
            get => _tabTitle;
            set { _tabTitle = value; OnPropertyChanged(nameof(TabTitle)); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
        }

        public bool IsDirty
        {
            get => _isDirty;
            set { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public class FileTreeItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string Icon { get; set; } = "📄";
        public bool IsDirectory { get; set; }
        public ObservableCollection<FileTreeItem> Children { get; } = new();
    }

    // ─────────────────── Toolbar & Context Menu Click Handlers ───────────────────
    // Using Click events with Focusable=False on buttons keeps keyboard focus/caret
    // in the editor while the button is activated, so format operations work correctly.
    public partial class MainWindow
    {
        private void Btn_Bold(object s, RoutedEventArgs e)          { Editor.Focus(); WrapSelection("**", "**", "negrita"); }
        private void Btn_Italic(object s, RoutedEventArgs e)        { Editor.Focus(); WrapSelection("*", "*", "cursiva"); }
        private void Btn_Strike(object s, RoutedEventArgs e)        { Editor.Focus(); WrapSelection("~~", "~~", "tachado"); }
        private void Btn_Code(object s, RoutedEventArgs e)          { Editor.Focus(); WrapSelection("`", "`", "código"); }
        private void Btn_CodeBlock(object s, RoutedEventArgs e)     { Editor.Focus(); InsertCodeBlock(); }
        private void Btn_Paragraph(object s, RoutedEventArgs e)      { Editor.Focus(); ConvertToParagraph(); }
        private void Btn_H1(object s, RoutedEventArgs e)            { Editor.Focus(); InsertHeading("1"); }
        private void Btn_H2(object s, RoutedEventArgs e)            { Editor.Focus(); InsertHeading("2"); }
        private void Btn_H3(object s, RoutedEventArgs e)            { Editor.Focus(); InsertHeading("3"); }
        private void Btn_H4(object s, RoutedEventArgs e)            { Editor.Focus(); InsertHeading("4"); }
        private void Btn_H5(object s, RoutedEventArgs e)            { Editor.Focus(); InsertHeading("5"); }
        private void Btn_H6(object s, RoutedEventArgs e)            { Editor.Focus(); InsertHeading("6"); }
        private void Btn_UList(object s, RoutedEventArgs e)         { InsertList("unordered"); Editor.Focus(); }
        private void Btn_OList(object s, RoutedEventArgs e)         { InsertList("ordered"); Editor.Focus(); }
        private void Btn_TaskList(object s, RoutedEventArgs e)      { InsertTaskList(); Editor.Focus(); }
        private void Btn_Quote(object s, RoutedEventArgs e)         { InsertBlockquote(); Editor.Focus(); }
        private void Btn_Link(object s, RoutedEventArgs e)          { Editor.Focus(); InsertLink(); }
        private void Btn_Hr(object s, RoutedEventArgs e)            { Editor.Focus(); InsertHr(); }
        private void Btn_Table(object s, RoutedEventArgs e)         { Editor.Focus(); InsertTable(); }
        private void Btn_Image(object s, RoutedEventArgs e)         { Editor.Focus(); InsertImage(); }
        private void Btn_Footnote(object s, RoutedEventArgs e)      { Editor.Focus(); InsertFootnote(); }
        private void Btn_MathBlock(object s, RoutedEventArgs e)     { Editor.Focus(); InsertMathBlock(); }
        private void Btn_TOC(object s, RoutedEventArgs e)           { Editor.Focus(); InsertTOC(); }
        private void Btn_ViewEditor(object s, RoutedEventArgs e)    => SetViewMode("Editor");
        private void Btn_ViewSplit(object s, RoutedEventArgs e)     => SetViewMode("Split");
        private void Btn_ViewPreview(object s, RoutedEventArgs e)   => SetViewMode("Preview");
        private void Btn_ViewWysiwyg(object s, RoutedEventArgs e)  => SetViewMode("WYSIWYG");
        private void Btn_SelectAll(object s, RoutedEventArgs e)     { Editor.Focus(); Editor.SelectAll(); }
        private void Btn_Find(object s, RoutedEventArgs e)          { Editor.Focus(); ToggleFindBar(false); }

        // Clipboard context menu handlers
        private void CopyAsMarkdown_Click(object s, RoutedEventArgs e)  => CopyAsMarkdown();
        private void CopyAsHtml_Click(object s, RoutedEventArgs e)      => CopyAsHtml();
        private void PastePlainText_Click(object s, RoutedEventArgs e)  => PastePlainText();

        // Table context menu handlers
        private void TableAddRow_Click(object s, RoutedEventArgs e)     { Editor.Focus(); TableEditHelper.AddRow(Editor); }
        private void TableAddCol_Click(object s, RoutedEventArgs e)     { Editor.Focus(); TableEditHelper.AddColumn(Editor); }
        private void TableDeleteRow_Click(object s, RoutedEventArgs e)  { Editor.Focus(); TableEditHelper.DeleteRow(Editor); }
        private void TableDeleteCol_Click(object s, RoutedEventArgs e)  { Editor.Focus(); TableEditHelper.DeleteColumn(Editor); }

        // (Context menu for WYSIWYG is now handled entirely in HTML/JS inside the WebView2)

        // Visualization handlers
        private void Btn_ToggleSidebar(object s, RoutedEventArgs e)     => ToggleSidebar();
        private void Btn_ShowFiles(object s, RoutedEventArgs e)
        {
            if (SidebarBorder.Visibility != Visibility.Visible) ToggleSidebar();
            SidebarTabFiles.IsChecked = true;
        }
        private void Btn_ShowOutline(object s, RoutedEventArgs e)
        {
            if (SidebarBorder.Visibility != Visibility.Visible) ToggleSidebar();
            SidebarTabOutline.IsChecked = true;
        }
        private void SidebarTab_Checked(object s, RoutedEventArgs e)
        {
            // Guard against calls before UI is initialized
            if (SidebarFilesPanel == null || SidebarOutlinePanel == null) return;
            bool showFiles = SidebarTabFiles.IsChecked == true;
            SidebarFilesPanel.Visibility = showFiles ? Visibility.Visible : Visibility.Collapsed;
            SidebarOutlinePanel.Visibility = showFiles ? Visibility.Collapsed : Visibility.Visible;
        }
        private void Btn_SourceMode(object s, RoutedEventArgs e)        => SetViewMode("Editor");
        private void Btn_FocusMode(object s, RoutedEventArgs e)         => ToggleFocusMode();
        private void Btn_TypewriterMode(object s, RoutedEventArgs e)    => ToggleTypewriterMode();
        private void Btn_Fullscreen(object s, RoutedEventArgs e)        => ToggleFullscreen();
        private void Btn_AlwaysOnTop(object s, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            if (MenuAlwaysOnTop != null)
                MenuAlwaysOnTop.Header = Topmost ? "✓ Siempre encima" : "Siempre encima";
        }
        private void Btn_ZoomIn(object s, RoutedEventArgs e)
        {
            Editor.FontSize = Math.Min(Editor.FontSize + 2, 48);
            StatusFontSize.Text = $"{Editor.FontSize}px";
        }
        private void Btn_ZoomOut(object s, RoutedEventArgs e)
        {
            Editor.FontSize = Math.Max(Editor.FontSize - 2, 8);
            StatusFontSize.Text = $"{Editor.FontSize}px";
        }
        private void Btn_ZoomReset(object s, RoutedEventArgs e)
        {
            Editor.FontSize = 14;
            StatusFontSize.Text = "14px";
        }
        private void Btn_ToggleStatusBar(object s, RoutedEventArgs e)
        {
            StatusBarMain.Visibility = StatusBarMain.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
            if (MenuStatusBar != null)
                MenuStatusBar.Header = StatusBarMain.Visibility == Visibility.Visible
                    ? "✓ Mostrar barra de estado" : "Mostrar barra de estado";
        }
    }
}
