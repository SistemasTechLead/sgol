---
categoria: diseno
tipo: referencia
estado_documental: inspeccionado
clasificado_el: 2026-11-03
---

# Estructura y navegación de SGOL

[Diseño actual](00-diseno-sgol.md) · [Fundamentos](01-fundamentos-visuales.md) · [Pantallas](05-pantallas.md)

## Estructura operativa

El layout operativo contiene un enlace para saltar al contenido, cabecera superior, barra lateral y área principal. La cabecera muestra el título de la pantalla desde Razor, el nombre y rol de la sesión, y un avatar que abre el menú de cuenta con Cerrar sesión. La ayuda de pantalla aparece junto al título cuando hay contenido disponible.

El título superior está presente tanto en escritorio como en móvil. Los títulos duplicados del contenido se ocultan en pantalla mediante CSS, sin depender de la inicialización del script para mostrar el título superior. Los diálogos mantienen sus propios títulos. La impresión tiene reglas independientes.

En escritorio, la barra lateral contiene logo, destinos permitidos y contexto de sucursal. Su borde permite cambiar el ancho con arrastre o teclado entre 72 y 224 px; por debajo de 180 px se activa la presentación compacta. La preferencia se conserva en el navegador.

En móvil, un botón abre la navegación principal en un diálogo. El diálogo ofrece Cerrar navegación y los mismos destinos autorizados. La cabecera distribuye botón de navegación, título y ayuda, identidad y menú de cuenta.

Fuentes: [layout](../../../../src/Sgol.Web/Pages/Shared/_Layout.cshtml), [cabecera y ayuda](../../../../src/Sgol.Web/wwwroot/js/desktop-workspace.js), [barra lateral](../../../../src/Sgol.Web/wwwroot/js/sidebar.js).

## Destinos por rol

Esta tabla describe la visibilidad del destino principal y el filtro general de acceso. Cada formulario y servicio aplica permisos adicionales.

| Destino | Dirección | Asesor Externo | Subcoordinación | Piso de ventas |
|---|---|---|---|---|
| Mi trabajo | Sí | Sí | Sí | Sí |
| Tablero | Sí | Sí | Sí | Sí |
| Supervisión | Sí | Sí | Sí | — |
| Asignar tarea | Sí | Sí | Sí | — |
| Personas y accesos | Sí | — | — | — |
| Configuración | Sí | Sí | Sí | Sí |
| Tareas | Sí | Sí | — | — |
| Planificación | Sí | Sí | Sí | — |
| Reportes | Sí | Sí | Sí | — |

El nombre visible Asesor Externo corresponde al código persistido `ADMINISTRACION`. El orden visual coloca Tablero y Configuración después de los restantes destinos visibles. Las rutas `/supervision` pertenecen al contexto de Supervisión.

Fuentes: [RoleScreenAccess](../../../../src/Sgol.Web/Interface/Navigation/RoleScreenAccess.cs), [nombres de roles](../../../../src/Sgol.Web/Interface/Navigation/SessionPresentation.cs).

## Apartados dentro de cada módulo

`_SectionNavigation` produce enlaces GET con nombre accesible de navegación y `aria-current="page"` en el destino seleccionado. El destino activo tiene fondo y texto del par de acento; los demás destinos presentan superficie neutra. Crear o asignar son acciones diferenciadas de los destinos.

| Módulo | Apartados y acciones que puede presentar |
|---|---|
| Mi trabajo | Mis tareas, Mis procesos, Petición de arrastre. |
| Tareas | Tareas, Crear tarea, Procesos, Asignar tarea, Porcentaje para el cierre. Crear y configurar cierre se muestran a Dirección; otras opciones dependen del rol. |
| Planificación | Planes, Crear nuevo plan, Operaciones semanales, Plan de proceso. La creación y los apartados se filtran por rol y permiso de consulta. |
| Personas y accesos | Personas y accesos, Crear usuario, Especializaciones; navegación de Dirección. |
| Supervisión | Supervisión, Validación, Carga y reasignación, Porcentaje para el bono. |
| Tablero | Tablero, Rol de tareas. |

Cambiar entre Supervisión y Validación conserva los filtros aplicables de la consulta. Planificación distingue sus contextos mediante `mode`, y el detalle de un plan abierto desde Operaciones semanales mantiene ese contexto. Cobertura de cierre es un botón de apertura junto a la navegación de Planificación, fuera del elemento de navegación.

Fuente: [SectionNavigation](../../../../src/Sgol.Web/Presentation/Navigation/SectionNavigation.cs), [estilo final de controles](../../../../src/Sgol.Web/wwwroot/css/interactive-controls.css).

## Composición adaptable

El documento raíz define el contenedor `sgol-viewport`. El punto de cambio principal es 48 rem; determinados componentes tienen ajustes adicionales. Configuración ofrece tres opciones independientes del tema:

- Automática: utiliza el espacio disponible; incluye una adaptación para dispositivos táctiles en horizontal.
- Móvil: limita la composición a 420 px y la centra.
- Escritorio: establece un ancho mínimo de 1280 px; en un teléfono puede requerir desplazamiento o escala.

En móvil, las tablas seleccionadas se presentan como registros verticales con etiquetas y datos secundarios desplegables. Las matrices semanales ofrecen navegación por día y las editables conservan sus controles. Los scripts trasladan o envuelven controles existentes para ajustar la presentación; al cambiar de composición o imprimir restauran las ubicaciones que gestionan. Mi trabajo tiene una composición móvil propia.

El comportamiento compartido está en [mobile.js](../../../../src/Sgol.Web/wwwroot/js/mobile.js), [mobile-hierarchy.js](../../../../src/Sgol.Web/wwwroot/js/mobile-hierarchy.js), [mobile.css](../../../../src/Sgol.Web/wwwroot/css/mobile.css) y [theme.js](../../../../src/Sgol.Web/wwwroot/js/theme.js).

## Acceso, errores y quiosco

Acceso usa una composición de marca y formulario. Los errores de sistema conservan el contexto de sesión cuando está disponible y ofrecen una acción de recuperación. El quiosco usa `_BoardLayout`, con el tablero como contenido principal, navegación propia entre sus dos vistas y sin menú de administración ni bono interactivo. Estos casos no reciben la transformación de cabecera operativa.

Fuentes: [layout de tablero dedicado](../../../../src/Sgol.Web/Pages/Shared/_BoardLayout.cshtml), [errores](../../../../src/Sgol.Web/Pages/Shared/_SystemError.cshtml). Detalles en [Pantallas](05-pantallas.md).

## Ubicación documental

Pertenece a [Diseño e interacción](../../mapas/diseno.md). Estado documental: **inspeccionado**. La fecha de clasificación no es una fecha de verificación funcional.
