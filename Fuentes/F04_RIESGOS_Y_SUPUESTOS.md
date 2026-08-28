# F04 — Riesgos y supuestos de alcance del MVP de SGOL

## 1. Control del entregable

| Campo | Valor |
|---|---|
| Fase | 04 — Alcance, prioridades y MVP |
| Estado | APROBADO E INCORPORADO A LAS FUENTES DEL PROYECTO |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Alcance relacionado | `F04_ALCANCE_Y_MVP.md`; `F04_MAPA_DE_VERSIONES.md` |
| Regla | Un supuesto no confirmado no se convierte en requisito ni en hecho |
| Aprobación del responsable | `Apruebo F04-JP-001 a F04-JP-005 y los tres entregables F04` |

## 2. Escala cualitativa

- **Impacto alto:** puede invalidar el MVP, su integridad, autoridad, trazabilidad o aceptación.
- **Impacto medio:** puede exigir replanificación o reducir utilidad sin invalidar el núcleo.
- **Probabilidad alta/media/baja:** estimación documental de F04, no medición estadística.
- **Bloquea F04:** requiere decisión antes de aprobar el MVP.

## 3. Registro de riesgos

| ID | Riesgo | Evidencia o causa | Prob. | Impacto | Tratamiento de alcance | Bloquea F04 |
|---|---|---|---|---|---|---|
| RSK-001 | Las ocho tareas candidatas podrían no seguir siendo necesarias. | `ACTIVA_REAL_GUARDADA` es una señal legacy, no confirmación humana actual. | Alta | Alto | Catálogo de ocho tareas confirmado por F04-JP-002; validar sus detalles operativos en F05. | No; alcance aprobado |
| RSK-002 | El MVP puede quedar sobredimensionado pese a limitar tareas. | El ciclo íntegro cruza 35 capacidades granulares. | Media | Alto | Mantener sólo el flujo ordinario; dejar excepciones, cierre formal, eventos y condiciones para posterior. | No; riesgo aceptado por F04-JP-001 |
| RSK-003 | El alta manual de tareas por evento o condición puede ser insuficiente. | CAP-016/CAP-017 quedaron posteriores. | Media | Medio | Validar que el piloto tolere alta manual; si no, tramitar un cambio de alcance. | No; riesgo aceptado por F04-JP-003 |
| RSK-004 | No incluir excepciones ni cierre formal puede impedir una operación semanal completa. | CAP-034 a CAP-038 quedan posteriores, aunque F03 ya definió sus reglas. | Media | Alto | El piloto mantiene vencidas activas; una necesidad de cierre formal exige cambio de alcance. | No; riesgo aceptado por F04-JP-001 |
| RSK-005 | La evidencia configurada puede imponer carga sin valor. | Las fuentes legacy marcan evidencia para 189 definiciones, pero DEC-046 exige revisión individual. | Alta | Medio | Configurar evidencia sólo cuando una tarea MVP tenga un resultado que deba comprobarse. | No; se resuelve por tarea en F05 |
| RSK-006 | La validación podría convertirse en aprobación indiscriminada. | La existencia de validador legacy no prueba necesidad; DEC-008 la hace condicional. | Alta | Medio | Exigir justificación y criterio verificable por tarea MVP antes de activar validación. | No; F05 |
| RSK-007 | La asignación automática podría elegir personas formalmente elegibles pero operativamente inadecuadas. | La política usa rol, disponibilidad, turno y carga; no incluye habilidades no documentadas. | Media | Alto | Confirmar que el rol requerido basta para las ocho tareas; usar corrección jerárquica trazada, no equivalencias inventadas. | No; F05 puede detectar vacío bloqueante |
| RSK-008 | Los indicadores básicos pueden interpretarse como desempeño o compensación. | DEC-037 y DEC-038 separan operación de incentivos. | Media | Alto | Rotularlos como indicadores operativos; prohibir efectos monetarios y documentar denominadores en F05. | No |
| RSK-009 | Dejar fuera migración puede limitar consulta histórica o continuidad. | CAP-048/CAP-049 son condicionales; no se aportó necesidad de historia en MVP. | Media | Medio | No migrar en el MVP y conservar fuentes legacy sin modificarlas. | No; riesgo aceptado por F04-JP-004 |
| RSK-010 | Incluir migración prematuramente puede dominar el MVP con ambigüedades. | Identidades múltiples, ejecuciones huérfanas y estados no canónicos están documentados. | Alta | Alto | Mantener CAP-048/CAP-049 opcionales hasta demostrar beneficio. | No si se aprueba F04-JP-004 |
| RSK-011 | Tareas opcionales pueden entrar por presión de catálogo o facilidad técnica. | Existen 186 definiciones canónicas no comprobadas y múltiples artefactos legacy. | Alta | Alto | Aplicar reglas de promoción y control de cambios; no usar existencia, macro o adaptador como evidencia. | No |
| RSK-012 | Una tarea opcional puede invadir nómina, incentivos o integraciones excluidas. | TAR-0103, TAR-0107, TAR-0122 y varias tareas mencionan sistemas externos. | Media | Alto | Conservarlas opcionales con límites de DEC-037, DEC-039 a DEC-042; exigir cambio de alcance si los rebasan. | No |
| RSK-013 | No existe línea base para prometer ahorro, productividad o adopción. | Las fuentes no aportan métricas comparables aprobadas. | Alta | Medio | Usar éxito funcional verificable; medir línea base antes de fijar objetivos de resultado. | No; tratamiento aprobado por F04-JP-005 |
| RSK-014 | Requisitos legales de evidencia, auditoría o conservación podrían modificar alcance. | PNB-014; no se aportó norma o plazo concreto. | Media | Alto | Revisión especializada antes de decisiones físicas de F06; no inventar retención. | No para alcance funcional actual |
| RSK-015 | La ausencia de decisión técnica sobre autenticación, respaldo o almacenamiento se confunda con omisión funcional. | PNB-010 a PNB-013 están diferidos correctamente. | Media | Medio | Mantener cuenta individual, auditoría y conservación como requisitos; resolver mecanismo en F06. | No |

## 4. Registro de supuestos

| ID | Supuesto de trabajo | Base | Estado | Cómo se confirma o invalida | Consecuencia si es falso |
|---|---|---|---|---|---|
| SUP-001 | El piloto y MVP cubren sólo `LOR-001`. | DEC-031. | Aprobado en F03 | Sólo cambia mediante decisión nueva. | Debe revisarse alcance multi-sucursal. |
| SUP-002 | La zona operativa es `America/Mexico_City`. | DEC-058. | Aprobado en F03 | Sólo cambia mediante decisión nueva. | Fechas, semanas y vencimientos deben revaluarse. |
| SUP-003 | La jerarquía aprobada representa la autoridad operativa vigente. | DEC-001, DEC-004, DEC-049. | Aprobado en F03 | Validación de usuarios reales en F05. | El modelo de permisos no puede especificarse. |
| SUP-004 | Las ocho tareas de 6.1 son adecuadas para el alcance del piloto. | Señal `ACTIVA_REAL_GUARDADA` en FTE-012 y F04-JP-002. | Confirmado en F04 | Detalles operativos se validan en F05. | Un cambio exige control de alcance. |
| SUP-005 | El piloto puede operar las tareas no programadas mediante alta manual. | DEC-060 y F04-JP-003. | Confirmado en F04 | Se comprueba mediante criterios F05/F09. | Una insuficiencia exige cambio de alcance. |
| SUP-006 | El primer corte puede operar sin excepciones formales, cierre forzado y reapertura. | Estrategia aprobada por F04-JP-001. | Confirmado en F04 | Se comprueba en el piloto. | Una insuficiencia exige promover CAP-034 a CAP-038 mediante cambio de alcance. |
| SUP-007 | No se necesita consultar ni migrar historia para aceptar el MVP. | CAP-048/CAP-049 condicionales y F04-JP-004. | Confirmado en F04 | Se comprueba en el piloto. | Debe redefinirse el alcance y resolver PNB-007 a PNB-009. |
| SUP-008 | Rol, disponibilidad y turno configurado bastan para elegibilidad inicial. | DEC-034, DEC-057, DEC-064. | Aprobado como regla general; pendiente por tarea | Especificación y pruebas F05. | Se requiere decisión funcional adicional, no una inferencia de habilidad. |
| SUP-009 | Los indicadores funcionales EXI-001 a EXI-011 bastan para aceptar alcance sin prometer productividad. | Ausencia de línea base; DEC-038; F04-JP-005. | Confirmado en F04 | Se comprueba en F05/F09. | Nuevas métricas exigen línea base y decisión aprobada. |
| SUP-010 | Ninguna integración externa es indispensable para completar el piloto. | DEC-040, DEC-041. | Aprobado en F03 | Prueba del flujo en F05/F09. | La solicitud sería cambio de alcance, no detalle técnico. |

## 5. Supuestos rechazados explícitamente

No se acepta ninguno de los siguientes como base del MVP:

- que las 202 definiciones legacy sean necesarias;
- que `BLOQUEADA_HASTA_ADAPTADOR` demuestre necesidad de integración;
- que `DIFERIDA_PREPROD` comprometa una versión posterior;
- que puesto, título o texto legacy conceda rol, autoridad o elegibilidad;
- que una evidencia o validación legacy sea correcta por existir;
- que una cuenta compartida sea aceptable;
- que un indicador operativo pueda derivar pago, sanción o movimiento de nómina;
- que migración y coexistencia sean obligatorias para iniciar operación futura;
- que una meta de productividad pueda fijarse sin línea base.

## 6. Seguimiento de supuestos hacia F05 y F06

| Destino | Elementos que se transfieren si F04 se aprueba |
|---|---|
| F05 | Políticas por tarea, criterios de evidencia/validación, suficiencia de elegibilidad, denominadores de indicadores, flujos y pruebas del catálogo MVP. |
| F06 | Autenticación, almacenamiento, respaldo, recuperación, retención física, idempotencia física y revisión legal aplicable. |
| Versión posterior | Excepciones, cierre/reapertura, inicio medido, eventos y condiciones internas. |
| Decisión futura de alcance | Migración, coexistencia, tareas opcionales, integraciones o cualquier capacidad excluida. |

## 7. Estado de aprobación

F04-JP-001 a F04-JP-005 resolvieron la puerta de alcance para RSK-001 a RSK-004, RSK-009 y RSK-013. Los riesgos permanecen visibles con su tratamiento aprobado; deberán convertirse en bloqueantes si durante F05 impiden especificar o probar una capacidad del MVP.

La Fase 04 quedó aprobada e incorporada a las fuentes del proyecto el 2026-08-27. La Fase 05 está habilitada.
