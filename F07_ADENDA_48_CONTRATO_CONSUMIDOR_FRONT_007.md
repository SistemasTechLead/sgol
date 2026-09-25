# Adenda 48 — Contrato consumidor de FRONT-007

## Control

El responsable aprobó expresamente durante FRONT-007 tres extensiones limitadas a UI-I06/I07: lectura de rol e historia, extensión mínima de `docs/design/componentes.md` y `estados-y-mensajes.md`, y generación por servidor de la contraseña temporal de reset MFA con visualización única y entrega presencial. No habilita FRONT-008, TECH-FRONT-005 ni break-glass.

## Contrato efectivo

1. `GET /api/v1/users/{id}/role-assignments` devuelve el `RoleAssignmentDetails` existente y ETag sólo cuando hay rol `ACTIVO`. Requiere `PER-ROL-ADMIN` vigente y cuenta activa con empleo vigente en `LOR-001`. Cuenta inexistente, inactiva o fuera de alcance comparte 404 seguro. La consulta no crea historia ni autoridad.
2. UI-I06 reutiliza `/personas-y-accesos`, el cliente HTTP y la sesión request-scoped. Asignar usa la ausencia de rol; cambiar o revocar el vigente exige su ETag mediante `If-Match`, además de CSRF, motivo e `Idempotency-Key` por intención. Un 412 exige recarga explícita y no reintento automático.
3. En reset MFA, `POST /api/v1/users/{id}/mfa-reset` acepta el flujo previo con contraseña suministrada. Si se omite, el servidor genera la contraseña temporal y sólo la respuesta nueva la presenta a Dirección, con `Cache-Control: no-store`; el replay no puede recuperarla. Dirección la entrega presencialmente fuera de SGOL. No se guarda en auditoría, idempotencia, URL, log o captura.
4. El servidor exige MFA del actor autenticado en los últimos cinco minutos para reset de terceros antes de mutar. Revalida permiso, cuenta y empleo; conserva revocación de TOTP/códigos, rotación de `SecurityStamp`, auditoría y no efecto en rechazo.

Esta adenda complementa F06, la Adenda 41 y la fila 70 de la Adenda 45. La autorización puntual de Adenda 47 para FRONT-006 no se usa como fundamento del reset.
