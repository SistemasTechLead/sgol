# Markdown en descripciones e instrucciones

## Alcance y conservación

Cambio mixto de presentación: interpretar Markdown únicamente al consultar descripciones de tareas y procesos e instrucciones. Los textarea, validaciones, límites, persistencia y snapshots conservan el texto fuente. Los registros existentes interpretan las marcas que ya contengan; no hay migración ni actualización masiva. No se cambia la presentación de comentarios de entrega/revisión, títulos, causas ni el texto para copiar/publicar.

Productores revisados: `OperationalTaskService`, `OperationalTasks`, `OperationalTaskService.OccasionalAccess`, `OperationalProcessCatalogService`. Copias: `OperationalPlanGenerator`, `AssignmentDefinition`, `AssignmentRefreshService`, `OperationalTaskService.OccasionScheduling` y `OperationalSupervisionService`. Sus contratos permanecen intactos.

Consumidores incluidos: `/tareas`, `/tareas/asignar`, `/tareas/procesos`, `/planificacion/procesos`, el diálogo de tarea de Mi trabajo, `_ProcessActionForm`, `_OccasionMaterial` y `Validations/Index`. Todos usan `_Markdown.cshtml` y `MarkdownContent.Render`.

## Seguridad y formato

Markdig 1.5.1, fijado en administración central y lockfiles, interpreta Markdown en el servidor. Se habilitan negritas, cursivas, tachado, títulos, listas, citas, código, tablas y enlaces. Se conservan los saltos de línea simples.

- HTML deshabilitado en el parser: etiquetas aportadas por el usuario se codifican como texto.
- Solo enlaces absolutos HTTP, HTTPS y mailto. Enlaces relativos, protocolos de ejecución y datos se muestran sin enlace; autolinks pasan por la misma lista de protocolos.
- Imágenes Markdown muestran su texto alternativo sin descargar contenido remoto.
- No extensiones de atributos, HTML genérico, diagramas ejecutables ni JavaScript de renderizado. El resultado de este renderizador es el único HTML confiado por el parcial.
- Los enlaces permitidos pueden llevar a sitios externos: la lista de protocolos no certifica su contenido.

Referencia de implementación: [renderizadores de Markdig](https://xoofx.github.io/markdig/docs/advanced/renderers/).

## Matriz y evidencia

| Escenario | Esperado / prueba | Entorno | Estado |
|---|---|---|---|
| Sintaxis, texto normal, vacío, saltos, texto incompleto, 4000 caracteres | Renderizado estructurado, contenido conservado; `Sgol.MarkdownChecks` | Aislado sin BD | Aprobado: 11 casos, incluyendo seguridad |
| HTML, script, SVG, iframe, atributos ejecutables | Texto inerte; ausencia de nodos ejecutables | Renderizador y navegador sin CSP para no ocultar fallos | Aprobado |
| javascript/data/vbscript/file, entidades, esquemas mixtos, autolinks, referencias | Sin destino ejecutable | Renderizador y DOM real | Aprobado |
| Imágenes externas | Texto alternativo, sin petición de imagen | Navegador | Aprobado |
| Editor y consulta | Fuente original en textarea, formato al consultar | Componente aislado | Aprobado; no equivale a guardar una tarea real |
| 320/1280 px, temas light/dark/special, Chromium/WebKit | Sin desbordamiento global; enlaces enfocables; details abre/cierra con teclado | Componente con CSS real | Aprobado: 12 combinaciones; capturas en `artifacts/markdown-checks` |
| Integración Razor | Todas las vistas compilan | Build aislado | Aprobado: cero errores y advertencias |
| Crear/editar, guardar/reabrir en pantallas autenticadas | Persistencia literal y consulta formateada de tarea/proceso | Aplicación completa | No ejecutado: sesión de pruebas disponible vencida; navegador del usuario sin conexión habilitada |
| Permisos, concurrencia, cargas, históricos | Sin cambios en servicios ni escrituras de datos por esta implementación | Revisión del diff y productores/consumidores | Inspeccionado; regresiones de BD no aplicables a cambios de persistencia porque no los hay |
| Actualización local autorizada | Proceso carga el ensamblado compilado y sirve acceso/CSS | Aplicación local con proxy existente | Aprobado: proceso ejecuta `artifacts/markdown-build/bin/Sgol.Web/debug/Sgol.Web.dll`; `/acceso` y `/css/markdown.css` HTTP 200 |

Comandos reproducibles:

```powershell
dotnet build src/Sgol.Web -p:UseSharedCompilation=false --artifacts-path artifacts/markdown-build -v quiet
dotnet run --project tools/Sgol.MarkdownChecks -p:UseSharedCompilation=false
# Con Playwright disponible en NODE_PATH:
node tools/Sgol.MobileChecks/markdown-checks.cjs
```

Se preservaron los cambios previos del workspace. La actualización local inicia el ensamblado construido con el lanzador existente y conserva proxy, configuración y BD; no ejecuta migraciones ni genera tareas de prueba en la simulación diaria. Sin commit ni push. Estado: implementado y disponible localmente, con validación del flujo autenticado pendiente.
