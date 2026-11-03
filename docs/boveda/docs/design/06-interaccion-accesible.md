---
categoria: diseno
tipo: referencia
estado_documental: inspeccionado
clasificado_el: 2026-11-03
---

# Interacción accesible de SGOL

[Diseño actual](00-diseno-sgol.md) · [Componentes](03-componentes.md) · [Estructura](02-estructura-y-navegacion.md) · [Estados](04-patrones-y-estados.md)

## Estructura y nombres

El documento utiliza idioma `es-MX`. El layout común ofrece Saltar al contenido principal y un destino principal enfocable. Los menús tienen nombre accesible y el apartado activo se identifica con `aria-current`. Los iconos decorativos se ocultan a tecnologías de asistencia; los controles conservan texto visible o un nombre accesible.

Los componentes de tabla proporcionan caption y encabezados de columna. Los registros móviles conservan etiquetas asociadas al dato. Las galerías identifican las imágenes y las acciones anterior/siguiente. Los indicadores usan etiquetas para distinguir progreso, capacidad y calificación.

Fuentes: [layout](../../../../src/Sgol.Web/Pages/Shared/_Layout.cshtml), [tabla](../../../../src/Sgol.Web/Pages/Shared/_DataTable.cshtml), [tarjetas de validación](../../../../src/Sgol.Web/Pages/Validations/_ValidationCards.cshtml).

## Teclado y foco

| Interacción | Comportamiento implementado |
|---|---|
| Enlaces y botones | Elementos nativos con foco visible. |
| Desplegables | `summary` nativo o botón con `aria-expanded` en las adaptaciones. |
| Navegación móvil | Diálogo modal; Tab y Shift+Tab recorren los enlaces y botones visibles de forma circular. |
| Diálogo común | Al abrir enfoca el control designado; al cerrar devuelve el foco al activador registrado. Escape utiliza el comportamiento del diálogo nativo. |
| Menú de cuenta | Se cierra al salir, pulsar fuera o usar Escape; Escape devuelve el foco al activador. |
| Ayuda de cabecera | Cierra por el activador, clic exterior o Escape; no bloquea el resto de la página como un modal. |
| Barra lateral | Separador enfocable; flechas cambian ancho, Inicio contrae y Fin amplía. |
| Error de acceso | La región de error o aviso recibe foco. |

Los módulos de procesos, reportes, bono y tareas pueden añadir controladores específicos. El manejo compartido no demuestra por sí solo que toda combinación de diálogos tenga un recorrido correcto en todos los navegadores.

Fuentes: [components.js](../../../../src/Sgol.Web/wwwroot/js/components.js), [sidebar.js](../../../../src/Sgol.Web/wwwroot/js/sidebar.js), [desktop-workspace.js](../../../../src/Sgol.Web/wwwroot/js/desktop-workspace.js).

## Formularios y respuesta

Los campos compartidos asocian etiquetas y errores mediante identificadores. Los grupos relacionados usan leyenda. Los estados de carga y envío utilizan `aria-busy` en los recorridos conectados al controlador. Los avisos de error usan regiones de alerta; las confirmaciones y algunas actualizaciones usan regiones de estado.

Mostrar/Ocultar contraseña mantiene su nombre y estado al alternar. Los controles de filtros no se deshabilitan por el mero hecho de plegarse. Los scripts de adaptación conservan los controles y su formulario en vez de reemplazarlos por una copia visual independiente.

Fuentes: [_FormField](../../../../src/Sgol.Web/Pages/Shared/_FormField.cshtml), [_CredentialField](../../../../src/Sgol.Web/Pages/Shared/_CredentialField.cshtml), [mejora de filtros](../../../../src/Sgol.Web/wwwroot/js/desktop-workspace.js).

## Espacio disponible y movimiento

La composición utiliza límites de ancho, reflujo y contenedores desplazables para tablas y diálogos. En móvil se incrementa el tamaño de los campos y se utilizan objetivos comunes de 44 px; el login utiliza 48 px. Estas medidas describen las reglas compartidas, no una auditoría de todos los controles individuales.

Los textos largos pueden partirse en paneles y títulos. Las tablas conservan su desplazamiento interno cuando no cabe una representación vertical. El selector de composición Escritorio fuerza 1280 px incluso en un teléfono; ese comportamiento es diferente de la adaptación Automática.

Los estilos contienen tratamiento de `prefers-reduced-motion` para animaciones, incluidos el esqueleto y la aguja del bono. Las reglas de impresión ocultan navegación y acciones donde corresponda y restauran la composición gestionada por los adaptadores.

Fuentes: [components.css](../../../../src/Sgol.Web/wwwroot/css/components.css), [mobile.css](../../../../src/Sgol.Web/wwwroot/css/mobile.css), [task-bonus.css](../../../../src/Sgol.Web/wwwroot/css/task-bonus.css).

## Alcance comprobado

Esta referencia identifica los mecanismos presentes en el código. La conformidad visual, el contraste efectivo, el lector de pantalla, el zoom y la interacción en dispositivos físicos requieren pruebas sobre la pantalla renderizada. No se atribuye una validación global a SGOL por la existencia de atributos o estilos de accesibilidad.

## Ubicación documental

Pertenece a [Diseño e interacción](../../mapas/diseno.md). Estado documental: **inspeccionado**. La fecha de clasificación no es una fecha de verificación funcional.
