# Actualizar Asignaciones

Dirección abre **Tareas**. El botón global **Actualizar Asignaciones** aparece a la derecha, al mismo nivel que la navegación entre pantallas. Se habilita cuando hay pendientes vigentes de una o varias tareas con diferencias efectivas respecto al catálogo, independientemente de la fila seleccionada y de los filtros de búsqueda. Una revisión distinta sin cambios de parámetros no basta para habilitarlo. El contador incluye etapas pendientes de procesos aunque todavía no tengan asignación ejecutable.

## Flujo

1. Editar y guardar el catálogo. Esto no actualiza automáticamente los compromisos existentes.
2. Abrir **Actualizar Asignaciones** desde la cabecera. No requiere seleccionar filas.
3. Revisar el resumen y las tarjetas agrupadas por tarea. Solo se muestran los parámetros que cambian; responsables, fechas y avance quedan en detalles desplegables. También aparecen los movimientos indirectos de otras tareas.
4. Si corresponde, autorizar recalcular estimaciones manuales de esfuerzo restante, sustituir plazos con arrastre aprobado o adoptar calendarios históricos sin instantánea verificable. Los responsables se conservan cuando cumplen la elegibilidad vigente; el motor busca alternativas si hace falta. Cada cambio de opciones requiere calcular otra vista previa.
5. Escribir el motivo y aplicar. Todos los cambios de todas las tareas afectadas se calculan en una misma agenda y se guardan en una transacción, con la misma exclusión mutua que entregas, avances y planificación. Si cambia el estado revisado, se debe calcular otra propuesta.

Los bloques nuevos usan una recepción con aproximadamente tres minutos para revisar la propuesta. Si se rebasa el inicio propuesto, la confirmación rechaza el cambio y permite revisar otra vista previa. El motor tiene límites explícitos: 500 bloques y 20,000 exploraciones. Agotar la búsqueda se informa como resultado inconcluso, no como prueba de inviabilidad.

## Identidad y trabajo realizado

- Se conserva el ID de una obligación existente, sus avances, autores, evidencias, correcciones y transferencias.
- Se excluyen entregas, pendientes de revisión, concluidas, no satisfechas y asignaciones sustituidas. Se considera únicamente el reemplazo vigente.
- Las progresivas admiten cambios de meta y carga con avance. Reducir la meta por debajo del avance conserva las unidades reales; la carga calculada no se vuelve negativa y no se simula una entrega.
- Un cambio de unidad con avance o abandonar PROGRESO exige resolver previamente su conversión; no se inventan equivalencias ni se borran aportaciones.
- Las estimaciones manuales de minutos restantes requieren autorización explícita para sustituirse cuando cambian carga o meta.
- Las nuevas entregas deben cumplir el requisito vigente. Los archivos anteriores siguen vinculados. La entrega revalida la revisión después de adquirir el bloqueo, para impedir aceptar evidencia validada contra parámetros anteriores.
- Cambiar responsable registra una transferencia, manteniendo la autoría anterior. El rol requerido se conserva en la instantánea de definición y se distingue del rol del trabajador elegido.

## Cambios de datos sin reacomodo

Nombre, descripción, requisito de evidencia y límites de fotografías se actualizan sin cambiar responsable, estado, activación, vencimiento, avance, carga ni estimación manual. Estas asignaciones quedan fijas incluso cuando otra tarea del mismo lote requiere reacomodo. No se exige adoptar un calendario histórico para cambiar solo estos datos; su instantánea parcial no afirma haber adoptado parámetros de agenda desconocidos. Carga, meta, elegibilidad, criticidad o calendario sí pasan por el cálculo de asignaciones. Los procesos documentales mantienen las restricciones de su contrato de evidencia.

## Agenda y recurrencias

### Vencidas de días anteriores sin avance

Una asignación independiente vencida de un día anterior puede adoptar cambios de nombre, instrucciones, requisito, checklist, límites de imágenes y carga configurada sin reprogramarse. Para esta excepción debe seguir pendiente y no tener avances, marcas de checklist, archivos, capturas, transferencias, estimación manual, correcciones ni vínculos con un proceso u ocasión. Cambios de elegibilidad, criticidad, meta o calendario mantienen la revisión normal de agenda.

Se conservan responsable, estado, activación, vencimiento, política y decisión temporal. También se conserva `LoadMinutes` como carga histórica, por lo que las sumas de aquel día no se reescriben. La nueva carga configurada queda capturada en la definición de la asignación, junto con `_HistoricalAgendaLoadMinutes`, y se utiliza para comparar futuras actualizaciones; esto evita mostrarla permanentemente como desactualizada. Una corrección nueva usa la carga configurada adoptada. La asignación original continúa vencida: actualizar no equivale a entregar, aprobar o renovar su plazo.

La vista previa distingue esta acción con «Actualizar sin reacomodar el día pasado» y explica la carga configurada frente a la histórica. Las tareas vecinas de ese día no se liberan para reacomodo. Los pendientes futuros continúan sujetos al cálculo normal, incluidos los movimientos indirectos que se muestran antes de confirmar. Cuando falta una agenda compatible, el aviso identifica una tarea, responsable, fecha y hora implicados en el bloqueo.

Verificación: `Sgol.TaskChecks --expired-refresh-only` comprueba el caso crítico vencido, adopción de checklist, carga histórica y fechas intactas, conservación de vecinos y entregas, duración futura, reintentos y exclusión de evidencia o marcas existentes. En una copia aislada de los datos locales del 28/10/2026, la vista previa global permite actualizar TAR-0062 sin conflictos, conservando su bloque del 27/10. La propuesta no se aplicó a las asignaciones reales.

Se intenta mantener un bloque que siga siendo válido. Cuando hacen falta cambios, se buscan responsables elegibles y huecos respetando capacidad, horarios, descansos, ausencias, criticidad y circuito de caja. Las tareas vecinas futuras pueden desplazarse y esos cambios aparecen en la propuesta. Los compromisos heredados sin agenda verificable requieren adoptar una política y disponer de horarios; no se consideran huecos libres.

La conciliación de recurrencias se limita a los planes existentes. Las entregas y pendientes anteriores cuentan para su ciclo; no se recrean automáticamente repeticiones de días pasados. Los días fijos se conservan y las fechas mensuales usan las ventanas del calendario mensual. Las fechas inexistentes producen un conflicto. Se retiran únicamente ocurrencias sin actividad ni vínculos protegidos; la auditoría conserva su instantánea anterior. Los retiros por desactivación se autorizan con **Retirar asignaciones de tareas inactivas**, incluso si hay avances o transferencias. Las tareas activas excluidas del plan semanal tienen una decisión distinta: **Retirar pendientes de tareas que salieron del plan**. Ambas retiran pendientes del trabajo activo mediante `RetiredAt`, liberan capacidad y conservan evidencias, avances, autoría y vínculos. No retiran entregas ni cierres. Si se desactiva una etapa pendiente, se cancelan el proceso y sus etapas pendientes; las etapas entregadas y las versiones publicadas se conservan.

Las ocasiones conservan su nombre, material y ventana propios; solo adoptan parámetros heredados del catálogo. Los cambios de pertenencia a procesos deben resolverse en el editor de procesos.

## Arrastres, procesos e historial

Una solicitud de arrastre pendiente bloquea cambios contradictorios. Sustituir el plazo de un arrastre aprobado requiere marcar la autorización y revisar los nuevos vencimientos. Las decisiones y solicitudes originales son inmutables. Historial y Reportes conservan el plazo anterior a una reprogramación; entregar después de ese plazo sigue apareciendo como posterior al plazo original.

En procesos se actualizan coherentemente etapa, intento vigente, asignación si existe, reserva, política y vencimiento. Las etapas siguientes pendientes pueden reacomodarse dentro de sus restricciones y se validan dependencias, anclas críticas y plazos fijos. Una etapa en espera no genera una asignación ficticia. Las versiones publicadas y las etapas entregadas permanecen intactas. Cambiar un proceso documental a otro tipo de evidencia requiere resolver su versión y flujo, en lugar de convertir silenciosamente su contrato.

Cada aplicación lleva clave idempotente, motivo y auditoría antes/después. La repetición del mismo envío no duplica asignaciones ni eventos. La huella incluye catálogo, asignaciones, capacidad, horarios, estados de proceso, arrastres y actividad relevante.

## Persistencia y verificación

Migración: `20261028000129_AddAssignmentDefinitionRefresh`. Añade instantáneas JSON a asignaciones y etapas y permite conservar avance superior a una meta reducida. Recupera el calendario de la revisión histórica cuando existe evidencia suficiente; marca las situaciones heredadas que necesitan adopción explícita. No cambia responsables, requisitos, fechas, avances ni estados durante la migración. Revertir la restricción antigua puede requerir resolver antes las metas que quedaron por debajo del avance; no se recortan unidades para facilitar un downgrade.

Prueba focalizada: `tools/Sgol.TaskChecks --assignment-refresh-only`, únicamente contra una base vacía `localhost/sgol_task_checks_*`. Incluye autorización, diferencias efectivas, protección de entregas/cierres, avance/evidencia/autoría, idempotencia, concurrencia, cambio de responsable, metas reducidas, recurrencias, estimaciones manuales, arrastres, puntualidad y etapas pendientes/activas de procesos. La compilación de Razor y la comprobación sintáctica de JavaScript complementan estas pruebas; no sustituyen una validación visual del navegador.

### Resultado del 27 de octubre de 2026

- 40 comprobaciones focalizadas aprobadas en una base vacía aislada, incluyendo preferencia por personas distintas y conservación de activaciones válidas al aumentar repeticiones.
- Regresión de entrega anticipada aprobada: comentario, progreso, cámara, reintento y revisión.
- Compilación final de Web/Razor y TaskChecks: cero errores y advertencias. TaskImport también compila con la dependencia nueva de la página Tareas.
- JavaScript válido y modelo EF sin cambios pendientes respecto a la migración.
- Migración probada sobre datos de prueba existentes: hashes de campos operativos conservados.
- Migración aplicada al entorno local: las 539 asignaciones conservaron el hash de sus datos operativos. No se ejecutó la actualización operativa de ninguna tarea real. El servicio local respondió `alive` y la ruta nueva exige autenticación.
- Vista simplificada revisada en navegador mediante un servidor local de prueba, con datos ficticios y aplicación operativa bloqueada.

La acción global se verifica con `--assignment-refresh-global-only`: varias tareas modificadas, búsqueda filtrada, agenda compartida sin solapamientos, ausencia de cambios parciales, desactivación al quedar sincronizadas e invalidación de toda la propuesta ante concurrencia en otra tarea.

La detección compara los parámetros efectivos de cada asignación (requisito, carga, instrucciones, evidencia y calendario copiado), no solo su instantánea o número de revisión. Se aprobaron 18 comprobaciones específicas de la operación global, incluidas diferencias reales con instantáneas aparentemente vigentes.

La migración `20261028004727_AddAssignmentRetirement` agrega el retiro lógico y permite reutilizar la clave de una ocurrencia retirada. No retira asignaciones durante la migración y rechaza revertirse si existen retiros que podrían reaparecer. `--assignment-retirement-only` verifica retiro con avances, evidencia, transferencias, correcciones, archivo del plan e inactivación de etapas; `--assignment-refresh-global-only` también verifica cambios descriptivos heredados dentro de un lote con reacomodo, conservación de todos los campos operativos y detección posterior de cambios de carga.

Verificación final: 24 comprobaciones de actualización global, 40 de actualización focalizada y 30 de retiro aprobadas en bases aisladas. La prueba visual confirmó que cambiar una casilla bloquea Confirmar hasta revisar de nuevo. El entorno real conserva cero asignaciones retiradas.

