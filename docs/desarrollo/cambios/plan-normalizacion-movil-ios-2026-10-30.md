# Plan de ajuste y normalización móvil — 30/10/2026

Estado: diseño y plan aprobados por el usuario el 30/10/2026, con las precisiones de alcance y ayuda compacta incorporadas en este documento. Implementación incorporada al árbol de trabajo y cargada en el entorno compartido con autorización posterior; véase el registro final. La validación ejecutada y sus límites se registran al final; las simulaciones siguen siendo referencias con datos ficticios.

## Objetivo y límites

Reducir altura innecesaria, alinear ayudas y establecer una jerarquía reconocible de acciones en la vista móvil. Conservar identidad Loretta, Poppins, tokens, rutas, formularios, autorizaciones, filtros, selección, confirmaciones e historial actuales. Inspiración iOS en composición, agrupación y claridad; no copiar su color azul, tipografía, efectos de cristal ni controles nativos.

Clasificación: interfaz, con riesgo de interacción al mover controles y plegar acciones. No se proponen cambios de negocio, base de datos, cargas, responsables o plazos. Alcance actual: implementación y validación autorizadas. El alcance inicial no incluía reinicio ni despliegue; el usuario autorizó posteriormente cargar la revisión compartida. Commit y push siguen sin autorización.

El alcance de la implementación prevista incluye **todas las pantallas existentes del sistema en su vista móvil**, no solamente las tres simuladas. Es obligatorio recorrer el inventario completo, aplicar los patrones compartidos que correspondan y registrar por pantalla qué se cambió o por qué un patrón no aplica. También cubre variantes por rol, consulta, creación/edición/detalle, diálogos y estados de error, vacío, carga y éxito. Las diferencias de propósito entre tablas, calendarios, formularios y quiosco deben conservarse; no constituyen exclusiones de la revisión móvil.

Aplicar dentro de `@media screen` y del contenedor existente `sgol-viewport` hasta `48rem`, conservando la preferencia Auto/Móvil/Escritorio. No sustituir por detección de sistema operativo ni por un breakpoint arbitrario de ventana. El móvil forzado en monitor debe recibir la propuesta y el escritorio forzado en teléfono debe conservar escritorio. Fuera de ese contenedor y en impresión, presentación y comportamiento sin cambios.

## Evidencia actual e incertidumbres

Revisados `AGENTS.md`, `docs/desarrollo/PROTOCOLO_IMPLEMENTACION.md`, `docs/design/ESTILO_VISUAL_V2.md` y `docs/plan-normalizacion-movil.md`. Rama observada: `master`. Al iniciar existía un cambio ajeno de una línea en `wwwroot/css/mobile.css`, relativo a columnas de la cabecera de aplicación; se conserva.

| Hallazgo de código | Fuente real | Implicación y comprobación pendiente |
| --- | --- | --- |
| Mi trabajo tiene título, descripción, navegación, subtítulo móvil y ayuda como hijos de la cabecera. La composición móvil mezcla flex y reglas compartidas. | `Pages/MyWork/Index.cshtml`, `wwwroot/css/components.css` reglas `.my-work-heading`, `wwwroot/css/navigation.css` | Posible suma de márgenes y saltos. Medir en navegador el hueco exacto: no se ha demostrado todavía su causa única. |
| Filtros se inserta antes del formulario cuando la cabecera del panel está oculta; Mi trabajo oculta esa cabecera en móvil. | `Pages/MyWork/_OperationalTasks.cshtml`, `wwwroot/js/mobile.js`, función `filters()` | Compactar la transición entre cabecera y Filtros sin esconder el buscador ni alterar `WorkSearch`. |
| Ayuda genérica usa un botón con texto `i`, heredando padding y borde de `.mobile-fold-toggle`. Mi trabajo usa un `summary` y una letra dentro de un círculo de 20px. | `mobile.js: filters()`, `mobile.css: .mobile-heading`, `components.css: .my-work-help` | Dos geometrías distintas. El desplazamiento óptico reportado requiere captura renderizada; no corregir con un desplazamiento horizontal arbitrario. |
| Ya existe un SVG `info` con geometría centrada. | `Pages/Shared/_Icon.cshtml` | Reutilizarlo en móvil; icono visible menor, área de interacción conservada. |
| Las filas móviles son una columna; el botón Más datos se añade a la primera celda de acciones distinta de la identidad. | `mobile.js: rows()`, `mobile.css: .mobile-records`, `Pages/People/Index.cshtml` | En Personas, mover el control a la cabecera del registro puede ahorrar una línea. Evitar modificar todas las tablas por accidente. |
| Tareas comparte navegación de destinos con accesos de crear/asignar/configurar; Actualizar Asignaciones se agrega como otro botón primario. | `Pages/Tasks/Index.cshtml`, `Presentation/Navigation/SectionNavigation.cs`, `Pages/Shared/_SectionNavigation.cshtml`, `task-actions.css` | Separar navegación de acciones; un destino seleccionado no debe competir visualmente con una acción principal. |
| Panel de Tareas usa cuadrícula de dos columnas. Eliminar se presenta como secundario y la confirmación usa primario. En móvil se oculta la explicación de elegibilidad de asignación. | `Pages/Tasks/Index.cshtml`, `task-actions.css`, `wwwroot/js/task-actions.js: select()` | Dar tratamiento de peligro y explicación visible de las acciones deshabilitadas. Conservar el diálogo y las condiciones actuales. |

No se inspeccionó la aplicación autenticada renderizada en esta fase; no hay mediciones actuales ni capturas de antes/después del producto. La imagen aportada es referencia de jerarquía de acciones, no una captura de SGOL. Las maquetas no deben presentarse como evidencia de corrección.

## Sistema común propuesto

- Espaciado basado en tokens: 4px entre elementos íntimamente relacionados, 8px entre controles, 12px de margen lateral móvil y 16px entre grupos distintos. Revisar márgenes de párrafos y navegación antes de reducir padding indiscriminadamente.
- Título y ayuda en una fila; contexto secundario solo donde aporte información. Filtros inmediatamente después del bloque de navegación/contexto. No reservar altura para una ayuda cerrada.
- Icono de información visible de 16–18px, centrado geométricamente mediante SVG; área táctil mínima propia de SGOL de 44 × 44px CSS. No confundir puntos nativos de Apple con píxeles CSS universales.
- Ayuda en una **ventanita flotante compacta anclada al icono**, como la ayuda actual de Mi trabajo; sustituye la propuesta inicial de bloque desplegado en flujo. No ocupa una fila nueva ni desplaza filtros o contenido al abrirse. Texto breve, ancho orientativo de hasta 280px y limitado al ancho útil disponible, tipografía legible y fondo opaco en cada tema. Conservar significado, leyenda y enlaces necesarios, evitando repeticiones; no truncar contenido esencial para cumplir una altura fija. Solo ante ampliación o contenido que lo requiera, permitir desplazamiento interno acotado a la altura disponible.
- Abrir por toque, Enter o Espacio; cerrar al pulsar de nuevo, pulsar fuera o Escape. Nombre accesible y estado expandido; no depender de hover. La ayuda no es modal y no captura el foco; Escape devuelve el foco al activador. Reposicionar si alcanza bordes o teclado; escritorio conserva su ayuda actual.
- Primario: acción principal contextual, relleno de acento SGOL. Secundario: alternativa neutra. Terciario/textual: consulta o configuración auxiliar. Peligro: acción destructiva, claramente separada y con confirmación original.
- La navegación conserva `aria-current` y sus destinos; su selección usa énfasis de navegación en móvil y no otro gran botón de acción.
- No ocultar texto esencial ni recortar nombres para lograr la densidad objetivo. Ajustar columnas y permitir varias líneas; inputs móviles de 16px y zoom disponible.

## Intervenciones por pantalla

### Mi trabajo

1. Conservar h1 y apartados Mis tareas/Mis procesos/Petición de arrastre. Situar el icono de información **en la misma fila del título «Mi trabajo»**, con alineación vertical centrada.
2. **Quitar de la composición móvil el subtítulo «Tus tareas asignadas» y su fila**; no reemplazarlo por otro texto redundante. Conservar encabezados semánticos necesarios para accesibilidad y la presentación actual de escritorio.
3. Reducir acumulación de márgenes hasta Filtros; objetivo inicial de separación de 8–12px con ayuda cerrada, excluyendo avisos que deban mostrarse.
4. Explicación breve y leyenda disponibles en la ventanita compacta, sin convertirla en un panel grande ni repetir la descripción de la pantalla. No tocar textos de validación ni orden de tareas por color y vencimiento.
5. Mantener búsqueda global de asignaciones, paginación, acciones de evidencia, progreso y diálogos actuales.

### Personas y accesos

1. Encabezado de registro en dos columnas: identidad flexible y Más datos a la derecha. Mantener estado visible.
2. Comparar dos alternativas en simulación: control alineado con el nombre (preferida por proximidad a la identidad) o con el estado (da más ancho a nombres largos). A 320px y con texto ampliado, permitir reflujo si las columnas no caben.
3. Los datos secundarios conservan sus etiquetas; Editar, Desactivar/Reactivar y Eliminar permanecen disponibles según permisos. No mezclar el desplegable con el envío de formularios.
4. Aplicar una variante explícita a `.people-table`; no imponer columnas a tablas de planes, reportes o capacidad.
5. Al volver a escritorio, restaurar la ubicación original de cualquier elemento movido y mantener asociaciones de encabezados/celdas y orden de lectura.

### Tareas: parte superior

Separar destinos de navegación y acciones en grupos alineados, reutilizando enlaces y permisos existentes:

| Elemento actual | Presentación móvil propuesta |
| --- | --- |
| Tareas / Procesos | Navegación reconocible con destino actual marcado; no competir como primarios operativos. |
| Crear tarea | Primario del catálogo para Dirección. |
| Asignar tarea | Secundario; conserva acceso a elección de tarea/destinatarios. |
| Actualizar Asignaciones | Secundario, con cantidad/estado y deshabilitación actuales; abre revisión, no aplica automáticamente. |
| Porcentaje para el cierre | Terciario, separado de crear/asignar. Conservar alcance para nuevas operaciones. |

No usar selectores por texto localizado para identificar estas acciones. Si se necesita identificación nueva, agregar metadatos de presentación neutros al componente compartido, sin cambiar su resultado visual de escritorio ni duplicar enlaces.

### Tareas: panel contextual

Propuesta preferida: resumen breve de tarea seleccionada, Editar como acción principal universal y Más acciones para desplegar el resto. En el bloque expandido: Asignar tarea ocasional como secundaria, Activar/Desactivar como textual y Eliminar como peligro después de un separador. El encabezado del catálogo y el panel contextual tienen ámbitos distintos.

El cambio de énfasis de Editar es una propuesta de diseño, no una afirmación sobre la frecuencia real de uso. Si el feedback prioriza asignar, intercambiar énfasis sin alterar elegibilidad.

Conservar que Asignar tarea ocasional solo se habilita para tarea activa, ocasional y del proceso General. Mostrar su razón cuando no aplique: hoy la explicación se oculta en móvil. Estado sin selección: acciones deshabilitadas y explicación; no elegir tarea automáticamente. Conservar ID, revisión, radio, campos ocultos, CSRF y el flujo de confirmación. Recarga y regreso mantienen el comportamiento actual de selección.

El panel compacto reduce ocupación permanente; el desplegable añade un toque para acciones secundarias. No apilar permanentemente cuatro botones grandes flotantes: eso ocuparía más pantalla. Expandir dentro del flujo, limitar superposición y comprobar teclado/área segura. No convertirlo en un modal nuevo salvo que las pruebas demuestren necesidad. Si es sticky, no debe tapar última fila, mensajes ni foco.

## Normalización de las demás pantallas

Inventario actualizado desde las directivas `@page` de `Pages/`: **27 páginas** observadas en esta revisión. Reconciliar otra vez al implementar para incluir páginas añadidas entre fases. Aplicar los patrones equivalentes en todas las pantallas, sin forzar todos los contenidos al mismo molde:

| Familia | Rutas/componentes de partida | Revisión |
| --- | --- | --- |
| Trabajo | `/mi-trabajo`, procesos, detalle, arrastre y evidencia | Cabecera, ayuda, filtros, registro, acciones y retorno. |
| Catálogos | `/personas-y-accesos`, especializaciones, `/tareas`, procesos | Identidad/estado/detalles; distinguir navegación de acciones. |
| Operación | `/validaciones`, `/supervision/carga`, `/planificacion`, `/planificacion/procesos`, `/asignar-tarea` | Reutilizar jerarquía, formularios completos y confirmaciones; no ocultar conflictos. |
| Consulta | `/tablero`, `/tablero/rol-de-tareas`, `/reportes` | Resumen compacto, filtros y matrices con semántica propia. |
| Configuración y acceso | `/configuracion`, `/acceso` y variantes existentes | Solo corregir discrepancias de los patrones aplicables; conservar flujos de sesión y recuperación. |

Inventario obligatorio original de cobertura. El estado implementado y la evidencia por pantalla se encuentran en la sección final; esta lista por sí sola no acredita pruebas.

| Ruta existente | Archivo en `Pages/` | Atención específica además de los patrones comunes |
| --- | --- | --- |
| `/` | `Entry.cshtml` | Entrada, redirecciones y sesión no disponible. |
| `/acceso` | `Access/Index.cshtml` | Login, contraseña, MFA y recuperación; teclado y errores. |
| `/branches/{branchCode}` | `Branches/Details.cshtml` | Detalle de sucursal, permisos y contenido variable. |
| `/mi-trabajo` | `MyWork/Index.cshtml` | Título con ayuda compacta, sin subtítulo redundante; filtros y entregas. |
| `/mi-trabajo/arrastre` | `MyWork/Carryover.cshtml` | Solicitud, motivo, estado e historial. |
| `/mi-trabajo/procesos` | `MyWork/Processes.cshtml` | Etapas, selección y ejecución del proceso. |
| `/mi-trabajo/tareas/{obligationId:guid}` | `MyWork/Details.cshtml` | Evidencia, revisiones e historial completo. |
| `/mi-trabajo/fotografias/{id:guid}` | `MyWork/Photographs.cshtml` | Galería, apertura, descarga y retorno. |
| `/personas-y-accesos` | `People/Index.cshtml` | Perfil, Más datos, búsqueda y acciones de acceso. |
| `/personas-y-accesos/personas/{personId:guid}` | `People/Details.cshtml` | Empleo, vigencia, horario e historial. |
| `/personas-y-accesos/especializaciones` | `People/Specializations.cshtml` | Lista, editor y estados de especialización. |
| `/tareas` | `Tasks/Index.cshtml` | Catálogo, variantes de editor/cierre y panel contextual. |
| `/tareas/asignar` | `Tasks/Assign.cshtml` | Formulario y confirmación de asignación ocasional. |
| `/asignar-tarea` | `Occasional/Index.cshtml` | Elección de tarea/destinatarios y creación ocasional. |
| `/tareas/procesos` | `Tasks/Processes.cshtml` | Listado, versiones y editor de procesos. |
| `/tareas/procesos/items` | `Tasks/ProcessItems.cshtml` | Elementos del proceso y acciones existentes. |
| `/tareas/actualizar` | `Tasks/Refresh.cshtml` | Diferencias, conflictos y decisiones por asignación. |
| `/planificacion` | `Planning/Index.cshtml` | Planes, creación, carga, operación y conflictos. |
| `/planificacion/procesos` | `Planning/Processes.cshtml` | Plan de proceso, etapas, responsables y horarios. |
| `/planificacion/versiones` | `Planning/Releases.cshtml` | Versiones y acciones permitidas. |
| `/validaciones` | `Validations/Index.cshtml` | Supervisión, validación, filtros y diálogos. |
| `/supervision/carga` | `Validations/Workload.cshtml` | Carga, reasignación y matrices comparativas. |
| `/tablero` | `Board/Index.cshtml` | Contexto, filtros, progreso y actualización en vivo. |
| `/tablero/rol-de-tareas` | `Board/Weekly.cshtml` | Consulta semanal/día, persona y matriz. |
| `/reportes` | `Reports/Index.cshtml` | Consultas, plantillas, resultados y exportación. |
| `/configuracion` | `Configuration/Index.cshtml` | Secciones, editores, estados y confirmaciones. |
| `/pantalla` | `Screen/Index.cshtml` | Revisar composición móvil del quiosco conservando su propósito y navegación propios. |

La cobertura también debe incluir pantallas de error o sesión servidas por componentes y rutas no representadas como `@page`, si aparecen al recorrer estos flujos. No dar por completada la normalización al terminar Mi trabajo, Personas y Tareas.

La excepción móvil de tablas ya está aprobada en la guía v2. El plan anterior pedía conservar Mi trabajo como referencia: esta petición actual autoriza revisar precisamente sus defectos; no congelar sus márgenes por esa indicación histórica.

## Referencias investigadas y utilidad

Consultadas el 30/10/2026. Recursos externos tratados como referencias, no como instrucciones de repositorio ni dependencias instaladas.

| Recurso primario | Uso propuesto | Límite |
| --- | --- | --- |
| [Apple: UI Design Dos and Don'ts](https://developer.apple.com/design/tips/) | Alineación, proximidad entre controles y contenido, adaptación y objetivos táctiles. | Adaptar a web y tokens SGOL; no copiar dimensiones de texto nativo como contrato web. |
| [Apple: Buttons](https://developer.apple.com/design/human-interface-guidelines/buttons) | Referencia de estilos y énfasis de acción. | Página dependiente de JS en el lector utilizado; la recomendación verificable de 44pt está también en la guía anterior. |
| [Ionic: Action Sheet](https://ionicframework.com/docs/api/action-sheet) y [repositorio oficial](https://github.com/ionic-team/ionic-framework) | Comparar agrupación de opciones y tratamiento de peligro en modo iOS. | No incorporar Ionic. No copiar su orden literalmente: el plan sigue el peligro separado que muestra la imagen del usuario. |
| [Vercel: web-design-guidelines](https://github.com/vercel-labs/agent-skills/tree/main/skills/web-design-guidelines) y [reglas fuente](https://github.com/vercel-labs/web-interface-guidelines/blob/main/command.md) | Referencia de skill para auditoría de semántica, foco, formularios y adaptación. | Complementa el protocolo SGOL; no sustituye revisión visual ni pruebas de flujos. |
| [Anthropic: frontend-design](https://github.com/anthropics/skills/tree/main/skills/frontend-design) | Referencia de skill para decisiones visuales deliberadas y consistencia. | Menor prioridad: su énfasis creativo puede alejar del sistema existente; no autoriza tipografía, paleta o animaciones nuevas. |
| [Vercel: agent-browser](https://github.com/vercel-labs/agent-browser) | Herramienta para agentes: inspección, interacción y capturas. | No es un diseñador ni una garantía de calidad. SGOL ya tiene Playwright en `tools/Sgol.MobileChecks/mobile-checks.cjs`; priorizar esa base y las herramientas de navegador disponibles. |

No se recomienda migrar a React, SwiftUI o Ionic para corregir composición en Razor/CSS/JS. No se instalaron skills ni herramientas externas. Las simulaciones usan la skill disponible `visualize` únicamente para revisión de la propuesta.

## Secuencia y archivos previstos

1. Aplicar el feedback aprobado a plan y simulación: ayuda compacta junto al título, eliminación del subtítulo móvil de Mi trabajo y alcance obligatorio de todas las pantallas. Más datos junto al nombre es la referencia visual preferida; la variante junto al estado queda como alternativa de comparación.
2. Antes de implementar, identificar versión que sirve realmente las pantallas, acceso local/LAN y destino de datos. Registrar baseline renderizado con datos representativos y tema claro/oscuro.
3. Ajustar primitivas móviles en `wwwroot/css/mobile.css`; aislar variantes en `navigation.css` y `task-actions.css` dentro del contenedor. Corregir helpers de ayuda y filas en `wwwroot/js/mobile.js` sin duplicar controles ni perder restauración.
4. Integrar en `Pages/MyWork/Index.cshtml`, `_OperationalTasks.cshtml`, `Pages/People/Index.cshtml`, `Pages/Tasks/Index.cshtml` y, si es necesario, `_SectionNavigation.cshtml`/metadatos de navegación. Compartir SVG mediante `_Icon.cshtml`; si la ayuda se genera en JS, reutilizar una plantilla del icono, sin biblioteca paralela.
5. Recorrer y cerrar cada fila del inventario completo por rol y estado; aplicar los patrones compartidos donde correspondan y justificar cualquier no aplicable. Añadir comprobaciones significativas a `tools/Sgol.MobileChecks/mobile-checks.cjs`. No reducir esta fase a las tres pantallas simuladas.
6. Validar interfaz y recorridos reales, revisar diff y actualizar trazabilidad/guía visual con la nueva excepción móvil. Publicación o reinicio no forman parte del plan actual.

## Matriz de aceptación y validación

Todas las pruebas del producto siguientes son **propuestas**; no ejecutadas en esta fase documental. Las pruebas de maqueta, si se realizan, se registran aparte.

| Escenario / precondición | Acción y resultado esperado | Entorno y prueba | Estado |
| --- | --- | --- | --- |
| Mi trabajo con tareas y ayuda cerrada | Abrir, filtrar y limpiar; separación inicial de 8–12px entre grupos correspondientes, sin filas vacías; conservar consulta, orden y paginación. | Aplicación autenticada, navegador a 320/390/430px; captura y medidas DOM. | Propuesto |
| Ayuda en Mi trabajo y cabeceras equivalentes | Abrir ventanita por toque y teclado; cerrar con activador, fuera y Escape; icono centrado de 16–18px, objetivo 44px, sin cambiar altura de cabecera o posición de filtros. Contenido legible, acotado al viewport y foco conservado. Mi trabajo no muestra el subtítulo redundante. | Navegador real, claro/oscuro/special, 320px y zoom 200%; capturas y medidas DOM antes/después. | Propuesto |
| Personas: nombre largo, propio, activo, inactivo, primer ingreso pendiente | Expandir/contraer Más datos; nombre y estado legibles, datos secundarios accesibles y acciones por rol conservadas. | Simulación representativa para lectura; navegador y lector de pantalla. | Propuesto |
| Personas: filtro activo más detalles abiertos | Cambiar ancho/tema y volver a escritorio; conservar formulario y datos, restaurar composición y orden de lectura. | Navegador, ciclos móvil/escritorio; comparar FormData y DOM. | Propuesto |
| Tareas: Dirección y Asesor Externo | Comprobar todos los enlaces actuales y permisos, crear/editar y volver; un primario por grupo operativo. | Navegador autenticado por rol, formularios sobre base aislada. | Propuesto |
| Panel: sin selección, ocasional General activa, inactiva y tarea de proceso | Seleccionar con radio/toque y expandir acciones; ID/revisión correctos, habilitación y explicación acordes al contrato. | Navegador + formularios reales, base aislada con fixtures. | Propuesto |
| Eliminar tarea/persona existente con historial | Abrir y cancelar: sin cambios. Confirmar en fixture: reglas actuales e historial conservados, estilo de peligro inequívoco. | Base aislada; HTTP real con CSRF, comparación antes/después. | Propuesto |
| Cambio simultáneo y revisión obsoleta | Abrir edición y cambiar desde otra sesión; conservar conflicto y ausencia de escritura parcial actuales. | Dos sesiones en base aislada, formulario real. | Propuesto |
| Vacío, carga, éxito, error y textos largos | Verificar mensajes completos y acciones disponibles; no plegar errores ni ocultar información de bloqueo. | Navegador con fixtures y respuestas controladas. | Propuesto |
| Panel expandido con teclado y última fila | Recorrer foco y botones; ningún campo, mensaje o registro tapado, cierre alcanzable y área segura respetada. | Emulación y Safari en iPhone físico; orientación vertical/horizontal. | Propuesto |
| Límite 48rem, vista guardada y texto ampliado | Probar 768/769px a raíz predeterminada, límite real en rem con fuente aumentada, móvil forzado en monitor y escritorio forzado en teléfono. | Navegador, contenedor medido; antes/después. | Propuesto |
| Escritorio e impresión | Comparar baseline a 1280/1440px y vista impresa; misma composición, botones, destinos y contratos. | Capturas de regresión y estilos computados. | Propuesto |
| Todas las pantallas existentes | Cerrar las 27 filas del inventario y las nuevas que aparezcan al reconciliarlo, por rol y variantes; patrones coherentes sin alterar matrices, formularios ni permisos. Registrar resultado/evidencia por ruta; las tres simulaciones no satisfacen esta cobertura. | Chromium/Edge y WebKit; Safari real para aceptación iOS. | Propuesto |
| Migraciones y reglas de capacidad | No hay cambios de esquema o planificación en esta propuesta. | Revisión del alcance/diff; pruebas amplias de negocio no corresponden a la fase documental. | No aplicable |

La suite móvil existente comprueba desbordamientos y conservación de campos, pero no sustituye revisar jerarquía, centrado óptico, espaciado, Safari físico o funcionamiento de escrituras. Su comentario inicial menciona `browser-state.json`, mientras el código leído usa `state-alex.json`: verificar entradas reales antes de ejecutarla, sin publicar estados de sesión.

## Criterios de cierre posterior

Sin scroll horizontal global a 320px; controles táctiles conservados; ayuda visual consistente; más contenido útil inicial; todas las acciones originales accesibles; filtros y estados sin efectos secundarios; escritorio e impresión sin diferencias atribuibles al cambio. Registrar aprobado/fallido/no ejecutado por escenario y mantener pendientes visibles. No prometer porcentaje de ahorro de altura hasta medir el baseline real.

## Simulación entregada para feedback

Vista interactiva local fuera del producto: `C:/Users/siste/.codex/visualizations/2026/10/31/01a18f7a-0651-71a0-bff5-7b624ca87a3c/propuesta-movil-sgol.html`.

Incluye las tres pantallas, dos alternativas de Personas mediante carrusel, filtros desplegables, ayuda, selección de tareas, elegibilidad de asignación y ejemplo de confirmación/cancelación. Usa Poppins local incrustada y pares de colores claro/oscuro de SGOL. El menú superior y algunos destinos solo muestran un aviso de simulación. El catálogo abre con una selección ficticia para mostrar la jerarquía: la implementación debe conservar el arranque sin selección actual. No hay persistencia operativa ni conexión a SGOL.

Comprobaciones de maqueta aprobadas con Playwright/Edge: tres pantallas a anchos de ventana 320/390/736px sin desbordamiento del contenedor ni controles fuera; expansión/contracción de datos; cambio de alternativa del carrusel; ayuda; selección de tarea ocasional/de proceso y deshabilitación; expansión de acciones; confirmación y cancelación; sin errores JavaScript en la pasada final. Capturas revisadas de Mi trabajo, Personas y Tareas en oscuro. Esto valida únicamente la maqueta. La aserción inmediata de visibilidad de confirmación que resultó intermitente en la primera revisión se sustituyó por espera de estado.

Revisión posterior al feedback: ayuda flotante comprobada en las tres pantallas a 320/390/736px; ancho acotado a 280px y al contenedor, misma altura de cabecera antes/después de abrir, cierre por Escape y toque fuera, apertura con Enter. Verificada ausencia del subtítulo «Tus tareas asignadas» en Mi trabajo y revisada su captura con ventanita abierta. Todas estas comprobaciones de maqueta aprobaron; las pruebas del producto siguen propuestas.

Al implementar, incluir también el tema `special` que existe en `tokens.css`, además de claro/oscuro. La maqueta sigue la apariencia clara/oscura del visor; no representa todavía ese tercer tema. El usuario aprobó el diseño y el plan con ayuda compacta, icono junto al h1 y eliminación del subtítulo móvil de Mi trabajo. Se mantiene la jerarquía Editar/Asignar y el panel compacto aprobados; Más datos junto al nombre es la referencia visual y junto al estado permanece como alternativa, sin constituir un bloqueo de negocio.
## Implementación y validación del 30/10/2026

Esta sección sustituye el estado documental anterior: implementación autorizada y realizada en el árbol de trabajo, sin commit, push ni despliegue. Las matrices anteriores describen la propuesta original; el estado ejecutado se registra aquí. Clasificación: interfaz con interacción cliente; sin cambios de esquema, servicios, permisos ni reglas de planificación.

### Entorno y conservación

- Reconciliación de `@page`: siguen existiendo 27 páginas. Se incluyeron formularios, variantes de consulta y rutas de sesión/error en un manifiesto de 43 destinos. Un HTTP 200 de una redirección no se cuenta como cobertura del formulario de destino original.
- Aplicación compartida observada: proceso 26992, ensamblado `.local/runtime/qa-interactions-3cc8db1/Web/Sgol.Web.dll`, proxy LAN separado. No se reinició ni sustituyó.
- Candidato compilado en `.local/mobile-ios-build`, instancia HTTPS local 7284 con base `sgol_task_checks_mobile_ios_20261030`, copia obtenida mediante `pg_dump`/`pg_restore`. Las altas y cambios de prueba se realizan exclusivamente ahí. Se conserva la copia para reproducibilidad.
- Comparador: binario anterior ejecutado aparte en 7285 contra la misma copia aislada. No se cambió su directorio ni sus archivos. Comparar con el código fuente sin identificar el binario habría dado un baseline incorrecto.
- Se preservó exactamente la modificación previa de las columnas de `.encabezado-aplicacion` en `mobile.css`. El plan ya era un archivo sin seguimiento al iniciar; no se atribuye su contenido previo a esta implementación.

### Cambios y contratos

`mobile.css` concentra las nuevas reglas dentro de `@media screen` / `@container sgol-viewport (max-width: 48rem)`. Conserva Auto/Móvil/Escritorio. Reutiliza Poppins, icono `info`, colores y superficies de los tres temas. Cabeceras, navegación, tablas verticales, filtros, formularios y diálogos siguen siendo los elementos del producto.

`mobile.js` genera ayudas no modales con área de 44 px, SVG de 17 px, ancho máximo 280 px y desplazamiento interno solo cuando es necesario. No desplazan el contenido; cierran con activador, fuera y Escape, que devuelve foco. Se posicionan respecto al activador y viewport visual, reservando espacio para no cubrirlo. La descripción original sigue disponible en escritorio. Mi trabajo conserva su leyenda y elimina el subtítulo móvil redundante; también oculta en móvil la descripción repetida para alinear ayuda y título.

En Personas, el control Más datos pertenece a la celda de identidad, a la derecha y con reflujo. Estado y acciones conservan sus celdas. No se crean copias de formularios. Las tablas complejas y matrices mantienen sus estrategias existentes.

La navegación de Tareas incorpora metadatos de presentación, sin identificación por texto traducido. Destinos y acciones tienen énfasis y filas diferentes. El panel conserva selección inicial vacía, radio, IDs, revisión, CSRF y handlers. Editar es primario móvil; Más acciones despliega asignación, activación y eliminación. Se conserva visible la explicación de elegibilidad; eliminación usa peligro. El panel tiene altura acotada y desplazamiento interno. El orden DOM móvil coincide con el visual; los marcadores restituyen el orden de escritorio.

Se corrigió además `identity()` para usar atributos DOM: un formulario con un campo `name="id"` devuelve ese control al acceder a `form.id`, lo que producía un `aria-controls` inválido. No cambia el valor del campo ni su envío.

Productores/consumidores conservados: `SectionNavigation.Create` → `_SectionNavigation`; páginas Razor → `mobile.js` → CSS; filas del catálogo → `task-actions.js:select` → formularios `Save`/`Change`, asignación y diálogo originales. No se modificaron servicios, snapshots, historial ni validaciones del servidor.

### Cobertura por pantalla

**Aplicado** significa integración de presentación, no validación exhaustiva de todos los estados. El barrido por rutas mide respuesta, desbordamiento y presencia de pantalla; las pruebas de interacción se detallan aparte. Evidencias locales privadas: `.local/mobile-ios/` y su subdirectorio `compact/`.

| Página | Aplicación del patrón | Evidencia / estado y límite |
| --- | --- | --- |
| `/` | Entrada y redirección conservadas; estilos compartidos de sesión/error. | Barrido autenticado; no equivale a probar sesión expirada. |
| `/acceso` | Formularios originales, controles y ancho compartidos. | Login real y cambio de contraseña inicial en tres altas aisladas; MFA/recuperación no ejecutados. |
| `/branches/{branchCode}` | Cabecera con ayuda, tabla y acciones compartidas. | Lectura de sucursal en barrido; escritura no aplicable. |
| `/mi-trabajo` | Título/ayuda, leyenda flotante, filtros próximos y navegación. | Capturas revisadas, ayudas y búsqueda; escritorio/impresión comparados. Entrega completa no cubierta por esta prueba visual. |
| `/mi-trabajo/arrastre` | Ayuda, filtros/tabla y formulario existente. | Lectura en barrido; solicitud y aprobación nuevas no ejecutadas. |
| `/mi-trabajo/procesos` | Ayuda de cabecera y navegación; etapas existentes. | Lectura por cuatro roles; ejecución de todas las etapas no ejecutada. |
| `/mi-trabajo/tareas/{obligationId}` | Cabecera y error compartidos. | El ID heredado del manifiesto devuelve 503 por consulta de obligación no encontrada; no valida detalle exitoso. Pendiente fixture de obligación compatible. |
| `/mi-trabajo/fotografias/{id}` | Título compacto y galería original. | Ruta renderizada; captura/carga, descarga y dispositivo físico no ejecutados. |
| `/personas-y-accesos` | Ayuda, identidad/Más datos, estado, formularios y navegación. | Lectura, alta real, nombres largos y primer acceso; restauración de formularios/escritorio. |
| `/personas-y-accesos/personas/{personId}` | Anchos/formularios existentes; tablas y controles compartidos. | Detalle leído; cada cambio de empleo/horario no ejecutado. |
| `/personas-y-accesos/especializaciones` | Ayuda, tabla, navegación y editor existente. | Lista y creación renderizadas; guardado de especialización no ejecutado. |
| `/tareas` | Ayuda, destinos/acciones, variantes y panel contextual. | Lista, crear, editar y porcentaje incluidos; pruebas de formulario y concurrencia registradas aparte. |
| `/tareas/asignar` | Título compacto y formulario con contratos originales. | Formulario abierto; asignación con reserva efectiva no ejecutada. |
| `/asignar-tarea` | Ayuda, navegación, selección y formulario compartidos. | Lectura por roles; creación de asignación no ejecutada. |
| `/tareas/procesos` | Ayuda/navegación y editor existente. | Lista por roles; publicación/versionado no ejecutados. |
| `/tareas/procesos/items` | Título compacto, navegación y campos existentes. | Lista/editor renderizados; cambio de tarifas no ejecutado. |
| `/tareas/actualizar` | Reflujo de cabecera y controles; explicación de consecuencias permanece visible. | Previsualización y conflictos leídos; no se confirmó actualización masiva. |
| `/planificacion` | Ayuda, navegación, filtros, matrices y confirmaciones existentes. | Consulta/crear/operaciones; comparación de escritorio/impresión. No se publicó plan. |
| `/planificacion/procesos` | Navegación y diálogo de instrucciones acotado al contenedor. | Lectura del editor; asignación de responsables y publicación no ejecutadas. |
| `/planificacion/versiones` | Ayuda, navegación, tablas y diálogos originales. | Lectura; publicación de versiones no ejecutada. |
| `/validaciones` | Ayuda, filtros y navegación; revisión/confirmaciones originales. | Vistas por rol y apertura prevista en manifiesto; decisiones de revisión no escritas. |
| `/supervision/carga` | Cabecera y presentación de matrices existentes. | Lectura por rol; reasignación no ejecutada. |
| `/tablero` | Navegación compartida, filtros y tarjetas existentes. | Barrido; no se sustituyen tarjetas por filas genéricas. |
| `/tablero/rol-de-tareas` | Navegación compartida; selector de día y matriz original. | Barrido de consulta; exportación/impresión completa del tablero no ejecutada. |
| `/reportes` | Ayuda, filtros y acciones existentes. | Barrido, temas y comparación escritorio/impresión; exportaciones PDF/Excel no ejecutadas. |
| `/configuracion` | Ayuda compacta y controles de apariencia originales. | Preferencias y temas; comparación escritorio/impresión. |
| `/pantalla` | Título/espaciado de conexión móvil; propósito de quiosco conservado. | Pantalla de reconexión renderizada; agente local de quiosco no disponible en la prueba. |

### Comprobaciones y límites

- Compilación Release: aprobada, cero errores y advertencias (`.local/mobile-ios-build.log`).
- Roles: aprobadas tres altas por formulario real, cambio de contraseña inicial y 33 consultas a 320 px con respuestas 200/403 y sin desbordamiento (`roles-results.json`). Dirección usa sesión autenticada real del fixture copiado.
- Escritorio, retorno móvil/escritorio e impresión: 21 comparaciones aprobadas de geometría/control en siete vistas (`desktop-comparison.json`), contra el binario anterior. No es una comparación píxel a píxel de todas las páginas.
- Los ensayos iniciales agotaron la cuota HTTP y recibieron 429; **no se cuentan como pantallas aprobadas**. El ejecutor comprueba estados HTTP y espera/reintenta los rechazos temporales sin desactivar validaciones ni cuotas.
- Las capturas de maqueta continúan siendo referencias, no evidencia del producto.
- Safari en iPhone físico, teclado virtual/orientación, lector de pantalla y todos los estados históricos de cada operación: **no ejecutados**. No hay hardware físico accesible en este entorno. Los estados no cubiertos de la tabla anterior siguen pendientes, aunque su página use los patrones compartidos.
- No hay cambios de migración/capacidad: pruebas de modificación de esas reglas **no aplicables**. No se ejecutaron suites generales ajenas a este cambio de presentación.
- Formularios reales de Tareas: aprobados crear/editar, rechazo de revisión obsoleta en segunda pestaña, comprobación posterior del valor persistido, desactivación y elegibilidad, cancelación y confirmación de eliminación de un fixture (`forms-results.json`). Se corrigió el ejecutor para localizar el fixture mediante la búsqueda real del catálogo paginado; buscarlo solo en la primera página no era una comprobación válida.

- Ampliación final: **602 comprobaciones aprobadas**, cero fallos y cero errores JavaScript, en Chromium/Edge y WebKit, cruzando los 43 destinos con claro/oscuro/special y texto al 100/200 % a 320px (`compact/themes-results.json`, `themes-final.log`). Incluye estados HTTP esperados; el 503 del detalle legado sigue siendo cobertura de error, no de éxito.
- El primer barrido de seis anchos produjo 587/588 aprobaciones. La entrada a 769px falló con 503 al agotarse la cuota interna de sesión; la repetición dirigida obtuvo 200 (`final-regressions.json`). Hubo además un error de EventSource del tablero durante esa carga; se conserva el registro original en `compact/results.json` sin borrar el fallo.
- Correcciones surgidas de pruebas: ayuda ampliada tapando su activador; asociación ARIA de formularios con campo `id`; orden visual de Actualizar Asignaciones afectado por CSS cargado después; selector de arrastre con `min-width:10rem` que desbordaba 54px al 200%. Las reglas finales corrigen estos casos solo en móvil. Las ayudas se cierran antes/al entrar en impresión para no dejar dimensiones inline en el texto impreso.
- Interacciones finales: **72 escenarios aprobados**, cero errores JavaScript, en Chromium y WebKit (`compact/interactions-results.json`). Apertura/cierre de ayuda con Enter, activador, fuera y Escape; altura de cabecera estable; área táctil, posición dentro del viewport, conservación de FormData, elegibilidad y cancelación, retorno a escritorio y preferencias.
- Tablero repetido sin carga masiva: página y flujo `Events` con HTTP 200, sin errores en ambos motores (`board-recheck.json`). El error previo bajo cuota se mantiene registrado como fallo inicial, resuelto en repetición dirigida.
- Último selector del catálogo a 320×568: foco visible por encima del panel, tanto plegado como expandido. La expansión se limita al 45% de la altura visual y permite desplazamiento interno; no se dio por probada la interacción con teclado virtual físico.
- Cierre de verificación: cambio previo conservado; revisión de diff y sintaxis JavaScript aprobadas. Evidencia y fixtures permanecen privados en `.local/mobile-ios/`. Sin commit, push, despliegue ni reinicio compartido. Las limitaciones de hardware y escenarios operativos no ejecutados de la tabla siguen abiertas; no se declara validación exhaustiva del producto.
- Foco en última fila repetido en ambos motores: cuatro combinaciones aprobadas (panel abierto/cerrado), evidencia `focus-results.json`. Las dos instancias temporales de 7284/7285 se cerraron al terminar; el proceso compartido 26992 siguió activo. La base aislada y los archivos de evidencia se conservaron.

### Carga en el entorno compartido autorizada posteriormente

El 30/10/2026 el usuario solicitó explícitamente reiniciar el entorno compartido para ver los cambios. Se publicó el árbol de trabajo en `.local/runtime/mobile-normalization-20261030/Web` y se sustituyó únicamente el proceso Web: PID anterior 26992, nuevo PID 33056. Se mantuvieron el proxy y las dependencias, con la configuración compartida de `sgol_local`; no se ejecutaron limpieza ni migraciones manuales.

Verificación aprobada: ensamblado esperado en el puerto interno, `health/live=alive`, `health/ready=ready` y salud por LAN. Sesión real por LAN: Mi trabajo sin subtítulo móvil, ayuda flotante con cierre por Escape y panel de acciones de Tareas disponible. Los bytes servidos de `mobile.css`, `mobile.js` y `task-actions.js` coinciden por SHA-256 con el código actual. Evidencia privada: `.local/deploy-mobile-20261030/deployment.json` y `browser-verification.json`. No se hizo commit ni push. Las limitaciones de validación previamente documentadas siguen vigentes.

### Refinamiento posterior: densidad de Tareas

A petición del usuario se revisó la captura del catálogo compartido y se consultó [Unslop, de Damir Sokolovsky](https://github.com/heydamir/unslop/blob/main/skills/unslop/SKILL.md) (CC BY 4.0) como referencia de jerarquía y reducción de ruido visual. No se instaló la skill ni se ejecutó código de ese repositorio. Se mantienen Poppins, tokens, temas y objetivos táctiles mínimos de 44 px de SGOL.

Cambio de interfaz con interacción: `/tareas` conserva Crear y Asignar como acciones visibles; Actualizar Asignaciones, su explicación y Porcentaje para el cierre se agrupan en Gestión. El contador pendiente permanece visible en su activador. Las instrucciones generales pasan a la ayuda de cabecera. El panel contextual aparece al seleccionar una fila; la explicación de elegibilidad acompaña las acciones secundarias desplegadas. Se reducen bordes de navegación y fondos interiores de la fila seleccionada. No se suprime contenido operativo ni se cambian handlers, permisos, revisión, selección, confirmaciones o validaciones.

Implementación acotada a `Pages/Tasks/Index.cshtml`, `wwwroot/css/task-catalog-mobile.css` y `wwwroot/js/task-catalog-mobile.js`. Los nodos originales se trasladan sin clonar campos y se restituyen al volver a escritorio o imprimir. El CSS utiliza el contenedor `sgol-viewport`. La referencia nueva desde Razor evita modificar la versión compartida que sigue cargando el binario anterior.

Entorno de prueba: candidato Release en HTTPS 7284 y binario compartido anterior en HTTPS 7285, ambos contra `sgol_task_checks_mobile_ios_20261030`; no contra `sgol_local`. Evidencia privada conservada en `.local/task-catalog-refinement/`. En ambos motores, a 390 px, la primera tarea pasó de Y=679 a Y=338: 341 px menos antes de llegar al catálogo. Es una medición de la página real del fixture, no de una maqueta.

| Escenario | Resultado esperado / prueba | Estado y evidencia |
| --- | --- | --- |
| Catálogo en 320, 390, 430, 768, 769 y 1440 px; claro, oscuro y special; texto 200% a 320 | Sin desbordamiento; límite móvil respetado, contenido accesible | Aprobado Chromium/Edge y WebKit; `density-results.json`, `checks-final.log`; capturas revisadas |
| Gestión, ayuda, selección y acciones secundarias | Teclado Enter, cierre de ayuda con Escape, panel condicionado a selección, conservación de FormData y elegibilidad | Aprobado en ambos motores; ejecutor `tools/Sgol.MobileChecks/task-catalog-density.cjs` |
| Escritorio e impresión: catálogo, crear, porcentaje y búsqueda vacía | Geometría y controles equivalentes al binario anterior; restitución al cambiar de modo | Aprobado en ambos motores; `density-results.json` |
| Impresión desde móvil y preferencias Móvil/Escritorio | Restituir instrucciones y controles originales; respetar preferencia guardada | Aprobado en ambos motores; `roles-print.json` |
| Asesor Externo, Subcoordinación, Piso de ventas | Lectura o denegación según permisos, sin controles privilegiados ni desbordamiento | Aprobado en ambos motores; `roles-print.json` |
| Crear/editar, revisión obsoleta, persistencia, desactivar/elegibilidad y eliminar cancelar/confirmar | Servicios y formularios reales conservan contratos | Aprobado mediante `compact-mobile-forms.cjs` en base aislada; `forms.log` y `.local/mobile-ios/forms-results.json` |
| Compilación y revisión estática | Release sin errores, sintaxis y diff correctos | Aprobado; `build.log`, cero errores/advertencias |
| Pruebas iniciales de los nuevos desplegables | Apertura por teclado e impresión sin carrera de actualización | Falló inicialmente el foco en summary por el revelador compartido; corregido localmente. La comprobación de impresión necesitó esperar la restitución DOM. Las repeticiones finales pasaron |
| Safari físico, lector de pantalla, teclado virtual y todos los estados históricos | Verificación con hardware y fixtures correspondientes | No ejecutado; permanecen las limitaciones generales de este documento |
| Carga de esta revisión en el entorno compartido | Publicar/reiniciar con autorización aplicable | No ejecutado en esta revisión; sigue activo el binario `mobile-normalization-20261030` previamente autorizado |

Las comprobaciones amplias de las otras 26 páginas corresponden a la sección anterior; no se presentan como repetidas en esta revisión acotada. Se conserva el cambio previo de `mobile.css`. Sin commit ni push.

### Jerarquía compartida y carga autorizada — 30/10/2026

El usuario autorizó cargar la revisión, condicionada a aplicar primero los principios de jerarquía de Unslop a las demás pantallas. Se revisó el inventario y sus parciales actuales: los patrones de ayuda y datos secundarios ya estaban integrados; el problema compartido restante era la competencia visual entre destinos, acciones y superficies anidadas. Se añadió `mobile-hierarchy.css` al layout: navegación textual con indicador activo, desplegables neutros, menor decoración anidada y jerarquía entre identidad y metadatos. `SectionNavigation` identifica Crear usuario y Crear nuevo plan como acciones; `mobile-hierarchy.js` coloca esas acciones después de los destinos en móvil y restaura el orden DOM en escritorio/impresión. No se usan órdenes CSS divergentes del teclado. No se ocultan mensajes de error, estados, instrucciones de campos ni consecuencias operativas.

Reconciliación con las 27 páginas del inventario anterior (incluye las variantes del manifiesto de 43 destinos):

| Pantallas | Aplicación / criterio |
| --- | --- |
| `/`, `/acceso`, `/branches/{branchCode}` | Entrada y acceso mantienen su flujo enfocado y mensajes; sucursal recibe superficies compartidas. No se pliegan credenciales ni recuperación. |
| `/mi-trabajo`, `/mi-trabajo/arrastre`, `/mi-trabajo/procesos`, detalle de obligación y fotografías | Navegación más discreta cuando existe, ayudas y datos secundarios anteriores, menor decoración de paneles; evidencia y estado permanecen visibles. |
| Personas, detalle de persona y especializaciones | Crear usuario separado de destinos; identidad/Más datos y estado conservados; superficies y filtros más discretos. |
| Tareas, asignación ocasional, `/asignar-tarea`, procesos, items y actualización | Incluye el refinamiento específico del catálogo y navegación común. Editores conservan requisitos, consecuencias y confirmaciones. |
| Planificación, plan de proceso y versiones | Crear plan tras destinos; navegación y paneles simplificados. Reservas, calendario y advertencias no se pliegan. |
| Validaciones y carga/reasignación | Navegación discreta, filtros secundarios; responsables, vencimiento, estado y acción de revisión siguen visibles. |
| Tablero y rol de tareas | Navegación coherente; se conservan indicadores semánticos de progreso, filtros y selector de día. |
| Reportes | Menor énfasis de filtros/contexto y paneles; métricas, leyendas de interpretación y exportación permanecen disponibles. |
| Configuración | Paneles más sencillos; temas y preferencias Auto/Móvil/Escritorio conservan sus controles. |
| `/pantalla` | Conserva conexión/quiosco; no se agregan navegación ni acciones ficticias para forzar el patrón. |

Evidencia nueva en `.local/mobile-hierarchy/`, con la misma base aislada `sgol_task_checks_mobile_ios_20261030`:

- **Aprobado:** 602 comprobaciones en 43 rutas/variantes, Chromium y WebKit, claro/oscuro/special, 320 px y texto 100/200%; cero fallos y errores JavaScript (`themes.log`, `compact/themes-results.json`). El detalle legado con 503 esperado sigue siendo prueba de error, no detalle exitoso.
- **Aprobado:** 72 escenarios de ayudas, teclado, formularios sin modificaciones por presentación, selección y modos de vista (`interactions.log`). No equivalen a 72 escrituras de negocio.
- **Aprobado tras el ajuste final de orden:** 24 combinaciones de dos páginas, dos motores y seis anchos 320/390/430/768/769/1440; campos enviados intactos, orden móvil y retorno escritorio/impresión, texto ampliado (`order-final.log`, `hierarchy-results.json`). Ejecutor mantenido: `tools/Sgol.MobileChecks/mobile-hierarchy-checks.cjs`.
- **Aprobado:** 16 comparaciones de geometría escritorio/impresión con la hoja nueva habilitada/deshabilitada; ocho capturas reales revisadas (`visual-final.log`, `desktop-print.json`, `screen-*.png`). La primera comparación falló por medir antes de terminar el traslado asíncrono del DOM; la repetición espera los frames de restitución.
- **Fallo inicial delimitado:** la prueba nueva de anchos exigía ausencia de overflow también en escritorio a 769 px. Esa composición anterior conserva su ancho mínimo. Se restringió el criterio de reflujo al contenedor móvil, manteniendo la comprobación de restauración a 769/1440. No se afirma que el escritorio quepa en 769 px.
- **Aprobado:** compilación Release sin errores/advertencias, publicación y diff. Las pruebas de escritura del catálogo del apartado anterior siguen siendo evidencia aplicable; no se repitieron porque no se cambiaron sus contratos.
- **No ejecutado:** Safari físico, lector de pantalla, teclado virtual y escenarios históricos pendientes de la matriz original. El ajuste no elimina esas limitaciones.

Carga compartida ejecutada con autorización expresa: binario `.local/runtime/mobile-hierarchy-20261030/Web/Sgol.Web.dll`, PID Web anterior 33056, nuevo 7448. Se conservaron `sgol_local`, proxy y dependencias. `health/live`, `health/ready` y acceso LAN aprobados. Sesión real LAN: ayuda de Mi trabajo, Gestión de Tareas, panel oculto sin selección y visible tras seleccionar. SHA-256 de los siete recursos CSS/JS comprobados coincide con el árbol de trabajo (`.local/deploy-hierarchy-20261030/deployment.json`, `browser-verification.json`). No se hizo commit ni push ni limpieza de datos.
