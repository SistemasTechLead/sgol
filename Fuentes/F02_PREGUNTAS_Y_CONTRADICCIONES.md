# F02 — Preguntas y contradicciones de SGOL

| Campo | Valor |
|---|---|
| Estado | Aprobado e incorporado a las fuentes del proyecto |
| Fecha de aprobación e incorporación | 2026-08-26 |

## 1. Control y propósito

Este registro hace visibles las decisiones no resueltas encontradas durante la normalización. No decide por el responsable, no define MVP y no selecciona tecnología.

Convenciones:

- **Contradicción:** dos representaciones o reglas no pueden aplicarse simultáneamente sin una decisión o reconciliación.
- **Pregunta abierta:** falta un dato, política, contrato o elección explícita.
- **[E]:** hecho observado en la fuente.
- **[I]:** clasificación o impacto inferido para organizar la futura Fase 03; debe confirmarse.
- **B-F03:** [I] candidata a bloquear la consolidación funcional o la definición posterior de alcance.
- **P-F04/F06:** [I] candidata a posponerse hasta que el alcance o diseño técnico determine que la capacidad se implementará.

### Criterio indicado por el responsable para el tratamiento posterior

El 2026-08-26 el responsable indicó que el sistema legacy está incompleto, contiene funciones sin uso operativo comprobado y presenta complejidad tipo “código espagueti”. Por ello, la existencia de una tabla, macro, procedimiento, botón, función o nombre técnico en el legacy **no demuestra por sí sola una necesidad del negocio**.

Este criterio se normaliza para Fase 03 de la siguiente manera:

1. Si una respuesta funcional explícita y no contradictoria existe en las fuentes, se utiliza como hecho documentado.
2. Si no existe respuesta y tampoco hay evidencia de que la función sea necesaria o usada en la operación, no se promueve a requisito. Se marca como **capacidad no comprobada / candidata a descartar o diferir**, sin definir todavía el MVP.
3. Si el vacío corresponde sólo a tecnología, almacenamiento, proveedor, rendimiento o despliegue, se difiere a Fase 06 y no bloquea la consolidación funcional salvo que oculte una decisión de negocio.
4. Si la respuesta ausente es necesaria para mantener integridad, seguridad, trazabilidad, cálculo económico o una transición coherente, se presenta al responsable como pregunta con alternativas simples; no se inventa un valor por defecto.
5. Si dos fuentes se contradicen, el “tratamiento sugerido” no elige una de ellas. La contradicción requiere una decisión explícita del responsable.
6. Entre alternativas funcionalmente válidas, Fase 03 debe favorecer la regla más simple que cubra una necesidad operativa comprobada, evitando conservar complejidad legacy sin justificación.

El campo “Tratamiento sugerido” de este documento es, por tanto, una regla de **enrutamiento y simplificación**, no una respuesta automática a la pregunta ni una autorización para copiar comportamiento legacy.

Fuente de control principal: FTE-044, hojas `10_Bloqueos_F20` (38 bloqueos vigentes) y `11_Pendientes_Heredados` (162 registros históricos). Los 162 registros se revisaron y se consolidaron aquí sin duplicar asuntos equivalentes; los resueltos por fases posteriores no se reabren.

## 2. Contradicciones identificadas

### F02-CON-001 — Estado documental de Fase 00 desactualizado — RESUELTA

- **Hecho A:** [E] `F00_CONTROL_DEL_PROYECTO.md`, sección 1 y 11, conserva estado “pendiente de aprobación e incorporación”.
- **Hecho B:** [E] `F01_INVENTARIO_DE_FUENTES.md`, secciones 1 y 10, declara Fase 01 aprobada e incorporada y usa F00 como entrada de control.
- **Verificación al detectar el asunto:** [E] existía una copia idéntica de F01 dentro de `Fuentes`, pero no `Fuentes\F00_CONTROL_DEL_PROYECTO.md`.
- **Decisión del responsable, 2026-08-26:** [E] actualizar F00 y asegurar que aparezca dentro de `Fuentes`.
- **Resolución aplicada:** [E] F00 declara estado completado, aprobado e incorporado; se añadió una copia idéntica en `Fuentes\F00_CONTROL_DEL_PROYECTO.md`.
- **Estado:** Resuelta; no debe formularse como pregunta en Fase 03.
- **Capacidades afectadas:** Ninguna funcional; afecta trazabilidad de fases.

### F02-CON-002 — Convención ISO frente a cálculos semanales legacy

- **Hecho A:** [E] FTE-009, `Funciones puras`, exige `obtener_anio_semana_iso` y límites ISO.
- **Hecho B:** [E] FTE-010, `DIVERGENCIA_ISO` y `PENDIENTES`, registra componentes legacy con convención no ISO.
- **Efecto:** [E] una misma fecha puede resolverse a períodos distintos.
- **Pregunta:** ¿se aprueba ISO como única autoridad y cómo se reconcilia el histórico divergente?
- **Capacidades afectadas:** CAP-010, CAP-011, CAP-015, CAP-024, CAP-037.

### F02-CON-003 — `EN_PROCESO` histórico sin inicio real

- **Hecho A:** [E] FTE-023, `Inicio de ejecución`, establece que una ejecución existe sólo cuando el trabajo se inicia realmente.
- **Hecho B:** [E] FTE-024, `Pendientes`, registra 722 filas legacy `EN_PROCESO` sin timestamp de inicio.
- **Efecto:** [E] no puede afirmarse que las 722 filas sean ejecuciones activas canónicas.
- **Pregunta:** ¿qué clasificación histórica se asignará sin inventar fecha de inicio?
- **Capacidades afectadas:** CAP-026 a CAP-028, CAP-048.

### F02-CON-004 — Validaciones cumplidas con evidencia obligatoria ausente

- **Hecho A:** [E] FTE-025/FTE-027 establecen gates de evidencia para validación según política.
- **Hecho B:** [E] FTE-026 `Pendientes` y FTE-028 `Pendientes` registran 172 ejecuciones validadas como cumplidas sin evidencia obligatoria entregada.
- **Efecto:** [E] el histórico no satisface la regla canónica; no debe corregirse silenciosamente.
- **Pregunta:** ¿cómo se conservará y etiquetará esta inconsistencia histórica durante migración?
- **Capacidades afectadas:** CAP-031, CAP-033, CAP-048.

### F02-CON-005 — Reapertura legacy frente a cierre terminal

- **Hecho A:** [E] FTE-009/FTE-031 y FTE-044 `07_Estados` establecen que `CERRADA` es terminal para el mismo período.
- **Hecho B:** [E] FTE-032 `Transiciones`/`Pendientes` registra una transición ordinaria legacy `CERRADA → DISPONIBLE_PARA_SIMULACION`.
- **Efecto:** [E] ambos ciclos no pueden gobernar la misma identidad de período.
- **Pregunta:** ¿se retira la reapertura ordinaria y se crea un nuevo período, dejando sólo corrección extraordinaria?
- **Capacidades afectadas:** CAP-011, CAP-038.

### F02-CON-006 — Autoridad de validación Piso

- **Hecho A:** [E] FTE-027 declara que la autoridad efectiva se resuelve mediante política y permisos F2.
- **Hecho B:** [E] FTE-028 `Pendientes` registra conflicto entre `S046-DEC-003`, el permiso actual `VALIDAR_PISO`, textos de `Supervisor_Asignado` y seguridad vigente; 116 partidas presentan desacuerdo y 19 carecen de supervisor textual.
- **Efecto:** [E] no existe una fuente única inequívoca para resolver quién valida Piso.
- **Pregunta:** ¿qué política y permiso únicos se aprueban por alcance?
- **Capacidades afectadas:** CAP-007, CAP-032, CAP-033, CAP-043.

### F02-CON-007 — Arrastre como continuidad frente a nuevas filas PLAN/EJEC

- **Hecho A:** [E] FTE-029 y FTE-043, regla 9, indican que arrastrar conserva la identidad salvo obligación distinta explícita.
- **Hecho B:** [E] FTE-030 `Casos_Arrastre`/`Pendientes` registra tres pares `-ARR01` creados como PLAN/EJEC nuevos por necesidad técnica legacy.
- **Efecto:** [E] contar las filas como obligaciones nuevas duplicaría la semántica si eran continuidad.
- **Pregunta:** ¿se confirman los tres pares como las mismas tres `InstanciaTrabajo`?
- **Capacidades afectadas:** CAP-019, CAP-035, CAP-048.

### F02-CON-008 — Cierre por fila frente a cierre del período

- **Hecho A:** [E] FTE-031 define el cierre como hecho y gobierno del período completo.
- **Hecho B:** [E] FTE-032 `Cierres_Historicos`/`Pendientes` registra tres filas con `Fecha_Cierre`, `Cerrada_Por` y/o `Bloqueada_PostCierre`.
- **Efecto:** [E] cerrar una fila no demuestra que el período haya cumplido todos los gates.
- **Pregunta:** ¿cómo se preservan esas marcas históricas sin convertirlas en cierres de período?
- **Capacidades afectadas:** CAP-037, CAP-038, CAP-048.

### F02-CON-009 — Histórico T182 frente a medición KPI

- **Hecho A:** [E] FTE-033 exige fórmula, fuente, unidad, periodicidad y versión para una medición KPI reproducible.
- **Hecho B:** [E] FTE-040 `08_Pendientes` indica que el histórico T182 podría relacionarse con KPI, pero no confirma evidencia suficiente.
- **Efecto:** [E] convertirlo automáticamente podría fabricar mediciones no sustentadas.
- **Pregunta:** ¿hay evidencia suficiente y una regla de reconciliación aprobada?
- **Capacidades afectadas:** CAP-039, CAP-048.

### F02-CON-010 — Rol/validador/responsable expresados como texto o puesto

- **Hecho A:** [E] FTE-007 establece que usuario, empleado, puesto, rol y autorización son conceptos distintos.
- **Hecho B:** [E] FTE-008 `EQUIVALENCIAS`/`PENDIENTES` y FTE-018 `Puestos_Sin_Mapeo` contienen etiquetas históricas que pueden representar puesto, regla de asignación, validador o rol.
- **Efecto:** [E] una equivalencia por texto podría conceder autoridad o elegibilidad incorrectas.
- **Pregunta:** ¿cómo se reclasifica cada etiqueta contra IDs y dominios canónicos?
- **Capacidades afectadas:** CAP-002, CAP-007, CAP-020, CAP-032, CAP-048.

### F02-CON-011 — Estados legacy mezclan plan, ejecución, validación, excepción y cierre

- **Hecho A:** [E] FTE-043 `14. Máquinas de estado` conserva una máquina por entidad/dominio.
- **Hecho B:** [E] FTE-022 `Estados_Actuales`, FTE-024 `Estados_Actuales` y sus pendientes muestran etiquetas legacy que mezclan esos dominios.
- **Efecto:** [E] una columna global no puede conservar todas las reglas y transiciones canónicas.
- **Pregunta:** ¿qué mapeo de cada etiqueta legacy se aprueba sin elegir por semejanza textual?
- **Capacidades afectadas:** CAP-024 a CAP-038, CAP-048.

### F02-CON-012 — Arrastres registrados como validación

- **Hecho A:** [E] FTE-027 y FTE-029 tratan validación y excepción como hechos independientes.
- **Hecho B:** [E] FTE-028 `Pendientes` registra tres arrastres como validación.
- **Efecto:** [E] conservarlos en un único hecho contradice la separación canónica.
- **Pregunta:** ¿cómo se separa decisión de validación y excepción al migrar sin perder procedencia?
- **Capacidades afectadas:** CAP-033, CAP-035, CAP-048.

## 3. Preguntas abiertas vigentes

Las preguntas F02-PRE-001 a F02-PRE-038 corresponden uno a uno con FTE-044, `10_Bloqueos_F20`, registros `F20-B01` a `F20-B38`. La columna “Tratamiento sugerido” es [I] y debe confirmarse en Fase 03; no es una decisión aprobada.

| ID | Pregunta abierta | Fuente y ubicación | Impacto documentado | Tratamiento sugerido [I] | Capacidades |
|---|---|---|---|---|---|
| F02-PRE-001 | ¿Cuál es el catálogo definitivo de sucursales y sus claves únicas? | FTE-044, `10_Bloqueos_F20`, `F20-B01`; FTE-008, `PENDIENTES` | Bloquea despliegue multi-sucursal/alcances | B-F03 | CAP-005, CAP-007 |
| F02-PRE-002 | ¿Qué mecanismo de autenticación e identificador externo estable se usará? | FTE-044, `F20-B02`; FTE-008, `PENDIENTES` | Bloquea autenticación productiva | Separar contrato funcional en F03 de proveedor/tecnología en F06 | CAP-006 |
| F02-PRE-003 | ¿Qué autoridad única aplica a `VALIDAR_PISO`? | FTE-044, `F20-B03`; FTE-028, `Pendientes` | Bloquea bandeja productiva de validación Piso | B-F03 | CAP-007, CAP-032, CAP-033, CAP-043 |
| F02-PRE-004 | ¿Cómo se mapean nueve puestos textuales a `id_puesto` oficial? | FTE-044, `F20-B04`; FTE-018, `Puestos_Sin_Mapeo` | Bloquea políticas automáticas afectadas | B-F03 | CAP-002, CAP-020, CAP-021, CAP-048 |
| F02-PRE-005 | ¿Qué definiciones restringen turno y cuáles declaran ausencia explícita de restricción? | FTE-044, `F20-B05`; FTE-018, `Pendientes` | Bloquea usar turno como gate | B-F03 | CAP-020, CAP-021 |
| F02-PRE-006 | ¿Qué configuración F5/F7/F11/F12/F13 corresponde a TAR-0195…TAR-0202? | FTE-044, `F20-B06`; FTE-040, `08_Pendientes` | Bloquea cutover de esas ocho TAR | B-F03 por ese alcance | CAP-012, CAP-014, CAP-021, CAP-029, CAP-032, CAP-034, CAP-049 |
| F02-PRE-007 | ¿Cuál es la regla final de desempate del balanceador? | FTE-044, `F20-B07`; FTE-020, `Pendientes` | Bloquea asignación automática ante empate | B-F03 | CAP-022, CAP-023 |
| F02-PRE-008 | ¿Qué ventana/penalización de rotación aplica y se conserva o elimina la preferencia implícita de Apertura? | FTE-044, `F20-B08`; FTE-020, `Pendientes` | Bloquea ranking definitivo afectado | B-F03 | CAP-004, CAP-022 |
| F02-PRE-009 | ¿Cuál es el alcance y la clave de negocio exactos de `PlanOperativo`? | FTE-044, `F20-B09`; FTE-022, `Pendientes` | Bloquea unicidad física del plan | B-F03 | CAP-024 |
| F02-PRE-010 | ¿Quién puede publicar el plan inicial y el incremental por alcance? | FTE-044, `F20-B10`; FTE-022, `Pendientes` | Bloquea publicación productiva | B-F03 | CAP-025 |
| F02-PRE-011 | ¿Qué política gobierna obligaciones posteriores a la publicación inicial? | FTE-044, `F20-B11`; FTE-022, `Pendientes` | Bloquea activaciones tardías | B-F03 | CAP-025 |
| F02-PRE-012 | ¿Qué tareas requieren `INICIAR` explícito y duración real? | FTE-044, `F20-B12`; FTE-024, `Pendientes` | Bloquea semántica UX/tiempos | B-F03 | CAP-026, CAP-028, CAP-042 |
| F02-PRE-013 | ¿Se permite ejecución anticipada y bajo qué regla temporal? | FTE-044, `F20-B13`; FTE-024, `Pendientes` | Bloquea inicio fuera de ventana | B-F03 | CAP-026 |
| F02-PRE-014 | Para 83 TAR con evidencia múltiple, ¿aplica `TODAS`, `CUALQUIERA` o `AL_MENOS_N`? | FTE-044, `F20-B14`; FTE-026, `Pendientes` | Bloquea gate de esas TAR | B-F03 | CAP-029, CAP-031 |
| F02-PRE-015 | ¿Qué tipos sustituyen `POR_CLASIFICAR` en 36 TAR? | FTE-044, `F20-B15`; FTE-026, `Pendientes` | Bloquea validación de evidencia | B-F03 | CAP-029 a CAP-031 |
| F02-PRE-016 | ¿Cuál es el criterio verificable para cinco TAR sin criterio de validación? | FTE-044, `F20-B16`; FTE-028, `Pendientes` | Bloquea validación productiva | B-F03 | CAP-032, CAP-033 |
| F02-PRE-017 | ¿Cómo se normaliza la autoridad de validación de diez TAR? | FTE-044, `F20-B17`; FTE-028, `Pendientes` | Bloquea resolución automática del validador | B-F03 | CAP-007, CAP-032, CAP-033 |
| F02-PRE-018 | ¿Cuál es la `PoliticaExcepcion` estructurada del alcance productivo? | FTE-044, `F20-B18`; FTE-030, `Pendientes` | Bloquea automatización de excepciones | B-F03 | CAP-034 a CAP-036 |
| F02-PRE-019 | ¿Cuál es el criterio explícito de traslado para 21 TAR? | FTE-044, `F20-B19`; FTE-030, `Pendientes` | Bloquea reprogramación automática | B-F03 | CAP-034, CAP-035 |
| F02-PRE-020 | ¿Cuál es la política de reasignación y qué ocurre con una ejecución activa? | FTE-044, `F20-B20`; FTE-030, `Pendientes` | Bloquea reasignación/handoff | B-F03 | CAP-023, CAP-035, CAP-036 |
| F02-PRE-021 | ¿Qué gates integran la `PoliticaCierrePeriodo` versionada? | FTE-044, `F20-B21`; FTE-032, `Pendientes` | Bloquea cierre productivo | B-F03 | CAP-037, CAP-038 |
| F02-PRE-022 | ¿Cuál es la ruta extraordinaria de corrección postcierre? | FTE-044, `F20-B22`; FTE-032/FTE-038, `Pendientes` | Bloquea correcciones postcierre | B-F03 si la capacidad se conserva | CAP-008, CAP-030, CAP-033, CAP-038, CAP-047 |
| F02-PRE-023 | ¿Qué política económica relaciona resultados SGOL con incentivos monetarios? | FTE-044, `F20-B23`; FTE-034, `05_Reglas_Gates` | Bloquea bono monetario por tareas | B-F03 | CAP-040, CAP-041 |
| F02-PRE-024 | ¿Cuáles son fórmula, unidad, fuente, periodicidad y versión de cada KPI ejecutable? | FTE-044, `F20-B24`; FTE-034, `02_KPI_Actual` | Bloquea medición automática | B-F03 para KPI que compita por alcance | CAP-039, CAP-040 |
| F02-PRE-025 | ¿Qué retención/acceso requiere auditoría y qué dimensionamiento físico necesitará? | FTE-044, `F20-B25`; FTE-038, `08_Pendientes` | Bloquea diseño físico de observabilidad | Política funcional en F03; tecnología en F06 | CAP-045, CAP-046 |
| F02-PRE-026 | ¿Cuáles son RPO, RTO, frecuencia, cifrado, retención y prueba de restauración? | FTE-044, `F20-B26`; FTE-038, `08_Pendientes` | Bloquea recuperación productiva | Política/objetivos en F03 o F06; tecnología en F06 | CAP-030, CAP-045, CAP-047 |
| F02-PRE-027 | ¿Cuál es el destino canónico de T221 o su exclusión justificada? | FTE-044, `F20-B27`; FTE-040, `08_Pendientes` | Bloquea retiro de ese concepto legacy | B-F03 por ese alcance | CAP-012, CAP-048 |
| F02-PRE-028 | ¿Cómo se resuelven 96 identidades legacy con mapa múltiple? | FTE-044, `F20-B28`; FTE-040, `08_Pendientes` | Bloquea migración histórica afectada | B-F03 para criterio; ejecución posterior | CAP-001, CAP-048 |
| F02-PRE-029 | ¿Qué pruebas y gates permiten retirar el productor legacy? | FTE-044, `F20-B29`; FTE-040, `06_Gates_Cutover` | Bloquea retiro legacy | P-F04/F06 tras definir alcance de migración | CAP-048, CAP-049 |
| F02-PRE-030 | ¿Existe evidencia suficiente para convertir T182 en mediciones KPI? | FTE-044, `F20-B30`; FTE-040, `08_Pendientes` | Bloquea esa migración específica | B-F03 por esa decisión histórica | CAP-039, CAP-048 |
| F02-PRE-031 | ¿Cuál es el POS/sistema de ventas y su contrato? | FTE-044, `F20-B31`; FTE-042, `PENDIENTES` | Bloquea integración POS | P-F04/F06 si la integración entra en alcance | CAP-016, CAP-050 |
| F02-PRE-032 | ¿Cuál es el contrato técnico de Sizes & Colors/S&C? | FTE-044, `F20-B32`; FTE-042, `PENDIENTES` | Bloquea integración automática S&C | P-F04/F06 | CAP-050 |
| F02-PRE-033 | ¿Cash Planner expone integración y qué operaciones pueden autorizarse? | FTE-044, `F20-B33`; FTE-042, `PENDIENTES` | Bloquea automatización financiera | B-F03 para facultades; contrato en F06 si entra en alcance | CAP-041, CAP-050 |
| F02-PRE-034 | ¿Qué plataforma de comercio electrónico se integrará? | FTE-044, `F20-B34`; FTE-042, `PENDIENTES` | Bloquea integración e-commerce | P-F04/F06 | CAP-050 |
| F02-PRE-035 | ¿Qué proveedor y política de WhatsApp/correo/mensajería se usarán? | FTE-044, `F20-B35`; FTE-042, `PENDIENTES` | Bloquea transporte automatizado | Política/consentimiento en F03; proveedor en F06 | CAP-050 |
| F02-PRE-036 | ¿Cuáles son los contratos de archivo, inbox/outbox, reintentos y retención de payloads? | FTE-044, `F20-B36`; FTE-042, `PENDIENTES` | Bloquea modelo físico de integración | Contrato lógico en F03/F05; físico en F06 | CAP-016, CAP-046, CAP-050 |
| F02-PRE-037 | ¿Cómo se reemplaza CFG-064 y se unifica la lectura de nómina en un adaptador? | FTE-044, `F20-B37`; FTE-042, `PENDIENTES` | Bloquea despliegue portable actual | P-F06/implementación | CAP-041, CAP-050 |
| F02-PRE-038 | ¿Qué proyectos `AUT-*` cruzan frontera externa y cuáles son automatización interna? | FTE-044, `F20-B38`; FTE-042, `PENDIENTES` | Bloquea roadmap técnico preciso, no el núcleo | B-F03 para clasificación; detalle posterior | CAP-013, CAP-050 |

## 4. Otros asuntos abiertos heredados que no deben perderse

Los siguientes grupos no crean nuevas preguntas duplicadas, pero deben conservarse como contexto al resolver las anteriores:

| Grupo | Contenido | Fuente y ubicación | Tratamiento |
|---|---|---|---|
| Datos de personal | Bajas/reingresos, fracción diaria parcial, asignación organizacional y mapeos externos. | FTE-006, `Pendientes`; FTE-044, `11_Pendientes_Heredados`, filas F01 | Consolidar al resolver F02-PRE-001, F02-PRE-004, F02-PRE-028 y contratos de importación. |
| Configuración/calendario | Festivos, días laborables, zona horaria, período activo y “Modo Operación”. | FTE-010, `PENDIENTES`; FTE-044, filas F03 | Resolver semántica antes de publicar políticas temporales; tecnología no corresponde aún. |
| Catálogo F4 | Claves jerárquicas, FLU, CHK, KPI, AUT y vínculo TAR-0007. | FTE-012, `Pendientes`; FTE-044, filas F04 | Conservar bajo CAP-012/CAP-013 y preguntas relacionadas. |
| Históricos PLAN/EJEC | 700 EJEC sin PLAN fuente, timestamps faltantes, estados mezclados y referencias legacy. | FTE-016/FTE-024/FTE-040, `Pendientes` | Preservar procedencia y calidad; no fabricar hechos. |
| Excepciones y cierre | Hechos legacy escasos o ausentes no demuestran inexistencia de capacidad; tampoco prueban política completa. | FTE-030/FTE-032, `Pendientes` | Resolver política futura separada de evidencia histórica. |
| Auditoría e integración | Payloads sensibles, acceso a logs, inbox/outbox, dead-letter y contratos de archivo. | FTE-038/FTE-042, `Pendientes` | Definir requisitos funcionales antes del diseño físico. |

## 5. Priorización propuesta para Fase 03

Esta secuencia es [I] una propuesta de facilitación, no una decisión funcional. Debe aplicarse junto con el criterio del responsable: primero comprobar necesidad operativa y no trasladar automáticamente funciones legacy sin uso demostrado.

1. Resolver contradicciones F02-CON-002 a F02-CON-012 y la autoridad F02-PRE-003.
2. Resolver identidad/alcance y políticas que condicionan comportamiento: F02-PRE-001, 004–024, 027–028, 030, 033 y 038.
3. Clasificar qué preguntas externas/técnicas pueden quedar como pendientes no bloqueantes después de definir alcance: F02-PRE-002, 025–026, 029 y 031–037.
4. Registrar literalmente cada decisión aprobada y su efecto en CAP/REG/GLO antes de emitir el catálogo consolidado F03.

## 6. Validación

| Criterio | Resultado |
|---|---|
| Contradicciones identificadas y estado visible | Cumplido: F02-CON-001 a F02-CON-012; F02-CON-001 resuelta y 11 vigentes |
| Preguntas vigentes con ID y fuente | Cumplido: F02-PRE-001 a F02-PRE-038 |
| Pendientes heredados revisados | Cumplido: 162 registros revisados y agrupados sin reabrir los resueltos |
| Inferencias diferenciadas | Cumplido mediante `[E]` y `[I]` |
| Decisiones inventadas | Ninguna |
| MVP o tecnología definidos | No |
