# SGOL — Documento Maestro del Modelo Objetivo

## 1. Propósito del SGOL

SGOL es el sistema de gobierno operativo de Loretta. Su función es convertir definiciones operativas versionadas en obligaciones de trabajo trazables, asignarlas bajo reglas explícitas de elegibilidad y capacidad, planificarlas, ejecutarlas, validar su cumplimiento, administrar excepciones, cerrar períodos, producir resultados de KPI e incentivos, auditar decisiones e intercambiar información con sistemas externos sin perder identidad, histórico, autoridad ni idempotencia.

## 2. Alcance del sistema

El alcance comprende personal y capacidad; organización y seguridad; calendario y períodos; catálogo canónico de procesos/tareas; activación; obligaciones; elegibilidad; asignación; planificación; ejecución; evidencias; validación; excepciones; cierre; KPI/incentivos/nómina; modelos de lectura y UX; auditoría y resiliencia; migración/coexistencia; e integraciones externas. La lógica de negocio queda separada de cualquier interfaz o tecnología de hoja de cálculo.

## 3. Principios operativos

- Identidades estables y opacas para hechos; claves de negocio únicas sólo donde el dominio las define.
- Los nombres visibles nunca son claves relacionales.
- Configuración, hechos, vistas derivadas y eventos son responsabilidades distintas.
- Las reglas versionadas conservan histórico; una versión utilizada no se borra.
- Los comandos que cambian estado son transaccionales, idempotentes cuando pueden reintentarse y auditables.
- Ninguna vista o dashboard es fuente de verdad.
- Los estados pertenecen a su dominio; no se fuerza un estado global que mezcle ejecución, validación, excepción y cierre.
- El período operativo usa una única convención ISO y `CERRADA` es terminal para ese período.
- La autorización se evalúa por usuario, permiso, alcance, contexto y gates; ocultar controles de UX no constituye seguridad.
- La migración y coexistencia son temporales; nunca se permiten dos productores autorizados para la misma obligación lógica.
- Las integraciones usan contratos versionados, idempotencia, correlación, reintento controlado y referencias externas sin sustituir identidades SGOL.

## 4. Arquitectura funcional

La arquitectura lógica se organiza en capas: **catálogos/configuración** (F1-F5, F7-F8, F11-F15), **ciclo de obligación** (F5-F14), **lectura y UX** (F16), **servicios transversales** (F17), **migración temporal** (F18) e **integraciones** (F19). Cada dominio expone funciones puras para lectura/cálculo y comandos para cambio de estado. Los eventos se publican después del commit.

## 5. Modelo de dominios

| Fase | Dominio | Entidades_Consolidadas | Logica_Consolidada | Vistas_DTO |
| --- | --- | --- | --- | --- |
| F01 | Empleados, puestos, turnos y capacidad | 8 | 20 | 2 |
| F02 | Organización, sucursales, usuarios, roles y permisos | 10 | 23 | 0 |
| F03 | Configuración, calendario y semana operativa | 7 | 13 | 0 |
| F04 | Modelo canónico de la operación | 14 | 13 | 0 |
| F05 | Activación de tareas | 8 | 17 | 0 |
| F06 | Generación de instancias de trabajo | 1 | 10 | 0 |
| F07 | Elegibilidad | 6 | 7 | 0 |
| F08 | Balanceador y asignación | 4 | 14 | 3 |
| F09 | Planificación y publicación | 3 | 20 | 3 |
| F10 | Ejecución operativa | 2 | 14 | 3 |
| F11 | Evidencias | 5 | 14 | 4 |
| F12 | Validación, supervisión y autorizaciones | 5 | 14 | 3 |
| F13 | Excepciones del ciclo de vida | 8 | 25 | 0 |
| F14 | Cierre semanal e intersemanal | 3 | 16 | 2 |
| F15 | Bonos, KPI e integración con nómina | 7 | 23 | 0 |
| F16 | Reportes, dashboards y UX por roles | 0 | 11 | 11 |
| F17 | Auditoría, logs, corridas, transacciones, respaldo y rollback | 5 | 12 | 4 |
| F18 | Coexistencia legacy/canónica y migración | 4 | 8 | 0 |
| F19 | Integraciones y automatizaciones externas | 6 | 9 | 0 |

## 6. Modelo relacional

Se adopta una convención lógica `snake_case` y esquemas por dominio. Los tipos físicos de datos y el motor SQL pueden variar, pero no las identidades, relaciones, restricciones ni semántica.

### Esquema `activacion`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| ProgramacionActivacion | programacion_activacion | CONFIGURACION_VERSIONADA | PERMANENTE | id_programacion_activacion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | ReglaActivacion, CalendarioOperativo |
| ReglaActivacion | regla_activacion | CONFIGURACION_VERSIONADA | PERMANENTE | id_regla_activacion | id_tarea + version_tarea + tipo + alcance + version_regla | DefinicionTarea, ReglaTemporal |
| ReglaCondicion | regla_condicion | CONFIGURACION_VERSIONADA | PERMANENTE | id_regla_condicion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | ReglaActivacion |
| ReglaEvento | regla_evento | CONFIGURACION_VERSIONADA | PERMANENTE | id_regla_evento | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | ReglaActivacion, TipoEventoNegocio |
| ReglaProcesoOrigen | regla_proceso_origen | CONFIGURACION_VERSIONADA | PERMANENTE | id_regla_proceso_origen | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | ReglaActivacion |
| ReglaTemporal | regla_temporal | CONFIGURACION_VERSIONADA | PERMANENTE | id_regla_temporal | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | CalendarioOperativo |
| TipoEventoNegocio | tipo_evento_negocio | CATALOGO_SQL | PERMANENTE | id_tipo_evento_negocio | codigo_tipo_evento | No aplica / polimorfica cuando se indique |

### Esquema `asignacion`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| AsignacionTrabajo | asignacion_trabajo | HECHO_SQL | PERMANENTE | id_asignacion | id_instancia con una sola asignacion ACTIVA | InstanciaTrabajo, Empleado |
| CriterioRankingAsignacion | criterio_ranking_asignacion | CONFIGURACION_VERSIONADA | PERMANENTE | id_criterio_ranking | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | PoliticaAsignacionTarea |
| PoliticaAsignacionTarea | politica_asignacion_tarea | CONFIGURACION_VERSIONADA | PERMANENTE | id_politica_asignacion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | DefinicionTarea |
| PrioridadAsignacion | prioridad_asignacion | CONFIGURACION_VERSIONADA | PERMANENTE | id_prioridad_asignacion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | PoliticaAsignacionTarea |

### Esquema `auditoria`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| CorridaSistema | corrida_sistema | INFRAESTRUCTURA_AUDITORIA | CONDICIONAL_SEGUN_NECESIDAD | id_corrida | id_corrida opaco; no reutilizable | No aplica / polimorfica cuando se indique |
| ErrorSistema | error_sistema | INFRAESTRUCTURA_AUDITORIA | CONDICIONAL_SEGUN_NECESIDAD | id_error | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | CorridaSistema |
| EventoAuditoria | evento_auditoria | INFRAESTRUCTURA_AUDITORIA | PERMANENTE | id_evento_auditoria | id_evento_auditoria; append-only | Usuario, PeriodoOperativo, Autorizacion |
| PuntoRecuperacion | punto_recuperacion | INFRAESTRUCTURA_AUDITORIA | CONDICIONAL_SEGUN_NECESIDAD | id_punto_recuperacion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | No aplica / polimorfica cuando se indique |
| RegistroIdempotencia | registro_idempotencia | INFRAESTRUCTURA_AUDITORIA | PERMANENTE | idempotency_key | idempotency_key | No aplica / polimorfica cuando se indique |

### Esquema `calendario`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| CalendarioOperativo | calendario_operativo | CONFIGURACION_VERSIONADA | PERMANENTE | id_calendario | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | Organizacion, Sucursal |
| ConfiguracionOperativa | configuracion_operativa | CONFIGURACION_VERSIONADA | PERMANENTE | id_configuracion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | No aplica / polimorfica cuando se indique |
| EstadoPeriodoOperativo | estado_periodo_operativo | CATALOGO_SQL | PERMANENTE | codigo_estado_periodo | codigo_estado_periodo | No aplica / polimorfica cuando se indique |
| ExcepcionCalendario | excepcion_calendario | HECHO_SQL | PERMANENTE | id_excepcion_calendario | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | CalendarioOperativo |
| TransicionPeriodo | transicion_periodo | HISTORIAL_APPEND_ONLY | PERMANENTE | id_transicion_periodo | id_transicion_periodo | PeriodoOperativo, Usuario |
| PeriodoOperativo | periodo_operativo | HECHO_SQL | PERMANENTE | id_periodo_operativo | alcance + anio_iso + semana_iso | CalendarioOperativo |
| ReglaDiaOperativo | regla_dia_operativo | CONFIGURACION_VERSIONADA | PERMANENTE | id_regla_dia | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | CalendarioOperativo |

### Esquema `catalogo_operativo`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| Automatizacion | automatizacion | CONFIGURACION_VERSIONADA | PERMANENTE | id_automatizacion | codigo AUT-#### | No aplica / polimorfica cuando se indique |
| ChecklistDefinicion | checklist_definicion | MAESTRO_SQL | PERMANENTE | id_checklist | codigo CHK-#### + version/vigencia | No aplica / polimorfica cuando se indique |
| ChecklistItem | checklist_item | HECHO_SQL | PERMANENTE | id_item | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | ChecklistDefinicion |
| DefinicionTarea | definicion_tarea | MAESTRO_SQL | PERMANENTE | id_tarea | codigo TAR-#### | No aplica / polimorfica cuando se indique |
| FlujoOperativo | flujo_operativo | CATALOGO_SQL | PERMANENTE | id_flujo | codigo FLU-#### | No aplica / polimorfica cuando se indique |
| FlujoTarea | flujo_tarea | RELACION_SQL | PERMANENTE | id_flujo_tarea | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | FlujoOperativo, DefinicionTarea |
| Macroproceso | macroproceso | CATALOGO_SQL | PERMANENTE | id_macroproceso | codigo_macroproceso | No aplica / polimorfica cuando se indique |
| ParametroRegla | parametro_regla | CONFIGURACION_VERSIONADA | PERMANENTE | id_parametro | codigo PAR-#### | ReglaConfigurable |
| Proceso | proceso | CATALOGO_SQL | PERMANENTE | id_proceso | codigo_proceso dentro del macroproceso (por definir sin colisiones) | Macroproceso |
| ReglaConfigurable | regla_configurable | CONFIGURACION_VERSIONADA | PERMANENTE | id_regla | codigo REG-#### | No aplica / polimorfica cuando se indique |
| RelacionTarea | relacion_tarea | RELACION_SQL | PERMANENTE | id_relacion_tarea | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | DefinicionTarea |
| Subproceso | subproceso | CATALOGO_SQL | PERMANENTE | id_subproceso | codigo_subproceso dentro del proceso (por definir sin colisiones) | Proceso |
| VersionDefinicionTarea | version_definicion_tarea | HECHO_SQL | PERMANENTE | id_version_tarea | id_tarea + numero/version + alcance/vigencia | DefinicionTarea, Subproceso |

### Esquema `cierre`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| CierrePeriodo | cierre_periodo | HECHO_SQL | PERMANENTE | id_cierre_periodo | id_periodo_operativo (un cierre definitivo) | PeriodoOperativo, PoliticaCierrePeriodo, Usuario, Autorizacion |
| PoliticaCierrePeriodo | politica_cierre_periodo | CONFIGURACION_VERSIONADA | PERMANENTE | id_politica_cierre | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | Organizacion |

### Esquema `ejecucion`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| EjecucionTarea | ejecucion_tarea | HECHO_SQL | PERMANENTE | id_ejecucion | id_partida_plan con ejecucion compatible unica segun politica | PartidaPlan, InstanciaTrabajo, AsignacionTrabajo |
| TransicionEstadoEjecucion | transicion_estado_ejecucion | HISTORIAL_APPEND_ONLY | PERMANENTE | id_transicion_ejecucion | id_transicion_ejecucion | EjecucionTarea, Usuario |

### Esquema `elegibilidad`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| EmpleadoEspecialidad | empleado_especialidad | ASIGNACION_HISTORICA | PERMANENTE | (id_empleado, id_especialidad, vigente_desde) | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | Empleado, Especialidad |
| Especialidad | especialidad | CATALOGO_SQL | PERMANENTE | id_especialidad | codigo/nombre normalizado de especialidad | No aplica / polimorfica cuando se indique |
| EspecialidadRequeridaTarea | especialidad_requerida_tarea | RELACION_SQL | PERMANENTE | (id_politica_elegibilidad, id_especialidad) | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | PoliticaElegibilidadTarea, Especialidad |
| PoliticaElegibilidadTarea | politica_elegibilidad_tarea | CONFIGURACION_VERSIONADA | PERMANENTE | id_politica_elegibilidad | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | DefinicionTarea |
| PuestoPermitidoTarea | puesto_permitido_tarea | RELACION_SQL | PERMANENTE | (id_politica_elegibilidad, id_puesto) | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | PoliticaElegibilidadTarea, Puesto |
| TurnoPermitidoTarea | turno_permitido_tarea | RELACION_SQL | PERMANENTE | (id_politica_elegibilidad, id_turno) | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | PoliticaElegibilidadTarea, Turno |

### Esquema `evidencias`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| Evidencia | evidencia | HECHO_SQL | PERMANENTE | id_evidencia | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | EjecucionTarea, TipoEvidencia, Usuario |
| ReferenciaEvidencia | referencia_evidencia | HECHO_SQL | PERMANENTE | id_referencia_evidencia | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | Evidencia |
| RequisitoEvidencia | requisito_evidencia | CONFIGURACION_VERSIONADA | PERMANENTE | id_requisito_evidencia | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | VersionDefinicionTarea |
| RequisitoEvidenciaTipo | requisito_evidencia_tipo | RELACION_SQL | PERMANENTE | (id_requisito_evidencia, id_tipo_evidencia) | id_requisito_evidencia + id_tipo_evidencia | RequisitoEvidencia, TipoEvidencia |
| TipoEvidencia | tipo_evidencia | CATALOGO_SQL | PERMANENTE | id_tipo_evidencia | codigo_tipo_evidencia | No aplica / polimorfica cuando se indique |

### Esquema `excepciones`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| CambioCompromiso | cambio_compromiso | HECHO_SQL | PERMANENTE | id_cambio_compromiso | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | ExcepcionCicloVida, InstanciaTrabajo |
| CausaExcepcion | causa_excepcion | CATALOGO_SQL | PERMANENTE | id_causa_excepcion | codigo_causa | No aplica / polimorfica cuando se indique |
| DecisionExcepcion | decision_excepcion | HISTORIAL_APPEND_ONLY | PERMANENTE | id_decision_excepcion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | ExcepcionCicloVida, Autorizacion, Usuario |
| ExcepcionCicloVida | excepcion_ciclo_vida | HECHO_SQL | PERMANENTE | id_excepcion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | InstanciaTrabajo, PartidaPlan, EjecucionTarea, CausaExcepcion, Autorizacion |
| IncidenciaOperativa | incidencia_operativa | HECHO_SQL | CONDICIONAL_SEGUN_NECESIDAD | id_incidencia_operativa | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | InstanciaTrabajo |
| PoliticaExcepcion | politica_excepcion | CONFIGURACION_VERSIONADA | PERMANENTE | id_politica_excepcion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | VersionDefinicionTarea |
| Reasignacion | reasignacion | HECHO_SQL | PERMANENTE | id_reasignacion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | ExcepcionCicloVida, InstanciaTrabajo, AsignacionTrabajo |
| RelacionObligacion | relacion_obligacion | RELACION_SQL | PERMANENTE | id_relacion_obligacion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | InstanciaTrabajo, ExcepcionCicloVida |

### Esquema `incentivos`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| DefinicionKPI | definicion_kpi | MAESTRO_SQL | PERMANENTE | id_kpi | codigo KPI-#### | No aplica / polimorfica cuando se indique |
| AsignacionPoliticaIncentivo | asignacion_politica_incentivo | RELACION_SQL | PERMANENTE | id_asignacion_politica | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | PoliticaIncentivo, Empleado, Puesto, Sucursal |
| LoteNomina | lote_nomina | HECHO_SQL | PERMANENTE | id_lote_nomina | identificador_corte/receptor + version | PeriodoOperativo |
| MedicionKPI | medicion_kpi | HECHO_SQL | CONDICIONAL_SEGUN_NECESIDAD | id_medicion_kpi | id_kpi + version + periodo/ventana + contexto | DefinicionKPI, PeriodoOperativo |
| MovimientoNomina | movimiento_nomina | HECHO_SQL | PERMANENTE | id_movimiento_nomina | clave_idempotencia_movimiento | ResultadoIncentivo, Empleado, LoteNomina |
| PoliticaIncentivo | politica_incentivo | CONFIGURACION_VERSIONADA | PERMANENTE | id_politica_incentivo | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | No aplica / polimorfica cuando se indique |
| ResultadoIncentivo | resultado_incentivo | HECHO_SQL | PERMANENTE | id_resultado_incentivo | id_empleado + id_periodo_operativo + id_politica/version | PeriodoOperativo, Empleado, PoliticaIncentivo |

### Esquema `integracion`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| CheckpointSincronizacion | checkpoint_sincronizacion | HECHO_CONTROL_INTEGRACION | CONDICIONAL_SEGUN_NECESIDAD | (id_integracion, particion) | id_integracion + particion | ConfiguracionIntegracion, LoteIntegracion |
| ConfiguracionIntegracion | configuracion_integracion | CONFIGURACION_INTEGRACION | PERMANENTE | id_integracion | id_sistema_externo + nombre + version_contrato | SistemaExterno |
| LoteIntegracion | lote_integracion | HECHO_CONTROL_INTEGRACION | CONDICIONAL_SEGUN_NECESIDAD | id_lote_integracion | id_integracion + identificador_origen/huella_contenido | ConfiguracionIntegracion, CorridaSistema |
| MensajeIntegracion | mensaje_integracion | HECHO_CONTROL_INTEGRACION | CONDICIONAL_SEGUN_NECESIDAD | id_mensaje_integracion | id_integracion + clave_idempotencia | ConfiguracionIntegracion, ErrorSistema |
| ReferenciaExterna | referencia_externa | HECHO_CONTROL_INTEGRACION | PERMANENTE | id_referencia_externa | id_sistema_externo + tipo_objeto_externo + id_objeto_externo + vigencia | SistemaExterno |
| SistemaExterno | sistema_externo | CATALOGO_INTEGRACION | PERMANENTE | id_sistema_externo | codigo | No aplica / polimorfica cuando se indique |

### Esquema `migracion`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| ControlCoexistencia | control_coexistencia | MIGRACION_TEMPORAL | TEMPORAL_HASTA_CUTOVER | id_control | alcance_semantico + clave_contexto + vigencia | Autorizacion |
| MapaEquivalenciaMigracion | mapa_equivalencia_migracion | MIGRACION_TEMPORAL | TEMPORAL_HASTA_CUTOVER | id_mapa | version_mapa + tipo_objeto_origen + id_origen + contexto | No aplica / polimorfica cuando se indique |
| ReferenciaOrigenLegacy | referencia_origen_legacy | MIGRACION_TEMPORAL | TEMPORAL_HASTA_CUTOVER | id_referencia_origen | tipo_origen + id_origen | No aplica / polimorfica cuando se indique |

### Esquema `obligaciones`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| InstanciaTrabajo | instancia_trabajo | HECHO_SQL | PERMANENTE | id_instancia | clave_idempotencia | DefinicionTarea, ReglaActivacion, PeriodoOperativo |

### Esquema `personal`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| AsignacionPuestoEmpleado | asignacion_puesto_empleado | ASIGNACION_HISTORICA | PERMANENTE | id_asignacion_puesto | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | Empleado, Puesto |
| AsignacionTurnoEmpleado | asignacion_turno_empleado | ASIGNACION_HISTORICA | PERMANENTE | id_asignacion_turno | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | Empleado, Turno |
| DisponibilidadEmpleadoDia | disponibilidad_empleado_dia | HECHO_SQL | PERMANENTE | id_disponibilidad_dia | id_disponibilidad_periodo + fecha | DisponibilidadEmpleadoPeriodo |
| DisponibilidadEmpleadoPeriodo | disponibilidad_empleado_periodo | HECHO_SQL | PERMANENTE | id_disponibilidad_periodo | id_empleado + id_periodo_operativo | Empleado, PeriodoOperativo |
| Empleado | empleado | MAESTRO_SQL | PERMANENTE | id_empleado | codigo_empleado | No aplica / polimorfica cuando se indique |
| PoliticaCapacidadPuesto | politica_capacidad_puesto | CONFIGURACION_VERSIONADA | PERMANENTE | id_politica_capacidad | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | Puesto |
| Puesto | puesto | CATALOGO_SQL | PERMANENTE | id_puesto | clave/codigo de puesto administrado | AreaOrganizacional |
| Turno | turno | CATALOGO_SQL | PERMANENTE | id_turno | clave/codigo de turno administrado | No aplica / polimorfica cuando se indique |

### Esquema `planificacion`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| PartidaPlan | partida_plan | HECHO_SQL | PERMANENTE | id_partida_plan | id_plan + id_instancia | PlanOperativo, InstanciaTrabajo, AsignacionTrabajo |
| PlanOperativo | plan_operativo | HECHO_SQL | PERMANENTE | id_plan | id_periodo_operativo + alcance con un solo plan activo equivalente | PeriodoOperativo |
| PublicacionPlan | publicacion_plan | HECHO_SQL | CONDICIONAL_SEGUN_NECESIDAD | id_publicacion_plan | id_plan + clave_idempotente_comando | PlanOperativo, Usuario |

### Esquema `seguridad`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| AreaOrganizacional | area_organizacional | CATALOGO_SQL | PERMANENTE | id_area | clave de area dentro de organizacion | Organizacion, Sucursal |
| AsignacionUsuarioRol | asignacion_usuario_rol | ASIGNACION_HISTORICA | PERMANENTE | id_asignacion_usuario_rol | usuario + rol + alcance + vigente_desde | Usuario, RolSeguridad, Sucursal, AreaOrganizacional |
| Autorizacion | autorizacion | HECHO_SQL | PERMANENTE | id_autorizacion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | Usuario |
| EquivalenciaPuestoRol | equivalencia_puesto_rol | RELACION_SQL | PERMANENTE | id_equivalencia_puesto_rol | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | Puesto, RolSeguridad |
| Organizacion | organizacion | MAESTRO_SQL | PERMANENTE | id_organizacion | clave de negocio de organizacion | No aplica / polimorfica cuando se indique |
| Permiso | permiso | CATALOGO_SQL | PERMANENTE | id_permiso | codigo_permiso | No aplica / polimorfica cuando se indique |
| RolPermiso | rol_permiso | RELACION_SQL | PERMANENTE | id_rol_permiso | rol + permiso + alcance + vigencia | RolSeguridad, Permiso |
| RolSeguridad | rol_seguridad | CATALOGO_SQL | PERMANENTE | id_rol | codigo_rol | No aplica / polimorfica cuando se indique |
| Sucursal | sucursal | MAESTRO_SQL | PERMANENTE | id_sucursal | clave de negocio de sucursal | Organizacion |
| Usuario | usuario | MAESTRO_SQL | PERMANENTE | id_usuario | identidad_autenticada estable (cuando se defina el proveedor) | Empleado |

### Esquema `validacion`


| Entidad | Tabla_SQL | Clasificacion | Permanencia | PK | Clave_Natural_o_Unicidad | FKs_Relevantes |
| --- | --- | --- | --- | --- | --- | --- |
| CriterioValidacion | criterio_validacion | CONFIGURACION_VERSIONADA | PERMANENTE | id_criterio_validacion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | PoliticaValidacion |
| EvaluacionCriterio | evaluacion_criterio | HECHO_SQL | CONDICIONAL_SEGUN_NECESIDAD | id_evaluacion_criterio | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | Validacion, CriterioValidacion |
| PoliticaValidacion | politica_validacion | CONFIGURACION_VERSIONADA | PERMANENTE | id_politica_validacion | No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia. | VersionDefinicionTarea |
| Validacion | validacion | HECHO_SQL | PERMANENTE | id_validacion | id_ejecucion + version/decision vigente sin sobreescritura | EjecucionTarea, PoliticaValidacion, Usuario, Autorizacion |

## 7. Diccionario de entidades

### `personal.asignacion_puesto_empleado` — AsignacionPuestoEmpleado

Hecho persistente e histórico que vincula a un empleado con un puesto durante un intervalo de vigencia.

- **PK:** `id_asignacion_puesto`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** Empleado, Puesto.
- **Atributos relevantes:** `id_asignacion_puesto`; `id_empleado`; `id_puesto`; `vigente_desde`; `vigente_hasta` nullable; `es_principal` cuando el modelo futuro permita más de una asignación simultánea.
- **Permanencia:** PERMANENTE.
- **Restricciones:** No se permiten dos asignaciones principales solapadas para el mismo empleado. | El puesto vigente se resuelve por fecha, no por valor copiado en el registro del empleado. | Todo cambio de puesto debe conservar el histórico anterior..

### `personal.asignacion_turno_empleado` — AsignacionTurnoEmpleado

Hecho persistente e histórico que vincula a un empleado con un turno durante un intervalo de vigencia.

- **PK:** `id_asignacion_turno`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** Empleado, Turno.
- **Atributos relevantes:** `id_asignacion_turno`; `id_empleado`; `id_turno`; `vigente_desde`; `vigente_hasta` nullable.
- **Permanencia:** PERMANENTE.
- **Restricciones:** No se permiten asignaciones de turno principales solapadas para el mismo empleado. | El turno vigente se determina por fecha. | Un turno inactivo no puede originar nuevas asignaciones..

### `personal.disponibilidad_empleado_dia` — DisponibilidadEmpleadoDia

Detalle persistente de disponibilidad diaria dentro del período.

- **PK:** `id_disponibilidad_dia`.
- **Unicidad de negocio:** id_disponibilidad_periodo + fecha.
- **FK principales:** DisponibilidadEmpleadoPeriodo.
- **Atributos relevantes:** `id_disponibilidad_dia`; `id_disponibilidad_periodo`; `fecha`; `fraccion_disponible`, en rango 0 a 1.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Un único registro por empleado y fecha. | `fraccion_disponible = 0` bloquea la disponibilidad para esa fecha. | La interpretación de fracciones parciales para repartir minutos intradía deberá mantenerse separada de la capacidad semanal hasta que la planificación defina explícitamente ese prorrateo..

### `personal.disponibilidad_empleado_periodo` — DisponibilidadEmpleadoPeriodo

Hecho persistente que conserva la disponibilidad operativa declarada para un empleado en un período.

- **PK:** `id_disponibilidad_periodo`.
- **Unicidad de negocio:** id_empleado + id_periodo_operativo.
- **FK principales:** Empleado, PeriodoOperativo.
- **Atributos relevantes:** `id_disponibilidad_periodo`; `id_empleado`; referencia al período operativo, que se formaliza en Fase 3; `fraccion_carga_tareas`, en rango 0 a 1; referencia de origen y momento de carga cuando se requiera trazabilidad de integración.
- **Permanencia:** PERMANENTE.
- **Restricciones:** `fraccion_carga_tareas = 0` significa que el empleado no aporta capacidad SGOL en ese período. | La existencia del registro no sustituye la validación de que el empleado esté laboralmente activo. | Registros duplicados con la misma clave deben reconciliarse antes de calcular capacidad..

### `personal.empleado` — Empleado

Entidad persistente con identidad propia.

- **PK:** `id_empleado`.
- **Unicidad de negocio:** codigo_empleado.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_empleado`: identificador interno estable; `codigo_empleado`: clave de negocio estable y única; `nombre_completo`: nombre visible; `fecha_ingreso`: inicio conocido de la relación laboral; `fecha_baja`: fin de vigencia laboral cuando corresponda; `estado_empleado`: ACTIVO o INACTIVO.
- **Permanencia:** PERMANENTE.
- **Restricciones:** El nombre no puede utilizarse como clave de relación. | `codigo_empleado` debe ser único y no reutilizable. | El estado laboral y la disponibilidad operativa son conceptos distintos. | Un empleado inactivo no puede considerarse disponible en una fecha posterior a su baja..

### `personal.politica_capacidad_puesto` — PoliticaCapacidadPuesto

Configuración persistente y versionable de cuánto de la disponibilidad nominal puede asignarse a tareas SGOL para un puesto.

- **PK:** `id_politica_capacidad`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** Puesto.
- **Atributos relevantes:** `id_politica_capacidad`; `id_puesto`; `vigente_desde`; `vigente_hasta` nullable; `factor_max_tareas_normal`; `factor_max_tareas_quincena`; `activa`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Los factores deben estar en rango 0 a 1. | Debe existir como máximo una política vigente por puesto y fecha. | Si una política está inactiva, el dominio puede exponer la capacidad nominal completa. | La ausencia de una política requerida es un error de configuración; no se debe aplicar un porcentaje por defecto silencioso. | La determinación de si una fecha pertenece a ventana de quincena se delega al calendario operativo de Fase 3..

### `personal.puesto` — Puesto

Catálogo administrado por el negocio.

- **PK:** `id_puesto`.
- **Unicidad de negocio:** clave/codigo de puesto administrado.
- **FK principales:** AreaOrganizacional.
- **Atributos relevantes:** `id_puesto`: clave estable del catálogo; `nombre_puesto`; `nivel`; `horas_base_semana`; `puede_supervisar`; `activo`; referencia al área organizacional, cuya definición se completa en Fase 2.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Las relaciones operativas deben usar `id_puesto`, no el texto del nombre. | Un puesto inactivo conserva su histórico, pero no admite nuevas asignaciones salvo autorización de migración. | Las horas base deben provenir de configuración persistente; no deben fijarse como constante dentro de funciones..

### `personal.turno` — Turno

Catálogo de turnos reales de trabajo.

- **PK:** `id_turno`.
- **Unicidad de negocio:** clave/codigo de turno administrado.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_turno`; `nombre_turno`; `hora_inicio`; `hora_fin`; `tipo_turno`; `activo`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Un turno asignable a una persona debe representar una jornada o ventana real. | Un valor que signifique “sin restricción de turno” no representa un turno trabajado y debe modelarse como regla de compatibilidad de una tarea, no como asignación de personal..

### `seguridad.area_organizacional` — AreaOrganizacional

Catálogo persistente de áreas funcionales.

- **PK:** `id_area`.
- **Unicidad de negocio:** clave de area dentro de organizacion.
- **FK principales:** Organizacion, Sucursal.
- **Atributos relevantes:** `id_area`; `id_organizacion`; `id_sucursal` nullable cuando el área sea corporativa o transversal; `codigo_area`; `nombre`; `id_area_padre` nullable para jerarquía cuando exista; `activa`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** El área utilizada por un puesto debe resolverse mediante `id_area`, no por texto libre. | Las áreas corporativas pueden no depender de una sucursal específica. | Una misma etiqueta histórica no se fusiona con otra área sin una equivalencia aprobada..

### `seguridad.asignacion_usuario_rol` — AsignacionUsuarioRol

Hecho persistente e histórico que concede un rol a un usuario dentro de un alcance.

- **PK:** `id_asignacion_usuario_rol`.
- **Unicidad de negocio:** usuario + rol + alcance + vigente_desde.
- **FK principales:** Usuario, RolSeguridad, Sucursal, AreaOrganizacional.
- **Atributos relevantes:** `id_asignacion_usuario_rol`; `id_usuario`; `id_rol`; `tipo_alcance`; `id_sucursal` nullable; `id_area` nullable; `vigente_desde`; `vigente_hasta` nullable; `asignado_por`; `motivo` cuando corresponda.
- **Permanencia:** PERMANENTE.
- **Restricciones:** La vigencia de una asignación debe evaluarse por fecha y contexto. | Un alcance global no requiere una sucursal ficticia. | Las asignaciones revocadas se conservan para auditoría. | La asignación de un rol sensible debe quedar trazada con el usuario que la otorgó..

### `seguridad.autorizacion` — Autorizacion

Hecho persistente que registra una decisión explícita de autoridad sobre una operación concreta.

- **PK:** `id_autorizacion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** Usuario.
- **Atributos relevantes:** `id_autorizacion`; `fecha_hora`; `id_usuario_autorizador`; `tipo_autorizacion`; referencia a la operación o entidad autorizada; `rol_objetivo` o alcance funcional cuando corresponda; `motivo`; `estado_autorizacion`; `resultado`; `observaciones`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Una autorización debe identificar al usuario real que tomó la decisión. | El motivo es obligatorio para operaciones críticas cuando la política lo exija. | Una autorización no concede permanentemente un permiso ni modifica el rol del usuario. | La autorización debe poder correlacionarse con la operación que habilitó..

### `seguridad.equivalencia_puesto_rol` — EquivalenciaPuestoRol

Configuración opcional que permite proponer o resolver una equivalencia entre un puesto operativo y un rol de seguridad cuando el negocio decida derivar autoridad desde una responsabilidad organizacional.

- **PK:** `id_equivalencia_puesto_rol`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** Puesto, RolSeguridad.
- **Atributos relevantes:** `id_equivalencia`; `id_puesto`; `id_rol`; `tipo_equivalencia`; `vigente_desde`; `vigente_hasta` nullable; `activa`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** La equivalencia no sustituye una asignación explícita de rol salvo que exista una política aprobada que así lo establezca. | Una equivalencia ambigua debe devolver error o requerir decisión, nunca elegir silenciosamente. | Las responsabilidades canónicas de tareas que describen “responsable” o “validador” no se convierten automáticamente en roles de seguridad..

### `seguridad.organizacion` — Organizacion

Entidad persistente que representa la unidad empresarial propietaria de la operación.

- **PK:** `id_organizacion`.
- **Unicidad de negocio:** clave de negocio de organizacion.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_organizacion`; `codigo_organizacion`; `nombre`; `activa`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** `codigo_organizacion` debe ser único y estable. | La desactivación no elimina su histórico ni el de sus sucursales..

### `seguridad.permiso` — Permiso

Catálogo de capacidades atómicas del sistema.

- **PK:** `id_permiso`.
- **Unicidad de negocio:** codigo_permiso.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_permiso`; `codigo_permiso`; `descripcion`; `activo`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Cada permiso debe representar una sola capacidad verificable. | Los permisos de acceso visual y los permisos de modificación deben permanecer separados cuando impliquen niveles distintos de autoridad. | La ausencia de concesión activa equivale a denegación..

### `seguridad.rol_permiso` — RolPermiso

Relación persistente entre un rol y un permiso.

- **PK:** `id_rol_permiso`.
- **Unicidad de negocio:** rol + permiso + alcance + vigencia.
- **FK principales:** RolSeguridad, Permiso.
- **Atributos relevantes:** `id_rol`; `id_permiso`; `permitido`; `tipo_alcance`; referencia de alcance cuando aplique; `requiere_autorizacion_adicional`; `id_rol_autorizador` nullable; `vigente_desde`; `vigente_hasta` nullable; `activo`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** No pueden existir dos concesiones vigentes incompatibles para la misma combinación de rol, permiso y alcance. | El alcance debe representarse con identificadores o reglas explícitas, no mediante textos ambiguos. | Un permiso que requiere autorización adicional no queda satisfecho sólo porque el usuario posea el rol..

### `seguridad.rol_seguridad` — RolSeguridad

Catálogo administrado por el negocio para agrupar autoridad funcional.

- **PK:** `id_rol`.
- **Unicidad de negocio:** codigo_rol.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_rol`; `codigo_rol`; `nombre`; `descripcion`; `activo`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Un rol no representa un puesto laboral. | Un rol puede asignarse a múltiples usuarios. | Un usuario puede tener más de un rol cuando exista autorización y los alcances no produzcan conflicto. | La baja de un rol no elimina asignaciones históricas..

### `seguridad.sucursal` — Sucursal

Entidad persistente con identidad propia.

- **PK:** `id_sucursal`.
- **Unicidad de negocio:** clave de negocio de sucursal.
- **FK principales:** Organizacion.
- **Atributos relevantes:** `id_sucursal`; `id_organizacion`; `codigo_sucursal`; `nombre`; `domicilio` cuando sea necesario para la operación; `zona_horaria` cuando el despliegue opere en más de una zona; `activa`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** `codigo_sucursal` debe ser único dentro de la organización. | Una sucursal inactiva conserva su histórico y no admite nuevas asignaciones operativas salvo procesos de migración o corrección autorizada. | El alcance global se representa mediante reglas de alcance, nunca creando una sucursal denominada “TODAS”..

### `seguridad.usuario` — Usuario

Entidad persistente que representa una identidad habilitada para acceder a SGOL.

- **PK:** `id_usuario`.
- **Unicidad de negocio:** identidad_autenticada estable (cuando se defina el proveedor).
- **FK principales:** Empleado.
- **Atributos relevantes:** `id_usuario`; `id_empleado` nullable; `identificador_acceso` o identificador estable entregado por el proveedor de autenticación; `nombre_visible`; `estado_usuario`; `fecha_alta`; `fecha_baja` nullable.
- **Permanencia:** PERMANENTE.
- **Restricciones:** `id_usuario` es la referencia interna para auditoría y autorización. | El nombre visible no puede utilizarse como clave de autenticación ni de relación. | Un usuario puede existir sin empleado únicamente cuando exista una razón operativa aprobada, por ejemplo una cuenta técnica. | Un empleado puede no tener usuario si no necesita acceso al sistema. | Un usuario inactivo no puede iniciar nuevas operaciones ni recibir nuevas asignaciones de rol. | La tecnología de autenticación no forma parte de la regla de negocio; el dominio sólo requiere una identidad autenticada estable..

### `calendario.calendario_operativo` — CalendarioOperativo

Entidad persistente que identifica el conjunto de reglas temporales aplicables a una organización o alcance operativo.

- **PK:** `id_calendario`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** Organizacion, Sucursal.
- **Atributos relevantes:** `id_calendario`; `codigo_calendario`; alcance organizacional; `zona_horaria`; `vigente_desde`; `vigente_hasta` nullable; `activo`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Un alcance no puede tener dos calendarios principales vigentes e incompatibles para la misma fecha. | La zona horaria debe ser válida y resoluble por el sistema. | Las reglas recurrentes de días operativos pueden variar por alcance..

### `calendario.configuracion_operativa` — ConfiguracionOperativa

Configuración persistente únicamente para valores que el negocio pueda cambiar sin desplegar una nueva versión del sistema.

- **PK:** `id_configuracion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_configuracion`; `clave`; `tipo_dato`; `valor_tipado`; `tipo_alcance`; referencia al alcance cuando corresponda; `vigente_desde`; `vigente_hasta` nullable; `activa`; `modificada_por`; `motivo_cambio` cuando sea requerido.
- **Permanencia:** PERMANENTE.
- **Restricciones:** La clave debe ser única dentro de su alcance y vigencia. | El valor debe validarse según su tipo declarado. | No se almacenan aquí estados de ciclo de vida ni valores derivados. | Los parámetros sensibles deben conservar histórico de cambios. | La configuración de un dominio debe ser interpretada por el servicio propietario de ese dominio..

### `calendario.estado_periodo_operativo` — EstadoPeriodoOperativo

Catálogo de estados del ciclo semanal.

- **PK:** `codigo_estado_periodo`.
- **Unicidad de negocio:** codigo_estado_periodo.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** Atributos mínimos definidos por la semántica del dominio; completar tipos físicos durante implementación sin alterar las claves/reglas aquí consolidadas..
- **Permanencia:** PERMANENTE.
- **Restricciones:** Los códigos de estado son estables y no dependen del texto visible en la interfaz. | Las acciones permitidas se validan contra el estado vigente..

### `calendario.excepcion_calendario` — ExcepcionCalendario

Hecho persistente que modifica el comportamiento normal de una fecha concreta.

- **PK:** `id_excepcion_calendario`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** CalendarioOperativo.
- **Atributos relevantes:** `id_excepcion_calendario`; `id_calendario`; `fecha_referencia`; `fecha_efectiva`; `tipo_excepcion`; `motivo`; `es_operativo`; `caracter_obligatorio` cuando corresponda; `autorizada`; `autorizada_por` nullable; `vigente`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** `fecha_referencia` conserva la fecha normativa o de origen cuando una observancia sea trasladada. | `fecha_efectiva` es la fecha que afecta la operación. | Una excepción no autorizada no modifica el calendario operativo. | Debe impedirse más de una excepción activa contradictoria para la misma fecha, calendario y alcance..

### `calendario.transicion_periodo` — TransicionPeriodo

Historial append-only de transiciones de `PeriodoOperativo` cuando la trazabilidad tenga valor de negocio o auditoría.

- **PK:** `id_transicion_periodo`.
- **Unicidad de negocio:** id_transicion_periodo.
- **FK principales:** PeriodoOperativo, Usuario.
- **Atributos relevantes:** `id_historial_estado`; `id_periodo_operativo`; `fecha_hora`; `estado_anterior`; `estado_nuevo`; `tipo_transicion`; `motivo`; `id_usuario_autorizador`; `resultado`; referencia de transacción o corrida cuando corresponda; `id_transicion_periodo`; estado anterior; estado nuevo; fecha/hora; actor; motivo; correlación.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Toda transición efectiva debe registrar el período afectado. | Una restauración debe identificar la transacción que la originó. | Un intento bloqueado puede registrarse como evento de auditoría sin modificar el estado..

### `calendario.periodo_operativo` — PeriodoOperativo

Entidad persistente que representa una semana operativa concreta.

- **PK:** `id_periodo_operativo`.
- **Unicidad de negocio:** alcance + anio_iso + semana_iso.
- **FK principales:** CalendarioOperativo.
- **Atributos relevantes:** `id_periodo_operativo`; `anio_iso`; `semana_iso`; `fecha_inicio`; `fecha_fin`; `id_calendario`; `estado_periodo`; `creado_en`; `creado_por`; `cerrado_en` nullable.
- **Permanencia:** PERMANENTE.
- **Restricciones:** `semana_iso` debe estar entre 1 y el número válido de semanas ISO del año indicado. | `fecha_inicio` y `fecha_fin` se derivan del año/semana ISO y deben ser consistentes con ellos. | Un período sólo puede tener un estado vigente. | Los hechos de planificación y ejecución deben referenciar `id_periodo_operativo`, evitando repetir año/semana como única relación lógica. | Un período cerrado no vuelve a estado inicial para reutilizarlo en otra semana..

### `calendario.regla_dia_operativo` — ReglaDiaOperativo

Configuración persistente que define el comportamiento normal de cada día de la semana dentro de un calendario.

- **PK:** `id_regla_dia`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** CalendarioOperativo.
- **Atributos relevantes:** `id_regla_dia`; `id_calendario`; `dia_semana`; `es_operativo`; ventanas horarias cuando correspondan; `vigente_desde`; `vigente_hasta` nullable.
- **Permanencia:** PERMANENTE.
- **Restricciones:** La semana se interpreta de lunes a domingo. | La condición de sábado o domingo se calcula; la condición operativa se resuelve por esta regla y sus excepciones. | Una excepción de fecha prevalece sobre la regla recurrente cuando esté vigente y autorizada..

### `catalogo_operativo.automatizacion` — Automatizacion

Definición de un componente ejecutable gobernable con código `AUT-####`, únicamente cuando exista automatización real.

- **PK:** `id_automatizacion`.
- **Unicidad de negocio:** codigo AUT-####.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_automatizacion`; nombre; tipo; contrato de entrada/salida; propietario; versión; estado; tareas o procesos relacionados.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Una clasificación como “potencialmente automatizable” no crea una Automatizacion. | Un adaptador de integración o activación no se considera automáticamente una Automatizacion; se clasifica por su responsabilidad real..

### `catalogo_operativo.checklist_definicion` — ChecklistDefinicion

Definición reusable y versionable de controles verificables con código `CHK-####`.

- **PK:** `id_checklist`.
- **Unicidad de negocio:** codigo CHK-#### + version/vigencia.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_checklist`; código `CHK-####`; nombre; propósito; versión/vigencia; estado activo.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `catalogo_operativo.checklist_item` — ChecklistItem

Ítem ordenado de un checklist.

- **PK:** `id_item`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** ChecklistDefinicion.
- **Atributos relevantes:** `id_item`; `id_checklist`; orden; instrucción o criterio verificable; obligatoriedad; tipo de respuesta o evidencia cuando corresponda.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `catalogo_operativo.definicion_tarea` — DefinicionTarea

Identidad lógica estable de una obligación posible.

- **PK:** `id_tarea`.
- **Unicidad de negocio:** codigo TAR-####.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_tarea` con código `TAR-####`; estado funcional de disponibilidad de la definición; referencia a su versión vigente; fecha de creación; fecha de retiro cuando corresponda.
- **Permanencia:** PERMANENTE.
- **Restricciones:** El identificador nunca se reutiliza para otra obligación. | Desactivar una definición no elimina su historia. | Una definición no crea por sí sola una instancia de trabajo. | Metas, KPI, reglas, checklists y acciones auxiliares no deben registrarse como tareas independientes salvo que representen una obligación real con resultado verificable..

### `catalogo_operativo.flujo_operativo` — FlujoOperativo

Contexto transversal con identidad estable `FLU-####` que agrupa tareas relacionadas por una operación de negocio.

- **PK:** `id_flujo`.
- **Unicidad de negocio:** codigo FLU-####.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_flujo`; `codigo_flujo`; `nombre`; `proposito`; propietario funcional; `activo`; versión o vigencia cuando cambie su significado.
- **Permanencia:** PERMANENTE.
- **Restricciones:** La relación con tareas se modela explícitamente. | Un flujo no activa tareas por el mero hecho de contenerlas. | La secuencia y causalidad sólo existen cuando se declaran mediante reglas o relaciones específicas..

### `catalogo_operativo.flujo_tarea` — FlujoTarea

Relación versionable entre una definición de tarea y un flujo operativo.

- **PK:** `id_flujo_tarea`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** FlujoOperativo, DefinicionTarea.
- **Atributos relevantes:** `id_flujo`; `id_tarea`; vigencia; rol de la tarea dentro del flujo cuando exista un catálogo formal; orden sólo cuando la operación lo defina explícitamente.
- **Permanencia:** PERMANENTE.
- **Restricciones:** No se infiere orden por el código de tarea. | Una tarea puede participar en más de un flujo si el negocio lo requiere..

### `incentivos.definicion_kpi` — DefinicionKPI

Entidad gobernada por el catálogo lógico del sistema.

- **PK:** `id_kpi`.
- **Unicidad de negocio:** codigo KPI-####.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_kpi`; nombre; descripción; unidad; definición de cálculo; fuente de datos; periodicidad de medición; vigencia; `id_kpi` estable; nombre y descripción; unidad de medida; granularidad/contexto; fórmula o estrategia de cálculo versionada; fuente o contrato de datos requerido; periodicidad; regla de redondeo; estado de publicación; propietario funcional.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Cada KPI representa una sola medida interpretable. | Una tarea o proceso puede relacionarse con varios KPI. | Las metas y resultados se modelan separadamente de la definición del KPI. | El cálculo y persistencia de resultados se formaliza en la fase 15..

### `catalogo_operativo.macroproceso` — Macroproceso

Nivel superior de clasificación funcional.

- **PK:** `id_macroproceso`.
- **Unicidad de negocio:** codigo_macroproceso.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_macroproceso`; `codigo` estable; `nombre`; `descripcion`; `activo`; vigencia cuando corresponda.
- **Permanencia:** PERMANENTE.
- **Restricciones:** El código debe ser único. | Un macroproceso puede contener varios procesos. | No se elimina físicamente si existen definiciones históricas asociadas..

### `catalogo_operativo.parametro_regla` — ParametroRegla

Valor tipado administrable asociado a una regla o dominio, con código `PAR-####`.

- **PK:** `id_parametro`.
- **Unicidad de negocio:** codigo PAR-####.
- **FK principales:** ReglaConfigurable.
- **Atributos relevantes:** `id_parametro`; `id_regla` o dominio propietario; clave; tipo de dato; valor tipado; unidad; alcance; vigencia.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `catalogo_operativo.proceso` — Proceso

Agrupación funcional perteneciente a un macroproceso.

- **PK:** `id_proceso`.
- **Unicidad de negocio:** codigo_proceso dentro del macroproceso (por definir sin colisiones).
- **FK principales:** Macroproceso.
- **Atributos relevantes:** `id_proceso`; `codigo` estable; `id_macroproceso`; `nombre`; `descripcion`; `activo`; vigencia.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Su identidad debe ser inequívoca dentro del modelo. | Debe pertenecer a un único macroproceso vigente..

### `catalogo_operativo.regla_configurable` — ReglaConfigurable

Regla persistente sólo cuando su comportamiento sea administrable por el negocio, con código `REG-####`.

- **PK:** `id_regla`.
- **Unicidad de negocio:** codigo REG-####.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_regla`; dominio propietario; tipo de regla estructurado; versión; prioridad/orden sólo cuando exista semántica real de evaluación; activa; vigencia.
- **Permanencia:** PERMANENTE.
- **Restricciones:** No se permite interpretar texto libre como código ejecutable. | Una validación determinista de integridad, elegibilidad, ranking o transición se implementa como función/guard salvo que exista una razón real para administrarla como datos..

### `catalogo_operativo.relacion_tarea` — RelacionTarea

Relación dirigida y tipada entre dos definiciones de tarea.

- **PK:** `id_relacion_tarea`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** DefinicionTarea.
- **Atributos relevantes:** tarea origen; tarea destino; tipo de relación; condición o contexto estructurado cuando corresponda; vigencia.
- **Permanencia:** PERMANENTE.
- **Restricciones:** La relación debe tener semántica explícita. | Los ciclos se rechazan cuando el tipo de dependencia no los permita. | Una relación causal que pueda crear trabajo se ejecuta mediante las reglas de activación de la fase 5, no por esta entidad por sí sola..

### `catalogo_operativo.subproceso` — Subproceso

Unidad funcional que clasifica primariamente las definiciones de tarea.

- **PK:** `id_subproceso`.
- **Unicidad de negocio:** codigo_subproceso dentro del proceso (por definir sin colisiones).
- **FK principales:** Proceso.
- **Atributos relevantes:** `id_subproceso`; `codigo` estable; `id_proceso`; `nombre`; `descripcion`; `activo`; vigencia.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Debe pertenecer a un único proceso vigente. | Una definición de tarea vigente debe poder resolverse a un subproceso válido..

### `catalogo_operativo.version_definicion_tarea` — VersionDefinicionTarea

Contenido versionado que describe qué significa y cómo debe interpretarse una tarea.

- **PK:** `id_version_tarea`.
- **Unicidad de negocio:** id_tarea + numero/version + alcance/vigencia.
- **FK principales:** DefinicionTarea, Subproceso.
- **Atributos relevantes:** `id_version_tarea`; `id_tarea`; número de versión; `vigente_desde`; `vigente_hasta` nullable; nombre; descripción operativa; resultado esperado; criterio de validación funcional; subproceso primario; tipo funcional cuando aplique; tipo de activación como clasificación de alto nivel; perfil o puesto responsable requerido; regla de validación/supervisión requerida; duración estimada nullable; nivel de riesgo; criticidad operativa cuando corresponda; requisito general de evidencia; nivel de automatización; motivo del cambio; autoridad que aprobó la versión cuando sea requerido.
- **Permanencia:** PERMANENTE.
- **Restricciones:** No puede haber dos versiones vigentes incompatibles para la misma tarea y alcance. | Las obligaciones ya creadas deben conservar referencia a la versión que les dio origen o una instantánea equivalente. | La ausencia de duración estimada no invalida por sí sola la identidad de la tarea. | Riesgo y criticidad son conceptos distintos y no deben fusionarse automáticamente. | La configuración detallada de activación no se almacena como una regla libre dentro de esta entidad..

### `activacion.programacion_activacion` — ProgramacionActivacion

Configuración estructurada para activaciones PROGRAMADA.

- **PK:** `id_programacion_activacion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** ReglaActivacion, CalendarioOperativo.
- **Atributos relevantes:** regla de calendario; días de semana o mes; ventanas intradía; fecha de inicio/fin; exclusión o tratamiento de días inhábiles; anticipación respecto a un vencimiento real; granularidad o clave de contexto, por ejemplo servicio.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `activacion.regla_activacion` — ReglaActivacion

Configuración versionable que define el mecanismo por el cual una tarea puede activarse.

- **PK:** `id_regla_activacion`.
- **Unicidad de negocio:** id_tarea + version_tarea + tipo + alcance + version_regla.
- **FK principales:** DefinicionTarea, ReglaTemporal.
- **Atributos relevantes:** identificador estable de la regla; `id_tarea` y versión de definición aplicable; tipo de activación: `PROGRAMADA`, `EVENTO`, `CONDICIONAL` o `PROCESO`; estado y vigencia; alcance operativo cuando corresponda; referencia a configuración específica del mecanismo; política temporal opcional; versión y motivo de cambio.
- **Permanencia:** PERMANENTE.
- **Restricciones:** No puede considerarse ejecutable si faltan parámetros obligatorios. | Una versión retirada no se reutiliza ni se elimina si originó obligaciones históricas. | Dos reglas vigentes para la misma tarea y alcance sólo pueden coexistir si su semántica es explícitamente compatible..

### `activacion.regla_condicion` — ReglaCondicion

Configuración para activaciones CONDICIONAL.

- **PK:** `id_regla_condicion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** ReglaActivacion.
- **Atributos relevantes:** condición estructurada; fuente/contexto evaluado; campos obligatorios; referencia padre cuando exista causalidad; granularidad de generación.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `activacion.regla_evento` — ReglaEvento

Configuración para activaciones EVENTO.

- **PK:** `id_regla_evento`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** ReglaActivacion, TipoEventoNegocio.
- **Atributos relevantes:** tipo de evento aceptado; fuente del evento; predicados requeridos; clave de correlación del caso; campos obligatorios de contexto; reglas de autorización del evento cuando correspondan.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `activacion.regla_proceso_origen` — ReglaProcesoOrigen

Configuración para activaciones PROCESO.

- **PK:** `id_regla_proceso_origen`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** ReglaActivacion.
- **Atributos relevantes:** tipo de caso o proceso origen; identificador del caso; timestamp de origen; campos heredables; condición de entrada; regla temporal asociada.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `activacion.regla_temporal` — ReglaTemporal

Componente reusable para cálculo temporal.

- **PK:** `id_regla_temporal`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** CalendarioOperativo.
- **Atributos relevantes:** tipo: SLA, offset, anticipación o ventana; valor; unidad; punto temporal de origen; calendario aplicable; tratamiento de días inhábiles; regla de redondeo o límite cuando corresponda.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `activacion.tipo_evento_negocio` — TipoEventoNegocio

Catálogo compartido de tipos de evento que pueden ser producidos por otros dominios o integraciones y consumidos por reglas EVENTO.

- **PK:** `id_tipo_evento_negocio`.
- **Unicidad de negocio:** codigo_tipo_evento.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** Atributos mínimos definidos por la semántica del dominio; completar tipos físicos durante implementación sin alterar las claves/reglas aquí consolidadas..
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `obligaciones.instancia_trabajo` — InstanciaTrabajo

Representa una obligación real que debe poder sobrevivir a cambios de asignación, planificación o ejecución.

- **PK:** `id_instancia`.
- **Unicidad de negocio:** clave_idempotencia.
- **FK principales:** DefinicionTarea, ReglaActivacion, PeriodoOperativo.
- **Atributos relevantes:** `id_instancia`; `id_tarea` y `version_definicion`; referencia a regla/version de activación; `clave_idempotencia` con unicidad; timestamp de creación; timestamp de activación; período operativo; momento objetivo o fecha de compromiso inicial cuando corresponda; fecha/hora de vencimiento cuando exista; alcance operativo necesario para distinguir la obligación; tipo e identificador del caso/evento/proceso origen cuando corresponda; referencia a instancia padre sólo cuando exista una relación real entre obligaciones; correlación técnica para auditoría; referencias legacy opcionales de migración.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `elegibilidad.empleado_especialidad` — EmpleadoEspecialidad

Relación histórica entre empleado y especialidad.

- **PK:** `(id_empleado, id_especialidad, vigente_desde)`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** Empleado, Especialidad.
- **Atributos relevantes:** `id_empleado`; `id_especialidad`; vigencia desde/hasta; estado o validación cuando corresponda.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `elegibilidad.especialidad` — Especialidad

Catálogo de competencias o especializaciones operativas que realmente condicionan quién puede ejecutar determinadas tareas.

- **PK:** `id_especialidad`.
- **Unicidad de negocio:** codigo/nombre normalizado de especialidad.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_especialidad`; nombre; descripción; estado.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `elegibilidad.especialidad_requerida_tarea` — EspecialidadRequeridaTarea

Relación entre política de elegibilidad y especialidad obligatoria.

- **PK:** `(id_politica_elegibilidad, id_especialidad)`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** PoliticaElegibilidadTarea, Especialidad.
- **Atributos relevantes:** Atributos mínimos definidos por la semántica del dominio; completar tipos físicos durante implementación sin alterar las claves/reglas aquí consolidadas..
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `elegibilidad.politica_elegibilidad_tarea` — PoliticaElegibilidadTarea

Configuración versionada que indica cómo debe construirse el conjunto de candidatos de una definición de tarea.

- **PK:** `id_politica_elegibilidad`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** DefinicionTarea.
- **Atributos relevantes:** `id_politica_elegibilidad`; `id_tarea` y versión de definición; `modo_elegibilidad`; vigencia; estado de publicación; necesidad de capacidad mínima; necesidad de disponibilidad en fecha; necesidad de contexto origen; `POR_PUESTO`; `EMPLEADO_FIJO`; `RESPONSABLE_ORIGEN`; `POR_ESPECIALIDAD`; `PUESTOS_ALTERNATIVOS`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `elegibilidad.puesto_permitido_tarea` — PuestoPermitidoTarea

Relación entre una política de elegibilidad y uno o varios puestos aceptables.

- **PK:** `(id_politica_elegibilidad, id_puesto)`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** PoliticaElegibilidadTarea, Puesto.
- **Atributos relevantes:** Atributos mínimos definidos por la semántica del dominio; completar tipos físicos durante implementación sin alterar las claves/reglas aquí consolidadas..
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `elegibilidad.turno_permitido_tarea` — TurnoPermitidoTarea

Relación entre una política y turnos compatibles.

- **PK:** `(id_politica_elegibilidad, id_turno)`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** PoliticaElegibilidadTarea, Turno.
- **Atributos relevantes:** Atributos mínimos definidos por la semántica del dominio; completar tipos físicos durante implementación sin alterar las claves/reglas aquí consolidadas..
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `asignacion.asignacion_trabajo` — AsignacionTrabajo

Hecho persistente que vincula una instancia con el empleado responsable una vez confirmada la decisión.

- **PK:** `id_asignacion`.
- **Unicidad de negocio:** id_instancia con una sola asignacion ACTIVA.
- **FK principales:** InstanciaTrabajo, Empleado.
- **Atributos relevantes:** `id_asignacion`; `id_instancia`; `id_empleado`; `id_politica_asignacion` y versión aplicada; `origen_asignacion`: AUTOMATICA, MANUAL o CONTEXTO_ORIGEN; `fecha_hora_asignacion`; `estado_asignacion`; identidad del usuario o servicio que confirmó; clave idempotente de la operación cuando corresponda.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Una instancia tiene como máximo una asignación `ACTIVA`. | La reasignación no sobrescribe silenciosamente el hecho anterior; su tratamiento se gobierna en F13 y su trazabilidad en F17. | El nombre, puesto o turno visibles del empleado no sustituyen `id_empleado`. | No se almacenan `minutos_antes` ni `minutos_despues` como verdad primaria si pueden reconstruirse desde la capacidad y las asignaciones confirmadas..

### `asignacion.criterio_ranking_asignacion` — CriterioRankingAsignacion

Configuración versionada de criterios que pueden ordenar candidatos dentro de una política.

- **PK:** `id_criterio_ranking`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** PoliticaAsignacionTarea.
- **Atributos relevantes:** `id_criterio_ranking`; `id_politica_asignacion`; `tipo_criterio`; orden de aplicación; dirección o preferencia; peso únicamente cuando el negocio haya aprobado un modelo ponderado; vigencia y estado; capacidad asignable restante; carga ya comprometida; repetición de la misma tarea durante el período; prioridad de puesto; prioridad de especialidad; prioridad de empleado específico; otros criterios de negocio formalmente definidos.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `asignacion.politica_asignacion_tarea` — PoliticaAsignacionTarea

Configuración versionada que determina cómo se selecciona responsable para una definición de tarea.

- **PK:** `id_politica_asignacion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** DefinicionTarea.
- **Atributos relevantes:** `id_politica_asignacion`; `id_tarea` y versión de definición; `modo_asignacion`; vigencia; estado de publicación; indicador de consumo de capacidad; referencia a la política de ranking aplicable cuando corresponda; `AUTOMATICA_BALANCEADA`: rankea múltiples candidatos elegibles; `UNICO_ELEGIBLE`: confirma sólo cuando F7 devuelve exactamente un candidato; `PRIORIDAD_CONFIGURADA`: aplica una cadena explícita de preferencias; `RESPONSABLE_ORIGEN`: consume la identidad contextual resuelta por F7; no vuelve a inferirla; `MANUAL`: no selecciona automáticamente.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `asignacion.prioridad_asignacion` — PrioridadAsignacion

Relación configurada que expresa una preferencia discreta dentro de una política.

- **PK:** `id_prioridad_asignacion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** PoliticaAsignacionTarea.
- **Atributos relevantes:** `id_prioridad_asignacion`; `id_politica_asignacion`; `tipo_objetivo`: PUESTO, ESPECIALIDAD o EMPLEADO; identificador canónico del objetivo; `prioridad`; vigencia; estado.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `planificacion.partida_plan` — PartidaPlan

Representa la incorporación de una obligación concreta al plan.

- **PK:** `id_partida_plan`.
- **Unicidad de negocio:** id_plan + id_instancia.
- **FK principales:** PlanOperativo, InstanciaTrabajo, AsignacionTrabajo.
- **Atributos relevantes:** `id_partida_plan`; `id_plan`; `id_instancia`; `id_asignacion_publicada`; fecha/hora o ventana planificada cuando aplique; estado de la partida; fecha/hora de publicación; identidad de publicación; tipo de incorporación: inicial o incremental cuando corresponda; referencia de publicación/correlación para auditoría.
- **Permanencia:** PERMANENTE.
- **Restricciones:** `(id_plan, id_instancia)` debe ser único para partidas activas. | `id_instancia` debe pertenecer al mismo período y alcance que el plan. | `id_asignacion_publicada` debe ser una asignación activa de la misma instancia al momento de publicar. | La partida no recrea descripción, puesto, supervisor, evidencia, bono o reglas de la tarea si éstos ya pueden reconstruirse desde entidades versionadas..

### `planificacion.plan_operativo` — PlanOperativo

Representa el plan de un período y alcance operativo.

- **PK:** `id_plan`.
- **Unicidad de negocio:** id_periodo_operativo + alcance con un solo plan activo equivalente.
- **FK principales:** PeriodoOperativo.
- **Atributos relevantes:** `id_plan`; `id_periodo_operativo`; alcance organizativo cuando corresponda; `estado_plan`; fecha/hora de creación; fecha/hora de primera publicación; identidad de quien publica; versión sólo cuando exista una necesidad real de conservar versiones completas; metadatos de auditoría y correlación cuando correspondan.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Debe existir una regla de unicidad para evitar dos planes activos equivalentes sobre el mismo período y alcance. | El estado del plan no reemplaza el estado del `PeriodoOperativo`; ambas máquinas tienen responsabilidades distintas y deben permanecer consistentes. | La ausencia de una versión histórica completa no impide auditar cambios: F17 puede conservar eventos y F13 conserva hechos de excepción..

### `planificacion.publicacion_plan` — PublicacionPlan

Hecho de negocio opcional cuando se necesite agrupar y auditar un lote publicado, especialmente para publicaciones incrementales.

- **PK:** `id_publicacion_plan`.
- **Unicidad de negocio:** id_plan + clave_idempotente_comando.
- **FK principales:** PlanOperativo, Usuario.
- **Atributos relevantes:** `id_publicacion_plan`; `id_plan`; tipo de publicación: `INICIAL` o `INCREMENTAL`; timestamp; usuario/servicio autorizador; clave idempotente del comando; número de partidas incluidas; resultado.
- **Permanencia:** CONDICIONAL_SEGUN_NECESIDAD.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `ejecucion.ejecucion_tarea` — EjecucionTarea

Representa una ejecución real iniciada sobre una partida publicada.

- **PK:** `id_ejecucion`.
- **Unicidad de negocio:** id_partida_plan con ejecucion compatible unica segun politica.
- **FK principales:** PartidaPlan, InstanciaTrabajo, AsignacionTrabajo.
- **Atributos relevantes:** `id_ejecucion`; `id_partida_plan`; `id_asignacion_ejecutada` o referencia equivalente a la asignación vigente al iniciar; `estado_ejecucion`; `iniciada_en`; `iniciada_por`; `concluida_en`, nullable mientras siga en curso; `concluida_por`, nullable mientras siga en curso; observaciones del responsable cuando correspondan; clave idempotente del comando de inicio/conclusión; metadatos mínimos de correlación y auditoría.
- **Permanencia:** PERMANENTE.
- **Restricciones:** `id_ejecucion` es opaco y único. | Una partida no puede tener dos ejecuciones activas simultáneas. | En el alcance definido, una partida tiene como máximo una ejecución ordinaria; intentos adicionales o reaperturas requieren una política explícita de F12/F13 y no se permiten por defecto. | `concluida_en >= iniciada_en` cuando ambos timestamps existan. | Una ejecución no puede iniciarse sobre una partida no publicada, cerrada, anulada o no ejecutable. | La asignación asociada debe corresponder a la misma obligación y estar vigente en el momento de inicio..

### `ejecucion.transicion_estado_ejecucion` — TransicionEstadoEjecucion

Historial append-only del ciclo de vida operativo.

- **PK:** `id_transicion_ejecucion`.
- **Unicidad de negocio:** id_transicion_ejecucion.
- **FK principales:** EjecucionTarea, Usuario.
- **Atributos relevantes:** `id_transicion`; `id_ejecucion`; `estado_anterior`; `estado_nuevo`; timestamp; actor; comando o causa; referencia de correlación/idempotencia.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `evidencias.evidencia` — Evidencia

Hecho persistente registrado para una ejecución.

- **PK:** `id_evidencia`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** EjecucionTarea, TipoEvidencia, Usuario.
- **Atributos relevantes:** `id_evidencia` opaco y único; `id_ejecucion`; `id_tipo_evidencia`; descripción u observación del aportante cuando corresponda; `registrada_en`; `registrada_por`; `id_evidencia_reemplazada`, nullable cuando sea una sustitución; estado de integridad estructural cuando se materialice para auditoría.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `evidencias.referencia_evidencia` — ReferenciaEvidencia

Referencia verificable asociada a una evidencia cuando el tipo la permite.

- **PK:** `id_referencia_evidencia`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** Evidencia.
- **Atributos relevantes:** `id_referencia_evidencia`; `id_evidencia`; clase de referencia permitida por el tipo; valor de referencia; metadatos mínimos de integridad o localización cuando existan.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `evidencias.requisito_evidencia` — RequisitoEvidencia

Configuración persistente asociada a una versión de definición de tarea o a una regla contextual explícita.

- **PK:** `id_requisito_evidencia`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** VersionDefinicionTarea.
- **Atributos relevantes:** `id_requisito_evidencia`; `id_version_tarea` o definición aplicable; descripción de la evidencia esperada; `modo_cumplimiento`: `TODAS`, `CUALQUIERA` o `AL_MENOS_N`; `cantidad_minima` cuando aplique; vigencia; prioridad/orden cuando existan varios grupos de requisito.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `evidencias.requisito_evidencia_tipo` — RequisitoEvidenciaTipo

Relación entre un requisito y uno o más `TipoEvidencia` permitidos o exigidos.

- **PK:** `(id_requisito_evidencia, id_tipo_evidencia)`.
- **Unicidad de negocio:** id_requisito_evidencia + id_tipo_evidencia.
- **FK principales:** RequisitoEvidencia, TipoEvidencia.
- **Atributos relevantes:** `id_requisito_evidencia`; `id_tipo_evidencia`; cantidad mínima específica cuando la regla lo requiera; condición contextual opcional si está formalmente definida.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `evidencias.tipo_evidencia` — TipoEvidencia

Catálogo de modalidades válidas de prueba.

- **PK:** `id_tipo_evidencia`.
- **Unicidad de negocio:** codigo_tipo_evidencia.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_tipo_evidencia` o código estable; nombre; descripción; permite referencia tipo link; permite referencia tipo folio; permite observación; estado activo/inactivo; vigencia cuando sea necesaria.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `validacion.criterio_validacion` — CriterioValidacion

Regla verificable que define qué significa cumplimiento para una versión de tarea.

- **PK:** `id_criterio_validacion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** PoliticaValidacion.
- **Atributos relevantes:** `id_criterio_validacion`; `id_politica_validacion`; `codigo_criterio`; `descripcion`; `orden_evaluacion`; `obligatorio`; `activo`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Los criterios múltiples deben conservar identidad y orden propios; no se depende de un texto concatenado para auditar qué condición falló. | Un criterio puede requerir evidencia de F11, datos operativos o una comprobación humana. | La ausencia de criterios en una política que exige validación es un error de configuración..

### `validacion.evaluacion_criterio` — EvaluacionCriterio

Detalle persistente cuando la política requiere demostrar cómo se obtuvo la decisión.

- **PK:** `id_evaluacion_criterio`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** Validacion, CriterioValidacion.
- **Atributos relevantes:** `id_evaluacion_criterio`; `id_validacion`; `id_criterio_validacion`; `resultado_criterio`; `observacion` nullable; referencias a evidencias o datos de soporte cuando corresponda.
- **Permanencia:** CONDICIONAL_SEGUN_NECESIDAD.
- **Restricciones:** Debe conservar el criterio exacto y su versión aplicable al momento de la decisión. | No modifica la evidencia de F11; sólo la referencia como fundamento..

### `validacion.politica_validacion` — PoliticaValidacion

Configuración persistente y versionada que define cómo se valida una obligación.

- **PK:** `id_politica_validacion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** VersionDefinicionTarea.
- **Atributos relevantes:** `id_politica_validacion`; `id_version_tarea` o referencia equivalente a la definición vigente; `requiere_validacion`; `codigo_permiso_validador` o regla de autoridad; `tipo_alcance_validador`; `permite_autovalidacion`; `requiere_autorizacion_adicional`; `tipo_autorizacion_requerida` nullable; `vigente_desde`; `vigente_hasta` nullable; `activa`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Debe existir como máximo una política efectiva no ambigua para el mismo contexto de validación. | La política no debe identificar al validador por nombre visible. | Cuando la autoridad dependa de un rol, puesto o nivel organizacional, la equivalencia se resuelve mediante configuración de F2. | Una política incompleta no puede habilitar validación productiva..

### `validacion.validacion` — Validacion

Decisión persistente sobre una ejecución.

- **PK:** `id_validacion`.
- **Unicidad de negocio:** id_ejecucion + version/decision vigente sin sobreescritura.
- **FK principales:** EjecucionTarea, PoliticaValidacion, Usuario, Autorizacion.
- **Atributos relevantes:** `id_validacion`; `id_ejecucion`; `id_politica_validacion`; `id_usuario_validador`; `fecha_hora_validacion`; `resultado`; `observaciones`; `motivo_no_cumplimiento` nullable; `id_validacion_anterior` nullable cuando sustituya una decisión; `estado_decision` para distinguir una decisión vigente de una revocada o sustituida; `id_correlacion_lote` nullable.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Una validación debe identificar al usuario real que tomó la decisión. | Una ejecución no puede tener dos decisiones vigentes incompatibles. | `NO_CUMPLIDA` requiere motivo explícito. | `INCOMPLETA` debe identificar al menos los criterios no satisfechos o una observación equivalente y trazable. | `CUMPLIDA` requiere todos los gates obligatorios satisfechos. | La validación no modifica directamente el bono ni el KPI..

### `excepciones.cambio_compromiso` — CambioCompromiso

Hecho persistente para toda modificación de fecha o ventana de compromiso.

- **PK:** `id_cambio_compromiso`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** ExcepcionCicloVida, InstanciaTrabajo.
- **Atributos relevantes:** `id_cambio_compromiso`; `id_excepcion`; `id_instancia`; compromiso anterior; compromiso nuevo; período origen y período destino cuando corresponda; motivo y timestamp efectivo.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `excepciones.causa_excepcion` — CausaExcepcion

Catálogo de causas normalizadas.

- **PK:** `id_causa_excepcion`.
- **Unicidad de negocio:** codigo_causa.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_causa_excepcion`; código y descripción; tipos de excepción a los que aplica; requiere acción correctiva; requiere escalamiento; nivel/regla de autorización sugerida; vigencia.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `excepciones.decision_excepcion` — DecisionExcepcion

Historial append-only de decisiones sobre una excepción.

- **PK:** `id_decision_excepcion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** ExcepcionCicloVida, Autorizacion, Usuario.
- **Atributos relevantes:** `id_decision_excepcion`; `id_excepcion`; tipo de decisión: autorizar, rechazar, evaluar, revocar; actor; fecha/hora; motivo/resolución; `id_autorizacion` nullable; referencias de política y correlación.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `excepciones.excepcion_ciclo_vida` — ExcepcionCicloVida

Hecho principal de F13.

- **PK:** `id_excepcion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** InstanciaTrabajo, PartidaPlan, EjecucionTarea, CausaExcepcion, Autorizacion.
- **Atributos relevantes:** `id_excepcion`; `id_instancia`; `id_partida_plan` nullable; `id_ejecucion` nullable; `tipo_excepcion`: `CANCELACION`, `POSPOSICION`, `ARRASTRE_CONTINUACION`, `REASIGNACION` u otro tipo aprobado; `id_causa_excepcion` nullable sólo cuando la política lo permita; motivo/detalle; estado de excepción; solicitada_en / solicitada_por; aplicada_en / aplicada_por nullable; clave idempotente y correlación de auditoría.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `excepciones.incidencia_operativa` — IncidenciaOperativa

Se crea sólo cuando una incidencia necesita identidad y seguimiento independientes de la excepción.

- **PK:** `id_incidencia_operativa`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** InstanciaTrabajo.
- **Atributos relevantes:** Atributos mínimos definidos por la semántica del dominio; completar tipos físicos durante implementación sin alterar las claves/reglas aquí consolidadas..
- **Permanencia:** CONDICIONAL_SEGUN_NECESIDAD.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `excepciones.politica_excepcion` — PoliticaExcepcion

Configuración versionada por definición de tarea y contexto.

- **PK:** `id_politica_excepcion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** VersionDefinicionTarea.
- **Atributos relevantes:** `id_politica_excepcion`; `id_version_tarea`; `tipo_excepcion`; `permitida`; estados/condiciones de origen permitidos; regla de fecha o ventana cuando corresponda; regla de causa obligatoria; regla de autorización; límite o tratamiento de recurrencia; `genera_obligacion_distinta`; vigencia y estado activo.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `excepciones.reasignacion` — Reasignacion

Hecho persistente que modifica la responsabilidad de una obligación.

- **PK:** `id_reasignacion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** ExcepcionCicloVida, InstanciaTrabajo, AsignacionTrabajo.
- **Atributos relevantes:** `id_reasignacion`; `id_excepcion`; `id_instancia`; `id_asignacion_anterior`; `id_asignacion_nueva`; motivo; efectiva_en; actor y autorización.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `excepciones.relacion_obligacion` — RelacionObligacion

Vincula una obligación origen con otra obligación realmente distinta creada por una excepción.

- **PK:** `id_relacion_obligacion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** InstanciaTrabajo, ExcepcionCicloVida.
- **Atributos relevantes:** Atributos mínimos definidos por la semántica del dominio; completar tipos físicos durante implementación sin alterar las claves/reglas aquí consolidadas..
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `cierre.cierre_periodo` — CierrePeriodo

Representa el hecho de que un período cumplió sus gates y quedó cerrado definitivamente.

- **PK:** `id_cierre_periodo`.
- **Unicidad de negocio:** id_periodo_operativo (un cierre definitivo).
- **FK principales:** PeriodoOperativo, PoliticaCierrePeriodo, Usuario, Autorizacion.
- **Atributos relevantes:** `id_cierre_periodo`; `id_periodo_operativo` único; `cerrado_en`; `cerrado_por`; `id_politica_cierre` / versión aplicada; conteos de control o huella de evaluación cuando se requiera auditoría; correlación de transacción/auditoría; observación o motivo autorizado nullable.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `cierre.politica_cierre_periodo` — PoliticaCierrePeriodo

Configuración versionada de los gates de cierre.

- **PK:** `id_politica_cierre`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** Organizacion.
- **Atributos relevantes:** `id_politica_cierre`; vigencia; estados de período desde los que se permite cerrar; tratamientos terminales aceptados; regla de validaciones pendientes; regla de excepciones pendientes; regla de autoridad/autorización; controles adicionales configurables.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `incentivos.asignacion_politica_incentivo` — AsignacionPoliticaIncentivo

Vincula una política con la población a la que aplica.

- **PK:** `id_asignacion_politica`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** PoliticaIncentivo, Empleado, Puesto, Sucursal.
- **Atributos relevantes:** `id_asignacion_politica`; `id_politica_incentivo`; alcance por empleado, puesto, rol, sucursal o criterio autorizado; base individual cuando corresponda; vigencia; prioridad o regla de coexistencia entre políticas.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `incentivos.lote_nomina` — LoteNomina

Agrupa movimientos de un corte cuando el receptor requiera una unidad de transferencia o conciliación.

- **PK:** `id_lote_nomina`.
- **Unicidad de negocio:** identificador_corte/receptor + version.
- **FK principales:** PeriodoOperativo.
- **Atributos relevantes:** `id_lote_nomina`; período/corte; creado_en / creado_por; total de movimientos e importe; estado; huella del contenido; referencia externa.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `incentivos.medicion_kpi` — MedicionKPI

Se persiste cuando el valor participa en incentivos, auditoría, cierre o comparación histórica no reproducible con garantías suficientes.

- **PK:** `id_medicion_kpi`.
- **Unicidad de negocio:** id_kpi + version + periodo/ventana + contexto.
- **FK principales:** DefinicionKPI, PeriodoOperativo.
- **Atributos relevantes:** `id_medicion_kpi`; `id_kpi` y versión; `id_periodo_operativo` o ventana temporal; contexto de medición: empleado, sucursal, proceso, tarea u otro; valor; numerador/denominador cuando corresponda; unidad; calculado_en; versión de fuente o huella de entradas; estado de calidad/revisión.
- **Permanencia:** CONDICIONAL_SEGUN_NECESIDAD.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `incentivos.movimiento_nomina` — MovimientoNomina

Instrucción económica que cruza la frontera entre SGOL y nómina.

- **PK:** `id_movimiento_nomina`.
- **Unicidad de negocio:** clave_idempotencia_movimiento.
- **FK principales:** ResultadoIncentivo, Empleado, LoteNomina.
- **Atributos relevantes:** `id_movimiento_nomina`; `id_resultado_incentivo`; `id_empleado`; período/corte de nómina destino; código de concepto de nómina; importe y moneda; signo/tipo de movimiento; clave idempotente única; estado de integración; referencia externa cuando exista; fecha de preparación, envío y confirmación.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `incentivos.politica_incentivo` — PoliticaIncentivo

Representa una regla económica aprobada y versionada.

- **PK:** `id_politica_incentivo`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_politica_incentivo`; código y nombre; tipo de incentivo; versión; vigencia; estado `BORRADOR`, `VIGENTE`, `RETIRADA`; base económica o estrategia para obtenerla; KPI/hechos requeridos; umbrales; factores de cumplimiento; reglas de prorrateo; exclusiones y excepciones; reglas de redondeo; necesidad de aprobación manual; autoridad que aprobó la versión.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `incentivos.resultado_incentivo` — ResultadoIncentivo

Hecho económico calculado para una persona y un período.

- **PK:** `id_resultado_incentivo`.
- **Unicidad de negocio:** id_empleado + id_periodo_operativo + id_politica/version.
- **FK principales:** PeriodoOperativo, Empleado, PoliticaIncentivo.
- **Atributos relevantes:** `id_resultado_incentivo`; `id_periodo_operativo`; `id_empleado`; `id_politica_incentivo` y versión; base elegible; factores aplicados; monto calculado; moneda; detalle explicable de componentes; estado; calculado_en; aprobado_en / aprobado_por cuando aplique; huella de entradas y mediciones KPI utilizadas; referencia a corrección o versión anterior.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `auditoria.corrida_sistema` — CorridaSistema

Existe sólo cuando una ejecución técnica necesita conservar inicio, fin, parámetros, resultado, conteos o error como una unidad persistente.

- **PK:** `id_corrida`.
- **Unicidad de negocio:** id_corrida opaco; no reutilizable.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_corrida`; `tipo_corrida`; `id_operacion` raíz; `id_correlacion`; `fecha_hora_inicio`; `fecha_hora_fin`; `estado_corrida`; `tipo_disparador`; `id_usuario_iniciador` o identidad de servicio; versión del componente; parámetros normalizados o referencia segura a ellos; hash de entrada cuando sea útil; conteos de entrada/salida; `id_corrida_origen` cuando sea un reintento; resumen del resultado; referencia al error principal cuando exista.
- **Permanencia:** CONDICIONAL_SEGUN_NECESIDAD.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `auditoria.error_sistema` — ErrorSistema

Se persiste cuando el error necesita investigación, correlación, reintento, alerta o histórico operativo.

- **PK:** `id_error`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** CorridaSistema.
- **Atributos relevantes:** `id_error`; `fecha_hora`; `id_operacion`; `id_correlacion`; `id_corrida` cuando aplique; `codigo_error`; `categoria_error`; `componente`; mensaje seguro; detalle técnico sanitizado o referencia a telemetría externa; `reintentable`; severidad; referencia a entidad afectada cuando sea conocida.
- **Permanencia:** CONDICIONAL_SEGUN_NECESIDAD.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `auditoria.evento_auditoria` — EventoAuditoria

Representa un hecho auditable sobre una entidad o decisión de negocio.

- **PK:** `id_evento_auditoria`.
- **Unicidad de negocio:** id_evento_auditoria; append-only.
- **FK principales:** Usuario, PeriodoOperativo, Autorizacion.
- **Atributos relevantes:** `id_evento_auditoria`; `fecha_hora`; `id_operacion`; `id_correlacion` cuando aplique; `id_usuario` o identidad del actor técnico; `tipo_actor`; `accion`; `tipo_entidad`; `id_entidad`; `id_periodo_operativo` cuando aplique; `resultado`; `motivo` cuando sea requerido; `id_autorizacion` cuando exista autorización F12/F13/F14/F15; referencia o resumen controlado del estado anterior; referencia o resumen controlado del estado posterior; versión del servicio/componente; metadatos técnicos no sensibles.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `auditoria.punto_recuperacion` — PuntoRecuperacion

Es metadato de infraestructura, no una copia del contenido de negocio dentro del mismo esquema.

- **PK:** `id_punto_recuperacion`.
- **Unicidad de negocio:** No definida como clave natural; la identidad persistente es opaca y las restricciones de unicidad provienen de sus relaciones/vigencia..
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_punto_recuperacion`; `fecha_hora_creacion`; tipo; referencia segura al almacenamiento; versión/esquema; checksum o evidencia de integridad; estado de verificación; política de retención; `creado_por`; última prueba de restauración cuando corresponda.
- **Permanencia:** CONDICIONAL_SEGUN_NECESIDAD.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `auditoria.registro_idempotencia` — RegistroIdempotencia

Persiste el control de una solicitud que puede reintentarse o recibirse más de una vez.

- **PK:** `idempotency_key`.
- **Unicidad de negocio:** idempotency_key.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `idempotency_key`; `tipo_operacion`; `request_hash`; `estado`; `id_operacion`; referencia al resultado confirmado; `creado_en`; `actualizado_en`; expiración cuando la política lo permita.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `migracion.control_coexistencia` — ControlCoexistencia

Configuración temporal que determina qué productor tiene autoridad para generar una obligación durante el periodo de transición.

- **PK:** `id_control`.
- **Unicidad de negocio:** alcance_semantico + clave_contexto + vigencia.
- **FK principales:** Autorizacion.
- **Atributos relevantes:** `id_control`; `alcance_semantico`; `clave_contexto` cuando aplique; `productor_autorizado`; `productor_bloqueado`; `estado`; `motivo`; `vigente_desde`; `vigente_hasta`; `id_autorizacion_cutover`; `LEGACY`; `CANONICO`; `NINGUNO` para una suspensión controlada.
- **Permanencia:** TEMPORAL_HASTA_CUTOVER.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `migracion.mapa_equivalencia_migracion` — MapaEquivalenciaMigracion

Estructura temporal y versionada que describe cómo se traduce una identidad o concepto de origen hacia el modelo canónico.

- **PK:** `id_mapa`.
- **Unicidad de negocio:** version_mapa + tipo_objeto_origen + id_origen + contexto.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_mapa`; `version_mapa`; `tipo_objeto_origen`; `id_origen`; `tipo_destino`; `id_destino` cuando exista; `tipo_relacion`; `contexto_requerido`; `confianza`; `estado_resolucion`; `evidencia_decision`; `vigente_desde`; `vigente_hasta`.
- **Permanencia:** TEMPORAL_HASTA_CUTOVER.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `migracion.referencia_origen_legacy` — ReferenciaOrigenLegacy

Referencia persistente que permite localizar el registro original del que provino una entidad o hecho canónico.

- **PK:** `id_referencia_origen`.
- **Unicidad de negocio:** tipo_origen + id_origen.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_referencia_origen`; `sistema_origen`; `tipo_objeto_origen`; `id_objeto_origen`; `tipo_entidad_destino`; `id_entidad_destino`; `id_corrida_migracion`; `estado_reconciliacion`; `transformacion_aplicada`; `hash_origen` o evidencia equivalente cuando sea útil; `creado_en`.
- **Permanencia:** TEMPORAL_HASTA_CUTOVER.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `integracion.checkpoint_sincronizacion` — CheckpointSincronizacion

Se utiliza únicamente en integraciones que leen cambios incrementales de una fuente.

- **PK:** `(id_integracion, particion)`.
- **Unicidad de negocio:** id_integracion + particion.
- **FK principales:** ConfiguracionIntegracion, LoteIntegracion.
- **Atributos relevantes:** `id_integracion`; `particion` cuando aplique; `cursor_confirmado`; `confirmado_en`; `id_lote_integracion`.
- **Permanencia:** CONDICIONAL_SEGUN_NECESIDAD.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `integracion.configuracion_integracion` — ConfiguracionIntegracion

Contrato lógico y versionado de una integración concreta.

- **PK:** `id_integracion`.
- **Unicidad de negocio:** id_sistema_externo + nombre + version_contrato.
- **FK principales:** SistemaExterno.
- **Atributos relevantes:** `id_integracion`; `id_sistema_externo`; `nombre`; `direccion` (`ENTRADA`, `SALIDA`, `BIDIRECCIONAL`); `modo_intercambio`; `version_contrato`; `version_esquema`; `estado`; `politica_idempotencia`; `politica_reintento`; `vigente_desde`; `vigente_hasta`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `integracion.lote_integracion` — LoteIntegracion

Representa una importación o exportación por lote cuando su recuperación o reconciliación necesita persistencia.

- **PK:** `id_lote_integracion`.
- **Unicidad de negocio:** id_integracion + identificador_origen/huella_contenido.
- **FK principales:** ConfiguracionIntegracion, CorridaSistema.
- **Atributos relevantes:** `id_lote_integracion`; `id_integracion`; `id_corrida_sistema`; `identificador_origen`; `huella_contenido`; `version_esquema`; `recibido_en`; `estado`; `registros_recibidos`; `registros_aplicados`; `registros_rechazados`; `checkpoint_origen` cuando corresponda.
- **Permanencia:** CONDICIONAL_SEGUN_NECESIDAD.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `integracion.mensaje_integracion` — MensajeIntegracion

Se persiste únicamente cuando un mensaje individual necesita reintento, recuperación, correlación o auditoría.

- **PK:** `id_mensaje_integracion`.
- **Unicidad de negocio:** id_integracion + clave_idempotencia.
- **FK principales:** ConfiguracionIntegracion, ErrorSistema.
- **Atributos relevantes:** `id_mensaje_integracion`; `id_integracion`; `direccion`; `id_mensaje_externo` cuando exista; `id_operacion` F17; `clave_idempotencia`; `tipo_mensaje`; `version_esquema`; `estado`; `recibido_o_emitido_en`; `numero_intentos`; `ultimo_error` o referencia al error F17; `correlacion_negocio`.
- **Permanencia:** CONDICIONAL_SEGUN_NECESIDAD.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `integracion.referencia_externa` — ReferenciaExterna

Vincula una entidad SGOL con la identidad asignada por otro sistema.

- **PK:** `id_referencia_externa`.
- **Unicidad de negocio:** id_sistema_externo + tipo_objeto_externo + id_objeto_externo + vigencia.
- **FK principales:** SistemaExterno.
- **Atributos relevantes:** `id_referencia_externa`; `id_sistema_externo`; `tipo_entidad_sgol`; `id_entidad_sgol`; `tipo_objeto_externo`; `id_objeto_externo`; `vigente_desde`; `vigente_hasta`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

### `integracion.sistema_externo` — SistemaExterno

Catálogo de sistemas, servicios, fuentes o canales con los que SGOL puede intercambiar información.

- **PK:** `id_sistema_externo`.
- **Unicidad de negocio:** codigo.
- **FK principales:** No aplica / polimorfica cuando se indique.
- **Atributos relevantes:** `id_sistema_externo`; `codigo`; `nombre`; `tipo`; `dueno_dato`; `estado`; `clasificacion_datos`; `responsable_negocio`; `responsable_tecnico`.
- **Permanencia:** PERMANENTE.
- **Restricciones:** Aplican las reglas de dominio y de vigencia documentadas en su fase propietaria..

## 8. Catálogos

Los catálogos permanentes incluyen organización funcional, puestos, turnos, áreas, roles, permisos, estados de período, macroprocesos/procesos/subprocesos, flujos, tipos de evento, especialidades, tipos de evidencia, causas de excepción y sistemas externos. Todo catálogo con código de negocio debe imponer unicidad y conservar histórico de desactivación cuando haya sido referenciado.

## 9. Relaciones

Las relaciones se expresan mediante FKs directas siempre que el tipo sea conocido. Referencias polimórficas sólo se admiten en auditoría e integración cuando el contrato incluye `tipo_entidad + id_entidad`. Toda relación temporal debe resolver vigencia para la fecha de contexto y rechazar solapamientos incompatibles. La matriz privada contiene el inventario expandido de FKs.

## 10. Reglas de negocio

1. Empleado activo, disponibilidad y capacidad son conceptos distintos.
2. Rol de seguridad, puesto y responsabilidad operativa no son equivalentes automáticos.
3. Activar una tarea no crea por sí mismo plan ni ejecución; F5 solicita y F6 crea la obligación.
4. SLA es una regla temporal superpuesta, no un tipo de activación.
5. Elegibilidad determina candidatos; asignación selecciona y persiste responsable.
6. `InstanciaTrabajo`, `PlanOperativo/PartidaPlan`, `AsignacionTrabajo` y `EjecucionTarea` tienen identidades independientes.
7. Una ejecución sólo existe cuando el trabajo se inicia realmente.
8. Evidencia, validación y excepción son hechos independientes de la ejecución.
9. Posponer, arrastrar o reasignar conserva la identidad de la obligación salvo creación explícita de una obligación distinta.
10. El cierre evalúa todas las obligaciones del período y exige tratamiento terminal válido.
11. KPI e incentivos se calculan con políticas versionadas; una clasificación operativa no se convierte automáticamente en dinero.
12. Las vistas por rol derivan datos desde las fuentes canónicas y nunca duplican la autoridad de negocio.
13. Respaldo, rollback transaccional y compensación son mecanismos distintos.
14. Migración no autoriza doble generación.
15. Ninguna integración externa puede saltar permisos, gates o máquinas de estado internas.

## 11. Catálogo de funciones

| Fase | Tipo | Nombre | Contrato |
| --- | --- | --- | --- |
| F01 | FUNCION | calcular_limite_asignable(id_empleado, fecha, periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | FUNCION | calcular_minutos_asignados(id_empleado, periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | FUNCION | calcular_minutos_disponibles(id_empleado, periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | FUNCION | calcular_minutos_libres(id_empleado, periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | FUNCION | calcular_utilizacion_capacidad(id_empleado, periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | FUNCION | clasificar_estado_capacidad(utilizacion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | FUNCION | determinar_empleado_activo(id_empleado, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | FUNCION | listar_personal_disponible(periodo, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | FUNCION | obtener_disponibilidad_dia(id_empleado, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | FUNCION | obtener_disponibilidad_periodo(id_empleado, periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | FUNCION | obtener_puesto_vigente(id_empleado, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | FUNCION | obtener_turno_vigente(id_empleado, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | FUNCION | resolver_politica_capacidad(id_puesto, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | FUNCION | listar_roles_vigentes(id_usuario, fecha, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | FUNCION | puede_validar(id_usuario, contexto_validacion, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | FUNCION | resolver_alcance_usuario(id_usuario, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | FUNCION | resolver_autoridad_requerida(tipo_operacion, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | FUNCION | resolver_equivalencia_puesto_rol(id_puesto, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | FUNCION | resolver_usuario_autenticado(identidad_autenticada) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | FUNCION | tiene_permiso(id_usuario, codigo_permiso, contexto, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | FUNCION | validar_autorizacion(id_usuario, tipo_operacion, referencia_objeto, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F03 | FUNCION | calcular_dias_operativos(fecha_inicio, fecha_fin, alcance) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F03 | FUNCION | clasificar_periodo_respecto_hoy(id_periodo_operativo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F03 | FUNCION | determinar_dia_operativo(fecha, alcance) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F03 | FUNCION | obtener_anio_semana_iso(fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F03 | FUNCION | obtener_configuracion(clave, alcance, fecha_vigencia) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F03 | FUNCION | obtener_limites_periodo_iso(anio_iso, semana_iso) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F03 | FUNCION | obtener_periodo_operativo(fecha, alcance) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F03 | FUNCION | trasladar_fecha_operativa(fecha, direccion, alcance) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F03 | FUNCION | validar_estado_periodo(id_periodo_operativo, estados_permitidos) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F04 | FUNCION | determinar_version_vigente_tarea(id_tarea, fecha_contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F04 | FUNCION | obtener_regla_configurable(id_regla, alcance, fecha_contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F04 | FUNCION | resolver_checklists_tarea(id_tarea, version) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F04 | FUNCION | resolver_dependencias_tarea(id_tarea, version) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F04 | FUNCION | resolver_flujos_tarea(id_tarea, version) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F04 | FUNCION | resolver_jerarquia_tarea(id_tarea, version) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F04 | FUNCION | resolver_kpi_tarea(id_tarea, version) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F04 | FUNCION | validar_definicion_tarea(id_tarea, version) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | FUNCION | CalcularFechaHabilObjetivo(fecha, politica_calendario) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | FUNCION | CalcularMomentoPorAnticipacion(vencimiento, cantidad, unidad, calendario) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | FUNCION | CalcularVencimiento(origen_temporal, regla_temporal, calendario) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | FUNCION | ConstruirClaveIdempotenciaActivacion(regla, contexto, momento_logico) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | FUNCION | EvaluarCondicion(regla, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | FUNCION | EvaluarEvento(regla, evento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | FUNCION | EvaluarProgramacion(regla, calendario, timestamp) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | FUNCION | ProcesarContextoActivacion(...) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | FUNCION | ResolverOrigenProceso(regla, caso) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | FUNCION | ResolverVentanasIntradia(regla, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | FUNCION | ValidarReglaActivacionVigente(regla, timestamp, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | FUNCION | VerificarDuplicadoActivacion(clave) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F06 | FUNCION | CompararPayloadIdempotente(instancia_existente, solicitud) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F06 | FUNCION | GenerarIDInstancia() | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F06 | FUNCION | ResolverReferenciaOrigen(solicitud) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F06 | FUNCION | ResolverVencimientoInstancia(politica_temporal, contexto, calendario) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F06 | FUNCION | ValidarClaveIdempotencia(solicitud) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F06 | FUNCION | ValidarSolicitudGenerable(solicitud) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F07 | FUNCION | evaluar_candidato(id_instancia, id_empleado, momento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F07 | FUNCION | explicar_elegibilidad(id_instancia, id_empleado, momento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F07 | FUNCION | listar_candidatos_elegibles(id_instancia, momento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F07 | FUNCION | listar_personal_base(instancia, momento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F07 | FUNCION | publicar_politica_elegibilidad(...) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F07 | FUNCION | resolver_politica_elegibilidad(id_tarea, version, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F07 | FUNCION | resolver_restricciones_contextuales(instancia, politica) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | FUNCION | balancear_conjunto(ids_instancia, momento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | FUNCION | calcular_componentes_ranking(id_instancia, id_empleado, politica, momento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | FUNCION | obtener_candidatos_para_ranking(id_instancia, momento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | FUNCION | proponer_asignacion(id_instancia, momento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | FUNCION | rankear_candidatos(id_instancia, momento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | FUNCION | resolver_desempate(ranking, politica) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | FUNCION | resolver_politica_asignacion(id_tarea, version, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | FUNCION | revalidar_capacidad_candidato(id_instancia, id_empleado, momento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | FUNCION | calcular_compromiso_planificado(id_instancia, politica, calendario) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | FUNCION | detectar_duplicado_partida(id_plan, id_instancia) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | FUNCION | listar_instancias_candidatas_plan(id_plan) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | FUNCION | listar_pendientes_planificacion(id_plan) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | FUNCION | obtener_plan_periodo(id_periodo, alcance) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | FUNCION | resumir_plan(id_plan) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | FUNCION | validar_asignacion_publicable(id_instancia, id_asignacion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | FUNCION | validar_partida_plan(id_plan, id_instancia, momento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | FUNCION | calcular_duracion_real(id_ejecucion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | FUNCION | listar_pendientes_inicio(id_periodo, alcance, usuario) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | FUNCION | obtener_ejecucion_activa(id_partida_plan) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | FUNCION | obtener_ejecucion_por_partida(id_partida_plan) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | FUNCION | obtener_historial_ejecucion(id_ejecucion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | FUNCION | puede_modificarse_ejecucion(id_ejecucion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | FUNCION | validar_asignacion_para_ejecucion(id_partida_plan, actor, momento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | FUNCION | validar_partida_ejecutable(id_partida_plan, momento, actor) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | FUNCION | validar_transicion_ejecucion(estado_actual, comando) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | FUNCION | evaluar_cumplimiento_estructural_evidencia(id_ejecucion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | FUNCION | explicar_faltantes_evidencia(id_ejecucion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | FUNCION | listar_evidencias_ejecucion(id_ejecucion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | FUNCION | obtener_evidencia_vigente(id_evidencia) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | FUNCION | obtener_requisitos_evidencia(id_ejecucion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | FUNCION | obtener_tipos_evidencia_permitidos(id_ejecucion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | FUNCION | requiere_evidencia(id_ejecucion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | FUNCION | validar_referencia_evidencia(id_tipo_evidencia, clase_referencia, valor) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | FUNCION | validar_tipo_evidencia(id_ejecucion, id_tipo_evidencia) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | FUNCION | evaluar_gate_evidencia_para_validacion(id_ejecucion, resultado_propuesto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | FUNCION | explicar_bloqueo_validacion(id_ejecucion, id_usuario, resultado_propuesto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | FUNCION | obtener_criterios_validacion(id_politica_validacion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | FUNCION | obtener_decision_validacion_vigente(id_ejecucion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | FUNCION | obtener_politica_validacion(id_ejecucion, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | FUNCION | puede_validar(id_ejecucion, id_usuario, resultado_propuesto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | FUNCION | resolver_autoridad_validacion(id_ejecucion, id_usuario, fecha) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | FUNCION | calcular_numero_arrastres(id_instancia) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | FUNCION | explicar_bloqueo_excepcion(id_instancia, tipo, actor) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | FUNCION | listar_causas_excepcion(tipo, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | FUNCION | obtener_asignacion_vigente(id_instancia) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | FUNCION | obtener_estado_operativo_compuesto(id_instancia) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | FUNCION | obtener_politica_excepcion(id_instancia, tipo, momento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | FUNCION | requiere_escalamiento_excepcion(id_instancia, causa, recurrencia, criticidad) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | FUNCION | resolver_autoridad_excepcion(id_instancia, tipo, actor) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | FUNCION | validar_excepcion_permitida(id_instancia, tipo, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | FUNCION | validar_nuevo_compromiso(id_instancia, fecha_ventana) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | FUNCION | contar_ejecuciones_activas(id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | FUNCION | contar_excepciones_pendientes(id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | FUNCION | contar_obligaciones_sin_tratamiento(id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | FUNCION | contar_validaciones_pendientes(id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | FUNCION | es_periodo_mutable(id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | FUNCION | evaluar_cierre_periodo(id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | FUNCION | listar_bloqueos_cierre(id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | FUNCION | obtener_periodo_siguiente(id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | FUNCION | validar_autoridad_cierre(id_periodo, actor) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | FUNCION | validar_integridad_periodo(id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | FUNCION | calcular_base_elegible(...) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | FUNCION | calcular_factor_cumplimiento(...) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | FUNCION | calcular_factor_prorrateo(...) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | FUNCION | calcular_incentivo(...) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | FUNCION | calcular_kpi(id_kpi, contexto, periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | FUNCION | evaluar_elegibilidad_incentivo(id_empleado, id_politica, periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | FUNCION | explicar_resultado_incentivo(id_resultado) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | FUNCION | generar_clave_idempotencia_nomina(...) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | FUNCION | reconciliar_movimiento_nomina(...) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | FUNCION | resolver_politicas_incentivo(id_empleado, periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | FUNCION | validar_autoridad_aprobacion(...) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | FUNCION | validar_calidad_medicion_kpi(id_medicion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F16 | FUNCION | explicar_indicador(codigo_indicador, id_periodo, filtros) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F16 | FUNCION | obtener_dashboard_direccion(id_usuario, id_periodo, alcance) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F16 | FUNCION | obtener_estado_cierre(id_usuario, id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F16 | FUNCION | obtener_excepciones_intersemanales(id_usuario, id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F16 | FUNCION | obtener_inicio_usuario(id_usuario, id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F16 | FUNCION | obtener_kpi_incentivos(id_usuario, id_periodo, alcance) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F16 | FUNCION | obtener_matriz_area(id_usuario, id_periodo, alcance) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F16 | FUNCION | obtener_pendientes_validacion(id_usuario, id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F16 | FUNCION | obtener_resumen_carga_colaborador(id_empleado, id_periodo) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F16 | FUNCION | obtener_tareas_usuario(id_usuario, id_periodo, ventana) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F16 | FUNCION | resolver_acciones_disponibles(id_usuario, objeto, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F17 | FUNCION | generar_id_corrida() | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F17 | FUNCION | generar_id_operacion() | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F17 | FUNCION | verificar_punto_recuperacion(id_punto_recuperacion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F18 | FUNCION | resolver_equivalencia(origen, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F18 | FUNCION | transformar_registro_legacy(registro, mapa) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F18 | FUNCION | validar_no_doble_generacion(alcance, clave_negocio) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F18 | FUNCION | validar_registro_migrado(origen, candidato) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F19 | FUNCION | normalizar_evento_externo(mensaje) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F19 | FUNCION | validar_mensaje_integracion(mensaje) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |

## 12. Catálogo de procedimientos y comandos

| Fase | Tipo | Nombre | Contrato |
| --- | --- | --- | --- |
| F01 | COMANDO | actualizar_vigencia_empleado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | COMANDO | asignar_puesto_empleado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | COMANDO | asignar_turno_empleado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | COMANDO | publicar_politica_capacidad_puesto | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | COMANDO | registrar_disponibilidad_dia | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | COMANDO | registrar_disponibilidad_periodo | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F01 | COMANDO | registrar_empleado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | COMANDO | activar_usuario | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | COMANDO | asignar_rol_usuario | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | COMANDO | crear_sucursal | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | COMANDO | desactivar_sucursal | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | COMANDO | desactivar_usuario | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | COMANDO | registrar_area_organizacional | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | COMANDO | registrar_autorizacion | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | COMANDO | registrar_usuario | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | COMANDO | revocar_rol_usuario | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F03 | COMANDO | cambiar_configuracion_operativa | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F03 | COMANDO | cambiar_estado_periodo | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F03 | COMANDO | crear_periodo_operativo | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F03 | COMANDO | registrar_excepcion_calendario | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F04 | COMANDO | crear_definicion_tarea | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F04 | COMANDO | desactivar_definicion_tarea | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F04 | COMANDO | publicar_version_definicion | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F04 | COMANDO | vincular_dependencia_tarea | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F04 | COMANDO | vincular_tarea_flujo | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | SERVICIO_COMANDO | EmitirSolicitudGeneracion(...) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F06 | SERVICIO_COMANDO | CrearInstanciaDesdeSolicitud(solicitud) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F06 | SERVICIO_COMANDO | ObtenerOCrearPorIdempotencia(solicitud) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F06 | SERVICIO_COMANDO | RegistrarReferenciaLegacyInstancia(...) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | COMANDO | confirmar_asignacion(id_instancia, id_empleado, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | COMANDO | confirmar_asignacion_manual(...) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | COMANDO | agregar_partida_revision(id_plan, id_instancia, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | COMANDO | cerrar_plan_desde_cierre_operativo(id_plan, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | COMANDO | crear_plan_operativo(id_periodo, alcance, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | COMANDO | publicar_partida_incremental(id_plan, id_instancia, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | COMANDO | publicar_plan_inicial(id_plan, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | COMANDO | retirar_partida_no_publicada(id_partida, motivo, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | COMANDO | concluir_ejecucion(id_ejecucion, observaciones, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | COMANDO | iniciar_ejecucion(id_partida_plan, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | COMANDO | registrar_evidencia | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | COMANDO | sustituir_evidencia | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | COMANDO | sustituir_decision_validacion | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | COMANDO | validar_ejecucion | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | COMANDO | validar_lote | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | COMANDO | aplicar_excepciones_lote | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | COMANDO | arrastrar_obligacion | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | COMANDO | cancelar_obligacion | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | COMANDO | posponer_obligacion | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | COMANDO | reasignar_obligacion | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | COMANDO | resolver_excepcion | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | COMANDO | cerrar_periodo | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | COMANDO | corregir_postcierre | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | COMANDO | preparar_periodo_siguiente | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | COMANDO | anular_o_corregir_resultado_incentivo | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | COMANDO | aprobar_resultado_incentivo | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | COMANDO | liquidar_incentivos_periodo | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | COMANDO | preparar_movimientos_nomina | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F17 | SERVICIO_COMANDO | completar_corrida(id_corrida, resultado, conteos) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F17 | SERVICIO_COMANDO | confirmar_idempotencia(clave, resultado) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F17 | SERVICIO_COMANDO | crear_punto_recuperacion() | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F17 | SERVICIO_COMANDO | fallar_corrida(id_corrida, error) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F17 | SERVICIO_COMANDO | iniciar_corrida(tipo_corrida, contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F17 | SERVICIO_COMANDO | registrar_error(error) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F17 | SERVICIO_COMANDO | registrar_evento_auditoria(evento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F17 | SERVICIO_COMANDO | reservar_idempotencia(clave, tipo_operacion, request_hash) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F17 | SERVICIO_COMANDO | restaurar_punto_recuperacion(id_punto_recuperacion, autorizacion) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F18 | SERVICIO_COMANDO | autorizar_cutover(alcance) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F18 | SERVICIO_COMANDO | migrar_lote(contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F18 | SERVICIO_COMANDO | reconciliar_lote(id_corrida) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F18 | SERVICIO_COMANDO | retirar_productor_legacy(alcance) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F19 | SERVICIO_COMANDO | adaptador_sistema_externo | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F19 | SERVICIO_COMANDO | enviar_comando_externo(comando) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F19 | SERVICIO_COMANDO | exportar_lote(id_integracion, alcance) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F19 | SERVICIO_COMANDO | importar_lote(id_integracion, origen) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F19 | SERVICIO_COMANDO | reconciliar_integracion(alcance) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F19 | SERVICIO_COMANDO | reintentar_mensaje(id_mensaje) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |

## 13. Eventos y automatizaciones

| Fase | Tipo | Nombre | Contrato |
| --- | --- | --- | --- |
| F02 | EVENTO | AutorizacionRegistrada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | EVENTO | RolAsignadoAUsuario | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | EVENTO | RolRevocadoDeUsuario | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | EVENTO | UsuarioActivado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | EVENTO | UsuarioDesactivado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F02 | EVENTO | UsuarioRegistrado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | HANDLER | HandlerCambioContextoCondicion(contexto) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | HANDLER | HandlerEventoNegocio(evento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | HANDLER | HandlerProcesoOrigen(caso) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F05 | JOB | EvaluarActivacionesProgramadas | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F06 | EVENTO | InstanciaCreada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | EVENTO | AsignacionConfirmada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | EVENTO | id_asignacion | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | EVENTO | id_empleado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F08 | EVENTO | id_instancia | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | EVENTO | PartidaPlanAgregada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | EVENTO | PartidaPlanPublicada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | EVENTO | PartidaPlanPublicadaIncrementalmente | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | EVENTO | PlanCerrado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | EVENTO | PlanCreado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F09 | EVENTO | PlanPublicado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | EVENTO | EjecucionConcluida | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | EVENTO | EjecucionIniciada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F10 | EVENTO | EstadoEjecucionCambiado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | EVENTO | EvidenciaRegistrada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | EVENTO | EvidenciaSustituida | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F11 | EVENTO | RequisitosEvidenciaSatisfechos | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | EVENTO | AutorizacionRegistrada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | EVENTO | DecisionValidacionSustituida | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | EVENTO | EjecucionValidada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F12 | EVENTO | ValidacionBloqueada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | EVENTO | CompromisoReprogramado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | EVENTO | ExcepcionAplicada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | EVENTO | ExcepcionAutorizada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | EVENTO | ExcepcionRechazada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | EVENTO | ExcepcionSolicitada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | EVENTO | IncidenciaRegistrada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | EVENTO | ObligacionArrastrada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | EVENTO | ObligacionCancelada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F13 | EVENTO | ObligacionReasignada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | EVENTO | CorreccionPostCierreRegistrada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | EVENTO | PeriodoCerrado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F14 | EVENTO | PeriodoListoParaCierre | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | EVENTO | AjusteEconomicoRegistrado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | EVENTO | IncentivoAprobado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | EVENTO | IncentivoCalculado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | EVENTO | IncentivoRequiereRevision | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | EVENTO | MedicionKPICalculada | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | EVENTO | MovimientoNominaConfirmado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F15 | EVENTO | MovimientoNominaPreparado | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |
| F19 | HANDLER | procesar_evento_externo(evento) | Definido en la fase fuente; entradas/salidas deben mantenerse tipadas e idempotentes cuando modifiquen estado. |

Las automatizaciones internas se registran como `Automatizacion` sólo cuando existe componente ejecutable real. Los proyectos o potenciales de automatización no se convierten automáticamente en integraciones.

## 14. Máquinas de estado

| Dominio | Entidad | Estados | Regla |
| --- | --- | --- | --- |
| F03 | PeriodoOperativo | DISPONIBLE_PARA_SIMULACION -> EN_REVISION -> PUBLICADA -> EN_EJECUCION -> CERRADA | CERRADA es terminal para el mismo periodo; rollback técnico no es transición ordinaria. |
| F04 | DefinicionTarea/VersionDefinicionTarea | ACTIVA / NO_ACTIVA_PARA_NUEVAS_OBLIGACIONES; versiones anteriores permanecen históricas | Estados de piloto/preproducción/migración no son estados funcionales permanentes. |
| F05 | ReglaActivacion | BORRADOR -> ACTIVA <-> SUSPENDIDA -> RETIRADA | RETIRADA es terminal para esa versión. |
| F05 | EvaluacionActivacion (derivada) | INICIADA -> NO_CUMPLE \| BLOQUEADA_CONFIGURACION \| BLOQUEADA_IDEMPOTENCIA \| BLOQUEADA_COEXISTENCIA \| LISTA_GENERACION -> EMITIDA | EMITIDA sólo confirma aceptación por F6, no existencia de obligación si falla la transacción. |
| F08 | AsignacionTrabajo | ACTIVA -> REVOCADA | Una sola asignación activa por instancia; reasignación F13 cierra la anterior. |
| F09 | PlanOperativo | EN_CONSTRUCCION -> EN_REVISION -> PUBLICADO -> CERRADO | CERRADO sólo cuando F14 confirma cierre. |
| F09 | PartidaPlan | EN_REVISION -> PUBLICADA | Excepciones y cierre se modelan en F13/F14, no como estados propios de F9. |
| F10 | EjecucionTarea | PENDIENTE_INICIO (derivado) -> EN_PROCESO -> CONCLUIDA | PENDIENTE_INICIO no crea fila de ejecución. |
| F13 | ExcepcionCicloVida | SOLICITADA -> AUTORIZADA\|RECHAZADA -> APLICADA; REVOCADA sólo por ruta extraordinaria | La política puede autorizar/aplicar en una transacción si el actor tiene autoridad directa. |
| F17 | CorridaSistema | INICIADA -> COMPLETADA \| FALLIDA \| CANCELADA | Un reintento crea otra corrida relacionada; no sobrescribe la fallida. |
| F17 | RegistroIdempotencia | EN_PROCESO -> COMPLETADA \| FALLIDA | La política determina si una clave fallida puede reintentarse. |
| F18 | ReferenciaOrigenLegacy.estado_reconciliacion | PENDIENTE \| MAPEO_UNICO \| AMBIGUO \| TRANSFORMADO \| RECONCILIADO \| EXCLUIDO_JUSTIFICADO \| ERROR | Un lote sólo cierra cuando todos los registros están en estado terminal permitido. |
| F19 | MensajeIntegracion entrada/salida | Según dirección: recibido/validado/aplicado/rechazado o pendiente_envio/enviado/confirmado/fallido | Reintentos sólo para fallos transitorios; idempotencia obligatoria. |

## 15. Modelo de permisos y autoridad

La seguridad aplica denegación por defecto. Los permisos efectivos provienen de asignaciones de rol vigentes y alcance. Cuando una política exige autorización adicional, permiso y autorización deben cumplirse.

| Codigo_Logico | Dominio | Descripcion | Autoridad |
| --- | --- | --- | --- |
| CONSULTAR_OPERACION | F02/F16 | Consultar datos dentro del alcance efectivo del usuario. | Roles vigentes + alcance; denegación por defecto. |
| ADMIN_PERSONAL | F01 | Registrar/vigenciar empleados, puestos, turnos y disponibilidad. | Rol administrativo autorizado. |
| ADMIN_SEGURIDAD | F02 | Gestionar usuarios, roles, permisos, alcances y equivalencias. | Segregación de funciones y auditoría obligatoria. |
| ADMIN_CALENDARIO | F03 | Configurar calendario, excepciones y parámetros operativos. | Permiso específico; cambios sensibles auditados. |
| GOBERNAR_CATALOGO_TAREAS | F04 | Crear/versionar/desactivar definiciones, flujos, checklists, reglas, KPI y automatizaciones. | Gobierno del catálogo. |
| ADMIN_ACTIVACION | F05 | Publicar/suspender/retirar reglas de activación. | Permiso de configuración y alcance. |
| ASIGNAR_TRABAJO | F08 | Confirmar asignación automática o manual. | Servicio autorizado o permiso manual específico. |
| PUBLICAR_PLAN | F09 | Publicar plan inicial o partida incremental. | Permiso por alcance y estado de período. |
| EJECUTAR_TAREA | F10 | Iniciar/concluir ejecución propia o delegada según política. | Actor asignado o autoridad explícita. |
| REGISTRAR_EVIDENCIA | F11 | Adjuntar/sustituir evidencia de una ejecución. | Responsable o actor permitido por política. |
| VALIDAR | F12 | Emitir decisión de validación dentro del alcance y política. | PoliticaValidacion + permisos F2; no derivar del nombre de puesto. |
| AUTORIZAR_EXCEPCIONES | F13 | Autorizar/aplicar cancelación, posposición, arrastre o reasignación. | Tipo + alcance + política; separación de funciones cuando aplique. |
| CERRAR_PERIODO | F14 | Cerrar período cuando todos los gates estén satisfechos. | Autoridad de cierre explícita; no heredada de control semanal. |
| APROBAR_INCENTIVOS | F15 | Aprobar resultados económicos y preparar nómina. | Gobierno económico versionado y segregado. |
| CONSULTAR_AUDITORIA | F17 | Consultar auditoría/errores/corridas según sensibilidad. | Alcance ejecutivo o técnico autorizado. |
| AUTORIZAR_CUTOVER | F18 | Transferir autoridad de productor legacy a canónico. | Autorización explícita después de gates. |
| ADMIN_INTEGRACIONES | F19 | Configurar contratos, habilitar integraciones y gestionar reintentos. | Permiso técnico/negocio; secretos fuera de tablas de negocio. |

## 16. Planificación y ejecución

El flujo canónico es: **definición vigente → regla de activación → solicitud idempotente → InstanciaTrabajo → evaluación de elegibilidad → asignación → plan/partida → inicio de ejecución → conclusión → validación o excepción → cierre**. Las etapas posteriores referencian la misma obligación y no regeneran su identidad.

## 17. Evidencias y validaciones

Los requisitos de evidencia se versionan por tarea/contexto y admiten múltiples tipos con cardinalidad explícita. F11 determina integridad y presencia; F12 decide cumplimiento. Una decisión `CUMPLIDA` debe satisfacer el gate de evidencia cuando ésta sea obligatoria. Las decisiones se conservan sin sobrescribir el histórico.

## 18. Excepciones

Cancelación, posposición/reprogramación, arrastre/continuación, reasignación e incidencias se modelan como hechos F13. Las excepciones conservan causa, autoridad, decisión, efecto y correlación. No se codifican como estados de validación ni calculan directamente incentivos.

## 19. Cierre operativo

`cerrar_periodo` es una transacción atómica e idempotente que sólo confirma cuando todas las obligaciones del período tienen tratamiento terminal válido, no hay validaciones o excepciones pendientes y los gates configurados se satisfacen. Después del commit el período queda `CERRADA`; correcciones posteriores son operaciones extraordinarias auditables y no reaperturas silenciosas.

## 20. KPI y bonos

`DefinicionKPI` es el único catálogo canónico de KPI. `MedicionKPI` persiste sólo cuando el valor debe ser trazable. Las políticas de incentivo son versionadas y separan elegibilidad, fórmula, topes, excepciones y aprobación. Los movimientos hacia nómina son idempotentes y auditables; nómina conserva la responsabilidad de salario, impuestos y retenciones.

## 21. Auditoría y trazabilidad

Toda operación modificadora recibe `id_operacion`; los flujos pueden compartir `id_correlacion`. `EventoAuditoria` es append-only. `CorridaSistema` existe sólo para ejecuciones técnicas con ciclo de vida propio. `RegistroIdempotencia` evita efectos duplicados. `ErrorSistema` conserva errores investigables. Los puntos de recuperación gobiernan respaldos externos; no sustituyen transacciones.

## 22. Integraciones

Cada integración define sistema externo, contrato versionado, dirección, esquema, política de idempotencia/reintento y referencias externas. Los lotes, mensajes y checkpoints se persisten sólo cuando la recuperación/reconciliación lo exige. Los secretos permanecen en infraestructura segura.

## 23. Reportes y vistas de usuario

| Fase | Vista_o_DTO | SQL_Propuesto | Persistencia | Descripcion |
| --- | --- | --- | --- | --- |
| F01 | Capacidad operativa | vw_capacidad_operativa | NO por defecto | Debe exponerse como vista o función calculada a partir de empleado, puesto vigente, disponibilidad y trabajo asignado. |
| F01 | Estado de capacidad | vw_estado_de_capacidad | NO por defecto | La clasificación actual requerida por el dominio es: - DISPONIBLE: utilización menor a 70%. - ADECUADO: utilización desde 70% y menor a 90%. - SATURADO: utilización igual o mayor a 90%. |
| F08 | ColaPendienteAsignacion | vw_cola_pendiente_asignacion | NO por defecto | Vista de instancias sin asignación activa y su causa actual. No es una tabla duplicada de obligaciones. |
| F08 | PropuestaAsignacion | vw_propuesta_asignacion | NO por defecto | Resultado previo a confirmar: - candidato ganador; - motivo explicable; - versión de la evaluación F7; - versión de capacidad; - versión de política; - estado de propuesta. |
| F08 | RankingCandidatos | vw_ranking_candidatos | NO por defecto | Resultado de ordenar candidatos elegibles en un instante. |
| F09 | ColaPendientePlanificacion | vw_cola_pendiente_planificacion | NO por defecto | Vista de `InstanciaTrabajo` que todavía no puede publicarse y su causa actual, por ejemplo: - `SIN_ASIGNACION_ACTIVA`; - `FUERA_DE_PERIODO`; - `CONFIGURACION_INCOMPLETA`; - `VALIDACION_PLAN_FALLIDA`; - `EXCEPCION_REQUIERE_AUTORIZACION`. |
| F09 | ResumenPlan | vw_resumen_plan | NO por defecto | Agregación por estado, responsable, fecha, criticidad, proceso o sucursal. Es una consulta, no una tabla duplicada. |
| F09 | VistaPlanOperativo | vw_vista_plan_operativo | NO por defecto | Proyección para UX que combina plan, partida, instancia, definición versionada, asignación activa/publicada y atributos organizativos visibles. Esta vista puede mostrar nombres y descripciones sin convertirlos en llaves persistentes. |
| F10 | DuracionReal | vw_duracion_real | NO por defecto | `concluida_en - iniciada_en` cuando ambos timestamps existen. Es cálculo derivado. |
| F10 | PendientesInicio | vw_pendientes_inicio | NO por defecto | Vista de partidas publicadas y ejecutables para las cuales aún no existe una ejecución iniciada. |
| F10 | VistaEjecucionOperativa | vw_vista_ejecucion_operativa | NO por defecto | Proyección que combina `PartidaPlan`, `InstanciaTrabajo`, asignación vigente, `EjecucionTarea`, definición de tarea y datos visibles de evidencia/validación sin duplicarlos en la entidad de ejecución. |
| F11 | CumplimientoEstructuralEvidencia | vw_cumplimiento_estructural_evidencia | NO por defecto | Resultado calculado que indica si la ejecución posee la cantidad y los tipos de evidencia requeridos, con referencias compatibles con cada modalidad. |
| F11 | PendientesEvidencia | vw_pendientes_evidencia | NO por defecto | Vista de ejecuciones concluidas o próximas a concluir cuyos requisitos obligatorios todavía no están satisfechos. |
| F11 | RequiereEvidencia | vw_requiere_evidencia | NO por defecto | Valor derivado de los requisitos vigentes aplicables a la ejecución. |
| F11 | VistaEvidenciasEjecucion | vw_vista_evidencias_ejecucion | NO por defecto | Proyección de ejecución, requisitos, evidencias, tipos, referencias y datos de captura para UX, validación y auditoría. |
| F12 | AutoridadValidacionEfectiva | vw_autoridad_validacion_efectiva | NO por defecto | Resultado derivado de: - usuario autenticado; - roles y permisos vigentes de F2; - alcance organizacional; - política de validación; - identidad del ejecutor; - regla de autovalidación; - estado del período. |
| F12 | EstadoValidacionEjecucion | vw_estado_validacion_ejecucion | NO por defecto | Derivado de la existencia y vigencia de decisiones: - `PENDIENTE`; - `CUMPLIDA`; - `INCOMPLETA`; - `NO_CUMPLIDA`. |
| F12 | PendientesValidacion | vw_pendientes_validacion | NO por defecto | Vista de ejecuciones concluidas que requieren validación y no tienen una decisión vigente. |
| F14 | EstadoPostCierre | vw_estado_post_cierre | NO por defecto | Se deriva de `PeriodoOperativo.estado = CERRADA`. No necesita una bandera repetida por partida o ejecución. |
| F14 | EvaluacionCierrePeriodo | vw_evaluacion_cierre_periodo | NO por defecto | No requiere tabla persistente por defecto. Se calcula a partir del período y sus obligaciones. |
| F16 | ViewAsignacionesPeriodo | vw_view_asignaciones_periodo | NO por defecto | Expone asignaciones confirmadas y, cuando esté autorizado, diagnósticos de no asignación. Las propuestas temporales de F8 no se convierten en hechos persistentes por existir en una pantalla. |
| F16 | ViewDireccionEjecutiva | vw_view_direccion_ejecutiva | NO por defecto | Agrega indicadores operativos, alertas, autorizaciones y referencias de auditoría según permisos efectivos. |
| F16 | ViewEstadoCierrePeriodo | vw_view_estado_cierre_periodo | NO por defecto | Proyección F14 con conteos, pendientes y diagnóstico de cierre. |
| F16 | ViewExcepcionesIntersemanales | vw_view_excepciones_intersemanales | NO por defecto | Presenta hechos F13 y continuidad entre períodos. |
| F16 | ViewInicioUsuario | vw_view_inicio_usuario | NO por defecto | Combina período, permisos efectivos, alertas relevantes y accesos disponibles para el usuario. |
| F16 | ViewKPIIncentivos | vw_view_kpiincentivos | NO por defecto | Presenta `MedicionKPI` y `ResultadoIncentivo` F15 según nivel de sensibilidad y autoridad. |
| F16 | ViewMatrizArea | vw_view_matriz_area | NO por defecto | Proyección de obligaciones por área/rol/alcance y período. La agrupación por tarea y día es exclusivamente de presentación. |
| F16 | ViewPendientesValidacion | vw_view_pendientes_validacion | NO por defecto | Devuelve únicamente obligaciones F12 pendientes dentro del alcance efectivo del validador. |
| F16 | ViewPlanPeriodo | vw_view_plan_periodo | NO por defecto | Presenta el `PlanOperativo` y sus partidas F9 sin duplicar los datos de obligación. |
| F16 | ViewResumenCargaColaborador | vw_view_resumen_carga_colaborador | NO por defecto | Agrega minutos/obligaciones asignadas, iniciadas, concluidas y con decisión final. Los porcentajes se calculan a partir de definiciones explícitas. |
| F16 | ViewTareasUsuario | vw_view_tareas_usuario | NO por defecto | Obligaciones dentro del alcance del usuario con estado operativo, vencimiento, evidencia requerida y acciones permitidas. |
| F17 | ViewAuditoriaEjecutiva | vw_view_auditoria_ejecutiva | NO por defecto | Proyección de eventos relevantes para Dirección según permisos y alcance. |
| F17 | ViewCorridasSistema | vw_view_corridas_sistema | NO por defecto | Consulta de corridas por tipo, estado, período, iniciador, duración y resultado. |
| F17 | ViewErroresOperativos | vw_view_errores_operativos | NO por defecto | Consulta restringida de errores técnicos con correlación a corrida/operación. |
| F17 | ViewTrazabilidadEntidad | vw_view_trazabilidad_entidad | NO por defecto | Reconstruye los eventos auditables asociados a una entidad de negocio sin convertir la auditoría en su fuente de estado actual. |

Las vistas se filtran por permisos y alcance efectivo. La materialización sólo se permite por rendimiento y debe tener política explícita de refresco e invalidación.

## 24. Reglas de consistencia e idempotencia

- Unicidad de `clave_idempotencia` en obligaciones y comandos reintentables.
- Una sola asignación activa por instancia.
- Una partida activa por `(plan, instancia)`.
- Una ejecución compatible por partida según la política de ciclo de vida.
- Una decisión de validación vigente sin borrar decisiones previas.
- Un cierre definitivo por período.
- Ninguna regla versionada puede solaparse de forma incompatible para el mismo propietario/alcance.
- Todas las FKs críticas se validan antes del commit.
- Los eventos externos se aplican una sola vez por clave de idempotencia.
- Las integraciones no avanzan checkpoint hasta confirmar aplicación/reconciliación.
- El cutover impide autoridad simultánea legacy/canónica.

## 25. Estrategia de migración

La migración se ejecuta por lotes idempotentes y reconciliables. Cada registro de origen conserva `ReferenciaOrigenLegacy`; las equivalencias ambiguas no se resuelven por heurística silenciosa. El cutover se autoriza por alcance únicamente después de probar el ciclo de vida aplicable, reconciliar conteos/hechos y garantizar ausencia de doble generación. Las estructuras `MapaEquivalenciaMigracion` y `ControlCoexistencia` son temporales.

## 26. Modelo tecnológico objetivo

- **Base relacional transaccional** para catálogos, configuración, hechos e histórico.
- **Capa de servicios/comandos** que aplica reglas, seguridad, transacciones e idempotencia.
- **Event/handler layer** para desacoplar efectos posteriores al commit e integraciones.
- **Scheduler** únicamente para jobs programados explícitos.
- **Capa de lectura** con vistas/DTO por rol y alcance.
- **Almacenamiento de evidencias** externo o especializado cuando corresponda, manteniendo referencias e integridad en SQL.
- **Observabilidad y recuperación** separadas de la lógica de negocio.
- **Adaptadores externos** aislados por proveedor/contrato.

El lenguaje, framework y motor SQL pueden cambiar sin alterar este modelo funcional.

## Gates de implementación aún abiertos

F20 consolida el modelo objetivo, pero no autoriza completar por inferencia decisiones de negocio faltantes. Los pendientes de las fases anteriores se conservan en la matriz privada y deben cerrarse antes de habilitar el alcance que afectan. Entre los bloqueos relevantes están políticas incompletas de tareas, autoridad/alcance, evidencias, desempates, contratos de integración, catálogos organizativos, autenticación y reglas económicas aún no aprobadas.
