# componentes.md — SGOL / Loretta Zapatería

## Precisión vigente del estilo v2

La base visual v2 está aceptada y documentada en [ESTILO_VISUAL_V2.md](ESTILO_VISUAL_V2.md), por solicitud expresa de actualización del responsable el 2026-09-30. Sus reglas precisan los ejemplos anteriores: secundario neutro sin borde rojo, fuentes locales, logo/iconos, sesión a la derecha y acceso con composición de marca/formulario. Las correcciones y el complemento fueron aprobados mediante «Apruebo las correcciones y el complemento del plan para implementar». La implementación local se registra por grupo en [DISENO_RENOVADO_V2_IMPLEMENTACION.md](../traceability/DISENO_RENOVADO_V2_IMPLEMENTACION.md); la aprobación del diseño no equivale a publicación o integración. Los contratos y mensajes funcionales se conservan.

## Referencia renovada aprobada — composición compartida

[Adenda 55](../../F07_ADENDA_55_REFERENCIA_DE_DISENO_RENOVADO.md) incorpora §§3–9 del [plan aprobado](PLAN_DISENO_RENOVADO.md) mediante «Apruebo las secciones y la adenda». Las reglas siguientes son operativas para las tareas pendientes y el hito de adaptación. Las composiciones funcionales específicas, los seis estados y el catálogo de mensajes se conservan. La [guía](referencia-renovada.md) define la transición; esta documentación no acredita implementación productiva.

### Layout y encabezados compartidos

El shell combina encabezado de sesión, columna lateral y main con ancho máximo `--ancho-contenido`. Conserva el orden DOM: salto al contenido, identidad/sesión/logout, navegación y main. La posición visual no reordena tabulación. Fondo de trabajo `--color-superficie-elevada`; encabezado, navegación y paneles `--color-superficie`. Main usa padding `--espacio-32`, en estrecho `--espacio-16`. Navegación usa `--ancho-navegacion`, separador `--grosor-borde`/`--color-borde` y comportamiento móvil modal de [navegacion.md](navegacion.md).

Encabezado de página: un h1 en `--tipografia-titulo` (estrecho: `--tipografia-titulo-compacto`), descripción ya aprobada si existe, contexto opcional confirmado y acciones autorizadas. No inventa breadcrumb, textos decorativos, período ni totales: el conteo de una página con cursor sólo acredita los elementos recibidos. Acceso/MFA siguen fuera del shell; composición de marca/formulario con `--ancho-acceso` y límites internos de `--ancho-formulario`, conforme a ESTILO_VISUAL_V2.md.

Panel de sección: fondo blanco, borde decorativo por variables, `--radio-tarjeta` y `--sombra-baja`. Cabecera con h2 en `--tipografia-grande`, ayuda y acciones, padding `--espacio-24`; h3 usa `--tipografia-media`. Banda de filtros con gap `--espacio-12`, padding vertical `--espacio-16` y horizontal `--espacio-24`. En estrecho cabecera/filtros usan `--espacio-16` y wrap. Separación entre secciones `--espacio-24`; del encabezado al contenido `--espacio-32`.

Una columna es el valor predeterminado. Dos paneles independientes pueden compartir fila si caben sin recortes y pasan a una columna en estrecho, en el mismo orden. Tablas densas ocupan el ancho disponible. La base se materializa extendiendo los parciales/CSS compartidos existentes, sin biblioteca paralela; cada tarea sólo materializa los componentes necesarios dentro de su alcance.

### Controles, densidad y agrupación

Se conservan primario, secundario y destructivo con una acción primaria por bloque. Campos nativos, select, textarea, credencial, fecha, checkbox y radio conservan etiquetas, ayudas, errores, radio de control y padding de la escala. Campos usan superficie blanca y borde fuerte. Controles no heredan tamaño de título en teléfono. Área mínima por `--alto-control-minimo`; iconos por `--tamano-icono`, vacío por `--tamano-icono-grande`; grosor/desfase de foco por sus tokens. El hover de un control deshabilitado nunca queda activo.

La variante de botón **textual** es auxiliar: fondo transparente, texto en acento, mismo padding `--espacio-8`/`--espacio-16`, radio y área mínima que los demás botones. Hover en superficie elevada, foco completo; deshabilitado usa texto deshabilitado sin hover, ocupado usa mensaje aprobado/spinner y prevención de segundo envío. Error y vacío corresponden a su región consumidora. Usar enlace para navegar y botón para una operación; nunca cambiar su semántica para obtener el aspecto visual.

Tablas mantienen caption, th/scope, columnas y cursor Anterior/Siguiente; encabezado blanco, texto secundario y divisores suaves. Celdas simples usan padding vertical `--espacio-8` y horizontal `--espacio-12`; celdas con varios datos `--espacio-16`, en estrecho `--espacio-12`. Acciones siempre conservan área mínima. Código/contexto puede usar tipografía pequeña; nombre usa base y `--peso-medio`; metadatos son secundarios. Agrupar datos en una celda requiere mantener etiquetas, asociación y todos los valores. No se truncan datos esenciales ni convierten filas a tarjetas.

Badges conservan pastilla, par semántico, texto e icono; los grupos usan wrap y gap `--espacio-8`. Estado base y bandera siguen separados. Alertas conservan icono/texto, par semántico, `--radio-tarjeta` y padding `--espacio-16`. Diálogos conservan superficie blanca, sombra alta, radio y anchos vigentes, Cancelar inicial, Escape y retorno; motivo sólo si el contrato lo exige. Subida conserva progreso, cuarentena y estados de escaneo; sólo LIMPIO habilita el vínculo contractual.

### Estados y accesibilidad transversales

| Estado de pantalla | Cobertura exigida |
|---|---|
| Normal | Datos confirmados, jerarquía estable y acciones permitidas. |
| Foco | `:focus-visible` por variables, sin recortes; acento o peligro según componente, orden DOM y retorno vigente. |
| Deshabilitado | Control temporalmente inactivo; lectura sigue legible. Sin autorización se oculta la acción. |
| Error | Alerta de sección/resumen enfocable, error asociado; conflicto visible y sin reintento automático. |
| Cargando | `aria-busy` regional y mensaje aprobado; prevención de doble envío, esqueleto que reserva tabla o progreso en botón/upload. |
| Vacío | Ausencia, filtros e historia según mensajes aprobados; sólo acciones disponibles y autorizadas. |

La matriz existente conserva sus «No aplica» por componente: seis estados de pantalla no obligan a crear seis variantes de un elemento estático. Esqueleto usa `--alto-esqueleto` dentro de filas reservadas y `--duracion-esqueleto`; `prefers-reduced-motion` elimina la animación conservando el mensaje. Enlaces de contenido se subrayan. Sólo el contenedor de tabla desplaza horizontalmente; filtros/acciones/diálogos refluyen. No se oculta overflow de html/body para encubrir desbordamientos. Aplicar WCAG 2.2 AA, área mínima del proyecto y verificaciones de [accesibilidad.md](accesibilidad.md).

### Precedencia de ejemplos históricos

Las extensiones aprobadas de cursor, `dialog` nativo y mensajes seguros prevalecen sobre los ejemplos base antiguos de páginas numeradas, `div role="alertdialog"` o acciones ilustrativas. Los ejemplos no crean ordenación, borrado, enlaces ni mensajes ajenos al contrato consumidor. Los registros funcionales siguientes conservan su alcance; el título «aprobada» no significa que todas las pantallas ya tengan el diseño renovado.

## UI-E01/E02/E03 — FRONT-016 aprobada

Adenda 54 incorpora íntegramente §§4–6 de `docs/traceability/FRONT_016_PLAN_DE_IMPLEMENTACION.md`, aprobados mediante «Bueno sigue con la tarea, apruebo la adenda integramente». Son la composición operativa de bandeja propia y avisos en `/mi-trabajo`, consulta separada por alcance y detalle GET con historia en `/mi-trabajo/tareas/{obligationId:guid}`. Reutiliza tabla/cursor, filtros GET, alertas, badges, vacío y foco; conserva seis estados y responsive. Marcación individual sin diálogo ni motivo, botón ocupado y foco a Leído o al encabezado si desaparece la fila. Carga de consultas usa esqueleto de tabla y aria-busy. No incluye evidencia aportable ni conclusión. BR-D13 se consume; BR-N04 se aplica.

## UI-G05 y UI-G06 aprobadas para FRONT-015

El responsable aprobó íntegramente las secciones 2–8 de `F07_ADENDA_53_PROPUESTA_CONSUMIDOR_FRONT_015.md` mediante «Apruebo íntegramente las secciones 2–8» el 2026-09-29. Sus §§6/7 se incorporan como composición operativa específica: plan semanal actual e historia de snapshots, tablas con cursor, filtros de año/semana y confirmación contextual de publicación sin motivo. La variante del dialog base omite textarea porque el comando sólo admite cuerpo vacío; conserva Cancelar como foco inicial, Escape, retorno al disparador y acción primaria. UI-G05 se presenta a los cuatro roles con PER-PLAN-VER; UI-G06 sólo ofrece publicar con PER-PLAN-PUBLICAR. Se conservan los seis estados, tokens y accesibilidad existentes.

Inventario de componentes base. La densidad es intencional: quien usa este sistema viene de Excel y espera ver muchas filas y controles compactos, no tarjetas espaciadas de sitio de marketing. Todo el marcado usa exclusivamente las variables de `tokens.md` — cero valores sueltos.

Todo control interactivo debe tener foco visible por teclado (`:focus-visible`, nunca solo `:focus`, para no mostrar el anillo en un clic de mouse) y todo estado de error debe ir acompañado de texto, no solo de un borde rojo — ver `estados-y-mensajes.md`.

## Campo de texto

| Estado | Descripción visual | Tokens |
|---|---|---|
| Normal | Borde delgado, fondo blanco, texto primario. | `--color-borde-fuerte`, `--color-superficie`, `--color-texto-primario`, `--radio-control` |
| Hover | Borde ligeramente más oscuro, sin cambio de fondo. | `--color-texto-secundario` (borde en hover) |
| Foco visible | Anillo de 2px en acento alrededor del control, borde cambia a acento. | `--color-acento`, `outline-offset: var(--desfase-foco)` |
| Deshabilitado | Fondo superficie-elevada, texto deshabilitado, cursor `not-allowed`, sin hover ni foco. | `--color-superficie-elevada`, `--color-texto-deshabilitado`, `--color-borde` |
| Error | Borde en peligro, mensaje de error debajo con el mismo color y un ícono. | `--color-peligro`, `--color-peligro-fondo` (solo si se agrega fondo tenue al mensaje) |
| Cargando | No aplica a un campo de texto individual — ver Tabla y Subida de archivo. | — |

```html
<label class="campo">
  <span class="campo__etiqueta">Nombre del colaborador</span>
  <input class="campo__control" type="text" aria-invalid="false" />
</label>

<label class="campo campo--error">
  <span class="campo__etiqueta">Minutos asignados</span>
  <input class="campo__control" type="text" aria-invalid="true" aria-describedby="campo-minutos-error" />
  <span id="campo-minutos-error" class="campo__mensaje-error">Ingresa un número mayor a cero.</span>
</label>
```

```css
.campo__control {
  font: var(--tipografia-base);
  color: var(--color-texto-primario);
  background: var(--color-superficie);
  border: var(--grosor-borde) solid var(--color-borde-fuerte);
  border-radius: var(--radio-control);
  padding: var(--espacio-8) var(--espacio-12);
}
.campo__control:not(:disabled):hover { border-color: var(--color-texto-secundario); }
.campo__control:focus-visible {
  outline: var(--grosor-foco) solid var(--color-acento);
  outline-offset: var(--desfase-foco);
  border-color: var(--color-acento);
}
.campo__control:disabled {
  background: var(--color-superficie-elevada);
  color: var(--color-texto-deshabilitado);
  border-color: var(--color-borde);
  cursor: not-allowed;
}
.campo--error .campo__control { border-color: var(--color-peligro); }
.campo__mensaje-error { font: var(--tipografia-pequena); color: var(--color-peligro); }
```

## Select

Mismos estados y mismos tokens que campo de texto — la única diferencia visual es el ícono de flecha, que hereda `--color-texto-secundario` en normal y `--color-texto-deshabilitado` cuando el control está deshabilitado.

| Estado | Descripción visual | Tokens |
|---|---|---|
| Normal | Igual que campo de texto, con ícono de flecha a la derecha. | `--color-borde-fuerte`, `--color-texto-secundario` (ícono) |
| Hover | Borde más oscuro. | `--color-texto-secundario` |
| Foco visible | Anillo de acento. | `--color-acento` |
| Deshabilitado | Fondo elevado, texto e ícono deshabilitados. | `--color-superficie-elevada`, `--color-texto-deshabilitado` |
| Error | Borde en peligro + mensaje. | `--color-peligro` |
| Cargando | Ícono de flecha se sustituye por un spinner mientras se llenan las opciones (por ejemplo, sucursales dependientes de otra selección). | `--color-texto-secundario` |

```html
<label class="campo">
  <span class="campo__etiqueta">Sucursal</span>
  <select class="campo__control campo__control--select">
    <option>Selecciona una sucursal</option>
  </select>
</label>
```

## Checkbox

| Estado | Descripción visual | Tokens |
|---|---|---|
| Normal | Cuadro con borde, sin relleno. | `--color-borde-fuerte` |
| Hover | Borde en acento tenue (sin llenar). | `--color-acento` (solo borde) |
| Foco visible | Anillo de 2px alrededor del cuadro completo. | `--color-acento` |
| Deshabilitado | Cuadro y check en tono deshabilitado, sin hover. | `--color-texto-deshabilitado`, `--color-borde` |
| Marcado | Fondo acento, check en `--color-acento-texto`. | `--color-acento`, `--color-acento-texto` |
| Error | Borde en peligro (uso: checkbox obligatorio no marcado al enviar formulario). | `--color-peligro` |

```html
<label class="casilla">
  <input type="checkbox" class="casilla__control" />
  <span class="casilla__caja" aria-hidden="true"></span>
  <span class="casilla__texto">Confirmo que la evidencia es correcta</span>
</label>
```

## Botón primario

Una sola acción primaria por pantalla o por bloque — nunca dos botones primarios compitiendo.

| Estado | Descripción visual | Tokens |
|---|---|---|
| Normal | Fondo acento, texto acento-texto, sin borde. | `--color-acento`, `--color-acento-texto`, `--radio-control` |
| Hover | Fondo acento-hover. | `--color-acento-hover` |
| Foco visible | Anillo de 2px, offset 2px, además del fondo. | `--color-acento` (outline) |
| Deshabilitado | Fondo superficie-elevada, texto deshabilitado, sin sombra. | `--color-superficie-elevada`, `--color-texto-deshabilitado` |
| Cargando | Fondo acento-hover fijo, texto reemplazado por spinner + palabra "Guardando…", botón inerte (`aria-busy="true"`, `disabled`). | `--color-acento-hover`, `--color-acento-texto` |

```html
<button class="boton boton--primario" type="submit">
  Marcar como concluida
</button>

<button class="boton boton--primario" type="submit" aria-busy="true" disabled>
  <svg class="boton__spinner" aria-hidden="true"><!-- spinner --></svg>
  Guardando…
</button>
```

```css
.boton--primario {
  font: var(--tipografia-base);
  background: var(--color-acento);
  color: var(--color-acento-texto);
  border: none;
  border-radius: var(--radio-control);
  padding: var(--espacio-8) var(--espacio-16);
}
.boton--primario:not(:disabled):hover { background: var(--color-acento-hover); }
.boton--primario:focus-visible { outline: var(--grosor-foco) solid var(--color-acento); outline-offset: var(--desfase-foco); }
.boton--primario:disabled {
  background: var(--color-superficie-elevada);
  color: var(--color-texto-deshabilitado);
  cursor: not-allowed;
}
```

## Botón secundario

| Estado | Descripción visual | Tokens |
|---|---|---|
| Normal | Fondo superficie-elevada, texto primario, contorno transparente. | `--color-superficie-elevada`, `--color-texto-primario` |
| Hover | Fondo marca-arena, texto primario; sin contorno rojo. | `--color-marca-arena`, `--color-texto-primario` |
| Foco visible | Anillo de acento. | `--color-acento` |
| Deshabilitado | Fondo superficie-elevada y texto deshabilitado; sin hover. | `--color-superficie-elevada`, `--color-texto-deshabilitado` |
| Cargando | Igual patrón que el primario: spinner + texto de progreso, `aria-busy`. | `--color-acento` |

```html
<button class="boton boton--secundario" type="button">Cancelar</button>
```

## Botón destructivo

Reservado para acciones irreversibles (rechazar una solicitud, eliminar una configuración en borrador). Siempre exige confirmación — ver modal de confirmación.

| Estado | Descripción visual | Tokens |
|---|---|---|
| Normal | Fondo peligro, texto blanco. | `--color-peligro`, `#FFFFFF` |
| Hover | Fondo peligro oscurecido (ver nota). | `--color-peligro` con `filter: brightness(0.88)` — no hay token de hover dedicado para peligro porque es una acción de bajo uso; se deriva en tiempo de estilo en vez de sumar otro token permanente. |
| Foco visible | Anillo en peligro, no en acento — para no confundir visualmente con una acción primaria normal. | `--color-peligro` |
| Deshabilitado | Igual patrón que botón primario. | `--color-superficie-elevada`, `--color-texto-deshabilitado` |
| Cargando | Spinner + "Rechazando…", `aria-busy="true"`. | `#FFFFFF` sobre `--color-peligro` |

```html
<button class="boton boton--destructivo" type="button">Rechazar solicitud</button>
```

## Tabla con encabezado y paginación

Componente central del sistema — la mayoría de las pantallas son variaciones de esto.

| Estado | Descripción visual | Tokens |
|---|---|---|
| Normal | Encabezado en superficie blanca, texto secundario en peso medio, filas en superficie con borde inferior sutil. | `--color-superficie`, `--color-texto-secundario`, `--color-borde` |
| Fila hover | Fondo superficie-elevada. | `--color-superficie-elevada` |
| Fila con foco (navegación por teclado, si la tabla es interactiva) | Anillo interno de acento en la celda activa. | `--color-acento` |
| Encabezado ordenable, foco visible | Anillo de acento en el botón de la columna. | `--color-acento` |
| Vacía | Ver `estados-y-mensajes.md`, sección de estado vacío. | — |
| Cargando | Filas esqueleto (bloques grises pulsantes) en vez de spinner central, para no saltar el layout. Ver `estados-y-mensajes.md`. | `--color-borde` |
| Paginación, botón normal | Texto secundario, sin fondo. | `--color-texto-secundario` |
| Paginación, página activa | Fondo acento tenue... en realidad fondo `--color-superficie-elevada` con texto en acento y borde inferior en acento, para no repetir el patrón de botón primario en un control de navegación. | `--color-acento`, `--color-superficie-elevada` |
| Paginación, deshabilitado (primera/última página) | Texto deshabilitado. | `--color-texto-deshabilitado` |

```html
<table class="tabla">
  <thead class="tabla__encabezado">
    <tr>
      <th><button class="tabla__orden">Colaborador</button></th>
      <th><button class="tabla__orden">Min. asignados</button></th>
      <th>Pendientes</th>
    </tr>
  </thead>
  <tbody>
    <tr class="tabla__fila">
      <td>Vianney Viridiana Villareal Villa</td>
      <td>980</td>
      <td>84</td>
    </tr>
  </tbody>
</table>

<nav class="paginacion" aria-label="Paginación de tabla">
  <button class="paginacion__boton" disabled>Anterior</button>
  <button class="paginacion__boton paginacion__boton--activo" aria-current="page">1</button>
  <button class="paginacion__boton">2</button>
  <button class="paginacion__boton">Siguiente</button>
</nav>
```

```css
.tabla__encabezado { background: var(--color-superficie); }
.tabla__encabezado th { font: var(--tipografia-pequena); color: var(--color-texto-secundario); padding: var(--espacio-8) var(--espacio-12); }
.tabla__fila td { font: var(--tipografia-base); color: var(--color-texto-primario); padding: var(--espacio-8) var(--espacio-12); border-bottom: var(--grosor-borde) solid var(--color-borde); }
.tabla__fila:hover { background: var(--color-superficie-elevada); }
.tabla__orden:focus-visible { outline: var(--grosor-foco) solid var(--color-acento); outline-offset: var(--desfase-foco); }
```

## Badge de estado

Ver `estados-de-dominio.md` para la tabla completa de qué color/ícono/texto usa cada estado. Aquí solo el patrón estructural.

| Estado del badge en sí | Descripción visual | Tokens |
|---|---|---|
| Normal | Fondo tenue del semántico correspondiente, texto del mismo semántico, ícono del mismo color, radio píldora. | Depende del estado — ver `estados-de-dominio.md` |
| Dentro de fila con foco (si el badge es también un botón, p. ej. para expandir detalle) | Anillo de acento. | `--color-acento` |

```html
<span class="badge badge--exito">
  <svg class="badge__icono" aria-hidden="true"><!-- check --></svg>
  Concluida
</span>
```

```css
.badge {
  display: inline-flex;
  align-items: center;
  gap: var(--espacio-4);
  font: var(--tipografia-pequena);
  padding: var(--espacio-4) var(--espacio-8);
  border-radius: var(--radio-pastilla);
}
.badge--exito { background: var(--color-exito-fondo); color: var(--color-exito); }
.badge--advertencia { background: var(--color-advertencia-fondo); color: var(--color-advertencia); }
.badge--peligro { background: var(--color-peligro-fondo); color: var(--color-peligro); }
.badge--info { background: var(--color-info-fondo); color: var(--color-info); }
```

## Modal de confirmación

| Estado | Descripción visual | Tokens |
|---|---|---|
| Normal | Panel centrado, sombra alta, fondo superficie, overlay oscuro detrás. | `--color-superficie`, `--sombra-alta`, `--radio-tarjeta`, `--ancho-modal` |
| Foco al abrir | El foco se mueve al primer elemento interactivo (normalmente el botón "Cancelar", nunca el destructivo, para que un Enter accidental no confirme). | `--color-acento` (anillo del elemento enfocado) |
| Botón destructivo dentro del modal | Mismo patrón que botón destructivo suelto. | `--color-peligro` |
| Cargando (confirmando) | Ambos botones se deshabilitan, el que se presionó muestra spinner. | `--color-texto-deshabilitado` |
| Cierre por tecla Escape o clic fuera | Regresa el foco al elemento que abrió el modal. | — (comportamiento, no token) |

```html
<div class="modal" role="alertdialog" aria-modal="true" aria-labelledby="modal-titulo" aria-describedby="modal-texto">
  <h2 id="modal-titulo" class="modal__titulo">Rechazar solicitud</h2>
  <p id="modal-texto" class="modal__texto">Esta acción no se puede deshacer. La solicitud volverá al colaborador como rechazada.</p>
  <div class="modal__acciones">
    <button class="boton boton--secundario" type="button" autofocus>Cancelar</button>
    <button class="boton boton--destructivo" type="button">Rechazar</button>
  </div>
</div>
```

## Subida de archivo (evidencia)

| Estado | Descripción visual | Tokens |
|---|---|---|
| Normal | Zona con borde punteado, ícono de subir, texto de instrucción. | `--color-borde-fuerte`, `--color-texto-secundario` |
| Hover / arrastrando archivo encima | Borde en acento, fondo superficie-elevada. | `--color-acento`, `--color-superficie-elevada` |
| Foco visible (input accesible detrás del área) | Anillo de acento en toda la zona. | `--color-acento` |
| Cargando | Barra de progreso en acento, nombre de archivo y porcentaje. | `--color-acento`, `--color-borde` (riel de la barra) |
| Completa | Ícono de archivo + nombre + badge `Completa` (ver estados de dominio) + botón para quitar. | `--color-exito` (badge) |
| Error | Borde en peligro, mensaje de error debajo (formato no soportado, archivo muy grande). | `--color-peligro`, `--color-peligro-fondo` |
| Deshabilitado | Zona en superficie-elevada, texto deshabilitado, sin interacción. | `--color-superficie-elevada`, `--color-texto-deshabilitado` |

```html
<div class="subida" role="group" aria-labelledby="subida-titulo">
  <span id="subida-titulo" class="subida__titulo">Evidencia de tarea</span>
  <label class="subida__zona">
    <input type="file" class="subida__input" />
    <span class="subida__instruccion">Arrastra un archivo o haz clic para seleccionar</span>
  </label>
</div>
```

## Navegación lateral por rol

El sistema tiene cuatro roles canónicos: Dirección, Administración, Subcoordinación y Piso de ventas. La navegación lateral muestra sólo los grupos y páginas implementados que corresponden a la sesión activa; una opción no autorizada no se muestra atenuada. El registro de rutas, agrupación, destino inicial y separación entre visibilidad y autorización se define en [`navegacion.md`](navegacion.md). El menú es presentación y nunca sustituye la autorización efectiva del servidor.

| Estado | Descripción visual | Tokens |
|---|---|---|
| Ítem normal | Texto secundario, sin fondo, ícono en texto secundario. | `--color-texto-secundario` |
| Ítem hover | Fondo superficie-elevada. | `--color-superficie-elevada` |
| Ítem activo (sección actual) | Fondo superficie-elevada, texto y borde izquierdo en acento, texto en peso medio. | `--color-acento`, `--color-superficie-elevada` |
| Ítem con foco visible por teclado | Anillo de acento alrededor de todo el ítem. | `--color-acento` |
| Grupo colapsado/expandido (p. ej. "Cierres y control") | Ícono de flecha rota texto-secundario. | `--color-texto-secundario` |

```html
<nav class="navegacion-lateral" aria-label="Navegación principal">
  <a class="navegacion-lateral__item navegacion-lateral__item--activo" aria-current="page" href="/tareas-del-dia">
    Tareas del día
  </a>
  <a class="navegacion-lateral__item" href="/pendientes-de-validar">
    Pendientes de validar
  </a>
</nav>
```

## Extensiones de base aprobadas en TECH-FRONT-001

Estas extensiones completan la base compartida sin crear pantallas funcionales ni reglas de negocio. Conservan HTML nativo y mejora progresiva.

### Campo de credencial

Se usa exclusivamente para contraseña, TOTP y recovery code. Nunca recibe un valor inicial ni vuelve a renderizar el secreto. Desactiva autocorrección, capitalización y spellcheck. TOTP usa `inputmode="numeric"` y `autocomplete="one-time-code"`; contraseña usa el valor de `autocomplete` correspondiente al recorrido consumidor; recovery code usa `autocomplete="off"`.

El botón mostrar/ocultar es opcional, tiene área mínima de 44×44, referencia al input mediante `aria-controls` y anuncia el estado mediante `aria-pressed`. El control vuelve a ocultarse al renderizar; ningún valor se escribe en almacenamiento del navegador ni en logs.

Estados: normal, foco, deshabilitado y error como campo de texto. Cargando no aplica al campo; durante el envío se deshabilita desde el formulario consumidor.

### Textarea y grupo de radio

`textarea` comparte etiqueta, ayuda, error asociado, foco y estado deshabilitado con el campo de texto. Puede crecer verticalmente y el motivo obligatorio usa `required` además de validación de servidor.

El grupo de radio usa `fieldset` y `legend`; todas las opciones comparten `name`, mantienen área interactiva mínima y tienen foco visible individual. Una opción deshabilitada conserva texto legible y cursor no interactivo. El error pertenece al grupo completo y se enlaza con `aria-describedby`.

### Fecha local y rango

La primitiva usa `input type="date"` o `datetime-local`, muestra siempre la zona operativa `America/Mexico_City` y transmite valores ISO sin convertir reglas de negocio con la zona del navegador. Un rango son dos campos etiquetados, inicio y fin; el servidor valida orden, límites y días aplicables.

Estados: normal, foco, deshabilitado y error. La carga de datos deshabilita el formulario o usa el patrón de esqueleto del contenedor; no se sustituye el valor por un spinner dentro del campo.

### UI-I03 — disponibilidad diaria

En el detalle autorizado de persona, una sección «Disponibilidad» muestra dos campos de fecha etiquetados «Desde» y «Hasta», con la zona `America/Mexico_City` visible. La consulta presenta cada día del rango con fecha ISO y texto «Sin registro», «Disponible» o «No disponible»; ausencia de registro nunca equivale a `false`. El día se selecciona con un campo `type="date"` etiquetado «Día», y un `fieldset` de dos radios «Disponible» y «No disponible» define el único valor a guardar. Un día registrado se precarga para corrección; uno nuevo exige elegir valor. La acción se llama «Guardar disponibilidad». No hay porcentaje, horario ni tercera opción de valor.

Normal: los tres estados tienen texto e icono junto a la fecha. Foco: rango, día y radios siguen el orden visual y usan `:focus-visible`; la selección y el estado se anuncian por texto. Deshabilitado: la acción se bloquea mientras la solicitud está en curso o cuando la persona está inactiva; los datos permanecen legibles. Cargando: la región de resultados usa `aria-busy` y anuncia «Consultando disponibilidad…» o «Guardando disponibilidad…». Vacío: el rango sin registros conserva sus días «Sin registro» y la acción autorizada para crear uno. Error: resumen con foco y error asociado a fecha o rango. En móvil los campos y resultados refluyen a una columna sin desplazamiento horizontal ni pérdida de controles.

### UI-C01/C02/C03 — sucursal, semana y calendario

UI-C01 presenta en `/configuracion` una ficha de sólo lectura de `LOR-001` con código, nombre, estado y zona. No ofrece edición de sucursal mientras BR-API01 carezca de contrato de mutación aprobado. La ausencia o respuesta denegada no muestra datos parciales. La consulta está disponible a toda sesión vigente cuyo GET de sucursal sea autorizado por el servidor.

UI-C02 y UI-C03 comparten `/planificacion` sin subrutas. UI-C02 usa campos etiquetados de año y semana ISO y muestra inicio lunes, fin domingo y estado derivado `VIGENTE` o `TRANSCURRIDA` sólo tras la respuesta de `GET /weeks`. UI-C03 usa dos campos `type="date"` etiquetados «Desde» y «Hasta», muestra `America/Mexico_City` y lista únicamente los días publicados vigentes devueltos por `GET /calendar`; un día ausente no se presenta como laborable ni se inventa. Los filtros son GET allowlisted y no incluyen secretos ni intención de mutación.

La edición de UI-C03 se habilita únicamente para `PER-CALENDARIO-ADMIN` proyectado y después de seleccionar una release existente en `BORRADOR` desde el listado autorizado. La lectura de días borrador por release y rango aporta su versión para `If-Match` al corregir. Un día nuevo no usa `If-Match`; un día ya borrador lo exige. El formulario usa día local, tipo cerrado, motivo obligatorio y confirmación motivada que identifica release, fecha y consecuencia: la versión sigue en borrador y no modifica el calendario vigente hasta publicación. Nunca crea ni publica releases desde esta pantalla. Sin borrador, el editor presenta el vacío aprobado y enlaza a `/configuracion` para la creación de UI-C04, sin simular una mutación de calendario.

Normal: ficha, período y resultados muestran texto junto al estado. Foco: filtros, selector de borrador, día y confirmación siguen el orden DOM; el diálogo inicia en Cancelar y devuelve foco al disparador. Deshabilitado: guardar queda inactivo sin borrador, con datos incompletos, durante envío o después de 412 hasta recarga explícita. Cargando: la región de consulta usa `aria-busy` y el botón anuncia «Guardando…». Vacío: rango sin días publicados o borrador sin días conservan texto distinto y acción autorizada. Error: resumen enfocable y errores asociados; 401 limpia sesión, 403 no muestra recurso ni editor, 412 conserva conflicto y no reintenta. En móvil, filtros y editor refluyen a una columna y la página no desborda horizontalmente.

### Resumen de errores y aviso de éxito

El resumen de errores usa `role="alert"`, título descriptivo y lista textual. Al fallar un envío, el consumidor puede mover el foco programáticamente al resumen con `tabindex="-1"`; los campos conservan además su error asociado.

El aviso de éxito usa `role="status"` y `aria-live="polite"`. No sustituye la respuesta persistida ni implica que una operación pendiente, recuperada o en escaneo haya terminado.

### Confirmación motivada

La base usa el elemento nativo `dialog` abierto mediante `showModal()`. Contiene título, resumen inequívoco del recurso, textarea de motivo y botones cuyos textos repiten la acción. El foco inicial está en Cancelar; el modal atrapa el foco, Escape lo cierra y al cerrar devuelve el foco al control que lo abrió. En carga se deshabilitan ambos botones y la acción confirma `aria-busy`.

La variante destructiva usa el botón destructivo. Una acción crítica no destructiva usa el botón primario. El parcial no ejecuta la mutación: la pantalla consumidora aporta formulario, CSRF, ETag e idempotencia según su contrato.

### Subida y análisis de evidencia

El componente de presentación distingue con texto e icono:

- lista para seleccionar;
- cargando, con progreso y cancelación;
- esperando análisis antimalware (`PENDIENTE`);
- archivo limpio (`LIMPIO`);
- malware detectado (`INFECTADO`);
- archivo inválido (`INVALIDO`);
- error de escaneo (`ERROR_ESCANEO`).

El nombre del archivo se muestra como texto, nunca como HTML. La URL firmada, headers de autorización, SHA-256 completo y detalles internos del escáner no se renderizan. `LIMPIO` es el único estado que puede habilitar el vínculo posterior de evidencia; el componente base no implementa S3, hash ni escaneo.

### Tabla con cursor, filtros y responsive

La paginación muestra sólo Anterior/Siguiente. El cursor permanece dentro del `href` generado por servidor y nunca se presenta como número de página ni se interpreta en JavaScript. Un extremo ausente se muestra como texto deshabilitado con `aria-disabled="true"`.

Los filtros usan un formulario GET con etiquetas y orden DOM; limpiar filtros es una acción explícita. Las acciones por fila conservan 44×44 y nombres visibles o accesibles. En viewport estrecho la tabla permanece semánticamente como tabla y usa desplazamiento horizontal; no convierte filas en tarjetas ni duplica encabezados. La navegación pasa a una columna y ningún control queda oculto o fuera del orden de tabulación.

## Matriz de cobertura de componentes base

### BR-D02 — recorrido de acceso

`UI-A01..A05` usan páginas de formulario de una columna, fuera de la navegación del shell, con un solo botón primario por paso. El orden es título, instrucción, alerta o resumen, campos de credencial y acción. El backend determina el paso mediante `nextStep`; una visita directa sin continuación vigente vuelve a `/acceso`. Las transiciones internas llevan sólo un marcador protegido y breve del paso, nunca contraseñas, TOTP, códigos, cookies o CSRF en la URL. Ese marcador no autoriza: cada POST requiere la preautenticación y la validación del servidor.

Los formularios aplican los estados normal, foco, deshabilitado, error y cargando de los componentes existentes. El vacío de un formulario inicial significa campos sin valor; no se muestra un estado de datos vacío. Durante el envío se deshabilita la acción y se anuncia «Verificando…». Un bloqueo o desafío vencido muestra alerta textual y una acción para volver a acceso; jamás presenta el shell como sesión plena. La pantalla de códigos de recuperación aparece sólo en la respuesta que los crea, sin GET que pueda volver a mostrarlos, y explica que deben guardarse fuera de SGOL antes de continuar.

| Componente | Normal | Foco | Deshabilitado | Error | Cargando | Vacío |
|---|---:|---:|---:|---:|---:|---:|
| Campo/select/credencial/textarea/fecha | Sí | Sí | Sí | Sí | Contenedor o select | No aplica |
| Checkbox/radio | Sí | Sí | Sí | Sí | Contenedor | No aplica |
| Botón | Sí | Sí | Sí | No aplica | Sí | No aplica |
| Tabla/cursor | Sí | Sí en controles | Sí en cursor | Mensaje/alerta | Esqueleto | `_EmptyState` |
| Modal motivado | Sí | Sí y retorno | Sí durante envío | Motivo asociado | `aria-busy` | No aplica |
| Upload | Sí | Sí | Sí | Sí | Progreso/PENDIENTE | Lista para seleccionar |
| Alertas | Sí | Foco programático en resumen | No aplica | Sí | No aplica | No aplica |

## BR-D03 — encabezado de sesión de UI-A06/A07

El encabezado autenticado presenta sólo `DisplayName`, `RoleCode`, la expiración efectiva y la acción «Cerrar sesión» del `SessionSnapshot` vigente. El nombre del rol sale de un mapa cerrado: `DIRECCION` → «Dirección», `ADMINISTRACION` → «Administración», `SUBCOORDINACION` → «Subcoordinación» y `PISO_VENTAS` → «Piso de ventas». Un rol desconocido falla cerrado: no se muestra identidad, navegación ni contenido funcional.

La expiración efectiva es el menor instante de `IdleExpiresAt` y `AbsoluteExpiresAt`. Se convierte en el servidor a `America/Mexico_City`; muestra hora para el día local actual y fecha más hora para otro día. No depende de la zona del navegador. No hay contador ni temporizador JavaScript. Cada petición protegida vuelve a leer el snapshot request-scoped.

| Estado | Presentación y operación |
|---|---|
| Cargando | La región de sesión se marca ocupada y no presupone identidad, rol, enlaces ni acciones. |
| Normal | Identidad, rol, expiración y botón de logout visibles; la navegación sólo contiene rutas implementadas y visibles. |
| Expiración próxima | No se usa una advertencia anticipada: el snapshot no define umbral. Se muestra la expiración efectiva exacta hasta que el servidor la invalide. |
| Expirada o invalidada | Se elimina la presentación de identidad y navegación, se limpian las cookies permitidas y se vuelve a acceso con el mensaje aprobado. |
| Error | Mensaje seguro y textual según `estados-y-mensajes.md`, sin respuesta técnica ni secreto; el resumen recibe foco. |
| Vacío | El anfitrión `/mi-trabajo` muestra «Aún no hay secciones disponibles» sin enlazar unidades UI-E aún no implementadas. Logout permanece disponible. |
| Foco y deshabilitado | Los controles usan `:focus-visible` con tokens existentes; logout sólo se deshabilita durante su POST. |

El orden semántico y de tabulación es salto a contenido, marca e información de sesión, logout, navegación disponible y contenido principal. En viewport estrecho el encabezado refluye a una columna sin desplazamiento horizontal y conserva los mismos textos y acciones. El diálogo de navegación móvil conserva Escape, foco modal y retorno al disparador. Sólo se usan las variables de `tokens.md` para propiedades visuales.

## UI-I04/I05 — cuentas individuales

En `/personas-y-accesos`, la sección «Cuentas» reutiliza la tabla semántica con columnas persona, usuario y estado. Cada fila ofrece sólo la acción correspondiente al estado vigente; no existe detalle navegable de cuenta. El formulario de alta elige una persona existente de la lista autorizada y pide un identificador de usuario. La API vuelve a validar que la persona esté activa y dentro de `LOR-001`; la opción de la lista no concede autoridad. El alta no asigna rol ni restablece MFA.

Desactivar y reactivar usan `dialog` nativo con resumen de usuario y persona, motivo obligatorio, «Cancelar» como foco inicial y botón que repite el verbo. Tras Escape o cancelación, el foco vuelve al disparador; si el estado cambió y el disparador desapareció, pasa al encabezado de «Cuentas». Las mutaciones deshabilitan su control mientras están en curso y muestran «Guardando…». El listado usa `_EmptyState` cuando no tiene cuentas y conserva la acción de alta si está autorizada. Un error usa resumen enfocable y error asociado al campo pertinente. La tabla puede desplazarse dentro de su contenedor en móvil; el formulario y los diálogos refluyen a una columna.

### UI-I06/I07 — rol e intervención MFA

Dentro de «Cuentas», cada cuenta activa consultada por la API muestra «Sin rol vigente» o el único rol `ACTIVO`, seguido de una tabla semántica de versiones `ACTIVO`/`SUSTITUIDO` con sus instantes UTC. Una historia vacía usa el estado vacío textual y permite asignar un rol autorizado. No hay ruta navegable de detalle de cuenta o rol. Cuenta inactiva, fuera de alcance o lectura fallida no presenta controles de rol ni reset.

Asignar o cambiar usa select de los cuatro roles canónicos y confirmación motivada. Revocar y restablecer MFA usan confirmación motivada destructiva. El resumen indica rol sustituido o revocado, invalidación de sesiones y, en reset, revocación de TOTP/códigos y nuevo acceso obligatorio. El foco inicial es «Cancelar», Escape cierra y restaura el foco; durante el POST se deshabilita el botón y la región queda ocupada. El 412 bloquea los controles de ese recurso hasta la acción explícita «Recargar roles». Los errores asociados y el resumen reciben foco. En móvil, controles y diálogos refluyen a una columna; sólo las tablas desplazan horizontalmente dentro de su contenedor.

La contraseña generada para un reset nuevo se presenta una sola vez en el bloque sensible de activación ya existente, con encabezado y credencial enmascarados en capturas. Un replay carece del secreto. La visibilidad de estos controles usa permisos de sesión sólo para presentación; la API vuelve a decidir autoridad, vigencia, jerarquía y MFA reciente.

El servidor genera la contraseña temporal y la respuesta nueva la muestra una sola vez a Dirección después del alta o la reactivación. Esta aprobación del responsable en `FRONT-006` ajusta el criterio previo «sin secreto en DOM» de la fila 69 de la Adenda 45. No se almacena en idempotencia, auditoría, navegación, URL ni almacenamiento del navegador. La pantalla lleva `no-store`, no ofrece impresión ni descarga, y la credencial no aparece en capturas, reportes o logs. Un replay nunca la vuelve a mostrar. Dirección la entrega presencialmente a la persona fuera de SGOL; no hay GET para recuperarla.

## UI-C04 — releases de configuración

En `/configuracion`, UI-C04 presenta una tabla semántica de releases de `LOR-001` con versión, estado, vigencia y publicación. La misma lista es la historia: no se crea un detalle ni una subruta. `BORRADOR`, `VIGENTE` y `SUSTITUIDA` usan los badges de `estados-de-dominio.md`. Un borrador no tiene número de versión ni vigencia hasta su publicación; esos campos se muestran como «—» y nunca se inventan. El listado vacío explica que aún no hay releases y ofrece «Crear borrador» sólo a quien tiene `PER-CONFIG-ADMIN`.

La creación usa un formulario POST con CSRF e intención idempotente, sin campos que el API no acepta. Un borrador existente permanece visible en la tabla y puede publicarse desde su fila. La publicación usa la confirmación motivada aprobada por BR-D04: resumen de la release y de la versión vigente que sustituirá, fecha y hora de entrada en vigor en `America/Mexico_City`, motivo obligatorio y botones «Cancelar» y «Publicar release». El formulario envía el instante UTC al API y conserva `If-Match` de la versión del borrador. No aloja formularios de edición de calendario, TAR ni sucursal.

Tras una creación nueva confirmada, el resultado ofrece un enlace visible «Editar días del borrador» a `/planificacion` con el `releaseId` real. La fila de cada borrador también ofrece ese enlace para retomarlo tras recargar. La edición permanece en UI-C03; al volver a `/configuracion`, la historia y publicación de UI-C04 se recargan desde el servidor. Esta composición usa las dos rutas aprobadas y no crea una pantalla nueva.

Normal: estado, versión y vigencia se leen como texto. Foco: tabla, fecha, motivo y diálogo siguen el orden visual, con foco inicial en «Cancelar» y retorno al disparador al cerrar. Deshabilitado: acciones de publicación se ocultan sin permiso y se bloquean para un borrador en conflicto hasta una recarga explícita; durante el POST, los botones se deshabilitan. Cargando: tabla ocupada y acción «Guardando…». Vacío: mensaje y creación autorizada. Error: resumen enfocable, errores de fecha y motivo asociados, y `correlationId` seguro cuando exista. En móvil la tabla conserva su semántica y desplaza sólo dentro de su contenedor; formulario y diálogo refluyen a una columna y el ancho de página no rebasa el viewport.

## BR-D06 — UI-C05, definiciones TAR

En `/configuracion`, UI-C05 muestra un catálogo semántico de las ocho TAR canónicas con código, nombre y estado de su versión actual. La selección abre detalle e historia dentro de la misma ruta aprobada; no convierte rutas `/api/v1` en enlaces. El detalle conserva código, nombre, versión, estado, vigencia y release recibidos del servidor. La tabla histórica distingue `BORRADOR`, `VIGENTE`, `SUSTITUIDA` e `INACTIVA_NUEVAS` con texto e icono. La ausencia de versión y la ausencia de historia tienen estados vacíos propios.

El editor cerrado de HU-011 permite elegir únicamente una TAR del catálogo y una release existente en `BORRADOR`. Muestra `schemaVersion=1` y que el esquema aprobado de `taskPayload` es el objeto vacío `{}`; no ofrece un campo JSON, nombres editables ni opciones de TAR fuera del MVP. Crear versión no altera obligaciones existentes. La publicación de un borrador y la desactivación de nuevas generaciones usan confirmaciones motivadas separadas con fecha y hora de vigencia en `America/Mexico_City`, motivo obligatorio, resumen de TAR/release/consecuencia y foco inicial en «Cancelar». Publicar usa el ETag del borrador; desactivar usa el de la versión vigente. Ambas acciones se deshabilitan durante envío y tras conflicto hasta recarga explícita.

Normal: catálogo, detalle e historia sólo presentan datos persistidos. Foco: enlaces, selector, campos y diálogos siguen orden DOM, `:focus-visible` y retorno al disparador al cerrar. Deshabilitado: una acción sin permiso no se presenta; una intención incompleta o en curso queda inactiva. Cargando: región de consulta con `aria-busy` y acción «Guardando…». Vacío: catálogo, detalle sin versión e historia sin versiones se explican por separado. Error: resumen enfocable, errores de fecha/motivo asociados y 412 sin reenvío. En móvil, formulario y confirmaciones refluyen a una columna; sólo las tablas desplazan horizontalmente dentro de su contenedor.

## UI-C06/C07 — políticas de activación y elegibilidad

Dentro del detalle seleccionado de una de las ocho TAR en `/configuracion`, dos secciones separadas muestran la versión `VIGENTE` y la historia completa recibida de sus GET autorizados. Cada sección tiene su propio vacío, error y región `aria-busy`. Un 403 no presenta datos ni controles de la política. Los controles aparecen sólo con el permiso proyectado correspondiente; la API vuelve a autorizar cada operación.

El editor de UI-C06 acepta únicamente una release `BORRADOR` existente y la versión TAR exacta aplicable. El mecanismo y `originKeySchema` proceden del catálogo cerrado: seis TAR son `MANUAL` con `schedule=null`; `TAR-0005` usa `WORKING_DAY_WINDOWS`, días laborables, 12:00 y 17:00 en `America/Mexico_City`; `TAR-0026` usa `BUSINESS_DAYS_BEFORE_DUE_DATE`, tres días hábiles antes, ajuste al día hábil anterior y un único campo de hora local `HH:mm`. No hay editor JSON, evento, condición ni origen exterior. UI-C07 muestra el rol canónico exacto de la TAR, disponibilidad positiva obligatoria y turno sin restricción; no ofrece texto de puesto ni selector de turno porque el contrato MVP sólo permite `requiredShift=null`.

Cada guardado abre un `dialog` de confirmación con TAR, release, política que quedará en historia y efecto sobre nuevas generaciones; las obligaciones existentes permanecen inmutables. «Cancelar» recibe foco inicial; Escape y cierre restauran el foco. Un 412 o `IF_MATCH_*` bloquea el editor de la política afectada hasta «Recargar política», que realiza un GET nuevo y exige otra decisión explícita. Envío deshabilita la acción y anuncia «Guardando…»; validación asocia el error al selector de release o a la hora local. En móvil, formularios y diálogos refluyen en una columna, y sólo las tablas históricas desplazan dentro de su contenedor. La página no desborda horizontalmente.

## UI-C08/C09 — políticas de evidencia y validación

Dos secciones independientes del detalle TAR en `/configuracion?taskCode=...` muestran versión vigente, historia completa y borradores autorizados de cada política. Cada sección usa su propio vacío, alerta de error, `aria-busy` y foco de resumen. Los requisitos de evidencia aparecen en una lista ordenada con código, tipo, obligatoriedad y condición exactos del catálogo para esa TAR; `DIFERENCIA_O_DANO` se anuncia con texto, no sólo color. La matriz de validación muestra ejecutor, `SUPERIOR_INMEDIATO`, validador y los tres resultados cerrados. Ninguna propiedad canónica es editable.

Cada editor cerrado permite elegir sólo una release `BORRADOR` y confirmar el envío en un `dialog` que identifica TAR, release y efecto sobre versiones futuras; las obligaciones ya creadas conservan su snapshot. El formulario POST conserva antiforgery, intención idempotente y ETag fuerte de la lectura autorizada de esa política. «Cancelar» recibe foco inicial; Escape devuelve foco al disparador. Durante envío, el botón queda deshabilitado y anuncia «Guardando…». Un 412 o `IF_MATCH_*` bloquea sólo ese editor hasta «Recargar política», que hace un GET nuevo y requiere otra decisión explícita. Error de release o de contrato se asocia al campo y enfoca el resumen. En teléfono el formulario y el diálogo refluyen a una columna; sólo la tabla histórica puede desplazar dentro de su contenedor y la página no desborda.

## UI-G01 — composición de alta manual aprobada

El responsable aprobó la sección 5 de FRONT_013_PROPUESTA_CONSUMIDOR.md mediante «Lo autorizo». Esta composición no aprueba por arrastre campos CAT, selector de padres ni cambios transaccionales todavía propuestos en Adenda 51.

«Alta manual» es una sección de `/planificacion` que conserva semana y calendario. Presenta catálogo autorizado, sucursal de sólo lectura, año/semana ISO y rango recibido, regla vigente de sólo lectura y formulario cerrado de la TAR cuando su contrato esté aprobado. TAR-0005 y TAR-0026 no ofrecen alta manual. No hay editor JSON ni IDs inventados. Se oculta la sección sin PER-OBLIGACION-CREAR proyectado; la API reautoriza siempre.

La confirmación corta «Crear solicitud manual» resume TAR, período y origen, con «Cancelar» y «Crear solicitud», sin motivo nuevo. Cancelar recibe foco; Escape lo devuelve al disparador. El texto sólo promete una obligación si su vínculo ha sido confirmado por servidor. Una intención conserva clave y cuerpo exactos para recuperar un envío incierto; editar exige decisión explícita para otra intención. No se cambia clave ni se reenvía automáticamente ante conflicto.

Normal muestra datos/versiones recibidos. Carga usa región aria-busy y «Enviando solicitud…», acción inactiva. Vacío del catálogo tiene mensaje propio y recarga explícita. Error conserva asociación al campo y resumen enfocable; 403 retira datos de esa sección. En teléfono, una columna y select con overflow/text-overflow; sólo tablas desplazan en su contenedor. html/body no desbordan, foco visible y controles cumplen accesibilidad.md y tokens.md. Esta aprobación no materializa todavía la pantalla.

### UI-G01 — aprobación íntegra de Adenda 51

La aprobación «Si la apruebo» incorpora íntegramente la sección 13 de Adenda 51 y los seis esquemas cerrados de su sección 6. La composición previa deja de estar limitada por aprobación parcial. Los reclamantes disponen de añadir/quitar con mínimo dos, máximo veinte y retorno de foco al anterior o a Añadir. El selector de recepción se filtra mediante POST de lectura en Razor, mantiene el filtro fuera de URL y pagina sólo opciones autorizadas; su período se hereda como sólo lectura. La intención Data Protection liga actor, cuerpo y clave durante ocho horas; no usa almacenamiento de navegador. La preparación del formulario permite revisión y abre la confirmación con foco inicial en Cancelar. El diálogo resume origen, TAR y período; Escape/cierre retornan al disparador. Después del envío, los datos de la intención quedan de sólo lectura; recuperar conserva cuerpo y clave. Preparar otra solicitud requiere decisión explícita y no envía.
El tratamiento de overflow del select nativo se aplica también a su contenedor de campo cuando WebKit propaga el texto seleccionado fuera del control. El margen de recorte conserva el anillo de foco de `--espacio-4`; no se oculta el desbordamiento global de html/body para hacer pasar las comprobaciones.
## UI-G02/G03/G04 — composición consumidora aprobada

El responsable aprobó íntegramente las secciones 2–7 de `docs/traceability/FRONT_014_PROPUESTA_CONSUMIDOR.md` mediante «La apruebo» el 2026-09-29. Esas secciones se incorporan por referencia como contrato operativo específico de estas unidades, exclusivamente para FRONT-014. La composición vive en `/planificacion`, conserva el snapshot confirmado, carga recibida y confirmación motivada con intención protegida. No incorpora bandeja ni asignación automática. Se reutilizan BR-D04/D13, tokens y estados existentes; el servidor decide elegibilidad y autoridad.

## FRONT-017 — composición consumidora aprobada

Adenda 56 incorpora íntegramente FRONT_017_PLAN_DE_IMPLEMENTACION.md, aprobado mediante «Apruebo integramente el plan, recuerda seguir los documentos de diseño para crear las pantallas a como están diseñadas las anteriores». §§3–7 definen BR-D07/D08, los 18 formularios cerrados, etiquetas, mensajes, estados y navegación dentro del detalle de tarea. Se conserva estilo v2, CSS propio/variables/componentes compartidos. Sólo primera aportación; LIMPIO permite aportar, no confirma evidencia. Sin preview/descarga/sustitución/conclusión. Los textos de §6 son catálogo operativo literal; PENDIENTE_CARGA y PENDIENTE_ESCANEO se distinguen. Cada booleano Sí/No sin selección inicial. No hay JSON libre ni biblioteca adicional.
