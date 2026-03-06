# PlannamTypora

Editor de Markdown para Windows inspirado en [Typora](https://typora.io/), construido con WPF (.NET 9).

![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D4)
![License](https://img.shields.io/badge/License-MIT-green)

## Características

### Editor
- Editor de código con resaltado de sintaxis Markdown (AvalonEdit)
- Vista previa en tiempo real con renderizado FlowDocument
- Modo WYSIWYG completo con edición visual (WebView2)
- Vista dividida editor + preview
- Pestañas múltiples con indicador de cambios sin guardar
- Buscar y reemplazar con soporte de expresiones regulares y coincidencia de mayúsculas

### Formato
- **Inline**: negrita, cursiva, tachado, código inline, enlaces, imágenes
- **Bloques**: encabezados H1-H6, párrafo, citas, listas (viñetas, numeradas, tareas), bloques de código, ecuaciones, líneas horizontales
- **Tablas**: inserción, edición de celdas, agregar/eliminar filas y columnas, mover filas/columnas, alineación de texto por columna, barra flotante de herramientas al estilo Typora
- Notas al pie y tabla de contenidos automática

### Interfaz
- Temas claro y oscuro
- Explorador de archivos lateral con árbol de carpetas
- Panel de índice/outline con navegación a secciones
- Barra de estado con información de línea, columna, palabras y modo activo
- Menú contextual completo tanto en editor de código como en WYSIWYG
- Modo pantalla completa y modo sin distracciones
- Zoom (Ctrl+/Ctrl-)

### Exportación
- Exportar a HTML
- Exportar a PDF

### Persistencia
- Guardado automático del estado de la aplicación (tema, modo de vista, sidebar, posición de ventana, tamaño de fuente)
- Historial de archivos recientes

## Requisitos

- Windows 10/11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (para compilar)
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (incluido en Windows 10/11 actualizados)

## Compilar

```bash
# Debug
dotnet build

# Release (ejecutable independiente)
dotnet publish -c Release -r win-x64 --self-contained
```

El ejecutable se genera en `bin/Release/net9.0-windows/win-x64/publish/`.

## Ejecutar

```bash
dotnet run
```

O abrir directamente el `.exe` generado en la carpeta de publicación.

## Atajos de teclado

Consulta el archivo [HELP.md](HELP.md) para la lista completa de atajos y guía de uso.

## Tecnologías

| Componente | Tecnología |
|---|---|
| Framework UI | WPF (.NET 9) |
| Editor de código | AvalonEdit |
| Parsing Markdown | Markdig |
| Vista WYSIWYG | WebView2 |
| HTML a Markdown | ReverseMarkdown |
| Exportación PDF | WebView2 PrintToPdfAsync |

## Estructura del proyecto

```
PlannamTypora/
├── MainWindow.xaml          # Layout principal (XAML)
├── MainWindow.xaml.cs       # Lógica principal
├── App.xaml / App.xaml.cs   # Configuración de la aplicación y temas
├── Services/
│   ├── WebPreviewBridge.cs  # Motor WYSIWYG (HTML/CSS/JS para WebView2)
│   ├── MarkdownConverter.cs # Markdown a FlowDocument
│   ├── HtmlToMarkdown.cs    # HTML a Markdown (ReverseMarkdown)
│   ├── FileService.cs       # Operaciones de archivo y recientes
│   ├── SettingsService.cs   # Persistencia de configuración
│   └── TableEditHelper.cs   # Edición de tablas en modo código
├── Themes/
│   ├── DarkTheme.xaml       # Tema oscuro
│   └── LightTheme.xaml      # Tema claro
└── docs/
    └── plans/               # Documentos de diseño
```

## Licencia

MIT
