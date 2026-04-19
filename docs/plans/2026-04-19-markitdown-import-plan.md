# Plan de implementación: Importación de documentos vía markitdown

> **Para agentes ejecutores:** SUB-SKILL REQUERIDA: usa `superpowers:subagent-driven-development` (recomendado) o `superpowers:executing-plans` para ejecutar este plan tarea a tarea. Los pasos usan sintaxis de checkbox (`- [ ]`) para seguimiento.

**Objetivo:** Añadir a QuillMD la capacidad de importar PDF, DOCX, PPTX, XLSX, HTML, EPUB, CSV, JSON, XML, ZIP y Outlook `.msg` convirtiéndolos a Markdown mediante `markitdown.exe` (bundle PyInstaller) empaquetado con la aplicación.

**Arquitectura:** Un `markitdown.exe` standalone generado con PyInstaller vive en `Resources/markitdown/` y se copia a `bin/` durante `dotnet publish`. QuillMD lo invoca vía `Process.Start` desde `MarkItDownService`. `ImportService` orquesta la UI (diálogo modal bloqueante con cancelación y timeout) y añade una pestaña nueva con el resultado.

**Tech Stack:** .NET 9 WPF, CommunityToolkit.Mvvm, Python 3.11+ (solo build-time), PyInstaller, pip-licenses.

**Nota sobre testing:** QuillMD no tiene proyecto de tests actualmente. Este plan sigue la cultura del proyecto: **verificación vía checklist manual de smoke tests** al final (Tarea 13). Si el usuario quiere añadir un proyecto de xUnit para `MarkItDownService`/`ImportService`, es una tarea adicional a negociar fuera del alcance de este plan.

**Spec de referencia:** `docs/plans/2026-04-19-markitdown-import-design.md`

---

## Resumen de archivos

**Crear:**
- `tools/markitdown-bundle/pyproject.toml` — dependencias Python fijadas
- `tools/markitdown-bundle/entry.py` — entry-point para PyInstaller
- `tools/markitdown-bundle/build.ps1` — script de build Windows
- `tools/markitdown-bundle/.gitignore` — excluye venv/dist/build
- `tools/markitdown-bundle/README.md` — instrucciones de uso
- `Resources/markitdown/.gitkeep` — preserva el directorio vacío en git
- `Services/MarkItDownService.cs`
- `Services/ImportService.cs`
- `Views/ImportProgressDialog.xaml` + `Views/ImportProgressDialog.xaml.cs`

**Modificar:**
- `.gitignore` (añadir `Resources/markitdown/*.exe` y `Resources/markitdown/THIRD-PARTY-NOTICES.md`)
- `QuillMD.csproj` (incluir `Resources/markitdown/**` como `<None>` con CopyToOutputDirectory)
- `MainWindow.xaml` (añadir `MenuItem` "Importar…" y habilitar drag & drop)
- `MainWindow.xaml.cs`:
  - Añadir `ImportCommand` + handler `ImportDocument()`
  - Añadir handlers `Window_DragOver` y `Window_Drop`
  - Modificar `TabModel` (línea 1672): añadir `SuggestedSavePath` property
  - Modificar `NewTab()` (línea 345): aceptar parámetro `suggestedSavePath`
  - Modificar `SaveAs()` (línea 505): usar `_activeTab.SuggestedSavePath` si existe
  - Añadir `ShowThirdPartyNotices()` y cablearlo al menú Ayuda
- `HELP.md` (documentar la nueva función)
- `README.md` (sección "Licencias de terceros")

---

## Task 1: Crear el subproyecto Python del bundle

**Files:**
- Create: `tools/markitdown-bundle/pyproject.toml`
- Create: `tools/markitdown-bundle/entry.py`
- Create: `tools/markitdown-bundle/.gitignore`
- Create: `tools/markitdown-bundle/README.md`

- [ ] **Step 1: Crear `tools/markitdown-bundle/pyproject.toml`**

```toml
[project]
name = "markitdown-bundle"
version = "0.1.0"
description = "Bundle Python de markitdown para QuillMD"
requires-python = ">=3.11"
dependencies = [
    "markitdown[pdf,docx,pptx,xlsx,outlook]==0.1.2",
]

[project.optional-dependencies]
build = [
    "pyinstaller>=6.11.0",
    "pip-licenses>=5.0.0",
]
```

> Nota: fija la versión `0.1.2` de markitdown. Si al ejecutar `pip install` hay una versión más reciente disponible y la quieres usar, bump manualmente.

- [ ] **Step 2: Crear `tools/markitdown-bundle/entry.py`**

```python
"""Entry point for PyInstaller bundling of markitdown CLI."""
from markitdown.__main__ import main

if __name__ == "__main__":
    main()
```

- [ ] **Step 3: Crear `tools/markitdown-bundle/.gitignore`**

```
.venv/
venv/
__pycache__/
*.pyc
build/
dist/
*.spec
```

- [ ] **Step 4: Crear `tools/markitdown-bundle/README.md`**

```markdown
# markitdown-bundle

Subproyecto que genera `markitdown.exe` standalone (via PyInstaller) para
empaquetar dentro de QuillMD.

## Requisitos

- Windows 10/11
- Python 3.11+ en PATH

## Generar el bundle

Desde esta carpeta, en PowerShell:

```powershell
./build.ps1
```

Esto:

1. Crea un venv limpio en `.venv/`.
2. Instala `markitdown[pdf,docx,pptx,xlsx,outlook]` + PyInstaller + pip-licenses.
3. Empaqueta con PyInstaller a `dist/markitdown.exe`.
4. Genera `THIRD-PARTY-NOTICES.md` con los textos de licencia de todas las deps.
5. Copia ambos artefactos a `../../Resources/markitdown/`.

## Actualizar la versión de markitdown

Edita `pyproject.toml`, bumpea la versión de `markitdown`, ejecuta `build.ps1` y
commitea los cambios (el `.exe` y `THIRD-PARTY-NOTICES.md` están gitignored; lo
único que se commitea es el `pyproject.toml`).
```

- [ ] **Step 5: Commit**

```bash
git add tools/markitdown-bundle/
git commit -m "chore: añadir subproyecto markitdown-bundle (pyproject + entry + docs)"
```

---

## Task 2: Crear el script de build PowerShell

**Files:**
- Create: `tools/markitdown-bundle/build.ps1`

- [ ] **Step 1: Crear `tools/markitdown-bundle/build.ps1`**

```powershell
# build.ps1 - Genera markitdown.exe + THIRD-PARTY-NOTICES.md para QuillMD
# Uso: ./build.ps1

$ErrorActionPreference = "Stop"

$here = $PSScriptRoot
$resourcesDir = Join-Path $here "..\..\Resources\markitdown"
$venvPath = Join-Path $here ".venv"

Write-Host "=== markitdown-bundle build ===" -ForegroundColor Cyan

# 1. Asegurar Python disponible
$python = (Get-Command python -ErrorAction SilentlyContinue).Source
if (-not $python) { throw "Python 3.11+ no encontrado en PATH." }

$version = & $python --version
Write-Host "Usando $version"

# 2. Crear venv limpio
if (Test-Path $venvPath) {
    Write-Host "Limpiando venv anterior..."
    Remove-Item -Recurse -Force $venvPath
}
& $python -m venv $venvPath
$venvPython = Join-Path $venvPath "Scripts\python.exe"

# 3. Instalar deps (proyecto + extras de build)
Write-Host "Instalando dependencias..." -ForegroundColor Cyan
& $venvPython -m pip install --upgrade pip
& $venvPython -m pip install -e "$here[build]"

# 4. Generar THIRD-PARTY-NOTICES.md ANTES del PyInstaller
Write-Host "Generando THIRD-PARTY-NOTICES.md..." -ForegroundColor Cyan
$noticesPath = Join-Path $here "THIRD-PARTY-NOTICES.md"
& $venvPython -m piplicenses `
    --from=mixed `
    --with-license-file `
    --with-urls `
    --with-notice-file `
    --format=markdown `
    --ignore-packages pip setuptools wheel pip-licenses pyinstaller pyinstaller-hooks-contrib altgraph `
    --output-file $noticesPath

# 5. Build con PyInstaller
Write-Host "Empaquetando con PyInstaller..." -ForegroundColor Cyan
Push-Location $here
try {
    & $venvPython -m PyInstaller `
        --onefile `
        --name markitdown `
        --collect-all markitdown `
        --collect-all magika `
        --console `
        --clean `
        --noconfirm `
        entry.py
} finally {
    Pop-Location
}

$builtExe = Join-Path $here "dist\markitdown.exe"
if (-not (Test-Path $builtExe)) { throw "PyInstaller no produjo dist/markitdown.exe" }

# 6. Smoke test: convertir un archivo de texto trivial
Write-Host "Smoke test..." -ForegroundColor Cyan
$tmpFile = Join-Path $env:TEMP "qmd_smoke.txt"
"hola mundo" | Out-File -FilePath $tmpFile -Encoding utf8
$out = & $builtExe $tmpFile
if ($LASTEXITCODE -ne 0) { throw "Smoke test falló (exit $LASTEXITCODE). Salida: $out" }
Remove-Item $tmpFile

# 7. Copiar artefactos a Resources/markitdown/
New-Item -ItemType Directory -Force -Path $resourcesDir | Out-Null
Copy-Item $builtExe -Destination $resourcesDir -Force
Copy-Item $noticesPath -Destination $resourcesDir -Force

$exeSize = [math]::Round((Get-Item (Join-Path $resourcesDir "markitdown.exe")).Length / 1MB, 1)
Write-Host ""
Write-Host "Build OK" -ForegroundColor Green
Write-Host "  markitdown.exe -> Resources/markitdown/ ($exeSize MB)"
Write-Host "  THIRD-PARTY-NOTICES.md -> Resources/markitdown/"
```

- [ ] **Step 2: Commit**

```bash
git add tools/markitdown-bundle/build.ps1
git commit -m "chore: añadir build.ps1 para generar markitdown.exe + notices"
```

---

## Task 3: Ejecutar el build y verificar manualmente

- [ ] **Step 1: Ejecutar el script (desde Windows PowerShell)**

```powershell
cd tools/markitdown-bundle
./build.ps1
```

Expected: mensajes de progreso y al final `"Build OK"` con tamaño ~80-100 MB.

- [ ] **Step 2: Verificar artefactos generados**

```powershell
dir ../../Resources/markitdown/
```

Expected:
- `markitdown.exe` (~80-100 MB)
- `THIRD-PARTY-NOTICES.md` (~100-500 KB)

- [ ] **Step 3: Smoke test manual con un PDF real**

Coge un PDF cualquiera del disco y ejecuta:

```powershell
../../Resources/markitdown/markitdown.exe "C:\ruta\a\algun.pdf"
```

Expected: salida Markdown por stdout con el texto del PDF.

- [ ] **Step 4: Verificar contenido de THIRD-PARTY-NOTICES.md**

Abrir el archivo y comprobar que incluye las licencias de:
- `markitdown` (MIT)
- `pdfminer-six`, `pdfplumber` (MIT)
- `mammoth`, `lxml` (BSD)
- `python-pptx` (MIT)
- `pandas`, `openpyxl` (BSD/MIT)
- `magika` (Apache 2.0)
- `requests`, `beautifulsoup4`, `markdownify`

Si alguna falta, revisar `pip-licenses` — normalmente significa que hay que quitarla del `--ignore-packages` del script.

- [ ] **Step 5: Commit (sin binarios — están gitignored)**

Ningún cambio a commitear aquí si todo funcionó.

---

## Task 4: Integrar los artefactos en el build de QuillMD

**Files:**
- Create: `Resources/markitdown/.gitkeep`
- Modify: `.gitignore`
- Modify: `QuillMD.csproj`

- [ ] **Step 1: Añadir `.gitkeep` para preservar la carpeta**

```bash
touch Resources/markitdown/.gitkeep
```

- [ ] **Step 2: Modificar `.gitignore` — añadir al final:**

```
## markitdown bundle (generado por tools/markitdown-bundle/build.ps1)
Resources/markitdown/*.exe
Resources/markitdown/THIRD-PARTY-NOTICES.md
```

- [ ] **Step 3: Modificar `QuillMD.csproj` — añadir en el `<ItemGroup>` que ya tiene `<Resource Include="Resources\app.ico" />`:**

Reemplazar el bloque existente:

```xml
  <ItemGroup>
    <Resource Include="Resources\app.ico" />
    <Resource Include="Themes\LightTheme.xaml" />
    <Resource Include="Themes\DarkTheme.xaml" />
    <EmbeddedResource Include="Services\MarkdownHighlighting.xshd" />
  </ItemGroup>
```

Por:

```xml
  <ItemGroup>
    <Resource Include="Resources\app.ico" />
    <Resource Include="Themes\LightTheme.xaml" />
    <Resource Include="Themes\DarkTheme.xaml" />
    <EmbeddedResource Include="Services\MarkdownHighlighting.xshd" />
    <None Include="Resources\markitdown\markitdown.exe" Condition="Exists('Resources\markitdown\markitdown.exe')">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <TargetPath>markitdown\markitdown.exe</TargetPath>
    </None>
    <None Include="Resources\markitdown\THIRD-PARTY-NOTICES.md" Condition="Exists('Resources\markitdown\THIRD-PARTY-NOTICES.md')">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <TargetPath>markitdown\THIRD-PARTY-NOTICES.md</TargetPath>
    </None>
  </ItemGroup>
```

- [ ] **Step 4: Verificar que el build copia el exe**

```bash
dotnet build
ls bin/Debug/net9.0-windows/markitdown/
```

Expected: `markitdown.exe` y `THIRD-PARTY-NOTICES.md` en ese directorio.

- [ ] **Step 5: Commit**

```bash
git add .gitignore QuillMD.csproj Resources/markitdown/.gitkeep
git commit -m "build: integrar markitdown.exe y notices en el output de QuillMD"
```

---

## Task 5: Crear `MarkItDownService`

**Files:**
- Create: `Services/MarkItDownService.cs`

- [ ] **Step 1: Crear `Services/MarkItDownService.cs`**

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QuillMD.Services
{
    public enum ConversionStatus
    {
        Success,
        ExecutableMissing,
        NonZeroExit,
        EmptyOutput,
        Cancelled,
        Timeout,
        Exception
    }

    public record ConversionResult(
        ConversionStatus Status,
        string Markdown,
        string ErrorMessage);

    public static class MarkItDownService
    {
        public const int DefaultTimeoutSeconds = 60;

        private static string ExecutablePath =>
            Path.Combine(AppContext.BaseDirectory, "markitdown", "markitdown.exe");

        public static async Task<ConversionResult> ConvertAsync(
            string inputPath,
            CancellationToken cancellationToken,
            int timeoutSeconds = DefaultTimeoutSeconds)
        {
            if (!File.Exists(ExecutablePath))
            {
                return new ConversionResult(
                    ConversionStatus.ExecutableMissing,
                    string.Empty,
                    $"No se encontró markitdown.exe en:\n{ExecutablePath}\n\nReinstala QuillMD.");
            }

            var psi = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            psi.ArgumentList.Add(inputPath);

            Process? process = null;
            try
            {
                process = Process.Start(psi);
                if (process == null)
                {
                    return new ConversionResult(
                        ConversionStatus.Exception,
                        string.Empty,
                        "No se pudo iniciar el proceso markitdown.exe");
                }

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                try
                {
                    await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    TryKill(process);
                    if (cancellationToken.IsCancellationRequested)
                        return new ConversionResult(ConversionStatus.Cancelled, string.Empty, "Cancelado por el usuario.");
                    return new ConversionResult(ConversionStatus.Timeout, string.Empty,
                        $"La conversión superó {timeoutSeconds} s y se abortó.");
                }

                string stdout = await stdoutTask.ConfigureAwait(false);
                string stderr = await stderrTask.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    string preview = Truncate(stderr, 500);
                    return new ConversionResult(ConversionStatus.NonZeroExit, string.Empty,
                        $"markitdown.exe terminó con código {process.ExitCode}.\n\n{preview}");
                }

                if (string.IsNullOrWhiteSpace(stdout))
                {
                    return new ConversionResult(ConversionStatus.EmptyOutput, string.Empty,
                        "El archivo no contiene contenido extraíble.");
                }

                return new ConversionResult(ConversionStatus.Success, stdout, string.Empty);
            }
            catch (Exception ex)
            {
                TryKill(process);
                return new ConversionResult(ConversionStatus.Exception, string.Empty,
                    $"Error inesperado: {ex.Message}");
            }
        }

        private static void TryKill(Process? p)
        {
            try
            {
                if (p != null && !p.HasExited)
                    p.Kill(entireProcessTree: true);
            }
            catch { /* best effort */ }
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s.Substring(0, max) + "…");
    }
}
```

- [ ] **Step 2: Verificar que compila**

```bash
dotnet build
```

Expected: build OK sin warnings sobre `MarkItDownService`.

- [ ] **Step 3: Commit**

```bash
git add Services/MarkItDownService.cs
git commit -m "feat(import): añadir MarkItDownService — wrapper async sobre markitdown.exe"
```

---

## Task 6: Crear `ImportService`

**Files:**
- Create: `Services/ImportService.cs`

- [ ] **Step 1: Crear `Services/ImportService.cs`**

```csharp
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
```

- [ ] **Step 2: Verificar que compila**

```bash
dotnet build
```

- [ ] **Step 3: Commit**

```bash
git add Services/ImportService.cs
git commit -m "feat(import): añadir ImportService con whitelist de extensiones"
```

---

## Task 7: Crear el diálogo de progreso `ImportProgressDialog`

**Files:**
- Create: `Views/ImportProgressDialog.xaml`
- Create: `Views/ImportProgressDialog.xaml.cs`
- Modify: `QuillMD.csproj` (asegurar compilación del Views/)

- [ ] **Step 1: Crear la carpeta `Views/`**

```bash
mkdir -p Views
```

- [ ] **Step 2: Crear `Views/ImportProgressDialog.xaml`**

```xml
<Window x:Class="QuillMD.Views.ImportProgressDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Importando"
        Width="420" Height="160"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize"
        ShowInTaskbar="False"
        WindowStyle="ToolWindow">
    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <TextBlock x:Name="StatusText"
                   Grid.Row="0"
                   Text="Convirtiendo…"
                   FontSize="14"
                   TextTrimming="CharacterEllipsis" />

        <ProgressBar Grid.Row="1"
                     Margin="0,12,0,0"
                     Height="6"
                     IsIndeterminate="True" />

        <Button x:Name="CancelButton"
                Grid.Row="3"
                Content="Cancelar"
                Width="100"
                Height="28"
                HorizontalAlignment="Right"
                Click="CancelButton_Click" />
    </Grid>
</Window>
```

- [ ] **Step 3: Crear `Views/ImportProgressDialog.xaml.cs`**

```csharp
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
```

- [ ] **Step 4: Verificar que compila**

```bash
dotnet build
```

Expected: compila sin errores; la carpeta `Views/` se detecta automáticamente por `Microsoft.NET.Sdk` con `<UseWPF>true</UseWPF>`.

- [ ] **Step 5: Commit**

```bash
git add Views/ImportProgressDialog.xaml Views/ImportProgressDialog.xaml.cs
git commit -m "feat(import): añadir ImportProgressDialog — modal bloqueante con cancelación"
```

---

## Task 8: Extender `TabModel` con `SuggestedSavePath`

**Files:**
- Modify: `MainWindow.xaml.cs` (clase `TabModel`, línea ~1672)

- [ ] **Step 1: En `MainWindow.xaml.cs`, localizar la clase `TabModel` (línea ~1672) y añadir la propiedad**

Dentro de la clase `TabModel`, **después** de la propiedad `IsDirty`, añadir:

```csharp
        public string? SuggestedSavePath { get; set; }
```

El archivo resulta así (fragmento):

```csharp
        public bool IsDirty
        {
            get => _isDirty;
            set { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); }
        }

        public string? SuggestedSavePath { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
```

- [ ] **Step 2: Verificar que compila**

```bash
dotnet build
```

- [ ] **Step 3: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat(import): añadir TabModel.SuggestedSavePath para pestañas importadas"
```

---

## Task 9: Modificar `NewTab` y `SaveAs` para usar `SuggestedSavePath`

**Files:**
- Modify: `MainWindow.xaml.cs` (`NewTab` línea ~345, `SaveAs` línea ~505)

- [ ] **Step 1: Modificar la firma y cuerpo de `NewTab` (línea ~345)**

Reemplazar:

```csharp
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
```

Por:

```csharp
        private TabModel NewTab(string? filePath = null, string? content = null, string? suggestedSavePath = null, bool markDirty = false)
        {
            App.Log($"NewTab: filePath={filePath ?? "(new)"} suggested={suggestedSavePath ?? "(none)"}");
            var doc = new MarkdownDocument
            {
                FilePath = filePath ?? string.Empty,
                Content = content ?? string.Empty,
                IsDirty = markDirty
            };

            var tab = new TabModel
            {
                Document = doc,
                TabTitle = string.IsNullOrEmpty(suggestedSavePath)
                    ? doc.FileName
                    : System.IO.Path.GetFileNameWithoutExtension(suggestedSavePath) + (markDirty ? " •" : ""),
                IsActive = false,
                IsDirty = markDirty,
                SuggestedSavePath = suggestedSavePath
            };

            Tabs.Add(tab);
            ActivateTab(tab);
            return tab;
        }
```

Nota: ahora retorna `TabModel` para que `ImportService` pueda obtener la pestaña creada. El resto de llamadas existentes a `NewTab()` siguen funcionando (firma retrocompatible).

- [ ] **Step 2: Modificar `SaveAs` (línea ~505) para usar `SuggestedSavePath`**

Reemplazar:

```csharp
        private void SaveAs()
        {
            if (_activeTab == null) return;
            string? path = FileService.SaveFileAs(_activeTab.Document.IsNewFile ? null : _activeTab.Document.FilePath);
            if (path == null) return;
```

Por:

```csharp
        private void SaveAs()
        {
            if (_activeTab == null) return;

            string? suggested;
            if (!_activeTab.Document.IsNewFile)
                suggested = _activeTab.Document.FilePath;
            else
                suggested = _activeTab.SuggestedSavePath;  // null si no hay sugerencia

            string? path = FileService.SaveFileAs(suggested);
            if (path == null) return;
```

- [ ] **Step 3: En el bloque que sigue dentro de `SaveAs`, limpiar `SuggestedSavePath` tras guardar**

Localizar (dentro del mismo `SaveAs`):

```csharp
            if (FileService.WriteFile(path, Editor.Text))
            {
                _activeTab.Document.FilePath = path;
                _activeTab.Document.Content = Editor.Text;
                _activeTab.IsDirty = false;
                _activeTab.TabTitle = _activeTab.Document.FileName;
                AddToRecent(path);
                UpdateTitle();
            }
```

Reemplazar por:

```csharp
            if (FileService.WriteFile(path, Editor.Text))
            {
                _activeTab.Document.FilePath = path;
                _activeTab.Document.Content = Editor.Text;
                _activeTab.IsDirty = false;
                _activeTab.SuggestedSavePath = null;
                _activeTab.TabTitle = _activeTab.Document.FileName;
                AddToRecent(path);
                UpdateTitle();
            }
```

- [ ] **Step 4: Verificar que compila y no rompe el flujo de Save existente**

```bash
dotnet build
```

Abrir un archivo `.md` existente, modificarlo, `Ctrl+S` → debe guardar. Crear un archivo nuevo, `Ctrl+S` → debe abrir SaveAs sin sugerencia. (Esto es smoke manual, hacerlo antes de commitear.)

- [ ] **Step 5: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat(import): propagar SuggestedSavePath desde NewTab a SaveAs"
```

---

## Task 10: Añadir `ImportDocument()` y el comando

**Files:**
- Modify: `MainWindow.xaml.cs`

- [ ] **Step 1: Añadir declaración del comando cerca del resto (línea ~77, junto a `InsertTableCommand`)**

Añadir esta línea:

```csharp
        public ICommand ImportCommand { get; }
```

- [ ] **Step 2: Inicializar el comando en el constructor de `MainWindow`**

Dentro del constructor, en el bloque que inicializa los demás comandos (cerca de línea ~85), añadir:

```csharp
            ImportCommand = new RelayCommand(async () => await ImportDocument());
```

- [ ] **Step 3: Añadir el método `ImportDocument` (antes o después de `OpenFile`, línea ~437)**

```csharp
        private async System.Threading.Tasks.Task ImportDocument()
        {
            App.Log("ImportDocument: showing dialog");

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importar documento",
                Filter = QuillMD.Services.ImportService.OpenFileDialogFilter,
                FilterIndex = 1
            };
            if (dialog.ShowDialog() != true) { App.Log("ImportDocument: cancelled"); return; }

            string path = dialog.FileName;
            await ImportFromPath(path);
        }

        private async System.Threading.Tasks.Task ImportFromPath(string path)
        {
            App.Log($"ImportFromPath: {path}");

            if (!QuillMD.Services.ImportService.IsImportable(path))
            {
                MessageBox.Show(
                    $"El formato de \"{System.IO.Path.GetFileName(path)}\" no es importable.",
                    "Formato no soportado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cts = new System.Threading.CancellationTokenSource();
            var progress = new QuillMD.Views.ImportProgressDialog(path, cts) { Owner = this };

            QuillMD.Services.ConversionResult? result = null;
            progress.Loaded += async (_, _) =>
            {
                try
                {
                    result = await QuillMD.Services.MarkItDownService.ConvertAsync(path, cts.Token);
                }
                finally
                {
                    progress.AutoClose();
                }
            };

            progress.ShowDialog();

            if (result == null || result.Status == QuillMD.Services.ConversionStatus.Cancelled)
            {
                App.Log("ImportFromPath: cancelled");
                return;
            }

            if (result.Status != QuillMD.Services.ConversionStatus.Success)
            {
                App.Log($"ImportFromPath: failed — {result.Status}");
                MessageBox.Show(
                    result.ErrorMessage,
                    "Error al importar",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string suggestedPath = QuillMD.Services.ImportService.SuggestedMarkdownPath(path);
            App.Log($"ImportFromPath: success, creating tab (suggest={suggestedPath})");
            NewTab(filePath: null, content: result.Markdown, suggestedSavePath: suggestedPath, markDirty: true);
        }
```

- [ ] **Step 4: Verificar que compila**

```bash
dotnet build
```

- [ ] **Step 5: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat(import): añadir ImportDocument + ImportFromPath orquestando el flujo"
```

---

## Task 11: Cablear el menú "Archivo → Importar…"

**Files:**
- Modify: `MainWindow.xaml`

- [ ] **Step 1: Localizar el `MenuItem` "Abrir" en `MainWindow.xaml`**

Buscar (usar Ctrl+F en el editor o `grep`):

```
Command="{Binding OpenFileCommand}"
```

- [ ] **Step 2: Añadir el nuevo `MenuItem` inmediatamente después del "Abrir" existente**

```xml
<MenuItem Header="_Importar…"
          Command="{Binding ImportCommand}"
          InputGestureText="Ctrl+Shift+I" />
```

- [ ] **Step 3: Añadir el `KeyBinding` a la ventana (si `MainWindow.xaml` tiene `<Window.InputBindings>`, añadirlo ahí; si no, crearlo)**

Dentro de `<Window.InputBindings>`:

```xml
<KeyBinding Modifiers="Ctrl+Shift" Key="I" Command="{Binding ImportCommand}" />
```

- [ ] **Step 4: Verificar que compila y que el menú aparece**

```bash
dotnet run
```

Abrir menú Archivo → debe aparecer "Importar…" con atajo Ctrl+Shift+I. Click debe abrir el OpenFileDialog con el filtro de documentos.

- [ ] **Step 5: Probar un import real**

Con un PDF pequeño: Archivo → Importar… → seleccionar PDF → ver diálogo modal → ver pestaña nueva con Markdown + título `<nombre>.md •`.

Ctrl+S en esa pestaña → SaveAs se abre con el nombre sugerido en la carpeta original.

- [ ] **Step 6: Commit**

```bash
git add MainWindow.xaml
git commit -m "feat(import): añadir MenuItem Archivo → Importar… (Ctrl+Shift+I)"
```

---

## Task 12: Habilitar drag & drop

**Files:**
- Modify: `MainWindow.xaml` (atributos `AllowDrop` + handlers)
- Modify: `MainWindow.xaml.cs` (añadir `Window_DragOver` y `Window_Drop`)

- [ ] **Step 1: En `MainWindow.xaml`, añadir los atributos al `<Window>` raíz**

Añadir a los atributos del `<Window ...>`:

```
AllowDrop="True"
DragOver="Window_DragOver"
Drop="Window_Drop"
```

- [ ] **Step 2: En `MainWindow.xaml.cs`, añadir los handlers (cerca de los existentes de eventos de ventana, p. ej. al final de la región "File Operations")**

```csharp
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    string ext = System.IO.Path.GetExtension(files[0]).ToLowerInvariant();
                    bool isMd = ext == ".md" || ext == ".markdown" || ext == ".txt";
                    bool isImportable = QuillMD.Services.ImportService.IsImportable(files[0]);
                    if (isMd || isImportable)
                    {
                        e.Effects = DragDropEffects.Copy;
                        e.Handled = true;
                        return;
                    }
                }
            }
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;

            string path = files[0];
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

            if (ext == ".md" || ext == ".markdown" || ext == ".txt")
            {
                // Flujo clásico: abrir como texto
                var existing = Tabs.FirstOrDefault(t => t.Document.FilePath == path);
                if (existing != null) { ActivateTab(existing); return; }
                string? content = FileService.ReadFile(path);
                if (content == null) return;
                NewTab(path, content);
                AddToRecent(path);
                return;
            }

            if (QuillMD.Services.ImportService.IsImportable(path))
            {
                await ImportFromPath(path);
            }
        }
```

- [ ] **Step 3: Verificar que compila**

```bash
dotnet build
```

- [ ] **Step 4: Smoke test drag & drop**

Ejecutar `dotnet run`, arrastrar:
- Un `.md` a la ventana → se abre como texto (flujo existente).
- Un `.pdf` a la ventana → se lanza el import.
- Un `.exe` a la ventana → cursor muestra "prohibido" y no hace nada.

- [ ] **Step 5: Commit**

```bash
git add MainWindow.xaml MainWindow.xaml.cs
git commit -m "feat(import): habilitar drag & drop con detección de formato"
```

---

## Task 13: Enlace "Avisos de terceros" en menú Ayuda

**Files:**
- Modify: `MainWindow.xaml` (añadir MenuItem en menú Ayuda)
- Modify: `MainWindow.xaml.cs` (añadir comando + handler)

- [ ] **Step 1: Declarar el comando en `MainWindow.xaml.cs` (junto a `ShowAboutCommand`)**

```csharp
        public ICommand ShowThirdPartyNoticesCommand { get; }
```

- [ ] **Step 2: Inicializar el comando en el constructor**

```csharp
            ShowThirdPartyNoticesCommand = new RelayCommand(ShowThirdPartyNotices);
```

- [ ] **Step 3: Añadir el método `ShowThirdPartyNotices`**

```csharp
        private void ShowThirdPartyNotices()
        {
            string path = System.IO.Path.Combine(AppContext.BaseDirectory, "markitdown", "THIRD-PARTY-NOTICES.md");
            if (!System.IO.File.Exists(path))
            {
                MessageBox.Show(
                    "No se encontró THIRD-PARTY-NOTICES.md.\n\nSe esperaba en:\n" + path,
                    "Avisos de terceros",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir el archivo:\n{ex.Message}",
                    "Avisos de terceros", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
```

- [ ] **Step 4: En `MainWindow.xaml`, añadir el `MenuItem` dentro del menú Ayuda (junto al "Acerca de")**

```xml
<MenuItem Header="_Avisos de terceros…"
          Command="{Binding ShowThirdPartyNoticesCommand}" />
```

- [ ] **Step 5: Verificar que compila y que el menú funciona**

```bash
dotnet run
```

Menú Ayuda → Avisos de terceros → debe abrir el `.md` con el visor por defecto.

- [ ] **Step 6: Commit**

```bash
git add MainWindow.xaml MainWindow.xaml.cs
git commit -m "feat(import): añadir acceso a Avisos de terceros desde menú Ayuda"
```

---

## Task 14: Actualizar documentación

**Files:**
- Modify: `README.md`
- Modify: `HELP.md`

- [ ] **Step 1: En `README.md`, en la sección "Características" añadir bullet en "Importación" (crear subsección si no existe, justo antes de "Exportación")**

```markdown
### Importación
- Importar PDF, DOCX, PPTX, XLSX/XLS, HTML, EPUB, CSV, JSON, XML, ZIP, Outlook `.msg`
- Menú `Archivo → Importar…` o arrastrar archivo sobre la ventana
- Conversión a Markdown vía [markitdown](https://github.com/microsoft/markitdown) de Microsoft (empaquetado con la aplicación)
```

- [ ] **Step 2: En `README.md`, al final, antes de "Licencia", añadir sección nueva:**

```markdown
## Licencias de terceros

QuillMD incluye [markitdown](https://github.com/microsoft/markitdown) (MIT, Microsoft Corporation) y sus dependencias transitivas para la función de importación. El archivo `THIRD-PARTY-NOTICES.md` en la distribución contiene los avisos completos de todas las dependencias empaquetadas (markitdown, pdfminer.six, pdfplumber, mammoth, lxml, python-pptx, pandas, openpyxl, magika y otras). También accesible desde el menú `Ayuda → Avisos de terceros…`.
```

- [ ] **Step 3: En `HELP.md`, añadir sección "Importar documentos"**

```markdown
## Importar documentos

QuillMD puede importar formatos no-Markdown y convertirlos a Markdown:

- **PDF** (`.pdf`)
- **Word** (`.docx`)
- **PowerPoint** (`.pptx`)
- **Excel** (`.xlsx`, `.xls`)
- **HTML** (`.html`, `.htm`)
- **EPUB** (`.epub`)
- **Outlook** (`.msg`)
- **Datos estructurados** (`.csv`, `.json`, `.xml`)
- **ZIP** (`.zip` — itera por el contenido)

### Cómo importar

- **Menú:** `Archivo → Importar…` (atajo `Ctrl+Shift+I`)
- **Drag & drop:** arrastra el archivo a la ventana de QuillMD

La conversión abre el documento convertido en una pestaña nueva sin guardar. Al pulsar `Ctrl+S` la primera vez, se sugiere guardar como `<nombre-original>.md` en la carpeta del archivo fuente.

### Limitaciones

- Las imágenes embebidas no se extraen a archivos; markitdown genera placeholders o las omite según el formato.
- Transcripción de audio y vídeos de YouTube no están disponibles en v1.
- Timeout por defecto: 60 segundos por conversión. Archivos muy grandes pueden abortarse.
```

- [ ] **Step 4: Commit**

```bash
git add README.md HELP.md
git commit -m "docs: documentar importación de documentos y licencias de terceros"
```

---

## Task 15: Smoke tests manuales (checklist de release)

Ejecutar esta lista **antes de publicar v1.1.0**. Usar archivos reales, no de juguete.

### Preparación

- [ ] `./tools/markitdown-bundle/build.ps1` ejecutado sin errores.
- [ ] `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:TrimMode=partial` genera ejecutable.
- [ ] `bin/Release/.../publish/markitdown/markitdown.exe` y `THIRD-PARTY-NOTICES.md` presentes en el output.

### Flujo feliz

- [ ] Importar `informe.pdf` textual (≥ 5 páginas) → pestaña con contenido legible.
- [ ] Importar `curriculum.docx` con tablas e imágenes → contenido + placeholders de imagen.
- [ ] Importar `presentacion.pptx` → una sección Markdown por slide.
- [ ] Importar `datos.xlsx` multi-hoja → una tabla por hoja.
- [ ] Importar `noticia.html` local → Markdown razonable sin CSS/JS.
- [ ] Importar `libro.epub` → contenido paginado en Markdown.
- [ ] Importar `mensaje.msg` (Outlook) → cabeceras + cuerpo.

### Triggers UI

- [ ] Menú `Archivo → Importar…` abre el diálogo con el filtro correcto.
- [ ] Atajo `Ctrl+Alt+I` abre el mismo diálogo (NOTA: el plan inicial proponía `Ctrl+Shift+I` pero esa combinación ya estaba en uso para Insertar imagen; se cambió durante la implementación).
- [ ] Drag & drop de PDF → se importa.
- [ ] Drag & drop de `.md` → se abre como texto (no regresión).
- [ ] Drag & drop de `.exe` → cursor "prohibido", no hace nada.

### Post-importación

- [ ] Pestaña importada aparece con indicador de *dirty* (•).
- [ ] `Ctrl+S` en pestaña importada → SaveAs con nombre `<original>.md` en carpeta del original.
- [ ] Tras guardar, desaparece el indicador de *dirty* y el título cambia al nombre real.

### Errores

- [ ] Cancelar durante conversión de PDF grande → proceso termina, sin pestaña nueva, sin crash.
- [ ] Timeout forzado (editar temporalmente `MarkItDownService.DefaultTimeoutSeconds` a 2) con un PDF grande → mensaje "superó 2 s y se abortó".
- [ ] Renombrar `markitdown/markitdown.exe` a `.bak` y reintentar → mensaje "Falta markitdown.exe".
- [ ] Importar archivo corrupto (PDF con bytes basura) → mensaje de error con preview de stderr.

### Licencias

- [ ] `Ayuda → Avisos de terceros…` abre `THIRD-PARTY-NOTICES.md`.
- [ ] ZIP de release contiene `LICENSE`, `THIRD-PARTY-NOTICES.md` y `markitdown/markitdown.exe`.

### Regresión

- [ ] Abrir archivo `.md` existente → flujo igual que antes.
- [ ] Crear archivo nuevo, escribir, `Ctrl+S` → SaveAs sin sugerencia pre-rellenada.
- [ ] Exportar a HTML / PDF sigue funcionando.

---

## Task 16: Tag de release

- [ ] **Step 1: Si todos los smoke tests pasan, crear el tag**

```bash
git tag -a v1.1.0 -m "v1.1.0: importación de documentos vía markitdown"
```

- [ ] **Step 2: (Manual, opcional) Subir release a GitHub**

Subir el ZIP con `QuillMD.exe`, `LICENSE`, `THIRD-PARTY-NOTICES.md` y `markitdown/markitdown.exe`.

---

## Resumen del impacto

- **Archivos nuevos en repo:** 10 (bundle Python + servicios C# + diálogo WPF + .gitkeep)
- **Archivos modificados:** 4 (`.gitignore`, `QuillMD.csproj`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `README.md`, `HELP.md`)
- **Binarios distribuidos adicionales (no en git):** ~100 MB (`markitdown.exe` + notices)
- **Commits previstos:** ~14 (uno por task, ~1-3 por algunas)
- **Tiempo estimado:** 1-2 días (build del bundle + implementación C# + smoke tests)
