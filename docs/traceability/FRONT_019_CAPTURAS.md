# FRONT-019 — Capturas de funcionamiento

Revisión solicitada por el responsable: se vuelven a entregar únicamente **escritorio-administracion-pendientes.png** y **escritorio-historia-vacia.png**, con filtros alineados, título «Supervisión» y versión centrada. Las demás capturas no forman parte de esta nueva solicitud de revisión.

2026-10-01. **36 capturas**, dos por estado: escritorio 1440×900 y móvil emulado 390×844, página completa. Aplicación ASP.NET Core/Kestrel HTTPS y PostgreSQL real, Chromium, cuatro cuentas y datos sintéticos. No son maquetas. Archivos locales reproducibles con Front019BrowserTests y SGOL_FRONT019_CAPTURE_DIR; excluidos de Git mediante .gitignore y siempre fuera de Fuentes. No incluyen secretos, cookies, valores de intención, URLs firmadas ni evidencia real. El estado cargando pausa sólo la navegación después del evento real para fotografiar el estado transitorio; no acredita red real lenta. Las capturas no sustituyen zoom nativo, lector, dispositivos físicos o WebKit ni resuelven sus diferidos.

| Estado | Escritorio | Móvil |
|---|---|---|
| Administración: pendientes y supervisión de Subcoordinación/Piso | [Ver](../../.artifacts/front-019/escritorio-administracion-pendientes.png) | [Ver](../../.artifacts/front-019/movil-administracion-pendientes.png) |
| Detalle concluido, historia vacía y emisión disponible | [Ver](../../.artifacts/front-019/escritorio-historia-vacia.png) | [Ver](../../.artifacts/front-019/movil-historia-vacia.png) |
| Resultado y fundamento antes de emitir | [Ver](../../.artifacts/front-019/escritorio-confirmacion-inicial.png) | [Ver](../../.artifacts/front-019/movil-confirmacion-inicial.png) |
| Error transaccional; recuperar la misma intención | [Ver](../../.artifacts/front-019/escritorio-resultado-incierto.png) | [Ver](../../.artifacts/front-019/movil-resultado-incierto.png) |
| Primera decisión Incompleta | [Ver](../../.artifacts/front-019/escritorio-incompleta.png) | [Ver](../../.artifacts/front-019/movil-incompleta.png) |
| Resultado, fundamento y motivo de sustitución | [Ver](../../.artifacts/front-019/escritorio-confirmacion-sustitucion.png) | [Ver](../../.artifacts/front-019/movil-confirmacion-sustitucion.png) |
| Tres versiones conservadas, una Vigente y anteriores Sustituidas | [Ver](../../.artifacts/front-019/escritorio-historia-tres-resultados.png) | [Ver](../../.artifacts/front-019/movil-historia-tres-resultados.png) |
| Vacío regional con filtros | [Ver](../../.artifacts/front-019/escritorio-filtro-sin-coincidencias.png) | [Ver](../../.artifacts/front-019/movil-filtro-sin-coincidencias.png) |
| Error de pareja año/semana incompleta | [Ver](../../.artifacts/front-019/escritorio-error-filtro.png) | [Ver](../../.artifacts/front-019/movil-error-filtro.png) |
| Subcoordinación: sólo Piso | [Ver](../../.artifacts/front-019/escritorio-subcoordinacion-supervision.png) | [Ver](../../.artifacts/front-019/movil-subcoordinacion-supervision.png) |
| Piso: acceso denegado a la colección | [Ver](../../.artifacts/front-019/escritorio-piso-denegado.png) | [Ver](../../.artifacts/front-019/movil-piso-denegado.png) |
| Dirección: tres niveles inferiores | [Ver](../../.artifacts/front-019/escritorio-direccion-supervision.png) | [Ver](../../.artifacts/front-019/movil-direccion-supervision.png) |
| Motivo explícito antes del escalamiento | [Ver](../../.artifacts/front-019/escritorio-confirmacion-escalamiento.png) | [Ver](../../.artifacts/front-019/movil-confirmacion-escalamiento.png) |
| Decisión escalada conservada | [Ver](../../.artifacts/front-019/escritorio-escalamiento-confirmado.png) | [Ver](../../.artifacts/front-019/movil-escalamiento-confirmado.png) |
| Excepción propia de Dirección, confirmación | [Ver](../../.artifacts/front-019/escritorio-confirmacion-autovalidacion-direccion.png) | [Ver](../../.artifacts/front-019/movil-confirmacion-autovalidacion-direccion.png) |
| Excepción registrada expresamente | [Ver](../../.artifacts/front-019/escritorio-autovalidacion-direccion-confirmada.png) | [Ver](../../.artifacts/front-019/movil-autovalidacion-direccion-confirmada.png) |
| Listener real de consulta: estado ocupado, skeleton y control deshabilitado | [Ver](../../.artifacts/front-019/escritorio-cargando.png) | [Ver](../../.artifacts/front-019/movil-cargando.png) |
| ETag obsoleto: bloquear y recargar antes de otra decisión | [Ver](../../.artifacts/front-019/escritorio-conflicto-412.png) | [Ver](../../.artifacts/front-019/movil-conflicto-412.png) |

El informe FRONT_019_VALIDACION_Y_SUPERVISION.md conserva explicación de pantallas/acciones/puestos/estados y resultados locales. Revisión visual del responsable pendiente; no implica autorización de publicación.
