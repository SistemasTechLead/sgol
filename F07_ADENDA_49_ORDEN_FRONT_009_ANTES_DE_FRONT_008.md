# SGOL — Adenda 49: orden acotado de FRONT-009 y FRONT-008

## Decisión aprobada

El responsable aprobó el 2026-09-25 que `FRONT-009` pueda integrarse antes de `FRONT-008`, como excepción exclusiva al orden de la fila 77 de `F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md`. `FRONT-009` parte de `origin/master` sin incorporar los commits del PR draft `#74` de `FRONT-008`.

La excepción altera sólo el orden de integración. `FRONT-008` conserva la responsabilidad de consultar `LOR-001`, semana ISO y calendario, editar días de una release `BORRADOR` y completar el recorrido combinado de primera release tras incorporar `FRONT-009`. Su PR `#74` debe adaptarse al nuevo `origin/master` sin perder el trabajo de `FRONT-009` y obtener validación y autorización de merge propias.

No se aprueban `FRONT-010`, `TECH-FRONT-005`, `BR-API01`, edición de sucursal, nuevos endpoints, reglas de dominio ni merge automático. La publicación de `FRONT-009` exige una orden separada; el merge exige autorización expresa y check verde del SHA exacto.
