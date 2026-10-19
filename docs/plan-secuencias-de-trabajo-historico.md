# Plan de implementación de secuencias de trabajo

> Documento histórico. Fue sustituido por el [plan unificado de procesos](plan-procesos-de-trabajo.md).
> Los estados y pendientes siguientes describen avances anteriores; no son el plan vigente.

Fecha: 13 de octubre de 2026.

Estado: piloto definido y fase 2 verificada en copia local. Integración operativa pendiente; prototipo externo preparado para revisión visual.

Incorporar procesos con etapas que se habilitan por una entrega o aprobación, reutilizando el catálogo, las asignaciones, las evidencias y la planificación de SGOL. Una actividad realizada por la misma persona, con un solo resultado y sin controles intermedios, permanece como una tarea completa. Barrer y trapear es un ejemplo de esa tarea única.

## Criterio para separar etapas

Crear una etapa cuando existe un cambio de responsable, una decisión de revisión o autorización, un resultado intermedio que deba conservarse, o un plazo que deba medirse por separado. Que una actividad tenga varios pasos no exige crear varias tareas. Que una persona haga todos los pasos tampoco impide separarlos si existe alguno de esos controles.

El piloto confirmado es el chequeo semestral de inventario: Piso registra, Subcoordinación contrasta con Sizes & Colors, Administración chequea y Dirección acepta o devuelve. Administración no sella en este caso. La [ficha del piloto](secuencias/piloto-documental.md) describe sus decisiones, calendario, plazos y devoluciones.

## Alcance inicial

- Secuencias lineales con una etapa anterior por etapa.
- Inicio por calendario o por creación de una ocasión. La frecuencia pertenece a la secuencia; las etapas posteriores no se generan además por sus propios calendarios.
- Habilitación por entrega registrada o por aprobación expresa, con inicio mínimo opcional.
- Vencimiento fijo o plazo contado desde la habilitación efectiva.
- Un responsable por etapa y ejecución; el rol y la especialización provienen de la tarea seleccionada.
- Evidencia del mismo trámite con versiones identificables y acceso limitado a los participantes autorizados.
- Devolución desde la etapa activa hacia una etapa anterior, con motivo y nuevos intentos, sin borrar las entregas anteriores.
- Cierre satisfactorio de la secuencia solo cuando la última etapa cumple su condición de cierre.

Las bifurcaciones, las dependencias entre procesos independientes, varias predecesoras simultáneas, la edición de PDF y las firmas electrónicas quedan para un alcance posterior. Registrar un documento sellado o una autorización en SGOL no equivale a implementar una firma electrónica.

## Reglas de funcionamiento

### Habilitación y fechas

La programación indica cuándo se prevé el trabajo. La dependencia indica qué evento permite avanzar. La habilitación efectiva registra cuándo se cumplieron ambas condiciones. El vencimiento establece el límite de cumplimiento.

Una etapa puede empezar cuando llegó su inicio mínimo, su dependencia se cumplió y su asignación sigue siendo válida. El servidor verifica estas condiciones al registrar avances, capturar evidencia, entregar o decidir; el color de la tarjeta no concede permiso.

Si la dependencia se cumple antes del inicio mínimo, se registra el evento, pero la etapa sigue programada. Si se cumple después, la habilitación ocurre cuando se registra ese evento. El plazo relativo se cuenta desde la habilitación efectiva, no desde la fecha prevista ni desde una visita a la pantalla. Los inicios diferidos deben materializarse mediante un procesamiento programado e idempotente; una lectura puede reconciliar un estado vencido sin alterar la fecha de habilitación que correspondía.

El vencimiento fijo no se mueve por un retraso anterior. Una etapa cuyo límite pasó antes de habilitarse queda bloqueada y fuera de plazo, con la causa identificada. Debe definirse su tratamiento sin generar un intervalo inválido donde el inicio sea posterior al vencimiento. El modelo actual exige `DueAt > StartsAt`; por ello, no se puede resolver el caso sobrescribiendo esas dos fechas.

### Entrega y aprobación

Una entrega del responsable corresponde a `CompletionKind = ENTREGA`. Un cierre de Supervisión sin entrega no debe disparar una regla de entrega. Para una regla de aprobación se exige la decisión favorable registrada, con los requisitos documentales específicos del proceso.

Una tarea configurada como etapa de revisión usa una acción expresa de aprobar o devolver. Esa decisión puede reutilizar el servicio y las reglas de revisión existentes donde sean compatibles, pero no debe exigir una segunda aprobación equivalente. La aceptación de la entrega anterior y el resultado de la etapa revisora deben guardarse de manera atómica y con su autor real.

No se debe permitir que la facultad general de Dirección para cerrar tareas salte inadvertidamente etapas de un proceso configurado. Los cierres administrativos y las cancelaciones deben tener un tratamiento explícito y no simular una entrega o autorización documental.

### Identidad y repetición

Cada ejecución tiene un identificador propio. La entrega de una etapa habilita solamente la etapa siguiente de esa ejecución y ronda de trabajo. Un intento sustituido por devolución o reasignación no puede habilitar etapas nuevas.

Las reglas se versionan al iniciar una ejecución. Cambiar el catálogo o editar una secuencia afecta ejecuciones futuras; no modifica silenciosamente las que ya están en curso.

### Responsables y permisos

Dirección configura la ruta. El responsable entrega o decide dentro de su etapa; no elige libremente otro rol ni asigna trabajo mediante un envío manual.

La participación en una secuencia no amplía la jerarquía general de Supervisión. Se añade permiso contextual para consultar el documento de entrada y actuar en la etapa asignada. Ese permiso no concede acceso a otras tareas, archivos o trámites del responsable anterior. También deben protegerse las descargas por URL directa.

### Devoluciones

La etapa activa puede devolver a una anterior con un motivo obligatorio. Se conserva el historial y se abre un nuevo intento en la etapa de destino; las etapas posteriores de la ruta deberán ejecutarse otra vez sobre el resultado corregido. Las anteriores al destino conservan su validez.

La devolución deja sin efecto la aprobación dependiente del documento devuelto, pero no borra sus registros. El documento corregido crea otra versión. Se debe mostrar qué versión recibió, revisó, selló o autorizó cada participante.

La devolución solo se permite mientras el proceso está en curso y desde su etapa activa. Una revisión tardía de un intento anterior no revierte automáticamente etapas ya avanzadas. La reapertura de una secuencia concluida requiere un alcance posterior o una nueva ejecución vinculada.

## Fase 1 Definir los procesos del Excel

1. Clasificar las filas como tarea completa, etapa de proceso o caso pendiente de definición.
2. Elegir un trámite documental real como piloto y registrar un ejemplo de devolución.
3. Describir para cada etapa el resultado esperado, el rol, la evidencia de entrada y salida, la condición de avance, el plazo y el destino de devolución.
4. Resolver si la revisión de Subcoordinación corresponde a la revisión actual de Supervisión o a una actividad documental distinta.
5. Definir para cada plazo si cuenta horas transcurridas o tiempo laboral. Para la primera versión se propone conservar horas transcurridas de SGOL; los turnos de texto libre no sirven como calendario laboral.

Entregable: ficha del piloto y matriz de etapas, con reglas suficientes para configurar y probar el proceso. Los comentarios del Excel son evidencia para modelar; no se convierten automáticamente en reglas ejecutables. Actualizar la guía de llenado y el libro solo después de estabilizar los campos.

Criterio de aceptación: cada espera, avance, devolución y cierre puede explicarse con un ejemplo; no quedan términos ambiguos como «terminar» o «mandar».

## Fase 2 Diseñar datos y compatibilidad

Separar cuatro conceptos: definición versionada de secuencia, definición de etapa, ejecución concreta y etapa de ejecución con sus intentos. Cada intento se vincula a una asignación operativa existente; la etapa identifica cuál es el intento vigente.

Guardar condición y evento de liberación, inicio previsto, inicio mínimo, dependencia satisfecha, habilitación efectiva, regla de vencimiento, fechas efectivas, ronda e identificadores de la ejecución y sus documentos. Las fechas previstas no deben presentarse como fechas efectivas ni alimentar incumplimientos como si ya hubiera empezado el trabajo.

Mantener las asignaciones independientes y la separación física actual de entregas y revisiones. Elegir mediante un mapa de dependencias qué fechas conservar en `operational_assignment` y cuáles ubicar en la etapa de ejecución; resolver de manera explícita las fechas aún desconocidas y la restricción de rango existente.

Añadir vínculos y restricciones para impedir referencias entre ejecuciones distintas, posiciones repetidas, múltiples intentos vigentes y dobles habilitaciones. Las restricciones deben permitir inserciones y devoluciones atómicas.

Entregable: diagrama, diccionario de campos, transiciones, diseño de restricciones y SQL de migración revisable. Incluir respaldo y prueba de recuperación. Las asignaciones históricas no reciben dependencias inventadas.

Criterio de aceptación: el esquema representa plazos relativos pendientes, habilitaciones tardías, devoluciones y tareas independientes sin alterar su historia.

## Fase 3 Integrar generación y capacidad

Modificar `OperationalPlanGenerator`, `PlanningCapacity` y los servicios de ocasiones para generar una ejecución completa a partir de su entrada. Identificar las repeticiones por secuencia, fecha y posición; evitar que las etapas se generen de nuevo como tareas independientes por una selección accidental del plan.

Preparar responsables y carga prevista de todas las etapas usando el reparto actual por rol, especialización y capacidad. Las etapas bloqueadas deben reservar carga estimada; liberarlas no agrega esa misma carga por segunda vez. La planificación debe mostrar que sus fechas todavía son estimaciones.

Al habilitar, reconciliar la reserva con las fechas efectivas y revalidar la elegibilidad del responsable y la capacidad. Si no puede atenderla, intentar el reparto permitido por rol; si no existe una opción válida, registrar «Pendiente de asignación» y avisar a Dirección. La entrega anterior se conserva y el proceso no desaparece ni queda falsamente disponible.

Integrar las reservas y etapas con redistribución, indisponibilidad, cambios de rol y reasignación. Una redistribución no cambia la dependencia ni el documento; una reasignación crea otro intento de la misma etapa.

Entregable: vista previa de carga y generación atómica, con fechas previstas diferenciadas de las efectivas.

Criterio de aceptación: cada repetición crea una única ejecución, reserva toda la carga prevista y no sobreasigna silenciosamente al liberarse una etapa.

## Fase 4 Implementar transiciones y devolución

Crear un servicio de secuencias compartido por entrega, aprobación, devolución y habilitación programada. Reutilizar transacciones, bloqueo y revisiones de concurrencia actuales. Guardar el evento de la etapa anterior y el cambio de la siguiente en la misma transacción cuando suceden inmediatamente.

Adaptar `OperationalTaskService`, `OperationalSupervisionService` y la captura fotográfica para aplicar la misma condición de disponibilidad. Registrar los eventos de habilitación, asignación, devolución y cierre con actor, fecha, ejecución, etapa, intento y versión documental.

Las reglas de entrega y aprobación se evalúan sobre el intento vigente y el evento concreto, no únicamente sobre `CompletedAt`. El procesamiento diferido debe poder repetirse y recuperarse tras un reinicio sin generar nuevas asignaciones o recalcular el inicio desde cero.

Entregable: flujo completo del piloto, incluido el retorno a Piso y la nueva revisión del documento corregido.

Criterio de aceptación: la concurrencia, los reenvíos y los reinicios no habilitan dos veces una etapa; una devolución conserva la trazabilidad y exige las decisiones correspondientes sobre la nueva versión.

## Fase 5 Integrar permisos y evidencia documental

Aplicar autorización por etapa y ejecución a consultas, decisiones, historial y archivos. Mantener el acceso de Supervisión conforme a la jerarquía actual y añadir únicamente el acceso contextual necesario para participantes del proceso.

Reutilizar validación técnica, análisis de malware y almacenamiento de evidencia. Añadir vínculos de versiones y procedencia sin duplicar innecesariamente los bytes. Consultar la versión de entrada debe ser posible aunque ya exista una versión posterior.

Entregable: matriz de permisos y visor del documento recibido y producido por cada etapa.

Criterio de aceptación: un participante accede al material necesario de su trámite y el servidor rechaza consultas o descargas de procesos ajenos. La autorización final identifica inequívocamente el documento autorizado.

## Fase 6 Incorporar configuración y pantallas

Dirección administra una sección «Secuencias de trabajo»: nombre, versión, calendario o inicio ocasional, tareas de cada etapa, condición, plazo y devolución permitida. Crear y editar una tarea simple conserva su recorrido habitual.

En «Mi trabajo», mostrar el nombre del proceso, la etapa, su responsable, el documento de entrada y el motivo de espera. Las acciones disponibles deben corresponder a entregar, revisar, sellar o autorizar según la etapa, sin vocabulario técnico.

Conservar la selección del Tablero Vivo por vencimiento efectivo del día. Para etapas con vencimiento fijo, mostrar la espera si vencen hoy. Las etapas con vencimiento relativo aún desconocido se consultan en el proceso y en próximas etapas de «Mi trabajo»; no se les inventa un vencimiento para hacerlas aparecer en el tablero.

Conservar el gris con texto explícito para «Programada» y «En espera de…». Mostrar separadamente bloqueos, falta de responsable, vencimiento y entregas. Actualizar la lógica del servidor y de `live-board.js` y `assigned-tasks.js` para que el reloj del navegador no libere una dependencia.

En Supervisión y Reportes, separar entrega de tarea, aprobación de etapa y cierre de proceso. Distinguir el tiempo de espera anterior del tiempo disponible para el responsable. Revisar los porcentajes del tablero para que una entrega parcial no se interprete como autorización final.

Entregable: pantallas y prototipo visual externo antes de modificar la interfaz. Una vez aprobado el diseño, implementar y retirar el prototipo temporal.

Criterio de aceptación: la interfaz explica por qué una etapa espera, quién debe actuar y sobre qué documento; la vista de escritorio y la vista estrecha permiten completar el mismo flujo.

## Fase 7 Migrar y verificar

1. Probar la migración sobre una copia local con respaldo, comparación de asignaciones y huellas de evidencias. Las filas anteriores deben conservar responsables, fechas, entregas y revisiones.
2. Ampliar `Sgol.TaskChecks` con secuencias por entrega y aprobación, cierre por supervisión sin entrega, inicio mínimo, plazos fijos y relativos, retraso más allá del vencimiento, procesamiento diferido y devolución.
3. Verificar múltiples ejecuciones del mismo proceso, documentos de distinta versión, intentos sustituidos, reenvíos concurrentes y ausencia de dobles reservas de capacidad.
4. Cubrir falta de personal, cambios de rol, especialización, indisponibilidad, redistribución y continuación después de resolver una asignación pendiente.
5. Ampliar `Sgol.BoardChecks`, `Sgol.ReportChecks` y `Sgol.RoleAccessChecks` para visibilidad, cálculos y acceso contextual. Mantener verdes las pruebas de tareas independientes, ocasiones y borrado de planes.
6. Revisar cancelación, desactivación del catálogo y eliminación de planes: impedir referencias huérfanas y conservar los mecanismos actuales de vista previa, confirmación y transacción. No habilitar borrado automático de procesos activos.
7. Compilar, revisar cambios pendientes de EF, reiniciar el servicio y comprobar `/health/ready`.
8. Ejecutar el piloto autenticado con los cuatro roles, en escritorio y una pantalla estrecha. Validar hardware y acceso LAN desde otro dispositivo cuando formen parte del uso previsto.

Entregable: evidencia de verificaciones, migración y recuperación, y recorrido completo del piloto con una devolución.

Criterio de aceptación: las verificaciones anteriores y el piloto pasan; no hay pérdida de datos ni regresiones de tareas comunes. Un resultado de pruebas automatizadas no sustituye la validación del proceso por sus participantes.

## Fase 8 Introducir el piloto y ampliar el catálogo

Habilitar la función inicialmente para el piloto. Las secuencias desactivadas no inician ejecuciones nuevas, pero las existentes continúan o se cancelan expresamente; apagar la creación de procesos no debe abandonar etapas activas.

Recorrer una ejecución favorable y otra con devolución. Confirmar que las etiquetas, plazos y decisiones reflejan la operación de la tienda. Actualizar `docs/tareas-operativas.md`, `docs/historial-entregas-revisiones.md` y la guía del Excel con el comportamiento final.

Clasificar y configurar los demás procesos del Excel solo después de validar el piloto. Registrar cada avance en este plan y sus evidencias. No convertir automáticamente comentarios ambiguos en secuencias.

Criterio de aceptación: las personas de Piso, Subcoordinación, Administración y Dirección pueden explicar y ejecutar su etapa; las tareas completas continúan con el funcionamiento habitual.

## Orden de ejecución y avance

| Fase | Depende de | Estado |
| --- | --- | --- |
| 1 Definir procesos | Caso real y comentarios del Excel | Piloto definido; resto del catálogo pendiente |
| 2 Diseñar datos | Ficha y reglas de fase 1 | Completada: modelo, SQL, diccionario, transiciones y pruebas en copia |
| 3 Generación y capacidad | Modelo de fase 2 | Pendiente |
| 4 Transiciones y devolución | Fases 2 y 3 | Pendiente |
| 5 Permisos y documentos | Fases 2 y 4 | Pendiente |
| 6 Pantallas | Contratos de fases 3 a 5 y diseño visual aprobado | Integración pendiente; propuesta externa anticipada en revisión |
| 7 Migración y verificación | Fases 2 a 6; pruebas desarrolladas durante cada fase | Pendiente |
| 8 Piloto y catálogo | Validación de fase 7 | Pendiente |

## Puntos de integración

| Componente | Cambio previsto |
| --- | --- |
| `OperationalTasks.cs` y configuraciones de persistencia | Vínculos con etapa e intento vigente; compatibilidad con fechas actuales. |
| `OperationalPlanGenerator.cs` y `EfDirectionPreparedPlanService.cs` | Generación de ejecuciones completas y prevención de etapas independientes duplicadas. |
| `PlanningCapacity.cs` y `OperationalCapacityService.cs` | Reservas previstas, fechas efectivas, revalidación y redistribución. |
| `OperationalTaskService.Occasions.cs` | Inicio ocasional de una secuencia sin duplicar su identidad con la ocasión. |
| `OperationalTaskService.cs` y `OperationalTaskService.Camera.cs` | Bloqueo de acciones prematuras y evento de entrega. |
| `OperationalSupervisionService.cs` | Decisiones, devoluciones y reasignación conservando la etapa. |
| `OperationalTaskService.History.cs` | Historial del proceso, sus intentos y versiones documentales. |
| `LiveBoardReader.cs` y `OperationalWorkPlan.cs` | Visibilidad y disponibilidad coherentes con dependencia y vencimiento. |
| Páginas Tareas, Planificación, Mi trabajo, Supervisión y Reportes | Configuración y seguimiento entendibles. |
| `live-board.js` y `assigned-tasks.js` | Colores y acciones que respetan el estado del servidor. |
| Migraciones, borrado de planes y desactivación de tareas | Preservación, recuperación e integridad del proceso. |

La primera implementación comienza por la ficha del proceso documental. El esquema y las pantallas se ajustan a ese flujo definido, evitando construir disparadores genéricos para actividades que pueden permanecer como una sola tarea.

## Registro de avance — 13 de octubre de 2026

- Se inspeccionaron las dos plantillas locales. La actual conserva 202 códigos y nombres, con todos los campos operativos vacíos y sin comentarios de celda. No hay evidencia suficiente para convertir filas en etapas.
- Se creó la [ficha del piloto y matriz de etapas](secuencias/piloto-documental.md), con decisiones pendientes explícitas.
- Se generó la [cobertura de las 202 filas](secuencias/clasificacion-catalogo.csv), todas pendientes de definición, y su [evidencia con huella de origen](secuencias/evidencia-catalogo.json). El libro permanece intacto.
- Se preparó el [mapa de integración verificado contra el código](secuencias/mapa-integracion.md). Detecta la restricción de fechas, cierre sin entrega, reparto, permisos y borrado que deben resolverse en fase 2.
- Falta identificar la hoja real, criterios de revisión/sello/autorización, plazos y un ejemplo de devolución. Se solicitó esa definición al usuario.
- Las fases 2 a 8 siguen pendientes. No se modificó código de producción, esquema, datos ni interfaz; no se ejecutaron migración, compilación, reinicio ni piloto autenticado en este avance documental.

### Definición posterior del piloto

- El usuario definió el chequeo de inventario, revisiones en Sizes & Colors, búsquedas de faltantes/sobrantes y devoluciones sucesivas hacia la etapa anterior.
- Confirmó PDF, febrero y agosto, lunes entre los días 20 y 25 o día 20 cuando no haya lunes.
- Plazos relativos: Piso 6 horas; Subcoordinación 1 hora; Administración 1 hora; Dirección 168 horas. La última indicación sustituye su plazo inicialmente ilimitado.
- Dirección puede aceptar una versión con diferencias pendientes. Se conservarán la lista de diferencias y su conclusión; no se declararán resueltas por esa aceptación.
- Se inició el modelo separado de versiones, etapas, ejecuciones, intentos, documentos y eventos. Las asignaciones independientes mantienen sus fechas y fragmentos actuales.
- Se añadieron comprobaciones ejecutables de calendario, plazos, procesamiento diferido, responsables pendientes, reglas de entrega/aprobación y devoluciones.

### Fase 2 — Modelo y compatibilidad verificados

- Se implementaron ocho entidades EF: secuencia, versión, etapa de definición, ejecución, etapa de ejecución, intento, documento y evento. El [diccionario y las transiciones](secuencias/modelo-datos.md) describen sus campos y límites.
- Se generó `20261013193317_AddOperationalSequences` y el [SQL incremental revisable](../outputs/secuencias/migracion.sql). Es aditivo respecto de las asignaciones; agrega una clave compuesta de integridad sin cambiar sus valores o fechas.
- Las FKs compuestas impiden mezclar ejecuciones, etapas, planes y documentos. Las referencias circulares y la exclusión de dos etapas accionables son diferidas para permitir transacciones de devolución.
- La versión usada es inmutable. Los eventos y documentos conservan su historia; la evidencia sigue en su almacenamiento actual. Reasignar exige otro intento.
- Se respaldó `sgol_local` y se recuperó en una copia de pruebas. Las huellas antes/después coinciden para asignaciones, entregas, revisiones, evidencias, ocasiones, catálogo, capacidad y planes. La bajada/reaplicación del esquema conserva esos datos y la bajada se rechaza si existen ejecuciones de secuencias.
- Compilación sin advertencias ni errores. Pasaron 122 comprobaciones reportadas de reglas/esquema, 359 de tareas incluyendo las nuevas reglas, 54 de tablero, 75 de reportes y la matriz de pantallas de cuatro roles más acceso anónimo. Los conteos de reglas/esquema y tareas se solapan; no deben sumarse como pruebas distintas.
- Se corrigieron cuatro expectativas antiguas de Tablero Vivo en `Sgol.TaskChecks`: selección por vencimiento del día, entrega temprana y progreso semanal. No se cambió el lector ni las pantallas del tablero.
- El [registro de verificación](secuencias/verificacion-fase-2.md) enlaza los logs y precisa qué todavía no se probó.
- Se confirmó `/health/ready` del servicio existente. No se aplicó la migración al entorno de uso diario ni se reinició o activó el piloto.

### Propuesta visual anticipada

- Se preparó un [prototipo HTML externo](../outputs/secuencias/prototipo/index.html) con las cuatro vistas de rol, documentos, revisiones, devoluciones sucesivas y cierre con diferencias. Usa ejemplos y no escribe en SGOL.
- Pasaron 19 simulaciones de su código en un entorno aislado. No son pruebas de navegador, layout, autenticación ni permisos reales.
- La revisión automática rechazó iniciar el servidor local del prototipo con el motivo «blocked by policy». El navegador rechazó abrir `file:` porque solo permite `http:` y `https:`. No se verificó visualmente ni se intentó eludir esos bloqueos.
- Se solicitó revisión manual y aprobación del diseño, requerida por la fase 6 antes de modificar la interfaz. La propuesta no sustituye los contratos y la integración pendientes de fases 3 a 5.

### Regeneración de la propuesta visual

El usuario indicó que el HTML se abría como código. Se generó una alternativa
gráfica en [PDF](../output/pdf/secuencias-inventario/propuesta-visual-inventario.pdf)
con cuatro páginas: revisión en escritorio, aceptación de Dirección, pantalla
estrecha y configuración. Se renderizaron y revisaron las cuatro imágenes para
comprobar texto, espacios, controles y plazos. La revisión corresponde al diseño
estático del PDF, no a una pantalla autenticada ni a pruebas de hardware.
El HTML se conserva como referencia de la simulación. El usuario aprobó después
el diseño como plantilla reutilizable para cada proceso. Esa aprobación visual
no acredita implementación ni pruebas de la interfaz real.

### Ajuste de alcance — Procesos desde Tareas

El usuario propuso administrar los procesos desde Tareas: nombre, descripción,
búsqueda e incorporación de tareas, creación de tareas con proceso preseleccionado
y reglas entre etapas. Todas las tareas tendrán un proceso; General conservará
su ejecución independiente sin dependencias. El [ajuste de integración](secuencias/procesos-desde-tareas.md)
registra la propuesta, la prevención de asignaciones duplicadas y el orden de trabajo.
Se reutiliza el modelo preparado; el nuevo editor y la pertenencia aún no están implementados.

### Integración pendiente

Las fases 3 a 5 todavía deben implementar generación atómica, reservas integradas con
el ledger y redistribución, entrega/aprobación/devolución persistentes, procesamiento
programado y autorización contextual. Las reglas puras y el esquema no activan ese
recorrido por sí mismos. Después se integrarán las pantallas aprobadas y se ejecutarán
las fases 7 y 8, incluido el piloto autenticado y la validación de sus participantes.
