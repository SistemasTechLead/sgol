# F04 — Alcance y definición propuesta del MVP de SGOL

## 1. Control del entregable

| Campo | Valor |
|---|---|
| Fase | 04 — Alcance, prioridades y MVP |
| Estado | APROBADO E INCORPORADO A LAS FUENTES DEL PROYECTO |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Entradas aprobadas | `F00_CONTROL_DEL_PROYECTO.md`; `F01_INVENTARIO_DE_FUENTES.md`; entregables F02; `F03_REGISTRO_DE_DECISIONES.md`; `F03_CATALOGO_CONSOLIDADO.md`; `F03_PENDIENTES_NO_BLOQUEANTES.md` |
| Catálogo clasificado | CAP-001 a CAP-050; TAR-0001 a TAR-0202 |
| Límite | No selecciona tecnología, no diseña arquitectura, no especifica historias y no produce código |
| Aprobación del responsable | `Apruebo F04-JP-001 a F04-JP-005 y los tres entregables F04` |

## 2. Convenciones de alcance

- **MVP:** indispensable para probar un ciclo operativo útil de SGOL en `LOR-001` con usuarios reales, tareas reales, responsabilidad, evidencia, validación, consulta y trazabilidad.
- **Posterior:** necesidad funcional reconocida, pero no indispensable para validar el primer ciclo operativo.
- **Opcional:** posibilidad plausible sin necesidad operativa actual comprobada; no constituye compromiso de versión.
- **Fuera de alcance:** elemento expresamente excluido o incompatible con las decisiones aprobadas.
- **Hecho documentado:** contenido respaldado por fuentes o decisiones aprobadas.
- **Decisión de alcance:** juicio de producto aprobado y aplicable a las fases siguientes.

La categoría F03 `Conservada`, `Simplificada` o `Redefinida` confirma la interpretación funcional, no su entrada automática al MVP. La categoría F03 `Condicional` o `No comprobada` tampoco se promueve sin decisión.

## 3. Objetivo aprobado del MVP

Permitir que una sola sucursal, `LOR-001` / Loretta, administre un conjunto pequeño y explícitamente aprobado de tareas operativas mediante un ciclo completo y trazable:

1. Dirección mantiene personas, cuentas, rol, disponibilidad, calendario y definiciones vigentes.
2. SGOL crea obligaciones manuales o programadas sin duplicarlas.
3. SGOL determina elegibilidad, asigna por la regla aprobada y permite corrección jerárquica con motivo.
4. La persona responsable consulta, ejecuta y concluye su trabajo con la evidencia configurada.
5. El superior correspondiente valida cuando la definición lo exige.
6. Responsables y superiores consultan trabajo, vencimientos, historial e indicadores operativos básicos dentro de su alcance.
7. Los hechos de configuración, asignación, ejecución, evidencia y validación conservan autoría e historial.

### 3.1 Resultado de negocio que debe probar

El MVP no busca digitalizar todo el catálogo legacy. Debe demostrar que SGOL puede coordinar trabajo real de una semana operativa con autoridad clara y evidencia reconstruible, sin depender de incentivos, nómina, mensajería externa, integraciones externas ni automatizaciones heredadas.

### 3.2 Límites deliberados del primer corte

- Una sucursal: `LOR-001`.
- Zona horaria: `America/Mexico_City`.
- Un rol activo por usuario y sucursal.
- Activación MVP: creación manual y recurrencia programada. Los eventos y condiciones internas quedan para una versión posterior.
- Sin migración histórica ni coexistencia obligatoria con el productor legacy.
- Sin flujo formal de excepciones, cierre forzado o reapertura en el primer corte; las tareas vencidas continúan activas conforme a DEC-066. Estas funciones quedan posteriores y deberán incorporarse antes de declarar equivalencia operativa completa con el ciclo semanal aprobado en F03.
- Sin selección tecnológica ni decisiones de diseño físico.

## 4. Áreas funcionales del MVP

| Área F04 | Resultado incluido en MVP | Capacidades principales |
|---|---|---|
| AF-01 — Personas, organización y autoridad | Dirección administra empleados, cuentas, sucursal, rol único, disponibilidad y alcance jerárquico. | CAP-001 a CAP-007 |
| AF-02 — Gobierno operativo | Dirección administra configuración, calendario, semana y definiciones de las tareas aprobadas. | CAP-009 a CAP-012 |
| AF-03 — Activación y generación básica | Creación manual y recurrencia respetan calendario e idempotencia funcional. | CAP-014, CAP-015, CAP-018, CAP-019 |
| AF-04 — Elegibilidad, asignación y plan | SGOL determina candidatos, asigna de forma explicable, admite corrección trazada y publica un plan semanal incremental. | CAP-020 a CAP-025 |
| AF-05 — Ejecución, evidencia y validación | Se consulta y concluye trabajo; la evidencia obligatoria bloquea cumplimiento y la validación es jerárquica e independiente. | CAP-027 a CAP-033 |
| AF-06 — Visibilidad, control y continuidad funcional | Bandejas, supervisión, indicadores básicos, auditoría, no duplicación y conservación de historial. | CAP-039, CAP-042 a CAP-047 |

## 5. Clasificación de las 50 capacidades

| ID | Clasificación F04 propuesta | Prioridad | Alcance o motivo | Dependencias principales |
|---|---|---:|---|---|
| CAP-001 | MVP | P0 | Identidad y vigencia laboral estables. | — |
| CAP-002 | MVP | P0 | Puesto y turno como datos, sin autoridad implícita. | CAP-001 |
| CAP-003 | MVP | P0 | Disponibilidad diaria necesaria para elegibilidad. | CAP-001 |
| CAP-004 | MVP | P0 | Carga activa necesaria para asignación. | CAP-019, CAP-022 |
| CAP-005 | MVP | P0 | Alcance inicial `LOR-001`. | — |
| CAP-006 | MVP | P0 | Cuentas individuales administradas por Dirección. | CAP-001, CAP-005 |
| CAP-007 | MVP | P0 | Jerarquía y rol único gobiernan autoridad y visibilidad. | CAP-005, CAP-006 |
| CAP-008 | Posterior | P1 | Autorizaciones explícitas se incorporan con el flujo formal de excepciones. | CAP-007, CAP-034 a CAP-036 |
| CAP-009 | MVP | P0 | Configuración versionada y auditable del alcance incluido. | CAP-007, CAP-045 |
| CAP-010 | MVP | P0 | Semana, zona y calendario del plan. | CAP-005, CAP-009 |
| CAP-011 | MVP | P0 | Período semanal para planificar; reapertura se implementa con CAP-038 posterior. | CAP-010, CAP-024 |
| CAP-012 | MVP | P0 | Sólo las ocho definiciones aprobadas en 6.1; no las 202 por herencia. | CAP-009 |
| CAP-013 | Opcional | P2 | Dependencias, flujos y checklists sólo si una tarea aprobada demuestra necesitarlos. | CAP-012 |
| CAP-014 | MVP | P0 | Alcance MVP limitado a alta manual y programación recurrente. | CAP-009, CAP-010, CAP-012 |
| CAP-015 | MVP | P0 | Recurrencia idempotente y omisión de días no laborables. | CAP-010, CAP-014, CAP-018 |
| CAP-016 | Posterior | P1 | Activación por eventos internos; no indispensable para el primer corte. | CAP-014, CAP-018 |
| CAP-017 | Posterior | P1 | Evaluación de condiciones y umbrales internos. | CAP-014, CAP-018 |
| CAP-018 | MVP | P0 | Solicitud de generación única para altas manuales y recurrentes. | CAP-014, CAP-015 |
| CAP-019 | MVP | P0 | Instancia de trabajo única y recuperable. | CAP-018 |
| CAP-020 | MVP | P0 | Elegibilidad por vigencia, sucursal, rol y disponibilidad. | CAP-001 a CAP-007, CAP-019 |
| CAP-021 | MVP | P0 | Política explícita de rol, disponibilidad y turno para las tareas MVP. | CAP-009, CAP-012, CAP-020 |
| CAP-022 | MVP | P0 | Asignación determinista por menor carga y desempates aprobados. | CAP-004, CAP-020 |
| CAP-023 | MVP | P0 | Corrección jerárquica con motivo e historial. | CAP-007, CAP-022, CAP-045 |
| CAP-024 | MVP | P0 | Un plan por sucursal y semana. | CAP-010, CAP-011, CAP-019 |
| CAP-025 | MVP | P0 | Publicación jerárquica e incremental del mismo plan. | CAP-007, CAP-024 |
| CAP-026 | Posterior | P1 | Inicio explícito y medición temporal sólo para tareas que lo justifiquen. | CAP-012, CAP-027 |
| CAP-027 | MVP | P0 | Conclusión con evidencia y vencimiento visible sin transición automática. | CAP-019, CAP-029 a CAP-031 |
| CAP-028 | MVP | P0 | Consulta de trabajo, vencimientos, procedencia e historial. | CAP-019, CAP-027, CAP-045 |
| CAP-029 | MVP | P0 | Evidencia configurada sólo para las tareas MVP que la requieran. | CAP-009, CAP-012 |
| CAP-030 | MVP | P0 | Sustitución controlada y versiones de evidencia. | CAP-027, CAP-029, CAP-045 |
| CAP-031 | MVP | P0 | Evidencia obligatoria como gate de cumplimiento. | CAP-027, CAP-029, CAP-030 |
| CAP-032 | MVP | P0 | Política de validación sólo para las tareas MVP que la requieran. | CAP-007, CAP-009, CAP-012 |
| CAP-033 | MVP | P0 | Validación separada, jerárquica y con resultados aprobados. | CAP-007, CAP-030 a CAP-032 |
| CAP-034 | Posterior | P1 | Catálogo formal de causas y tipos de excepción. | CAP-009, CAP-012 |
| CAP-035 | Posterior | P1 | Cancelación, posposición, traslado y reasignación por excepción. | CAP-034, CAP-036 |
| CAP-036 | Posterior | P1 | Solicitud, autorización y escalamiento de excepciones. | CAP-007, CAP-008, CAP-034 |
| CAP-037 | Posterior | P1 | Gates, pendientes y cierre forzado. | CAP-011, CAP-035, CAP-036 |
| CAP-038 | Posterior | P1 | Cierre, reapertura y sustitución histórica. | CAP-011, CAP-037, CAP-045 |
| CAP-039 | MVP | P0 | Conteos operativos básicos; sin KPI configurable o monetario. | CAP-019, CAP-027, CAP-033 |
| CAP-040 | Fuera de alcance | — | Incentivos monetarios excluidos por DEC-037. | — |
| CAP-041 | Fuera de alcance | — | Movimientos de nómina excluidos por DEC-039. | — |
| CAP-042 | MVP | P0 | Bandeja interna personal; sin mensajería externa. | CAP-019, CAP-027 a CAP-030 |
| CAP-043 | MVP | P0 | Supervisión jerárquica del trabajo y validaciones del MVP. | CAP-007, CAP-033, CAP-039 |
| CAP-044 | MVP | P0 | Vista de Dirección con indicadores operativos básicos. | CAP-007, CAP-039, CAP-043 |
| CAP-045 | MVP | P0 | Auditoría funcional no eliminable por usuarios. | Transversal |
| CAP-046 | MVP | P0 | Semántica funcional para evitar duplicados y registrar errores. | CAP-018, CAP-019, CAP-045 |
| CAP-047 | MVP | P0 | Conservación y recuperación sin borrar historial; su diseño físico queda para F06. | CAP-045 |
| CAP-048 | Opcional | P2 | Migración histórica sólo si se aprueba valor operativo concreto. | CAP-001, CAP-028, CAP-045 |
| CAP-049 | Opcional | P2 | Coexistencia y retiro legacy sólo si se aprueba migración. | CAP-048 |
| CAP-050 | Fuera de alcance | — | Integraciones y mensajería externas no requeridas inicialmente; `AUT-*` no se hereda. | — |

### 5.1 Conteo de cobertura

| Clasificación | Cantidad | IDs |
|---|---:|---|
| MVP | 35 | CAP-001 a CAP-007; CAP-009 a CAP-012; CAP-014, CAP-015; CAP-018 a CAP-025; CAP-027 a CAP-033; CAP-039; CAP-042 a CAP-047 |
| Posterior | 9 | CAP-008; CAP-016, CAP-017; CAP-026; CAP-034 a CAP-038 |
| Opcional | 3 | CAP-013; CAP-048; CAP-049 |
| Fuera de alcance | 3 | CAP-040; CAP-041; CAP-050 |
| Total | 50 | CAP-001 a CAP-050, sin faltantes ni duplicados |

## 6. Revisión individual de definiciones de tarea

### 6.1 Definiciones aprobadas para MVP

La señal `ACTIVA_REAL_GUARDADA` de FTE-012 permitió formular una propuesta sin promover automáticamente el catálogo legacy. El responsable aprobó mediante F04-JP-002 las ocho entradas siguientes como catálogo concreto del MVP.

| ID | Definición | Señal documental | Tratamiento MVP aprobado |
|---|---|---|---|
| TAR-0005 | Monitorear el avance de ventas y activar acciones correctivas | `ACTIVA_REAL_GUARDADA` | Programada; conservar sólo si se confirma ejecución actual y resultado verificable. |
| TAR-0007 | Liberar la mercancía al vencer un Separado de Cortesía | `ACTIVA_REAL_GUARDADA` | Alta manual en MVP; activación por proceso queda posterior. |
| TAR-0008 | Resolver una controversia de asignación de venta con base en evidencia | `ACTIVA_REAL_GUARDADA` | Alta manual en MVP; condición automática queda posterior. |
| TAR-0011 | Gestionar la reparación o el cambio de una garantía autorizada después de 90 días | `ACTIVA_REAL_GUARDADA` | Alta manual en MVP; evento automático queda posterior. |
| TAR-0018 | Montar o actualizar exhibiciones conforme al planograma y la zonificación | `ACTIVA_REAL_GUARDADA` | Alta manual en MVP; evento automático queda posterior. |
| TAR-0026 | Programar y realizar el pago de servicios básicos | `ACTIVA_REAL_GUARDADA` | Programada; SGOL coordina la tarea, no ejecuta pagos. |
| TAR-0092 | Coordinar la recepción de mercancía contra la nota de envío | `ACTIVA_REAL_GUARDADA` | Alta manual en MVP; evento automático queda posterior. |
| TAR-0093 | Documentar y notificar incidencia de recepción de mercancía | `ACTIVA_REAL_GUARDADA` | Alta manual en MVP; condición automática queda posterior y la notificación es interna. |

### 6.2 Definiciones opcionales, sin compromiso de versión

Se revisaron individualmente las 186 definiciones restantes que FTE-012 conserva como canónicas, pero ninguna tiene evidencia aprobada suficiente para afirmar que debe entrar al MVP o a una versión comprometida:

- `TAR-0001` a `TAR-0175`, excepto `TAR-0005`, `TAR-0007`, `TAR-0008`, `TAR-0011`, `TAR-0018`, `TAR-0026`, `TAR-0092` y `TAR-0093`: **Opcional**. Son 167 definiciones con estado documental `BLOQUEADA_HASTA_ADAPTADOR`; el nombre o el adaptador legacy no prueba necesidad.
- `TAR-0176` a `TAR-0194`: **Opcional**. Son 19 definiciones con estado documental `DIFERIDA_PREPROD`; ese estado legacy tampoco prueba prioridad futura.

Cada ID del intervalo fue revisado bajo la misma regla conservadora. Su eventual promoción exige, individualmente: responsable actual, frecuencia o disparador real, resultado verificable, razón para gestionarla en SGOL y confirmación de que no invade nómina, incentivos o integraciones excluidas.

Quedan especialmente restringidas, sin decisión silenciosa:

- `TAR-0103` y `TAR-0107`: sólo podrían coordinar trabajo documental interno; SGOL no puede preparar, autorizar ni enviar movimientos de nómina.
- `TAR-0122`: no puede calcular o liquidar incentivos derivados de tareas.
- Tareas que mencionan Cash Planner, S&C, tienda en línea u otro sistema: su nombre no autoriza integración; cualquier coordinación futura tendría que funcionar sin integración externa o requerir un cambio de alcance aprobado.

### 6.3 Definiciones fuera de alcance

| IDs | Clasificación | Fundamento |
|---|---|---|
| TAR-0195 a TAR-0202 | Fuera de alcance | Excluidas del catálogo funcional futuro y de la migración operativa por DEC-047. |
| T221 | Fuera de alcance | Elemento legacy adicional excluido por DEC-047; no forma parte del intervalo TAR-0001 a TAR-0202. |

### 6.4 Control de cobertura de las 202 definiciones

| Grupo | Cantidad |
|---|---:|
| MVP aprobado | 8 |
| Opcional | 186 |
| Posterior | 0 |
| Fuera de alcance | 8 |
| Total TAR-0001 a TAR-0202 | 202 |

`T221` se registra aparte porque DEC-047 lo excluye, pero no pertenece a la secuencia `TAR-####` de 202 definiciones.

## 7. Dependencias del MVP

| Dependencia | Requisito de entrada | Capacidades afectadas | Consecuencia si falta |
|---|---|---|---|
| DEP-001 — Catálogo piloto aprobado | Confirmar las ocho tareas o sustituirlas por una lista explícita. | CAP-012, CAP-014, CAP-021, CAP-029, CAP-032 | No puede cerrarse F04 ni especificarse F05. |
| DEP-002 — Personas y autoridad | Personas vigentes, cuenta individual, rol único y disponibilidad. | CAP-001 a CAP-007, CAP-020 a CAP-023 | No existe asignación ni supervisión válida. |
| DEP-003 — Calendario y semana | Calendario de `LOR-001` y semana ISO administrados por Dirección. | CAP-010, CAP-011, CAP-014, CAP-015, CAP-024 | No puede generarse o publicar un plan coherente. |
| DEP-004 — Política por tarea | Para cada tarea MVP: rol requerido, activación, evidencia y validación. | CAP-012, CAP-014, CAP-021, CAP-029, CAP-032 | La tarea no queda especificable ni comprobable. |
| DEP-005 — Integridad transversal | Identidad única, auditoría, versiones e idempotencia funcional. | CAP-018, CAP-019, CAP-030, CAP-045 a CAP-047 | Se pierde trazabilidad o aparecen duplicados. |
| DEP-006 — Decisiones F05 | Flujos, validaciones, datos, estados y criterios de aceptación del alcance aprobado. | Todo el MVP | La aprobación F04 habilita, pero no sustituye, la especificación F05. |

## 8. Criterios verificables de éxito del MVP

Estos criterios definen éxito funcional y no presuponen tecnología:

| ID | Criterio |
|---|---|
| EXI-001 | Las ocho tareas aprobadas —o la lista que las sustituya— tienen definición vigente y política explícita; ninguna otra definición puede generar trabajo. |
| EXI-002 | En una prueba de una semana de `LOR-001`, cada obligación manual o recurrente esperada se crea una sola vez y pertenece al plan correcto. |
| EXI-003 | Toda asignación automática de la prueba puede explicarse por elegibilidad, carga y desempates aprobados; toda corrección conserva motivo e historial. |
| EXI-004 | El responsable puede consultar, ejecutar y concluir una tarea; si falta evidencia obligatoria, la conclusión cumplida queda bloqueada. |
| EXI-005 | Toda tarea configurada para validación recibe una decisión separada por una autoridad válida y no admite autovalidación fuera de Dirección. |
| EXI-006 | Las tareas vencidas permanecen activas y visibles; el vencimiento no fabrica cancelación, excepción ni resultado de validación. |
| EXI-007 | Responsables, superiores y Dirección sólo consultan el alcance permitido, incluidas evidencias e historial. |
| EXI-008 | Cambios de configuración, asignación, ejecución, evidencia y validación quedan atribuidos y reconstruibles; ningún usuario elimina auditoría o versiones. |
| EXI-009 | Los indicadores del MVP se limitan a pendientes, concluidas, validadas, incumplidas y carga por persona; no muestran incentivos ni movimientos de nómina. |
| EXI-010 | No existen dependencias indispensables de mensajería, datos o disparadores externos para completar la prueba. |
| EXI-011 | No queda ningún defecto bloqueante ni criterio crítico sin evidencia al emitir el dictamen de aceptación de Fase 09. |

No se fijan todavía metas de adopción, tiempo ahorrado o productividad: las fuentes no aportan línea base ni umbral aprobado. Esos objetivos sólo podrán añadirse con evidencia y decisión del responsable.

## 9. Fuera de alcance explícito

- CAP-040: cálculo o liquidación de incentivos monetarios derivados de tareas.
- CAP-041: preparación, autorización o envío de movimientos de nómina.
- CAP-050: integraciones externas, mensajería externa y proyectos `AUT-*` por herencia.
- TAR-0195 a TAR-0202 y T221.
- Migración histórica, reconciliación de identidades ambiguas, coexistencia y retiro legacy, salvo aprobación posterior de CAP-048/CAP-049.
- Motor KPI configurable, KPI monetarios y T182 como KPI.
- Selección de tecnología, arquitectura, protocolos, almacenamiento, RPO/RTO, API, despliegue y código.
- Funciones no incluidas expresamente, aunque existan como macro, tabla, botón, procedimiento o automatización legacy.

## 10. Decisiones de producto aprobadas

| ID | Estado | Decisión aprobada | Efecto |
|---|---|---|---|
| F04-JP-001 | APROBADA | Aplicar el ciclo descrito en secciones 3 a 5, dejando excepciones y cierre formal para posterior. | Fija las 35 capacidades MVP. |
| F04-JP-002 | APROBADA | Incluir las ocho tareas de 6.1 como catálogo concreto del MVP. | Resuelve PNB-001 para el MVP. |
| F04-JP-003 | APROBADA | Incluir sólo creación manual y recurrencia; las tareas de evento, condición o proceso se crean manualmente en el MVP. | Mantiene CAP-016/CAP-017 posteriores sin bloquear las ocho tareas. |
| F04-JP-004 | APROBADA | No incluir CAP-048/CAP-049 en el MVP ni comprometerlas todavía. | PNB-007 a PNB-009 dejan de afectar el primer corte. |
| F04-JP-005 | APROBADA | Aplicar EXI-001 a EXI-011 como criterios funcionales; no fijar metas de productividad sin línea base. | Habilita la especificación objetiva en F05. |

## 11. Cierre de Fase 04

La Fase 04 quedó completada y aprobada el 2026-08-27:

1. F04-JP-001 a F04-JP-005 fueron aprobadas expresamente;
2. el catálogo MVP quedó fijado en ocho tareas y 35 capacidades;
3. los tres entregables F04 fueron aprobados;
4. las versiones finales se incorporaron a `Fuentes` y sus copias fueron verificadas por SHA-256.

La Fase 05 está habilitada y debe especificar únicamente el MVP aprobado en este documento.
