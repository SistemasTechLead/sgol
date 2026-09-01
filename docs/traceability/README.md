# Trazabilidad de implementación

Este directorio registra el mapeo verificable entre identificadores aprobados, criterios y pruebas o comandos. Una fila sólo declara la evidencia indicada; no implica cobertura funcional adicional.

El estado aceptado para el inicio incremental del siguiente chat se mantiene en [`IMPLEMENTATION_STATUS.md`](IMPLEMENTATION_STATUS.md). Ese archivo evita revalidar tareas cerradas cuando su commit permanece en la ascendencia del checkout, sin sustituir los gates de salida de la tarea actual. Su cierre en un único pull request se rige por `F07_ENMIENDA_001_CIERRE_DE_TAREA_EN_UN_PR.md`: el registro preparado en una rama sólo adquiere eficacia cuando él y el commit implementado están en `master` y se conservan los checks y la aprobación requeridos.

La plantilla reutilizable para registrar una ejecución sin datos sensibles está en [`TEST_EVIDENCE_TEMPLATE.md`](TEST_EVIDENCE_TEMPLATE.md).

| ID | Criterio | Prueba o evidencia definida |
|---|---|---|
| INIT-CA-001 | SDK .NET 10 fijado y restore bloqueado reproducible | `global.json`; `Directory.Packages.props`; `packages.lock.json`; `dotnet --version`; `dotnet restore --locked-mode` |
| INIT-CA-002 | Compilación Release sin errores ni advertencias nuevas | `dotnet build --no-restore --configuration Release` |
| INIT-CA-003 | Pruebas unitarias, arquitectura y smoke pasan | `dotnet test --no-build --configuration Release` |
| INIT-CA-004 | Host inicia y vida responde sin dependencias externas | `Sgol.UnitTests.HostSmokeTests.LiveEndpoint_ReturnsSuccessWithoutBusinessData` |
| INIT-CA-005 | `Sgol.BuildingBlocks` no depende de `Sgol.Web` | `Sgol.ArchitectureTests.BuildingBlocksDependencyTests.BuildingBlocks_DoesNotDependOnWeb` |
| INIT-CA-006 | Sin función, tabla, secreto ni dependencia no justificada | Revisión de diff y proyectos; `dotnet format --verify-no-changes` |
| INIT-CA-007 | `Fuentes/` permanece sin cambios y no es salida, salvo una rebaselización explícita y exacta | `scripts/ci/verify-fuentes-protection.ps1`; transición de un solo uso registrada en `scripts/ci/fuentes-approved-rebaseline.json` |
| INIT-CA-008 | Requisitos y operación local no secreta documentados | `README.md` |
| TECH-BASE-002 | PostgreSQL real local/CI aislado y primera conexión | `Sgol.IntegrationTests.PostgreSqlPersistenceTests.EmptyMigration_ConnectsToPostgreSql_WithoutFunctionalTables`; `docs/operations/postgresql-local.md` |
| TECH-BASE-002 | Migración vacía, versionada, repetible y sin tablas funcionales | `20260827000000_InitializePersistence`; la prueba aplica la migración dos veces y comprueba que sólo existe `__EFMigrationsHistory` |
| TECH-BASE-002 | Configuración de conexión fuera de Git | `Sgol.Web.Infrastructure.Persistence.PersistenceServiceCollectionExtensions`; `ConnectionStrings__Sgol` documentada sin valor |
| TECH-BASE-003 | Pipeline reproducible exclusivo de pull request | `.github/workflows/pull-request.yml`; SDK, acciones y Gitleaks fijados por versión e integridad |
| TECH-BASE-003 | Restore bloqueado, build Release con análisis estático, pruebas y formato | Pasos nombrados con los cuatro comandos obligatorios; `Directory.Build.props` trata advertencias de analizadores como errores |
| TECH-BASE-003 | Integración aislada con PostgreSQL real | La suite ejecuta `Sgol.IntegrationTests.PostgreSqlPersistenceTests.EmptyMigration_ConnectsToPostgreSql_WithoutFunctionalTables` mediante Testcontainers |
| TECH-BASE-003 | Dependencias directas/transitivas y secretos | `scripts/ci/Assert-NoVulnerablePackages.ps1`; Gitleaks 8.30.1 verificado por SHA-256 y ejecutado con redacción |
| TECH-BASE-003 | Protección de `Fuentes/`, permisos mínimos y ausencia de despliegue | Diff base/cabeza del PR; `contents: read`; checkout sin credenciales persistentes; `docs/operations/pull-request-pipeline.md` |
| TECH-BASE-004 / RT-003 | Dirección permitida y límites de `Domain`, hosts, módulos y `Sgol.BuildingBlocks` | `Sgol.ArchitectureTests.ArchitectureBoundaryTests.Repository_RespectsApprovedDependencyDirection`; reglas `ARCH-001` a `ARCH-004` |
| TECH-BASE-004 / RT-003 | Una dependencia prohibida produce un diagnóstico identificable | `Sgol.ArchitectureTests.ArchitectureBoundaryTests.SyntheticForbiddenDependencies_AreDetectedWithRuleDiagnostics` |
| TECH-BASE-004 | Plantilla HU–CAP–CA/CP/CAT/NFR–prueba completa y sin datos sensibles | `docs/traceability/TEST_EVIDENCE_TEMPLATE.md`; `Sgol.ArchitectureTests.TraceabilityTemplateTests.EvidenceTemplate_ContainsRequiredTraceabilityAndSafetyFields` |
| TECH-BASE-004 | Ejecución automática en todo PR | El proyecto `Sgol.ArchitectureTests` pertenece a `SGOL.slnx` y se ejecuta en el paso `dotnet test --no-build --configuration Release` del pipeline PR |
| TECH-BASE-005 | Reloj UTC y UUID v7 son puertos inyectables y deterministas en prueba | `Sgol.UnitTests.PrimitiveTests.ClockPort_AllowsDeterministicTime`; `Sgol.UnitTests.PrimitiveTests.UuidGenerator_UsesInjectedClockAndCreatesVersion7Uuid` |
| TECH-BASE-005 | `correlationId` UUID v7 se propaga o se reemplaza de forma segura | `Sgol.UnitTests.HttpPrimitiveTests.ValidUuid7CorrelationId_IsPropagatedInHeaderAndBody`; `Sgol.UnitTests.HttpPrimitiveTests.InvalidCorrelationId_IsReplacedByUuid7` |
| TECH-BASE-005 | Errores usan Problem Details correlacionado sin exponer secretos | `Sgol.UnitTests.HttpPrimitiveTests.MissingRoute_ReturnsProblemDetailsWithCorrelationId`; `Sgol.UnitTests.HttpPrimitiveTests.UnhandledError_DoesNotExposeSecretInProblemDetailsOrLogs` |
| TECH-BASE-005 / NFR-010 | El reloj compartido expone instantes UTC | `Sgol.BuildingBlocks.Time`; `Sgol.UnitTests.PrimitiveTests.ClockPort_AllowsDeterministicTime` |
| TECH-BASE-005 | Logs JSON minimizados y contrato operativo documentado | `docs/operations/http-primitives.md`; revisión de `Sgol.Web.Infrastructure.Http` |
| TECH-ID-BOOT-001 / F07-JP-001 | Primera persona, cuenta activa y rol `DIRECCION` se crean con auditoría en una transacción | `DirectionBootstrapTests.InitialExecution_CreatesActiveDirectionIdentityAndAuditInOneTransaction` |
| TECH-ID-BOOT-001 / F07-JP-001 | Segunda ejecución rechazada permanentemente por integridad PostgreSQL y sin efectos | `DirectionBootstrapTests.SecondExecution_IsPermanentlyRejectedWithoutAdditionalEffects`; PK/check de `direction_bootstrap` |
| TECH-ID-BOOT-001 / F07-JP-001 | Fallo en persona, cuenta, rol o auditoría revierte toda la creación | `DirectionBootstrapTests.FailureInAnyCreationOrAuditStep_RollsBackEverything` |
| TECH-ID-BOOT-001 / F07-JP-001 | Primer acceso conserva cambio de contraseña y enrolamiento TOTP pendientes | `must_change_password=true`; `mfa_enrolled_at IS NULL`; prueba positiva de bootstrap |
| TECH-ID-BOOT-001 / F07-JP-001 | Secreto efímero ausente de texto persistido, auditoría y errores | `DirectionBootstrapTests.Secret_IsAbsentFromErrorsAuditAndPersistedPlainText`; `DirectionBootstrapInput.ToString`; salida genérica de `Sgol.Admin` |
| TECH-ID-BOOT-001 / F07-JP-001 | Sin endpoint público, cuenta predeterminada ni datos funcionales precargados | `HostSmokeTests.PublicBootstrapEndpoint_DoesNotExist`; `DirectionBootstrapTests.Migration_DoesNotPreloadAnAccountRoleOrFunctionalData` |

Los resultados de cada ejecución se reportan en la entrega de la tarea; este archivo conserva el formato y la relación estable, no un estado transitorio de ejecución.
