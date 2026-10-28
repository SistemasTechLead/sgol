# Criticidad operativa: implementación y validación

Entrega local: 21 de octubre de 2026. Alcance: nuevas operaciones; sin migrar, regenerar, reclasificar ni reprogramar asignaciones actuales.

## Comportamiento entregado

- Catálogo con Crítica, Importante y Flexible. Por instrucción del usuario: TAR-0062 (apertura) y TAR-0200 (cierre) críticas; las otras 276 definiciones no eliminadas flexibles. Las tres definiciones eliminadas no se modificaron.
- Política persistida y versionada, inicialmente 50%. Dirección puede usar **Ajustar Porcentaje Para el Cierre** en `/tareas`; el ajuste admite enteros 0–500 y conserva las versiones anteriores.
- Activación nominal, criticidad, margen y política capturados en nuevas asignaciones. Cierre ordinario: activación asignada más `ceil(carga × (100 + margen) / 100)` minutos. El margen reserva tiempo sin aumentar la carga.
- Primero anclas críticas, luego importantes y flexibles. Se permiten huecos entre críticas; no adelantar, invadir descansos, superponer bloques ni trasladar automáticamente al día siguiente. El balanceo también valida los intervalos.
- Vista previa con persona, criticidad, nominal, activación efectiva, cierre, carga y horario. Guardado con huella obligatoria, revisión de entradas y agenda, bloqueo e idempotencia. El paso del tiempo puede exigir recalcular una vista previa.
- Ajuste extraordinario desde Supervisión: conserva alcance de permisos, motivo, propuesta y confirmación. Solo permite mover activadas no vencidas si el supervisor las selecciona; conserva vencidas, evidencia y avances. Puede reacomodar movibles futuras del destinatario para recibir una crítica, mostrando todos los movimientos en la misma confirmación. Expone conflictos por tarea y candidato y registra el cambio en auditoría e historial visible de la asignación.
- Asignaciones ocasionales con vista previa por destinatario y cierre calculado en servidor. Al vincular una ocasión a un plan anterior se mantiene expresamente su contrato anterior.
- Metas PROGRESO con cierre semanal y atribución de carga vigente, fuera de los bloques de ejecución.
- Nuevas versiones de procesos capturan reglas y criticidad; al preparar el plan capturan política y reservas persistidas. Las etapas respetan dependencia, disponibilidad y carga, con el mismo cálculo al habilitarse. Las versiones publicadas antes del cambio conservan su contrato; se requiere una nueva publicación para adoptar la agenda automática. Las metas PROGRESO no se convierten en etapas continuas.
- Tablero y Mi trabajo leen fechas y criticidad guardadas. Se mantienen colores, urgencia y filtros. No se añadió botón «Comenzar», replanificador periódico ni reacomodo por entregas o apertura de pantallas.

## Cierre del local

El catálogo de TAR-0200 ahora configura activación 20:00 de lunes a sábado y 14:00 el domingo. Con carga 5 min y margen 50%, sus nuevas ocurrencias cierran 20:08 / 14:08. Las asignaciones existentes siguen con sus fechas guardadas.

No se extendió ningún turno ni se cambió cobertura. Un bloque que no quepa hasta el cierre calculado produce un conflicto en la preparación. La cobertura del miércoles identificada durante el análisis requiere revisión para un nuevo plan; la criticidad no concede tiempo adicional de jornada.

## Preservación comprobada

Se realizaron respaldos PostgreSQL en formato custom, se verificó su catálogo con `pg_restore --list` y se restauraron copias aisladas antes de las pruebas.

La primera migración agregó campos anulables y configuración. Dentro de la transacción se compararon las filas completas anteriores de las 243 asignaciones y los dos planes, descontando únicamente las nuevas columnas nulas: igualdad exacta.

La segunda migración agregó metadatos de procesos y se aplicó junto con la clasificación autorizada del catálogo. Se usó el bloqueo transaccional común, una huella del catálogo revisado y auditoría individual para los 278 cambios. Antes de confirmar se comprobó igualdad exacta de asignaciones, planes, registros de entrega y revisiones.

Resultado: **243 asignaciones preservadas; 0 con política nueva y 0 con criticidad propagada**. No se eliminaron o regeneraron asignaciones, no se reasignó trabajo y no se cambiaron horarios personales. Los cambios de catálogo conservan sus snapshots anteriores. Las entregas legítimas del piso pueden seguir cambiando estados después de estas comprobaciones.

## Validación

- Compilación de `SistemaSGOL.slnx` y publicación de Sgol.Web sin errores ni advertencias.
- Pruebas nuevas en `tools/Sgol.TaskChecks/OperationalSchedulingChecks.cs`: redondeo, margen 0/50, anclas simultáneas, huecos antes de una crítica posterior, descanso, falta de tramo continuo, reserva de margen tras entrega temprana y compatibilidad con compromisos anteriores.
- Caso Dennis: 195 min de carga reservan 294 min por redondeo individual; las once tareas de las 09:00 terminan a las 16:08 respetando descanso 13:00–15:00.
- Integración en copia aislada: vista previa idéntica al guardado, balanceo válido, creación y ocasión idempotentes, cambio concurrente de porcentaje, preservación de planes publicados y redistribución por ausencia.
- Activadas: sin selección no se mueven; solo se mueve la seleccionada; si vence entre vista previa y confirmación se rechaza el ajuste. La vencida conserva persona, activación y cierre. El historial muestra el motivo de la redistribución confirmada.
- Ausencia con agenda del receptor ocupada: una crítica 09:00–09:15 desplaza una flexible futura a 09:15–10:45; ambos cambios se revisan y guardan juntos sin invadir otras anclas.
- Procesos: reservas persistidas, margen capturado aunque cambie la configuración global, espera de activación y dependencia, habilitación con cierre correcto y conservación del vencimiento en reconciliaciones posteriores.
- Todas las pruebas de integración conservaron las 243 asignaciones preexistentes de la copia. La comparación final en la base de piso también confirmó igualdad de todos sus contratos con la captura previa.
- Rendimiento: 200 definiciones generaron exactamente 10 000 ocurrencias en 14,50 segundos sobre una copia aislada, sin persistir asignaciones. Se usan índices y cachés limitados a la solicitud para horarios, disponibilidad y carga; no hay recálculo por tarjeta.
- Regresión: 55 comprobaciones de horarios personales, reglas de capacidad y reglas de calendario/dependencias/devoluciones de procesos.
- Verificación HTTP autenticada de la versión publicada: catálogo, ajuste de porcentaje, creación, Planificación, Supervisión, Mi trabajo, Tablero y asignación ocasional devuelven 200. `/health/ready` devuelve 200. No se publicaron planes de prueba en la base de piso.

Comandos de checks adicionales:

```powershell
dotnet run --project tools/Sgol.TaskChecks -- --scheduling-rules-only
# Solo contra una copia localhost cuyo nombre empiece sgol_task_checks_:
# establecer SGOL_TASK_CHECKS_CONNECTION por el mecanismo local de configuración, sin guardar contraseñas en Git.
dotnet run --project tools/Sgol.TaskChecks -- --scheduling-integration-only
```

Los tests de integración agregan fixtures a la copia aislada y nunca deben apuntar a la base operativa. El ejecutable comprueba host y prefijo del nombre de base antes de migrar o escribir.

## Evidencia local

`.local/implementacion-criticidad/`, excluida de Git, contiene respaldos verificados, copias de fuentes previas, SQL con controles de preservación, resultados de transacciones, logs de compilación/pruebas y respuestas HTTP. La prueba final completa está en `release-acceptance.log` y las rutas comprobadas en `http-results.json`. La prueba de volumen está en `benchmark-complete.log`.

La aplicación activa ejecuta el artefacto publicado de esta entrega con el mismo content root y configuración LAN. El proxy y las sesiones existentes se conservaron. El script local de arranque de esta entrega está junto al artefacto; el lanzador habitual puede volver a compilar el checkout actual en un próximo arranque.
