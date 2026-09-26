# SGOL — Adenda 50 a F07: contrato consumidor de FRONT-011

## Control

| Campo | Valor |
|---|---|
| Estado | Aprobada expresamente por el responsable el 2026-09-26 para FRONT-011; incorporación propuesta en la rama `codex/front-011` |
| Motivo | BR-API09 y UI-C06/C07 carecían de lectura versionada y contrato de presentación suficientes para editar y recargar tras `412` |
| Alcance | Sólo `FRONT-011`, `HU-012` y `HU-017` |
| Fuente | Fila 79 de Adenda 45, F05 y F06 vigentes, contratos reales de `ActivationPolicyCatalog` y `EligibilityPolicyCatalog` |

## Lectura API mínima

Se agregan dos métodos GET al mismo recurso canónico que ya admitía PUT:

- `GET /api/v1/task-definitions/{taskCode}/activation-policy`;
- `GET /api/v1/task-definitions/{taskCode}/eligibility-policy`.

Ambos reciben sólo uno de los ocho códigos TAR MVP. La respuesta `200` usa `data: { taskCode, current, history }` y `meta.correlationId`. `current` es la versión `VIGENTE` o `null`; `history` contiene las versiones de esa TAR en orden descendente de versión, con los campos ya definidos en `ActivationRuleVersionDetails` o `EligibilityPolicyVersionDetails`. Cuando existe `current`, la respuesta lleva su ETag fuerte `"<rowVersion>"`; sin vigente, no lleva ETag. La lectura reautoriza en servidor a Dirección vigente de `LOR-001` conforme al permiso de la política. Una TAR fuera del MVP devuelve `404 DEFINICION_NO_DISPONIBLE` sin datos de otra TAR; ausencia de sesión da `401` y falta de autoridad da `403`.

El PUT existente conserva `Idempotency-Key`, CSRF e `If-Match` al sustituir una versión vigente. La respuesta PUT informa `meta.replayed` para distinguir una intención recuperada de una nueva; ese dato no pertenece al recurso persistido. Un `412 VERSION_CONFLICT` exige GET nuevo y decisión explícita, sin retry automático. Los servicios conservan auditoría, historia y no efecto de fallos en PostgreSQL.

## Presentación aprobada

UI-C06/C07 viven en el detalle TAR ya registrado de `/configuracion?taskCode=...`. El editor de activación toma modo, `originKeySchema` y schedule del catálogo cerrado: manual con `null`, `TAR-0005` con dos ventanas laborables 12:00 y 17:00, y `TAR-0026` con tres días hábiles antes y hora local `HH:mm`. El editor de elegibilidad usa el rol exacto de cada TAR, disponibilidad positiva y turno nulo. No hay editor JSON, campo de puesto, evento, condición, origen exterior, subruta web nueva ni publicación de política independiente de la release. `docs/design/componentes.md`, `estados-y-mensajes.md` y `navegacion.md` fijan los estados, mensajes, confirmaciones, foco y reflow consumidores aprobados.

Esta adenda no modifica el contenido congelado de F00–F07 ni autoriza FRONT-012, FRONT-013 o TECH-FRONT-005.
