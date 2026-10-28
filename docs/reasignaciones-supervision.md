# Devoluciones desde Supervisión

Al guardar «No concluida» con reasignación a la misma persona o al rol, se crea una corrección enlazada mediante `ReassignedFromId`. La entrega anterior conserva evidencia, requisito, responsable y fechas; su revisión queda como `REASIGNADA`.

La corrección utiliza el requisito, instrucciones y límites de evidencia vigentes del catálogo. Conserva la carga, rol y especialidad de la asignación revisada. Se activa al guardar la devolución, sin buscar huecos ni exigir capacidad libre: puede superponerse con otras tareas, incluidas las críticas. Ningún otro compromiso cambia por recibirla.

El plazo es carga más margen, redondeado hacia arriba, y se recorta a la salida del tramo de turno vigente, incluyendo el inicio de un descanso. Una devolución con 40 minutos de carga y margen del 50 % tiene 60 minutos; si quedan cinco minutos de turno, dispone de cinco. Se conserva el margen persistido de la asignación; una entrega antigua sin política adopta la política vigente solamente en su nueva corrección. El vencimiento se guarda al crearla y no cambia al consultar la pantalla. Los ajustes automáticos de capacidad protegen las correcciones de reacomodos.

Se exige que el destinatario esté activo, autorizado, con el rol o especialidad requeridos y presente en su horario base o excepción explícita. No se permite recibir en descanso, ausencia, fuera de turno ni al instante exacto de salida; tampoco se amplía implícitamente el horario por cobertura. Si falla una validación, no se guarda revisión ni asignación parcial. Una solicitud repetida con revisión vieja no crea otra corrección.

La revisión y la corrección se guardan en la misma transacción, bajo el bloqueo operativo y con auditoría. Después de guardar, Supervisión abre la nueva corrección y permite consultar la entrega anterior.

## Simulación y verificación del 23 de octubre de 2026

- `--reassignment-lifecycle` en una copia restaurada y aislada de la base local, protegida por el prefijo `sgol_task_checks_*`.
- Tarea real de Viri «Gestionar devoluciones de producto defectuoso», 40 minutos, turno guardado hasta las 19:00. A las 17:41 del 22 de octubre, la búsqueda anterior devuelve `SCHEDULING_NO_CONTIGUOUS_SLOT`; la nueva devolución se guarda de 17:41 a 18:41. Las demás asignaciones permanecen iguales. Las 17:41 proceden del nombre de la imagen; no hay registro del instante exacto de la solicitud fallida.
- Con una reserva que llena el turno y capacidad en cero, una segunda corrección se activa a las 18:55 y vence a las 19:00. A las 19:00 se rechaza sin escrituras parciales.
- A las 13:25 se activa y vence a las 13:30, inicio del descanso. A las 14:00, dentro del descanso, se rechaza sin crear corrección parcial.
- Se verifican devolución al mismo responsable y al rol, requisito vigente, historia anterior, auditoría, repetición obsoleta y contratos antiguos.
- Registro de ejecución: `outputs/reasignaciones-20261023/lifecycle.log`. No se ejecutó ninguna devolución sobre los datos operativos reales.

## Verificación histórica del 22 de octubre de 2026 (regla anterior)

- Compilación sin errores ni advertencias.
- `Sgol.TaskChecks --reassignment-lifecycle` sobre copia restaurada y aislada `sgol_task_checks_*`: entrega vencida, misma persona, reserva ocupada, requisito vigente, historial anterior, invariantes de otras asignaciones, auditoría, repetición obsoleta, rechazo fuera de turno sin cambios parciales, reasignación al rol y contrato antiguo.
- `--scheduling-rules-only` y `--capacity-rules-only`: aprobadas.
- Aplicación LAN: acceso autenticado a Supervisión, explicación del flujo y requisito de fotografía visibles; `/health/ready` HTTP 200.
- La batería general se detuvo en la creación de un plan por `PLAN_CAPACITY_STALE`, antes de ejecutar sus escenarios de Supervisión. Ese resultado no se cuenta como aprobación de la batería general.

No se migraron ni redistribuyeron las asignaciones operativas existentes para aplicar este cambio.
