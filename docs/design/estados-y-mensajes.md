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

## Catálogo común aprobado para `TECH-FRONT-002`

La presentación común selecciona el mensaje por la combinación de HTTP y `code`. Nunca muestra `title`, `detail`, `instance` ni `errors` sin una decisión de la pantalla consumidora. Una combinación desconocida usa un mensaje seguro y no convierte el error en éxito. La aprobación de BR-API11/12 se recibió en el chat de `TECH-FRONT-002` el 2026-09-23.

| HTTP | `code` | Título | Mensaje |
|---:|---|---|---|
| 400 | `CSRF_INVALID`, `CSRF_INVALIDO` (alias de compatibilidad) | No se pudo verificar la solicitud | Recarga la página antes de volver a enviarla. |
| 400 | `IF_MATCH_REQUERIDO` | Falta la versión del registro | Recarga el registro antes de guardar los cambios. |
| 400 | `IF_MATCH_INVALIDO` | La versión del registro no es válida | Recárgalo antes de guardar. |
| 428 | `IF_MATCH_REQUERIDO` | Falta la versión de la reconciliación | Recarga la reconciliación antes de aprobarla. |
| 412 | `VERSION_CONFLICT` | Este registro cambió mientras lo editabas | Probablemente alguien más lo actualizó. Recarga para ver la versión más reciente antes de guardar, o tus cambios podrían sobrescribir los de la otra persona. |

`CSRF_INVALID` es el código canónico de la Adenda 41; `CSRF_INVALIDO` permanece como alias sólo para leer respuestas integradas. En la aprobación de reconciliación, el backend devuelve `428 IF_MATCH_REQUERIDO` tanto si falta `If-Match` como si su formato es inválido: la interfaz no distingue causas que la respuesta no permite distinguir. Para rol y corrección de disponibilidad, `400 IF_MATCH_INVALIDO` también puede indicar ausencia. Un `412` conserva el conflicto visible y jamás dispara reintento automático.

## 403 — autorización denegada

## BR-M01/BR-M02 — mensajes del acceso hospedado

Estos textos se aplican únicamente a `UI-A01..A05`. La interfaz selecciona por código cerrado de la API y nunca refleja el usuario, contraseña, TOTP, recovery code, `title` o `detail` crudos. La misma respuesta `AUTHENTICATION_FAILED` se presenta de forma idéntica para usuario inexistente, cuenta inactiva o contraseña incorrecta.

| Respuesta | Título visible | Mensaje y acción |
|---|---|---|
| `401 AUTHENTICATION_FAILED` | No se pudo iniciar sesión | Revisa los datos de acceso e inténtalo de nuevo. |
| `400 PASSWORD_NO_CUMPLE_POLITICA` | La contraseña no cumple los requisitos | Usa una contraseña nueva de 14 a 128 caracteres. Revisa los campos e inténtalo de nuevo. |
| `400 DATOS_AUTENTICACION_INVALIDOS` | Revisa los datos ingresados | Corrige los campos señalados e inténtalo de nuevo. |
| `401 CODIGO_MFA_INVALIDO` | No se pudo verificar el código | Revisa el código e inténtalo de nuevo. No indica si se usó TOTP o recuperación. |
| `401 DESAFIO_INVALIDO` | El paso de acceso venció | Vuelve a iniciar sesión para continuar. |
| `423 ACCOUNT_LOCKED` | El acceso está bloqueado temporalmente | Espera antes de volver a intentarlo. No se muestra identidad ni contador de fallos. |
| `429 RATE_LIMITED` | Demasiadas solicitudes | Espera antes de volver a intentarlo. No se reenvía automáticamente. |
| `400 CSRF_INVALID` o rechazo antiforgery de Razor | No se pudo verificar la solicitud | Recarga la página antes de volver a enviarla. |
| `409 MFA_STATE_INCONSISTENT` | Se requiere recuperación administrada | Solicita a Dirección el restablecimiento de MFA. |

Al mostrar por primera y única vez códigos de recuperación, el título es «Guarda tus códigos de recuperación» y la instrucción es «Estos códigos no volverán a mostrarse. Guárdalos en un lugar seguro fuera de SGOL». La pantalla no ofrece impresión, descarga ni copia automática. Tras TOTP válido o regeneración confirmada, el texto «Sesión iniciada» se muestra sólo cuando existe una cookie plena emitida por la API; la preautenticación nunca usa ese texto.

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

### UI-I01 — personas

La lista sin registros muestra «Aún no hay personas registradas» y la acción «Registrar persona» para Dirección. El alta con `409 CODIGO_PERSONA_DUPLICADO` muestra «El código de persona ya está registrado» junto al campo de código; conserva los valores introducidos y no afirma que se creó otra persona. Un `403` de la sección muestra «No tienes permiso para ver personas y accesos» sin datos de la lista ni acción de alta. El detalle inexistente o fuera de alcance usa el mismo `404`: «No existe o no está disponible en tu alcance». Los errores desconocidos conservan el mensaje seguro común y, cuando exista, el `correlationId`; nunca se refleja `title` ni `detail` de la API.

### UI-I02 — empleo y vigencia

La edición en el detalle presenta «Actualizar empleo» con puesto, turno y motivo obligatorio. Un historial sin versiones muestra «Aún no hay historial laboral» sin inventar una acción. `409 DATOS_LABORALES_SIN_CAMBIO` muestra «Puesto y turno ya son los vigentes» y conserva los campos; `409 VIGENCIA_SIN_CAMBIO` muestra «La vigencia solicitada ya es la vigente». Ninguno se anuncia como éxito.

La baja abre «Dar de baja a esta persona» y resume nombre y código: «La persona quedará inactiva. Su historial laboral se conservará». La reactivación abre «Reactivar a esta persona» y resume el mismo recurso: «La persona volverá a estar activa. Su historial laboral se conservará». Ambos diálogos exigen «Motivo» y ofrecen «Cancelar» y, respectivamente, «Dar de baja» o «Reactivar». Un motivo vacío se asocia al campo y se anuncia antes de enviar. Tras éxito se muestra «Empleo actualizado», «Persona dada de baja» o «Persona reactivada», con el historial recibido de la API.

Ante `412 VERSION_CONFLICT`, el detalle muestra el mensaje común de conflicto y una acción explícita «Recargar detalle». El formulario y los diálogos dejan de estar disponibles hasta esa recarga; no se reenvía la intención ni se toma una nueva versión automáticamente. Los errores de API desconocidos usan el mensaje seguro común y `correlationId` cuando exista. El motivo nunca aparece en URL, historial visible, captura ni log.

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

## Confirmación de éxito

Un éxito usa título y consecuencia concreta, no sólo “Operación exitosa”. Se anuncia con `role="status"` y no mueve el foco si el usuario continúa en el mismo contexto.

Ejemplo:

> Se guardaron los cambios
> La versión visible corresponde a la actualización más reciente.

`RECUPERADA`, `PENDIENTE` y una carga aún no analizada no se presentan como éxito nuevo. Cada pantalla consumidora usa el estado contractual exacto y no promete una consecuencia que el servidor todavía no confirmó.

## Resumen de errores

Cuando hay más de un error, se muestra un resumen con título y lista antes del formulario. El resumen usa `role="alert"`, puede recibir foco programático y no sustituye el mensaje enlazado a cada campo. Nunca incluye stack, SQL, ruta privada, cookie, TOTP, recovery code, cadena de conexión, URL firmada ni contenido de evidencia.

## Estados seguros de subida

| Estado | Mensaje visible mínimo |
|---|---|
| `PENDIENTE` | “Esperando análisis antimalware.” |
| `LIMPIO` | “El archivo terminó el análisis y puede vincularse como evidencia.” |
| `INFECTADO` | “El archivo fue rechazado por seguridad y no se vinculó.” |
| `INVALIDO` | “El archivo no cumple el tipo o formato permitido y no se vinculó.” |
| `ERROR_ESCANEO` | “No se pudo completar el análisis. El archivo permanece sin vincular.” |

Los mensajes no muestran la URL firmada, detalles del motor antimalware ni recomiendan reintento automático. La pantalla consumidora decide si ofrece un nuevo intento conforme a su contrato.

## BR-D15 — sesión y cierre de UI-A06/A07

| Situación | Mensaje visible | Efecto |
|---|---|---|
| Acción normal | «Cerrar sesión» | Formulario POST con antiforgery, nunca GET. |
| En curso | «Cerrando sesión…» | Control deshabilitado y región ocupada accesible; no hay segundo envío ni reintento automático. |
| Logout confirmado por `204` | «Sesión cerrada.» | Descartar snapshot, antiforgery y cookies permitidas; redirigir a `/acceso`. |
| Sesión expirada o invalidada | «Tu sesión terminó. Inicia sesión nuevamente.» | Descartar estado y cookies permitidas; volver a `/acceso`. |
| CSRF inválido | «No se pudo verificar la solicitud. Recarga la página antes de volver a enviarla.» | Conservar el mensaje común de `CSRF_INVALID`; no repetir POST ni afirmar cierre remoto. |
| Logout remoto sin confirmación o excepción | «La sesión se cerró en este dispositivo, pero no se pudo confirmar el cierre en el servidor.» | Contención local mediante eliminación de cookies permitidas; volver a `/acceso` sin afirmar auditoría remota. |

Los avisos de acceso después de redirección reciben foco programático y no contienen cookies, tokens, cuerpo técnico, JSON crudo ni detalles internos. La acción de logout sigue disponible cuando no haya secciones funcionales visibles. El servidor conserva la autoridad y vuelve a validar cada recurso.
