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
                // Ir a la siguiente fila
                int nextLineNum = currentLine.LineNumber + 1;
                if (nextLineNum > doc.LineCount) return false;

                var nextLine = doc.GetLineByNumber(nextLineNum);
                string nextLineText = doc.GetText(nextLine.Offset, nextLine.Length).Trim();

                // Si es separador, saltar a la siguiente
                if (SeparatorRegex.IsMatch(nextLineText))
                {
                    nextLineNum++;
                    if (nextLineNum > doc.LineCount) return false;
                    nextLine = doc.GetLineByNumber(nextLineNum);
                    nextLineText = doc.GetText(nextLine.Offset, nextLine.Length).Trim();
                }

                if (!TableRowRegex.IsMatch(nextLineText)) return false;

                // Primera celda de la nueva fila
                int firstPipe = doc.GetText(nextLine.Offset, nextLine.Length).IndexOf('|');
                if (firstPipe < 0) return false;
                cellStart = nextLine.Offset + firstPipe + 1;
            }

            // Encontrar el final de la celda
            int cellEnd = fullText.IndexOf('|', cellStart);
            if (cellEnd < 0) cellEnd = cellStart;

            // Seleccionar contenido de la celda (trim spaces)
            string cellContent = fullText.Substring(cellStart, cellEnd - cellStart);
            int trimStart = cellContent.Length - cellContent.TrimStart().Length;
            int trimEnd = cellContent.Length - cellContent.TrimEnd().Length;

            editor.Select(cellStart + trimStart, cellContent.Trim().Length);
            editor.ScrollToLine(doc.GetLineByOffset(cellStart).LineNumber);
            return true;
        }

        /// Mueve el cursor a la celda anterior (Shift+Tab)
        public static bool MoveToPrevCell(TextEditor editor)
        {
            if (!IsInTable(editor)) return false;

            var doc = editor.Document;
            int offset = editor.CaretOffset;
            string fullText = doc.Text;

            // Buscar | anterior al cursor
            int prevPipe = fullText.LastIndexOf('|', Math.Max(0, offset - 1));
            if (prevPipe < 0) return false;

            // Buscar el pipe anterior a ese (inicio de la celda)
            int cellEnd = prevPipe;
            int cellStartPipe = fullText.LastIndexOf('|', Math.Max(0, prevPipe - 1));
            if (cellStartPipe < 0) return false;

            int cellStart = cellStartPipe + 1;

            // Verificar que estamos en una linea de tabla
            var line = doc.GetLineByOffset(cellStart);
            string lineText = doc.GetText(line.Offset, line.Length).Trim();

            // Si es separador, saltar a la fila anterior
            if (SeparatorRegex.IsMatch(lineText))
            {
                if (line.LineNumber <= 1) return false;
                var prevLine = doc.GetLineByNumber(line.LineNumber - 1);
                string prevLineText = doc.GetText(prevLine.Offset, prevLine.Length);
                int lastPipe = prevLineText.LastIndexOf('|');
                int secondLastPipe = prevLineText.LastIndexOf('|', Math.Max(0, lastPipe - 1));
                if (secondLastPipe < 0) return false;
                cellStart = prevLine.Offset + secondLastPipe + 1;
                cellEnd = prevLine.Offset + lastPipe;
            }

            string cellContent = fullText.Substring(cellStart, cellEnd - cellStart);
            int trimStart = cellContent.Length - cellContent.TrimStart().Length;

            editor.Select(cellStart + trimStart, cellContent.Trim().Length);
            editor.ScrollToLine(doc.GetLineByOffset(cellStart).LineNumber);
            return true;
        }

        /// Inserta una nueva fila al final de la tabla
        public static void AddRow(TextEditor editor)
        {
            if (!IsInTable(editor)) return;

            var doc = editor.Document;
            var currentLine = doc.GetLineByOffset(editor.CaretOffset);

            // Contar columnas
            string lineText = doc.GetText(currentLine.Offset, currentLine.Length);
            int cols = lineText.Count(c => c == '|') - 1;
            if (cols < 1) cols = 1;

            // Encontrar el final de la tabla
            int lastTableLine = currentLine.LineNumber;
            for (int i = currentLine.LineNumber; i <= doc.LineCount; i++)
            {
                var ln = doc.GetLineByNumber(i);
                string txt = doc.GetText(ln.Offset, ln.Length).Trim();
                if (TableRowRegex.IsMatch(txt) || SeparatorRegex.IsMatch(txt))
                    lastTableLine = i;
                else
                    break;
            }

            var lastLine = doc.GetLineByNumber(lastTableLine);
            string newRow = "\n|" + string.Join("|", Enumerable.Repeat("     ", cols)) + "|";
            doc.Insert(lastLine.EndOffset, newRow);

            // Mover al primer celda de la nueva fila
            var newLine = doc.GetLineByNumber(lastTableLine + 1);
            string newLineText = doc.GetText(newLine.Offset, newLine.Length);
            int firstPipe = newLineText.IndexOf('|');
            if (firstPipe >= 0)
                editor.CaretOffset = newLine.Offset + firstPipe + 1;
        }

        /// Inserta una nueva columna a la derecha
        public static void AddColumn(TextEditor editor)
        {
            if (!IsInTable(editor)) return;

            var doc = editor.Document;

            // Encontrar todas las lineas de la tabla
            var (startLine, endLine) = GetTableBounds(editor);

            // Insertar celda al final de cada fila (antes del ultimo |)
            for (int i = endLine; i >= startLine; i--)
            {
                var ln = doc.GetLineByNumber(i);
                string text = doc.GetText(ln.Offset, ln.Length);
                int lastPipe = text.LastIndexOf('|');
                if (lastPipe < 0) continue;

                string txt = doc.GetText(ln.Offset, ln.Length).Trim();
                string insert = SeparatorRegex.IsMatch(txt) ? "------|" : "     |";
                doc.Insert(ln.Offset + lastPipe, insert);
            }
        }

        /// Elimina la columna donde esta el cursor
        public static void DeleteColumn(TextEditor editor)
        {
            if (!IsInTable(editor)) return;

            var doc = editor.Document;
            int colIndex = GetCurrentColumnIndex(editor);
            if (colIndex < 0) return;

            var (startLine, endLine) = GetTableBounds(editor);

            for (int i = endLine; i >= startLine; i--)
            {
                var ln = doc.GetLineByNumber(i);
                string text = doc.GetText(ln.Offset, ln.Length);
                var cells = SplitTableRow(text);
                if (colIndex < cells.Count)
                {
                    cells.RemoveAt(colIndex);
                    string newRow = "|" + string.Join("|", cells) + "|";
                    doc.Replace(ln.Offset, ln.Length, newRow);
                }
            }
        }

        /// Elimina la fila donde esta el cursor
        public static void DeleteRow(TextEditor editor)
        {
            if (!IsInTable(editor)) return;

            var doc = editor.Document;
            var currentLine = doc.GetLineByOffset(editor.CaretOffset);
            string txt = doc.GetText(currentLine.Offset, currentLine.Length).Trim();

            // No permitir borrar la fila separadora
            if (SeparatorRegex.IsMatch(txt)) return;

            int offset = currentLine.Offset;
            int length = currentLine.TotalLength; // includes newline
            doc.Remove(offset, Math.Min(length, doc.TextLength - offset));
        }

        /// Genera una tabla markdown de filas x cols
        public static string GenerateTable(int rows, int cols)
        {
            var sb = new StringBuilder();

            // Header
            sb.Append('|');
            for (int c = 0; c < cols; c++)
                sb.Append($" Col {c + 1} |");
            sb.AppendLine();

            // Separator
            sb.Append('|');
            for (int c = 0; c < cols; c++)
                sb.Append("-------|");
            sb.AppendLine();

            // Data rows
            for (int r = 0; r < rows; r++)
            {
                sb.Append('|');
                for (int c = 0; c < cols; c++)
                    sb.Append("       |");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // --- Helpers ---

        private static (int startLine, int endLine) GetTableBounds(TextEditor editor)
        {
            var doc = editor.Document;
            var currentLine = doc.GetLineByOffset(editor.CaretOffset);
            int start = currentLine.LineNumber;
            int end = currentLine.LineNumber;

            // Search backwards
            for (int i = currentLine.LineNumber - 1; i >= 1; i--)
            {
                var ln = doc.GetLineByNumber(i);
                string txt = doc.GetText(ln.Offset, ln.Length).Trim();
                if (TableRowRegex.IsMatch(txt) || SeparatorRegex.IsMatch(txt))
                    start = i;
                else
                    break;
            }

            // Search forwards
            for (int i = currentLine.LineNumber + 1; i <= doc.LineCount; i++)
            {
                var ln = doc.GetLineByNumber(i);
                string txt = doc.GetText(ln.Offset, ln.Length).Trim();
                if (TableRowRegex.IsMatch(txt) || SeparatorRegex.IsMatch(txt))
                    end = i;
                else
                    break;
            }

            return (start, end);
        }

        private static int GetCurrentColumnIndex(TextEditor editor)
        {
            var doc = editor.Document;
            var line = doc.GetLineByOffset(editor.CaretOffset);
            string text = doc.GetText(line.Offset, line.Length);
            int posInLine = editor.CaretOffset - line.Offset;

            int col = -1;
            for (int i = 0; i < posInLine && i < text.Length; i++)
            {
                if (text[i] == '|') col++;
            }
            return Math.Max(0, col);
        }

        private static List<string> SplitTableRow(string row)
        {
            var cells = new List<string>();
            string trimmed = row.Trim();
            if (trimmed.StartsWith('|')) trimmed = trimmed.Substring(1);
            if (trimmed.EndsWith('|')) trimmed = trimmed.Substring(0, trimmed.Length - 1);
            cells.AddRange(trimmed.Split('|'));
            return cells;
        }
    }
}
