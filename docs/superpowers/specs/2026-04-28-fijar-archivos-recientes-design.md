# Fijar archivos en "Archivos recientes"

Fecha: 2026-04-28
Autor: Alberto Llaguno

## Objetivo

Permitir que el usuario "fije" archivos dentro del menú **Archivo → Archivos recientes** para que no roten fuera de la lista. Los fijados aparecen en una sección superior, separados por una línea horizontal de los recientes. La interacción se hace mediante un icono de chincheta (📌) clicable junto a cada entrada.

## Estado actual

- `MainWindow.xaml.cs:27` expone `ObservableCollection<string> RecentFiles`.
- `MainWindow.xaml:77-85` enlaza ese collection al `MenuItem "Archivos recientes"` vía `ItemsSource`.
- `Services/FileService.cs:9-115` mantiene `MaxRecentFiles = 10` y persiste rutas en `%AppData%\QuillMD\recent.txt` (una por línea). Carga filtra rutas inexistentes.
- `AddToRecent` (`MainWindow.xaml.cs:759`) reordena: quita la ruta si ya estaba, la inserta al principio, recorta a 10, y guarda.

## Diseño

### Almacenamiento

Dos archivos planos en `%AppData%\QuillMD\`:

- `recent.txt` — sin cambios. Sigue siendo el formato actual (una ruta por línea, máx 10).
- `pinned.txt` — nuevo, mismo formato (máx 10).

Sin migración: instalaciones existentes mantienen su `recent.txt` intacto y arrancan con `pinned.txt` ausente (= lista de fijados vacía).

### Modelo de datos

En `MainWindow.xaml.cs`:

- `ObservableCollection<string> PinnedFiles { get; }` — máx 10. Orden: el más recientemente fijado arriba.
- `ObservableCollection<string> RecentFiles { get; }` — máx 10. Orden: el más recientemente abierto arriba (igual que ahora).

**Invariante:** una misma ruta nunca está en las dos listas a la vez.

En `Services/FileService.cs`:

- Constante `MaxPinnedFiles = 10`.
- `LoadPinnedFiles()` — lee `pinned.txt`, filtra rutas inexistentes (igual que `LoadRecentFiles`).
- `SavePinnedFiles(List<string> pinnedFiles)` — escribe a `pinned.txt`.
- Refactor menor: extraer un helper `GetSettingsPath(string fileName)` para evitar duplicar la construcción de la ruta entre los dos archivos. La función actual sin parámetro queda como wrapper que pasa `"recent.txt"`.

`AddRecentFile` se mantiene tal cual; la responsabilidad de mantener la invariante (no añadir a recientes si está en fijados) la asume el code-behind de `MainWindow`.

### UI

El `MenuItem "Archivos recientes"` deja de usar `ItemsSource`. Se vacía y se reconstruye programáticamente cada vez que cambian `PinnedFiles` o `RecentFiles` (un único método `RebuildRecentMenu()` en `MainWindow.xaml.cs`).

Estructura del submenú reconstruido:

```
Archivos recientes
├── 📌 Documento-importante.md      ← sección fijados (chincheta opaca)
├── 📌 Plan-2026.md
├── ──────────────                  ← Separator (solo si ambas secciones tienen entradas)
├── 📌 nota-de-ayer.md              ← sección recientes (chincheta tenue, opacidad 0.35)
├── 📌 borrador.md
└── …
```

Cada entrada es un `MenuItem` cuyo `Header` es un `DockPanel` con:

- A la izquierda: un `Button` sin chrome con el glifo unicode `📌`. `Click` toggles fijar/desfijar; el handler establece `e.Handled = true` para que el `Click` del `MenuItem` no se dispare a la vez.
- Al centro/derecha: un `TextBlock` con el **nombre del archivo** (`Path.GetFileName(path)`).
- `ToolTip` del `MenuItem`: ruta completa.
- `Click` del `MenuItem` completo: abre el archivo (delega en `OpenRecentCommand` con la ruta).

Estado visual de la chincheta:

- Fijado: `Opacity = 1`.
- No fijado: `Opacity = 0.35`, sube a 1 en hover (mismo glifo, sin cambiar el carácter).

No se introducen nuevos assets gráficos: se usa el glifo unicode `📌` con la fuente del sistema.

### Comportamiento

**Al abrir un archivo** (cualquier vía: menú Abrir, drag & drop, importación, clic en una entrada del menú, abrir reciente al inicio):

- Si la ruta está en `PinnedFiles` → no se mueve; solo se abre.
- Si está en `RecentFiles` → se mueve al principio.
- Si no está en ninguna → se inserta al principio de `RecentFiles`. Si la lista supera 10, se descarta la última.

**Al hacer clic en la chincheta de un reciente** (fijar):

- Se quita de `RecentFiles`.
- Se inserta al principio de `PinnedFiles`.
- Si `PinnedFiles` ya tiene 10 entradas, se muestra un `MessageBox` informativo ("Has alcanzado el máximo de 10 archivos fijados. Quita alguno antes de fijar otro.") y la operación se cancela (la ruta sigue en `RecentFiles`).

**Al hacer clic en la chincheta de un fijado** (desfijar):

- Se quita de `PinnedFiles`.
- Se inserta al principio de `RecentFiles`. Si la lista supera 10, se descarta la última.

**Persistencia:** cada operación que modifique cualquiera de las dos listas escribe el archivo correspondiente inmediatamente (mismo patrón que `AddRecentFile` actual).

### Archivos inexistentes

- En el arranque, `LoadRecentFiles()` filtra entradas inexistentes (comportamiento actual). `LoadPinnedFiles()` **no** filtra: las entradas fijadas se conservan aunque no exista el archivo en ese momento (puede ser un USB desconectado o una unidad de red caída).
- Al hacer clic en una entrada fijada cuyo archivo no existe, se muestra un `MessageBox` (`MessageBoxButton.YesNo`) con el texto: *"No se encuentra el archivo `<ruta>`. ¿Quitarlo de fijados?"*. **Sí** lo elimina de `PinnedFiles` y guarda. **No** cierra el diálogo sin tocar nada (el archivo permanece fijado, útil si la unidad volverá a estar disponible).
- Al hacer clic en una entrada reciente cuyo archivo no existe (caso raro porque el filtro al cargar ya las quita, pero puede pasar si el archivo se borra durante la sesión), se mantiene el comportamiento actual: error y se quita de `RecentFiles`.

### Cambios por archivo

- `Services/FileService.cs`
  - Añadir `MaxPinnedFiles = 10`.
  - Añadir `LoadPinnedFiles()` y `SavePinnedFiles(List<string>)`.
  - Refactor menor: extraer `GetSettingsPath(string fileName)` y dejar `GetSettingsPath()` como wrapper que pasa `"recent.txt"`.

- `MainWindow.xaml`
  - El `MenuItem` de `MainWindow.xaml:77-85` queda con un `x:Name` (p. ej. `RecentFilesMenuItem`) y sin `ItemsSource` ni `ItemContainerStyle`. Sus `Items` se rellenan desde code-behind.

- `MainWindow.xaml.cs`
  - Añadir `ObservableCollection<string> PinnedFiles`.
  - Añadir comandos `PinCommand` / `UnpinCommand` (`IRelayCommand<string>`).
  - Añadir método `RebuildRecentMenu()` que construye los `MenuItem` programáticamente y se llama tras cambios en cualquiera de las dos listas (suscribiéndolo al `CollectionChanged` de ambas).
  - Modificar `AddToRecent` para respetar la invariante: si la ruta está en `PinnedFiles`, no se añade a `RecentFiles`.
  - En `LoadOrCreateInitialTab` (`MainWindow.xaml.cs:145`), cargar también `PinnedFiles` desde `FileService.LoadPinnedFiles()`.
  - `OpenRecentCommand` se mantiene y sirve para ambas secciones; el handler `OpenRecentFile` (`MainWindow.xaml.cs:606`) detecta si la ruta es fijada y si falla la apertura, dispara el diálogo descrito en "Archivos inexistentes".

### Fuera de alcance

- Reordenación manual de fijados (drag & drop, flechas).
- Iconografía vectorial / Segoe MDL2 Assets.
- Sincronización de fijados entre instalaciones (cloud, etc.).
- Atajos de teclado para fijar/desfijar.
- Migración a JSON o esquema con metadatos extra (fecha de fijado, etiquetas, etc.).

## Validación

Smoke test manual en Windows tras compilar (`build.ps1` desde WSL vía `powershell.exe`):

1. **Carga inicial** — instalación nueva (sin `pinned.txt`): el menú muestra solo recientes; no hay separador.
2. **Fijar un reciente** — clic en chincheta tenue: el archivo desaparece de recientes y aparece arriba en una nueva sección, con separador. Se crea `%AppData%\QuillMD\pinned.txt` con la ruta.
3. **Reabrir el fijado** — abrir un archivo fijado desde el menú: la posición no cambia.
4. **Abrir un reciente que existe** — sigue moviéndose al principio de recientes (no regresión).
5. **Desfijar** — clic en chincheta opaca: el archivo desaparece de fijados y aparece al principio de recientes.
6. **Tope de fijados** — fijar 10 archivos, intentar fijar el 11º: aparece `MessageBox` y la operación se cancela.
7. **Persistencia** — reiniciar QuillMD: ambas secciones se recuperan con su orden.
8. **Fijado borrado** — fijar un archivo, borrarlo del disco, hacer clic: aparece el diálogo Sí/No. **Sí** lo quita de fijados; **No** lo deja. Cada opción se comporta como se describe.
9. **Apariencia** — chinchetas opacas para fijados, tenues para recientes; al hacer hover sobre una tenue sube a opacidad plena.
10. **Compatibilidad** — instalación existente con `recent.txt` poblado: tras actualizar, los recientes se preservan; la sección de fijados arranca vacía.
