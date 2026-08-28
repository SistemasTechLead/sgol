# SGOL — Fase 1 · Empleados, puestos, turnos y capacidad

## Propósito del dominio

Este dominio define quién puede participar en la operación, bajo qué puesto y turno se encuentra vigente, qué disponibilidad operativa tiene en cada período y qué porción de esa capacidad puede destinarse a tareas SGOL. Su responsabilidad termina en exponer personal y capacidad válidos; la elegibilidad de una tarea concreta y la selección del responsable pertenecen a fases posteriores.

## Mapa del dominio

El dominio se divide en seis responsabilidades:

1. **Identidad del empleado**: conservar una identidad estable independiente del nombre visible.
2. **Puestos**: administrar el catálogo de puestos operativos y sus propiedades estructurales.
3. **Asignaciones vigentes**: conservar histórico de puesto y turno por empleado.
4. **Disponibilidad operativa**: registrar la fracción de tiempo que un empleado puede dedicar a tareas SGOL por período y por día.
5. **Política de capacidad por puesto**: definir cuánto de la disponibilidad nominal puede asignarse a tareas SGOL según el contexto operativo.
6. **Capacidad calculada**: derivar minutos disponibles, asignados, libres, utilización y estado de capacidad sin duplicar hechos que ya existan en planificación o asignación.

## Entidades definitivas

### Empleado

Entidad persistente con identidad propia.

**Atributos relevantes**
- `id_empleado`: identificador interno estable.
- `codigo_empleado`: clave de negocio estable y única.
- `nombre_completo`: nombre visible.
- `fecha_ingreso`: inicio conocido de la relación laboral.
- `fecha_baja`: fin de vigencia laboral cuando corresponda.
- `estado_empleado`: ACTIVO o INACTIVO.

**Reglas**
- El nombre no puede utilizarse como clave de relación.
- `codigo_empleado` debe ser único y no reutilizable.
- El estado laboral y la disponibilidad operativa son conceptos distintos.
- Un empleado inactivo no puede considerarse disponible en una fecha posterior a su baja.

### Puesto

Catálogo administrado por el negocio.

**Atributos relevantes**
- `id_puesto`: clave estable del catálogo.
- `nombre_puesto`.
- `nivel`.
- `horas_base_semana`.
- `puede_supervisar`.
- `activo`.
- referencia al área organizacional, cuya definición se completa en Fase 2.

**Reglas**
- Las relaciones operativas deben usar `id_puesto`, no el texto del nombre.
- Un puesto inactivo conserva su histórico, pero no admite nuevas asignaciones salvo autorización de migración.
- Las horas base deben provenir de configuración persistente; no deben fijarse como constante dentro de funciones.

### AsignacionPuestoEmpleado

Hecho persistente e histórico que vincula a un empleado con un puesto durante un intervalo de vigencia.

**Atributos relevantes**
- `id_asignacion_puesto`.
- `id_empleado`.
- `id_puesto`.
- `vigente_desde`.
- `vigente_hasta` nullable.
- `es_principal` cuando el modelo futuro permita más de una asignación simultánea.

**Reglas**
- No se permiten dos asignaciones principales solapadas para el mismo empleado.
- El puesto vigente se resuelve por fecha, no por valor copiado en el registro del empleado.
- Todo cambio de puesto debe conservar el histórico anterior.

### Turno

Catálogo de turnos reales de trabajo.

**Atributos relevantes**
- `id_turno`.
- `nombre_turno`.
- `hora_inicio`.
- `hora_fin`.
- `tipo_turno`.
- `activo`.

**Reglas**
- Un turno asignable a una persona debe representar una jornada o ventana real.
- Un valor que signifique “sin restricción de turno” no representa un turno trabajado y debe modelarse como regla de compatibilidad de una tarea, no como asignación de personal.

### AsignacionTurnoEmpleado

Hecho persistente e histórico que vincula a un empleado con un turno durante un intervalo de vigencia.

**Atributos relevantes**
- `id_asignacion_turno`.
- `id_empleado`.
- `id_turno`.
- `vigente_desde`.
- `vigente_hasta` nullable.

**Reglas**
- No se permiten asignaciones de turno principales solapadas para el mismo empleado.
- El turno vigente se determina por fecha.
- Un turno inactivo no puede originar nuevas asignaciones.

### DisponibilidadEmpleadoPeriodo

Hecho persistente que conserva la disponibilidad operativa declarada para un empleado en un período.

**Atributos relevantes**
- `id_disponibilidad_periodo`.
- `id_empleado`.
- referencia al período operativo, que se formaliza en Fase 3.
- `fraccion_carga_tareas`, en rango 0 a 1.
- referencia de origen y momento de carga cuando se requiera trazabilidad de integración.

**Clave de negocio**
- Un único registro por empleado y período operativo.

**Reglas**
- `fraccion_carga_tareas = 0` significa que el empleado no aporta capacidad SGOL en ese período.
- La existencia del registro no sustituye la validación de que el empleado esté laboralmente activo.
- Registros duplicados con la misma clave deben reconciliarse antes de calcular capacidad.

### DisponibilidadEmpleadoDia

Detalle persistente de disponibilidad diaria dentro del período.

**Atributos relevantes**
- `id_disponibilidad_dia`.
- `id_disponibilidad_periodo`.
- `fecha`.
- `fraccion_disponible`, en rango 0 a 1.

**Reglas**
- Un único registro por empleado y fecha.
- `fraccion_disponible = 0` bloquea la disponibilidad para esa fecha.
- La interpretación de fracciones parciales para repartir minutos intradía deberá mantenerse separada de la capacidad semanal hasta que la planificación defina explícitamente ese prorrateo.

### PoliticaCapacidadPuesto

Configuración persistente y versionable de cuánto de la disponibilidad nominal puede asignarse a tareas SGOL para un puesto.

**Atributos relevantes**
- `id_politica_capacidad`.
- `id_puesto`.
- `vigente_desde`.
- `vigente_hasta` nullable.
- `factor_max_tareas_normal`.
- `factor_max_tareas_quincena`.
- `activa`.

**Reglas**
- Los factores deben estar en rango 0 a 1.
- Debe existir como máximo una política vigente por puesto y fecha.
- Si una política está inactiva, el dominio puede exponer la capacidad nominal completa.
- La ausencia de una política requerida es un error de configuración; no se debe aplicar un porcentaje por defecto silencioso.
- La determinación de si una fecha pertenece a ventana de quincena se delega al calendario operativo de Fase 3.

## Datos derivados que no deben convertirse en tablas de hechos

### Capacidad operativa

Debe exponerse como vista o función calculada a partir de empleado, puesto vigente, disponibilidad y trabajo asignado.

**Cálculos mínimos**
- `horas_disponibles = horas_base_semana * fraccion_carga_tareas`.
- `minutos_disponibles = horas_disponibles * 60`.
- `minutos_asignados = suma de minutos de obligaciones asignadas que consumen capacidad en el período`.
- `minutos_libres = minutos_disponibles - minutos_asignados`.
- `utilizacion = minutos_asignados / minutos_disponibles`, con tratamiento explícito de división entre cero.
- `limite_asignable = minutos_disponibles * factor de política vigente` cuando la política esté activa.

`minutos_asignados`, `minutos_libres`, `utilizacion` y el estado de capacidad son valores derivados y no deben duplicarse como hechos independientes si pueden reconstruirse de forma determinista.

### Estado de capacidad

La clasificación actual requerida por el dominio es:
- DISPONIBLE: utilización menor a 70%.
- ADECUADO: utilización desde 70% y menor a 90%.
- SATURADO: utilización igual o mayor a 90%.

Esta clasificación informa el nivel de uso nominal y no sustituye el límite específico de capacidad asignable del puesto.

## Relaciones

- Un **Empleado** puede tener múltiples **AsignacionPuestoEmpleado** a lo largo del tiempo.
- Un **Puesto** puede estar asociado a múltiples empleados y a múltiples versiones de **PoliticaCapacidadPuesto**.
- Un **Empleado** puede tener múltiples **AsignacionTurnoEmpleado** a lo largo del tiempo.
- Un **Turno** puede estar asignado a múltiples empleados.
- Un **Empleado** tiene cero o una **DisponibilidadEmpleadoPeriodo** por período.
- Una **DisponibilidadEmpleadoPeriodo** contiene cero o varios registros de **DisponibilidadEmpleadoDia**.
- La capacidad calculada consume puesto vigente, turno vigente, disponibilidad y política de capacidad.
- Elegibilidad y balanceo consumen este dominio, pero no forman parte de él.

## Funciones del dominio

### `determinar_empleado_activo(id_empleado, fecha)`
Devuelve si el empleado tiene vigencia laboral para la fecha indicada.

### `obtener_puesto_vigente(id_empleado, fecha)`
Devuelve el puesto vigente o un error de integridad si existen asignaciones principales solapadas.

### `obtener_turno_vigente(id_empleado, fecha)`
Devuelve el turno vigente o un error de integridad si existen asignaciones principales solapadas.

### `obtener_disponibilidad_periodo(id_empleado, periodo)`
Devuelve la disponibilidad operativa declarada para el período.

### `obtener_disponibilidad_dia(id_empleado, fecha)`
Devuelve la fracción de disponibilidad de la fecha; ausencia de dato debe distinguirse de disponibilidad explícita igual a cero.

### `calcular_minutos_disponibles(id_empleado, periodo)`
Calcula minutos nominales disponibles usando horas base y fracción de carga.

### `calcular_minutos_asignados(id_empleado, periodo)`
Obtiene la carga ya comprometida desde los hechos de asignación o planificación. Su fuente definitiva se formaliza en Fases 8 y 9.

### `calcular_minutos_libres(id_empleado, periodo)`
Resta minutos asignados de minutos disponibles.

### `calcular_utilizacion_capacidad(id_empleado, periodo)`
Calcula el porcentaje de utilización nominal.

### `clasificar_estado_capacidad(utilizacion)`
Devuelve DISPONIBLE, ADECUADO o SATURADO.

### `resolver_politica_capacidad(id_puesto, fecha)`
Obtiene la política vigente del puesto.

### `calcular_limite_asignable(id_empleado, fecha, periodo)`
Calcula el máximo de minutos que el motor puede comprometer a tareas SGOL de acuerdo con la política vigente y la clasificación de calendario provista por Fase 3.

### `listar_personal_disponible(periodo, fecha)`
Devuelve empleados laboralmente activos con disponibilidad operativa mayor que cero, puesto y turno vigentes y disponibilidad diaria positiva cuando la fecha sea requerida.

## Comandos y transacciones

### `registrar_empleado`
Crea una identidad de empleado con clave de negocio única.

### `actualizar_vigencia_empleado`
Modifica el estado laboral conservando las fechas necesarias para reconstruir vigencia histórica.

### `asignar_puesto_empleado`
Cierra la asignación principal anterior cuando corresponda y crea la nueva asignación de forma atómica.

### `asignar_turno_empleado`
Cierra la asignación de turno anterior cuando corresponda y crea la nueva asignación de forma atómica.

### `registrar_disponibilidad_periodo`
Inserta o actualiza la disponibilidad del período sólo después de validar identidad, período y unicidad.

### `registrar_disponibilidad_dia`
Registra la disponibilidad diaria dentro de una disponibilidad de período existente.

### `publicar_politica_capacidad_puesto`
Cierra la vigencia anterior y activa la nueva política sin perder histórico.

## Validaciones obligatorias

- Código de empleado obligatorio y único.
- Puesto y turno deben existir y estar activos para nuevas asignaciones.
- No se permiten solapamientos de asignaciones principales de puesto o turno.
- Toda disponibilidad debe referenciar a un empleado conocido.
- Las fracciones de carga y disponibilidad deben estar entre 0 y 1.
- La clave empleado-período debe ser única.
- Duplicados idénticos de una fuente pueden deduplicarse de forma idempotente; duplicados con valores de capacidad incompatibles deben bloquear la importación.
- Una referencia de puesto proveniente de una integración debe resolverse a un `id_puesto` oficial; valores no reconocidos son error de mapeo.
- Un valor de “sin restricción de turno” no puede persistirse como turno laboral de un empleado.
- Las horas base deben resolverse desde configuración persistente.
- No se debe aplicar una política de capacidad inexistente mediante un fallback silencioso.

## Estados y transiciones

### Empleado
- ACTIVO → INACTIVO.
- INACTIVO → ACTIVO sólo mediante una nueva vigencia laboral explícita si el negocio permite reingreso.

### Puesto y Turno
- ACTIVO → INACTIVO conserva histórico y bloquea nuevas asignaciones.

### Política de capacidad
- VIGENTE → CERRADA al entrar en vigor una versión posterior.

### Capacidad calculada
DISPONIBLE, ADECUADO y SATURADO son clasificaciones derivadas, no estados persistentes de una entidad.

## Permisos

La modificación de identidad laboral, asignaciones de puesto/turno, disponibilidad y políticas de capacidad requiere autoridad administrativa. La correspondencia entre esa autoridad y usuarios/roles se define en Fase 2. Las funciones de lectura de capacidad pueden ser consumidas por elegibilidad, balanceo, planificación y reportes según sus permisos respectivos.

## Entradas

- Maestro de empleados con identificador estable y vigencia laboral.
- Catálogo oficial de puestos.
- Catálogo oficial de turnos reales.
- Asignaciones históricas de puesto y turno.
- Disponibilidad operativa por período y fecha.
- Políticas de capacidad por puesto.
- Período y clasificación de calendario provenientes de Fase 3.
- Minutos ya comprometidos provenientes de asignación/planificación.

## Salidas

- Personal activo para una fecha o período.
- Puesto vigente por empleado.
- Turno vigente por empleado.
- Disponibilidad semanal y diaria.
- Minutos nominales disponibles.
- Límite máximo asignable.
- Minutos libres y utilización.
- Estado de capacidad.
- Motivos de error de integridad o configuración.

## Errores de dominio

- EMPLEADO_NO_EXISTE.
- CODIGO_EMPLEADO_DUPLICADO.
- EMPLEADO_INACTIVO.
- PUESTO_NO_EXISTE.
- PUESTO_INACTIVO.
- TURNO_NO_EXISTE.
- TURNO_INACTIVO.
- ASIGNACION_PUESTO_SOLAPADA.
- ASIGNACION_TURNO_SOLAPADA.
- DISPONIBILIDAD_FUERA_RANGO.
- DISPONIBILIDAD_DUPLICADA_CONFLICTIVA.
- PUESTO_ORIGEN_SIN_EQUIVALENCIA.
- POLITICA_CAPACIDAD_NO_CONFIGURADA.
- PERIODO_NO_RESUELTO.

## Dependencias con otras fases

- **Fase 2**: área, sucursal, usuarios, roles y permisos administrativos.
- **Fase 3**: período operativo, calendario y determinación de ventana normal/quincena.
- **Fase 4**: las definiciones de tarea referencian puesto y restricciones de turno.
- **Fase 7**: elegibilidad consume personal, puesto, turno y disponibilidad.
- **Fase 8**: balanceo consume límite asignable y capacidad restante.
- **Fase 9**: planificación aporta carga comprometida cuando corresponda.
- **Fase 15**: nómina utiliza información económica sin convertirla en parte del dominio operativo de capacidad.
- **Fase 19**: adaptadores externos cargan empleado/disponibilidad y resuelven identificadores de origen.

## Pendientes diferidos

- Definir en Fase 3 la regla formal de calendario que distingue ventana normal y quincena.
- Confirmar en Fase 8 si las reservas de servicio requieren una política separada o son equivalentes al límite máximo de tareas.
- Definir en Fase 8/9 la fuente canónica de `minutos_asignados` para evitar doble conteo entre propuesta, plan y ejecución.
- Definir en Fase 19 el contrato de importación que transforme identificadores y nombres externos al modelo canónico.
- Resolver en Fase 2 la relación exacta entre puesto, área, sucursal y autoridad.
