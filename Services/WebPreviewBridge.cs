using Markdig;

namespace PlannamTypora.Services
{
    public static class WebPreviewBridge
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        public static string GenerateHtml(string markdown, bool isDark, bool editable = false)
        {
            string body = Markdig.Markdown.ToHtml(markdown, Pipeline);
            string css = isDark ? GetDarkCss() : GetLightCss();
            string editableAttr = editable ? "contenteditable='true'" : "";
            string script = editable ? GetEditorScript() : "";

            return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>{css}</style>
</head>
<body {editableAttr} id='editor'>
{body}
</body>
{(editable ? $@"<div class='input-overlay' id='inputOverlay'>
  <div class='input-dialog'>
    <label id='inputLabel'>URL:</label>
    <input type='text' id='inputField' />
    <div class='dlg-buttons'>
      <button id='inputCancel'>Cancelar</button>
      <button class='primary' id='inputOk'>Aceptar</button>
    </div>
  </div>
</div>
<div class='table-toolbar' id='tableToolbar'>
  <button id='tbAlignLeft' title='Alinear izquierda'><svg width='16' height='16' viewBox='0 0 16 16'><rect x='2' y='3' width='12' height='1.5' fill='currentColor'/><rect x='2' y='7' width='8' height='1.5' fill='currentColor'/><rect x='2' y='11' width='12' height='1.5' fill='currentColor'/></svg></button>
  <button id='tbAlignCenter' title='Alinear centro'><svg width='16' height='16' viewBox='0 0 16 16'><rect x='2' y='3' width='12' height='1.5' fill='currentColor'/><rect x='4' y='7' width='8' height='1.5' fill='currentColor'/><rect x='2' y='11' width='12' height='1.5' fill='currentColor'/></svg></button>
  <button id='tbAlignRight' title='Alinear derecha'><svg width='16' height='16' viewBox='0 0 16 16'><rect x='2' y='3' width='12' height='1.5' fill='currentColor'/><rect x='6' y='7' width='8' height='1.5' fill='currentColor'/><rect x='2' y='11' width='12' height='1.5' fill='currentColor'/></svg></button>
  <div class='tb-sep'></div>
  <button id='tbMoreActions' title='Más acciones'><svg width='16' height='16' viewBox='0 0 16 16'><circle cx='8' cy='3' r='1.5' fill='currentColor'/><circle cx='8' cy='8' r='1.5' fill='currentColor'/><circle cx='8' cy='13' r='1.5' fill='currentColor'/></svg></button>
  <button id='tbDeleteTable' class='tb-danger' title='Eliminar tabla'><svg width='16' height='16' viewBox='0 0 16 16'><path d='M5 2V1h6v1h3v1.5H2V2h3zM3 5h10l-.7 9.5H3.7L3 5zm3 1.5v6h1v-6H6zm3 0v6h1v-6H9z' fill='currentColor'/></svg></button>
</div>
<script>{script}</script>" : "")}
</html>";
        }

        private static string GetDarkCss() => @"
            body { background: #1e1e1e; color: #d4d4d4; font-family: 'Segoe UI', Calibri, sans-serif;
                   font-size: 15px; line-height: 1.7; padding: 40px; margin: 0; }
            h1, h2, h3, h4, h5, h6 { color: #569cd6; font-weight: 600; margin-top: 1.2em; margin-bottom: 0.4em; }
            h1 { font-size: 1.8em; border-bottom: 1.5px solid #3c3c3c; padding-bottom: 8px; }
            h2 { font-size: 1.5em; border-bottom: 1px solid #3c3c3c; padding-bottom: 4px; }
            h3 { font-size: 1.25em; }
            h4 { font-size: 1.1em; }
            h5 { font-size: 1em; }
            h6 { font-size: 0.9em; color: #7a7a7a; }
            code { background: #18181c; color: #ce9178; padding: 2px 6px; border-radius: 3px;
                   font-family: 'Cascadia Code', Consolas, 'Courier New', monospace; font-size: 0.9em; }
            pre { background: #18181c; border-left: 4px solid #4b4b50; padding: 14px;
                  border-radius: 0; overflow-x: auto; margin: 1em 0; }
            pre code { background: none; padding: 0; color: #ce9178; }
            blockquote { border-left: 4px solid #649b5a; margin: 1em 0; padding: 0.5em 16px; color: #649b5a;
                         background: rgba(100, 155, 90, 0.05); }
            a { color: #4ec9b0; text-decoration: none; }
            a:hover { text-decoration: underline; }
            table { border-collapse: collapse; width: 100%; margin: 1em 0; }
            th, td { border: 1px solid #3c3c3c; padding: 8px 12px; }
            th { background: #2d2d30; font-weight: 600; }
            tr:nth-child(even) { background: rgba(255,255,255,0.02); }
            hr { border: none; border-top: 1px solid #464646; margin: 24px 0; }
            img { max-width: 100%; border-radius: 4px; margin: 0.5em 0; }
            ul, ol { padding-left: 2em; }
            li { margin: 0.3em 0; }
            .task-list-item { list-style: none; margin-left: -1.5em; }
            .task-list-item input[type='checkbox'] { margin-right: 8px; transform: scale(1.2); }
            .footnotes { border-top: 1px solid #3c3c3c; margin-top: 2em; padding-top: 1em; font-size: 0.9em; }
            .footnote-backref { text-decoration: none; }
            ::-webkit-scrollbar { width: 8px; }
            ::-webkit-scrollbar-track { background: #1e1e1e; }
            ::-webkit-scrollbar-thumb { background: #555; border-radius: 4px; }
            ::selection { background: #264f78; }

            /* ─── Context Menu ─── */
            .ctx-menu { position: fixed; z-index: 10000; background: #252526; border: 1px solid #3c3c3c;
                        border-radius: 4px; padding: 3px 0; min-width: 180px; box-shadow: 0 4px 16px rgba(0,0,0,0.5);
                        font-size: 12px; color: #ccc; display: none; overflow: visible; }
            .ctx-menu.show { display: block; }
            .ctx-item { padding: 4px 20px 4px 10px; cursor: default; display: flex; justify-content: space-between;
                        align-items: center; white-space: nowrap; }
            .ctx-item:hover { background: #094771; color: #fff; }
            .ctx-item .shortcut { color: #888; font-size: 10px; margin-left: 24px; }
            .ctx-item:hover .shortcut { color: #bbb; }
            .ctx-sep { height: 1px; background: #3c3c3c; margin: 2px 0; }
            .ctx-sub { position: relative; }
            .ctx-sub > .ctx-menu { display: none; position: absolute; left: 100%; top: -3px; }
            .ctx-sub:hover > .ctx-menu { display: block; }
            .ctx-sub > .ctx-item::after { content: '\25B6'; font-size: 8px; margin-left: 12px; }

            .input-overlay { position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.5);
                             z-index: 20000; display: none; align-items: center; justify-content: center; }
            .input-overlay.show { display: flex; }
            .input-dialog { background: #2d2d30; border-radius: 6px; padding: 16px 20px; min-width: 340px;
                            box-shadow: 0 8px 32px rgba(0,0,0,0.5); font-size: 13px; color: #d4d4d4; }
            .input-dialog label { display: block; margin-bottom: 6px; font-weight: 600; }
            .input-dialog input { width: 100%; padding: 6px 8px; border: 1px solid #3c3c3c; border-radius: 4px;
                                  font-size: 13px; box-sizing: border-box; outline: none; background: #1e1e1e; color: #d4d4d4; }
            .input-dialog input:focus { border-color: #569cd6; }
            .input-dialog .dlg-buttons { display: flex; justify-content: flex-end; gap: 8px; margin-top: 12px; }
            .input-dialog button { padding: 5px 16px; border: 1px solid #3c3c3c; border-radius: 4px; background: #3c3c3c;
                                   cursor: pointer; font-size: 12px; color: #d4d4d4; }
            .input-dialog button.primary { background: #0078d4; color: #fff; border-color: #0078d4; }
            .input-dialog button:hover { opacity: 0.85; }

            /* ─── Table Toolbar ─── */
            .table-toolbar { position: absolute; display: none; z-index: 9000; background: #2d2d30;
                             border: 1px solid #3c3c3c; border-radius: 4px; padding: 2px 4px;
                             box-shadow: 0 2px 8px rgba(0,0,0,0.4); gap: 1px; align-items: center; }
            .table-toolbar.show { display: flex; }
            .table-toolbar button { background: transparent; border: 1px solid transparent; border-radius: 3px;
                                    color: #d4d4d4; cursor: pointer; padding: 3px 6px; font-size: 14px; line-height: 1; }
            .table-toolbar button:hover { background: #3c3c3c; }
            .table-toolbar button.active { background: #264f78; border-color: #569cd6; }
            .table-toolbar .tb-sep { width: 1px; height: 18px; background: #3c3c3c; margin: 0 2px; }
            .table-toolbar button.tb-danger:hover { background: #5a1d1d; color: #f48771; }
        ";

        private static string GetLightCss() => @"
            body { background: #fafafa; color: #242424; font-family: 'Segoe UI', Calibri, sans-serif;
                   font-size: 15px; line-height: 1.7; padding: 40px; margin: 0; }
            h1, h2, h3, h4, h5, h6 { color: #0064b4; font-weight: 600; margin-top: 1.2em; margin-bottom: 0.4em; }
            h1 { font-size: 1.8em; border-bottom: 1.5px solid #ddd; padding-bottom: 8px; }
            h2 { font-size: 1.5em; border-bottom: 1px solid #eee; padding-bottom: 4px; }
            h3 { font-size: 1.25em; }
            h4 { font-size: 1.1em; }
            h5 { font-size: 1em; }
            h6 { font-size: 0.9em; color: #767676; }
            code { background: #f2f2f2; color: #960000; padding: 2px 6px; border-radius: 3px;
                   font-family: 'Cascadia Code', Consolas, 'Courier New', monospace; font-size: 0.9em; }
            pre { background: #f2f2f2; border-left: 4px solid #c3c3c3; padding: 14px;
                  border-radius: 0; overflow-x: auto; margin: 1em 0; }
            pre code { background: none; padding: 0; color: #960000; }
            blockquote { border-left: 4px solid #009600; margin: 1em 0; padding: 0.5em 16px; color: #508250;
                         background: rgba(0, 150, 0, 0.03); }
            a { color: #0064b4; text-decoration: none; }
            a:hover { text-decoration: underline; }
            table { border-collapse: collapse; width: 100%; margin: 1em 0; }
            th, td { border: 1px solid #ccc; padding: 8px 12px; }
            th { background: #e6e6e6; font-weight: 600; }
            tr:nth-child(even) { background: rgba(0,0,0,0.02); }
            hr { border: none; border-top: 1px solid #c8c8c8; margin: 24px 0; }
            img { max-width: 100%; border-radius: 4px; margin: 0.5em 0; }
            ul, ol { padding-left: 2em; }
            li { margin: 0.3em 0; }
            .task-list-item { list-style: none; margin-left: -1.5em; }
            .task-list-item input[type='checkbox'] { margin-right: 8px; transform: scale(1.2); }
            .footnotes { border-top: 1px solid #ccc; margin-top: 2em; padding-top: 1em; font-size: 0.9em; }
            .footnote-backref { text-decoration: none; }
            ::-webkit-scrollbar { width: 8px; }
            ::-webkit-scrollbar-track { background: #fafafa; }
            ::-webkit-scrollbar-thumb { background: #ccc; border-radius: 4px; }
            ::selection { background: #c5d9f7; }

            /* ─── Context Menu ─── */
            .ctx-menu { position: fixed; z-index: 10000; background: #f8f8f8; border: 1px solid #ccc;
                        border-radius: 4px; padding: 3px 0; min-width: 180px; box-shadow: 0 4px 16px rgba(0,0,0,0.15);
                        font-size: 12px; color: #333; display: none; overflow: visible; }
            .ctx-menu.show { display: block; }
            .ctx-item { padding: 4px 20px 4px 10px; cursor: default; display: flex; justify-content: space-between;
                        align-items: center; white-space: nowrap; }
            .ctx-item:hover { background: #0078d4; color: #fff; }
            .ctx-item .shortcut { color: #999; font-size: 10px; margin-left: 24px; }
            .ctx-item:hover .shortcut { color: #ddd; }
            .ctx-sep { height: 1px; background: #ddd; margin: 2px 0; }
            .ctx-sub { position: relative; }
            .ctx-sub > .ctx-menu { display: none; position: absolute; left: 100%; top: -3px; }
            .ctx-sub:hover > .ctx-menu { display: block; }
            .ctx-sub > .ctx-item::after { content: '\25B6'; font-size: 8px; margin-left: 12px; }

            .input-overlay { position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.3);
                             z-index: 20000; display: none; align-items: center; justify-content: center; }
            .input-overlay.show { display: flex; }
            .input-dialog { background: #fff; border-radius: 6px; padding: 16px 20px; min-width: 340px;
                            box-shadow: 0 8px 32px rgba(0,0,0,0.25); font-size: 13px; color: #333; }
            .input-dialog label { display: block; margin-bottom: 6px; font-weight: 600; }
            .input-dialog input { width: 100%; padding: 6px 8px; border: 1px solid #ccc; border-radius: 4px;
                                  font-size: 13px; box-sizing: border-box; outline: none; }
            .input-dialog input:focus { border-color: #0078d4; }
            .input-dialog .dlg-buttons { display: flex; justify-content: flex-end; gap: 8px; margin-top: 12px; }
            .input-dialog button { padding: 5px 16px; border: 1px solid #ccc; border-radius: 4px; background: #f0f0f0;
                                   cursor: pointer; font-size: 12px; }
            .input-dialog button.primary { background: #0078d4; color: #fff; border-color: #0078d4; }
            .input-dialog button:hover { opacity: 0.85; }

            /* ─── Table Toolbar ─── */
            .table-toolbar { position: absolute; display: none; z-index: 9000; background: #fff;
                             border: 1px solid #ccc; border-radius: 4px; padding: 2px 4px;
                             box-shadow: 0 2px 8px rgba(0,0,0,0.15); gap: 1px; align-items: center; }
            .table-toolbar.show { display: flex; }
            .table-toolbar button { background: transparent; border: 1px solid transparent; border-radius: 3px;
                                    color: #333; cursor: pointer; padding: 3px 6px; font-size: 14px; line-height: 1; }
            .table-toolbar button:hover { background: #e8e8e8; }
            .table-toolbar button.active { background: #c5d9f7; border-color: #0078d4; }
            .table-toolbar .tb-sep { width: 1px; height: 18px; background: #ddd; margin: 0 2px; }
            .table-toolbar button.tb-danger:hover { background: #fdd; color: #c00; }
        ";

        private static string GetEditorScript() => @"
            let debounceTimer;
            const editor = document.getElementById('editor');

            function notifyChange() {
                clearTimeout(debounceTimer);
                debounceTimer = setTimeout(() => {
                    window.chrome.webview.postMessage(JSON.stringify({
                        type: 'contentChanged',
                        html: editor.innerHTML
                    }));
                }, 500);
            }

            editor.addEventListener('input', notifyChange);

            // Handle checkbox clicks and Ctrl+click on links
            editor.addEventListener('click', (e) => {
                if (e.target.type === 'checkbox') {
                    setTimeout(notifyChange, 100);
                }
                if (e.ctrlKey || e.metaKey) {
                    let el = e.target;
                    while (el && el !== editor) {
                        if (el.tagName === 'A' && el.href) {
                            e.preventDefault();
                            window.chrome.webview.postMessage(JSON.stringify({
                                type: 'openLink', url: el.href
                            }));
                            return;
                        }
                        el = el.parentNode;
                    }
                }
            });

            // Scroll sync
            let scrollTimer;
            window.addEventListener('scroll', () => {
                clearTimeout(scrollTimer);
                scrollTimer = setTimeout(() => {
                    const pct = window.scrollY / (document.body.scrollHeight - window.innerHeight);
                    window.chrome.webview.postMessage(JSON.stringify({
                        type: 'scroll',
                        percentage: isNaN(pct) ? 0 : pct
                    }));
                }, 50);
            });

            // ─── Formatting API (called from C# via ExecuteScriptAsync) ───

            function formatBold()        { document.execCommand('bold'); notifyChange(); }
            function formatItalic()      { document.execCommand('italic'); notifyChange(); }
            function formatStrike()      { document.execCommand('strikeThrough'); notifyChange(); }
            function formatUnderline()   { document.execCommand('underline'); notifyChange(); }

            function formatCode() {
                const sel = window.getSelection();
                if (!sel.rangeCount) return;
                const range = sel.getRangeAt(0);
                const text = range.toString();
                if (!text) return;
                const code = document.createElement('code');
                code.textContent = text;
                range.deleteContents();
                range.insertNode(code);
                // Move cursor after code element
                sel.collapseToEnd();
                notifyChange();
            }

            function formatHeading(level) {
                const tag = 'H' + level;
                const sel = window.getSelection();
                if (!sel.rangeCount) return;
                // Check if inside PRE block first
                let node = sel.anchorNode;
                let pre = null;
                while (node && node !== editor) {
                    if (node.nodeName === 'PRE') { pre = node; break; }
                    node = node.parentNode;
                }
                if (pre) {
                    const h = document.createElement(tag);
                    h.textContent = pre.textContent;
                    pre.parentNode.replaceChild(h, pre);
                    const range = document.createRange();
                    range.selectNodeContents(h);
                    range.collapse(false);
                    sel.removeAllRanges();
                    sel.addRange(range);
                    notifyChange();
                    return;
                }
                let block = sel.anchorNode;
                // Walk up to find the block-level parent
                while (block && block !== editor && !['P','H1','H2','H3','H4','H5','H6','DIV','LI','BLOCKQUOTE'].includes(block.nodeName)) {
                    block = block.parentNode;
                }
                if (!block || block === editor) {
                    document.execCommand('formatBlock', false, tag);
                } else if (block.nodeName === tag) {
                    document.execCommand('formatBlock', false, 'P');
                } else {
                    document.execCommand('formatBlock', false, tag);
                }
                notifyChange();
            }

            function formatParagraph() {
                // Check if we're inside a PRE/CODE block - formatBlock can't convert those
                const sel = window.getSelection();
                if (sel.rangeCount) {
                    let node = sel.anchorNode;
                    let pre = null;
                    while (node && node !== editor) {
                        if (node.nodeName === 'PRE') { pre = node; break; }
                        node = node.parentNode;
                    }
                    if (pre) {
                        const p = document.createElement('p');
                        p.textContent = pre.textContent;
                        pre.parentNode.replaceChild(p, pre);
                        // Place cursor inside the new paragraph
                        const range = document.createRange();
                        range.selectNodeContents(p);
                        range.collapse(false);
                        sel.removeAllRanges();
                        sel.addRange(range);
                        notifyChange();
                        return;
                    }
                }
                document.execCommand('formatBlock', false, 'P');
                notifyChange();
            }

            function formatBlockquote() {
                document.execCommand('formatBlock', false, 'BLOCKQUOTE');
                notifyChange();
            }

            function formatUnorderedList() {
                document.execCommand('insertUnorderedList');
                notifyChange();
            }

            function formatOrderedList() {
                document.execCommand('insertOrderedList');
                notifyChange();
            }

            function formatTaskList() {
                document.execCommand('insertUnorderedList');
                // Convert each <li> in the current list to task-list items with checkboxes
                const sel = window.getSelection();
                if (sel.rangeCount) {
                    let node = sel.anchorNode;
                    while (node && node !== editor) {
                        if (node.nodeName === 'UL') break;
                        node = node.parentNode;
                    }
                    if (node && node.nodeName === 'UL') {
                        node.classList.add('contains-task-list');
                        node.querySelectorAll('li').forEach(li => {
                            if (!li.querySelector('input[type=checkbox]')) {
                                li.classList.add('task-list-item');
                                const cb = document.createElement('input');
                                cb.type = 'checkbox';
                                cb.style.marginRight = '8px';
                                li.insertBefore(cb, li.firstChild);
                            }
                        });
                    }
                }
                notifyChange();
            }

            function formatHr() {
                document.execCommand('insertHorizontalRule');
                notifyChange();
            }

            function showInputDialog(label, defaultVal, callback) {
                saveSelection();
                const overlay = document.getElementById('inputOverlay');
                const field = document.getElementById('inputField');
                const lbl = document.getElementById('inputLabel');
                lbl.textContent = label;
                field.value = defaultVal || '';
                overlay.classList.add('show');
                field.focus();
                field.select();

                function cleanup() {
                    overlay.classList.remove('show');
                    document.getElementById('inputOk').onclick = null;
                    document.getElementById('inputCancel').onclick = null;
                    field.onkeydown = null;
                }
                document.getElementById('inputOk').onclick = () => {
                    const val = field.value.trim();
                    cleanup();
                    if (val) { restoreSelection(); callback(val); }
                };
                document.getElementById('inputCancel').onclick = () => { cleanup(); };
                field.onkeydown = (ev) => {
                    if (ev.key === 'Enter') { ev.preventDefault(); document.getElementById('inputOk').click(); }
                    if (ev.key === 'Escape') { ev.preventDefault(); document.getElementById('inputCancel').click(); }
                };
            }

            function insertLink() {
                showInputDialog('URL del enlace:', 'https://', (url) => {
                    document.execCommand('createLink', false, url);
                    notifyChange();
                });
            }

            function insertImage() {
                showInputDialog('URL de la imagen:', 'https://', (url) => {
                    document.execCommand('insertImage', false, url);
                    notifyChange();
                });
            }

            function formatCodeBlock() {
                const sel = window.getSelection();
                if (!sel.rangeCount) return;
                const range = sel.getRangeAt(0);
                const text = range.toString() || 'code here';
                const pre = document.createElement('pre');
                const code = document.createElement('code');
                code.textContent = text;
                pre.appendChild(code);
                range.deleteContents();
                range.insertNode(pre);
                sel.collapseToEnd();
                notifyChange();
            }

            function insertTable(rows, cols) {
                rows = rows || 3;
                cols = cols || 3;
                let html = '<table><thead><tr>';
                for (let c = 0; c < cols; c++) html += '<th>Header ' + (c+1) + '</th>';
                html += '</tr></thead><tbody>';
                for (let r = 0; r < rows; r++) {
                    html += '<tr>';
                    for (let c = 0; c < cols; c++) html += '<td>&nbsp;</td>';
                    html += '</tr>';
                }
                html += '</tbody></table>';
                document.execCommand('insertHTML', false, html);
                notifyChange();
            }

            // ─── Table editing ───

            function getTableContext() {
                const sel = window.getSelection();
                if (!sel.rangeCount) return null;
                let node = sel.anchorNode;
                let td = null, tr = null, table = null;
                while (node && node !== editor) {
                    if (node.nodeName === 'TD' || node.nodeName === 'TH') td = node;
                    if (node.nodeName === 'TR') tr = node;
                    if (node.nodeName === 'TABLE') { table = node; break; }
                    node = node.parentNode;
                }
                if (!table || !tr || !td) return null;
                const rowIdx = Array.from(tr.parentNode.children).indexOf(tr);
                const colIdx = Array.from(tr.children).indexOf(td);
                return { table, tr, td, rowIdx, colIdx };
            }

            function isInTable() {
                return getTableContext() !== null;
            }

            function tableAddRow() {
                const ctx = getTableContext();
                if (!ctx) return;
                const tbody = ctx.table.querySelector('tbody') || ctx.table;
                const cols = ctx.tr.children.length;
                const newRow = document.createElement('tr');
                for (let i = 0; i < cols; i++) {
                    const cell = document.createElement('td');
                    cell.innerHTML = '&nbsp;';
                    newRow.appendChild(cell);
                }
                // Insert after current row
                if (ctx.tr.nextSibling) {
                    ctx.tr.parentNode.insertBefore(newRow, ctx.tr.nextSibling);
                } else {
                    ctx.tr.parentNode.appendChild(newRow);
                }
                // Focus first cell of new row
                newRow.children[0].focus();
                notifyChange();
            }

            function tableAddCol() {
                const ctx = getTableContext();
                if (!ctx) return;
                const rows = ctx.table.querySelectorAll('tr');
                rows.forEach((row, i) => {
                    const isHeader = row.parentNode.nodeName === 'THEAD';
                    const cell = document.createElement(isHeader ? 'th' : 'td');
                    cell.innerHTML = isHeader ? 'Header' : '&nbsp;';
                    row.appendChild(cell);
                });
                notifyChange();
            }

            function tableDeleteRow() {
                const ctx = getTableContext();
                if (!ctx) return;
                // Don't delete if it's the only data row
                const allRows = ctx.table.querySelectorAll('tbody tr, tr');
                if (allRows.length <= 1) return;
                ctx.tr.remove();
                notifyChange();
            }

            function tableDeleteCol() {
                const ctx = getTableContext();
                if (!ctx) return;
                const colIdx = ctx.colIdx;
                const rows = ctx.table.querySelectorAll('tr');
                // Don't delete if it's the only column
                if (rows[0] && rows[0].children.length <= 1) return;
                rows.forEach(row => {
                    if (row.children[colIdx]) row.children[colIdx].remove();
                });
                notifyChange();
            }

            // ─── Extended table editing ───

            function tableInsertRowBefore() {
                const ctx = getTableContext();
                if (!ctx) return;
                const cols = ctx.tr.children.length;
                const newRow = document.createElement('tr');
                for (let i = 0; i < cols; i++) {
                    const cell = document.createElement('td');
                    cell.innerHTML = '&nbsp;';
                    newRow.appendChild(cell);
                }
                ctx.tr.parentNode.insertBefore(newRow, ctx.tr);
                newRow.children[0].focus();
                notifyChange();
            }

            function tableInsertColBefore() {
                const ctx = getTableContext();
                if (!ctx) return;
                const colIdx = ctx.colIdx;
                const rows = ctx.table.querySelectorAll('tr');
                rows.forEach(row => {
                    const isHeader = row.parentNode.nodeName === 'THEAD';
                    const cell = document.createElement(isHeader ? 'th' : 'td');
                    cell.innerHTML = isHeader ? 'Header' : '&nbsp;';
                    if (row.children[colIdx]) {
                        row.insertBefore(cell, row.children[colIdx]);
                    } else {
                        row.appendChild(cell);
                    }
                });
                notifyChange();
            }

            function tableMoveRowUp() {
                const ctx = getTableContext();
                if (!ctx) return;
                const prev = ctx.tr.previousElementSibling;
                if (prev) { ctx.tr.parentNode.insertBefore(ctx.tr, prev); notifyChange(); }
            }

            function tableMoveRowDown() {
                const ctx = getTableContext();
                if (!ctx) return;
                const next = ctx.tr.nextElementSibling;
                if (next) { ctx.tr.parentNode.insertBefore(next, ctx.tr); notifyChange(); }
            }

            function tableMoveColLeft() {
                const ctx = getTableContext();
                if (!ctx || ctx.colIdx === 0) return;
                const rows = ctx.table.querySelectorAll('tr');
                rows.forEach(row => {
                    const cur = row.children[ctx.colIdx];
                    const prev = row.children[ctx.colIdx - 1];
                    if (cur && prev) row.insertBefore(cur, prev);
                });
                notifyChange();
            }

            function tableMoveColRight() {
                const ctx = getTableContext();
                if (!ctx) return;
                const rows = ctx.table.querySelectorAll('tr');
                rows.forEach(row => {
                    const cur = row.children[ctx.colIdx];
                    const next = row.children[ctx.colIdx + 1];
                    if (cur && next) row.insertBefore(next, cur);
                });
                notifyChange();
            }

            function tableCopy() {
                const ctx = getTableContext();
                if (!ctx) return;
                window.chrome.webview.postMessage(JSON.stringify({
                    type: 'copyToClipboard', text: ctx.table.outerHTML
                }));
            }

            function tableDelete() {
                const ctx = getTableContext();
                if (!ctx) return;
                hideTableToolbar();
                ctx.table.remove();
                notifyChange();
            }

            // ─── Table alignment toolbar ───
            const tableToolbar = document.getElementById('tableToolbar');
            let _activeTable = null;

            function tableAlignColumn(align) {
                const ctx = getTableContext();
                if (!ctx) return;
                const colIdx = ctx.colIdx;
                const rows = ctx.table.querySelectorAll('tr');
                rows.forEach(row => {
                    const cell = row.children[colIdx];
                    if (cell) cell.style.textAlign = align;
                });
                updateToolbarAlignState(align);
                notifyChange();
            }

            function getColumnAlign(table, colIdx) {
                const firstRow = table.querySelector('tr');
                if (!firstRow) return 'left';
                const cell = firstRow.children[colIdx];
                return cell ? (cell.style.textAlign || 'left') : 'left';
            }

            function updateToolbarAlignState(align) {
                document.getElementById('tbAlignLeft').classList.toggle('active', align === 'left' || align === '');
                document.getElementById('tbAlignCenter').classList.toggle('active', align === 'center');
                document.getElementById('tbAlignRight').classList.toggle('active', align === 'right');
            }

            function showTableToolbar(table) {
                _activeTable = table;
                const rect = table.getBoundingClientRect();
                tableToolbar.style.left = (rect.left + window.scrollX) + 'px';
                tableToolbar.style.top = (rect.top + window.scrollY - tableToolbar.offsetHeight - 4) + 'px';
                tableToolbar.classList.add('show');
            }

            function hideTableToolbar() {
                tableToolbar.classList.remove('show');
                _activeTable = null;
            }

            function positionTableToolbar() {
                if (!_activeTable || !document.body.contains(_activeTable)) {
                    hideTableToolbar();
                    return;
                }
                const rect = _activeTable.getBoundingClientRect();
                tableToolbar.style.left = (rect.left + window.scrollX) + 'px';
                tableToolbar.style.top = (rect.top + window.scrollY - tableToolbar.offsetHeight - 4) + 'px';
            }

            // Show/hide toolbar on caret movement
            function checkTableFocus() {
                const ctx = getTableContext();
                if (ctx) {
                    if (_activeTable !== ctx.table) {
                        showTableToolbar(ctx.table);
                    }
                    updateToolbarAlignState(getColumnAlign(ctx.table, ctx.colIdx));
                } else {
                    hideTableToolbar();
                }
            }

            editor.addEventListener('click', function tbCheck() { setTimeout(checkTableFocus, 10); });
            editor.addEventListener('keyup', function tbCheck(e) {
                if (['ArrowLeft','ArrowRight','ArrowUp','ArrowDown','Tab'].includes(e.key)) {
                    setTimeout(checkTableFocus, 10);
                }
            });
            window.addEventListener('scroll', positionTableToolbar);

            // Prevent toolbar from stealing focus
            tableToolbar.addEventListener('mousedown', (e) => { e.preventDefault(); });

            // Toolbar button handlers
            document.getElementById('tbAlignLeft').addEventListener('click', (e) => {
                e.preventDefault(); e.stopPropagation(); tableAlignColumn('left');
            });
            document.getElementById('tbAlignCenter').addEventListener('click', (e) => {
                e.preventDefault(); e.stopPropagation(); tableAlignColumn('center');
            });
            document.getElementById('tbAlignRight').addEventListener('click', (e) => {
                e.preventDefault(); e.stopPropagation(); tableAlignColumn('right');
            });
            document.getElementById('tbDeleteTable').addEventListener('click', (e) => {
                e.preventDefault(); e.stopPropagation(); tableDelete();
            });
            document.getElementById('tbMoreActions').addEventListener('click', (e) => {
                e.preventDefault(); e.stopPropagation();
                const rect = e.currentTarget.getBoundingClientRect();
                let popup = document.getElementById('tableActionsPopup');
                if (!popup) {
                    popup = document.createElement('div');
                    popup.id = 'tableActionsPopup';
                    popup.className = 'ctx-menu';
                    popup.innerHTML = `
                        <div class='ctx-item' data-action='tableInsertRowBefore'>Insertar fila antes</div>
                        <div class='ctx-item' data-action='tableAddRow'>Insertar fila despu\u00e9s<span class='shortcut'>Ctrl+Enter</span></div>
                        <div class='ctx-item' data-action='tableInsertColBefore'>Insertar columna antes</div>
                        <div class='ctx-item' data-action='tableAddCol'>Insertar columna despu\u00e9s</div>
                        <div class='ctx-sep'></div>
                        <div class='ctx-item' data-action='tableMoveRowUp'>Mover fila arriba<span class='shortcut'>Alt+\u2191</span></div>
                        <div class='ctx-item' data-action='tableMoveRowDown'>Mover fila abajo<span class='shortcut'>Alt+\u2193</span></div>
                        <div class='ctx-item' data-action='tableMoveColLeft'>Mover col. izq.<span class='shortcut'>Alt+\u2190</span></div>
                        <div class='ctx-item' data-action='tableMoveColRight'>Mover col. der.<span class='shortcut'>Alt+\u2192</span></div>
                        <div class='ctx-sep'></div>
                        <div class='ctx-item' data-action='tableDelRow'>Eliminar fila</div>
                        <div class='ctx-item' data-action='tableDelCol'>Eliminar columna</div>
                        <div class='ctx-sep'></div>
                        <div class='ctx-item' data-action='tableCopy'>Copiar tabla</div>
                        <div class='ctx-item' data-action='tableDelete'>Eliminar tabla</div>
                    `;
                    document.body.appendChild(popup);
                    popup.addEventListener('click', (ev) => {
                        const item = ev.target.closest('.ctx-item');
                        if (!item) return;
                        const action = item.dataset.action;
                        if (actions[action]) actions[action]();
                        popup.classList.remove('show');
                    });
                }
                popup.style.left = rect.left + 'px';
                popup.style.top = rect.bottom + 4 + 'px';
                popup.classList.add('show');
                const hidePopup = (ev) => {
                    if (!popup.contains(ev.target) && ev.target !== e.currentTarget) {
                        popup.classList.remove('show');
                        document.removeEventListener('mousedown', hidePopup);
                    }
                };
                setTimeout(() => document.addEventListener('mousedown', hidePopup), 10);
            });

            // Keyboard shortcuts within WYSIWYG
            editor.addEventListener('keydown', (e) => {
                if (e.ctrlKey && !e.shiftKey && !e.altKey) {
                    switch(e.key) {
                        case 'b': e.preventDefault(); formatBold(); break;
                        case 'i': e.preventDefault(); formatItalic(); break;
                        case 'e': e.preventDefault(); formatCode(); break;
                        case 'k': e.preventDefault(); insertLink(); break;
                        case '1': e.preventDefault(); formatHeading(1); break;
                        case '2': e.preventDefault(); formatHeading(2); break;
                        case '3': e.preventDefault(); formatHeading(3); break;
                        case '4': e.preventDefault(); formatHeading(4); break;
                        case '5': e.preventDefault(); formatHeading(5); break;
                        case '6': e.preventDefault(); formatHeading(6); break;
                        case '0': e.preventDefault(); formatParagraph(); break;
                        case 'Enter': if (isInTable()) { e.preventDefault(); tableAddRow(); } break;
                    }
                }
                // Alt+arrows for table row/col movement
                if (e.altKey && !e.ctrlKey && !e.shiftKey && isInTable()) {
                    switch(e.key) {
                        case 'ArrowUp': e.preventDefault(); tableMoveRowUp(); break;
                        case 'ArrowDown': e.preventDefault(); tableMoveRowDown(); break;
                        case 'ArrowLeft': e.preventDefault(); tableMoveColLeft(); break;
                        case 'ArrowRight': e.preventDefault(); tableMoveColRight(); break;
                    }
                }
            });

            // ─── HTML Context Menu ───

            // Save/restore selection so formatting applies to the right text
            let _savedRange = null;
            function saveSelection() {
                const sel = window.getSelection();
                if (sel.rangeCount > 0) _savedRange = sel.getRangeAt(0).cloneRange();
            }
            function restoreSelection() {
                if (_savedRange) {
                    const sel = window.getSelection();
                    sel.removeAllRanges();
                    sel.addRange(_savedRange);
                }
            }

            function buildContextMenu() {
                const menu = document.createElement('div');
                menu.className = 'ctx-menu';
                menu.id = 'ctxMenu';
                menu.innerHTML = `
                    <div class='ctx-item' data-action='undo'>Deshacer<span class='shortcut'>Ctrl+Z</span></div>
                    <div class='ctx-item' data-action='redo'>Rehacer<span class='shortcut'>Ctrl+Y</span></div>
                    <div class='ctx-sep'></div>
                    <div class='ctx-item' data-action='cut'>Cortar<span class='shortcut'>Ctrl+X</span></div>
                    <div class='ctx-item' data-action='copy'>Copiar<span class='shortcut'>Ctrl+C</span></div>
                    <div class='ctx-item' data-action='paste'>Pegar<span class='shortcut'>Ctrl+V</span></div>
                    <div class='ctx-item' data-action='selectAll'>Seleccionar todo<span class='shortcut'>Ctrl+A</span></div>
                    <div class='ctx-sep'></div>
                    <div class='ctx-sub'>
                        <div class='ctx-item'>Copiar / Pegar como...</div>
                        <div class='ctx-menu'>
                            <div class='ctx-item' data-action='copyMarkdown'>Copiar como Markdown<span class='shortcut'>Ctrl+Shift+C</span></div>
                            <div class='ctx-item' data-action='copyHtml'>Copiar como HTML</div>
                            <div class='ctx-item' data-action='copyCodeContent'>Copiar contenido de c\u00f3digo</div>
                            <div class='ctx-item' data-action='copyPlainText'>Copiar sin formato</div>
                            <div class='ctx-sep'></div>
                            <div class='ctx-item' data-action='pastePlain'>Pegar como texto sin formato<span class='shortcut'>Ctrl+Shift+V</span></div>
                        </div>
                    </div>
                    <div class='ctx-sep'></div>
                    <div class='ctx-item' data-action='bold'>Negrita<span class='shortcut'>Ctrl+B</span></div>
                    <div class='ctx-item' data-action='italic'>Cursiva<span class='shortcut'>Ctrl+I</span></div>
                    <div class='ctx-item' data-action='strike'>Tachado</div>
                    <div class='ctx-item' data-action='code'>C\u00f3digo<span class='shortcut'>Ctrl+E</span></div>
                    <div class='ctx-item' data-action='link'>Enlace<span class='shortcut'>Ctrl+K</span></div>
                    <div class='ctx-sep'></div>
                    <div class='ctx-sub'>
                        <div class='ctx-item'>P\u00e1rrafo</div>
                        <div class='ctx-menu'>
                            <div class='ctx-item' data-action='paragraph'>P\u00e1rrafo<span class='shortcut'>Ctrl+0</span></div>
                            <div class='ctx-sep'></div>
                            <div class='ctx-item' data-action='h1'>Encabezado 1<span class='shortcut'>Ctrl+1</span></div>
                            <div class='ctx-item' data-action='h2'>Encabezado 2<span class='shortcut'>Ctrl+2</span></div>
                            <div class='ctx-item' data-action='h3'>Encabezado 3<span class='shortcut'>Ctrl+3</span></div>
                            <div class='ctx-item' data-action='h4'>Encabezado 4<span class='shortcut'>Ctrl+4</span></div>
                            <div class='ctx-item' data-action='h5'>Encabezado 5<span class='shortcut'>Ctrl+5</span></div>
                            <div class='ctx-item' data-action='h6'>Encabezado 6<span class='shortcut'>Ctrl+6</span></div>
                            <div class='ctx-sep'></div>
                            <div class='ctx-item' data-action='blockquote'>Cita</div>
                            <div class='ctx-item' data-action='ulist'>Lista con vi\u00f1etas</div>
                            <div class='ctx-item' data-action='olist'>Lista numerada</div>
                        </div>
                    </div>
                    <div class='ctx-sub'>
                        <div class='ctx-item'>Insertar</div>
                        <div class='ctx-menu'>
                            <div class='ctx-item' data-action='image'>Imagen<span class='shortcut'>Ctrl+Shift+I</span></div>
                            <div class='ctx-item' data-action='hr'>L\u00ednea horizontal</div>
                            <div class='ctx-item' data-action='table'>Tabla<span class='shortcut'>Ctrl+T</span></div>
                            <div class='ctx-item' data-action='codeblock'>Bloque de c\u00f3digo<span class='shortcut'>Ctrl+Shift+K</span></div>
                        </div>
                    </div>
                    <div class='ctx-sep ctx-table-sep' style='display:none'></div>
                    <div class='ctx-sub ctx-table-sub' style='display:none'>
                        <div class='ctx-item'>Tabla</div>
                        <div class='ctx-menu'>
                            <div class='ctx-item' data-action='tableInsertRowBefore'>Insertar fila antes</div>
                            <div class='ctx-item' data-action='tableAddRow'>Insertar fila despu\u00e9s<span class='shortcut'>Ctrl+Enter</span></div>
                            <div class='ctx-item' data-action='tableInsertColBefore'>Insertar columna antes</div>
                            <div class='ctx-item' data-action='tableAddCol'>Insertar columna despu\u00e9s</div>
                            <div class='ctx-sep'></div>
                            <div class='ctx-item' data-action='tableMoveRowUp'>Mover fila arriba<span class='shortcut'>Alt+\u2191</span></div>
                            <div class='ctx-item' data-action='tableMoveRowDown'>Mover fila abajo<span class='shortcut'>Alt+\u2193</span></div>
                            <div class='ctx-item' data-action='tableMoveColLeft'>Mover columna izq.<span class='shortcut'>Alt+\u2190</span></div>
                            <div class='ctx-item' data-action='tableMoveColRight'>Mover columna der.<span class='shortcut'>Alt+\u2192</span></div>
                            <div class='ctx-sep'></div>
                            <div class='ctx-item' data-action='tableDelRow'>Eliminar fila</div>
                            <div class='ctx-item' data-action='tableDelCol'>Eliminar columna</div>
                            <div class='ctx-sep'></div>
                            <div class='ctx-item' data-action='tableCopy'>Copiar tabla</div>
                            <div class='ctx-item' data-action='tableDelete'>Eliminar tabla</div>
                        </div>
                    </div>
                `;
                document.body.appendChild(menu);
                return menu;
            }

            const ctxMenu = buildContextMenu();

            // ─── Copy As... functions ───

            function getSelectedHtml() {
                const sel = window.getSelection();
                if (!sel.rangeCount) return '';
                const range = sel.getRangeAt(0);
                const div = document.createElement('div');
                div.appendChild(range.cloneContents());
                return div.innerHTML;
            }

            function copyAsMarkdown() {
                const html = getSelectedHtml();
                if (!html) return;
                window.chrome.webview.postMessage(JSON.stringify({
                    type: 'copyAsMarkdown', html: html
                }));
            }

            function copyAsHtml() {
                const html = getSelectedHtml();
                if (!html) return;
                window.chrome.webview.postMessage(JSON.stringify({
                    type: 'copyToClipboard', text: html
                }));
            }

            function copyCodeContent() {
                let text = '';
                const sel = window.getSelection();
                if (sel.rangeCount) {
                    let node = sel.anchorNode;
                    while (node && node !== editor) {
                        if (node.nodeName === 'PRE' || node.nodeName === 'CODE') {
                            text = node.textContent;
                            break;
                        }
                        node = node.parentNode;
                    }
                }
                if (!text && sel.toString()) text = sel.toString();
                if (text) {
                    window.chrome.webview.postMessage(JSON.stringify({
                        type: 'copyToClipboard', text: text
                    }));
                }
            }

            function copyPlainText() {
                const sel = window.getSelection();
                const text = sel.toString();
                if (text) {
                    window.chrome.webview.postMessage(JSON.stringify({
                        type: 'copyToClipboard', text: text
                    }));
                }
            }

            function pastePlainText() {
                window.chrome.webview.postMessage(JSON.stringify({
                    type: 'requestPaste'
                }));
            }

            // Action dispatcher
            const ctxActions = {
                undo: () => document.execCommand('undo'),
                redo: () => document.execCommand('redo'),
                cut: () => document.execCommand('cut'),
                copy: () => document.execCommand('copy'),
                paste: () => document.execCommand('paste'),
                selectAll: () => document.execCommand('selectAll'),
                copyMarkdown: copyAsMarkdown,
                copyHtml: copyAsHtml,
                copyCodeContent: copyCodeContent,
                copyPlainText: copyPlainText,
                pastePlain: pastePlainText,
                bold: formatBold,
                italic: formatItalic,
                strike: formatStrike,
                code: formatCode,
                link: insertLink,
                paragraph: formatParagraph,
                h1: () => formatHeading(1),
                h2: () => formatHeading(2),
                h3: () => formatHeading(3),
                h4: () => formatHeading(4),
                h5: () => formatHeading(5),
                h6: () => formatHeading(6),
                blockquote: formatBlockquote,
                ulist: formatUnorderedList,
                olist: formatOrderedList,
                tasklist: formatTaskList,
                image: insertImage,
                hr: formatHr,
                table: () => insertTable(3,3),
                codeblock: formatCodeBlock,
                tableInsertRowBefore: tableInsertRowBefore,
                tableAddRow: tableAddRow,
                tableInsertColBefore: tableInsertColBefore,
                tableAddCol: tableAddCol,
                tableMoveRowUp: tableMoveRowUp,
                tableMoveRowDown: tableMoveRowDown,
                tableMoveColLeft: tableMoveColLeft,
                tableMoveColRight: tableMoveColRight,
                tableDelRow: tableDeleteRow,
                tableDelCol: tableDeleteCol,
                tableCopy: tableCopy,
                tableDelete: tableDelete
            };

            ctxMenu.addEventListener('mousedown', (e) => {
                // Prevent mousedown from stealing focus/selection
                e.preventDefault();
            });
            ctxMenu.addEventListener('click', (e) => {
                const item = e.target.closest('[data-action]');
                if (!item) return;
                const action = item.getAttribute('data-action');
                ctxMenu.classList.remove('show');
                restoreSelection();
                if (ctxActions[action]) ctxActions[action]();
            });

            // Show on right-click
            document.addEventListener('contextmenu', (e) => {
                e.preventDefault();
                saveSelection();
                // Show/hide table submenu
                const inTbl = isInTable();
                ctxMenu.querySelectorAll('.ctx-table-sep, .ctx-table-sub').forEach(el => {
                    el.style.display = inTbl ? '' : 'none';
                });
                // Position - ensure menu fits in viewport
                ctxMenu.style.left = '-9999px';
                ctxMenu.style.top = '-9999px';
                ctxMenu.classList.add('show');
                const mw = ctxMenu.offsetWidth, mh = ctxMenu.offsetHeight;
                const vw = window.innerWidth, vh = window.innerHeight;
                let x = e.clientX, y = e.clientY;
                if (x + mw > vw) x = Math.max(4, vw - mw - 4);
                if (y + mh > vh) y = Math.max(4, vh - mh - 4);
                ctxMenu.style.left = x + 'px';
                ctxMenu.style.top = y + 'px';
            });

            // Reposition submenus that overflow the viewport
            ctxMenu.addEventListener('mouseover', (e) => {
                const sub = e.target.closest('.ctx-sub');
                if (!sub) return;
                const subMenu = sub.querySelector('.ctx-menu');
                if (!subMenu) return;
                // Reset position
                subMenu.style.left = ''; subMenu.style.right = '';
                subMenu.style.top = ''; subMenu.style.bottom = '';
                const rect = subMenu.getBoundingClientRect();
                if (rect.right > window.innerWidth) {
                    subMenu.style.left = 'auto';
                    subMenu.style.right = '100%';
                }
                if (rect.bottom > window.innerHeight) {
                    subMenu.style.top = 'auto';
                    subMenu.style.bottom = '-3px';
                }
            });

            // Hide on click outside or Escape
            document.addEventListener('click', (e) => {
                if (!ctxMenu.contains(e.target)) ctxMenu.classList.remove('show');
            });
            document.addEventListener('keydown', (e) => {
                if (e.key === 'Escape') ctxMenu.classList.remove('show');
            });
        ";
    }
}
