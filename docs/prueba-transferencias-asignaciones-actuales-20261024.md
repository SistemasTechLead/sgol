# Prueba aislada de transferencia con las asignaciones actuales

## Resultado

La transferencia funciona para las asignaciones con otro destinatario habilitado. **No todas las pendientes se pueden transferir con los perfiles y reglas actuales.** De 249 asignaciones reales, 27 estaban pendientes: 12 se transfirieron correctamente a cada uno de sus dos destinatarios alternativos (24 confirmaciones reales en la copia), 13 carecen de otro colaborador con el rol/especialidad requeridos y 2 de cierre de caja requieren cobertura autorizada. Las otras 222 asignaciones, entregadas o con decisión final, se rechazaron sin escrituras.

No se modificó código funcional como resultado de esta prueba. Se añadió el verificador reproducible `CurrentAssignmentTransferChecks` y su opción `--transfer-current-assignments`.

## Aislamiento y muestra

- Respaldo nuevo de `sgol_local` con `pg_dump -Fc`, validado con `pg_restore --list` y restaurado en una base independiente con prefijo `sgol_task_checks_*`.
- Ejecución definitiva: `sgol_task_checks_transfer_current_20261024_v2`.
- Instante simulado de ejecución: **24 de octubre de 2026, 18:39:21, hora de Ciudad de México**. La hora se fija para repetir las validaciones de recepción durante la matriz; no se cambian horarios ni se inventan excepciones.
- Se conservaron los IDs, tareas, responsables iniciales, avances, evidencias, perfiles, especializaciones y horarios del respaldo. No se crearon asignaciones de ejemplo ni se habilitó artificialmente a destinatarios.
- Se utilizó el supervisor real de Dirección y se comprobó que sus 249 asignaciones visibles coincidieran con la muestra completa.
- Los casos se confirmaron secuencialmente en la copia: la segunda transferencia de una tarea parte de la primera y las cargas de la copia reflejan los casos anteriores. Cada caso verifica que todas las demás asignaciones permanezcan idénticas a su estado inmediatamente anterior. No es una regeneración ni redistribución de planes.

## Transferencias exitosas

| Estado inicial | Asignaciones distintas | Transferencias confirmadas |
| --- | ---: | ---: |
| Vencida | 2 | 4 |
| Activa | 1 | 2 |
| Programada | 8 | 16 |
| Activa con avances | 1 | 2 |
| **Total** | **12** | **24** |

Se cubrieron 9 asignaciones sin evidencia obligatoria y 3 con fotografía. En la tarea real `TAR-0021`, «Reponer productos vendidos y corregir huecos de exhibición», se conservaron las **17 de 50 unidades** de Alejandra al transferirla a Dennis y después a Mariana, sin cambiar autoría ni registros de avance.

Se comprobaron empalmes con otras tareas —hasta 9 coincidencias de plazo en un caso— sin mover esos compromisos. Los destinatarios no quedaron por encima de su capacidad configurada en esta muestra; no se presenta esto como una nueva prueba de sobrecarga.

## Restricciones encontradas

| Responsable inicial | Asignaciones | Motivo |
| --- | --- | --- |
| Viri | TAR-0212, TAR-0198, TAR-0103, TAR-0122, TAR-0162 | No existe otro colaborador habilitado con la especialidad Administración. |
| Viri | TAR-0221 | No existe otro colaborador activo del rol requerido para una tarea sin especialidad. |
| Mariana | TAR-0228 (2 asignaciones), TAR-0204 | No existe otro colaborador habilitado con la especialidad Crédito y Cobranza. |
| Alejandra | TAR-0402, TAR-0311, TAR-0358, TAR-0303 | No existe otro colaborador habilitado con la especialidad Servicio al Cliente. |
| Mariana | TAR-0200 (2 asignaciones) | `CLOSING_COVERAGE_REQUIRED`: los receptores alternativos no cuentan con la cobertura de cierre requerida. |

Para los casos sin receptor se comprobó también el rechazo de un destinatario inválido sin escrituras. Para los cierres se recorrieron los tramos de horario reales de los candidatos desde la fecha de la prueba hasta siete días después; ninguno permitió una transferencia con la cobertura capturada. No se alteró esa cobertura para forzar el resultado.

## Invariantes verificadas en cada confirmación

- Conservación del ID, meta, avance acumulado, contrato de evidencia y estados de entrega/revisión. Ninguna entrega o revisión se fabrica al transferir.
- Cambio exclusivo de responsabilidad, recepción, nuevo plazo, estimación pendiente y metadatos autorizados de transferencia.
- Recibo con estado previo completo, fechas anteriores, supervisor, motivo y unidades conservadas.
- Exactamente un recibo y un evento de auditoría por confirmación.
- Coincidencia de la carga persistida del receptor con la vista previa.
- Retiro de la tarea de Mi trabajo del cedente y aparición en Mi trabajo del receptor, conservando el avance.
- Movimiento visible en el historial de ambos participantes; lectura de autoría de avances válida.
- Repetir una confirmación con revisión anterior devuelve `TRANSFER_STALE`, sin otro recibo ni auditoría.
- Las restantes asignaciones permanecen idénticas y ninguna tarea independiente modifica un proceso.
- Evidencia, archivos, avances, catálogos, capacidades, horarios, planes y demás tablas protegidas mantienen sus huellas durante la matriz. Las huellas de bytes de evidencia se comprueban antes/después del conjunto completo.

## Límites de cobertura

La muestra actual no contiene asignaciones vinculadas a etapas de procesos. Esta ejecución no demuestra de nuevo el flujo de procesos. Tampoco contiene una tarea vencida con avances, aunque sí incluye vencidas y una activa con avance. Las reglas de especialidad y cobertura se conservaron: «forzar» sobrecarga/empalmes no equivale a autorizar otro puesto, especialidad o cobertura de cierre.

## Protección de la base operativa

Coincidieron las huellas iniciales/finales de **31 tablas** de la base real, incluyendo 249 asignaciones, sus entregas/revisiones, 5 registros de avance, 132 fotografías, 3 archivos y el historial de transferencias. **La base real conserva cero transferencias**; las 24 confirmaciones existen únicamente en la copia aislada.

La compilación del verificador terminó sin errores ni advertencias. El ejecutor rechaza conexiones que no sean `localhost` y cuyo nombre no empiece con `sgol_task_checks_`.

Los resultados completos por asignación/destinatario están en `outputs/transferencias-actuales-20261024/`: `inventory.json`, `matrix.json`, `summary.json`, `matrix.log`, `source-before.txt`, `source-after.txt`, `clone-before.txt`, `build.log` y el respaldo privado `source.dump`. El respaldo y los datos de prueba están excluidos de Git.
