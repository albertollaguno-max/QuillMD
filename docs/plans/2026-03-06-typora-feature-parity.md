# PlannamTypora: Paridad con Typora - Plan de Implementacion

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Llevar PlannamTypora a paridad funcional con Typora, incluyendo WYSIWYG editing en el preview, menu contextual rico, y todas las funcionalidades de formato/insercion que Typora ofrece.

**Architecture:** Enfoque incremental por fases. Fase 1 amplia las funcionalidades Markdown existentes. Fase 2 construye el menu contextual rico. Fase 3 mejora el preview con WebView2 para WYSIWYG. Fase 4 pule la experiencia (clipboard, shortcuts, TOC).

**Tech Stack:** .NET 9, WPF, AvalonEdit, Markdig, CommunityToolkit.Mvvm, Microsoft.Web.WebView2 (Fase 3)

---

## Estado Actual

### Ya implementado
- Editor AvalonEdit con syntax highlighting Markdown
- Preview FlowDocument con conversion Markdig
- Formato: Bold, Italic, Strikethrough, Inline code, H1-H3
- Bloques: Code block, Blockquote, HR, Ordered/Unordered lists
- Link, Image (placeholder texto), Export HTML
- Tabs, Sidebar explorador, Find/Replace, Temas Dark/Light
- Archivos recientes, font size control

### Falta (mapeado de Typora)
- H4, H5, H6
- Task lists (checkboxes)
- Tablas (insercion + edicion markdown)
- Notas al pie (footnotes)
- Bloques de ecuaciones (math LaTeX)
- YAML front matter (renderizado)
- Tabla de contenidos (TOC)
- Imagenes reales en preview
- Clipboard: Copiar como Markdown/HTML, Pegar sin formato
- Menu contextual rico estilo Typora
- **WYSIWYG: Edicion directa en el preview**
- Sidebar Indice (outline de headings del documento)
- Menu Visualizacion: modo sin distracciones, maquina de escribir, pantalla completa, siempre encima, zoom, toggle statusbar, modo codigo fuente

---

## FASE 1: Funcionalidades Markdown Faltantes

### Task 1: Encabezados H4-H6

**Files:**
- Modify: `MainWindow.xaml` (menu Formato + toolbar)
- Modify: `MainWindow.xaml.cs` (handlers + InsertHeading ya soporta 1-6)

**Step 1:** Agregar H4, H5, H6 al menu Formato despues de H3:
```xml
<MenuItem Header="Titulo H4" Click="Btn_H4"/>
<MenuItem Header="Titulo H5" Click="Btn_H5"/>
<MenuItem Header="Titulo H6" Click="Btn_H6"/>
```

**Step 2:** Agregar handlers en code-behind:
```csharp
private void Btn_H4(object s, RoutedEventArgs e) { Editor.Focus(); InsertHeading("4"); }
private void Btn_H5(object s, RoutedEventArgs e) { Editor.Focus(); InsertHeading("5"); }
private void Btn_H6(object s, RoutedEventArgs e) { Editor.Focus(); InsertHeading("6"); }
```

**Step 3:** Verificar que ConvertHeading en MarkdownConverter.cs ya maneja niveles 4-6 (ya lo hace en el switch con fontSize 16, 14, 13).

**Step 4:** Build y verificar.

---

### Task 2: Task Lists (Checkboxes)

**Files:**
- Modify: `MainWindow.xaml` (menu + toolbar)
- Modify: `MainWindow.xaml.cs` (handler + metodo InsertTaskList)
- Modify: `Services/MarkdownConverter.cs` (renderizar checkboxes en preview)

**Step 1:** Agregar metodo InsertTaskList:
```csharp
private void InsertTaskList()
{
    var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
    string currentLine = Editor.Document.GetText(line.Offset, line.Length);
    string prefix = "- [ ] ";

    if (string.IsNullOrEmpty(currentLine))
    {
        int pos = Editor.CaretOffset;
        Editor.Document.Insert(pos, prefix);
        SafeSetCaret(pos + prefix.Length);
    }
    else
    {
        string newLine = prefix + currentLine;
        Editor.Document.Replace(line.Offset, line.Length, newLine);
        SafeSetCaret(line.Offset + newLine.Length);
    }
    Editor.Focus();
}
```

**Step 2:** Agregar handler y menu item:
```csharp
private void Btn_TaskList(object s, RoutedEventArgs e) { Editor.Focus(); InsertTaskList(); }
```
```xml
<MenuItem Header="Lista de tareas" Click="Btn_TaskList"/>
```

**Step 3:** En MarkdownConverter.cs, actualizar ConvertList para detectar task list items. Markdig con UseAdvancedExtensions() ya parsea `- [ ]` y `- [x]` como `TaskList`. Modificar ConvertList:
```csharp
private static System.Windows.Documents.List ConvertList(ListBlock lb, bool isDark)
{
    var list = new System.Windows.Documents.List
    {
        Margin = new Thickness(0, 4, 0, 8),
        Padding = new Thickness(24, 0, 0, 0),
        MarkerStyle = lb.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc
    };

    foreach (var item in lb.OfType<ListItemBlock>())
    {
        var listItem = new ListItem();

        // Check for task list item
        if (item is Markdig.Extensions.TaskLists.TaskList == false
            && item.Count > 0 && item[0] is ParagraphBlock taskPara
            && taskPara.Inline?.FirstChild is LiteralInline firstLit)
        {
            // Markdig marks task items via TaskList extension attributes
        }

        foreach (var child in item)
        {
            var el = ConvertBlock(child, isDark);
            if (el != null) listItem.Blocks.Add(el);
        }
        list.ListItems.Add(listItem);
    }

    // If it's a task list, remove bullet markers
    if (lb is { } && lb.Any(i => i is ListItemBlock lib
        && lib.GetType().GetProperty("Checked") != null))
    {
        list.MarkerStyle = TextMarkerStyle.None;
    }

    return list;
}
```

Nota: Markdig con TaskLists extension agrega un atributo `Checked` a ListItemBlock. La implementacion real debe inspeccionar las inline del primer parrafo del item buscando el patron checkbox. Alternativa mas simple: buscar en el texto renderizado `[ ]` y `[x]` al inicio y reemplazar con caracteres unicode checkbox.

**Implementacion simplificada:** En ConvertParagraph o ConvertInline, detectar LiteralInline que empiece con `[ ] ` o `[x] ` y reemplazar:
```csharp
// En ConvertInline, case LiteralInline:
case LiteralInline lit:
    string text = lit.Content.ToString();
    if (text.StartsWith("[ ] "))
    {
        yield return new Run("\u2610 ") { FontSize = 16 }; // empty checkbox
        yield return new Run(text.Substring(4));
    }
    else if (text.StartsWith("[x] ") || text.StartsWith("[X] "))
    {
        yield return new Run("\u2611 ") { FontSize = 16,
            Foreground = new SolidColorBrush(isDark
                ? Color.FromRgb(78, 201, 176)
                : Color.FromRgb(0, 150, 0)) }; // checked
        yield return new Run(text.Substring(4));
    }
    else
    {
        yield return new Run(text);
    }
    break;
```

**Step 4:** Build y verificar con `- [ ] tarea` y `- [x] completada`.

---

### Task 3: Tablas - Insercion con Selector Visual de Grid

**Files:**
- Create: `Views/TableSizePopup.xaml` + `Views/TableSizePopup.xaml.cs`
- Modify: `MainWindow.xaml` (menu + toolbar)
- Modify: `MainWindow.xaml.cs` (handler + InsertTable)

**Step 1:** Crear popup selector visual de grid (estilo Typora). Un popup con una cuadricula de celdas donde al pasar el raton se resaltan las filas/columnas deseadas y al hacer click se inserta la tabla con esas dimensiones.

Crear `Views/TableSizePopup.xaml`:
```xml
<Window x:Class="PlannamTypora.Views.TableSizePopup"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Insertar tabla" SizeToContent="WidthAndHeight"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        Topmost="True" ShowInTaskbar="False" ResizeMode="NoResize"
        Deactivated="Window_Deactivated">
    <Border Background="{DynamicResource SurfaceBrush}"
            BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1"
            CornerRadius="4" Padding="8">
        <StackPanel>
            <UniformGrid x:Name="Grid" Rows="8" Columns="8" Width="200" Height="200"/>
            <TextBlock x:Name="SizeLabel" Text="0 x 0"
                       HorizontalAlignment="Center" Margin="0,6,0,0"
                       Foreground="{DynamicResource ForegroundMutedBrush}" FontSize="12"/>
        </StackPanel>
    </Border>
</Window>
```

`Views/TableSizePopup.xaml.cs`:
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PlannamTypora.Views
{
    public partial class TableSizePopup : Window
    {
        public int SelectedRows { get; private set; }
        public int SelectedCols { get; private set; }
        public bool Confirmed { get; private set; }

        private readonly Rectangle[,] _cells = new Rectangle[8, 8];

        public TableSizePopup()
        {
            InitializeComponent();
            BuildGrid();
        }

        private void BuildGrid()
        {
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var rect = new Rectangle
                    {
                        Width = 22, Height = 22,
                        Margin = new Thickness(1),
                        Fill = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                        Stroke = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                        StrokeThickness = 1,
                        RadiusX = 2, RadiusY = 2
                    };
                    int row = r, col = c;
                    rect.MouseEnter += (s, e) => HighlightCells(row + 1, col + 1);
                    rect.MouseLeftButtonDown += (s, e) =>
                    {
                        SelectedRows = row + 1;
                        SelectedCols = col + 1;
                        Confirmed = true;
                        Close();
                    };
                    _cells[r, c] = rect;
                    Grid.Children.Add(rect);
                }
            }
        }

        private void HighlightCells(int rows, int cols)
        {
            var active = App.IsDarkTheme
                ? Color.FromRgb(86, 156, 214)
                : Color.FromRgb(0, 120, 212);
            var inactive = App.IsDarkTheme
                ? Color.FromRgb(60, 60, 60)
                : Color.FromRgb(220, 220, 220);

            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    _cells[r, c].Fill = new SolidColorBrush(
                        r < rows && c < cols ? active : inactive);

            SizeLabel.Text = $"{rows} x {cols}";
        }

        private void Window_Deactivated(object sender, EventArgs e) => Close();
    }
}
```

**Step 2:** Metodo InsertTable que abre el popup:
```csharp
private void InsertTable()
{
    var popup = new Views.TableSizePopup();
    // Posicionar cerca del cursor o centro de ventana
    popup.Owner = this;
    popup.WindowStartupLocation = WindowStartupLocation.CenterOwner;
    popup.ShowDialog();

    if (!popup.Confirmed) return;
    int rows = popup.SelectedRows;
    int cols = popup.SelectedCols;

    var sb = new StringBuilder();
    int start = Editor.CaretOffset;
    if (start > 0 && Editor.Document.GetCharAt(start - 1) != '\n')
        sb.Append('\n');

    // Header row
    sb.Append('|');
    for (int c = 0; c < cols; c++)
        sb.Append($" Columna {c + 1} |");
    sb.Append('\n');

    // Separator row
    sb.Append('|');
    for (int c = 0; c < cols; c++)
        sb.Append("-----------|");
    sb.Append('\n');

    // Data rows
    for (int r = 0; r < rows - 1; r++)
    {
        sb.Append('|');
        for (int c = 0; c < cols; c++)
            sb.Append("           |");
        sb.Append('\n');
    }
    sb.Append('\n');

    Editor.Document.Insert(start, sb.ToString());
    Editor.Focus();
}
```

**Step 3:** Menu + keybinding:
```xml
<MenuItem Header="Tabla" Click="Btn_Table" InputGestureText="Ctrl+T"/>
<KeyBinding Key="T" Modifiers="Ctrl" Command="{Binding InsertTableCommand}"/>
```

**Step 4:** Build y verificar selector visual.

---

### Task 3b: Tablas - Edicion Inteligente en Editor

**Files:**
- Create: `Services/TableEditHelper.cs`
- Modify: `MainWindow.xaml.cs` (integrar helper en editor)

Este helper detecta cuando el cursor esta dentro de una tabla markdown y proporciona:
- **Tab**: mover a siguiente celda
- **Shift+Tab**: mover a celda anterior
- **Enter al final de ultima fila**: agregar fila nueva
- **Auto-formateo**: alinear pipes al salir de la tabla

**Step 1:** Crear `Services/TableEditHelper.cs`:
```csharp
using System.Text;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace PlannamTypora.Services
{
    public static class TableEditHelper
    {
        private static readonly Regex TableRowRegex = new(@"^\|.*\|$", RegexOptions.Compiled);
        private static readonly Regex SeparatorRegex = new(@"^\|[\s\-:|]+\|$", RegexOptions.Compiled);

        /// Detecta si el cursor esta en una linea de tabla
        public static bool IsInTable(TextEditor editor)
        {
            var line = editor.Document.GetLineByOffset(editor.CaretOffset);
            string text = editor.Document.GetText(line.Offset, line.Length).Trim();
            return TableRowRegex.IsMatch(text);
        }

        /// Mueve el cursor a la siguiente celda (Tab)
        public static bool MoveToNextCell(TextEditor editor)
        {
            if (!IsInTable(editor)) return false;

            var doc = editor.Document;
            int offset = editor.CaretOffset;
            string fullText = doc.Text;

            // Buscar siguiente | despues del cursor
            int nextPipe = fullText.IndexOf('|', offset);
            if (nextPipe < 0) return false;

            // Saltar al contenido despues del pipe
            int cellStart = nextPipe + 1;

            // Si llegamos al final de la linea, ir a la primera celda de la siguiente fila
            var currentLine = doc.GetLineByOffset(offset);
            if (nextPipe >= currentLine.EndOffset - 1)
            {
                // Ir a siguiente linea
                if (currentLine.LineNumber < doc.LineCount)
                {
                    var nextLine = doc.GetLineByNumber(currentLine.LineNumber + 1);
                    string nextLineText = doc.GetText(nextLine.Offset, nextLine.Length).Trim();

                    // Si es separador, saltar una mas
                    if (SeparatorRegex.IsMatch(nextLineText) &&
                        nextLine.LineNumber < doc.LineCount)
                    {
                        nextLine = doc.GetLineByNumber(nextLine.LineNumber + 1);
                        nextLineText = doc.GetText(nextLine.Offset, nextLine.Length).Trim();
                    }

                    if (TableRowRegex.IsMatch(nextLineText))
                    {
                        int pipePos = doc.GetText(nextLine.Offset, nextLine.Length).IndexOf('|');
                        if (pipePos >= 0)
                            cellStart = nextLine.Offset + pipePos + 1;
                    }
                    else
                    {
                        // Fuera de tabla: agregar fila nueva
                        AddRow(editor, currentLine);
                        return true;
                    }
                }
            }

            // Seleccionar contenido de la celda
            int cellEnd = fullText.IndexOf('|', cellStart);
            if (cellEnd < 0) cellEnd = cellStart;

            string cellContent = fullText.Substring(cellStart, cellEnd - cellStart);
            int trimStart = cellContent.Length - cellContent.TrimStart().Length;
            int trimEnd = cellContent.Length - cellContent.TrimEnd().Length;

            editor.Select(cellStart + trimStart,
                          cellContent.Trim().Length);
            return true;
        }

        /// Mueve a celda anterior (Shift+Tab)
        public static bool MoveToPrevCell(TextEditor editor)
        {
            if (!IsInTable(editor)) return false;

            int offset = editor.CaretOffset;
            string fullText = editor.Document.Text;

            // Buscar | antes del cursor
            int prevPipe = fullText.LastIndexOf('|', Math.Max(0, offset - 1));
            if (prevPipe <= 0) return false;

            // Buscar el | antes de ese (inicio de celda anterior)
            int cellEnd = prevPipe;
            int cellStart = fullText.LastIndexOf('|', Math.Max(0, prevPipe - 1));
            if (cellStart < 0) return false;
            cellStart++;

            string cellContent = fullText.Substring(cellStart, cellEnd - cellStart);
            int trimStart = cellContent.Length - cellContent.TrimStart().Length;

            editor.Select(cellStart + trimStart, cellContent.Trim().Length);
            return true;
        }

        /// Agrega una fila nueva al final de la tabla
        public static void AddRow(TextEditor editor, DocumentLine lastTableLine)
        {
            string lineText = editor.Document.GetText(lastTableLine.Offset, lastTableLine.Length);
            int colCount = lineText.Count(c => c == '|') - 1;
            if (colCount <= 0) colCount = 1;

            var sb = new StringBuilder("\n|");
            for (int i = 0; i < colCount; i++)
                sb.Append("           |");

            editor.Document.Insert(lastTableLine.EndOffset, sb.ToString());
            // Mover cursor a primera celda de nueva fila
            var newLine = editor.Document.GetLineByNumber(lastTableLine.LineNumber + 1);
            int pipePos = editor.Document.GetText(newLine.Offset, newLine.Length).IndexOf('|');
            if (pipePos >= 0)
                editor.CaretOffset = newLine.Offset + pipePos + 2;
        }

        /// Agrega una columna a la tabla completa
        public static void AddColumn(TextEditor editor)
        {
            if (!IsInTable(editor)) return;
            var (startLine, endLine) = GetTableBounds(editor);
            if (startLine < 0) return;

            var doc = editor.Document;
            // Insertar celda al final de cada fila de la tabla (antes del ultimo |)
            for (int i = endLine; i >= startLine; i--)
            {
                var line = doc.GetLineByNumber(i);
                string text = doc.GetText(line.Offset, line.Length);
                int lastPipe = text.LastIndexOf('|');
                if (lastPipe >= 0)
                {
                    string isSeparator = SeparatorRegex.IsMatch(text.Trim())
                        ? "-----------|" : "           |";
                    // Si es header
                    if (i == startLine)
                        isSeparator = " Nueva col |";
                    doc.Insert(line.Offset + lastPipe, isSeparator);
                }
            }
        }

        /// Elimina la columna donde esta el cursor
        public static void RemoveColumn(TextEditor editor)
        {
            if (!IsInTable(editor)) return;
            int colIndex = GetCurrentColumnIndex(editor);
            if (colIndex < 0) return;

            var (startLine, endLine) = GetTableBounds(editor);
            var doc = editor.Document;

            for (int i = endLine; i >= startLine; i--)
            {
                var line = doc.GetLineByNumber(i);
                string text = doc.GetText(line.Offset, line.Length);
                var cells = SplitTableRow(text);
                if (colIndex < cells.Count)
                {
                    cells.RemoveAt(colIndex);
                    string newRow = "|" + string.Join("|", cells) + "|";
                    doc.Replace(line.Offset, line.Length, newRow);
                }
            }
        }

        /// Elimina la fila donde esta el cursor
        public static void RemoveRow(TextEditor editor)
        {
            if (!IsInTable(editor)) return;
            var line = editor.Document.GetLineByOffset(editor.CaretOffset);
            string text = editor.Document.GetText(line.Offset, line.Length).Trim();
            if (SeparatorRegex.IsMatch(text)) return; // no borrar separador

            int totalLength = line.TotalLength; // incluye newline
            editor.Document.Remove(line.Offset, totalLength);
        }

        /// Establece alineacion de la columna actual
        public static void SetColumnAlignment(TextEditor editor, string alignment)
        {
            if (!IsInTable(editor)) return;
            int colIndex = GetCurrentColumnIndex(editor);
            var (startLine, endLine) = GetTableBounds(editor);
            var doc = editor.Document;

            // Encontrar la linea separadora
            for (int i = startLine; i <= endLine; i++)
            {
                var line = doc.GetLineByNumber(i);
                string text = doc.GetText(line.Offset, line.Length).Trim();
                if (SeparatorRegex.IsMatch(text))
                {
                    var cells = SplitTableRow(
                        doc.GetText(line.Offset, line.Length));
                    if (colIndex < cells.Count)
                    {
                        cells[colIndex] = alignment switch
                        {
                            "left"   => ":----------",
                            "center" => ":---------:",
                            "right"  => "----------:",
                            _        => "-----------"
                        };
                        string newRow = "|" + string.Join("|", cells) + "|";
                        doc.Replace(line.Offset, line.Length, newRow);
                    }
                    break;
                }
            }
        }

        // --- Helpers privados ---

        private static (int start, int end) GetTableBounds(TextEditor editor)
        {
            var doc = editor.Document;
            int currentNum = doc.GetLineByOffset(editor.CaretOffset).LineNumber;
            int start = currentNum, end = currentNum;

            while (start > 1)
            {
                var prev = doc.GetLineByNumber(start - 1);
                if (!TableRowRegex.IsMatch(doc.GetText(prev.Offset, prev.Length).Trim()))
                    break;
                start--;
            }
            while (end < doc.LineCount)
            {
                var next = doc.GetLineByNumber(end + 1);
                if (!TableRowRegex.IsMatch(doc.GetText(next.Offset, next.Length).Trim()))
                    break;
                end++;
            }
            return (start, end);
        }

        private static int GetCurrentColumnIndex(TextEditor editor)
        {
            var line = editor.Document.GetLineByOffset(editor.CaretOffset);
            string text = editor.Document.GetText(line.Offset,
                editor.CaretOffset - line.Offset);
            return text.Count(c => c == '|') - 1;
        }

        private static List<string> SplitTableRow(string row)
        {
            row = row.Trim();
            if (row.StartsWith("|")) row = row.Substring(1);
            if (row.EndsWith("|")) row = row.Substring(0, row.Length - 1);
            return row.Split('|').ToList();
        }
    }
}
```

**Step 2:** Integrar en MainWindow. Interceptar Tab/Shift+Tab cuando estamos en tabla:
```csharp
// En constructor, despues de InitializeComponent:
Editor.TextArea.PreviewKeyDown += Editor_PreviewKeyDown;

private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.Tab && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            if (TableEditHelper.MoveToPrevCell(Editor))
                e.Handled = true;
        }
        else
        {
            if (TableEditHelper.MoveToNextCell(Editor))
                e.Handled = true;
        }
    }
}
```

**Step 3:** Agregar menu contextual de tabla (aparece con click derecho dentro de tabla):
```csharp
// En ContextMenu del editor, agregar submenu Tabla:
// (deteccion dinamica: habilitar solo cuando cursor esta en tabla)
```
```xml
<!-- En ContextMenu, agregar antes de Buscar: -->
<MenuItem Header="Tabla" x:Name="TableContextMenu">
    <MenuItem Header="Agregar fila arriba" Click="TableAddRowAbove_Click"/>
    <MenuItem Header="Agregar fila abajo" Click="TableAddRowBelow_Click"/>
    <MenuItem Header="Eliminar fila" Click="TableRemoveRow_Click"/>
    <Separator/>
    <MenuItem Header="Agregar columna" Click="TableAddCol_Click"/>
    <MenuItem Header="Eliminar columna" Click="TableRemoveCol_Click"/>
    <Separator/>
    <MenuItem Header="Alinear izquierda" Click="TableAlignLeft_Click"/>
    <MenuItem Header="Alinear centro" Click="TableAlignCenter_Click"/>
    <MenuItem Header="Alinear derecha" Click="TableAlignRight_Click"/>
    <Separator/>
    <MenuItem Header="Eliminar tabla" Click="TableDelete_Click"/>
</MenuItem>
```

Handlers:
```csharp
private void TableAddRowBelow_Click(object s, RoutedEventArgs e)
{
    var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
    TableEditHelper.AddRow(Editor, line);
}
private void TableRemoveRow_Click(object s, RoutedEventArgs e)
    => TableEditHelper.RemoveRow(Editor);
private void TableAddCol_Click(object s, RoutedEventArgs e)
    => TableEditHelper.AddColumn(Editor);
private void TableRemoveCol_Click(object s, RoutedEventArgs e)
    => TableEditHelper.RemoveColumn(Editor);
private void TableAlignLeft_Click(object s, RoutedEventArgs e)
    => TableEditHelper.SetColumnAlignment(Editor, "left");
private void TableAlignCenter_Click(object s, RoutedEventArgs e)
    => TableEditHelper.SetColumnAlignment(Editor, "center");
private void TableAlignRight_Click(object s, RoutedEventArgs e)
    => TableEditHelper.SetColumnAlignment(Editor, "right");
private void TableDelete_Click(object s, RoutedEventArgs e)
{
    // Seleccionar toda la tabla y eliminar
    var (startLine, endLine) = GetTableBoundsPublic();
    if (startLine < 0) return;
    var first = Editor.Document.GetLineByNumber(startLine);
    var last = Editor.Document.GetLineByNumber(endLine);
    Editor.Document.Remove(first.Offset, last.EndOffset - first.Offset);
}
```

**Step 4:** Mostrar/ocultar submenu "Tabla" en ContextMenu segun si el cursor esta en tabla:
```csharp
// En el evento ContextMenuOpening del editor:
Editor.ContextMenu.Opened += (s, e) =>
{
    TableContextMenu.Visibility = TableEditHelper.IsInTable(Editor)
        ? Visibility.Visible : Visibility.Collapsed;
};
```

**Step 5:** Build y verificar: Tab entre celdas, agregar filas/columnas, alineacion, eliminar.

---

### Task 4: Notas al Pie (Footnotes)

**Files:**
- Modify: `MainWindow.xaml.cs` (InsertFootnote)
- Modify: `MainWindow.xaml` (menu)
- Modify: `Services/MarkdownConverter.cs` (renderizar footnotes)

**Step 1:** Metodo InsertFootnote:
```csharp
private void InsertFootnote()
{
    int pos = Editor.CaretOffset;
    string marker = "[^1]";
    string definition = "\n\n[^1]: Texto de la nota al pie";

    Editor.Document.Insert(pos, marker);
    Editor.Document.Insert(Editor.Document.TextLength, definition);
    SafeSetCaret(pos + marker.Length);
    Editor.Focus();
}
```

**Step 2:** Menu item:
```xml
<MenuItem Header="Nota al pie" Click="Btn_Footnote"/>
```
Handler:
```csharp
private void Btn_Footnote(object s, RoutedEventArgs e) { Editor.Focus(); InsertFootnote(); }
```

**Step 3:** En MarkdownConverter, Markdig con UseAdvancedExtensions ya parsea footnotes como `FootnoteGroup` y `FootnoteLink`. Agregar al switch de ConvertBlock:
```csharp
Markdig.Extensions.Footnotes.FootnoteGroup fg => ConvertFootnoteGroup(fg, isDark),
```

Implementar:
```csharp
private static Section ConvertFootnoteGroup(Markdig.Extensions.Footnotes.FootnoteGroup fg, bool isDark)
{
    var section = new Section
    {
        Margin = new Thickness(0, 20, 0, 0),
        BorderBrush = isDark
            ? new SolidColorBrush(Color.FromRgb(60, 60, 60))
            : new SolidColorBrush(Color.FromRgb(200, 200, 200)),
        BorderThickness = new Thickness(0, 1, 0, 0),
        Padding = new Thickness(0, 8, 0, 0)
    };

    var title = new Paragraph(new Run("Notas al pie")
    {
        FontWeight = FontWeights.SemiBold,
        FontSize = 12,
        Foreground = isDark
            ? new SolidColorBrush(Color.FromRgb(128, 128, 128))
            : new SolidColorBrush(Color.FromRgb(100, 100, 100))
    }) { Margin = new Thickness(0, 0, 0, 4) };
    section.Blocks.Add(title);

    foreach (var footnote in fg.OfType<Markdig.Extensions.Footnotes.Footnote>())
    {
        foreach (var child in footnote)
        {
            var el = ConvertBlock(child, isDark);
            if (el is Paragraph p)
            {
                var label = new Run($"[{footnote.Order}] ")
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = isDark
                        ? new SolidColorBrush(Color.FromRgb(86, 156, 214))
                        : new SolidColorBrush(Color.FromRgb(0, 100, 180))
                };
                p.Inlines.InsertBefore(p.Inlines.FirstInline, label);
                p.FontSize = 13;
                section.Blocks.Add(p);
            }
            else if (el != null)
            {
                section.Blocks.Add(el);
            }
        }
    }

    return section;
}
```

**Step 4:** En ConvertInline, manejar FootnoteLink:
```csharp
case Markdig.Extensions.Footnotes.FootnoteLink fl:
    yield return new Run($"[{fl.Order}]")
    {
        FontSize = 10,
        BaselineAlignment = BaselineAlignment.Superscript,
        Foreground = isDark
            ? new SolidColorBrush(Color.FromRgb(86, 156, 214))
            : new SolidColorBrush(Color.FromRgb(0, 100, 180))
    };
    break;
```

**Step 5:** Build y verificar con `texto[^1]` y `[^1]: definicion`.

---

### Task 5: Bloques de Ecuaciones (Math LaTeX)

**Files:**
- Modify: `Services/MarkdownConverter.cs`

**Step 1:** Markdig con UseAdvancedExtensions parsea `$...$` (inline) y `$$...$$` (block) como `MathInline` y `MathBlock`. Agregar al switch de ConvertBlock:
```csharp
Markdig.Extensions.Mathematics.MathBlock mb => ConvertMathBlock(mb, isDark),
```

```csharp
private static WpfBlock ConvertMathBlock(Markdig.Extensions.Mathematics.MathBlock mb, bool isDark)
{
    string latex = mb.Lines.ToString().Trim();
    var bg = isDark
        ? new SolidColorBrush(Color.FromRgb(30, 30, 35))
        : new SolidColorBrush(Color.FromRgb(248, 248, 248));
    var fg = isDark
        ? new SolidColorBrush(Color.FromRgb(181, 206, 168))
        : new SolidColorBrush(Color.FromRgb(0, 100, 50));

    var para = new Paragraph
    {
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(0, 12, 0, 12),
        Background = bg,
        Padding = new Thickness(16, 8, 16, 8),
        FontFamily = new FontFamily("Cambria Math, Consolas"),
        FontSize = 16,
        Foreground = fg,
        FontStyle = FontStyles.Italic
    };
    para.Inlines.Add(new Run(latex));
    return para;
}
```

**Step 2:** En ConvertInline, manejar MathInline:
```csharp
case Markdig.Extensions.Mathematics.MathInline mi:
    yield return new Run(mi.Content.ToString())
    {
        FontFamily = new FontFamily("Cambria Math, Consolas"),
        FontStyle = FontStyles.Italic,
        Foreground = isDark
            ? new SolidColorBrush(Color.FromRgb(181, 206, 168))
            : new SolidColorBrush(Color.FromRgb(0, 100, 50)),
        Background = isDark
            ? new SolidColorBrush(Color.FromRgb(30, 30, 35))
            : new SolidColorBrush(Color.FromRgb(248, 248, 248))
    };
    break;
```

Nota: Esto renderiza el LaTeX como texto con fuente matematica, no como formulas renderizadas. Para rendering real de LaTeX se necesitaria WPF-Math o similar (se puede agregar despues).

**Step 3:** Build y verificar con `$E = mc^2$` y `$$\sum_{i=1}^n x_i$$`.

---

### Task 6: YAML Front Matter (Renderizado)

**Files:**
- Modify: `Services/MarkdownConverter.cs`

**Step 1:** Agregar al switch de ConvertBlock:
```csharp
Markdig.Extensions.Yaml.YamlFrontMatterBlock yaml => ConvertYamlFrontMatter(yaml, isDark),
```

```csharp
private static WpfBlock ConvertYamlFrontMatter(Markdig.Extensions.Yaml.YamlFrontMatterBlock yaml, bool isDark)
{
    string content = yaml.Lines.ToString().Trim();
    var bg = isDark
        ? new SolidColorBrush(Color.FromRgb(35, 35, 40))
        : new SolidColorBrush(Color.FromRgb(245, 245, 250));
    var fg = isDark
        ? new SolidColorBrush(Color.FromRgb(156, 170, 190))
        : new SolidColorBrush(Color.FromRgb(80, 80, 120));

    var section = new Section
    {
        Background = bg,
        BorderBrush = isDark
            ? new SolidColorBrush(Color.FromRgb(60, 60, 80))
            : new SolidColorBrush(Color.FromRgb(180, 180, 210)),
        BorderThickness = new Thickness(0, 0, 0, 2),
        Margin = new Thickness(0, 0, 0, 16),
        Padding = new Thickness(14, 8, 14, 8)
    };

    var header = new Paragraph(new Run("YAML Front Matter")
    {
        FontWeight = FontWeights.SemiBold,
        FontSize = 11,
        Foreground = isDark
            ? new SolidColorBrush(Color.FromRgb(100, 100, 130))
            : new SolidColorBrush(Color.FromRgb(120, 120, 150))
    }) { Margin = new Thickness(0, 0, 0, 4) };
    section.Blocks.Add(header);

    foreach (var line in content.Split('\n'))
    {
        var para = new Paragraph(new Run(line.TrimEnd('\r')))
        {
            FontFamily = new FontFamily("Cascadia Code, Consolas"),
            FontSize = 12,
            Foreground = fg,
            Margin = new Thickness(0),
            LineHeight = 18
        };
        section.Blocks.Add(para);
    }

    return section;
}
```

**Step 2:** Build y verificar con documento que tenga `---\ntitle: Test\n---`.

---

### Task 7: Tabla de Contenidos (TOC)

**Files:**
- Modify: `MainWindow.xaml.cs` (InsertTOC)
- Modify: `MainWindow.xaml` (menu)
- Modify: `Services/MarkdownConverter.cs` (renderizar `[TOC]`)

**Step 1:** Metodo InsertTOC:
```csharp
private void InsertTOC()
{
    int pos = Editor.CaretOffset;
    Editor.Document.Insert(pos, "[TOC]\n\n");
    SafeSetCaret(pos + 6);
    Editor.Focus();
}
```

**Step 2:** En MarkdownConverter, detectar parrafo que contiene solo `[TOC]` y generar tabla de contenidos a partir de los HeadingBlock del documento:
```csharp
// En ToFlowDocument, despues de parsear:
var headings = parsed.OfType<HeadingBlock>().ToList();

// En ConvertParagraph, detectar [TOC]:
private static WpfBlock? ConvertParagraphOrTOC(ParagraphBlock p, bool isDark,
    List<HeadingBlock> headings)
{
    if (p.Inline?.FirstChild is LiteralInline lit
        && lit.Content.ToString().Trim() == "[TOC]"
        && p.Inline.FirstChild.NextSibling == null)
    {
        return BuildTOC(headings, isDark);
    }
    return ConvertParagraph(p, isDark);
}
```

La funcion BuildTOC genera una Section con links a cada heading:
```csharp
private static Section BuildTOC(List<HeadingBlock> headings, bool isDark)
{
    var section = new Section
    {
        Margin = new Thickness(0, 8, 0, 16),
        Padding = new Thickness(16, 8, 16, 8),
        Background = isDark
            ? new SolidColorBrush(Color.FromRgb(35, 35, 38))
            : new SolidColorBrush(Color.FromRgb(245, 245, 245)),
        BorderBrush = isDark
            ? new SolidColorBrush(Color.FromRgb(60, 60, 60))
            : new SolidColorBrush(Color.FromRgb(200, 200, 200)),
        BorderThickness = new Thickness(1)
    };

    var title = new Paragraph(new Run("Tabla de Contenidos")
    {
        FontWeight = FontWeights.SemiBold
    }) { Margin = new Thickness(0, 0, 0, 8) };
    section.Blocks.Add(title);

    foreach (var h in headings)
    {
        string text = h.Inline?.FirstChild?.ToString() ?? "";
        int indent = (h.Level - 1) * 20;
        var para = new Paragraph
        {
            Margin = new Thickness(indent, 2, 0, 2),
            FontSize = 13
        };
        para.Inlines.Add(new Run(text)
        {
            Foreground = isDark
                ? new SolidColorBrush(Color.FromRgb(78, 201, 176))
                : new SolidColorBrush(Color.FromRgb(0, 100, 180))
        });
        section.Blocks.Add(para);
    }

    return section;
}
```

**Step 3:** Actualizar la firma de ConvertBlock para pasar headings, o usar un campo estatico temporal durante la conversion.

**Step 4:** Build y verificar.

---

### Task 8: Imagenes Reales en Preview

**Files:**
- Modify: `Services/MarkdownConverter.cs` (renderizar imagenes)

**Step 1:** En ConvertInline, case LinkInline cuando IsImage, cargar la imagen real:
```csharp
case LinkInline link when link.IsImage:
    var imgContainer = new InlineUIContainer();
    try
    {
        string imgPath = link.Url ?? "";
        // Resolver ruta relativa si es necesario
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        if (Uri.TryCreate(imgPath, UriKind.Absolute, out var absUri))
            bitmap.UriSource = absUri;
        else
            bitmap.UriSource = new Uri(imgPath, UriKind.RelativeOrAbsolute);
        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.EndInit();

        var image = new System.Windows.Controls.Image
        {
            Source = bitmap,
            MaxWidth = 600,
            MaxHeight = 400,
            Stretch = System.Windows.Media.Stretch.Uniform,
            Margin = new Thickness(0, 4, 0, 4)
        };

        // Tooltip con alt text
        string altText = "";
        foreach (var child in link)
            if (child is LiteralInline altLit)
                altText += altLit.Content.ToString();
        if (!string.IsNullOrEmpty(altText))
            image.ToolTip = altText;

        imgContainer.Child = image;
    }
    catch
    {
        imgContainer.Child = new System.Windows.Controls.TextBlock
        {
            Text = $"[Imagen no encontrada: {link.Url}]",
            FontStyle = FontStyles.Italic,
            Foreground = new SolidColorBrush(Colors.Gray)
        };
    }
    yield return imgContainer;
    break;
```

**Step 2:** Build y verificar con `![alt](ruta/imagen.png)`.

---

### Task 9: Strikethrough en Preview

**Files:**
- Modify: `Services/MarkdownConverter.cs`

**Step 1:** El case EmphasisInline actual solo maneja bold (count==2) e italic (count==1). Markdig usa delimitador `~` para strikethrough. Actualizar:
```csharp
case EmphasisInline em:
    var span = new Span();
    if (em.DelimiterChar == '~' && em.DelimiterCount == 2)
    {
        span.TextDecorations = TextDecorations.Strikethrough;
    }
    else if (em.DelimiterCount >= 2)
    {
        span.FontWeight = FontWeights.Bold;
    }
    else
    {
        span.FontStyle = FontStyles.Italic;
    }
    foreach (var child in em)
        foreach (var wpfChild in ConvertInline(child, isDark))
            span.Inlines.Add(wpfChild);
    yield return span;
    break;
```

**Step 2:** Build y verificar con `~~texto tachado~~`.

---

## FASE 2: Menu Contextual Rico (Estilo Typora)

### Task 10: Reestructurar ContextMenu del Editor

**Files:**
- Modify: `MainWindow.xaml` (reemplazar ContextMenu del editor)

**Step 1:** Reemplazar el ContextMenu actual del TextEditor con la estructura estilo Typora:

```xml
<avalonedit:TextEditor.ContextMenu>
    <ContextMenu>
        <!-- Acciones basicas -->
        <MenuItem Header="Deshacer" Command="ApplicationCommands.Undo"
                  CommandTarget="{Binding ElementName=Editor}" InputGestureText="Ctrl+Z"/>
        <MenuItem Header="Rehacer" Command="ApplicationCommands.Redo"
                  CommandTarget="{Binding ElementName=Editor}" InputGestureText="Ctrl+Y"/>
        <Separator/>
        <MenuItem Header="Cortar" Command="ApplicationCommands.Cut"
                  CommandTarget="{Binding ElementName=Editor}" InputGestureText="Ctrl+X"/>
        <MenuItem Header="Copiar" Command="ApplicationCommands.Copy"
                  CommandTarget="{Binding ElementName=Editor}" InputGestureText="Ctrl+C"/>
        <MenuItem Header="Pegar" Command="ApplicationCommands.Paste"
                  CommandTarget="{Binding ElementName=Editor}" InputGestureText="Ctrl+V"/>
        <MenuItem Header="Seleccionar todo" Click="Btn_SelectAll" InputGestureText="Ctrl+A"/>
        <Separator/>

        <!-- Copiar como... -->
        <MenuItem Header="Copiar como...">
            <MenuItem Header="Copiar como Markdown" Click="CopyAsMarkdown_Click"
                      InputGestureText="Ctrl+Shift+C"/>
            <MenuItem Header="Copiar como HTML" Click="CopyAsHtml_Click"/>
            <MenuItem Header="Pegar como texto sin formato" Click="PastePlainText_Click"
                      InputGestureText="Ctrl+Shift+V"/>
        </MenuItem>
        <Separator/>

        <!-- Formato inline -->
        <MenuItem Header="Negrita" Click="Btn_Bold" InputGestureText="Ctrl+B"/>
        <MenuItem Header="Cursiva" Click="Btn_Italic" InputGestureText="Ctrl+I"/>
        <MenuItem Header="Tachado" Click="Btn_Strike"/>
        <MenuItem Header="Codigo" Click="Btn_Code" InputGestureText="Ctrl+E"/>
        <MenuItem Header="Enlace" Click="Btn_Link" InputGestureText="Ctrl+K"/>
        <Separator/>

        <!-- Parrafo (submenu) -->
        <MenuItem Header="Parrafo">
            <MenuItem Header="Parrafo" Click="Btn_Paragraph" InputGestureText="Ctrl+0"/>
            <Separator/>
            <MenuItem Header="Encabezado 1" Click="Btn_H1" InputGestureText="Ctrl+1"/>
            <MenuItem Header="Encabezado 2" Click="Btn_H2" InputGestureText="Ctrl+2"/>
            <MenuItem Header="Encabezado 3" Click="Btn_H3" InputGestureText="Ctrl+3"/>
            <MenuItem Header="Encabezado 4" Click="Btn_H4" InputGestureText="Ctrl+4"/>
            <MenuItem Header="Encabezado 5" Click="Btn_H5" InputGestureText="Ctrl+5"/>
            <MenuItem Header="Encabezado 6" Click="Btn_H6" InputGestureText="Ctrl+6"/>
            <Separator/>
            <MenuItem Header="Cita" Click="Btn_Quote"/>
            <MenuItem Header="Lista con vinetas" Click="Btn_UList"/>
            <MenuItem Header="Lista numerada" Click="Btn_OList"/>
            <MenuItem Header="Lista de tareas" Click="Btn_TaskList"/>
        </MenuItem>

        <!-- Insertar (submenu) -->
        <MenuItem Header="Insertar">
            <MenuItem Header="Imagen" Click="Btn_Image" InputGestureText="Ctrl+Shift+I"/>
            <MenuItem Header="Nota al pie" Click="Btn_Footnote"/>
            <MenuItem Header="Linea horizontal" Click="Btn_Hr"/>
            <MenuItem Header="Tabla" Click="Btn_Table" InputGestureText="Ctrl+T"/>
            <MenuItem Header="Bloque de codigo" Click="Btn_CodeBlock" InputGestureText="Ctrl+Shift+K"/>
            <MenuItem Header="Bloque de ecuaciones" Click="Btn_MathBlock"/>
            <MenuItem Header="Tabla de contenidos" Click="Btn_TOC"/>
        </MenuItem>
        <Separator/>

        <!-- Buscar -->
        <MenuItem Header="Buscar..." Click="Btn_Find" InputGestureText="Ctrl+F"/>
    </ContextMenu>
</avalonedit:TextEditor.ContextMenu>
```

---

### Task 11: Handlers para Nuevas Opciones del Menu Contextual

**Files:**
- Modify: `MainWindow.xaml.cs`

**Step 1:** Agregar handlers faltantes:
```csharp
private void Btn_Image(object s, RoutedEventArgs e) { Editor.Focus(); InsertImage(); }
private void Btn_MathBlock(object s, RoutedEventArgs e) { Editor.Focus(); InsertMathBlock(); }
private void Btn_TOC(object s, RoutedEventArgs e) { Editor.Focus(); InsertTOC(); }

private void InsertMathBlock()
{
    int start = Editor.CaretOffset;
    string prefix = "";
    if (start > 0 && Editor.Document.GetCharAt(start - 1) != '\n')
        prefix = "\n";
    string insertion = $"{prefix}$$\nE = mc^2\n$$\n";
    Editor.Document.Insert(start, insertion);
    SafeSetCaret(start + prefix.Length + 3); // cursor en contenido
    Editor.Focus();
}
```

**Step 2:** Build y verificar todos los items del context menu.

---

### Task 12: Clipboard Avanzado

**Files:**
- Modify: `MainWindow.xaml.cs`

**Step 1:** Implementar CopyAsMarkdown (copia seleccion como markdown puro):
```csharp
private void CopyAsMarkdown_Click(object s, RoutedEventArgs e)
{
    string selected = Editor.SelectedText;
    if (!string.IsNullOrEmpty(selected))
        Clipboard.SetText(selected);
}
```

**Step 2:** CopyAsHtml (convierte seleccion markdown a HTML y copia):
```csharp
private void CopyAsHtml_Click(object s, RoutedEventArgs e)
{
    string selected = Editor.SelectedText;
    if (string.IsNullOrEmpty(selected)) return;

    var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    string html = Markdig.Markdown.ToHtml(selected, pipeline);
    Clipboard.SetText(html);
}
```

**Step 3:** PastePlainText (pega sin formato):
```csharp
private void PastePlainText_Click(object s, RoutedEventArgs e)
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
```

**Step 4:** Agregar KeyBindings:
```xml
<KeyBinding Key="C" Modifiers="Ctrl+Shift" Command="{Binding CopyAsMarkdownCommand}"/>
<KeyBinding Key="V" Modifiers="Ctrl+Shift" Command="{Binding PastePlainTextCommand}"/>
```

**Step 5:** Build y verificar.

---

### Task 13: KeyBindings para Encabezados (Ctrl+1 a Ctrl+6, Ctrl+0)

**Files:**
- Modify: `MainWindow.xaml` (InputBindings)
- Modify: `MainWindow.xaml.cs` (commands)

**Step 1:** Agregar commands:
```csharp
public ICommand ParagraphCommand { get; }
// En constructor:
ParagraphCommand = new RelayCommand(ConvertToParagraph);
```

**Step 2:** Agregar KeyBindings:
```xml
<KeyBinding Key="D1" Modifiers="Ctrl" Command="{Binding FormatHeadingCommand}" CommandParameter="1"/>
<KeyBinding Key="D2" Modifiers="Ctrl" Command="{Binding FormatHeadingCommand}" CommandParameter="2"/>
<KeyBinding Key="D3" Modifiers="Ctrl" Command="{Binding FormatHeadingCommand}" CommandParameter="3"/>
<KeyBinding Key="D4" Modifiers="Ctrl" Command="{Binding FormatHeadingCommand}" CommandParameter="4"/>
<KeyBinding Key="D5" Modifiers="Ctrl" Command="{Binding FormatHeadingCommand}" CommandParameter="5"/>
<KeyBinding Key="D6" Modifiers="Ctrl" Command="{Binding FormatHeadingCommand}" CommandParameter="6"/>
<KeyBinding Key="D0" Modifiers="Ctrl" Command="{Binding ParagraphCommand}"/>
```

**Step 3:** Build y verificar Ctrl+1 a Ctrl+6 y Ctrl+0.

---

## FASE 3: Preview WYSIWYG (Edicion en Preview)

### Task 14: Agregar WebView2 al Proyecto

**Files:**
- Modify: `PlannamTypora.csproj` (agregar paquete)
- Modify: `MainWindow.xaml` (reemplazar FlowDocumentScrollViewer con WebView2)
- Create: `Services/WebPreviewBridge.cs` (comunicacion bidireccional)

**Step 1:** Agregar dependencia WebView2:
```xml
<PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2903.40" />
```

**Step 2:** En MainWindow.xaml, reemplazar FlowDocumentScrollViewer con WebView2:
```xml
<wv2:WebView2
    Grid.Column="2"
    x:Name="PreviewWebView"
    Visibility="Visible"
    DefaultBackgroundColor="Transparent"/>
```

Agregar namespace: `xmlns:wv2="clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf"`

**Step 3:** Crear Services/WebPreviewBridge.cs que:
- Genera HTML completo con CSS del tema actual
- Inyecta JavaScript para contenteditable
- Escucha cambios del DOM via WebMessageReceived
- Convierte HTML editado de vuelta a Markdown

```csharp
namespace PlannamTypora.Services
{
    public class WebPreviewBridge
    {
        public static string GenerateHtml(string markdown, bool isDark)
        {
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
            string body = Markdig.Markdown.ToHtml(markdown, pipeline);

            string css = isDark ? GetDarkCss() : GetLightCss();

            return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>{css}</style>
</head>
<body contenteditable='true' id='editor'>
{body}
</body>
<script>
{GetEditorScript()}
</script>
</html>";
        }

        private static string GetDarkCss() => @"
            body {{ background: #1e1e1e; color: #d4d4d4; font-family: 'Segoe UI';
                   font-size: 15px; line-height: 1.6; padding: 40px; }}
            h1,h2,h3,h4,h5,h6 {{ color: #569cd6; }}
            code {{ background: #18181c; color: #ce9178; padding: 2px 6px; border-radius: 3px;
                   font-family: 'Cascadia Code', Consolas; }}
            pre {{ background: #18181c; border-left: 4px solid #4b4b50; padding: 14px; }}
            pre code {{ background: none; padding: 0; }}
            blockquote {{ border-left: 4px solid #649b5a; padding-left: 16px; color: #649b5a; }}
            a {{ color: #4ec9b0; }}
            table {{ border-collapse: collapse; width: 100%; }}
            th,td {{ border: 1px solid #3c3c3c; padding: 8px; }}
            th {{ background: #2d2d30; }}
            hr {{ border: none; border-top: 1px solid #464646; }}
            img {{ max-width: 100%; }}
            .task-list-item {{ list-style: none; }}
            .task-list-item input {{ margin-right: 8px; }}
        ";

        private static string GetLightCss() => @"
            body {{ background: #fafafa; color: #242424; font-family: 'Segoe UI';
                   font-size: 15px; line-height: 1.6; padding: 40px; }}
            h1,h2,h3,h4,h5,h6 {{ color: #0064b4; }}
            code {{ background: #f2f2f2; color: #960000; padding: 2px 6px; border-radius: 3px;
                   font-family: 'Cascadia Code', Consolas; }}
            pre {{ background: #f2f2f2; border-left: 4px solid #c3c3c3; padding: 14px; }}
            pre code {{ background: none; padding: 0; }}
            blockquote {{ border-left: 4px solid #009600; padding-left: 16px; color: #508250; }}
            a {{ color: #0064b4; }}
            table {{ border-collapse: collapse; width: 100%; }}
            th,td {{ border: 1px solid #ccc; padding: 8px; }}
            th {{ background: #e6e6e6; }}
            hr {{ border: none; border-top: 1px solid #c8c8c8; }}
            img {{ max-width: 100%; }}
            .task-list-item {{ list-style: none; }}
            .task-list-item input {{ margin-right: 8px; }}
        ";

        private static string GetEditorScript() => @"
            // Debounce changes and send back to WPF
            let debounceTimer;
            const editor = document.getElementById('editor');

            editor.addEventListener('input', () => {
                clearTimeout(debounceTimer);
                debounceTimer = setTimeout(() => {
                    window.chrome.webview.postMessage({
                        type: 'contentChanged',
                        html: editor.innerHTML
                    });
                }, 500);
            });

            // Handle checkbox clicks
            editor.addEventListener('click', (e) => {
                if (e.target.type === 'checkbox') {
                    setTimeout(() => {
                        window.chrome.webview.postMessage({
                            type: 'contentChanged',
                            html: editor.innerHTML
                        });
                    }, 100);
                }
            });

            // Scroll sync
            editor.addEventListener('scroll', () => {
                const pct = editor.scrollTop / (editor.scrollHeight - editor.clientHeight);
                window.chrome.webview.postMessage({
                    type: 'scroll',
                    percentage: pct
                });
            });
        ";
    }
}
```

**Step 4:** En MainWindow.xaml.cs, inicializar WebView2 y manejar mensajes:
```csharp
private async void InitializeWebView()
{
    await PreviewWebView.EnsureCoreWebView2Async();
    PreviewWebView.CoreWebView2.WebMessageReceived += (s, e) =>
    {
        // Parse JSON message and update editor content
        // This is the WYSIWYG bridge
    };
}
```

**Step 5:** En RefreshPreview, usar WebView2 en lugar de FlowDocument:
```csharp
private void RefreshPreview()
{
    if (_currentViewMode == "Editor") return;
    try
    {
        string html = WebPreviewBridge.GenerateHtml(Editor.Text, App.IsDarkTheme);
        PreviewWebView.NavigateToString(html);
    }
    catch (Exception ex)
    {
        App.LogFatal("RefreshPreview", ex);
    }
}
```

---

### Task 15: HTML-to-Markdown Converter (para WYSIWYG)

**Files:**
- Create: `Services/HtmlToMarkdown.cs`

**Step 1:** Crear convertidor HTML a Markdown basico. Esto permite que cuando el usuario edita en el preview (WebView2 contenteditable), los cambios HTML se conviertan de vuelta a Markdown:

```csharp
namespace PlannamTypora.Services
{
    public static class HtmlToMarkdown
    {
        // Usar libreria ReverseMarkdown para la conversion
        // Agregar al csproj: <PackageReference Include="ReverseMarkdown" Version="4.6.0" />
        public static string Convert(string html)
        {
            var converter = new ReverseMarkdown.Converter(new ReverseMarkdown.Config
            {
                GithubFlavored = true,
                RemoveComments = true,
                SmartHrefHandling = true
            });
            return converter.Convert(html);
        }
    }
}
```

**Step 2:** Agregar dependencia:
```xml
<PackageReference Include="ReverseMarkdown" Version="4.6.0" />
```

**Step 3:** En el handler de WebMessageReceived, usar HtmlToMarkdown para actualizar el editor:
```csharp
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

            _suppressTextChanged = true;
            Editor.Text = markdown;
            _suppressTextChanged = false;

            if (_activeTab != null)
            {
                _activeTab.Document.Content = markdown;
                _activeTab.IsDirty = true;
                UpdateTitle();
            }
        }
    }
    catch (Exception ex) { App.LogFatal("WebMessageReceived", ex); }
};
```

---

### Task 16: Modo Vista WYSIWYG

**Files:**
- Modify: `MainWindow.xaml` (agregar boton vista WYSIWYG)
- Modify: `MainWindow.xaml.cs` (SetViewMode para WYSIWYG)

**Step 1:** Agregar cuarto modo de vista "WYSIWYG" que muestra solo el WebView2 con contenteditable:
```csharp
case "WYSIWYG":
    EditorColumn.Width = new GridLength(0);
    SplitterColumn.Width = new GridLength(0);
    PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
    EditorPreviewSplitter.Visibility = Visibility.Collapsed;
    Editor.Visibility = Visibility.Collapsed;
    PreviewWebView.Visibility = Visibility.Visible;
    RefreshPreview();
    break;
```

**Step 2:** Agregar boton en toolbar:
```xml
<Button Style="{DynamicResource ToolbarButtonStyle}" Focusable="False"
        Click="Btn_ViewWysiwyg" ToolTip="WYSIWYG" FontSize="14">W</Button>
```

**Step 3:** Handler:
```csharp
private void Btn_ViewWysiwyg(object s, RoutedEventArgs e) => SetViewMode("WYSIWYG");
```

---

## FASE 4: Visualizacion (Estilo Typora)

### Task 17: Sidebar con Pestana Indice (Document Outline)

**Files:**
- Modify: `MainWindow.xaml` (agregar TabControl al sidebar con Archivos + Indice)
- Modify: `MainWindow.xaml.cs` (generar outline desde headings)

**Contexto:** En Typora, el sidebar tiene dos pestanas: "Archivos" (arbol de archivos actual) e "Indice" (outline del documento basado en headings H1-H6). El Indice se actualiza en tiempo real mientras escribes.

**Step 1:** En MainWindow.xaml, envolver el TreeView existente del sidebar en un TabControl con dos tabs:

```xml
<!-- Reemplazar el TreeView directo del sidebar con: -->
<TabControl x:Name="SidebarTabs" Grid.Row="0"
            Background="{DynamicResource SidebarBrush}"
            BorderThickness="0" Padding="0">
    <!-- Tab Archivos (contenido existente) -->
    <TabItem Header="Archivos" Padding="8,4">
        <TreeView x:Name="FolderTree" ... />
        <!-- mover el TreeView existente aqui -->
    </TabItem>
    <!-- Tab Indice (outline) -->
    <TabItem Header="Indice" Padding="8,4">
        <TreeView x:Name="OutlineTree"
                  Background="{DynamicResource SidebarBrush}"
                  BorderThickness="0" FontSize="13">
        </TreeView>
    </TabItem>
</TabControl>
```

**Step 2:** En MainWindow.xaml.cs, crear metodo que parsea headings y construye el outline:

```csharp
private void RefreshOutline()
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
```

**Step 3:** Llamar `RefreshOutline()` desde `RefreshPreview()` (ya se invoca en cada cambio de texto) y al cambiar de tab de documento.

**Step 4:** Build y verificar que el outline se actualiza al escribir headings y que al hacer doble-click navega al heading.

---

### Task 18: Menu Visualizacion Completo

**Files:**
- Modify: `MainWindow.xaml` (agregar menu Visualizacion)
- Modify: `MainWindow.xaml.cs` (handlers)

**Contexto:** Typora tiene un menu "Visualizacion" con: Alternar barra lateral, Indice, Archivos, Modo codigo fuente, Modo sin distracciones, Modo maquina de escribir, Pantalla completa, Siempre encima, Zoom (acercar/alejar/tamano real), Mostrar barra de estado.

**Step 1:** Agregar menu Visualizacion en MainWindow.xaml despues del menu Formato:

```xml
<MenuItem Header="_Visualizacion">
    <MenuItem Header="Alternar barra lateral" Click="Btn_ToggleSidebar" InputGestureText="Ctrl+Shift+L"/>
    <Separator/>
    <MenuItem Header="Archivos" Click="Btn_ShowFiles"/>
    <MenuItem Header="Indice" Click="Btn_ShowOutline"/>
    <Separator/>
    <MenuItem Header="Modo codigo fuente" Click="Btn_SourceMode" InputGestureText="Ctrl+/"/>
    <MenuItem Header="Modo sin distracciones" Click="Btn_FocusMode" InputGestureText="F8"/>
    <MenuItem Header="Modo maquina de escribir" Click="Btn_TypewriterMode"/>
    <Separator/>
    <MenuItem Header="Pantalla completa" Click="Btn_Fullscreen" InputGestureText="F11"/>
    <MenuItem Header="Siempre encima" Click="Btn_AlwaysOnTop" x:Name="MenuAlwaysOnTop"/>
    <Separator/>
    <MenuItem Header="Acercar" Click="Btn_ZoomIn" InputGestureText="Ctrl++"/>
    <MenuItem Header="Alejar" Click="Btn_ZoomOut" InputGestureText="Ctrl+-"/>
    <MenuItem Header="Tamano real" Click="Btn_ZoomReset" InputGestureText="Ctrl+0"/>
    <Separator/>
    <MenuItem Header="Mostrar barra de estado" Click="Btn_ToggleStatusBar" x:Name="MenuStatusBar"/>
</MenuItem>
```

**Step 2:** Handlers en MainWindow.xaml.cs:

```csharp
// -- Sidebar toggle --
private void Btn_ToggleSidebar(object s, RoutedEventArgs e)
{
    if (SidebarColumn.Width.Value > 0)
    {
        _savedSidebarWidth = SidebarColumn.Width.Value;
        SidebarColumn.Width = new GridLength(0);
        SidebarSplitter.Visibility = Visibility.Collapsed;
    }
    else
    {
        SidebarColumn.Width = new GridLength(_savedSidebarWidth > 0 ? _savedSidebarWidth : 220);
        SidebarSplitter.Visibility = Visibility.Visible;
    }
}
private double _savedSidebarWidth = 220;

// -- Sidebar tabs --
private void Btn_ShowFiles(object s, RoutedEventArgs e)
{
    if (SidebarColumn.Width.Value == 0) Btn_ToggleSidebar(s, e);
    SidebarTabs.SelectedIndex = 0;
}

private void Btn_ShowOutline(object s, RoutedEventArgs e)
{
    if (SidebarColumn.Width.Value == 0) Btn_ToggleSidebar(s, e);
    SidebarTabs.SelectedIndex = 1;
}

// -- Source mode (solo editor, sin preview) --
private void Btn_SourceMode(object s, RoutedEventArgs e) => SetViewMode("Editor");

// -- Focus mode (sin distracciones: oculta sidebar, toolbar, statusbar, menu) --
private bool _isFocusMode = false;
private void Btn_FocusMode(object s, RoutedEventArgs e)
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
        SidebarSplitter.Visibility = Visibility.Collapsed;
    }
    else
    {
        SidebarColumn.Width = new GridLength(_savedSidebarWidth > 0 ? _savedSidebarWidth : 220);
        SidebarSplitter.Visibility = Visibility.Visible;
    }
}

// -- Typewriter mode (linea activa siempre centrada) --
private bool _isTypewriterMode = false;
private void Btn_TypewriterMode(object s, RoutedEventArgs e)
{
    _isTypewriterMode = !_isTypewriterMode;
    // Escuchar CaretPositionChanged para centrar la linea activa
    if (_isTypewriterMode)
        Editor.TextArea.Caret.PositionChanged += TypewriterScroll;
    else
        Editor.TextArea.Caret.PositionChanged -= TypewriterScroll;
}

private void TypewriterScroll(object? sender, EventArgs e)
{
    var textView = Editor.TextArea.TextView;
    var caretLine = Editor.TextArea.Caret.Line;
    var visualTop = textView.GetVisualTopByDocumentLine(caretLine);
    double viewportCenter = Editor.TextArea.TextView.ActualHeight / 2.0;
    Editor.ScrollToVerticalOffset(visualTop - viewportCenter + textView.DefaultLineHeight / 2.0);
}

// -- Fullscreen (F11) --
private WindowState _previousWindowState;
private WindowStyle _previousWindowStyle;
private bool _isFullscreen = false;
private void Btn_Fullscreen(object s, RoutedEventArgs e)
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

// -- Always on top --
private void Btn_AlwaysOnTop(object s, RoutedEventArgs e)
{
    Topmost = !Topmost;
    if (MenuAlwaysOnTop != null)
        MenuAlwaysOnTop.Header = Topmost ? "✓ Siempre encima" : "Siempre encima";
}

// -- Zoom --
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

// -- Toggle status bar --
private void Btn_ToggleStatusBar(object s, RoutedEventArgs e)
{
    StatusBarMain.Visibility = StatusBarMain.Visibility == Visibility.Visible
        ? Visibility.Collapsed : Visibility.Visible;
    if (MenuStatusBar != null)
        MenuStatusBar.Header = StatusBarMain.Visibility == Visibility.Visible
            ? "✓ Mostrar barra de estado" : "Mostrar barra de estado";
}
```

**Step 3:** Agregar InputBindings:
```xml
<KeyBinding Key="OemQuestion" Modifiers="Ctrl" Command="{Binding SourceModeCommand}"/>
<KeyBinding Key="F8" Command="{Binding FocusModeCommand}"/>
<KeyBinding Key="F11" Command="{Binding FullscreenCommand}"/>
<KeyBinding Key="L" Modifiers="Ctrl+Shift" Command="{Binding ToggleSidebarCommand}"/>
<KeyBinding Key="OemPlus" Modifiers="Ctrl" Command="{Binding ZoomInCommand}"/>
<KeyBinding Key="OemMinus" Modifiers="Ctrl" Command="{Binding ZoomOutCommand}"/>
```

**Step 4:** Build y verificar cada opcion del menu Visualizacion.

---

## FASE 5: Pulido y Extras

### Task 19: Actualizar Menu Principal con Todas las Opciones

**Files:**
- Modify: `MainWindow.xaml`

**Step 1:** Actualizar menu Formato para incluir TODAS las opciones:
- Parrafo, H1-H6
- Negrita, Cursiva, Tachado, Codigo inline
- Subrayado (si se implementa)
- Tachar

**Step 2:** Agregar menu Insertar o incluir en Formato:
- Tabla, Imagen, Nota al pie, TOC
- Bloque de codigo, Bloque de ecuaciones
- Linea horizontal

---

### Task 20: Actualizar Toolbar Completo

**Files:**
- Modify: `MainWindow.xaml`

**Step 1:** Reorganizar toolbar para incluir todos los botones nuevos de forma limpia:
- Grupo archivo: Nuevo, Abrir, Guardar
- Grupo formato: B, I, S, Code, Link
- Grupo parrafo: P, H1, H2, H3 (dropdown para H4-H6)
- Grupo bloques: Quote, UL, OL, TaskList
- Grupo insertar: Table, Image, CodeBlock, Math, HR
- Grupo vista: Editor, Split, Preview, WYSIWYG
- Tema

---

### Task 21: Mejorar Exportacion HTML

**Files:**
- Modify: `MainWindow.xaml.cs` (ExportHtml)

**Step 1:** Actualizar ExportHtml para generar HTML completo con CSS embebido (reutilizar CSS del WebPreviewBridge):
```csharp
private void ExportHtml()
{
    if (_activeTab == null) return;
    string? path = FileService.SaveFileAs(
        Path.GetFileNameWithoutExtension(_activeTab.Document.FilePath) + ".html");
    if (path == null) return;

    string html = WebPreviewBridge.GenerateHtml(Editor.Text, App.IsDarkTheme);
    // Quitar contenteditable para export
    html = html.Replace("contenteditable='true'", "");
    FileService.WriteFile(path, html);
}
```

---

### Task 22: Persistencia de Preferencias

**Files:**
- Create: `Services/SettingsService.cs`
- Modify: `MainWindow.xaml.cs`

**Step 1:** Crear servicio de configuracion que guarde:
- Tema actual (dark/light)
- Modo de vista preferido
- Tamano de fuente
- Ancho de sidebar
- Ventana (posicion, tamano, maximizada)

```csharp
namespace PlannamTypora.Services
{
    public static class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PlannamTypora", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
                        File.ReadAllText(SettingsPath)) ?? new();
            }
            catch { }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath,
                    System.Text.Json.JsonSerializer.Serialize(settings,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }

    public class AppSettings
    {
        public bool IsDarkTheme { get; set; } = true;
        public string ViewMode { get; set; } = "Split";
        public double FontSize { get; set; } = 14;
        public double SidebarWidth { get; set; } = 220;
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 750;
        public bool WindowMaximized { get; set; } = false;
    }
}
```

---

### Task 23: Mejorar Alineacion de Tablas en Preview

**Files:**
- Modify: `Services/MarkdownConverter.cs` (ConvertTable)

**Contexto:** Las tablas en el preview FlowDocument no se alinean bien - las columnas no tienen ancho uniforme y el separador `│` no queda alineado entre filas. Esto se debe a que actualmente las tablas se renderizan como Paragraphs con Runs separados por `│`, en lugar de usar una estructura tipo grid.

**Opciones:**
1. Usar `System.Windows.Documents.Table` de WPF (nativo FlowDocument) con TableRow/TableCell reales - esto da alineación perfecta con bordes y anchos proporcionales
2. Alternativamente, en la fase WebView2 las tablas HTML `<table>` se renderizan perfectamente

**Step 1:** Reescribir `ConvertTable` para usar `System.Windows.Documents.Table`:
```csharp
private static WpfBlock ConvertTable(MdTable table, bool isDark)
{
    var wpfTable = new System.Windows.Documents.Table
    {
        CellSpacing = 0,
        BorderBrush = isDark
            ? new SolidColorBrush(Color.FromRgb(70, 70, 70))
            : new SolidColorBrush(Color.FromRgb(200, 200, 200)),
        BorderThickness = new Thickness(1),
        Margin = new Thickness(0, 8, 0, 8)
    };

    // Columnas
    int colCount = table.OfType<MdTableRow>().FirstOrDefault()?.Count ?? 1;
    for (int i = 0; i < colCount; i++)
        wpfTable.Columns.Add(new TableColumn());

    var rowGroup = new TableRowGroup();
    bool isHeader = true;

    foreach (var mdRow in table.OfType<MdTableRow>())
    {
        var row = new System.Windows.Documents.TableRow();
        if (isHeader)
        {
            row.Background = isDark
                ? new SolidColorBrush(Color.FromRgb(45, 45, 48))
                : new SolidColorBrush(Color.FromRgb(230, 230, 230));
            row.FontWeight = FontWeights.SemiBold;
        }

        foreach (var mdCell in mdRow.OfType<MdTableCell>())
        {
            var cell = new System.Windows.Documents.TableCell
            {
                BorderBrush = isDark
                    ? new SolidColorBrush(Color.FromRgb(70, 70, 70))
                    : new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(8, 4, 8, 4)
            };

            var para = new Paragraph { Margin = new Thickness(0) };
            foreach (var childBlock in mdCell)
            {
                if (childBlock is ParagraphBlock pb && pb.Inline != null)
                    foreach (var mdInline in pb.Inline)
                        foreach (var wpfInline in ConvertInline(mdInline, isDark))
                            para.Inlines.Add(wpfInline);
            }
            cell.Blocks.Add(para);
            row.Cells.Add(cell);
        }

        rowGroup.Rows.Add(row);
        isHeader = false;
    }

    wpfTable.RowGroups.Add(rowGroup);
    return wpfTable;
}
```

**Step 2:** Build y verificar que las tablas se muestran con columnas alineadas y bordes uniformes.

---

### Task 24: Exportar a PDF

**Files:**
- Modify: `MainWindow.xaml` (agregar MenuItem "Exportar PDF" en menu Archivo)
- Modify: `MainWindow.xaml.cs` (handler ExportPdf, logica de impresion)

**Contexto:** Typora permite exportar el documento actual directamente a PDF. En WPF/.NET, la forma mas fiable es usar el `PrintDialog` con un `XpsDocumentWriter` para generar el PDF, o bien aprovechar WebView2 que tiene API nativa de impresion a PDF via `CoreWebView2.PrintToPdfAsync()`.

**Enfoque recomendado:** Usar `CoreWebView2.PrintToPdfAsync()` ya que el WebView2 ya esta integrado y renderiza el Markdown como HTML con todos los estilos. Esto produce un PDF identico a lo que se ve en preview.

**Step 1:** Agregar MenuItem en XAML:
```xml
<MenuItem Header="Exportar _PDF..." Click="ExportPdf_Click" InputGestureText="Ctrl+Shift+P"/>
```

**Step 2:** Implementar handler:
```csharp
private async void ExportPdf_Click(object sender, RoutedEventArgs e)
{
    if (!_webViewReady || PreviewWebView.CoreWebView2 == null)
    {
        MessageBox.Show("WebView2 no esta listo.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    var dlg = new Microsoft.Win32.SaveFileDialog
    {
        Filter = "PDF|*.pdf",
        FileName = _activeTab?.Document.FileName?.Replace(".md", ".pdf") ?? "documento.pdf"
    };
    if (dlg.ShowDialog() != true) return;

    // Renderizar HTML actual en WebView2 (no editable, limpio)
    string markdown = Editor.Text;
    bool isDark = false; // PDF siempre en tema claro para impresion
    string html = WebPreviewBridge.GenerateHtml(markdown, isDark, editable: false);
    await PreviewWebView.CoreWebView2.NavigateToString(html);
    // Esperar a que cargue
    await Task.Delay(500);
    await PreviewWebView.CoreWebView2.PrintToPdfAsync(dlg.FileName);
    // Restaurar vista WYSIWYG si estaba activa
    if (IsWysiwygMode) RefreshWysiwygPreview();
    MessageBox.Show("PDF exportado correctamente.", "Exportar PDF", MessageBoxButton.OK, MessageBoxImage.Information);
}
```

**Step 3:** Build y probar exportacion PDF con un documento de prueba.

---

### Task 25: Mejora Visual de la Interfaz

**Files:**
- Modify: `Themes/LightTheme.xaml` (bordes finos, esquinas redondeadas, colores refinados)
- Modify: `Themes/DarkTheme.xaml` (bordes finos, esquinas redondeadas, colores refinados)
- Modify: `MainWindow.xaml` (sidebar con TabControl mejorado o toggles estilizados, bordes generales)

**Contexto:** La interfaz actual tiene bordes gruesos, el sidebar usa botones feos como tabs, y en general necesita pulido visual para acercarse al acabado de Typora.

**Mejoras:**
1. **Bordes mas finos y redondeados:** Reducir BorderThickness a 0.5-1px en paneles, usar CornerRadius donde sea posible.
2. **Sidebar con tabs estilizados:** Reemplazar los botones actuales por un TabControl con estilo limpio (sin bordes gruesos, iconos minimalistas, fondo que se integre con el panel).
3. **Toolbar minimalista:** Botones sin bordes visibles, solo hover highlight, iconos monocromo.
4. **Separadores sutiles:** Lineas finas entre paneles en lugar de bordes gruesos.
5. **StatusBar limpia:** Fondo integrado con el tema, texto pequeno y discreto.

**Step 1:** Actualizar estilos en LightTheme.xaml y DarkTheme.xaml:
- Reducir todos los BorderThickness
- Agregar CornerRadius a botones y paneles
- Unificar palette de colores

**Step 2:** Redisenar sidebar en MainWindow.xaml:
- Usar TabControl con estilo custom (HeaderTemplate con iconos)
- Tabs horizontales en la parte superior del sidebar
- Estilo limpio sin bordes gruesos

**Step 3:** Build y verificar aspecto visual en ambos temas.

---

### Task 26: Alineacion de Texto en Celdas de Tablas

**Files:**
- Modify: `Services/WebPreviewBridge.cs` (CSS de tablas en ambos temas)
- Modify: `Services/MarkdownConverter.cs` (ConvertTable - respetar alineacion Markdig)

**Contexto:** Markdig parsea la alineacion de columnas definida en la sintaxis Markdown (`:---`, `:---:`, `---:`). Actualmente el CSS y el converter ignoran esta alineacion y todo el texto queda alineado a la izquierda.

**Mejoras:**
1. **En WebView2 (WYSIWYG/Preview HTML):** Markdig ya genera atributos `style="text-align:center"` etc. en el HTML de las tablas. Solo hay que asegurarse de que el CSS no los sobreescriba. Verificar que `th, td { text-align: left; }` no force la alineacion.
2. **En FlowDocument (Split/Preview mode):** En `ConvertTable`, leer `MdTableCell.ColumnIndex` y `MdTable.ColumnDefinitions[i].Alignment` para setear `TextAlignment` en cada celda.

**Step 1:** En WebPreviewBridge.cs, cambiar `th, td { ... text-align: left; }` a `th, td { ... }` (quitar text-align forzado) para que Markdig pueda aplicar su alineacion inline.

**Step 2:** En MarkdownConverter.cs ConvertTable, leer la alineacion de columna:
```csharp
var alignment = table.ColumnDefinitions.Count > colIdx
    ? table.ColumnDefinitions[colIdx].Alignment : null;
if (alignment == Markdig.Extensions.Tables.TableColumnAlign.Center)
    para.TextAlignment = TextAlignment.Center;
else if (alignment == Markdig.Extensions.Tables.TableColumnAlign.Right)
    para.TextAlignment = TextAlignment.Right;
```

**Step 3:** Build y verificar con tabla que tenga columnas alineadas a izquierda, centro y derecha.

---

## Resumen de Dependencias Nuevas

```xml
<!-- Agregar al csproj -->
<PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2903.40" />
<PackageReference Include="ReverseMarkdown" Version="4.6.0" />
```

## Orden de Implementacion Recomendado

1. Tasks 1-2 (H4-H6, Task lists) - rapido, sin dependencias
2. Tasks 3, 9 (Tablas insercion, Strikethrough) - rapido
3. Tasks 4-6 (Footnotes, Math, YAML) - preview rendering
4. Tasks 7-8 (TOC, Imagenes) - preview rendering
5. Tasks 10-13 (Menu contextual + clipboard + keybindings) - UX
6. Tasks 14-16 (WebView2 WYSIWYG) - la gran feature
7. Tasks 17-18 (Sidebar Indice + Menu Visualizacion) - visualizacion estilo Typora
8. Tasks 19-22 (Pulido, toolbar, export, settings) - finalizacion
9. Task 23 (Alineacion tablas preview) - mejora visual
10. Task 24 (Exportar PDF) - funcionalidad nueva
11. Task 25 (Mejora visual interfaz) - pulido UI final
12. Task 26 (Alineacion texto en celdas de tablas) - mejora tablas
