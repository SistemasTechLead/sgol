# estados-y-mensajes.md — SGOL / Loretta Zapatería

## Precisión vigente del estilo v2

La base visual v2 está aceptada y documentada en [ESTILO_VISUAL_V2.md](ESTILO_VISUAL_V2.md), por solicitud expresa de actualización del responsable el 2026-09-30. Sus reglas precisan los ejemplos anteriores: secundario neutro sin borde rojo, fuentes locales, logo/iconos, sesión a la derecha y acceso con composición de marca/formulario. Las correcciones y el complemento fueron aprobados mediante «Apruebo las correcciones y el complemento del plan para implementar». La implementación local se registra por grupo en [DISENO_RENOVADO_V2_IMPLEMENTACION.md](../traceability/DISENO_RENOVADO_V2_IMPLEMENTACION.md); la aprobación del diseño no equivale a publicación o integración. Los contratos y mensajes funcionales se conservan.

## Referencia renovada — conservación y precedencia aprobadas

[Adenda 55](../../F07_ADENDA_55_REFERENCIA_DE_DISENO_RENOVADO.md) incorpora la renovación de presentación descrita en [componentes.md](componentes.md) y [referencia-renovada.md](referencia-renovada.md). Conserva literalmente los catálogos consumidores y sus consecuencias; no añade textos funcionales por copiar la maqueta.

Error y vacío son obligatorios en cada pantalla. El vacío distingue ausencia de datos, filtros sin coincidencias e historia sin registros según su catálogo, con icono/texto y sólo acción autorizada. Se presenta dentro del panel compartido, sin simular un resultado. Un error usa alerta con par semántico, texto, icono, radio y padding por variables; resumen enfocable y asociación al campo se conservan. Éxito usa `role="status"`, nunca presupone una operación nueva ante replay ni archivo vinculado durante escaneo.

Carga usa `aria-busy` regional y mensajes aprobados. Tabla reserva filas con esqueleto; una operación puntual usa progreso en su botón. `prefers-reduced-motion` elimina animación y conserva información textual. Los seis estados y sus «No aplica» por componente son los de componentes.md.

**Precedencia:** el catálogo común aprobado de TECH-FRONT-002 y los catálogos consumidores prevalecen sobre ejemplos genéricos antiguos de Problem Details, traceId/consola o acciones de conflicto. No se renderizan `title`, `detail`, `instance`, `errors` ni respuestas crudas por arrastre; desconocidos usan mensaje seguro y `correlationId` cuando existe. Los ejemplos no autorizan «Ver qué cambió», borrado, reenvío automático ni una acción ajena al contrato. La conservación de ejemplos históricos no los convierte en mensajes operativos nuevos.

## UI-E01/E02/E03 — BR-M10 aprobada para FRONT-016

El catálogo completo de §6 de `docs/traceability/FRONT_016_PLAN_DE_IMPLEMENTACION.md` queda incorporado por Adenda 54, aprobado mediante «Bueno sigue con la tarea, apruebo la adenda integramente». Distingue vacío propio, filtro sin coincidencias, avisos e historia, errores de filtros, denegación base y 404 convergente. MARKED_READ dice «Aviso marcado como leído. La tarea no cambió»; ALREADY_READ dice «Este aviso ya estaba leído. La tarea no cambió». Marcación incierta exige consultar, no reintentar automáticamente. Sólo se reflejan resultados confirmados y correlationId seguro; no title/detail crudos. La tarea relacionada no disponible no revela nombre ni vínculo. BR-M10 queda resuelta sólo para esta consumidora.

## UI-G05 y UI-G06 aprobadas para FRONT-015

La tabla completa de mensajes y los seis estados de la sección 7 de `F07_ADENDA_53_PROPUESTA_CONSUMIDOR_FRONT_015.md` quedó aprobada íntegramente mediante «Apruebo íntegramente las secciones 2–8» el 2026-09-29 y se incorpora por referencia como catálogo operativo consumidor. Los vacíos distinguen plan inexistente, ausencia de obligaciones visibles e historia sin publicaciones visibles. Los resultados confirmados no crean estados de dominio. PUBLICADO no implica publicación de todos los niveles. Un 412 exige recarga y nueva intención; un resultado incierto sólo permite reenviar la misma intención original. La confirmación no exige motivo ni selecciona nivel u obligaciones.

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

### UI-I03 — disponibilidad diaria

Un rango sin valores registrados muestra «Aún no hay disponibilidad registrada en este rango» y «Selecciona un día y guarda Disponible o No disponible». Cada día sin registro conserva el texto «Sin registro»; un registro `false` dice «No disponible». Las fechas se muestran como día local de `America/Mexico_City`.

Si falta una fecha, el orden del rango es inválido, excede el límite del servidor o el día seleccionado queda fuera del rango, el resumen indica «Revisa el rango y el día» y asocia «Selecciona fechas válidas dentro del rango permitido» a los campos. Una respuesta `400 FECHA_INVALIDA` o `CONSULTA_DISPONIBILIDAD_INVALIDA` no se anuncia como guardada. Tras PUT confirmado se muestra «Disponibilidad registrada» para día nuevo o «Disponibilidad corregida» para día existente; sólo la respuesta persistida actualiza la lista.

Ante `412 VERSION_CONFLICT` se muestra el mensaje común y «Recargar disponibilidad». Hasta esa recarga explícita se bloquea el formulario y no se reintenta ni se adopta automáticamente otra versión. `400 IF_MATCH_INVALIDO` también exige recarga antes de corregir, incluso si el servidor lo devolvió porque faltó `If-Match`. Persona inactiva o fuera de alcance muestra un error seguro sin controles de escritura. Los demás errores usan el mensaje seguro por código y `correlationId` cuando exista.

### UI-C01/C02/C03 — configuración y planificación

UI-C01: la ficha se titula «Sucursal Loretta». Si el GET no encuentra `LOR-001`, muestra «No existe o no está disponible en tu alcance» sin datos parciales. Otro código se rechaza y no se ofrece como opción. La zona se muestra literalmente como `America/Mexico_City` cuando el servidor confirma ese valor.

UI-C02: «Semana ISO» identifica año, semana, lunes, domingo y el estado calculado por el servidor. «Revisa el año y la semana ISO» se asocia a ambos campos si la combinación es inválida; una semana transcurrida se consulta sin acciones de cierre o reapertura. `403` muestra «No tienes permiso para consultar el período semanal» y no presenta datos previos.

UI-C03: el rango sin días publicados dice «No hay días publicados en este rango» y «Consulta otro rango». No convierte ausencia en día laborable. «Revisa el rango de calendario» y «Selecciona fechas válidas» se asocian a Desde/Hasta ante fechas inexistentes u orden inverso. Cada fila muestra la fecha, «Laborable», «Festivo» o «Cierre extraordinario» y el valor laborable recibido. `403` muestra «No tienes permiso para consultar el calendario» sin filas.

El editor muestra «No hay una release en borrador disponible» si el listado autorizado no trae una y ofrece «Crear borrador» como enlace a UI-C04 en `/configuracion`; no crea la release ni sugiere una llamada técnica. Un borrador sin días dice «Aún no hay días en este borrador» y permite elegir un día si Dirección tiene permiso. La confirmación dice «Guardar día en borrador» y «El día seleccionado quedará en la release borrador; el calendario vigente no cambiará hasta que se publique». Exige «Motivo». Éxito nuevo: «Día agregado al borrador»; corrección: «Día corregido en el borrador». Ante `409 CONFIGURACION_BORRADOR_REQUERIDA` o un `412 VERSION_CONFLICT`, el editor muestra «El borrador cambió; recárgalo antes de continuar», bloquea guardar y no reintenta. `403` oculta el editor. Un error desconocido usa el mensaje seguro y `correlationId`, sin reflejar el motivo ni datos de sesión.

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
  height: var(--alto-esqueleto);
  border-radius: var(--radio-control);
  background: linear-gradient(90deg, var(--color-borde) 25%, var(--color-superficie-elevada) 50%, var(--color-borde) 75%);
  background-size: 200% 100%;
  animation: esqueleto-pulso var(--duracion-esqueleto) ease-in-out infinite;
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

## UI-I04/I05 — cuentas

La lista vacía dice «Aún no hay cuentas registradas» y ofrece «Crear cuenta» sólo a quien tiene `PER-USUARIO-ADMIN` vigente. La creación confirmada dice «Cuenta creada» y presenta una vez la contraseña temporal generada por el servidor con la instrucción «Entrégala presencialmente a la persona fuera de SGOL. No volverá a mostrarse». La reactivación confirmada usa «Cuenta reactivada» y la misma instrucción. Un replay dice «La operación ya se había procesado; la contraseña temporal no puede volver a mostrarse» y nunca inventa un valor nuevo.

`409 CUENTA_DUPLICADA` dice «La persona o el usuario ya tiene una cuenta» y pide revisar la selección; no confirma alta. `404 PERSONA_NO_ENCONTRADA` dice «No existe o no está disponible en tu alcance». `409 PERSONA_FUERA_DE_ALCANCE` dice «La persona debe estar activa en LOR-001». `400 DATOS_CUENTA_INVALIDOS` señala los campos del formulario sin reflejar `detail` de la API. `409 ESTADO_CUENTA_SIN_CAMBIO` pide recargar la lista antes de otra acción. `409 IDEMPOTENCY_KEY_CONFLICT` no reenvía automáticamente la solicitud. Los errores desconocidos usan el mensaje seguro común y `correlationId` cuando existe.

«Desactivar cuenta» resume «La cuenta dejará de permitir acceso y sus sesiones quedarán invalidadas. El historial se conserva». «Reactivar cuenta» resume «La cuenta volverá a estar activa, se invalidarán las sesiones previas y se exigirá cambio de contraseña temporal». Ambos diálogos requieren «Motivo», con error «Ingresa el motivo antes de continuar». Tras respuesta confirmada, «Cuenta desactivada» o «Cuenta reactivada» se anuncia con `role="status"`; el listado refleja la respuesta del servidor. Un `403` muestra «No tienes permiso para administrar cuentas» sin datos ni controles.

## UI-I06/I07 — rol y reset MFA

Sin rol se lee «Sin rol vigente»; sin versiones, «Aún no hay historial de roles» y «Asigna un único rol canónico si corresponde». Un rol activo dice «Vigente» y cada versión anterior «Sustituido», con texto e icono. Tras respuesta confirmada se anuncia «Rol asignado», «Rol cambiado» o «Rol revocado»; replay dice «El cambio de rol ya se había procesado». Ninguna respuesta de error se presenta como cambio efectuado.

«Cambiar rol» advierte que el vigente quedará sustituido y las sesiones previas se invalidarán. «Revocar rol» advierte que dejará de aplicar y permanecerá en historia. «Restablecer MFA» advierte que revocará TOTP y códigos, invalidará sesiones y exigirá nueva contraseña y MFA; requiere motivo y MFA del actor de no más de cinco minutos. Un reset nuevo presenta la contraseña temporal una sola vez y exige entrega presencial fuera de SGOL; el replay avisa que no puede volver a mostrarse. Motivos y secretos no entran en URL, historial visible, captura ni log.

`412 VERSION_CONFLICT` y `400 IF_MATCH_INVALIDO` dicen «Este rol cambió mientras lo editabas» y ofrecen «Recargar roles»; no hay reenvío ni adopción automática del nuevo ETag. `409 ROL_ACTIVO_DUPLICADO` dice «La cuenta ya tiene un rol vigente». `409 ROL_SIN_CAMBIO` dice «Ese rol ya está vigente». `404 CUENTA_NO_DISPONIBLE` y los casos de cuenta inactiva o fuera de alcance dicen «No existe o no está disponible en tu alcance». `403 MFA_RECIENTE_REQUERIDO` dice «Se requiere MFA reciente» y pide iniciar sesión nuevamente con MFA. Un `403` de rol no muestra controles ni datos administrativos. Los demás errores usan mensaje seguro y `correlationId` cuando existe.

## UI-C04 — releases de configuración

Una lista sin releases dice «Aún no hay releases de configuración» y ofrece «Crear borrador» a Dirección. Un borrador disponible dice «Borrador disponible; todavía no está vigente» sin afirmar que un replay creó otra versión. Al publicarse, «Release publicada; la versión anterior permanece en el historial». Un replay confirmado dice «La operación ya se había procesado» y se verifica en la lista antes de otra intención.

La confirmación «Publicar release» resume la versión de borrador y avisa que una versión vigente pasará a «Sustituida» conservando historia. «Fecha y hora de vigencia» muestra `America/Mexico_City`; «Motivo» es obligatorio. La ausencia o invalidez de cualquiera presenta «Revisa la fecha y el motivo» y un mensaje asociado al campo. La fecha no se interpreta con la zona del navegador.

`422 VIGENCIA_SOLAPADA` dice «La vigencia se solapa con otra versión publicada» y pide elegir otra fecha después de consultar la historia. `422 PUBLICACION_INVALIDA` y los errores de políticas incompletas dicen «No se puede publicar esta release» y piden revisar la configuración vigente; no simulan éxito. `412 VERSION_CONFLICT` y `400 IF_MATCH_INVALIDO/IF_MATCH_REQUERIDO` dicen «Este borrador cambió mientras lo editabas», bloquean su publicación y ofrecen «Recargar releases». Nunca adoptan otra versión ni reenvían el POST automáticamente. `403` dice «No tienes permiso para administrar releases» y no muestra lista ni acciones. Los demás errores usan el mensaje seguro por código con `correlationId` cuando exista; nunca muestran JSON crudo, motivo en URL ni datos de auditoría.

## UI-C06/C07 — políticas por TAR

Sin versión vigente, cada detalle dice respectivamente «Esta TAR aún no tiene política de activación vigente» o «Esta TAR aún no tiene política de elegibilidad vigente». La historia vacía dice «Aún no hay versiones de esta política». Si no hay release borrador, el editor dice «No hay una release en borrador disponible» y enlaza a su creación sólo con permiso. El catálogo de TAR conserva su vacío propio.

La confirmación «Guardar política de activación» o «Guardar política de elegibilidad» identifica TAR y release, y advierte «La versión vigente quedará en historia; las obligaciones existentes no cambiarán». Un guardado nuevo confirmado dice «Política guardada en borrador; aún no genera trabajo». Un replay dice «La operación ya se había procesado; consulta la historia antes de otra intención». La publicación sigue perteneciendo a UI-C04 y no se simula desde estos editores.

`422 POLITICA_ACTIVACION_INVALIDA` señala el mecanismo u hora local; `422 POLITICA_ELEGIBILIDAD_INVALIDA` señala rol, disponibilidad o turno sin mostrar JSON crudo. `422 VERSION_TAR_REQUERIDA` pide seleccionar la versión TAR aplicable. `409 CONFIGURACION_BORRADOR_REQUERIDA` pide recargar releases. `422 DEFINICION_NO_MVP` y `404 DEFINICION_NO_DISPONIBLE` no presentan datos de otra TAR. Un conflicto `412 VERSION_CONFLICT` o `400 IF_MATCH_*` dice «Esta política cambió mientras la editabas», bloquea su formulario y ofrece «Recargar política». No reenvía ni adopta un ETag nuevo automáticamente. Un `403` dice «No tienes permiso para administrar esta política» y retira datos y controles de esa sección. Los errores desconocidos usan el mensaje seguro común y `correlationId`.

## UI-C08/C09 — evidencia y validación por TAR

Sin versión vigente: «Esta TAR aún no tiene política de evidencia vigente» o «Esta TAR aún no tiene política de validación vigente». Historia vacía: «Aún no hay versiones de esta política». Sin release borrador: «No hay una release en borrador disponible». La carga y el error pertenecen a cada sección, nunca al catálogo completo.

Los diálogos «Guardar política de evidencia» y «Guardar política de validación» identifican TAR y release y advierten: «La versión vigente quedará en historia; las obligaciones existentes no cambiarán». El éxito de una intención nueva dice «Política guardada en borrador; aún no genera trabajo». Una intención recuperada dice «La operación ya se había procesado; consulta la historia antes de otra intención». Publicar la release pertenece a UI-C04.

`POLITICA_EVIDENCIA_INVALIDA` indica que los requisitos, el orden, tipo o condición no coinciden con el catálogo. `POLITICA_VALIDACION_INVALIDA` indica que la autoridad o los resultados no coinciden con la matriz. `VERSION_TAR_REQUERIDA`, `CONFIGURACION_BORRADOR_REQUERIDA` y `CONFIGURACION_SOLAPADA` piden revisar detalle e historia antes de decidir otra intención. `VERSION_CONFLICT` e `IF_MATCH_*` dicen «Esta política cambió mientras la editabas», bloquean sólo su editor y ofrecen «Recargar política», sin reintento automático. Un 403 retira datos y controles de esa sección; un código fuera del MVP converge en «No existe o no está disponible en tu alcance». Los errores muestran `correlationId` cuando exista, nunca JSON crudo.

## BR-D06 — UI-C05, definiciones TAR

El catálogo vacío dice «No hay definiciones TAR disponibles» sin inventar una novena. Un detalle sin versión dice «Esta TAR aún no tiene versión publicada» y la historia vacía «Aún no hay versiones de esta TAR». La creación confirmada dice «Borrador de TAR creado; todavía no genera trabajo»; publicación confirmada, «Versión de TAR publicada; las obligaciones anteriores permanecen intactas»; desactivación confirmada, «Se detuvieron las nuevas generaciones; las obligaciones existentes permanecen intactas». Un replay dice «La operación ya se había procesado» y pide consultar el detalle antes de una intención nueva.

«Publicar versión de TAR» resume código, versión y release, advierte que la versión vigente quedará en historia y exige fecha/hora de vigencia y motivo. «Desactivar nuevas generaciones» resume código y release, aclara que no borra obligaciones previas y exige los mismos campos. Cancelar recibe el foco inicial; Escape cierra y devuelve el foco. Un error de fecha o motivo dice «Revisa la fecha y el motivo» y se asocia a cada campo. `422 DEFINICION_NO_MVP` dice «Esta TAR no pertenece al catálogo MVP». `422/400 DEFINICION_INVALIDA` dice «La definición no cumple el esquema aprobado» sin reflejar payload ni detalle técnico. `409 CONFIGURACION_BORRADOR_REQUERIDA` dice «Selecciona una release que siga en borrador». `422 CONFIGURACION_SOLAPADA` pide consultar la historia y elegir otra vigencia. `412 VERSION_CONFLICT` o `400 IF_MATCH_*` dice «Esta versión cambió mientras la editabas», bloquea la acción y ofrece «Recargar definiciones»; no reintenta. `403` de administración oculta acciones, y el 403 de lectura no presenta datos TAR. Un código inexistente o fuera de alcance usa el 404 convergente. El error desconocido usa el mensaje seguro y `correlationId`.

## UI-G01 — mensajes de composición aprobados

Aprobados durante FRONT-013 como sección 5 de FRONT_013_PROPUESTA_CONSUMIDOR.md; no incluyen mensajes funcionales nuevos pendientes de Adenda 51.

Catálogo vacío: «No hay tareas disponibles para alta manual en tu alcance», acción «Recargar opciones». Carga: «Enviando solicitud…». Confirmación: «Crear solicitud manual», con TAR/período/origen y botones «Cancelar» y «Crear solicitud».

ACEPTADA: «Solicitud aceptada». RECUPERADA: «Se recuperó la misma solicitud; no se creó otra». Rechazo HTTP: «No se pudo crear la solicitud», sin inventar ID ni estado persistido. El GET muestra el resultado real y un vínculo de obligación sólo si existe. Un conflicto 409 dice «Esta solicitud ya se envió con otros datos»; ofrece «Recuperar resultado» sólo si conserva el cuerpo original y «Preparar otra solicitud» mediante decisión explícita. Ninguna acción reenvía o rota clave automáticamente.

REGLA_MANUAL_REQUERIDA: «La regla vigente no permite alta manual». TAR_INACTIVA: «Esta tarea no admite nuevas generaciones». PERIODO_INVALIDO: «Selecciona un período válido». ORIGEN_INVALIDO: «Revisa la referencia de origen». ACCESO_DENEGADO: «No tienes permiso para crear esta solicitud». GENERATION_REQUEST_NO_ENCONTRADA: «No existe o no está disponible en tu alcance». Desconocidos usan el mensaje seguro común y correlationId, sin reflejar title/detail técnicos.

Los errores se asocian al campo y al resumen enfocable. 403 retira datos y controles de la sección. La aprobación de estos mensajes no hace equivaler una referencia libre a un formulario CAT completo ni afirma que el POST actual materialice la obligación.

### UI-G01 — extensión íntegra aprobada por Adenda 51

«Si la apruebo» aprueba los mensajes y estados de la sección 13: «Registra la referencia del documento; SGOL no verifica su contenido», «Se creará una obligación pendiente; aún no estará asignada ni publicada», «La intención venció y no se pudo confirmar su resultado», «Solicitud histórica sin obligación vinculada», «No hay recepciones disponibles para vincular» y «Actualizar recepciones». No se ofrece un enlace a una pantalla de obligación inexistente. Los errores funcionales usan «No se pudo crear la solicitud» y el mensaje cerrado de la tabla HTTP/code de la sección 11 de Adenda 51; sólo los desconocidos usan el mensaje seguro común, conservando correlationId; los campos inválidos muestran «Revisa este campo» asociado al control. Los instantes indican «Hora de Ciudad de México». Separado indica plazo ordinario de 24 horas y referencia documental para otro plazo; garantía indica autorización externa previa y siete días laborables del calendario publicado.
## UI-G02/G03/G04 — composición consumidora aprobada

El responsable aprobó íntegramente las secciones 2–7 de `docs/traceability/FRONT_014_PROPUESTA_CONSUMIDOR.md` mediante «La apruebo» el 2026-09-29. Esas secciones se incorporan por referencia como contrato operativo específico de estas unidades, exclusivamente para FRONT-014. La composición vive en `/planificacion`, conserva el snapshot confirmado, carga recibida y confirmación motivada con intención protegida. No incorpora bandeja ni asignación automática. Se reutilizan BR-D04/D13, tokens y estados existentes; el servidor decide elegibilidad y autoridad.

## FRONT-017 — composición consumidora aprobada

Adenda 56 incorpora íntegramente FRONT_017_PLAN_DE_IMPLEMENTACION.md, aprobado mediante «Apruebo integramente el plan, recuerda seguir los documentos de diseño para crear las pantallas a como están diseñadas las anteriores». §§3–7 definen BR-D07/D08, los 18 formularios cerrados, etiquetas, mensajes, estados y navegación dentro del detalle de tarea. Se conserva estilo v2, CSS propio/variables/componentes compartidos. Sólo primera aportación; LIMPIO permite aportar, no confirma evidencia. Sin preview/descarga/sustitución/conclusión. Los textos de §6 son catálogo operativo literal; PENDIENTE_CARGA y PENDIENTE_ESCANEO se distinguen. Cada booleano Sí/No sin selección inicial. No hay JSON libre ni biblioteca adicional.

## FRONT-018 — composición consumidora aprobada

Adenda 57 incorpora íntegramente el plan FRONT_018_PLAN_DE_IMPLEMENTACION.md, aprobado mediante «Si apruebo integramente el plan». Sus §§5–7 son contrato operativo literal de UI-E06/E07/E08: versiones paginadas, revisión explícita, faltantes exactos, sustitución con autoridad/motivo y conclusión sin cuerpo. Conserva estilo v2, seis estados, componentes compartidos, variables oficiales, accesibilidad y navegación en el detalle existente. Descarga/preview excluidos. Revisión tiene únicamente la excepción atómica de snapshot/auditoría de Adenda 19; no cambia ejecución ni decisiones anteriores.
## FRONT-019 — composición consumidora aprobada

Adenda 58 incorpora el plan único FRONT_019_PLAN_DE_IMPLEMENTACION.md aprobado mediante «Apruebo el plan». Sus §§4–8 son el contrato operativo de UI-V01..V04: pendientes/supervisión en /validaciones; emisión, sustitución e historia en el detalle existente, tres resultados, fundamento y motivos separados, confirmación y conflicto. BR-D09/M09 resueltas sólo para esta consumidora. Catálogo literal y seis estados en §§7–8; mapas cerrados de requisito, resultado y autoridad con pares/iconos existentes. CSS propio, variables oficiales y base v2 conservados. Proyección validationActions sólo de presentación, servidor reautoriza. Lecturas puras sin efectos; revisión explícita conserva snapshot/auditoría atómicos deduplicados. Descarga/preview excluidos sólo para FRONT-019; BR-API04 global permanece abierta. Sin FRONT-020/TECH-FRONT-005.
