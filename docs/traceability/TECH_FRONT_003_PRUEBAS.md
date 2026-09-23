# TECH-FRONT-003 — registro de pruebas locales

## Alcance

Infraestructura de sesión y CSRF de Razor, puente de cookies al cliente común y shell condicionado. No hay páginas `NAV-*` protegidas implementadas todavía; el registro de rutas de producción permanece vacío. Las rutas sintéticas de prueba sólo verifican el filtro y el destino seguro.

| Criterio | Evidencia enfocada |
|---|---|
| Consulta request-scoped, cuatro roles, sin caché entre requests, preauth sin identidad, expiración | `TechFront003Tests.SessionIsQueriedOnceWithinRequestAndAgainForNextRequest`, `PreauthOrInvalidSessionNeverBuildsShellIdentity` |
| Sesión hospedada válida, vencida, alterada e invalidación persistente | `HostedAuthenticationKestrelSmokeTests.RealKestrelHttpsCompletesHostedAuthenticationContract`: Kestrel HTTPS y PostgreSQL real; `GET /api/v1/auth/session`, cuatro roles, rama y negocio; ticket vencido emitido con el mismo ámbito Data Protection, cookie alterada, y sello, rol, empleo y estado alterados en base de datos |
| Cookie y `Set-Cookie` cerrados por nombre, método, ruta y atributos | `TechFront003Tests.CookieBridgeForwardsOnlyAllowedCookiesAndRequiresCsrfPair`, `SetCookieIsValidatedBeforePropagation`, `SetCookieRouteAndBatchMustBeAllowedBeforeAnyCookieIsApplied` |
| CSRF Razor → API, par ausente, mezclado o inválido y no efecto | `HostedAuthenticationTests.RazorValidatedCsrfPairReachesApiAndMixedPairHasNoEffect`, `TechFront003Tests.RazorCsrfPairMustValidateBeforeApiMutation`; la prueba hospedada constata que un login sin CSRF no llama al servicio |
| Cambio de principal, 401, logout y limpieza local ante fallo remoto | `TechFront003Tests.AuthenticationTransitionsInvalidateSnapshotAndCsrf`, `LogoutFailureClearsBrowserCookiesAndSnapshot`; `HostedAuthenticationKestrelSmokeTests` valida 401 real y limpieza de identidad Razor |
| Destino local, protegido, registrado y todavía autorizado | `TechFront003Tests.ReturnDestinationRejectsExternalApiMutationAndTampering` |
| Navegación por `roleCode`, permisos y páginas implementadas; teclado y foco | `TechFront003Tests.NavigationRequiresCanonicalRolePermissionAndImplementedRazorRoute`, `InterfaceDesignRulesTests`; el diálogo nativo usa `showModal`, `Escape` y devuelve foco al disparador mediante `components.js` |
| 401, 403, 404 y Problem Details seguros | `SgolApiClientTests.ContractedErrorStatuses_RemainErrorsWithSafePresentation`, `UnauthorizedForbiddenAndHiddenResource_HaveDistinctSafeTitles`, `BranchPageTests.UnsupportedBranch_ConvergesOnTheSameNotFoundAsMissingCanonicalSeed` |
| Tipos de contenido de autenticación y no efecto | `HostedAuthenticationTests` y `HostedAuthenticationKestrelSmokeTests`; causa y corrección en `TECH_FRONT_003_BLOQUEO_401.md` |

## Límite de esta tarea

El shell muestra «Aún no hay secciones disponibles» para una sesión plena sin páginas `NAV-*` implementadas. La navegación real, el recorrido visual de login y el uso de `allowedActions` y `links` en pantallas de negocio pertenecen a historias posteriores y requieren sus propios contratos y pruebas. TECH-FRONT-003 sólo aporta filtros cerrados, sesión y CSRF para esas pantallas. No se ejecuta Playwright común ni la suite integral.
