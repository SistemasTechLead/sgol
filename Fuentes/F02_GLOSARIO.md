# F02 — Glosario consolidado de SGOL

| Campo | Valor |
|---|---|
| Estado | Aprobado e incorporado a las fuentes del proyecto |
| Fecha de aprobación e incorporación | 2026-08-26 |

## 1. Convenciones

- Los términos se normalizan para evitar equivalencias silenciosas entre conceptos distintos.
- **[E]** significa definición explícita en las fuentes; **[I]** significa síntesis documental sin agregar reglas; **[NE]** significa no especificado.
- La forma técnica en `monoespaciado` conserva el nombre canónico documentado; no implica selección de tecnología.
- Fuente transversal: FTE-043, `5. Modelo de dominios`, `7. Diccionario de entidades`, `8. Catálogos` y `10. Reglas de negocio`; FTE-044, `01_Tablas_SQL`, `05_Vistas_DTO` y `13_Matriz_Consolidacion`.

## 2. Términos

| ID | Término | Definición normalizada | Certeza | Fuente principal y ubicación |
|---|---|---|---|---|
| GLO-001 | SGOL | Sistema de gestión operativa que modela personas, configuración, obligaciones, planificación, ejecución, control y trazabilidad. | [E] | FTE-043, `1. Propósito del SGOL` |
| GLO-002 | Fuente canónica | Registro o dominio propietario desde el cual debe derivarse un dato, evitando duplicados con autoridad propia. | [I] | FTE-043, `3. Principios operativos`, `10. Reglas de negocio` |
| GLO-003 | Alcance efectivo | Conjunto de organización, sucursales y/o áreas sobre el que un usuario puede ejercer un permiso vigente. | [E] | FTE-007, `AsignacionUsuarioRol`, `Reglas de autorización y acceso` |
| GLO-004 | Organización | Unidad empresarial propietaria de la operación. | [E] | FTE-007, `Organización` |
| GLO-005 | Sucursal | Ubicación o unidad operativa con identidad y código propios; “TODAS” no es una sucursal. | [E] | FTE-007, `Sucursal` |
| GLO-006 | Área organizacional | Catálogo jerárquico de funciones de negocio, corporativo o asociado a una sucursal. | [E] | FTE-007, `AreaOrganizacional` |
| GLO-007 | Empleado | Identidad laboral estable, distinta de la identidad de acceso. | [E] | FTE-005, `Empleado` |
| GLO-008 | Usuario | Identidad habilitada para acceder a SGOL, vinculable opcionalmente a un empleado. | [E] | FTE-007, `Usuario` |
| GLO-009 | Puesto | Catálogo de función laboral/operativa; no equivale automáticamente a rol de seguridad. | [E] | FTE-005, `Puesto`; FTE-007, `Principios obligatorios` |
| GLO-010 | Turno | Jornada o ventana real asignable a una persona; “sin restricción” no es un turno trabajado. | [E] | FTE-005, `Turno` |
| GLO-011 | Asignación de puesto | Hecho histórico que vincula empleado y puesto durante un intervalo de vigencia. | [E] | FTE-005, `AsignacionPuestoEmpleado` |
| GLO-012 | Asignación de turno | Hecho histórico que vincula empleado y turno durante un intervalo de vigencia. | [E] | FTE-005, `AsignacionTurnoEmpleado` |
| GLO-013 | Disponibilidad operativa | Fracción de tiempo que un empleado puede dedicar a tareas SGOL por período o día; no equivale a estado laboral. | [E] | FTE-005, `DisponibilidadEmpleadoPeriodo`, `DisponibilidadEmpleadoDia` |
| GLO-014 | Política de capacidad | Configuración versionada que limita la porción de disponibilidad asignable según puesto y contexto. | [E] | FTE-005, `PoliticaCapacidadPuesto` |
| GLO-015 | Capacidad operativa | Cálculo derivado de minutos disponibles, asignados, libres, utilización y límite asignable. | [E] | FTE-005, `Capacidad operativa` |
| GLO-016 | Estado de capacidad | Clasificación derivada `DISPONIBLE`, `ADECUADO` o `SATURADO`; no es estado persistente. | [E] | FTE-005, `Estado de capacidad` |
| GLO-017 | Rol de seguridad | Agrupación de permisos; no sustituye puesto ni responsabilidad operativa. | [E] | FTE-007, `RolSeguridad` |
| GLO-018 | Permiso | Capacidad atómica concedida a uno o varios roles y evaluada con contexto/alcance. | [E] | FTE-007, `Permiso` |
| GLO-019 | Asignación usuario–rol | Hecho histórico que concede un rol a un usuario con vigencia y alcance. | [E] | FTE-007, `AsignacionUsuarioRol` |
| GLO-020 | Equivalencia puesto–rol | Regla configurable que relaciona puesto y rol cuando el negocio lo necesita; no es identidad automática. | [E] | FTE-007, `EquivalenciaPuestoRol` |
| GLO-021 | Autorización | Decisión excepcional, explícita y auditable para una operación y objeto concretos; no reemplaza permisos permanentes. | [E] | FTE-007, `Autorizacion` |
| GLO-022 | Denegación por defecto | Regla por la que la ausencia de permiso equivale a denegación. | [E] | FTE-007, `Principios obligatorios` |
| GLO-023 | Configuración operativa | Parámetro tipado, con alcance y vigencia, que puede ser resuelto sin constantes ocultas. | [E] | FTE-009, `ConfiguracionOperativa` |
| GLO-024 | Calendario operativo | Conjunto de reglas y excepciones que determina días y cómputos operativos por alcance. | [E] | FTE-009, `CalendarioOperativo` |
| GLO-025 | Excepción de calendario | Alteración explícita de la regla recurrente para una fecha y alcance. | [E] | FTE-009, `ExcepcionCalendario` |
| GLO-026 | Período operativo | Identidad temporal, normalmente semanal, con alcance y ciclo de estados propio. | [E] | FTE-009, `PeriodoOperativo` |
| GLO-027 | Semana ISO | Convención para obtener año/semana y límites del período; la fuente exige retirar cálculos no ISO como autoridad. | [E] | FTE-009, `Funciones puras`; FTE-010, `DIVERGENCIA_ISO` |
| GLO-028 | Macroproceso | Nivel superior de la jerarquía operativa. | [E] | FTE-011, `Macroproceso` |
| GLO-029 | Proceso | Nivel de jerarquía subordinado a macroproceso. | [E] | FTE-011, `Proceso` |
| GLO-030 | Subproceso | Clasificación primaria inmediata de una definición de tarea. | [E] | FTE-011, `Subproceso` |
| GLO-031 | Definición de tarea | Identidad `TAR-####` de una obligación operativa genuinamente distinta. | [E] | FTE-011, `DefinicionTarea` |
| GLO-032 | Versión de definición | Representación vigente/histórica de la semántica de una tarea sin alterar obligaciones anteriores. | [E] | FTE-011, `VersionDefinicionTarea` |
| GLO-033 | Flujo operativo | Agrupación versionable de tareas dentro de una secuencia o contexto operativo. | [E] | FTE-011, `FlujoOperativo`, `FlujoTarea` |
| GLO-034 | Relación de tarea | Vínculo dirigido y tipado entre definiciones; pertenecer al mismo flujo no lo sustituye. | [E] | FTE-011, `RelacionTarea` |
| GLO-035 | Checklist | Definición versionada y conjunto ordenado de ítems evaluables asociados a una tarea. | [E] | FTE-011, `ChecklistDefinicion`, `ChecklistItem` |
| GLO-036 | Regla configurable | Regla persistida sólo cuando el negocio necesita administrarla; no contiene código libre ejecutable. | [E] | FTE-011, `ReglaConfigurable` |
| GLO-037 | Parámetro de regla | Valor tipado, con unidad, alcance y vigencia, asociado a una regla o dominio. | [E] | FTE-011, `ParametroRegla` |
| GLO-038 | KPI | Definición atómica de una medición con fórmula, unidad, fuente, periodicidad y vigencia. | [E] | FTE-011, `IndicadorKPI`; FTE-033, `DefinicionKPI` |
| GLO-039 | Automatización | Componente ejecutable real, gobernable y versionado; un potencial o proyecto no basta para crearlo. | [E] | FTE-011, `Automatizacion` |
| GLO-040 | Regla de activación | Configuración versionada que determina el mecanismo por el que una tarea puede solicitar trabajo. | [E] | FTE-013, `ReglaActivacion` |
| GLO-041 | Activación programada | Evaluación basada en calendario, fechas y/o ventanas estructuradas. | [E] | FTE-013, `ProgramacionActivacion` |
| GLO-042 | Activación por evento | Evaluación de un hecho identificado, correlacionado y trazable. | [E] | FTE-013, `ReglaEvento` |
| GLO-043 | Activación condicional | Evaluación de un predicado estructurado sobre un contexto identificado. | [E] | FTE-013, `ReglaCondicion` |
| GLO-044 | Activación por proceso | Evaluación asociada a un caso origen con identidad y timestamp. | [E] | FTE-013, `ReglaProcesoOrigen` |
| GLO-045 | SLA | Regla temporal para calcular vencimientos/momentos objetivo; no es tipo de activación. | [E] | FTE-013, `Principios obligatorios`, `ReglaTemporal` |
| GLO-046 | Evaluación de activación | Resultado explicable de aplicar una regla a un contexto; puede no cumplir o quedar bloqueado. | [E] | FTE-013, `EvaluacionActivacion` |
| GLO-047 | Solicitud de activación | Contrato idempotente que F5 entrega a F6; todavía no es una obligación persistente. | [E] | FTE-013, `SolicitudActivacion` |
| GLO-048 | Clave de idempotencia | Clave determinista de la obligación lógica que impide duplicados ante reintentos. | [E] | FTE-013, `Reglas de idempotencia`; FTE-015, `Identidad e idempotencia` |
| GLO-049 | Instancia de trabajo | Obligación canónica persistente creada por F6, independiente de plan, asignación y ejecución. | [E] | FTE-015, `Entidad definitiva: InstanciaTrabajo` |
| GLO-050 | Elegibilidad | Evaluación que determina y explica quiénes pueden ejecutar una instancia; no selecciona responsable. | [E] | FTE-017, `Propósito del dominio`, `Criterios de elegibilidad` |
| GLO-051 | Candidato elegible | Empleado que satisface los criterios habilitados por la política para una instancia y momento. | [E] | FTE-017, `CandidatoElegible` |
| GLO-052 | Política de elegibilidad | Configuración versionada de los criterios utilizados para construir candidatos. | [E] | FTE-017, `PoliticaElegibilidadTarea` |
| GLO-053 | Política de asignación | Configuración versionada que determina cómo ordenar y seleccionar candidatos. | [E] | FTE-019, `PoliticaAsignacionTarea` |
| GLO-054 | Ranking de candidatos | Resultado derivado de ordenar candidatos elegibles en un instante. | [E] | FTE-019, `RankingCandidatos` |
| GLO-055 | Propuesta de asignación | Resultado previo, explicable y no persistente por defecto, que identifica un candidato ganador. | [E] | FTE-019, `PropuestaAsignacion` |
| GLO-056 | Asignación de trabajo | Hecho persistente que vincula una instancia con su responsable una vez confirmada la decisión. | [E] | FTE-019, `AsignacionTrabajo` |
| GLO-057 | Plan operativo | Hecho que representa el plan de un período y alcance. | [E] | FTE-021, `PlanOperativo` |
| GLO-058 | Partida de plan | Incorporación de una instancia concreta a un plan; no duplica la obligación. | [E] | FTE-021, `PartidaPlan` |
| GLO-059 | Publicación inicial | Transacción que hace publicable/ejecutable el conjunto inicial válido de partidas. | [E] | FTE-021, `Publicación inicial` |
| GLO-060 | Publicación incremental | Incorporación posterior de una obligación a un plan publicado sin duplicarlo ni reabrirlo. | [E] | FTE-021, `Publicación incremental` |
| GLO-061 | Ejecución de tarea | Hecho que existe cuando el trabajo se inicia realmente sobre una partida publicada. | [E] | FTE-023, `EjecucionTarea`, `Inicio de ejecución` |
| GLO-062 | Pendiente de inicio | Condición derivada de una partida ejecutable sin ejecución iniciada; no crea una fila de ejecución. | [E] | FTE-023, `PendientesInicio` |
| GLO-063 | Evidencia | Prueba persistente registrada para una ejecución. | [E] | FTE-025, `Evidencia` |
| GLO-064 | Tipo de evidencia | Catálogo de modalidades válidas de prueba. | [E] | FTE-025, `TipoEvidencia` |
| GLO-065 | Requisito de evidencia | Configuración versionada que define obligatoriedad, cantidad, combinación y tipos para una ejecución. | [E] | FTE-025, `RequisitoEvidencia`, `RequisitoEvidenciaTipo` |
| GLO-066 | Referencia de evidencia | Localizador verificable asociado a una evidencia cuando su tipo lo permite. | [E] | FTE-025, `ReferenciaEvidencia` |
| GLO-067 | Cumplimiento estructural de evidencia | Resultado derivado sobre cantidad, tipos y referencias; no es una decisión de validación. | [E] | FTE-025, `CumplimientoEstructuralEvidencia` |
| GLO-068 | Política de validación | Configuración versionada que define criterios, autoridad, autovalidación y gates para validar. | [E] | FTE-027, `PoliticaValidacion` |
| GLO-069 | Criterio de validación | Regla verificable que define qué significa cumplimiento para una versión de tarea. | [E] | FTE-027, `CriterioValidacion` |
| GLO-070 | Validación | Decisión persistente sobre una ejecución; independiente del estado de ejecución. | [E] | FTE-027, `Validacion` |
| GLO-071 | Autoridad de validación efectiva | Resultado derivado de usuario, roles, alcance, política, ejecutor, autovalidación y estado del período. | [E] | FTE-027, `AutoridadValidacionEfectiva` |
| GLO-072 | Política de excepción | Configuración versionada de tipos, causas, autoridad y efectos permitidos. | [E] | FTE-029, `PoliticaExcepcion` |
| GLO-073 | Excepción del ciclo de vida | Hecho principal que gobierna una desviación autorizable de una obligación. | [E] | FTE-029, `ExcepcionCicloVida` |
| GLO-074 | Decisión de excepción | Registro append-only de autorización, rechazo o resolución de una excepción. | [E] | FTE-029, `DecisionExcepcion` |
| GLO-075 | Cambio de compromiso | Hecho que registra una modificación de fecha o ventana de compromiso. | [E] | FTE-029, `CambioCompromiso` |
| GLO-076 | Reasignación | Hecho que modifica el responsable y cierra la asignación anterior sin cambiar silenciosamente la obligación. | [E] | FTE-029, `Reasignacion` |
| GLO-077 | Arrastre | Continuidad de una obligación hacia otro período; conserva identidad salvo creación explícita de otra obligación. | [E] | FTE-029, `Efectos por tipo`; FTE-043, regla 9 |
| GLO-078 | Política de cierre | Configuración versionada de gates que deben cumplirse para cerrar un período. | [E] | FTE-031, `PoliticaCierrePeriodo` |
| GLO-079 | Cierre de período | Hecho de que un período cumplió sus gates y quedó cerrado definitivamente. | [E] | FTE-031, `CierrePeriodo` |
| GLO-080 | Corrección postcierre | Ruta extraordinaria y compensatoria posterior al cierre; no es reapertura ni rollback técnico. | [E] | FTE-031, `corregir_postcierre`, `Protección postcierre` |
| GLO-081 | Medición KPI | Valor calculado para una definición, sujeto y período, persistido cuando requiere historia/reconciliación. | [E] | FTE-033, `MedicionKPI` |
| GLO-082 | Política de incentivo | Regla económica aprobada y versionada para calcular resultados de incentivo. | [E] | FTE-033, `PoliticaIncentivo` |
| GLO-083 | Resultado de incentivo | Hecho económico calculado para una persona y período. | [E] | FTE-033, `ResultadoIncentivo` |
| GLO-084 | Movimiento de nómina | Instrucción económica que cruza la frontera entre SGOL y nómina. | [E] | FTE-033, `MovimientoNomina` |
| GLO-085 | Modelo de lectura | Vista/DTO derivado para UX o reporte; no duplica los hechos ni la autoridad de los dominios propietarios. | [E] | FTE-035, `Modelos de lectura / vistas objetivo` |
| GLO-086 | Evento de auditoría | Hecho inmutable y correlacionable sobre una entidad o decisión de negocio. | [E] | FTE-037, `EventoAuditoria` |
| GLO-087 | Corrida de sistema | Unidad persistente de una ejecución técnica con inicio, fin, parámetros, resultado, conteos o error. | [E] | FTE-037, `CorridaSistema` |
| GLO-088 | Registro de idempotencia | Control persistente de una solicitud que puede recibirse o reintentarse más de una vez. | [E] | FTE-037, `RegistroIdempotencia` |
| GLO-089 | Punto de recuperación | Metadato de infraestructura para verificar/restaurar; no es copia de negocio en el mismo esquema. | [E] | FTE-037, `PuntoRecuperacion` |
| GLO-090 | Rollback transaccional | Reversión dentro de la misma frontera transaccional ante fallo; distinto de compensación y restauración. | [E] | FTE-037, `Rollback transaccional` |
| GLO-091 | Compensación | Operación posterior que neutraliza un efecto conservando historia. | [E] | FTE-037, `Compensación` |
| GLO-092 | Referencia de origen legacy | Vínculo que permite localizar el registro original de una entidad/hecho migrado. | [E] | FTE-039, `ReferenciaOrigenLegacy` |
| GLO-093 | Mapa de equivalencia | Configuración temporal/versionada que traduce identidades o conceptos legacy al modelo canónico. | [E] | FTE-039, `MapaEquivalenciaMigracion` |
| GLO-094 | Control de coexistencia | Configuración temporal que determina qué productor tiene autoridad por alcance durante transición. | [E] | FTE-039, `ControlCoexistencia` |
| GLO-095 | Cutover | Transferencia autorizada de la producción legacy al productor canónico después de cumplir gates. | [I] | FTE-039, `Gates de cutover`, `autorizar_cutover` |
| GLO-096 | Sistema externo | Servicio, fuente o canal con el que SGOL puede intercambiar información. | [E] | FTE-041, `SistemaExterno` |
| GLO-097 | Configuración de integración | Contrato lógico y versionado de una integración concreta. | [E] | FTE-041, `ConfiguracionIntegracion` |
| GLO-098 | Mensaje de integración | Unidad individual persistida cuando necesita reintento, recuperación, correlación o auditoría. | [E] | FTE-041, `MensajeIntegracion` |
| GLO-099 | Lote de integración | Unidad de importación/exportación persistida cuando necesita recuperación o reconciliación. | [E] | FTE-041, `LoteIntegracion` |
| GLO-100 | Checkpoint de sincronización | Marca de avance usada sólo en integraciones que leen cambios incrementales. | [E] | FTE-041, `CheckpointSincronizacion` |
| GLO-101 | Gate | Condición verificable que debe cumplirse antes de una transición u operación sensible. | [I] | FTE-027, `Gates de validación`; FTE-031, `Gates de cierre`; FTE-039, `Gates de cutover` |
| GLO-102 | Estado derivado | Clasificación calculada desde hechos canónicos; no se persiste por defecto ni se convierte en máquina de otra entidad. | [I] | FTE-005, `Datos derivados`; FTE-043, `14. Máquinas de estado` |

## 3. No equivalencias obligatorias

| Concepto A | No equivale a | Fuente |
|---|---|---|
| Empleado | Usuario | FTE-007, `Principios obligatorios` |
| Puesto | Rol de seguridad | FTE-007, `Principios obligatorios` |
| Disponibilidad | Empleado activo | FTE-005, `Empleado`, `DisponibilidadEmpleadoPeriodo` |
| Elegibilidad | Asignación | FTE-017/FTE-019; FTE-043, regla 5 |
| Activación | Instancia, plan o ejecución | FTE-013/FTE-015; FTE-043, regla 3 |
| SLA | Tipo de activación | FTE-013, `Principios obligatorios` |
| Instancia | Partida, asignación o ejecución | FTE-015; FTE-043, regla 6 |
| Conclusión de ejecución | Validación | FTE-023/FTE-027 |
| Evidencia | Criterio o decisión de validación | FTE-025/FTE-027 |
| Excepción | Estado de ejecución | FTE-023/FTE-029 |
| Cierre operativo | Rollback técnico | FTE-031/FTE-037 |
| Clasificación operativa | Importe de incentivo | FTE-033; FTE-043, regla 11 |
| Vista/DTO | Fuente canónica o permiso | FTE-035; FTE-043, regla 12 |
| Respaldo | Rollback o compensación | FTE-037; FTE-043, regla 13 |
| Migración | Autorización para doble generación | FTE-039; FTE-043, regla 14 |

## 4. Validación del glosario

- Se consolidaron 102 términos con ID estable.
- Las definiciones propuestas por síntesis están marcadas `[I]`; no se presenta ninguna inferencia como hecho.
- Se preservaron explícitamente las no equivalencias que evitan contradicciones entre dominios.
- No se incluyeron decisiones de MVP ni tecnología.
