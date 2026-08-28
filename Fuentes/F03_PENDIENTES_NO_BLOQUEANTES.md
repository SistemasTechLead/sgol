# F03 — Pendientes no bloqueantes de SGOL

## 1. Control del entregable

| Campo | Valor |
|---|---|
| Fase | 03 — Resolución de dudas y consolidación |
| Estado | APROBADO E INCORPORADO A LAS FUENTES DEL PROYECTO |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Regla | Ningún elemento de este documento impide iniciar Fase 04 después de aprobar e incorporar los entregables F03 |

## 2. Pendientes por fase destino

| ID | Pendiente | Motivo por el que no bloquea F03 | Fase destino | Fuentes/decisiones |
|---|---|---|---|---|
| PNB-001 | Clasificar individualmente las definiciones legacy de tarea como MVP, posterior, opcional o fuera de alcance. | F03 ya decidió que ninguna se promueve automáticamente; la selección es juicio de alcance. | F04 | DEC-046; CAP-012 |
| PNB-002 | Determinar cuáles tareas concretas necesitan evidencia, validación, inicio medido, turno, horario, evento o condición. | Las reglas generales ya están aprobadas; la selección depende de qué tareas entren en alcance. | F04/F05 | DEC-008, DEC-019 a DEC-022, DEC-046, DEC-053, DEC-060 a DEC-062 |
| PNB-003 | Definir criterios verificables para las tareas del alcance que requieran validación. | Las cinco TAR legacy sin criterio no son requisitos automáticos. Sólo las tareas seleccionadas deben especificarse. | F04/F05 | F02-PRE-016; DEC-008, DEC-046, DEC-053 |
| PNB-004 | Clasificar los tipos `POR_CLASIFICAR` únicamente para tareas que entren en alcance. | Los 36 valores legacy no prueban necesidad; la regla futura exige todos los tipos configurados. | F04/F05 | F02-PRE-015; DEC-021, DEC-046 |
| PNB-005 | Detallar causas, datos y criterios permitidos para cancelación, posposición, traslado y reasignación por cada tarea del alcance. | Autoridad, identidad y flujo general ya están definidos. | F05 | DEC-016 a DEC-018, DEC-051 |
| PNB-006 | Definir historias, criterios de aceptación y pruebas para cierre forzado, reapertura y sustitución de resultados. | Las decisiones funcionales están aprobadas; falta especificación verificable del MVP. | F05 | DEC-012 a DEC-016 |
| PNB-007 | Resolver manualmente identidades legacy múltiples sólo si el alcance de migración requiere indicadores por persona. | Se aprobó conservarlas sin fusión; no afectan operación futura ni alcance funcional. | F04/F05/F08 según alcance | DEC-048 |
| PNB-008 | Diseñar el procedimiento de migración para ejecuciones huérfanas, estados ambiguos, puestos ambiguos y validaciones sin evidencia. | El tratamiento semántico ya está decidido; falta ejecución y prueba de migración. | F05/F06/F08 según alcance | DEC-023, DEC-028, DEC-029, DEC-055 a DEC-057 |
| PNB-009 | Definir alcance de coexistencia, pruebas y gates de retiro del productor legacy. | Depende de si F04 incluye migración y coexistencia. | F04/F06 | F02-PRE-029; CAP-049 |
| PNB-010 | Elegir mecanismo de autenticación e identificador externo estable. | La regla funcional ya exige cuenta individual y usuario estable; proveedor y protocolo son técnicos. | F06 | F02-PRE-002; DEC-043 |
| PNB-011 | Definir RPO, RTO, frecuencia de respaldo, cifrado, retención física y pruebas de restauración. | Son objetivos y decisiones de arquitectura/operación; no alteran las reglas funcionales consolidadas. | F06 | F02-PRE-026; CAP-047 |
| PNB-012 | Definir almacenamiento, retención física, archivado e índices de auditoría y evidencias. | La regla funcional impide eliminación por usuarios y conserva versiones; el diseño físico es técnico. | F06 | F02-PRE-025; DEC-045, DEC-068 |
| PNB-013 | Diseñar transacciones, reintentos, colas internas y manejo físico de idempotencia. | La unicidad funcional ya está definida; la implementación corresponde a arquitectura. | F06 | DEC-063; CAP-046 |
| PNB-014 | Revisar requisitos legales aplicables a evidencia, auditoría y conservación. | No existe una norma concreta aportada en las fuentes; no debe inventarse un plazo. | F06 o asesoría correspondiente | DEC-045, DEC-068 |
| PNB-015 | Precisar diseño de indicadores operativos, denominadores y presentación. | Se aprobó el conjunto conceptual y se descartó el motor KPI; faltan UX y criterios verificables. | F04/F05 | DEC-038; CAP-039, CAP-043, CAP-044 |

## 3. Cobertura de preguntas F02-PRE-001 a F02-PRE-038

| Pregunta | Resultado en F03 | Referencia |
|---|---|---|
| F02-PRE-001 | Resuelta: una sucursal `LOR-001` / `Loretta`; `TODAS` es alcance. | DEC-031 |
| F02-PRE-002 | Regla funcional resuelta; mecanismo técnico diferido. | DEC-043; PNB-010 |
| F02-PRE-003 | Resuelta mediante jerarquía, superior inmediato y escalamiento. | DEC-001 a DEC-004 |
| F02-PRE-004 | Resuelta para autoridad; puestos ambiguos no conceden rol ni elegibilidad. | DEC-032, DEC-057 |
| F02-PRE-005 | Resuelta: turno sólo restringe cuando la tarea lo configura. | DEC-034 |
| F02-PRE-006 | Resuelta por exclusión de TAR-0195 a TAR-0202. | DEC-047 |
| F02-PRE-007 | Resuelta con ranking y desempate deterministas. | DEC-035 |
| F02-PRE-008 | Resuelta; no se conserva preferencia Apertura ni penalización legacy. | DEC-035 |
| F02-PRE-009 | Resuelta: plan único por sucursal y semana. | DEC-010 |
| F02-PRE-010 | Resuelta con publicación jerárquica por alcance. | DEC-009 |
| F02-PRE-011 | Resuelta mediante publicación incremental en el mismo plan. | DEC-011 |
| F02-PRE-012 | Resuelta: inicio explícito sólo para tareas con medición temporal. | DEC-019 |
| F02-PRE-013 | Resuelta: ejecución anticipada permitida salvo restricción. | DEC-020 |
| F02-PRE-014 | Resuelta: todos los tipos configurados son obligatorios. | DEC-021 |
| F02-PRE-015 | No bloqueante; clasificar sólo tareas incluidas en alcance. | PNB-004 |
| F02-PRE-016 | No bloqueante; definir criterio sólo para tareas incluidas. | PNB-003 |
| F02-PRE-017 | Resuelta por jerarquía, roles explícitos y política por tarea. | DEC-001 a DEC-004, DEC-032 |
| F02-PRE-018 | Resuelta con solicitud y autorización jerárquicas. | DEC-051 |
| F02-PRE-019 | Resuelta: destino individual y traslado con identidad conservada. | DEC-016, DEC-017 |
| F02-PRE-020 | Resuelta: reasignación conserva ejecución e historial. | DEC-018, DEC-036 |
| F02-PRE-021 | Resuelta: Dirección puede forzar cierre con lista, motivo y destino individual. | DEC-014, DEC-016 |
| F02-PRE-022 | Resuelta: reapertura trazable y sustitución sin sobrescritura. | DEC-012, DEC-013, DEC-026 |
| F02-PRE-023 | Resuelta: SGOL no calcula incentivos monetarios. | DEC-037 |
| F02-PRE-024 | Resuelta: sólo indicadores operativos derivados. | DEC-038 |
| F02-PRE-025 | Regla funcional resuelta; diseño físico diferido. | DEC-044, DEC-045; PNB-012 |
| F02-PRE-026 | Diferida por ser técnica/operativa de arquitectura. | PNB-011 |
| F02-PRE-027 | Resuelta por exclusión de T221. | DEC-047 |
| F02-PRE-028 | Resuelta: conservar identidades no resueltas sin fusionar. | DEC-048 |
| F02-PRE-029 | No bloqueante; depende del alcance de migración. | PNB-009 |
| F02-PRE-030 | Resuelta: T182 no se convierte en KPI. | DEC-030 |
| F02-PRE-031 | Resuelta: integración POS no comprobada ni necesaria inicialmente. | DEC-040 |
| F02-PRE-032 | Resuelta: integración S&C no comprobada ni necesaria inicialmente. | DEC-040 |
| F02-PRE-033 | Resuelta: SGOL no opera nómina ni Cash Planner. | DEC-039, DEC-040 |
| F02-PRE-034 | Resuelta: integración de comercio electrónico no comprobada. | DEC-040 |
| F02-PRE-035 | Resuelta: notificaciones sólo dentro de SGOL. | DEC-041 |
| F02-PRE-036 | No aplica al alcance inicial sin integraciones; diseño interno se difiere a F06. | DEC-040, DEC-063; PNB-013 |
| F02-PRE-037 | Resuelta: SGOL no opera nómina y CFG-064 no se reemplaza. | DEC-039 |
| F02-PRE-038 | Resuelta: ningún `AUT-*` se promueve automáticamente. | DEC-042 |

## 4. Capacidades y elementos candidatos a descartar o diferir

| Elemento | Clasificación | Fundamento |
|---|---|---|
| CAP-040 — Incentivos monetarios | Candidata a descartar | DEC-037 |
| CAP-041 — Movimientos de nómina | Candidata a descartar | DEC-039 |
| CAP-050 — Integraciones externas | Candidata a diferir o descartar | DEC-040, DEC-041 |
| TAR-0195 a TAR-0202 y T221 | Excluidas del catálogo y migración operativa | DEC-047 |
| T182 como KPI | Excluido | DEC-030 |
| Proyectos `AUT-*` | No comprobados | DEC-042 |
| Preferencia Apertura, penalizaciones y desempates legacy | Sustituidos | DEC-035 |
| Porcentajes y límites legacy de capacidad | No comprobados para asignación inicial | DEC-035, DEC-064 |
| Modos de evidencia `CUALQUIERA` y `AL_MENOS_N` | No conservados como regla general | DEC-021 |
| Mensajería externa | No requerida | DEC-041 |

## 5. Declaración de no bloqueo

No queda ninguna decisión funcional abierta que afecte integridad, seguridad, trazabilidad, cálculos económicos o transiciones coherentes antes de definir alcance. Los elementos anteriores tienen fase destino o fueron excluidos justificadamente.
