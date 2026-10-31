# Cobertura de cierre para Subcoordinación — 31/10/2026

## Alcance y comportamiento

Cambio mixto de autorización e interfaz. Dirección y Subcoordinación pueden consultar y guardar la cobertura compartida desde **Planificación → Cobertura de cierre**. No se amplían los permisos para planes de Dirección, edición de horarios personales ni excepciones. Asesor Externo y Piso de ventas permanecen excluidos.

La interfaz reutiliza el diálogo existente. `Index.Closing.cs` define `CanManageClosing`, independiente de `CanPrepareDirectionPlan`; `Index.DirectionPlans.cs`, `_DirectionPlans.cshtml` y `_Closing.cshtml` lo consumen. El POST exige rol admitido, permiso de consulta de planificación y antiforgery. `ClosingPlanningService.cs` valida autoridad vigente mediante `AuthorizeWeeklyOperationsAsync`, cuya comprobación actual admite únicamente Dirección/Subcoordinación activos, con empleo y sucursal válidos.

El dato sigue entrando por `ClosingSelectionInput`, se valida contra los candidatos del horario efectivo y se guarda como `ClosingCoverageVersion` con actor, fecha, revisión e intención; conserva auditoría `CLOSING_COVERAGE_SAVED`. `TemporalEligibilityContext` continúa consumiendo esas versiones para cobertura y horario efectivo. No cambia esquema, generación, asignación, ejecución ni snapshots históricos. Se conservan exclusión de Dirección como candidato, ausencias, elegibilidad, extensión de una hora, bloqueo transaccional, idempotencia y rechazo de revisiones vencidas.

## Entorno y evidencia

Rama `master`, con cambios previos de interfaz móvil conservados. El proceso compartido observado usa `src/Sgol.Web/bin/Debug/net10.0/Sgol.Web.exe` bajo `dotnet watch`; durante el trabajo se recompiló y reinició automáticamente. No se ejecutó despliegue separado, commit ni push. Las escrituras de prueba se limitaron a `localhost:5440/sgol_task_checks_closing_20261031`, creada expresamente; no se modificaron coberturas de la simulación compartida.

Prueba mantenible: `tools/Sgol.TaskChecks/ClosingPermissionChecks.cs`, opción `--closing-permissions-only`. Usa servicios reales, Razor, antiforgery, API y PostgreSQL; la sesión autenticada se sustituye por un fixture de rol (no prueba el login). Fecha del reloj del fixture: 05/10/2026; no confundir con fecha operativa actual.

Comandos ejecutados:

- `dotnet build tools/Sgol.TaskChecks/Sgol.TaskChecks.csproj -c Release --no-restore -o .local/closing-permissions-build`: aprobado, cero errores/advertencias.
- `dotnet .local/closing-permissions-build/Sgol.TaskChecks.dll --closing-permissions-only`, con conexión privada a base aislada: aprobado.
- Registros locales: `.local/closing-permissions-build-r2.log`, `.local/closing-permissions-test.log`.

## Matriz de validación

| Escenario / acción | Resultado esperado | Prueba y entorno | Estado |
|---|---|---|---|
| Dirección/Subcoordinación consultan candidatos | Ambas autorizadas | Servicio + PostgreSQL aislado | Aprobado |
| Subcoordinación abre, guarda y reabre formulario | Redirección, éxito y autoría real persistida | HTTP Razor/API aislados | Aprobado |
| Dirección guarda después de Subcoordinación | Nueva revisión compartida | HTTP + base aislada | Aprobado |
| Asesor Externo/Piso llaman servicio de consulta/guardado | 403 | Servicio aislado | Aprobado |
| Asesor Externo consulta pantalla y manipula POST | Sin controles; POST 403 | HTTP aislado | Aprobado |
| Subcoordinación intenta crear plan de Dirección o leer horario personal | 403 | HTTP/servicio aislados | Aprobado |
| Formulario sin CSRF | 400, sin escritura | HTTP aislado | Aprobado |
| Repetir intención | Misma cobertura, sin duplicados | HTTP + conteo en base | Aprobado |
| Enviar revisión anterior tras guardado ajeno | 409, sin versión adicional | HTTP + conteo en base | Aprobado |
| Elegir persona sin horario elegible | 422, sin versión adicional | HTTP + conteo en base | Aprobado |
| Abrir/cerrar diálogo con teclado y Escape | Foco inicial en Cerrar, retorno al botón | Navegador integrado, aplicación real aislada | Aprobado |
| Escritorio y ancho 320 px | Acceso y formulario visibles, scroll interno | Inspección renderizada, tema claro | Aprobado |
| Domingo sin candidatos | Guardado deshabilitado | Navegador, fixture aislado | Aprobado |
| Temas oscuro/especial, Safari físico y lector de pantalla | Conservar presentación existente | Sin cambios CSS, no cubiertos en esta ejecución | No ejecutado |
| Login real y guardado en simulación compartida | Acceso con sesión real de Subcoordinación | No se usaron credenciales ni se alteraron coberturas compartidas | No ejecutado |
| Comparación de asignaciones históricas existentes | Sin modificaciones | Inspección del flujo; fixture sin historial operativo | No ejecutado como integración histórica |
| Migración de datos/esquema | No requerida | Sin cambios de modelo | No aplicable |

El caso de revisión vencida simula un segundo formulario abierto antes del primer guardado; no es una prueba de dos transacciones ejecutadas simultáneamente. Las comprobaciones HTTP de autorización y persistencia están aprobadas; la cobertura visual completa y el login compartido quedan pendientes.
