# componentes.md — SGOL / Loretta Zapatería

Inventario de componentes base. La densidad es intencional: quien usa este sistema viene de Excel y espera ver muchas filas y controles compactos, no tarjetas espaciadas de sitio de marketing. Todo el marcado usa exclusivamente las variables de `tokens.md` — cero valores sueltos.

Todo control interactivo debe tener foco visible por teclado (`:focus-visible`, nunca solo `:focus`, para no mostrar el anillo en un clic de mouse) y todo estado de error debe ir acompañado de texto, no solo de un borde rojo — ver `estados-y-mensajes.md`.

## Campo de texto

| Estado | Descripción visual | Tokens |
|---|---|---|
| Normal | Borde delgado, fondo blanco, texto primario. | `--color-borde-fuerte`, `--color-superficie`, `--color-texto-primario`, `--radio-control` |
| Hover | Borde ligeramente más oscuro, sin cambio de fondo. | `--color-texto-secundario` (borde en hover) |
| Foco visible | Anillo de 2px en acento alrededor del control, borde cambia a acento. | `--color-acento`, `outline-offset: 2px` |
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
  border: 1px solid var(--color-borde-fuerte);
  border-radius: var(--radio-control);
  padding: var(--espacio-8) var(--espacio-12);
}
.campo__control:hover { border-color: var(--color-texto-secundario); }
.campo__control:focus-visible {
  outline: 2px solid var(--color-acento);
  outline-offset: 2px;
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
.boton--primario:hover { background: var(--color-acento-hover); }
.boton--primario:focus-visible { outline: 2px solid var(--color-acento); outline-offset: 2px; }
.boton--primario:disabled {
  background: var(--color-superficie-elevada);
  color: var(--color-texto-deshabilitado);
  cursor: not-allowed;
}
```

## Botón secundario

| Estado | Descripción visual | Tokens |
|---|---|---|
| Normal | Fondo transparente, borde y texto en acento. | `--color-acento` (borde y texto), `--color-superficie` (fondo) |
| Hover | Fondo superficie-elevada, borde acento-hover. | `--color-superficie-elevada`, `--color-acento-hover` |
| Foco visible | Anillo de acento. | `--color-acento` |
| Deshabilitado | Borde y texto deshabilitados. | `--color-borde`, `--color-texto-deshabilitado` |
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
| Normal | Encabezado en superficie-elevada, texto secundario en mayúsculas pequeñas, filas en superficie con borde inferior sutil. | `--color-superficie-elevada`, `--color-texto-secundario`, `--color-borde` |
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
.tabla__encabezado { background: var(--color-superficie-elevada); }
.tabla__encabezado th { font: var(--tipografia-pequena); color: var(--color-texto-secundario); padding: var(--espacio-8) var(--espacio-12); }
.tabla__fila td { font: var(--tipografia-base); color: var(--color-texto-primario); padding: var(--espacio-8) var(--espacio-12); border-bottom: 1px solid var(--color-borde); }
.tabla__fila:hover { background: var(--color-superficie-elevada); }
.tabla__orden:focus-visible { outline: 2px solid var(--color-acento); outline-offset: 2px; }
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

La edición de UI-C03 se habilita únicamente para `PER-CALENDARIO-ADMIN` proyectado y después de seleccionar una release existente en `BORRADOR` desde el listado autorizado. La lectura de días borrador por release y rango aporta su versión para `If-Match` al corregir. Un día nuevo no usa `If-Match`; un día ya borrador lo exige. El formulario usa día local, tipo cerrado, motivo obligatorio y confirmación motivada que identifica release, fecha y consecuencia: la versión sigue en borrador y no modifica el calendario vigente hasta publicación. Nunca crea ni publica releases desde esta pantalla. Sin borrador, el editor presenta el vacío aprobado y no ofrece una mutación imposible.

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

La creación usa un formulario POST con CSRF e intención idempotente, sin campos que el API no acepta. Un borrador existente permanece visible en la tabla y puede publicarse desde su fila. La publicación usa la confirmación motivada aprobada por BR-D04: resumen de la release y de la versión vigente que sustituirá, fecha y hora de entrada en vigor en `America/Mexico_City`, motivo obligatorio y botones «Cancelar» y «Publicar release». El formulario envía el instante UTC al API y conserva `If-Match` de la versión del borrador. No ofrece edición de calendario, TAR ni sucursal.

Normal: estado, versión y vigencia se leen como texto. Foco: tabla, fecha, motivo y diálogo siguen el orden visual, con foco inicial en «Cancelar» y retorno al disparador al cerrar. Deshabilitado: acciones de publicación se ocultan sin permiso y se bloquean para un borrador en conflicto hasta una recarga explícita; durante el POST, los botones se deshabilitan. Cargando: tabla ocupada y acción «Guardando…». Vacío: mensaje y creación autorizada. Error: resumen enfocable, errores de fecha y motivo asociados, y `correlationId` seguro cuando exista. En móvil la tabla conserva su semántica y desplaza sólo dentro de su contenedor; formulario y diálogo refluyen a una columna y el ancho de página no rebasa el viewport.
