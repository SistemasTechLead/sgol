# Validación en un solo envío

## Alcance e implementación

Cambio mixto solicitado para las tarjetas de Validación y ampliado a la revisión individual desde la pantalla principal de Supervisión: «Validar» aplica las decisiones sin vista previa ni segunda confirmación. La petición sustituye para estos recorridos la recomendación histórica de mantener propuesta/confirmación. Reprogramación posterior, capacidad e incidencias conservan sus recorridos.

`_ValidationCards.cshtml` elimina `_ReviewProposal` y el parámetro stage; `validation-cards.js` conserva Validar al restaurar la página. `Index.Batch.cs::OnPostBatchReviewAsync` envía directamente a `OperationalSupervisionService.ReviewBatchAsync`. Este servicio existente comprueba permisos, revisión de cada registro, evidencia, comentario y programación; calcula correcciones bajo bloqueo y transacción, conserva auditoría y revierte todo el grupo si falla una tarea. No se modifican reglas de planificación, contratos históricos, esquema ni datos existentes fuera de las revisiones solicitadas. El aviso al abandonar selecciones sin guardar sigue vigente.

## Matriz ejecutada

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Devolución con programación FREE | Un POST sin fingerprint crea corrección y conserva origen | CorrectionHttpChecks, PostgreSQL aislado | Aprobado |
| Reenvío de la misma revisión | Conflicto, una sola corrección | HTTP real y conteo SQL aislado | Aprobado |
| Grupo con comentario faltante | Rechazo completo, tarea identificada, comentario válido conservado | HTTP y consulta SQL | Aprobado |
| CSRF ausente | 400 sin ejecutar | HTTP real | Aprobado |
| Sin selección / comentario vacío | Botón deshabilitado / validación nativa | Chromium y WebKit, Razor real | Aprobado |
| Teclado y guardado directo | Enter produce un POST, sin diálogo ni propuesta, mensaje de éxito | validation-direct-checks.cjs | Aprobado |
| Tamaños, temas, texto largo | Sin desbordamiento, control disponible | 320/390/768/1280, claro/oscuro/especial, dos motores: 24 combinaciones | Aprobado |
| Corrección individual, error en tarjeta, reprogramación e historial | Preservar comportamiento y datos | HTTP y correction-scheduling-checks.cjs, 26 casos de navegador | Aprobado |
| Compilación y diff | Sin errores ni advertencias; diff sin problemas | Build Web/TaskChecks, diff --check | Aprobado |
| Publicación | Artefacto probado activo, salud y acceso disponibles | SHA-256, proceso y HTTP local/LAN | Aprobado |
| Sesión real compartida | Recorrido autenticado completo | No se usa cuenta operativa para regresión | No ejecutado; navegador aislado sustituye sesión, conserva Razor/servicios/CSRF/SQL reales |
| Migración | No requerida | No cambia esquema | No aplicable |

Base exclusiva `sgol_task_checks_validation_direct_20261104` en localhost:5440. Evidencias: `.local/validation-direct-20261104/`. Un primer caso esperaba tarjetas de Piso de ventas dentro de Pendientes de Dirección; se corrigió el apartado de la prueba. Una ejecución de la suite de correcciones reutilizó una tarea cerrada por la prueba anterior; se regeneró el fixture y la suite final aprobó. No se relajaron reglas del producto.

La ampliación individual modifica `Index.cshtml::data-supervision-review`, `OnPostReviewAsync` y `supervision.js`: usa el mismo servicio transaccional, conserva la validación de binding y muestra Procesando durante el envío, bloqueando dobles clics y restaurando Validar al volver a la página. `CorrectionHttpChecks` verifica ahora persistencia individual sin propuesta/fingerprint, reintento, CSRF e historial. La suite de correcciones aprobó 26 casos; `supervision-direct-checks.cjs` aprobó 24 combinaciones de tamaño/tema en Chromium/WebKit con guardado por teclado en un POST y sin confirmación. El error de fecha conserva comentario, modalidad y foco; no genera corrección. Base aislada `sgol_task_checks_supervision_direct_20261104`, evidencia `.local/supervision-direct-20261104/`. La prueba de bono se adapta al nuevo botón; su recorrido completo de bono no se volvió a ejecutar (solo sintaxis). Compilación sin errores/advertencias; diff de archivos afectados limpio. Los avisos de whitespace en archivos ajenos no fueron corregidos en este cambio.

## Publicación y referencia

Web publicada en `.local/runtime/validation-direct-20261104/Sgol.Web.dll`; SHA-256 idéntico a la DLL probada. Se conservan los cambios previos de Personas incluidos en la publicación anterior. Lanzador 21424 y Web 33556 observados al finalizar; proxy 20652 conservado. Salud live/ready interna y `/acceso` local/LAN devuelven 200 con TLS normal. Artefacto anterior conservado: `.local/runtime/people-without-exceptions-20261104/`. Sin migraciones, commit ni push; las escrituras de pruebas se limitaron a la base aislada.

Referencias existentes reconciliadas: bóveda `docs/funcional/supervision.md`, `docs/design/04-patrones-y-estados.md`, `docs/design/05-pantallas.md`, `docs/instalacion-local.md` y `organizacion/base-documental.md`. Sin notas nuevas ni cambios de mapas/inventario. La copia versionada se actualizará si se solicita un commit documental.

Publicación final de la ampliación: `.local/runtime/supervision-direct-20261104/Sgol.Web.dll`, SHA-256 `BF4317BF9A718CA9997D209623A4E392A9CF05D55DC62786193DE3E165EE6210`, idéntico al ensamblado de pruebas. Sustituye `.local/runtime/report-card-grid-20261104/`, que se conserva para recuperación, incluyendo los cambios previos de Reportes y Carga/reasignación. Web 34428, lanzador 36580 y proxy 20652 observados al publicar. Salud interna live/ready y acceso local/LAN: 200 con TLS normal. Sin migraciones ni escrituras operativas compartidas. Referencias de la bóveda reconciliadas e índice actualizado; no se añadieron enlaces ni se alteró la estructura de mapas/inventario. Sigue pendiente una sesión autenticada compartida; los recorridos reales probados usan sesión sintética aislada.
