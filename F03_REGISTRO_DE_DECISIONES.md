# F03 — Registro de decisiones de SGOL

## 1. Control del entregable

| Campo | Valor |
|---|---|
| Fase | 03 — Resolución de dudas y consolidación |
| Estado | APROBADA E INCORPORADA A LAS FUENTES DEL PROYECTO |
| Fecha de inicio | 2026-08-26 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Responsable de las decisiones | Responsable humano del proyecto SGOL |
| Regla de cierre | No cerrar mientras existan decisiones funcionales bloqueantes |

## 2. Criterio de registro

- Cada respuesta aprobada se conserva literalmente.
- La normalización posterior se identifica como interpretación y no sustituye el texto del responsable.
- Una ambigüedad con efecto en autoridad, seguridad, trazabilidad, integridad, cálculos económicos o transiciones permanece bloqueante hasta ser aclarada.
- La presencia de comportamiento legacy no se considera prueba suficiente de necesidad operativa.

## 3. Transcripción literal recibida — Grupo 1

```text
1.-
direccion: si opera sus propias tareas, tambien revisa trabajo ajeno, autoriza excepciones, publica planes y cierra semana.
administracion: si opera, si supervisa a subcoordinadora y a asesoras, si valida, si excepciones, si publica planes, si cierra la semana.
subcoordinacion: si opera sus tareas, si revisa trabajo ajeno, si valida trabajo ajeno, si autoriza excepcion, publica planes, si cierra la semana. (es quien opera)
piso ventas: si operan sus tareas, no supervisan, no validan, no autorizan excepciones, no publican planes y no cierran la semana.
quiero manejar este punto como una jerarquia, el director puede supervisar y validar a todos, pero administracion no puede supervisar y validar a director pero si a todo y asi con los demas puestos llegando hasta piso de ventas.
2.- las validaciones van conforme a la jerarquia y todas las tareas de el asesor de ventas pueden ser validadas hasta por el director
3.-nunca a excepcion del director&#x20;
```

## 4. Decisiones registradas

### Transcripción literal recibida — Grupo 2

```text
4. A, 5.B, 6.B
```

### Transcripción literal recibida — Grupo 3

```text
7.A, 8.B, 9.A
```

### Transcripción literal recibida — Grupo 4

```text
10. A, 11. A, 12. B
```

### Transcripción literal recibida — Grupo 5

```text
13. A, 14. B, 15. A
```

### Transcripción literal recibida — Grupo 6

```text
16. A, 17. A, 18. B
```

### Transcripción literal recibida — Grupo 7

```text
19. A, 20. A, 21. A
```

### Transcripción literal recibida — Grupo 8

```text
22. A, 23. A, 24. A
```

### Transcripción literal recibida — Grupo 9

```text
25. C, 26. A, 27. A
```

### Transcripción literal recibida — Grupo 10

```text
28. a, 29. a, 30. a
```

### Transcripción literal recibida — Grupo 11

```text
31. A, 32. A, 33. C
```

### Transcripción literal recibida — Grupo 12

```text
34. A, 35. A, 36. A
```

### Transcripción literal recibida — Grupo 13

```text
37. A, 38. A, 39. A
```

### Transcripción literal recibida — Grupo 14

```text
40 A, 41. A, 42. A
```

### Transcripción literal recibida — Grupo 15

```text
43. A, 44. A, 45. A
```

### Transcripción literal recibida — Grupo 16

```text
46. A, 47. C, 48. A
```

### Transcripción literal recibida — Grupo 17

```text
49\. A, 50. A, 51. A
```

### Transcripción literal recibida — Grupo 18

```text
52. A, 53. A, 54. A 
```

### Transcripción literal recibida — Grupo 19

```text
55. A, 56. A, 57. A
```

### Transcripción literal recibida — Grupo 20

```text
58. A, 59. A, 60. B
```

### Transcripción literal recibida — Grupo 21

```text
61. A, 62. A, 63. A
```

### Transcripción literal recibida — Grupo 22

```text
64. A, 65. A, 66. A
```

### Transcripción literal recibida — Grupo 23

```text
67. A 68. A 69. A
```

### DEC-001 — Autoridad organizada como jerarquía descendente

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | Véase la respuesta 1 de la sección 3. |
| Interpretación controlada | La jerarquía indicada es `DIRECCION > ADMINISTRACION > SUBCOORDINACION > PISO_VENTAS`. Cada nivel puede supervisar y validar niveles inferiores, pero no niveles superiores. |
| Regla complementaria | DEC-004 establece superior inmediato, una sola validación y escalamiento trazado. |
| Fuentes confrontadas | FTE-007, `Matriz inicial de autoridad`; FTE-008, hojas `ROLES_Y_AUTORIDAD`, `PERMISOS_ORIGEN` y `PENDIENTES`; FTE-027, `Permisos y autoridad`; FTE-028, hojas `Permisos_Validacion` y `Pendientes`; F02-CON-006; F02-PRE-003. |
| Capacidades afectadas | CAP-007, CAP-008, CAP-025, CAP-032, CAP-033, CAP-036, CAP-037, CAP-038, CAP-043. |
| Efecto | Sustituye la matriz inicial que impedía a Dirección validar Piso directamente y que reservaba el cierre definitivo a Dirección. El catálogo consolidado deberá reflejar la jerarquía aprobada cuando se resuelvan sus reglas de aplicación. |

### DEC-002 — Las tareas de Piso pueden ser validadas dentro de la jerarquía hasta Dirección

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `las validaciones van conforme a la jerarquia y todas las tareas de el asesor de ventas pueden ser validadas hasta por el director` |
| Interpretación controlada | Una tarea ejecutada por Piso puede ser validada por un nivel superior, incluido Dirección. |
| Regla complementaria | DEC-004 define la responsabilidad primaria y el escalamiento. Sigue pendiente decidir qué tareas requieren validación. |
| Contradicción resuelta parcialmente | F02-CON-006 deja de resolverse mediante una autoridad única fija `VALIDAR_PISO`; la autoridad se deriva de la jerarquía aprobada. Falta definir la regla inequívoca de selección. |
| Capacidades afectadas | CAP-007, CAP-032, CAP-033, CAP-043. |

### DEC-003 — Prohibición de autovalidación con excepción de Dirección

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `nunca a excepcion del director&#x20;` |
| Interpretación controlada | Ningún actor puede validar su propia tarea, excepto Dirección. |
| Aclaración aprobada | DEC-006 permite que Dirección autovalide todas sus tareas. Esto no resuelve por sí mismo las excepciones propias ni el cierre del período, que son operaciones distintas de la validación de tareas. |
| Capacidades afectadas | CAP-008, CAP-033, CAP-036, CAP-038, CAP-040, CAP-041, CAP-045. |

### DEC-004 — Superior inmediato como validador ordinario

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `4. A` |
| Alternativa aprobada | El superior inmediato valida normalmente. Un nivel posterior interviene sólo por ausencia, escalamiento o corrección, dejando motivo. Basta una validación. |
| Efecto | Define responsabilidad primaria y evita exigir una cadena completa de aprobaciones. Dirección conserva capacidad de validar niveles inferiores cuando exista escalamiento trazado. |
| Capacidades afectadas | CAP-007, CAP-032, CAP-033, CAP-043, CAP-045. |

### DEC-005 — Separación entre operación propia, excepción y cierre

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `5.B` |
| Alternativa aprobada | Una persona puede operar y publicar, pero sus propias excepciones y el cierre del período en que participó requieren un superior diferente. |
| Efecto | Impide que Administración y Subcoordinación autoricen sus propias excepciones o cierren por sí solas un período en el que participaron; debe intervenir un superior jerárquico distinto. |
| Excepción aprobada | DEC-007 permite que Dirección autorice su propia excepción y cierre el período aunque haya participado. |
| Capacidades afectadas | CAP-008, CAP-025, CAP-036, CAP-037, CAP-038, CAP-045. |

### DEC-006 — Autovalidación de las tareas propias de Dirección

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `6.B` |
| Alternativa aprobada | Dirección puede autovalidar todas sus tareas. |
| Límite de interpretación | “Tareas” no se extiende automáticamente a autorizar una excepción propia ni a cerrar un período; DEC-005 distingue esas operaciones y DEC-007 debe resolver el caso sin superior. |
| Efecto | La autovalidación de Dirección se conserva como decisión auditable, aunque no representa revisión independiente. |
| Capacidades afectadas | CAP-033, CAP-040, CAP-041, CAP-045. |

### DEC-007 — Excepción de Dirección para excepción propia y cierre

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `7.A` |
| Alternativa aprobada | Dirección puede autorizar su propia excepción y cerrar el período aunque haya participado en la operación. |
| Relación con DEC-005 | Es una excepción expresa a la separación exigida para los demás niveles jerárquicos. |
| Efecto de control | La operación debe quedar auditada, pero no se presenta como revisión independiente ni como separación de funciones. |
| Capacidades afectadas | CAP-008, CAP-036, CAP-037, CAP-038, CAP-045. |

### DEC-008 — Validación humana sólo cuando esté configurada

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `8.B` |
| Alternativa aprobada | Sólo las tareas expresamente configuradas como sujetas a validación requieren validación humana. Las demás concluyen mediante ejecución y evidencia aplicable, sin decisión humana de validación. |
| Efecto | La capacidad de un superior para validar no convierte todas las tareas de niveles inferiores en tareas obligatoriamente validables. Las políticas legacy no se conservan sin necesidad operativa comprobada. |
| Capacidades afectadas | CAP-012, CAP-029 a CAP-033, CAP-042, CAP-043. |

### DEC-009 — Publicación de planes conforme a la jerarquía y alcance

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `9.A` |
| Alternativa aprobada | Cada nivel publica planes para su propio ámbito y para niveles inferiores dentro de su alcance; Dirección puede publicar para todos. |
| Efecto | Administración no publica planes de Dirección; Subcoordinación no publica planes de Administración ni Dirección; Piso no publica planes. El alcance organizacional vigente sigue siendo obligatorio. |
| Capacidades afectadas | CAP-007, CAP-024, CAP-025, CAP-042, CAP-043. |

### DEC-010 — Un plan por sucursal y semana operativa

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `10. A` |
| Alternativa aprobada | Existe un plan operativo único por sucursal y semana operativa; contiene las áreas y niveles jerárquicos incluidos en esa sucursal. |
| Efecto | La clave funcional del plan combina sucursal y período operativo. No se crean planes independientes por área ni un único plan multisucursal. |
| Capacidades afectadas | CAP-005, CAP-011, CAP-024, CAP-025, CAP-043. |

### DEC-011 — Publicación incremental dentro del mismo plan

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `11. A` |
| Alternativa aprobada | Las obligaciones que aparecen después de la publicación inicial se incorporan al mismo plan mediante publicación incremental, conservando quién, cuándo y qué se añadió. |
| Efecto | No se crea un segundo plan para el mismo par sucursal/período. La publicación inicial y cada incremento quedan trazados como eventos o versiones del mismo plan. |
| Capacidades afectadas | CAP-014 a CAP-019, CAP-024, CAP-025, CAP-045. |

### DEC-012 — Dirección puede reabrir un período cerrado

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `12. B` |
| Alternativa aprobada | Dirección puede reabrir el mismo período y devolverlo a operación ordinaria. |
| Contradicción resuelta | F02-CON-005 se resuelve a favor de conservar una reapertura ordinaria autorizada, en lugar de tratar `CERRADA` como estado terminal absoluto. |
| Salvaguardas | DEC-013 exige conservar el cierre anterior, auditar reapertura y cambios, registrar un nuevo cierre y sustituir resultados sin sobrescribirlos. |
| Capacidades afectadas | CAP-008, CAP-011, CAP-038, CAP-040, CAP-041, CAP-045. |

### DEC-013 — Reapertura trazable sin sobrescritura

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `13. A` |
| Alternativa aprobada | Al reabrir se conserva el cierre anterior; se registra quién reabrió y por qué; los cambios posteriores quedan auditados; el nuevo cierre crea otro registro; y los resultados emitidos se marcan como sustituidos en lugar de sobrescribirse. |
| Efecto | Debe poder reconstruirse qué estado y resultado estaban vigentes antes y después de cada reapertura. |
| Capacidades afectadas | CAP-038, CAP-040, CAP-041, CAP-045, CAP-048. |

### DEC-014 — Dirección puede forzar el cierre con pendientes

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `14. B` |
| Alternativa aprobada | Dirección puede forzar el cierre aunque existan asuntos pendientes, dejando motivo y listado de excepciones. |
| Regla complementaria | DEC-016 exige congelar los pendientes y asignar individualmente su destino. |
| Capacidades afectadas | CAP-008, CAP-035 a CAP-038, CAP-043, CAP-045. |

### DEC-015 — Semana ISO y reconciliación histórica conservadora

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `15. A` |
| Alternativa aprobada | La semana operativa usa la convención ISO de lunes a domingo. En el histórico se conserva la etiqueta original; sólo se convierte cuando la fecha permite correspondencia inequívoca y los casos ambiguos quedan marcados. |
| Contradicción resuelta | F02-CON-002 se resuelve usando ISO como autoridad futura y evitando fabricar equivalencias históricas. |
| Capacidades afectadas | CAP-010, CAP-011, CAP-015, CAP-024, CAP-037, CAP-048. |

### DEC-016 — Destino individual de pendientes en cierre forzado

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `16. A` |
| Alternativa aprobada | Los asuntos pendientes se congelan en la semana cerrada y Dirección asigna individualmente un destino: cancelar, posponer o trasladar. Ninguno permanece editable dentro del período cerrado. |
| Efecto | El cierre forzado requiere motivo general, listado de pendientes y decisión explícita por cada obligación afectada. No existe traslado automático. |
| Capacidades afectadas | CAP-035 a CAP-038, CAP-043, CAP-045. |

### DEC-017 — Continuidad de identidad al trasladar una obligación

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `17. A` |
| Alternativa aprobada | Una obligación trasladada conserva la misma identidad de negocio y se vincula al plan de la nueva semana, manteniendo el historial del traslado. |
| Contradicción resuelta | F02-CON-007 se resuelve tratando los registros `-ARR01` como continuidad, no como obligaciones nuevas independientes, cuando representen el mismo trabajo trasladado. |
| Efecto | Los conteos no deben duplicar la obligación por el cambio de período. |
| Capacidades afectadas | CAP-019, CAP-024, CAP-035, CAP-048. |

### DEC-018 — Reasignación mediante cambio trazable de responsable

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `18. B` |
| Alternativa aprobada | Una tarea iniciada conserva la misma ejecución cuando cambia de responsable; se guarda quién trabajó antes y después y el momento del cambio. |
| Efecto | La ejecución requiere historial de responsables e intervalos. La reasignación no puede sobrescribir al responsable anterior ni atribuir todo el tiempo al nuevo. |
| Capacidades afectadas | CAP-023, CAP-026 a CAP-028, CAP-035, CAP-036, CAP-045. |

### DEC-019 — Inicio explícito sólo para tareas con medición de tiempo

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `19. A` |
| Alternativa aprobada | Sólo las tareas configuradas expresamente para medir tiempo requieren inicio explícito y duración real. Las demás pueden pasar directamente a conclusión. |
| Efecto | No se fabrican horas de inicio ni duración para tareas que no necesitan medición temporal. |
| Capacidades afectadas | CAP-012, CAP-026 a CAP-028, CAP-042. |

### DEC-020 — Ejecución anticipada permitida salvo restricción explícita

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `20. A` |
| Alternativa aprobada | Una obligación publicada puede ejecutarse antes del horario programado, salvo que su definición establezca expresamente una restricción de no iniciar antes de una fecha u hora. |
| Efecto | El horario es objetivo operativo por defecto y sólo actúa como gate cuando exista una restricción comprobada. |
| Capacidades afectadas | CAP-012, CAP-025, CAP-026, CAP-042. |

### DEC-021 — Todos los tipos de evidencia configurados son obligatorios

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `21. A` |
| Alternativa aprobada | Sólo las tareas configuradas requieren evidencia. Cuando una tarea configura varios tipos de evidencia, todos son obligatorios. |
| Efecto | Se descartan como reglas generales no comprobadas los modos `CUALQUIERA` y `AL_MENOS_N`. La mera existencia de 83 configuraciones legacy no obliga a conservarlas. |
| Capacidades afectadas | CAP-012, CAP-029 a CAP-031, CAP-033. |

### DEC-022 — La evidencia obligatoria bloquea la conclusión cumplida

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `22. A` |
| Alternativa aprobada | Si falta una evidencia configurada como obligatoria, la tarea no puede concluirse como cumplida; permanece pendiente o bloqueada hasta aportar la evidencia o aplicar una excepción autorizada. |
| Efecto | Un validador ordinario no puede omitir evidencia. Cualquier dispensa debe existir como excepción separada, autorizada y auditable. |
| Capacidades afectadas | CAP-027, CAP-030, CAP-031, CAP-033, CAP-036. |

### DEC-023 — Conservación advertida de validaciones legacy sin evidencia

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `23. A` |
| Alternativa aprobada | Las 172 validaciones históricas conservan su resultado original y se marcan como `histórico sin evidencia comprobable`; no se inventa evidencia ni se consideran conformes con la regla futura. |
| Contradicción resuelta | F02-CON-004 se resuelve preservando procedencia y haciendo visible la inconsistencia, sin cambiar retroactivamente el resultado. |
| Capacidades afectadas | CAP-031, CAP-033, CAP-039, CAP-040, CAP-048. |

### DEC-024 — Separación entre ejecución y validación

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `24. A` |
| Alternativa aprobada | La ejecución concluye con su propio estado y, cuando corresponda, recibe una decisión independiente con resultado `CUMPLIDA`, `INCOMPLETA` o `NO_CUMPLIDA`. |
| Efecto | La validación no sobrescribe el estado de ejecución; cancelación, posposición, traslado y reasignación continúan como excepciones separadas. |
| Contradicción relacionada | Contribuye a resolver F02-CON-011; aún se requiere un tratamiento explícito para etiquetas legacy mezcladas. |
| Capacidades afectadas | CAP-026 a CAP-028, CAP-032 a CAP-036, CAP-048. |

### DEC-025 — Sin consecuencia automática para validación desfavorable

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `25. C` |
| Alternativa aprobada | Una validación `INCOMPLETA` o `NO_CUMPLIDA` no reabre ni crea trabajo automáticamente. El superior decide individualmente si corresponde corregir, cancelar o generar seguimiento. |
| Efecto | La decisión posterior debe quedar trazada y, si genera trabajo, debe vincularse con la ejecución y validación originales. |
| Capacidades afectadas | CAP-019, CAP-027, CAP-033 a CAP-036, CAP-045. |

### DEC-026 — Sustitución jerárquica y trazable de validaciones

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `26. A` |
| Alternativa aprobada | El validador original o un superior jerárquico puede sustituir una decisión equivocada; el motivo es obligatorio y ambas decisiones se conservan. |
| Efecto | La decisión anterior pierde vigencia, pero no se elimina ni sobrescribe. La autoridad se evalúa con la jerarquía y alcance aprobados. |
| Capacidades afectadas | CAP-008, CAP-033, CAP-038, CAP-045. |

### DEC-027 — Arrastres legacy migrados como excepciones

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `27. A` |
| Alternativa aprobada | Los tres arrastres legacy conservan su procedencia y se migran como excepciones de traslado. No se crea una decisión de validación si no existe evidencia separada. |
| Contradicción resuelta | F02-CON-012 queda resuelta mediante separación de los hechos de validación y excepción. |
| Capacidades afectadas | CAP-033, CAP-035, CAP-045, CAP-048. |

### DEC-028 — Estado legacy sin inicio comprobable

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `28. a` |
| Alternativa aprobada | Las 722 filas legacy `EN_PROCESO` sin fecha real de inicio se conservan como `estado legacy sin inicio comprobable` y no se consideran ejecuciones activas. |
| Contradicción resuelta | F02-CON-003 queda resuelta sin fabricar timestamps ni actividad. |
| Capacidades afectadas | CAP-026 a CAP-028, CAP-045, CAP-048. |

### DEC-029 — Marcas históricas por fila no equivalen a cierre de período

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `29. a` |
| Alternativa aprobada | Las marcas de cierre de tres filas se conservan como atributos históricos de esas filas; no demuestran que la semana completa haya cerrado. |
| Contradicción resuelta | F02-CON-008 queda resuelta preservando las marcas sin crear cierres de período no sustentados. |
| Capacidades afectadas | CAP-037, CAP-038, CAP-045, CAP-048. |

### DEC-030 — T182 no se convierte en KPI

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `30. a` |
| Alternativa aprobada | El histórico T182 se conserva únicamente como referencia legacy y no se convierte en mediciones KPI. |
| Contradicción resuelta | F02-CON-009 y F02-PRE-030 quedan resueltas por falta de evidencia funcional suficiente. |
| Efecto | T182 no participa en cálculos, incentivos ni indicadores futuros. |
| Capacidades afectadas | CAP-039, CAP-040, CAP-048. |

### DEC-031 — Catálogo inicial de una sucursal Loretta

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `31. A` |
| Alternativa aprobada | El catálogo inicial contiene una sola sucursal con nombre oficial `Loretta` y código estable `LOR-001`. |
| Efecto | `TODAS` se conserva únicamente como regla de alcance global y nunca como sucursal. El modelo mantiene identidad de sucursal para crecimiento futuro sin inventar sucursales actuales. |
| Capacidades afectadas | CAP-005, CAP-007, CAP-010, CAP-011, CAP-024, CAP-025. |

### DEC-032 — Asignación explícita de roles a usuarios

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `32. A` |
| Alternativa aprobada | Los roles de acceso se asignan expresamente a cada usuario. El puesto laboral no concede autoridad automáticamente. |
| Contradicción resuelta | F02-CON-010 se resuelve impidiendo que textos de puesto, validador o responsable se conviertan automáticamente en roles o permisos. |
| Efecto | Los nueve puestos textuales pendientes pueden mapearse como datos laborales cuando exista evidencia, pero no bloquean la seguridad ni otorgan autoridad. |
| Capacidades afectadas | CAP-002, CAP-007, CAP-020, CAP-032, CAP-045, CAP-048. |

### DEC-033 — Asignación automática sin confirmación humana

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `33. C` |
| Alternativa aprobada | SGOL asigna automáticamente las tareas a personas sin requerir confirmación humana previa. |
| Reglas complementarias | DEC-034 define elegibilidad; DEC-035 ranking y desempate; DEC-036 corrección jerárquica. |
| Capacidades afectadas | CAP-004, CAP-020 a CAP-023, CAP-025, CAP-045. |

### DEC-034 — Elegibilidad automática mínima y restricción opcional de turno

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `34. A` |
| Alternativa aprobada | Para recibir una tarea, la persona debe estar activa, pertenecer a `LOR-001`, tener asignado el rol requerido y disponibilidad positiva. El turno sólo restringe cuando la tarea lo configura expresamente; sin restricción, cualquier turno es válido. |
| Efecto | F02-PRE-005 queda resuelta sin usar turno como gate universal. La ausencia de restricción se distingue de un turno ficticio `Cualquiera`. |
| Capacidades afectadas | CAP-001 a CAP-004, CAP-020 a CAP-023. |

### DEC-035 — Menor carga activa y desempate determinista

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `35. A` |
| Alternativa aprobada | Entre personas elegibles se elige quien tenga menor número de tareas activas asignadas. En empate, quien lleve más tiempo sin recibir asignación automática; el empate final se resuelve por código estable de empleado. |
| Efecto | F02-PRE-007 y F02-PRE-008 quedan resueltas. No se conserva una preferencia implícita por Apertura ni una penalización legacy no comprobada. |
| Capacidades afectadas | CAP-004, CAP-022, CAP-023, CAP-045. |

### DEC-036 — Reasignación jerárquica de asignaciones automáticas

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `36. A` |
| Alternativa aprobada | El superior inmediato o cualquier nivel jerárquico posterior puede corregir una asignación automática, con motivo obligatorio y conservación del historial. |
| Efecto | Se aplica DEC-018 cuando la ejecución ya inició. La corrección no elimina la asignación ni al responsable anterior. |
| Capacidades afectadas | CAP-023, CAP-026, CAP-035, CAP-036, CAP-045. |

### DEC-037 — Sin incentivos monetarios derivados de tareas

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `37. A` |
| Alternativa aprobada | SGOL no calcula bonos ni incentivos monetarios a partir del resultado de tareas. |
| Efecto | CAP-040 deja de ser una necesidad operativa comprobada y queda candidata a descartar. No se requiere una política económica en Fase 03. |
| Capacidades afectadas | CAP-033, CAP-039, CAP-040, CAP-044. |

### DEC-038 — Sólo indicadores operativos básicos derivados

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `38. A` |
| Alternativa aprobada | SGOL muestra cantidades derivadas de tareas pendientes, concluidas, validadas e incumplidas y carga por persona. No incluye un motor de KPI configurable. |
| Efecto | CAP-039 se reduce a indicadores operativos derivados, sin fórmulas KPI administrables. F02-PRE-024 deja de bloquear la consolidación. |
| Capacidades afectadas | CAP-028, CAP-039, CAP-043, CAP-044. |

### DEC-039 — SGOL no opera movimientos de nómina

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `39. A` |
| Alternativa aprobada | SGOL no crea, prepara, autoriza ni envía movimientos de nómina. |
| Efecto | CAP-041 y la sustitución de CFG-064 quedan sin necesidad operativa para SGOL; F02-PRE-037 no bloquea y su detalle técnico se descarta salvo cambio de alcance posterior. |
| Capacidades afectadas | CAP-041, CAP-050. |

### DEC-040 — Sin integraciones externas indispensables

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `40 A` |
| Alternativa aprobada | Ninguna integración con POS, inventario, comercio electrónico o sistemas financieros es indispensable para la operación inicial. Los datos necesarios se administran dentro de SGOL o mediante carga manual. |
| Efecto | F02-PRE-031 a F02-PRE-034 y F02-PRE-036 dejan de ser bloqueantes. Sus contratos y proveedores no se definen en Fase 03. CAP-050 queda como capacidad no comprobada y candidata a diferir o descartar. |
| Capacidades afectadas | CAP-016, CAP-041, CAP-046, CAP-050. |

### DEC-041 — Notificaciones únicamente dentro de SGOL

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `41. A` |
| Alternativa aprobada | SGOL utiliza avisos y bandejas internas; no requiere correo electrónico ni mensajería inmediata como canal operativo. |
| Efecto | F02-PRE-035 no bloquea y no se selecciona proveedor de mensajería. |
| Capacidades afectadas | CAP-042, CAP-043, CAP-050. |

### DEC-042 — Proyectos `AUT-*` no promovidos por existencia legacy

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `42. A` |
| Alternativa aprobada | Ningún proyecto `AUT-*` se convierte automáticamente en requisito; sólo podrá incorporarse si posteriormente se describe y aprueba una necesidad operativa concreta. |
| Efecto | F02-PRE-038 queda resuelta. Los `AUT-*` existentes se clasifican como capacidades no comprobadas, candidatas a descartar o diferir. |
| Capacidades afectadas | CAP-013, CAP-050. |

### DEC-043 — Cuentas individuales sin uso compartido

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `43. A` |
| Alternativa aprobada | Cada persona utiliza una cuenta individual; no existen cuentas compartidas. |
| Efecto | Toda validación, excepción, reasignación, publicación, reapertura y cierre debe atribuirse a un usuario individual. El mecanismo técnico de autenticación se difiere a Fase 06. |
| Capacidades afectadas | CAP-006 a CAP-008, CAP-025, CAP-033, CAP-036, CAP-038, CAP-045. |

### DEC-044 — Visibilidad jerárquica del historial funcional

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `44. A` |
| Alternativa aprobada | Cada usuario consulta sus propias acciones; cada superior consulta las propias y las de niveles inferiores dentro de su alcance; Dirección consulta todo. |
| Efecto | La auditoría funcional aplica la jerarquía y alcance aprobados; no expone indiscriminadamente acciones de usuarios pares o superiores. |
| Capacidades afectadas | CAP-007, CAP-028, CAP-043 a CAP-045. |

### DEC-045 — Auditoría no eliminable por usuarios

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `45. A` |
| Alternativa aprobada | Ningún usuario puede eliminar registros de auditoría. Pueden archivarse, pero permanecen vinculados a las operaciones. La duración física y la política legal se definen en Fase 06. |
| Efecto | F02-PRE-025 queda funcionalmente resuelta. Dimensionamiento, almacenamiento y retención física se difieren a Fase 06. |
| Capacidades afectadas | CAP-045 a CAP-047. |

### DEC-046 — Catálogo legacy sujeto a revisión funcional individual

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `46. A` |
| Alternativa aprobada | Ninguna de las 202 definiciones legacy se convierte automáticamente en requisito. En Fase 04 se revisarán y clasificarán individualmente según necesidad operativa comprobada. |
| Efecto | La existencia de una definición, configuración o automatización legacy no determina el alcance. Las capacidades genéricas se consolidan en F03 y las tareas concretas se priorizan en F04. |
| Capacidades afectadas | CAP-012 a CAP-018, CAP-029, CAP-032, CAP-034, CAP-039. |

### DEC-047 — Exclusión de TAR-0195 a TAR-0202 y T221

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `47. C` |
| Alternativa aprobada | TAR-0195 a TAR-0202 y T221 se excluyen tanto del catálogo funcional futuro como del histórico operativo que se migre. |
| Límite obligatorio | La exclusión no elimina ni modifica los archivos fuente originales. Las referencias permanecen en las fuentes documentales sólo como procedencia del análisis. |
| Efecto | F02-PRE-006 y F02-PRE-027 quedan resueltas; no se requiere completar su configuración. |
| Capacidades afectadas | CAP-012, CAP-014, CAP-021, CAP-029, CAP-032, CAP-034, CAP-048, CAP-049. |

### DEC-048 — Identidades legacy múltiples conservadas sin fusión

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `48. A` |
| Alternativa aprobada | Las 96 identidades históricas con equivalencias múltiples se conservan como identidades legacy no resueltas. No se fusionan con una persona canónica ni participan en indicadores por persona hasta su resolución. |
| Efecto | F02-PRE-028 queda resuelta para consolidación. La reconciliación futura es trabajo de migración y no puede elegir coincidencias por orden o similitud textual. |
| Capacidades afectadas | CAP-001, CAP-039, CAP-045, CAP-048. |

### DEC-049 — Un solo rol activo por usuario y sucursal

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `49\. A` |
| Interpretación controlada | La barra invertida se conserva como parte de la respuesta literal; la selección corresponde a la alternativa 49.A. |
| Alternativa aprobada | Cada usuario tiene un solo rol activo por sucursal. La jerarquía concede autoridad sobre niveles inferiores sin acumular sus roles. |
| Efecto | Se eliminan combinaciones simultáneas de roles en el mismo alcance y se simplifica la evaluación de autoridad. |
| Capacidades afectadas | CAP-006 a CAP-008, CAP-020, CAP-032, CAP-045. |

### DEC-050 — Dirección administra usuarios y roles

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `50. A` |
| Alternativa aprobada | Sólo Dirección puede crear, activar o desactivar usuarios y asignar, cambiar o revocar roles. |
| Efecto | Ningún otro nivel puede elevar privilegios propios o ajenos. Todas las operaciones de seguridad quedan auditadas. |
| Capacidades afectadas | CAP-006 a CAP-008, CAP-045. |

### DEC-051 — Excepciones solicitadas y autorizadas jerárquicamente

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `51. A` |
| Alternativa aprobada | La persona responsable solicita la excepción y su superior inmediato aprueba o rechaza. Puede escalarse a niveles posteriores con motivo. Dirección puede autorizar su propia excepción conforme a DEC-007. |
| Efecto | Se separan solicitud y autorización para los niveles inferiores a Dirección y se conserva motivo, actor, fecha y resultado. |
| Capacidades afectadas | CAP-008, CAP-034 a CAP-036, CAP-045. |

### DEC-052 — Dirección administra los datos laborales operativos

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `52. A` |
| Alternativa aprobada | Sólo Dirección administra altas, bajas, puesto, turno y sucursal de los empleados. |
| Efecto | La administración de datos laborales y la administración de cuentas/roles son facultades distintas, pero ambas quedan reservadas a Dirección. Los cambios conservan vigencia e historial. |
| Capacidades afectadas | CAP-001 a CAP-003, CAP-005, CAP-045. |

### DEC-053 — Dirección administra definiciones y políticas de tarea

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `53. A` |
| Alternativa aprobada | Sólo Dirección crea o modifica definiciones de tarea y sus reglas de evidencia, validación, turno y horario. |
| Efecto | Las configuraciones deben versionarse y auditarse porque alteran elegibilidad, cumplimiento y autoridad. |
| Capacidades afectadas | CAP-009, CAP-012 a CAP-014, CAP-021, CAP-029, CAP-032, CAP-034, CAP-045. |

### DEC-054 — Dirección captura y corrige disponibilidad

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `54. A ` |
| Alternativa aprobada | Dirección captura y corrige la disponibilidad utilizada por la asignación automática. |
| Efecto | Los empleados no modifican su propia elegibilidad. Toda actualización de disponibilidad queda atribuida y auditada. |
| Capacidades afectadas | CAP-003, CAP-004, CAP-020 a CAP-023, CAP-045. |

### DEC-055 — Ejecuciones legacy huérfanas conservadas con advertencia

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `55. A` |
| Alternativa aprobada | Las aproximadamente 700 ejecuciones legacy sin plan fuente se conservan como ejecuciones históricas huérfanas, con referencia original y advertencia. No se fabrica un plan y no participan en métricas que exijan plan. |
| Efecto | Se conserva procedencia sin crear relaciones inexistentes. |
| Capacidades afectadas | CAP-024, CAP-028, CAP-039, CAP-045, CAP-048. |

### DEC-056 — Mapeo de estados legacy sólo con evidencia inequívoca

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `56. A` |
| Alternativa aprobada | Las etiquetas legacy se separan por dominio únicamente cuando exista evidencia inequívoca. Los casos ambiguos conservan la etiqueta original, se marcan como no resueltos y no participan en transiciones canónicas. |
| Contradicción resuelta | F02-CON-011 queda resuelta sin mapeo por semejanza textual. |
| Capacidades afectadas | CAP-024 a CAP-038, CAP-045, CAP-048. |

### DEC-057 — Puestos legacy ambiguos conservados sin efecto funcional

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `57. A` |
| Alternativa aprobada | Los puestos históricos sin coincidencia inequívoca se conservan como texto legacy no resuelto y no conceden rol, elegibilidad ni autoridad. |
| Efecto | F02-PRE-004 deja de bloquear. La normalización futura requiere una equivalencia explícita aprobada; nunca similitud automática. |
| Capacidades afectadas | CAP-002, CAP-007, CAP-020, CAP-032, CAP-045, CAP-048. |

### DEC-058 — Zona horaria `America/Mexico_City`

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `58. A` |
| Alternativa aprobada | La zona horaria operativa de `LOR-001` es `America/Mexico_City`. |
| Efecto | Fechas de semana, vencimientos, publicaciones, cierres y auditoría se interpretan en esa zona. |
| Capacidades afectadas | CAP-009 a CAP-011, CAP-015 a CAP-018, CAP-024 a CAP-028, CAP-037, CAP-038, CAP-045. |

### DEC-059 — Calendario operativo administrado por Dirección

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `59. A` |
| Alternativa aprobada | Dirección administra días laborables, festivos y cierres extraordinarios. Las tareas programadas respetan ese calendario y la disponibilidad individual. |
| Efecto | El calendario no depende de una integración externa. Sus cambios deben versionarse y auditarse. |
| Capacidades afectadas | CAP-009, CAP-010, CAP-014, CAP-015, CAP-020, CAP-045. |

### DEC-060 — Activación manual, programada y por hechos internos

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `60. B` |
| Alternativa aprobada | SGOL admite creación manual, programación recurrente y activaciones por eventos o condiciones internas. No admite disparadores de sistemas externos dentro del alcance aprobado. |
| Reglas complementarias | DEC-061 define eventos internos; DEC-062 condiciones internas; DEC-063 evita duplicados. Los nombres, macros o reglas legacy no se convierten automáticamente en disparadores. |
| Capacidades afectadas | CAP-014 a CAP-018, CAP-025, CAP-045. |

### DEC-061 — Catálogo mínimo de eventos internos configurables

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `61. A` |
| Alternativa aprobada | Una regla configurada puede reaccionar a conclusión de tarea, resultado de validación, resolución de excepción y apertura o cierre de semana. |
| Efecto | Ningún evento genera obligaciones por defecto; Dirección debe vincularlo expresamente con una definición de tarea vigente. |
| Capacidades afectadas | CAP-014, CAP-016, CAP-018, CAP-019, CAP-025, CAP-033, CAP-036, CAP-038. |

### DEC-062 — Condiciones internas configurables

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `62. A` |
| Alternativa aprobada | Las reglas pueden evaluar fecha, estado, conteos o umbrales operativos internos. |
| Límite | Los umbrales sirven para activar trabajo y no constituyen KPI monetarios ni un motor general de indicadores. Sólo usan datos internos de SGOL. |
| Capacidades afectadas | CAP-014, CAP-017, CAP-018, CAP-039, CAP-045. |

### DEC-063 — Idempotencia funcional de la generación

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `63. A` |
| Alternativa aprobada | Un evento repetido o una condición reevaluada no crea duplicados: existe como máximo una obligación para la misma regla, alcance, período y hecho originador. |
| Efecto | Los reintentos y reevaluaciones recuperan la obligación existente; no aumentan artificialmente carga, plan ni indicadores. |
| Capacidades afectadas | CAP-015 a CAP-019, CAP-024, CAP-025, CAP-039, CAP-045, CAP-046. |

### DEC-064 — Disponibilidad binaria por día

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `64. A` |
| Alternativa aprobada | La disponibilidad diaria se registra como disponible o no disponible. |
| Efecto | No se usan porcentajes ni ventanas horarias para el balanceo inicial. La elegibilidad exige disponibilidad positiva, interpretada como `disponible`. |
| Capacidades afectadas | CAP-003, CAP-004, CAP-020 a CAP-023. |

### DEC-065 — Recurrencia omitida en día no laborable

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `65. A` |
| Alternativa aprobada | Si una recurrencia corresponde a un día no laborable, esa ocurrencia no se genera. No se adelanta ni se mueve automáticamente. |
| Efecto | El calendario administrado por Dirección es gate de activación programada. |
| Capacidades afectadas | CAP-010, CAP-014, CAP-015, CAP-018. |

### DEC-066 — Vencimiento visible sin transición automática

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `66. A` |
| Alternativa aprobada | Una tarea vencida sin conclusión permanece activa y se muestra como vencida hasta que se concluya o reciba una excepción autorizada. |
| Efecto | El vencimiento no crea por sí mismo una validación `NO_CUMPLIDA`, cancelación ni traslado. |
| Capacidades afectadas | CAP-019, CAP-027, CAP-028, CAP-034 a CAP-036, CAP-042, CAP-043. |

### DEC-067 — Sustitución jerárquica de evidencia según estado

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `67. A` |
| Alternativa aprobada | El responsable puede sustituir evidencia antes de concluir la tarea. Después de la conclusión sólo un superior puede hacerlo con motivo. Una validación existente no cambia automáticamente. |
| Efecto | Si la evidencia nueva cambia el fundamento de una validación, debe aplicarse DEC-026 para sustituir explícitamente la decisión. |
| Capacidades afectadas | CAP-030, CAP-031, CAP-033, CAP-045. |

### DEC-068 — Conservación de todas las versiones de evidencia

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `68. A` |
| Alternativa aprobada | Ninguna versión de evidencia se elimina; la versión anterior queda marcada como sustituida y continúa vinculada a la tarea. |
| Efecto | Cada validación puede reconstruir exactamente qué versiones de evidencia estaban vigentes cuando fue emitida. |
| Capacidades afectadas | CAP-030, CAP-031, CAP-033, CAP-045, CAP-047. |

### DEC-069 — Acceso jerárquico a evidencias

| Campo | Contenido |
|---|---|
| Estado | APROBADA |
| Decisión literal | `69. A` |
| Alternativa aprobada | El responsable consulta las evidencias de sus tareas; los superiores consultan las de niveles inferiores dentro de su alcance; Dirección consulta todas. |
| Efecto | La visibilidad de evidencia aplica las mismas reglas de jerarquía, alcance y auditoría funcional. |
| Capacidades afectadas | CAP-007, CAP-028, CAP-030, CAP-033, CAP-043 a CAP-045. |

## 5. Decisiones bloqueantes siguientes

| ID | Asunto | Motivo de bloqueo | Estado |
|---|---|---|---|
| — | Ninguna | — | RESUELTO |

## 6. Auditoría final de contradicciones F02

| Contradicción | Resolución aprobada | Decisiones |
|---|---|---|
| F02-CON-001 | Ya estaba resuelta en F02 y no se reabrió. | Resolución previa F00/F02 |
| F02-CON-002 | Semana ISO como autoridad futura; histórico ambiguo conserva etiqueta. | DEC-015 |
| F02-CON-003 | `EN_PROCESO` sin inicio queda como estado legacy no comprobable. | DEC-028 |
| F02-CON-004 | Histórico conserva resultado con advertencia; regla futura bloquea cumplimiento sin evidencia. | DEC-022, DEC-023 |
| F02-CON-005 | Dirección puede reabrir; cierres y cambios anteriores permanecen. | DEC-012, DEC-013 |
| F02-CON-006 | Jerarquía descendente; superior inmediato valida y niveles posteriores escalan. | DEC-001 a DEC-004 |
| F02-CON-007 | El traslado conserva la identidad de la obligación. | DEC-017 |
| F02-CON-008 | Marcas por fila no equivalen a cierre de período. | DEC-029 |
| F02-CON-009 | T182 no se convierte en KPI. | DEC-030 |
| F02-CON-010 | Puesto y rol permanecen separados; textos ambiguos no conceden autoridad. | DEC-032, DEC-057 |
| F02-CON-011 | Estados por dominio; etiquetas ambiguas permanecen legacy y no gobiernan transiciones. | DEC-024, DEC-056 |
| F02-CON-012 | Arrastres históricos se conservan como excepciones, no como validaciones. | DEC-027 |

## 7. Validación del registro

| Criterio | Resultado |
|---|---|
| Decisiones con ID continuo | Cumplido: DEC-001 a DEC-069 |
| Respuestas literales conservadas | Cumplido: grupos 1 a 23 |
| Decisiones aprobadas | 69 |
| IDs faltantes o duplicados | Ninguno |
| Contradicciones bloqueantes restantes | Ninguna |
| Preguntas F02-PRE con resolución o fase destino | 38 de 38; véase `F03_PENDIENTES_NO_BLOQUEANTES.md` |

## 8. Estado de la fase

La Fase 03 quedó completada con observaciones no bloqueantes, aprobada por el responsable e incorporada a las fuentes del proyecto el 2026-08-27. La Fase 04 está habilitada.
