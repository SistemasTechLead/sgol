# SGOL — Adenda 43 a F07: demo automatizada y cierre de `CV-05` (`TECH-E2E-CV-05`)

## 1. Control, estado y decisión

| Campo | Valor |
|---|---|
| Estado | `APROBADA ÍNTEGRAMENTE POR EL RESPONSABLE EL 2026-09-22; IMPLEMENTACIÓN PENDIENTE` |
| Fecha de preparación | 2026-09-22 |
| Base verificada | `origin/master = 00da83ca26f67e523d0d6067af737f38315e62b0` |
| Corte | `CV-05 — Control y aceptación` |
| Tarea que se propone insertar | `TECH-E2E-CV-05 — Demo automatizada y cierre del corte CV-05` |
| Historias | `HU-029`, `HU-032`, `HU-033`, `HU-034`, `HU-035` |
| Criterios | `CA-029`, `CA-032` a `CA-035` y `CP-029-P/N`, `CP-032-P/N` a `CP-035-P/N` |

El responsable aprobó íntegra y explícitamente esta adenda, incluida la excepción condicional de evidencia de la sección 12, el 2026-09-22. Desde esa decisión, `TECH-E2E-CV-05` es contrato eficaz; su implementación, publicación e integración conservan las condiciones de esta adenda. No modifica F00–F07 congelados ni `Fuentes/`.

## 2. Hechos de precedencia y eficacia

El remoto `refs/heads/master` y la referencia local `origin/master` resuelven al SHA de la tabla. Los PR efectivos `#50` (`HU-029`), `#51` (`HU-032`), `#52` (`HU-033`), `#53` (`HU-034`), `#54` (`TECH-OPS-001`), `#57` (`HU-035`), `#56` (`TECH-AUTH-001`) y `#58` (`TECH-E2E-CV-04`) están `MERGED` hacia `master`; sus cabezas y merges son ancestros de esa base y sus checks `TECH-BASE-003 / PR gates` terminaron `SUCCESS` sobre la cabeza exacta. Para `#58`: cabeza `945c966edbc84552bab5abd91a9866e128f18ead`, run `35772495865`, merge `00da83ca26f67e523d0d6067af737f38315e62b0`. `#55` no fue el merge efectivo de `HU-035`.

En `origin/master` no existe `F07_ADENDA_43_*` ni contrato aprobado `TECH-E2E-CV-05`; tampoco existe rama local/remota ni PR de cierre CV-05. El texto de `docs/traceability/IMPLEMENTATION_STATUS.md` para CV-04 fue escrito antes del merge y se corregirá en el hito con la evidencia anterior.

Al aprobarse, la adenda inserta `TECH-E2E-CV-05` **después** de las ocho dependencias verificadas y **antes** del cierre formal de `CV-05`. Aprobación documental no significa tarea implementada. `Implementada localmente` exige código, matriz y gates locales; `Publicada` exige PR y check exacto; `Integrada/Terminada` y `CV-05 Cerrado` exigen autorización posterior de merge, merge commit sin squash y ascendencia en `origin/master`. Ningún hito administrativo de otro corte se sustituye por esta demo.

## 3. Fuentes y fronteras

Gobiernan `F07_BACKLOG_DE_IMPLEMENTACION.md` fila `CV-05`; F05 filas `HU-029`, `HU-032` a `HU-035`, sus CA/CP; `RN-002`, `RN-004`, `RN-010`, `RN-025` a `RN-030`; `ADR-004`, `ADR-012`; `NFR-001`, `NFR-002`, `NFR-004`, `NFR-010`; Adendas 27 a 33 y 41; y el código integrado que implementa sus contratos. La Adenda 42 sirve sólo de precedente de aislamiento, comando, autenticación hospedada, diagnóstico y cleanup. No aporta matriz, semilla ni resultados a CV-05.

Si la demo descubre que el código integrado contradice un contrato, se documenta la brecha y se detiene esa parte. Esta adenda no autoriza cambiar DTO, ruta, permiso, estado, regla, migración, persistencia productiva o comportamiento de esas historias.

## 4. Entregable y comando

Se propone `tests/Sgol.Cv05Demo/`, proyecto no productivo, aislado y agregado a `SGOL.slnx`. Sólo referencia `Sgol.Web` y contratos transitivos integrados; ningún proyecto `src/` lo referencia. No referencia ni extiende `Sgol.Cv02Demo`, `Sgol.Cv03Demo` o `Sgol.Cv04Demo`. Reutiliza paquetes ya fijados, sin dependencia nueva.

Comando único: `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo\run-cv05.ps1 -Mode Automated`. Sólo admite `Automated`; no acepta URL, conexión, actor, fecha, recurso, secreto, puerto, timeout, reintento, destino, modo interactivo ni conservación de datos. Es finito, ejecuta dos ciclos completos en infraestructura e identidades nuevas y compara una huella funcional de constantes públicas, estados y conteos. Los secretos efímeros quedan fuera de esa huella.

`PREFLIGHT` verifica antes de pruebas costosas: arquitectura AMD64, .NET 10, Docker, ejecutable Release, última migración integrada, catálogo cerrado, ausencia de configuración externa que redirija conexión/Kestrel/auth, directorio de salida ignorado por Git y referencias permitidas. Falla cerrado con código allowlist; no eleva límites ni reintenta para tapar errores. La ausencia de una dependencia real o de un contrato requerido bloquea el gate.

## 5. Infraestructura y autenticación reales

Cada ciclo levanta PostgreSQL real desde base vacía con todas las migraciones, red y contenedor propios, puerto aleatorio sólo loopback y sin volumen persistente; Kestrel productivo escucha sólo HTTPS en loopback con certificado temporal cuya huella exacta valida el cliente. Para `HU-035` levanta además los dos almacenes S3 compatibles, buckets separados, antimalware y los procesos `Worker`/`Operations` e imagen OCI ya aceptados por `TECH-OPS-001`/`HU-035`, en red aislada y con restore PostgreSQL **nuevo y vacío**. Se comprueba que el destino nunca es el origen. No se usa SQLite, `TestServer` como transporte de aceptación, cloud, datos reales ni proveedor de pago.

Dirección, Administración, Subcoordinación y Piso usan personas, empleos, roles, permisos y cuentas sintéticas **persistidas** en `LOR-001`. Dirección pasa por bootstrap real; las demás por administración real aceptada. Cada actor obtiene CSRF, login, cambio de contraseña cuando proceda, enrolamiento/verificación TOTP, cookie plena y sesión por las rutas HTTPS de la Adenda 41. Cada cliente tiene su propia cookie. No se fabrica `ClaimsPrincipal`, claim, desafío MFA, ticket o cookie. Contraseñas, TOTP, recovery codes, CSRF, conexiones, claves S3, certificado y material de cifrado se generan en runtime, permanecen privados y se eliminan al finalizar. Se comprueban negativas de cookie, MFA, CSRF y sesión invalidada sin producir efectos; el bloqueo compartido de loopback se verifica por estado PostgreSQL o fixture aislado, sin solicitudes adicionales que consuman el mismo rate limit.

## 6. Semilla y regla de no fabricación

`CV05-SEED-V1` fija semana ISO, `LOR-001`, las ocho TAR MVP, identidades públicas sintéticas, obligaciones, asignaciones, políticas y hechos de prueba necesarios para ambos universos, incluidos una obligación no asignada, una vencida sin `NO_CUMPLIDA`, una decisión sustituida y una vigente. Las fechas son UTC y la semana operativa usa `America/Mexico_City`. La segunda corrida reproduce la huella mediante infraestructura nueva.

El fixture sólo crea precondiciones basales cuando no existe API aceptada; usa endpoints y servicios productivos aceptados para crear y consultar hechos demostrados. Queda prohibido insertar directamente el indicador leído, la vista de Dirección, el evento de auditoría o su cadena, la decisión/conflicto idempotente, el resultado de reconciliación, las diferencias, el estado `MATCHED`/`DIFFERENT`/`FAILED`, la aprobación o una recuperación supuestamente exitosa. Los negativos de recuperación pueden alterar **sólo** una copia de restore u objeto sintético descartable antes de invocar el comparador productivo; nunca fabricar el resultado ni tocar el origen. Todo cambio basal y su exclusión de la huella se enumera en el runbook.

## 7. Matriz cerrada

Cada ID es estable en pruebas y reportes. Los negativos validan respuesta o estado contractual, historia, auditoría atribuible cuando el contrato la exige y ausencia de éxito falso. Los GET de `HU-029`, `HU-032` y `HU-033` no alteran negocio; la consulta de `HU-035` sí registra `RECOVERY_RECONCILIATION_VIEWED` conforme a su contrato.

| ID | Escenario | Contrato y resultado exigido |
|---|---|---|
| `S01` | `OperationalIndicatorsReconcileAuthorizedScope` | `HU-029`, `CP-029-P`: `GET /api/v1/indicators` concilia `pending`, `concluded`, `validated`, `nonCompliant`, `activeLoadByPerson`, período, alcance y denominadores; `pending+concluded=baseObligationsCount`. |
| `S02` | `OverdueAndSupersededNeverInflateCounts` | `HU-029`, `CP-029-N`: vencida sin decisión vigente `NO_CUMPLIDA` no cuenta como incumplida; decisión sustituida no duplica validada/incumplida; no aparece monto. |
| `S03` | `IndicatorHierarchyCursorAndReadOnlySnapshot` | `HU-029`: cuatro roles ven exactamente su alcance aprobado; filtro y cursor no expanden alcance; página y totales salen de snapshot read-only único. |
| `S04` | `DirectionSeesEntireBranchAndUnassigned` | `HU-032`, `CP-032-P`: `GET /api/v1/direction/overview` concilia los mismos cinco indicadores en toda `LOR-001`, cuatro niveles y obligaciones no asignadas sin fila ficticia de carga. |
| `S05` | `DirectionRoleAndFiveIndicatorBoundary` | `HU-032`, `CP-032-N`: otro rol, permiso aislado o puesto textual no conceden vista; no hay incentivo, nómina, integración ni sexto indicador. |
| `S06` | `AuditTraceReconstructsPersistedChain` | `HU-033`, `CP-033-P`: `GET /api/v1/audit-events?traceObligationId=...` y detalle reconstruyen configuración→asignación→evidencia→validación desde vínculos persistidos, con actor/fecha/cambio minimizados y completitud explícita. |
| `S07` | `AuditScopeCursorMinimizationAndNoReadEffect` | `HU-033`: jerarquía vigente, anti-IDOR, cursor/snapshot y allowlist de `before/after`; lectura no crea evento ni cambia versión, ETag o recurso. |
| `S08` | `AuditDeletionIsRejectedAndAttributed` | `HU-033`, `CP-033-N`: Dirección autenticada recibe `405 AUDIT_NOT_DELETABLE`, un solo `AUDIT_EVENT_DELETE_ATTEMPTED`, sin borrado; anónimo recibe `401` sin esa auditoría funcional. |
| `S09` | `IdenticalMutationReplaysOriginalOutcome` | `HU-034`, `CP-034-P`: dos solicitudes idénticas con la misma `Idempotency-Key` por la ruta productiva de generación retornan ID/resultado original y una sola obligación, sin segundo efecto. |
| `S10` | `ChangedPayloadConflictsWithoutDuplicate` | `HU-034`, `CP-034-N`: misma clave/scope con contenido distinto retorna `409 IDEMPOTENCY_CONFLICT`, registro `RECHAZADA` y auditoría `IDEMPOTENCY_CONFLICT_REJECTED`, sin segundo recurso ni éxito falso. |
| `S11` | `UnauthorizedReplayAndStaleIfMatchDoNotReexecute` | `HU-034`: autorización precede al lookup; replay autorizado conserva status/payload/ETag/Location originales aun con guarda mutable posterior; rechazo no revela resultado ajeno. |
| `S12` | `ConcurrentIdempotentRequestsHaveOneWinner` | `HU-034` y Adenda 31: carreras de misma clave/solicitud y generación usan unicidad/transacción PostgreSQL; un ganador y replay estable, sin duplicado ni auditoría de éxito duplicada. |
| `S13` | `RecoveryReferenceRestoreAndMatch` | `HU-035`, `CP-035-P`: Dirección solicita; job/Operations hacen snapshot exportado, backup, réplica, restore aislado y comparador `SGOL-FUNCTIONAL-SNAPSHOT-1`; IDs, vínculos, versiones, conteos, evidencia y auditoría concilian, con RPO/RTO dentro de umbral y `MATCHED`. |
| `S14` | `DirectionApprovesMatchedOnly` | `HU-035`: aprobación con `Idempotency-Key` e `If-Match` produce `APPROVED` y auditoría; replay es estable y ETag obsoleto da `412 VERSION_CONFLICT` sin segunda aprobación. |
| `S15` | `MissingEvidenceProducesVisibleDifference` | `HU-035`, `CP-035-N`: objeto sintético faltante en restore/replica o evidencia relacional faltante termina `DIFFERENT` o `FAILED` conforme a comparabilidad; clase/código visible, no aprobable, sin reparar ni fabricar. |
| `S16` | `MissingOrAlteredAuditBlocksRecovery` | `HU-035`, `CP-035-N`: copia restaurada con auditoría faltante/alterada produce diferencia o fallo cerrado; origen y resultado previo permanecen intactos. |
| `S17` | `RecoveryManifestCorruptionFailsClosed` | `HU-035`: manifiesto/objeto corrupto o inaccesible termina `FAILED` con código aceptado; no se confunde con igualdad ni se aprueba. |
| `S18` | `RecoveryAuthorizationAuditAndNoEffect` | `HU-035`: sólo Dirección vigente con `PER-CONTINUIDAD-VER` solicita/consulta/aprueba; anti-IDOR; consulta inserta sólo auditoría de vista, sin mutar resultado ni negocio. |
| `S19` | `RecoveryConcurrencyAndStageReplayStayUnique` | `HU-035`: dos solicitudes/comandos equivalentes recuperan resultado; advisory lock y unicidad por etapa impiden segunda ejecución, overwrite o secuencia terminal duplicada. |
| `S20` | `HostedAuthenticationFailuresHaveNoBusinessEffect` | `TECH-AUTH-001`: cookie ausente/manipulada, CSRF ausente/inválido, MFA incompleto y sesión invalidada bloquean mutaciones de CV-05 antes del servicio. |
| `S21` | `SecondRunHasSameFunctionalFingerprint` | Dos ciclos nuevos reproducen catálogo, estados, conteos y hashes públicos sin depender de secretos efímeros. |
| `S22` | `CleanupLeavesNoOwnedResource` | Éxito y fallo controlado dejan cero procesos, conexiones, contenedores, redes, volúmenes, bases, buckets/objetos temporales, certificados y archivos privados de la corrida. |

La matriz no se amplía ni relaja durante implementación sin nueva decisión. Los escenarios de corrupción son copias aisladas y no sustituyen los caminos productivos positivos.

## 8. Diagnóstico, no efecto y concurrencia

Fases permitidas: `PREFLIGHT`, `POSTGRESQL`, `STORES`, `ANTIMALWARE`, `OCI`, `HTTPS`, `CSRF`, `LOGIN`, `PASSWORD_CHANGE`, `MFA`, `SEED`, `INDICATORS`, `DIRECTION`, `AUDIT`, `IDEMPOTENCY`, `BACKUP`, `REPLICA`, `RESTORE`, `RECONCILIATION`, `REPORT`, `CLEANUP`.

El reporte público sólo contiene fase, `S01..S22` o `NONE`, estado `PASSED|FAILED|NOT_APPLICABLE`, código allowlist `CV05_*`, exit entero, duración y conteos cerrados. Códigos mínimos: `CV05_PRECONDITION_FAILED`, `CV05_POSTGRESQL_FAILED`, `CV05_STORE_FAILED`, `CV05_HTTPS_FAILED`, `CV05_AUTH_FAILED`, `CV05_HTTP_CONTRACT_FAILED`, `CV05_DATABASE_CONTRACT_FAILED`, `CV05_SCENARIO_FAILED`, `CV05_REPORT_FAILED`, `CV05_CLEANUP_FAILED`, `CV05_UNEXPECTED_FAILURE`, `CV05_EXECUTION_PASSED`. Un código desconocido se reduce a `CV05_UNEXPECTED_FAILURE`. El detalle privado puede existir sólo en temporal local efímero y se destruye.

No se publican contraseñas, TOTP, recovery codes, cookies, CSRF, cadenas de conexión, SQL, payloads, cuerpos/respuestas sensibles, excepciones, mensajes originales, URI/keys de objetos, hashes privados, IDs de personas, certificados ni rutas privadas. Los resultados de procesos nativos capturan inmediatamente `$LASTEXITCODE`; se guardan por separado proceso, escenario, reporte y cleanup. Se comprueban invariantes con PostgreSQL real; una consulta de auditoría `HU-035` se evalúa con su auditoría de vista esperada, no como ausencia absoluta de escritura.

## 9. Cleanup y semántica de salida

En `finally` se cierran clientes y lectores, se detienen Web/Worker/Operations, se cierran conexiones, se eliminan temporales y secretos, contenedores/bases de origen y restore, dos almacenes, red, volúmenes y objetos pertenecientes al `run-id`; se verifica por identidad exacta que no quedan recursos propios. No hay borrado fuera del ámbito efímero propio ni de `Fuentes/`.

Un fallo primario conserva su código aunque cleanup falle y registra `CV05_CLEANUP_FAILED` secundario. Cleanup fallido bloquea siempre; no oculta la causa primaria. Sólo se emite `PASS` y exit explícito `0` después de los dos ciclos, todos los escenarios, reporte válido, sanitización y cleanup verificado. Cualquier otro camino devuelve exit distinto de cero. No se elevan timeouts, límites o reintentos para convertir un fallo en verde.

## 10. Reportes, archivos obligatorios y opcionales

El comando crea fuera de Git `.artifacts/cv05/latest/TECH-E2E-CV-05-report.json` y `.md`, con `schemaId=sgol.tech-e2e-cv05.report`, versión 1, commit, base, arquitectura, dependencias, fases, `S01..S22`, dos ciclos, huella pública, estados separados de ejecución/reporte/cleanup, y `browser=NO_APLICA`, `accessibility=NO_APLICA`. Son **obligatorios** para PASS local y para validación dentro del runner; se verifican existencia, schema, SHA, catálogo y sanitización antes de publicar cualquier evidencia. Un único fallo anterior sanitizado puede rotarse a `previous-failure`; el éxito anterior nunca se confunde con el actual.

Metadatos públicos OCI, resultado de escaneo, backup/réplica/restore/reconciliación y sus digest/counts son obligatorios **cuando su gate correspondiente se ejecutó y pasó**. Diagnósticos de fallo de red, stream o `HU-035` que sólo se generan ante una condición específica son **opcionales**: su ausencia en un run exitoso es válida. Pruebas puras cubren ambos casos, presente y ausente, antes del run integral; el publicador nunca exige un diagnóstico opcional como condición de éxito. Ausencia de un archivo obligatorio sí falla el gate.

## 11. Interfaz y límites

No hay UI aprobada para CV-05. Navegador y accesibilidad se informan `NO_APLICA`; no se lee `Fuentes/IdentidadMarca`, no se crea Razor, HTML, CSS, SPA ni Playwright. No se cambian endpoints productivos, permisos, modelo, migraciones, paquetes, límites, rate limiting, auditoría, retención funcional ni runbooks históricos de CV-02/CV-03/CV-04. Si una migración fuese imprescindible, se demuestra la brecha y se pide una corrección mínima antes de editar; se actualizan todos los inventarios ejecutables de última migración. Una excepción Gitleaks, si resultara indispensable, debe señalar el secreto público exacto con `regexTarget="secret"` y anclas completas.

## 12. Decisión expresa de evidencia gratuita para CI: primero artifacts, después contingencia

GitHub Free mantiene presupuesto `$0`. La cuota acumulada de artifacts del ciclo impidió CV-04; borrar artifacts existentes no restablece ese consumo. [GitHub documenta](https://docs.github.com/en/billing/concepts/product-billing/github-actions) uso gratuito de Actions en repositorios públicos con runners estándar. El 2026-09-22 se ejecutó una prueba mínima en SGOL público: [run `35779296378`](https://github.com/SistemasTechLead/sgol/actions/runs/35779296378), SHA `e00884318f7c314921aa616aad6d3eb4da3547eb`, `upload-artifact` `SUCCESS`; la API registró el artifact `10717472576`, 190 bytes, `expired=false`, expiración `2026-10-06T20:17:23Z`. Se eliminaron la rama y el worktree de prueba. Esto demuestra que **un artifact pequeño pudo subirse mientras el repositorio era público**; no demuestra que el layout OCI completo quepa o que funcione si el repositorio vuelve a privado.

### 12.1 Primera vía obligatoria: artifacts originales

Antes del primer pipeline CV-05 se verifica cuenta, repositorio, visibilidad `PUBLIC`, presupuesto `$0`, runner estándar, SHA de cabeza y retención configurada de al menos 14 días. Si la visibilidad ya no es pública o no se pueden comprobar estas condiciones, se detiene el envío del pipeline y se solicita decisión; no se supone que la prueba mínima siga siendo aplicable. No se cambia facturación, presupuesto, plan, runner ni proveedor.

El primer workflow del PR ejecuta **todos los gates contractuales** y, después de validarlos, usa `actions/upload-artifact` fijada por SHA, una vez por archivo/grupo previsto, con `retention-days: 14`: reportes originales JSON/Markdown sanitizados de CV-05, los reportes CV-04 que el workflow vigente exige y la evidencia pública TECH-OPS/HU-035, incluido el layout OCI descargable ligado al digest del mismo `headSha`. El inventario obligatorio se valida antes del upload; diagnósticos que sólo aparecen ante una condición son opcionales y su ausencia no falla un run exitoso. El gate exige que cada artifact obligatorio figure en la API del run como no expirado, con vencimiento correspondiente a los 14 días solicitados (según la resolución de segundos de GitHub), nombre, tamaño y digest concordante con la salida de `upload-artifact`. `upload-artifact` exitoso sin esa verificación no basta para declarar retención acreditada. No se suben secretos, manifiestos privados, logs crudos, TRX integral ni evidencia real.

Si los gates y uploads pasan y la API confirma esos artifacts sobre la cabeza exacta, se usa **esta primera vía**. No se activa la excepción de retención ni se sustituye evidencia descargable por resumen. El reporte y la trazabilidad registran URLs, IDs, digest, vencimiento y SHA de cada artifact requerido. El presupuesto sigue en `$0`; si GitHub muestra un cargo o una condición de pago, se detiene el proceso antes de continuar.

### 12.2 Activación estricta de la contingencia

La contingencia sólo se activa si el primer pipeline identifica un fallo concreto de `upload-artifact` por cuota/almacenamiento, o si un artifact obligatorio no puede quedar registrado con la retención exigida, **después** de que sus archivos originales, gates funcionales y sanitización hayan pasado. Un fallo de escenario, build, seguridad, archivo obligatorio faltante, diagnóstico opcional mal clasificado, digest incoherente o infraestructura no se atribuye a la cuota y debe corregirse por su causa propia. No se borra evidencia para intentar resetear consumo acumulado ni se relanza el mismo run esperando otro resultado.

Antes de pasar a la contingencia se agrega una prueba enfocada que reproduce la causa de publicación y demuestra ambos casos del inventario: diagnóstico opcional presente y ausente, mientras todo archivo obligatorio sigue exigido. Se documentan run, job, SHA, paso y código cerrado del fallo, sin copiar el mensaje original sensible. La rama recibe un cambio **sólo** en el publicador/workflow de evidencia, con diff revisado y nuevo SHA; el pipeline requerido corre una vez para esa nueva cabeza exacta. No se combina el gate funcional verde de un SHA con un publicador verde de otro para fingir un único check.

### 12.3 Segunda vía: resumen y logs, con excepción visible

En la contingencia, el runner conserva y valida durante el job los JSON/Markdown originales y el layout OCI, liga sus hashes/digest al `headSha` y publica sólo una representación sanitizada y cerrada de reportes, matriz, conteos, digest OCI, SBOM/procedencia, escaneo y resultados operativos en resumen y logs. Se omiten para **ese PR** los uploads de CV-05 y los pasos heredados CV-04/TECH-OPS/HU-035 que consumirían cuota. El publicador falla si falta un original obligatorio, si los hashes no concuerdan o si resumen/log no se publican. Los diagnósticos opcionales pueden faltar. Sólo salen campos allowlist; nunca binarios, secretos, payloads, excepciones o mensajes originales. Resumen y logs no consumen cuota de artifacts según GitHub; antes del run se vuelve a comprobar su retención configurada, al menos 14 días (90 días observados al preparar esta propuesta).

**Excepción condicional, visible y limitada al PR CV-05:** por esta vía no habrá JSON/Markdown originales ni layout OCI descargables durante 14 días. Resumen/log y digest no reemplazan esos archivos ni permiten reanalizar offline el binario OCI. El requisito de conservación descargable queda `VALIDACION_DIFERIDA_POR_CUOTA`, jamás `PASSED`. El layout OCI es el binario indispensable para una revisión posterior del mismo digest; no existe sustituto gratuito aprobado bajo las autorizaciones actuales. La aprobación íntegra de esta adenda autoriza **únicamente si ocurre el fallo de 12.2** esta excepción concreta, sin extender automáticamente la del PR #58. Si el responsable no acepta perder archivos descargables, se detiene antes de ejecutar la segunda vía y se solicita otra decisión.

El segundo workflow muestra `RETENTION_EXCEPTION_CV05`, `VALIDACION_DIFERIDA_POR_CUOTA` y el inventario exacto `ORIGINALS_NOT_DOWNLOADABLE` en resumen, logs y trazabilidad. Un run sin esas declaraciones falla aunque los escenarios pasen. El check sólo puede ser verde si todos los gates funcionales y el publicador sanitizado pasan **en ese mismo SHA**; ese verde no acredita la retención original. Nunca se usa un rerun de `upload-artifact` como remedio de cuota.

## 13. Validación y publicación

Después de aprobar y completar cada nivel, se ejecuta una sola vez: (1) sintaxis y pruebas puras de script, catálogo, reporte, sanitización, exits y diagnósticos opcionales presente/ausente; (2) build Release de proyectos afectados; (3) pruebas unitarias/componentes filtradas de los cinco contratos; (4) PostgreSQL/Testcontainers enfocado observado mientras está activo; (5) smoke Kestrel HTTPS, auth hospedada y dependencias reales; (6) gate integral CV-05 una vez; (7) build Release de `SGOL.slnx` y validadores de rutas, secretos, migraciones, `Fuentes/`, formato aplicable y `git diff --check`. Sólo entonces commit, push sin force a `codex/tech-e2e-cv-05`, PR exclusivo y pipeline remoto sobre SHA exacto. Todo fallo concreto exige regresión enfocada antes de otro SHA; la transición entre métodos sólo se permite por la causa demostrada en 12.2, con cambio revisado y nuevo SHA. No se relanza un run por costumbre.

Antes de cada commit/push se muestran `git status`, rutas exactas y `git diff --check`; staging sólo por rutas explícitas. Se comprueba que `Fuentes/`, checkout principal, históricos CV-02/CV-03/CV-04, CV-06 y frontend no cambiaron. Cuenta GitHub, repositorio, SHA remoto y árbol local/remoto se verifican antes de publicación. Si se usa Git Data API por bloqueo interactivo, el padre remoto debe ser el esperado, sin force y con árbol idéntico; el check se exige sobre la cabeza remota del PR.

## 14. Archivos previstos después de aprobar

- Esta adenda, incorporada a la raíz del nuevo worktree desde `origin/master`.
- `tests/Sgol.Cv05Demo/**`, `scripts/demo/run-cv05.ps1`, `tests/Sgol.ArchitectureTests/Cv05DemoArchitectureTests.cs`, `SGOL.slnx` y `.gitignore` sólo para `.artifacts/cv05/`.
- `.github/workflows/pull-request.yml` y publicadores/validadores **nuevos** bajo `scripts/ci/` sólo para el gate CV-05, verificación de artifacts descargables y, si se activa 12.2, resumen/log y excepción de retención de este PR.
- `docs/operations/cv05-demo.md`, `docs/traceability/README.md` y `docs/traceability/IMPLEMENTATION_STATUS.md`, incluida corrección documental del estado pre-merge de CV-04.

Una necesidad de `src/**`, migración, `Directory.Packages.props`, lockfiles históricos, `tests/Sgol.Cv04Demo/**`, scripts CV-04, F00–F07 congelados o `Fuentes/**` exige detener y plantear otra decisión. Esta adenda no autoriza frontend, despliegue, recursos cloud, gasto, datos reales ni tareas posteriores.

## 15. Decisión de aprobación íntegra

El responsable aprobó **íntegramente y sin cambios contractuales** esta adenda, incluido el orden obligatorio de la sección 12: primero artifacts descargables en repositorio público; sólo ante fallo demostrado de cuota, nuevo SHA y contingencia sanitizada con excepción de retención visible. Quedó autorizada la implementación, validación y publicación del PR exclusivo de `TECH-E2E-CV-05`. El merge requiere autorización posterior explícita.
