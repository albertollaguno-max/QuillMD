# Fijar archivos en "Archivos recientes" — Plan de implementación

> **Para agentes:** SUB-SKILL REQUERIDA: Usa superpowers:subagent-driven-development (recomendado) o superpowers:executing-plans para implementar este plan tarea por tarea. Los pasos usan sintaxis de checkbox (`- [ ]`) para tracking.

**Goal:** Añadir capacidad de fijar archivos en el menú "Archivos recientes" con un icono de chincheta clicable. Los fijados aparecen arriba, separados por una línea horizontal, en una lista independiente con su propio máximo de 10.

**Architecture:** Lista de fijados persistida en archivo plano `pinned.txt` paralelo al `recent.txt` actual (sin migración). Dos `ObservableCollection<string>` en `MainWindow.xaml.cs` (`PinnedFiles`, `RecentFiles`). El submenú se reconstruye programáticamente cada vez que cambian las listas, con `MenuItem`s cuyo `Header` es un `DockPanel` (botón chincheta + nombre de archivo).

**Tech Stack:** WPF/.NET 9, CommunityToolkit.Mvvm (`RelayCommand`), `ObservableCollection`. Sin nuevos NuGets.

**Spec:** `docs/superpowers/specs/2026-04-28-fijar-archivos-recientes-design.md`

**Verificación:** Smoke tests manuales en Windows tras `dotnet build`. No hay framework de tests automatizados en QuillMD; cada tarea termina con un build + un smoke test acotado y un commit.

---

## Estructura de archivos

| Archivo | Cambio | Responsabilidad |
|---|---|---|
| `Services/FileService.cs` | Modificar | Persistencia de fijados (`Load/SavePinnedFiles`, constante `MaxPinnedFiles`); refactor de `GetSettingsPath`. |
| `MainWindow.xaml` | Modificar | El `MenuItem "Archivos recientes"` deja de usar `ItemsSource` y se identifica con `x:Name="RecentFilesMenuItem"`. |
| `MainWindow.xaml.cs` | Modificar | Añadir `PinnedFiles`, comandos `PinCommand`/`UnpinCommand`, método `RebuildRecentMenu()`, lógica de invariante en `AddToRecent`, manejo de fijado inexistente en `OpenRecentFile`. |

---

## Build helper

Cada tarea termina con un build desde WSL invocando `dotnet` en Windows. Si falla, no se commitea: se corrige y se vuelve a buildear.

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Build succeeded.` con 0 errores.

---

## Task 1: Refactor `GetSettingsPath` para aceptar nombre de archivo

**Files:**
- Modify: `Services/FileService.cs:110-116`

- [ ] **Step 1: Sustituir `GetSettingsPath` por una versión parametrizada**

Reemplaza el método actual (`Services/FileService.cs:110-116`):

```csharp
private static string GetSettingsPath(string fileName = "recent.txt")
{
    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    string dir = Path.Combine(appData, "QuillMD");
    Directory.CreateDirectory(dir);
    return Path.Combine(dir, fileName);
}
```

El parámetro tiene valor por defecto `"recent.txt"` para no romper las llamadas existentes.

- [ ] **Step 2: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Build succeeded.`

- [ ] **Step 3: Smoke test rápido**

Ejecutar QuillMD, abrir un archivo cualquiera, cerrar. Verificar que `%AppData%\QuillMD\recent.txt` se sigue actualizando como antes (no regresión).

- [ ] **Step 4: Commit**

```bash
git add Services/FileService.cs
git commit -m "refactor(filesvc): parametrizar GetSettingsPath con fileName"
```

---

## Task 2: Persistencia de fijados en `FileService`

**Files:**
- Modify: `Services/FileService.cs:9-10` (constantes), `Services/FileService.cs:72-108` (añadir métodos)

- [ ] **Step 1: Añadir constante `MaxPinnedFiles`**

Justo debajo de `private const int MaxRecentFiles = 10;` (`Services/FileService.cs:9`):

```csharp
public const int MaxRecentFiles = 10;
public const int MaxPinnedFiles = 10;
```

(Cambia `private` por `public` en `MaxRecentFiles` para coherencia y para poder consultarlo desde `MainWindow.xaml.cs` si fuera necesario.)

- [ ] **Step 2: Añadir `LoadPinnedFiles`**

Justo debajo de `LoadRecentFiles` (después de la línea 90 actual):

```csharp
public static List<string> LoadPinnedFiles()
{
    var result = new List<string>();
    try
    {
        string settingsPath = GetSettingsPath("pinned.txt");
        if (File.Exists(settingsPath))
        {
            var lines = File.ReadAllLines(settingsPath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    result.Add(trimmed);
            }
        }
    }
    catch { }
    return result;
}
```

A diferencia de `LoadRecentFiles`, **no filtra** por `File.Exists`: las rutas fijadas se conservan aunque el archivo no exista en este momento (USB desconectado, unidad de red caída).

- [ ] **Step 3: Añadir `SavePinnedFiles`**

Justo debajo del método anterior:

```csharp
public static void SavePinnedFiles(List<string> pinnedFiles)
{
    try
    {
        File.WriteAllLines(GetSettingsPath("pinned.txt"), pinnedFiles);
    }
    catch { }
}
```

- [ ] **Step 4: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add Services/FileService.cs
git commit -m "feat(filesvc): persistencia de archivos fijados (pinned.txt)"
```

---

## Task 3: Colección `PinnedFiles` y carga inicial

**Files:**
- Modify: `MainWindow.xaml.cs:27` (añadir colección), `MainWindow.xaml.cs:145-148` (carga inicial)

- [ ] **Step 1: Declarar `PinnedFiles`**

Justo debajo de `public ObservableCollection<string> RecentFiles { get; } = new();` (`MainWindow.xaml.cs:27`):

```csharp
public ObservableCollection<string> RecentFiles { get; } = new();
public ObservableCollection<string> PinnedFiles { get; } = new();
```

- [ ] **Step 2: Cargar al arrancar**

Después de `App.Log($"Loaded {recent.Count} recent files");` (línea ~148), añadir:

```csharp
App.Log("Loading pinned files...");
var pinned = FileService.LoadPinnedFiles();
foreach (var f in pinned) PinnedFiles.Add(f);
App.Log($"Loaded {pinned.Count} pinned files");
```

- [ ] **Step 3: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Build succeeded.`

- [ ] **Step 4: Smoke test**

Ejecutar QuillMD. Crear manualmente `%AppData%\QuillMD\pinned.txt` con 2 rutas válidas. Cerrar. Volver a abrir. En el log debe aparecer `Loaded 2 pinned files`. Borrar el archivo manualmente para limpiar antes del siguiente test.

- [ ] **Step 5: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat(recents): añadir colección PinnedFiles con carga inicial"
```

---

## Task 4: Preparar el `MenuItem` para construcción programática

**Files:**
- Modify: `MainWindow.xaml:77-85`

- [ ] **Step 1: Quitar `ItemsSource` y dar `x:Name`**

Reemplaza el bloque actual (`MainWindow.xaml:77-85`):

```xml
                <MenuItem x:Name="RecentFilesMenuItem" Header="Archivos recientes"/>
```

(Una sola línea. Sin `ItemsSource`, sin `ItemContainerStyle`. Los items se añaden desde code-behind en la siguiente tarea.)

- [ ] **Step 2: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Build succeeded.`

- [ ] **Step 3: Smoke test**

Ejecutar QuillMD. Abrir el menú **Archivo → Archivos recientes**. Debe aparecer **vacío** (sin entradas). Esto es esperado: los items se añadirán en la siguiente tarea. La aplicación no debe crashear.

- [ ] **Step 4: Commit**

```bash
git add MainWindow.xaml
git commit -m "refactor(menu): preparar RecentFilesMenuItem para construcción programática"
```

---

## Task 5: Implementar `RebuildRecentMenu()` y vincular a cambios de las colecciones

**Files:**
- Modify: `MainWindow.xaml.cs` (añadir método `RebuildRecentMenu` y suscripciones a `CollectionChanged`)

- [ ] **Step 1: Añadir el método `RebuildRecentMenu`**

Inserta este método en `MainWindow.xaml.cs`, junto a `AddToRecent` (alrededor de la línea 759, antes de la región `// ─────────────────── File Tree ───────────────────`):

```csharp
private void RebuildRecentMenu()
{
    if (RecentFilesMenuItem == null) return;
    RecentFilesMenuItem.Items.Clear();

    foreach (var path in PinnedFiles)
        RecentFilesMenuItem.Items.Add(BuildRecentMenuItem(path, isPinned: true));

    if (PinnedFiles.Count > 0 && RecentFiles.Count > 0)
        RecentFilesMenuItem.Items.Add(new Separator());

    foreach (var path in RecentFiles)
        RecentFilesMenuItem.Items.Add(BuildRecentMenuItem(path, isPinned: false));
}

private MenuItem BuildRecentMenuItem(string path, bool isPinned)
{
    var fileName = Path.GetFileName(path);

    var pinButton = new Button
    {
        Content = "📌",
        ToolTip = isPinned ? "Quitar de fijados" : "Fijar",
        Background = System.Windows.Media.Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(2, 0, 6, 0),
        Margin = new Thickness(0),
        Cursor = System.Windows.Input.Cursors.Hand,
        Focusable = false,
        Opacity = isPinned ? 1.0 : 0.35,
        FontSize = 12,
        VerticalAlignment = VerticalAlignment.Center
    };

    if (!isPinned)
    {
        pinButton.MouseEnter += (_, _) => pinButton.Opacity = 1.0;
        pinButton.MouseLeave += (_, _) => pinButton.Opacity = 0.35;
    }

    pinButton.Click += (_, e) =>
    {
        e.Handled = true;
        if (isPinned) UnpinFile(path);
        else PinFile(path);
    };

    var label = new TextBlock
    {
        Text = fileName,
        VerticalAlignment = VerticalAlignment.Center
    };

    var panel = new DockPanel { LastChildFill = true };
    DockPanel.SetDock(pinButton, Dock.Left);
    panel.Children.Add(pinButton);
    panel.Children.Add(label);

    var item = new MenuItem
    {
        Header = panel,
        ToolTip = path
    };
    item.Click += async (_, e) =>
    {
        if (e.OriginalSource is Button) return; // evita doble disparo si el click vino del botón
        await OpenRecentFile(path);
    };
    return item;
}

// Stubs — implementación real en Task 6 y Task 7
private void PinFile(string path) { }
private void UnpinFile(string path) { }
```

- [ ] **Step 2: Suscribirse a `CollectionChanged` y reconstruir tras la carga inicial**

Justo después del bloque `App.Log($"Loaded {pinned.Count} pinned files");` añadido en Task 3, añadir:

```csharp
RecentFiles.CollectionChanged += (_, _) => RebuildRecentMenu();
PinnedFiles.CollectionChanged += (_, _) => RebuildRecentMenu();
RebuildRecentMenu();
```

- [ ] **Step 3: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Build succeeded.`

- [ ] **Step 4: Smoke test**

Ejecutar QuillMD. Abrir un par de archivos. Abrir **Archivo → Archivos recientes**: deben aparecer las entradas con el nombre del archivo (no la ruta), un icono 📌 a la izquierda en cada una (tenue), y al pasar el ratón por encima del icono debe ponerse opaco. Hacer clic en una entrada (en la zona del nombre) debe abrir el archivo. Hacer clic en el icono **todavía no hace nada** (los stubs están vacíos), pero **no debe abrir el archivo** ni crashear.

- [ ] **Step 5: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat(menu): construcción programática del submenú de recientes"
```

---

## Task 6: Comando `PinFile` (mover de recientes a fijados)

**Files:**
- Modify: `MainWindow.xaml.cs` (sustituir el stub `PinFile`)

- [ ] **Step 1: Implementar `PinFile`**

Reemplaza el stub vacío de `PinFile` por:

```csharp
private void PinFile(string path)
{
    if (PinnedFiles.Count >= FileService.MaxPinnedFiles)
    {
        MessageBox.Show(
            "Has alcanzado el máximo de 10 archivos fijados. Quita alguno antes de fijar otro.",
            "Archivos fijados",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }

    RecentFiles.Remove(path);
    PinnedFiles.Insert(0, path);

    FileService.SavePinnedFiles(PinnedFiles.ToList());
    PersistRecentFiles();
}

private void PersistRecentFiles()
{
    try
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QuillMD", "recent.txt");
        File.WriteAllLines(path, RecentFiles);
    }
    catch { }
}
```

(`PersistRecentFiles` es un helper local porque `FileService.AddRecentFile` reordena, y aquí solo necesitamos volcar la lista actual tal cual.)

- [ ] **Step 2: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Build succeeded.`

- [ ] **Step 3: Smoke test**

Ejecutar QuillMD. Abrir 3 archivos. Abrir el menú de recientes. Hacer clic en la chincheta de uno: debe desaparecer de la sección de recientes y aparecer **arriba**, en una nueva sección, separada por una línea horizontal. Verificar que `%AppData%\QuillMD\pinned.txt` contiene la ruta y `recent.txt` ya no.

Test de tope: fijar 10 archivos uno por uno. Al intentar fijar el 11º debe aparecer el `MessageBox` informativo y el archivo debe seguir en recientes.

- [ ] **Step 4: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat(recents): comando PinFile con tope de 10 fijados"
```

---

## Task 7: Comando `UnpinFile` (mover de fijados a recientes)

**Files:**
- Modify: `MainWindow.xaml.cs` (sustituir el stub `UnpinFile`)

- [ ] **Step 1: Implementar `UnpinFile`**

Reemplaza el stub vacío:

```csharp
private void UnpinFile(string path)
{
    PinnedFiles.Remove(path);

    RecentFiles.Remove(path); // por si acaso, mantén la invariante
    RecentFiles.Insert(0, path);
    while (RecentFiles.Count > FileService.MaxRecentFiles)
        RecentFiles.RemoveAt(RecentFiles.Count - 1);

    FileService.SavePinnedFiles(PinnedFiles.ToList());
    PersistRecentFiles();
}
```

- [ ] **Step 2: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Build succeeded.`

- [ ] **Step 3: Smoke test**

Estado de partida: al menos un archivo fijado (de la tarea anterior). Hacer clic en su chincheta opaca: debe desaparecer de la sección de fijados y aparecer al principio de recientes. Si no quedan fijados, el separador horizontal debe desaparecer. Verificar `pinned.txt` y `recent.txt` reflejan el cambio.

Test de persistencia: fijar 2 archivos, cerrar la app, volver a abrir. Los 2 fijados deben aparecer arriba, en el orden esperado (último fijado arriba).

- [ ] **Step 4: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat(recents): comando UnpinFile"
```

---

## Task 8: Invariante en `AddToRecent` (no añadir si ya está fijado)

**Files:**
- Modify: `MainWindow.xaml.cs:759-765` (cuerpo de `AddToRecent`)

- [ ] **Step 1: Modificar `AddToRecent`**

Reemplaza el método actual:

```csharp
private void AddToRecent(string path)
{
    if (PinnedFiles.Contains(path)) return; // los fijados no se mueven ni se duplican en recientes

    var list = RecentFiles.ToList();
    FileService.AddRecentFile(path, list);
    RecentFiles.Clear();
    foreach (var f in list) RecentFiles.Add(f);
}
```

- [ ] **Step 2: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Build succeeded.`

- [ ] **Step 3: Smoke test**

Fijar un archivo. Reabrirlo (desde recientes, drag&drop, o **Archivo → Abrir**). Verificar que **no aparece duplicado** en recientes y **no cambia de posición** en fijados. El archivo debe abrirse correctamente.

Test colateral: abrir un archivo nuevo (no fijado). Debe aparecer al principio de recientes (no regresión).

- [ ] **Step 4: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "fix(recents): respetar invariante (fijado no se añade a recientes)"
```

---

## Task 9: Manejo de fijado inexistente en `OpenRecentFile`

**Files:**
- Modify: `MainWindow.xaml.cs:606-624` (cuerpo de `OpenRecentFile`)

- [ ] **Step 1: Modificar `OpenRecentFile`**

Reemplaza el método actual:

```csharp
private async Task OpenRecentFile(string? path)
{
    if (string.IsNullOrEmpty(path)) return;

    if (!File.Exists(path))
    {
        if (PinnedFiles.Contains(path))
        {
            var result = MessageBox.Show(
                $"No se encuentra el archivo:\n{path}\n\n¿Quitarlo de fijados?",
                "Archivo fijado no encontrado",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                PinnedFiles.Remove(path);
                FileService.SavePinnedFiles(PinnedFiles.ToList());
            }
            return;
        }

        MessageBox.Show($"El archivo ya no existe:\n{path}", "Archivo no encontrado",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        RecentFiles.Remove(path);
        PersistRecentFiles();
        return;
    }

    var existing = Tabs.FirstOrDefault(t => t.Document.FilePath == path);
    if (existing != null) { await ActivateTab(existing); return; }
    string? content = FileService.ReadFile(path);
    if (content != null)
    {
        await NewTab(path, content);
        AddToRecent(path);
    }
}
```

Cambios respecto al original:
- Si la ruta no existe **y está en `PinnedFiles`**: diálogo Sí/No para quitar de fijados.
- Si no existe y está en recientes: comportamiento previo, pero ahora persistimos `recent.txt` con `PersistRecentFiles` (antes el `RecentFiles.Remove` solo modificaba la colección en memoria sin volcar).
- El final (cargar el contenido y abrir pestaña) no cambia.

- [ ] **Step 2: Build**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

Esperado: `Build succeeded.`

- [ ] **Step 3: Smoke test**

Test 1 — fijado borrado: fijar un archivo, **borrarlo del disco** (o renombrarlo), hacer clic en él en el menú. Debe aparecer el diálogo Sí/No. Pulsar **Sí**: la entrada desaparece. Repetir y pulsar **No**: la entrada se queda.

Test 2 — fijado en USB desconectado: fijar un archivo, simular desconexión cambiando temporalmente el nombre de su carpeta padre, hacer clic. Aparece el diálogo. Pulsar **No**. Restaurar el nombre. Volver a hacer clic: ahora abre normalmente.

Test 3 — reciente borrado: borrar un archivo que está en recientes (no fijado), hacer clic. Aparece el aviso anterior y desaparece de la lista (no regresión).

- [ ] **Step 4: Commit**

```bash
git add MainWindow.xaml.cs
git commit -m "feat(recents): diálogo Sí/No al abrir un fijado inexistente"
```

---

## Task 10: Smoke test completo (validación del spec)

Esta tarea no añade código. Ejecuta los 10 smoke tests del spec contra una build limpia y deja constancia.

- [ ] **Step 1: Build limpia**

```bash
powershell.exe -Command "cd F:\PlannamTypora; dotnet build -c Debug"
```

- [ ] **Step 2: Borrar settings de prueba previos**

Eliminar manualmente `%AppData%\QuillMD\recent.txt` y `pinned.txt` para empezar desde cero.

- [ ] **Step 3: Ejecutar los 10 smoke tests del spec**

Recorrer la sección "Validación" de `docs/superpowers/specs/2026-04-28-fijar-archivos-recientes-design.md` punto por punto:

1. Carga inicial (instalación nueva).
2. Fijar un reciente.
3. Reabrir un fijado.
4. Abrir un reciente que existe.
5. Desfijar.
6. Tope de fijados (intentar fijar el 11º).
7. Persistencia entre sesiones.
8. Fijado borrado (diálogo Sí/No).
9. Apariencia de las chinchetas.
10. Compatibilidad con `recent.txt` existente.

Si alguno falla, abrir una nueva tarea de fix antes de continuar.

- [ ] **Step 4: Commit (vacío) marcando final del feature**

Si todos pasan, no hay nada nuevo que commitear. Saltar este step.

---

## Cierre del feature

Una vez los 10 smoke tests pasan, el feature está listo para integrar a `main`. La preferencia del usuario es **merge `--no-ff`** (no rebase). Antes del merge, abrir un PR informal o hacer el merge directo desde local — depende del flujo elegido al cerrar.

```bash
git checkout main
git merge --no-ff feat/pinned-recents -m "feat: fijar archivos en Archivos recientes"
```

(Esperar al OK explícito antes de `git push`.)

---

## Self-review

**Cobertura del spec:**
- Almacenamiento (`pinned.txt` paralelo, sin migración) → Task 2.
- Modelo de datos (`PinnedFiles`, invariante, refactor `GetSettingsPath`) → Tasks 1, 3, 8.
- UI (chincheta clicable, opacidad, separador, nombre de archivo, tooltip de ruta) → Tasks 4, 5.
- Comportamiento al fijar/desfijar (con tope) → Tasks 6, 7.
- Apertura de fijado inexistente (diálogo Sí/No) → Task 9.
- Validación → Task 10.

**Sin placeholders:** todos los pasos contienen código completo o comandos exactos.

**Consistencia de tipos:** `PinFile`/`UnpinFile` aparecen como stubs en Task 5 y se implementan en Tasks 6 y 7 con la misma firma. `PersistRecentFiles` se introduce en Task 6 y se reutiliza en Task 9. `MaxPinnedFiles` se define como `public` en Task 2 y se usa en Tasks 6 y 7.
