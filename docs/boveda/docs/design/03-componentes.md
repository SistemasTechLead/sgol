---
categoria: diseno
tipo: referencia
estado_documental: inspeccionado
clasificado_el: 2026-11-03
---

# Componentes de interfaz de SGOL

[Diseño actual](00-diseno-sgol.md) · [Fundamentos](01-fundamentos-visuales.md) · [Patrones](04-patrones-y-estados.md) · [Interacción accesible](06-interaccion-accesible.md)

## Acciones y desplegables

| Componente | Presentación y comportamiento |
|---|---|
| Botón primario | Fondo de acento y texto contrastado; destaca la acción principal del grupo. |
| Botón secundario | Superficie neutra y borde; cancelar, consultar o actuar sin énfasis principal. |
| Acción textual o terciaria | Los estilos finales le dan superficie, borde y sombra discreta. El nombre de la clase no implica que sea visualmente transparente. |
| Destructivo | Variante de peligro disponible para acciones como eliminar perfiles. Algunos formularios de eliminación utilizan actualmente botón primario y explicación de consecuencias; no existe uniformidad total de esta variante. |
| Destino de apartado | Enlace de navegación, estado activo de acento y `aria-current`. |
| Desplegable | `details/summary`, superficie neutra, ancho del contenido y flecha junto al texto; la flecha cambia de orientación al abrir. |
| Más datos | Control de los registros móviles; cambia a Menos datos cuando está abierto. |

Los botones comunes tienen mínimo de 44 px de alto, foco visible y texto que puede refluir. Los deshabilitados presentan menor énfasis y cursor correspondiente. Los enlaces dentro de prosa conservan su tratamiento de enlace.

Fuentes: [navigation.css](../../../../src/Sgol.Web/wwwroot/css/navigation.css), [interactive-controls.css](../../../../src/Sgol.Web/wwwroot/css/interactive-controls.css).

## Campos y grupos de formulario

`_FormField` reúne etiqueta y entrada o selector, valor, deshabilitado, carga y mensaje de error asociado. `_TextArea`, `_Checkbox`, `_RadioGroup` y `_LocalDateField` cubren los tipos comunes. También hay campos declarados directamente en las vistas con las clases compartidas.

Los grupos de opciones emplean `fieldset` y `legend`. Los errores de campo se acompañan de `aria-invalid` y de una descripción cuando usa el componente. `_CredentialField` distingue contraseña y código de autenticación; Mostrar/Ocultar cambia el tipo de entrada y su estado `aria-pressed`.

La carga de archivo tiene presentación específica; la evidencia fotográfica utiliza controles de cámara, previsualización, repetición de captura y selección de tomas. El checklist distingue editor de definición, respuesta editable y evidencia de consulta.

Fuentes: [parciales compartidos](../../../../src/Sgol.Web/Pages/Shared), [comportamiento común](../../../../src/Sgol.Web/wwwroot/js/components.js), [checklist](../../../../src/Sgol.Web/wwwroot/js/checklist.js), [captura](../../../../src/Sgol.Web/wwwroot/js/camera-capture.js).

## Paneles, tablas y listas

Los paneles reúnen cabecera y contenido sobre una superficie con borde. Las tablas usan contenedor desplazable; `_DataTable` proporciona caption, encabezados, acciones, estado vacío, carga y paginación. Las tablas escritas directamente en las pantallas tienen variantes de columnas y densidad.

En móvil, las tablas elegidas por el adaptador conservan sus elementos y asocian las celdas con etiquetas para lectura vertical. Los datos secundarios se pliegan mediante Más datos. Las matrices de calendario y carga tienen adaptación propia. La tabla de tareas reduce el relleno en escritorio y agrupa identidad, código y control de descripción; la barra contextual responde a la tarea seleccionada. La descripción abierta ocupa una franja de ancho completo bajo la fila en escritorio, con hasta tres columnas de ancho objetivo 22rem y sin recortar contenido. En móvil vuelve al desplegable original en una columna. Sin JavaScript conserva el desplegable dentro de la celda.

La paginación puede ser Anterior/Siguiente, número de página o ampliación de la cantidad visible según la consulta. Validación muestra Anterior/Siguiente cuando existen varias páginas; Supervisión tiene su lógica de carga de más registros. No se impone un único patrón de paginación a todos los módulos.

Fuentes: [_DataTable](../../../../src/Sgol.Web/Pages/Shared/_DataTable.cshtml), [_ListPage](../../../../src/Sgol.Web/Pages/Shared/_ListPage.cshtml), [adaptación móvil](../../../../src/Sgol.Web/wwwroot/js/mobile.js), [barra de tareas](../../../../src/Sgol.Web/wwwroot/css/task-actions.css).

## Filtros

Cuando el formulario contiene únicamente un campo de búsqueda, el campo y sus acciones aparecen directamente, sin desplegable de filtros en móvil ni en escritorio. Los parámetros ocultos conservan el contexto y no cuentan como filtros adicionales. Esta regla se aplica a Mi trabajo, la búsqueda de ocasionales en Asignar tarea, la búsqueda para añadir tareas a un proceso y el detalle de Reportes. Los formularios con varios filtros y los selectores de fecha, semana o estado conservan su presentación específica.

La barra de filtros de escritorio separa la consulta de la navegación con superficie, borde, rótulo y grupos desplegables. Los campos originales siguen perteneciendo al formulario. El resumen del grupo muestra la etiqueta y, cuando procede, el valor seleccionado. Abrir un grupo no lo expande automáticamente a ocupar el espacio sobrante de la fila.

Los grupos comunes tienen un límite de 24 rem; el contenido de campo usa 18 rem, 14 rem para fechas y 12 rem para números, siempre limitado al ancho disponible. Los botones Filtrar/Aplicar y Limpiar conservan su ancho de contenido. Validación dispone de una barra propia con apartado, tarea, fechas, periodicidad, estado, requisito, responsable, plan y parámetros.

Fuentes: [desktop-workspace.js](../../../../src/Sgol.Web/wwwroot/js/desktop-workspace.js), [filtros de Validación](../../../../src/Sgol.Web/Pages/Validations/_ValidationFilters.cshtml), [estilos finales](../../../../src/Sgol.Web/wwwroot/css/interactive-controls.css).

## Ayuda y mensajes

Las pantallas operativas no muestran ayuda general de cabecera ni su botón de información. Los errores, instrucciones de campos y consecuencias de confirmaciones permanecen en su contexto operativo.

`_ProblemAlert` presenta título, explicación y detalles técnicos desplegables cuando existe identificador. `_SuccessAlert` anuncia el resultado con `role="status"`. `_EmptyState` reúne título, explicación opcional y acción opcional. `_ValidationSummary` concentra errores de formulario. `_StatusBadge` combina icono y texto con una variante semántica.

Fuentes: [presentación operativa](../../../../src/Sgol.Web/wwwroot/css/desktop-workspace.css), [_ProblemAlert](../../../../src/Sgol.Web/Pages/Shared/_ProblemAlert.cshtml), [_EmptyState](../../../../src/Sgol.Web/Pages/Shared/_EmptyState.cshtml).

## Diálogos

Los diálogos nativos usan título, contenido, cierre o cancelación y acciones. La base modal tiene una anchura de referencia de 480 px; Supervisión utiliza hasta 680 px con límites de viewport. Procesos, reportes y bono definen sus propias dimensiones. El contenido largo desplaza dentro del diálogo cuando lo exige su variante.

El mecanismo compartido abre por `data-dialog-open`, enfoca `data-initial-focus` y devuelve el foco al activador al cerrar. Algunos módulos tienen controladores específicos. Los formularios fallidos pueden reabrir el diálogo y enfocar su error. El diálogo de navegación móvil implementa recorrido circular de Tab y Shift+Tab.

Fuentes: [components.js](../../../../src/Sgol.Web/wwwroot/js/components.js), [components.css](../../../../src/Sgol.Web/wwwroot/css/components.css), [_MotivatedConfirmation](../../../../src/Sgol.Web/Pages/Shared/_MotivatedConfirmation.cshtml).

## Evidencias y contenido enriquecido

Los campos de mínimo y máximo de imágenes del catálogo y de tareas ocasionales admiten 1–50. La captura y las galerías utilizan el límite de la asignación; los registros anteriores no adoptan 50 automáticamente.

Las descripciones e instrucciones se presentan mediante `_Markdown`. Admite formato de texto, listas y tablas; el HTML está deshabilitado, las imágenes se representan mediante su texto alternativo y los enlaces activos se limitan a HTTP, HTTPS y correo. La edición conserva el texto Markdown original.

La presentación de evidencia distingue comentario, documento PDF, Excel, fotografía, captura de pantalla, checklist y ausencia de requisito. Las galerías permiten recorrer imágenes y abrirlas completas. Los recorridos documentales muestran versiones, revisión y sustitución sin presentar una versión anterior como la actual.

Fuentes: [MarkdownContent](../../../../src/Sgol.Web/Presentation/Components/MarkdownContent.cs), [_OperationalPhotoGallery](../../../../src/Sgol.Web/Pages/MyWork/_OperationalPhotoGallery.cshtml), [_EvidenceVersions](../../../../src/Sgol.Web/Pages/MyWork/_EvidenceVersions.cshtml).

## Avatar, progreso y bono

El avatar muestra la inicial cuando no hay personalización. Configuración ofrece estilos generados, paleta y variación; Guardar avatar persiste la elección en la cuenta. El componente aparece junto a personas y en el tablero.

`_TaskProgress` representa tareas completadas frente al total. Las tareas por unidades utilizan un indicador de avance independiente. Las barras de capacidad comparan minutos programados y disponibles. Estos indicadores no son el bono.

`_TaskBonusGauge` muestra un velocímetro, porcentaje e icono de información dentro de un botón. Su diálogo reúne resultado, cálculo, periodo/cierre y puntos/entregas. Cuando hay valor calculable se expone como medidor de 0 a 100; la ausencia de resultado no se representa como un cero calculado. La variante compacta se utiliza en el tablero con sesión normal. Mi trabajo consulta la semana actual; Supervisión conserva consulta por semana.

Fuentes: [_PersonAvatar](../../../../src/Sgol.Web/Pages/Shared/_PersonAvatar.cshtml), [_TaskProgress](../../../../src/Sgol.Web/Pages/Shared/_TaskProgress.cshtml), [_TaskBonusGauge](../../../../src/Sgol.Web/Pages/Shared/_TaskBonusGauge.cshtml), [task-bonus.css](../../../../src/Sgol.Web/wwwroot/css/task-bonus.css).

## Ubicación documental

Pertenece a [Diseño e interacción](../../mapas/diseno.md). Estado documental: **inspeccionado**. La fecha de clasificación no es una fecha de verificación funcional.
