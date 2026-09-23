# TECH-FRONT-002 — cliente Razor/HTTP común

## Autoridad, base y alcance

- Orden expresa del responsable: sólo `TECH-FRONT-002`. Tras revisar la implementación local, autorizó commit, push y PR; el merge, despliegue e inicio de `TECH-FRONT-003` no están autorizados.
- Base verificada: `origin/master` `8c76c094da653903cd72acce9373dd2b0e4a2914`, PR `#62` `MERGED`; cabeza `7a0d8540dbfd211fc0f5156a02d834c5832bbabf`, check requerido `SUCCESS` run `35906130681`, merge `8c76c094da653903cd72acce9373dd2b0e4a2914`. Ambos son ancestros.
- Rama/worktree aislados: `codex/tech-front-002`, `C:\Users\josej\Dev\SGOL-tech-front-002`. El checkout principal conserva sus tres modificaciones ajenas.
- Fuentes: Adendas 44/45, F06 API §§2–4 y 7, F06 pruebas §§3, 6–8, 10–11 y 14, F06 arquitectura §8, cinco documentos `docs/design`, inventario externo definitivo y backend integrado. Sin pantalla funcional, reglas de negocio, permiso cliente, endpoint ni DTO nuevo.

## Inventario observado antes de editar comportamiento

| Superficie | Evidencia integrada |
|---|---|
| Envelopes | `data` + `meta.correlationId` en éxitos singulares. Colecciones como `loads`, obligaciones y auditoría devuelven `data[]` con `meta.nextCursor` y `meta.count`; `inbox` es singular con colecciones anidadas. `POST auth/logout` devuelve `204` sin cuerpo. |
| Errores | `Results.Problem` y middleware emiten `application/problem+json` con `status`, `code` cuando corresponde y `correlationId`; el handler seguro de error 500 genérico puede omitir `code`. `title`, `detail` e `instance` no se usan para texto visible. |
| Correlación | `CorrelationIdMiddleware` emite UUID v7 en `X-Correlation-ID`; los envelopes y problemas lo conservan en JSON. |
| ETag | Respuestas de persona/disponibilidad, configuración, calendario, políticas, validación, plan, evidencia y continuidad incluyen `ETag` fuerte entre comillas en operaciones aplicables. |
| Idempotencia | `Idempotency-Key` UUID canónico. Replays aparecen como `meta.replayed` en continuidad o `data.result = RECUPERADA` en otras operaciones; cuerpo distinto con la misma clave produce `409 IDEMPOTENCY_CONFLICT`. |
| CSRF | Adenda 41 y middleware usan `400 CSRF_INVALID`; inbox, evidencia, conclusión y validación también emiten `400 CSRF_INVALIDO`. |

## BR-API11 aprobada

`CSRF_INVALID` es canónico; `CSRF_INVALIDO` es alias de lectura compatible. Los dos se traducen al mismo título y mensaje de `docs/design/estados-y-mensajes.md`. No se cambia el backend ni se duplican mensajes por endpoint.

## BR-API12 aprobada

| Condición/respuesta | Endpoints integrados representativos | Presentación |
|---|---|---|
| Falta `If-Match`: `400 IF_MATCH_REQUERIDO` | Corrección de asignación, publicación de configuración/plan, conclusión, definición de tarea y decisión de validación; sustitución de políticas cuando corresponde | Recarga el registro antes de guardar los cambios. |
| Ausente o inválido: `400 IF_MATCH_INVALIDO` | Cambio/revocación de rol y corrección de disponibilidad pueden usarlo también para ausencia; otros endpoints lo usan para formato inválido | La versión del registro no es válida. Recárgalo antes de guardar. |
| Ausente o inválido: `428 IF_MATCH_REQUERIDO` | Aprobación de reconciliación de continuidad; el endpoint no distingue ambos casos | Recarga la reconciliación antes de aprobarla. |
| Versión desactualizada: `412 VERSION_CONFLICT` | Mutaciones versionadas | Este registro cambió mientras lo editabas. No hay reintento automático. |

La selección depende de `(status, code)`. El cliente no infiere la causa cuando el backend reúne dos condiciones en el mismo par. Una combinación desconocida conserva el error y usa texto seguro.

## Catálogo de presentación común

| HTTP | Código(s) reconocido(s) | Presentación segura |
|---:|---|---|
| 400 | `CSRF_INVALID`, `CSRF_INVALIDO`, `IF_MATCH_REQUERIDO`, `IF_MATCH_INVALIDO` | Mensajes aprobados anteriores; otros códigos: mensaje genérico de campos inválidos. |
| 401 / 403 / 404 | Códigos del endpoint | Sesión requerida / permiso denegado / recurso no encontrado o fuera de alcance, separados; nunca éxito ni autoridad inferida. |
| 409 | `IDEMPOTENCY_CONFLICT` | Clave ya usada con otros datos; otros códigos usan fallback seguro. |
| 412 | `VERSION_CONFLICT` | Conflicto visible y recarga manual; otros códigos de 412 se tratan como respuesta de concurrencia no reconocida. |
| 413 / 415 | `FILE_TOO_LARGE` / `FILE_TYPE_NOT_ALLOWED` | Archivo demasiado grande / tipo no permitido. |
| 422 | `EVIDENCIA_FALTANTE` | Completar requisitos obligatorios; otros códigos usan fallback seguro. |
| 423 / 429 | `ACCOUNT_LOCKED` / `RATE_LIMITED` | Bloqueo temporal / límite de solicitudes. |
| 428 | `IF_MATCH_REQUERIDO` | Mensaje aprobado de reconciliación. |
| 500 | Código opcional, incluido desconocido | Error genérico con correlación, sin datos internos. |
| 503 | `DEPENDENCY_UNAVAILABLE`, `FILE_SCAN_PENDING` | Servicio no disponible; otros códigos usan fallback seguro. |

## Implementación y cobertura

- `SgolApiClient` está registrado en Web como cliente tipado. Construye únicamente rutas relativas `/api/v1` del mismo origen y usa el contexto Razor activo. El handler desactiva CookieContainer y redirecciones automáticas; se reenvía únicamente `__Host-SGOL-Session` de la solicitud activa. No habilita CORS.
- Mutaciones exigen token CSRF explícito. `ApiMutationIntent` genera UUID y liga la clave a método, ruta, `If-Match` y huella SHA-256 del cuerpo; un cambio de intención falla antes de otra petición. No guarda ni registra el cuerpo.
- Respuestas singulares, colecciones y `204` se validan según su forma; se conservan cursor, conteo, correlación, ETag y señal semántica de replay. Content type, JSON, status, correlación y código inválidos fallan cerrados. Se liberan request, response, contenido y documento JSON.
- `ProblemDetailsPresenter` emplea código y status aprobados; nunca muestra `title`, `detail`, stack, SQL, ruta privada, cookie, secreto ni contenido de evidencia. La autorización efectiva continúa exclusivamente en servidor.
- Pruebas de componente con `HttpMessageHandler` sintético: éxito singular/colección, cookie, ETag/If-Match, idempotencia y replay, todos los 14 status, alias CSRF, matriz If-Match, cuerpo/content type/correlación inválidos, error 500 seguro, 401/403/404 diferenciados, no retry/segunda mutación, ruta restringida y disposición. No sustituyen aceptación funcional real.

## Validación local

| Comprobación | Resultado |
|---|---|
| `scripts/ci/preflight.ps1` | Pasó: base `8c76c094`, rama correcta, árbol limpio. |
| `dotnet restore --locked-mode` | Una vez por worktree nuevo; 25 proyectos, cero errores/advertencias. |
| `dotnet build --no-restore --configuration Release` | 25 proyectos, cero errores/advertencias. |
| Unitarias enfocadas `SgolApiClientTests|SharedInterfaceTests` | 41/41 aprobadas. |
| `git diff --check` | Pasó, sin errores de whitespace en archivos seguidos por Git. |

PostgreSQL, Testcontainers, Docker, S3, ClamAV y Playwright: **no aplican** a este componente. No se ejecutaron suites completas, servidores ni migraciones. Estado del corte de validación: `Implementada localmente`; la publicación y la integración requieren evidencia posterior y no se infieren de este registro.
