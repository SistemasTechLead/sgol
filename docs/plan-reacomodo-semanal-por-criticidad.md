# Plan: reparación de Semana 43 y generación con reacomodo por criticidad

**Estado: aplicado a las asignaciones futuras del 22 al 25; anteriores conservadas por instrucción expresa del usuario.** Fecha: 21 de octubre de 2026. Zona operativa: `America/Mexico_City`. Véase [informe de aplicación](../outputs/adopcion-futuras-20261021/informe.md) e [implementación](reacomodo-semanal-implementacion.md). Se conservaron 120 IDs futuros, se agregó el precorte dominical y se retiró TAR-0063 sin actividad. Los 122 registros anteriores quedaron intactos; TAR-0179 sigue pendiente con su contrato original, y se reservó su carga al calcular la agenda futura. El texto siguiente conserva el diseño y los criterios originales.

Prioridad de entrega: dejar las asignaciones pendientes del **jueves 22 al domingo 25 de octubre de 2026** dentro del horario base de sus responsables, con precorte, conciliación y alarma en orden. Después, demostrar que el generador encuentra por sí mismo una solución bajo esas mismas restricciones. No se publicarán propuestas con tareas fuera de turno para completar artificialmente el plan.

Este documento amplía el [plan de cierre y criticidad](plan-cierre-automatico-por-carga.md). Sus cambios de horarios, clasificación y adelantos sustituyen las reglas anteriores únicamente cuando se implementen y se apliquen expresamente. No representa cambios ya guardados.

## 1. Alcance y evidencia de partida

Los cuatro cambios solicitados son:

1. Aplicar las correcciones de catálogo y clasificación acordadas.
2. Excluir tareas inactivas de nuevas generaciones; TAR-0063 no debe recibir asignación.
3. Permitir adelantos a tareas flexibles dentro del mismo día y del turno.
4. Reacomodar decisiones anteriores durante la generación cuando una tarea no encuentra un bloque suficiente, respetando la criticidad.

El corte utilizado por las últimas simulaciones encontró 121 asignaciones desde el 22 al 25. Una corresponde a TAR-0063, inactiva, el sábado 24 para Viri. Excluirla deja **120 ocurrencias existentes que se deben conservar**. Agregar el precorte dominical faltante produce **121 ocurrencias objetivo**, pero no son exactamente el mismo conjunto original.

| Día | Corte original | Retiro de TAR-0063 | Precorte nuevo | Objetivo del caso de reparación |
|---|---:|---:|---:|---:|
| Jueves 22 | 33 | 0 | 0 | 33 |
| Viernes 23 | 41 | 0 | 0 | 41 |
| Sábado 24 | 38 | −1 | 0 | 37 |
| Domingo 25 | 9 | 0 | +1 | 10 |
| Total | 121 | −1 | +1 | **121** |

Estos conteos son una referencia verificable del corte, no una autorización para tocar filas obtenidas por conteos o por nombre. Al aplicar se deben volver a identificar las asignaciones por ID, estado, revisión, persona, día y plan.

La distribución conservando responsables cabe con los ajustes. Sin embargo, el generador actual no reproduce necesariamente esa agenda: aun excluyendo TAR-0063 y ensayando libertad de adelanto para flexibles, falla con TAR-0204, cobranza de Mariana, que necesita **180 minutos continuos** para 120 minutos de carga más el margen. Existe, por ejemplo, un bloque factible el jueves de 11:00 a 14:00; el generador pierde ese espacio por decisiones anteriores y no las revisa.

Evidencia: [agenda y tabla de ajustes](../outputs/simulacion-asignaciones-20261022-v4/informe.md), [prueba excluyendo inactivas y permitiendo adelantos](../outputs/simulacion-asignaciones-20261022-v5/informe.md). El adelanto de esa prueba se aproximó en una copia aislada dando libertad de búsqueda desde la entrada; **no es una implementación entregada de la nueva regla**.

## 2. Cambio 1: catálogo, horarios y circuito de caja

Mantener el margen global vigente, inicialmente **50%**. Cada bloque reserva `ceil(carga × 1,50)` minutos completos; la carga usada para capacidad sigue siendo la carga nominal, sin sumar el margen como trabajo adicional.

Las columnas de hora son activaciones nominales. Las importantes pueden retrasarse; las críticas mantienen su ancla exacta. No se crearán ocurrencias dominicales para tareas que no las tienen salvo el precorte indicado expresamente.

| Código y tarea | Criticidad | Carga nominal | Lunes–sábado | Domingo |
|---|---|---:|---|---|
| TAR-0062 Apertura operativa | Conservar Crítica | 10 min | Conservar 09:00 | Conservar 10:00 |
| TAR-0222 Inspección de apertura | **Importante** | 10 min | Conservar 09:00 | Sin nueva ocurrencia |
| TAR-0325 Fondo de caja y registro de apertura | **Importante** | 5 min | Conservar 09:00 | Conservar 10:00 |
| TAR-0004 Precorte | **Importante** | 20 min | **18:00** | **12:52; incluir domingo** |
| TAR-0032 Cierre y conciliación de caja | **Importante** | **35 → 20 min** | Conservar 18:30 | **13:22** |
| TAR-0200 Cierre de tienda y alarma | Conservar Crítica | 5 min | **19:52** | **13:52** |
| TAR-0201 Barrer banqueta y regar árboles | Conservar Flexible | 15 min | **17:30** | Conservar 10:00 |
| TAR-0330 Bajar cortina de sol | **Importante** | 5 min | Conservar 15:00 | Sin nueva ocurrencia |
| TAR-0344 Subir cortinas de sol | **Importante** | 5 min | Conservar 18:00 | Sin nueva ocurrencia |
| TAR-0221 Seguimiento vespertino | **Importante** | 20 min | Conservar 17:00 | Sin nueva ocurrencia |
| TAR-0402 Reporte diario de ventas | **Importante** | 15 min | Conservar 18:30 | Sin nueva ocurrencia |
| TAR-0021 Meta semanal de reposición | Conservar Flexible; fuera de bloques | Conservar 15 min | Conservar plazo y avance | Conservar plazo semanal |
| TAR-0063 Rol semanal de limpieza | **Conservar inactiva** | Sin nueva asignación | Excluir de generación | Excluir de generación |

Para TAR-0004 incluir domingo en los días seleccionados y pasar la frecuencia de seis a siete días, con hora dominical propia. Ajustar su nombre para eliminar la referencia contradictoria «de las 19:00», conservando código e historial.

Secuencias obligatorias por fecha y sucursal, incluso si participan personas diferentes:

- Lunes–sábado: **precorte 18:00–18:30 → conciliación 18:30–19:00 → alarma 19:52–20:00**. El hueco hasta la alarma puede contener otra tarea compatible; no se exige continuidad entre los tres bloques.
- Domingo: **precorte 12:52–13:22 → conciliación 13:22–13:52 → alarma 13:52–14:00**.
- El cierre calculado del predecesor debe ser menor o igual a la activación del sucesor. Una relación no se deduce únicamente del nombre de una tarea; se registra el circuito acordado. Reutilizar las relaciones existentes si pueden representar estas ocurrencias generales; si no, añadir una relación mínima de planificación, sin convertir todas las tareas en procesos ni introducir estados de ejecución.

## 3. Horario razonable: límites obligatorios

Usar **los tramos base vigentes**, con excepciones de horario guardadas expresamente, descansos, disponibilidad, vigencia de empleo y rol. La cobertura selecciona quién cierra; no concede automáticamente más jornada.

La función temporal vigente añade una hora al último tramo de quien cubre el cierre. Esa extensión implícita debe dejar de producir disponibilidad nueva para la política de agenda de esta entrega. Mariana tiene domingo 10:00–14:00 y entre semana 11:00–15:00 / 16:00–20:00. Alarma y margen deben terminar a esas salidas, no a las salidas extendidas calculadas anteriormente.

Conservar los snapshots temporales de asignaciones históricas. Introducir el comportamiento estricto en la versión nueva de planificación, y no reinterpretar silenciosamente entregas, revisiones o cálculos históricos. Otros consumidores que aún usan la extensión deben inventariarse y actualizarse de forma coherente; el generador no debe recibir una jornada y Supervisión otra para la misma política.

Una tarea debe caber entera dentro de un tramo. No se permite dividir bloques, ocupar descansos, sobreponer tareas, trasladarlas automáticamente al día siguiente, reducir el margen para hacerlas caber ni asignarlas a alguien sin rol o especialidad adecuados.

## 4. Cambio 2: inactivas y conservación del trabajo publicado

- Nuevos planes toman únicamente definiciones activas, no eliminadas y elegibles en el catálogo seleccionado.
- Al reutilizar una selección antigua, informar cuáles se excluyen; no fallar con un mensaje genérico por una definición inactiva ni reactivarla para continuar.
- TAR-0063 permanece inactiva y no se genera nuevamente. Su ocurrencia futura existente se incluye como **retiro explícito** en la vista previa de reparación.
- El retiro se realiza con el mecanismo auditable de eliminación/cancelación que corresponda a su ciclo de vida y relaciones. No usar estados como «reasignada» si no hubo reasignación, ni eliminar entregas o evidencia para retirar trabajo.
- Si esa ocurrencia ya tiene entrega, evidencia, avance o revisión al momento de aplicar, detener ese retiro y presentar la diferencia para resolverla; no borrar historia.
- Recalcular repeticiones restantes a partir de ocurrencias conservadas, claves de calendario y fechas válidas. Excluir una tarea no debe duplicar otras ni llenar el hueco inventando una tarea distinta.

## 5. Cambio 3: adelantos de flexibles

Para cada ocurrencia persistir por separado hora nominal y activación efectiva. **No cambiar el catálogo real a las 00:00:** eso fue exclusivamente una técnica del ensayo diagnóstico.

| Criticidad | Antes de nominal | Después de nominal | Regla |
|---|---|---|---|
| Crítica | No | No | Ancla exacta; conflicto si no cabe |
| Importante | No | Sí | Buscar el menor retraso entre agendas completas factibles |
| Flexible | Sí | Sí | Dentro del mismo día y de los tramos permitidos |

La búsqueda inicial conserva la hora preferida si existe un bloque completo desde ella. Si no se encuentra una agenda completa, puede adelantar o retrasar flexibles y revisar colocaciones anteriores. No llevar todas las flexibles al inicio del turno por el solo hecho de existir espacio.

La preferencia de conservar una hora no es una restricción dura para una flexible. Si se necesita mover una flexible que ya cabe para permitir otra obligación, la búsqueda puede hacerlo. Las tareas que no admiten adelanto quedan como importantes o críticas según el catálogo acordado.

Este permiso se usa al preparar y guardar planes y en el reacomodo extraordinario autorizado. No crea un replanificador periódico ni mueve tareas automáticamente al entregar otra, al pasar el tiempo o al abrir una pantalla. Las activadas no vencidas siguen requiriendo selección explícita del supervisor para redistribuirlas durante la operación; las vencidas conservan su vencimiento.

## 6. Cambio 4: búsqueda con reacomodo y criticidad

### 6.1 Separar prioridad operativa de orden interno de búsqueda

Reservar una importante en el primer hueco que parece servir y tratar esa decisión como inamovible puede impedir una tarea larga aunque exista otra agenda válida. **Prioridad no significa que todas las decisiones de las importantes sean definitivas antes de analizar las flexibles.**

Las críticas son restricciones fijas. Las importantes conservan su límite mínimo y prioridad de horario. La búsqueda puede revisar sus personas y retrasos permitidos si eso permite una solución completa mejor. No cambia su criticidad para sortear un fallo del algoritmo.

### 6.2 Modelo de restricciones y preferencias

Construir una sola captura de entradas para vista previa y guardado: ocurrencias, trabajadores elegibles, horarios base y excepciones, disponibilidad, especialidades, límites de capacidad, compromisos conservados, avances semanales, claves existentes, dependencias y versión de reglas.

Variables: persona, activación y cierre de cada ocurrencia. Fechas fijas se conservan; las distribuidas mantienen sus reglas de frecuencia y días candidatos en generación nueva. El caso de reparación conserva días y responsables salvo un cambio de responsable expresamente revisado en la propuesta.

Restricciones duras:

1. Duración completa con margen capturado, tramos laborales, descansos y ausencia de cruces.
2. Anclas críticas, activación mínima de importantes y orden del circuito de caja.
3. Rol, especialidad, vigencia, disponibilidad y cobertura de cierre dentro del turno real.
4. Capacidad nominal disponible, considerando metas y otros compromisos aunque no ocupen bloques ordinarios.
5. Frecuencias, fechas permitidas, claves únicas y conservación de trabajo fuera del alcance.

Entre **soluciones completas factibles**, elegir en este orden:

1. Menor retraso de importantes, con desempates estables.
2. Menos cambios de responsable y día en reparación de trabajo publicado; otros cambios necesitan revisión explícita.
3. Menor desviación de flexibles respecto a su hora preferida, evitando movimientos innecesarios.
4. Balance de carga y criterios de rotación existentes como desempates, sin destruir la factibilidad lograda.

No descartar una flexible obligatoria para mantener una importante unos minutos antes si existe una solución completa que respete las reglas y retrase legítimamente esa importante. Una publicación completa exige todas sus ocurrencias; un resultado parcial se muestra como diagnóstico y no se guarda silenciosamente como completo.

### 6.3 Estrategia de búsqueda

1. Normalizar el calendario de obligaciones y restar las ocurrencias ya conservadas, sin generar de nuevo el pasado.
2. Detectar conflictos evidentes: críticas que se cruzan, tarea mayor que cualquier tramo continuo elegible, falta de responsable y carga total imposible.
3. Reservar críticas y compromisos protegidos. Separar componentes por trabajadores/días compartidos y dependencias; no separar componentes que compitan por las mismas personas o capacidad.
4. Construir una primera agenda usando criticidad, escasez de candidatos y de huecos, duración y orden de dependencias. Identificar desde el principio tareas como TAR-0204 que solo una persona puede hacer y requieren tres horas seguidas.
5. Si una colocación falla, explorar huecos anteriores de flexibles, otros huecos, candidatos elegibles y fechas distribuidas permitidas. Deshacer decisiones previas dentro del componente y volver a intentar. No limitarse a deslizar la última tarea hacia delante.
6. Usar propagación de restricciones y poda: no aceptar una colocación provisional que deje a otra obligación sin ninguna alternativa, si existe otra colocación candidata que no la bloquee.
7. Validar de nuevo la agenda completa después de balancearla. Si el balance rompe un bloque o una dependencia, rechazar ese movimiento o devolverlo a la búsqueda; nunca conservar un resultado inválido.

Caso obligatorio de regresión: el generador debe encontrar una solución para el conjunto semanal acordado sin clasificar TAR-0204 como importante, sin dividir sus 180 minutos y sin ocupar minutos después de la salida de Mariana.

### 6.4 Qué significa «siempre encontrar la manera»

La obligación es **buscar alternativas y reacomodar antes de declarar un conflicto**, no producir un horario imposible. Si dos críticas coinciden para el único trabajador elegible o un bloque no cabe en ningún tramo, ninguna búsqueda puede arreglarlo sin modificar restricciones.

Distinguir los resultados:

- **Agenda completa validada:** se puede presentar para publicación.
- **Inviabilidad demostrada:** explicar las tareas, candidatos y restricciones incompatibles, con sus horas y duraciones.
- **Búsqueda inconclusa:** se alcanzó un límite de recursos sin probar inexistencia de una solución. No llamarlo «sin trabajador disponible» ni «inviable»; conservar el diagnóstico y permitir un intento ampliado sobre las mismas entradas.

La búsqueda exacta se acota a componentes y a un dominio finito de minutos/candidatos. Definir límites medidos de exploración y tiempos antes de entregar, y versionarlos. Agotar un límite no autoriza una agenda parcial ni una afirmación de imposibilidad. No prometer solución instantánea para cualquier cantidad de tareas.

Usar orden estable por tarea/fecha/ocurrencia/persona y presupuesto determinista de exploración. No desempatar con identificadores aleatorios creados durante el cálculo. Las preferencias y sus revisiones forman parte de la huella de la vista previa.

## 7. Orden de implementación: primero esta semana

### Fase A — Fijar el caso real y preparar la reparación

- Capturar por lectura consistente las asignaciones del 22 al 25, catálogo, horarios base, dependencias y capacidad, incluyendo compromisos anteriores todavía abiertos.
- Separar 120 ocurrencias existentes que siguen vigentes, el retiro de TAR-0063 y el alta del precorte dominical. Revalidar conteos porque la operación puede cambiar después del corte.
- No dar por resuelto el pendiente anterior TAR-0179 de Dennis, cuyo plazo alcanza el jueves. Mostrar su estado real y tratarlo como compromiso protegido hasta que se complete o se resuelva mediante el flujo autorizado; no borrarlo para conseguir una prueba favorable.
- Preparar un caso reproducible en copia aislada. La primera prioridad es obtener una propuesta completa para estos responsables y horarios, con diferencias visibles por asignación.

### Fase B — Motor compartido y contrato nuevo

- Implementar el límite de horario base, las nuevas criticidades/horas, adelantos y búsqueda con reacomodo.
- Integrar las mismas reglas en planificación, vista previa, guardado, balanceo, asignaciones ocasionales y ajustes extraordinarios. Revisar procesos para que sus reservas no sean ignoradas ni su contrato anterior reinterpretado.
- Versionar la nueva regla de agenda y capturar política, margen, nominal y activación efectiva por asignación. Conservar el porcentaje y fórmula previos cuando no cambian; una nueva versión no se aplica a históricos por lectura.
- La vista previa y el guardado deben producir la misma agenda. Incluir reglas, horarios, catálogo, obligaciones conservadas y ocurrencias efectivas en la huella; rechazar cambios concurrentes o activaciones que ya hayan pasado.

### Fase C — Probar una regeneración, no solo un acomodo manual

- Reproducir en copia el alcance activo de Semana 43, excluyendo TAR-0063, y generar desde el 22 hasta el domingo.
- Comprobar integridad de las obligaciones y frecuencias, no solo contar tareas: cualquier diferencia de fechas/personas debe quedar explicada. No usar el plan antiguo completo con definiciones inactivas como si fuera una selección vigente.
- Comparar el resultado automático con la agenda factible del caso; documentar cómo resuelve el bloque de cobranza y el circuito dominical de caja.
- Probar la semana completa siguiente como regresión separada, con sus fechas y disponibilidad reales. Que funcione del jueves al domingo no valida por sí solo todos los lunes–miércoles futuros.

### Fase D — Aplicar de forma controlada al trabajo pendiente

- Antes de escribir: respaldo `pg_dump -Fc`, verificación de catálogo y restauración de prueba; vista previa exacta con IDs y huella.
- Mostrar cambios de catálogo y, por separado, modificaciones de asignaciones: responsable, criticidad, carga, nominal, activación y cierre anteriores/propuestos; mostrar también retiro y alta.
- Mantener IDs de las 120 ocurrencias existentes que continúan. No borrar y recrear el plan publicado ni perder relaciones, evidencia, avances o revisiones. Alta únicamente del precorte dominical; retiro únicamente de TAR-0063 si sigue siendo una pendiente sin actividad protegida.
- Aplicar mediante el mecanismo existente de permisos, bloqueo transaccional común, auditoría y comprobación de revisiones. El usuario debe confirmar esa propuesta concreta antes de actualizar las asignaciones operativas; la solicitud actual autoriza crear este plan, no ejecutarlo.
- Modificar únicamente las pendientes futuras incluidas y todavía válidas en el instante de confirmación. Si alguna ya se activó, venció o recibió evidencia, recalcular el alcance bajo las reglas vigentes y mostrar la diferencia, sin aplicar el conjunto antiguo.
- Capturar el contrato nuevo en las asignaciones adoptadas; no cambiar la política global del plan padre de forma que reinterpretaría sus asignaciones anteriores. Conservar la trazabilidad de la adopción parcial de Semana 43.
- Si el conjunto final conserva el corte, verificar las 121 ocurrencias objetivo por identidad: 120 conservadas + 1 nueva; la igualdad del conteo con las 121 originales no prueba preservación.
- Comparar invariantes antes/después. Fuera del conjunto confirmado: igualdad de fechas, responsables, cargas y contratos; conservar historia, entregas, evidencias y avances en todos los casos.

### Fase E — Publicar y comprobar la aplicación activa

- Compilar y ejecutar las pruebas pertinentes; publicar y comprobar `/health/ready` y las rutas de planificación, Supervisión, Mi trabajo y Tablero.
- Validar que las pantallas muestran fechas persistidas y criticidad capturada, sin volver a calcularlas ni extender jornadas al consultar.
- Generar un informe final con conteos, IDs de altas/retiros, agenda por persona/día, conflictos restantes y evidencia de preservación. No declarar terminada la entrega si el generador solo produjo una propuesta parcial o una simulación manual.

## 8. Criterios de aceptación

| Caso | Resultado requerido |
|---|---|
| Pendientes del 22 al 25 | Todas las obligaciones vigentes caben dentro de los horarios base o el sistema explica un impedimento real no resuelto; ninguna se omite para completar la agenda |
| Caso de corte, resueltos los compromisos previos por vía legítima | 33 jueves, 41 viernes, 37 sábado y 10 domingo; 121 ocurrencias, sin TAR-0063 y con precorte dominical |
| Domingo de Mariana | Precorte 12:52–13:22, conciliación 13:22–13:52 y alarma 13:52–14:00; salida 14:00 |
| Jueves de Dennis | Barrido preferido 17:30, precorte 18:00–18:30 y conciliación 18:30–19:00; salida 19:00 |
| Cobranza TAR-0204 | El generador encuentra un bloque continuo de 180 min con persona elegible, sin exigir elevar su criticidad |
| Flexibles | Una que no cabe después puede adelantarse; también se puede mover una colocada si permite una solución completa; nunca cruza descanso ni día |
| Importantes | No comienzan antes de nominal y reciben preferencia frente a flexibles entre soluciones completas |
| Críticas | Conservan activación exacta y margen completo; los choques se informan |
| Regla de caja | El orden se comprueba por ocurrencia y día aun con personas distintas |
| Inactivas | Se excluyen de nuevas generaciones, se informan y no se reactivan automáticamente |
| Metas, procesos y otros planes | No desaparece su carga ni se invaden sus reservas; históricos conservan contrato |
| Inviabilidad real | No se fuerza una salida posterior, un trabajador sin calificación ni un traslado al día siguiente |
| Búsqueda limitada | Inconclusa se distingue de inviable; no se guarda un plan parcial como completo |
| Vista previa y guardado | Misma agenda y mismas entradas; rechazo verificable de una huella obsoleta |
| Repetición de confirmación | No duplica asignaciones, retiros ni cambios de catálogo |
| Historial | Se conservan datos fuera del alcance; cada modificación adoptada queda auditada |

Agregar pruebas con un caso donde el intento codicioso falle y el reacomodo encuentre una solución conocida, y un caso pequeño enumerado exhaustivamente para verificar que no se informa inviabilidad falsa. Medir rendimiento del caso real y de componentes grandes; comparar con el comportamiento anterior sin asumir que el tiempo de un generador sin reacomodo será el del nuevo buscador.

## 9. Archivos y puntos de integración

- `Infrastructure/Persistence/Planning/OperationalScheduling.cs`: colocación, restricciones, captura y versión de agenda.
- `OperationalPlanGenerator.cs`: calendario, búsqueda, alternativas y diagnósticos.
- `OperationalPlanBalancer.cs`: balance subordinado a factibilidad y validación completa.
- `PersonalSchedules.cs` y `OperationalCapacityService.cs`: disponibilidad base, compromisos y capacidad coherentes.
- `PlanningCapacity.cs` y `EfDirectionPreparedPlanService.cs`: vista previa, huella y guardado.
- `OperationalTaskService.*`, `OperationalCapacityService.Scheduling.cs` y servicios de procesos: mismos contratos y preservación de reservas.
- Páginas de Tareas, Planificación y Supervisión: clasificación, diferencias concretas y confirmación del alcance, sin nuevos controles de ejecución para colaboradores.
- `tools/Sgol.TaskChecks/OperationalSchedulingChecks.cs`: casos reales, reacomodo, límites, regresiones y preservación en copia aislada.

La prioridad es entregar la reparación comprobada de esta semana y la reproducción automática de ese caso. No se incluyen optimización permanente durante el día, aprendizaje de cargas, ejecución partida, edición de horarios personales para ocultar conflictos ni nuevas funcionalidades ajenas al problema.
