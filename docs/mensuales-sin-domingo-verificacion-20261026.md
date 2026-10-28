# Tareas mensuales y descanso de Viri

## Diagnóstico confirmado

La consulta de la base operativa encontró **cero asignaciones guardadas de Viri con inicio en domingo**. El error anterior correspondía a intentos fallidos de la vista previa, no a asignaciones persistidas.

TAR-0025, TAR-0028 y TAR-0379 son mensuales distribuidas, con una repetición y sin días mensuales preferidos. El generador utilizaba el día 1 como fecha obligatoria. El 01/11/2026 es domingo y Viri descansa. La elegibilidad rechazaba correctamente ese día, pero la generación no buscaba otro día para estas mensuales y podía bloquear el plan completo.

## Corrección

- Las mensuales distribuidas sin fecha explícita buscan una fecha desde su activación nominal, dentro del mismo mes, del alcance del plan y desde el día de generación. Se conservan los controles de rol, especialización, horario, ausencias, capacidad, duración y compromisos anteriores.
- Domingo no se usa para estas mensuales. Los cierres y demás días fijos mantienen sus reglas.
- Si el plan termina el domingo 1 de noviembre, la ocurrencia se registra como pendiente explícita, con explicación específica. La siguiente semana puede recuperarla dentro de noviembre.
- Cada repetición tiene una clave mensual estable que incluye mes y número de repetición. No se duplica al volver a generar o cambiar de semana. Las reservas anteriores con claves por fecha también cuentan para la frecuencia mensual.
- Los días mensuales preferidos conservan sus límites originales de proximidad y propiedad por semana. La búsqueda ampliada respeta primero la distancia a esa fecha, igual que la búsqueda inicial.
- La versión de búsqueda pasa a 3 para invalidar las huellas de preparación anteriores.

No se modificaron definiciones de tareas, horarios, roles ni asignaciones en la base operativa. El cambio se aplica al preparar nuevos planes.

## Prueba aislada con datos actuales

Respaldo nuevo de sgol_local con pg_dump -Fc y validación mediante pg_restore --list. La prueba definitiva se ejecutó en **sgol_task_checks_monthly_schedule_20261026_v2**, restaurada desde ese respaldo después del ajuste final del orden de búsqueda.

Se comprobó la vista previa de la semana 44 y la recuperación en la semana 45. El servicio real guardó exclusivamente en la copia un plan con:

| Tarea | Persona | Fecha local | Horario local |
| --- | --- | --- | --- |
| TAR-0379 · Capturar pedidos | Viri | Lunes 02/11/2026 | 09:00–12:00 |
| TAR-0025 · Verificar mobiliario, fachada, escaparates y letreros | Viri | Lunes 02/11/2026 | 12:00–12:15 |
| TAR-0028 · Preparar requisición mensual de insumos | Viri | Lunes 02/11/2026 | 12:15–13:30 |

La prueba verificó además repetición doble con claves distintas, no duplicación entre semanas ni después de persistir, reconocimiento de reservas anteriores, rechazo por ausencia con pendientes explícitas y conservación íntegra de los registros originales. Los datos auxiliares se revirtieron mediante rollback.

Las comprobaciones mensuales existentes pasaron, incluidas fechas preferidas, límites del mes, domingo, reserva entre semanas y vista previa. Se actualizó su fixture para declarar criticidad y fijar el instante simulado, requisitos de la agenda actual. También pasaron las 60 comprobaciones de horarios y las reglas de agenda, con sus 25 casos exhaustivos.

Las huellas de las **31 tablas operativas protegidas de la fuente permanecieron idénticas**. La creación de planes de prueba y asignaciones nuevas ocurrió únicamente en la copia. No se demuestra aquí la viabilidad de todas las demás tareas del catálogo simultáneamente.

El ejecutor reproducible es tools/Sgol.TaskChecks/DistributedMonthlyChecks.cs, opción **--monthly-distributed-current**. Requiere una base localhost con prefijo sgol_task_checks_, como el resto del ejecutor. Los respaldos, huellas y logs están en outputs/domingo-viri-20261026/, excluidos de Git.
