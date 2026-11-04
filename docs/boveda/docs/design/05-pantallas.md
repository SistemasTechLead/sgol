---
categoria: diseno
tipo: referencia
estado_documental: inspeccionado
clasificado_el: 2026-11-03
---

# Pantallas actuales de SGOL

[Diseño actual](00-diseno-sgol.md) · [Navegación](02-estructura-y-navegacion.md) · [Componentes](03-componentes.md) · [Patrones y estados](04-patrones-y-estados.md)

## Acceso y sesión

El acceso de escritorio tiene dos paneles: identidad Loretta y formulario. La marca incluye logo, versión del ensamblado y la ubicación Delicias, Chihuahua; la bienvenida usa Georgia. El formulario usa Poppins, etiquetas visibles, ayuda, usuario, contraseña, Mostrar/Ocultar y la acción Iniciar sesión.

El login móvil reúne marca y ubicación, título Acceso a SGOL, formulario y versión al final. La columna útil tiene hasta 360 px y márgenes con áreas seguras. Se centra verticalmente cuando cabe y permite desplazamiento documental cuando falta altura. Los campos y la acción principal tienen un mínimo de 48 px.

La misma ruta presenta cambio de contraseña temporal, enrolamiento MFA, clave de configuración, verificación, códigos de recuperación y finalización según el estado de autenticación. Estos pasos tienen su contenido y acciones propios; no se les aplica la composición compacta exclusiva del estado login. Los avisos y errores se muestran antes del formulario y reciben foco mediante el controlador común.

Fuente: [Access/Index.cshtml](../../../../src/Sgol.Web/Pages/Access/Index.cshtml), [mobile.css](../../../../src/Sgol.Web/wwwroot/css/mobile.css).

## Mi trabajo

La búsqueda de asignaciones aparece directamente, con Buscar y Limpiar; aplica el patrón de [buscador único](03-componentes.md#filtros).

La pantalla combina navegación de apartados, bono de la semana actual, consulta de tareas y progreso diario. La ayuda de cabecera reúne explicación de orden y colores. El listado muestra tarea, inicio, vencimiento, carga, requisito, estado y acción de evidencia. Incluye contexto de plan, especialización, criticidad y avance cuando corresponde.

En móvil, la tarea prioriza nombre, estado, vencimiento, requisito y acción; los detalles adicionales se despliegan. Abrir una tarea operativa muestra un diálogo con descripción, evidencia, conclusión, avance, decisiones e historial según su situación. Una entrega enviada ofrece consulta; una tarea habilitada ofrece captura o avance. Las fotografías también tienen una página de galería.

Mis procesos presenta ejecuciones, etapa activa, responsables, carga y estados de las etapas. El formulario de la etapa expone la acción aplicable; el historial conserva rondas y versiones de documentos. El detalle de obligación documental contiene versiones, aportación/sustitución, conclusión, revisión e historia.

Petición de arrastre ofrece selección de vencidas, propuesta de posposición y cargas usadas para calcularla. El incidente del día ocupa un desplegable separado. Mis peticiones muestra estados e historia con detalles secundarios plegados.

Fuentes: [Mi trabajo](../../../../src/Sgol.Web/Pages/MyWork/Index.cshtml), [tabla](../../../../src/Sgol.Web/Pages/MyWork/_OperationalTaskTable.cshtml), [diálogo](../../../../src/Sgol.Web/Pages/MyWork/_OperationalTaskDialog.cshtml), [procesos](../../../../src/Sgol.Web/Pages/MyWork/Processes.cshtml), [arrastre](../../../../src/Sgol.Web/Pages/MyWork/Carryover.cshtml).

## Tareas y asignación ocasional

La búsqueda de ocasionales en Asignar tarea aparece directamente, según el patrón de [buscador único](03-componentes.md#filtros).

El catálogo tiene navegación, Actualizar Asignaciones, filtros combinables y tabla compacta. La identidad de la tarea reúne nombre, código y descripción; la selección habilita acciones contextuales. Los filtros cubren búsqueda/proceso, requisito, periodicidad, repeticiones, criticidad, rol, estado y calendario. El contenido descriptivo utiliza Markdown de lectura.

Crear/editar organiza el formulario en datos, evidencia, calendario/plazo y carga/opciones. Los campos de días, recurrencia, fotografías, checklist y progreso dependen de la configuración seleccionada. La pantalla de porcentaje de cierre está en el mismo módulo y explica el alcance del cambio sobre nuevas operaciones.

Actualizar Asignaciones presenta casos compactos con tarea, causa y solución; luego muestra resultados agrupados y confirmación. La selección de excepciones y los detalles de asignaciones permanecen disponibles antes de aplicar.

Asignar tarea lista las ocasionales disponibles y permite crear una nueva según permisos. El formulario de asignación reúne información, responsables y fechas, ofrece horarios propuestos y muestra Confirmar asignación cuando hay propuesta preparada.

Fuentes: [catálogo](../../../../src/Sgol.Web/Pages/Tasks/Index.cshtml), [actualización](../../../../src/Sgol.Web/Pages/Tasks/Refresh.cshtml), [ocasionales](../../../../src/Sgol.Web/Pages/Occasional/Index.cshtml), [asignación](../../../../src/Sgol.Web/Pages/Tasks/Assign.cshtml).

## Catálogo de procesos

La búsqueda para añadir tareas al proceso aparece directamente, según el patrón de [buscador único](03-componentes.md#filtros).

La consulta muestra nombre, descripción y situación de publicación/habilitación. El editor reúne datos, calendario de inicio y tareas ordenadas. Las tarjetas de etapa abren un diálogo de edición; añadir tareas usa un selector con búsqueda. Las opciones avanzadas agrupan inicio mínimo y devolución. Los ítems compartidos y sus tiempos tienen una pantalla propia.

El editor distingue guardar borrador, publicar, habilitar nuevos inicios, abandonar cambios y eliminar. Presenta instrucciones de las etapas, enlaces a planificación y consecuencias de las acciones cuando corresponden. General identifica las tareas independientes.

Fuentes: [editor de procesos](../../../../src/Sgol.Web/Pages/Tasks/Processes.cshtml), [ítems](../../../../src/Sgol.Web/Pages/Tasks/ProcessItems.cshtml), [processes.css](../../../../src/Sgol.Web/wwwroot/css/processes.css).

## Planificación

Planes muestra la consulta y los detalles. Crear nuevo plan y Operaciones semanales reúnen periodo, selección de tareas, capacidad y resultado preparado. Los conflictos pueden abrir una revisión contextual; los cambios de fecha y capacidad pendientes aparecen antes de confirmar y pueden deshacerse. El detalle presenta tareas, asignaciones, progreso y procesos incluidos cuando existen.

Cobertura de cierre se abre desde el botón junto a la navegación. El diálogo muestra día, responsable, horario resultante y elección de cobertura. Las matrices de carga conservan desplazamiento y tratamiento de filas/columnas adaptado al espacio disponible.

Plan de proceso selecciona una definición publicada y habilitada, nombre/fechas y responsables por tarea. Las instrucciones se consultan en diálogos. Ver fechas y capacidad prepara el resultado; Guardar plan preparado y Activar plan son pasos distintos. La eliminación ofrece las consecuencias y, cuando corresponde, la descarga de archivos antes de confirmar.

La ruta de versiones del calendario sigue implementada con consulta de releases, creación de borrador y publicación condicionadas al permiso correspondiente. No es un destino principal de la navegación compartida.

Fuentes: [planes](../../../../src/Sgol.Web/Pages/Planning/_DirectionPlans.cshtml), [cobertura](../../../../src/Sgol.Web/Pages/Planning/_Closing.cshtml), [plan de proceso](../../../../src/Sgol.Web/Pages/Planning/Processes.cshtml), [versiones](../../../../src/Sgol.Web/Pages/Planning/Releases.cshtml).

## Supervisión y Validación

Incidencias presenta primero Registrar ausencia para Dirección, con fecha, colaboradora, motivo, revisión de efectos y confirmación conjunta. Debajo aparece Consultar incidencias, con filtros compactos por fecha/nombre y filas paginadas de disponibilidad, pendientes/vencidas/minutos y recibos. La vista previa agrupa primero las tareas que requieren decisión, después reasignaciones y otros cambios y al final los retiros por ausencia. Las tareas usan filas compactas con historial desplegable. El grupo Opciones de transferencia (destinataria y sobrecarga) se muestra solo al elegir Transferir; al retirar o quitar la decisión, sus controles se limpian, ocultan y deshabilitan, quedando fuera del envío y del recorrido de teclado. El selector de destinataria excluye incompatibilidades de rol/especialidad, ausencia y falta de horario suficiente; conserva personas elegibles con falta de capacidad para revisar sobrecarga explícita. La selección anterior inválida se descarta con aviso. Reparto automático admite sobrecarga explícita por tarea y muestra la destinataria concreta y su exceso en la vista previa. Las transferencias vencidas permiten elegir Retirar por ausencia. Los conflictos siguen tarea, causa, efecto y detalles; cambiar decisiones invalida la propuesta. Un rechazo conserva las decisiones para volver a revisar. Los mensajes distinguen cambios de estado de vencimientos e inicios pasados; los límites temporales identifican la tarea. La composición usa controles y paneles existentes, con adaptación móvil, temas y teclado. Véase [la regla funcional](../funcional/supervision.md#incidencias-de-ausencia).

El recibo de Incidencias agrupa la evidencia conservada por tarea en desplegables accesibles con teclado. Cada enlace descarga el archivo y muestra su fecha, autora cuando existe y estado de entrega o captura sin entregar. Solo aparecen archivos anteriores a la confirmación dentro del alcance del supervisor.

Supervisión presenta capacidad, peticiones de arrastre y apartados de tareas con filtros y acciones. La posición de arrastres respecto a pendientes depende de si hay peticiones autorizadas por resolver. Supervisar/Ver abre el detalle con descripción, evidencia, conclusión, avance y decisión. Las correcciones incluyen programación y propuesta antes de confirmar.

Validación utiliza tarjetas de evidencia. Cuando la entrega contiene un comentario de conclusión, la tarjeta lo muestra bajo «Comentario de conclusión», independientemente del requisito de evidencia. Conserva los saltos de línea, trata el contenido como texto y omite el bloque cuando no hay conclusión. Cada tarjeta identifica tarea, responsable, fechas, estado y contenido entregado, junto a la decisión editable cuando procede. Las galerías tienen navegación entre fotos; los errores se colocan en la tarjeta afectada. La barra superior reúne apartado y filtros desplegables. El envío de revisión por lote conserva la fase de propuesta/confirmación. Anterior/Siguiente aparece solo cuando corresponde.

Las peticiones de posposición se organizan por solicitud y tarea. El editor en contexto muestra decisión, destinatario o día cuando aplica, motivo, obstáculos y efecto previsto. La historia separa cabecera resumida de datos secundarios. Carga y reasignación agrupa por persona, muestra minutos/capacidad y prepara una transferencia con avance e historial.

Porcentaje para el bono permite elegir semana y examinar a cada persona con velocímetro y detalle de tareas, base, descuento y motivo.

Fuentes: [Supervisión](../../../../src/Sgol.Web/Pages/Validations/Index.cshtml), [tarjetas](../../../../src/Sgol.Web/Pages/Validations/_ValidationCards.cshtml), [peticiones](../../../../src/Sgol.Web/Pages/Validations/_PostponementRequest.cshtml), [carga](../../../../src/Sgol.Web/Pages/Validations/Workload.cshtml), [bono](../../../../src/Sgol.Web/Pages/Validations/Bonus.cshtml).

## Tablero y Rol de tareas

Tablero muestra contexto de fecha y conexión, filtros por plan/tarea/estado, progreso diario y semanal, progreso por colaborador y tarjetas de tareas. El área de tareas permite desplazamiento por teclado. Los colores indican situación temporal y entrega, con etiquetas y leyenda.

En móvil, Tablero y Rol de tareas comparten una fila de destinos; Filtros y Personas se sitúan debajo. Las tarjetas se leen en una columna. Rol de tareas muestra semana, actividad, carga y asignaciones por día, con persona y estado; ofrece semana anterior, actual y siguiente.

La sesión ordinaria utiliza el layout de aplicación y puede mostrar bono por persona. La pantalla dedicada tiene layout propio oscuro, navegación entre las dos vistas y estado de conexión; no ofrece los controles de la cuenta ordinaria ni el bono interactivo.

Fuentes: [tablero](../../../../src/Sgol.Web/Pages/Board/_BoardContent.cshtml), [semana](../../../../src/Sgol.Web/Pages/Board/_WeeklyContent.cshtml), [layout dedicado](../../../../src/Sgol.Web/Pages/Shared/_BoardLayout.cshtml), [live-board.css](../../../../src/Sgol.Web/wwwroot/css/live-board.css).

## Personas y accesos

La consulta presenta perfiles con avatar, rol, estado y acciones; identifica el perfil propio. Crear/editar reúne identidad, acceso y especializaciones según permisos. Horario laboral, excepciones y cobertura usan sus formularios. La contraseña temporal se presenta como resultado específico cuando corresponde.

El detalle personal utiliza el nombre como título de pantalla y paneles de empleo, vigencia, disponibilidad e historial laboral. Dar de baja y reactivar tienen confirmación. La pantalla de especializaciones permite editar y consultar personas, tareas y asignaciones pendientes relacionadas.

La ficha de sucursal permanece como ruta de consulta implementada y restringida al ámbito de administración de personas; no figura como destino principal independiente.

Fuentes: [personas](../../../../src/Sgol.Web/Pages/People/Index.cshtml), [detalle](../../../../src/Sgol.Web/Pages/People/Details.cshtml), [horarios](../../../../src/Sgol.Web/Pages/People/_Schedule.cshtml), [especializaciones](../../../../src/Sgol.Web/Pages/People/Specializations.cshtml), [sucursal](../../../../src/Sgol.Web/Pages/Branches/Details.cshtml).

## Reportes

La búsqueda dentro del detalle aparece directamente, según el patrón de [buscador único](03-componentes.md#filtros); conserva los parámetros ocultos del reporte.

La pantalla reúne tipo de reporte, periodo, filtros y presentación. Más filtros y presentación incluye opciones adicionales y columnas del detalle/PDF. Generar reporte aplica la consulta; un aviso identifica cambios aún no aplicados.

El resultado muestra nombre, descripción, periodo, corte y alcance, indicadores, gráficos y detalle con búsqueda. Las filas interactivas de gráficos tienen tratamiento de control. Las plantillas personales se guardan o eliminan mediante diálogo. Exportar a PDF abre opciones de contenido y vista correspondiente; la búsqueda del detalle no redefine por sí sola la exportación.

En móvil se prioriza el resultado y se pliega configuración secundaria. Los controles originales de plantilla, filtros y exportación mantienen su comportamiento.

Fuentes: [Reportes](../../../../src/Sgol.Web/Pages/Reports/Index.cshtml), [reports.js](../../../../src/Sgol.Web/wwwroot/js/reports.js), [reports.css](../../../../src/Sgol.Web/wwwroot/css/reports.css).

## Configuración

Tu avatar contiene vista previa, estilo, paleta y Generar otro, con Guardar avatar. Hay inicial y estilos Mármol, Caritas, Píxeles, Atardecer, Anillos y Bauhaus. La elección pertenece a la cuenta y puede mostrarse en otros dispositivos.

Apariencia ofrece Claro, Oscuro y Especial. La opción llamada Versión de la aplicación selecciona la composición Escritorio, Móvil o Automática; no cambia la versión del software. Tema y composición se aplican al instante y se guardan en el navegador, con mensaje que informa si el almacenamiento falló.

Fuente: [Configuración](../../../../src/Sgol.Web/Pages/Configuration/Index.cshtml).

## Errores y entrada

La entrada `/` redirige según la sesión o muestra Sesión no disponible. `/pantalla` presenta Conectando pantalla durante la recuperación del acceso dedicado. Los errores de sistema muestran símbolo, título, explicación, ayuda, destino de recuperación y detalles para soporte; conservan contexto de sesión cuando está disponible.

Fuentes: [entrada](../../../../src/Sgol.Web/Pages/Entry.cshtml), [pantalla](../../../../src/Sgol.Web/Pages/Screen/Index.cshtml), [error de sistema](../../../../src/Sgol.Web/Pages/Shared/_SystemError.cshtml).

## Inventario de rutas

Las rutas siguientes tienen una página Razor en el código revisado. La existencia de la ruta no implica que todos los roles puedan abrirla ni que todas aparezcan en el menú.

| Ruta | Vista propietaria |
|---|---|
| `/` | [Pages/Entry.cshtml](../../../../src/Sgol.Web/Pages/Entry.cshtml) |
| `/acceso` | [Pages/Access/Index.cshtml](../../../../src/Sgol.Web/Pages/Access/Index.cshtml) |
| `/asignar-tarea` | [Pages/Occasional/Index.cshtml](../../../../src/Sgol.Web/Pages/Occasional/Index.cshtml) |
| `/branches/{branchCode}` | [Pages/Branches/Details.cshtml](../../../../src/Sgol.Web/Pages/Branches/Details.cshtml) |
| `/configuracion` | [Pages/Configuration/Index.cshtml](../../../../src/Sgol.Web/Pages/Configuration/Index.cshtml) |
| `/mi-trabajo` | [Pages/MyWork/Index.cshtml](../../../../src/Sgol.Web/Pages/MyWork/Index.cshtml) |
| `/mi-trabajo/arrastre` | [Pages/MyWork/Carryover.cshtml](../../../../src/Sgol.Web/Pages/MyWork/Carryover.cshtml) |
| `/mi-trabajo/fotografias/{id:guid}` | [Pages/MyWork/Photographs.cshtml](../../../../src/Sgol.Web/Pages/MyWork/Photographs.cshtml) |
| `/mi-trabajo/procesos` | [Pages/MyWork/Processes.cshtml](../../../../src/Sgol.Web/Pages/MyWork/Processes.cshtml) |
| `/mi-trabajo/tareas/{obligationId:guid}` | [Pages/MyWork/Details.cshtml](../../../../src/Sgol.Web/Pages/MyWork/Details.cshtml) |
| `/pantalla` | [Pages/Screen/Index.cshtml](../../../../src/Sgol.Web/Pages/Screen/Index.cshtml) |
| `/personas-y-accesos` | [Pages/People/Index.cshtml](../../../../src/Sgol.Web/Pages/People/Index.cshtml) |
| `/personas-y-accesos/especializaciones` | [Pages/People/Specializations.cshtml](../../../../src/Sgol.Web/Pages/People/Specializations.cshtml) |
| `/personas-y-accesos/personas/{personId:guid}` | [Pages/People/Details.cshtml](../../../../src/Sgol.Web/Pages/People/Details.cshtml) |
| `/planificacion` | [Pages/Planning/Index.cshtml](../../../../src/Sgol.Web/Pages/Planning/Index.cshtml) |
| `/planificacion/procesos` | [Pages/Planning/Processes.cshtml](../../../../src/Sgol.Web/Pages/Planning/Processes.cshtml) |
| `/planificacion/versiones` | [Pages/Planning/Releases.cshtml](../../../../src/Sgol.Web/Pages/Planning/Releases.cshtml) |
| `/reportes` | [Pages/Reports/Index.cshtml](../../../../src/Sgol.Web/Pages/Reports/Index.cshtml) |
| `/supervision/bono-tareas` | [Pages/Validations/Bonus.cshtml](../../../../src/Sgol.Web/Pages/Validations/Bonus.cshtml) |
| `/supervision/incidencias` | [Pages/Validations/Incidents.cshtml](../../../../src/Sgol.Web/Pages/Validations/Incidents.cshtml) |
| `/supervision/carga` | [Pages/Validations/Workload.cshtml](../../../../src/Sgol.Web/Pages/Validations/Workload.cshtml) |
| `/tablero` | [Pages/Board/Index.cshtml](../../../../src/Sgol.Web/Pages/Board/Index.cshtml) |
| `/tablero/rol-de-tareas` | [Pages/Board/Weekly.cshtml](../../../../src/Sgol.Web/Pages/Board/Weekly.cshtml) |
| `/tareas` | [Pages/Tasks/Index.cshtml](../../../../src/Sgol.Web/Pages/Tasks/Index.cshtml) |
| `/tareas/actualizar` | [Pages/Tasks/Refresh.cshtml](../../../../src/Sgol.Web/Pages/Tasks/Refresh.cshtml) |
| `/tareas/asignar` | [Pages/Tasks/Assign.cshtml](../../../../src/Sgol.Web/Pages/Tasks/Assign.cshtml) |
| `/tareas/procesos` | [Pages/Tasks/Processes.cshtml](../../../../src/Sgol.Web/Pages/Tasks/Processes.cshtml) |
| `/tareas/procesos/items` | [Pages/Tasks/ProcessItems.cshtml](../../../../src/Sgol.Web/Pages/Tasks/ProcessItems.cshtml) |
| `/validaciones` | [Pages/Validations/Index.cshtml](../../../../src/Sgol.Web/Pages/Validations/Index.cshtml) |

## Ubicación documental

Pertenece a [Diseño e interacción](../../mapas/diseno.md). Estado documental: **inspeccionado**. La fecha de clasificación no es una fecha de verificación funcional.
