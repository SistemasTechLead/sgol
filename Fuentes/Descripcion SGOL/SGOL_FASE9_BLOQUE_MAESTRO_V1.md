# SGOL — Fase 9 · Planificación y publicación

## Propósito del dominio

Este dominio organiza las `InstanciaTrabajo` que ya existen y, cuando corresponda, ya tienen una asignación activa, dentro de un plan operativo asociado a un `PeriodoOperativo`. Su responsabilidad es revisar qué obligaciones entrarán al plan, fijar su compromiso operativo de publicación y hacerlas visibles para ejecución sin volver a crear la obligación ni recalcular su responsable.

La obligación nace en F6. La asignación pertenece a F8. La ejecución pertenece a F10. Las reprogramaciones, cancelaciones, arrastres y reasignaciones posteriores a publicación pertenecen a F13.

## Principios obligatorios

- Publicar una obligación no la crea: sólo compromete una `InstanciaTrabajo` ya existente dentro del plan operativo.
- El plan y sus partidas tienen identidad propia; no se utiliza la identidad de la tarea, la fecha ni la ejecución como sustitutos.
- El plan pertenece a un `PeriodoOperativo` de F3; año y semana son atributos derivados del período, no una clave duplicada distribuida por filas.
- La publicación consume la asignación activa confirmada por F8 y no vuelve a ejecutar ranking ni elegibilidad.
- Una instancia no puede tener dos partidas activas dentro del mismo plan.
- Una publicación no puede producir dos partidas para la misma instancia por reintento del mismo comando.
- La validación previa a publicar debe trabajar sobre referencias canónicas e identidades, no sobre nombres visibles.
- La publicación inicial de un lote es transaccional: o se publican todas las partidas seleccionadas o ninguna cambia de estado.
- Después de publicar, los cambios operativos no se realizan mediante edición silenciosa; deben usar comandos de excepción y conservar trazabilidad.
- Los atributos de tarea versionados, requisitos de evidencia, reglas de validación, políticas de bono y datos de personal no se duplican en el plan salvo que exista una necesidad explícita de snapshot regulatorio o histórico.
- La condición de cierre se obtiene del período y del cierre operativo; no se mantiene una bandera manual paralela para decidir si una partida puede modificarse.
- Las obligaciones todavía sin asignación válida permanecen en una cola derivada de pendientes y no se convierten en partidas publicadas.
- La arquitectura debe permitir publicación incremental controlada cuando una activación válida ocurra después de la publicación inicial del período; esta ruta requiere política explícita y no autoriza reabrir o reescribir silenciosamente lo ya publicado.

## Mapa del dominio

1. **Periodo operativo**: contexto temporal gobernado por F3.
2. **Plan operativo**: conjunto identificado de obligaciones planificadas para el período y alcance correspondiente.
3. **Instancia candidata**: obligación de F6 aún no incorporada al plan.
4. **Asignación activa**: responsable confirmado por F8 que será publicado para la obligación.
5. **Partida de plan**: vínculo persistente entre plan e instancia con su compromiso de ejecución.
6. **Validación de plan**: controles que determinan si una partida puede publicarse.
7. **Publicación**: transición transaccional que hace ejecutables las partidas válidas.
8. **Cola pendiente**: instancias que no pueden publicarse todavía por falta de asignación, configuración o validación.
9. **Cambios posteriores**: excepciones controladas que delegan su semántica a F13.
10. **Salida a ejecución**: partidas publicadas consumibles por F10.

## Entidades persistentes

### PlanOperativo

Representa el plan de un período y alcance operativo.

**Atributos relevantes**
- `id_plan`;
- `id_periodo_operativo`;
- alcance organizativo cuando corresponda;
- `estado_plan`;
- fecha/hora de creación;
- fecha/hora de primera publicación;
- identidad de quien publica;
- versión sólo cuando exista una necesidad real de conservar versiones completas;
- metadatos de auditoría y correlación cuando correspondan.

**Restricciones**
- Debe existir una regla de unicidad para evitar dos planes activos equivalentes sobre el mismo período y alcance.
- El estado del plan no reemplaza el estado del `PeriodoOperativo`; ambas máquinas tienen responsabilidades distintas y deben permanecer consistentes.
- La ausencia de una versión histórica completa no impide auditar cambios: F17 puede conservar eventos y F13 conserva hechos de excepción.

### PartidaPlan

Representa la incorporación de una obligación concreta al plan.

**Atributos relevantes**
- `id_partida_plan`;
- `id_plan`;
- `id_instancia`;
- `id_asignacion_publicada`;
- fecha/hora o ventana planificada cuando aplique;
- estado de la partida;
- fecha/hora de publicación;
- identidad de publicación;
- tipo de incorporación: inicial o incremental cuando corresponda;
- referencia de publicación/correlación para auditoría.

**Restricciones**
- `(id_plan, id_instancia)` debe ser único para partidas activas.
- `id_instancia` debe pertenecer al mismo período y alcance que el plan.
- `id_asignacion_publicada` debe ser una asignación activa de la misma instancia al momento de publicar.
- La partida no recrea descripción, puesto, supervisor, evidencia, bono o reglas de la tarea si éstos ya pueden reconstruirse desde entidades versionadas.

### PublicacionPlan

Hecho de negocio opcional cuando se necesite agrupar y auditar un lote publicado, especialmente para publicaciones incrementales.

**Atributos relevantes**
- `id_publicacion_plan`;
- `id_plan`;
- tipo de publicación: `INICIAL` o `INCREMENTAL`;
- timestamp;
- usuario/servicio autorizador;
- clave idempotente del comando;
- número de partidas incluidas;
- resultado.

Si la auditoría de F17 cubre completamente esta necesidad, puede implementarse como evento auditado en lugar de tabla independiente. No debe duplicar las partidas.

## Objetos derivados; no tablas de hechos por defecto

### ColaPendientePlanificacion

Vista de `InstanciaTrabajo` que todavía no puede publicarse y su causa actual, por ejemplo:
- `SIN_ASIGNACION_ACTIVA`;
- `FUERA_DE_PERIODO`;
- `CONFIGURACION_INCOMPLETA`;
- `VALIDACION_PLAN_FALLIDA`;
- `EXCEPCION_REQUIERE_AUTORIZACION`.

### VistaPlanOperativo

Proyección para UX que combina plan, partida, instancia, definición versionada, asignación activa/publicada y atributos organizativos visibles. Esta vista puede mostrar nombres y descripciones sin convertirlos en llaves persistentes.

### ResumenPlan

Agregación por estado, responsable, fecha, criticidad, proceso o sucursal. Es una consulta, no una tabla duplicada.

## Estados y transiciones

### PlanOperativo

Estados mínimos:
- `EN_CONSTRUCCION`;
- `EN_REVISION`;
- `PUBLICADO`;
- `CERRADO` únicamente cuando F14 confirme el cierre correspondiente.

Transiciones normales:
- `EN_CONSTRUCCION -> EN_REVISION`;
- `EN_REVISION -> PUBLICADO`;
- `PUBLICADO -> CERRADO` por el proceso de cierre.

Una publicación incremental válida no necesita regresar el plan a `EN_REVISION`; incorpora nuevas partidas mediante un comando autorizado y auditable mientras el período permita nuevas obligaciones.

### PartidaPlan

Estados mínimos de F9:
- `EN_REVISION`;
- `PUBLICADA`.

Estados como reasignada, pospuesta, arrastrada, cancelada o cerrada no se redefinen aquí. Son efectos del ciclo de vida de F13/F14 y deben conservar hechos e histórico propios.

## Reglas de construcción del plan

Una instancia puede entrar a revisión sólo si:
- existe y no está anulada por una excepción previa;
- pertenece al período/alcance del plan;
- no existe ya una partida activa para esa instancia;
- su definición y versión son resolubles;
- cumple las precondiciones de planificación establecidas por la configuración aplicable.

La construcción del plan no genera una nueva `InstanciaTrabajo` y no inventa un identificador basado en tarea+fecha.

## Reglas de publicación

### Validación previa

Antes de publicar cada partida se debe confirmar:
- plan y período válidos;
- estado del plan compatible;
- instancia vigente;
- ausencia de duplicado por `id_instancia`;
- asignación activa compatible con la instancia;
- fecha/ventana planificada válida;
- autoridad de publicación;
- ausencia de bloqueo por cierre;
- integridad de referencias canónicas necesarias.

### Publicación inicial

`publicar_plan_inicial(id_plan, partidas, contexto)` debe:
1. bloquear lógicamente el plan para evitar publicación concurrente;
2. revalidar todas las partidas;
3. verificar la transición del período autorizada por F3;
4. persistir las partidas y sus timestamps de publicación;
5. cambiar `PlanOperativo` a `PUBLICADO`;
6. realizar la transición de período que corresponda;
7. confirmar la transacción;
8. emitir `PlanPublicado` y `PartidaPlanPublicada` después del commit.

Un fallo revierte todos los cambios del lote.

### Publicación incremental

`publicar_partida_incremental(id_plan, id_instancia, contexto)` sólo puede utilizarse cuando una obligación fue creada válidamente después de la publicación inicial y una política explícita permite incorporarla al plan abierto.

Debe revalidar período, instancia, asignación, duplicidad y autoridad. No reabre el plan, no modifica partidas anteriores y no cambia por sí sola el estado del período.

### Idempotencia

La repetición del mismo comando con la misma clave debe devolver el resultado ya confirmado. La protección principal se basa en `id_instancia` y en la clave idempotente de publicación, no en nombre de tarea o fecha.

## Congelamiento y cambios posteriores

Publicar congela el compromiso operativo de la partida: instancia, asignación publicada y condiciones planificadas no pueden ser sustituidas mediante edición directa.

Los cambios posteriores deben ejecutarse mediante comandos de dominio:
- reasignación -> F13 utilizando la asignación de F8;
- reprogramación/posposición -> F13;
- cancelación -> F13;
- arrastre/derivación -> F13;
- cierre -> F14.

La UI puede mostrar el estado resultante, pero no debe modificar celdas/campos de forma libre para representar estos procesos.

## Funciones y consultas

- `obtener_plan_periodo(id_periodo, alcance)`.
- `listar_instancias_candidatas_plan(id_plan)`.
- `validar_partida_plan(id_plan, id_instancia, momento)`.
- `validar_asignacion_publicable(id_instancia, id_asignacion)`.
- `detectar_duplicado_partida(id_plan, id_instancia)`.
- `calcular_compromiso_planificado(id_instancia, politica, calendario)` cuando la fecha/ventana no venga fijada desde F5/F6.
- `listar_pendientes_planificacion(id_plan)`.
- `resumir_plan(id_plan)`.

## Comandos transaccionales

- `crear_plan_operativo(id_periodo, alcance, contexto)`.
- `agregar_partida_revision(id_plan, id_instancia, contexto)`.
- `retirar_partida_no_publicada(id_partida, motivo, contexto)`.
- `publicar_plan_inicial(id_plan, contexto)`.
- `publicar_partida_incremental(id_plan, id_instancia, contexto)`.
- `cerrar_plan_desde_cierre_operativo(id_plan, contexto)` — invocado por F14, no por edición manual.

## Eventos

- `PlanCreado`.
- `PartidaPlanAgregada`.
- `PlanPublicado`.
- `PartidaPlanPublicada`.
- `PartidaPlanPublicadaIncrementalmente` cuando aplique.
- `PlanCerrado` emitido por el cierre coordinado de F14.

Los eventos no sustituyen a los hechos persistentes; sirven para notificación, auditoría e integración.

## Permisos

- Crear o preparar planes requiere permiso de planificación dentro del alcance organizativo correspondiente.
- Publicar requiere permiso explícito de publicación y no se hereda sólo por ser responsable de una tarea.
- La publicación incremental debe exigir el mismo nivel de autoridad o una política específica equivalente.
- Las excepciones posteriores requieren los permisos definidos por F13.
- La consulta del plan respeta el alcance de datos de F2.

## Errores de dominio

- `PLAN_YA_EXISTE_PARA_PERIODO`;
- `PLAN_NO_PUBLICABLE`;
- `PERIODO_NO_PUBLICABLE`;
- `INSTANCIA_FUERA_DE_PERIODO`;
- `INSTANCIA_YA_PLANIFICADA`;
- `INSTANCIA_NO_PUBLICABLE`;
- `ASIGNACION_PUBLICABLE_INEXISTENTE`;
- `ASIGNACION_CAMBIO_ANTES_PUBLICAR`;
- `PARTIDA_DUPLICADA`;
- `PUBLICACION_DUPLICADA`;
- `PUBLICACION_INCREMENTAL_NO_AUTORIZADA`;
- `PLAN_CERRADO`;
- `CONFLICTO_CONCURRENCIA_PLAN`;
- `PUBLICACION_NO_AUTORIZADA`.

## Dependencias

- **F2**: autoridad y alcance de quien prepara/publica.
- **F3**: `PeriodoOperativo`, calendario y transiciones permitidas.
- **F4**: definición/version de tarea y atributos configurables.
- **F5/F6**: activación e `InstanciaTrabajo`.
- **F8**: `AsignacionTrabajo` activa.
- **F10**: consume partidas publicadas para iniciar/administrar ejecución.
- **F11/F12/F15**: evidencia, validación y bono se consultan desde sus políticas; F9 no los redefine.
- **F13**: cambios posteriores a publicación.
- **F14**: cierre del período y del plan.
- **F17**: auditoría, correlación e idempotencia técnica.
- **F18**: equivalencias temporales de IDs legacy durante migración.

## Criterios de consistencia

- Un plan activo por período/alcance definido.
- Cero partidas activas duplicadas para una misma instancia.
- Cero partidas publicadas sin instancia válida.
- Cero partidas publicadas con asignación ajena a la instancia.
- Cero publicación inicial parcial ante error.
- Cero edición silenciosa de partidas publicadas.
- Toda publicación conserva timestamp, actor y correlación.
- El estado del plan y el estado del período nunca se contradicen.
- Las obligaciones dinámicas posteriores a publicación sólo entran mediante una ruta incremental explícitamente autorizada.
