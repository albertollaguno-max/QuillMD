"""Entry point for PyInstaller bundling of markitdown CLI."""
import sys

# Sin terminal, PyInstaller inicializa sys.stdout/stderr con un encoding
# limitado (cp1252 / ascii con errors=replace) antes de que las env vars
# PYTHONIOENCODING/PYTHONUTF8 tengan efecto. Reconfigurar a UTF-8 aquí
# garantiza que los acentos y el resto de Unicode salgan correctos.
if sys.stdout is not None:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # pyright: ignore[reportAttributeAccessIssue]
if sys.stderr is not None:
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")  # pyright: ignore[reportAttributeAccessIssue]

# Resolved at build time inside the Windows venv created by build.ps1.
from markitdown.__main__ import main  # pyright: ignore[reportMissingImports]

if __name__ == "__main__":
    main()
