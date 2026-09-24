# Adenda 47 — Ajuste de activación temporal para FRONT-006

## Control

Esta adenda registra la decisión expresa del responsable durante FRONT-006. Ajusta únicamente la fila 69 de `F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md` en lo relativo a BR-API08, la generación y la presentación de la contraseña temporal. No habilita FRONT-007, reset MFA, asignación de roles ni otro canal de entrega.

## Contrato efectivo de FRONT-006

1. `POST /api/v1/users` y `POST /api/v1/users/{id}/reactivate` aceptan el flujo existente con `TemporaryPassword` suministrada para consumidores previos. Cuando el campo se omite, el backend genera una contraseña temporal criptográficamente aleatoria y la persiste sólo como hash.
2. La respuesta de una mutación nueva con generación contiene `account` y `temporaryPassword` para una visualización única a Dirección. La respuesta lleva `Cache-Control: no-store`. Listados, auditoría, registros de idempotencia, logs, URL y GET de cuentas excluyen el secreto.
3. El replay con la misma `Idempotency-Key` devuelve el resultado persistido sin contraseña; otra carga de la página tampoco puede recuperarla. Una clave reutilizada con contenido distinto conserva el conflicto. No se reintenta automáticamente.
4. La excepción aprobada al criterio anterior «sin secreto en DOM» permite mostrarlo únicamente en la respuesta nueva de alta o reactivación. Las capturas enmascaran el bloque completo y la identidad de sesión; los reportes no serializan el valor. Dirección entrega la contraseña presencialmente a la persona fuera de SGOL. No hay correo, SMS, descarga ni impresión.
5. La activación sigue exigiendo cambio de contraseña y MFA según la Adenda 41. La cuenta permanece individual, vinculada a una persona activa de `LOR-001`; desactivación y reactivación conservan historia, auditoría e invalidación de sesiones.

## Evidencia y límites

El código de FRONT-006 y las pruebas enfocadas de API, PostgreSQL y navegador deben demostrar secreto sólo en respuesta nueva, ausencia en replay y persistencia, autorización de `PER-USUARIO-ADMIN`, CSRF, idempotencia, auditoría y no-efecto. Esta adenda no declara publicación, integración ni cierre formal por sí misma.
