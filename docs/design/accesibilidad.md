# accesibilidad.md — SGOL / Loretta Zapatería

## Precisión vigente del estilo v2

La base visual v2 está aceptada y documentada en [ESTILO_VISUAL_V2.md](ESTILO_VISUAL_V2.md), por solicitud expresa de actualización del responsable el 2026-09-30. Sus reglas precisan los ejemplos anteriores: secundario neutro sin borde rojo, fuentes locales, logo/iconos, sesión a la derecha y acceso con composición de marca/formulario. Las cuatro correcciones solicitadas siguen en revisión visual; no hay nueva implementación productiva por este registro. Los contratos y mensajes funcionales se conservan.

Reglas de accesibilidad del sistema. La norma de aceptación de SGOL es WCAG 2.2 nivel AA, conforme a `F06_ESTRATEGIA_DE_PRUEBAS.md`.

## Renovación aprobada y límites de evidencia

[Adenda 55](../../F07_ADENDA_55_REFERENCIA_DE_DISENO_RENOVADO.md) incorpora las reglas de [referencia-renovada.md](referencia-renovada.md). Se conserva la paleta y todos los pares siguientes. El nuevo uso de superficie elevada como fondo de trabajo sólo admite los pares aprobados; paneles/controles permanecen blancos. No se intercambian colores decorativos y operativos.

Foco y área mínima se expresan mediante `--grosor-foco`, `--desfase-foco` y `--alto-control-minimo`. Enlaces de contenido llevan subrayado visible. Los estados usan texto e icono, y controles deshabilitados no conservan hover activo; los datos necesarios para leer una pantalla siguen legibles. `prefers-reduced-motion` elimina pulsación y animaciones de carga sin eliminar su mensaje.

Durante la implementación se verifica teclado, foco sin recortes, asociación de etiquetas/errores, área mínima, contraste efectivo, texto ampliado y reflow a 320 px CSS. Sólo el contenedor de tabla desplaza horizontalmente; filtros/acciones/diálogos refluyen. No se oculta overflow de html/body para aparentar cumplimiento. La navegación móvil continúa modal con Escape, foco contenido y retorno; la maqueta móvil abierta no reemplaza ese contrato.

La comprobación documental de valores no acredita contraste renderizado ni WCAG integral. Capturas externas son evidencia de maqueta; las mediciones productivas y revisión visual corresponden al hito que materialice el diseño. Se conserva el área mínima del proyecto, aunque la norma permita excepciones menores.

## Norma de aceptación y verificación

Las relaciones de contraste de este documento se calculan sobre los valores de `tokens.md` con la fórmula de luminancia relativa conservada por WCAG 2.2. La tabla existente no basta por sí sola para declarar conformidad: cada cambio de token o componente debe volver a verificar contraste, foco visible, operación por teclado, área mínima, etiquetas y asociación de errores contra WCAG 2.2 AA.

## Uso de color por capa

La paleta se organiza en dos capas con propósitos distintos. Los colores de marca (`--color-marca-crema`, `--color-marca-arena`, `--color-marca-durazno`, `--color-marca-decorativo`) son de uso exclusivamente decorativo: identifican a Loretta en encabezados, banners y pantallas de bienvenida. Nunca se usan como color de texto sobre `--color-superficie` ni como fondo de un control interactivo.

El acento operativo (`--color-acento`, `--color-acento-hover`, `--color-acento-texto`) es el único par de color que se usa en botones, enlaces, controles marcados y anillos de foco. Comparte la familia cromática de la marca — mismo matiz, en una variante de mayor contraste — pero es el que rige toda interacción.

Esta separación es la regla: la identidad visual vive en los elementos decorativos, la accesibilidad de los controles vive en el acento operativo, y los dos nunca se intercambian.

## Pares de color aprobados

| Par | Valores | Contraste | Texto normal (4.5:1) | Texto grande / UI (3:1) |
|---|---|---|---|---|
| texto-primario / superficie | `#2B2521` / `#FFFFFF` | 15.12:1 | Cumple | Cumple |
| texto-primario / superficie-elevada | `#2B2521` / `#F6F1EC` | 13.47:1 | Cumple | Cumple |
| texto-secundario / superficie | `#6B6055` / `#FFFFFF` | 6.12:1 | Cumple | Cumple |
| texto-secundario / superficie-elevada | `#6B6055` / `#F6F1EC` | 5.46:1 | Cumple | Cumple |
| acento / superficie | `#B64120` / `#FFFFFF` | 5.58:1 | Cumple | Cumple |
| acento-hover / superficie | `#9C371C` / `#FFFFFF` | 7.05:1 | Cumple | Cumple |
| acento-texto / acento | `#FFFFFF` / `#B64120` | 5.58:1 | Cumple | Cumple |
| acento-texto / acento-hover | `#FFFFFF` / `#9C371C` | 7.05:1 | Cumple | Cumple |
| borde-fuerte / superficie | `#9E9285` / `#FFFFFF` | 3.04:1 | No aplica a texto | Cumple (límite de control interactivo) |
| exito / exito-fondo | `#2F7A4F` / `#E7F3EC` | 4.59:1 | Cumple | Cumple |
| exito / superficie | `#2F7A4F` / `#FFFFFF` | 5.23:1 | Cumple | Cumple |
| advertencia / advertencia-fondo | `#92600B` / `#FBF0DC` | 4.77:1 | Cumple | Cumple |
| advertencia / superficie | `#92600B` / `#FFFFFF` | 5.38:1 | Cumple | Cumple |
| peligro / peligro-fondo | `#B23A2E` / `#FBEAE7` | 5.10:1 | Cumple | Cumple |
| peligro / superficie | `#B23A2E` / `#FFFFFF` | 5.94:1 | Cumple | Cumple |
| info / info-fondo | `#2A5F8A` / `#E8F1F7` | 5.92:1 | Cumple | Cumple |
| info / superficie | `#2A5F8A` / `#FFFFFF` | 6.77:1 | Cumple | Cumple |

Estos son los únicos pares de texto/fondo permitidos en la interfaz. Cualquier combinación fuera de esta tabla — por ejemplo, un color de marca decorativo detrás de texto — no se usa.

`--color-borde` (divisor visual entre filas, sin función de identificar un control) no forma parte de esta tabla porque no está sujeto al requisito de 3:1: es decorativo, no un límite que el usuario deba distinguir para operar el sistema. Donde el borde sí delimita un control interactivo — campos de formulario, tarjetas seleccionables — se usa `--color-borde-fuerte`, que sí cumple 3:1.

`--color-texto-deshabilitado` tiene un contraste intencionalmente menor (2.63:1 sobre `--color-superficie`) porque corresponde al texto de componentes inactivos, que la norma no exige que cumpla el mismo nivel de contraste que el texto operativo: un campo deshabilitado no participa en tareas obligatorias de lectura.

## Área mínima de clic

44×44 px CSS como área táctil o de clic mínima para cualquier control interactivo (botón, checkbox, ítem de menú, botón de paginación), aunque el elemento visual sea más pequeño — el padding invisible completa el área. En filas de tabla con varias acciones (por ejemplo, iconos de editar o validar), el área clicable se agranda con padding, nunca el ícono visual.

## Foco visible por teclado

Todo control interactivo usa `:focus-visible` (nunca `:focus` a secas, para no mostrar el anillo en un clic de mouse): `outline: var(--grosor-foco) solid var(--color-acento)` con `outline-offset: var(--desfase-foco)`. En botones destructivos el anillo usa `--color-peligro` en vez de `--color-acento`, para no confundir visualmente una acción irreversible con un control normal — ver `componentes.md`, botón destructivo.

El anillo nunca se suprime con `outline: none` sin reemplazo. Si un componente necesita un indicador de foco distinto al anillo por razones de layout (por ejemplo, una fila de tabla completa), el reemplazo mantiene el mismo contraste mínimo de 3:1 contra el fondo adyacente.

## Orden de tabulación en formularios

Sigue el orden visual de lectura (arriba a abajo, izquierda a derecha); no se usa `tabindex` positivo para reordenarlo manualmente. Dentro de un modal, el foco queda atrapado (focus trap) mientras esté abierto y regresa al elemento que lo abrió al cerrarse, tal como se documenta en `componentes.md`. El botón de acción destructiva de un modal de confirmación nunca es el primer elemento enfocable — ver `estados-y-mensajes.md`, confirmación destructiva.

## Viewport estrecho y reflow

En teléfono, el layout cambia a una columna sin exigir desplazamiento horizontal de página. La tabla de datos conserva su semántica y puede desplazarse dentro de su propio contenedor; la navegación precede al contenido en el DOM y ningún control desaparece por el ancho. Zoom, texto ampliado y orientación no bloquean botones, errores, títulos ni el foco visible.

Los controles que muestran u ocultan credenciales anuncian su estado, los grupos de radio usan `fieldset`/`legend`, los errores se asocian al control y el resumen puede recibir foco programático. El `dialog` nativo se abre modalmente, acepta Escape y devuelve el foco al disparador.

## Regla de color

Ningún estado, bandera o resultado se comunica solo por color. Todo badge, alerta o indicador lleva texto en español y, salvo el texto plano de cuerpo, un ícono. Esto no es opcional para daltonismo — es además lo único que funciona en una captura impresa en blanco y negro, un caso de uso real del sistema.
