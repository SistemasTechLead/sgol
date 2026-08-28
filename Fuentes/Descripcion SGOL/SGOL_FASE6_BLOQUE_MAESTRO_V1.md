# SGOL — Fase 6 · Generación de instancias de trabajo

## Propósito del dominio

Este dominio crea y conserva la **obligación operativa real** que nace cuando una `SolicitudActivacion` válida es aceptada. La instancia es el punto estable que separa la definición de tarea de su futura asignación, planificación, ejecución, evidencia, validación y cierre.

La generación **no vuelve a decidir si la tarea debía activarse**, **no asigna responsable final**, **no publica un plan** y **no crea una ejecución**. Esas responsabilidades pertenecen a F5, F7/F8, F9 y F10 respectivamente.

## Principios obligatorios

- Sólo se persiste una `InstanciaTrabajo` cuando existe una obligación real que debe conservar histórico.
- Una solicitud de activación aceptada produce como máximo una obligación lógica por su `clave_idempotencia`.
- El `id_instancia` es una identidad propia, opaca e inmutable. No se construye concatenando tarea, fecha, responsable, semana ni sucursal.
- La clave de idempotencia y el ID de instancia son conceptos distintos: la primera evita duplicados; el segundo identifica la obligación creada.
- Un reintento con la misma clave y payload compatible devuelve la misma instancia. No es error ni crea una segunda fila.
- La misma clave con datos materialmente incompatibles produce `CONFLICTO_IDEMPOTENCIA`; nunca se sobrescribe silenciosamente la obligación existente.
- La instancia referencia una versión inmutable de la definición de tarea para preservar significado histórico sin copiar texto mutable innecesariamente.
- La fecha objetivo, el timestamp de activación y el vencimiento son campos distintos y no se usan como clave primaria.
- El caso, evento o proceso origen se conserva mediante referencias tipadas. No se incrusta como texto libre dentro de observaciones.
- La falta de responsable final no impide crear la obligación. Elegibilidad y asignación pertenecen a F7/F8.
- PLAN y EJEC tienen identidades propias y referencian la instancia; no sustituyen a `InstanciaTrabajo`.
- Las pruebas PREPROD, registros sintéticos y filas eliminadas mediante rollback no son hechos de negocio y no se migran como instancias reales.
- Reprogramar, arrastrar o derivar una obligación no equivale por sí mismo a una nueva activación. F13 definirá cuándo se conserva la instancia y cuándo existe una derivación explícita.

## Mapa del dominio

1. **Solicitud de entrada**: contrato aceptado proveniente de F5.
2. **Validación de generación**: definición, período, contexto, vencimiento e idempotencia.
3. **Instancia de trabajo**: obligación persistente con identidad propia.
4. **Referencia de origen**: caso, evento, proceso o contexto que explica su nacimiento.
5. **Idempotencia**: garantía de que un reintento no duplica obligaciones.
6. **Transacción**: creación atómica de la instancia.
7. **Evento de salida**: `InstanciaCreada` después del commit.
8. **Referencias legacy**: trazabilidad de migración sin contaminar la identidad objetivo.

## Entidad definitiva: InstanciaTrabajo

Representa una obligación real que debe poder sobrevivir a cambios de asignación, planificación o ejecución.

**Atributos relevantes**
- `id_instancia`;
- `id_tarea` y `version_definicion`;
- referencia a regla/version de activación;
- `clave_idempotencia` con unicidad;
- timestamp de creación;
- timestamp de activación;
- período operativo;
- momento objetivo o fecha de compromiso inicial cuando corresponda;
- fecha/hora de vencimiento cuando exista;
- alcance operativo necesario para distinguir la obligación;
- tipo e identificador del caso/evento/proceso origen cuando corresponda;
- referencia a instancia padre sólo cuando exista una relación real entre obligaciones;
- correlación técnica para auditoría;
- referencias legacy opcionales de migración.

**No contiene como responsabilidad propia**
- responsable final o ranking de candidatos;
- estado de publicación del plan;
- estado de ejecución;
- evidencia entregada;
- resultado de validación;
- impacto de bono;
- cierre semanal.

## Contrato de entrada desde F5

La entrada mínima debe incluir tarea/version, regla/version de activación, clave de idempotencia, timestamp de activación y período operativo. Debe incluir contexto/origen y vencimiento cuando la regla los requiera.

F6 valida el contrato y puede utilizar funciones temporales compartidas para resolver un vencimiento pendiente, pero **no inventa ni reinterpreta la regla de activación**.

## Identidad e idempotencia

### ID de instancia

Debe ser opaco e inmutable. Su formato físico es decisión tecnológica posterior.

### Clave de idempotencia

Representa la obligación lógica. Debe existir una restricción de unicidad efectiva sobre ella o sobre sus componentes normalizados.

**Comportamiento obligatorio**
- clave inexistente → crear instancia;
- misma clave + mismo significado → devolver la instancia existente;
- misma clave + significado distinto → error `CONFLICTO_IDEMPOTENCIA`.

Un `ID_Disparo` puede formar parte de la correlación, pero no se asume universalmente que un disparo siempre produzca exactamente una obligación. La granularidad final la determina la regla de activación.

## Relación con planificación y ejecución

`InstanciaTrabajo` es anterior a la publicación y a la ejecución.

- F7/F8 determinan elegibilidad y asignación.
- F9 crea o versiona el plan que referencia la instancia.
- F10 crea y administra la ejecución que referencia la instancia.

Cambiar responsable, replanificar o ejecutar no cambia la identidad de la obligación salvo una regla explícita de derivación definida en F13.

## Funciones, comandos y eventos

- `ValidarSolicitudGenerable(solicitud)`.
- `ValidarClaveIdempotencia(solicitud)`.
- `ResolverVencimientoInstancia(politica_temporal, contexto, calendario)` cuando aún sea necesario.
- `GenerarIDInstancia()`.
- `CompararPayloadIdempotente(instancia_existente, solicitud)`.
- `ResolverReferenciaOrigen(solicitud)`.
- `CrearInstanciaDesdeSolicitud(solicitud)` — comando transaccional.
- `ObtenerOCrearPorIdempotencia(solicitud)` — operación idempotente.
- `InstanciaCreada` — evento posterior al commit.
- `RegistrarReferenciaLegacyInstancia(...)` — operación de F18.

## Resultado de generación

La operación devuelve uno de estos resultados lógicos:

- `CREADA`: se insertó una nueva obligación;
- `YA_EXISTE`: reintento compatible, se devuelve el mismo `id_instancia`;
- `CONFLICTO`: misma clave con payload incompatible;
- `RECHAZADA`: solicitud inválida o contexto insuficiente.

F6 termina con una instancia creada. Los estados de asignación, plan, ejecución, excepción y cierre se documentan en fases posteriores.

## Reglas de negocio y validaciones

- La definición y versión deben existir y ser aplicables al momento de activación.
- La solicitud debe provenir del contrato F5; F6 no procesa disparadores crudos.
- La clave de idempotencia es obligatoria antes de persistir.
- El período operativo debe resolverse con el gobierno de F3 y respetar restricciones de cierre definidas posteriormente en F14.
- Los orígenes EVENTO/PROCESO y las condiciones que lo requieran deben conservar identidad de caso/contexto.
- Un vencimiento no puede contradecir la política temporal aplicable.
- Un fallo de creación no puede dejar una instancia parcial ni publicar `InstanciaCreada` antes del commit.
- Una fila de prueba o rollback no se convierte en obligación histórica.
- La ausencia de asignación final no bloquea la creación de la instancia.

## Hallazgos que condicionan la migración

La fotografía S050 muestra 214 filas en PLAN y 914 filas históricas en EJEC. Los 214 PLAN actuales tienen una sola EJEC relacionada y comparten el mismo `ID_TareaGenerada`, lo que confirma que ese campo funciona hoy como identidad implícita de obligación, pero no como una entidad separada.

En EJEC existen 903 identificadores con patrón `T###-YYYYMMDD`, 3 con sufijo de arrastre y 8 provenientes de subtareas legacy decimales. Estos formatos se conservarán sólo como referencias externas de migración.

El esquema `tarea + fecha` no puede representar de forma general dos obligaciones de una misma tarea en el mismo día para ventanas, servicios o contextos distintos. Por ello no se convierte en la clave natural del modelo objetivo.

S049 introduce IDs `INST-*`, pero el motor todavía crea en una misma operación la instancia, `PLAN` y `EJEC` y exige responsable/validador antes de generar. Esa responsabilidad se descompone: F6 crea la obligación; F7/F8 asignan; F9 planifica/publica; F10 ejecuta.

Las 6 filas de `S049_Preprod_Instancias` y 6 filas actuales de `S049_Motor` son evidencia PREPROD; no tienen PLAN/EJEC generados en la fotografía y no se migran como obligaciones productivas.

## Dependencias

- **F3**: período operativo, calendario y gobierno temporal.
- **F4**: definición y versión de tarea.
- **F5**: solicitud, regla de activación, contexto e idempotencia lógica.
- **F7/F8**: elegibilidad y asignación.
- **F9**: planificación y publicación.
- **F10**: ejecución.
- **F13**: arrastre, reprogramación y derivaciones.
- **F14**: restricciones de período cerrado.
- **F17**: auditoría, correlación, errores, outbox y transacciones.
- **F18**: equivalencias legacy y reconstrucción histórica.
- **F19**: contratos de casos/eventos externos.

## Errores de dominio

- `SOLICITUD_GENERACION_INVALIDA`.
- `DEFINICION_NO_VALIDA`.
- `PERIODO_NO_RESUELTO`.
- `CLAVE_IDEMPOTENCIA_FALTANTE`.
- `CONFLICTO_IDEMPOTENCIA`.
- `ORIGEN_NO_RESUELTO`.
- `CONTEXTO_INSTANCIA_INSUFICIENTE`.
- `VENCIMIENTO_INVALIDO`.
- `TRANSACCION_GENERACION_INCOMPLETA`.
- `HECHO_NO_PRODUCTIVO`.
