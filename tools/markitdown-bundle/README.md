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
