# F02 — Catálogo funcional normalizado de SGOL

## 1. Control del entregable

| Campo | Valor |
|---|---|
| Fase | 02 — Normalización del catálogo funcional |
| Fecha | 2026-08-26 |
| Estado | Completada con preguntas y contradicciones visibles; aprobada e incorporada a las fuentes del proyecto |
| Fecha de aprobación e incorporación | 2026-08-26 |
| Entradas de control | `F00_CONTROL_DEL_PROYECTO.md`; `F01_INVENTARIO_DE_FUENTES.md`; FTE-001 a FTE-044 |
| Límites | No define MVP, prioridades, arquitectura, tecnología ni código de producción |
| Cobertura | 50 capacidades normalizadas de F1–F19; F0 y F20 se usan como inventario y consolidación transversal |

## 2. Método y convención

Una **capacidad** es un resultado funcional coherente que SGOL debe poder producir para un actor humano o técnico. Las funciones puras, comandos, handlers, jobs, eventos y vistas documentados son operaciones de soporte y se agrupan bajo la capacidad cuyo resultado realizan. Esta clasificación evita convertir las 283 entradas de lógica de FTE-044, hoja `06_Logica`, en 283 capacidades artificialmente fragmentadas.

La presencia de una capacidad en este catálogo significa únicamente que está documentada en las fuentes. **No demuestra uso real, necesidad vigente, prioridad ni inclusión futura.** En particular, una macro, botón, procedimiento, tabla o función del respaldo legacy no se convierte en requisito por existir. Fase 03 deberá distinguir capacidades confirmadas, contradictorias y no comprobadas; Fase 04 decidirá alcance y MVP.

Etiquetas de certeza:

- **[E] Explícito:** consta directamente en la fuente citada.
- **[I] Inferencia documental:** síntesis necesaria para agrupar hechos compatibles; no añade comportamiento.
- **[NE] No especificado:** la fuente no determina el dato.

Reglas de lectura:

- “Fuente y ubicación” cita el bloque Markdown y la hoja Excel de apoyo. FTE-043/FTE-044 sólo corroboran la consolidación.
- Cuando una ficha agrupa varias operaciones, sus nombres aparecen en **Resultados**.
- Las preguntas se remiten a `F02_PREGUNTAS_Y_CONTRADICCIONES.md` mediante IDs estables.
- “No especificado” no equivale a “no aplica”.

## 3. Matriz de cobertura

| Dominio documental | Capacidades | Fuente Markdown / Excel |
|---|---|---|
| F1 Personal y capacidad | CAP-001 a CAP-004 | FTE-005 / FTE-006 |
| F2 Organización y seguridad | CAP-005 a CAP-008 | FTE-007 / FTE-008 |
| F3 Configuración y calendario | CAP-009 a CAP-011 | FTE-009 / FTE-010 |
| F4 Catálogo operativo | CAP-012 a CAP-013 | FTE-011 / FTE-012 |
| F5 Activación | CAP-014 a CAP-018 | FTE-013 / FTE-014 |
| F6 Obligaciones | CAP-019 | FTE-015 / FTE-016 |
| F7 Elegibilidad | CAP-020 a CAP-021 | FTE-017 / FTE-018 |
| F8 Asignación | CAP-022 a CAP-023 | FTE-019 / FTE-020 |
| F9 Planificación | CAP-024 a CAP-025 | FTE-021 / FTE-022 |
| F10 Ejecución | CAP-026 a CAP-028 | FTE-023 / FTE-024 |
| F11 Evidencias | CAP-029 a CAP-031 | FTE-025 / FTE-026 |
| F12 Validación | CAP-032 a CAP-033 | FTE-027 / FTE-028 |
| F13 Excepciones | CAP-034 a CAP-036 | FTE-029 / FTE-030 |
| F14 Cierre | CAP-037 a CAP-038 | FTE-031 / FTE-032 |
| F15 KPI, incentivos y nómina | CAP-039 a CAP-041 | FTE-033 / FTE-034 |
| F16 UX y reportes | CAP-042 a CAP-044 | FTE-035 / FTE-036 |
| F17 Auditoría y recuperación | CAP-045 a CAP-047 | FTE-037 / FTE-038 |
| F18 Migración y coexistencia | CAP-048 a CAP-049 | FTE-039 / FTE-040 |
| F19 Integraciones | CAP-050 | FTE-041 / FTE-042 |

## 4. Fichas de capacidades

### CAP-001 — Administrar identidad y vigencia del empleado

- **Fuente y ubicación:** [E] FTE-005, `Empleado`, `Funciones del dominio`, `Comandos y transacciones`, `Estados y transiciones`; FTE-006, hojas `Modelo_Objetivo`, `Reglas_Funciones`, `Validaciones`.
- **Objetivo:** [E] Conservar una identidad laboral estable, única y con vigencia histórica.
- **Actor:** [E] Actor con autoridad administrativa (`ADMIN_PERSONAL`); el rol concreto es [NE].
- **Disparador:** [E] Alta, baja, reingreso o corrección autorizada de un empleado.
- **Entradas:** [E] Código de empleado, nombre, fechas de ingreso/baja y estado.
- **Reglas:** [E] Código único y no reutilizable; el nombre no es clave; estado laboral y disponibilidad son distintos.
- **Resultados:** [E] Empleado registrado o vigencia actualizada; operaciones `registrar_empleado`, `actualizar_vigencia_empleado`, `determinar_empleado_activo`.
- **Estados:** [E] `ACTIVO → INACTIVO`; reingreso sólo mediante nueva vigencia explícita si el negocio lo permite.
- **Excepciones:** [E] `EMPLEADO_NO_EXISTE`, `CODIGO_EMPLEADO_DUPLICADO`, `EMPLEADO_INACTIVO`.
- **Permisos:** [E] `ADMIN_PERSONAL`.
- **Dependencias:** [E] F2 para autoridad; F18/F19 para migración e importación.
- **Preguntas abiertas:** [E] Reconstrucción histórica de bajas/reingresos y mapeo externo; consolidadas en F02-PRE-028 y F02-PRE-036 cuando afecten migración/integración.

### CAP-002 — Administrar puestos, turnos y asignaciones vigentes

- **Fuente y ubicación:** [E] FTE-005, `Puesto`, `AsignacionPuestoEmpleado`, `Turno`, `AsignacionTurnoEmpleado`, `Comandos y transacciones`; FTE-006, `Modelo_Objetivo`, `Relaciones`, `Validaciones`.
- **Objetivo:** [E] Resolver el puesto y turno reales de una persona para cualquier fecha, conservando histórico.
- **Actor:** [E] Actor con `ADMIN_PERSONAL`; rol concreto [NE].
- **Disparador:** [E] Alta/cambio de puesto o turno, o consulta contextual.
- **Entradas:** [E] Empleado, puesto/turno, intervalo de vigencia y marca de asignación principal.
- **Reglas:** [E] No se permiten asignaciones principales solapadas; catálogos inactivos no admiten nuevas asignaciones; “sin restricción” no es un turno laboral.
- **Resultados:** [E] Asignación histórica creada y anterior cerrada atómicamente; `asignar_puesto_empleado`, `asignar_turno_empleado`, `obtener_puesto_vigente`, `obtener_turno_vigente`.
- **Estados:** [E] Puesto/turno `ACTIVO → INACTIVO`; las asignaciones se rigen por vigencia temporal.
- **Excepciones:** [E] Puesto/turno inexistente o inactivo; asignación solapada; referencia externa sin equivalencia.
- **Permisos:** [E] `ADMIN_PERSONAL`.
- **Dependencias:** [E] F2 organización; F7 elegibilidad; F18/F19 equivalencias.
- **Preguntas abiertas:** [E] F02-PRE-004 y F02-PRE-005.

### CAP-003 — Registrar disponibilidad operativa

- **Fuente y ubicación:** [E] FTE-005, `DisponibilidadEmpleadoPeriodo`, `DisponibilidadEmpleadoDia`, `Comandos y transacciones`, `Entradas`; FTE-006, `Modelo_Objetivo`, `Reglas_Funciones`, `Hallazgos_Datos`.
- **Objetivo:** [E] Conservar la fracción de tiempo disponible para SGOL por período y, opcionalmente, por día.
- **Actor:** [E] Actor administrativo o integración autorizada; identidad exacta [NE].
- **Disparador:** [E] Carga o actualización de disponibilidad.
- **Entradas:** [E] Empleado, período, fecha, fracción de carga/disponibilidad y referencia de origen.
- **Reglas:** [E] Fracciones entre 0 y 1; unicidad empleado-período y empleado-fecha; ausencia de dato no equivale a cero.
- **Resultados:** [E] Disponibilidad persistida; `registrar_disponibilidad_periodo`, `registrar_disponibilidad_dia`, consultas correspondientes.
- **Estados:** [NE] No especificado; se utiliza vigencia y claves de negocio.
- **Excepciones:** [E] Dato fuera de rango, duplicado conflictivo, empleado o período no resuelto.
- **Permisos:** [E] `ADMIN_PERSONAL`; las integraciones deben respetar autoridad interna.
- **Dependencias:** [E] F3 período; F19 carga externa.
- **Preguntas abiertas:** [E] Tratamiento de fracción diaria parcial y contrato de importación; no existe decisión aprobada.

### CAP-004 — Calcular y clasificar capacidad operativa

- **Fuente y ubicación:** [E] FTE-005, `PoliticaCapacidadPuesto`, `Capacidad operativa`, `Estado de capacidad`, `Funciones del dominio`; FTE-006, `Reglas_Funciones`; FTE-044, `05_Vistas_DTO` filas F01.
- **Objetivo:** [E] Exponer minutos disponibles, asignados, libres, utilización, límite asignable y clasificación de capacidad sin duplicar hechos.
- **Actor:** [E] Servicios de elegibilidad, asignación, planificación y reportes; consulta humana según alcance.
- **Disparador:** [E] Consulta o revalidación de capacidad para empleado, fecha y período.
- **Entradas:** [E] Horas base, disponibilidad, carga comprometida, puesto y política vigente, clasificación de calendario.
- **Reglas:** [E] Fórmulas documentadas; división por cero explícita; política versionada; sin fallback silencioso; umbrales 70% y 90%.
- **Resultados:** [E] Vistas `Capacidad operativa` y `Estado de capacidad`; funciones `calcular_*`, `clasificar_estado_capacidad`, `resolver_politica_capacidad`.
- **Estados:** [E] Clasificaciones derivadas `DISPONIBLE`, `ADECUADO`, `SATURADO`; no son estados persistentes.
- **Excepciones:** [E] Política no configurada, período no resuelto, datos conflictivos.
- **Permisos:** [E] Lectura conforme al consumidor; publicar política requiere `ADMIN_PERSONAL`.
- **Dependencias:** [E] F3, F8 y F9.
- **Preguntas abiertas:** [E] Fuente canónica de minutos asignados y semántica de reservas; relacionadas con F02-PRE-008 y F02-PRE-009.

### CAP-005 — Administrar organización, sucursales y áreas

- **Fuente y ubicación:** [E] FTE-007, `Organización`, `Sucursal`, `AreaOrganizacional`, `Comandos y transacciones`; FTE-008, `ORGANIZACION_ORIGEN`, `ENTIDADES_OBJETIVO`, `REGLAS_FUNCIONES`, `PENDIENTES`.
- **Objetivo:** [E] Mantener identidades y jerarquías organizacionales que delimitan la operación y el acceso.
- **Actor:** [E] Actor con `ADMIN_SEGURIDAD` o autoridad organizacional configurada.
- **Disparador:** [E] Creación, modificación o desactivación de unidad organizativa.
- **Entradas:** [E] Organización, códigos estables, sucursal, área, jerarquía, zona horaria y vigencia.
- **Reglas:** [E] Códigos únicos; no usar “TODAS” como sucursal ficticia; desactivar conserva histórico.
- **Resultados:** [E] Organización/área/sucursal persistida; `crear_sucursal`, `desactivar_sucursal`, `registrar_area_organizacional`.
- **Estados:** [E] Activa/inactiva; transiciones detalladas [NE].
- **Excepciones:** [E] Código duplicado, referencia organizacional inválida o área ambigua.
- **Permisos:** [E] `ADMIN_SEGURIDAD`; auditoría de cambios.
- **Dependencias:** [E] F1, F3 y todos los dominios que aplican alcance.
- **Preguntas abiertas:** [E] F02-PRE-001.

### CAP-006 — Administrar usuarios de acceso

- **Fuente y ubicación:** [E] FTE-007, `Usuario`, `Funciones del dominio`, `Comandos y transacciones`, `Eventos del dominio`; FTE-008, `USUARIOS_ORIGEN`, `MATRIZ_MIGRACION`, `REGLAS_FUNCIONES`, `PENDIENTES`.
- **Objetivo:** [E] Registrar y resolver identidades habilitadas para acceder a SGOL sin confundirlas con empleados.
- **Actor:** [E] Administrador de seguridad; proveedor de autenticación como actor técnico [I].
- **Disparador:** [E] Alta, activación, desactivación o autenticación.
- **Entradas:** [E] Identificador de acceso estable, vínculo opcional a empleado, nombre visible y vigencia.
- **Reglas:** [E] Nombre visible no es clave; usuario y empleado son entidades distintas; ausencia de permiso deniega.
- **Resultados:** [E] `registrar_usuario`, `activar_usuario`, `desactivar_usuario`, `resolver_usuario_autenticado`; eventos de usuario.
- **Estados:** [E] Activo/inactivo; secuencia exacta [NE].
- **Excepciones:** [E] Identidad no resuelta, duplicada, inactiva o sin justificación para cuenta técnica.
- **Permisos:** [E] `ADMIN_SEGURIDAD`.
- **Dependencias:** [E] F1 para vínculo laboral; mecanismo de autenticación externo.
- **Preguntas abiertas:** [E] F02-PRE-002.

### CAP-007 — Administrar roles, permisos y alcances

- **Fuente y ubicación:** [E] FTE-007, `RolSeguridad`, `Permiso`, `RolPermiso`, `AsignacionUsuarioRol`, `EquivalenciaPuestoRol`, `Matriz inicial de autoridad`, `Reglas de autorización y acceso`; FTE-008, `PERMISOS_ORIGEN`, `ROLES_Y_AUTORIDAD`, `EQUIVALENCIAS`, `REGLAS_FUNCIONES`.
- **Objetivo:** [E] Resolver capacidades atómicas y alcance efectivo mediante asignaciones vigentes, sin equiparar rol y puesto.
- **Actor:** [E] Administrador de seguridad para cambios; cualquier operación protegida consume la resolución.
- **Disparador:** [E] Asignar/revocar rol o comprobar autorización.
- **Entradas:** [E] Usuario, rol, permiso, vigencia, sucursal/área y contexto.
- **Reglas:** [E] Denegación por defecto; histórico obligatorio; rol no es puesto; alcance global no crea entidad ficticia.
- **Resultados:** [E] `asignar_rol_usuario`, `revocar_rol_usuario`, `listar_roles_vigentes`, `resolver_alcance_usuario`, `tiene_permiso`, `resolver_equivalencia_puesto_rol`.
- **Estados:** [E] Asignaciones vigentes/revocadas por intervalo; estados de rol/permiso [NE].
- **Excepciones:** [E] Rol/permiso no vigente, alcance insuficiente o equivalencia no resuelta.
- **Permisos:** [E] `ADMIN_SEGURIDAD`; segregación y auditoría obligatorias.
- **Dependencias:** [E] F1 puestos; F12 autoridad de validación; F16 visibilidad.
- **Preguntas abiertas:** [E] F02-PRE-003; además, combinaciones incompatibles de roles no especificadas.

### CAP-008 — Registrar y validar autorizaciones explícitas

- **Fuente y ubicación:** [E] FTE-007, `Autorizacion`, `Reglas de autorización y acceso`, `Funciones del dominio`, `Comandos y transacciones`; FTE-008, `ROLES_Y_AUTORIDAD`, `REGLAS_FUNCIONES`.
- **Objetivo:** [E] Conservar una decisión excepcional y auditable para una operación/objeto concretos.
- **Actor:** [E] Autoridad requerida resuelta por política; identidad concreta [NE].
- **Disparador:** [E] Operación que exige aprobación adicional o trazabilidad formal.
- **Entradas:** [E] Usuario autorizador, tipo de operación, referencia de objeto, contexto, vigencia y motivo.
- **Reglas:** [E] Autorización excepcional no sustituye permisos permanentes; debe ser verificable y auditable.
- **Resultados:** [E] `registrar_autorizacion`, `resolver_autoridad_requerida`, `validar_autorizacion`; evento `AutorizacionRegistrada`.
- **Estados:** [NE] No especificado; vigencia/revocación exactas pendientes.
- **Excepciones:** [E] Autoridad insuficiente, autorización ausente/expirada o contexto incompatible.
- **Permisos:** [E] `ADMIN_SEGURIDAD` y permiso de dominio correspondiente.
- **Dependencias:** [E] F12–F15 y F17.
- **Preguntas abiertas:** [E] Revocación y contratos por tipo de operación; F02-PRE-003, F02-PRE-017 y F02-PRE-022.

### CAP-009 — Administrar configuración operativa versionada

- **Fuente y ubicación:** [E] FTE-009, `ConfiguracionOperativa`, `obtener_configuracion`, `cambiar_configuracion_operativa`; FTE-010, `PARAMETROS_ORIGEN`, `MATRIZ_MIGRACION`, `REGLAS_FUNCIONES`.
- **Objetivo:** [E] Resolver parámetros tipados por clave, alcance y vigencia sin constantes ocultas.
- **Actor:** [E] Actor con permiso específico de configuración/calendario; rol concreto [NE].
- **Disparador:** [E] Publicación/cambio de un parámetro o consulta contextual.
- **Entradas:** [E] Clave, valor tipado, unidad, alcance, fechas de vigencia y motivo.
- **Reglas:** [E] Tipado, vigencia y alcance explícitos; sin texto libre ejecutable ni sobrescritura del histórico.
- **Resultados:** [E] Configuración publicada y resoluble; `obtener_configuracion`, `cambiar_configuracion_operativa`.
- **Estados:** [E] Versiones vigentes/cerradas por intervalo; máquina detallada [NE].
- **Excepciones:** [E] Clave inexistente, valor/tipo inválido, solapamiento o alcance ambiguo.
- **Permisos:** [E] `ADMIN_CALENDARIO`; cambios sensibles auditados.
- **Dependencias:** [E] F2 alcance; F17 auditoría.
- **Preguntas abiertas:** [E] Semántica y consumidores de “Modo Operación”; no existe decisión aprobada.

### CAP-010 — Administrar calendario y excepciones de días operativos

- **Fuente y ubicación:** [E] FTE-009, `CalendarioOperativo`, `ReglaDiaOperativo`, `ExcepcionCalendario`, `Funciones puras`, `registrar_excepcion_calendario`; FTE-010, `CALENDARIO_ORIGEN`, `EXCEPCIONES_CALENDARIO`, `DIVERGENCIA_ISO`, `PENDIENTES`.
- **Objetivo:** [E] Determinar días, límites y traslados operativos por alcance.
- **Actor:** [E] Administrador de calendario; consumidores automáticos de F5/F13/F15.
- **Disparador:** [E] Cambio de regla/excepción o cálculo temporal.
- **Entradas:** [E] Fecha, alcance, regla recurrente, excepción, zona horaria y dirección de traslado.
- **Reglas:** [E] Convención ISO; excepciones prevalecen según política; calendario común para cálculos.
- **Resultados:** [E] `determinar_dia_operativo`, `trasladar_fecha_operativa`, `calcular_dias_operativos`, `obtener_anio_semana_iso`, `obtener_limites_periodo_iso`.
- **Estados:** [NE] No especificado para calendario/excepción.
- **Excepciones:** [E] Calendario no resuelto, excepción conflictiva, alcance o zona horaria no definidos.
- **Permisos:** [E] `ADMIN_CALENDARIO`.
- **Dependencias:** [E] F2 sucursal/alcance; F5, F9, F13–F15.
- **Preguntas abiertas:** [E] Calendario real por sucursal, festivos y zona horaria; la divergencia ISO se registra como F02-CON-002.

### CAP-011 — Administrar el período operativo

- **Fuente y ubicación:** [E] FTE-009, `PeriodoOperativo`, `EstadoPeriodoOperativo`, `HistorialEstadoPeriodo`, `crear_periodo_operativo`, `cambiar_estado_periodo`, `Máquina de estados`; FTE-010, `ESTADOS_TRANSICIONES`, `HISTORIAL_ESTADO_ORIGEN`; FTE-044, `07_Estados` fila F03.
- **Objetivo:** [E] Identificar y gobernar el ciclo semanal por alcance sin usar un parámetro global mutable.
- **Actor:** [E] Actor autorizado de calendario/operación; F14 confirma el cierre.
- **Disparador:** [E] Creación del período o transición operativa.
- **Entradas:** [E] Año/semana ISO, alcance, fechas límite, estado objetivo, actor y motivo.
- **Reglas:** [E] Identidad por período/alcance; historial append-only; `CERRADA` terminal para la misma identidad.
- **Resultados:** [E] Período creado/transicionado; `obtener_periodo_operativo`, `validar_estado_periodo`, `clasificar_periodo_respecto_hoy`.
- **Estados:** [E] `DISPONIBLE_PARA_SIMULACION → EN_REVISION → PUBLICADA → EN_EJECUCION → CERRADA`.
- **Excepciones:** [E] Transición inválida, período duplicado/no resuelto o intento de reapertura ordinaria.
- **Permisos:** [E] `ADMIN_CALENDARIO`; `CERRAR_PERIODO` para cierre.
- **Dependencias:** [E] F9 y F14; F17 para restauración técnica.
- **Preguntas abiertas:** [E] Período activo único frente a ID explícito por operación; F02-CON-005 sobre reapertura legacy.

### CAP-012 — Crear, versionar y desactivar definiciones de tarea

- **Fuente y ubicación:** [E] FTE-011, `DefinicionTarea`, `VersionDefinicionTarea`, `Funciones puras`, `Procedimientos o comandos`, `Estados y transiciones`; FTE-012, `Tareas`, `Funciones`, `Pendientes`.
- **Objetivo:** [E] Mantener identidades `TAR-####` y versiones semánticas históricas para obligaciones operativas genuinamente distintas.
- **Actor:** [E] Gobierno del catálogo.
- **Disparador:** [E] Alta, nueva versión o retiro para nuevas obligaciones.
- **Entradas:** [E] Identidad, jerarquía, propósito, resultado esperado, vigencia y referencias configuradas.
- **Reglas:** [E] ID único/no reutilizable; una versión vigente compatible; separar resultado, criterio y evidencia; no alterar histórico.
- **Resultados:** [E] `crear_definicion_tarea`, `publicar_version_definicion`, `desactivar_definicion_tarea`, `validar_definicion_tarea`, `determinar_version_vigente_tarea`.
- **Estados:** [E] `ACTIVA` / `NO_ACTIVA_PARA_NUEVAS_OBLIGACIONES`; versiones anteriores históricas.
- **Excepciones:** [E] ID duplicado, versión solapada, jerarquía/referencia inválida.
- **Permisos:** [E] `GOBERNAR_CATALOGO_TAREAS`.
- **Dependencias:** [E] F1–F3 y F5–F19.
- **Preguntas abiertas:** [E] TAR-0195…TAR-0202 y T221; F02-PRE-006 y F02-PRE-027.

### CAP-013 — Mantener jerarquías, flujos, dependencias y componentes del catálogo

- **Fuente y ubicación:** [E] FTE-011, `Macroproceso` a `Automatizacion`, `Relaciones principales`, `Funciones puras`, `Procedimientos o comandos`; FTE-012, `Jerarquia`, `Flujos`, `Checklists`, `KPI`, `Reglas`, `Automatizacion`, `Relaciones`, `Funciones`.
- **Objetivo:** [E] Resolver para cada versión su jerarquía, flujos, dependencias, checklists, reglas, KPI y automatizaciones reales.
- **Actor:** [E] Gobierno del catálogo.
- **Disparador:** [E] Cambio versionado de estructura o consulta por consumidores.
- **Entradas:** [E] IDs canónicos, tipos de relación, orden, vigencia, parámetros tipados y semántica.
- **Reglas:** [E] Relaciones tipadas; ciclos sólo si la semántica los permite; KPI atómico; texto libre no es código; potencial de automatización no crea componente.
- **Resultados:** [E] `vincular_tarea_flujo`, `vincular_dependencia_tarea`, `resolver_jerarquia_tarea`, `resolver_flujos_tarea`, `resolver_dependencias_tarea`, `resolver_checklists_tarea`, `resolver_kpi_tarea`, `obtener_regla_configurable`.
- **Estados:** [E] Vigencia/versiones; estados de despliegue no son estados funcionales.
- **Excepciones:** [E] Referencia inválida, ciclo no permitido, KPI no atómico, regla no estructurada.
- **Permisos:** [E] `GOBERNAR_CATALOGO_TAREAS`; cambios auditados.
- **Dependencias:** [E] F5, F7–F8, F11–F12, F15, F19.
- **Preguntas abiertas:** [E] Catálogos FLU/CHK/AUT y secuencia interna; clasificación AUT externa en F02-PRE-038.

### CAP-014 — Administrar reglas de activación

- **Fuente y ubicación:** [E] FTE-013, `ReglaActivacion`, `ProgramacionActivacion`, `ReglaEvento`, `ReglaCondicion`, `ReglaProcesoOrigen`, `ReglaTemporal`, `Estados y transiciones`, `Permisos`; FTE-014, `Componentes`, `Tipos_Activacion`, `Parametros`, `Estados`, `Funciones`.
- **Objetivo:** [E] Publicar, suspender o retirar configuración versionada que determine cuándo una tarea puede solicitar una obligación.
- **Actor:** [E] Actor con `ADMIN_ACTIVACION`.
- **Disparador:** [E] Alta/cambio de política de activación.
- **Entradas:** [E] Tarea/versión, tipo, alcance, vigencia, mecanismo específico, parámetros y regla temporal opcional.
- **Reglas:** [E] Tipos admitidos `PROGRAMADA`, `EVENTO`, `CONDICIONAL`, `PROCESO`; SLA no es tipo; sin texto libre ejecutable.
- **Resultados:** [E] Regla ejecutable, suspendida o retirada; `ValidarReglaActivacionVigente`.
- **Estados:** [E] `BORRADOR → ACTIVA ↔ SUSPENDIDA → RETIRADA`.
- **Excepciones:** [E] Regla inexistente/no vigente, configuración incompleta o parámetro inválido.
- **Permisos:** [E] `ADMIN_ACTIVACION`.
- **Dependencias:** [E] F2–F4 y F17.
- **Preguntas abiertas:** [E] Configuración de TAR-0195…TAR-0202; F02-PRE-006.

### CAP-015 — Evaluar activaciones programadas

- **Fuente y ubicación:** [E] FTE-013, `ProgramacionActivacion`, `Reglas de idempotencia`, `Funciones, comandos, handlers y jobs`; FTE-014, `Tipos_Activacion`, `Disparos_Prueba`, `Funciones`.
- **Objetivo:** [E] Determinar si una regla programada cumple para una fecha/ventana y calcular su momento objetivo.
- **Actor:** [E] Job `EvaluarActivacionesProgramadas`; actor humano [NE].
- **Disparador:** [E] Cadencia de calendario; la cadencia física es [NE] y se reserva a implementación.
- **Entradas:** [E] Regla activa, timestamp, calendario, ventana, anticipación y alcance.
- **Reglas:** [E] Ventanas y unidades estructuradas; días hábiles comunes; clave lógica incluye tarea, regla, ventana y alcance.
- **Resultados:** [E] `EvaluarProgramacion`, `ResolverVentanasIntradia`, `CalcularFechaHabilObjetivo`, `CalcularMomentoPorAnticipacion` y evaluación explicable.
- **Estados:** [E] Estados de `EvaluacionActivacion` definidos en CAP-018.
- **Excepciones:** [E] Calendario/ventana no resueltos, parámetros incompletos, duplicado o coexistencia.
- **Permisos:** [E] Servicio autorizado; modificar regla requiere `ADMIN_ACTIVACION`.
- **Dependencias:** [E] F3, F4, F17 y F18.
- **Preguntas abiertas:** [E] Cadencia física [NE]; no corresponde definirla en F02.

### CAP-016 — Evaluar activaciones por evento

- **Fuente y ubicación:** [E] FTE-013, `ReglaEvento`, `TipoEventoNegocio`, `Reglas de idempotencia`, `Funciones, comandos, handlers y jobs`; FTE-014, `Tipos_Activacion`, `Disparos_Prueba`, `Funciones`.
- **Objetivo:** [E] Evaluar un evento identificado contra una regla vigente y producir una decisión explicable.
- **Actor:** [E] `HandlerEventoNegocio`; productor externo/interno no obtiene autoridad de administración.
- **Disparador:** [E] Evento de negocio con identidad, fuente, correlación y timestamp.
- **Entradas:** [E] Evento, regla, contexto/caso, alcance y campos obligatorios.
- **Reglas:** [E] Evento sin ID no es idempotente; predicados estructurados; conservar correlación.
- **Resultados:** [E] `EvaluarEvento`, `ConstruirClaveIdempotenciaActivacion`, evaluación lista/bloqueada/no cumplida.
- **Estados:** [E] Los de `EvaluacionActivacion`.
- **Excepciones:** [E] `EVENTO_SIN_IDENTIDAD`, contexto insuficiente, duplicado, regla no vigente.
- **Permisos:** [E] Servicio autorizado; reglas bajo `ADMIN_ACTIVACION`.
- **Dependencias:** [E] F6, F17 y F19.
- **Preguntas abiertas:** [E] Contratos de eventos, reintentos y fuente; F02-PRE-031 a F02-PRE-037 según integración.

### CAP-017 — Evaluar activaciones condicionales o de proceso

- **Fuente y ubicación:** [E] FTE-013, `ReglaCondicion`, `ReglaProcesoOrigen`, `EvaluacionActivacion`, `Funciones, comandos, handlers y jobs`; FTE-014, `Tipos_Activacion`, `Cohorte8`, `Funciones`.
- **Objetivo:** [E] Determinar si un contexto o caso origen cumple una condición explícita para solicitar una obligación.
- **Actor:** [E] `HandlerCambioContextoCondicion` o `HandlerProcesoOrigen`.
- **Disparador:** [E] Cambio de contexto o creación/cambio de caso origen.
- **Entradas:** [E] Regla, contexto/caso identificado, padre, timestamp y campos requeridos.
- **Reglas:** [E] Padre-hijo no activa por sí solo; condición explícita; conservar caso/timestamp; granularidad definida.
- **Resultados:** [E] `EvaluarCondicion`, `ResolverOrigenProceso`, `CalcularVencimiento` y evaluación explicable.
- **Estados:** [E] Los de `EvaluacionActivacion`.
- **Excepciones:** [E] `CONTEXTO_INSUFICIENTE`, `CASO_ORIGEN_NO_RESUELTO`, `PADRE_NO_RESUELTO`.
- **Permisos:** [E] Servicio autorizado; reglas bajo `ADMIN_ACTIVACION`.
- **Dependencias:** [E] F3, F4, F6, F17–F19.
- **Preguntas abiertas:** [E] Campos de contexto que deben persistirse frente a referenciarse; no especificado.

### CAP-018 — Emitir una solicitud de generación idempotente

- **Fuente y ubicación:** [E] FTE-013, `SolicitudActivacion`, `Reglas de idempotencia`, `EmitirSolicitudGeneracion`, `Estados y transiciones`; FTE-014, `Funciones`, `Guardas_Legacy`, `Estados`.
- **Objetivo:** [E] Entregar a F6 una solicitud única, trazable y suficientemente tipada.
- **Actor:** [E] Servicio de activación.
- **Disparador:** [E] Evaluación en `LISTA_GENERACION`.
- **Entradas:** [E] Tarea/versión, regla/versión, tipo, clave de idempotencia, timestamp, origen/padre, vencimiento, alcance y trazabilidad.
- **Reglas:** [E] Verificar duplicado y coexistencia antes de emitir; no contiene asignación ni crea directamente PLAN/EJEC.
- **Resultados:** [E] `EmitirSolicitudGeneracion`, `VerificarDuplicadoActivacion`; evaluación `EMITIDA` si F6 acepta.
- **Estados:** [E] `INICIADA → NO_CUMPLE | BLOQUEADA_* | LISTA_GENERACION → EMITIDA`.
- **Excepciones:** [E] Duplicado, coexistencia, configuración o calendario no resuelto.
- **Permisos:** [E] Servicio autorizado; administración separada.
- **Dependencias:** [E] F6 y F17–F18.
- **Preguntas abiertas:** [E] Qué evaluaciones negativas/bloqueadas se persisten; [NE].

### CAP-019 — Crear o recuperar una instancia de trabajo

- **Fuente y ubicación:** [E] FTE-015, `Entidad definitiva: InstanciaTrabajo`, `Contrato de entrada desde F5`, `Identidad e idempotencia`, `Funciones, comandos y eventos`; FTE-016, `Contrato_Entrada`, `Identidad_Claves`, `Funciones`, `Validaciones`.
- **Objetivo:** [E] Persistir una obligación canónica exactamente una vez a partir de una solicitud válida.
- **Actor:** [E] Servicio de generación; actor humano [NE].
- **Disparador:** [E] Solicitud aceptada desde F5.
- **Entradas:** [E] Contrato de activación, clave idempotente, versión de tarea, origen, vencimiento y alcance.
- **Reglas:** [E] ID de instancia independiente; misma clave/mismo payload devuelve la misma instancia; misma clave/payload incompatible es error; no crea PLAN/EJEC.
- **Resultados:** [E] `CrearInstanciaDesdeSolicitud`, `ObtenerOCrearPorIdempotencia`, `GenerarIDInstancia`, `InstanciaCreada`.
- **Estados:** [NE] No se define máquina propia persistente en F6.
- **Excepciones:** [E] Solicitud no generable, clave inválida/conflictiva, referencia o calendario no resueltos.
- **Permisos:** [E] Servicio interno autorizado; permiso humano [NE].
- **Dependencias:** [E] F4–F5, F9–F10, F13, F17–F19.
- **Preguntas abiertas:** [E] Uno o varios intentos de ejecución; continuidad frente a instancia derivada; entrega confiable del evento.

### CAP-020 — Evaluar y explicar elegibilidad

- **Fuente y ubicación:** [E] FTE-017, `Criterios de elegibilidad`, `Estado de la evaluación`, `Códigos mínimos de explicación`, `Funciones y contratos`; FTE-018, `Criterios`, `Snapshot_Elegibilidad`, `Sin_Candidato`, `Puestos_Sin_Mapeo`.
- **Objetivo:** [E] Construir candidatos aptos y explicar cada aceptación o rechazo sin asignar responsable.
- **Actor:** [E] Servicio de elegibilidad; consultas autorizadas de operación.
- **Disparador:** [E] Instancia que necesita candidatos o solicitud de explicación.
- **Entradas:** [E] Instancia, momento, política, empleado, puesto, turno, disponibilidad, capacidad, responsable de origen, especialidad y empleado fijo.
- **Reglas:** [E] Sólo criterios habilitados por política; no mezclar ranking ni validador; resultado reproducible y explicable.
- **Resultados:** [E] `listar_personal_base`, `resolver_restricciones_contextuales`, `evaluar_candidato`, `listar_candidatos_elegibles`, `explicar_elegibilidad`.
- **Estados:** [E] Elegible/no elegible/con configuración incompleta mediante códigos explicativos; catálogo exacto en fuente.
- **Excepciones:** [E] Política/configuración incompleta, puesto/turno/especialidad sin mapeo, sin candidato.
- **Permisos:** [E] Lectura según alcance; publicación de políticas requiere autoridad configurada.
- **Dependencias:** [E] F1–F4, F6, F8, F17–F19.
- **Preguntas abiertas:** [E] F02-PRE-004, F02-PRE-005 y F02-PRE-006.

### CAP-021 — Administrar políticas de elegibilidad

- **Fuente y ubicación:** [E] FTE-017, `PoliticaElegibilidadTarea`, relaciones de puesto/turno/especialidad, `publicar_politica_elegibilidad`, `Validaciones de configuración`; FTE-018, `Modelo_Objetivo`, `Reglas_S049_Separadas`, `Pendientes`.
- **Objetivo:** [E] Versionar los criterios que construyen candidatos para una versión de tarea.
- **Actor:** [E] Autoridad de configuración; permiso lógico específico [NE].
- **Disparador:** [E] Alta o cambio de requisitos de elegibilidad.
- **Entradas:** [E] Tarea/versión, criterios habilitados, IDs de puesto/turno/especialidad, vigencia y contexto.
- **Reglas:** [E] Referencias canónicas; ausencia de restricción debe ser explícita; no hardcodes por nombre.
- **Resultados:** [E] Política publicada y resoluble; `resolver_politica_elegibilidad`, `publicar_politica_elegibilidad`.
- **Estados:** [E] Versionada/vigente; máquina detallada [NE].
- **Excepciones:** [E] Configuración inválida o referencia no resuelta.
- **Permisos:** [E] Autoridad conforme a F2; código de permiso [NE].
- **Dependencias:** [E] F1–F4, F8 y F18.
- **Preguntas abiertas:** [E] F02-PRE-004 a F02-PRE-006.

### CAP-022 — Rankear candidatos y proponer asignación

- **Fuente y ubicación:** [E] FTE-019, `Reglas de ranking`, `Funciones y contratos`, objetos `RankingCandidatos` y `PropuestaAsignacion`; FTE-020, `Ranking_Actual`, `Snapshot_Motor`, `Politica_Capacidad_Actual`, `Pendientes`; FTE-044, `05_Vistas_DTO` filas F08.
- **Objetivo:** [E] Ordenar candidatos elegibles con política versionada y producir una propuesta explicable.
- **Actor:** [E] Motor de asignación; usuario autorizado puede consultar diagnóstico.
- **Disparador:** [E] Instancia sin asignación o balanceo de conjunto.
- **Entradas:** [E] Candidatos F7, capacidad revalidada, política, criterios, prioridades y momento.
- **Reglas:** [E] Revalidación transaccional; criterios configurados; propuesta no es hecho; desempate no depende del orden físico.
- **Resultados:** [E] `calcular_componentes_ranking`, `rankear_candidatos`, `resolver_desempate`, `proponer_asignacion`, `balancear_conjunto`.
- **Estados:** [E] Resultado propuesto/sin candidato/bloqueado según fuente; detalle final pendiente.
- **Excepciones:** [E] Sin candidato, capacidad cambió, política incompleta o empate no resoluble.
- **Permisos:** [E] Consulta por alcance; confirmar requiere `ASIGNAR_TRABAJO`.
- **Dependencias:** [E] F1, F6–F7, F17.
- **Preguntas abiertas:** [E] F02-PRE-007 y F02-PRE-008.

### CAP-023 — Confirmar asignación automática o manual

- **Fuente y ubicación:** [E] FTE-019, `AsignacionTrabajo`, `Comandos transaccionales`, `Eventos`, `Estados y resultados`, `Permisos`; FTE-020, `Flujo_Asignacion`, `Modelo_Objetivo`; FTE-044, `07_Estados` fila F08.
- **Objetivo:** [E] Persistir un único responsable vigente para una instancia tras revalidar condiciones.
- **Actor:** [E] Servicio autorizado o usuario con permiso manual específico.
- **Disparador:** [E] Propuesta aceptable o decisión manual justificada.
- **Entradas:** [E] Instancia, empleado, contexto, versión de evaluación/capacidad/política y motivo manual.
- **Reglas:** [E] Una asignación activa por instancia; operación atómica e idempotente; manual no omite elegibilidad salvo autorización explícita documentada.
- **Resultados:** [E] `confirmar_asignacion`, `confirmar_asignacion_manual`, evento `AsignacionConfirmada`.
- **Estados:** [E] `ACTIVA → REVOCADA`; reasignación pertenece a F13.
- **Excepciones:** [E] Capacidad insuficiente, candidato inelegible, concurrencia, duplicado o autorización insuficiente.
- **Permisos:** [E] `ASIGNAR_TRABAJO`.
- **Dependencias:** [E] F2, F6–F7, F9, F13, F17.
- **Preguntas abiertas:** [E] F02-PRE-007 y F02-PRE-020.

### CAP-024 — Crear y construir un plan operativo

- **Fuente y ubicación:** [E] FTE-021, `PlanOperativo`, `PartidaPlan`, `Reglas de construcción del plan`, `Funciones y consultas`, `Comandos transaccionales`; FTE-022, `Modelo_Objetivo`, `Mapa_Columnas_PLAN`, `Flujo_Publicacion`.
- **Objetivo:** [E] Agrupar obligaciones de un período/alcance y preparar partidas revisables sin duplicar la obligación.
- **Actor:** [E] Planificador autorizado; rol concreto [NE].
- **Disparador:** [E] Preparación de un período o incorporación de instancia candidata.
- **Entradas:** [E] Período, alcance, instancias, asignaciones activas, compromisos y contexto.
- **Reglas:** [E] Plan y partida tienen identidad propia; unicidad por plan+instancia; estados separados de ejecución/excepción/cierre.
- **Resultados:** [E] `crear_plan_operativo`, `agregar_partida_revision`, `obtener_plan_periodo`, `listar_instancias_candidatas_plan`, `validar_partida_plan`, `resumir_plan`.
- **Estados:** [E] Plan `EN_CONSTRUCCION → EN_REVISION`; partida `EN_REVISION`.
- **Excepciones:** [E] Duplicado, fuera de período, sin asignación o configuración incompleta.
- **Permisos:** [E] Preparación por autoridad de planificación; permiso exacto distinto de publicación [NE].
- **Dependencias:** [E] F2–F3, F6, F8 y F17.
- **Preguntas abiertas:** [E] F02-PRE-009.

### CAP-025 — Publicar plan inicial o incremental

- **Fuente y ubicación:** [E] FTE-021, `Reglas de publicación`, `Publicación inicial`, `Publicación incremental`, `Idempotencia`, `Congelamiento y cambios posteriores`, `Comandos transaccionales`; FTE-022, `Flujo_Publicacion`, `Procedimientos_VBA`, `Pendientes`.
- **Objetivo:** [E] Hacer ejecutables partidas válidas, incluyendo obligaciones posteriores, sin duplicar ni reabrir el plan.
- **Actor:** [E] Usuario con `PUBLICAR_PLAN` dentro del alcance; autoridad exacta pendiente.
- **Disparador:** [E] Aprobación de publicación inicial o llegada posterior de obligación publicable.
- **Entradas:** [E] Plan, partidas, instante, asignaciones, estado del período y contexto de publicación.
- **Reglas:** [E] Validación previa, atomicidad e idempotencia; incremental conserva plan publicado; no publicar no asignadas sin política.
- **Resultados:** [E] `publicar_plan_inicial`, `publicar_partida_incremental`, `retirar_partida_no_publicada`; eventos de publicación; cola de pendientes.
- **Estados:** [E] Plan `EN_REVISION → PUBLICADO → CERRADO`; partida `EN_REVISION → PUBLICADA`.
- **Excepciones:** [E] Validación fallida, duplicado, concurrencia, excepción no autorizada o período inválido.
- **Permisos:** [E] `PUBLICAR_PLAN` por alcance/estado.
- **Dependencias:** [E] F3, F6, F8, F10, F13–F14, F17.
- **Preguntas abiertas:** [E] F02-PRE-010 y F02-PRE-011.

### CAP-026 — Iniciar una ejecución real

- **Fuente y ubicación:** [E] FTE-023, `Inicio de ejecución`, `Estados y transiciones`, `Comandos transaccionales`, `Permisos`; FTE-024, `Flujo_Ejecucion`, `Transiciones_Objetivo`, `Procedimientos_VBA`; FTE-044, `07_Estados` fila F10.
- **Objetivo:** [E] Crear una ejecución únicamente cuando el trabajo se inicia realmente sobre una partida publicada.
- **Actor:** [E] Responsable asignado o autoridad explícita con `EJECUTAR_TAREA`.
- **Disparador:** [E] Comando de inicio sobre partida ejecutable.
- **Entradas:** [E] `id_partida_plan`, actor, momento y contexto.
- **Reglas:** [E] Partida publicada, asignación válida, período habilitado, una ejecución activa; operación transaccional e idempotente.
- **Resultados:** [E] `iniciar_ejecucion`, `validar_partida_ejecutable`, `validar_asignacion_para_ejecucion`, evento `EjecucionIniciada`.
- **Estados:** [E] `PENDIENTE_INICIO` es derivado; al iniciar se crea `EN_PROCESO`.
- **Excepciones:** [E] Partida no ejecutable, actor no asignado, ejecución activa, período cerrado o concurrencia.
- **Permisos:** [E] `EJECUTAR_TAREA`.
- **Dependencias:** [E] F2–F3, F8–F9, F17.
- **Preguntas abiertas:** [E] F02-PRE-012 y F02-PRE-013.

### CAP-027 — Concluir una ejecución

- **Fuente y ubicación:** [E] FTE-023, `Conclusión de ejecución`, `Reglas de integridad y concurrencia`, `Comandos transaccionales`, `Eventos`; FTE-024, `Flujo_Ejecucion`, `Transiciones_Objetivo`, `Pendientes`.
- **Objetivo:** [E] Registrar el fin real de trabajo sin mezclar evidencia, validación, excepción o cierre.
- **Actor:** [E] Responsable asignado o autoridad explícita.
- **Disparador:** [E] Comando de conclusión.
- **Entradas:** [E] Ejecución, observaciones, actor, timestamp y contexto.
- **Reglas:** [E] Sólo desde `EN_PROCESO`; transición única y auditable; requisitos de evidencia se evalúan en F11 según política.
- **Resultados:** [E] `concluir_ejecucion`, `validar_transicion_ejecucion`; eventos `EjecucionConcluida` y `EstadoEjecucionCambiado`.
- **Estados:** [E] `EN_PROCESO → CONCLUIDA`.
- **Excepciones:** [E] Transición inválida, evidencia/gate pendiente cuando aplique, concurrencia o período cerrado.
- **Permisos:** [E] `EJECUTAR_TAREA`.
- **Dependencias:** [E] F11–F14 y F17.
- **Preguntas abiertas:** [E] Política de conclusión cuando una excepción afecta ejecución activa; F02-PRE-020.

### CAP-028 — Consultar pendientes, ejecución e historial

- **Fuente y ubicación:** [E] FTE-023, `Objetos derivados`, `Funciones y consultas`; FTE-024, `Mapa_Columnas_EJEC`; FTE-044, `05_Vistas_DTO` filas F10.
- **Objetivo:** [E] Exponer partidas pendientes de inicio, ejecución actual, duración real e historial sin duplicar hechos.
- **Actor:** [E] Usuario operativo/supervisor dentro de alcance.
- **Disparador:** [E] Consulta de bandeja, detalle o auditoría operativa.
- **Entradas:** [E] Período, alcance, usuario, partida o ejecución.
- **Reglas:** [E] Duración = conclusión menos inicio cuando ambos existen; acciones por IDs canónicos; `PENDIENTE_INICIO` no crea fila.
- **Resultados:** [E] `listar_pendientes_inicio`, `obtener_ejecucion_*`, `obtener_historial_ejecucion`, `calcular_duracion_real`, `puede_modificarse_ejecucion`.
- **Estados:** [E] Presenta `PENDIENTE_INICIO`, `EN_PROCESO`, `CONCLUIDA` sin incorporar estados ajenos.
- **Excepciones:** [E] Inicio/fin histórico faltante, acceso fuera de alcance o referencia no resuelta.
- **Permisos:** [E] `CONSULTAR_OPERACION` y, para auditoría, permiso correspondiente.
- **Dependencias:** [E] F2, F9–F17.
- **Preguntas abiertas:** [E] Tratamiento histórico de timestamps faltantes; F02-CON-003.

### CAP-029 — Configurar tipos y requisitos de evidencia

- **Fuente y ubicación:** [E] FTE-025, `TipoEvidencia`, `RequisitoEvidencia`, `RequisitoEvidenciaTipo`, `Reglas de negocio`; FTE-026, `Catalogo_Tipos`, `Requisitos_202`, `Reglas`, `Pendientes`.
- **Objetivo:** [E] Definir qué evidencia exige una versión de tarea, en qué cantidad/combinación y qué referencias admite.
- **Actor:** [E] Gobierno del catálogo/evidencia; rol exacto [NE].
- **Disparador:** [E] Publicación de requisito o tipo.
- **Entradas:** [E] Tarea/versión, obligatoriedad, cardinalidad, combinación, tipos permitidos, vigencia y contexto.
- **Reglas:** [E] Evidencia, criterio de validación y requisito son conceptos separados; política versionada; referencia compatible con tipo.
- **Resultados:** [E] Requisitos y catálogo resolubles por ejecución.
- **Estados:** [E] Vigente/no vigente por versión; máquina propia [NE].
- **Excepciones:** [E] Tipo/requisito inválido, combinación o cardinalidad sin definir.
- **Permisos:** [E] Gobierno de catálogo; código lógico específico [NE].
- **Dependencias:** [E] F4, F10, F12, F17/F19.
- **Preguntas abiertas:** [E] F02-PRE-014 y F02-PRE-015.

### CAP-030 — Registrar o sustituir evidencia

- **Fuente y ubicación:** [E] FTE-025, `Evidencia`, `ReferenciaEvidencia`, `Comandos transaccionales`, `Eventos`, `Permisos`; FTE-026, `Funciones_Comandos`, `Procedimientos_VBA`, `Relaciones`.
- **Objetivo:** [E] Conservar prueba trazable y, cuando se autorice, sustituirla sin borrar el historial.
- **Actor:** [E] Responsable o actor permitido por política; sustitución extraordinaria puede exigir autoridad adicional.
- **Disparador:** [E] Carga de evidencia o corrección/sustitución.
- **Entradas:** [E] Ejecución, tipo, contenido/referencia, metadatos de captura, actor y motivo.
- **Reglas:** [E] Tipo permitido, referencia verificable, vínculo por ID de ejecución; sustitución conserva la anterior.
- **Resultados:** [E] `registrar_evidencia`, `sustituir_evidencia`; eventos `EvidenciaRegistrada`, `EvidenciaSustituida`.
- **Estados:** [E] Evidencia vigente/sustituida mediante relación histórica; etiquetas exactas [NE].
- **Excepciones:** [E] Tipo no permitido, referencia inválida, ejecución inexistente o período protegido.
- **Permisos:** [E] `REGISTRAR_EVIDENCIA`; sustitución postconclusión/postvalidación [NE].
- **Dependencias:** [E] F2, F10, F12, F14, F17/F19.
- **Preguntas abiertas:** [E] Repositorio/retención y permiso de sustitución; relacionadas con F02-PRE-015, F02-PRE-022 y F02-PRE-026.

### CAP-031 — Evaluar cumplimiento estructural de evidencia

- **Fuente y ubicación:** [E] FTE-025, `Objetos derivados`, `Funciones y consultas`, `Reglas de negocio`; FTE-026, `Reglas`, `Funciones_Comandos`; FTE-044, `05_Vistas_DTO` filas F11.
- **Objetivo:** [E] Determinar si una ejecución cumple cantidades, tipos y referencias exigidos, y explicar faltantes.
- **Actor:** [E] Ejecución, validación, cierre y consulta UX como consumidores.
- **Disparador:** [E] Registro/sustitución, intento de concluir/validar o consulta.
- **Entradas:** [E] Ejecución, requisitos vigentes, evidencias vigentes y tipos permitidos.
- **Reglas:** [E] Evaluación derivada y reproducible; no equivale a decisión de validación.
- **Resultados:** [E] `requiere_evidencia`, `obtener_requisitos_evidencia`, `evaluar_cumplimiento_estructural_evidencia`, `explicar_faltantes_evidencia`; vistas y evento de requisitos satisfechos.
- **Estados:** [E] Cumple/no cumple y pendientes derivados; nomenclatura exhaustiva [NE].
- **Excepciones:** [E] Requisito incompleto, tipo por clasificar o evidencia histórica inconsistente.
- **Permisos:** [E] Lectura dentro del alcance; detalle sensible conforme a política.
- **Dependencias:** [E] F10, F12, F14, F17.
- **Preguntas abiertas:** [E] F02-PRE-014, F02-PRE-015; contradicción F02-CON-004.

### CAP-032 — Configurar políticas y criterios de validación

- **Fuente y ubicación:** [E] FTE-027, `PoliticaValidacion`, `CriterioValidacion`, `Gates de validación`, `Permisos y autoridad`; FTE-028, `Politicas_202`, `Permisos_Validacion`, `Reglas`, `Pendientes`.
- **Objetivo:** [E] Definir cómo se decide cumplimiento y qué autoridad puede decidirlo por tarea/contexto.
- **Actor:** [E] Gobierno de validación/seguridad; rol exacto depende de política.
- **Disparador:** [E] Publicación o cambio de política/criterio.
- **Entradas:** [E] Tarea/versión, criterios, resultado permitido, evidencia requerida, autovalidación, permiso, alcance y vigencia.
- **Reglas:** [E] Criterios verificables y versionados; autoridad combina política y permisos F2; no se deriva del nombre del puesto.
- **Resultados:** [E] Política y criterios resolubles; `obtener_politica_validacion`, `obtener_criterios_validacion`, `resolver_autoridad_validacion`.
- **Estados:** [E] Vigencia de política; estados propios [NE].
- **Excepciones:** [E] Política/criterio ausente, autoridad ambigua o conflicto de seguridad.
- **Permisos:** [E] Administración según F2; validar requiere `VALIDAR`.
- **Dependencias:** [E] F2, F4, F10–F11, F17.
- **Preguntas abiertas:** [E] F02-PRE-003, F02-PRE-016 y F02-PRE-017.

### CAP-033 — Validar ejecuciones y sustituir decisiones

- **Fuente y ubicación:** [E] FTE-027, `Validacion`, `EvaluacionCriterio`, `Funciones`, `Comandos y transacciones`, `Eventos`; FTE-028, `Historial_Validacion`, `Funciones_Comandos`, `Relaciones`; FTE-044, `05_Vistas_DTO` filas F12.
- **Objetivo:** [E] Emitir una decisión individual o por lote y conservar sustituciones auditables.
- **Actor:** [E] Usuario con `VALIDAR` y autoridad efectiva.
- **Disparador:** [E] Ejecución concluida pendiente de validación o corrección extraordinaria.
- **Entradas:** [E] Ejecución(es), resultado propuesto, evaluaciones de criterio, evidencias, actor y contexto.
- **Reglas:** [E] Gate de evidencia; alcance/autoridad; autovalidación según política; lote atómico según contrato; sustitución conserva decisión anterior.
- **Resultados:** [E] `puede_validar`, `evaluar_gate_evidencia_para_validacion`, `validar_ejecucion`, `validar_lote`, `sustituir_decision_validacion`; eventos correspondientes.
- **Estados:** [E] Derivado `PENDIENTE`, `CUMPLIDA`, `INCOMPLETA`, `NO_CUMPLIDA`.
- **Excepciones:** [E] Autoridad insuficiente, evidencia/gate incompleto, decisión vigente incompatible o período protegido.
- **Permisos:** [E] `VALIDAR`; sustitución extraordinaria [NE].
- **Dependencias:** [E] F2, F10–F11, F13–F17.
- **Preguntas abiertas:** [E] F02-PRE-003, F02-PRE-016, F02-PRE-017 y F02-PRE-022; F02-CON-004 y F02-CON-006.

### CAP-034 — Administrar políticas y causas de excepción

- **Fuente y ubicación:** [E] FTE-029, `PoliticaExcepcion`, `CausaExcepcion`, `Estados y transiciones`, `Funciones y reglas`; FTE-030, `Catalogo_Causas`, `Politica_202`, `Comandos_Reglas`, `Pendientes`.
- **Objetivo:** [E] Versionar tipos, causas, autoridad y efectos permitidos para excepciones del ciclo de vida.
- **Actor:** [E] Gobierno de excepción; rol concreto [NE].
- **Disparador:** [E] Publicación/cambio de política o causa.
- **Entradas:** [E] Tarea/contexto, tipo, causa, efectos, vigencia, requisitos y autoridad.
- **Reglas:** [E] Configuración estructurada; impacto económico no pertenece por defecto a F13; no inferir traslado desde frecuencia/criticidad.
- **Resultados:** [E] Política y catálogo resolubles para operaciones de excepción.
- **Estados:** [E] Vigencia de política; máquina específica [NE].
- **Excepciones:** [E] Política ausente, causa no permitida o efecto no autorizado.
- **Permisos:** [E] Gobierno conforme a F2; aplicar exige `AUTORIZAR_EXCEPCIONES`.
- **Dependencias:** [E] F2, F4, F10, F15, F17.
- **Preguntas abiertas:** [E] F02-PRE-018 y F02-PRE-019.

### CAP-035 — Cancelar, posponer, arrastrar o reasignar una obligación

- **Fuente y ubicación:** [E] FTE-029, `CambioCompromiso`, `Reasignacion`, `RelacionObligacion`, `Efectos por tipo`, `Comandos transaccionales`; FTE-030, `Casos_Arrastre`, `Reasignacion_Evidencia`, `Comandos_Reglas`.
- **Objetivo:** [E] Aplicar un efecto excepcional conservando identidad e historia de la obligación, salvo creación explícita de otra distinta.
- **Actor:** [E] Actor con `AUTORIZAR_EXCEPCIONES` según tipo/alcance; solicitante separado cuando aplique.
- **Disparador:** [E] Excepción autorizada o autoridad directa permitida por política.
- **Entradas:** [E] Obligación, tipo, causa, nueva fecha/responsable, motivo, autorización y contexto.
- **Reglas:** [E] Posponer/arrastrar/reasignar no crean automáticamente nueva instancia; reasignar revoca asignación anterior; tratamiento de ejecución activa debe ser explícito.
- **Resultados:** [E] `cancelar_obligacion`, `posponer_obligacion`, `arrastrar_obligacion`, `reasignar_obligacion`; cambios y eventos persistentes.
- **Estados:** [E] La excepción se rige por CAP-036; estados de ejecución/plan no se sobrescriben con etiquetas legacy.
- **Excepciones:** [E] Política/causa/autoridad ausente, período cerrado, ejecución activa sin política o compromiso inválido.
- **Permisos:** [E] `AUTORIZAR_EXCEPCIONES`.
- **Dependencias:** [E] F6, F8–F10, F14–F17.
- **Preguntas abiertas:** [E] F02-PRE-019 y F02-PRE-020; F02-CON-007.

### CAP-036 — Solicitar, autorizar, rechazar, aplicar y resolver excepciones

- **Fuente y ubicación:** [E] FTE-029, `ExcepcionCicloVida`, `DecisionExcepcion`, `IncidenciaOperativa`, `Estados y transiciones`, `resolver_excepcion`, `aplicar_excepciones_lote`, `Eventos`; FTE-030, `Modelo_Objetivo`, `Comandos_Reglas`, `Relaciones`.
- **Objetivo:** [E] Gobernar el expediente auditable de una excepción y su decisión individual o por lote.
- **Actor:** [E] Solicitante, autoridad de excepción y servicio transaccional; separación exacta por política.
- **Disparador:** [E] Solicitud o detección de excepción.
- **Entradas:** [E] Obligación, tipo, causa, evidencia/contexto, decisión, actor y autorización.
- **Reglas:** [E] Historial append-only; autorizar/aplicar en una transacción sólo con autoridad directa; revocación extraordinaria.
- **Resultados:** [E] `resolver_excepcion`, `aplicar_excepciones_lote`; eventos de solicitud, autorización, rechazo, aplicación e incidencia.
- **Estados:** [E] `SOLICITADA → AUTORIZADA | RECHAZADA → APLICADA`; `REVOCADA` sólo por ruta extraordinaria.
- **Excepciones:** [E] Transición inválida, lote parcial, política o autoridad faltante.
- **Permisos:** [E] `AUTORIZAR_EXCEPCIONES`.
- **Dependencias:** [E] F2, F10, F14, F17.
- **Preguntas abiertas:** [E] F02-PRE-018 a F02-PRE-020 y F02-PRE-022.

### CAP-037 — Evaluar gates de cierre del período

- **Fuente y ubicación:** [E] FTE-031, `PoliticaCierrePeriodo`, `EvaluacionCierrePeriodo`, `Gates de cierre`, `Funciones y reglas`; FTE-032, `Gates_Cierre`, `Comandos_Reglas`, `Pendientes`; FTE-044, `05_Vistas_DTO` filas F14.
- **Objetivo:** [E] Determinar si todas las obligaciones tienen tratamiento terminal válido y si el período puede cerrarse.
- **Actor:** [E] Servicio de evaluación y usuario de control semanal.
- **Disparador:** [E] Consulta o intento de cierre.
- **Entradas:** [E] Período, política vigente, ejecuciones, evidencias, validaciones, excepciones y pendientes.
- **Reglas:** [E] Evaluar el conjunto completo; no basta con ausencia de `EN_PROCESO`; resultado derivado, no tabla por defecto.
- **Resultados:** [E] Evaluación explicable, pendientes y evento `PeriodoListoParaCierre` cuando corresponda.
- **Estados:** [E] Listo/no listo derivado; estado del período permanece propio de F3/F14.
- **Excepciones:** [E] Política ausente, gate incompleto, datos inconsistentes o excepción sin resolver.
- **Permisos:** [E] Consulta según alcance; cierre requiere `CERRAR_PERIODO`.
- **Dependencias:** [E] F3, F9–F13, F17.
- **Preguntas abiertas:** [E] F02-PRE-021.

### CAP-038 — Cerrar período, preparar el siguiente y corregir postcierre

- **Fuente y ubicación:** [E] FTE-031, `CierrePeriodo`, `Máquina de estado`, `Comandos transaccionales`, `Rollback y manejo de errores`, `Protección postcierre`; FTE-032, `Transiciones`, `Cierres_Historicos`, `Logs_Cierre`, `Pendientes`.
- **Objetivo:** [E] Cerrar atómicamente un período aprobado, impedir mutación ordinaria y habilitar continuidad sin reabrir la misma identidad.
- **Actor:** [E] Autoridad explícita con `CERRAR_PERIODO`; corrección postcierre requiere autoridad reforzada [NE].
- **Disparador:** [E] Gates satisfechos; preparación del siguiente ciclo; necesidad extraordinaria de corrección.
- **Entradas:** [E] Período, evaluación, actor, motivo, operación de corrección/compensación.
- **Reglas:** [E] `CERRADA` terminal; rollback técnico no es transición; siguiente ciclo usa otro período; corrección no equivale a reapertura.
- **Resultados:** [E] `cerrar_periodo`, `preparar_periodo_siguiente`, `corregir_postcierre`; eventos `PeriodoCerrado`, `CorreccionPostCierreRegistrada`.
- **Estados:** [E] Período llega a `CERRADA`; estado postcierre derivado.
- **Excepciones:** [E] Gate fallido, concurrencia, intento de reapertura o corrección no autorizada.
- **Permisos:** [E] `CERRAR_PERIODO`; permiso extraordinario de corrección [NE].
- **Dependencias:** [E] F2–F3, F9–F13, F17–F18.
- **Preguntas abiertas:** [E] F02-PRE-021 y F02-PRE-022; F02-CON-005 y F02-CON-008.

### CAP-039 — Definir y medir KPI

- **Fuente y ubicación:** [E] FTE-033, `DefinicionKPI`, `MedicionKPI`, `Reglas de cálculo`, `KPI`, `Funciones y reglas`; FTE-034, `02_KPI_Actual`, `05_Reglas_Gates`, `06_Modelo_Objetivo`.
- **Objetivo:** [E] Mantener definiciones ejecutables y producir mediciones reproducibles por sujeto/período.
- **Actor:** [E] Gobierno de KPI para definición; servicio de cálculo para medición.
- **Disparador:** [E] Publicación de KPI o cierre/corte de medición.
- **Entradas:** [E] Fórmula, unidad, fuente, periodicidad, vigencia, población, período y hechos canónicos.
- **Reglas:** [E] KPI atómico; medición persiste cuando participa en incentivos/auditoría/cierre o no es reproducible con garantías.
- **Resultados:** [E] Definición versionada, medición y evento `MedicionKPICalculada`.
- **Estados:** [E] Vigencia de definición; estado de medición [NE].
- **Excepciones:** [E] Fórmula/fuente/unidad incompleta o datos no reconciliados.
- **Permisos:** [E] Gobierno económico; código específico [NE].
- **Dependencias:** [E] F4, F10–F14, F17–F18.
- **Preguntas abiertas:** [E] F02-PRE-024 y F02-PRE-030.

### CAP-040 — Liquidar, aprobar o corregir incentivos

- **Fuente y ubicación:** [E] FTE-033, `PoliticaIncentivo`, `AsignacionPoliticaIncentivo`, `ResultadoIncentivo`, `Elegibilidad económica`, `Cálculo de incentivo`, `Comandos transaccionales`; FTE-034, `05_Reglas_Gates`, `06_Modelo_Objetivo`.
- **Objetivo:** [E] Calcular un resultado económico versionado y someterlo a aprobación/corrección auditables.
- **Actor:** [E] Servicio de liquidación y autoridad con `APROBAR_INCENTIVOS`.
- **Disparador:** [E] Corte de período, aprobación o ajuste autorizado.
- **Entradas:** [E] Política, población, KPI/resultado operativo, período, topes, excepciones y actor.
- **Reglas:** [E] Política económica explícita/versionada; clasificación operativa no se convierte automáticamente en dinero; ajustes conservan histórico.
- **Resultados:** [E] `liquidar_incentivos_periodo`, `aprobar_resultado_incentivo`, `anular_o_corregir_resultado_incentivo`; eventos económicos.
- **Estados:** [E] Calculado/requiere revisión/aprobado según eventos; máquina completa [NE].
- **Excepciones:** [E] Política ausente, cálculo no reproducible, gate de cierre o aprobación faltante.
- **Permisos:** [E] `APROBAR_INCENTIVOS`; segregación de funciones.
- **Dependencias:** [E] F2, F12–F14, F17.
- **Preguntas abiertas:** [E] F02-PRE-023 y F02-PRE-024.

### CAP-041 — Preparar y confirmar movimientos de nómina

- **Fuente y ubicación:** [E] FTE-033, `MovimientoNomina`, `LoteNomina`, `Integración con nómina`, `preparar_movimientos_nomina`; FTE-034, `03_Nomina_Periodo`, `04_Kardex_Activo`, `06_Modelo_Objetivo`.
- **Objetivo:** [E] Convertir resultados aprobados en instrucciones económicas conciliables para el sistema de nómina.
- **Actor:** [E] Autoridad económica y adaptador de nómina.
- **Disparador:** [E] Resultados aprobados listos para un corte.
- **Entradas:** [E] Resultados, empleado, concepto, importe, período/corte, lote y referencia externa.
- **Reglas:** [E] Sólo resultados aprobados; lote cuando el receptor lo requiera; cruce externo auditable e idempotente.
- **Resultados:** [E] `preparar_movimientos_nomina`; movimientos/lote; eventos `MovimientoNominaPreparado` y `MovimientoNominaConfirmado`.
- **Estados:** [E] Preparado/confirmado según eventos; otros estados [NE].
- **Excepciones:** [E] Empleado/concepto no mapeado, duplicado, rechazo o conciliación fallida.
- **Permisos:** [E] `APROBAR_INCENTIVOS` y `ADMIN_INTEGRACIONES` según frontera.
- **Dependencias:** [E] F1–F2, F15, F17, F19.
- **Preguntas abiertas:** [E] F02-PRE-023, F02-PRE-033 y F02-PRE-037.

### CAP-042 — Consultar inicio y bandeja personal de trabajo

- **Fuente y ubicación:** [E] FTE-035, `Inicio operativo`, `Bandeja personal de trabajo`, `ViewInicioUsuario`, `ViewTareasUsuario`, `Acciones expuestas por la UX`; FTE-036, `03_UX_Roles`, `04_Acciones_UX`, `07_Modelo_Objetivo`.
- **Objetivo:** [E] Mostrar al usuario período, alertas, tareas, estado, vencimiento, evidencia y acciones permitidas dentro de su alcance.
- **Actor:** [E] Personal operativo y demás usuarios autenticados.
- **Disparador:** [E] Inicio de sesión, apertura o actualización de bandeja.
- **Entradas:** [E] Usuario, roles/alcance, período y fuentes canónicas de plan, asignación, ejecución, evidencia y validación.
- **Reglas:** [E] La vista no duplica autoridad; acciones se calculan por permiso/estado; usar IDs canónicos.
- **Resultados:** [E] `ViewInicioUsuario`, `ViewTareasUsuario` y funciones de lectura correspondientes.
- **Estados:** [E] Presentación normalizada de estados de dominios propietarios; no crea estado global.
- **Excepciones:** [E] Acceso denegado, modelo de lectura desactualizado o acción no permitida.
- **Permisos:** [E] `CONSULTAR_OPERACION`; comandos conservan sus permisos de dominio.
- **Dependencias:** [E] F2–F15, F17/F19.
- **Preguntas abiertas:** [E] Vista de equipo para Piso y alcance exacto de equipo; no bloquean esta ficha.

### CAP-043 — Consultar matrices, validación y controles semanales

- **Fuente y ubicación:** [E] FTE-035, `Matriz operativa de área`, `Bandeja de validación`, `Control semanal`, `Control intersemanal de excepciones`, modelos de lectura asociados; FTE-036, `03_UX_Roles`, `04_Acciones_UX`, `05_Indicadores`, `08_Pendientes`.
- **Objetivo:** [E] Proveer vistas de equipo, carga, validación, plan, asignaciones, cierre y excepciones según autoridad.
- **Actor:** [E] Subcoordinación, administración y validadores; usuarios de Piso sólo si se aprueba.
- **Disparador:** [E] Consulta operativa, validación o control de período.
- **Entradas:** [E] Alcance efectivo, período y datos F8–F14.
- **Reglas:** [E] Sólo objetos dentro del alcance; diagnósticos visibles según permiso; porcentajes requieren denominador explícito.
- **Resultados:** [E] `ViewMatrizArea`, `ViewResumenCargaColaborador`, `ViewPendientesValidacion`, `ViewPlanPeriodo`, `ViewAsignacionesPeriodo`, `ViewEstadoCierrePeriodo`, `ViewExcepcionesIntersemanales`.
- **Estados:** [E] Proyecciones de estados propietarios.
- **Excepciones:** [E] Alcance ambiguo, autoridad de validación conflictiva o denominador no definido.
- **Permisos:** [E] `CONSULTAR_OPERACION`, `VALIDAR` y permisos de acción correspondientes.
- **Dependencias:** [E] F2, F8–F14, F17.
- **Preguntas abiertas:** [E] F02-PRE-003; denominadores oficiales, alcance de equipo y vista de Piso [NE].

### CAP-044 — Consultar dirección, KPI, auditoría y exportaciones

- **Fuente y ubicación:** [E] FTE-035, `Dirección`, `ViewDireccionEjecutiva`, `ViewKPIIncentivos`, `Reportes y exportaciones`, `Permisos`; FTE-036, `02_Vistas_Actuales`, `05_Indicadores`, `07_Modelo_Objetivo`.
- **Objetivo:** [E] Entregar indicadores y exportaciones autorizadas sin crear fuentes paralelas de negocio.
- **Actor:** [E] Dirección y administración según sensibilidad/alcance.
- **Disparador:** [E] Consulta, descarga o revisión ejecutiva.
- **Entradas:** [E] Alcance, período, métricas F9–F15 y referencias de auditoría/integración.
- **Reglas:** [E] Definiciones de indicador explícitas; fuentes canónicas; exportación respeta seguridad y sensibilidad.
- **Resultados:** [E] `ViewDireccionEjecutiva`, `ViewKPIIncentivos`, reportes/exportaciones documentados.
- **Estados:** [NE] No especificado; sólo presenta estados de origen.
- **Excepciones:** [E] Acceso insuficiente, indicador no definible o datos no reconciliados.
- **Permisos:** [E] `CONSULTAR_OPERACION`, `CONSULTAR_AUDITORIA` y autoridad económica cuando corresponda.
- **Dependencias:** [E] F2, F9–F17, F19.
- **Preguntas abiertas:** [E] Denominadores oficiales, auditoría ejecutiva y salud de integraciones [NE].

### CAP-045 — Registrar y consultar auditoría trazable

- **Fuente y ubicación:** [E] FTE-037, `EventoAuditoria`, `Política de auditoría por dominio`, `registrar_evento_auditoria`, vistas de lectura; FTE-038, `02_LOG_Actual`, `05_Auditoria_QA`, `06_Modelo_Objetivo`; FTE-044, `05_Vistas_DTO` filas F17.
- **Objetivo:** [E] Registrar hechos auditables y reconstruir la historia de una entidad sin usar auditoría como estado actual.
- **Actor:** [E] Comandos de todos los dominios como productores; usuarios ejecutivos/técnicos autorizados como lectores.
- **Disparador:** [E] Cambio/decisión auditable o consulta.
- **Entradas:** [E] Entidad, operación, actor, timestamp, antes/después permitido, correlación y contexto.
- **Reglas:** [E] Append-only; datos sensibles/payloads mínimos; correlación; política por dominio.
- **Resultados:** [E] `registrar_evento_auditoria`, `ViewAuditoriaEjecutiva`, `ViewTrazabilidadEntidad`.
- **Estados:** [NE] Evento inmutable; no máquina propia.
- **Excepciones:** [E] Error de registro, datos sensibles excesivos o correlación ausente.
- **Permisos:** [E] `CONSULTAR_AUDITORIA` según sensibilidad.
- **Dependencias:** [E] Todos los dominios.
- **Preguntas abiertas:** [E] F02-PRE-025 y política de acceso/retención [NE].

### CAP-046 — Gestionar corridas, idempotencia y errores

- **Fuente y ubicación:** [E] FTE-037, `CorridaSistema`, `RegistroIdempotencia`, `ErrorSistema`, `Servicios y funciones`, `Patrón transaccional`; FTE-038, `03_Corridas_Lotes`, `06_Modelo_Objetivo`, `07_Reglas_Transaccionales`; FTE-044, `07_Estados` filas F17.
- **Objetivo:** [E] Controlar ejecuciones técnicas, reintentos, claves idempotentes y errores correlacionados.
- **Actor:** [E] Servicios internos e integraciones; operador técnico autorizado para consulta.
- **Disparador:** [E] Inicio/fin/fallo de corrida, operación reintentable o error relevante.
- **Entradas:** [E] Tipo, contexto, clave, hash, resultado, conteos, error e IDs de operación/correlación.
- **Reglas:** [E] Reintento crea corrida relacionada; no sobrescribe fallo; misma clave con payload incompatible rechaza; frontera transaccional.
- **Resultados:** [E] `iniciar/completar/fallar_corrida`, `reservar/confirmar_idempotencia`, `registrar_error`, vistas de corridas/errores.
- **Estados:** [E] Corrida `INICIADA → COMPLETADA | FALLIDA | CANCELADA`; idempotencia `EN_PROCESO → COMPLETADA | FALLIDA`.
- **Excepciones:** [E] Clave conflictiva, corrida inexistente, estado inválido o error no correlacionado.
- **Permisos:** [E] `CONSULTAR_AUDITORIA`; ejecución por servicios autorizados.
- **Dependencias:** [E] Todos los comandos modificadores, especialmente F5–F6 y F18–F19.
- **Preguntas abiertas:** [E] Retención, índices, particiones y política de reintento tras clave fallida; F02-PRE-025.

### CAP-047 — Crear puntos de recuperación, respaldar, restaurar y compensar

- **Fuente y ubicación:** [E] FTE-037, `PuntoRecuperacion`, `Respaldo, recuperación y rollback`, `crear/verificar/restaurar_punto_recuperacion`, `Permisos`; FTE-038, `04_Rollback_Actual`, `07_Reglas_Transaccionales`, `08_Pendientes`.
- **Objetivo:** [E] Proteger la operación distinguiendo rollback transaccional, compensación, respaldo y restauración.
- **Actor:** [E] Operador técnico/autorizador de recuperación; identidades concretas [NE].
- **Disparador:** [E] Preparación preventiva, fallo, prueba o restauración autorizada.
- **Entradas:** [E] Alcance, punto de recuperación, autorización, política de respaldo y contexto.
- **Reglas:** [E] Punto es metadato, no copia de negocio; restauración requiere verificación y autorización; compensar no reescribe historia.
- **Resultados:** [E] `crear_punto_recuperacion`, `verificar_punto_recuperacion`, `restaurar_punto_recuperacion`; rollback/compensación conforme a dominio.
- **Estados:** [NE] No especificado para punto; corrida asociada usa CAP-046.
- **Excepciones:** [E] Punto inválido, autorización insuficiente, prueba fallida o objetivo incompatible.
- **Permisos:** [E] Autoridad técnica reforzada; código específico [NE].
- **Dependencias:** [E] F14 y todos los comandos transaccionales.
- **Preguntas abiertas:** [E] F02-PRE-026.

### CAP-048 — Migrar y reconciliar datos legacy

- **Fuente y ubicación:** [E] FTE-039, `Tipos de relación de migración`, `ReferenciaOrigenLegacy`, `MapaEquivalenciaMigracion`, `Servicios y funciones`, `Migración de definiciones`, `Migración de obligaciones`; FTE-040, `01_Matriz_Migracion`, `02_Resoluciones_S050`, `03_Cobertura_Legacy`, `04_Historico_EJEC`, `08_Pendientes`.
- **Objetivo:** [E] Transformar lotes con procedencia, equivalencias y reconciliación explícitas sin inventar datos ausentes.
- **Actor:** [E] Servicio de migración y autoridad de datos.
- **Disparador:** [E] Corrida de prueba/producción o reconciliación.
- **Entradas:** [E] Registros legacy, mapa versionado, contexto, referencias de origen y reglas de validación.
- **Reglas:** [E] Tipos `UNO_A_UNO`, `VARIOS_A_UNO`, `UNO_A_VARIOS`, `CAMBIO_DE_TIPO`, `SIN_DESTINO`; preservar calidad/procedencia; no fabricar PLAN/timestamps.
- **Resultados:** [E] `resolver_equivalencia`, `transformar_registro_legacy`, `validar_registro_migrado`, `migrar_lote`, `reconciliar_lote`.
- **Estados:** [E] `PENDIENTE | MAPEO_UNICO | AMBIGUO | TRANSFORMADO | RECONCILIADO | EXCLUIDO_JUSTIFICADO | ERROR`.
- **Excepciones:** [E] Mapa ambiguo/sin destino, referencia ausente, validación o reconciliación fallida.
- **Permisos:** [E] Servicio autorizado; cutover separado.
- **Dependencias:** [E] F1–F17 y F19.
- **Preguntas abiertas:** [E] F02-PRE-027 a F02-PRE-030; F02-CON-003, F02-CON-004 y F02-CON-007 a F02-CON-009.

### CAP-049 — Gobernar coexistencia y cutover

- **Fuente y ubicación:** [E] FTE-039, `ControlCoexistencia`, `Prevención de doble generación`, `Gates de cutover`, `autorizar_cutover`, `retirar_productor_legacy`; FTE-040, `05_Guardas_Coexistencia`, `06_Gates_Cutover`, `07_Modelo_Objetivo`.
- **Objetivo:** [E] Asegurar un único productor autorizado por alcance durante transición y retirar el legacy sólo tras gates.
- **Actor:** [E] Autoridad con `AUTORIZAR_CUTOVER` y servicios productores.
- **Disparador:** [E] Inicio/cambio de coexistencia, evaluación de gates o retiro.
- **Entradas:** [E] Alcance, productor actual/objetivo, cobertura, reconciliación, pruebas y autorización.
- **Reglas:** [E] No doble generación; cutover por alcance; estructuras temporales se retiran o archivan sólo con decisión.
- **Resultados:** [E] `validar_no_doble_generacion`, `autorizar_cutover`, `retirar_productor_legacy`.
- **Estados:** [E] Control temporal de autoridad; máquina exacta [NE].
- **Excepciones:** [E] Gates incompletos, cobertura ambigua, productor doble o rollback no autorizado.
- **Permisos:** [E] `AUTORIZAR_CUTOVER`.
- **Dependencias:** [E] F5–F6, F17–F19.
- **Preguntas abiertas:** [E] F02-PRE-006 y F02-PRE-027 a F02-PRE-030.

### CAP-050 — Configurar y operar integraciones externas

- **Fuente y ubicación:** [E] FTE-041, `SistemaExterno`, `ConfiguracionIntegracion`, `LoteIntegracion`, `MensajeIntegracion`, `ReferenciaExterna`, `CheckpointSincronizacion`, `Componentes lógicos`, `Estados`, `Reglas de idempotencia`; FTE-042, `HUELLA_TECNICA`, `SISTEMAS_CANDIDATOS`, `REFERENCIAS_OPERATIVAS`, `CLASIFICACION_OBJETIVO`, `PENDIENTES`; FTE-044, `07_Estados` fila F19.
- **Objetivo:** [E] Intercambiar datos/comandos mediante contratos versionados, trazables, idempotentes y sometidos a reglas internas.
- **Actor:** [E] Administrador con `ADMIN_INTEGRACIONES`, adaptador y sistema externo.
- **Disparador:** [E] Mensaje/evento, lote, comando saliente, reintento o reconciliación.
- **Entradas:** [E] Sistema/configuración, esquema/versión, IDs externos, payload, correlación, checkpoint, alcance y credenciales fuera de tablas de negocio.
- **Reglas:** [E] Ninguna integración salta permisos/gates/estados; validar/normalizar; idempotencia; reintentos sólo transitorios; trazabilidad de referencias.
- **Resultados:** [E] `adaptador_sistema_externo`, `validar_mensaje_integracion`, `normalizar_evento_externo`, `procesar_evento_externo`, `importar_lote`, `exportar_lote`, `enviar_comando_externo`, `reintentar_mensaje`, `reconciliar_integracion`.
- **Estados:** [E] Entrada: recibido/validado/aplicado/rechazado; salida: pendiente_envio/enviado/confirmado/fallido.
- **Excepciones:** [E] Contrato inválido, rechazo, duplicado, fallo permanente/transitorio, dead-letter o reconciliación fallida.
- **Permisos:** [E] `ADMIN_INTEGRACIONES`; autoridad de negocio adicional según operación.
- **Dependencias:** [E] F1–F18 y sistemas externos aún por identificar.
- **Preguntas abiertas:** [E] F02-PRE-031 a F02-PRE-038.

## 5. Reglas transversales normalizadas

| ID | Regla explícita | Fuente y ubicación |
|---|---|---|
| REG-001 | Empleado activo, disponibilidad y capacidad son conceptos distintos. | FTE-043, `10. Reglas de negocio`, punto 1; FTE-005, `Principios/Reglas` |
| REG-002 | Rol, puesto y responsabilidad operativa no son equivalentes automáticos. | FTE-043, punto 2; FTE-007, `Principios obligatorios` |
| REG-003 | F5 solicita y F6 crea la obligación; activación no crea plan ni ejecución. | FTE-043, punto 3; FTE-013/FTE-015 |
| REG-004 | SLA es regla temporal superpuesta, no tipo de activación. | FTE-043, punto 4; FTE-013, `Principios obligatorios` |
| REG-005 | Elegibilidad determina candidatos; asignación selecciona y persiste responsable. | FTE-043, punto 5; FTE-017/FTE-019 |
| REG-006 | Instancia, plan/partida, asignación y ejecución tienen identidades independientes. | FTE-043, punto 6; FTE-015/FTE-019/FTE-021/FTE-023 |
| REG-007 | La ejecución existe sólo cuando el trabajo se inicia realmente. | FTE-043, punto 7; FTE-023, `Inicio de ejecución` |
| REG-008 | Evidencia, validación y excepción son hechos independientes. | FTE-043, punto 8; FTE-025/FTE-027/FTE-029 |
| REG-009 | Posponer, arrastrar o reasignar conserva identidad salvo obligación distinta explícita. | FTE-043, punto 9; FTE-029 |
| REG-010 | El cierre evalúa todas las obligaciones y exige tratamiento terminal válido. | FTE-043, punto 10; FTE-031 |
| REG-011 | KPI e incentivos requieren políticas versionadas; no se deriva dinero de una clasificación operativa. | FTE-043, punto 11; FTE-033 |
| REG-012 | Las vistas derivan de fuentes canónicas y no duplican autoridad. | FTE-043, punto 12; FTE-035 |
| REG-013 | Respaldo, rollback transaccional y compensación son mecanismos distintos. | FTE-043, punto 13; FTE-037 |
| REG-014 | Migración no autoriza doble generación. | FTE-043, punto 14; FTE-039 |
| REG-015 | Una integración externa no puede omitir permisos, gates ni estados internos. | FTE-043, punto 15; FTE-041 |

## 6. Cobertura de datos, estados, permisos y lógica

- **Datos:** las entidades persistentes y vistas asociadas a cada capacidad se ubican en FTE-043, `6. Modelo relacional`, `7. Diccionario de entidades` y `23. Reportes y vistas de usuario`, y en FTE-044, hojas `01_Tablas_SQL`, `05_Vistas_DTO` y `13_Matriz_Consolidacion`.
- **Lógica:** las operaciones citadas por las fichas cubren los grupos de FTE-043, `11. Catálogo de funciones`, `12. Catálogo de procedimientos y comandos` y `13. Eventos y automatizaciones`, corroborados por FTE-044, `06_Logica` (283 registros). Una operación auxiliar no citada nominalmente queda cubierta por la ficha del mismo dominio y resultado; esto es una [I] clasificación, no una exclusión funcional.
- **Uso operativo:** no se presume por presencia técnica. Cuando las fuentes no demuestran que una operación se utiliza o responde a una necesidad del negocio, debe marcarse como no comprobada y no conservarse automáticamente durante la consolidación.
- **Estados:** se conservan máquinas por entidad; no se crea un estado global. Fuente: FTE-043, `14. Máquinas de estado`; FTE-044, `07_Estados`.
- **Permisos:** se conservan 17 capacidades lógicas de seguridad y denegación por defecto. Fuente: FTE-043, `15. Modelo de permisos y autoridad`; FTE-044, `08_Permisos`.
- **Errores:** los códigos específicos permanecen en sus dominios y en FTE-044, `09_Errores`; no se inventó una taxonomía nueva.

## 7. Validación del catálogo

| Criterio | Resultado |
|---|---|
| Todas las capacidades tienen ID estable | Cumplido: CAP-001 a CAP-050 |
| Todas tienen fuente y ubicación | Cumplido: cada ficha cita sección Markdown y hoja Excel |
| Todos los campos solicitados están presentes | Cumplido; se usa `[NE] No especificado` donde corresponde |
| Inferencias separadas de hechos | Cumplido mediante etiquetas `[E]`, `[I]`, `[NE]` |
| Reglas, estados, permisos y errores no se mezclan entre dominios | Cumplido |
| No se definió MVP ni tecnología | Cumplido |
| Preguntas y contradicciones visibles | Cumplido mediante referencias al entregable específico |
