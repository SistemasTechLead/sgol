# FRONT-020 — Capturas de funcionamiento

2026-10-01. **50 capturas**, revisión visual expresa pendiente. Aplicación ASP.NET Core/Kestrel HTTPS con PostgreSQL real, cuentas y datos sintéticos; escritorio 1440×900 con Chromium y móvil emulado 390×844 con WebKit. Son pantallas de la aplicación integrada. Capturas locales en `.artifacts/front-020`, fuera de Fuentes y de Git; se reproducen con Front020BrowserTests y `SGOL_FRONT020_CAPTURE_DIR`.

No incluyen contraseñas, cookies, intenciones protegidas, URLs firmadas, datos reales ni contenido de evidencia. Los hashes son sintéticos. La captura de carga observa el evento real de envío impidiendo una única navegación, para conservar el estado transitorio; no prueba latencia de red. Las etapas de continuidad son registros contractuales sintéticos en la base efímera, sin iniciar recuperación operativa ni acreditar el simulacro integral backend.

| Pantalla y significado | Escritorio | Móvil |
|---|---|---|
| Operación de Dirección: propio puesto e inferiores | [Ver](../../.artifacts/front-020/escritorio-indicadores-0.png) | [Ver](../../.artifacts/front-020/movil-indicadores-0.png) |
| Operación de Administración: propio puesto e inferiores | [Ver](../../.artifacts/front-020/escritorio-indicadores-1.png) | [Ver](../../.artifacts/front-020/movil-indicadores-1.png) |
| Operación de Subcoordinación: propio puesto y Piso | [Ver](../../.artifacts/front-020/escritorio-indicadores-2.png) | [Ver](../../.artifacts/front-020/movil-indicadores-2.png) |
| Piso: únicamente su alcance propio | [Ver](../../.artifacts/front-020/escritorio-indicadores-3.png) | [Ver](../../.artifacts/front-020/movil-indicadores-3.png) |
| Panorama de Dirección: toda LOR-001, conteos y carga | [Ver](../../.artifacts/front-020/escritorio-panorama.png) | [Ver](../../.artifacts/front-020/movil-panorama.png) |
| Consulta sin obligaciones: conteos y denominadores cero | [Ver](../../.artifacts/front-020/escritorio-ceros.png) | [Ver](../../.artifacts/front-020/movil-ceros.png) |
| Error de filtros: no se muestran conteos inventados | [Ver](../../.artifacts/front-020/escritorio-error-filtros.png) | [Ver](../../.artifacts/front-020/movil-error-filtros.png) |
| Cargando: mensaje visible y botón ocupado | [Ver](../../.artifacts/front-020/escritorio-cargando.png) | [Ver](../../.artifacts/front-020/movil-cargando.png) |
| Auditoría general: intervalo UTC, eventos y cerca | [Ver](../../.artifacts/front-020/escritorio-auditoria-eventos.png) | [Ver](../../.artifacts/front-020/movil-auditoria-eventos.png) |
| Traza: etapas ausentes declaradas, sin reconstruir hechos | [Ver](../../.artifacts/front-020/escritorio-traza.png) | [Ver](../../.artifacts/front-020/movil-traza.png) |
| Evento: campos minimizados y Regresar a la consulta | [Ver](../../.artifacts/front-020/escritorio-auditoria-detalle.png) | [Ver](../../.artifacts/front-020/movil-auditoria-detalle.png) |
| Continuidad inicial: consulta por ID y solicitud motivada | [Ver](../../.artifacts/front-020/escritorio-continuidad-vacio.png) | [Ver](../../.artifacts/front-020/movil-continuidad-vacio.png) |
| Confirmación de solicitud: Cancelar inicialmente enfocado | [Ver](../../.artifacts/front-020/escritorio-confirmacion-solicitud.png) | [Ver](../../.artifacts/front-020/movil-confirmacion-solicitud.png) |
| Solicitud registrada: todavía no acredita recuperación | [Ver](../../.artifacts/front-020/escritorio-solicitada.png) | [Ver](../../.artifacts/front-020/movil-solicitada.png) |
| Capturando referencia: sin aprobación disponible | [Ver](../../.artifacts/front-020/escritorio-reference_capturing.png) | [Ver](../../.artifacts/front-020/movil-reference_capturing.png) |
| Referencia preparada: todavía sin resultado confirmado | [Ver](../../.artifacts/front-020/escritorio-reference_ready.png) | [Ver](../../.artifacts/front-020/movil-reference_ready.png) |
| Restauración iniciada: sin aceptación disponible | [Ver](../../.artifacts/front-020/escritorio-restore_started.png) | [Ver](../../.artifacts/front-020/movil-restore_started.png) |
| Comparando: aún no confirma recuperación | [Ver](../../.artifacts/front-020/escritorio-reconciling.png) | [Ver](../../.artifacts/front-020/movil-reconciling.png) |
| MATCHED: coincide y Dirección aún debe aceptar | [Ver](../../.artifacts/front-020/escritorio-coincidente.png) | [Ver](../../.artifacts/front-020/movil-coincidente.png) |
| DIFFERENT: todas las filas recibidas, sin aprobar ni reparar | [Ver](../../.artifacts/front-020/escritorio-diferencias.png) | [Ver](../../.artifacts/front-020/movil-diferencias.png) |
| FAILED: no acredita recuperación correcta | [Ver](../../.artifacts/front-020/escritorio-fallo.png) | [Ver](../../.artifacts/front-020/movil-fallo.png) |
| Reporte truncado: advertencia, aprobación bloqueada | [Ver](../../.artifacts/front-020/escritorio-truncado.png) | [Ver](../../.artifacts/front-020/movil-truncado.png) |
| Confirmación de aprobación: motivo, ID y secuencia original | [Ver](../../.artifacts/front-020/escritorio-confirmacion-aprobacion.png) | [Ver](../../.artifacts/front-020/movil-confirmacion-aprobacion.png) |
| APPROVED: aceptación registrada, sin modificar datos reconciliados | [Ver](../../.artifacts/front-020/escritorio-aprobada.png) | [Ver](../../.artifacts/front-020/movil-aprobada.png) |
| Conflicto 412: recargar antes de otra aprobación | [Ver](../../.artifacts/front-020/escritorio-conflicto-412.png) | [Ver](../../.artifacts/front-020/movil-conflicto-412.png) |

Consultar indicadores/panorama/auditoría no escribe datos; consultar continuidad sí registra VIEWED. Preparar y Cancelar no ejecutan la operación. Solicitar inicia exclusivamente la captura de referencia del proceso existente; Aprobar registra la aceptación de un resultado admisible. Recargar reconciliación vuelve a consultar y auditar; Regresar restaura la consulta de auditoría. No hay exportación, descarga, borrado, reparación o despliegue.

La revisión visual no convierte en PASS los diferidos de zoom nativo, lector, dispositivos físicos, PUT/S3 local anterior, aislamiento SeaweedFS productivo, carrera evidencia/emisión ni rendimiento extremo de 100000 filas. Consulte el informe del hito para las comprobaciones y sus límites.
