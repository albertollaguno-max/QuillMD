# Instancia única con reenvío de archivos

Fecha: 2026-04-28
Autor: Alberto Llaguno

## Objetivo

Cuando ya hay un QuillMD abierto y el usuario hace doble clic en un `.md` desde el Explorador de Windows (o lanza una segunda instancia con uno o varios archivos), la nueva ejecución debe:

1. Detectar que hay otra instancia.
2. Enviarle las rutas vía IPC.
3. Cerrarse sin mostrar ventana.

La instancia existente abre cada ruta como una nueva pestaña (o activa la pestaña existente si ese archivo ya está abierto), y se trae al frente con la ventana restaurada si estaba minimizada. Si la nueva ejecución no trae archivos (doble clic en `QuillMD.exe` directo), la primera instancia solo se trae al frente.

## Estado actual

- `App.xaml.cs:OnStartup` lee `e.Args[0]` y guarda la ruta en la propiedad estática `StartupFilePath` (string nullable).
- `MainWindow.xaml.cs:162-170`, en el constructor, comprueba `App.StartupFilePath` y abre el archivo como pestaña si existe.
- No hay detección de instancia previa: cada lanzamiento es un proceso independiente con su propia `MainWindow`.
- `OutputType=WinExe`, sin restricciones especiales en `QuillMD.csproj`.
- El `MainWindow.xaml.cs:606-624` (`OpenRecentFile`) ya trae el patrón de "si hay pestaña con esa ruta, activa en vez de duplicar". Se reutiliza.

## Diseño

### Mecanismo IPC

Combinación estándar de Windows: **`Mutex` per-usuario + `NamedPipeServerStream` per-usuario**.

- En `App.OnStartup`, antes de `base.OnStartup`: intentar adquirir un `Mutex` con nombre `Local\QuillMD-SingleInstance-<userSid>`. El prefijo `Local\` lo confina a la sesión del usuario actual; el SID evita colisiones en máquinas multi-usuario o entre usuarios distintos en el mismo Windows.
- Si se adquiere → soy la **primera instancia**. Arranco un `Task.Run` con un loop infinito: `WaitForConnectionAsync` → leer hasta EOF → parsear → disparar evento → desconectar → siguiente.
- Si no se adquiere → ya hay otra instancia. Conecto al `NamedPipeClientStream` con timeout de 2 s, envío `args` filtrados línea a línea, llamo `Application.Current.Shutdown(0)` y `return` desde `OnStartup` sin invocar `base.OnStartup` (no se crea `MainWindow`).
- Si el pipe da timeout o falla → log de aviso en `QuillMD_crash.log`, libero (no readquiero) el `Mutex` y **arranco como instancia normal** (degradación segura: si la primera está colgada, no dejo al usuario sin app).

**Justificación frente a alternativas:**
- `WM_COPYDATA`: requiere P/Invoke + búsqueda de HWND por nombre de ventana. Más glue de Win32, menos limpio en WPF.
- `Microsoft.VisualBasic.ApplicationServices.WindowsFormsApplicationBase`: minimal código pero mezcla WinForms y WPF. Funciona pero rechina.
- NamedPipe es nativo de la BCL (`System.IO.Pipes`), async-friendly, sin dependencias nuevas en el `csproj`.

### Protocolo del pipe

UTF-8, líneas separadas por `\n`. Cada conexión es un mensaje de N+1 líneas:

```
v1
<ruta-1>
<ruta-2>
...
```

- Primera línea: header de versión (`v1`).
- Líneas siguientes: rutas absolutas. Cero rutas significa "solo trae la ventana al frente".

Cliente: abre, escribe el bloque, cierra. Servidor: lee hasta EOF, parsea, dispara el evento, vuelve a `WaitForConnectionAsync`.

El header `v1` permite ampliar el protocolo en el futuro (p. ej. `v2` con flags como "abrir en read-only" o "moverse a un workspace concreto") sin romper compatibilidad: si la primera instancia recibe un header desconocido, simplemente ignora el mensaje y loggea.

### Filtrado en cliente

Antes de conectar al pipe, la segunda instancia filtra `e.Args` quedándose solo con paths que `File.Exists`. Mismo criterio que `App.OnStartup` actual (que descarta `args[0]` si no existe). Si tras filtrar no queda ninguno, envía el mensaje con header `v1` y cero rutas — la primera instancia interpreta "trae al frente sin abrir nada".

### Activación de ventana

Cuando la primera instancia recibe un mensaje, sobre el `Dispatcher` de la UI:

1. Si `MainWindow.WindowState == WindowState.Minimized`, restaurar a `WindowState.Normal`.
2. `MainWindow.Activate()` para llevar el foco.
3. Truco WPF para sortear la regla de Windows que impide a un proceso no-foreground robar foco: `Topmost = true; Topmost = false;` justo después de `Activate()`. Es un patrón conocido y ampliamente usado.
4. Para cada ruta recibida: si ya existe una pestaña con esa ruta (`Tabs.FirstOrDefault(t => t.Document.FilePath == path)`), activarla; si no, leer el contenido, crear nueva pestaña con `NewTab(path, content)` y `AddToRecent(path)`.

### Cambios por archivo

#### `App.xaml.cs`

- Reorganizar `OnStartup`:
  1. Hookear los handlers de excepciones (sin cambios).
  2. Llamar a `var result = SingleInstance.TryAcquire(e.Args, out var validated);`.
  3. Si `result == ForwardedToFirst`: `Shutdown(0)` y `return` (sin invocar `base.OnStartup`, sin mostrar ventana).
  4. Si `result == FirstInstance` o `FirstUnreachable`: continuar con el flujo (loggear el caso degradado), guardar `validated` en `StartupFilePaths`, suscribir el handler de `MessageReceived`, llamar `base.OnStartup`.
- Cambiar la propiedad estática `StartupFilePath` (string?) → `StartupFilePaths` (`IReadOnlyList<string>`, nunca null; lista vacía si no hay archivos válidos en el primer arranque).
- Añadir un evento estático `public static event Action<IReadOnlyList<string>>? FilesReceived;` que `MainWindow` se suscribe para procesar reenvíos posteriores. La invocación va sobre el `Dispatcher` desde el thread del pipe.
- Liberar el `Mutex` y cancelar el server en `OnExit`.

#### `MainWindow.xaml.cs`

- Constructor: en lugar de leer `App.StartupFilePath` (string único), iterar `App.StartupFilePaths`. Para cada ruta:
  - `string? content = FileService.ReadFile(path); if (content != null) { _ = NewTab(path, content); AddToRecent(path); }`
  - El `NewTab` final queda como pestaña activa.
  - Si la lista está vacía, llamar `NewTab()` (pestaña en blanco) como ahora.
- Suscribirse a `App.FilesReceived` con un handler que sobre el `Dispatcher`:
  1. Trae al frente (state restore + Activate + Topmost trick).
  2. Si la lista trae rutas, las abre/activa como en el constructor.

#### Nuevo: `Services/SingleInstance.cs`

API pública:

```csharp
public enum SingleInstanceResult
{
    FirstInstance,      // soy la primera; el caller debe continuar arranque normal
    ForwardedToFirst,   // ya había instancia previa; le envié los args; el caller debe Shutdown(0)
    FirstUnreachable    // hay otra instancia pero no responde; arrancar degradado como primera
}

public static class SingleInstance
{
    public static SingleInstanceResult TryAcquire(string[] args, out IReadOnlyList<string> validatedArgs);
    public static void Release();
    public static event Action<IReadOnlyList<string>>? MessageReceived;
}
```

- `TryAcquire`:
  - Construye los nombres `MutexName = $"Local\\QuillMD-SingleInstance-{WindowsIdentity.GetCurrent().User}"` y `PipeName = $"QuillMD-Pipe-{WindowsIdentity.GetCurrent().User}"`.
  - Filtra `args` con `File.Exists` → `validatedArgs` (lista vacía si no queda ninguno).
  - Intenta `new Mutex(initiallyOwned: true, MutexName, out bool createdNew)`.
  - Si `createdNew == true`: arranca el server (un `Task.Run` con loop `WaitForConnectionAsync` → parsear → invocar `MessageReceived`) y devuelve `FirstInstance`.
  - Si no: intenta `Connect(2000)` al pipe y enviar el bloque (header `v1` + `validatedArgs` línea a línea, también si está vacío).
    - Éxito → `ForwardedToFirst`.
    - Timeout o `IOException` → log a `QuillMD_crash.log` con la causa y devuelve `FirstUnreachable`. El mutex no adquirido se descarta sin liberar (no era nuestro).
- `Release`: cancela el `CancellationTokenSource` del loop server y libera el `Mutex` (solo si `TryAcquire` devolvió `FirstInstance`). Idempotente. Se llama desde `App.OnExit`.
- `MessageReceived`: evento que se dispara desde el thread del pipe cuando llega una conexión válida. La lista puede venir vacía (significa "trae al frente sin abrir nada"). El suscriptor es responsable de marshalling al UI thread (`Application.Current.Dispatcher.Invoke(...)`). `App` reexpone este evento como `App.FilesReceived`.

#### `QuillMD.csproj`

Sin cambios. `System.IO.Pipes` y `System.Threading.Mutex` están en la BCL.

### Concurrencia y cierre

- El loop del server procesa **un cliente cada vez**. Suficiente: el caso real es muy puntual (un humano haciendo doble clic en un archivo). Si llegan dos lanzamientos casi simultáneos, el segundo queda en `WaitForNamedPipeAsync` hasta que el primero termine.
- El `Task.Run` del server respeta un `CancellationToken` que se cancela en `OnExit`. Tras la cancelación, `WaitForConnectionAsync` lanza `OperationCanceledException` y el loop sale limpiamente.
- Si la primera instancia muere por crash sin pasar por `OnExit`, el SO libera el `Mutex` al terminar el proceso → la siguiente ejecución se convierte en primera instancia normalmente. No hay estado huérfano que limpiar.

### Errores y degradación

- **Cliente con timeout** (2 s): log `WARN: pipe timeout, arrancando como instancia degradada` + arranque normal.
- **Cliente con `IOException` distinto de timeout**: idem, log con `ex.Message`.
- **Servidor falla al leer**: cierra esa conexión, vuelve a `WaitForConnectionAsync`. No tira el server entero por una conexión malformada.
- **Header desconocido en el servidor**: log `WARN: unknown protocol header X`, ignora el mensaje, no abre nada, no trae al frente.

### Fuera de alcance

- Asociación de archivos `.md` con QuillMD a nivel de Windows (la gestiona el usuario manualmente desde "Abrir con…").
- Soporte para argumentos de línea de comandos avanzados (flags `--read-only`, `--workspace=…`, etc.). El protocolo `v1` solo lleva rutas; queda preparado para `v2` futuro.
- Comunicación entre instancias QuillMD de **usuarios distintos** (caso multi-sesión Windows). Cada usuario tiene su propio mutex/pipe; son procesos completamente independientes, como debe ser.

## Validación

Smoke tests manuales tras `dotnet build`:

1. Sin instancia previa, sin args → arranca normal, mutex adquirido, pipe escuchando (verificable en `QuillMD_crash.log`).
2. Sin instancia previa, con un archivo → abre con esa pestaña activa (no regresión).
3. Sin instancia previa, con varios archivos (`QuillMD.exe a.md b.md c.md` desde un cmd) → abre las tres pestañas, la última activa.
4. Instancia abierta, doble clic en otro `.md` desde el Explorador → la existente añade pestaña, la segunda se cierra, no aparece ventana nueva.
5. Instancia abierta minimizada → la ventana se restaura y queda al frente al recibir el archivo.
6. Instancia abierta, doble clic en `QuillMD.exe` directo (sin args) → solo se trae al frente, no abre pestaña en blanco extra.
7. Instancia abierta, multi-selección (`Ctrl`-clic en varios `.md` + Enter) → todas las rutas se reenvían como pestañas.
8. Pestaña ya abierta del mismo archivo → activa la existente en vez de duplicar.
9. Archivo inexistente o ruta inválida pasada por línea de comandos → la segunda instancia lo filtra; si no queda ninguno, solo trae al frente.
10. Primera instancia colgada (simulable con un `Thread.Sleep` puntual en `MainWindow_Loaded` o pausando con un depurador) → la segunda da timeout 2 s y arranca como nueva ventana; el archivo se abre allí; log con la causa.
11. Cierre limpio: cerrar la primera, abrir nueva → toma el rol de primera sin warnings.
