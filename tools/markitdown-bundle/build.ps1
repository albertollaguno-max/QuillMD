# build.ps1 - Genera markitdown.exe + THIRD-PARTY-NOTICES.md para QuillMD
# Uso: ./build.ps1

$ErrorActionPreference = "Stop"

$here = $PSScriptRoot
$resourcesDir = Join-Path $here "..\..\Resources\markitdown"
$venvPath = Join-Path $here ".venv"

Write-Host "=== markitdown-bundle build ===" -ForegroundColor Cyan

# 1. Asegurar Python 3.11+ disponible
$python = (Get-Command python -ErrorAction SilentlyContinue).Source
if (-not $python) { throw "Python 3.11+ no encontrado en PATH." }

$version = & $python --version
$versionParts = ($version -split ' ')[1] -split '\.'
$major = [int]$versionParts[0]
$minor = [int]$versionParts[1]
if ($major -lt 3 -or ($major -eq 3 -and $minor -lt 11)) {
    throw "Se requiere Python 3.11+. Encontrado: $version"
}
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
if ([string]::IsNullOrWhiteSpace($out)) { throw "Smoke test produjo salida vacía — el bundle puede estar roto." }
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
