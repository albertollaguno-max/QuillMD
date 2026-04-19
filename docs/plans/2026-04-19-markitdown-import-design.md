# Diseño: Importación de documentos vía markitdown

**Fecha:** 2026-04-19
**Estado:** Aprobado por el usuario (pendiente de plan de implementación)
**Autor:** Alberto (Plannam) + asistente

## Objetivo

Permitir que QuillMD importe documentos en formatos no-Markdown (PDF, DOCX, PPTX, XLSX, HTML, EPUB, CSV, JSON, XML, `.msg` de Outlook, ZIP) convirtiéndolos a Markdown mediante [markitdown](https://github.com/microsoft/markitdown) de Microsoft, que se distribuye empaquetado junto a la aplicación sin requerir Python en el sistema del usuario.

## Decisiones cerradas

| Tema | Decisión |
|---|---|
| Librería de conversión | markitdown de Microsoft (Python, MIT) |
| Empaquetado | PyInstaller `--onefile` → `markitdown.exe` autocontenido |
| Subset de formatos | `markitdown[pdf,docx,pptx,xlsx,outlook]` (excluye audio y YouTube) |
| Triggers UI | Menú `Archivo → Importar…` + drag & drop sobre la ventana |
| Salida | Nueva pestaña sin guardar; el *Save As* sugiere `<nombre-original>.md` en la carpeta original |
| Feedback | Diálogo modal bloqueante con spinner + botón Cancelar; timeout 60 s |
| Errores | `MessageBox` con primeras 500 chars de stderr; sin retries ni fallbacks |

## Arquitectura y empaquetado

### Estructura del repositorio

```
tools/
└── markitdown-bundle/
    ├── pyproject.toml        # markitdown[pdf,docx,pptx,xlsx,outlook]==<version>
    ├── build.ps1             # script Windows que compila markitdown.exe y genera notices
    └── .gitignore            # excluye venv, dist, build, *.exe

Resources/
└── markitdown/               # artefactos bundle-ados (no en git, se rellena tras build.ps1)
    ├── markitdown.exe
    └── THIRD-PARTY-NOTICES.md
```

- **No se vendoriza el código fuente de markitdown en este repo.** El `pyproject.toml` fija la versión y `pip install` la resuelve en cada build del bundle.
- El `.csproj` incluye `Resources/markitdown/**` con `<None CopyToOutputDirectory="PreserveNewest" />` para que se copien junto a `QuillMD.exe` en `dotnet publish`.
- Actualizar markitdown implica: bumpear versión en `pyproject.toml` → ejecutar `build.ps1` → commitear el cambio de versión (no los binarios).

### Script `build.ps1` (resumen funcional)

1. Crear venv limpio en `tools/markitdown-bundle/.venv`.
2. `pip install -e . pyinstaller pip-licenses`.
3. `pyinstaller --onefile --name markitdown -c <entry-point>` (usa el CLI de markitdown como entry).
4. `pip-licenses --from=mixed --with-license-file --with-urls --format=markdown --output-file ../../Resources/markitdown/THIRD-PARTY-NOTICES.md`.
5. Copiar `dist/markitdown.exe` a `Resources/markitdown/markitdown.exe`.
6. Verificar que el `.exe` convierte un PDF de prueba (smoke test).

### Componentes nuevos en C#

| Archivo | Responsabilidad |
|---|---|
| `Services/MarkItDownService.cs` | Localiza `markitdown.exe`, lanza `Process`, captura stdout/stderr, aplica timeout y cancelación. API: `Task<ConversionResult> ConvertAsync(string path, CancellationToken ct)`. |
| `Services/ImportService.cs` | Orquesta la importación: valida extensión, levanta el diálogo, llama a `MarkItDownService`, crea la pestaña nueva. API: `Task ImportAsync(string path)`. |
| `Views/ImportProgressDialog.xaml` + `.cs` | Diálogo modal sobre `MainWindow`. Texto dinámico, spinner animado, botón Cancelar. Inyecta `CancellationTokenSource`. |
| Handlers en `MainWindow.xaml.cs` | `OnImportMenuClick` (abre `OpenFileDialog` con filtro), `OnDragOver` / `OnDrop` (detecta extensiones importables). |
| Entrada en `MainWindow.xaml` | Nuevo `MenuItem` *Importar…* bajo *Archivo*, con atajo `Ctrl+Shift+I`. |

### Whitelist de extensiones

```
.pdf .docx .pptx .xlsx .xls .msg .epub .html .htm .csv .json .xml .zip
```

Archivos con otras extensiones siguen la lógica actual (abrirse como texto si son `.md`/`.markdown`/`.txt`, ignorarse en otro caso).

## Flujo de datos

### Camino feliz

1. Usuario dispara import (menú o drop).
2. `ImportService.ImportAsync(path)`:
   - Valida que el archivo exista y su extensión esté en la whitelist.
   - Instancia `ImportProgressDialog`, lo muestra como modal con `Owner = MainWindow`.
   - Crea `CancellationTokenSource` y lo pasa al diálogo (para el botón Cancelar) y al servicio.
3. `MarkItDownService.ConvertAsync(path, ct)`:
   - Resuelve la ruta de `markitdown.exe` vía `AppContext.BaseDirectory + "markitdown/markitdown.exe"`.
   - Verifica existencia; si falta, retorna `ConversionResult.Failed("Falta markitdown.exe...")`.
   - Lanza `Process` con `ProcessStartInfo`:
     ```
     FileName = "<base>/markitdown/markitdown.exe"
     Arguments = "\"<path>\""
     RedirectStandardOutput = true
     RedirectStandardError  = true
     UseShellExecute        = false
     CreateNoWindow         = true
     StandardOutputEncoding = UTF-8
     StandardErrorEncoding  = UTF-8
     ```
   - Lee stdout y stderr de forma asíncrona en paralelo (`Task.WhenAll`).
   - Aplica `Process.WaitForExitAsync(ct)` con timeout interno de 60 s (configurable en `SettingsService` bajo `ImportTimeoutSeconds`).
4. Al obtener `ExitCode == 0` y stdout no vacío:
   - Se cierra el diálogo.
   - Se llama a `MainViewModel.CreateUnsavedTab(markdown, suggestedSavePath)` donde `suggestedSavePath = Path.ChangeExtension(originalPath, ".md")`.
   - La pestaña aparece marcada como *dirty* desde el inicio; el primer `Save As` arranca en la carpeta original con el nombre sugerido.

### Cancelación

- Botón *Cancelar* del diálogo → `cts.Cancel()`.
- Handler del servicio: `process.Kill(entireProcessTree: true)`; el diálogo se cierra; no se crea pestaña; no se muestra error.

### Timeout

- 60 s por defecto. Al expirar, misma ruta que cancelación **más** un `MessageBox` informativo: *"La conversión superó 60 s y se abortó."*

### Manejo de errores

| Causa | Detección | Mensaje al usuario |
|---|---|---|
| `markitdown.exe` no existe | Check previo en `MarkItDownService` | *"Falta markitdown.exe. Reinstala QuillMD."* |
| `ExitCode != 0` | Tras `WaitForExitAsync` | *"Error al convertir <archivo>:\n\n<primeras 500 chars de stderr>"* |
| Archivo corrupto / formato no soportado | Reflejado vía stderr | Mismo patrón que el anterior |
| Salida vacía (stdout == "") | Post-proceso en el servicio | *"El archivo no contiene contenido extraíble."* |
| Timeout (60 s) | Flag interno | *"Conversión abortada tras 60 s."* |
| Excepción inesperada del `Process` | `try/catch` en el servicio | Mensaje genérico + `ex.Message` |

Sin retries, sin fallbacks. Si falla, falla; el usuario reintenta manualmente.

### Imágenes embebidas

markitdown no extrae imágenes a archivos separados por defecto; genera placeholders o las omite. Para v1 se asume este comportamiento. Documentarlo en `HELP.md`. Una futura v2 podría extraer imágenes a una subcarpeta `<nombre>_assets/` — fuera de alcance.

## Licencias y distribución

### Problema

MIT, BSD y Apache 2.0 (licencias de markitdown y su árbol de dependencias) exigen que el texto de la licencia y el aviso de copyright viajen físicamente con el binario redistribuido. Un enlace al repositorio upstream **no** cumple estas licencias.

### Solución

1. **Generación automática de `THIRD-PARTY-NOTICES.md`** en `build.ps1` usando `pip-licenses`:
   ```
   pip-licenses --from=mixed --with-license-file --with-urls \
                --format=markdown --output-file THIRD-PARTY-NOTICES.md
   ```
   Produce un documento con nombre, versión, URL y **texto completo de la licencia** de cada paquete bundle-ado.

2. **En el ZIP de release** (`QuillMD-v<x.y.z>-win-x64.zip`) se incluyen:
   - `QuillMD.exe`
   - `LICENSE` (MIT de QuillMD, ya existe)
   - `THIRD-PARTY-NOTICES.md` (generado en cada rebuild del bundle)
   - `markitdown/markitdown.exe`

3. **UI** — en `Ayuda → Acerca de…` se añade un enlace *"Avisos de terceros"* que abre el `.md` local con el visor por defecto del sistema.

4. **README principal** — nueva sección *"Licencias de terceros"* con:
   > QuillMD incluye [markitdown](https://github.com/microsoft/markitdown) (MIT, Microsoft Corporation) y sus dependencias transitivas. Consulte `THIRD-PARTY-NOTICES.md` en la distribución para los avisos completos.

## Testing

### Pruebas manuales (smoke tests obligatorios antes de release)

- Importar PDF textual (`informe.pdf`) → pestaña nueva con contenido legible.
- Importar DOCX con tablas e imágenes → contenido + placeholders de imagen.
- Importar PPTX → una sección Markdown por slide.
- Importar XLSX multi-hoja → una tabla por hoja.
- Drag & drop de archivo importable → mismo resultado que el menú.
- Drag & drop de `.md` → sigue abriéndose como texto (no regresión).
- Cancelar a mitad de un PDF grande → proceso terminado, sin pestaña, sin crash.
- Importar archivo inexistente (vía menú) → mensaje claro de error.
- Renombrar temporalmente `markitdown.exe` → mensaje *"Falta markitdown.exe"*.

### Pruebas automatizadas

- Unit tests en `MarkItDownService` con un `IProcessRunner` mockeado: éxito, `ExitCode != 0`, timeout, salida vacía, cancelación.
- `ImportService` con `MarkItDownService` falso: validación de whitelist, sugerencia de `suggestedSavePath`.
- Sin tests de integración que lancen el `.exe` real (demasiado lentos para CI; cubierto manualmente).

## Fuera de alcance (v1)

- Transcripción de audio (formatos `.mp3`, `.wav`) y YouTube.
- Extracción de imágenes embebidas a archivos separados.
- Asociación de archivos en el instalador (click derecho → *Abrir con QuillMD*).
- Import diferencial / merge con pestaña existente.
- Detección inteligente del formato por contenido (hoy se decide por extensión).
- Actualización automática del bundle de markitdown desde la app.

## Estimación de tamaño

| Componente | Tamaño |
|---|---|
| QuillMD actual (single-file self-contained) | ~100 MB |
| `markitdown.exe` con `[pdf,docx,pptx,xlsx,outlook]` | ~100 MB |
| **Total distribuible (descomprimido)** | **~200 MB** |
| ZIP de release (comprimido) | ~70-80 MB |

## Próximos pasos

1. Revisión de este spec por el usuario.
2. Generar plan de implementación detallado (con la skill `superpowers:writing-plans`).
3. Ejecutar el plan (orden sugerido: script de build → `MarkItDownService` → `ImportService` → UI → licencias/notices → smoke tests).
