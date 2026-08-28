# SGOL — Fase 3 · Configuración, calendario y semana operativa

## Propósito del dominio

Este dominio define el contexto temporal bajo el que opera SGOL: qué período se está planificando o ejecutando, qué fechas pertenecen a ese período, qué días son operativos, qué excepciones de calendario aplican, qué configuración temporal gobierna los cálculos y qué estados puede atravesar un período semanal.

El dominio debe separar de forma estricta **configuración**, **datos de calendario derivados**, **excepciones persistentes** y **estado del ciclo de vida de un período**.

## Mapa del dominio

1. **Configuración temporal**: parámetros de negocio realmente administrables, tipados, con alcance y vigencia.
2. **Calendario operativo**: reglas generales que determinan cómo se interpreta una fecha dentro de una organización o sucursal.
3. **Excepciones de calendario**: festivos, cierres, aperturas extraordinarias y traslados autorizados.
4. **Período operativo**: identidad única de una semana de trabajo y su ciclo de vida.
5. **Cálculos temporales**: año/semana ISO, límites de período, día de semana, fin de mes y otras propiedades derivables.
6. **Gobierno semanal**: validación de estados y transiciones del período.

## Principios obligatorios

- El período operativo debe identificarse como una sola unidad; año y semana no son dos estados independientes.
- Toda semana utilizada por SGOL debe seguir una única convención ISO: lunes como inicio y semana 1 como la que contiene el primer jueves del año.
- Las propiedades derivables de una fecha no deben persistirse salvo que exista una razón de rendimiento o auditoría demostrada.
- Un día de fin de semana no equivale necesariamente a un día no operativo.
- Los festivos y cierres extraordinarios son excepciones de negocio y sí deben conservarse cuando afecten la operación.
- El estado de una semana pertenece al período operativo concreto; nunca debe almacenarse como un estado global desacoplado del período.
- Un período cerrado permanece cerrado. La apertura de una nueva semana crea o activa otro período; no reutiliza el estado del período anterior.
- Una restauración por rollback sólo puede revertir una transición dentro de una operación controlada y debe quedar auditada.
- La fecha y hora operativa debe resolverse con la zona horaria aplicable al alcance de la operación, no con la configuración local del equipo cliente.
- Las configuraciones de otros dominios permanecen en sus dominios propietarios; no se centralizan indiscriminadamente en un único registro genérico.

## Entidades definitivas

### ConfiguracionOperativa

Configuración persistente únicamente para valores que el negocio pueda cambiar sin desplegar una nueva versión del sistema.

**Atributos relevantes**
- `id_configuracion`.
- `clave`.
- `tipo_dato`.
- `valor_tipado`.
- `tipo_alcance`.
- referencia al alcance cuando corresponda.
- `vigente_desde`.
- `vigente_hasta` nullable.
- `activa`.
- `modificada_por`.
- `motivo_cambio` cuando sea requerido.

**Reglas**
- La clave debe ser única dentro de su alcance y vigencia.
- El valor debe validarse según su tipo declarado.
- No se almacenan aquí estados de ciclo de vida ni valores derivados.
- Los parámetros sensibles deben conservar histórico de cambios.
- La configuración de un dominio debe ser interpretada por el servicio propietario de ese dominio.

### CalendarioOperativo

Entidad persistente que identifica el conjunto de reglas temporales aplicables a una organización o alcance operativo.

**Atributos relevantes**
- `id_calendario`.
- `codigo_calendario`.
- alcance organizacional.
- `zona_horaria`.
- `vigente_desde`.
- `vigente_hasta` nullable.
- `activo`.

**Reglas**
- Un alcance no puede tener dos calendarios principales vigentes e incompatibles para la misma fecha.
- La zona horaria debe ser válida y resoluble por el sistema.
- Las reglas recurrentes de días operativos pueden variar por alcance.

### ReglaDiaOperativo

Configuración persistente que define el comportamiento normal de cada día de la semana dentro de un calendario.

**Atributos relevantes**
- `id_regla_dia`.
- `id_calendario`.
- `dia_semana`.
- `es_operativo`.
- ventanas horarias cuando correspondan.
- `vigente_desde`.
- `vigente_hasta` nullable.

**Reglas**
- La semana se interpreta de lunes a domingo.
- La condición de sábado o domingo se calcula; la condición operativa se resuelve por esta regla y sus excepciones.
- Una excepción de fecha prevalece sobre la regla recurrente cuando esté vigente y autorizada.

### ExcepcionCalendario

Hecho persistente que modifica el comportamiento normal de una fecha concreta.

**Atributos relevantes**
- `id_excepcion_calendario`.
- `id_calendario`.
- `fecha_referencia`.
- `fecha_efectiva`.
- `tipo_excepcion`.
- `motivo`.
- `es_operativo`.
- `caracter_obligatorio` cuando corresponda.
- `autorizada`.
- `autorizada_por` nullable.
- `vigente`.

**Reglas**
- `fecha_referencia` conserva la fecha normativa o de origen cuando una observancia sea trasladada.
- `fecha_efectiva` es la fecha que afecta la operación.
- Una excepción no autorizada no modifica el calendario operativo.
- Debe impedirse más de una excepción activa contradictoria para la misma fecha, calendario y alcance.

### PeriodoOperativo

Entidad persistente que representa una semana operativa concreta.

**Atributos relevantes**
- `id_periodo_operativo`.
- `anio_iso`.
- `semana_iso`.
- `fecha_inicio`.
- `fecha_fin`.
- `id_calendario`.
- `estado_periodo`.
- `creado_en`.
- `creado_por`.
- `cerrado_en` nullable.

**Clave de negocio**
- combinación de alcance operativo, `anio_iso` y `semana_iso`.

**Reglas**
- `semana_iso` debe estar entre 1 y el número válido de semanas ISO del año indicado.
- `fecha_inicio` y `fecha_fin` se derivan del año/semana ISO y deben ser consistentes con ellos.
- Un período sólo puede tener un estado vigente.
- Los hechos de planificación y ejecución deben referenciar `id_periodo_operativo`, evitando repetir año/semana como única relación lógica.
- Un período cerrado no vuelve a estado inicial para reutilizarlo en otra semana.

### EstadoPeriodoOperativo

Catálogo de estados del ciclo semanal.

**Estados confirmados**
- `DISPONIBLE_PARA_SIMULACION`.
- `EN_REVISION`.
- `PUBLICADA`.
- `EN_EJECUCION`.
- `CERRADA`.

**Reglas**
- Los códigos de estado son estables y no dependen del texto visible en la interfaz.
- Las acciones permitidas se validan contra el estado vigente.

### HistorialEstadoPeriodo

Hecho persistente de auditoría para cada transición o restauración autorizada.

**Atributos relevantes**
- `id_historial_estado`.
- `id_periodo_operativo`.
- `fecha_hora`.
- `estado_anterior`.
- `estado_nuevo`.
- `tipo_transicion`.
- `motivo`.
- `id_usuario_autorizador`.
- `resultado`.
- referencia de transacción o corrida cuando corresponda.

**Reglas**
- Toda transición efectiva debe registrar el período afectado.
- Una restauración debe identificar la transacción que la originó.
- Un intento bloqueado puede registrarse como evento de auditoría sin modificar el estado.

## Datos derivados y vistas

Las siguientes propiedades deben calcularse a partir de una fecha o período y no requieren persistencia como dato maestro:

- identificador calendario `AAAAMMDD` cuando sólo sea una representación de fecha;
- nombre y número de día de semana;
- indicador sábado/domingo;
- día del mes;
- año calendario;
- año ISO y semana ISO;
- mes;
- último día de mes;
- semana ordinal dentro del mes;
- fecha inicial y final de una semana ISO;
- clasificación de un período como anterior, vigente o futuro respecto de la fecha operativa actual.

Puede existir una vista o dimensión de fechas materializada por rendimiento, pero su contenido debe generarse desde reglas canónicas y nunca competir con ellas como fuente de verdad.

## Funciones puras

### obtener_anio_semana_iso(fecha)
Devuelve `anio_iso` y `semana_iso` bajo una única convención ISO.

### obtener_limites_periodo_iso(anio_iso, semana_iso)
Devuelve lunes inicial y domingo final del período.

### obtener_periodo_operativo(fecha, alcance)
Resuelve el período al que pertenece una fecha y el calendario aplicable.

### determinar_dia_operativo(fecha, alcance)
Evalúa regla recurrente, excepciones autorizadas y vigencia; devuelve si la fecha es operativa y la causa.

### trasladar_fecha_operativa(fecha, direccion, alcance)
Obtiene la fecha operativa siguiente o anterior según las reglas del calendario.

### calcular_dias_operativos(fecha_inicio, fecha_fin, alcance)
Cuenta fechas operativas dentro de un intervalo sin alterar estado.

### clasificar_periodo_respecto_hoy(id_periodo_operativo)
Devuelve si el período es anterior, vigente o futuro utilizando la zona horaria aplicable.

### validar_estado_periodo(id_periodo_operativo, estados_permitidos)
Valida que una operación pueda ejecutarse en el estado actual; no cambia estado.

### obtener_configuracion(clave, alcance, fecha_vigencia)
Devuelve un valor tipado y vigente o un error explícito si la configuración es inexistente, ambigua o inválida.

## Procedimientos o comandos

### crear_periodo_operativo
Crea un período nuevo después de validar año/semana ISO, alcance y ausencia de duplicados.

### cambiar_estado_periodo
Cambia el estado del período de forma transaccional, valida transición, autoridad y precondiciones, y registra historial.

### registrar_excepcion_calendario
Crea o modifica una excepción de calendario con validación de conflictos, vigencia y autorización.

### cambiar_configuracion_operativa
Modifica un parámetro administrable, valida tipo/alcance y conserva trazabilidad.

## Máquina de estados del período

### Transiciones normales

`DISPONIBLE_PARA_SIMULACION → EN_REVISION → PUBLICADA → EN_EJECUCION → CERRADA`

### Restauración técnica

Una operación que cambió estado y posteriormente falla puede restaurar el estado anterior únicamente dentro de un mecanismo explícito de rollback. La restauración no constituye una transición ordinaria de negocio.

### Regla de cierre

`CERRADA` es terminal para el mismo período. El siguiente ciclo semanal se representa mediante otro `PeriodoOperativo`.

Las precondiciones específicas para publicar, iniciar ejecución y cerrar definitivamente se detallan en las fases propietarias de planificación, ejecución y cierre.

## Validaciones

- año y semana ISO válidos;
- unicidad de período por alcance;
- coherencia entre período y límites calculados;
- configuración tipada y vigente;
- zona horaria válida;
- inexistencia de excepciones contradictorias;
- transición de estado permitida;
- autoridad del usuario para cambios sensibles;
- prohibición de usar fecha/hora local no normalizada como autoridad temporal;
- prohibición de usar semana calendario no ISO en componentes que gobiernen el período operativo.

## Permisos

Como mínimo deben existir capacidades separadas para:

- consultar calendario y período;
- seleccionar o preparar un período de planificación;
- administrar excepciones de calendario;
- modificar configuración operativa;
- cambiar estados semanales según la etapa correspondiente;
- ejecutar restauraciones o correcciones controladas.

La asignación concreta de estos permisos se resuelve mediante el dominio de seguridad de Fase 2.

## Dependencias

- **Fase 2**: organización, sucursales, usuarios y permisos para alcance y autoridad.
- **Fase 4 y Fase 5**: reglas que consumen calendario y parámetros para determinar activaciones.
- **Fase 9 y Fase 10**: publicación y ejecución condicionadas por el estado del período.
- **Fase 13 y Fase 14**: traslado, arrastre, cierre y transición entre períodos.
- **Fase 17**: auditoría, transacciones, corridas y rollback.
- **Fase 19**: configuración de fuentes externas e integraciones.

## Errores de dominio

- `PERIODO_INEXISTENTE`.
- `PERIODO_DUPLICADO`.
- `ANIO_SEMANA_ISO_INVALIDOS`.
- `ESTADO_PERIODO_INVALIDO`.
- `TRANSICION_PERIODO_NO_PERMITIDA`.
- `PERIODO_CERRADO`.
- `CONFIGURACION_INEXISTENTE`.
- `CONFIGURACION_AMBIGUA`.
- `VALOR_CONFIGURACION_INVALIDO`.
- `ZONA_HORARIA_INVALIDA`.
- `EXCEPCION_CALENDARIO_CONFLICTIVA`.
- `FECHA_NO_OPERATIVA` cuando la operación exija un día hábil.
- `AUTORIDAD_INSUFICIENTE`.

## Salidas del dominio

El dominio entrega al resto de SGOL:

- período operativo identificado de forma estable;
- límites temporales de la semana;
- año y semana ISO consistentes;
- estado vigente del período;
- condición operativa de cada fecha y causa;
- fecha trasladada según calendario;
- parámetros tipados y vigentes;
- trazabilidad de cambios de configuración, calendario y estado.
