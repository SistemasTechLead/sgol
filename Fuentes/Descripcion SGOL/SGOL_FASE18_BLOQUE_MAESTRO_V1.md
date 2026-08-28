# SGOL — Fase 18 · Coexistencia legacy/canónica y migración

## Propósito del dominio

Este dominio gobierna la transición controlada desde fuentes, identificadores y productores anteriores hacia el modelo canónico del SGOL. Su objetivo es preservar trazabilidad, impedir doble generación, transformar únicamente aquello cuya equivalencia esté demostrada y retirar componentes anteriores sólo cuando el sustituto canónico haya probado que reproduce la obligación de negocio completa.

La migración no redefine los dominios F1-F17. Conserva su semántica y determina cómo incorporar datos y operaciones preexistentes sin inventar precisión que las fuentes no contienen.

## Principios obligatorios

- Una equivalencia de definición no autoriza por sí sola el retiro del productor anterior.
- Migrar una definición y migrar hechos históricos son procesos distintos.
- La identidad canónica nunca se deriva de renombrar un identificador legacy.
- Todo registro migrado conserva una referencia verificable a su origen.
- Un mapeo ambiguo nunca se resuelve por orden de filas, similitud de texto o conveniencia técnica.
- Cuando un objeto legacy puede corresponder a varios conceptos canónicos, la resolución debe utilizar contexto de negocio suficiente o conservarse como no reconciliado.
- Cuando varios objetos legacy convergen en una sola definición canónica, los hechos históricos no se fusionan automáticamente. Sólo se consolidan cuando existe evidencia de que representan la misma obligación real.
- Los campos ausentes no se rellenan con valores inferidos salvo que exista una regla de transformación formal, versionada y auditable.
- Las reglas actuales no se aplican retroactivamente para alterar el significado de hechos históricos. Una validación legacy puede conservarse como tal aunque no satisfaga los gates actuales, siempre marcada con su procedencia.
- Durante coexistencia debe existir un único productor autorizado por cada obligación y contexto semántico.
- Dos productores no pueden generar simultáneamente la misma obligación bajo la misma clave de negocio.
- El cutover es una operación gobernada, auditable e idempotente.
- El retiro de un productor anterior sólo ocurre después de demostrar equivalencia funcional de extremo a extremo.
- Las guardas de coexistencia son temporales. Deben desaparecer cuando finalice el cutover del alcance que protegen.
- La migración debe poder reconciliar conteos, relaciones, claves naturales, estados y excepciones antes de declarar completado un lote.
- Los lotes de migración usan la infraestructura de corridas, auditoría, errores e idempotencia definida en F17.
- La restauración o compensación ante un fallo de migración se define antes de ejecutar el lote; no se improvisa mediante edición manual.

## Tipos de relación de migración

### `UNO_A_UNO`
Un concepto legacy tiene un único destino canónico demostrado.

### `VARIOS_A_UNO`
Múltiples conceptos legacy convergen en una sola definición canónica. Cada hecho fuente conserva su identidad de procedencia; sólo se consolidan hechos cuando representan la misma obligación.

### `UNO_A_VARIOS`
Un concepto legacy contiene responsabilidades que en el modelo canónico pertenecen a varias definiciones. La migración del histórico requiere contexto suficiente para decidir el destino de cada hecho; si no existe, el hecho queda no reconciliado.

### `CAMBIO_DE_TIPO`
Un concepto legacy cambia de responsabilidad semántica, por ejemplo de tarea a KPI, regla o parámetro. Los hechos históricos no se reetiquetan automáticamente como hechos del nuevo tipo.

### `SIN_DESTINO`
No existe evidencia suficiente para definir un destino. El concepto se conserva transitoriamente o sólo como procedencia hasta que exista decisión de negocio.

## Entidades y estructuras de migración

### `ReferenciaOrigenLegacy`

Referencia persistente que permite localizar el registro original del que provino una entidad o hecho canónico.

Atributos mínimos:
- `id_referencia_origen`;
- `sistema_origen`;
- `tipo_objeto_origen`;
- `id_objeto_origen`;
- `tipo_entidad_destino`;
- `id_entidad_destino`;
- `id_corrida_migracion`;
- `estado_reconciliacion`;
- `transformacion_aplicada`;
- `hash_origen` o evidencia equivalente cuando sea útil;
- `creado_en`.

La referencia permanece después del retiro del sistema anterior cuando sea necesaria para auditoría, soporte o conciliación.

### `MapaEquivalenciaMigracion`

Estructura temporal y versionada que describe cómo se traduce una identidad o concepto de origen hacia el modelo canónico.

Atributos mínimos:
- `id_mapa`;
- `version_mapa`;
- `tipo_objeto_origen`;
- `id_origen`;
- `tipo_destino`;
- `id_destino` cuando exista;
- `tipo_relacion`;
- `contexto_requerido`;
- `confianza`;
- `estado_resolucion`;
- `evidencia_decision`;
- `vigente_desde`;
- `vigente_hasta`.

No forma parte del modelo operativo permanente una vez terminada la migración, salvo que se decida conservarlo como archivo histórico de transformación.

### `ControlCoexistencia`

Configuración temporal que determina qué productor tiene autoridad para generar una obligación durante el periodo de transición.

Atributos mínimos:
- `id_control`;
- `alcance_semantico`;
- `clave_contexto` cuando aplique;
- `productor_autorizado`;
- `productor_bloqueado`;
- `estado`;
- `motivo`;
- `vigente_desde`;
- `vigente_hasta`;
- `id_autorizacion_cutover`.

Valores de `productor_autorizado`:
- `LEGACY`;
- `CANONICO`;
- `NINGUNO` para una suspensión controlada.

Nunca se autoriza `LEGACY_Y_CANONICO` para el mismo alcance.

### `CorridaSistema` de tipo `MIGRACION`

F18 reutiliza la entidad definida en F17. Cada lote relevante conserva parámetros, versión de mapa, conteos, resultado, errores y correlación.

## Estados de reconciliación

`ReferenciaOrigenLegacy.estado_reconciliacion` utiliza como mínimo:
- `PENDIENTE`;
- `MAPEO_UNICO`;
- `AMBIGUO`;
- `TRANSFORMADO`;
- `RECONCILIADO`;
- `EXCLUIDO_JUSTIFICADO`;
- `ERROR`.

Un lote sólo puede declararse reconciliado cuando todos sus registros están en un estado terminal permitido por la política del lote.

## Servicios y funciones

### `resolver_equivalencia(origen, contexto)`
Devuelve el destino canónico cuando el mapa vigente y el contexto permiten una resolución inequívoca. Si existen varios destinos compatibles, devuelve `AMBIGUO` y no selecciona uno arbitrariamente.

### `transformar_registro_legacy(registro, mapa)`
Produce una representación canónica candidata sin persistirla. Debe distinguir campos transportados, transformados, omitidos y desconocidos.

### `validar_registro_migrado(origen, candidato)`
Ejecuta controles de integridad estructural y de dominio antes de persistir el candidato.

### `migrar_lote(contexto)`
Comando transaccional/orquestado que reserva idempotencia, inicia corrida F17, transforma registros, persiste hechos válidos, registra referencias de origen y produce conteos de conciliación.

### `reconciliar_lote(id_corrida)`
Compara fuente y destino usando conteos, claves, relaciones y controles de calidad. No declara éxito únicamente porque no existan errores técnicos.

### `validar_no_doble_generacion(alcance, clave_negocio)`
Comprueba qué productor posee autoridad y verifica que la obligación no haya sido generada previamente por otra fuente.

### `autorizar_cutover(alcance)`
Cambia de forma auditable la autoridad de generación desde el productor anterior al canónico una vez satisfechos todos los gates.

### `retirar_productor_legacy(alcance)`
Deshabilita el productor anterior después del cutover confirmado. El retiro técnico no elimina las referencias históricas.

## Migración de definiciones

Una definición puede migrarse cuando:
1. su responsabilidad de negocio está identificada;
2. su destino canónico es inequívoco o existe una transformación aprobada;
3. las reglas de activación, asignación, evidencia, validación y excepción necesarias están definidas;
4. se ha probado el ciclo de vida completo aplicable;
5. existe una política de coexistencia que evita doble generación;
6. existe autorización de cutover.

Una definición que cambie de tipo —por ejemplo, tarea a KPI— no convierte automáticamente sus ejecuciones históricas en mediciones del nuevo tipo.

## Migración de obligaciones y ejecuciones históricas

Cada hecho histórico se trata de forma independiente de la definición:

1. Se identifica la fuente y se conserva su ID original.
2. Se determina si representa una obligación real.
3. Se resuelve, si es posible, la definición canónica aplicable.
4. Se crea una nueva identidad canónica opaca para la obligación migrada.
5. Se conservan los timestamps y estados realmente conocidos.
6. Los campos desconocidos permanecen desconocidos; no se fabrican inicios, evidencias, validadores ni planes.
7. La información de evidencia, validación y excepciones se descompone hacia sus dominios F11-F13 cuando la fuente lo demuestra.
8. Si una referencia a plan existe pero el plan fuente ya no está disponible, se conserva como procedencia y no se fabrica un `PlanOperativo` histórico.
9. Los arrastres se reconcilian con la obligación original cuando la fuente demuestra continuidad; no se cuentan automáticamente como obligaciones nuevas.
10. Una validación histórica que no satisfaga los gates actuales se conserva como decisión legacy con marca de procedencia y calidad, no como una nueva validación F12 emitida retroactivamente.

## Prevención de doble generación durante coexistencia

Para cada alcance sujeto a cutover debe existir una clave semántica de coexistencia suficiente para distinguir la obligación real. La validación ocurre antes de F6 y debe considerar, según el dominio:
- definición;
- período;
- fecha/ventana;
- sucursal;
- caso o evento origen;
- servicio o subtipo;
- cualquier dimensión necesaria para evitar falsos positivos.

Las guardas por coincidencia de fecha sólo son válidas cuando la equivalencia real puede demostrarse con esa dimensión. No deben generalizarse a otros contextos.

## Gates de cutover

Un alcance sólo puede pasar de productor `LEGACY` a `CANONICO` cuando se hayan satisfecho todos los siguientes gates:

1. **Equivalencia semántica aprobada.**
2. **Definición canónica completa** en los dominios requeridos.
3. **Activación F5** configurada e idempotente.
4. **Generación F6** probada sin duplicados.
5. **Elegibilidad y asignación F7-F8** probadas cuando apliquen.
6. **Planificación/publicación F9** probada cuando aplique.
7. **Ejecución F10** probada.
8. **Evidencia F11 y validación F12** probadas cuando apliquen.
9. **Excepciones F13** probadas cuando apliquen.
10. **Cierre F14** probado para obligaciones que participan en el cierre.
11. **Impacto económico F15** validado cuando aplique.
12. **Auditoría, idempotencia y recuperación F17** verificadas.
13. **Reconciliación del histórico** definida o completada según el alcance.
14. **Productor legacy identificado y deshabilitable** sin afectar otros alcances.
15. **Autorización explícita de cutover** registrada.

Si un gate falla, el productor legacy permanece autorizado para ese alcance y el canónico no toma control real.

## Retiro de estructuras temporales

Después del cutover completo:
- las guardas de coexistencia del alcance se desactivan;
- el productor legacy queda inhabilitado;
- los adaptadores creados únicamente para traducir estructuras legacy se retiran cuando no tengan otros consumidores;
- las tablas o staging exclusivamente temporales dejan de participar en runtime;
- las referencias históricas necesarias para auditoría permanecen;
- los reportes de reconciliación se conservan según la política de auditoría/migración.

## Errores de dominio

- `MIGRACION_MAPEO_AMBIGUO`;
- `MIGRACION_DESTINO_NO_DEFINIDO`;
- `MIGRACION_REGISTRO_INCOMPATIBLE`;
- `MIGRACION_REFERENCIA_ORIGEN_DUPLICADA`;
- `MIGRACION_RECONCILIACION_FALLIDA`;
- `COEXISTENCIA_DOBLE_PRODUCTOR`;
- `COEXISTENCIA_DUPLICADO_DETECTADO`;
- `CUTOVER_GATES_INCOMPLETOS`;
- `CUTOVER_NO_AUTORIZADO`;
- `LEGACY_RETIRO_BLOQUEADO`.

## Dependencias

- F4 define las identidades y responsabilidades canónicas.
- F5-F6 definen activación, idempotencia y generación de obligaciones.
- F7-F15 aportan las reglas funcionales que deben probarse antes del cutover.
- F17 aporta corridas, auditoría, errores, transacciones, idempotencia y recuperación.
- F19 deberá aplicar los mismos principios a fuentes e integraciones externas.
- F20 consolidará qué estructuras de migración permanecen sólo como histórico y cuáles se retiran definitivamente.

## Resultado del dominio

El sistema debe poder demostrar, para cualquier registro migrado o alcance en coexistencia:
- de qué fuente provino;
- qué transformación se aplicó;
- qué identidad canónica recibió;
- si el mapeo fue inequívoco;
- qué productor estaba autorizado en ese momento;
- cómo se evitó una doble generación;
- cómo se reconciliaron fuente y destino;
- quién autorizó el cutover;
- y por qué el productor anterior pudo ser retirado.
