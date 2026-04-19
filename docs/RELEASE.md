# Guía de release de QuillMD

Cómo cortar y publicar una nueva versión. Este documento existe porque en varios releases se perdieron pasos (versión sin bumpear, artefactos de runtime colándose en el ZIP, etc.); es la receta comprobada.

## Convención de versionado

Semver: `vMAJOR.MINOR.PATCH`.

- **PATCH** (p. ej. `v1.1.1`): fix bugs sin cambios de UX ni de formatos soportados.
- **MINOR** (p. ej. `v1.2.0`): nuevas funciones compatibles (nuevo formato de importación, nuevo atajo, nuevo menú).
- **MAJOR** (p. ej. `v2.0.0`): ruptura de compatibilidad, cambios grandes de arquitectura.

## 1. Previo al tag

- [ ] Rama: todo el trabajo mergeado a `main` (con `--no-ff` si viene de feature branch multi-commit).
- [ ] `dotnet build` desde WSL: `powershell.exe -Command "cd F:\\PlannamTypora; dotnet build --nologo"` → **0 warnings / 0 errores**.
- [ ] Smoke test manual en Windows de la feature nueva Y de los flujos que pueda afectar.
- [ ] Si se tocó el bundle Python:
  ```powershell
  cd F:\PlannamTypora\tools\markitdown-bundle
  .\build.ps1
  ```
  (5-10 min; regenera `markitdown.exe` y `THIRD-PARTY-NOTICES.md` en `Resources/markitdown/`.)

## 2. Bump de versión

Tres ubicaciones; **las tres** o el diálogo *Acerca de* dará una versión distinta al tag:

1. `QuillMD.csproj`:
   ```xml
   <AssemblyVersion>X.Y.0.0</AssemblyVersion>
   <FileVersion>X.Y.0.0</FileVersion>
   ```
2. `MainWindow.xaml.cs`, método `ShowAbout`: `"QuillMD vX.Y.Z\n..."`.
3. Commit de bump: `chore(release): preparar vX.Y.Z`.

## 3. Tag y push

```powershell
# En main, con el commit de bump como HEAD
git tag -a vX.Y.Z -m "vX.Y.Z — <titular corto>

<2-4 bullets de lo que cambia>"

git push origin main
git push origin vX.Y.Z
```

## 4. Publicar artefactos

`dotnet publish` genera un `publish/` que **incluye basura runtime** si la app se ejecutó antes desde esa carpeta — hay que limpiarla antes de zipear.

```powershell
# 4.1 Matar instancias abiertas (bloquean QuillMD.exe)
Get-Process QuillMD -EA SilentlyContinue | Stop-Process -Force

# 4.2 Publish self-contained single-file
cd F:\PlannamTypora
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:TrimMode=partial

# 4.3 Limpiar basura del publish/
$publish = "bin\Release\net9.0-windows\win-x64\publish"
Remove-Item -Recurse -Force $publish\QuillMD.exe.WebView2 -EA SilentlyContinue
Remove-Item -Force $publish\QuillMD_crash.log -EA SilentlyContinue
Remove-Item -Force $publish\QuillMD.pdb -EA SilentlyContinue

# 4.4 Copiar LICENSE al publish/ (dotnet no lo hace solo)
Copy-Item LICENSE $publish -Force

# 4.5 Zipear
Compress-Archive -Path (Join-Path $publish '*') `
  -DestinationPath QuillMD-vX.Y.Z-win-x64.zip -Force
```

### Qué debe contener el ZIP

Verifica antes de subir:

- `QuillMD.exe` (~150-170 MB, single-file self-contained)
- DLLs nativas WPF: `D3DCompiler_47_cor3.dll`, `wpfgfx_cor3.dll`, `PresentationNative_cor3.dll`, `PenImc_cor3.dll`, `vcruntime140_cor3.dll`
- `WebView2Loader.dll` (raíz) + `runtimes/win-x64/native/WebView2Loader.dll`
- `LICENSE`
- `markitdown/markitdown.exe` (~67 MB)
- `markitdown/THIRD-PARTY-NOTICES.md` (~220 KB — formato por secciones, no tabla)
- XMLs de IntelliSense de WebView2 (~1 MB total; inofensivos, pueden quedarse)

### Qué NO debe contener

- `QuillMD.exe.WebView2/` (datos de runtime: cookies, cache, extension state)
- `QuillMD_crash.log` (crash logs de testing)
- `*.pdb` (símbolos de debug)

## 5. Crear el Release en GitHub

`gh` CLI no está instalado; proceso manual:

1. https://github.com/albertollaguno-max/QuillMD/releases
2. **Draft a new release**.
3. *Choose a tag* → seleccionar `vX.Y.Z` (ya empujado).
4. Title: `vX.Y.Z — <titular>`.
5. Descripción: bullets de los cambios, enlace a PR/issues si aplican.
6. Arrastrar `QuillMD-vX.Y.Z-win-x64.zip`.
7. **Publish release**.

## 6. Post-release

- [ ] Verificar que el release aparece público en `/releases`.
- [ ] Borrar el ZIP local del working directory si ya no lo necesitas.
- [ ] Si se generaron ficheros temporales de diagnóstico (`F:\temp_*`), limpiarlos.

---

## Flujo de trabajo para añadir features/fixes

Estilo habitual (v1.1.0 se hizo así):

1. **Brainstorming + spec**: documento de diseño en `docs/plans/YYYY-MM-DD-<tema>-design.md`. Decisiones cerradas con el usuario antes de tocar código.
2. **Plan**: `docs/plans/YYYY-MM-DD-<tema>-plan.md` con tasks/batches ejecutables, cada uno con código y commits previstos.
3. **Implementación**: rama `feat/<tema>` o `fix/<tema>`. Commits pequeños, mensajes en español ("feat(x): ...", "fix(x): ...", "docs: ...", "chore: ...").
4. **Verificación**: `dotnet build` verde + smoke test manual. QuillMD **no tiene proyecto de tests**; no se añade xUnit sin pedirlo explícitamente.
5. **Merge**: `git merge --no-ff feat/<tema>` a main, con mensaje de merge que resuma la release.
6. **Release**: seguir los pasos 1-6 de arriba.

Referencias internas:
- `docs/plans/` — specs y planes de implementación anteriores (ejemplos de formato).
- `tools/markitdown-bundle/README.md` — cómo regenerar `markitdown.exe`.
- `HELP.md` — documentación end-user que hay que actualizar con cada feature visible.
- `README.md` — sección "Características" + "Licencias de terceros" si se añaden dependencias bundle-adas.
