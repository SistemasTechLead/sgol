# Trazabilidad de implementación

Este directorio registra el mapeo verificable entre identificadores aprobados, criterios y pruebas o comandos. Una fila sólo declara la evidencia indicada; no implica cobertura funcional adicional.

| ID | Criterio | Prueba o evidencia definida |
|---|---|---|
| INIT-CA-001 | SDK .NET 10 fijado y restore bloqueado reproducible | `global.json`; `Directory.Packages.props`; `packages.lock.json`; `dotnet --version`; `dotnet restore --locked-mode` |
| INIT-CA-002 | Compilación Release sin errores ni advertencias nuevas | `dotnet build --no-restore --configuration Release` |
| INIT-CA-003 | Pruebas unitarias, arquitectura y smoke pasan | `dotnet test --no-build --configuration Release` |
| INIT-CA-004 | Host inicia y vida responde sin dependencias externas | `Sgol.UnitTests.HostSmokeTests.LiveEndpoint_ReturnsSuccessWithoutBusinessData` |
| INIT-CA-005 | `Sgol.BuildingBlocks` no depende de `Sgol.Web` | `Sgol.ArchitectureTests.BuildingBlocksDependencyTests.BuildingBlocks_DoesNotDependOnWeb` |
| INIT-CA-006 | Sin función, tabla, secreto ni dependencia no justificada | Revisión de diff y proyectos; `dotnet format --verify-no-changes` |
| INIT-CA-007 | `Fuentes/` permanece sin cambios y no es salida | `git diff -- Fuentes`; comparación de inventario SHA-256 antes/después |
| INIT-CA-008 | Requisitos y operación local no secreta documentados | `README.md` |
| TECH-BASE-002 | PostgreSQL real local/CI aislado y primera conexión | `Sgol.IntegrationTests.PostgreSqlPersistenceTests.EmptyMigration_ConnectsToPostgreSql_WithoutFunctionalTables`; `docs/operations/postgresql-local.md` |
| TECH-BASE-002 | Migración vacía, versionada, repetible y sin tablas funcionales | `20260827000000_InitializePersistence`; la prueba aplica la migración dos veces y comprueba que sólo existe `__EFMigrationsHistory` |
| TECH-BASE-002 | Configuración de conexión fuera de Git | `Sgol.Web.Infrastructure.Persistence.PersistenceServiceCollectionExtensions`; `ConnectionStrings__Sgol` documentada sin valor |
| TECH-BASE-003 | Pipeline reproducible exclusivo de pull request | `.github/workflows/pull-request.yml`; SDK, acciones y Gitleaks fijados por versión e integridad |
| TECH-BASE-003 | Restore bloqueado, build Release con análisis estático, pruebas y formato | Pasos nombrados con los cuatro comandos obligatorios; `Directory.Build.props` trata advertencias de analizadores como errores |
| TECH-BASE-003 | Integración aislada con PostgreSQL real | La suite ejecuta `Sgol.IntegrationTests.PostgreSqlPersistenceTests.EmptyMigration_ConnectsToPostgreSql_WithoutFunctionalTables` mediante Testcontainers |
| TECH-BASE-003 | Dependencias directas/transitivas y secretos | `scripts/ci/Assert-NoVulnerablePackages.ps1`; Gitleaks 8.30.1 verificado por SHA-256 y ejecutado con redacción |
| TECH-BASE-003 | Protección de `Fuentes/`, permisos mínimos y ausencia de despliegue | Diff base/cabeza del PR; `contents: read`; checkout sin credenciales persistentes; `docs/operations/pull-request-pipeline.md` |

Los resultados de cada ejecución se reportan en la entrega de la tarea; este archivo conserva el formato y la relación estable, no un estado transitorio de ejecución.
