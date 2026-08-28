# SGOL — Fase 7 · Elegibilidad

## Propósito del dominio

Este dominio determina, para una `InstanciaTrabajo` concreta, **qué empleados pueden ser candidatos válidos** para asumir la obligación en el contexto y momento evaluados. Su salida es un conjunto explicable de candidatos; no selecciona ganador, no publica el plan y no modifica la ejecución.

## Principios obligatorios

- Elegibilidad y asignación son responsabilidades distintas. F7 filtra candidatos; F8 los ordena y selecciona.
- La evaluación se realiza por `id_instancia`, empleado candidato y momento de evaluación.
- La combinación instancia-empleado es un resultado derivado y no se persiste por defecto.
- Un candidato es elegible sólo cuando cumple **todas las restricciones obligatorias** aplicables.
- La elegibilidad no depende de una puntuación acumulativa. Los criterios obligatorios se expresan como predicados y motivos de aceptación/rechazo.
- La ausencia de una restricción de puesto o turno significa “sin restricción”; valores como `CUALQUIERA` o `TODOS` no se modelan como puestos ni turnos reales.
- La ausencia de datos requeridos no se interpreta como comodín. Produce una evaluación `INDETERMINADA` y bloquea la asignación automática.
- Las reglas deben referenciar identidades canónicas (`id_empleado`, `id_puesto`, `id_turno`, `id_especialidad`), nunca nombres libres como llaves operativas.
- La vigencia laboral, puesto, turno, disponibilidad y capacidad provienen del dominio de personal y capacidad definido en F1.
- La capacidad utilizada para evaluar debe corresponder al mismo instante/contexto que la asignación. F8 debe revalidarla antes de confirmar para evitar decisiones sobre datos obsoletos.
- Una prioridad entre puestos, especialidades o candidatos no convierte a un candidato en elegible; pertenece al ranking de F8.
- La resolución del validador o supervisor no forma parte de F7 y se gobierna en F12.

## Mapa del dominio

1. **Instancia evaluada**: obligación real creada en F6.
2. **Política de elegibilidad**: modo y requisitos aplicables a la versión de la tarea.
3. **Personal candidato**: empleados activos en el alcance operativo relevante.
4. **Restricciones estructurales**: puesto, turno y especialidad.
5. **Restricciones contextuales**: responsable del caso/proceso origen u otra identidad impuesta por el contexto.
6. **Disponibilidad y capacidad**: posibilidad real de asumir la obligación en el momento evaluado.
7. **Resultado explicable**: ELEGIBLE, NO_ELEGIBLE o INDETERMINADA con códigos de motivo.
8. **Salida a F8**: conjunto de candidatos elegibles con atributos necesarios para ranking, sin seleccionar responsable.

## Entidades y catálogos persistentes

### PoliticaElegibilidadTarea

Configuración versionada que indica cómo debe construirse el conjunto de candidatos de una definición de tarea.

**Atributos relevantes**
- `id_politica_elegibilidad`;
- `id_tarea` y versión de definición;
- `modo_elegibilidad`;
- vigencia;
- estado de publicación;
- necesidad de capacidad mínima;
- necesidad de disponibilidad en fecha;
- necesidad de contexto origen.

**Modos admitidos cuando exista configuración explícita**
- `POR_PUESTO`;
- `EMPLEADO_FIJO`;
- `RESPONSABLE_ORIGEN`;
- `POR_ESPECIALIDAD`;
- `PUESTOS_ALTERNATIVOS`.

La política no almacena nombres de personas, puestos o turnos como texto operativo.

### PuestoPermitidoTarea

Relación entre una política de elegibilidad y uno o varios puestos aceptables.

- ausencia de filas: sin restricción de puesto;
- una o varias filas: el candidato debe tener un puesto vigente incluido en el conjunto;
- el orden o preferencia entre puestos no pertenece a esta relación; se define en F8.

### TurnoPermitidoTarea

Relación entre una política y turnos compatibles.

- ausencia de filas: sin restricción de turno;
- una o varias filas: el turno vigente del candidato debe estar incluido.

### Especialidad

Catálogo de competencias o especializaciones operativas que realmente condicionan quién puede ejecutar determinadas tareas.

**Atributos relevantes**
- `id_especialidad`;
- nombre;
- descripción;
- estado.

### EmpleadoEspecialidad

Relación histórica entre empleado y especialidad.

**Atributos relevantes**
- `id_empleado`;
- `id_especialidad`;
- vigencia desde/hasta;
- estado o validación cuando corresponda.

### EspecialidadRequeridaTarea

Relación entre política de elegibilidad y especialidad obligatoria.

La prioridad entre varios especialistas elegibles es responsabilidad de F8.

## Objetos derivados; no tablas por defecto

### EvaluacionElegibilidad

Resultado lógico de evaluar un empleado contra una instancia.

**Salida mínima**
- `id_instancia`;
- `id_empleado`;
- `estado_elegibilidad`;
- lista completa de `codigos_motivo`;
- puesto y turno vigentes usados en la evaluación;
- disponibilidad considerada;
- capacidad restante considerada;
- timestamp/versiones de datos utilizados.

### CandidatoElegible

DTO consumido por F8. Sólo existe cuando `estado_elegibilidad = ELEGIBLE`.

Puede incluir capacidad restante, puesto, turno, especialidades y otros atributos de ranking, pero **no contiene un score de selección calculado por F7**.

## Criterios de elegibilidad

### Vigencia laboral

`determinar_empleado_activo(id_empleado, fecha)` debe ser verdadero.

### Puesto compatible

Si la política define puestos permitidos, el `id_puesto` vigente debe pertenecer al conjunto. Un alias textual no constituye compatibilidad.

### Turno compatible

Si existe restricción de turno, el turno vigente debe pertenecer al conjunto. Sin restricción configurada, cualquier turno laboral válido puede continuar.

### Disponibilidad en fecha

La disponibilidad del empleado para la fecha objetivo debe ser positiva. Un dato ausente se distingue de una disponibilidad explícita igual a cero.

### Capacidad mínima

Cuando la tarea consume capacidad, la capacidad asignable restante debe ser suficiente para la duración o carga requerida. La fuente de capacidad se obtiene de F1 y se revalida en F8 antes de confirmar la asignación.

### Responsable de origen

Cuando `modo_elegibilidad = RESPONSABLE_ORIGEN`, el contexto de la instancia debe resolver un `id_empleado` origen. Ese empleado será el único candidato posible si además supera las validaciones de vigencia y restricciones aplicables.

### Especialidad

Cuando la política requiere especialidad, el empleado debe tener una asignación de especialidad vigente para la fecha evaluada.

### Empleado fijo

Cuando una definición aprobada requiera un empleado específico, la referencia será por `id_empleado` y deberá validar vigencia. El nombre visible no es una regla.

## Estado de la evaluación

- `ELEGIBLE`: todos los criterios obligatorios evaluaron verdadero.
- `NO_ELEGIBLE`: al menos un criterio evaluó falso con datos suficientes.
- `INDETERMINADA`: falta un dato, mapeo, contexto o configuración indispensable para decidir.

No existe transición de estado persistente obligatoria: cada evaluación se recalcula contra el estado vigente. Si se conserva un snapshot por auditoría, pertenece a F17.

## Códigos mínimos de explicación

- `EMPLEADO_INACTIVO`;
- `PUESTO_NO_COMPATIBLE`;
- `PUESTO_SIN_MAPEO`;
- `TURNO_NO_COMPATIBLE`;
- `TURNO_NO_RESUELTO`;
- `SIN_DISPONIBILIDAD_FECHA`;
- `CAPACIDAD_INSUFICIENTE`;
- `RESPONSABLE_ORIGEN_FALTANTE`;
- `RESPONSABLE_ORIGEN_INACTIVO`;
- `ESPECIALIDAD_REQUERIDA_FALTANTE`;
- `POLITICA_ELEGIBILIDAD_NO_PUBLICADA`;
- `DURACION_NO_RESUELTA`;
- `DATOS_CAPACIDAD_NO_CONSISTENTES`.

La salida debe poder contener múltiples códigos simultáneos; un solo “motivo principal” puede presentarse en UX, pero no sustituye la explicación completa.

## Funciones y contratos

### `resolver_politica_elegibilidad(id_tarea, version, fecha)`

Obtiene la política publicada aplicable.

### `listar_personal_base(instancia, momento)`

Obtiene empleados potencialmente disponibles dentro del alcance operativo, sin crear combinaciones persistentes.

### `resolver_restricciones_contextuales(instancia, politica)`

Resuelve empleado origen u otras referencias exigidas por el contexto.

### `evaluar_candidato(id_instancia, id_empleado, momento)`

Evalúa todos los criterios aplicables y devuelve `EvaluacionElegibilidad` con la lista completa de motivos.

### `listar_candidatos_elegibles(id_instancia, momento)`

Devuelve únicamente candidatos con evaluación `ELEGIBLE` y los atributos que F8 necesita para ranking.

### `explicar_elegibilidad(id_instancia, id_empleado, momento)`

Devuelve criterios evaluados, valores utilizados y códigos de motivo.

### `publicar_politica_elegibilidad(...)`

Comando de configuración que valida referencias canónicas y versiona la política antes de permitir su uso productivo.

## Validaciones de configuración

- Toda política publicada referencia una tarea/version válida.
- Todo `id_puesto`, `id_turno`, `id_empleado` o `id_especialidad` referenciado debe existir y ser resoluble.
- No se publican reglas basadas en nombres, alias libres o cadenas concatenadas.
- `RESPONSABLE_ORIGEN` exige contrato de contexto capaz de entregar la identidad origen.
- `POR_ESPECIALIDAD` exige una especialidad definida y relaciones de empleados vigentes.
- Un requisito de capacidad exige duración/carga resoluble.
- Una política con referencias incompatibles o incompletas permanece no publicada.

## Permisos

- La evaluación puede ser invocada por servicios de asignación y planificación.
- La publicación o modificación de políticas de elegibilidad requiere autoridad de configuración.
- La consulta de motivos debe respetar el alcance del usuario sobre empleados y operación.

## Dependencias

- **F1**: empleado activo, puesto/turno vigentes, disponibilidad, capacidad y especialidades de personal cuando se administren allí.
- **F2**: alcance organizativo y permisos.
- **F3**: fecha y período operativo.
- **F4**: definición/version de tarea y requisitos configurables.
- **F6**: instancia de trabajo y contexto origen.
- **F8**: ranking, prioridades, desempate, consumo secuencial de capacidad y selección del responsable.
- **F9**: restricciones de planificación e intradía que requieran granularidad adicional.
- **F12**: resolución de validador/supervisor.
- **F17**: auditoría opcional de snapshots de elegibilidad.
- **F18**: equivalencias de puestos, turnos, tareas y referencias legacy.
- **F19**: contratos de contexto provenientes de sistemas externos.

## Errores de dominio

- `POLITICA_ELEGIBILIDAD_NO_PUBLICADA`;
- `REFERENCIA_ELEGIBILIDAD_INVALIDA`;
- `PUESTO_SIN_MAPEO`;
- `TURNO_SIN_MAPEO`;
- `RESPONSABLE_ORIGEN_NO_RESUELTO`;
- `ESPECIALIDAD_NO_RESUELTA`;
- `CAPACIDAD_NO_RESUELTA`;
- `DURACION_REQUERIDA_NO_RESUELTA`;
- `EVALUACION_ELEGIBILIDAD_INDETERMINADA`.
