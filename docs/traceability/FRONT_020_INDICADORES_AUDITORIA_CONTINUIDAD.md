# FRONT-020 — Indicadores, auditoría y continuidad

2026-10-01. **Implementada localmente**, aspecto visual, commit, PR y publicación aprobados mediante «Apruebo el aspecto visual, su commit, pr y publicacion». Plan aprobado íntegramente mediante «Apruebo integramente el plan», incorporado por Adenda 59. Rama local `codex/front-020`, base integrada de FRONT-019. Commit implementado: commit que contiene esta actualización, resoluble en Git. Publicada al existir en el proveedor el PR que incorpora este registro; no Integrada ni Terminada formal. Merge y despliegue pendientes de sus autorizaciones expresas.

## Resultado y alcance

UI-R01/R02: `/indicadores` consulta los cinco indicadores existentes por período ISO explícito. Operación conserva personas propias e inferiores; el panorama de Dirección incluye toda LOR-001 y explica las obligaciones sin asignación. Conteos y denominadores provienen del contrato; no se añade cálculo de negocio, sexto indicador ni exportación. Hay filtros independientes, cursores protegidos por actor/sección/contexto, ceros explícitos, carga y errores sin convertir anomalías en ceros.

UI-U01/U02: `/auditoria` diferencia los modos Eventos y Traza de obligación. Exige intervalo UTC incluido/excluido de hasta 31 días, valida filtros y combinaciones, consume la cerca y el cursor existentes y declara etapas ausentes. `/auditoria/eventos/{eventId}` muestra la proyección minimizada, cambios escalares autorizados y presencia de motivo, sin su contenido. Regresar conserva filtros/cursor y foco mediante contexto protegido. No existe operación de borrado.

UI-K01..K03: `/continuidad` consulta sólo por ID y permite preparar solicitud motivada. El detalle presenta estado, secuencia, hashes, objetivos y todas las filas de diferencias recibidas; MATCHED sigue pendiente de aprobación, DIFFERENT/FAILED/truncado nunca permiten aprobar. El GET conserva su auditoría VIEWED; preparación/cancelación no consultan API ni escriben. Confirmación explícita usa CSRF, intención protegida, motivo normalizado, misma clave y ETag original; el resultado incierto sólo permite recuperar la misma operación. Escape/Cancelar restauran foco y funcionan mediante POST sin JavaScript. No se ejecuta recuperación operativa, reparación ni despliegue desde la interfaz.

## Archivos del cambio

- Adenda 59, plan aprobado y cinco extensiones consumidoras en `docs/design`; informe, manifiesto de capturas y `IMPLEMENTATION_STATUS.md`.
- `src/Sgol.Web/Pages/Indicators`, `Pages/Audit`, `Pages/Continuity`; helpers acotados en `Interface/Reporting`, `Interface/Auditing`, `Interface/Continuity`.
- Cliente API: metadatos de indicadores/auditoría, Location de creación y validación de protocolo. Proyección de permisos existentes en `Roles.cs`; navegación, shell y destinos internos protegidos. Mejora progresiva en `continuity.js` y deshabilitado inmediato en `my-work.js`.
- Pruebas FRONT-020 unitarias, arquitectura, navegador y fixture PostgreSQL; parcial de continuidad para consulta/rollback/replay. Inventarios FRONT-002/005/006 actualizados. `.gitignore` conserva capturas y logs sintéticos locales fuera de Git.

Sin nuevas dependencias, restore, migraciones, cambios de fórmulas, contratos backend, Worker/Operations, global.json, originales congelados ni archivos dentro de Fuentes.

## Criterios y evidencia

| Criterio | Comprobación enfocada |
|---|---|
| CA/CP-029 | Prueba persistente HU029 de las cinco fórmulas, decisiones vigentes/sustituidas y no-efecto; UI por cuatro puestos, denominadores, período explícito, ceros y rechazo de filtros. |
| CA/CP-032 | Prueba HU032 con todos los roles y obligaciones sin asignación, panorama sólo Dirección; acceso denegado de otros puestos y consultas puras. |
| CA/CP-033 | Suite AuditQueryPersistenceTests: jerarquía histórica, cuatro etapas, minimización, cerca/cursor, cadena oculta íntegra, permisos obsoletos, detalle y protección de auditoría. Navegador: consulta general/traza incompleta/detalle/regreso, sin cambios en filas. |
| CA/CP-035 | Consulta añade VIEWED; fallo de su inserción no entrega reporte. Solicitud/aprobación/replay y rollback ante auditoría fallida conservan historia/idempotencia/outbox. Navegador: solicitud, etapas, MATCHED/DIFFERENT/FAILED/truncado/APPROVED, cancelación, reenvío y ETag obsoleto 412 sin efecto. |
| Accesibilidad/diseño | Tokens/componentes v2, teclado, Cancelar inicial/Escape/retorno, contraste, área de controles, reflow 320 y texto 200%, movimiento reducido, escritorio Chromium/móvil WebKit HTTPS y alternativa sin JS. Capturas de aplicación integrada sintética. |

El límite contractual de 100000 diferencias se verifica como colección completa en presentación, sin cursor o truncado añadido. No se acredita una medición de rendimiento extremo del navegador con 100000 filas; se conserva como validación diferida específica por costo de ese volumen, sin ocultar ni paginar artificialmente el reporte.

## Validación local

SDK aislado `C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe`, compilación y pruebas secuenciales. Sin suites globales ni gates remotos. TRX y logs locales en TestResults y `.artifacts`, fuera de Git.

| Comando ejecutado | Resultado |
|---|---|
| `dotnet build --no-restore --configuration Release` | PASS; 0 errores/advertencias. |
| UnitTests `--filter 'FullyQualifiedName~Front020\|FullyQualifiedName~IndicatorApiEndpoint\|FullyQualifiedName~DirectionOverviewApiEndpoint\|FullyQualifiedName~AuditApiEndpoint\|FullyQualifiedName~ContinuityApiEndpoint\|FullyQualifiedName~SgolApiClient\|FullyQualifiedName~Navigation\|FullyQualifiedName~RoleAdministration\|FullyQualifiedName~TechFront003'` | 209/209 PASS, sin omisiones; incluye destinos protegidos, cookies/CSRF y proyección por puesto. |
| ArchitectureTests `--filter 'FullyQualifiedName~Front020\|FullyQualifiedName~InterfaceDesignRules\|FullyQualifiedName~IndicatorArchitecture\|FullyQualifiedName~AuditArchitecture\|FullyQualifiedName~ContinuityArchitecture'` | 20/20 PASS, sin omisiones. |
| IntegrationTests `--filter 'FullyQualifiedName~ContinuityPersistenceTests\|FullyQualifiedName~AuditQueryPersistenceTests\|FullyQualifiedName~Hu029Reconciles\|FullyQualifiedName~Hu032Reconciles'` | 17/17 PASS, PostgreSQL real, sin omisiones. |
| FrontendBrowserTests `--filter 'FullyQualifiedName~Front020'` | Ejecución final 2/2 PASS, escritorio Chromium/móvil WebKit, sin omisiones; 50 capturas. |
| FrontendBrowserTests inventarios `Front002\|Front005\|Front006` en ejecución combinada con FRONT-020 | 3/3 PASS. La ejecución combinada completa tuvo 2 fallos de la prueba de carga FRONT-020; su corrección se acreditó en la ejecución final 2/2, sin omitir ni relajar la aserción. |
| `git diff --check` y `git diff --cached --check` | PASS, incluidos archivos nuevos al preparar el commit. |

Todos los test usan `--no-build --configuration Release`. Compilación final repetida sólo por añadir cobertura directa de permisos/destinos detectada en la revisión, sin cambios productivos posteriores al navegador final. Persistencia no repetida tras cambios exclusivamente de presentación/pruebas. Formato global y gates integrales reservados para publicación/integración autorizada; no constan como PASS. Originales F00–F07 y Fuentes sin diferencias en Git.

Incidencias corregidas durante implementación: conflictos de sintaxis Razor y compilación, UUID de correlación del fixture (debe ser v7), aserción sobre acento HTML codificado y carrera de navegador al observar una navegación retenida. Se mantiene la aserción de carga; se observa el evento de envío antes de navegar mediante prevención controlada de un único submit, sin pausas arbitrarias ni atribuir esta captura a latencia real.

## Preparación de publicación autorizada

Tras la aprobación visual/commit/PR/publicación, el preflight confirma árbol limpio, rama codex/front-020, Fuentes protegidas y sesión gh autenticada. El SDK de PATH sigue sin satisfacer global.json; se conserva el SDK aislado 10.0.400, sin restore ni cambio de global.json. Master remoto conserva la base integrada aceptada; no hay otro PR de la rama.

Formato global inicial `dotnet format --verify-no-changes --no-restore` detectó WHITESPACE/ENDOFLINE en archivos del hito. `dotnet format whitespace --no-restore --include <archivos .cs de FRONT-020>` corrigió únicamente espacios y saltos; segunda verificación global PASS (exit 0). Compilación Release --no-restore PASS, 0 errores/advertencias. UnitTests completos --no-build --configuration Release 1022/1022 PASS; ArchitectureTests completos 69/69 PASS, sin omisiones. Git diff y staged --check PASS. Las capturas aprobadas y los contratos conservan su comportamiento; los gates remotos del PR deben validar la cabeza exacta publicada, sin atribuirle resultados de otra cabeza.

## Límites y revisión vigentes

Zoom nativo, lector de pantalla y dispositivos físicos conservan Validación diferida por falta de ensayo real; emulación/reflow no los reemplazan. Sigue diferido WebKit Windows PUT/S3 HTTP local por ausencia de respuesta en el recorrido anterior, ajeno a estas consultas. Aislamiento productivo completo SeaweedFS no acreditado por entorno efímero; optional isolated network histórico SKIPPED permanece SKIPPED. La carrera específica sustitución de evidencia contra emisión no reensayada en FRONT-019 conserva ausencia de nuevo PASS. No se repite ni acredita el simulacro integral backend por mostrar reportes sintéticos.

Revisión visual expresa del manifiesto `FRONT_020_CAPTURAS.md` aprobada por el responsable, junto con commit, PR y publicación. Autoriza push y un único PR, checks, seguimiento y correcciones proporcionales del hito; merge/despliegue requieren sus autorizaciones. Cabeza y pipeline vigentes resolubles en el PR; no añadir commits administrativos para escribir sus números o hashes. Mantener seguimiento en este mismo chat; configurar/verificar heartbeat si el turno termina antes del resultado, quieto mientras no haya cambio accionable. Pausarlo al solicitar merge de la cabeza exacta validada.
