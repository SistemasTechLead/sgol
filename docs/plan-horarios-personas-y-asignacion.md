# Plan de implementación de horarios personales y asignación de tareas

> Documento histórico. La política vigente exige presencia en la activación y, para recepción tardía, también al recibir. Las referencias siguientes a ventanas flexibles, fraccionamiento y espera de entrada quedaron sustituidas; no describen reglas activas. Véase [simplificación vigente](horarios/simplificacion.md).

Fecha de definición: 15 de octubre de 2026. Zona operativa: `America/Mexico_City`.

Este plan permite implementar horarios semanales, descansos, excepciones y una rotación de cierre en SistemaSGOL. La asignación debe respetar la presencia laboral prevista sin sustituir las reglas actuales de rol, especialización, capacidad, procesos e historial. El margen de cierre sirve exclusivamente para terminar esa tarea después de la salida.

El destinatario es otro chat de Codex trabajando en `C:\Users\siste\Dev\SistemaSGOL`. Este documento contiene las instrucciones de ejecución, las decisiones funcionales y las verificaciones de aceptación. Su creación no ha modificado código, base de datos ni configuración operativa.

## 1 Alcance y reglas que deben conservarse

Implementar la funcionalidad completa en las rutas actuales. Incorporar el horario al editar una persona en `/personas-y-accesos`, la configuración temporal a Tareas y los conflictos a las vistas de planificación existentes. No crear otro sistema paralelo de personas, planes o tareas.

Conservar:

- Asignación por rol, especialización, condición A prueba, empleo y cuenta activos.
- Capacidad operativa y ajustes de Incidencias, sin aumentar minutos ni porcentajes por las horas de presencia.
- Plan de Operaciones Semanal, planes normales, prevención de ocurrencias duplicadas y reservas de Procesos.
- Separación entre General y los procesos con etapas. Una etapa se entrega o decide mediante su servicio especializado.
- Evidencia, autorizaciones, auditoría, idempotencia, control de versiones y protección CSRF.
- Asignaciones, entregas, revisiones, documentos e historial existentes.
- Diseño visual v2, navegación, apariencia móvil guardada y modo oscuro.

Fuera de alcance: asistencia real, reloj checador, nómina, cálculo de horas extra, cambios de horario comercial, optimización completa de agenda minuto a minuto y asignación automática de actividades que no están en el catálogo.

## 2 Reglas operativas acordadas

### 2.1 Horarios base

| Persona | Lunes a sábado | Domingo |
| --- | --- | --- |
| Denisse | 09:00–13:00 y 15:00–19:00 | Descanso |
| Ale | 09:00–14:00 y 16:00–19:00 | Descanso |
| Viri | 09:00–13:30 y 15:30–19:00 | Descanso |
| Mariana | 11:00–15:00 y 16:00–20:00, excepto miércoles que descansa | 10:00–14:00 |
| Director | 10:00–20:00 | No confirmado por el usuario; no inferir |

Director no participa en cierres. Su exclusión debe ser expresa en la regla de cierre, aunque coincida su presencia con la activación. Un horario no concede permisos ni cambia el rol de nadie.

Cada tramo incluye la entrada y excluye la salida: `[entrada, salida)`. Los espacios entre tramos son descanso. A las 13:30 Viri ya está en descanso; a las 15:30 vuelve a ser elegible. Ningún margen de cierre elimina el descanso entre turnos.

### 2.2 Cierre y margen para terminar

| Día | Activación | Responsable previsto | Salida aplicable | Fin del margen de una hora después de salir |
| --- | --- | --- | --- | --- |
| Lunes, martes, jueves, viernes y sábado | 19:50 | Mariana | 20:00 | 21:00 |
| Miércoles | 19:50 | Persona de la rotación | 20:00 | 21:00 |
| Domingo | 13:50 | Mariana | 14:00 | 15:00 |

La persona debe estar trabajando a la hora de activación. El margen posterior de 60 minutos permite terminar el cierre, pero no recibir nuevas tareas fuera del turno, trabajar durante la comida ni aumentar la capacidad diaria. No modificar los horarios de apertura o cierre comercial de la tienda.

El usuario indica que el cierre suele terminar alrededor de las 20:20. No convertir esa hora aproximada en obligación, duración fija ni horario laboral nuevo.

Separar tres conceptos en dominio y presentación: hora de activación, duración/carga estimada y límite para terminar. La interpretación de este plan es margen hasta una hora después de salir, no plazo de una hora desde las 19:50. Para el cierre nuevo, reflejar el límite 21:00 o 15:00 de forma coherente con `DueAt`; no mostrar 20:50 como si representara esa misma regla.

No utilizar el margen para bloquear entregas tardías que hoy puedan registrarse: el vencimiento puede indicar atraso, conservando la lógica actual de entrega y revisión. La presencia se valida al asignar o habilitar trabajo, no como un nuevo bloqueo general al subir evidencia.

### 2.3 Rotación de los miércoles

| Miércoles de referencia | Persona |
| --- | --- |
| 2026-10-14 | Denisse |
| 2026-10-21 | Ale |
| 2026-10-28 | Viri |
| 2026-11-04 | Denisse |
| 2026-11-11 | Ale |
| 2026-11-18 | Viri |

Ciclo: **Denisse → Ale → Viri → Denisse**. Ancla: miércoles 14 de octubre de 2026 con Denisse. Esa referencia no autoriza crear ni corregir una asignación histórica.

En su miércoles, la persona conserva el tramo matutino y extiende únicamente su tramo vespertino hasta las 20:00. Mariana continúa descansando. La extensión es presencia laboral prevista; conserva los límites de capacidad operativa existentes.

Calcular el turno por fecha y semana local desde el ancla, usando semanas completas y módulo normalizado. No contar generaciones de planes, entregas ni asignaciones previas. Previsualizar, borrar, regenerar o reintentar un plan no consume turnos. El cambio de año ISO no reinicia el ciclo.

Si la persona del turno está ausente, inactiva, sin el rol/especialización necesarios o sin capacidad, mostrar el conflicto. No saltarla, ampliar el horario de otra persona ni trasladar el ciclo automáticamente. Una sustitución explícita por fecha debe registrar quién cubre y su presencia válida, conservar el ciclo de las semanas siguientes y aplicar auditoría.

### 2.4 Domingo

Configurar barrer y regar árboles a las 10:00, conservando sus duraciones, requisitos, códigos y demás propiedades actuales. El cierre se incluye a las 13:50 conforme a las aclaraciones posteriores del usuario.

El usuario limitó la operación dominical a barrer, regar árboles y el cierre aclarado después. Revisar si otras tareas del catálogo generan domingos y presentar sus conflictos. El horario personal no elimina por sí mismo esas ocurrencias. No desactivar ni reprogramar en lote tareas ajenas sin identificar su correspondencia y el impacto.

## 3 Inspección inicial obligatoria

Antes de editar, leer las instrucciones aplicables y verificar el estado real del checkout. En la inspección del 15 de octubre este directorio no tenía repositorio Git; no asumir que existe `.git`, no inicializarlo ni cambiar de checkout para resolverlo. Si ahora existe, respetar cambios previos y no hacer reset o limpieza.

Leer:

- `docs/plan-procesos-de-trabajo.md`.
- `docs/design/ESTILO_VISUAL_V2.md` y `docs/design/referencia-renovada.md`.
- `docs/design/tokens.md`, `docs/design/componentes.md` y `docs/design/accesibilidad.md`.
- Las páginas y servicios de la sección siguiente y las instrucciones locales que existan en ese momento.

Realizar lecturas limitadas, sin exponer credenciales. Identificar en la base real o en una exportación verificable:

1. Los `PersonId` y `UserId` correctos de Denisse, Ale, Viri, Mariana y Director; distinguir cuentas de prueba, inactivas y homónimos.
2. Los IDs de las tareas de barrer, regar árboles y cierre, sus procesos, roles, especializaciones, carga, plazo, calendario y revisiones.
3. Si el cierre operativo comprende una tarea o varias. No agrupar tareas ni convertirlas en proceso sin evidencia y autorización.
4. Horas de activación existentes, asignaciones futuras y planes vigentes afectados.
5. SDK, herramientas EF locales, mecanismo de arranque, proceso Web real, Worker si interviene, conexión y estado de migraciones.

No inventar códigos ni seleccionar usuarios por coincidencia parcial para escribir datos. Una ambigüedad de identidad bloquea únicamente esa configuración; continuar con código y pruebas aisladas.

## 4 Mapa de integración actual

Las rutas siguientes fueron comprobadas en el checkout al elaborar el plan; volver a verificar firmas y consumidores antes de modificarlas.

| Área | Archivos relevantes |
| --- | --- |
| Personas y accesos | `src/Sgol.Web/Pages/People/Index.cshtml`, `Index.cshtml.cs`; `Infrastructure/Persistence/Identity/UserProfileService.cs` |
| Persona y disponibilidad diaria | `src/Modules/Organization/Contracts/Person.cs`, `Availability.cs`; `src/Sgol.Web/Pages/People/Details.Availability.cs`; servicios de disponibilidad en persistencia |
| Tareas y calendario | `src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTasks.cs`, que contiene `OperationalTask`, `OperationalAssignment` y `OperationalSchedule` |
| Edición de tareas | `src/Sgol.Web/Pages/Tasks/Index.cshtml`, `Index.cshtml.cs`; seguir los contratos y servicios usados por estos archivos |
| Generación independiente | `src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalPlanGenerator.cs` |
| Capacidad compartida | `src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCapacityService.cs`, que contiene `CapacityLedger` |
| Vista previa y concurrencia | `src/Sgol.Web/Infrastructure/Persistence/Planning/PlanningCapacity.cs`; `EfDirectionPreparedPlanService.cs`; `src/Sgol.Web/Interface/Endpoints/DirectionPreparedPlanApiEndpoints.cs` |
| Ocasionales y supervisión | `OperationalTaskService.Occasions.cs`, `OperationalTaskService.OccasionalAccess.cs`, `OperationalSupervisionService.cs` en persistencia de Planning |
| Procesos | `src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/ProcessPlanningService.cs`, `OperationalProcessService.cs`, `OperationalSequenceRules.cs` y modelos de versiones/etapas |
| Presentación de ejecución | `src/Sgol.Web/Pages/MyWork`, `LiveBoardReader.cs`, `OperationalWorkPlan.cs` y consumidores de fechas en tablero y reportes |
| Diseño y formularios | `src/Sgol.Web/wwwroot/css/components.css`, `task-form.css`, `processes.css`; `wwwroot/js/task-calendar.js`, `task-process.js`, `direction-plans.js` |

Estado funcional relevante:

- `OperationalPlanGenerator` elige responsable al generar, usando la fecha futura de la ocurrencia. No esperar a la hora del reloj para filtrar esa planificación.
- `CapacityLedger.CanFit` comprueba capacidad diaria y cualificaciones, sin turnos intradía.
- El catálogo usa `StartHour` entero de 0 a 23; no representa 19:50. Ampliar modelo, validación y presentación a minutos es parte obligatoria de la feature.
- `DeadlineHours` no basta para expresar todos los límites locales de cierre; resolverlo sin cambiar los plazos de tareas normales ni los contratos de Procesos.
- `DifferentPeople` busca diversidad y puede usar desempate aleatorio. No implementa esta rotación y no debe gobernar los cierres configurados.
- La disponibilidad diaria negativa ya se consulta. Un nuevo horario no puede revertir una ausencia explícita.
- La página Personas actualmente exige Dirección, aunque el servicio de perfiles admite otros administradores. Conservar la autorización efectiva; no ampliar accesos como parte de este trabajo.

## 5 Modelo de datos propuesto

Los nombres siguientes son orientativos. Ajustarlos al estilo real del proyecto conservando estas responsabilidades y restricciones.

### 5.1 Horarios personales

Guardar el horario por `PersonId`, con versión, vigencia desde/hasta, zona operativa, autor y fecha de cambio. Cada versión contiene días y tramos con precisión de minutos. Diferenciar explícitamente:

- Horario no configurado.
- Día de descanso configurado.
- Día laborable con uno o varios tramos.

No representar falta de datos como disponibilidad de 24 horas. Para nuevas asignaciones que aplican la política de horarios, devolver conflicto de horario faltante. No afectar registros históricos ni invalidar sesiones por cambios que no sean de identidad o seguridad.

Validar en servidor tramos completos, entrada anterior a salida, ausencia de solapamientos, límites diarios, vigencias no superpuestas y versiones esperadas. En esta primera versión no se necesitan turnos que crucen medianoche: rechazarlos con un mensaje claro, sin reinterpretarlos.

Guardar excepciones por fecha como sustitución completa del día: descanso/ausencia o lista explícita de tramos, con motivo, revisión y auditoría. Evitar que omitir un campo o esconderlo en la UI borre horarios existentes.

### 5.2 Reglas temporales de tarea

Extender el calendario para incluir minutos y, cuando corresponda, una hora distinta por día. Una solución compatible es añadir `StartMinute` con valor inicial cero y reglas por día que sustituyan la hora base. Otra representación es válida si migra todas las horas previas exactamente, preserva contratos y evita dos fuentes de verdad editables.

Agregar una política explícita de ejecución:

- **Inicio fijo:** exige presencia en la activación y tiempo utilizable suficiente en el mismo tramo; aplica a barrer, regar y cierre.
- **Ventana flexible:** permite elegir tiempo laboral posterior dentro de `[StartsAt, DueAt)`, sin exigir presencia cuando se publica el trabajo. Aplica a tareas que realmente permiten hacerse después, incluidas metas de varios días cuando corresponda.

Para cierre, guardar margen posterior a la salida de 60 minutos y regla de responsable/rotación. Identificar las tareas configuradas por ID y política; nunca usar `Name.Contains("cerrar")` o nombres de personas en el algoritmo.

No activar automáticamente el margen para cualquier tarea ni extender todos los vencimientos. Mantener `LoadMinutes` como carga; no sustituirla por los 60 minutos de tolerancia. Si la carga real no cabe ni con el margen, informar el conflicto sin reducirla.

Definir una migración compatible de políticas: conservar para tareas existentes el significado de sus fechas y modo de trabajo; no clasificar todo como inicio fijo a medianoche. Las tareas sin política confirmada deben poder revisarse en una vista previa antes de activar la validación operativa.

### 5.3 Rotación y cobertura

Guardar una rotación versionada con fecha ancla, participantes ordenados por `PersonId`, día de aplicación y referencia a las tareas de cierre. La función de calendario debe ser pura y determinista.

La extensión vespertina del miércoles se deriva del mismo turno de rotación. Evitar una tabla manual de excepciones semanales que pueda desincronizarse de la persona elegida. Mostrar el horario efectivo derivado en Personas y planificación. Admitir sustitución explícita por fecha sin alterar el ancla.

Si varias tareas integran el mismo cierre, compartir la cobertura de esa fecha y validar la carga agregada; no consumir turnos por cada tarea.

### 5.4 Evidencia histórica de la decisión

Conservar en la asignación o en un registro relacionado las referencias/versiones del horario, excepción, política temporal y rotación usadas, el tramo efectivo y el límite excepcional calculado. Las consultas de historial no deben explicar una asignación antigua mediante el horario vigente hoy.

Agregar relaciones e índices adecuados y restricciones de base de datos para intervalos, unicidad y concurrencia. Mantener eliminación restringida o versiones históricas; no introducir cascadas sobre asignaciones o auditoría.

## 6 Servicio compartido de elegibilidad temporal

Crear una política compartida, con reloj inyectado y resultados estructurados, que reciba persona, tarea/etapa, ocurrencia y contexto. Debe devolver si es elegible, motivo, horario/versiones aplicados y ventana efectiva. Cargar datos por lote en el contexto de planificación, sin consultas por candidato y ocurrencia.

Orden lógico de validación:

1. Cuenta, empleo, rol y especialización válidos, conservando las reglas actuales.
2. Horario con vigencia para la fecha de ejecución, no solo el vigente al crear el plan.
3. Ausencia diaria y excepciones. Una ausencia explícita gana sobre el horario semanal y la cobertura derivada.
4. Cobertura y rotación de cierre, incluida la exclusión de Director.
5. Presencia y tiempo utilizable según inicio fijo o ventana flexible.
6. Capacidad operativa y reparto actual entre los candidatos que siguen siendo elegibles.

Para inicio fijo, exigir `entrada <= activación < salida`. La carga debe caber hasta la salida del tramo o, solo en cierre autorizado, hasta salida + margen. No sumar el tramo vespertino para permitir que una tarea fija atraviese la comida.

Para ventana flexible, intersectar la ventana de la tarea con los tramos laborales y excluir descansos/ausencias. Permitir acumular tiempo entre tramos o días únicamente si la naturaleza de la tarea admite fraccionamiento; representar esa decisión explícitamente y no deducirla del nombre. Mantener coherente la atribución diaria de carga en `Footprint`, las metas y las reservas.

La validación comprueba factibilidad y capacidad, no promete una agenda sin solapamientos minuto a minuto. No asignar horas exactas ficticias ni contar como capacidad disponible tiempo fuera del turno. Mantener los límites diarios actuales y limitar la factibilidad por el tiempo laboral real, sin convertir presencia en un aumento de capacidad.

Los cierres se asignan a la persona de cobertura dentro de las restricciones de rol y especialización. Si una configuración real de rol impide el resultado esperado, señalar el conflicto; no cambiar roles ni permitir un salto silencioso de autorización.

En reasignaciones activas y activaciones tardías de procesos, revisar también el momento real de recepción. Una tarea fija de la mañana no puede entregarse como nueva asignación a alguien que entró horas después solo porque la planificación original empezó antes. Para trabajo flexible se puede conservar una reserva futura, identificada como programada y sin exigir ejecución durante el descanso.

No retrasar activaciones de Procesos, ampliar plazos fijos ni saltar etapas implícitamente. Cuando no exista responsable temporalmente elegible, conservar su estado pendiente y explicar cuándo y por qué puede reintentarse según los mecanismos actuales. Para una persona reservada no disponible, no reemplazarla sin la acción especializada de reasignación.

## 7 Integración y concurrencia

Usar la misma política para generación normal y semanal, selección automática y manual de ocasionales, supervisión y redistribución por capacidad, reservas y activación real de Procesos. Revisar todas las llamadas a `CanFit` y las reconstrucciones de `CapacityLedger` para no perder el nuevo contexto temporal.

No introducir una validación de horario actual dentro de `CanFit` que rompa la planificación futura o interprete todos los inicios de Procesos como citas rígidas. Separar capacidad y elegibilidad temporal, componiéndolas con el contexto apropiado.

Incluir horarios, excepciones, políticas y rotaciones relevantes en los fingerprints de planes normales y de Procesos. Una edición concurrente debe invalidar la previsualización y devolver el conflicto existente de vista obsoleta o uno equivalente documentado. Ordenar las colecciones antes de calcular las huellas.

En cada escritura, volver a validar dentro de la transacción y bajo el mecanismo de bloqueo actual. Extenderlo para serializar cambios de horario/rotación y asignación cuando proceda. Una comprobación de huella fuera de la transacción no basta.

Preservar intención/idempotencia: un reintento no crea otra versión de horario ni una segunda cobertura, etapa o asignación. La nueva precisión de minutos no debe alterar `ScheduleKey` de ocurrencias existentes ni producir duplicados por cambiar de 19:00 a 19:50.

Previsualizar todos los conflictos útiles por tarea, persona y fecha. Inicialmente conservar generación atómica de planes independientes: si una ocurrencia requerida no tiene responsable, no guardar un plan parcial silencioso. Procesos mantienen sus estados pendientes propios.

## 8 Experiencia y diseño

### 8.1 Personas y accesos

Dentro del editor actual, añadir una sección titulada **Horario laboral** después de los datos del perfil y especializaciones. Mostrar zona operativa y fecha de vigencia. Cada día permite indicar Descanso o editar sus tramos mediante campos de hora; permitir añadir/quitar tramos con límites claros.

Mostrar debajo **Excepciones por fecha** y, para quien participa, **Cobertura de cierre**, con próximos miércoles y horario efectivo hasta las 20:00 cuando le corresponda. La rotación compartida se configura una sola vez; las secciones personales la consultan sin crear copias independientes.

Guardar horarios mediante el servicio correspondiente y autorización servidor de Dirección. Si se guarda junto al perfil, aplicar todo de forma atómica sin escrituras parciales de nombre/rol cuando falla el horario. Si se usan formularios separados, usar acciones y revisiones independientes claramente etiquetadas, sin formularios HTML anidados.

Mostrar el impacto sobre asignaciones futuras antes de aplicar un cambio de horario. Conservar las asignaciones; registrar conflictos para su revisión. No reasignar ni cancelar automáticamente. La UI debe indicar que un cambio de horario no corrige por sí solo el trabajo ya programado.

### 8.2 Tareas y planificación

Usar campos de hora con minutos en lugar del entero 0–23. Mantener visible la hora base y añadir cambios por día solo cuando se necesiten. Mostrar el margen de cierre como **Margen para terminar después de la salida**, con ayuda que explique que no permite recibir otras tareas.

Presentar inicio fijo y ventana flexible con etiquetas comprensibles. No llevar términos de EF, fingerprints, IDs o estados internos a los formularios de operación.

En la previsualización, mostrar fecha, tarea, responsable previsto, horario aplicable y conflicto cuando exista. Ejemplos de mensajes: “Mariana entra a las 11:00 y esta tarea empieza a las 09:00”, “Viri está en descanso a las 14:00” y “Ale cubre el cierre de este miércoles, pero está marcada como no disponible”.

En Mi trabajo y detalles, mostrar inicio, vencimiento y margen cuando aplique. Conservar las rutas y acciones especializadas de Procesos. No cambiar la semántica del día de tablero, que hoy usa `DueAt`, como efecto secundario de agregar horarios.

### 8.3 Contrato visual obligatorio

- Reutilizar `encabezado-pagina encabezado-pagina--catalogo`, `encabezado-pagina__contenido`, `encabezado-pagina__descripcion` y `encabezado-pagina__acciones`. Conservar Crear usuario y luego Ver especializaciones, alineados a la izquierda en la cabecera.
- Usar `panel-seccion`, sus cabeceras, `campo`, `campo__control`, ayudas, alertas, botones y tablas existentes. No introducir otro framework, biblioteca de iconos o fuentes remotas.
- Conservar Poppins local, superficies, colores semánticos, bordes, radios, foco y espaciado por tokens de la guía v2. Primario de acento; secundario neutro; peligro solo donde corresponda.
- Acciones nuevas al inicio de su zona, con ancho de contenido y separación suficiente; no estirarlas a toda la fila. Evitar modificar estilos globales para arreglar únicamente este formulario.
- Diseñar con `@container sgol-viewport`, respetando `sgol.appearance.layout`. No basar toda la adaptación solo en el ancho de la ventana ni usar otro nombre de contenedor.
- En estrecho, el editor de días puede ser una lista de fieldsets con entradas/salidas apiladas. Una tabla de consulta existente conserva su semántica y desplazamiento interno; no convertir todas las tablas en tarjetas.
- Evitar overflow horizontal global, texto recortado, controles fuera de pantalla y botones sin espacio. Los scrollers internos deliberados no son overflow de página.
- No usar colores fijos claros que rompan el tema oscuro. Revisar también bordes de inputs, mensajes, selects y horas en ambos temas.
- Cada campo debe tener etiqueta; agrupar con `fieldset/legend`, asociar errores y conservar valores tras fallos. Área interactiva mínima de 44 px, foco visible y orden de teclado natural.
- Si Descanso u otra opción oculta campos, deshabilitarlos además de ocultarlos para que `required` no bloquee. El servidor vuelve a validar y no confía en campos deshabilitados.
- Conservar navegación móvil, espaciado de Cerrar navegación, preferencias de apariencia y formularios actuales ajenos al cambio.

## 9 Migración y activación segura

Separar migración de esquema, configuración de negocio y activación del filtro. No incluir nombres de personas, IDs de una instalación ni modificaciones masivas del catálogo en una migración EF estructural.

1. Capturar un inventario y respaldo recuperable antes de aplicar cambios a la base compartida; usar las herramientas y permisos disponibles. Guardar el procedimiento de recuperación sin exponer secretos.
2. Probar migraciones en una base aislada que represente los datos existentes. Verificar cuentas, personas, planes, asignaciones, evidencia e historial antes/después.
3. Migrar horas previas a minuto cero y conservar calendarios, claves y vencimientos históricos.
4. Preparar una operación explícita, idempotente y auditada para cargar los horarios y la rotación con IDs verificados. Incluir modo de previsualización y comparación de revisiones.
5. Establecer vigencia desde la fecha local de puesta en marcha, salvo decisión expresa distinta. La ancla del 14 de octubre sirve para el cálculo, sin exigir efectos retroactivos.
6. Configurar barrer/regar/cierre con los horarios acordados. Conservar las duraciones reales y verificar la suma de tareas dominicales y cierres. Revisar colisiones con tareas y roles existentes.
7. Simular semanas representativas y listar usuarios sin horario, tareas sin clasificación temporal y domingos incompatibles. Resolver únicamente los datos necesarios, sin bloquear el desarrollo por pendientes de otras cuentas.
8. Activar el filtro para nuevas asignaciones cuando la configuración necesaria esté completa. Un estado explícito de activación puede facilitar esta transición; no dejar una vía oculta para ignorar horarios.
9. Aplicar los cambios locales y reiniciar el proceso correcto solo cuando el usuario del chat ejecutor haya autorizado la implementación/puesta en marcha. El presente encargo fue crear el plan. Preparar código, verificaciones y operaciones revisables antes de cualquier aprobación que falte.

Un horario no configurado debe aparecer como tal. No inventar horario para Director el domingo ni para cuentas de prueba u otras personas. Si ese dato es necesario para asignar tareas, preguntar específicamente por él o dejar ese caso en conflicto; no detener las partes independientes.

Rollback: preferir desactivar la nueva política para nuevas generaciones o restaurar el binario compatible, manteniendo el historial y los datos nuevos. No ejecutar downgrade destructivo de columnas/tablas con información real. Si una reversión de esquema fuera necesaria, ensayarla en la copia y preservar los datos antes de solicitar su aplicación.

## 10 Fases y entregables

| Fase | Trabajo | Criterio de salida |
| --- | --- | --- |
| 0 | Inspección, IDs, dependencias, respaldo y diseño del cambio | Mapa de consumidores y datos reales; ambigüedades acotadas |
| 1 | Modelo, migración, horarios versionados y política temporal pura | Validaciones y pruebas aisladas de calendario/rotación aprobadas |
| 2 | Editor de horarios, excepciones y cobertura con diseño existente | Guardado seguro, errores útiles y revisión visual clara/oscura |
| 3 | Minutos, variantes por día, políticas y margen en tareas | 19:50 y 13:50 se guardan, leen y muestran correctamente |
| 4 | Generación, capacidad, huellas y previsualización de conflictos | Semana normal, miércoles y domingo se simulan con responsables correctos |
| 5 | Ocasionales, supervisión, reservas y activación de Procesos | No quedan rutas de asignación que eludan la política |
| 6 | Configuración inicial, migración probada y puesta en marcha autorizada | Datos reales comprobados; historia preservada; servicio correcto en ejecución |
| 7 | Verificación final y entrega | Evidencia automatizada y visual, limitaciones y recuperación documentadas |

El orden puede ajustarse por dependencias del código. No declarar la feature terminada si solo existen campos de horario y la asignación todavía los ignora, o si únicamente se protege la ruta de planes normales.

## 11 Pruebas de aceptación

Usar reloj controlado y zona `America/Mexico_City`. Cubrir límites exactos, no solo horas intermedias.

| Caso | Resultado esperado |
| --- | --- |
| Barrer fijo a las 09:00 en un día habitual | Mariana y Director no son elegibles por su entrada; los demás siguen sujetos a rol y capacidad |
| Barrer y regar el domingo a las 10:00 | Mariana puede recibirlos; conservar duración y requisitos; comprobar carga agregada |
| Mariana miércoles | No recibe nuevas tareas en ningún tramo de ese día |
| Viri a las 13:29, 13:30, 15:29 y 15:30 | Presencia, descanso, descanso y presencia respectivamente |
| Denisse a las 13:00 y 15:00; Ale a las 14:00 y 16:00 | Descanso y presencia respectivamente |
| Tarea fija que no cabe antes de la comida | Conflicto; no usar el tramo posterior para justificarla |
| Ventana flexible que abre durante la comida | Puede reservarse trabajo posterior dentro de la ventana, sin programar ejecución en descanso |
| Tarea semanal que abre a medianoche | No excluir a todos por esa hora si su política es flexible |
| Cierre habitual a las 19:50 | Mariana elegible; Denisse, Ale y Viri fuera de turno; Director excluido |
| Cierre del 21/10, 28/10 y 04/11 | Ale, Viri y Denisse; extensión vespertina hasta las 20:00 solo para quien corresponde |
| Cierre del domingo a las 13:50 | Mariana elegible; margen hasta las 15:00 |
| Cierre terminado a las 20:20 | La salida a las 20:00 no invalida la asignación ni impide la entrega |
| Cierre con carga que supera el tiempo con margen | Conflicto explicativo; no modificar la carga automáticamente |
| Nueva tarea normal a las 20:20 o domingo a las 14:10 | No usar el margen de cierre para habilitar otra asignación |
| Tarea fija que empieza exactamente al salir | Persona no elegible |
| Persona de rotación ausente o sin capacidad | Conflicto; sin salto automático ni cambio del ciclo siguiente |
| Sustitución explícita de un miércoles | Cobertura y horario válidos, auditoría; siguiente miércoles conserva su turno |
| Simular, reintentar, regenerar o borrar plan | Mismo turno de la misma fecha, sin duplicados |
| Cambio de año, varias vueltas y plan de varias semanas | Rotación continua por fecha local |
| Horario editado después de previsualizar | Escritura rechazada como vista obsoleta; revalidación transaccional |
| Solicitud idempotente repetida | Un solo cambio efectivo y evento conforme al protocolo existente |
| Edición concurrente de horario o excepción | Conflicto de versión; sin pérdida de cambios |
| Rol o especialización incompatible con cierre | Conflicto; no saltar permisos ni cambiar perfiles |
| Asignación manual u ocasional fuera de turno | Misma política que automática, con mensaje útil |
| Activación/reasignación de etapa durante descanso | Respetar reserva, disponibilidad y estados; no cambiar plazos ni completar por la vía genérica |
| Horario modificado con asignaciones futuras | Conflictos visibles; asignaciones e historia conservadas |
| Otra persona sin horario | Estado explícito y conflicto, sin disponibilidad ilimitada |
| Datos y tareas previos a la migración | Mismas fechas, propietarios, documentos y registros históricos |

Añadir pruebas de servidor para entradas inválidas, autorización, cambios parciales y dobles envíos. Verificar que las excepciones de ausencia tengan precedencia y que una edición de calendario no revierta una ausencia existente.

Ampliar los checks existentes de `tools/Sgol.TaskChecks`, `tools/Sgol.UserProfileChecks`, `tools/Sgol.BoardChecks` y `tools/Sgol.RoleAccessChecks` según el cambio. Puede añadirse un conjunto dedicado a horarios y rotación si aporta cobertura de dominio. Revisar los checks de Procesos integrados en el proyecto, sin asumir que existe un proyecto de pruebas separado para cada función.

Ejecutar build y los checks afectados con el SDK de `global.json` y sus herramientas disponibles. Comprobar migraciones pendientes y consistencia del modelo con la herramienta EF local. No cambiar `global.json` para acomodar el PATH sin necesidad demostrada.

## 12 Verificación visual y de ejecución

Comprobar Personas, edición de tarea, previsualización de plan y ejecución afectada con datos representativos:

- Anchos aproximados de 320, 390, 768 y 1280 px; temas claro y oscuro.
- Apariencia móvil guardada también en una ventana de escritorio, y apariencia de escritorio existente.
- Teclado, zoom/texto al 200 %, etiquetas, errores y foco tras guardar/cancelar.
- Días de descanso, dos tramos, horas de media hora, excepción, miércoles de cobertura y lista de conflictos larga.
- Ausencia de overflow global, respetando contenedores internos de tablas.
- Campos ocultos y deshabilitados correctamente; errores de servidor conservan los datos capturados.

Reiniciar el Web real si se modifican binarios y verificar `/health/ready` y los assets servidos. No confundir build o checks con que la instancia abierta esté ejecutando el código nuevo. Si la URL local redirige a LAN, seguir la redirección en la verificación y usar la configuración real. No exponer cookies, contraseñas o cadenas de conexión.

Usar cuentas y entornos aislados para asignaciones de prueba. No crear trabajo de prueba para el personal real ni cambiar su capacidad para conseguir que una prueba pase. Si falta navegador autenticado o prueba en dispositivo, registrar la limitación con precisión y no afirmar que se verificó.

## 13 Entrega requerida al terminar la implementación

Entregar una explicación breve con enlaces a archivos y evidencia que distinga:

1. Qué se implementó y cómo se configura desde la interfaz.
2. Qué datos reales se configuraron, desde qué vigencia y cuáles quedaron pendientes.
3. Resultado de build, checks, migración y simulaciones, con evidencia real.
4. Resultado visual en claro/oscuro y móvil/escritorio, indicando límites de acceso o dispositivo.
5. Impacto sobre asignaciones futuras existentes y acción necesaria para conflictos, sin cambios retroactivos.
6. Estado del servicio en ejecución y procedimiento de recuperación.

Guardar la evidencia técnica junto al plan, por ejemplo en `docs/horarios/verificacion.md`, sin incluir secretos. No publicar, desplegar remotamente, cambiar cuentas o borrar datos ajenos al alcance por el hecho de haber completado el código.

## 14 Instrucción para el chat ejecutor

Leer este plan completo y las guías de diseño indicadas antes de implementar. Reutilizar servicios y componentes, cuestionar inconsistencias con datos reales y conservar la lógica de negocio existente. Avanzar por fases verificables sin pedir de nuevo decisiones ya fijadas aquí. Consultar únicamente los datos de negocio faltantes o las autorizaciones que realmente no existan en la conversación de ejecución, después de preparar las partes independientes y el cambio revisable.

No sustituir el plan por una implementación parcial de formularios. La feature se considera completa cuando horarios, descansos, rotación, minutos, margen exclusivo de cierre, concurrencia, historia y todas las rutas de asignación estén integrados y verificados.

## 15 Ejecución local del 15 de octubre de 2026

Se aplicó la implementación funcional y se activó la política para nuevas asignaciones, conservando las 73 asignaciones anteriores y su historial. La configuración inicial utiliza las identidades y la clasificación adicional confirmadas por el usuario durante la ejecución. Se integraron las rutas normales, semanales, ocasionales, Supervisión, redistribución y Procesos.

La [evidencia de implementación y verificación](horarios/verificacion.md) registra migraciones, respaldo/restauración, huellas, checks, capturas, ejecución y recuperación. La aceptación final conserva pendientes explícitos: incompatibilidad de rol de dos participantes de cierre, revisión de otros domingos y asignaciones futuras anteriores, domingo del Director sin confirmar y comprobaciones visuales al 200 % y en dispositivo físico.

## 16 Sustitución posterior autorizada: simplificación

La instrucción posterior del usuario elimina las políticas temporales por tarea, el fraccionamiento, el margen configurable y la rotación automática. Se conserva la hora base y se añade únicamente **Horario dominical**, nullable, sin duplicar tareas. La cobertura compartida se configura por fecha en **Planificación**, con propuesta por horario personal y extensión del último tramo; el miércoles requiere elección explícita. Se conservan permisos, cualificaciones, disponibilidad, capacidad y recorridos de Procesos.

Esta sección tiene precedencia sobre las reglas incompatibles de las secciones anteriores. El [resultado y evidencia de la simplificación](horarios/simplificacion.md) documenta migración, conservación, todas las rutas verificadas, funcionamiento local y pendientes actuales.
