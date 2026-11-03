---
categoria: diseno
tipo: referencia
estado_documental: inspeccionado
clasificado_el: 2026-11-03
---

# Fundamentos visuales de SGOL

[Diseño actual](00-diseno-sgol.md) · [Estructura](02-estructura-y-navegacion.md) · [Componentes](03-componentes.md)

## Identidad y recursos

La identidad utiliza el logo de Loretta en SVG, superficies cálidas y el nombre SGOL. El logo aparece en la barra lateral, la navegación móvil y el acceso. En las pantallas operativas, la barra superior identifica la pantalla abierta.

La tipografía operativa es Poppins local, en pesos 400, 500 y 600. Georgia, con respaldo Times New Roman y serif, se usa para la bienvenida de marca. Los iconos de interfaz son SVG del parcial compartido `_Icon`; los avatares tienen su propio generador y preferencias.

Fuentes: [logo](../../../../src/Sgol.Web/wwwroot/images/loretta.svg), [estilos base](../../../../src/Sgol.Web/wwwroot/css/components.css), [iconos](../../../../src/Sgol.Web/Pages/Shared/_Icon.cshtml).

## Temas

Hay tres apariencias: Claro, Oscuro y Especial. Claro usa blanco y crema; Oscuro usa superficies marrón oscuro y texto claro; Especial usa superficies rosadas y acento morado. El tema se aplica al documento antes de cargar los estilos y se conserva en ese navegador. El quiosco autenticado como pantalla utiliza tema oscuro.

| Token | Claro | Oscuro | Especial |
|---|---|---|---|
| `--color-superficie` | `#FFFFFF` | `#1F1B18` | `#FFFCFD` |
| `--color-superficie-elevada` | `#F6F1EC` | `#29231F` | `#FCEEF3` |
| `--color-borde` | `#E4DDD5` | `#494039` | `#E8D4E0` |
| `--color-borde-fuerte` | `#9E9285` | `#8D7E71` | `#A6819E` |
| `--color-texto-primario` | `#2B2521` | `#F2EAE2` | `#392C40` |
| `--color-texto-secundario` | `#6B6055` | `#C3B5A8` | `#70576B` |
| `--color-texto-deshabilitado` | `#A99E92` | `#9B8C7F` | `#A48C9B` |
| `--color-acento` | `#B64120` | `#F4A78A` | `#824579` |
| `--color-acento-hover` | `#9C371C` | `#FFC4AA` | `#693661` |
| `--color-acento-texto` | `#FFFFFF` | `#241B17` | `#FFFFFF` |
| `--color-capacidad` | `#85602B` | `#D7B16F` | `#85602B` |

Los colores semánticos distinguen éxito, advertencia, peligro e información. Especial conserva los valores semánticos base; Oscuro define sus propios pares de texto y fondo.

| Uso | Claro y Especial: texto / fondo | Oscuro: texto / fondo |
|---|---|---|
| Éxito | `#2F7A4F` / `#E7F3EC` | `#8ED5A8` / `#20382A` |
| Advertencia | `#92600B` / `#FBF0DC` | `#EEC777` / `#3C301C` |
| Peligro | `#B23A2E` / `#FBEAE7` | `#FFAAA0` / `#422723` |
| Información | `#2A5F8A` / `#E8F1F7` | `#9CCBEE` / `#223342` |

Los colores de las tareas expresan tiempo y entrega; su significado depende del contexto y se acompaña de texto. Ver [estados operativos](04-patrones-y-estados.md).

## Tipografía y densidad

| Token | Peso | Tamaño / interlínea |
|---|---|---|
| `--tipografia-micro` | 400 | 11 / 16 px |
| `--tipografia-pequena` | 400 | 12 / 16 px |
| `--tipografia-base` | 400 | 14 / 20 px |
| `--tipografia-media` | 500 | 16 / 24 px |
| `--tipografia-grande` | 600 | 20 / 28 px |
| `--tipografia-titulo` | 600 | 32 / 40 px |
| `--tipografia-titulo-compacto` | 600 | 24 / 32 px |
| `--tipografia-bienvenida` | 400, Georgia | 48 / 56 px |

La cabecera operativa usa su propia medida: título de 1.5 rem en escritorio y 1.125 rem en móvil, peso 600. Los campos móviles de texto, fecha y selección usan 16 px como base. El catálogo de tareas tiene celdas compactas de 8 px verticales y 12 px horizontales en escritorio; no hereda como regla universal el relleno de otras tablas.

## Espaciado y superficies

La escala compartida contiene 4, 8, 12, 16, 24, 32 y 48 px. El radio base es 4 px para controles, 8 px para tarjetas y 999 px para pastillas. Algunos componentes especializados, como la barra contextual de tareas, declaran un radio propio.

Los bordes base tienen 1 px. Las sombras baja, media y alta separan superficies, filtros y elementos flotantes. En móvil se reduce la decoración de paneles anidados. Los desplegables y acciones auxiliares tienen superficie neutra, borde y sombra discreta; los enlaces de prosa siguen siendo enlaces.

## Medidas de referencia

| Elemento | Valor base y alcance |
|---|---|
| Contenido | Token de 1280 px; el layout concreto decide su aplicación. |
| Formulario | Token de 640 px. |
| Modal común | Token de 480 px; los diálogos de tarea, procesos, reportes y bono tienen variantes. |
| Barra lateral | 224 px iniciales; ajustable entre 72 y 224 px. |
| Control común | Mínimo de 44 px; el login móvil eleva campos y acción principal a 48 px. |
| Icono común | 20 px; variante grande de 32 px; ayuda de cabecera con SVG de 17 px. |
| Avatar de sesión | Token de 40 px. |
| Logo | Tokens de 176 px y 256 px para bienvenida. |
| Foco | Token de grosor de 2 px; separación base de 2 px, con variantes por control. |

Fuente de valores: [tokens.css](../../../../src/Sgol.Web/wwwroot/css/tokens.css). Variantes: [desktop-workspace.css](../../../../src/Sgol.Web/wwwroot/css/desktop-workspace.css), [task-actions.css](../../../../src/Sgol.Web/wwwroot/css/task-actions.css), [mobile.css](../../../../src/Sgol.Web/wwwroot/css/mobile.css).

## Aplicación de estilos

El layout carga tokens y estilos de componentes antes de las adaptaciones móvil, navegación y espacio de trabajo. `interactive-controls.css` se carga después de esos estilos y define la presentación final de destinos y desplegables. Las páginas incorporan además estilos específicos de su módulo. Para comprobar una propiedad, se considera la cascada completa y el selector de la pantalla, no una declaración aislada.

El tema y la composición son preferencias independientes. Su selección se describe en [Configuración](05-pantallas.md); su aplicación está en [theme.js](../../../../src/Sgol.Web/wwwroot/js/theme.js).

## Ubicación documental

Pertenece a [Diseño e interacción](../../mapas/diseno.md). Estado documental: **inspeccionado**. La fecha de clasificación no es una fecha de verificación funcional.
