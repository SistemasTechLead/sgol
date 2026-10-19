# Entregas y revisiones operativas

La migración `20261013011233_SeparateOperationalLifecycle` separa físicamente los datos de finalización y supervisión de `operational_assignment`.

- `operational_submission`: PK `AssignmentId`, fecha de finalización, autor cuando se conoce, tipo de finalización, comentario y conclusión.
- `operational_review`: PK `AssignmentId`, estado, decisión solicitada, comentario, fecha y revisor cuando se conocen.
- `operational_assignment`: conserva programación, responsable, revisión de concurrencia y relación `ReassignedFromId`. `ReassignmentComment` conserva el motivo recibido por el nuevo intento, independientemente del comentario de su revisión posterior.

Hay una fila de estado en cada tabla por intento, incluso cuando la tarea está pendiente y los campos de evento están vacíos. No representan entregas o decisiones ocurridas hasta que existe la información correspondiente. EF Core usa entity splitting para conservar el contrato actual de pantallas y consultas, sin duplicar estas columnas en la asignación. Las filas están vinculadas por PK/FK; las FK inversas son diferidas para garantizar la composición al terminar la transacción. La inserción, entrega, revisión y eliminación siguen siendo atómicas.

El flujo permite una entrega y una decisión final por intento. Una reasignación crea otro intento, por lo que la cadena histórica se consulta mediante `ReassignedFromId`. No se implementó la reapertura con múltiples decisiones sobre el mismo intento; esa operación no existe en las pantallas actuales.

`CompletionKind` diferencia `ENTREGA`, `SUPERVISION` y `HISTORICO`. Supervisión puede cerrar una tarea sin que el trabajador haya enviado una entrega; ese cierre no se atribuye al trabajador. `ReviewDecision` diferencia aprobación, no satisfacción, reasignación a la misma persona y reasignación por rol. Para registros antiguos, la decisión se recupera desde un evento explícito de auditoría cuando existe; los autores y datos no comprobables permanecen desconocidos. La migración complementaria `20261013013017_RecoverHistoricalSubmissionAuthors` recupera autores únicamente cuando un evento de entrega registra el mismo identificador y fecha de finalización. En la base local se recuperaron cinco autores comprobables; no se usa el responsable actual para atribuir una entrega histórica.

## Consulta del historial

En Mi trabajo, abrir una tarea y desplegar **Historial de entrega y revisión**. Muestra la reasignación recibida, la finalización y la decisión, según corresponda, con fecha, autor conocido, motivo y conclusión. El historial del nuevo intento puede mostrar el motivo antes de entregar. La consulta respeta el acceso del responsable y los niveles de supervisión; no expone la historia de otros trabajadores sin autorización.

Las tablas permiten consultas SQL directas sobre las fechas y decisiones. Las pantallas, tablero y reportes conservan su lógica existente. La separación añade relaciones y joins por claves indexadas; no implica por sí sola un ahorro de cómputo.

## Migración y respaldo

La migración copia los campos anteriores antes de eliminarlos. Los autores históricos que no estaban registrados no se inventan. Las evidencias y sus bytes no se modifican.

Antes de revertir, `Down` preserva la procedencia y los dos comentarios en eventos técnicos `OPERATIONAL_LIFECYCLE_ROLLBACK_SNAPSHOT`, porque el esquema anterior no puede representarlos. `Up` recupera esos metadatos si los eventos todavía coinciden; no sobrescribe cambios posteriores del esquema anterior. Estos snapshots técnicos no deben eliminarse antes de una reaplicación. Un respaldo completo sigue siendo el medio de recuperación íntegra.

Las FK de autores, las FK inversas diferidas y los CHECK de los fragmentos se administran explícitamente en la migración SQL; EF no puede representar las FK de autor sobre esos fragmentos. Las migraciones futuras deben conservar dichas restricciones.

`outputs/lifecycle/separar-entregas-revisiones.sql` contiene el SQL incremental desde `NormalizeOperationalStorage`; no se debe ejecutar sobre una versión anterior distinta ni repetirlo una vez aplicado.

## Verificaciones

- Suite completa de tareas, planificación, avances, cámara, supervisión, reasignación y borrado de planes.
- Pruebas de autor/fecha separados, repetición de entrega sin sobrescritura, autorización del historial y protección contra eliminar un fragmento obligatorio.
- Reversión y reaplicación comparando autores, decisiones y ambos comentarios.
- Migración de una copia local y de `sgol_local`, comparando huellas de todos los metadatos de asignación y bytes de adjuntos, fotografías y material de referencia.
- Compilación completa, comprobaciones de tablero y reportes y revisión del modelo sin cambios pendientes.

La limpieza mensual de evidencias y el despliegue de DigitalOcean siguen fuera de esta migración.
