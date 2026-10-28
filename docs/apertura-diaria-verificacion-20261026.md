# Apertura por fecha y tratamiento de horas críticas pasadas

**Actualización del mismo día:** el usuario pidió incluir la apertura y las demás tareas vencidas del día inicial en operaciones semanales. Para ese flujo, el tratamiento como aviso fue sustituido por una asignación pendiente con su hora crítica original. La verificación final está en [Plan semanal completo](plan-semanal-completo-verificacion-20261026.md). El resto de este documento conserva la evidencia del cambio previo y su alcance inicial.

## Resultado

TAR-0062 se configuró como DIAS_FIJOS, máscara 127, conservando siete fechas semanales, carga, rol, rotación y horas: 09:00 de lunes a sábado y 10:00 el domingo. La revisión del catálogo pasó de 5 a 6.

La generación conserva las fechas nominales de las tareas críticas aunque alguna se haya configurado como semanal distribuida. Una hora crítica que ya pasó no se redistribuye ni se crea retroactivamente: se conserva un aviso con la fecha, tarea y motivo CRITICAL_ACTIVATION_PASSED. Si ya existe una asignación reservada para esa ocurrencia, se respeta su identidad y no se agrega un aviso falso.

La apertura del lunes 26/10/2026 posterior a las 09:00 queda como aviso; la generación crea únicamente las seis aperturas futuras. Esto elimina el intento de acomodar siete aperturas en seis días, causa de los mensajes de empalme y repetición de la captura. No se fabrica una entrega ni se marca realizada la apertura de hoy.

La vista previa y el detalle guardado muestran **Ocurrencias sin asignación**, incluyendo el motivo específico. En operaciones semanales se aclara el tratamiento de las horas críticas ya pasadas. Las faltas reales de cobertura futura siguen bloqueando la generación y se informa su fecha concreta.

## Verificación aislada

Se creó un respaldo nuevo con pg_dump -Fc, se validó con pg_restore --list y se restauró en **sgol_task_checks_opening_daily_20261026**. La configuración se aplicó primero en esa copia.

El verificador reproducible es tools/Sgol.TaskChecks/DailyOpeningChecks.cs, opción **--opening-daily-current**. Pasaron los siguientes casos:

- Antes de las 09:00: siete aperturas, una por fecha y con las horas de semana/domingo conservadas.
- Después de las 09:00 del lunes: seis aperturas futuras y un aviso fechado el lunes 26.
- Operaciones creadas el jueves después de abrir: tres aperturas de viernes a domingo y un aviso del jueves, sin arrastrar fechas anteriores.
- Ausencia de todo el personal un martes: bloqueo con diagnóstico del martes; la disponibilidad no se omite para forzar un plan.
- Configuración distribuida errónea de una tarea crítica: la defensa del generador conserva las fechas sin recolocar la ocurrencia vencida.
- Guardado real mediante el servicio, lectura posterior del aviso y reintento idempotente sin duplicados.
- Conservación íntegra de todas las asignaciones anteriores, incluyendo IDs, personas, fechas e historia.

La creación real del plan ocurrió exclusivamente en la copia. Se guardaron estas seis aperturas:

| Fecha | Persona | Inicio local |
| --- | --- | --- |
| 27/10/2026 | Alejandra | 09:00 |
| 28/10/2026 | Dennis | 09:00 |
| 29/10/2026 | Alejandra | 09:00 |
| 30/10/2026 | Dennis | 09:00 |
| 31/10/2026 | Alejandra | 09:00 |
| 01/11/2026 | Mariana | 10:00 |

También pasaron las 60 comprobaciones de horarios, las reglas de agenda con sus 25 casos exhaustivos y las comprobaciones mensuales existentes.

## Aplicación y preservación

La configuración real se aplicó después de la prueba, bajo bloqueo transaccional 734651001, con comprobación del estado completo de la tarea contra la copia y auditoría OPENING_DAILY_CALENDAR_CONFIGURED. No se cambiaron las asignaciones existentes ni se creó el plan de prueba en la base operativa.

Durante la prueba aislada coincidieron las huellas de 31 tablas protegidas de la fuente. Después de aplicar, solo operational_task cambió entre ellas; las tablas de asignaciones, planes, evidencia, entregas/revisiones y horarios conservaron sus huellas. El evento de auditoría es adicional y esperado.

Los avisos usan el formato de ocurrencias fechadas que ya conserva el plan en MonthlyPendingJson, manteniendo compatibilidad de almacenamiento y API sin migración. El motivo distingue las horas críticas pasadas de las pendientes mensuales recuperables. La versión de búsqueda pasa a 4 para invalidar preparaciones anteriores.

Esta prueba demuestra el calendario de apertura y su persistencia. No afirma que toda selección posible del resto del catálogo sea viable simultáneamente.

Respaldo, índice, huellas y logs privados: outputs/apertura-diaria-20261026/. Configurador local de una sola aplicación: .local/apertura-diaria-20261026/.
