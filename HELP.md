# QuillMD - Guía de uso

## Modos de visualización

QuillMD ofrece cuatro modos de trabajo, accesibles desde el menú **Visualización** o la barra de herramientas:

| Modo | Descripción |
|---|---|
| **Editor** | Solo el editor de código Markdown |
| **Preview** | Solo la vista previa renderizada |
| **Dividida (Split)** | Editor a la izquierda, preview a la derecha |
| **WYSIWYG** | Edición visual directa (lo que ves es lo que obtienes) |

## Atajos de teclado

### Archivo

| Atajo | Acción |
|---|---|
| `Ctrl+N` | Nueva pestaña |
| `Ctrl+O` | Abrir archivo |
| `Ctrl+Alt+I` | Importar documento (PDF, DOCX, etc.) |
| `Ctrl+S` | Guardar |
| `Ctrl+Shift+S` | Guardar como |
| `Ctrl+W` | Cerrar pestaña |
| `Ctrl+Tab` | Siguiente pestaña |
| `Ctrl+Shift+Tab` | Pestaña anterior |

### Edición

| Atajo | Acción |
|---|---|
| `Ctrl+Z` | Deshacer |
| `Ctrl+Y` | Rehacer |
| `Ctrl+X` | Cortar |
| `Ctrl+C` | Copiar |
| `Ctrl+V` | Pegar |
| `Ctrl+A` | Seleccionar todo |
| `Ctrl+Shift+C` | Copiar como Markdown |
| `Ctrl+Shift+V` | Pegar como texto sin formato |
| `Ctrl+F` | Buscar |
| `Ctrl+H` | Buscar y reemplazar |

### Formato inline

| Atajo | Acción |
|---|---|
| `Ctrl+B` | **Negrita** |
| `Ctrl+I` | *Cursiva* |
| `Ctrl+E` | `Código inline` |
| `Ctrl+K` | Insertar enlace |
| `Ctrl+Shift+I` | Insertar imagen |

### Párrafo

| Atajo | Acción |
|---|---|
| `Ctrl+0` | Párrafo normal |
| `Ctrl+1` a `Ctrl+6` | Encabezado H1 a H6 |

### Insertar

| Atajo | Acción |
|---|---|
| `Ctrl+T` | Insertar tabla |
| `Ctrl+Shift+K` | Bloque de código |
| `Ctrl+Enter` | Agregar fila a tabla (dentro de tabla) |

### Tablas (dentro de una tabla)

| Atajo | Acción |
|---|---|
| `Tab` | Ir a la siguiente celda |
| `Shift+Tab` | Ir a la celda anterior |
| `Ctrl+Enter` | Agregar fila debajo |
| `Alt+Arriba` | Mover fila arriba |
| `Alt+Abajo` | Mover fila abajo |
| `Alt+Izquierda` | Mover columna a la izquierda |
| `Alt+Derecha` | Mover columna a la derecha |

### Visualización

| Atajo | Acción |
|---|---|
| `Ctrl+Shift+L` | Mostrar/ocultar barra lateral |
| `Ctrl++` | Acercar (aumentar fuente) |
| `Ctrl+-` | Alejar (reducir fuente) |
| `F8` | Modo sin distracciones |
| `F11` | Pantalla completa |

### En WYSIWYG

| Atajo | Acción |
|---|---|
| `Ctrl+Click` en enlace | Abrir enlace en el navegador |

## Archivos recientes

Desde **Archivo > Archivos recientes** se accede al historial de los últimos archivos abiertos.

- Cada entrada muestra el nombre del archivo (la ruta completa aparece en el tooltip al pasar el ratón).
- A la izquierda de cada entrada hay un icono de chincheta 📌. Pulsa la chincheta tenue de un archivo reciente para **fijarlo**: pasa a la sección superior y deja de rotar fuera del menú al abrir nuevos archivos.
- Pulsa la chincheta opaca de un fijado para **desfijarlo**: vuelve a la sección de recientes.
- Una línea horizontal separa los fijados (arriba) de los recientes (abajo).
- Tope: 10 fijados y 10 recientes (independientes).
- Si un fijado apunta a un archivo borrado, al hacer clic aparece un diálogo Sí/No para quitarlo de la lista (útil si está en una unidad temporalmente desconectada y prefieres mantenerlo).

## Explorador de archivos

1. Abre una carpeta desde **Archivo > Abrir carpeta**
2. La barra lateral muestra el árbol de archivos `.md`
3. Haz clic en un archivo para abrirlo en una nueva pestaña
4. Alterna entre **Archivos** e **Índice** con los botones de la barra lateral
5. El panel de **Índice** muestra los encabezados del documento actual; haz clic para navegar

## Edición de tablas en WYSIWYG

Al hacer clic dentro de una tabla en modo WYSIWYG aparece una **barra flotante** sobre la tabla con:

- **Botones de alineación**: alinear izquierda, centro o derecha la columna actual
- **Más acciones** (icono de tres puntos): insertar/eliminar filas y columnas, mover, copiar tabla
- **Eliminar tabla** (icono de papelera)

También puedes acceder a todas estas opciones desde el **menú contextual** (clic derecho) dentro de la tabla.

## Buscar y reemplazar

1. Abre con `Ctrl+F` (buscar) o `Ctrl+H` (buscar y reemplazar)
2. Opciones disponibles:
   - **Aa**: coincidencia de mayúsculas/minúsculas
   - **Regex**: búsqueda con expresiones regulares
3. Usa las flechas para navegar entre coincidencias
4. En modo reemplazar, puedes reemplazar una a una o todas a la vez

## Exportar

### HTML
**Archivo > Exportar HTML** genera un archivo `.html` completo con estilos incluidos.

### PDF
**Archivo > Exportar a PDF** genera un archivo PDF a partir de la vista renderizada.

## Temas

Cambia entre tema claro y oscuro desde:
- El botón de tema en la barra de herramientas (sol/luna)
- **Visualización > Tema claro / Tema oscuro**

## Configuración persistente

QuillMD guarda automáticamente al cerrar:
- Tema activo
- Modo de visualización
- Estado de la barra lateral (visible/oculta y ancho)
- Tamaño de fuente
- Posición y tamaño de ventana
- Estado de la barra de estado
- Opción "Siempre encima"

La configuración se almacena en `%AppData%/QuillMD/settings.json`.

## Importar documentos

QuillMD puede importar formatos no-Markdown y convertirlos a Markdown:

- **PDF** (`.pdf`)
- **Word** (`.docx`)
- **PowerPoint** (`.pptx`)
- **Excel** (`.xlsx`, `.xls`)
- **HTML** (`.html`, `.htm`)
- **EPUB** (`.epub`)
- **Outlook** (`.msg`)
- **Datos estructurados** (`.csv`, `.json`, `.xml`)
- **ZIP** (`.zip` — itera por el contenido)

### Cómo importar

- **Menú:** `Archivo → Importar...` (atajo `Ctrl+Alt+I`)
- **Drag & drop:** arrastra el archivo a la ventana de QuillMD

La conversión abre el documento convertido en una pestaña nueva sin guardar. Al pulsar `Ctrl+S` la primera vez, se sugiere guardar como `<nombre-original>.md` en la carpeta del archivo fuente.

### Limitaciones

- La calidad de la conversión depende del formato origen. Los PDF complejos (multi-columna, tablas anidadas, escaneos sin OCR) pueden perder maquetación; es una limitación de markitdown, no de QuillMD.
- Las imágenes embebidas no se extraen a archivos; markitdown genera placeholders o las omite según el formato.
- Transcripción de audio y vídeos de YouTube no están disponibles en v1.
- Timeout por defecto: 60 segundos por conversión. Archivos muy grandes pueden abortarse.
