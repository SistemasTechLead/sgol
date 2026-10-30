# Etapas de procesos en Mi trabajo

Cambio mixto, 29/10/2026.

## Causa observada y alcance

La consulta de `OperationalTaskService.MineQuery` solo devuelve `OperationalAssignment`.
El motor crea esa asignación al habilitar la etapa, mientras que `StartAsync` y
`ProcessPlanningService.ActivateAsync` ya reservan responsables y carga para las demás
etapas. En la simulación compartida se observaron una etapa PROGRAMADA y dos EN_ESPERA,
sin asignaciones efectivas. Se consultaron agregados; no se modificaron esos datos.

`MinePageAsync` incluye ahora las reservas de ejecuciones EN_CURSO que pertenecen a la
persona autenticada y cuyo intento vigente todavía no tiene asignación. Se combinan
antes de ordenar y paginar en SQL. Una lectura con snapshot consistente evita mezclar
la reserva y su activación concurrente entre consultas de una misma página.

La tabla compartida muestra En espera, Programada o Pendiente de asignación, fechas
estimadas y Ver proceso. Las asignaciones efectivas conservan su formulario de entrega.
El temporizador cliente conserva el estado gris de las reservas aunque pase su fecha
estimada. El progreso diario sigue contando solamente asignaciones efectivas.

## Invariantes y recorrido

- No se crean asignaciones, migraciones ni nuevas reservas de capacidad por consultar.
- `OperationalProcessService.EnableScheduledStage` sigue decidiendo la activación.
- `MineFindAsync`, las evidencias y las entregas solo aceptan asignaciones efectivas.
- El intento vigente sustituye visualmente a su reserva sin duplicación al activarse.
- Las reservas de planes preparados sin activar no se incorporan; aún no hay ejecución.
- Las etapas de ejecuciones cerradas/canceladas y los intentos sustituidos conservan su historial.
- Se reutilizan `_OperationalTaskTable`, sus estilos móviles y la navegación existente.

## Matriz de validación

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Responsable con etapa EN_ESPERA | Fila en la misma tabla y acceso al proceso | Servicio y Razor HTTP, base aislada | Aprobado: log f |
| Búsqueda por código y página fuera de rango | Encuentra la reserva y ajusta página | Regresión en ProcessRuntimeChecks | Aprobado: log f |
| Otra persona / acceso por ID reservado | Sin filas ajenas; detalle ejecutable 404 | Servicio, base aislada | Aprobado: log f |
| Entrega de la etapa anterior | Una tarea efectiva sustituye a la reserva al llegar a su hora | Formulario HTTP con CSRF, TickAsync y servicio real | Aprobado: log f |
| Reserva PROGRAMADA | Conserva una sola fila pendiente hasta su activación | Servicio y base aislada | Aprobado: log f |
| PENDIENTE_ASIGNACION | Gris, mensaje propio, sin entrega | Consulta y presentación inspeccionadas | No ejecutado: sin fixture específico |
| Reintento y rechazo de entrega | No duplica PDF; conserva formulario y token de reintento; rechaza CSRF ausente, revisión obsoleta y asignación ajena | Formularios HTTP reales | Aprobado: log f |
| Retorno e historial de rondas | Solo intento vigente; historial previo preservado | Suite general de procesos | No ejecutado: suite histórica requiere adaptar sus avances de reloj al horario reservado |
| Escritorio y 320 px | Contenido completo, sin overflow global; scroll contenido en tabla de escritorio | Navegador contra Razor y servicios reales con sesión aislada | Aprobado |
| Temas y teclado | Estado gris legible y enlace accesible | Claro y especial a 320 px; oscuro en escritorio; Tab y Enter navegan al proceso correcto | Aprobado |
| Despliegue local/LAN | Versión cargada comprobada | Publicación local y reinicio exclusivo del servidor web el 30/10/2026 | Aprobado en servidor; pendiente inspección de la sesión de Brave |

Las primeras ejecuciones de la herramienta encontraron fixtures antiguos sin
criticidad ni horario obligatorio. Se actualizaron sus datos de ejemplo; no se
flexibilizaron validaciones del producto. Evidencia local en
`artifacts/process-my-work-checks-*.log` y compilación aislada en
`artifacts/process-my-work-build`.

Comprobación específica aprobada: `dotnet artifacts/process-my-work-build/bin/Sgol.TaskChecks/debug/Sgol.TaskChecks.dll --process-mywork-only`,
con `SGOL_TASK_CHECKS_CONNECTION` apuntando exclusivamente a `sgol_task_checks_mywork_f_20261029`.
Resultado final: `Process My work integration checks passed.` La herramienta se compiló
de nuevo tras ajustar el texto de progreso y se verificó ese texto en navegador.
La vista reutilizable `--process-mywork-preview` funciona solo con una base dedicada de
pruebas ya preparada. Expone exclusivamente loopback y usa la sesión ficticia de la
herramienta; no verifica el inicio de sesión real ni el acceso LAN.

## Carga en el entorno compartido — 30/10/2026

Autorización del usuario: «Vamos a cargarla y comprobarla en mi sesion local».

Se comprobó que localhost:7240 redirige al proxy LAN y ambos destinos sirven la misma
aplicación en 127.0.0.1:7241, conectada a `sgol_local` mediante la cuenta runtime.
La última migración ya aplicada era `20261029215055_AddProcessCatalogDeletion`;
no se ejecutaron migraciones ni preparación de datos.

Se publicó el código actual en `.local/runtime/process-my-work-20261030/Web`.
Se sustituyó el servidor de `.local/runtime/process-diagnostics-20261029/Web` y
se detuvo un segundo lanzador web que fallaba por puerto ocupado. El proxy se
conservó. `.local/processes.json` registra el nuevo lanzador para próximas operaciones.
El proceso que escucha 7241 carga expresamente el nuevo DLL. Huella y ruta de
reversión: `.local/runtime/process-my-work-20261030/deployment.json`.

Comprobaciones aprobadas: `health/live` devuelve alive tanto por loopback como por
el proxy HTTPS; `health/ready` interno devuelve ready; el JavaScript servido por
HTTPS incluye el tratamiento de las reservas; el registro de errores del nuevo
lanzador está vacío. No se enviaron entregas ni se modificaron asignaciones como
parte de la comprobación.

La inspección de la ventana «Mi trabajo — SGOL - Brave» fue detenida por el control
de aplicaciones: no pudo determinar su URL con confianza suficiente para aplicar
su política. No se intentó otra vía de control de esa ventana ni se extrajeron
cookies. La revisión visual dentro de la sesión del usuario requiere que este
recargue la pantalla; no se presenta como ejecutada.
