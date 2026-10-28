# Cierre de Viri: corrección y prueba aislada

El cierre del miércoles 28/10/2026 estaba seleccionado para Viri, pero TAR-0200 no tenía especialización y exigía el rol PISO_VENTAS. Viri tiene SUBCOORDINACION. Además, la agenda 2 consultaba el turno base hasta las 19:00, ignorando para esta tarea la extensión de cobertura hasta las 20:00.

## Cambios aplicados

- Se creó la especialización **Cierre de tienda** y se vinculó únicamente a TAR-0200, con incremento de revisión de 6 a 7.
- Se habilitó a Viri, Mariana, Dennis y Alejandra en esa especialización, preservando los candidatos anteriores de Piso de ventas. No se cambió el rol de ninguna persona.
- La colocación y validación temporal de la tarea de cierre consultan el horario con cobertura. Las tareas ordinarias de agenda 2 conservan el horario base. El diagnóstico y la vista previa muestran el mismo horario utilizado para asignar.
- La versión de elegibilidad pasa a 4 para invalidar huellas anteriores. La selección de cobertura sigue siendo obligatoria; no se omiten ausencias, descansos, descansos intermedios, capacidad ni límites del turno.
- La configuración se aplicó bajo bloqueo transaccional 734651001 y quedó registrada como CLOSING_SPECIALIZATION_CONFIGURED, con estado anterior, posterior, destinatarios y motivo.

## Evidencia aislada

Se respaldó sgol_local mediante pg_dump -Fc, se validó el índice con pg_restore --list y se restauró en **sgol_task_checks_cierre_viri_20261026**. La primera ejecución reprodujo la exclusión por rol y posteriormente demostró que la especialización por sí sola no bastaba por el uso del horario base. Se restauró nuevamente desde el respaldo antes de la ejecución definitiva.

La ejecución definitiva reprodujo el rechazo original, configuró la especialización en la copia y confirmó:

- Vista previa del miércoles: **Viri, 28/10/2026 19:52–20:00**, carga 5 minutos y duración con margen de 8 minutos.
- Creación real de un plan con siete cierres, persistido exclusivamente en la copia. El cierre del miércoles quedó con el ID de Viri.
- Las asignaciones existentes permanecieron idénticas antes y después de la configuración y la vista previa.
- Las huellas de las tablas operativas de la base fuente coincidieron durante toda la prueba aislada.

Las 60 comprobaciones de horarios pasaron, incluidas las regresiones nuevas: extensión válida para cierre, validación temporal coherente, ausencia respetada, tareas ordinarias sin extensión y rechazo si el cierre excede las 20:00. Las reglas de agenda también pasaron, incluidos los 25 casos contrastados con enumeración exhaustiva.

## Aplicación y alcance

Después de la prueba se aplicó la configuración a sgol_local y se verificó allí la misma vista previa de Viri. Dentro de las tablas operativas verificadas solo cambió operational_task; asignaciones, entregas, revisiones, evidencia, planes y horarios conservaron sus huellas. También se agregaron la especialización, sus cuatro vínculos y el evento de auditoría. No se creó el plan de prueba en la base operativa.

La aplicación se inició con el código corregido. /health/ready respondió 200 tanto por el servicio interno como por el acceso LAN.

**La prueba de cierre pasó; la generación con las 135 tareas activas del catálogo de operaciones no pasó.** Esa selección amplia no demuestra cuál era la selección exacta del formulario del usuario. El diagnóstico individual encontró otros conflictos, por ejemplo TAR-0025 y TAR-0028 el domingo 01/11, cuando su única candidata Viri está de descanso, y conflictos de apertura. No se modificaron esas tareas ni sus horarios. No se afirma que cualquier selección del plan semanal ya sea viable.

## Archivos locales de evidencia

En outputs/cierre-viri-20261026/ están source.dump, backup-list.txt, test-first.log, test.log, apply.log, schedule-rules.log, scheduling-rules.log, full-week.log, diagnosis.log y las huellas source-before.txt, source-after-isolated.txt y source-after-apply.txt. El ejecutor local está en .local/cierre-viri-20261026/ y usa conexiones por variable de entorno; no imprime contraseñas. El respaldo y las salidas privadas están excluidos de Git.
