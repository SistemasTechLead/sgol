# F06 — Contrato de API del MVP de SGOL

## 1. Control

| Campo | Valor |
|---|---|
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Aprobación literal del responsable | `Apruebo los seis entregables de Fase 06 y autorizo su incorporación a Fuentes` |
| Estilo | REST JSON, `/api/v1` |
| Cliente MVP | Interfaz web SGOL de mismo origen |
| Autenticación | Cookie segura de sesión, credenciales locales y MFA TOTP |
| Alcance | HU-001 a HU-035 y ocho tareas MVP |
| Exclusión | Este contrato no constituye una API pública ni autoriza integraciones externas |

## 2. Convenciones generales

- HTTPS obligatorio fuera de local.
- JSON UTF-8; nombres de propiedades `camelCase`.
- Fechas locales `YYYY-MM-DD`; instantes RFC 3339 con desplazamiento/UTC.
- IDs públicos UUID v7; códigos funcionales se conservan como texto.
- Respuesta exitosa singular: `{ "data": ..., "meta": ... }`.
- Colecciones: `{ "data": [...], "meta": { "nextCursor": ..., "count": ... } }`.
- Paginación por cursor, máximo 100; valor predeterminado 25.
- Filtros no reconocidos devuelven `400`, no se ignoran silenciosamente.
- `correlationId` se devuelve en todas las respuestas y se registra en auditoría.
- Mutaciones idempotentes usan `Idempotency-Key` UUID; actualizaciones usan `If-Match` con ETag.
- No se acepta método `DELETE` para hechos históricos, auditoría, evidencia, versiones, asignaciones o validaciones.
- `Content-Type: application/problem+json` para errores.

## 3. Seguridad del canal y sesión

La API de navegador usa cookie `Secure`, `HttpOnly`, `SameSite=Strict`. Toda mutación requiere token CSRF enlazado a la sesión. CORS está deshabilitado. Una petición no autenticada recibe `401`; autenticada sin autoridad, `403`, sin revelar si existe un recurso fuera de alcance.

### 3.1 Ciclo de autenticación

| Método y ruta | Operación | Permiso/condición | Resultado principal |
|---|---|---|---|
| `POST /api/v1/auth/login` | Verificar usuario/contraseña. | Cuenta activa; rate limit. | `200` desafío MFA o `423` bloqueo. |
| `POST /api/v1/auth/mfa/verify` | Verificar TOTP o código de recuperación. | Desafío vigente. | Crea sesión; rota identificador. |
| `POST /api/v1/auth/logout` | Cerrar sesión actual. | Autenticado. | `204`; invalida cookie. |
| `GET /api/v1/auth/session` | Obtener usuario, persona, rol, permisos efectivos y expiración. | Autenticado. | Snapshot sin secretos. |
| `POST /api/v1/auth/password/change` | Cambiar contraseña propia. | Contraseña actual + MFA reciente. | Invalida otras sesiones. |
| `POST /api/v1/auth/mfa/enroll` | Iniciar alta TOTP. | Primera activación o reset autorizado. | Secreto temporal, nunca logueado. |
| `POST /api/v1/auth/mfa/confirm` | Confirmar TOTP y emitir códigos de recuperación. | Código válido. | Códigos mostrados una vez. |
| `POST /api/v1/auth/recovery-codes/regenerate` | Sustituir códigos. | Contraseña + MFA reciente. | Invalida anteriores. |

No hay recuperación por correo o SMS. Dirección puede iniciar un reset administrado; la recuperación de la última cuenta de Dirección usa el runbook break-glass de `F06_SEGURIDAD_Y_OPERACION.md`.

## 4. Errores

```json
{
  "type": "https://sgol.local/problems/evidencia-faltante",
  "title": "No puede concluirse la obligación",
  "status": 422,
  "code": "EVIDENCIA_FALTANTE",
  "detail": "Faltan requisitos obligatorios.",
  "instance": "/api/v1/obligations/019.../conclusion",
  "correlationId": "019...",
  "errors": [
    { "field": "evidence", "code": "MISSING", "reference": "FOTO_FINAL" }
  ]
}
```

| HTTP | Uso |
|---:|---|
| 400 | Sintaxis, filtro o formato inválido. |
| 401 | Falta sesión o MFA incompleto. |
| 403 | Permiso/alcance/relación jerárquica denegado. |
| 404 | Recurso inexistente o deliberadamente oculto por alcance. |
| 409 | Unicidad o idempotencia con contenido conflictivo. |
| 412 | `If-Match`/versión desactualizada. |
| 413 | Archivo mayor de 15 MiB. |
| 415 | Tipo no permitido o no coincide con contenido. |
| 422 | Guarda funcional incumplida. |
| 423 | Cuenta bloqueada temporalmente. |
| 429 | Límite de intentos/solicitudes. |
| 500 | Error no controlado con referencia, sin detalles internos. |
| 503 | Dependencia no disponible o escaneo pendiente/fallido cuando impide continuar. |

Se conservan los errores normalizados F05: `ACCESO_DENEGADO`, `ALCANCE_INVALIDO`, `CUENTA_NO_INDIVIDUAL`, `ROL_MULTIPLE`, `CALENDARIO_INVALIDO`, `DEFINICION_NO_MVP`, `CONFIGURACION_SOLAPADA`, `ORIGEN_INCOMPLETO`, `OBLIGACION_DUPLICADA_RECUPERADA`, `SIN_CANDIDATO_ELEGIBLE`, `ASIGNACION_NO_AUTORIZADA`, `PLAN_FUERA_DE_ALCANCE`, `EVIDENCIA_FALTANTE`, `EVIDENCIA_SUSTITUCION_NO_AUTORIZADA`, `CONCLUSION_NO_PERMITIDA`, `VALIDACION_NO_AUTORIZADA`, `AUTOVALIDACION_NO_PERMITIDA`, `MOTIVO_OBLIGATORIO`, `AUDITORIA_NO_ELIMINABLE` y `ERROR_FUNCIONAL_REGISTRADO`.

Se añaden códigos técnicos: `IDEMPOTENCY_CONFLICT`, `VERSION_CONFLICT`, `FILE_TOO_LARGE`, `FILE_TYPE_NOT_ALLOWED`, `FILE_SCAN_PENDING`, `FILE_REJECTED`, `RATE_LIMITED`, `MFA_REQUIRED`, `ACCOUNT_LOCKED` y `DEPENDENCY_UNAVAILABLE`.

## 5. Recursos y endpoints

### 5.1 Personas, cuentas, roles y disponibilidad

| Método y ruta | Operación | Permiso | Reglas principales |
|---|---|---|---|
| `GET /people` | Listar personas visibles. | `PER-PERSONA-ADMIN` | MVP: Dirección. |
| `POST /people` | Crear persona. | `PER-PERSONA-ADMIN` | Código estable único; audita. |
| `GET /people/{id}` | Consultar persona e historial. | `PER-PERSONA-ADMIN` | Incluye versiones laborales. |
| `PATCH /people/{id}/employment` | Crear versión laboral. | `PER-PERSONA-ADMIN` | `If-Match`; no altera rol. |
| `POST /people/{id}/deactivate` | Baja funcional. | `PER-PERSONA-ADMIN` | No borra historia/cuenta. |
| `POST /people/{id}/reactivate` | Reactivar. | `PER-PERSONA-ADMIN` | Revalida cuenta/rol por separado. |
| `GET /users` | Listar cuentas. | `PER-USUARIO-ADMIN` | Sin hashes ni secretos. |
| `POST /users` | Crear cuenta/activación. | `PER-USUARIO-ADMIN` | Individual, una persona; contraseña temporal de un uso. |
| `POST /users/{id}/deactivate` | Desactivar e invalidar sesiones. | `PER-USUARIO-ADMIN` | Conserva hechos. |
| `POST /users/{id}/reactivate` | Reactivar. | `PER-USUARIO-ADMIN` | Requiere nueva activación segura. |
| `POST /users/{id}/mfa-reset` | Resetear MFA. | `PER-USUARIO-ADMIN` | Motivo; invalida sesiones y códigos. |
| `POST /users/{id}/role-assignments` | Asignar/cambiar/revocar rol. | `PER-ROL-ADMIN` | Un rol activo; versión histórica. |
| `GET /people/{id}/availability` | Consultar disponibilidad. | `PER-DISPONIBILIDAD-ADMIN` o alcance de asignación | Rango de fechas acotado. |
| `PUT /people/{id}/availability/{date}` | Crear/corregir valor. | `PER-DISPONIBILIDAD-ADMIN` | Sólo booleano; nueva versión. |

### 5.2 Sucursal, calendario y configuración

| Método y ruta | Operación | Permiso | Reglas |
|---|---|---|---|
| `GET /branches/LOR-001` | Consultar sucursal. | Autenticado | No admite otro código. |
| `PATCH /branches/LOR-001` | Mantener nombre/estado permitido. | `PER-SUCURSAL-ADMIN` | No crea sucursal nueva. |
| `GET /calendar?from=&to=` | Consultar calendario. | Autenticado | Fechas locales y zona visibles. |
| `PUT /calendar/{date}` | Crear versión de día. | `PER-CALENDARIO-ADMIN` | Laborable/festivo/cierre; motivo. |
| `GET /weeks/{isoYear}/{isoWeek}` | Consultar período. | `PER-PLAN-VER` | Único; sin cierre formal. |
| `GET /configuration/releases` | Listar versiones. | `PER-CONFIG-ADMIN` | Historial completo. |
| `POST /configuration/releases` | Crear borrador. | `PER-CONFIG-ADMIN` | Idempotente. |
| `POST /configuration/releases/{id}/publish` | Publicar. | `PER-CONFIG-ADMIN` | Valida no solapamiento; motivo. |

### 5.3 Definiciones y políticas

| Método y ruta | Operación | Permiso | Reglas |
|---|---|---|---|
| `GET /task-definitions` | Listar ocho TAR e historial. | Autenticado según alcance | Nunca devuelve opcionales como activas. |
| `GET /task-definitions/{taskCode}` | Consultar snapshot vigente/histórico. | Autenticado | `taskCode` sólo catálogo MVP. |
| `POST /task-definitions/{taskCode}/versions` | Crear borrador sucesor. | `PER-DEFINICION-ADMIN` | No altera obligaciones existentes. |
| `POST /task-definitions/{taskCode}/versions/{id}/publish` | Publicar versión. | `PER-DEFINICION-ADMIN` | Valida esquema y políticas. |
| `POST /task-definitions/{taskCode}/deactivate-new` | Impedir nuevas generaciones. | `PER-DEFINICION-ADMIN` | Historia permanece. |
| `PUT /task-definitions/{taskCode}/activation-policy` | Versionar manual/recurrencia. | `PER-ACTIVACION-ADMIN` | Rechaza evento/condición/exterior. |
| `PUT /task-definitions/{taskCode}/eligibility-policy` | Versionar rol/turno. | `PER-POLITICA-ADMIN` | Rol exacto; disponibilidad obligatoria. |
| `PUT /task-definitions/{taskCode}/evidence-policy` | Versionar requisitos. | `PER-EVIDENCIA-CONFIG` | Tipos aprobados; todos obligatorios. |
| `PUT /task-definitions/{taskCode}/validation-policy` | Versionar validación. | `PER-VALIDACION-CONFIG` | Superior inmediato; tres resultados. |

### 5.4 Generación, obligaciones y asignación

| Método y ruta | Operación | Permiso | Idempotencia/guardas |
|---|---|---|---|
| `POST /generation-requests` | Solicitar alta manual. | `PER-OBLIGACION-CREAR` | `Idempotency-Key`; TAR activa, nivel propio/inferior, origen completo. |
| `GET /generation-requests/{id}` | Consultar resultado. | Creador o superior visible | Muestra `ACEPTADA/RECUPERADA/RECHAZADA`. |
| `GET /obligations` | Bandeja/consulta jerárquica. | `PER-TAREA-VER` | Filtros: período, TAR, estado, condición, responsable; filtro de alcance obligatorio. |
| `GET /obligations/{id}` | Detalle e historia. | `PER-TAREA-VER` | Origen, fechas, vencida, versiones y links autorizados. |
| `GET /obligations/{id}/eligibility` | Explicación de candidatos/ranking. | `PER-ASIGNACION-EXPLICAR` | Oculta datos fuera de alcance. |
| `POST /obligations/{id}/assignment-corrections` | Sustituir responsable. | `PER-ASIGNACION-CORREGIR` | `If-Match`, elegible, inferior y motivo. |
| `GET /loads` | Carga activa por persona visible. | `PER-CARGA-VER` | Cuenta sólo pendientes con asignación vigente. |

Ejemplo de alta manual:

```json
{
  "taskCode": "TAR-0007",
  "branchCode": "LOR-001",
  "origin": {
    "type": "MANUAL",
    "reference": "SEP-2026-0042"
  },
  "input": {
    "separadoReference": "SEP-2026-0042",
    "merchandiseReference": "MRC-17",
    "startsAt": "2026-08-27T13:00:00-06:00",
    "dueAt": "2026-08-28T13:00:00-06:00"
  }
}
```

Respuesta recuperada:

```json
{
  "data": {
    "generationRequestId": "019...",
    "result": "RECUPERADA",
    "obligationId": "019..."
  },
  "meta": { "correlationId": "019..." }
}
```

### 5.5 Plan y publicación

| Método y ruta | Operación | Permiso | Reglas |
|---|---|---|---|
| `GET /plans/{isoYear}/{isoWeek}` | Obtener plan único. | `PER-PLAN-VER` | Devuelve alcance autorizado. |
| `POST /plans/{isoYear}/{isoWeek}/ensure` | Crear/recuperar plan. | `PER-PLAN-PUBLICAR` | Idempotente; jamás segundo plan. |
| `POST /plans/{planId}/publications` | Publicación inicial/incremental. | `PER-PLAN-PUBLICAR` | Propio nivel/inferior; nueva versión, misma identidad. |
| `GET /plans/{planId}/versions` | Historial. | `PER-PLAN-VER` | Respeta alcance de cada publicación. |

### 5.6 Archivos y evidencia

| Método y ruta | Operación | Permiso | Reglas |
|---|---|---|---|
| `POST /files/upload-intents` | Crear objeto de cuarentena y URL firmada. | `PER-EVIDENCIA-APORTAR` o `PER-EVIDENCIA-SUSTITUIR` | Nombre, tipo declarado, tamaño ≤15 MiB; obligación visible. |
| `POST /files/{id}/complete` | Confirmar carga y poner a escanear. | Creador de intención | Verifica objeto/tamaño/hash; idempotente. |
| `GET /files/{id}/status` | Consultar escaneo/réplica. | Actor autorizado sobre obligación | No expone clave interna ni URL permanente. |
| `POST /obligations/{id}/evidence` | Vincular archivo limpio/registro estructurado. | `PER-EVIDENCIA-APORTAR` | Tarea propia pendiente; requisito aplicable. |
| `POST /obligations/{id}/evidence/{itemId}/replacements` | Crear versión sucesora. | Antes: responsable; después: `PER-EVIDENCIA-SUSTITUIR` | Superior y motivo después de conclusión. |
| `GET /obligations/{id}/evidence` | Consultar versiones. | Alcance de F05 | Indica vigente/sustituida y hash. |
| `GET /files/{id}/download` | Obtener URL firmada corta. | Autorización revaluada | Sólo archivo limpio; sin CDN público. |

La URL de carga expira en 10 minutos y autoriza un objeto exacto. La URL de descarga expira en 5 minutos. Estos tiempos pueden endurecerse por configuración sin cambiar el contrato.

### 5.7 Conclusión y validación

| Método y ruta | Operación | Permiso | Reglas |
|---|---|---|---|
| `POST /obligations/{id}/conclusion` | Concluir tarea. | `PER-TAREA-EJECUTAR` | Responsable vigente, `If-Match`, evidencia completa; `Idempotency-Key`. |
| `GET /obligations/{id}/evidence-review` | Ver integridad y faltantes. | Responsable/superior visible | `COMPLETA/INCOMPLETA`. |
| `GET /validations/pending` | Bandeja de validación. | `PER-VALIDACION-EMITIR/ESCALAR` | Sólo autoridad válida. |
| `POST /obligations/{id}/validation-decisions` | Emitir decisión. | `PER-VALIDACION-EMITIR` o escalamiento | Tarea concluida; resultado permitido; fundamento; motivo si escalada. |
| `POST /validation-decisions/{id}/replacements` | Sustituir decisión. | `PER-VALIDACION-SUSTITUIR` | Validador original o superior; motivo; conserva anterior. |
| `GET /obligations/{id}/validations` | Consultar historial. | Alcance F05 | Ejecución permanece separada. |

Ejemplo de validación:

```json
{
  "result": "INCOMPLETA",
  "foundation": "La evidencia acredita ocho de los diez ítems del checklist.",
  "escalationReason": null
}
```

### 5.8 Bandejas, indicadores, auditoría y continuidad

| Método y ruta | Operación | Permiso | Reglas |
|---|---|---|---|
| `GET /me/inbox` | Bandeja propia y avisos. | `PER-BANDEJA-PROPIA` | Sólo propio; marcar lectura no cambia estado de tarea. |
| `POST /me/notices/{id}/read` | Marcar aviso leído. | Destinatario | Idempotente. |
| `GET /supervision/obligations` | Vista inferior. | `PER-SUPERVISION-VER` | Nunca pares/superiores. |
| `GET /indicators` | Cinco indicadores. | `PER-INDICADOR-VER` | Período, alcance y conteo base obligatorios; sin montos. |
| `GET /direction/overview` | Vista integral. | `PER-DIRECCION-VER` | `LOR-001`; cinco indicadores. |
| `GET /audit-events` | Consultar auditoría. | `PER-AUDITORIA-VER` | Cursor, actor, recurso, fecha y acción; filtro jerárquico. |
| `GET /audit-events/{id}` | Detalle. | `PER-AUDITORIA-VER` | Datos minimizados; no secretos. |
| `GET /continuity/recovery-runs` | Ver simulacros/recuperaciones. | `PER-CONTINUIDAD-VER` | Dirección. |
| `GET /continuity/recovery-runs/{id}` | Resultado y conciliación. | `PER-CONTINUIDAD-VER` | Sin credenciales/rutas secretas. |

No existe `DELETE /audit-events`, `DELETE /evidence`, `DELETE /obligations` ni equivalentes. Todo intento a una ruta inexistente de eliminación sensible se registra como evento de seguridad cuando está autenticado.

## 6. Contratos de tarea

Los campos por TAR se validan mediante esquema versionado asociado a `taskDefinitionVersionId`. La respuesta siempre identifica esa versión. El servidor rechaza:

- campos requeridos ausentes;
- campos ajenos al esquema vigente;
- referencias padre inexistentes o fuera de alcance;
- horario/SLA no aprobado;
- tipo de evidencia no permitido;
- intento de activar una TAR no MVP.

Las reglas exactas son CAT-001 a CAT-008 de F05. OpenAPI referencia un esquema discriminado por `taskCode`, no un objeto libre sin validación.

## 7. Control de concurrencia e idempotencia

### 7.1 `Idempotency-Key`

- Obligatoria en altas manuales, conclusión, publicación, validación, sustituciones y operaciones de archivo finalizadas.
- Alcance: usuario + operación + recurso.
- Misma clave y mismo cuerpo: misma respuesta semántica e ID.
- Misma clave y cuerpo distinto: `409 IDEMPOTENCY_CONFLICT`.
- No sustituye las claves funcionales permanentes de generación.

### 7.2 `ETag` / `If-Match`

- Los recursos mutables devuelven `ETag: "<rowVersion>"`.
- `PATCH`, publicación, conclusión o sustitución sensible requiere `If-Match`.
- Versión desactualizada: `412 VERSION_CONFLICT`, sin efecto parcial.

## 8. Versionado y compatibilidad

- Cambios compatibles agregan campos opcionales y preservan semántica.
- Cambios incompatibles requieren `/api/v2` y ADR.
- Campos no se reutilizan con otro significado.
- La UI y la API se despliegan juntas, pero el contrato se prueba de forma independiente.
- OpenAPI versionado forma parte del artefacto de liberación.

## 9. Límites y rate limiting

| Operación | Límite inicial |
|---|---|
| Login | 5 fallos por cuenta y 10 por IP/15 min; bloqueo 15 min con escalamiento por reincidencia. |
| MFA | 5 fallos por desafío; invalida desafío y aplica bloqueo. |
| API autenticada | 120 solicitudes/min por sesión, con límites más bajos en mutaciones. |
| Intenciones de archivo | 30/h por usuario, ajustable por evidencia real. |
| Consulta | rango máximo predeterminado 93 días; auditoría hasta 31 días por página/consulta. |

Los límites protegen, pero no reemplazan autorización. Un `429` no produce cambios.

## 10. Trazabilidad API–historias

| Historias | Recursos principales |
|---|---|
| HU-001 a HU-007 | personas, cuentas, roles, disponibilidad, sucursal |
| HU-008 a HU-012 | configuración, calendario, semanas, definiciones y políticas |
| HU-013 a HU-021 | generación, obligaciones, elegibilidad, asignación y planes |
| HU-022 a HU-028 | conclusión, consulta, evidencia y validación |
| HU-029 a HU-032 | indicadores, bandeja, supervisión y Dirección |
| HU-033 a HU-035 | auditoría, idempotencia y continuidad |

## 11. Criterios de aceptación del contrato

1. Cada HU-001 a HU-035 tiene al menos una operación o se identifica como cálculo interno.
2. Cada mutación declara permiso, guarda, idempotencia y respuesta de error.
3. Las pruebas negativas F05 pueden ejecutarse sin acceso directo a la base.
4. No hay rutas para capacidades posteriores o excluidas.
5. Descarga, auditoría y consultas aplican alcance jerárquico en servidor.
6. El OpenAPI generado coincide con este contrato y falla CI ante divergencia incompatible.
