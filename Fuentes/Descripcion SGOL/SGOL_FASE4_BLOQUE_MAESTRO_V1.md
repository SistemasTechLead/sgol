# SGOL — Fase 4 · Modelo canónico de la operación

## Propósito del dominio

Este dominio define el catálogo semántico que describe **qué trabajo existe en SGOL**, cómo se organiza dentro de la operación y qué conocimiento reusable gobierna su interpretación. Su responsabilidad termina antes de decidir **cuándo** nace una obligación real, **a quién** se asigna o **cómo** se ejecuta.

La definición de una tarea es una posibilidad operativa versionada. Una obligación activa sólo nace cuando las reglas de activación y generación de las fases siguientes determinan que corresponde crearla.

## Principios obligatorios

- La identidad lógica de una tarea es estable y utiliza el código `TAR-####`.
- Cambiar nombre, descripción, criterio, responsable, evidencia o configuración no crea una nueva identidad cuando sigue representando la misma obligación; crea una nueva versión de la definición.
- Una tarea nueva sólo se crea cuando existe una obligación operativa distinta, no para representar una meta, un indicador, un paso de checklist, una regla o una estructura técnica.
- Macroproceso, proceso y subproceso forman una jerarquía funcional; sus claves deben ser únicas y no ambiguas dentro de su nivel y relación padre.
- Un flujo operativo (`FLU-####`) es una agrupación o contexto transversal de tareas. Pertenecer al mismo flujo no implica por sí solo precedencia, activación ni dependencia causal.
- Las dependencias entre tareas se registran explícitamente y separadas de la pertenencia a un flujo.
- Un checklist (`CHK-####`) es una definición estructurada y versionable de controles o pasos; no debe almacenarse como texto libre dentro de la tarea cuando sus ítems deban verificarse individualmente.
- Un KPI (`KPI-####`) es una medición atómica. Una meta o indicador nunca debe representarse como una tarea sólo para poder reportarla.
- Una regla (`REG-####`) se persiste como configuración únicamente cuando el negocio puede administrarla sin modificar código. Las invariantes, validaciones y algoritmos deterministas se implementan como funciones o guards.
- Un parámetro (`PAR-####`) debe tener tipo, unidad, alcance, vigencia y regla propietaria; no se ejecuta como texto.
- Una automatización (`AUT-####`) existe sólo cuando hay un componente ejecutable gobernable y trazable. El “nivel de automatización” de una tarea es una clasificación, no una automatización por sí mismo.
- Activación, calendario, eventos, condiciones, SLA y generación de instancias se formalizan en las fases 5 y 6.
- Evidencia se normaliza en la fase 11; validación y autoridad se formalizan en la fase 12; definición y cálculo de KPI en la fase 15.

## Mapa del dominio

1. **Arquitectura de procesos**: Macroproceso → Proceso → Subproceso.
2. **Catálogo de trabajo**: DefinicionTarea y sus versiones.
3. **Flujos operativos**: agrupaciones transversales de tareas con identidad `FLU`.
4. **Relaciones y dependencias**: vínculos tipados entre tareas, independientes del flujo.
5. **Checklists**: definiciones reutilizables e ítems verificables.
6. **Reglas configurables y parámetros**: sólo conocimiento administrable por negocio.
7. **KPI**: indicadores atómicos relacionados con tareas o procesos.
8. **Automatizaciones**: componentes ejecutables explícitos, separados de su potencial o nivel de automatización.

## Entidades definitivas

### Macroproceso

Nivel superior de clasificación funcional.

**Atributos relevantes**
- `id_macroproceso`.
- `codigo` estable.
- `nombre`.
- `descripcion`.
- `activo`.
- vigencia cuando corresponda.

**Reglas**
- El código debe ser único.
- Un macroproceso puede contener varios procesos.
- No se elimina físicamente si existen definiciones históricas asociadas.

### Proceso

Agrupación funcional perteneciente a un macroproceso.

**Atributos relevantes**
- `id_proceso`.
- `codigo` estable.
- `id_macroproceso`.
- `nombre`.
- `descripcion`.
- `activo`.
- vigencia.

**Reglas**
- Su identidad debe ser inequívoca dentro del modelo.
- Debe pertenecer a un único macroproceso vigente.

### Subproceso

Unidad funcional que clasifica primariamente las definiciones de tarea.

**Atributos relevantes**
- `id_subproceso`.
- `codigo` estable.
- `id_proceso`.
- `nombre`.
- `descripcion`.
- `activo`.
- vigencia.

**Reglas**
- Debe pertenecer a un único proceso vigente.
- Una definición de tarea vigente debe poder resolverse a un subproceso válido.

### FlujoOperativo

Contexto transversal con identidad estable `FLU-####` que agrupa tareas relacionadas por una operación de negocio.

**Atributos relevantes**
- `id_flujo`.
- `codigo_flujo`.
- `nombre`.
- `proposito`.
- propietario funcional.
- `activo`.
- versión o vigencia cuando cambie su significado.

**Reglas**
- La relación con tareas se modela explícitamente.
- Un flujo no activa tareas por el mero hecho de contenerlas.
- La secuencia y causalidad sólo existen cuando se declaran mediante reglas o relaciones específicas.

### DefinicionTarea

Identidad lógica estable de una obligación posible.

**Atributos relevantes**
- `id_tarea` con código `TAR-####`.
- estado funcional de disponibilidad de la definición.
- referencia a su versión vigente.
- fecha de creación.
- fecha de retiro cuando corresponda.

**Reglas**
- El identificador nunca se reutiliza para otra obligación.
- Desactivar una definición no elimina su historia.
- Una definición no crea por sí sola una instancia de trabajo.
- Metas, KPI, reglas, checklists y acciones auxiliares no deben registrarse como tareas independientes salvo que representen una obligación real con resultado verificable.

### VersionDefinicionTarea

Contenido versionado que describe qué significa y cómo debe interpretarse una tarea.

**Atributos relevantes**
- `id_version_tarea`.
- `id_tarea`.
- número de versión.
- `vigente_desde`.
- `vigente_hasta` nullable.
- nombre.
- descripción operativa.
- resultado esperado.
- criterio de validación funcional.
- subproceso primario.
- tipo funcional cuando aplique.
- tipo de activación como clasificación de alto nivel.
- perfil o puesto responsable requerido.
- regla de validación/supervisión requerida.
- duración estimada nullable.
- nivel de riesgo.
- criticidad operativa cuando corresponda.
- requisito general de evidencia.
- nivel de automatización.
- motivo del cambio.
- autoridad que aprobó la versión cuando sea requerido.

**Reglas**
- No puede haber dos versiones vigentes incompatibles para la misma tarea y alcance.
- Las obligaciones ya creadas deben conservar referencia a la versión que les dio origen o una instantánea equivalente.
- La ausencia de duración estimada no invalida por sí sola la identidad de la tarea.
- Riesgo y criticidad son conceptos distintos y no deben fusionarse automáticamente.
- La configuración detallada de activación no se almacena como una regla libre dentro de esta entidad.

### FlujoTarea

Relación versionable entre una definición de tarea y un flujo operativo.

**Atributos relevantes**
- `id_flujo`.
- `id_tarea`.
- vigencia.
- rol de la tarea dentro del flujo cuando exista un catálogo formal.
- orden sólo cuando la operación lo defina explícitamente.

**Reglas**
- No se infiere orden por el código de tarea.
- Una tarea puede participar en más de un flujo si el negocio lo requiere.

### RelacionTarea

Relación dirigida y tipada entre dos definiciones de tarea.

**Atributos relevantes**
- tarea origen.
- tarea destino.
- tipo de relación.
- condición o contexto estructurado cuando corresponda.
- vigencia.

**Reglas**
- La relación debe tener semántica explícita.
- Los ciclos se rechazan cuando el tipo de dependencia no los permita.
- Una relación causal que pueda crear trabajo se ejecuta mediante las reglas de activación de la fase 5, no por esta entidad por sí sola.

### ChecklistDefinicion

Definición reusable y versionable de controles verificables con código `CHK-####`.

**Atributos relevantes**
- `id_checklist`.
- código `CHK-####`.
- nombre.
- propósito.
- versión/vigencia.
- estado activo.

### ChecklistItem

Ítem ordenado de un checklist.

**Atributos relevantes**
- `id_item`.
- `id_checklist`.
- orden.
- instrucción o criterio verificable.
- obligatoriedad.
- tipo de respuesta o evidencia cuando corresponda.

**Reglas de checklist**
- Los ítems deben poder evaluarse individualmente cuando su cumplimiento tenga valor operativo.
- Una tarea puede requerir uno o varios checklists.
- La ejecución de respuestas y evidencias pertenece a las fases 10 y 11.

### ReglaConfigurable

Regla persistente sólo cuando su comportamiento sea administrable por el negocio, con código `REG-####`.

**Atributos relevantes**
- `id_regla`.
- dominio propietario.
- tipo de regla estructurado.
- versión.
- prioridad/orden sólo cuando exista semántica real de evaluación.
- activa.
- vigencia.

**Reglas**
- No se permite interpretar texto libre como código ejecutable.
- Una validación determinista de integridad, elegibilidad, ranking o transición se implementa como función/guard salvo que exista una razón real para administrarla como datos.

### ParametroRegla

Valor tipado administrable asociado a una regla o dominio, con código `PAR-####`.

**Atributos relevantes**
- `id_parametro`.
- `id_regla` o dominio propietario.
- clave.
- tipo de dato.
- valor tipado.
- unidad.
- alcance.
- vigencia.

### IndicadorKPI

Definición atómica de una medición con código `KPI-####`.

**Atributos relevantes**
- `id_kpi`.
- nombre.
- descripción.
- unidad.
- definición de cálculo.
- fuente de datos.
- periodicidad de medición.
- vigencia.

**Reglas**
- Cada KPI representa una sola medida interpretable.
- Una tarea o proceso puede relacionarse con varios KPI.
- Las metas y resultados se modelan separadamente de la definición del KPI.
- El cálculo y persistencia de resultados se formaliza en la fase 15.

### Automatizacion

Definición de un componente ejecutable gobernable con código `AUT-####`, únicamente cuando exista automatización real.

**Atributos relevantes**
- `id_automatizacion`.
- nombre.
- tipo.
- contrato de entrada/salida.
- propietario.
- versión.
- estado.
- tareas o procesos relacionados.

**Reglas**
- Una clasificación como “potencialmente automatizable” no crea una Automatizacion.
- Un adaptador de integración o activación no se considera automáticamente una Automatizacion; se clasifica por su responsabilidad real.

## Catálogos y enumeraciones

Los siguientes conceptos pueden representarse como catálogos o enumeraciones estables cuando exista gobierno de negocio: tipo funcional, nivel de riesgo, criticidad operativa, nivel de automatización, tipo de relación entre tareas y estado funcional de una definición. Los estados de despliegue, migración, preproducción o piloto no forman parte del ciclo de vida funcional permanente de una tarea.

## Relaciones principales

- Macroproceso 1:N Proceso.
- Proceso 1:N Subproceso.
- Subproceso 1:N DefinicionTarea como clasificación primaria.
- DefinicionTarea 1:N VersionDefinicionTarea.
- FlujoOperativo N:M DefinicionTarea mediante FlujoTarea.
- DefinicionTarea N:M DefinicionTarea mediante RelacionTarea dirigida.
- DefinicionTarea N:M ChecklistDefinicion.
- DefinicionTarea N:M IndicadorKPI.
- DefinicionTarea N:M Automatizacion cuando existan componentes ejecutables.
- ReglaConfigurable 1:N ParametroRegla.

## Funciones puras

### validar_definicion_tarea(id_tarea, version)
Valida identidad, campos obligatorios, jerarquía, referencias y coherencia de la definición sin cambiar estado.

### determinar_version_vigente_tarea(id_tarea, fecha_contexto)
Resuelve la versión de definición aplicable en una fecha o contexto.

### resolver_jerarquia_tarea(id_tarea, version)
Devuelve macroproceso, proceso y subproceso aplicables.

### resolver_flujos_tarea(id_tarea, version)
Devuelve los flujos vigentes asociados sin activar obligaciones.

### resolver_dependencias_tarea(id_tarea, version)
Devuelve relaciones dirigidas y su semántica declarada.

### resolver_checklists_tarea(id_tarea, version)
Devuelve checklists e ítems vigentes requeridos por la definición.

### resolver_kpi_tarea(id_tarea, version)
Devuelve indicadores asociados sin calcular sus resultados.

### obtener_regla_configurable(id_regla, alcance, fecha_contexto)
Resuelve una regla administrable y sus parámetros tipados cuando corresponda.

## Procedimientos o comandos

### crear_definicion_tarea
Crea una nueva identidad `TAR` sólo para una obligación operativa genuinamente distinta.

### publicar_version_definicion
Valida y hace vigente una nueva versión sin alterar el significado histórico de obligaciones previas.

### desactivar_definicion_tarea
Impide nuevas activaciones de una definición sin eliminar su historial.

### vincular_tarea_flujo
Crea o cierra una asociación versionada entre tarea y flujo.

### vincular_dependencia_tarea
Registra una relación tipada entre tareas y rechaza referencias o ciclos inválidos según su semántica.

## Estados y transiciones

En esta fase sólo se confirma la distinción funcional entre una definición **activa** y **no activa para nuevas obligaciones**. Los estados de revisión, piloto, integración, preproducción o migración pertenecen al gobierno de despliegue y no deben confundirse con el estado funcional permanente.

Una nueva versión puede sustituir a la versión vigente mediante un comando controlado; la versión anterior permanece disponible para histórico. La activación de instancias reales se gobierna en la fase 5 y siguientes.

## Validaciones

- `TAR-####` único y no reutilizable.
- Jerarquía completa y sin claves ambiguas.
- Una versión vigente por tarea y alcance compatible.
- Resultado esperado separado de descripción e instrucciones.
- Criterio de validación separado de evidencia.
- Referencias a flujo, checklist, KPI, regla o automatización deben existir y estar vigentes.
- KPI compuesto debe descomponerse en medidas atómicas.
- Una meta no puede registrarse como tarea si no constituye una obligación real.
- Una relación de tarea debe declarar su tipo; pertenecer al mismo flujo no sustituye esta relación.
- Las reglas configurables no contienen código ni expresiones libres ejecutables.
- Los parámetros tienen tipo y unidad compatibles.
- No se infiere una automatización ejecutable a partir de un nivel de automatización.

## Permisos

- Crear o versionar definiciones requiere autoridad de gobierno del catálogo.
- Desactivar definiciones requiere autoridad equivalente o superior a la definida para gobierno operativo.
- Cambiar jerarquías, flujos, checklists, reglas, KPI o automatizaciones debe quedar auditado.
- Los permisos concretos se resuelven mediante el modelo de roles y alcance definido en la fase 2.

## Dependencias con otros dominios

- **Fase 1**: puestos y perfiles responsables.
- **Fase 2**: usuarios, roles, permisos y autoridad.
- **Fase 3**: calendario y configuración temporal.
- **Fase 5**: reglas de activación, calendario, evento, condición y SLA.
- **Fase 6**: creación de instancias de trabajo.
- **Fases 7–8**: elegibilidad y balanceo.
- **Fase 11**: tipos, requisitos e integridad de evidencia.
- **Fase 12**: validación, supervisión y autorizaciones.
- **Fase 15**: cálculo, metas, resultados y efectos de KPI.
- **Fase 17**: auditoría y versionado crítico.
- **Fase 18**: equivalencias y retiro de identidades legacy.
- **Fase 19**: automatizaciones e integraciones externas cuando correspondan.

## Errores de dominio

- `TAREA_ID_DUPLICADO`.
- `VERSION_TAREA_SOLAPADA`.
- `JERARQUIA_INVALIDA`.
- `REFERENCIA_FLUJO_INVALIDA`.
- `REFERENCIA_CHECKLIST_INVALIDA`.
- `KPI_NO_ATOMICO`.
- `DEPENDENCIA_TAREA_INVALIDA`.
- `CICLO_DEPENDENCIA_NO_PERMITIDO`.
- `REGLA_CONFIGURABLE_NO_ESTRUCTURADA`.
- `PARAMETRO_TIPO_INVALIDO`.
- `AUTOMATIZACION_NO_DEFINIDA`.

## Salidas del dominio

El dominio expone definiciones versionadas y resoluciones de lectura suficientes para que las fases de activación, generación, asignación, ejecución y reporte consuman una única semántica de tarea sin duplicar catálogos ni depender de estructuras de migración.
