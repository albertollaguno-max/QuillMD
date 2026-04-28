# Instancia única con reenvío de archivos — Plan de implementación

> **Para agentes:** SUB-SKILL REQUERIDA: Usa superpowers:subagent-driven-development (recomendado) o superpowers:executing-plans para implementar este plan tarea por tarea. Los pasos usan sintaxis de checkbox (`- [ ]`) para tracking.

**Goal:** Detectar instancia previa de QuillMD y, en lugar de abrir una ventana nueva, reenviarle las rutas pasadas por línea de comandos para que las abra como pestañas. Si la primera instancia no responde, degradar a arranque normal.

**Architecture:** Un módulo `Services/SingleInstance.cs` encapsula la lógica entera (mutex per-usuario + `NamedPipeServerStream`). `App.OnStartup` consulta `SingleInstance.TryAcquire`; si la respuesta es `ForwardedToFirst`, hace `Shutdown(0)` antes de mostrar ventana. La instancia ganadora arranca un loop async que escucha rutas (protocolo línea-delimitado UTF-8 con header `v1`), las marshaliza al `Dispatcher` UI y `MainWindow` las abre como pestañas con activación de ventana (restore + Topmost trick).

**Tech Stack:** WPF/.NET 9, `System.IO.Pipes` (BCL, sin NuGets nuevos), `System.Threading.Mutex`, `WindowsIdentity` para SID per-usuario.

**Spec:** `docs/superpowers/specs/2026-04-28-single-instance-design.md`

**Verificación:** Smoke tests manuales en Windows tras `dotnet build`. No hay framework de tests automatizados en QuillMD; cada tarea termina con un build verde + un commit. La verificación funcional la hace el usuario al final.

---

## Estructura de archivos

| Archivo | Cambio | Responsabilidad |
|---|---|---|
| `Services/SingleInstance.cs` | **Crear** | Mutex per-usuario, pipe server async, pipe client con timeout, evento `MessageReceived`. |
| `App.xaml.cs` | Modificar | Reemplazar `StartupFilePath` (string?) → `StartupFilePaths` (`IReadOnlyList<string>`). Llamar `SingleInstance.TryAcquire`. Suscribir `MessageReceived` → `FilesReceived`. Liberar en `OnExit`. |
| `MainWindow.xaml.cs` | Modificar | Constructor itera `StartupFilePaths`. Suscribirse a `App.FilesReceived` y, sobre el `Dispatcher`, traer al frente + abrir rutas. |

---

## Build helper

Cada tarea termina con un build desde WSL invocando `dotnet` en Windows:

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Compilación correcta. 0 Advertencia(s) 0 Errores`.

---

## Task 1: Soporte multi-archivo (`StartupFilePaths`)

**Files:**
- Modify: `App.xaml.cs:1-3` (añadir using), `App.xaml.cs:10` (propiedad), `App.xaml.cs:38-39` (lectura de args).
- Modify: `MainWindow.xaml.cs:162-179` (constructor, bloque de apertura de archivo de arranque).

- [ ] **Step 1: Añadir `using System.Linq;` en App.xaml.cs**

`App.xaml.cs` (líneas 1-3) actualmente tiene:

```csharp
using System.IO;
using System.Windows;
using System.Windows.Threading;
```

Añade `System.Linq` para usar `Where`:

```csharp
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
```

- [ ] **Step 2: Sustituir la propiedad `StartupFilePath` por `StartupFilePaths`**

En `App.xaml.cs:10`:

```csharp
public static string? StartupFilePath { get; private set; }
```

Reemplaza por:

```csharp
public static IReadOnlyList<string> StartupFilePaths { get; private set; } = Array.Empty<string>();
```

- [ ] **Step 3: Sustituir la lectura de args en `OnStartup`**

En `App.xaml.cs:38-39`:

```csharp
// Capture file path from command-line arguments
if (e.Args.Length > 0 && File.Exists(e.Args[0]))
    StartupFilePath = e.Args[0];
```

Reemplaza por:

```csharp
// Capture all valid file paths from command-line arguments
StartupFilePaths = e.Args.Where(File.Exists).ToArray();
```

- [ ] **Step 4: Sustituir el bloque de apertura en `MainWindow.xaml.cs`**

`MainWindow.xaml.cs:162-179` actualmente es:

```csharp
            if (!string.IsNullOrEmpty(App.StartupFilePath))
            {
                App.Log($"Opening startup file: {App.StartupFilePath}");
                string? content = FileService.ReadFile(App.StartupFilePath);
                if (content != null)
                {
                    _ = NewTab(App.StartupFilePath, content);
                    AddToRecent(App.StartupFilePath);
                }
                else
                {
                    _ = NewTab();
                }
            }
            else
            {
                _ = NewTab();
            }
```

Reemplaza por:

```csharp
            if (App.StartupFilePaths.Count > 0)
            {
                foreach (var path in App.StartupFilePaths)
                {
                    App.Log($"Opening startup file: {path}");
                    string? content = FileService.ReadFile(path);
                    if (content != null)
                    {
                        _ = NewTab(path, content);
                        AddToRecent(path);
                    }
                }
                // Defensive: if every file failed to read, ensure at least a blank tab
                if (Tabs.Count == 0) _ = NewTab();
            }
            else
            {
                _ = NewTab();
            }
```

- [ ] **Step 5: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Compilación correcta. 0 Advertencias 0 Errores.`

- [ ] **Step 6: Commit**

```bash
git add App.xaml.cs MainWindow.xaml.cs
git commit -m "refactor(startup): soporte multi-archivo en e.Args (StartupFilePaths)"
```

(Sin Co-Authored-By trailer.)

---

## Task 2: Esqueleto `Services/SingleInstance.cs` + wiring en `App`

**Files:**
- Create: `Services/SingleInstance.cs`.
- Modify: `App.xaml.cs:OnStartup` para llamar `TryAcquire` y `OnExit` para `Release`.

Esta tarea introduce la API y el wiring **sin lógica real**: `TryAcquire` siempre devuelve `FirstInstance` y no hace nada con el mutex/pipe. Garantiza que el resto del código se compile y que el flujo de control esté correcto antes de meterle complejidad.

- [ ] **Step 1: Crear `Services/SingleInstance.cs`**

Archivo nuevo:

```csharp
using System.Collections.Generic;
using System.IO;

namespace QuillMD.Services
{
    public enum SingleInstanceResult
    {
        FirstInstance,
        ForwardedToFirst,
        FirstUnreachable
    }

    public static class SingleInstance
    {
        public static event Action<IReadOnlyList<string>>? MessageReceived;

        public static SingleInstanceResult TryAcquire(string[] args, out IReadOnlyList<string> validatedArgs)
        {
            validatedArgs = args.Where(File.Exists).ToArray();
            // Skeleton: always behave as first instance until Task 3+ wire mutex and pipe.
            return SingleInstanceResult.FirstInstance;
        }

        public static void Release()
        {
            // Skeleton: no-op until Task 3 introduces the mutex.
        }

        // Helper used by App once Task 5 subscribes; kept here so it compiles in the skeleton.
        internal static void RaiseMessageReceived(IReadOnlyList<string> paths)
        {
            MessageReceived?.Invoke(paths);
        }
    }
}
```

Necesita `using System.Linq;` (para `Where`). Añádelo arriba si Visual Studio no lo sugiere, o verifica que en .NET 9 con `ImplicitUsings=enable` ya esté. **Confirma compilando.**

- [ ] **Step 2: Llamar `TryAcquire` desde `App.OnStartup`**

En `App.xaml.cs`, dentro de `OnStartup`, localiza la línea que añadiste en Task 1:

```csharp
            // Capture all valid file paths from command-line arguments
            StartupFilePaths = e.Args.Where(File.Exists).ToArray();
```

Sustituye esas dos líneas por:

```csharp
            // Single-instance gate (filtra args y decide si arrancamos o reenviamos).
            var siResult = QuillMD.Services.SingleInstance.TryAcquire(e.Args, out var validated);
            if (siResult == QuillMD.Services.SingleInstanceResult.ForwardedToFirst)
            {
                Log("Forwarded args to existing instance, exiting.");
                Shutdown(0);
                return;
            }
            if (siResult == QuillMD.Services.SingleInstanceResult.FirstUnreachable)
            {
                Log("WARN: previous instance unreachable, starting as degraded primary.");
            }
            StartupFilePaths = validated;
```

Asegúrate de que el `using System.Linq;` original en `App.xaml.cs` se mantiene (lo añadiste en Task 1).

- [ ] **Step 3: Liberar en `OnExit`**

Sobreescribe `OnExit` en `App.xaml.cs`. Si no existe `OnExit` aún (es lo más probable), añadirlo justo después de `OnStartup`:

```csharp
        protected override void OnExit(ExitEventArgs e)
        {
            QuillMD.Services.SingleInstance.Release();
            base.OnExit(e);
        }
```

- [ ] **Step 4: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Compilación correcta. 0 Errores`.

- [ ] **Step 5: Commit**

```bash
git add Services/SingleInstance.cs App.xaml.cs
git commit -m "feat(single-instance): esqueleto SingleInstance + wiring en App"
```

---

## Task 3: Implementar mutex y pipe server

**Files:**
- Modify: `Services/SingleInstance.cs` (sustituir el skeleton de `TryAcquire` y `Release` por la lógica real del lado server; el cliente queda para Task 4).

- [ ] **Step 1: Sustituir el contenido de `Services/SingleInstance.cs`**

Reemplaza el archivo completo por:

```csharp
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QuillMD.Services
{
    public enum SingleInstanceResult
    {
        FirstInstance,
        ForwardedToFirst,
        FirstUnreachable
    }

    public static class SingleInstance
    {
        private const string ProtocolHeader = "v1";

        private static Mutex? _mutex;
        private static CancellationTokenSource? _cts;
        private static Task? _serverTask;
        private static string? _pipeName;

        public static event Action<IReadOnlyList<string>>? MessageReceived;

        public static SingleInstanceResult TryAcquire(string[] args, out IReadOnlyList<string> validatedArgs)
        {
            validatedArgs = args.Where(File.Exists).ToArray();

            var sid = WindowsIdentity.GetCurrent().User?.Value ?? "anon";
            var mutexName = $@"Local\QuillMD-SingleInstance-{sid}";
            _pipeName = $"QuillMD-Pipe-{sid}";

            var mutex = new Mutex(initiallyOwned: true, mutexName, out bool createdNew);
            if (createdNew)
            {
                _mutex = mutex;
                _cts = new CancellationTokenSource();
                _serverTask = Task.Run(() => RunServerLoop(_pipeName, _cts.Token));
                return SingleInstanceResult.FirstInstance;
            }

            // Not the first instance — discard the unowned mutex (do not Release: not ours).
            mutex.Dispose();
            // Pipe client logic lands in Task 4. For now, treat as unreachable so we degrade.
            return SingleInstanceResult.FirstUnreachable;
        }

        public static void Release()
        {
            try { _cts?.Cancel(); } catch { }
            try { _serverTask?.Wait(500); } catch { }
            try { _mutex?.ReleaseMutex(); } catch { }
            try { _mutex?.Dispose(); } catch { }
            _cts = null;
            _serverTask = null;
            _mutex = null;
        }

        private static async Task RunServerLoop(string pipeName, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.In,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var raw = await reader.ReadToEndAsync().ConfigureAwait(false);
                    var lines = raw.Split('\n', StringSplitOptions.None)
                                   .Select(l => l.TrimEnd('\r'))
                                   .ToArray();

                    if (lines.Length == 0 || lines[0] != ProtocolHeader)
                    {
                        // Unknown protocol — ignore and keep listening.
                        continue;
                    }

                    var paths = lines.Skip(1)
                                     .Where(l => !string.IsNullOrEmpty(l))
                                     .ToArray();
                    MessageReceived?.Invoke(paths);
                }
                catch (OperationCanceledException) { /* graceful shutdown */ }
                catch (Exception)
                {
                    // Swallow per-connection errors; keep the server alive.
                }
                finally
                {
                    server?.Dispose();
                }
            }
        }
    }
}
```

Notas:
- El servidor solo lee (`PipeDirection.In`); el cliente solo escribe.
- `maxNumberOfServerInstances: 1` evita que dos clientes pisen el server (sólo uno a la vez; el siguiente queda esperando).
- Header `v1` desconocido → la conexión se ignora **sin disparar evento**.
- Header válido sin rutas → la lista llega vacía al evento (interpretación: "trae al frente").

- [ ] **Step 2: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Compilación correcta. 0 Errores`.

- [ ] **Step 3: Commit**

```bash
git add Services/SingleInstance.cs
git commit -m "feat(single-instance): mutex per-usuario + pipe server async"
```

---

## Task 4: Implementar pipe client (reenvío a la primera instancia)

**Files:**
- Modify: `Services/SingleInstance.cs` (cambiar la rama "no soy primero" para que conecte el cliente del pipe en lugar de devolver `FirstUnreachable` directamente).

- [ ] **Step 1: Sustituir la rama de "segunda instancia" en `TryAcquire`**

Localiza este bloque dentro de `TryAcquire`:

```csharp
            // Not the first instance — discard the unowned mutex (do not Release: not ours).
            mutex.Dispose();
            // Pipe client logic lands in Task 4. For now, treat as unreachable so we degrade.
            return SingleInstanceResult.FirstUnreachable;
```

Reemplázalo por:

```csharp
            // Not the first instance — discard the unowned mutex (do not Release: not ours).
            mutex.Dispose();
            return TryForwardToFirstInstance(_pipeName!, validatedArgs);
```

- [ ] **Step 2: Añadir el método `TryForwardToFirstInstance`**

Justo después del método `Release()` (y antes de `RunServerLoop`), añade:

```csharp
        private static SingleInstanceResult TryForwardToFirstInstance(string pipeName, IReadOnlyList<string> paths)
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    serverName: ".",
                    pipeName: pipeName,
                    direction: PipeDirection.Out);

                client.Connect(2000); // ms

                var sb = new StringBuilder();
                sb.Append(ProtocolHeader).Append('\n');
                foreach (var p in paths)
                    sb.Append(p).Append('\n');

                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                client.Write(bytes, 0, bytes.Length);
                client.Flush();
                return SingleInstanceResult.ForwardedToFirst;
            }
            catch (TimeoutException)
            {
                return SingleInstanceResult.FirstUnreachable;
            }
            catch (IOException)
            {
                return SingleInstanceResult.FirstUnreachable;
            }
            catch (UnauthorizedAccessException)
            {
                return SingleInstanceResult.FirstUnreachable;
            }
        }
```

(`UnauthorizedAccessException` cubre el caso de SID distinto entre procesos; en la práctica no debería darse porque el SID se deriva del usuario de cada proceso, pero queda como red de seguridad.)

- [ ] **Step 3: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Compilación correcta. 0 Errores`.

- [ ] **Step 4: Commit**

```bash
git add Services/SingleInstance.cs
git commit -m "feat(single-instance): pipe client con timeout 2 s y degradación"
```

---

## Task 5: Suscribir `MainWindow` y manejar reenvío

**Files:**
- Modify: `MainWindow.xaml.cs` (añadir handler para `SingleInstance.MessageReceived`, con marshalling al `Dispatcher`, restore + Activate + Topmost trick + apertura de pestañas).

- [ ] **Step 1: Suscribirse a `MessageReceived` en el constructor de `MainWindow`**

Añade la suscripción al final del constructor de `MainWindow` (después del `_ = NewTab();` final del bloque de `StartupFilePaths`, justo antes del cierre del constructor):

```csharp
            QuillMD.Services.SingleInstance.MessageReceived += OnFilesReceivedFromOtherInstance;
```

(Si el constructor termina con un `}` que cierra una llave anterior, busca el sitio correcto: justo antes del `}` final del constructor `public MainWindow()`.)

- [ ] **Step 2: Añadir el handler en `MainWindow.xaml.cs`**

Inserta este método junto a `AddToRecent` (alrededor de la línea 770, antes de la región `// ─────────────────── File Tree ───────────────────`):

```csharp
        private void OnFilesReceivedFromOtherInstance(IReadOnlyList<string> paths)
        {
            // Pipe server thread → marshal to UI thread.
            Application.Current.Dispatcher.Invoke(() =>
            {
                BringToFront();
                if (paths.Count == 0) return;

                foreach (var path in paths)
                {
                    if (!File.Exists(path)) continue;

                    var existing = Tabs.FirstOrDefault(t => t.Document.FilePath == path);
                    if (existing != null)
                    {
                        _ = ActivateTab(existing);
                        continue;
                    }

                    string? content = FileService.ReadFile(path);
                    if (content != null)
                    {
                        _ = NewTab(path, content);
                        AddToRecent(path);
                    }
                }
            });
        }

        private void BringToFront()
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Activate();

            // WPF trick to defeat the Windows "no foreground stealing" guard
            // when activation comes from a different process.
            Topmost = true;
            Topmost = false;
        }
```

- [ ] **Step 3: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Compilación correcta. 0 Errores`.

- [ ] **Step 4: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat(single-instance): MainWindow recibe rutas reenviadas y trae al frente"
```

---

## Task 6: Smoke tests manuales (usuario)

Esta tarea no añade código. El usuario ejecuta los 11 smoke tests del spec contra una build limpia para verificar el feature de extremo a extremo.

- [ ] **Step 1: Build limpia desde `feat/single-instance`**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

- [ ] **Step 2: Recordatorio sobre asociación de archivos**

Para que el doble clic en un `.md` desde el Explorador llame a `QuillMD.exe`, la asociación tiene que estar configurada en Windows ("Abrir con… → Elegir otra aplicación → QuillMD"). Si la asociación apunta al `.exe` de un release antiguo, los tests medirán el comportamiento del binario antiguo, no de esta build de Debug. Asegúrate de que la asociación apunta a `bin\Debug\net9.0-windows\QuillMD.exe` (o ejecuta los tests vía `cmd`/`powershell` con la ruta completa del archivo).

- [ ] **Step 3: Ejecutar los 11 smoke tests del spec**

Recorrer la sección "Validación" de `docs/superpowers/specs/2026-04-28-single-instance-design.md` punto por punto:

1. Sin instancia previa, sin args.
2. Sin instancia previa, con un archivo.
3. Sin instancia previa, con varios archivos (`QuillMD.exe a.md b.md c.md`).
4. Instancia abierta, doble clic en otro `.md` desde Explorador.
5. Instancia abierta minimizada → restaura y toma el frente.
6. Instancia abierta, doble clic en `QuillMD.exe` directo (sin args) → solo trae al frente.
7. Instancia abierta, multi-selección desde Explorador.
8. Pestaña ya abierta del mismo archivo → activa la existente.
9. Archivo inexistente → filtrado.
10. Primera instancia colgada → timeout 2 s y arranque degradado.
11. Cierre limpio + nueva apertura.

Si alguno falla, abrir una nueva tarea de fix antes de continuar.

---

## Cierre del feature

Cuando los 11 smoke tests pasen, el feature está listo para integrar a `main`. Preferencia del usuario: **merge `--no-ff`**.

```bash
git checkout main
git merge --no-ff feat/single-instance -m "feat: instancia única con reenvío de archivos"
```

(Esperar OK explícito antes de `git push`.)

---

## Self-review

**Cobertura del spec:**
- Mecanismo IPC (mutex + pipe per-usuario) → Tasks 3, 4.
- Protocolo `v1` con header + N rutas → Tasks 3 (servidor parsea) y 4 (cliente envía).
- Filtrado en cliente (`File.Exists`) → Tasks 1 y 2 (en `TryAcquire`).
- Activación de ventana (restore + Activate + Topmost trick) → Task 5.
- API pública (`enum SingleInstanceResult`, `TryAcquire`, `Release`, `MessageReceived`) → Tasks 2 (esqueleto) y 3-4 (implementación).
- Refactor `StartupFilePath` → `StartupFilePaths` → Task 1.
- Suscripción de `App` → reexposición en `MainWindow` → Tasks 2 (wiring de App, vía la propia clase `SingleInstance`) y 5 (subscriber). El spec menciona `App.FilesReceived` como reexport; este plan elige suscribirse directamente a `SingleInstance.MessageReceived` desde `MainWindow` para evitar capa duplicada — equivalente funcional, una indirección menos.
- Cierre limpio en `OnExit` → Task 2 (override) + Task 3 (`Release` real).
- Manejo de errores (timeout 2 s + degradación) → Task 4.

**Sin placeholders:** todos los pasos contienen código completo y comandos exactos.

**Consistencia de tipos:** `SingleInstanceResult` declarado en Task 2 con tres miembros, usado igual en Tasks 3 y 4. `MessageReceived` con firma `Action<IReadOnlyList<string>>?` en Task 2 y conservada. `TryAcquire(string[] args, out IReadOnlyList<string> validatedArgs)` igual en las tres tareas. `_pipeName` se declara en Task 3 y se usa en Task 4 (válido porque Task 4 modifica el mismo archivo).

**Decisión consciente sobre el spec:** el spec describe `App.FilesReceived` como reexport de `SingleInstance.MessageReceived`. Este plan suscribe `MainWindow` directamente a `SingleInstance.MessageReceived` para evitar añadir un evento extra en `App` que solo reenvía. Funcionalmente equivalente, una capa menos.
