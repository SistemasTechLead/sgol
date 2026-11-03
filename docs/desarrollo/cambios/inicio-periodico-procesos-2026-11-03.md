# Inicio periódico de procesos y operaciones semanales

## Alcance y decisiones

Cambio mixto solicitado el 3 de noviembre de 2026. El usuario confirmó:

- Programar la primera tarea y reservar capacidad de las siguientes; conservar la liberación por entrega/aprobación.
- Crear ejecuciones independientes aunque la repetición anterior siga pendiente.
- Incluir repeticiones y fechas; excluir metas por progreso de este cambio.

Inicio incorpora semanal, mensual, trimestral y anual, conservando ocasional y semestral. Cada versión guarda `StartScheduleJson`: modo distribuido/días fijos, repeticiones, días, fechas del mes, hora con minutos y participación en operaciones semanales. El checkbox solo corresponde a semanal. No se modifica la programación independiente de las tareas ni sus snapshots históricos.

Los inicios distribuidos admiten hasta uno por día nominal: 7 semanales, 28 mensuales, 90 trimestrales y 100 anuales. Las fechas mensuales explícitas determinan las repeticiones y omiten fechas inexistentes. Esta implementación no admite varias repeticiones del mismo proceso con la misma fecha nominal. Los parámetros del inicio pertenecen al proceso; las cargas, responsables y condiciones de etapas pertenecen a sus tareas.

## Investigación y recorrido

Antes del cambio, `Processes.cshtml`, `OperationalProcessCatalogService.ValidateRulesCore`, `OperationalSequenceRules.Dates` y la restricción `CK_sequence_calendar` solo admitían ocasional/semestral. El catálogo semanal filtraba tareas de General y no consultaba procesos.

- Editor: `Pages/Tasks/Processes.cshtml(.cs)`, `wwwroot/js/process-rules.js`, estilos existentes de procesos y campos.
- Persistencia: `OperationalSequenceVersion.StartScheduleJson`, copia al crear borrador en `EnsureDraft`, validación al guardar/publicar y migración `20261103223000_AddProcessRecurrence`.
- Calendario independiente del proceso: `OperationalSequenceRules.Dates` y `ProcessPlanningService.PreviewCoreAsync`.
- Catálogo semanal: `DirectionPlanningCatalogReader` representa el proceso una sola vez mediante el código de su primera tarea; `IsProcess` evita ofrecerlo como tarea independiente en planes generales o adiciones posteriores.
- Vista previa: `PlanningCapacity.ReadAsync/DailyLoadAsync` incorpora versiones, etapas, tarifas de ítems, personas y reservas. `WeeklyProcessPlanning` reutiliza selección de responsables y reservas de `ProcessPlanningService`.
- Confirmación: `EfDirectionPreparedPlanService.CreateAsync` persiste inicios y materializa ejecuciones en la misma transacción. Un savepoint revierte cualquier escritura anterior a un rechazo. La clave de inicio proceso/fecha evita duplicados entre planes de procesos y operaciones semanales.
- Ejecución: `OperationalProcessService.StartAsync` conserva snapshots, eventos, intentos y las dependencias existentes. Subcoordinación solo recibe la autorización semanal cuando el destino efectivo es un plan semanal. Las demás rutas conservan autorización de Dirección.
- Historial/eliminación: se reutilizan `PreparedProcessStart`, etapas y ejecuciones vinculadas al mismo plan. La eliminación existente ya recorre estas entidades; no se agrega una eliminación automática.

## Entorno y preservación

Rama inicial: master, con numerosas modificaciones preparadas y no preparadas ajenas. Se conservaron. Aplicación compartida y datos simulados no se actualizaron. Compilaciones y hosts de comprobación utilizan carpetas propias bajo `.local/process-recurrence-*`; se evitó sobrescribir el ejecutable de otra suite activa.

Pruebas PostgreSQL exclusivamente en localhost:5440, bases `sgol_task_checks_process_recurrence_20261103a` a `...f`, más bases separadas `...legacy_20261103` y `...mywork_20261103`. Los primeros intentos se conservan para diagnóstico. El host Razor aislado usa 7269/7268 con identidad sintética; no es la sesión compartida. No se guardan contraseñas en este documento ni en los logs.

## Matriz y evidencia

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Guardar/reabrir días, hora y participación semanal | Parámetros idénticos | Servicios reales / base b; Razor + navegador | Aprobado |
| Cambiar semanal a mensual, fechas 8/22 | Checkbox oculto y desmarcado, dos repeticiones | Razor real, Chromium/WebKit, 320/1280, tres temas | Aprobado |
| Plan semanal con dos inicios | Dos ejecuciones y cuatro etapas reservadas; etapas siguientes esperan | `ProcessRecurrenceChecks` / base b | Aprobado |
| Reintento de creación | Sin nuevas ejecuciones | Misma clave de idempotencia / base b | Aprobado |
| Siguiente semana con anteriores pendientes | Nuevas ejecuciones, etapas previas idénticas | Servicios / base b | Aprobado |
| Mensual/trimestral/anual | Frecuencia del periodo completo | Calendario real / suite | Aprobado |
| Fecha mensual inexistente | No inventar una fecha | Febrero con día 31 | Aprobado |
| Flag semanal en mensual / modo progreso | Rechazo del servidor | Suite de reglas | Aprobado |
| Trabajador sin permiso / Subcoordinación | Denegar trabajador; permitir plan semanal a Subcoordinación | Suite ampliada / bases c/e/f | Aprobado |
| Vista previa obsoleta | Rechazar sin plan parcial | Cambio de catálogo entre preview/confirmación / bases c/e/f | Aprobado |
| Formulario y accesibilidad básica | Guardar real, recargar, estado limpio, foco y sin desbordamiento | 12 combinaciones Chromium/WebKit, 320/1280, light/dark/special | Aprobado |
| Entrega de primera etapa semanal | Satisfacer dependencia de la siguiente | Servicio real / bases d/e/f | Aprobado |
| Plan mensual 8/22 a las 10:35 | Preparar y activar dos ejecuciones con minutos exactos | Servicios reales / bases d/e/f | Aprobado |
| Fecha de proceso ya reservada | Rechazo que identifica proceso y día | Suite final / base f | Aprobado |
| Plan con tareas independientes y procesos | Capacidad conjunta y detalle de todas las etapas | Suite final / bases e/f | Aprobado |
| Crear plan semanal desde formulario | Preview → confirmación → API → datos → detalle; replay sin duplicados | Razor real / base b, semanas sintéticas 48/50 (intento 49 detectó y corrigió el rechazo de mode=operations junto con planId) | Aprobado |
| Detalle semanal responsive | Reservas visibles, foco y ausencia de desbordamiento | 12 combinaciones adicionales de navegador/tamaño/tema | Aprobado |
| Navegación tras guardar | Mantener operaciones semanales; no redirigir a plan de proceso | Primer intento fallido, corrección y repetición real | Aprobado |
| Regresión Mi trabajo PDF existente | Entregar, conservar evidencia, esperar horario y liberar etapa | --process-mywork-only / base aislada | Aprobado |
| Variante antigua --process-plan-only | Exige disponibilidad inmediata de segunda etapa | Se detiene con etapa PROGRAMADA correctamente a las 03:00 UTC del día siguiente; la variante no avanza el reloj | Fallido |
| Migración en simulación compartida | Conservar datos existentes | No aplicada | No ejecutado |
| Dispositivo físico / lector de pantalla | Interacción completa | Sin dispositivo en esta ejecución | No ejecutado |

Comprobaciones mantenibles: `tools/Sgol.TaskChecks/ProcessRecurrenceChecks.cs`, `ProcessRecurrenceUi.cs`, `tools/Sgol.MobileChecks/process-recurrence-checks.cjs` y `process-weekly-plan-checks.cjs`. Logs y capturas: `.local/process-recurrence-checks/run.log`, `.local/process-recurrence-finish/run.log` (22 comprobaciones específicas aprobadas), `.local/process-recurrence-final/mywork.log`, `.local/process-recurrence-final/legacy.log`, `.local/process-recurrence-ui/browser.log`, `.local/process-recurrence-http/browser-final.log` y capturas en esas carpetas. Compilación de Web y TaskChecks sin errores ni advertencias. Un primer fixture falló porque su última etapa no autorizaba el resultado; se corrigió el fixture, conservando la validación de publicación.

## Límites de cierre

No hay commit, push, migración ni despliegue en la simulación compartida. Los planes ya creados no reciben procesos retroactivamente. La creación mensual completa se probó mediante servicios, no con su formulario de planificación; el guardado de parámetros mensuales sí se probó con Razor real. Se verificó la entrega de la primera etapa semanal; no se recorrieron todas las combinaciones de devoluciones, revisión final e ítems. La variante antigua fallida queda registrada y no se contabiliza como aprobada. Su condición `myWorkOnly || bonusChecks` es la que avanza el reloj; se ejecutó la variante `--process-mywork-only`, que verificó correctamente la liberación en el horario reservado.

El guardado del plan ahora conserva `mode=operations`. El redireccionamiento por `PreparedProcessStart` excluye planes semanales, y `DirectionPlanDetails.Processes` muestra sus etapas incluso cuando ninguna tarea está habilitada todavía. El indicador de progreso se limita a tareas habilitadas cuando hay procesos, evitando mostrar «Sin tareas asignadas» para un plan con reservas.

Para reproducir con las fuentes finales: ejecutar `--process-recurrence` sobre una base vacía de comprobaciones para la suite completa. Para navegador, usar otra base vacía con `--process-recurrence-seed`, iniciar `--process-recurrence-ui` sobre esa base y ejecutar ambos scripts de navegador. `SGOL_RECURRENCE_UI_DATE` permite fijar una semana aún no creada. El host limita sus puertos a localhost/loopback y usa una identidad sintética.

## Actualización de la aplicación compartida — 3 de noviembre de 2026

Autorización expresa posterior del usuario: «Aplica los cambios necesarios para actualizar la aplicacion compartida».

- Compilación publicada correctamente en `.local/runtime/process-recurrence-20261103/`; se confirmó que el proceso web carga ese ensamblado. Proxy LAN conservado.
- Destino comprobado: PostgreSQL localhost:5440, `sgol_local`, aplicación web en loopback:7241 y acceso HTTPS LAN en 192.168.1.92:7440. La migración de posposiciones ya estaba aplicada; solo faltaba `20261103223000_AddProcessRecurrence`.
- Respaldo completo previo en `.local/deploy-process-recurrence-20261103/before.dump`; manifiesto de restauración legible. Se conservó la ruta del ensamblado anterior para recuperación.
- Se aplicó exclusivamente el SQL equivalente al método Up revisado de esa migración, en una transacción con rol `sgol_migrator`, bloqueo operativo y registro en `__EFMigrationsHistory`. No se ejecutó un inicializador de datos ni una regeneración de planes.
- Los recuentos y hashes de contenido de 27 tablas operativas, planes e historial coinciden antes/después, excluyendo únicamente la nueva columna nullable. Se conservaron 789 asignaciones y 14 versiones de proceso; ninguna recibió una periodicidad nueva automáticamente.
- Verificaciones posteriores: health/live y health/ready internos 200; acceso LAN 200; recurso JS actualizado servido por LAN. El login directo al backend HTTP devolvió 500, incluso al intentar reproducir manualmente cabeceras de proxy; esa petición no valida el flujo HTTPS soportado. El login por el proxy LAN real respondió 200. El proxy mantiene restringida la ruta pública de readiness.
- Evidencias locales: `result.json`, `migration.sql`, `migration.log`, `data-before.txt`, `data-after.txt`, hash del ensamblado y logs en `.local/deploy-process-recurrence-20261103/`.
- No se creó ni activó un proceso operativo para probar el despliegue. Las pruebas funcionales previas siguen siendo las del entorno aislado; esta comprobación valida instalación, esquema, conservación de datos y disponibilidad.

Esta sección sustituye el estado anterior de migración/despliegue pendiente. No se realizó commit ni push.
