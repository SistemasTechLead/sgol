# Plan de implementación: criticidad operativa, acomodo horario y cierre por carga

Estado: implementado el 21 de octubre de 2026 para nuevas operaciones. Ver [informe de implementación y validación](criticidad-operativa-implementacion.md). Las asignaciones publicadas conservan su contrato anterior.

Ampliación propuesta, todavía no ejecutada: [reparación de Semana 43 y generación con reacomodo por criticidad](plan-reacomodo-semanal-por-criticidad.md). Incluye adelantos de flexibles, exclusión de inactivas, límites del horario base y las correcciones de caja y cierre acordadas después de esta implementación.

Corte de datos: **21 de octubre de 2026, 15:39:24, America/Mexico_City**. Lectura de PostgreSQL en transacción `REPEATABLE READ READ ONLY`. Los datos de operación pueden cambiar después del corte. No se modificaron catálogo, asignaciones, horarios, evidencia ni servidor.

## 1. Problema que se resuelve y límites

El cierre por carga hace medible el plazo de cada tarea, pero no organiza el trabajo de una persona. Si once tareas se activan juntas a las 09:00, calcular once cierres independientes conserva una concentración imposible. La criticidad debe gobernar la búsqueda de intervalos, además del orden de presentación.

La solución conjunta es: **reservar primero las anclas críticas, colocar importantes y después flexibles en huecos reales del turno, y calcular el cierre desde la activación finalmente asignada**. Las importantes pueden ocupar huecos entre críticas, sin invadirlas. La crítica de la tarde no obliga a esperar todo el día.

Esto garantiza coherencia del calendario bajo las cargas y restricciones registradas; no garantiza que una persona termine a tiempo. Carga estimada, interrupciones, atención al cliente y cumplimiento real siguen pudiendo producir retrasos. El sistema debe mostrar esos retrasos, no borrarlos moviendo cierres vencidos.

El análisis original delimitó este diseño. La implementación autorizada posteriormente aplica a nuevas operaciones bajo una política versionada. No autoriza regenerar Semana 43 ni ajustar las asignaciones publicadas, incluidas las futuras. Los escenarios son contrafactuales de preparación, no propuestas de escritura sobre los registros de hoy.

## 2. Decisiones confirmadas en esta revisión

| Tema | Regla acordada |
|---|---|
| Valores de catálogo | **Crítica, Importante y Flexible**; códigos internos `CRITICA`, `IMPORTANTE`, `FLEXIBLE`. |
| Crítica | La activación configurada es un ancla fija al asignar; otra tarea no puede desplazarla. Un choque entre críticas es un conflicto. |
| Importante | Puede retrasarse para ocupar el primer hueco completo disponible después de las reservas prioritarias. |
| Flexible | Se coloca después de importantes; puede retrasarse ante excepciones operativas. |
| Margen | Se reserva hasta el cierre, incluido el margen inicial del 50%. No iniciar el siguiente bloque al terminar solo la carga nominal. |
| Adelantos | No adelantar respecto a la activación configurada. |
| Descansos y día | Respetar tramos disponibles. No trasladar automáticamente al día siguiente. |
| Reacomodo durante el día | Solo por una excepción operativa gestionada por quien ya tiene permiso para ajustar carga; vista previa y confirmación. No por cada atraso o entrega. |
| Importantes durante una excepción | También pueden retrasarse si siguen pendientes. |
| Activadas no vencidas | Solo incluirlas si el supervisor las selecciona expresamente. No inferir que una activada está sin empezar. |
| Vencidas | Conservar vencimiento y retraso. Reacomodar otras pendientes no concede un plazo nuevo a la vencida. |
| Control del colaborador | No añadir botón «Comenzar», estado de ejecución, estimación restante ni controles de reprogramación para colaboradores. |
| PROGRESO | Mantener plazo semanal; excluir del acomodo de bloques hasta definir si se necesitan sesiones de trabajo. Su carga sigue contando conforme al contrato vigente. |
| Cierre del local | Para nuevas operaciones: 20:00–20:08 de lunes a sábado y 14:00–14:08 el domingo, con carga 5 min y margen 50%. Validar cobertura hasta el cierre calculado. |

La flexibilidad significa permiso para proponer movimientos dentro de estas reglas, no permiso para ignorar carga, especialidad, disponibilidad, cobertura, evidencia o fechas.

## 3. Diagnóstico actualizado y simulaciones

### Fotografía completa

| Conjunto | Cantidad |
|---|---:|
| Asignaciones, Semana 42 y Semana 43 | 243 |
| Con entrega registrada | 94: 67 CONCLUIDA y 27 PENDIENTE_REVISION |
| Sin entrega registrada | 149 |
| Asignaciones por progreso | 1 |
| Asignaciones de cierre del local | 7 |
| Ejecuciones / etapas de procesos | 0 / 0 |
| Registros de catálogo, incluyendo inactivos/eliminados | 281 |

Una consulta complementaria a las 15:40:39 encontró 41 fotografías, 1 adjunto y 2 avances. Son registros, no verificación del contenido de archivos. No usar estos conteos como objetivos inmutables de una migración futura.

### Dennis, miércoles 21: el caso completo

La base conserva **11 tareas a las 09:00 con 195 min de carga**, aunque en la pantalla o en otro momento se hayan observado nueve. Con cierre aislado al 50%, todas cerrarían a más tardar a las 10:00. La carga no cabe en esos 60 minutos.

Su horario es 09:00–13:00 y 15:00–19:00. Tiene otras tres tareas a las 12:00 y a las 15:00: en total, **14 tareas y 240 min de carga**. La reserva al 50%, redondeada por tarea, suma **362 min**. Las once de las 09:00 reservan **294 min**, no 293: se redondea cada ocurrencia, no la suma del grupo.

Supuesto del escenario S1, exclusivamente para experimentar: TAR-0062 crítica, TAR-0023 importante y las otras tareas flexibles. **No es una clasificación aprobada del catálogo.** Desempate por activación configurada y código; no pretende expresar un orden de negocio entre tareas del mismo nivel.

| Código | Tarea abreviada | Nivel supuesto | Carga | Activación simulada | Cierre simulado |
|---|---|---|---:|---|---|
| TAR-0062 | Apertura operativa | Crítica | 10 | 09:00 | 09:15 |
| TAR-0023 | Precios y señalización | Importante | 15 | 09:15 | 09:38 |
| T-0f6fd53e4ec6 | Vidrio interior y aparador | Flexible | 40 | 09:38 | 10:38 |
| TAR-0179 | Calzado exhibido | Flexible | 15 | 10:38 | 11:01 |
| TAR-0185 | Zona de caja | Flexible | 20 | 11:01 | 11:31 |
| TAR-0186 | Tapete | Flexible | 10 | 11:31 | 11:46 |
| TAR-0227 | Almacén planta alta | Flexible | 20 | 11:46 | 12:16 |
| TAR-0315 | Ordenar almacén | Flexible | 10 | 12:16 | 12:31 |
| TAR-0339 | Vidrios y espejos | Flexible | 10 | 12:31 | 12:46 |
| TAR-0345 | Tableo de cajas | Flexible | 30 | 15:00 | 15:45 |
| TAR-0350 | Transferencias | Flexible | 15 | 15:45 | 16:08 |
| TAR-0340 | Vidrios exteriores; origen 12:00 | Flexible | 20 | 16:08 | 16:38 |
| TAR-0317 | Cajas sin punto; origen 15:00 | Flexible | 20 | 16:38 | 17:08 |
| TAR-0330 | Cortina de sol; origen 15:00 | Flexible | 5 | 17:08 | 17:16 |

A las 12:46 quedan 14 minutos antes del descanso; no cabe el siguiente bloque de 45 minutos ni el de 23. Se dejan libres. No se parte la tarea para atravesar el descanso. El calendario completo termina a las 17:16 y respeta el turno, bajo estos supuestos.

La apertura real fue entregada a las 09:29:59; con el cierre hipotético de las 09:15 habría llegado tarde. Esto señala la necesidad de revisar la carga estimada o el margen con evidencia operativa. Ninguna regla de prioridad demuestra que diez minutos de carga sean correctos, ni permite reclasificar retrospectivamente esa entrega.

### Cuatro escenarios sobre el conjunto completo

Se simularon las **242 asignaciones que no son PROGRESO**, incluyendo las ya entregadas para reconstruir cómo se habría preparado el plan. Se conservaron persona y día originales; se aplicaron horarios semanales vigentes, excepciones y cobertura efectiva. Cada escenario tiene 242 resultados, 968 en total.

| Escenario | Supuesto | Activaciones distintas del original | Conflictos |
|---|---|---:|---:|
| S1 | Apertura crítica, precios importante, resto flexible; 50% | 151 | 7 sin hueco |
| S2 | S1 y almacén planta alta también crítica a las 09:00 | 149 | 9: 7 sin hueco y 2 choques de críticas |
| S3 | S1 y vidrios exteriores crítica a las 12:00 | 150 | 7 sin hueco |
| S4 | S1 con margen 0%, solo para comparar | 138 | 4 sin hueco |

En S2, apertura y almacén chocan en dos fechas; no se mueve ninguna crítica para ocultarlo. En S3, la importante sigue a las 09:15, se reserva 12:00–12:30 para la nueva ancla y la flexible de almacén ocupa 12:30–13:00. Esto comprueba que reservar toda la jornada antes de rellenar huecos evita que una flexible invada una crítica posterior.

Los siete conflictos de S1 son: TAR-0032 de Dennis el 19, 22 y 23; TAR-0032 de Mariana el 25; TAR-0402 de Alejandra el 19 y 21; y TAR-0200 de Alejandra el 21. Ejemplo: 18:30 + 35 min × 1.50 = 19:23, pero Dennis sale a las 19:00. Ser flexible solo permite retrasar y, por tanto, no soluciona ese caso. El domingo, conciliación a las 14:30 necesitaría hasta las 15:23, más allá de la cobertura efectiva de Mariana hasta las 15:00.

La simulación es una heurística determinista de huecos, **no una ejecución del generador de producción ni una certificación de factibilidad global**. No cambia destinatarios, ni verifica roles, especialidades, capacidad porcentual, reservas de otros sistemas o precedencias. Las clasificaciones del resto son artificiales; los resultados no indican que esas tareas deban ser flexibles. La implementación deberá integrar todas esas restricciones y revisar la clasificación real antes de publicar.

### Ausencia y capacidad: límite adicional

Hoy Alejandra tiene 17 tareas/250 min y Dennis 14/240 min. Transferir íntegro el día de Dennis a Alejandra produciría **31 tareas, 490 min de carga y 743 min de reserva al 50%**. Su configuración es de 480 min de capacidad. El exceso de carga es al menos diez minutos antes de contar otros compromisos, y los bloques tampoco caben en su jornada. No es una propuesta de transferencia ni una comprobación de elegibilidad; demuestra que mover tareas de responsable sin validar ambos límites falla incluso con prioridad.

## 4. Contrato de cálculo y persistencia

Para una ocurrencia ordinaria nueva:

```text
DuracionReservadaMinutos = ceil(LoadMinutes × (100 + MargenPorcentaje) / 100)
StartsAt = activación efectiva elegida por el planificador
DueAt = StartsAt + DuracionReservadaMinutos
Bloque reservado = [StartsAt, DueAt)
```

Usar aritmética decimal y redondeo al minuto por asignación. Intervalos semiabiertos: terminar a las 09:15 permite empezar otra tarea a las 09:15. No usar horas enteras como unidad intermedia. La duración es continua; si no cabe en un tramo se busca otro tramo completo del mismo día. No sumar descansos a una fórmula oculta de cierre.

La carga consumida sigue siendo `LoadMinutes`; reservar margen no altera el catálogo ni los indicadores de carga. **Ambas validaciones son obligatorias**: carga dentro de capacidad e intervalos sin solapamiento dentro del horario. No inferir que un porcentaje de capacidad reserva determinadas horas de atención al cliente; hoy ese parámetro limita minutos agregados, no define pausas ni citas de atención. Si existen compromisos horarios adicionales deben estar representados como disponibilidad o reserva explícita.

Persistencia propuesta, aditiva y mínima:

- `OperationalTask.OperationalCriticality`, con restricción de valores y participación en `Revision`, edición, historial e importación.
- Snapshot de criticidad en la asignación; no consultar el catálogo para reinterpretar una asignación publicada.
- Activación nominal de la ocurrencia (`NominalStartsAt`) y activación efectiva (`StartsAt`). La nominal incluye ajustes de calendario autorizados al generar la ocurrencia, no solo copiar el reloj del catálogo.
- Referencia a política versionada de cierre/acomodo en plan y asignación. Política inmutable: margen, versión de fórmula, versión de ordenación y autor/fecha. Una referencia nula identifica contrato anterior; no rellenar históricas con 50% ni con Flexible.
- `StartsAt`, `DueAt`, carga y evidencias continúan persistidos. La auditoría de cada reacomodo conserva antes/después, nominal, responsable, nivel, motivo y actor. No duplicar estados de negocio ni agregar una tabla de ejecución.

Margen global inicial 50%; sin porcentajes por tarea. Un cambio crea versión nueva y no altera planes existentes. Validación implementada del editor: porcentaje entero 0–500. La configuración inicial es 50%; cambiarla no modifica las simulaciones históricas ni los contratos publicados. Las versiones anteriores siguen disponibles para reproducir cálculos.

La criticidad es obligatoria para tareas incluidas en un nuevo plan automático. Durante la implementación, el usuario autorizó clasificar TAR-0062 y TAR-0200 como críticas y las otras 276 tareas no eliminadas como flexibles. No se cambió la clasificación de snapshots publicados ni de las tres definiciones eliminadas. Los formatos de importación anteriores pueden dejar nuevas definiciones sin clasificar hasta revisarlas. Mostrar lista exacta de faltantes y bloquear ese nuevo plan, sin asignar Flexible por defecto ni bloquear la operación histórica. No inferir criticidad a partir del nombre de la tarea.

## 5. Planificación de nuevas asignaciones

1. Capturar una sola política y un solo instante de evaluación para todo el lote; expandir ocurrencias con los calendarios existentes. Conservar reglas de días fijos, distribución semanal, fechas mensuales, repetición y distintas personas.
2. Cargar una vez personas, roles, especialidades, capacidad, ausencias, horarios, coberturas, asignaciones relevantes y reservas de procesos. No una consulta por candidato o tarjeta.
3. Separar PROGRESO, que conserva contrato semanal y consumo agregado. Para tareas de bloques, determinar candidatos elegibles y restricciones de cada ocurrencia.
4. Reservar primero todas las críticas, incluidas las de horas posteriores. Probar responsables elegibles dentro de las restricciones actuales; conservar el ancla. No asignar dos intervalos superpuestos a la misma persona, aunque la suma diaria quepa. Si ninguna combinación considerada puede colocarlas, devolver el conflicto.
5. Colocar importantes en el primer hueco completo que respete activación nominal y reservas críticas. Después colocar flexibles. Desempate propuesto: nominal, código y clave de ocurrencia; no agregar otro campo de prioridad sin necesidad.
6. Dentro de cada nivel mantener criterios existentes de especialidad, repetición, distribución y equilibrio cuando existan varias opciones viables. No degradar elegibilidad para mejorar equilibrio. Probar alternativas de persona/intervalo antes de declarar conflicto.
7. Respetar las fechas candidatas que permite el calendario actual. Una vez elegida la fecha de la ocurrencia, el acomodo horario no puede enviarla al siguiente día. No convertir una tarea de fecha fija en distribuida ni duplicarla al retrasarla.
8. Calcular `DueAt` sobre el `StartsAt` finalmente elegido y devolver la agenda completa, sus huecos y conflictos. Si una heurística no encuentra lugar, indicar «no se encontró acomodo con estas restricciones», sin afirmar una imposibilidad matemática que no se haya probado.
9. Al confirmar, revalidar política, catálogo, horarios, cargas, reservas, revisiones y conjunto exacto bajo el bloqueo/transacción existentes. El resultado guardado debe coincidir con la vista previa. Un cambio relevante exige nueva vista previa.

La clave de ocurrencia (`ScheduleKey`) identifica la ocurrencia nominal, no debe cambiar por mover minutos dentro del día. Esto preserva idempotencia y detección de repeticiones reservadas.

El generador actual permite horas ya transcurridas en el primer día de operaciones. Para contratos nuevos no publicar una crítica con ancla pasada como si acabara de activarse; devolver el conflicto. Para una movible nueva, buscar desde el mayor entre nominal y recepción/publicación, sin inventar activación retroactiva. Revalidar al guardar que el primer hueco siga disponible; no incluir el reloj completo en una huella que expire cada segundo.

Una asignación histórica pendiente no proporciona por sí sola una agenda de ejecución: su antiguo `DueAt` puede ser un límite de ocho horas para una carga de diez minutos. **No reservar automáticamente todo ese plazo ni ignorar su trabajo**. Cuando un plan nuevo comparte persona/día con compromisos históricos sin intervalos fiables, exigir revisar la reserva/compatibilidad en la vista previa, conservando fechas y contrato anteriores. No certificar el día como libre solo porque una asignación pertenece a otro plan.

## 6. Reacomodo extraordinario durante la operación

Reutilizar el flujo de ajuste de carga/capacidad y las excepciones de horario existentes. La excepción puede ser una ausencia, cambio de cobertura o redistribución autorizada. Una entrega temprana o un vencimiento por sí solos no reordenan el plan.

1. Quien tiene permiso registra motivo, rango y personas afectadas. La vista previa propone importantes y flexibles futuras pendientes; incluye activadas no vencidas solo cuando el supervisor las selecciona expresamente.
2. El supervisor confirma que las seleccionadas pueden moverse, porque el sistema no conoce si están físicamente en ejecución. Las no seleccionadas conservan sus bloques. No crear un estado nuevo para inferirlo.
3. Excluir entregadas, pendientes de revisión, terminales, vencidas, con evidencia o avance existente y reservas protegidas por su contrato. Los casos con avance/evidencia requieren el recorrido específico ya existente y revisión explícita; no arrastrarlos a un ajuste ordinario de agenda.
4. Mantener anclas críticas y restricciones del destinatario. Se puede proponer otro responsable elegible para una crítica todavía atendible en su bloque original, pero no desplazar su hora ni renovar el plazo. Si no hay cobertura, informar el conflicto.
5. Para importantes/flexibles seleccionadas, buscar hueco desde el mayor entre nominal, activación ya comprometida y momento de recepción. Propuesta conservadora: solo retrasar respecto a lo publicado, sin compactar automáticamente hacia antes ante una liberación.
6. Recalcular el cierre de la movible con su carga y **política capturada**, no con el margen global que exista ese día. Mostrar los compromisos anteriores y nuevos. Si ya venció al confirmar, excluirla y devolver vista previa obsoleta; nunca quitarle el retraso.
7. Preservar ID, plan, ocurrencia, evidencia y referencias en un simple ajuste de agenda. Para una transferencia ordinaria pendiente, extender el flujo actual que ya cambia destinatario sin regenerar asignaciones. Las devoluciones/nuevos intentos siguen su recorrido propio y su trazabilidad.
8. Aplicar solo el conjunto confirmado en una transacción auditada. No es necesario resolver toda la ausencia para poder guardarla: puede registrarse el hecho y conservar un conflicto operativo pendiente. No marcar como resuelto ni publicar una nueva agenda ficticia para lo que no cabe.

Permisos de servidor iguales al alcance de supervisión existente, sin autoasignación ni ampliación de roles. Ningún colaborador cambia su criticidad, activación o cierre. Las activadas seleccionadas deben quedar visibles con el motivo y el nuevo compromiso; la auditoría conserva el original.

No añadir replanificador periódico, cron, temporizador servidor, ajuste al abrir una página ni cascadas por cada entrega. SSE solo comunica cambios ya confirmados; los temporizadores actuales del navegador siguen representando fechas guardadas.

## 7. Progreso, cierre del local y procesos

### Progreso

TAR-0021 mantiene sus 50 unidades, avance 6, carga 15 y cierre semanal guardado. Las nuevas metas semanales también conservan su ventana de calendario y su atribución de carga vigente; quedan fuera del ordenamiento de intervalos. No tratar 15 min como tiempo para lograr 50 unidades, no multiplicar automáticamente por la meta y no introducir sesiones de trabajo en este cambio. Una criticidad de catálogo no cambia por sí sola esa semántica.

### Cierre del local

Decisión confirmada para nuevos planes: TAR-0200 debe activarse a las **20:00 de lunes a sábado** y a las **14:00 el domingo**. Con carga de 5 min y margen 50%, cierra a las **20:08** o **14:08**, respectivamente. El cierre sigue siendo calculado: un cambio de carga o de política para nuevas operaciones puede cambiar esos ocho minutos y exige validar de nuevo la cobertura.

La nueva hora será una modificación explícita de catálogo para nuevas ocurrencias, no un efecto del planificador ni una reescritura de Semana 43. Clasificar su criticidad requiere revisión del catálogo; el escenario de ancla se validará como crítica. No cambiar nombre, carga o asignaciones existentes sin incluirlos en el alcance correspondiente.

El ejercicio independiente con las siete asignaciones actuales, manteniendo destinatarios y cobertura vigente, encuentra espacio para 20:00–20:08 con Mariana los días 19, 20, 22, 23 y 24; para 14:00–14:08 también con Mariana el domingo 25. **El miércoles 21, Alejandra tiene cobertura solo hasta las 20:00 y no cabe 20:00–20:08.** Es un bloqueo para revisar la cobertura futura; no se extiende su turno automáticamente.

Conservar la lógica de elegibilidad/cobertura. Bajo la nueva política, retirar el reemplazo de `DueAt` por 21:00/15:00 en `TemporalEligibilityContext.Evaluate`, `CapacityLedger.CaptureTime` y `PlanningCapacity.Expired`; esas ramas quedan solo para contratos anteriores. No borrar la excepción horaria antes de identificar el contrato de cada asignación.

### Procesos

No hay ejecuciones/etapas actuales, pero sí código y reservas integradas. No ignorarlas para dar espacio a tareas nuevas. Los procesos publicados y sus ejecuciones siguen sus contratos capturados.

Para procesos nuevos bajo la política conjunta, compartir cálculo y agenda tanto en la reserva como en la habilitación efectiva. La activación de una etapa debe satisfacer dependencias, revisión y mínimo horario; la criticidad nunca salta una dependencia. Una crítica dependiente requiere un ancla explícita compatible; si la dependencia llega tarde, conflicto, no desplazamiento silencioso ni activación ficticia. Las movibles esperan el primer hueco permitido después de la dependencia. Capturar política/criticidad al publicar la versión/ejecución; estimación futura y asignación efectiva deben usar la misma política.

Este recorrido necesita validación específica: no declarar universal el nuevo contrato solo porque el generador semanal funcione. Se puede entregar por fases identificando expresamente los procesos aún compatibles con su política anterior, sin cambiarles plazos a mitad de una ejecución.

## 8. Puntos de integración verificados en el proyecto

| Área | Cambio necesario |
|---|---|
| `OperationalTasks.cs` y contratos de Planning | Catálogo, snapshot, nominal, validación y configuración EF aditiva. Conservar datos de compatibilidad `DeadlineHours`. |
| `OperationalTaskService`, historial, editor `/tareas` e importación | Editar/ver criticidad y faltantes; revisión auditable; adaptar formato, selección de columnas, validación y documentación de importación. No solo un selector visual. |
| `OperationalPlanGenerator` | Cambiar evaluación por candidato/día para incluir intervalos. Hoy ordena por especialidad/carga y calcula `StartsAt` desde el catálogo; no basta un `OrderBy` por criticidad. |
| `OperationalPlanBalancer` | Hoy cambia destinatarios después de generar sin cambiar fechas. Toda propuesta de movimiento/intercambio debe volver a comprobar/reconstruir los bloques afectados; no invalidar la agenda con el balanceo final. Versionar la política. |
| `CapacityLedger`, `PersonalSchedules` | Mantener carga agregada y añadir reservas temporales con el horario/cobertura efectivos. La elegibilidad actual comprueba presencia, pero no demuestra ausencia de solapamientos ni que quepa todo el bloque. |
| `PlanningCapacity` | Una sola generación simulada debe alimentar filas, carga diaria y agenda. Hoy hay caminos separados y una simulación especial para mensuales. No mostrar un diagnóstico horario distinto al plan que se guardará. |
| `EfDirectionPreparedPlanService.CreateAsync` | Hoy la huella se verifica condicionalmente. Exigirla en todo guardado bajo política nueva; extender la idempotencia al conjunto confirmado y reutilizar el bloqueo común. |
| `OperationalCapacityService` / `PersonalScheduleService` | Reutilizar vista previa, alcance de roles, motivo, selección de activadas y confirmación. Hoy la transferencia de carga conserva fechas y no busca huecos. |
| `OperationalSupervisionService` | Distinguir ajuste de agenda de devolución/nuevo intento. Una devolución no debe renovar plazos por accidente ni perder política/snapshot. |
| Ocasionales | Vista previa con activación elegida, nivel y cierre de servidor; respetar destinatarios múltiples y sus agendas. No usar un vencimiento enviado por navegador como autoridad. |
| `ProcessPlanningService` / `OperationalProcessService` | Reservas preparadas, etapas en espera, habilitación efectiva, cambios de destinatario y dependencias comparten política y agenda, con compatibilidad explícita. |
| Tablero, Mi trabajo, reportes y barras | Seguir leyendo `StartsAt`/`DueAt` persistidos. Mostrar criticidad sin sustituir colores de estado. Conservar filtros por cierre, conteos, evidencias y semántica de urgencia. |

Mantener el botón previsto **Ajustar Porcentaje Para el Cierre** en `/tareas`, permiso de Dirección, antifalsificación, versión vigente, ejemplo y alcance a operaciones nuevas. Retirar el plazo manual del recorrido automático ordinario; mantener explícito el plazo semanal de PROGRESO y datos necesarios para contratos antiguos.

La nueva vista previa debe explicar por fila: persona, día, tarea, criticidad, activación nominal, activación propuesta, cierre propuesto, carga, reserva, tramo y motivo del movimiento/conflicto. Ejemplo: «Dennis · TAR-0032 · 18:30–19:23 · turno hasta 19:00 · faltan 23 min continuos». No sustituirlo por «sin personal disponible».

## 9. Concurrencia, rendimiento y protección del historial

Una función pura calcula duración; un servicio de agenda determinista, sin acceso a base, coloca intervalos. Los servicios existentes cargan los datos y aplican el resultado. Reutilizar el resultado de la simulación en la propia solicitud; no duplicar generadores por pantalla.

La huella incluye versión de fórmula/acomodo/balanceo, política, revisiones de catálogo, nominales, horarios/excepciones/coberturas, elegibilidad, capacidad, reservas y revisiones/estados de asignaciones afectadas. El conjunto seleccionado de activadas y el motivo forman parte de la petición e idempotencia. Releer bajo bloqueo al confirmar; comprobar también el paso de activación/vencimiento aunque no cambió ninguna fila. Los escritores de horario, ausencia, evidencia y asignación deben coordinarse con ese control; comprobarlo con pruebas concurrentes.

No añadir consultas de política por tarjeta, cálculos de agenda en SSE ni recorridos masivos cuando cambie un parámetro. El trabajo adicional vive en preparar, validar y guardar un plan o ajuste. Ordenar reservas e indexarlas por persona/día evita probar cada minuto de cada candidato. Medir con el límite actual de 10 000 ocurrencias; acotar búsquedas alternativas y devolver un diagnóstico si se agota el límite, sin afirmar que se probó toda combinación.

Migración: solo esquema aditivo y configuración nueva. **Cero cambios** a IDs, personas, activaciones, cierres, carga, revisión, metas, avances o evidencia de asignaciones existentes. Agregar criticidad al catálogo no la propaga a snapshots publicados. Las operaciones añadidas a un plan anterior heredan su contrato; no mezclar silenciosamente fechas antiguas con la nueva política.

Cualquier adopción posterior de pendientes publicados requiere autorización separada, respaldo verificado, lista exacta, simulación vigente, conflictos, auditoría y verificación de invariantes. La autorización de este análisis y las respuestas de diseño no autorizan esa migración.

## 10. Secuencia de implementación y aceptación

1. **Preparar catálogo:** revisar clasificación real de las tareas elegidas para el primer plan; configurar las activaciones de cierre confirmadas y resolver su cobertura futura, revisar cargas sensibles y distinguir restricciones reales de textos en nombres. La clasificación inicial de los 278 registros no eliminados sigue la instrucción explícita posterior del usuario; no se infiere del nombre ni se propaga a asignaciones.
2. **Esquema y compatibilidad:** respaldo `pg_dump -Fc`, `pg_restore --list`, migración aditiva, políticas/snapshots/nominal y lecturas antiguas. Comparar asignaciones y relaciones antes/después con una fotografía tomada inmediatamente antes, no con los conteos de este documento.
3. **Núcleo y vista previa:** función de duración, agenda por intervalos, generador/capacidad/balanceo coherentes y conflictos verificables. Conservar PROGRESO. Integrar ocasionales y reservas de procesos según su contrato.
4. **Guardado:** huella obligatoria, idempotencia, bloqueo, misma agenda que la vista previa y rechazo de obsolescencia por cambios de política, carga, horario, evidencia o paso del tiempo.
5. **Excepciones operativas:** extender ajuste de carga existente con selección explícita, horarios antes/después, confirmación y auditoría. No incorporar ejecución ni replanificación automática.
6. **Interfaz/importación:** criticidad, porcentaje, ayuda de plazos, agenda y motivos concretos; actualizar todos los recorridos de importación/exportación y validación relacionados.
7. **Pruebas en copia aislada:** no sembrar escenarios en la base de piso. Reproducir S1–S4 con sus supuestos, cierre 20:00/14:00 y cobertura insuficiente; después repetir con criticidades reales. Añadir ausencia con carga 490>480, activadas seleccionadas/no seleccionadas, vencimiento entre vista previa y confirmación, descansos, críticas simultáneas, anclas posteriores, domingo, reintentos y procesos dependientes.
8. **Regresión y entrega:** checks pertinentes de tareas, horarios, capacidad, procesos y tablero; validar fórmulas 5/15/20/30/60 min, margen 0/50, medianoche, repetición, idempotencia y conflictos concurrentes. Despliegue en ventana acordada, respaldo reciente, `/health/ready`, acceso, rutas y evidencias reales. Preparar el siguiente plan; no sustituir Semana 43.

Aceptación funcional:

- Con 10 min/50%, una crítica de las 09:00 conserva 09:00–09:15; una importante puede empezar exactamente a las 09:15.
- Ninguna movible empieza antes de su nominal, cruza un descanso, invade un bloque crítico o salta al siguiente día automáticamente.
- Un grupo inviable produce conflictos concretos; no basta con que la carga semanal o diaria sea admisible.
- Vista previa y guardado son reproducibles; balanceo y transferencias no introducen solapamientos.
- Un ajuste extraordinario mueve solo el conjunto autorizado, conserva política y trazabilidad y no elimina vencimientos existentes.
- No existe botón «Comenzar», cron ni reordenamiento por apertura de pantalla/entrega.
- PROGRESO conserva su plazo; procesos no saltan dependencias; cierre del local valida turno y mantiene compatibilidad histórica.
- Historial, evidencias y revisión preservados; lectores consumen fechas guardadas sin cálculos adicionales de planificación.

## 11. Reversión y evidencia reproducible

Antes de generar trabajo nuevo puede deshabilitarse la entrada a esa política manteniendo el esquema. Después de generarlo, preservar fechas y referencias; no restaurar una base completa encima de entregas posteriores. Un binario anterior que vuelve a escribir cierres especiales no es una reversión segura sin compatibilidad. No borrar columnas para retirar la funcionalidad.

Evidencia local, fuera de Git, en `.local/analisis-criticidad/`: `snapshot.sql`, `snapshot.json`, `simular.ps1`, `simulacion.json` e `informe.md` con las 968 filas de los cuatro escenarios y las siete comprobaciones independientes de cierre. El script solo lee el snapshot y escribe resultados locales. La propuesta anterior de este documento se conservó allí como `plan-original-20261020.md`.

SHA-256 del snapshot: `F38C5B4AC684E5C6337E1B2CEF3221C8B3AA6A665C86FFBEF088A58BFAB89CDC`.

La revisión inicial de diseño no ejecutó migraciones, cambios de datos, pruebas de escritura ni reinicios. La implementación posterior y sus comprobaciones están descritas en el informe enlazado al inicio. Las simulaciones verifican mecánica horaria bajo supuestos declarados; la futura implementación necesita pruebas integradas con todas las restricciones reales.
