# Filtros del catálogo de tareas — 2 de noviembre de 2026

## Alcance y decisiones

Cambio mixto de consulta e interfaz en `/tareas`. Antes solo existían búsqueda por nombre/código y proceso. Ahora se combinan requisito, periodicidad efectiva, criticidad (incluida sin definir), estado, rol, calendario y repeticiones exactas (1–100). Los filtros se intersectan; vacío significa cualquier valor. Aplicar reinicia la página y Limpiar vuelve al catálogo completo. Se conserva la consulta al paginar y se informa el total.

Las tareas de procesos muestran «Inicio por proceso»: su periodicidad individual almacenada no representa su ejecución. Calendario y repeticiones solo seleccionan tareas periódicas de General; las ocasionales y etapas quedan excluidas de estos dos filtros. La ayuda del formulario explica la regla. No se modifican cargas, responsables, plazos, historial ni datos existentes.

## Trazabilidad e invariantes

- `OperationalTasks.cs`: definición persistida; `Index.OnPostSaveAsync` y `OperationalTaskService.SaveAsync` producen el catálogo. Ninguna escritura cambia.
- `TaskCatalogFilter.Apply`: predicados SQL sobre `OperationalTask`; `OperationalTaskService.PageAsync` autoriza, excluye eliminadas, aplica búsqueda/proceso/filtros antes de contar y paginar.
- `Index.cshtml.cs`: binding GET y rechazo comprensible de entradas inválidas; `Index.cshtml`: controles existentes de SGOL, total y limpieza.
- `Presentation/Navigation/ListPage.cs`: conserva parámetros al paginar, sin cambios.
- `task-catalog-mobile.css`: rejilla adaptable; se reutiliza el plegado existente de `mobile.js` sin modificarlo.
- Copias en asignaciones, reservas de procesos, ejecución, revisión, reportes e historial no consumen el nuevo objeto de filtros y conservan sus rutas y snapshots. No hay migración de producto.

Se consultaron AGENTS.md, PROTOCOLO_IMPLEMENTACION.md y ESTILO_VISUAL_V2.md. La restricción histórica de ocho TAR no corresponde al catálogo operativo actual y no se reintroduce.

## Entorno

Rama `master`, con cambios previos de planificación, JS móvil y pruebas que se conservaron. En `tools/Sgol.TaskChecks/Program.cs` esta tarea añade únicamente el despacho `--task-catalog-filters`.

Pruebas en PostgreSQL local, puerto 5440, base nueva `sgol_task_checks_filters_20261102`; nunca en `sgol_local`. Host aislado HTTPS en localhost:7293, páginas Razor, servicios y consultas SQL reales, con sesión de prueba sustituida explícitamente. Fechas de fixture fijadas por `CorrectionSchedulingChecks.Clock`; no se simula autenticación de usuario final. Sin escritura ni reinicio en la simulación compartida. No se desplegó ni se hizo commit/push.

## Matriz ejecutada

| Precondición y acción | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| 61 tareas coincidentes; combinar siete atributos y cambiar página | Total 61, páginas 50/11 sin duplicados | Servicio real / PostgreSQL aislado | Aprobado |
| Inactiva con criticidad histórica nula | Encontrarla con ambos filtros | Servicio real / aislado | Aprobado |
| Ocasional o etapa con parámetros individuales | Excluir de repeticiones; etapa solo por inicio de proceso | Servicio real / aislado | Aprobado |
| Proceso concreto combinado con periodicidad semanal | Sin resultados engañosos de calendario heredado | Servicio real / aislado | Aprobado |
| Catálogo sin filtros con tarea eliminada | Incluir inactivas y etapas, excluir eliminada | Servicio real / aislado | Aprobado |
| Sin coincidencias, página fuera de rango | Total cero y página normalizada | Servicio real / aislado | Aprobado |
| Actor de piso de ventas consulta catálogo | 403 | Autorización real / aislado | Aprobado |
| GET con requisito/repeticiones y página 2 | Binding, total y enlaces conservan filtros | HTTP Razor / aislado | Aprobado |
| Cantidad no numérica, 101, estado inválido, GUID inválido | Mensaje para corregir o limpiar | HTTP Razor / aislado | Aprobado |
| Comparar catálogo antes/después de consultas | Sin cambios persistidos | Snapshot JSON / aislado | Aprobado |
| 320, 390, 768 y 1440 px; temas claro, oscuro y especial | Controles dentro del ancho y sin desbordamiento global | Chromium/Edge y WebKit / página real | Aprobado, 24 combinaciones |
| Abrir filtros con teclado, aplicar, paginar, vaciar y limpiar | Interacción y navegación coherentes | Ambos motores / página real | Aprobado |
| Error de filtro en pantalla | Alerta visible | Ambos motores / página real | Aprobado |
| Compilar Web y herramienta | Sin errores ni advertencias | dotnet build | Aprobado |
| Safari físico, lector de pantalla, texto al 200% | Accesibilidad ampliada | Dispositivos/AT | No ejecutado |
| Autenticación real y carga local/LAN compartida | Nueva versión visible en simulación | Entorno compartido | No ejecutado: sin despliegue |
| Crear/asignar/entregar/revisar y concurrencia de escritura | Conservar comportamiento | No hay cambios en escrituras | No aplicable a estos predicados de consulta |

## Evidencia y reproducción

- `tools/Sgol.TaskChecks/TaskCatalogFilterChecks.cs`: 14 comprobaciones específicas de integración/HTTP. Ejecutar `--task-catalog-filters` con `SGOL_TASK_CHECKS_CONNECTION` apuntando a base aislada; `--ui` mantiene el host para navegador.
- `tools/Sgol.MobileChecks/task-catalog-filters.cjs`: consume el directorio con `checks.log` del host anterior. Requiere Playwright en NODE_PATH.
- Evidencia local ignorada: `.local/task-filters-20261102/checks.log`, `browser-results.json` y capturas de ambos motores en tres temas.
- Se corrigieron fixtures iniciales que excedían longitud de código y la restricción de ocasionales; no se relajaron validaciones. La prueba visual excluye campos hidden del cálculo de cajas y espera navegación antes de comprobar resultados.

La repetición final de navegador tuvo un timeout esperando el evento `load`. El host respondió HTTP 200 al comprobarlo. Se cambió la espera a DOMContentLoaded (manteniendo esperas explícitas por controles y resultados); la ejecución completa posterior aprobó nuevamente las 24 combinaciones y los recorridos en ambos motores. No se atribuyó el timeout a una causa de producto sin evidencia. Se inspeccionaron visualmente capturas de escritorio y el formulario móvil.

## Despliegue autorizado — 2 de noviembre de 2026

El usuario solicitó desplegar en la aplicación compartida. Se publicó y cargó `.local/runtime/task-filters-20261102/Sgol.Web.dll`, versión `1.0.0+task-filters-20261102`, conservando la actualización anterior de planificación `plan-moves-overdue-20261102`. Cambio de proceso con comprobación de ruta/hash y recuperación automática preparada. PID verificado al cargar: 25556.

Aprobados: salud interna live/ready, salud LAN, acceso HTTP 200 en localhost:7240 y LAN:7440, estilos de los filtros servidos desde ambos accesos. Base identificada: 127.0.0.1:5440/sgol_local; última migración existente `20261031222522_AddCorrectionScheduling`. No se ejecutaron migraciones ni escrituras de prueba sobre tareas/asignaciones compartidas. Sin commit/push.

Evidencia: `.local/deploy-task-filters-20261102/preflight.json`, `deployment.json`, `http-check.json` y logs. La sesión de navegador guardada ya no autorizó `/tareas` y redirigió a `/acceso`: no se declara comprobación autenticada de filtros en el entorno compartido. Se mantienen las pruebas funcionales y visuales aisladas aprobadas previamente. La comprobación de versión cargada y salud compartida sí quedó aprobada.
