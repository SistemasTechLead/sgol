# SGOL — Fase 8 · Balanceador y asignación

## Propósito del dominio

Este dominio selecciona, entre los candidatos declarados elegibles para una `InstanciaTrabajo`, el responsable que debe asumirla. Su objetivo es distribuir obligaciones de manera explicable, consistente con la capacidad disponible y con las prioridades de negocio publicadas, sin exceder límites operativos ni depender del orden físico de los datos.

La elegibilidad se recibe de F7. La publicación del plan pertenece a F9. La ejecución de la tarea pertenece a F10.

## Principios obligatorios

- Sólo pueden rankearse candidatos con resultado `ELEGIBLE` vigente de F7.
- Elegibilidad y ranking son responsabilidades distintas: un criterio de preferencia no vuelve elegible a quien no lo era.
- La capacidad utilizada para decidir debe revalidarse inmediatamente antes de confirmar la asignación.
- Una propuesta de asignación es un resultado derivado y no se persiste por defecto.
- Una asignación confirmada sí representa un hecho operativo y debe conservar identidad, responsable, instante, origen y política aplicada.
- El ranking debe ser explicable por componentes; no se utilizará un número opaco que impida reconstruir por qué ganó un candidato.
- Toda prioridad especial debe estar publicada y versionada mediante identidades canónicas.
- El orden físico de filas, nombres visibles o alias textuales no constituyen criterios de desempate válidos.
- La selección automática no puede aplicar porcentajes, pesos o preferencias de respaldo que no estén publicados.
- El balanceo masivo sólo puede ejecutarse en estados del período expresamente habilitados por F3. Nunca debe reescribir el período cuando éste se encuentre en ejecución.
- Una operación de asignación debe ser transaccional: revalidar candidato, revalidar capacidad, registrar el hecho y reservar la capacidad como una sola unidad lógica.
- Una instancia no puede tener dos asignaciones activas simultáneas.

## Mapa del dominio

1. **Instancia pendiente**: obligación creada en F6 que requiere responsable.
2. **Candidatos elegibles**: conjunto calculado por F7.
3. **Política de asignación**: estrategia publicada aplicable a la tarea y versión.
4. **Capacidad restante**: minutos asignables disponibles desde F1 en el mismo contexto temporal.
5. **Prioridades explícitas**: preferencias por puesto, especialidad, empleado u otra dimensión aprobada.
6. **Ranking explicable**: orden de candidatos y componentes que justifican la posición.
7. **Desempate**: criterio publicado que resuelve igualdad; si no existe, el resultado es no resoluble automáticamente.
8. **Propuesta**: candidato ganador aún no comprometido.
9. **Confirmación**: registro atómico del responsable definitivo y consumo de capacidad.
10. **Salida a F9**: instancia con asignación activa o instancia pendiente de intervención.

## Entidades y configuración persistentes

### PoliticaAsignacionTarea

Configuración versionada que determina cómo se selecciona responsable para una definición de tarea.

**Atributos relevantes**
- `id_politica_asignacion`;
- `id_tarea` y versión de definición;
- `modo_asignacion`;
- vigencia;
- estado de publicación;
- indicador de consumo de capacidad;
- referencia a la política de ranking aplicable cuando corresponda.

**Modos conceptuales**
- `AUTOMATICA_BALANCEADA`: rankea múltiples candidatos elegibles.
- `UNICO_ELEGIBLE`: confirma sólo cuando F7 devuelve exactamente un candidato.
- `PRIORIDAD_CONFIGURADA`: aplica una cadena explícita de preferencias.
- `RESPONSABLE_ORIGEN`: consume la identidad contextual resuelta por F7; no vuelve a inferirla.
- `MANUAL`: no selecciona automáticamente.

La ausencia de una política publicada bloquea la asignación automática.

### CriterioRankingAsignacion

Configuración versionada de criterios que pueden ordenar candidatos dentro de una política.

**Atributos relevantes**
- `id_criterio_ranking`;
- `id_politica_asignacion`;
- `tipo_criterio`;
- orden de aplicación;
- dirección o preferencia;
- peso únicamente cuando el negocio haya aprobado un modelo ponderado;
- vigencia y estado.

**Criterios admisibles cuando estén explícitamente publicados**
- capacidad asignable restante;
- carga ya comprometida;
- repetición de la misma tarea durante el período;
- prioridad de puesto;
- prioridad de especialidad;
- prioridad de empleado específico;
- otros criterios de negocio formalmente definidos.

La compatibilidad de puesto, turno, disponibilidad o especialidad obligatoria no se puntúa aquí: ya fue resuelta por F7.

### PrioridadAsignacion

Relación configurada que expresa una preferencia discreta dentro de una política.

**Atributos relevantes**
- `id_prioridad_asignacion`;
- `id_politica_asignacion`;
- `tipo_objetivo`: PUESTO, ESPECIALIDAD o EMPLEADO;
- identificador canónico del objetivo;
- `prioridad`;
- vigencia;
- estado.

Los nombres visibles pueden mostrarse en UX, pero no son llaves de la regla.

### AsignacionTrabajo

Hecho persistente que vincula una instancia con el empleado responsable una vez confirmada la decisión.

**Atributos relevantes**
- `id_asignacion`;
- `id_instancia`;
- `id_empleado`;
- `id_politica_asignacion` y versión aplicada;
- `origen_asignacion`: AUTOMATICA, MANUAL o CONTEXTO_ORIGEN;
- `fecha_hora_asignacion`;
- `estado_asignacion`;
- identidad del usuario o servicio que confirmó;
- clave idempotente de la operación cuando corresponda.

**Reglas**
- Una instancia tiene como máximo una asignación `ACTIVA`.
- La reasignación no sobrescribe silenciosamente el hecho anterior; su tratamiento se gobierna en F13 y su trazabilidad en F17.
- El nombre, puesto o turno visibles del empleado no sustituyen `id_empleado`.
- No se almacenan `minutos_antes` ni `minutos_despues` como verdad primaria si pueden reconstruirse desde la capacidad y las asignaciones confirmadas.

## Objetos derivados; no tablas de hechos por defecto

### RankingCandidatos

Resultado de ordenar candidatos elegibles en un instante.

**Salida mínima por candidato**
- `id_instancia`;
- `id_empleado`;
- posición;
- componentes de ranking utilizados;
- capacidad restante considerada;
- repeticiones o carga consideradas;
- versión de política;
- timestamp de evaluación.

### PropuestaAsignacion

Resultado previo a confirmar:
- candidato ganador;
- motivo explicable;
- versión de la evaluación F7;
- versión de capacidad;
- versión de política;
- estado de propuesta.

La propuesta puede invalidarse si cambia elegibilidad o capacidad antes del commit.

### ColaPendienteAsignacion

Vista de instancias sin asignación activa y su causa actual. No es una tabla duplicada de obligaciones.

## Reglas de ranking

### Capacidad

Cuando la política consuma capacidad, ningún candidato puede confirmarse si la nueva obligación supera el `limite_asignable` vigente expuesto por F1.

La comparación se realiza contra capacidad restante actual, no contra un snapshot antiguo. El balanceo debe considerar las asignaciones confirmadas previamente dentro del mismo período.

### Rotación

Si existe una política de rotación publicada, puede favorecer al candidato con menor repetición de una tarea o menor carga comparable. La ventana de rotación debe pertenecer a configuración de negocio; no se presupone por nombre o posición de la fila.

### Prioridades explícitas

Una cadena de prioridad puede favorecer un puesto, especialidad o empleado sobre otro sólo después de que todos los candidatos de la comparación hayan superado F7.

Si una prioridad contiene referencias inactivas o no resolubles, la política queda inválida para asignación automática.

### Orden de procesamiento de múltiples instancias

El balanceo de un conjunto es sensible al orden porque cada confirmación consume capacidad. Por ello, el orden de procesamiento debe ser una política explícita y versionada.

Puede considerar criticidad, vencimiento, ventana de ejecución, prioridad operativa u otros atributos aprobados. Si no existe política suficiente, el sistema no debe inventar un orden por posición física de datos.

### Desempate

El desempate final debe ser determinista y estar publicado. No se utilizará el primer registro leído como regla implícita.

Si todos los criterios publicados terminan en igualdad y no existe un desempate autorizado, el resultado será `EMPATE_NO_RESUELTO` y requerirá intervención o una política adicional.

## Funciones y contratos

### `resolver_politica_asignacion(id_tarea, version, fecha)`

Obtiene la política publicada aplicable.

### `obtener_candidatos_para_ranking(id_instancia, momento)`

Consume F7 y devuelve sólo candidatos `ELEGIBLE` con los atributos necesarios para F8.

### `revalidar_capacidad_candidato(id_instancia, id_empleado, momento)`

Obtiene capacidad restante y límite asignable vigentes desde F1.

### `calcular_componentes_ranking(id_instancia, id_empleado, politica, momento)`

Devuelve componentes explicables; no modifica estado.

### `rankear_candidatos(id_instancia, momento)`

Ordena candidatos usando exclusivamente criterios publicados y devuelve `RankingCandidatos`.

### `resolver_desempate(ranking, politica)`

Aplica la regla de desempate publicada o devuelve `EMPATE_NO_RESUELTO`.

### `proponer_asignacion(id_instancia, momento)`

Devuelve `PropuestaAsignacion` sin persistir el responsable definitivo.

### `balancear_conjunto(ids_instancia, momento)`

Procesa múltiples instancias según una política explícita de orden y devuelve propuestas o confirma asignaciones cuando el modo transaccional autorizado lo permita.

## Comandos transaccionales

### `confirmar_asignacion(id_instancia, id_empleado, contexto)`

Dentro de una única transacción debe:
1. validar que la instancia siga asignable;
2. revalidar F7 para el empleado;
3. revalidar capacidad y política vigentes;
4. comprobar que no exista otra asignación activa;
5. registrar `AsignacionTrabajo`;
6. hacer visible el consumo de capacidad;
7. confirmar la transacción;
8. emitir `AsignacionConfirmada` después del commit.

Si cualquiera de las validaciones falla, no debe quedar una asignación parcial ni capacidad consumida de forma huérfana.

### `confirmar_asignacion_manual(...)`

Exige autoridad definida en F2 y registra el origen MANUAL. Una asignación manual no puede evadir silenciosamente elegibilidad o capacidad; cualquier excepción formal se gobierna como excepción en F13.

## Eventos

### `AsignacionConfirmada`

Evento post-commit con, al menos:
- `id_asignacion`;
- `id_instancia`;
- `id_empleado`;
- origen;
- timestamp;
- versión de política.

Puede ser consumido por planificación, notificaciones o auditoría sin convertir el evento en la fuente primaria del hecho.

## Estados y resultados

### Resultado de propuesta
- `PROPUESTA`;
- `SIN_CANDIDATOS_ELEGIBLES`;
- `CAPACIDAD_INSUFICIENTE`;
- `POLITICA_NO_PUBLICADA`;
- `EMPATE_NO_RESUELTO`;
- `REQUIERE_ASIGNACION_MANUAL`;
- `PERIODO_NO_ASIGNABLE`.

### Estado de AsignacionTrabajo
- `ACTIVA`;
- `REVOCADA` cuando un proceso autorizado de reasignación cierre su vigencia.

La cola de no asignadas se deriva de instancias sin asignación activa y del resultado actual; no requiere una entidad paralela.

## Validaciones obligatorias

- La instancia existe y no está cerrada ni bloqueada para asignación.
- El período permite la operación solicitada.
- Existe política de asignación publicada para el modo automático.
- El empleado propuesto sigue siendo elegible.
- La capacidad restante cubre la carga y respeta el límite asignable.
- Todas las referencias de prioridad resuelven a IDs canónicos vigentes.
- No existe otra asignación activa para la misma instancia.
- El commit debe impedir sobreasignación concurrente mediante bloqueo o control optimista equivalente.
- Las operaciones repetidas con la misma clave idempotente no crean asignaciones duplicadas.
- Las causas de no asignación deben ser específicas; no se agrupan en un mensaje ambiguo.

## Permisos

- El servicio automático puede proponer o confirmar únicamente dentro del alcance y estados autorizados.
- La asignación manual requiere permiso específico y alcance organizativo válido.
- La modificación de políticas de ranking o prioridad requiere autoridad de configuración.
- La consulta de candidatos y carga debe respetar el alcance de datos de personal definido en F2.

## Dependencias

- **F1**: capacidad asignable, minutos restantes y política de capacidad por puesto.
- **F2**: permisos, alcance y autoridad para asignación manual/configuración.
- **F3**: período operativo, estado de semana y ventanas de calendario.
- **F4**: definición/version de tarea, criticidad y atributos de prioridad configurables.
- **F6**: `InstanciaTrabajo`.
- **F7**: candidatos elegibles y explicación de elegibilidad.
- **F9**: publicación del plan consume la asignación activa; no es responsabilidad de F8.
- **F10**: ejecución consume responsable publicado/activo sin redefinir el ranking.
- **F13**: reasignación, cancelación y excepciones al ciclo normal.
- **F17**: auditoría de propuestas, componentes de ranking y decisiones cuando sea necesario conservarlas.
- **F18**: equivalencias temporales de identificadores, puestos y responsables durante migración.

## Errores de dominio

- `POLITICA_ASIGNACION_NO_PUBLICADA`;
- `INSTANCIA_NO_ASIGNABLE`;
- `PERIODO_NO_ASIGNABLE`;
- `SIN_CANDIDATOS_ELEGIBLES`;
- `CAPACIDAD_INSUFICIENTE`;
- `PRIORIDAD_ASIGNACION_INVALIDA`;
- `EMPATE_NO_RESUELTO`;
- `CANDIDATO_DEJO_DE_SER_ELEGIBLE`;
- `CAPACIDAD_CAMBIO_ANTES_COMMIT`;
- `ASIGNACION_ACTIVA_YA_EXISTE`;
- `CONFLICTO_CONCURRENCIA_ASIGNACION`;
- `ASIGNACION_MANUAL_NO_AUTORIZADA`.
