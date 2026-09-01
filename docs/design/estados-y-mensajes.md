# estados-y-mensajes.md — SGOL / Loretta Zapatería

Este sistema niega mucho por diseño: roles distintos, validaciones, control de acceso. La forma en que comunica esos límites es parte del sistema de diseño, no un detalle menor.

## Tono general

Profesional pero cercano: claro, sin tecnicismos innecesarios, sin regañar al usuario ni fingir una calidez que no corresponde a un mensaje de error.

Regla concreta: un genérico **"Acceso denegado"** no le dice nada útil a quien lo lee — no sabe si es un problema temporal, un permiso que le falta, o un error del sistema. Cada negación dice **qué** se negó y, cuando aplica, **por qué** en términos de rol. Ejemplos abajo, no principios.

## Error de Problem Details (backend)

El backend ya emite RFC 7807. La interfaz no debe mostrar el JSON crudo ni el `type`/`traceId` como mensaje principal — esos van en un detalle colapsado o en consola, para soporte. Al usuario se le muestra `title` traducido y `detail` reescrito en tono humano cuando el backend lo permite; si el backend no manda `detail` en español, la interfaz tiene un mapa de `title` → mensaje ES por código.

**Mal** (mostrar el Problem Details crudo):
> Error 400: "One or more validation errors occurred." — see traceId 00-8f3a...

**Bien**:
> No se pudo guardar el registro. Revisa los campos marcados en rojo y vuelve a intentar.
> *(detalle técnico disponible en "Ver detalles" para soporte, con el traceId)*

```html
<div class="alerta alerta--peligro" role="alert">
  <svg class="alerta__icono" aria-hidden="true"><!-- octágono con exclamación --></svg>
  <div class="alerta__contenido">
    <p class="alerta__titulo">No se pudo guardar el registro</p>
    <p class="alerta__texto">Revisa los campos marcados en rojo y vuelve a intentar.</p>
    <details class="alerta__detalle">
      <summary>Ver detalles técnicos</summary>
      <code>traceId: 00-8f3a...</code>
    </details>
  </div>
</div>
```

## 403 — autorización denegada

Nunca "Acceso denegado" a secas. El mensaje nombra el recurso y, cuando ayuda a que el usuario entienda que no es un bug sino una regla de su rol, lo dice.

**Mal**:
> Acceso denegado.

**Bien** (ejemplos reales del dominio):
> No tienes permiso para ver tareas de otras personas. Si necesitas supervisar a tu equipo, pide a Dirección que te asigne el rol de subcoordinación.

> Esta configuración solo puede editarla Dirección. Puedes consultarla, pero no modificarla.

> No puedes validar tus propias tareas concluidas — necesitas que un superior las valide.

```html
<div class="estado-vacio estado-vacio--403">
  <svg class="estado-vacio__icono" aria-hidden="true"><!-- candado --></svg>
  <p class="estado-vacio__titulo">No tienes permiso para ver tareas de otras personas</p>
  <p class="estado-vacio__texto">Si necesitas supervisar a tu equipo, pide a Dirección que te asigne el rol de subcoordinación.</p>
</div>
```

## Conflicto de ETag / If-Match

Este es el caso más fácil de explicar mal. El usuario no sabe qué es un ETag y no debe enterarse de que existe — necesita saber que alguien más cambió el dato mientras él trabajaba, y qué hacer al respecto.

**Mal**:
> Error 412: Precondition Failed. El ETag no coincide con el recurso actual.

**Bien**:
> Este registro cambió mientras lo editabas — probablemente alguien más lo actualizó. Recarga para ver la versión más reciente antes de guardar tus cambios, o se perderá lo que hizo la otra persona.

Con dos acciones claras, nunca solo un botón de "Aceptar" que no resuelve nada:

```html
<div class="alerta alerta--advertencia" role="alert">
  <svg class="alerta__icono" aria-hidden="true"><!-- reloj de conflicto --></svg>
  <div class="alerta__contenido">
    <p class="alerta__titulo">Este registro cambió mientras lo editabas</p>
    <p class="alerta__texto">Probablemente alguien más lo actualizó. Recarga para ver la versión más reciente antes de guardar, o tus cambios podrían sobrescribir los de la otra persona.</p>
    <div class="alerta__acciones">
      <button class="boton boton--secundario" type="button">Recargar y perder mis cambios</button>
      <button class="boton boton--primario" type="button">Ver qué cambió</button>
    </div>
  </div>
</div>
```

## Estado vacío

Siempre con texto + acción sugerida, nunca solo un ícono y "No hay datos". La acción cambia según por qué está vacío: no es lo mismo "todavía no hay nada" que "filtraste hasta que no quedó nada".

**Sin datos todavía** (ej. panel de un colaborador nuevo, semana 1):
> Aún no hay tareas asignadas para esta semana.
> Acción sugerida: "Ir a Asignación final" (si el usuario tiene permiso) o, si no lo tiene, sin botón — solo el texto informativo.

**Filtro sin resultados** (ej. buscó un colaborador que no existe en esa sucursal):
> No encontramos coincidencias con "Juan Pérez" en esta sucursal.
> Acción sugerida: botón "Quitar filtros".

```html
<div class="estado-vacio">
  <svg class="estado-vacio__icono" aria-hidden="true"><!-- carpeta vacía --></svg>
  <p class="estado-vacio__titulo">Aún no hay tareas asignadas para esta semana</p>
  <button class="boton boton--secundario" type="button">Ir a Asignación final</button>
</div>
```

## Esqueleto de carga

Nunca un spinner central que salta el layout de una tabla: filas esqueleto del mismo alto que las filas reales, para que la pantalla no "brinque" cuando llegan los datos.

```html
<tbody class="tabla__cuerpo tabla__cuerpo--cargando" aria-busy="true" aria-live="polite">
  <tr class="tabla__fila-esqueleto"><td colspan="3"><span class="esqueleto"></span></td></tr>
  <tr class="tabla__fila-esqueleto"><td colspan="3"><span class="esqueleto"></span></td></tr>
  <tr class="tabla__fila-esqueleto"><td colspan="3"><span class="esqueleto"></span></td></tr>
</tbody>
```

```css
.esqueleto {
  display: block;
  height: 14px;
  border-radius: var(--radio-control);
  background: linear-gradient(90deg, var(--color-borde) 25%, var(--color-superficie-elevada) 50%, var(--color-borde) 75%);
  background-size: 200% 100%;
  animation: esqueleto-pulso 1.4s ease-in-out infinite;
}
@keyframes esqueleto-pulso {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}
```

Para una operación puntual (guardar, validar) que no es carga de página, el spinner sí va dentro del botón que la disparó — ver `componentes.md`, estado "Cargando" de botón primario.

## Confirmación destructiva

Nunca un solo "¿Estás seguro?" — el texto dice qué se pierde exactamente y es específico a la acción, no genérico.

**Mal**:
> ¿Estás seguro? Esta acción no se puede deshacer.

**Bien** (ejemplos reales):
> Rechazar esta solicitud
> El colaborador verá esta solicitud como rechazada y tendrá que enviarla de nuevo desde cero. No se puede deshacer.
> [Cancelar] [Rechazar solicitud]

> Sustituir esta configuración
> La versión actual pasará a "Sustituida" y dejará de aplicar de inmediato a todas las sucursales. Seguirá disponible como historial, pero no podrás revertir automáticamente. ¿Continuar?
> [Cancelar] [Sustituir configuración]

El botón de confirmación nunca dice "Aceptar" o "Sí" — repite el verbo de la acción ("Rechazar solicitud", no "Sí"), para que un usuario que solo lee botones por costumbre no confirme algo que no leyó. Ver `componentes.md`, Modal de confirmación, para el foco inicial en "Cancelar" y no en la acción destructiva.
