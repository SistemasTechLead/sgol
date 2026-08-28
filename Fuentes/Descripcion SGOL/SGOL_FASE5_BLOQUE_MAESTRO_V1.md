# SGOL — Fase 5 · Activación de tareas

## Propósito del dominio

Este dominio determina **cuándo una definición de tarea debe producir una solicitud de obligación operativa**. La activación evalúa calendario, eventos, condiciones o casos de proceso y entrega una decisión idempotente a la generación de instancias.

La activación **no asigna responsable**, **no crea PLAN/EJEC**, **no valida evidencia** y **no ejecuta la tarea**. Esas responsabilidades pertenecen a dominios posteriores.

## Principios obligatorios

- Una `DefinicionTarea` no genera trabajo por existir; requiere una `ReglaActivacion` vigente y ejecutable.
- Los mecanismos de activación admitidos son `PROGRAMADA`, `EVENTO`, `CONDICIONAL` y `PROCESO`.
- **SLA no es un tipo de activación.** Es una regla temporal que calcula vencimientos o momentos objetivo y puede coexistir con cualquiera de los mecanismos anteriores.
- Una ventana intradía es un parámetro estructurado de una activación PROGRAMADA, no una frase de frecuencia.
- Ninguna regla de negocio se ejecuta interpretando texto libre. Frecuencias, eventos, condiciones, offsets, unidades, calendarios y predicados deben estar estructurados y versionados.
- Un evento debe tener tipo, fuente, identidad/correlación y timestamp trazables.
- Una condición debe evaluarse sólo sobre un contexto identificado y con entradas suficientes para explicar por qué cumplió o no cumplió.
- Una activación derivada de un proceso debe conservar el identificador y timestamp del caso origen.
- La relación padre-hijo no activa automáticamente una tarea; sólo provee contexto. La condición explícita sigue siendo obligatoria.
- Cada evaluación capaz de generar trabajo debe construir una clave de idempotencia determinista antes de solicitar una instancia.
- Las guardas de coexistencia con sistemas o definiciones anteriores son controles temporales de migración, no parte permanente de la semántica de activación.
- La activación produce una `SolicitudActivacion`; la persistencia de la obligación ocurre en la Fase 6.

## Mapa del dominio

1. **Regla de activación**: configuración vigente que relaciona una tarea con un mecanismo de disparo.
2. **Programación**: calendario, fechas, ventanas y anticipaciones.
3. **Eventos**: hechos de negocio identificados que pueden disparar reglas.
4. **Condiciones**: predicados estructurados sobre un caso o contexto.
5. **Procesos origen**: casos cuya creación o cambio puede activar una obligación vinculada.
6. **Reglas temporales**: SLA, vencimientos, offsets y días hábiles.
7. **Idempotencia**: prevención determinista de solicitudes duplicadas.
8. **Salida a generación**: contrato explícito hacia la Fase 6.

## Entidades y componentes definitivos

### ReglaActivacion

Configuración versionable que define el mecanismo por el cual una tarea puede activarse.

**Atributos relevantes**
- identificador estable de la regla;
- `id_tarea` y versión de definición aplicable;
- tipo de activación: `PROGRAMADA`, `EVENTO`, `CONDICIONAL` o `PROCESO`;
- estado y vigencia;
- alcance operativo cuando corresponda;
- referencia a configuración específica del mecanismo;
- política temporal opcional;
- versión y motivo de cambio.

**Reglas**
- No puede considerarse ejecutable si faltan parámetros obligatorios.
- Una versión retirada no se reutiliza ni se elimina si originó obligaciones históricas.
- Dos reglas vigentes para la misma tarea y alcance sólo pueden coexistir si su semántica es explícitamente compatible.

### ProgramacionActivacion

Configuración estructurada para activaciones PROGRAMADA.

**Puede contener**
- regla de calendario;
- días de semana o mes;
- ventanas intradía;
- fecha de inicio/fin;
- exclusión o tratamiento de días inhábiles;
- anticipación respecto a un vencimiento real;
- granularidad o clave de contexto, por ejemplo servicio.

### ReglaEvento

Configuración para activaciones EVENTO.

**Atributos relevantes**
- tipo de evento aceptado;
- fuente del evento;
- predicados requeridos;
- clave de correlación del caso;
- campos obligatorios de contexto;
- reglas de autorización del evento cuando correspondan.

### ReglaCondicion

Configuración para activaciones CONDICIONAL.

**Atributos relevantes**
- condición estructurada;
- fuente/contexto evaluado;
- campos obligatorios;
- referencia padre cuando exista causalidad;
- granularidad de generación.

### ReglaProcesoOrigen

Configuración para activaciones PROCESO.

**Atributos relevantes**
- tipo de caso o proceso origen;
- identificador del caso;
- timestamp de origen;
- campos heredables;
- condición de entrada;
- regla temporal asociada.

### ReglaTemporal

Componente reusable para cálculo temporal.

**Atributos relevantes**
- tipo: SLA, offset, anticipación o ventana;
- valor;
- unidad;
- punto temporal de origen;
- calendario aplicable;
- tratamiento de días inhábiles;
- regla de redondeo o límite cuando corresponda.

**Regla principal**
Un valor temporal siempre debe almacenarse como cantidad tipada más unidad; no como fecha accidental ni texto libre.

### ParametroRegla

Valor administrable de negocio asociado a una regla, con tipo, unidad, vigencia y alcance. Los KPI no se almacenan como parámetros de activación.

### TipoEventoNegocio

Catálogo compartido de tipos de evento que pueden ser producidos por otros dominios o integraciones y consumidos por reglas EVENTO.

### EvaluacionActivacion

Resultado explicable de evaluar una regla contra un contexto.

**Salidas posibles**
- `NO_CUMPLE`;
- `BLOQUEADA_CONFIGURACION`;
- `BLOQUEADA_IDEMPOTENCIA`;
- `BLOQUEADA_COEXISTENCIA` mientras exista migración;
- `LISTA_GENERACION`;
- `EMITIDA` cuando la solicitud a Fase 6 haya sido aceptada.

Su persistencia completa es opcional y depende de los requisitos de auditoría de la Fase 17.

### SolicitudActivacion

Contrato de salida hacia la Fase 6.

**Contenido mínimo**
- tarea y versión de definición;
- regla y versión de activación;
- tipo de activación;
- clave de idempotencia;
- timestamp de activación;
- contexto/caso origen;
- referencia padre cuando aplique;
- fecha/hora de vencimiento calculada cuando exista;
- alcance operativo;
- metadatos mínimos de trazabilidad.

No contiene asignación final de responsable ni crea directamente una instancia persistente.

## Reglas de idempotencia

La clave de idempotencia debe representar una obligación lógica, no una ejecución técnica. Como mínimo incorpora la tarea, versión/regla y el contexto que hace única la activación.

- PROGRAMADA: tarea + regla + fecha/ventana lógica + alcance; si existe hijo parametrizado, incluir la clave del hijo.
- EVENTO: tarea + regla + identificador único del evento/caso + alcance.
- CONDICIONAL: tarea + regla + identificador del contexto/padre + granularidad definida.
- PROCESO: tarea + regla + identificador del caso origen.

Los reintentos con la misma clave deben devolver el mismo resultado lógico y no originar una segunda solicitud de obligación.

## Reglas de negocio confirmadas para la cohorte inicial

| Tarea | Tipo | Activación | Regla temporal / aclaración |
|---|---|---|---|
| TAR-0005 | PROGRAMADA | Ventanas 12:00 y 17:00; umbral 90% del avance esperado | Sin alerta: `REVISION_CONFORME_SIN_ACCION` |
| TAR-0007 | PROCESO | Nace desde `EXPEDIENTE_SEPARADO` y conserva caso/timestamp origen | SLA 24 h; si requiere más tiempo se convierte a Apartado formal |
| TAR-0008 | CONDICIONAL | Dos o más colaboradoras reclaman una misma operación | SLA 30 min desde detección formal |
| TAR-0011 | EVENTO | Garantía excepcional autorizada después de 90 días | Vencimiento 7 días hábiles post-autorización; 72 h es evaluación previa |
| TAR-0018 | EVENTO | Nueva lista, campaña, llegada de producto o rotación autorizada | Rotación extraordinaria requiere autorización de Coordinador de Sucursal |
| TAR-0026 | PROGRAMADA | Por servicio, 3 días hábiles antes del vencimiento real | Si el vencimiento es inhábil, objetivo = hábil anterior; periodicidad según recibo/proveedor |
| TAR-0092 | EVENTO | Cada recepción con `ID_Recepcion` único y timestamp de inicio | 45 min es KPI, no SLA contractual |
| TAR-0093 | CONDICIONAL | Irregularidad derivada de TAR-0092 | Conserva padre y datos base; hijo adicional sólo si existe caso independiente |

## Funciones, comandos, handlers y jobs

- `ValidarReglaActivacionVigente(regla, timestamp, contexto)`.
- `EvaluarProgramacion(regla, calendario, timestamp)`.
- `ResolverVentanasIntradia(regla, fecha)`.
- `CalcularFechaHabilObjetivo(fecha, politica_calendario)`.
- `CalcularMomentoPorAnticipacion(vencimiento, cantidad, unidad, calendario)`.
- `EvaluarEvento(regla, evento)`.
- `EvaluarCondicion(regla, contexto)`.
- `ResolverOrigenProceso(regla, caso)`.
- `CalcularVencimiento(origen_temporal, regla_temporal, calendario)`.
- `ConstruirClaveIdempotenciaActivacion(regla, contexto, momento_logico)`.
- `VerificarDuplicadoActivacion(clave)`.
- `ProcesarContextoActivacion(...)`: servicio de despacho por tipo.
- `EmitirSolicitudGeneracion(...)`: comando idempotente hacia Fase 6.
- `HandlerEventoNegocio(evento)`.
- `HandlerCambioContextoCondicion(contexto)`.
- `HandlerProcesoOrigen(caso)`.
- `EvaluarActivacionesProgramadas`: job de calendario; su cadencia física es decisión de implementación.

## Estados y transiciones

### ReglaActivacion

`BORRADOR → ACTIVA ↔ SUSPENDIDA → RETIRADA`

- Publicar requiere configuración válida y autoridad suficiente.
- Suspender impide nuevas solicitudes sin borrar histórico.
- Retirar es terminal para esa versión.

### EvaluacionActivacion

`INICIADA → NO_CUMPLE | BLOQUEADA_CONFIGURACION | BLOQUEADA_IDEMPOTENCIA | BLOQUEADA_COEXISTENCIA | LISTA_GENERACION → EMITIDA`

La transición `LISTA_GENERACION → EMITIDA` no significa que la obligación ya exista; sólo confirma que Fase 6 recibió la solicitud.

## Validaciones obligatorias

- La tarea y su versión deben estar vigentes.
- La regla debe estar ACTIVA y dentro de vigencia.
- Deben existir todos los parámetros obligatorios con tipo y unidad válidos.
- Fechas y ventanas deben resolverse con el calendario operativo común.
- Un evento debe tener identidad/correlación; un evento sin identificador no puede ser idempotente.
- Las condiciones deben disponer de todas sus entradas requeridas.
- Una regla PROCESO debe resolver un caso origen válido.
- Las relaciones padre-hijo deben conservar el identificador del padre.
- Debe construirse y validar la clave de idempotencia antes de emitir la solicitud.
- Mientras exista coexistencia, las guardas temporales deben validarse antes de Fase 6.

## Permisos

La creación, modificación, publicación, suspensión y retiro de reglas de activación requieren autorización conforme al modelo de roles y permisos. Los productores de eventos no adquieren por ese hecho facultad para modificar reglas ni aprobar excepciones.

## Dependencias

- **Fase 2**: autoridad para administrar/publicar reglas.
- **Fase 3**: calendario, días hábiles, período operativo y ventanas.
- **Fase 4**: definiciones de tarea, reglas y parámetros.
- **Fase 6**: creación idempotente de instancias.
- **Fase 17**: auditoría de evaluaciones, errores y reintentos.
- **Fase 18**: guardas temporales de coexistencia y retiro legacy.
- **Fase 19**: contratos de eventos e integraciones externas.

## Errores de dominio

- `REGLA_ACTIVACION_INEXISTENTE`.
- `REGLA_ACTIVACION_NO_VIGENTE`.
- `CONFIGURACION_ACTIVACION_INCOMPLETA`.
- `PARAMETRO_ACTIVACION_INVALIDO`.
- `EVENTO_SIN_IDENTIDAD`.
- `CONTEXTO_INSUFICIENTE`.
- `CASO_ORIGEN_NO_RESUELTO`.
- `PADRE_NO_RESUELTO`.
- `ACTIVACION_DUPLICADA`.
- `ACTIVACION_BLOQUEADA_COEXISTENCIA`.
- `CALENDARIO_NO_RESUELTO`.

