# Calendario automático al activar una versión publicada

## Alcance y decisiones

Cambio mixto, solicitado el 5 de noviembre de 2026. Al activar se crea un plan para los próximos doce meses, desde el día local de activación hasta el día anterior al aniversario. Dirección eligió asignación automática por elegibilidad/capacidad y conservar las fechas conflictivas como pendientes, sin bloquear las demás.

Se excluyen ocasiones y procesos marcados para operaciones semanales. Se reutilizan las reglas publicadas semanales, mensuales, trimestrales, semestrales y anuales, incluyendo omisión de días mensuales inexistentes. Publicar por sí solo no asigna. Reactivar conserva las fechas existentes, sin duplicarlas, y completa únicamente fechas faltantes en el nuevo horizonte. Los planes no se renuevan indefinidamente por temporizador.

## Investigación y cambios

- `OperationalProcessCatalogService.SetEnabledAsync`: antes solo cambiaba Enabled. Ahora captura calendario, versión, ítems y política dentro de la misma transacción y bloqueo de capacidad.
- `OperationalSequenceRules.Dates` y `ProcessStartSchedule`: productores de fechas nominales. No se sustituyen sus reglas.
- `PreparedProcessStart`: estado PENDIENTE, indicador Automatic, motivo, último intento y fecha original de una reprogramación. La migración conserva por defecto el comportamiento manual de registros anteriores.
- `ProcessPlanningService.Automatic`: selección por ejecución, reservas y arranque con servicios existentes; transacción por fecha y savepoint para rollback íntegro de un conflicto. Las fechas pendientes no participan del ledger de reservas PREPARADO.
- `OperationalProcessService.TickAsync`: reintenta pendientes cada cinco minutos y conserva el flujo de habilitación, ejecución y revisión de las etapas ya creadas. Solo asigna pendientes de hoy o futuras y de procesos habilitados; las vencidas requieren reprogramación manual, conservan su identidad y fecha original, y no generan trabajo atrasado; desactivar no cancela ejecuciones existentes.
- `/tareas/procesos`: activar intenta asignar y muestra enlaces a los calendarios. `/planificacion/procesos`: fechas, responsables, causa de pendientes y acción de reintento; autorización Dirección y antiforgery de Razor.
- Las claves C:proceso:fecha son compartidas con los planes manuales y semanales. Las instantáneas existentes se conservan incluso tras publicar otra versión.
- La eliminación de planes conserva su recorrido previo; el borrado/cambio de miembros del catálogo también protege los calendarios pendientes.

## Entorno y límites

Checkout con múltiples cambios previos ajenos, conservados. App compartida observada en proceso Web 38728 desde `.local/runtime/count-sources-20261105`; esta implementación todavía no se publica. Pruebas en bases localhost:5440 con prefijo `sgol_task_checks_auto_calendar_`; no escribir en `sgol_local`.

La bóveda se consultó mediante Graphify y sus notas Inicio, dominio, reglas, procesos y diseño. Referencias funcionales de procesos/planificación y patrones/pantallas pendientes de reconciliación cuando se despliegue. No se presenta este comportamiento como vigente en la bóveda ni se despliega para actualizar documentación.

## Matriz de validación

| Escenario | Resultado esperado | Prueba/entorno | Estado |
|---|---|---|---|
| Publicar y activar mensual día 27 | Publicar no crea trabajo; activar captura doce fechas cruzando año | AutomaticProcessCalendarChecks, PostgreSQL aislado | Aprobado |
| Una fecha sin horario | Resto asignado; causa y etapa visibles, sin carga parcial | Mismo servicio real | Aprobado |
| Recuperar horario y reintentar | Una ejecución por fecha, sin modificar las ya asignadas | Mismo servicio real | Aprobado |
| Desactivar/reactivar | Pendientes suspendidos mientras está desactivado; fechas sin duplicar | Mismo servicio real | Aprobado |
| Ocasional y operaciones semanales | Sin calendario automático | Mismo servicio real | Aprobado |
| Semanal, trimestral, semestral y anual | Fechas del calendario publicado | Mismo servicio real | Aprobado |
| Día 31 | Omite meses sin ese día | Mismo servicio real | Aprobado |
| Permisos | Trabajador no puede reintentar | Mismo servicio real | Aprobado |
| Llegada de fecha | Primera etapa disponible; dependencias e historial conservados | Temporizador real aislado | Aprobado |
| Formularios y presentación | Activación/reintento con CSRF, navegación y estados en tamaños/temas | Host Razor aislado y navegador | Aprobado |
| Compilación/modelo | Compila; migración solo añade estado/campos del calendario | Build y EF | Aprobado |

## Resultados y evidencia

- `integration-v6.log`: suite completa aprobada en `sgol_task_checks_auto_calendar_v6_20261105`; incluye dos reintentos concurrentes con contextos independientes, conservación exacta de etapas anteriores, excepciones de horario versionadas, expiración, reprogramación con fecha original y habilitación real al llegar el día. Las primeras iteraciones corrigieron fixtures (Revision de alta, dos etapas obligatorias, historial de horario inmutable y traducción de una consulta del test); no se deshabilitó ninguna validación.
- `browser-themes.log`, `browser-final.log` y `browser-recovery.log`: formularios Razor reales sobre esa base aislada, Chromium/Edge y WebKit, 320/768/1440 px, light/dark/special (atributo data-theme comprobado), teclado y ausencia de desbordamiento. Reprogramar conserva fecha original; activar asigna las ejecuciones; POST sin CSRF devuelve 400. Verificadas capturas; el estado en pausa deshabilita el reintento y enlaza al proceso. El recorrido final aprobó sus 18 combinaciones y la reprogramación, pero su aserción de cero pendientes falló: las ejecuciones semestral y anual de pruebas anteriores ya ocupaban el 28 de diciembre. La aplicación conservó correctamente el trimestral pendiente y mostró ambas colisiones críticas. `browser-recovery.log` comprobó ese diagnóstico y la reprogramación al 29 de diciembre: cuatro ejecuciones trimestrales asignadas, sin pendientes. Se ajustó el test mantenible para recuperar ese conflicto cuando reutiliza el fixture; no se relajaron las reglas de capacidad.
- `browser.log` comprobó formularios y tamaños, pero su selección inicial de tema usaba una clave incorrecta; se corrigió y sustituyó por `browser-themes.log` y `browser-final.log`. No se acredita ese primer recorrido como validación de temas.
- Build Web y TaskChecks aprobado. La última compilación tuvo avisos transitorios de copia por el host de pruebas aún abierto; cerrarlo permitió finalizar correctamente. EF `has-pending-model-changes --no-build`: sin cambios de modelo pendientes.
- Migraciones `20261105171431_AddAutomaticProcessCalendar` y `20261105172310_AddAutomaticProcessReprogramming`, aplicadas únicamente a las bases de prueba. Estado de filas manuales anteriores: Automatic=false, campos nuevos nulos; no se genera un calendario para todos los procesos existentes al migrar.
- Evidencia privada: `.local/automatic-calendar-20261105/`. Cambios previos ajenos conservados. Diff de archivos afectados revisado; comprobación de whitespace con cr-at-eol por la mezcla previa LF/CRLF.

## Límites pendientes y documentación al desplegar

No se desplegó ni migró la app compartida, no se crearon planes en su simulación y no se hizo commit/push. Su publicación observada sigue siendo count-sources-20261105. Las referencias de la bóveda y su copia versionada quedan pendientes hasta activar este comportamiento allí.

No se ejecutó la suite histórica completa: las pruebas antiguas que activaban mensuales para después preparar manualmente las mismas fechas requieren adaptar su expectativa al nuevo calendario automático. El recorrido específico nuevo está aprobado. Procesos con conteos conservan la captura existente, pero no se repitió en esta entrega el flujo completo de entrega, conteo, devolución y corrección. El rendimiento de calendarios semanales con cientos de etapas no se midió; se comprobó su generación de fechas, no una carga masiva.

La reprogramación de un pendiente conserva la hora publicada y permite días desde hoy hasta el final de ese plan; para un calendario terminado se prepara una ocasión nueva. No sobrescribe ejecuciones iniciadas. Las fechas nominales originales y las reprogramadas bloquean duplicados en la generación automática, manual y semanal.

Actualización documental futura: procesos.md (activación/calendario/reprogramación), planificacion.md (pendientes sin reserva), diseño 04-patrones-y-estados.md y 05-pantallas.md (acción directa y calendario en pausa), base-documental.md e instalación (artefacto/migraciones efectivos). No se modificaron las notas para atribuir vigencia a código sin publicar.


## Publicación autorizada

El usuario solicitó publicar en la app compartida. Se siguió el recorrido de infraestructura de la bóveda y se comprobó proceso/listeners. Publicado en `.local/runtime/automatic-calendar-20261105`, Web 24148, lanzador 10132, proxy 20652 conservado. SHA-256 B8105E3BE51E0D0366CB4365773348A32AB1A06B5A6ED8B9A8248CA2F5B74C07 igual al artefacto de pruebas. Respaldo privado de 303464382 bytes; ambas migraciones aplicadas sobre sgol_local, huellas de 97 tablas iguales antes/después excluyendo las nuevas columnas. Readiness interno, HTTPS local y LAN responden 200 con TLS normal. Cero fechas automáticas creadas durante la publicación. Referencias existentes reconciliadas en bóveda y copia versionada mediante cambios puntuales, conservando divergencias anteriores. Sin commit/push ni operación autenticada de prueba compartida.
