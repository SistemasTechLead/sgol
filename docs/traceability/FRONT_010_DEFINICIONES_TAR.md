# FRONT-010 — UI-C05, definiciones TAR

## Alcance y decisiones

- Base: `origin/master` `f709695291c4ac347065801e9c7164c43396af7c`, rama aislada `codex/front-010`. Las cabezas y merges de FRONT-008/009 son ancestros verificados; PR #74/#75 y sus checks para los SHA exactos constan como merged/SUCCESS.
- Contrato: fila 78 de Adenda 45, HU-011/CAP-012, CA-011/CP-011-P/N, CAT-001..008, F06 API y Adendas 44/46. Sólo `TAR-0005`, `TAR-0007`, `TAR-0008`, `TAR-0011`, `TAR-0018`, `TAR-0026`, `TAR-0092`, `TAR-0093`.
- BR-D06/UI-C05: extensión mínima de `docs/design` aprobada por el responsable durante FRONT-010. Catálogo, detalle, historia y editor cerrado comparten `/configuracion`; el esquema real es `schemaVersion=1`, `taskPayload={}` para las ocho TAR. No hay editor JSON ni campos de payload.
- Brecha consumidora de borradores: el responsable aprobó incluir versiones `BORRADOR` en `history` de los GET existentes sólo para Dirección autorizada. Otros perfiles activos reciben sólo historia publicada. No se crea endpoint ni DTO.
- El GET de catálogo entrega `meta.count` para cumplir el contrato del cliente HTTP común. La proyección de sesión incorpora el permiso ya existente `PER-DEFINICION-ADMIN` únicamente para Dirección; la API y PostgreSQL siguen autorizando cada lectura y escritura.
- Crear versión exige release `BORRADOR`, CSRF e intención idempotente. Publicar y desactivar exigen CSRF, `Idempotency-Key`, `If-Match`, fecha UTC derivada de `America/Mexico_City` y motivo. El 412 bloquea la acción hasta recarga explícita, sin reintento automático. La API conserva historia, auditoría y obligaciones existentes.

## Mapa de criterios y pruebas

| Contrato | Evidencia enfocada |
|---|---|
| HU-011, CA-011, CP-011-P, CAT-001..008 | `TaskDefinitionTests.Catalog_ContainsExactlyTheEightCanonicalDefinitions`, `TaskDefinitionPersistenceTests.V1V2DeactivationAndReactivationPreserveHistoryAndAudit`, `Front010BrowserTests.DirectionManagesEightDefinitionsAndOtherProfilesCannotMutateFromDeepLink` |
| CP-011-N, TAR fuera del MVP, campo extraño y esquema cerrado | `TaskDefinitionTests.Catalog_RejectsOptionalExcludedUnknownAndMalformedCodes`, `PayloadV1_AcceptsOnlyAnEmptyObject`, `CreateEndpoint_RejectsAdditionalPolicyFieldsBeforeCallingService`, `TaskDefinitionPersistenceTests.DatabaseRejectsNinthOptionalAndExcludedCodes` |
| Autorización, alcance y borrador no filtrado | `TaskDefinitionPersistenceTests.PermissionScopeAndStateFailuresHaveNoFunctionalEffect`, `DraftIsRecoverableByDirectionButHiddenFromOtherActiveProfiles`, `TaskDefinitionTests.SessionProjectsDefinitionAdministrationOnlyForDirection`, pruebas de deep link y 403 real de `Front010BrowserTests` |
| Concurrencia, replay, auditoría y no efecto | `TaskDefinitionPersistenceTests.DuplicateDraftAndStaleEtagAreRejectedWithoutSecondVersion`, `V1V2DeactivationAndReactivationPreserveHistoryAndAudit`, `AuditFailureRollsBackDraftAndIdempotencyRecord`, escenario 412 de FRONT-010 |
| Cliente compartido y colección | `TaskDefinitionTests.ListEndpoint_UsesCollectionEnvelopeRequiredBySharedClient` |
| Obligaciones previas inmutables | `TaskDefinitionTests.ObligationCreatedFromV1KeepsItsSnapshotWhenV2ReplacesTheDefinition`, `GenerationRequestPersistenceTests.AcceptedRequestMaterializesPendingAuditedObligationWithApprovedSnapshot` (evidencia de servidor preexistente) |
| Teclado, foco, móvil, escritorio, ancho y capturas sanitizadas | `Front010BrowserTests` y categoría completa `Category=FRONT_BROWSER` |

## Validación local

Ejecutada en el worktree aislado con datos sintéticos. `dotnet restore --locked-mode` una vez. `dotnet build --no-restore --configuration Release` pasó con 0 errores y 0 advertencias. `dotnet test tests/Sgol.UnitTests/Sgol.UnitTests.csproj --no-restore --configuration Release --filter FullyQualifiedName~TaskDefinitionTests` pasó 15/15. `dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --no-build --configuration Release --filter FullyQualifiedName~TaskDefinitionPersistenceTests` pasó 7/7 con PostgreSQL real; dos pruebas enfocadas de materialización y replay/no efecto de `GenerationRequestPersistenceTests` pasaron 2/2. `dotnet test tests/Sgol.ArchitectureTests/Sgol.ArchitectureTests.csproj --no-build --configuration Release --filter FullyQualifiedName~Hu034IdempotencyArchitectureTests` pasó 3/3; el inventario permanece en 19 consumidores. `Front010BrowserTests` pasó 1/1 en escritorio y móvil con Kestrel HTTPS/PostgreSQL/Chromium/WebKit, incluida la respuesta 403 del servidor para un perfil sin permiso. La pasada final de `dotnet test tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj --no-build --configuration Release --filter Category=FRONT_BROWSER` pasó 13/13 en la colección serializada tras añadir ese caso. `dotnet format whitespace SGOL.slnx --verify-no-changes --no-restore --include <archivos C# afectados>` pasó tras corregir cuatro observaciones de formato en la prueba nueva. Capturas sintéticas con todo el encabezado de sesión enmascarado: `C:\Users\josej\Dev\front-010-evidence\`, fuera del checkout.

`git diff --check` pasó. La suite integral no se ejecutó localmente por alcance del hito. La rama se publicó en PR `#76` tras la primera autorización; el primer commit `fad575fb9ef40e30053958ac18c75d72a8612bc9` aprobó `TECH-BASE-003 / PR gates` en run `36193225094`. El check del SHA documental final se verifica externamente antes de solicitar merge. El merge requiere una segunda autorización específica.
