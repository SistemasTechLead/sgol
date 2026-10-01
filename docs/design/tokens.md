# tokens.md — SGOL / Loretta Zapatería

## Precisión vigente del estilo v2

La base visual v2 está aceptada y documentada en [ESTILO_VISUAL_V2.md](ESTILO_VISUAL_V2.md), por solicitud expresa de actualización del responsable el 2026-09-30. Sus reglas precisan los ejemplos anteriores: secundario neutro sin borde rojo, fuentes locales, logo/iconos, sesión a la derecha y acceso con composición de marca/formulario. Las correcciones y el complemento fueron aprobados mediante «Apruebo las correcciones y el complemento del plan para implementar». La implementación local se registra por grupo en [DISENO_RENOVADO_V2_IMPLEMENTACION.md](../traceability/DISENO_RENOVADO_V2_IMPLEMENTACION.md); la aprobación del diseño no equivale a publicación o integración. Los contratos y mensajes funcionales se conservan.

Variables CSS del sistema de diseño, organizadas por el rol que cumplen en la interfaz. Ningún nombre de variable usa el nombre del color: se nombran por función, para que un cambio de paleta futuro no obligue a renombrar nada.

Referencia renovada aprobada e incorporada localmente por [Adenda 55](../../F07_ADENDA_55_REFERENCIA_DE_DISENO_RENOVADO.md), mediante «Apruebo las secciones y la adenda», §§3–9 del [plan](PLAN_DISENO_RENOVADO.md). La [guía de adopción](referencia-renovada.md) distingue referencia documentada de implementación productiva. Estos valores se materializan únicamente en el alcance de un hito o tarea aprobado; las pantallas existentes conservan su implementación hasta su adaptación.

## Usos de superficie y jerarquía renovados

El área de trabajo autenticada usa `--color-superficie-elevada` como fondo cálido; encabezado de sesión, navegación, paneles y tablas usan `--color-superficie`. El mismo fondo cálido puede reforzar encabezados de tabla, hover y navegación activa. Las tablas permanecen claras y densas; ningún panel completo adopta el color del estado de una obligación. Los colores de marca siguen siendo decorativos y los pares accesibles siguen los de `accesibilidad.md`.

Un h1 operativo usa `--tipografia-titulo`, en viewport estrecho `--tipografia-titulo-compacto`; h2 de sección usa `--tipografia-grande` y h3 `--tipografia-media`. El título de bienvenida usa `--tipografia-bienvenida` con `--fuente-marca`; el vacío de marca conserva `--tipografia-titulo-compacto` con esa familia. Controles y celdas nunca heredan el tamaño de un título. No se agregan fuentes remotas ni se leen originales congelados.

## Colores de marca (decorativos)

Se usan para elementos de identidad — encabezados de sección, portadas internas, piezas de marca — nunca como color de texto sobre blanco ni como fondo de botón u otro control interactivo. Esa distinción existe para mantener el sistema accesible: los tonos decorativos y los tonos operativos cumplen funciones distintas y no se mezclan.

| Token | Valor | Cuándo usarlo |
|---|---|---|
| `--color-marca-crema` | `#F6F1EC` | Fondo alterno cálido en pantallas de marca o de estado vacío. No se usa en tablas de datos densas. |
| `--color-marca-arena` | `#EAD9C8` | Franjas o realces decorativos secundarios, de uso puntual. |
| `--color-marca-durazno` | `#F0C3A7` | Acento decorativo suave, igual que arena. No se usa como fondo de texto largo. |
| `--color-marca-decorativo` | `#E8927A` | Encabezados de sección de marca, banners internos, pantallas de bienvenida junto al logo. No se usa como color de texto ni como fondo de botón o control interactivo. |

## Superficies, texto y bordes

| Token | Valor | Cuándo usarlo |
|---|---|---|
| `--color-superficie` | `#FFFFFF` | Encabezado de sesión, navegación, paneles, controles y tablas. |
| `--color-superficie-elevada` | `#F6F1EC` | Fondo del área autenticada, encabezados de tabla, hover y navegación activa; realce sin sombra fuerte. |
| `--color-borde` | `#E4DDD5` | Separadores visuales sin función de identificar un componente interactivo: líneas entre filas, divisores de sección. |
| `--color-borde-fuerte` | `#9E9285` | Borde de campos de formulario, tablas con rejilla visible, cualquier borde que por sí solo delimite un control interactivo. |
| `--color-texto-primario` | `#2B2521` | Texto de lectura estándar: etiquetas, contenido de tabla, cuerpo. |
| `--color-texto-secundario` | `#6B6055` | Texto de apoyo: metadatos, fechas, ayudas, subtítulos. |
| `--color-texto-deshabilitado` | `#A99E92` | Texto de campos y controles deshabilitados. |
| `--color-overlay` | `rgba(43, 37, 33, 0.18)` | Fondo detrás de un diálogo modal; no se usa para texto ni controles. |

## Acento operativo

| Token | Valor | Cuándo usarlo |
|---|---|---|
| `--color-acento` | `#B64120` | Botón primario, enlaces, checkbox/radio marcado, anillo de foco: cualquier control interactivo. |
| `--color-acento-hover` | `#9C371C` | Estado hover/activo del acento. |
| `--color-acento-texto` | `#FFFFFF` | Texto o ícono sobre un fondo de acento (botón primario, badge sólido). |

## Colores semánticos

Cada uno con su versión de texto (para usar sobre `--color-superficie` o sobre su propio fondo tenue) y su fondo tenue (para badges y alertas). Ningún estado se comunica solo por color: en `estados-de-dominio.md` y `componentes.md` cada uno lleva también texto en español e ícono.

| Token | Valor | Cuándo usarlo |
|---|---|---|
| `--color-exito` | `#2F7A4F` | Texto e ícono de éxito. |
| `--color-exito-fondo` | `#E7F3EC` | Fondo tenue de badge o alerta de éxito. |
| `--color-advertencia` | `#92600B` | Texto e ícono de advertencia. |
| `--color-advertencia-fondo` | `#FBF0DC` | Fondo tenue de badge o alerta de advertencia. |
| `--color-peligro` | `#B23A2E` | Texto e ícono de peligro o error. |
| `--color-peligro-fondo` | `#FBEAE7` | Fondo tenue de badge o alerta de peligro. |
| `--color-info` | `#2A5F8A` | Texto e ícono informativo. |
| `--color-info-fondo` | `#E8F1F7` | Fondo tenue de badge o alerta informativa. |

## Tipografía

Poppins es la tipografía base de toda la interfaz operativa: formularios, tablas, botones, navegación. The Seasons se reserva para `--tipografia-bienvenida` en bienvenida y `--tipografia-titulo-compacto` en estados vacíos de marca; nunca en tablas, formularios ni badges.

| Token | Valor | Cuándo usarlo |
|---|---|---|
| `--fuente-marca` | `"The Seasons", Georgia, "Times New Roman", serif` | Solo `--tipografia-bienvenida` en bienvenida o `--tipografia-titulo-compacto` en vacíos de marca — nunca en tablas, formularios ni badges. |
| `--fuente-base` | `"Poppins", -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Arial, sans-serif` | Toda la interfaz operativa: texto, tablas, controles, navegación. |
| `--tipografia-micro` | `400 11px/16px var(--fuente-base)` | Timestamps, contadores, texto auxiliar mínimo dentro de celdas. |
| `--tipografia-pequena` | `400 12px/16px var(--fuente-base)` | Celdas de tabla, etiquetas de formulario, texto secundario de listas. |
| `--tipografia-base` | `400 14px/20px var(--fuente-base)` | Texto de cuerpo estándar, valores de campo, contenido general. |
| `--tipografia-media` | `500 16px/24px var(--fuente-base)` | h3 de agrupación y subtítulos. |
| `--tipografia-grande` | `600 20px/28px var(--fuente-base)` | h2 de sección. |
| `--tipografia-titulo` | `600 32px/40px var(--fuente-base)` | h1 de página operativa en escritorio. |
| `--tipografia-titulo-compacto` | `600 24px/32px var(--fuente-base)` | h1 en viewport estrecho; título de bienvenida o vacío de marca. |

## Espaciado (escala de 4px)

| Token | Valor | Cuándo usarlo |
|---|---|---|
| `--espacio-4` | `4px` | Separación mínima: entre ícono y texto, padding vertical de badges. |
| `--espacio-8` | `8px` | Separación entre elementos relacionados dentro de un control (campo y etiqueta). |
| `--espacio-12` | `12px` | Padding interno de celdas de tabla, padding vertical de botones. |
| `--espacio-16` | `16px` | Padding de tarjetas y paneles, separación entre campos de un formulario. |
| `--espacio-24` | `24px` | Separación entre secciones dentro de una misma pantalla. |
| `--espacio-32` | `32px` | Separación entre bloques mayores (encabezado de página y contenido). |
| `--espacio-48` | `48px` | Márgenes exteriores de pantalla completa, separación antes o después de un modal. |

## Radios

| Token | Valor | Cuándo usarlo |
|---|---|---|
| `--radio-control` | `4px` | Campos de texto, botones, selects. Redondeo mínimo: es una herramienta de trabajo densa, no un producto de consumo. |
| `--radio-tarjeta` | `8px` | Tarjetas, paneles, modales. |
| `--radio-pastilla` | `999px` | Badges de estado, chips, contador de pendientes. |

## Sombras

| Token | Valor | Cuándo usarlo |
|---|---|---|
| `--sombra-baja` | `0 1px 2px rgba(43, 37, 33, 0.08)` | Elevación mínima: fila en hover, tarjeta en reposo. |
| `--sombra-media` | `0 2px 8px rgba(43, 37, 33, 0.12)` | Menús desplegables, popovers, tarjetas en hover. |
| `--sombra-alta` | `0 8px 24px rgba(43, 37, 33, 0.18)` | Modales, diálogos de confirmación. |

## Anchos de contenedor

| Token | Valor | Cuándo usarlo |
|---|---|---|
| `--ancho-contenido` | `1280px` | Ancho máximo del área de trabajo principal (tablas, matrices). |
| `--ancho-formulario` | `640px` | Formularios de una columna, diálogos medianos. |
| `--ancho-modal` | `480px` | Modales de confirmación, diálogos pequeños. |

El viewport estrecho comienza en `48rem`. Este umbral se declara literalmente sólo en la condición `@media`, porque las variables CSS no son válidas como límite de una media query; no autoriza otros valores visuales literales en hojas consumidoras.

## Primitivas compartidas renovadas

| Token | Valor | Uso permitido |
|---|---|---|
| `--grosor-borde` | `1px` | Divisores y contornos. |
| `--grosor-foco` | `2px` | Anillo de foco. |
| `--desfase-foco` | `2px` | Separación del anillo. |
| `--alto-control-minimo` | `44px` | Alto mínimo interactivo; también ancho mínimo de controles de icono. |
| `--tamano-icono` | `20px` | Iconos de controles y navegación. |
| `--tamano-icono-grande` | `32px` | Icono de vacío o mensaje de sección. |
| `--ancho-navegacion` | `224px` | Columna lateral de escritorio; no mínimo de página. |
| `--alto-esqueleto` | `16px` | Bloque de carga dentro de fila reservada; no alto universal de fila. |
| `--duracion-esqueleto` | `1.4s` | Animación de carga existente, anulada con movimiento reducido. |
| `--peso-medio` | `500` | Nombres de registros, navegación y columnas. |
| `--peso-fuerte` | `600` | Énfasis puntual de valores y títulos. |

Las hojas consumidoras usan variables también para borde, foco y área mínima. No se importan aliases de la maqueta, anchos por columna, tracking ni tiempos adicionales. El avatar y el mínimo específico de la bandeja usan exclusivamente las primitivas v2 declaradas debajo. La regla de `@media` anterior es la única excepción para su umbral; no habilita tamaños, espaciados o colores literales en consumidores.

## Bloque `:root` completo

```css
:root {
  /* Colores de marca (decorativos) */
  --color-marca-crema: #F6F1EC;
  --color-marca-arena: #EAD9C8;
  --color-marca-durazno: #F0C3A7;
  --color-marca-decorativo: #E8927A;

  /* Superficies, texto, bordes */
  --color-superficie: #FFFFFF;
  --color-superficie-elevada: #F6F1EC;
  --color-borde: #E4DDD5;
  --color-borde-fuerte: #9E9285;
  --color-texto-primario: #2B2521;
  --color-texto-secundario: #6B6055;
  --color-texto-deshabilitado: #A99E92;
  --color-overlay: rgba(43, 37, 33, 0.18);

  /* Acento operativo */
  --color-acento: #B64120;
  --color-acento-hover: #9C371C;
  --color-acento-texto: #FFFFFF;

  /* Semánticos */
  --color-exito: #2F7A4F;
  --color-exito-fondo: #E7F3EC;
  --color-advertencia: #92600B;
  --color-advertencia-fondo: #FBF0DC;
  --color-peligro: #B23A2E;
  --color-peligro-fondo: #FBEAE7;
  --color-info: #2A5F8A;
  --color-info-fondo: #E8F1F7;

  /* Tipografía */
  --fuente-marca: "The Seasons", Georgia, "Times New Roman", serif;
  --fuente-base: "Poppins", -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Arial, sans-serif;
  --tipografia-micro: 400 11px/16px var(--fuente-base);
  --tipografia-pequena: 400 12px/16px var(--fuente-base);
  --tipografia-base: 400 14px/20px var(--fuente-base);
  --tipografia-media: 500 16px/24px var(--fuente-base);
  --tipografia-grande: 600 20px/28px var(--fuente-base);
  --tipografia-titulo: 600 32px/40px var(--fuente-base);
  --tipografia-titulo-compacto: 600 24px/32px var(--fuente-base);

  /* Espaciado */
  --espacio-4: 4px;
  --espacio-8: 8px;
  --espacio-12: 12px;
  --espacio-16: 16px;
  --espacio-24: 24px;
  --espacio-32: 32px;
  --espacio-48: 48px;

  /* Radios */
  --radio-control: 4px;
  --radio-tarjeta: 8px;
  --radio-pastilla: 999px;

  /* Sombras */
  --sombra-baja: 0 1px 2px rgba(43, 37, 33, 0.08);
  --sombra-media: 0 2px 8px rgba(43, 37, 33, 0.12);
  --sombra-alta: 0 8px 24px rgba(43, 37, 33, 0.18);

  /* Anchos de contenedor */
  --ancho-contenido: 1280px;
  --ancho-formulario: 640px;
  --ancho-modal: 480px;

  /* Primitivas compartidas renovadas */
  --grosor-borde: 1px;
  --grosor-foco: 2px;
  --desfase-foco: 2px;
  --alto-control-minimo: 44px;
  --tamano-icono: 20px;
  --tamano-icono-grande: 32px;
  --ancho-navegacion: 224px;
  --alto-esqueleto: 16px;
  --duracion-esqueleto: 1.4s;
  --peso-medio: 500;
  --peso-fuerte: 600;

  /* Estilo v2 aceptado: composición y marca */
  --alto-encabezado: 88px;
  --diametro-avatar: 40px;
  --ancho-logo: 176px;
  --ancho-logo-bienvenida: 256px;
  --alto-bienvenida: 680px;
  --ancho-acceso: 1040px;
  --grosor-icono: 1.65;
  --ancho-filtro: 192px;
  --ancho-tabla-bandeja: 840px;
  --tipografia-bienvenida: 400 48px/56px var(--fuente-marca);
}
```

## Primitivas del estilo v2

Base aceptada; valores de ESTILO_VISUAL_V2.md. No son evidencia de implementación.

| Variable | Valor | Uso |
|---|---|---|
| `--alto-encabezado` | `88px` | Cabecera escritorio; en estrecho altura automática |
| `--diametro-avatar` | `40px` | Iniciales decorativas de sesión |
| `--ancho-logo` | `176px` | Marca lateral; max-width:100% |
| `--ancho-logo-bienvenida` | `256px` | Marca centrada de acceso; max-width:100% |
| `--alto-bienvenida` | `680px` | Composición de acceso escritorio; no mínimo fijo móvil |
| `--ancho-acceso` | `1040px` | Máximo de composición marca/formulario |
| `--grosor-icono` | `1.65` | Trazo SVG |
| `--ancho-filtro` | `192px` | Filtro compacto, ancho disponible en móvil |
| `--ancho-tabla-bandeja` | `840px` | Sólo tabla de bandeja agrupada, scroll propio |
| `--tipografia-bienvenida` | `400 48px/56px var(--fuente-marca)` | Bienvenida de marca, nunca operativo |

Poppins local 400/500/600 y The Seasons Regular conforme a procedencia y límites de ESTILO_VISUAL_V2.md. Mantener fallback y font-display:swap. No leer fuentes congeladas.
