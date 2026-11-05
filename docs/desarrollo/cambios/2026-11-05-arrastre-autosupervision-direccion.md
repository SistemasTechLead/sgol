# Autosupervisión de arrastres de Dirección

Cambio funcional autorizado el 05/11/2026; incluye actualización de la app compartida. No cambia composición visual, esquema ni registros existentes.

## Causa y alcance

`OperationalCarryover.Postponement.cs`, `ImmediateSupervisors`, buscaba solamente niveles superiores al solicitante. Dirección agotaba esa lista y `PreviewPostponementAsync` mostraba «No hay un supervisor inmediato activo». Además, `AuthorizedInboxAsync`, `DecideAsync` y `ResolutionSource` excluían toda solicitud propia. La autorización general de tareas ya permite a Dirección supervisar sus tareas.

Dirección se incluye como supervisor de sus propias peticiones. La bandeja y las decisiones integrales y por tarea admiten esa excepción, comprobando cuenta, empleo y rol vigentes mediante los servicios existentes. `HasSupervisor` aplica el mismo alcance a incidentes de jornada. Los otros roles conservan la prohibición. No se alteran niveles jerárquicos ni permisos generales de supervisión.

Recorrido: `/mi-trabajo/arrastre` crea la vista previa y envía; `OperationalCarryoverRequest` conserva fotografía, autor y estado pendiente; `/validaciones` consulta la bandeja y confirma la decisión. `OperationalCarryoverReader` y el historial conservan sus contratos. No se aprueba automáticamente al enviar ni se recalculan peticiones resueltas. Las pendientes existentes usan la nueva autorización al consultarse y decidirse.

## Validación

Copia PostgreSQL aislada `sgol_task_checks_self_direction_20261105`, restaurada de la simulación; actores y tareas sintéticos propios. Comando mantenible: `Sgol.TaskChecks --carryover-self-direction`. Evidencia privada: `.local/carryover-self-direction-final.log`.

| Escenario | Resultado esperado y prueba | Entorno | Estado |
|---|---|---|---|
| Director solicita posposición | Vista previa válida, envío pendiente y asignación idéntica; servicios y SQL | Copia aislada | Aprobado |
| Bandeja e historial propios | Incluye petición propia, consulta paginada de pendientes y consulta completa de historial | Copia aislada | Aprobado |
| Aprobar/rechazar tarea propia | Una decisión persistida; rechazo conserva asignación | Copia aislada | Aprobado |
| Reintentos de envío y decisión | Mismo ID y un solo resultado | Copia aislada | Aprobado |
| Incidente de jornada propio | Envío y aprobación integral con director como autor de decisión | Copia aislada | Aprobado |
| Piso, Subcoordinación y Administración | Autosupervisión denegada; propios fuera de bandeja supervisora | Copia aislada | Aprobado |
| Dirección inactiva | Acceso denegado | Copia aislada | Aprobado |
| Formularios de Dirección | GET, vista previa, envío, bandeja, vista previa de decisión y confirmación; comprobación SQL | Host aislado con sesión sintética, formularios Razor y CSRF reales | Aprobado |
| Concurrencia de agenda y reprogramación | Algoritmos y comprobaciones existentes conservados; no se repite suite completa | Inspección | No ejecutado |
| Navegador y sesión real compartida | Sin cambios visuales; no se usa cuenta real para introducir peticiones | Compartido | No ejecutado |

Se corrigió una suposición de la prueba: el historial completo puede ocupar varias páginas, por lo que la petición propia no tiene que estar en la primera. No fue un fallo del producto.

## Publicación

Artefacto preparado: `.local/runtime/carryover-self-direction-20261105/Sgol.Web.dll`. SHA-256 idéntico al ensamblado ejecutado en la comprobación final: `6F018DE62B03ECC6E287A19D7E45B7D081E5DEB1DFC8241EF93A220FD140ED77`. Pruebas y publicación finalizaron con código 0. La sustitución prevista se limita a Web y conserva publicación anterior, proxy y dependencias. No requiere migraciones ni escrituras de prueba en la base compartida.

**Activación completada el 05/11/2026:** tras la nueva instrucción de reintentar, se sustituyó únicamente Web sin parámetros force. Lanzador 28720, proceso 33712, puerto 7241 y ruta del nuevo artefacto verificados. Proxy 20652 conservado. Readiness interno respondió ready; acceso local y LAN respondieron HTTP 200 con TLS normal. No se ejecutaron migraciones ni peticiones de prueba en la simulación compartida. Se conserva el artefacto anterior para recuperación.

Se reconciliaron las notas existentes de arrastres, instalación y base documental en la bóveda. El flujo autenticado con la cuenta real del director en el host compartido no se ejecutó; la prueba funcional usa formularios reales y sesión sintética aislada.
## Sustitución de la regla: autorización automática al enviar

Petición posterior del usuario, 05/11/2026: Dirección no debe confirmar de nuevo en Supervisión. El comportamiento inicial descrito arriba corresponde al primer despliegue; esta actualización lo sustituye para nuevos envíos. Las solicitudes pendientes anteriores conservan su estado y pueden resolverse mediante el flujo existente; no se aprueban retroactivamente.

Cambio mixto: SubmitAsync y SubmitPostponementAsync guardan solicitud y autorización integral en la misma transacción y bajo el mismo bloqueo de agenda. DecideWithinTransactionAsync centraliza la decisión usada por el envío y por Supervisión; conserva permisos, huellas, revisiones, fechas y auditoría. AutomaticallyApprovesOwnAsync consulta cuenta, empleo y un único rol vigente de Dirección en la sucursal. No depende de un campo enviado por el navegador. El reintento devuelve el recibo existente, sin repetir autorización.

La pantalla mantiene Preparar petición / vista previa. Para Dirección, el botón final indica Confirmar y aplicar arrastre, informa del efecto dentro del formulario y devuelve éxito de autorización aplicada. Los demás roles conservan el envío al supervisor y las fechas pendientes de aprobación. La confirmación de incidente conserva advertencias de plazo y las reglas existentes.

| Escenario de la actualización | Resultado esperado | Entorno y prueba | Estado |
|---|---|---|---|
| Varias tareas de Dirección | Todas aprobadas y fechas de la propuesta aplicadas una vez | Servicios y SQL, copia aislada | Aprobado |
| Auditoría e historial | Autor, fecha, motivo automático, envío y aprobación conservados | Servicios y SQL | Aprobado |
| Reintento y doble envío concurrente | Mismo ID, dos eventos, una aplicación | Dos contextos PostgreSQL aislados | Aprobado |
| Vista previa obsoleta | Rechazo sin crear solicitud | Servicio y SQL | Aprobado |
| Fallo de persistencia después de crear solicitud | Revierte solicitud, auditoría y fechas | Interceptor de fallo en la aprobación, transacción PostgreSQL | Aprobado |
| Piso, Subcoordinación y Administración | Petición pendiente; no autosupervisión | Servicios y SQL | Aprobado |
| Incidente propio | Aprobación integral automática y autor de decisión | Servicios y SQL | Aprobado |
| Formularios reales | Envío aplica sin POST a Supervisión; mensaje correcto | Razor/CSRF/SQL con sesión sintética | Aprobado |
| Navegador y cuenta lateral conjunta | Tres temas, 320/768/1440, foco, menú y envío por teclado | Chromium/WebKit, host aislado | Aprobado: 19 escenarios |
| Sesión real del director compartida | No se crean peticiones de prueba en ese entorno | No ejecutada | No ejecutado |

El usuario también autorizó incluir la cuenta lateral y retirada del encabezado. La copia de compilación .local/checkouts/carryover-auto-20261105 contiene únicamente los archivos de ambos cambios sobre HEAD; preserva el checkout y sus modificaciones concurrentes. La publicación conjunta mantiene el proxy y datos, sin force ni migraciones.


Publicación conjunta completada: .local/runtime/carryover-auto-direction-20261105/Sgol.Web.dll; SHA-256 3643B836F2D17DE326853B591C2476E3F779E215019DBD64575A3EF4F39674DC, igual al ensamblado probado. Lanzador 35604, Web 37956, proxy 20652 conservado; readiness ready y acceso local/LAN 200 con TLS normal. Sin force, migraciones ni pruebas de escritura compartidas. Evidencia: .local/carryover-auto-combined-final.log y .local/carryover-auto-direction-ui/. La prueba de foco espera el evento close del diálogo antes de verificar el siguiente foco, evitando una carrera artificial del harness. Bóveda reconciliada.
