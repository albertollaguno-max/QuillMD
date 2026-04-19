"""Entry point for PyInstaller bundling of markitdown CLI."""
# Resolved at build time inside the Windows venv created by build.ps1.
from markitdown.__main__ import main  # pyright: ignore[reportMissingImports]

if __name__ == "__main__":
    main()
