# Plan de implementación de procesos de trabajo integrados con Tareas

Fecha: 13 de octubre de 2026.
Estado: pertenencia, editor documental y planificación por proceso integrados; migraciones aplicadas con recuperación comprobada. Ejemplo preparado, validación humana y ampliaciones pendientes.

Este documento sustituye el plan de secuencias y reúne la definición del inventario,
la administración de procesos desde Tareas y la plantilla visual aprobada. Es la
referencia para continuar la implementación. «Proceso» será el término de la interfaz;
los nombres técnicos `OperationalSequence` pueden conservarse donde ya están probados.

## Objetivo y estructura única

Administrar las tareas y los procesos en el mismo catálogo, reutilizando asignaciones,
planificación, capacidad, evidencias, supervisión y reportes de SGOL.

Cada tarea pertenece a un proceso mediante un identificador estable. El proceso
tiene nombre, descripción y configuración versionada. Una ejecución concreta es una
repetición de ese proceso; sus etapas utilizan las tareas elegidas del catálogo.

General es el proceso reservado para tareas independientes: no tiene una ruta común,
no crea ejecuciones colectivas ni impone condiciones de activación entre sus tareas.
Conserva los calendarios y las ocasiones actuales. Asignar una tarea a General no
la vuelve activa ni altera su rol, requisito, especialización o historia.

Un proceso con flujo controla su calendario o inicio ocasional y las reglas entre
sus etapas. Las tareas de ese proceso no se generan también por calendarios propios
ni mediante la asignación ocasional independiente. La planificación selecciona el
proceso como una unidad y muestra sus tareas y carga desglosadas.

Pertenecer a un proceso y estar habilitado para trabajar son hechos distintos.
Un borrador puede reunir tareas, pero solo una versión publicada y válida puede
iniciar ejecuciones. Toda entrada de generación debe aplicar esta distinción en el
servidor, incluidos los planes preparados, ocasiones y llamadas directas.

## Alcance inicial y criterios de modelado

- Una tarea pertenece a un solo proceso. Reutilizar la misma tarea en varios
  procesos simultáneamente queda fuera de esta primera versión.
- Procesos lineales: una etapa anterior y una sola etapa accionable por ejecución.
  Se permiten devoluciones configuradas hacia etapas anteriores; el inventario
  devuelve exactamente a la etapa anterior.
- Inicio por calendario o por una ocasión del proceso, con identidad única.
- Avance por entrega del responsable o aprobación expresa del intento vigente;
  inicio mínimo opcional; plazo relativo o vencimiento fijo.
- Un responsable por etapa, seleccionado por rol, especialización y capacidad.
- Versiones documentales, decisiones y devoluciones conservadas con autor y fecha.
- General mantiene las tareas completas sin dependencias entre ellas.

Una actividad con varios pasos no exige varias tareas. Separar etapas cuando cambia
el responsable, existe una revisión o autorización, hay un resultado intermedio que
conservar o un plazo independiente. Barrer y trapear puede seguir siendo una tarea
completa de General.

Las ramas, etapas simultáneas, varias predecesoras, dependencias entre procesos,
reapertura de procesos concluidos, edición de PDF y firmas electrónicas requieren
un alcance posterior. La revisión en Sizes & Colors es realizada por la persona;
este piloto no incluye una integración automática con ese sistema.

## Recorrido de configuración desde Tareas

Dirección administra los cambios; los demás roles conservan el acceso de consulta
que les corresponda. La cabecera de Tareas tendrá Crear tarea, Crear especialización
y Crear proceso. El acceso a especializaciones reutiliza la pantalla existente.

Crear proceso sigue este recorrido:

1. Capturar nombre y descripción y guardar un borrador con identidad estable.
2. Buscar tareas por nombre o código y añadirlas al proceso.
3. Crear una tarea desde ese mismo editor con el proceso preseleccionado, reutilizando
   los campos, validaciones y permisos actuales; volver al proceso conservando el borrador.
4. Ordenar las etapas y definir su acción, entrada, plazo, instrucciones y devolución.
5. Configurar el inicio y revisar tareas, reglas y carga antes de publicar la versión.
6. Activar el proceso para nuevas ejecuciones, sin iniciar trabajo por el mero hecho
   de haber creado o guardado su definición.

El formulario de tarea tendrá Proceso; al crear desde el catálogo su valor inicial
será General. El listado tendrá columna y filtro de proceso. Crear dentro del editor
usará el ID del borrador: el nombre por sí solo no es una referencia válida.

Los campos se reparten para evitar configuraciones contradictorias:

| Lugar | Datos que administra |
| --- | --- |
| Tarea | Nombre, código, rol, especialización, requisito de evidencia, carga estimada y disponibilidad para nuevas asignaciones. |
| Tarea de General | Además, su calendario, repeticiones y plazo individual actuales. |
| Proceso con flujo | Nombre, descripción, inicio por calendario u ocasión, publicación y activación. |
| Etapa del proceso | Tarea seleccionada, orden, acción, condición de entrada, inicio mínimo, plazo o límite fijo y devolución permitida. |

Para una tarea de proceso con flujo, el formulario mostrará que inicio y plazo los
controla el proceso y permitirá ir a sus reglas. Los valores individuales anteriores
pueden conservarse para compatibilidad, pero no deben alimentar ese recorrido.

Añadir una tarea de General a un borrador de proceso cambia su pertenencia y suspende
su generación independiente futura; el editor debe mostrar ese efecto. No afecta
asignaciones ya creadas. Una tarea de otro proceso no se mueve silenciosamente:
el cambio requiere revisar y publicar configuraciones coherentes en origen y destino.
No se permite dejar una versión vigente inválida ni perder trabajo activo.

Cancelar el formulario de una tarea nueva no crea la tarea. Cerrar el editor conserva
el borrador y muestra las tareas pendientes de publicación. Para abandonarlo se
ofrece devolver a General las tareas que no están comprometidas por una versión
publicada; no se eliminan tareas ni evidencias automáticamente.

## Piloto: chequeo de inventario

Calendario: febrero y agosto; lunes entre los días 20 y 25 inclusive; si no existe
lunes en ese intervalo, día 20. El ejemplo autorizado usa 11:00 de Ciudad de México y cargas provisionales de
360/60/60/60 minutos. Dirección conserva 168 horas de plazo y 60 minutos de trabajo.
Estos valores y las cuatro cuentas sintéticas no representan participantes ni cargas
reales del piloto. El plan de demostración usa una ocasión explícita en octubre;
el calendario publicado conserva febrero y agosto.

Evidencia: PDF. Los plazos son horas transcurridas desde la habilitación efectiva,
sin conversión implícita a turnos o días laborales.

| Etapa / tarea | Rol | Trabajo y decisión | Plazo | Devolución |
| --- | --- | --- | --- | --- |
| Registrar inventario completo | Piso de ventas | Contar todo, declarar registro completo y entregar PDF; en devolución buscar faltantes y presentar sobrantes. | 6 horas | Sin etapa anterior |
| Revisar inventario | Subcoordinación | Contrastar con Sizes & Colors; revisar hoja y zapatos tras la búsqueda; documentar diferencias no resueltas; aprobar el paso o devolver. | 1 hora | Piso |
| Chequear hoja e irregularidades | Administración | Revisar toda la hoja; si conoce información relevante, devolver con comentario; en otro caso pasar a Dirección. | 1 hora | Subcoordinación |
| Aceptar resultado del inventario | Dirección | Devolver con comentario o aceptar la versión exacta con conclusión y diferencias pendientes identificadas. | 168 horas / una semana | Administración |

Administración no sella en este piloto. Aprobar una revisión no declara resueltas
todas las irregularidades. Dirección cierra como Sin diferencias o Aceptado con
diferencias pendientes; la segunda opción conserva la lista y la conclusión.

Las observaciones de Dirección bajan por Administración y Subcoordinación hasta
Piso cuando hace falta búsqueda. Cada paso exige la decisión del responsable activo.
No se deriva trabajo arbitrariamente a otro rol.

Una revisión sin cambio puede conservar el PDF recibido y añadir su decisión.
Si se modifica la hoja, se crea una nueva versión. El PDF y la declaración de Piso
no prueban por sí solos que el conteo sea completo: la revisión operativa sigue
siendo necesaria. Las tareas y personas reales se elegirán explícitamente;
los nombres del Excel no bastan para inferir sus IDs.

## Reglas comunes del motor

### Programación, habilitación y vencimiento

Las fechas previstas sirven para planificar y reservar carga. Las efectivas indican
cuándo se habilitó el trabajo y cuándo vence. Una etapa requiere dependencia válida,
inicio mínimo cumplido y responsable elegible con capacidad. El servidor verifica
esas condiciones en avances, evidencia, entrega y decisiones.

Si llega la entrega antes del inicio mínimo, la etapa permanece programada. Si falta
responsable, permanece pendiente de asignación y se avisa a Dirección. El plazo
relativo empieza al habilitarse con responsable válido; no al visitar la pantalla.
El procesamiento programado materializa inicios y recupera retrasos sin desplazar
el instante que correspondía ni duplicar asignaciones.

Un límite fijo no se mueve por retrasos anteriores. Si pasó antes de habilitarse,
se conserva y se registra Fuera de plazo con causa, sin crear un rango inválido.
Dirección puede cancelar con motivo o iniciar una ejecución sustituta vinculada;
no puede convertir ese bloqueo en entrega ni cambiar silenciosamente el límite.
Las asignaciones actuales conservan la restricción `DueAt > StartsAt`.

### Entregas, decisiones e identidad

Solo ENTREGA del responsable satisface una dependencia por entrega. Un cierre
SUPERVISION sin entrega no habilita la siguiente etapa. La aprobación exige una
decisión expresa y los requisitos documentales de la etapa.

La revisión del documento de entrada y el resultado de la etapa revisora se guardan
atómicamente con el autor real; no se exige otra aprobación equivalente. La facultad
general de Dirección para cerrar tareas no permite saltarse el flujo.

Cada ejecución, ronda e intento tiene identidad propia. Los eventos del intento
vigente habilitan únicamente la siguiente etapa de esa ejecución. Reenvíos,
concurrencia y reinicios usan idempotencia, bloqueo transaccional y revisiones.

### Devoluciones, documentos e historia

La devolución se permite desde la etapa activa, con motivo y destino autorizado.
Abre otro intento en el destino y otra ronda para el tramo posterior. Ese tramo
debe ejecutarse nuevamente; el anterior al destino conserva su validez.

Las decisiones sustituidas y los PDFs anteriores siguen consultables como historia,
pero no autorizan el documento corregido. Una reasignación crea otro intento sin
cambiar la dependencia ni perder el documento de entrada. Una decisión tardía de
un intento sustituido no avanza ni revierte el proceso.

La aceptación final identifica el documento autorizado y bloquea devoluciones.
Una nueva investigación requiere otra ejecución vinculada. Cancelación y cierre
administrativo son eventos explícitos con motivo, sin simular entrega o aceptación.

### Versiones y permisos

Publicar guarda una versión coherente de pertenencia y reglas. Una versión usada
no se edita; los cambios de nombre, rol, pertenencia o reglas afectan nuevos inicios.
Las ejecuciones conservan su configuración y snapshots de tareas y participantes.

Desactivar un proceso impide nuevos inicios; las ejecuciones existentes continúan
o se cancelan expresamente. General permanece como destino de sistema y no puede
eliminarse ni desactivarse como una ruta común.

La participación concede acceso contextual al documento y decisiones necesarios
de la ejecución, sin ampliar la jerarquía general de Supervisión. El mismo control
protege historial, visor y descargas directas. Se reutilizan almacenamiento,
validación técnica y análisis de malware sin duplicar bytes innecesariamente.

## Fase 1 — Consolidar catálogo, piloto y contratos de uso

1. Mantener la clasificación del Excel en tarea completa, etapa o caso por definir;
   las 202 filas analizadas no contienen reglas suficientes para configuración automática.
2. Adoptar General y pertenencia única, recorrido de crear proceso y división de campos.
3. Conservar la ficha de inventario y elegir sus cuatro tareas, hora y cargas reales.
4. Definir contratos de borrador, publicación, activación, cambio de pertenencia y abandono.
5. Completar el diseño del editor de procesos usando la plantilla aprobada. Si cambia
   de forma material la interacción aprobada, presentar la parte nueva antes de integrarla.

Entregable: ficha, matriz, recorrido del editor y contratos sin acciones ambiguas.
Aceptación: se explica qué ocurre al crear, guardar, publicar, activar, mover una
tarea, devolver y cerrar; los datos necesarios para activar el piloto están definidos.

## Fase 2 — Ampliar el modelo y la compatibilidad

1. Reutilizar las ocho entidades del motor ya preparado; añadir descripción, General,
   estado de publicación y pertenencia de `OperationalTask` mediante referencia estable.
2. Representar borradores y versiones publicadas con sus tareas y reglas; validar
   coherencia entre pertenencia actual, publicación e historia de versiones anteriores.
3. Migrar las tareas existentes a General, manteniendo calendarios, fechas, asignaciones,
   entregas y revisiones. Las asignaciones históricas no reciben dependencias inventadas.
4. Mantener etapas pendientes y reservas fuera de asignaciones efectivas con fechas
   obligatorias; conservar integridad por ejecución, versión, plan, intento y documento.
5. Versionar cambios, registrar auditoría y proteger edición concurrente. General
   no se representa como una ejecución lineal con todas las tareas del catálogo.
6. Preparar SQL revisable, respaldo actualizado y recuperación en copia; revisar la
   migración base y su ampliación conjuntamente antes del despliegue.

Entregable: diccionario actualizado, transiciones, migración y pruebas de integridad.
Aceptación: todas las tareas tienen proceso; General conserva su conducta; publicación
coherente y fechas desconocidas se representan sin alterar historia ni fabricar fechas.

## Fase 3 — Integrar el editor de procesos con Tareas

1. Añadir los accesos Crear especialización y Crear proceso, columna y filtro de proceso.
2. Incorporar Proceso a crear/editar tareas; reutilizar servicios y validación existentes.
3. Implementar borrador con nombre y descripción, buscador de tareas y creación integrada
   con proceso preseleccionado y retorno al editor.
4. Implementar orden y reglas: entrada, acción, inicio mínimo, plazo, devolución y calendario.
5. Validar publicación: tareas activas y compatibles, reglas completas, primera entrada,
   posiciones únicas, devoluciones válidas y ausencia de pertenencias contradictorias.
6. Mostrar efectos de mover tareas, guardar o abandonar un borrador y desactivar;
   aplicar permisos, antiforgery, auditoría y concurrencia desde el servidor.

Entregable: catálogo y editor persistentes, sin generación prematura.
Aceptación: Dirección crea el inventario, reutiliza o crea sus tareas y publica una
configuración coherente; cancelar, recargar o repetir el envío no pierde ni duplica datos.

## Fase 4 — Unificar planificación, generación y capacidad

1. General sigue entrando en planificación por tareas y calendarios individuales.
   Los procesos con flujo entran por definición publicada y por fecha u ocasión.
2. Impedir asignación independiente de sus etapas en todas las entradas del servidor;
   la vista previa presenta proceso, versión, tareas, responsables y carga estimada.
3. Generar una única ejecución y sus etapas con clave de repetición e idempotencia.
   Crear la primera asignación efectiva solo cuando sea válida.
4. Reservar carga prevista de todas las etapas con el ledger actual. Al habilitar,
   reconciliar reservas y asignaciones para contabilizar el esfuerzo una sola vez.
5. Revalidar rol, especialización, indisponibilidad y capacidad; redistribuir cuando
   proceda o conservar Pendiente de asignación con aviso a Dirección.
6. Incluir versión y reservas en las huellas de vista previa; integrar reasignaciones
   y devoluciones para representar su nueva carga sin duplicar reservas anteriores.

Entregable: vista previa y generación atómica integradas con planes y ocasiones.
Aceptación: ninguna etapa aparece dos veces; se reserva el trabajo completo y la
liberación no sobreasigna silenciosamente ni deja ejecuciones abandonadas.

## Fase 5 — Implementar acciones, temporizador, permisos y documentos

1. Integrar un servicio de procesos con entrega, revisión, aprobación, devolución,
   reasignación, cancelación y cierre, usando transacciones y bloqueos actuales.
2. Compartir la verificación de disponibilidad con avances, cámara y documentos.
3. Persistir eventos, ronda, intento y documento exacto en la misma transacción de avance.
4. Implementar procesamiento diferido recuperable e idempotente para mínimos y asignaciones.
5. Integrar acceso contextual a consulta, decisiones, entrada/salida e historial;
   proteger URLs directas y mantener los límites generales de Supervisión.
6. Vincular versiones documentales al almacenamiento existente; conservar procedencia,
   decisiones y diferencias pendientes; cerrar con conclusión y PDF autorizado.

Entregable: recorrido persistente completo del inventario y matriz de permisos.
Aceptación: devolución y nueva revisión conservan historia; eventos antiguos,
envíos concurrentes y reinicios no avanzan dos veces; no se accede a procesos ajenos.

## Fase 6 — Conectar la plantilla visual y el seguimiento

Criterio obligatorio de apariencia: todas las pantallas nuevas y sus cambios deben
ser compatibles con modo claro y oscuro. Fondos, texto, bordes, acciones y estados
usan la paleta compartida del tema, sin colores claros fijos. Antes de entregar un
cambio visual se revisan ambos modos, incluidas etapas activas/en espera y formularios.

1. Reutilizar la plantilla aprobada para cada proceso: nombre, etapas, responsable,
   estado, documento recibido, decisiones, versiones, comentarios y próximas etapas.
2. Mi trabajo muestra motivos de espera y acciones de la etapa; General conserva
   su recorrido habitual. El estado del servidor determina la disponibilidad.
3. Mantener Tablero Vivo por vencimiento efectivo del día. Los límites fijos conocidos
   pueden mostrar espera si vencen hoy; las etapas relativas pendientes se consultan
   en el proceso, sin inventar un vencimiento para incluirlas en el tablero.
4. Separar Programada, En espera, Pendiente de asignación, Fuera de plazo y entrega;
   mantener gris y texto para espera, con accesibilidad en escritorio y pantalla estrecha.
5. Supervisión y Reportes distinguen entrega de tarea, aprobación de etapa y cierre
   del proceso, así como espera anterior y tiempo disponible del responsable.
6. Integrar cliente y servidor; una tarjeta, un color o el reloj del navegador no
   habilita una dependencia. Retirar el prototipo temporal al completar la integración.

Entregable: configuración y seguimiento dentro de SGOL usando la plantilla aprobada.
Aceptación: cada rol identifica quién actúa, por qué espera y sobre qué documento;
puede completar las mismas acciones en escritorio y pantalla estrecha.

## Fase 7 — Verificar, migrar y preparar el piloto

1. Probar esquema base y ampliación en copia con respaldo reciente y comparación de
   filas y huellas de evidencia; comprobar preservación y recuperación.
2. Verificar General, pertenencia única, borradores, publicación, creación integrada,
   cambio de proceso, desactivación y protección de versiones en uso.
3. Ampliar TaskChecks con entrega/aprobación, cierre de Supervisión sin entrega,
   mínimos, plazos relativos/fijos, límite vencido, temporizador y devoluciones.
4. Probar concurrencia, reenvíos, reinicios, varias ejecuciones, intentos sustituidos,
   versiones de documentos y ausencia de generación o carga duplicadas.
5. Probar falta de personal, especialización, cambio de rol, indisponibilidad,
   redistribución y continuación tras resolver una asignación pendiente.
6. Ampliar BoardChecks, ReportChecks y RoleAccessChecks con procesos y acceso contextual;
   mantener regresiones de tareas de General, ocasiones y borrado de planes.
7. Revisar cancelación y borrado: preservar referencias e historia y mantener vista
   previa, confirmación, auditoría y transacción para acciones destructivas.
8. Compilar, comprobar modelo EF y migraciones, aplicar en el entorno previsto con
   recuperación preparada, reiniciar servicio y comprobar `/health/ready`.
9. Ejecutar el piloto autenticado de los cuatro roles en escritorio y pantalla estrecha.
   Probar otro dispositivo para afirmaciones de hardware o acceso LAN.

Entregable: logs, preservación y recuperación demostradas y recorridos autenticados.
Aceptación: pasan las verificaciones sin pérdida de datos ni regresiones; las pruebas
automatizadas no sustituyen la validación de operación por los participantes.

## Fase 8 — Activar inventario y ampliar el catálogo

1. Publicar y habilitar inicialmente solo el inventario con tareas, hora y cargas reales.
2. Validar recorrido favorable, devolución por faltantes/sobrantes, observación de
   Administración, retorno desde Dirección y aceptación con diferencias pendientes.
3. Confirmar con los participantes que etiquetas, plazos y acciones reflejan su trabajo.
4. Actualizar guía de tareas, historial y Excel con Proceso y los campos finales;
   las reglas entre tareas se configuran en el proceso, no mediante texto ambiguo.
5. Clasificar y configurar otros procesos gradualmente. Mantener tareas completas en
   General cuando no requieren controles separados.
6. Registrar avance y evidencia en este plan. Desactivar nuevos inicios no abandona
   ejecuciones activas: continúan o se cancelan con decisión explícita.

Entregable: piloto operativo validado y guía para incorporar nuevos procesos.
Aceptación: los cuatro roles pueden explicar y ejecutar su etapa; General conserva
su funcionamiento y cada nuevo proceso se incorpora con reglas suficientes.

## Orden y estado real de avance

Las fases se ejecutan en orden; las pruebas y los permisos se desarrollan junto con
cada cambio. El editor puede construirse tras fase 2, pero no habilita la operación
hasta integrar fases 4 a 7. No se repite trabajo probado salvo ajustes o nuevas dependencias.

| Fase | Estado comprobado al 13/10/2026, 17:18 -06:00 |
| --- | --- |
| 1 Contratos y piloto | Ejemplo definido con los valores y recorrido elegidos por el usuario. Excel/MD cruzados semánticamente sin adoptar la arquitectura del MD ni inventar IDs TAR. Participantes reales pendientes. |
| 2 Modelo y compatibilidad | General, pertenencia única, borradores, publicaciones inmutables, snapshots y planes de proceso migrados; recuperación en copia y preservación histórica comprobadas. Traslado coordinado entre publicaciones pendiente. |
| 3 Editor desde Tareas | Crear proceso, descripción, buscador, creación con proceso preseleccionado, etapas, reglas, borrador/publicación/habilitación separados; comprobaciones de catálogo y acceso. |
| 4 Planificación y capacidad | Seleccionar proceso publicado, cargar tareas, elegir responsables, revisar calendario u ocasión y capacidad, guardar plan con reservas y activar únicamente la etapa disponible. Redistribución contextual de procesos pendiente. |
| 5 Acciones, permisos y documentos | Flujo PDF persistente, temporizador, intentos, devoluciones, diferencias, versiones y permisos contextuales comprobados. Reasignación por servicio con nuevo intento y ajuste temporal de capacidad comprobados; interfaz general, cancelación y ejecución sustituta pendientes. |
| 6 Plantilla y seguimiento | Editor y seguimiento documental implementados sobre la plantilla; planificación revisada en escritorio y 390 px. Vistas agregadas de Supervisión/Reportes pendientes. |
| 7 Verificación y migración | Compilación, regresión, recuperación, migración diaria, pruebas HTTP autenticadas y acceso habitual comprobados. Ver límites y evidencia de cada prueba. |
| 8 Activación y ampliación | El usuario activó el ejemplo. A petición posterior, Piso se reasignó a ana.demo y Dirección a direccion.local; las otras etapas mantienen sus cuentas de ejemplo. Ajuste temporal de Ana autorizado y probado en copia. Validación humana en curso; otros flujos y tareas no se cargaron sin campos y utilidad suficientes. |

La [verificación actual](secuencias/verificacion-procesos.md) registra el respaldo,
SQL, huellas, pruebas, capturas y los pendientes concretos. La
[guía del recorrido manual](secuencias/prueba-manual-inventario.md) permite activar
el ejemplo y probarlo con las cuatro cuentas sintéticas. Los conteos de reglas
compartidas no se suman como pruebas independientes.

## Puntos de integración

| Componente | Integración |
| --- | --- |
| `OperationalTasks.cs`, contratos y servicio del catálogo | Pertenencia, General, división de campos y validación compartida. |
| Entidades `OperationalSequence*` y reglas | Descripción, publicación, versiones coherentes y ejecución del proceso. |
| Páginas Tareas y especializaciones | Accesos, filtro, campo Proceso, editor y creación de tarea integrada. |
| `OperationalPlanGenerator` y planes preparados | Entrada por proceso, versión y repetición; bloqueo de generación independiente duplicada. |
| `PlanningCapacity` y `OperationalCapacityService` | Reservas, huellas, habilitación, devoluciones y redistribución. |
| `OperationalTaskService.Occasions` | Ocasión de General u ocasión completa de proceso. |
| Servicios de tareas, supervisión, cámara e historial | Disponibilidad, acciones, eventos y permisos contextuales. |
| Almacenamiento y descarga de evidencia | Versiones, procedencia y documento autorizado. |
| `LiveBoardReader`, Mi trabajo, Supervisión y Reportes | Fechas efectivas, estado y distinción entre tarea, etapa y proceso. |
| `live-board.js` y `assigned-tasks.js` | Presentación coherente con disponibilidad del servidor. |
| Migraciones y borrado de planes | Preservación, recuperación y protección de ejecuciones e historia. |

## Evidencia que se conserva

- [Ficha del inventario](secuencias/piloto-documental.md) y [clasificación del catálogo](secuencias/clasificacion-catalogo.csv).
- [Mapa de integración](secuencias/mapa-integracion.md) y [modelo base](secuencias/modelo-datos.md),
  que deben ampliarse con esta nueva pertenencia y publicación.
- Migración `20261013193317_AddOperationalSequences`, [SQL base](../outputs/secuencias/migracion.sql)
  y [registro de verificaciones](secuencias/verificacion-fase-2.md).
- Compilación y pruebas previas en copias dedicadas; huellas históricas coincidentes.
  Esos resultados prueban la base anterior, no el editor ni la integración propuesta.
- [Propuesta visual aprobada](../output/pdf/secuencias-inventario/propuesta-visual-inventario.pdf),
  renderizada e inspeccionada; aprobación de diseño, sin acreditar pruebas de navegador.

Las tres migraciones se aplicaron al entorno habitual después de recuperar el
respaldo actualizado en copia. El catálogo de prueba se retiró mediante borrado
lógico auditado, preservando las 35 asignaciones históricas y sus evidencias.
Solo se añadieron las cuatro tareas del ejemplo. Su plan fue activado por el usuario;
la reasignación posterior mantiene la ejecución y sus intentos anteriores. No se
activó el piloto real ni se acredita validación humana por las pruebas técnicas.

El [plan anterior y su registro](plan-secuencias-de-trabajo-historico.md) se conservan
como historia. El [ajuste inicial](secuencias/procesos-desde-tareas.md) queda como
antecedente: este documento contiene el plan operativo consolidado.
