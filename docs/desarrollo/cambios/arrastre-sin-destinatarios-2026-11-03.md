# Peticiones de arrastre: reasignación sin destinatarios

## Alcance y comportamiento

Cambio mixto pequeño, implementado con validación integrada pendiente. La bandeja de posposiciones mostraba el diagnóstico de aprobación, pero habilitaba Reasignar para cualquier tarea no entregada, incluso cuando su selector de destinatarios estaba vacío.

`IndexModel.LoadCarryoversAsync` ya consulta `OperationalSupervisionService.TransferRecipientsAsync` para cada tarea pendiente. Esa consulta excluye al responsable actual y filtra personas activas, alcance de supervisión y elegibilidad por especialidad o rol. `OperationalCarryoverService.PreviewDecisionAsync` vuelve a consultar el mismo servicio; la confirmación conserva sus controles de concurrencia. No se cambian reglas, almacenamiento, historial, permisos ni migraciones.

`_PostponementRequest.cshtml` usa la lista existente para mostrar «Sin personas para reasignar» con la especialidad requerida (o el requisito de mismo rol), junto al diagnóstico horario y con sus mismos estilos. El aviso también aparece sin conflicto horario. No afirma que falte esa especialidad en toda la organización: informa que no hay otra persona elegible dentro del alcance.

`postponement-review.js` bloquea el botón según ese dato independiente de la aprobación, enlaza el aviso con `aria-describedby` y retira la asociación al cambiar de selección. La opción del editor queda deshabilitada. Los borradores antiguos de reasignación sin destinatarios no permiten revisar, confirmar ni enviar; el usuario conserva su comentario y puede cambiar de acción. Las tareas entregadas conservan su tratamiento anterior, sin mostrar un diagnóstico engañoso de falta de personal.

## Evidencia y escenarios

Entorno: checkout `master`, 03/11/2026, con numerosas modificaciones previas, incluidos los archivos de posposiciones aún sin seguimiento. Solo se editaron la plantilla, su JavaScript y los archivos nuevos de esta corrección. No se escribieron datos ni se reiniciaron servicios compartidos.

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Sin destinatarios y aprobación bloqueada | Reasignar bloqueado; cambiar propuesta y rechazar disponibles | Playwright, componente aislado | Aprobado |
| Sin destinatarios y aprobación disponible | Reasignar bloqueado sin bloquear aprobación | Playwright, componente aislado | Aprobado |
| Seleccionar otra tarea con destinatarios | Reasignar habilitado, sin asociación al aviso anterior; abre por teclado | Playwright, componente aislado | Aprobado |
| Borrador antiguo o evento de reasignación sin destinatarios | No abre por evento ni permite enviar el borrador; permite elegir otra acción | Playwright, componente aislado | Aprobado |
| Tarea entregada | Cerrar sin cambios/rechazar; movimientos bloqueados | Playwright, componente aislado | Aprobado |
| Plantilla Razor compilada | Sin errores ni advertencias | `dotnet build src/Sgol.Web/Sgol.Web.csproj --no-restore -v quiet` | Aprobado |
| Consulta real sin especialidad Administración, POST y rechazo sin efectos | Lista vacía, aviso correcto y servidor conserva sus validaciones | Aplicación y base aislada | No ejecutado; las pruebas de componente no cubren servicios ni Razor renderizado |
| Pantalla real, reflujo, foco, contraste y textos largos | Avisos legibles y controles accesibles | Navegador autenticado | No ejecutado |

Regresión mantenible: `tools/Sgol.MobileChecks/postponement-recipient-checks.cjs`. Ejecutada con Playwright del runtime mediante `NODE_PATH`: 18 combinaciones de Chromium/Edge y WebKit, 320/768/1440 px y temas light/dark/special. Son verificaciones de interacción con HTML de prueba y CSS/JS del producto, no validación visual de la pantalla real. El primer intento no encontró Playwright; al configurar `NODE_PATH`, pasó.

Sin despliegue, commit ni push. La comprobación global `git diff --check` señaló espacios finales en modificaciones ajenas a este cambio; no se alteraron.

## Actualización de la app compartida autorizada

El 03/11/2026, a petición del usuario, se publicó el checkout actual en `.local/runtime/carryover-recipients-20261103` y se sustituyó el proceso web de `process-recurrence-20261103`, conservando el proxy. Destino comprobado: `127.0.0.1:5440`, base `sgol_local`; sin ejecutar migraciones ni escrituras operativas explícitas. Publicación correcta, proceso cargando el ensamblado nuevo, `/health/ready` en `ready`, acceso LAN `/acceso` HTTP 200 y JavaScript servido con `unavailableReassignment`. Evidencia local: `.local/carryover-recipients-20261103/deployment.json`. La comprobación autenticada del aviso en la pantalla real sigue pendiente. Sin commit ni push.

## Avisos en paralelo — ajuste visual solicitado

Se agrupan ambos avisos en `postponement-diagnostics`: el de reasignación queda a la derecha del diagnóstico de aprobación cuando caben dos columnas de 26rem; en estrecho se apilan. Un aviso único ocupa el ancho disponible. Se conservan textos, identificadores accesibles y bloqueos. Publicación correcta en `.local/runtime/carryover-notices-row-20261103`, sin migraciones. Readiness ready, LAN HTTP 200 y CSS servido con el nuevo contenedor comprobados. Prueba `postponement-notices-layout-checks.cjs`: 18 casos de componente aprobados (Chromium/Edge y WebKit; 320/768/1440; tres temas), alineación horizontal en escritorio y sin desbordamiento. No sustituye la inspección autenticada de la pantalla real.
