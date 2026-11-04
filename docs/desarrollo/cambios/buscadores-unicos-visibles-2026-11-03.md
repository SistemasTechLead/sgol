# Buscadores únicos visibles

Cambio de interfaz del 03/11/2026. Se conserva la consulta GET y sus parámetros; no hay cambios de persistencia, permisos, asignaciones ni datos simulados.

## Alcance e investigación

Los adaptadores `wwwroot/js/mobile.js` y `desktop-workspace.js` plegaban formularios que solo tenían una búsqueda. Se identificaron cuatro consumidores:

| Pantalla | Fuente | Consulta |
|---|---|---|
| Mi trabajo | `Pages/MyWork/_OperationalTasks.cshtml` | `WorkSearch`, Buscar y Limpiar |
| Asignar tarea ocasional | `Pages/Occasional/Index.cshtml` | `Search` |
| Añadir tareas a un proceso | `Pages/Tasks/Processes.cshtml` | `Search` e identificador oculto `id` |
| Detalle del reporte | `Pages/Reports/Index.cshtml` | `DetailSearch` y parámetros ocultos del reporte |

`SgolSearch.isStandalone` identifica un único input de búsqueda, excluyendo campos ocultos y acciones. Los dos adaptadores omiten el plegado en ese caso. `interactive-controls.css` alinea campo y acciones sin las columnas vacías de la antigua cuadrícula. Los filtros múltiples y los selectores únicos de semana/fecha/estado conservan su comportamiento. El layout dedicado del tablero no carga `search-text.js`; el adaptador móvil admite esa ausencia.

Se inspeccionaron los formularios de Tareas, Personas, Supervisión, Planificación, Mi trabajo y Reportes, además del componente `_DataTable`. La búsqueda de Planificación ya es directa. No se alteraron productores de datos ni handlers GET: la corrección mantiene los mismos elementos, nombres, valores, acciones y propietarios de formulario.

## Matriz y evidencia

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Cuatro formularios, 320/768/1440 px y vuelta a 320, light/dark/special | Búsqueda visible sin desplegable, sin desbordamiento | Playwright Chromium/Edge y WebKit, componentes con marcado extraído de Razor y CSS/JS reales | Aprobado, 96 combinaciones |
| Teclado y texto largo | Tab alcanza Buscar; Enter envía campos originales | Componentes, submit interceptado sin petición | Aprobado |
| Contexto oculto, cambios de ancho e impresión | FormData conserva búsqueda y contexto | Componentes | Aprobado |
| Formulario con búsqueda y selector | Conserva desplegables de escritorio y móvil | Componente de control | Aprobado |
| Sintaxis de los tres JS | Parseo correcto | `node --check` | Aprobado |
| Recursos efectivos de la app compartida | HTTP 200 y contenido igual al checkout | GET interno 7241; proceso 38004, ensamblado monthly-carryover-20261103 | Aprobado |
| Pantallas completas autenticadas y resultados GET, vacío/error | Buscar y Limpiar conservan resultados/contexto | Intento con sesión privada guardada redirige a `/acceso` | No ejecutado: sesión no vigente |
| Migraciones, concurrencia de escritura, histórico | Sin cambios | Cambio de presentación sin escrituras | No aplicable |

Regresión: `tools/Sgol.MobileChecks/standalone-search-checks.cjs`, con Playwright disponible en NODE_PATH. Evidencia privada: `.local/standalone-search/results.json` y capturas. Se inspeccionaron capturas de Mi trabajo a 320 y 1440 px; son componentes, no la pantalla autenticada completa. No se compiló .NET: no cambian C#, Razor ni ensamblados.

## Disponibilidad y documentación

Consultados Inicio → mapa de infraestructura → instalación local → base documental y acceso LAN. El lanzador usa un ensamblado publicado pero establece el directorio de trabajo en `src/Sgol.Web`; la comparación HTTP confirma que los cuatro recursos estáticos modificados ya se sirven desde el checkout. No se copió ninguna publicación, no se reinició el host y no se modificó la base. La sesión ya abierta requiere recargar para obtener los recursos nuevos.

Actualizadas las referencias existentes de componentes, pantallas e instalación local de la bóveda. La validación de las pantallas autenticadas sigue pendiente; la comprobación de archivos servidos no la sustituye. Modificaciones ajenas del checkout conservadas. Sin commit ni push.
