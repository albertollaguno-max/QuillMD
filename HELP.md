# PlannamTypora - Guía de uso

## Modos de visualización

PlannamTypora ofrece cuatro modos de trabajo, accesibles desde el menú **Visualización** o la barra de herramientas:

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

PlannamTypora guarda automáticamente al cerrar:
- Tema activo
- Modo de visualización
- Estado de la barra lateral (visible/oculta y ancho)
- Tamaño de fuente
- Posición y tamaño de ventana
- Estado de la barra de estado
- Opción "Siempre encima"

La configuración se almacena en `%AppData%/PlannamTypora/settings.json`.
