# Modelo de secuencias y compatibilidad

Fecha: 13 de octubre de 2026. Implementación de fase 2 en EF Core.
Migración: `20261013193317_AddOperationalSequences`.
SQL incremental revisable: `outputs/secuencias/migracion.sql`.
No se ha aplicado al entorno de uso diario.

## Decisión de fechas

Las asignaciones operativas conservan responsable y fechas obligatorias con
`DueAt > StartsAt`. No se alteran los contratos de tareas independientes.
Una etapa y su intento preparado existen antes de su asignación, con fechas
previstas para capacidad y fechas efectivas inicialmente nulas.
La futura integración crea una asignación cuando la habilitación y el reparto
permiten un rango válido. Una etapa fuera del plazo fijo conserva `FixedDueAt`,
registra `FUERA_DE_PLAZO` y no inventa una asignación inválida.

Dirección tiene 168 horas transcurridas. No se introducen asignaciones sin vencimiento.

## Diccionario de campos

Todos los identificadores son UUID. Las fechas con hora son instantes UTC.
Las posiciones, cantidades, horas y minutos son enteros. Los campos marcados
opcionales admiten NULL para expresar hechos aún desconocidos.

| Tabla | Campos | Significado |
| --- | --- | --- |
| `operational_sequence` | `Id`, `Name`, `Enabled`, `Revision` | Identidad estable, nombre y permiso para crear ejecuciones. Desactivar no cambia ejecuciones existentes. |
| `operational_sequence_version` | `Id`, `SequenceId`, `Number`, `CreatedAt`, `CreatedBy` | Versión y autor de configuración. Una versión usada por una ejecución no puede modificarse. |
| Versión | `StartKind`, `FirstMonth`, `WindowFromDay`, `WindowThroughDay`, `PreferredWeekDay`, `StartHour` | Inicio semestral u ocasional. Mes inicial y otro seis meses después; ventana inclusiva, día preferido y hora local. El piloto usa 2, 20, 25 y lunes. |
| `operational_sequence_step` | `VersionId`, `Position`, `TaskId`, `Action`, `Instructions` | Posición y tarea de catálogo seleccionada; acción documental e instrucciones. |
| Etapa de definición | `ReleaseCondition`, `DeadlineKind`, `DeadlineHours`, `MinimumStartOffsetHours`, `ReturnToPosition` | Condición de entrada, plazo relativo o fijo, inicio mínimo opcional y destino anterior permitido. La primera condición es `INICIO`. |
| `operational_sequence_run` | `Id`, `VersionId`, `PlanId`, `StartKey`, `Name`, `CreatedAt`, `CreatedBy` | Ejecución concreta; conserva versión y nombre. Clave de repetición única y plan existente. |
| Ejecución | `Status`, `ActivePosition`, `Round`, `Revision` | Estado, única posición activa, ronda de trabajo y control de concurrencia. |
| Ejecución | `PlannedStartsAt`, `ClosedAt`, `ClosedBy`, `ClosureOutcome`, `ClosureComment`, `AuthorizedDocumentId` | Programación y cierre. Los campos de cierre son opcionales hasta un cierre explícito. Una conclusión autorizada exige documento y comentario. |
| `operational_sequence_stage` | `Id`, `RunId`, `VersionId`, `Position` | Etapa concreta y vínculo con la definición de la misma ejecución. |
| Etapa concreta | `TaskId`, `TaskRevision`, `TaskCode`, `Name`, `Role`, `Requirement`, `SpecializationId`, `SpecializationName`, `LoadMinutes`, límites de fotografías | Snapshot del catálogo al iniciar. Cambiar el catálogo no modifica el trabajo ya generado. Especialización y su nombre son opcionales. |
| Etapa concreta | `Status`, `CurrentAttemptId`, `ReservedPersonId`, `ReservedPersonName`, `BlockReason`, `Revision` | Intento vigente, responsable de reserva opcional, motivo de bloqueo y concurrencia. Las reservas se integrarán con el ledger en fase 3. |
| Etapa concreta | `PlannedStartsAt`, `PlannedDueAt` | Estimaciones, siempre identificadas como previstas. No conceden disponibilidad ni calculan incumplimiento efectivo. |
| Etapa concreta | `MinimumStartsAt`, `FixedDueAt` | Inicio mínimo y límite fijo opcionales, cuando la regla los requiere. |
| Etapa concreta | `DependencySatisfiedAt`, `DependencyEventId` | Evento recibido e instante en que cumplió la dependencia. Ambos son nulos o ambos conocidos. |
| Etapa concreta | `EligibleAt`, `EnabledAt`, `EffectiveDueAt` | Condiciones temporales satisfechas, habilitación efectiva con responsable válido y vencimiento efectivo. Son opcionales mientras el trabajo espera. |
| `operational_sequence_attempt` | `Id`, `RunId`, `StageId`, `PlanId`, `Number`, `Round` | Identidad estable del intento; número por etapa y ronda en que se abrió. |
| Intento | `AssignmentId`, `InputDocumentId`, `OutputDocumentId` | Vínculos opcionales mientras está preparado; la asignación es obligatoria al estar activo, entregado o aprobado. Una versión existente puede ser entrada y resultado de una revisión sin cambios de PDF. |
| Intento | `Status`, `CreatedAt`, `FinishedAt`, `Comment`, `UnresolvedDifferences`, `Revision` | Estado e historia del trabajo y diferencias documentadas. Sustituir un intento conserva sus resultados. |
| `operational_sequence_document` | `Id`, `RunId`, `ProducerAttemptId`, `AssignmentId`, `Number`, `PreviousDocumentId`, `CreatedAt`, `CreatedBy`, `Sha256` | Versión documental, procedencia y huella. Referencia los bytes validados en `operational_attachment`; no los duplica. La versión anterior es opcional. |
| `operational_sequence_event` | `Id`, `RunId`, `StageId`, `AttemptId`, `DocumentId`, `Kind`, `EventKey`, `OccurredAt`, `ActorId`, `ActorKind`, `Comment` | Evento append only con contexto completo y clave idempotente. Documento opcional; actor obligatorio para usuario y nulo para sistema. |

`DeadlineKind = FIJO` usa el límite que se captura en `FixedDueAt` al generar la
ejecución. `RELATIVO` calcula el límite desde `EnabledAt`. Las reglas puras reciben
ese límite capturado; no lo recalculan al visitar una pantalla.

## Transiciones

| Estado de etapa | Evento o condición | Resultado |
| --- | --- | --- |
| `EN_ESPERA` | Falta evento válido del intento vigente | Continúa esperando; fechas efectivas nulas |
| `EN_ESPERA` | Dependencia satisfecha antes del mínimo | `PROGRAMADA`; se conserva el evento |
| Espera o programación | Dependencia y mínimo cumplidos; falta responsable elegible | `PENDIENTE_ASIGNACION`; conserva `EligibleAt` |
| Espera o programación | Responsable válido y capacidad disponible | `DISPONIBLE`; registra habilitación y vencimiento efectivo una sola vez |
| Espera o programación | Límite fijo anterior o igual a la habilitación posible | `FUERA_DE_PLAZO`; conserva límite fijo y causa, sin rango operativo inválido |
| `DISPONIBLE` | Entrega o aprobación vigente que satisface la regla | Etapa concluida; habilita solo la siguiente de esa ejecución |
| Etapa activa | Devolución con motivo a destino permitido | Nuevo intento en destino y nueva ronda para el tramo posterior; el tramo anterior conserva su validez |
| Etapa activa | Reasignación | Otro intento de la misma etapa; identidad, dependencia y documento se conservan |
| Última etapa | Aceptación documental expresa | Ejecución concluida, documento autorizado y conclusión conservados |
| Ejecución en curso | Cancelación expresa con motivo | Ejecución cancelada sin simular entrega |

La implementación actual incluye reglas puras de calendario, disponibilidad,
validez del evento y devolución. Las escrituras transaccionales y el procesamiento
programado que ejecutan estas transiciones corresponden a fases 3 y 4 y siguen pendientes.

Si el límite fijo aún no pasó pero el inicio mínimo ya permite identificar un
intervalo imposible, la regla conserva `PROGRAMADA` con la causa explícita. No
declara vencida una fecha futura. Al pasar el límite, pasa a `FUERA_DE_PLAZO`.

## Restricciones y atomicidad

- Unicidad de versión por secuencia, posición por versión y por ejecución, número de intento por etapa, asignación por intento, número de documento por ejecución y clave de evento por ejecución.
- La etapa apunta a un solo intento vigente. Su FK compuesta exige misma etapa y ejecución; una FK del intento exige que su asignación pertenezca al mismo plan que la ejecución.
- Entrada, salida, documento anterior y documento autorizado usan FKs compuestas por ejecución. La procedencia documental verifica que la asignación de evidencia sea la del intento productor.
- Las referencias circulares a intento, documento y evento son diferidas al commit. Se pueden crear o sustituir vínculos dentro de una única transacción; no pueden quedar inválidos al confirmar.
- Una exclusión GiST diferida impide dos etapas accionables en la misma ejecución. Permite devolver y cambiar la etapa activa dentro de una transacción. Reutiliza `btree_gist`, ya requerido por la capacidad existente.
- Una versión usada es inmutable, incluyendo inserciones tardías de etapas. Documentos y eventos no permiten UPDATE ni DELETE. Cambiar una asignación vinculada exige otro intento.
- Los CHECK rechazan intervalos inválidos, estados disponibles sin fechas, intentos activos sin asignación, autores inconsistentes y cierres sin documento o comentario cuando son requeridos.
- Las relaciones con planes, tareas, autores y evidencias restringen la eliminación. La fase 3 aún debe integrar errores entendibles en las vistas previas de borrado; por ahora esta protección es de integridad SQL.

## Respaldo y recuperación

Se creó `outputs/secuencias/sgol-local-before-sequences.dump` con `pg_dump -Fc` y
se recuperó en una base dedicada de pruebas. La copia contiene las 35 asignaciones
que tenía el origen en ese momento. Las comprobaciones comparan huellas de cada
fila y de los bytes de evidencias, sin exportar su contenido a los logs.

`--sequence-schema-only` compara datos antes/después de la subida, prueba bajada y
reaplicación, rechazos de integridad y rollback de fixtures. Los resultados quedan
en `outputs/secuencias/schema-checks.log`, `historical-before-upgrade.json` y
`historical-after-checks.json`.

La migración Down solo se permite si todavía no existen ejecuciones. Con historia
de secuencias, rechaza borrar sus tablas. La recuperación íntegra exige restaurar
un respaldo verificado en una copia antes de cambiar el destino de la aplicación.
El respaldo inicial no contiene ejecuciones posteriores: debe tomarse otro antes
de cualquier intervención futura para conservarlas.

## Ampliación integrada: pertenencia, publicación y planes

`operational_task.ProcessId` es obligatorio, con FK restrictiva y valor inicial
General (`00000000-0000-0000-0000-000000000010`). `operational_sequence` incorpora
descripción, identidad reservada de General y referencia a publicación propia.
La versión captura nombre, descripción, autor, hora explícitamente confirmada y
configuración; cada etapa conserva `TaskSnapshot` JSONB. Publicar congela estas
filas incluso antes de la primera ejecución. Una ejecución usa su versión capturada.

`operational_process_plan_start` conserva plan, proceso, versión publicada, identidad
de inicio, fecha, responsables por posición y estado PREPARADO/ACTIVADO. Guardar
reserva carga sin asignaciones ni ejecuciones. Activar convierte reservas preparadas
en reservas de ejecución y asignaciones habilitadas dentro de la misma transacción;
los reenvíos recuperan el mismo plan/ejecuciones. El borrado de un plan con ejecuciones
se rechaza; eliminar un plan sin activar usa el protocolo de confirmación y auditoría
existente y libera sus reservas.

La migración de publicación no baja si descartaría un proceso o pertenencia no
General; la de planes no baja si hay inicios preparados. El despliegue diario y su
respaldo recuperado se documentan en [verificación de procesos](verificacion-procesos.md).
