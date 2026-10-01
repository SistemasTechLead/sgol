# FRONT-018 — Versiones, revisión, sustitución y conclusión

2026-10-01. Plan único aprobado íntegramente mediante «Si apruebo integramente el plan», incorporado por Adenda 57. Implementación local; revisión visual aprobada por el responsable el 2026-10-01 mediante «Apruebo la implementación visual», sobre la cabeza local 38e2ce3. No existe autorización de publicación, PR, checks remotos, merge ni despliegue.

## Resultado y contrato

El detalle de Mi trabajo conserva su contexto e historia y añade UI-E06/E07/E08. La consulta de versiones muestra historial paginado, estado, aportante identificado por el UUID autorizado, fecha, motivo, predecesora y metadatos de archivo o campos estructurados cerrados. Los cursores de historia y evidencia se protegen y conservan sus filtros por separado. No hay descarga ni preview: BR-API04 queda resuelta únicamente para esta consumidora por exclusión aprobada, sin cierre global supuesto.

La revisión se solicita explícitamente. Navegar al detalle o filtrar versiones no crea snapshots. El GET contractual de revisión conserva la excepción aprobada de Adenda 19: primera huella crea exactamente snapshot inmutable y auditoría en la misma transacción; la huella repetida no agrega filas. Ninguna revisión cambia ejecución, evidencia, asignación, validación, idempotencia ni outbox. Los faltantes y las condiciones sin resolver se presentan conforme a la respuesta; no se recalculan reglas en Razor o JavaScript.

Sustituir conserva la versión anterior y utiliza la autoridad proyectada en servidor: responsable vigente en pendiente, superior estricto después de concluir, con motivo obligatorio en ese último caso. Editor y confirmación mantienen requisito, ítem, versión original y motivo. La recuperación utiliza el mismo cuerpo, clave e If-Match del ítem; un 412 exige recarga explícita. No modifica una conclusión ni una validación anterior.

Concluir requiere responsable vigente, permiso existente PER-TAREA-EJECUTAR, tarea pendiente y revisión completa para habilitar el control. El comando vuelve a comprobar la evidencia en servidor y envía cero bytes de cuerpo, CSRF, clave original e If-Match de obligación. Concluida sigue siendo distinta de validada. Después del cierre no se habilitan nuevas primeras aportaciones, reapertura ni conclusión ajena por jerarquía.

La sustitución binaria reutiliza la carga segura de FRONT-017: máximo 15 MiB, tipo real, firma, SHA-256, cuarentena y ClamAV. Sólo LIMPIO puede vincularse. El PUT firmado queda en el transporte transitorio, sin cookies de SGOL, registro ni presentación de URLs. Datos, archivos y capturas son sintéticos.

Criterios cubiertos: CA-025, CP-025-P y CP-025-N para versiones, conservación de historia, sustitución y autoridad; CA-026, CP-026-P y CP-026-N para revisión, faltantes y snapshot; CA-022, CP-022-P y CP-022-N para conclusión propia completa y rechazo sin efectos. CAT-001..008 se consumen mediante las políticas y payloads capturados de las ocho TAR; no se adelantan capacidades de validación posterior.

BR-D04/M08 consumen exclusivamente la composición, estados y catálogo aprobados del plan y Adenda 57. No hay endpoints, permisos, dependencias, migraciones ni capacidades FRONT-019/020 nuevos. Los dos permisos agregados a la proyección de sesión ya existían en los contratos aprobados. El lector de detalle compone evaluadores puros de los módulos y proyecta evidenceActions sin escritura.

## Archivos y diseño

- Contratos EvidenceContributions.cs, ObligationConclusions.cs, ObligationQueries.cs y Roles.cs; EfObligationQueryReader.cs y reutilización del evaluador de autoridad en EfEvidenceContributionService.cs.
- ApiClientContracts.cs y SgolApiClient.cs: referencias cerradas de faltantes; EvidenceReviewPresentation.cs, EvidenceIntention.cs, EvidenceReplacementIntention.cs y ConclusionIntention.cs.
- Details.cshtml/Details.cshtml.cs, Details.Evidence.cs y nuevos Details.EvidenceReview.cs, Details.EvidenceReplacement.cs, Details.Conclusion.cs; _EvidenceVersions, _EvidenceReview, _EvidenceReplacement, _Conclusion y adaptación de _EvidenceContribution. components.css, my-work.js y evidence-contribution.js conservan los componentes y variables existentes.
- Pruebas unitarias, de consulta PostgreSQL, arquitectura e inventarios anteriores directamente afectados; harness y recorridos HTTPS de navegador.
- Adenda 57, plan aprobado, referencias consumidoras en docs/design, IMPLEMENTATION_STATUS y este informe/manifiesto de capturas.

Se conserva el diseño del PR #85: CSS propio, escala y variables oficiales, componentes compartidos, Poppins local OFL y Georgia de sistema. The Seasons excluida. Diálogos nativos con Cancelar inicialmente enfocado, Escape, Tab/Shift+Tab y retorno; confirmación equivalente sin JavaScript. Sólo la tabla desplaza horizontalmente; filtros, acciones y diálogos refluyen. Texto 200 % mediante harness, reflow de página a 320 px CSS, área mínima y contraste renderizado se verifican sin acreditar zoom nativo o WCAG integral.

El motivo de 500 caracteres sin espacios permanece dentro del diálogo en móvil; 501 caracteres se rechazan antes de preparar la carga, sin efecto de negocio. El texto del diálogo compartido admite corte de palabras y conserva la escala oficial. Las capturas móviles binarias incluyen ese caso límite sintético.

## Validación local

La única comprobación inicial del entorno fue scripts/ci/preflight.ps1. Se respetó su salida: SDK 10.0.401 del PATH no coincide con global.json; compilación y pruebas usan el SDK aislado existente 10.0.400. No se modificó global.json ni se ejecutó restore. Git y Fuentes estaban limpios al inicio; se conserva 96dfe4d sobre el merge aceptado de FRONT-017, sin repetir sus gates.

Comandos ejecutados con `C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe`:

Los tests se ejecutaron mediante `dotnet test <proyecto> --no-build --configuration Release --filter '<filtro>'`. Proyectos: tests/Sgol.UnitTests, tests/Sgol.ArchitectureTests, tests/Sgol.IntegrationTests y tests/Sgol.FrontendBrowserTests. Filtros finales:

```text
UnitTests: FullyQualifiedName~Front018|FullyQualifiedName~Front016PresentationTests|FullyQualifiedName~Front017PresentationTests|FullyQualifiedName~SgolApiClientTests|FullyQualifiedName~ObligationConclusionApiEndpointTests|FullyQualifiedName~EvidenceReviewEvaluatorTests|FullyQualifiedName~RoleAdministrationTests
ArchitectureTests: FullyQualifiedName~Front016|FullyQualifiedName~Front017|FullyQualifiedName~Front018|FullyQualifiedName~EvidenceInfrastructure|FullyQualifiedName~ObligationConclusion|FullyQualifiedName~InterfaceDesignRules
IntegrationTests: FullyQualifiedName~ObligationQueryPersistenceTests.Front018|FullyQualifiedName~ObligationQueryPersistenceTests.Front017|FullyQualifiedName~EvidencePolicyPersistenceTests.PostgreSqlEnforcesClosedCatalogAndObligationCapturesImmutableApplicablePolicy
FrontendBrowserTests (flujo): FullyQualifiedName~VersionsExplicitReviewReplacementAndConclusion
FrontendBrowserTests (binarios): FullyQualifiedName~SuperiorBinaryReplacement
```

La ejecución anterior de regresión utilizó `FullyQualifiedName~Front018BrowserTests|FullyQualifiedName~Front017BrowserTests`; los dos casos FRONT-017 pasaron y los nuevos casos se corrigieron y validaron mediante los filtros finales anteriores. La primera ejecución PostgreSQL seleccionó `FullyQualifiedName~ObligationQueryPersistenceTests.Front018|FullyQualifiedName~ObligationQueryPersistenceTests.Front017|FullyQualifiedName~ObligationConclusionPersistenceTests|FullyQualifiedName~EvidenceContributionPersistenceTests|FullyQualifiedName~EvidenceReviewPersistenceTests`: 14 PASS y un fallo en preparación de MFA de la prueba FRONT-018, corregido y comprobado en los cuatro casos finales. No se atribuyen casos a filtros sin coincidencias. Los 12 casos existentes de conclusión pasaron en esa primera ejecución; el código servidor de conclusión no se modificó.

Captura final: variable `SGOL_FRONT018_CAPTURE_DIR` en `.artifacts/front-018`; resultados TRX locales ignorados en tests/Sgol.FrontendBrowserTests/TestResults/front018-browser-flow.trx y front018-files.trx. La variable anterior `artifacts/front-018` se usó en los primeros recorridos; las capturas se trasladaron con rutas verificadas dentro del workspace y hashes renovados al finalizar.

| Comprobación | Resultado |
|---|---|
| build --no-restore --configuration Release | PASS; 0 errores y 0 advertencias en la compilación final |
| UnitTests: Front018, Front016PresentationTests, Front017PresentationTests, SgolApiClientTests, ObligationConclusionApiEndpointTests, EvidenceReviewEvaluatorTests y RoleAdministrationTests | 172/172 PASS |
| ArchitectureTests: Front016/017/018, EvidenceInfrastructure, ObligationConclusion e InterfaceDesignRules | 18/18 PASS |
| IntegrationTests: Front017/018 de ObligationQueryPersistenceTests y PostgreSqlEnforcesClosedCatalogAndObligationCapturesImmutableApplicablePolicy | 4/4 PASS; PostgreSQL real, autoridad y lectura sin efectos |
| ObligationConclusionPersistenceTests: suite existente enfocada, incluidos sus controles históricos | 12/12 PASS; conserva invariantes de cierre sin implementar interfaz de validación posterior |
| Front017BrowserTests directamente afectados por reutilizar su carga y detalle | 2/2 PASS; Chromium, PostgreSQL, S3 y ClamAV reales |
| Front018BrowserTests: sustitución binaria y recorrido de versiones/revisión/sustitución/conclusión | 4/4 PASS en las ejecuciones finales enfocadas: binarios 2/2 y flujo 2/2; cursor de más de 25 versiones, conflicto, auditoría, permisos, recuperación y confirmación sin JavaScript |
| git diff --check | PASS; verificación local final al cerrar |

Las pruebas de revisión/evaluación y los 18 formularios cerrados existentes cubren las políticas capturadas de las ocho TAR y sus criterios CAT aplicables. No se convierten los resultados posteriores de validación CUMPLIDA/NO_CUMPLIDA en lógica de esta interfaz. Los recorridos nuevos comprueban revisión repetida sin nuevas filas, preparación sin efectos, rollback de auditoría, intención alterada, concurrencia de sustitución, replay y restricciones posteriores a conclusión. El servidor conserva autorización, sesión, CSRF, auditoría e idempotencia transaccional.

Incidencias corregidas durante la verificación: referencia Request en un parcial Razor; analizadores de SQL/ArgumentException en el harness; supuesto erróneo de MFA en una cuenta ya enrolada; inventario histórico que prohibía el consumidor de revisión ahora aprobado; JsonDocument con raíz null en representación binaria; expectativa 200 en una creación API que devuelve 201; selección excesiva de diálogos del shell en la prueba sin JavaScript. Un intento de recompilar durante testhost produjo bloqueo temporal de su DLL; se corrigió la secuencia y se esperó su finalización. No se cambiaron gates ni contratos para obtener PASS.

## Límites y revisión

Validación diferida: zoom nativo, lector de pantalla y dispositivos físicos. WebKit Windows con PUT al S3 local HTTP conserva la causa documentada de ausencia de respuesta HTTP; no se relaja la firma ni se declara Safari validado. SeaweedFS local no acredita aislamiento productivo completo de credenciales/buckets o despliegue. Formato global, suites completas y gates remotos se reservan a una eventual autorización expresa de publicación.

Capturas y SHA-256 en [FRONT_018_CAPTURAS.md](FRONT_018_CAPTURAS.md). Son aplicación funcionando con datos sintéticos, no maquetas. El responsable aprobó la implementación visual el 2026-10-01 mediante «Apruebo la implementación visual», sobre la cabeza local 38e2ce3 y las 25 capturas sintéticas entregadas. Las validaciones diferidas conservan sus causas. Esta aprobación no autoriza publicación, checks remotos, merge ni despliegue.
