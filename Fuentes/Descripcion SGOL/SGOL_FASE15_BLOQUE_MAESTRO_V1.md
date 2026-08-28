# SGOL — Fase 15 · Bonos, KPI e integración con nómina

## Propósito del dominio

Este dominio transforma hechos operativos cerrados y métricas verificables en resultados de KPI e incentivos económicos auditables. Su responsabilidad termina en la preparación y trazabilidad del movimiento económico que debe consumir nómina; no sustituye el cálculo integral de salario, impuestos, retenciones ni demás conceptos propios del sistema de nómina.

## Principios obligatorios

- Toda consecuencia económica, laboral o patrimonial se rige por `GOB-INC-001`.
- Ningún bono se deduce directamente de un estado técnico, una etiqueta de validación o una excepción sin una política de incentivo vigente y versionada.
- El cálculo económico se ejecuta sobre un `PeriodoOperativo` cerrado, salvo que una política autorizada defina expresamente otro corte.
- Una política identifica con precisión qué hechos, KPI, umbrales, prorrateos, exclusiones y excepciones participan en el cálculo.
- Los KPI se calculan a partir de datos fuente identificables; una etiqueta descriptiva no constituye una definición de KPI.
- Un KPI usado para determinar dinero debe conservar el valor calculado, la versión de su definición y la huella de sus entradas.
- El resultado económico aprobado no se sobrescribe por una recalculación posterior. Toda corrección genera una nueva versión o ajuste auditable.
- El sistema usa `id_empleado` estable para resultados e integración; el nombre sólo es un atributo descriptivo.
- La integración con nómina se realiza mediante movimientos idempotentes, no escribiendo directamente en una fila identificada por nombre y semana.
- Un movimiento de nómina debe poder reconciliarse con el resultado de incentivo que lo originó.
- Bonos de tareas, bonos de mercancía, bonos de meta, reconocimientos u otros incentivos son conceptos diferentes aunque terminen en la misma nómina.
- F15 no altera resultados F12 ni excepciones F13 para conseguir un resultado económico deseado.

## Mapa del dominio

1. **Definición de KPI**: especifica qué se mide, fórmula, unidad, universo, fuente y periodicidad.
2. **Medición de KPI**: resultado calculado para un período, empleado, sucursal, tarea, proceso u otro contexto.
3. **Política de incentivo**: regla versionada que transforma hechos/KPI en elegibilidad, factor y monto.
4. **Asignación de política**: determina a qué empleado, puesto, rol o población aplica una política y durante qué vigencia.
5. **Resultado de incentivo**: resultado económico trazable por empleado, período y política.
6. **Aprobación económica**: decisión autorizada necesaria antes de integrar a nómina cuando la política lo exija.
7. **Movimiento de nómina**: instrucción económica idempotente que representa el importe aprobado a transferir.
8. **Lote de nómina**: agrupación opcional de movimientos para un corte o exportación.

## Entidades persistentes

### DefinicionKPI

Entidad gobernada por el catálogo lógico del sistema.

**Atributos relevantes**
- `id_kpi` estable;
- nombre y descripción;
- unidad de medida;
- granularidad/contexto;
- fórmula o estrategia de cálculo versionada;
- fuente o contrato de datos requerido;
- periodicidad;
- regla de redondeo;
- vigencia;
- estado de publicación;
- propietario funcional.

Una lista de métricas escritas en texto no sustituye una `DefinicionKPI`.

### MedicionKPI

Se persiste cuando el valor participa en incentivos, auditoría, cierre o comparación histórica no reproducible con garantías suficientes.

**Atributos relevantes**
- `id_medicion_kpi`;
- `id_kpi` y versión;
- `id_periodo_operativo` o ventana temporal;
- contexto de medición: empleado, sucursal, proceso, tarea u otro;
- valor;
- numerador/denominador cuando corresponda;
- unidad;
- calculado_en;
- versión de fuente o huella de entradas;
- estado de calidad/revisión.

### PoliticaIncentivo

Representa una regla económica aprobada y versionada.

**Atributos relevantes**
- `id_politica_incentivo`;
- código y nombre;
- tipo de incentivo;
- versión;
- vigencia;
- estado `BORRADOR`, `VIGENTE`, `RETIRADA`;
- base económica o estrategia para obtenerla;
- KPI/hechos requeridos;
- umbrales;
- factores de cumplimiento;
- reglas de prorrateo;
- exclusiones y excepciones;
- reglas de redondeo;
- necesidad de aprobación manual;
- autoridad que aprobó la versión.

No existe un factor implícito para resultados parciales: debe declararse en la política.

### AsignacionPoliticaIncentivo

Vincula una política con la población a la que aplica.

**Atributos relevantes**
- `id_asignacion_politica`;
- `id_politica_incentivo`;
- alcance por empleado, puesto, rol, sucursal o criterio autorizado;
- base individual cuando corresponda;
- vigencia;
- prioridad o regla de coexistencia entre políticas.

La base individual de un incentivo debe conservar vigencia histórica; cambiarla no reescribe períodos anteriores.

### ResultadoIncentivo

Hecho económico calculado para una persona y un período.

**Atributos relevantes**
- `id_resultado_incentivo`;
- `id_periodo_operativo`;
- `id_empleado`;
- `id_politica_incentivo` y versión;
- base elegible;
- factores aplicados;
- monto calculado;
- moneda;
- detalle explicable de componentes;
- estado;
- calculado_en;
- aprobado_en / aprobado_por cuando aplique;
- huella de entradas y mediciones KPI utilizadas;
- referencia a corrección o versión anterior.

**Estados mínimos**
- `PENDIENTE_DATOS`;
- `CALCULADO`;
- `REQUIERE_REVISION`;
- `APROBADO`;
- `ANULADO`.

### MovimientoNomina

Instrucción económica que cruza la frontera entre SGOL y nómina.

**Atributos relevantes**
- `id_movimiento_nomina`;
- `id_resultado_incentivo`;
- `id_empleado`;
- período/corte de nómina destino;
- código de concepto de nómina;
- importe y moneda;
- signo/tipo de movimiento;
- clave idempotente única;
- estado de integración;
- referencia externa cuando exista;
- fecha de preparación, envío y confirmación.

El movimiento no contiene salario base, impuestos o retenciones salvo que SGOL sea explícitamente designado como autoridad de esos conceptos, lo cual no forma parte de este dominio.

### LoteNomina

Agrupa movimientos de un corte cuando el receptor requiera una unidad de transferencia o conciliación.

**Atributos relevantes**
- `id_lote_nomina`;
- período/corte;
- creado_en / creado_por;
- total de movimientos e importe;
- estado;
- huella del contenido;
- referencia externa.

## Datos derivados y vistas

No se persisten por defecto:
- porcentaje de cumplimiento mostrado en dashboards cuando puede recalcularse con datos cerrados;
- ranking o comparativos de KPI que no formen parte de una consecuencia económica;
- totales agregados de bonos por equipo/período;
- vistas de prenómina;
- indicadores visuales de impacto.

Cuando cualquiera de esos valores sea entrada de una decisión económica o contractual, debe existir una `MedicionKPI` o `ResultadoIncentivo` persistente que capture la versión utilizada.

## Reglas de cálculo

### Elegibilidad económica

`evaluar_elegibilidad_incentivo` debe comprobar como mínimo:
1. política vigente para el período;
2. empleado identificado y dentro del alcance;
3. período permitido para liquidación;
4. disponibilidad de todos los hechos/KPI obligatorios;
5. inexistencia de bloqueos o inconsistencias de F12/F13/F14;
6. ausencia de un resultado aprobado equivalente ya existente.

### Cálculo de incentivo

El cálculo debe ser determinista para una misma versión de política y las mismas entradas.

Conceptualmente:

`monto = base_elegible × factor_cumplimiento × factor_prorrateo ± ajustes_autorizados`

Cada componente debe provenir de la política vigente o de un ajuste autorizado. No se utilizan constantes ocultas ni reglas inferidas desde datos históricos.

### Cumplimiento de tareas

F12 aporta decisiones de cumplimiento y F13 aporta excepciones. F15 transforma esos hechos en factores económicos únicamente cuando una política publicada define esa relación.

- `CUMPLIDA` no significa automáticamente 100% económico.
- `INCOMPLETA` no tiene una penalización implícita.
- `NO_CUMPLIDA` no define por sí sola un descuento monetario.
- una excepción justificada no implica automáticamente neutralidad económica.

### KPI

`calcular_kpi` debe declarar:
- universo de observaciones;
- ventana temporal;
- filtros;
- numerador y denominador cuando aplique;
- tratamiento de faltantes;
- regla de redondeo;
- fuente y versión.

Un KPI de meta es resultado medible y no una obligación operativa independiente, salvo que exista además una tarea específica para ejecutar acciones asociadas.

## Comandos transaccionales

### `liquidar_incentivos_periodo`

1. valida que el período sea elegible para liquidación;
2. obtiene políticas vigentes y asignaciones aplicables;
3. calcula/persiste las mediciones KPI necesarias;
4. calcula un `ResultadoIncentivo` por empleado/política;
5. marca para revisión los casos con entradas incompletas o contradicciones;
6. no genera movimiento de nómina para resultados no aprobables;
7. registra auditoría y correlación.

El comando es idempotente para la misma versión de política y conjunto de entradas.

### `aprobar_resultado_incentivo`

Requiere autoridad explícita cuando la política lo determine. Registra actor, fecha y motivo. Una aprobación no modifica las entradas históricas.

### `preparar_movimientos_nomina`

Crea movimientos sólo desde resultados `APROBADO` y sin movimiento equivalente. La clave idempotente evita duplicar el mismo concepto económico en reintentos.

### `anular_o_corregir_resultado_incentivo`

No elimina el resultado original. Registra anulación/ajuste con referencia al hecho corregido y, si ya hubo integración, genera el movimiento compensatorio correspondiente.

## Integración con nómina

El contrato de salida debe incluir como mínimo:
- `id_movimiento_nomina`;
- clave idempotente;
- `id_empleado` estable;
- período/corte destino;
- código de concepto;
- monto;
- moneda;
- referencia al resultado de incentivo;
- fecha efectiva;
- observación o desglose necesario para conciliación.

La integración debe rechazar:
- empleados sin identidad externa resoluble;
- períodos ambiguos;
- conceptos sin mapeo;
- importes no aprobados;
- movimientos duplicados;
- montos no reconciliables con el resultado fuente.

La mecánica de transporte, reintentos y adaptadores externos se completa en F19; auditoría, correlación y recuperación técnica se completan en F17.

## Funciones y reglas

- `calcular_kpi(id_kpi, contexto, periodo)`.
- `validar_calidad_medicion_kpi(id_medicion)`.
- `resolver_politicas_incentivo(id_empleado, periodo)`.
- `evaluar_elegibilidad_incentivo(id_empleado, id_politica, periodo)`.
- `calcular_base_elegible(...)`.
- `calcular_factor_cumplimiento(...)`.
- `calcular_factor_prorrateo(...)`.
- `calcular_incentivo(...)`.
- `explicar_resultado_incentivo(id_resultado)`.
- `validar_autoridad_aprobacion(...)`.
- `generar_clave_idempotencia_nomina(...)`.
- `reconciliar_movimiento_nomina(...)`.

## Eventos

- `MedicionKPICalculada` cuando exista consumidor real;
- `IncentivoCalculado`;
- `IncentivoRequiereRevision`;
- `IncentivoAprobado`;
- `MovimientoNominaPreparado`;
- `MovimientoNominaConfirmado`;
- `AjusteEconomicoRegistrado`.

## Permisos y gobierno

- Definir o modificar políticas de incentivo requiere autoridad económica explícita.
- Aprobar una política y aprobar un resultado pueden ser permisos diferentes.
- El calculador puede ejecutarse automáticamente, pero no puede autoaprobar consecuencias que la política someta a autorización.
- Toda edición de vigencia, base individual, umbral, factor o monto aprobado debe quedar auditada.
- La visualización de montos y datos de nómina se limita por rol y alcance.

## Errores de dominio mínimos

- `PERIODO_NO_CERRADO_PARA_LIQUIDACION`.
- `POLITICA_INCENTIVO_NO_VIGENTE`.
- `EMPLEADO_SIN_POLITICA_APLICABLE`.
- `KPI_SIN_DEFINICION_PUBLICADA`.
- `KPI_SIN_DATOS_SUFICIENTES`.
- `RESULTADO_ECONOMICO_AMBIGUO`.
- `RESULTADO_YA_APROBADO`.
- `SIN_AUTORIDAD_ECONOMICA`.
- `CONCEPTO_NOMINA_SIN_MAPEO`.
- `EMPLEADO_SIN_IDENTIDAD_NOMINA`.
- `MOVIMIENTO_NOMINA_DUPLICADO`.
- `RECONCILIACION_NOMINA_FALLIDA`.

## Dependencias con otros dominios

- **F1**: `id_empleado`, vigencia laboral, puesto y disponibilidad.
- **F2**: permisos, autoridad y alcance.
- **F3/F14**: período operativo y cierre definitivo.
- **F4**: catálogo formal de KPI y definiciones de tarea/proceso.
- **F10**: hechos de ejecución.
- **F11**: evidencias que sustentan hechos utilizados por F12.
- **F12**: decisiones de cumplimiento.
- **F13**: excepciones y tratamientos autorizados.
- **F16**: vistas de KPI, incentivos y prenómina.
- **F17**: auditoría, transacciones, correlación y recuperación.
- **F18**: reconciliación de resultados económicos históricos.
- **F19**: adaptador técnico y sincronización con el sistema de nómina.

## Pendientes reservados para fases posteriores

- F16 definirá cómo se muestran KPI, resultados económicos y prenómina por rol.
- F17 definirá el detalle técnico de auditoría y recuperación de liquidaciones.
- F18 decidirá qué importes y clasificaciones históricas pueden migrarse como hechos fiables y cuáles sólo como referencia de trazabilidad.
- F19 definirá el contrato técnico concreto con el sistema receptor de nómina y sus reintentos.
