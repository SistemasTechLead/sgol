# F05 — Especificación funcional del MVP de SGOL

## 1. Control del entregable

| Campo | Valor |
|---|---|
| Fase | 05 — Especificación funcional del MVP |
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Aprobación literal del responsable | `Apruebo los cinco entregables` |
| Interpretación controlada | La aprobación del contenido completo incluye F05-JP-009, expuesta expresamente en este documento y en la matriz de permisos |
| Alcance rector | `F04_ALCANCE_Y_MVP.md`, 35 capacidades y 8 tareas MVP |
| Exclusiones | CAP-008, CAP-013, CAP-016, CAP-017, CAP-026, CAP-034 a CAP-038, CAP-040, CAP-041, CAP-048 a CAP-050 y toda tarea no incluida expresamente |
| Documentos relacionados | `F05_MATRIZ_DE_ROLES_Y_PERMISOS.md`; `F05_MODELO_DE_ESTADOS.md`; `F05_MATRIZ_DE_TRAZABILIDAD.md`; `F05_CRITERIOS_DE_ACEPTACION.md` |

## 2. Convenciones

- `HU-###`: historia asociada a una capacidad MVP.
- `RN-###`: regla funcional transversal.
- `CA-###`: criterio de aceptación objetivo.
- `CP-###-P/N`: prueba positiva o negativa de la historia.
- `F05-JP-###`: decisión funcional aprobada por el responsable durante F05.
- `VENCIDA` es una condición calculada, no un estado de ejecución.
- “Superior” significa un nivel jerárquico mayor dentro de `LOR-001`; “superior inmediato” aplica `DIRECCION > ADMINISTRACION > SUBCOORDINACION > PISO_VENTAS`.
- Los mecanismos físicos de autenticación, almacenamiento, archivos, concurrencia, respaldo y recuperación pertenecen a F06.

## 3. Decisiones funcionales F05

| ID | Estado | Decisión | Razón controlada |
|---|---|---|---|
| F05-JP-001 | APROBADA | TAR-0005: ejecuta `SUBCOORDINACION`; recurrencia laborable 12:00 y 17:00; evidencia digital; valida `ADMINISTRACION`. | Hace medibles las revisiones sin inventar integración ni efecto comercial automático. |
| F05-JP-002 | APROBADA | TAR-0007: ejecuta `PISO_VENTAS`; alta manual referida a un separado; vencimiento ordinario a 24 horas; valida `SUBCOORDINACION`. | El MVP documenta plazos distintos, pero no autoriza ampliaciones. |
| F05-JP-003 | APROBADA | TAR-0008: ejecuta `SUBCOORDINACION`; alta manual; objetivo de 30 minutos; valida `ADMINISTRACION`. | La decisión es humana y fundada; SGOL no altera sistemas de venta. |
| F05-JP-004 | APROBADA POR AUTORIZACIÓN DEL RESPONSABLE | TAR-0011: ejecuta `SUBCOORDINACION`; sólo casos previamente autorizados; objetivo de siete días hábiles; valida `ADMINISTRACION`. | Separa autorización de garantía, fuera de esta tarea, de su gestión posterior. |
| F05-JP-005 | APROBADA POR AUTORIZACIÓN DEL RESPONSABLE | TAR-0018: ejecuta `PISO_VENTAS`; alta manual; checklist, fotografía y planograma/lista; valida `SUBCOORDINACION`; sin SLA. | Convierte criterios textuales en controles objetivos sin inventar plazo. |
| F05-JP-006 | APROBADA POR AUTORIZACIÓN DEL RESPONSABLE | TAR-0026: ejecuta `ADMINISTRACION`; recurrencia por servicio y vencimiento; genera tres días hábiles antes; valida `DIRECCION`. | SGOL coordina y acredita, pero no realiza el pago. |
| F05-JP-007 | APROBADA POR AUTORIZACIÓN DEL RESPONSABLE | TAR-0092: ejecuta `SUBCOORDINACION`; alta manual por recepción; valida `ADMINISTRACION`; sin SLA. | El valor de 45 minutos era KPI candidato y no se convierte en obligación. |
| F05-JP-008 | APROBADA POR AUTORIZACIÓN DEL RESPONSABLE | TAR-0093: ejecuta `SUBCOORDINACION`; alta manual vinculada a TAR-0092; evidencia y aviso interno; valida `ADMINISTRACION`; sin SLA. | Mantiene el caso trazable sin integración o mensajería externa. |
| F05-JP-009 | APROBADA MEDIANTE APROBACIÓN DE LOS CINCO ENTREGABLES | Sólo `DIRECCION` administra personas, cuentas, roles, calendario, definiciones y políticas. Dirección, Administración y Subcoordinación crean obligaciones manuales y publican para su nivel y niveles inferiores; Piso sólo ejecuta las propias. | Completa permisos del MVP sin conceder autoridad por puesto ni permitir elevación de privilegios. |

## 4. Catálogo operativo de las ocho tareas

| Tarea | Activación y unicidad | Rol requerido | Datos de entrada | Evidencia obligatoria | Criterio de resultado | Validación |
|---|---|---|---|---|---|---|
| TAR-0005 | Dos recurrencias por día laborable, 12:00 y 17:00; clave tarea+sucursal+fecha+ventana. | `SUBCOORDINACION` | meta esperada, venta real, fuente | registro digital con cálculo y acción o conformidad | si avance <90 %, acción, responsable e inicio; en otro caso `REVISION_CONFORME_SIN_ACCION` | `ADMINISTRACION` |
| TAR-0007 | Alta manual por separado y vencimiento efectivo; ordinario 24 h. | `PISO_VENTAS` | separado, mercancía, inicio, vencimiento, referencia | liberación, mercancía, fecha/hora y retorno a exhibición | vencimiento confirmado, sin ampliación no documentada y mercancía disponible | `SUBCOORDINACION` |
| TAR-0008 | Alta manual; una controversia activa por operación. | `SUBCOORDINACION` | operación, detección, al menos dos reclamantes, evidencia | expediente, secuencia, decisión, fundamento y aviso interno | decisión basada en evidencia y comunicada privadamente; objetivo 30 min | `ADMINISTRACION` |
| TAR-0011 | Alta manual por expediente y autorización previa; una por autorización. | `SUBCOORDINACION` | expediente, autorización, producto, solución autorizada, fecha | evaluación, autorización, reparación/cambio, comprobantes y entrega | solución coincide con autorización; documentos conciliados; cliente notificado; objetivo 7 días hábiles | `ADMINISTRACION` |
| TAR-0018 | Alta manual por evento de exhibición; una por evento+planograma. | `PISO_VENTAS` | evento, zona, planograma/lista vigente | checklist completo, fotografía final y planograma/lista | todos los ítems del checklist conformes | `SUBCOORDINACION` |
| TAR-0026 | Recurrencia por servicio+vencimiento; genera 3 días hábiles antes. | `ADMINISTRACION` | servicio, vencimiento real, referencia del recibo | FORM-ADM-02 y comprobante localizable | pago acreditado a más tardar en vencimiento ajustado al hábil anterior | `DIRECCION` |
| TAR-0092 | Alta manual por `ID_Recepcion`; una activa por recepción. | `SUBCOORDINACION` | recepción, proveedor, nota, inicio, mercancía | nota/remisión/factura y F-ENT-001; foto si hay diferencia o daño | cantidad, modelo y estado coinciden o toda diferencia queda documentada | `ADMINISTRACION` |
| TAR-0093 | Alta manual por recepción+incidente; vinculada a TAR-0092. | `SUBCOORDINACION` | recepción, tipo, descripción y momento | fotografía, anotación en F-ENT-001 y constancia de aviso interno | incidencia registrada antes de la captura final y con soporte reclamable | `ADMINISTRACION` |

### 4.1 Checklist aprobado para TAR-0018

Todos los ítems son obligatorios: producto correcto; zona y familia correctas; formación estable; etiquetas visibles; alineación consistente; ocupación sin huecos o sobrecarga injustificados; limpieza; integridad; señalización; correspondencia con el planograma/lista vigente. Un `NO` impide concluir como cumplida.

### 4.2 Tratamientos deliberadamente no heredados

- TAR-0007 no se activa automáticamente desde un expediente.
- TAR-0008, TAR-0011, TAR-0018, TAR-0092 y TAR-0093 no reciben disparos automáticos por eventos o condiciones.
- TAR-0026 no se integra con banco, Cash Planner, proveedor o nómina.
- TAR-0092 no adopta 45 minutos como SLA.
- TAR-0093 notifica sólo dentro de SGOL.

### 4.3 Interpretación objetiva de la validación

- `CUMPLIDA`: la evidencia completa demuestra todos los criterios de resultado y, cuando existe un objetivo temporal aprobado, éste se respetó.
- `INCOMPLETA`: la ejecución concluyó estructuralmente, pero el resultado acreditado sólo satisface parte del criterio funcional.
- `NO_CUMPLIDA`: la evidencia demuestra que no se alcanzó un criterio indispensable o un objetivo temporal aprobado.
- El sistema nunca asigna estos resultados automáticamente: el validador elige uno y registra fundamento. La política sólo permite comprobar después si la decisión es coherente con los datos.
- TAR-0008, TAR-0011 y TAR-0026 tienen objetivo temporal. TAR-0005, TAR-0007, TAR-0018, TAR-0092 y TAR-0093 no reciben un SLA adicional en el MVP.

## 5. Reglas transversales

| ID | Regla |
|---|---|
| RN-001 | Sólo existe la sucursal `LOR-001` / Loretta; `TODAS` es alcance, no sucursal. |
| RN-002 | Cada persona usa código estable, vigencia laboral y una cuenta individual. |
| RN-003 | Cada usuario tiene un solo rol activo en `LOR-001`; puesto y turno no conceden permisos. |
| RN-004 | La disponibilidad es binaria por persona y día. |
| RN-005 | La semana es ISO lunes–domingo y toda fecha operativa usa `America/Mexico_City`. |
| RN-006 | Sólo Dirección modifica datos laborales, cuentas, roles, calendario, definiciones y políticas. |
| RN-007 | Sólo las ocho definiciones MVP activas pueden generar obligaciones. |
| RN-008 | Las definiciones y políticas se versionan; cambios posteriores no alteran obligaciones ya creadas. |
| RN-009 | Una recurrencia en día no laborable se omite; no se adelanta ni traslada automáticamente. |
| RN-010 | Máximo una obligación por regla, alcance, período y hecho originador; un reintento recupera la existente. |
| RN-011 | Elegibilidad exige persona activa, `LOR-001`, rol exacto y disponibilidad positiva; turno sólo si la definición lo restringe. |
| RN-012 | Asignación: menor carga activa; empate por mayor tiempo sin asignación automática; después código estable. |
| RN-013 | Un superior puede corregir una asignación de un nivel inferior con motivo; nunca se borra el responsable anterior. |
| RN-014 | Existe un solo plan por `LOR-001` y semana; las publicaciones tardías crean una nueva versión del mismo plan. |
| RN-015 | Dirección publica todo; Administración y Subcoordinación publican su nivel y niveles inferiores; Piso no publica. |
| RN-016 | Las tareas MVP no exigen inicio explícito ni medición de duración. |
| RN-017 | Toda evidencia configurada es obligatoria, incluidas las condiciones expresas de fotografía de TAR-0092. |
| RN-018 | El responsable sustituye evidencia sólo antes de concluir; después, sólo un superior con motivo. Todas las versiones permanecen. |
| RN-019 | Falta de evidencia impide concluir; el vencimiento no cambia el estado de ejecución. |
| RN-020 | Toda tarea MVP requiere validación separada por el superior inmediato; un nivel posterior actúa sólo por escalamiento con motivo. |
| RN-021 | Nadie se autovalida salvo Dirección. Una validación basta. |
| RN-022 | Una validación se resuelve como `CUMPLIDA`, `INCOMPLETA` o `NO_CUMPLIDA` y no sobrescribe la ejecución. |
| RN-023 | Sustituir una validación exige autoridad y motivo; la anterior queda histórica. |
| RN-024 | Validación desfavorable no reabre, cancela ni crea otra tarea automáticamente. |
| RN-025 | El responsable consulta lo propio; superiores consultan lo propio y niveles inferiores; Dirección consulta todo. |
| RN-026 | Indicadores: pendientes, concluidas, validadas, incumplidas y carga activa por persona. No hay KPI monetario. |
| RN-027 | Ningún usuario elimina auditoría, evidencia, versiones, asignaciones o validaciones. |
| RN-028 | Todo cambio de configuración, asignación, ejecución, evidencia, validación y publicación registra actor, fecha, alcance y antes/después aplicable. |
| RN-029 | No hay integración, mensajería externa, migración histórica, incentivo o movimiento de nómina en el MVP. |
| RN-030 | Recuperar o compensar conserva identidades e historia; mecanismos físicos se deciden en F06. |

## 6. Historias por capacidad MVP

Cada fila contiene todos los campos obligatorios. Los pasos y resultados completos de `CA` y `CP` están en `F05_CRITERIOS_DE_ACEPTACION.md`.

| HU / CAP | Historia, actor y permiso | Precondiciones y flujo principal | Alternativas y errores | Reglas, datos, validaciones y estados | CA / pruebas | Dependencias y trazabilidad |
|---|---|---|---|---|---|---|
| HU-001 / CAP-001 | Como Dirección quiero mantener identidad y vigencia laboral. Permiso `PER-PERSONA-ADMIN`. | `LOR-001` existe; crear código único o cambiar vigencia conservando historia. | Corrección crea versión; duplicado o código vacío se rechaza. | RN-002, RN-006, RN-027; código, nombre, vigencia; `ACTIVA/INACTIVA`. | CA-001; CP-001-P/N. | FTE-005/006; DEC-048,052. |
| HU-002 / CAP-002 | Como Dirección quiero registrar puesto y turno sin conceder autoridad. `PER-PERSONA-ADMIN`. | Persona existente; capturar valores laborales. | Se corrigen con vigencia; intento de derivar rol se ignora/rechaza. | RN-003, RN-006; puesto y turno; no cambian permisos. | CA-002; CP-002-P/N. | CAP-001; DEC-032,052,057. |
| HU-003 / CAP-003 | Como Dirección quiero registrar disponibilidad diaria. `PER-DISPONIBILIDAD-ADMIN`. | Persona activa y fecha local; guardar disponible/no disponible. | Corrección sustituye valor; valores parciales se rechazan. | RN-004, RN-005, RN-006; persona, fecha, valor binario. | CA-003; CP-003-P/N. | CAP-001; DEC-054,064. |
| HU-004 / CAP-004 | Como superior quiero conocer carga activa para asignar justamente. `PER-CARGA-VER`. | Existen obligaciones; contar las `PENDIENTE` asignadas por persona. | Sin tareas equivale a cero; concluidas no cuentan. | RN-012, RN-026; persona, conteo, instante. Estado derivado. | CA-004; CP-004-P/N. | CAP-019,022; DEC-035. |
| HU-005 / CAP-005 | Como Dirección quiero mantener el alcance de Loretta. `PER-SUCURSAL-ADMIN`. | Catálogo inicial; reconocer sólo `LOR-001`. | `TODAS` se usa al consultar, nunca como registro. Otro código se rechaza en MVP. | RN-001; código y nombre; `ACTIVA`. | CA-005; CP-005-P/N. | DEC-031. |
| HU-006 / CAP-006 | Como Dirección quiero administrar cuentas individuales. `PER-USUARIO-ADMIN`. | Persona existente; crear, activar o desactivar una cuenta no compartida. | Reactivación conserva historia; duplicada/compartida se rechaza. | RN-002, RN-006, RN-027; usuario, persona, vigencia; `ACTIVA/INACTIVA`. | CA-006; CP-006-P/N. | CAP-001,005; DEC-043,050. |
| HU-007 / CAP-007 | Como Dirección quiero asignar un rol único y aplicar jerarquía. `PER-ROL-ADMIN`. | Cuenta activa; asignar un rol canónico en `LOR-001`. | Cambio sustituye rol; segundo rol simultáneo o autoridad por puesto se rechaza. | RN-003, RN-006, RN-025; rol, vigencia, alcance; `ACTIVO/SUSTITUIDO`. | CA-007; CP-007-P/N. | CAP-005,006; DEC-001,004,032,044,049. |
| HU-008 / CAP-009 | Como Dirección quiero versionar configuración. `PER-CONFIG-ADMIN`. | Configuración válida; publicar nueva versión con motivo. | Borrador corregible; solapamiento o usuario no autorizado se rechaza. | RN-006, RN-008, RN-027, RN-028; versión, vigencia, actor; `BORRADOR/VIGENTE/SUSTITUIDA`. | CA-008; CP-008-P/N. | CAP-007,045; DEC-053,059. |
| HU-009 / CAP-010 | Como Dirección quiero administrar semana, zona y calendario. `PER-CALENDARIO-ADMIN`. | `LOR-001`; registrar laborables, festivos o cierres extraordinarios. | Cambio futuro crea versión; zona distinta o fecha inválida se rechaza. | RN-005, RN-006, RN-009; fecha, tipo de día, zona. | CA-009; CP-009-P/N. | CAP-005,009; DEC-015,058,059,065. |
| HU-010 / CAP-011 | Como planificador quiero identificar el período semanal correcto. `PER-PLAN-VER/PUBLICAR`. | Semana ISO calculable; obtener período único de `LOR-001`. | Semana transcurrida sigue consultable; cierre/reapertura formal no existe en MVP. | RN-005, RN-014; sucursal, año/semana, inicio/fin; `VIGENTE/TRANSCURRIDA` calculado. | CA-010; CP-010-P/N. | CAP-010,024; DEC-010,015; límite F04. |
| HU-011 / CAP-012 | Como Dirección quiero mantener sólo las ocho definiciones aprobadas. `PER-DEFINICION-ADMIN`. | Catálogo F04; crear versión o desactivar nuevas generaciones. | Historial permanece; novena TAR o ID excluido se rechaza. | RN-007, RN-008; TAR, versión, rol, políticas; definición `ACTIVA/INACTIVA_NUEVAS`. | CA-011; CP-011-P/N. | CAP-009; F04-JP-002; DEC-046,047,053. |
| HU-012 / CAP-014 | Como Dirección quiero configurar altas manuales y recurrencias MVP. `PER-ACTIVACION-ADMIN`. | Definición activa; configurar mecanismo permitido. | Regla se versiona; evento/condición/integración se rechaza. | RN-007 a RN-010; mecanismo, clave, calendario; `VIGENTE/SUSTITUIDA`. | CA-012; CP-012-P/N. | CAP-009,010,012; F04-JP-003. |
| HU-013 / CAP-015 | Como SGOL quiero generar recurrencias sin duplicar ni mover días inhábiles. Actor sistema. | Regla recurrente vigente y calendario; evaluar ocurrencia. | Día inhábil se omite; reintento recupera; regla inválida registra error. | RN-005, RN-009, RN-010; regla, fecha, clave; resultado `GENERADA/OMITIDA/RECUPERADA/RECHAZADA`. | CA-013; CP-013-P/N. | CAP-010,014,018; DEC-063,065. |
| HU-014 / CAP-018 | Como creador autorizado quiero emitir una solicitud única de generación. `PER-OBLIGACION-CREAR`. | Definición activa, alcance y origen completos; solicitar. | Reintento devuelve misma solicitud; fuera de alcance se rechaza. | RN-007, RN-010, RN-028; regla, alcance, período, origen; `ACEPTADA/RECUPERADA/RECHAZADA`. | CA-014; CP-014-P/N. | CAP-014,015; DEC-063. |
| HU-015 / CAP-019 | Como SGOL quiero crear o recuperar una obligación única. Actor sistema. | Solicitud aceptada; crear identidad y vincularla al plan. | Clave existente se recupera; error no deja duplicado. | RN-010, RN-014; obligación, TAR, versión, origen, responsable; `PENDIENTE/CONCLUIDA`. | CA-015; CP-015-P/N. | CAP-018; DEC-063. |
| HU-016 / CAP-020 | Como SGOL quiero calcular candidatos elegibles. Actor sistema; superiores consultan explicación. | Obligación, rol y fecha; filtrar vigencia, sucursal, rol, disponibilidad y turno configurado. | Sin candidatos registra error asignable; puestos similares no cuentan. | RN-003, RN-004, RN-011; candidato y razones de inclusión/exclusión. | CA-016; CP-016-P/N. | CAP-001 a 007,019; DEC-034,057,064. |
| HU-017 / CAP-021 | Como Dirección quiero definir política de elegibilidad por tarea. `PER-POLITICA-ADMIN`. | TAR MVP; guardar rol exacto, disponibilidad y turno opcional. | Nueva versión sustituye; texto de puesto o tarea excluida se rechaza. | RN-006 a RN-008, RN-011; TAR, rol, turno, vigencia. | CA-017; CP-017-P/N. | CAP-009,012,020; F05-JP-001 a 008. |
| HU-018 / CAP-022 | Como SGOL quiero asignar determinísticamente al candidato adecuado. Actor sistema. | Lista elegible; ordenar por carga, espera y código; asignar primero. | Un candidato se elige directamente; sin candidato registra error. | RN-012, RN-028; carga, última asignación, código, explicación; asignación `VIGENTE`. | CA-018; CP-018-P/N. | CAP-004,020; DEC-033 a 035. |
| HU-019 / CAP-023 | Como superior quiero corregir una asignación inferior. `PER-ASIGNACION-CORREGIR`. | Obligación pendiente de nivel inferior y nuevo responsable elegible; indicar motivo. | Otro superior posterior puede corregir; par, inferior, motivo vacío o inelegible se rechaza. | RN-011 a RN-013, RN-027; antes/después, actor, motivo; anterior `SUSTITUIDA`. | CA-019; CP-019-P/N. | CAP-007,022,045; DEC-036. |
| HU-020 / CAP-024 | Como planificador quiero un plan único por semana. `PER-PLAN-VER`. | Período y obligaciones; crear/recuperar plan y agrupar sin separar por nivel. | Sin obligaciones conserva plan vacío; segundo plan se recupera/rechaza. | RN-005, RN-010, RN-014; sucursal, semana, versión, obligaciones; `BORRADOR/PUBLICADO`. | CA-020; CP-020-P/N. | CAP-010,011,019; DEC-010,015. |
| HU-021 / CAP-025 | Como superior quiero publicar el plan dentro de mi alcance. `PER-PLAN-PUBLICAR`. | Plan y autoridad; publicar obligaciones propias e inferiores. | Obligación tardía crea versión incremental; publicar a superior o Piso publicando se rechaza. | RN-014, RN-015, RN-025, RN-028; versión, alcance, actor; anterior `SUSTITUIDA`. | CA-021; CP-021-P/N. | CAP-007,024; DEC-009,011. |
| HU-022 / CAP-027 | Como responsable quiero concluir mi tarea con evidencia completa. `PER-TAREA-EJECUTAR`. | Obligación propia pendiente; aportar evidencia y solicitar conclusión. | Puede ejecutar anticipadamente; si falta evidencia queda pendiente. | RN-016 a RN-019; resultado, fecha, evidencia; `PENDIENTE→CONCLUIDA`; vencida es bandera. | CA-022; CP-022-P/N. | CAP-019,029 a 031; DEC-021,022,066. |
| HU-023 / CAP-028 | Como usuario quiero consultar trabajo e historial permitido. `PER-TAREA-VER`. | Sesión individual y alcance; listar/detallar tareas, procedencia, vencimiento e historia. | Filtros no alteran datos; acceso a par/superior se deniega. | RN-002, RN-025, RN-027; obligación, origen, eventos, evidencia. | CA-023; CP-023-P/N. | CAP-019,027,045; DEC-044,055,056. |
| HU-024 / CAP-029 | Como Dirección quiero configurar evidencia mínima por tarea. `PER-EVIDENCIA-CONFIG`. | TAR MVP; guardar tipos y condiciones de sección 4. | Nueva versión no cambia tareas existentes; evidencia opcional no configurada no bloquea. | RN-006, RN-008, RN-017; tipo, condición, vigencia. | CA-024; CP-024-P/N. | CAP-009,012; F05-JP-001 a 008; DEC-021. |
| HU-025 / CAP-030 | Como responsable o superior quiero sustituir evidencia sin borrar versiones. `PER-EVIDENCIA-APORTAR` y `PER-EVIDENCIA-SUSTITUIR`. | Evidencia existente; antes de conclusión responsable, después superior con motivo. | Sustitución posterior no cambia validación; usuario sin autoridad se rechaza. | RN-018, RN-023, RN-027; versión, archivo/registro, actor, motivo; `VIGENTE/SUSTITUIDA`. | CA-025; CP-025-P/N. | CAP-027,029,045; DEC-067,068. |
| HU-026 / CAP-031 | Como SGOL quiero bloquear cumplimiento sin evidencia completa. Actor sistema. | Política y versiones vigentes; evaluar cada requisito. | Requisito condicional sólo aplica si se cumple condición; faltante informa qué falta. | RN-017 a RN-019; lista de requisitos y resultado `COMPLETA/INCOMPLETA`. | CA-026; CP-026-P/N. | CAP-027,029,030; DEC-022. |
| HU-027 / CAP-032 | Como Dirección quiero configurar validación y autoridad por tarea. `PER-VALIDACION-CONFIG`. | TAR MVP; todas requieren validación según F05-JP. | Versión futura sustituye; validador por puesto o par se rechaza. | RN-006, RN-008, RN-020, RN-021; TAR, rol ejecutor, superior. | CA-027; CP-027-P/N. | CAP-007,009,012; F05-JP-001 a 008. |
| HU-028 / CAP-033 | Como superior quiero validar una ejecución separadamente. `PER-VALIDACION-EMITIR`. | Tarea concluida y autoridad; revisar evidencia y emitir resultado. | Escalamiento/sustitución exige motivo; autovalidación no Dirección o segundo resultado vigente se rechaza. | RN-020 a RN-024; resultado, fundamento, evidencia vigente; decisión `VIGENTE/SUSTITUIDA`. | CA-028; CP-028-P/N. | CAP-007,030 a 032; DEC-003,004,006,024-026. |
| HU-029 / CAP-039 | Como superior quiero indicadores operativos básicos. `PER-INDICADOR-VER`. | Datos dentro del alcance; contar por definiciones de sección 8. | Sin datos muestra cero y denominador; identidades ambiguas no existen en MVP. | RN-004, RN-025, RN-026; período, alcance, conteos; valores derivados. | CA-029; CP-029-P/N. | CAP-019,027,033; DEC-038. |
| HU-030 / CAP-042 | Como responsable quiero una bandeja interna. `PER-BANDEJA-PROPIA`. | Cuenta activa; mostrar tareas propias, programadas, vencidas, evidencia faltante y avisos. | Sin elementos muestra vacío; no envía mensajes externos. | RN-019, RN-025, RN-029; obligación, fechas, alertas; lectura no cambia ejecución. | CA-030; CP-030-P/N. | CAP-019,027 a 030; DEC-041,066,069. |
| HU-031 / CAP-043 | Como superior quiero supervisar niveles inferiores. `PER-SUPERVISION-VER`. | Rol superior; consultar trabajo, evidencias, validaciones e indicadores inferiores. | Escalamiento de validación con motivo; pares/superiores se ocultan. | RN-020, RN-025, RN-026; filtros de nivel/persona/semana. | CA-031; CP-031-P/N. | CAP-007,033,039; DEC-001,004,044,069. |
| HU-032 / CAP-044 | Como Dirección quiero una vista completa de operación. `PER-DIRECCION-VER`. | Rol Dirección; consultar todo `LOR-001` e indicadores básicos. | Filtros permitidos; KPI monetario, nómina o integración no aparece. | RN-025, RN-026, RN-029; semana, rol, persona, conteos. | CA-032; CP-032-P/N. | CAP-007,039,043; DEC-038,040,044. |
| HU-033 / CAP-045 | Como usuario autorizado quiero reconstruir hechos sin poder borrarlos. `PER-AUDITORIA-VER`. | Operación auditable; registrar actor, fecha, alcance y cambio; consultar jerárquicamente. | Archivado conserva vínculo; eliminar o consultar fuera de alcance se rechaza. | RN-002, RN-025, RN-027, RN-028; evento, antes/después, motivo. | CA-033; CP-033-P/N. | Transversal; DEC-043 a 045,068,069. |
| HU-034 / CAP-046 | Como SGOL quiero evitar duplicados y registrar errores funcionales. Actor sistema; autorizados consultan. | Operación con clave idempotente; procesar una vez. | Reintento recupera; conflicto de datos rechaza y audita sin duplicar. | RN-010, RN-027, RN-028; clave, resultado, error; `ACEPTADA/RECUPERADA/RECHAZADA`. | CA-034; CP-034-P/N. | CAP-018,019,045; DEC-063. |
| HU-035 / CAP-047 | Como Dirección quiero que una recuperación funcional conserve historia. `PER-CONTINUIDAD-VER`. | Conjunto antes/después de recuperación; reconciliar identidades y vínculos. | Diferencia produce reporte y no se oculta; ningún usuario borra para “corregir”. | RN-027, RN-030; IDs, versiones, conteos y diferencias; hechos conservan estado. | CA-035; CP-035-P/N. | CAP-045; DEC-013,045,068; mecanismo F06. |

## 7. Flujos principales del MVP

1. **Gobierno:** Dirección mantiene persona → cuenta → rol → disponibilidad → calendario → definición y políticas versionadas.
2. **Generación:** alta manual o recurrencia → solicitud idempotente → obligación única → elegibilidad → asignación → plan semanal.
3. **Publicación:** actor autorizado publica su alcance; nuevas obligaciones actualizan el mismo plan con versión adicional.
4. **Ejecución:** responsable consulta → aporta evidencia → sistema comprueba integridad → concluye.
5. **Validación:** superior inmediato revisa → emite resultado separado; escalamiento o sustitución conserva motivo e historia.
6. **Control:** bandejas y vistas jerárquicas muestran trabajo e indicadores; auditoría permite reconstruir cada cambio.

## 8. Definición de indicadores

| Indicador | Definición objetiva |
|---|---|
| Pendientes | Obligaciones con estado `PENDIENTE` al instante de corte. |
| Concluidas | Obligaciones cuya ejecución tiene estado `CONCLUIDA` en el período consultado. |
| Validadas | Ejecuciones con una decisión de validación vigente, cualquiera que sea su resultado. |
| Incumplidas | Validaciones vigentes con resultado `NO_CUMPLIDA`; no equivale a vencidas. |
| Carga por persona | Número de obligaciones `PENDIENTE` actualmente asignadas a la persona. |

Cada resultado muestra período, alcance, filtros y conteo base. No se calcula productividad, incentivo, sanción ni nómina.

## 9. Errores funcionales normalizados

`ACCESO_DENEGADO`, `ALCANCE_INVALIDO`, `CUENTA_NO_INDIVIDUAL`, `ROL_MULTIPLE`, `CALENDARIO_INVALIDO`, `DEFINICION_NO_MVP`, `CONFIGURACION_SOLAPADA`, `ORIGEN_INCOMPLETO`, `OBLIGACION_DUPLICADA_RECUPERADA`, `SIN_CANDIDATO_ELEGIBLE`, `ASIGNACION_NO_AUTORIZADA`, `PLAN_FUERA_DE_ALCANCE`, `EVIDENCIA_FALTANTE`, `EVIDENCIA_SUSTITUCION_NO_AUTORIZADA`, `CONCLUSION_NO_PERMITIDA`, `VALIDACION_NO_AUTORIZADA`, `AUTOVALIDACION_NO_PERMITIDA`, `MOTIVO_OBLIGATORIO`, `AUDITORIA_NO_ELIMINABLE`, `ERROR_FUNCIONAL_REGISTRADO`.

## 10. Condición de cierre funcional

Las 35 capacidades tienen una historia, un criterio y pruebas positiva/negativa. Los cinco entregables fueron aprobados conjuntamente el 2026-08-27, incorporados a `Fuentes` y comprobados por igualdad SHA-256. Con esa comprobación, F05 queda cerrada y F06 está habilitada.
