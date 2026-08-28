# SGOL — Fase 13 · Excepciones del ciclo de vida

## Propósito del dominio

Este dominio gobierna desviaciones autorizadas del ciclo normal de una obligación: cancelación, posposición o reprogramación, arrastre o continuación, reasignación e incidencias operativas que requieran trazabilidad. Su responsabilidad es conservar el hecho excepcional, su causa, autoridad, decisión y efecto sin convertirlo en un resultado de validación, un estado ordinario de ejecución o un cálculo económico.

## Principios obligatorios

- Una excepción es un hecho persistente independiente de `EjecucionTarea`, `Validacion` y `PartidaPlan`.
- La excepción nunca se aplica mediante edición directa de fechas, responsable o estado visible.
- Cancelación, posposición, arrastre y reasignación no son resultados de validación F12.
- El impacto en bono o KPI no se decide en F13; F15 interpreta la excepción y su causa bajo la política económica vigente.
- Toda excepción debe identificar la obligación afectada, tipo, causa o motivo, actor, timestamp y política aplicada.
- Cuando se requiera autorización, se reutiliza el dominio de autorización de F2 y se conserva la decisión concreta; el rol por sí solo no sustituye la autorización.
- Una excepción debe ser idempotente: repetir el mismo comando no puede aplicar dos veces el mismo cambio.
- El cierre de F14 bloquea excepciones ordinarias; cualquier corrección postcierre requiere una ruta extraordinaria y auditable.
- Una incidencia sólo se persiste como entidad independiente cuando tiene identidad, seguimiento o histórico propio; si sólo explica una excepción puede quedar como causa o detalle de ésta.
- Posponer o arrastrar una obligación conserva por defecto la misma `InstanciaTrabajo`; cambiar la fecha no crea una nueva obligación.
- Una obligación sucesora sólo se crea cuando la política de negocio declara que existe una obligación adicional distinta. En ese caso F13 solicita su activación/generación a F5/F6 y registra la relación entre obligaciones.
- Reasignar conserva la misma obligación y cierra la vigencia de la asignación anterior; no reescribe el histórico.
- Las vistas pueden mostrar un estado operativo compuesto como `POSPUESTA`, `ARRASTRADA`, `CANCELADA` o `REASIGNADA`, pero esas etiquetas se derivan de hechos F13 y no deben sustituir el historial.

## Mapa del dominio

1. **Política de excepción**: configura qué tipos de excepción admite una versión de tarea y bajo qué condiciones.
2. **Causa de excepción**: catálogo administrado de motivos normalizados.
3. **Excepción de ciclo de vida**: hecho que solicita y aplica una desviación sobre una obligación.
4. **Decisión de excepción**: autorización, rechazo, evaluación o resolución auditable.
5. **Cambio de compromiso**: conserva fecha/ventana anterior y nueva para posposición o continuación.
6. **Reasignación**: conserva responsable/asignación anterior y nueva con vigencia.
7. **Incidencia operativa**: hecho independiente únicamente cuando necesita seguimiento propio.
8. **Relación de obligaciones**: vínculo origen/sucesora cuando una excepción realmente genera una obligación adicional.
9. **Estado operativo compuesto**: vista derivada para UX y reportes.

## Entidades persistentes

### PoliticaExcepcion

Configuración versionada por definición de tarea y contexto.

**Atributos relevantes**
- `id_politica_excepcion`;
- `id_version_tarea`;
- `tipo_excepcion`;
- `permitida`;
- estados/condiciones de origen permitidos;
- regla de fecha o ventana cuando corresponda;
- regla de causa obligatoria;
- regla de autorización;
- límite o tratamiento de recurrencia;
- `genera_obligacion_distinta`;
- vigencia y estado activo.

Una política incompleta bloquea la automatización de la excepción.

### CausaExcepcion

Catálogo de causas normalizadas.

**Atributos relevantes**
- `id_causa_excepcion`;
- código y descripción;
- tipos de excepción a los que aplica;
- requiere acción correctiva;
- requiere escalamiento;
- nivel/regla de autorización sugerida;
- vigencia.

La causa no contiene el resultado económico final. Ese efecto pertenece a F15.

### ExcepcionCicloVida

Hecho principal de F13.

**Atributos relevantes**
- `id_excepcion`;
- `id_instancia`;
- `id_partida_plan` nullable;
- `id_ejecucion` nullable;
- `tipo_excepcion`: `CANCELACION`, `POSPOSICION`, `ARRASTRE_CONTINUACION`, `REASIGNACION` u otro tipo aprobado;
- `id_causa_excepcion` nullable sólo cuando la política lo permita;
- motivo/detalle;
- estado de excepción;
- solicitada_en / solicitada_por;
- aplicada_en / aplicada_por nullable;
- clave idempotente y correlación de auditoría.

### DecisionExcepcion

Historial append-only de decisiones sobre una excepción.

**Atributos relevantes**
- `id_decision_excepcion`;
- `id_excepcion`;
- tipo de decisión: autorizar, rechazar, evaluar, revocar;
- actor;
- fecha/hora;
- motivo/resolución;
- `id_autorizacion` nullable;
- referencias de política y correlación.

### CambioCompromiso

Hecho persistente para toda modificación de fecha o ventana de compromiso.

**Atributos relevantes**
- `id_cambio_compromiso`;
- `id_excepcion`;
- `id_instancia`;
- compromiso anterior;
- compromiso nuevo;
- período origen y período destino cuando corresponda;
- motivo y timestamp efectivo.

No destruye el compromiso anterior y no crea otra `InstanciaTrabajo` por sí mismo.

### Reasignacion

Hecho persistente que modifica la responsabilidad de una obligación.

**Atributos relevantes**
- `id_reasignacion`;
- `id_excepcion`;
- `id_instancia`;
- `id_asignacion_anterior`;
- `id_asignacion_nueva`;
- motivo;
- efectiva_en;
- actor y autorización.

La nueva asignación debe cumplir elegibilidad F7 y las reglas de selección/autoridad de F8/F13.

### IncidenciaOperativa

Se crea sólo cuando una incidencia necesita identidad y seguimiento independientes de la excepción.

Puede vincularse con una o varias excepciones. Si la incidencia es únicamente un motivo puntual, se utiliza `CausaExcepcion` y no se crea una entidad adicional.

### RelacionObligacion

Vincula una obligación origen con otra obligación realmente distinta creada por una excepción.

Debe declarar el tipo de relación, por ejemplo `DERIVADA_DE_EXCEPCION`. No se utiliza para una simple posposición o continuación de la misma obligación.

## Estados y transiciones

### Estado de la excepción

- `SOLICITADA`;
- `AUTORIZADA` o `RECHAZADA` cuando la política requiere decisión;
- `APLICADA` cuando el efecto transaccional se confirmó;
- `REVOCADA` sólo mediante ruta extraordinaria que conserve la decisión previa.

La política puede omitir `SOLICITADA` y autorizar/aplicar en una sola transacción cuando el actor tenga autoridad directa, pero el hecho y la decisión deben seguir siendo auditables.

### Efectos por tipo

**Cancelación**
- impide nuevas ejecuciones y publicaciones ordinarias de la obligación;
- si existe una ejecución activa, F13 debe terminarla por una transición excepcional explícita; no puede quedar falsamente `EN_PROCESO`;
- no crea una validación ni calcula bono.

**Posposición / reprogramación**
- conserva la misma obligación;
- crea `CambioCompromiso` con valor anterior y nuevo;
- F9 publica/revisa la partida correspondiente sin duplicar `InstanciaTrabajo`;
- si ya existe ejecución activa, se aplica la política de interrupción/continuación definida para esa tarea.

**Arrastre / continuación**
- representa una obligación que sigue viva más allá del compromiso o período original;
- conserva la misma obligación por defecto y registra continuidad, causa, autorización y recurrencia;
- una repetición puede elevar autoridad o exigir acción correctiva según la política;
- sólo genera otra obligación cuando la política declara que el trabajo sucesor es un deber distinto.

**Reasignación**
- conserva la identidad de la obligación;
- cierra la vigencia de la asignación anterior y crea una nueva;
- no altera hechos ejecutados por el responsable anterior;
- una reasignación con ejecución activa requiere una regla explícita de handoff o interrupción.

**Incidencia**
- por sí sola no modifica el ciclo de vida;
- puede activar una excepción mediante una política explícita.

## Funciones y reglas

- `obtener_politica_excepcion(id_instancia, tipo, momento)`.
- `listar_causas_excepcion(tipo, contexto)`.
- `validar_excepcion_permitida(id_instancia, tipo, contexto)`.
- `resolver_autoridad_excepcion(id_instancia, tipo, actor)`.
- `validar_nuevo_compromiso(id_instancia, fecha_ventana)`.
- `calcular_numero_arrastres(id_instancia)`.
- `requiere_escalamiento_excepcion(id_instancia, causa, recurrencia, criticidad)`.
- `obtener_asignacion_vigente(id_instancia)`.
- `explicar_bloqueo_excepcion(id_instancia, tipo, actor)`.
- `obtener_estado_operativo_compuesto(id_instancia)` para F16.

## Comandos transaccionales

### `cancelar_obligacion`
Valida política, mutabilidad, causa, autoridad y ejecución activa; crea la excepción y su decisión, aplica la cancelación de forma atómica y emite `ObligacionCancelada` después del commit.

### `posponer_obligacion`
Valida nueva fecha/ventana y autoridad, crea excepción + `CambioCompromiso`, conserva la misma instancia y solicita a F9 la actualización/publicación correspondiente.

### `arrastrar_obligacion`
Registra la continuidad sobre la misma instancia, causa, fecha de continuación, recurrencia y autorización. No crea PLAN/EJEC de forma directa. Si la política define una obligación derivada distinta, emite una solicitud explícita hacia F5/F6.

### `reasignar_obligacion`
Valida que el candidato sea elegible, verifica autoridad, cierra la asignación anterior, persiste la nueva asignación y registra la excepción de manera atómica.

### `resolver_excepcion`
Registra una decisión formal sin sobrescribir decisiones previas y aplica el efecto únicamente cuando todos los gates estén satisfechos.

### `aplicar_excepciones_lote`
Recibe IDs explícitos y ejecuta los mismos gates por elemento. Conserva correlación de lote, resultados individuales y política de rollback.

## Eventos

- `ExcepcionSolicitada`;
- `ExcepcionAutorizada`;
- `ExcepcionRechazada`;
- `ExcepcionAplicada`;
- `ObligacionCancelada`;
- `CompromisoReprogramado`;
- `ObligacionArrastrada`;
- `ObligacionReasignada`;
- `IncidenciaRegistrada` cuando corresponda.

Los eventos se emiten después del commit.

## Permisos y autoridad

- F2 determina roles, permisos y alcance; F13 define qué permiso o autorización exige cada tipo de excepción.
- `AUTORIZAR_EXCEPCIONES` no concede permiso ilimitado: debe combinarse con tipo, alcance y política aplicable.
- Una persona no puede autoautorizar una excepción cuando la política exige separación de funciones.
- Repeticiones, criticidad alta o causas no identificadas pueden elevar la autoridad requerida cuando la política lo determine.
- La identidad del actor proviene del usuario autenticado; no se captura como nombre libre.

## Relaciones con otros dominios

- **F4** aporta versión de tarea y configuración base; F13 necesita `PoliticaExcepcion` estructurada.
- **F6** aporta `InstanciaTrabajo`; F13 no crea otra instancia para una simple reprogramación/continuación.
- **F7/F8** aportan elegibilidad y selección para reasignaciones.
- **F9** aplica cambios de compromiso sobre el plan sin duplicar obligaciones.
- **F10** aporta ejecución real y recibe transiciones excepcionales cuando una ejecución debe interrumpirse.
- **F12** conserva decisiones de cumplimiento; no debe registrar excepciones como validaciones.
- **F14** gobierna cierre, congelamiento y paso intersemanal.
- **F15** calcula impacto económico a partir de causas, validaciones y excepciones.
- **F16** presenta estado operativo compuesto y colas de excepciones.
- **F17** conserva auditoría, correlación, errores y transacciones.
- **F18** reconcilia artefactos legacy de arrastre/derivación y duplicidades históricas.

## Errores de dominio

- `EXCEPCION_NO_PERMITIDA_POR_POLITICA`;
- `POLITICA_EXCEPCION_NO_CONFIGURADA`;
- `CAUSA_EXCEPCION_REQUERIDA`;
- `AUTORIZACION_EXCEPCION_REQUERIDA`;
- `PERMISO_EXCEPCION_DENEGADO`;
- `NUEVO_COMPROMISO_INVALIDO`;
- `PERIODO_CERRADO_PARA_EXCEPCION`;
- `EXCEPCION_DUPLICADA`;
- `EXCEPCION_YA_APLICADA`;
- `REASIGNACION_SIN_CANDIDATO_ELEGIBLE`;
- `REASIGNACION_SIN_ASIGNACION_VIGENTE`;
- `EJECUCION_ACTIVA_REQUIERE_TRATAMIENTO_EXCEPCIONAL`;
- `RELACION_DERIVADA_NO_JUSTIFICADA`;
- `DECISION_EXCEPCION_CONFLICTIVA`.
