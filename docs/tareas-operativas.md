# Tareas y asignación de planes

## Tareas ocasionales y publicaciones en redes

Dirección puede elegir **Ocasional** en el catálogo. Estas tareas no tienen
repeticiones ni calendario de ejecución y nunca se cargan ni se generan con
planes periódicos, incluso si se intenta enviarlas directamente al servidor.
Las tareas existentes conservan su periodicidad y sus requisitos.

En el catálogo, **Asignar tarea ocasional** abre el formulario de una nueva
ocasión: nombre, instrucciones, texto para publicar y canales opcionales,
inicio y vencimiento en Ciudad de México, material de referencia y destinatarios.
Seleccionar varias personas crea una asignación independiente por persona;
el reparto automático genera una sola asignación usando la capacidad existente.
Se verifica rol, especialización, periodo de prueba, disponibilidad y capacidad.
Si una persona no puede recibirla, no se guarda ninguna asignación ni archivo.
Las ocasionales conservan sus destinatarios al ajustar capacidad; siguen
contando como carga protegida. Supervisión puede reasignarlas expresamente,
sin duplicar una misma ocasión en otra persona que ya la recibió.

Puede vincularse a un plan asignado cuyo intervalo contenga el inicio. Si se
elige **Asignación independiente**, el sistema crea un contenedor ocasional
que aparece en Planificación, Mi trabajo y Tablero Vivo; no hay que crear
un plan de calendario. Ese contenedor no admite generación periódica adicional.

Cada ocasión conserva instrucciones, texto, canales y hasta cinco archivos
JPG/PNG/PDF (15 MB por archivo y 30 MB en total), privados y verificados por
el validador técnico y ClamAV. El material es distinto de la evidencia de
entrega. Solo el responsable y supervisores autorizados pueden descargarlo,
con controles de acceso también en el servidor. Se muestra en Mi trabajo y
Supervisión; el texto se puede copiar y las imágenes se pueden descargar.
La reasignación conserva la ocasión original y exige una nueva entrega.
Cambiar o eliminar la plantilla no cambia instrucciones ni requisitos previos.
La eliminación explícita de un plan también elimina su material de referencia;
la confirmación incluye estos archivos en el total de adjuntos.

El nuevo requisito **Captura de pantalla** permite adjuntar un JPG/PNG desde
el dispositivo (una captura por entrega, máximo 15 MB). Se verifica su contenido
y se escanea antes de guardar; la entrega pasa a revisión. Supervisión exige
esa evidencia para aprobar. **Fotografía** continúa exigiendo captura por cámara.
Supervisión muestra la captura enviada dentro del detalle de la tarea, con
opciones para verla a tamaño completo y descargarla. La vista previa usa los
mismos permisos de evidencia privada que la descarga.
El sistema no publica en las redes ni verifica automáticamente el contenido:
la colaboradora publica y Supervisión revisa la captura.

Para la tarea de redes, crea una plantilla ocasional con requisito Captura de
pantalla y rol Piso de ventas. En cada ocasión agrega la foto, el texto exacto,
el canal, el vencimiento y las colaboradoras que deben publicar.
`tools/Sgol.LocalSetup --create-social-task` prepara esta plantilla en la base
local mediante el servicio autorizado del catálogo, sin generar encargos ni
modificar una plantilla que ya exista. La carga inicial es 10 minutos y el plazo
sugerido 24 horas, ambos editables.

La clave de cada formulario evita duplicar una ocasión al reenviar el mismo
contenido; si se cambian datos o material con esa misma clave, se rechaza por
conflicto. Para otra ocasión se debe abrir una nueva asignación. Tras un error,
los archivos se deben seleccionar nuevamente. Las pruebas de Sgol.TaskChecks
cubren los calendarios anteriores y el nuevo flujo, incluyendo atomicidad,
permisos, material privado, evidencias, idempotencia, capacidad y reasignación.

Dirección administra `/tareas`: nombre, un requisito de cierre (fotografía JPG/PNG,
comentario, documento PDF o ninguno), rol asignado, periodicidad, repeticiones por periodo,
plazo en horas y carga estimada en minutos por repetición. Los ocho ejemplos
anteriores se conservan como punto de partida. Sus parámetros son editables.

Dirección crea y administra especializaciones desde **Personas y accesos → Ver
especializaciones**. La pantalla muestra las personas asignadas, las tareas que
requieren cada especialización y las asignaciones pendientes. El perfil permite
seleccionar varias especializaciones o ninguna y marcar **A prueba**. Administración
conserva la gestión habitual de usuarios, pero no puede modificar estos campos.

En Crear/Editar tarea, **Requiere especializacion** despliega la selección de una
especialización activa. Las tareas anteriores quedan sin este requisito. Una tarea
especializada solo puede asignarse a una persona del rol configurado que posea esa
especialización y no esté A prueba. Si no existe una persona habilitada, el plan
falla sin guardar asignaciones parciales. Entre especialistas con capacidad se
elige la menor carga pendiente del ciclo de ejecución (semana, mes, trimestre o
año), incluyendo las nuevas repeticiones ya repartidas. Las tareas especializadas
se reparten primero dentro de cada fecha para reservar su capacidad.

La misma restricción se usa en la reasignación por revisión y la redistribución de
capacidad. Los cambios al catálogo conservan el nombre y requisito capturados en
las asignaciones anteriores. Se impide quitar una especialización o marcar A prueba
cuando deja asignaciones especializadas pendientes sin responsable habilitado.
Se puede renombrar o desactivar una especialización; para desactivarla no debe haber
tareas activas ni asignaciones pendientes que la requieran. Sus vínculos e historial
se conservan, y los cambios se auditan con revisión para detectar ediciones antiguas.

Para cargar ejemplos únicamente en la base local de desarrollo, ejecuta
`dotnet run --project tools/Sgol.LocalSetup -- --seed-specializations` después de
cargar `scripts/local-env.ps1`. Agrega Contaduría y Marketing, tres tareas ficticias
y un plan de prueba para la siguiente semana; configura perfiles del equipo demo,
incluida Carla como A prueba. No modifica el requisito de las tareas anteriores.

El formulario de planes semanales carga todas las tareas activas, incluidas las mensuales;
solo genera las fechas de cada calendario que caen dentro de la semana. Los otros
periodos cargan inicialmente las tareas cuya periodicidad coincide con el periodo elegido. El botón **Cargar Tareas Predefinidas** repite
esa selección sin duplicar entradas. Se pueden agregar otras tareas manualmente.
Al cambiar el periodo, se reemplaza la selección que se cargó automáticamente.

Al guardar se generan todas las repeticiones dentro de las fechas del plan. Los
ciclos se anclan al calendario: lunes, primer día del mes, trimestre o año.
Las fechas se distribuyen uniformemente en cada ciclo. Por ejemplo, dos veces
por semana produce lunes y jueves. Una tarea semanal agregada a un plan mensual
mantiene su frecuencia semanal; los ciclos parciales incluyen solamente las
fechas que caen dentro del plan. El inicio es a las 00:00 de Ciudad de México y
el vencimiento es inicio + plazo. Crear un plan de fechas pasadas conserva sus
fechas y puede producir tareas vencidas.

El reparto considera empleados activos con cuenta activa en Loretta y filtra por
el rol asignado de cada tarea: Dirección, Administración, Subcoordinación o Piso
de ventas. Dirección puede recibir las tareas configuradas para ese rol. Las
definiciones existentes se migran inicialmente a Piso de ventas y se pueden editar.
Se excluyen los días registrados como no disponibles. Si no hay un
registro de indisponibilidad, se considera disponible. No se interpretan los
campos de turno de texto libre como horarios.

Se suman los minutos de todas las tareas pendientes de cada trabajador,
incluidas las programadas para otros planes. Las obligaciones del sistema
anterior aportan 30 minutos cada una, porque no tienen duración estimada.
Las repeticiones se ordenan por fecha, y después por mayor carga. Cada una se
asigna a quien tenga menos minutos pendientes y se añade inmediatamente a su
carga. Los empates se resuelven por código estable de persona y UUID. Se guarda
la carga previa para explicar la elección en el detalle del plan.

El guardado es atómico, con bloqueo transaccional por sucursal e idempotencia.
Si alguna repetición no tiene candidato disponible, no se crea un plan parcial.
Una solicitud de guardado reenviada con la misma clave recupera su resultado.
Se admiten hasta 1,000 definiciones seleccionadas y 10,000 repeticiones por plan.

Durante la creación, **Asignar cargas de trabajo** consulta a las personas activas
con el rol de alguna tarea seleccionada que tenga repeticiones en el periodo.
Muestra minutos pendientes de todos los planes, minutos del periodo, capacidad
disponible y ocupación con el estilo de las barras existentes. Bajar la barra
reduce el porcentaje de capacidad disponible para las nuevas asignaciones.
No borra minutos pendientes ni transfiere tareas ya asignadas.

Los ajustes se guardan con el plan, reutilizando el registro de capacidad de
Supervisión, el calendario y el algoritmo de reparto. Mantienen los minutos
diarios configurados y cualquier reducción previa más restrictiva dentro del
periodo. La vigencia empieza como mínimo hoy y termina al finalizar el periodo;
al expirar se recupera la capacidad anterior. Cambiar tareas o fechas invalida
los ajustes preparados. Si la carga, el catálogo o la disponibilidad cambiaron
antes de guardar, se exige volver a consultar las barras. Un fallo de asignación
no guarda el plan ni sus ajustes; reenviar un guardado confirmado recupera el
mismo resultado sin duplicarlos.

Las tareas generadas aparecen en **Mi trabajo**, en “Tareas asignadas por planes”.
Mi trabajo reúne las tareas del día de todos los planes y ocasiones en una sola
tabla superior, ordenada por estado y vencimiento como el Tablero Vivo. Las
ocasionales activas aparecen junto con las otras tareas verdes. Cada fila indica
su plan o asignación de origen; las próximas tareas y el historial continúan
agrupados por plan abajo. Una tarea se muestra una sola vez y los colores se
actualizan mientras la pantalla permanece abierta.
El responsable puede entregarlas desde su inicio; el servidor exige el requisito
capturado al crear el plan y una conclusión. La entrega pasa a pendiente de revisión.
Los archivos tienen un máximo de 15 MB, pasan por el
validador técnico existente y ClamAV y se guardan en PostgreSQL como evidencias
privadas. El responsable y los supervisores con alcance sobre el rol asignado pueden descargarlos.
Entregar una tarea libera su carga para los planes siguientes. Los planes existentes se redistribuyen cuando un supervisor aplica un ajuste de capacidad con vista previa.

## Supervisión

Dirección ve todos los roles, Administración ve Subcoordinación y Piso de ventas,
y Subcoordinación ve Piso de ventas. Piso de ventas no tiene acceso. Los permisos
se verifican también al revisar una tarea o descargar su evidencia.

La pantalla conserva `/validaciones`, con el nombre Supervisión y un icono de ojo.
Las entregas pendientes aparecen primero, seguidas de todas las asignaciones
por orden jerárquico, incluidas las programadas, vencidas y revisadas. Cada
apartado filtra de forma independiente por semana, mes, trimestre, semestre,
año o fechas específicas; tarea, periodicidad, estado, requisito, persona,
plan, repeticiones, plazo y carga. Los rangos usan la fecha de inicio en Ciudad
de México. Periodicidad y repeticiones se conservan al generar la asignación.

Al abrir una tarea se muestra su requisito, evidencia y conclusión. El supervisor
puede concluirla o marcarla como no concluida con comentario obligatorio, eligiendo
reasignar a la misma persona, repartir dentro del rol o cerrar como no satisfecha.
Una reasignación crea una nueva asignación, conserva la entrega anterior y su
decisión, y renueva el plazo desde ese momento. El reparto usa personas activas
disponibles y su carga pendiente. Las revisiones se auditan y se protegen contra
decisiones simultáneas mediante revisión y bloqueo transaccional.

Editar, desactivar o eliminar una definición afecta a planes nuevos. La
eliminación oculta el registro y mantiene las asignaciones y evidencias previas.
Los parámetros de esas asignaciones son copias históricas. Los planes nuevos
usan estas tablas operativas; las obligaciones y los reportes del flujo anterior
se conservan. Los borradores anteriores no se convierten ni asignan retroactivamente.

## Verificación

`tools/Sgol.TaskChecks` ejecuta pruebas contra una base PostgreSQL vacía en
localhost cuyo nombre empieza por `sgol_task_checks_`. La conexión se recibe
mediante `SGOL_TASK_CHECKS_CONNECTION`. Aplica las migraciones y crea personas
ficticias exclusivamente en esa base. Verifica autorización, frecuencias,
balance, carga previa, idempotencia, indisponibilidad, atomicidad, cierre,
versiones del catálogo, desactivación, eliminación y validación de fotografías.
El escáner se simula para probar tanto archivos limpios como infectados.

La opción `--preview` abre un servidor temporal en los puertos 7251/7252 con una
sesión ficticia de Dirección para revisar las Razor Pages en la base aislada.
Este servidor se detiene con Ctrl+C y no forma parte de la aplicación operativa.

## Calendario, progreso y capacidad

El catálogo admite distribución por calendario, días semanales fijos y progreso semanal.
La hora de apertura se configura de 0 a 23 en Ciudad de México. Las futuras
asignaciones aparecen en «Próximas tareas» y no permiten entregar evidencias,
registrar avances ni cerrar desde supervisión antes de la apertura. La modalidad
«personas distintas» reserva cada persona para una repetición de la misma tarea y semana.
Si no hay suficientes candidatos, el plan se rechaza sin generar asignaciones parciales.

Las cuotas mensuales mantienen sus fechas ancladas al mes al incluirse en planes
semanales. Una clave por definición, fecha y repetición impide duplicarlas entre
planes superpuestos. Los planes históricos sin clave también reservan sus fechas.
Un nuevo plan que únicamente duplicaría repeticiones se rechaza. Un plan con
definiciones sin fechas en su intervalo puede quedar sin asignaciones; conserva
las definiciones seleccionadas. Desde el detalle de un plan vigente, Dirección
puede agregar tareas sin recrearlo. Las fechas pasadas conservan el vencimiento.

Las tareas pasivas tienen una meta semanal y una unidad editable. El trabajador
registra incrementos con una descripción. El servidor valida propietario,
apertura, revisión, incremento positivo y límite de la meta; guarda cada avance
y lo audita. Solo se envía la evidencia final al completar la meta. La carga
restante se reduce proporcionalmente y se distribuye entre los días disponibles
hasta el cierre de la semana. El supervisor puede consultar el historial.

Supervisión muestra carga y capacidad de colaboradores activos dentro del alcance
jerárquico, excluyendo ajustes sobre uno mismo. La base inicial es 120 minutos
operativos por día, configurable entre 1 y 480; no se interpreta el turno libre
como horario. Los días sin disponibilidad registrada se consideran disponibles.
El porcentaje puede bajar hasta 0 y tiene una vigencia desde hoy de hasta 91 días.
Al acabar el intervalo vuelve a regir la capacidad anterior o la base inicial.
Un ajuste posterior puede reemplazar parte del intervalo conservando sus extremos.

La vista previa calcula ocupación por minutos pendientes/capacidad y verifica
que el receptor tenga capacidad en cada día de ejecución. Primero mueve tareas
futuras sin avances, luego activas sin avances. Las tareas con progreso quedan
protegidas salvo que el supervisor habilite su transferencia expresamente.
Siempre se conserva rol, fecha, vencimiento, requisitos, evidencias y progreso.
Las obligaciones del flujo anterior aportan 30 minutos cada una y no se transfieren
por esta pantalla. La carga que no se pueda repartir se muestra como exceso.

Aplicar recalcula bajo el bloqueo transaccional de sucursal y compara la huella
de la vista previa. Si cambió la carga, disponibilidad o capacidad, se rechaza
sin cambios parciales y exige calcular otra vista previa. Los cambios se auditan.
Subir nuevamente la capacidad no devuelve tareas ya transferidas. La creación
de planes y la reasignación de supervisión usan el mismo balance por capacidad.
Los ajustes nuevos no reescriben los calendarios ni requisitos de asignaciones anteriores.
