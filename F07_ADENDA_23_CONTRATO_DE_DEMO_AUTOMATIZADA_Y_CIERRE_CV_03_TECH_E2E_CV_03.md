# SGOL — Adenda 23 a F07: contrato de demo automatizada y cierre de `CV-03` para `TECH-E2E-CV-03`

## 1. Control del documento y decisión solicitada

| Campo | Valor |
|---|---|
| Estado | `APROBADA íntegramente por el responsable el 2026-09-09` |
| Tarea propuesta | `TECH-E2E-CV-03 — Demo automatizada y cierre del corte CV-03` |
| Corte | `CV-03 — Ejecución comprobable` |
| Base exacta | `origin/master` en `0df916f3650890b15497e3143b7afb33e643de8d` |
| Decisión solicitada | Aprobación o rechazo íntegro de las secciones 2 a 16 |

Esta adenda resuelve exclusivamente la ausencia de contrato, entrada formal y ubicación aprobada para `TECH-E2E-CV-03`. El nombre y la creación de este archivo no constituyen aprobación. Sólo una aprobación humana íntegra autoriza comenzar el proyecto de demo, scripts, pruebas, documentación y trazabilidad previstos; no autoriza commit, publicación, pull request ni merge.

Una observación que cambie el proyecto, los escenarios, los servicios, las imágenes, los comandos, la seguridad, los reportes, los gates o los límites debe incorporarse a esta misma adenda y aprobarse antes de implementar.

## 2. Precedencia efectiva e inserción formal

La comprobación de inicio confirmó:

- no existe una adenda posterior a `F07_ADENDA_22_CONTRATO_DE_BANDEJA_PROPIA_Y_AVISOS_INTERNOS_HU_030.md`;
- `CV-03` exige como salida mínima `HU-022` a `HU-026` y `HU-030` aceptadas;
- `CV-04` recibe como entrada el cierre de `CV-03`;
- F07 exige `TECH-E2E-<CV>` antes de cerrar cada corte; y
- `HU-027` es la primera historia de `CV-04`, por lo que no puede iniciarse antes de cerrar `TECH-E2E-CV-03` y `CV-03`.

Se propone insertar formalmente:

| ID | Resultado verificable | Dependencias | Ejecutar antes de | No incluye |
|---|---|---|---|---|
| `TECH-E2E-CV-03` | Demo automatizada, finita y reproducible de las capacidades aceptadas de `CV-03`, con persistencia, almacenamiento y antimalware reales, seguridad negativa, evidencia sanitizada y segunda ejecución limpia | `HU-023`, `HU-024`, `TECH-EVID-001`, `HU-025`, `TECH-EVID-002`, `HU-026`, `HU-022`, `HU-030` y sus dependencias aceptadas | Cierre de `CV-03` y comienzo de `HU-027` | `HU-027`, `HU-028`, `HU-029`, `HU-031`, `HU-032`, validación, supervisión, indicadores, mensajería externa o producto nuevo |

La cadena precedente se reconoce como `Terminada` efectiva conforme a la regla condicional de cierre en un PR: `HU-023`, `HU-024`, `TECH-EVID-001`, `HU-025`, `TECH-EVID-002`, `HU-026`, `HU-022` y `HU-030`.

Para `HU-030` la evidencia primaria es: Adenda 22 aprobada íntegramente; PR `#45` fusionado; commits `096fd29e66c89066589da2da3009d86b2fbda1d2` y `4d502f4df78cd19b84261cd86089d0bc264d3c45`; run `34403972427`; check `TECH-BASE-003 / PR gates` `SUCCESS` sobre `4d502f4df78cd19b84261cd86089d0bc264d3c45`; PostgreSQL externo `25/25`; aceptación humana; merge `0df916f3650890b15497e3143b7afb33e643de8d`; `origin/master` exacto en ese merge; los dos commits y el merge ancestros de `origin/master`; y cero defectos bloqueantes conocidos. No se exige un commit administrativo que reescriba el encabezado histórico de propuesta.

La aprobación de esta adenda inserta el contrato, pero no cierra la tarea. `HU-027` permanece bloqueada hasta satisfacer la sección 15.

## 3. Fuentes y límite interpretativo

La propuesta se limita a las filas y secciones aplicables de F07, `HU-022` a `HU-026`, `HU-030`, `CAP-027` a `CAP-031`, `CAP-042`, `CA-022` a `CA-026`, `CA-030`, sus `CP` positivos y negativos, `RN-002`, `RN-006`, `RN-008`, `RN-016` a `RN-019`, `RN-023`, `RN-025`, `RN-027` a `RN-029`, las decisiones citadas por esas historias, las adendas 15 a 22, los contratos F06 directamente aplicables y las implementaciones fusionadas que la demo consume.

La Adenda 14 se usa sólo como precedente estructural para separar un harness de producto, cerrar comandos, controlar datos sintéticos, reportar evidencia y condicionar el cierre. No autoriza reutilizar las rutas, UI, semilla, escenarios, Playwright ni contrato de `CV-02`.

La demo complementa las pruebas de cada historia; no redefine sus respuestas ni convierte una observación del harness en regla funcional. Ante una discrepancia entre esta propuesta y un contrato fusionado, prevalece el contrato fusionado y se detiene la implementación para revisar esta adenda.

## 4. Naturaleza, ubicación y dependencias del entregable

Se crea un proyecto no productivo independiente:

```text
tests/Sgol.Cv03Demo/Sgol.Cv03Demo.csproj
```

No se extiende `tests/Sgol.Cv02Demo`. Separar el proyecto evita mezclar los contratos de `CV-02` y `CV-03`, impide que la semilla y el reporte de un corte oculten defectos del otro, y evita heredar Razor Pages, Playwright, estados visuales y rutas que no forman parte de `CV-03`.

`Sgol.Cv03Demo` será un ejecutable automatizado y proyecto xUnit. Podrá referenciar únicamente:

- `src/Sgol.Web/Sgol.Web.csproj`, para la composición real de persistencia, identidad/autorización, configuración, calendario, generación, obligación, asignación, evidencia, evaluación, conclusión, bandeja, avisos y HTTP; y
- `src/Sgol.Worker/Sgol.Worker.csproj`, para ejecutar el entry point y los manejadores reales de outbox e inspección cuando un escenario lo requiera.

Las referencias transitivas existentes de `Sgol.Web` no autorizan una nueva dependencia productiva. Ningún proyecto bajo `src/` puede referenciar `Sgol.Cv03Demo`; una prueba de arquitectura lo comprobará. El proyecto se incluirá en `SGOL.slnx` bajo `/tests/`.

Se reutilizan las versiones ya centralizadas de `Microsoft.NET.Test.Sdk`, `Testcontainers`, `Testcontainers.PostgreSql`, `xunit` y `xunit.runner.visualstudio`. No se agrega paquete. En particular, el proyecto no referencia `Microsoft.Playwright`.

La demo no se publica, despliega ni empaqueta como producto. No agrega ruta productiva, comportamiento productivo, tabla, entidad, migración, permiso, scheduler, outbox, canal ni regla funcional.

## 5. Comando, modo y ciclo de vida

El único comando canónico será:

```powershell
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo\run-cv03.ps1 -Mode Automated
```

El wrapper sólo acepta `-Mode Automated`. No existe modo predeterminado ni interactivo: sin UI navegable, un proceso retenido para exhibición humana aumentaría superficie y dejaría recursos vivos sin aportar una evidencia distinta. La evidencia humana se revisa en los reportes de la sección 11.

El script no contiene reglas de negocio y no acepta URL, puerto, conexión, credencial, imagen, sucursal, TAR, actor, fecha, UUID, semilla, archivo o payload arbitrario. En una ejecución:

1. rechaza configuración PostgreSQL, S3 o ClamAV aportada externamente y cualquier destino que no pertenezca a los contenedores creados por esa ejecución;
2. crea red, temporales y credenciales efímeras privadas;
3. inicia PostgreSQL, SeaweedFS y ClamAV en el orden y con las guardas de la sección 7;
4. aplica todas las migraciones a una base vacía;
5. aprovisiona los dos buckets privados y la regla CORS exacta mediante una identidad separada;
6. inicia el host real en `http://127.0.0.1:0`, prepara la semilla y ejecuta `S01` a `S24` en orden;
7. ejecuta Worker/outbox real cuando corresponda, sin invocar directamente el pipeline técnico para fabricar el resultado funcional;
8. escribe el reporte sanitizado; y
9. en `finally`, detiene Worker, host, clientes, conexiones y contenedores, borra buckets/objetos, red, base, credenciales y temporales incluso ante cancelación o fallo.

El proceso devuelve `0` sólo si los 24 escenarios, la sanitización y la limpieza pasan. No existe `--keep-data`, reanudación contra recursos anteriores ni servicio continuo.

## 6. Servicios reales, HTTP y seguridad

Los resultados bajo demostración deben atravesar las implementaciones reales aceptadas de identidad y autorización, configuración, calendario, generación, obligación, asignación, evidencia, evaluación, conclusión, bandeja, avisos, idempotencia, auditoría, persistencia y Worker. Los fixtures sólo pueden crear precondiciones; no pueden insertar como semilla una evidencia, snapshot, resultado, conclusión, aviso leído o auditoría que el escenario pretende demostrar.

El host de demo expone por loopback exclusivamente las rutas productivas ya existentes que usa la matriz. No agrega `/cv03`, consola, selector libre, Swagger ni endpoint auxiliar. Una ruta o método fuera de los contratos existentes devuelve el rechazo productivo correspondiente.

La autenticación de transporte usa un esquema exclusivo de prueba que emite únicamente la identidad y marca MFA del actor sintético seleccionado por una constante del escenario. No acepta actor desde el cliente. Cuenta, persona, empleo, rol canónico, vigencia, sucursal y permisos se resuelven desde PostgreSQL por los servicios reales; el esquema de prueba no concede alcance ni evita la autorización del recurso.

Toda mutación HTTP usa el contrato vigente: CSRF con cabecera y cookie, `Idempotency-Key` cuando aplica, `If-Match` fuerte cuando aplica y cuerpos allowlist. Se comprueban autorización positiva y negativa, MFA incompleto, cuenta/empleo/rol no vigentes y protección IDOR. Un UUID inexistente, de otra sucursal o fuera de alcance converge conforme al contrato sin confirmar existencia.

Los actores, archivos, referencias y fechas son sintéticos. No se almacenan ni reportan contraseñas, secretos TOTP, códigos de recuperación, cookies, tokens, cadenas de conexión, credenciales, URLs firmadas, claves de objeto, nombres originales, hashes privados, SQL, payloads estructurados completos ni contenido binario. Los errores y artefactos sólo admiten códigos, campos y mensajes de una allowlist; nunca incluyen stack trace crudo, ruta física, respuesta del proveedor o contenido de evidencia.

`IClock` e `IUuidGenerator` se sustituyen únicamente en la composición de demo por implementaciones deterministas y agotables. No se sustituye servicio funcional, repositorio, almacenamiento, escáner, transacción o autorización.

## 7. Infraestructura real y limpieza

La ejecución externa usa exclusivamente:

| Servicio | Versión o imagen fijada | Uso |
|---|---|---|
| PostgreSQL | `postgres:18.6-alpine3.23` | Persistencia, restricciones, transacciones, locks, carreras, auditoría e idempotencia |
| SeaweedFS | `chrislusf/seaweedfs:4.45@sha256:fc9f76fa993ad69966ffeb2f65d0318fcae39c6f8e20cf68ef7b3a5cb97769e5` | S3-compatible privado, cuarentena y limpio |
| ClamAV | `clamav/clamav:1.5.4-debian@sha256:be3cb41d9833ce9ffb98f3d3e1483c35c0d87060c2bda3624d75fd28bbf0b3bd` | `clamd` real y resultado antimalware |

PostgreSQL arranca primero y debe aceptar una conexión real. SeaweedFS arranca segundo; su puerto interno debe estar disponible y las identidades/buckets/CORS deben poder verificarse mediante petición firmada. ClamAV arranca tercero; su puerto debe estar disponible y debe responder al protocolo `zINSTREAM`. Sólo después se aplican migraciones y comienza la semilla.

Cada servicio usa contenedor nuevo, puerto aleatorio publicado sólo en loopback, sin volumen persistente y con credenciales aleatorias en memoria. La base usa prefijo `sgol_cv03_`; los buckets conservan los nombres aprobados `sgol-evidence-quarantine` y `sgol-evidence-clean`. La identidad operativa mantiene únicamente los permisos aprobados y no repara CORS.

Queda prohibido SQLite. Mocks, dobles o proveedores en memoria no constituyen evidencia de persistencia, S3 ni antimalware. La ejecución se realiza una sola vez fuera de la sesión por el desarrollador. Desde la sesión no se ejecuta ni diagnostica Docker, Testcontainers, SeaweedFS o ClamAV, no se instalan herramientas y no se modifica configuración persistente.

No se agrega Redis, broker, microservicio, scheduler, volumen, canal ni infraestructura nueva. Si la demo descubre un defecto reproducible en los adaptadores SeaweedFS/ClamAV o requiere cambiar imagen, digest, permisos, protocolo, health check o configuración, la implementación se detiene y se presenta una ampliación de alcance antes de tocar `src/`.

## 8. Semilla determinista

La semilla `CV03-SEED-V1` crea sólo datos sintéticos de `LOR-001` y las precondiciones mínimas:

- `DIR-CV03`, Dirección vigente;
- `ADMIN-CV03`, Administración vigente;
- `SUB-CV03`, Subcoordinación vigente;
- `RESP-A-CV03` y `RESP-B-CV03`, Piso de Ventas vigentes;
- `OUTSIDE-CV03`, actor vigente sin alcance sobre el recurso demostrado; y
- variantes deterministas de actor sin MFA, cuenta inactiva, empleo no vigente y rol no vigente para negativas.

Se crean obligaciones separadas para futuro, disponible, vencida, evidencia incompleta, evidencia completa, TAR-0092 aplicable/no aplicable y carreras. Fechas UTC y su interpretación en `America/Mexico_City`, UUID, claves idempotentes, ETag iniciales, referencias y motivos son constantes versionadas. La secuencia de UUID falla si aparece una creación no prevista.

El corpus binario se genera en memoria. JPEG, PNG y PDF son sintéticos. EICAR se habilita únicamente en la ejecución externa, se reconstruye en memoria desde fragmentos no contiguos, se escribe sólo en un temporal privado y se destruye en `finally`. Ningún contenido del corpus se incorpora a Git o al reporte.

## 9. Matriz cerrada de escenarios

Cada nombre es estable en pruebas y reportes. Toda fila negativa comprueba código/resultado, ausencia de cambio funcional, ausencia de recurso parcial, historia conservada y auditoría sólo cuando el contrato la exige.

| # | Escenario estable | HU, CA y CP | Regla aplicable | Resultado verificable |
|---:|---|---|---|---|
| 1 | `S01_ResponsibleReadsOnlyAllowedWork` | `HU-023`; `CA-023`; `CP-023-P` | `RN-002`, `RN-025`, `RN-027`; Adenda 15 §§3–6 | El responsable lista y detalla únicamente obligación propia con origen, fechas, vencimiento e historia permitida. |
| 2 | `S02_ForeignTaskConvergesWithoutDisclosure` | `HU-023`; `CA-023`; `CP-023-N` | `DEC-044`, `DEC-055`, `DEC-056`; Adenda 15 §6 | Tarea ajena, inexistente u otra sucursal converge en `404` sin filtración ni diferencias de cuerpo. |
| 3 | `S03_ObligationKeepsFrozenEvidencePolicy` | `HU-024`; `CA-024`; `CP-024-N` | `RN-006`, `RN-008`, `RN-017`, `DEC-021`; Adenda 16 §§8–9 | Publicar una política posterior no altera `evidence_policy_version_id` ni requisitos de la obligación creada. |
| 4 | `S04_ContributesStructuredEvidenceThroughRealService` | `HU-025`, `TECH-EVID-002`; `CA-025`; `CP-025-P` | `RN-018`, `RN-023`, `RN-027`; Adenda 20 §§5–8 | Aporta un payload `schemaVersion=1` permitido mediante el servicio real, sin S3, ClamAV ni outbox. |
| 5 | `S05_CleanBinaryTravelsThroughPrivateInfrastructure` | `HU-025`, `TECH-EVID-001`; `CA-025`; `CP-025-P` | Adendas 17 §§5–7 y 18 §§6–10 | Intención, PUT firmado a cuarentena, confirmación, Worker, ClamAV y promoción real terminan en vínculo `LIMPIO`. |
| 6 | `S06_InvalidOrInfectedBinaryNeverBecomesEvidence` | `HU-025`, `TECH-EVID-001`; `CA-025`; `CP-025-N` | Denegación cerrada de Adenda 17 §7 y Adenda 18 §§8–9 | Inválido e infectado permanecen fuera del bucket limpio y no crean versión funcional; no se filtra el diagnóstico crudo. |
| 7 | `S07_ReplacementPreservesCompleteHistory` | `HU-025`; `CA-025`; `CP-025-P` | `RN-018`, `RN-023`, `RN-027`, `DEC-067`, `DEC-068`; Adenda 18 §§11–14 | Sustituir conserva ambas versiones enlazadas y exactamente una `VIGENTE`; la anterior queda `SUSTITUIDA`. |
| 8 | `S08_SubstitutedEvidenceDoesNotSatisfyRequirement` | `HU-025`, `HU-026`; `CA-026`; `CP-026-N` | `RN-017` a `RN-019`; Adenda 19 §§9–11 | El evaluador ignora la versión `SUSTITUIDA`; si la vigente no satisface, devuelve el faltante exacto. |
| 9 | `S09_Tar0092DifferenceMakesPhotoApplicable` | `TECH-EVID-002`, `HU-026`; `CA-026`; `CP-026-P` | `F_ENT_001`, `DIFERENCIA_O_DANO`; Adendas 19 §9 y 20 §§8.7, 9 | Uno de los booleanos verdadero vuelve la foto `APLICABLE` y exige evidencia vigente satisfactoria. |
| 10 | `S10_Tar0092NoDifferenceMakesPhotoNotApplicable` | `TECH-EVID-002`, `HU-026`; `CA-026`; `CP-026-P` | `F_ENT_001`, `DIFERENCIA_O_DANO`; Adendas 19 §9 y 20 §9 | Ambos booleanos falsos vuelven la foto `NO_APLICABLE`; no se fabrica faltante fotográfico. |
| 11 | `S11_AllApplicableRequirementsEvaluateComplete` | `HU-026`; `CA-026`; `CP-026-P` | `RN-017` a `RN-019`, `DEC-022`; Adenda 19 §§8–14 | Todos los requisitos aplicables satisfechos producen `COMPLETA`, snapshot coherente y orden determinista. |
| 12 | `S12_MissingRequirementEvaluatesIncompleteExactly` | `HU-026`; `CA-026`; `CP-026-N` | `RN-017` a `RN-019`, `DEC-022`; Adenda 19 §§8–10 | Falta uno de varios tipos, produce `INCOMPLETA` y señala exactamente código, clase, condición y motivo faltante. |
| 13 | `S13_ConclusionWithMissingEvidenceHasNoEffect` | `HU-022`; `CA-022`; `CP-022-N` | `RN-016` a `RN-019`, `DEC-021`, `DEC-022`, `DEC-066`; Adenda 21 §§8–13 | La conclusión se rechaza; obligación sigue `PENDIENTE`, sin resultado, transición, snapshot de éxito ni escritura parcial. |
| 14 | `S14_CompleteConclusionPersistsExactSnapshotAndResult` | `HU-022`; `CA-022`; `CP-022-P` | `RN-016` a `RN-019`; Adenda 21 §§11–16 | Responsable vigente concluye `PENDIENTE→CONCLUIDA`; resultado, snapshot exacto, idempotencia y auditoría confirman atómicamente. |
| 15 | `S15_ConclusionReplayReturnsSameOutcome` | `HU-022`; `CA-022`; `CP-022-P` | Idempotencia de Adenda 21 §12 | Repetir misma clave y contenido lógico devuelve la misma conclusión sin segundo resultado ni transición. |
| 16 | `S16_ConcurrentConclusionsHaveSingleWinner` | `HU-022`; `CA-022`; `CP-022-N` | Serialización y locks de Adenda 21 §13 | Dos conclusiones realmente concurrentes dejan una sola ganadora, un resultado, un snapshot enlazado y ninguna mezcla. |
| 17 | `S17_OwnInboxShowsFutureAvailableAndOverdueTasks` | `HU-030`; `CA-030`; `CP-030-P` | `RN-019`, `RN-025`, `RN-029`, `DEC-041`, `DEC-066`; Adenda 22 §§6–10 | La bandeja propia clasifica futura, disponible y vencida con evaluación informativa correcta. |
| 18 | `S18_OverdueFlagDoesNotChangeExecutionStatus` | `HU-023`, `HU-030`; `CA-030`; `CP-030-P` | `RN-019`, `RN-025`; Adendas 15 §5 y 22 §9 | El vencimiento es derivado; `execution_status` permanece `PENDIENTE` y la lectura no escribe. |
| 19 | `S19_AssignmentProducesOneInternalNotice` | `HU-030`; `CA-030`; `CP-030-P` | `RN-029`; Adenda 22 §§11–13 | La asignación real produce exactamente un `OBLIGATION_ASSIGNED` para el nuevo responsable dentro de la transacción. |
| 20 | `S20_NoticeReadIsIdempotentAndAtomicallyAudited` | `HU-030`; `CA-030`; `CP-030-P` | `RN-029`; Adenda 22 §§14–16 | Primera lectura fija `read_at` y una auditoría; replay devuelve el instante original sin segunda escritura; aviso ajeno converge. |
| 21 | `S21_QueriesCreateNoSnapshotsResultsAuditOrWrites` | `HU-023`, `HU-026`, `HU-030`; `CA-023`, `CA-026`, `CA-030`; `CP-023-P`, `CP-026-P`, `CP-030-P` | Lectura `READ ONLY`, `AsNoTracking`; Adendas 15 §15, 19 §11 y 22 §§10, 15 | Listado, detalle y bandeja conservan huellas de tablas funcionales, snapshots, resultados, auditoría e idempotencia. |
| 22 | `S22_NoExternalNotificationIsProduced` | `HU-030`; `CA-030`; `CP-030-N` | `RN-029`; límite de Adenda 22 §§4, 11 | No existe correo, SMS, push, webhook, outbox de canal ni intento de red externa. |
| 23 | `S23_NoValidationSupervisionOrIndicatorsAreCreated` | `HU-022`, `HU-025`, `HU-026`, `HU-030`; `CA-022`, `CA-025`, `CA-026`, `CA-030`; `CP-022-P`, `CP-025-P`, `CP-026-P`, `CP-030-N` | Límites de Adendas 18–22; `RN-020` a `RN-024` reservadas para `HU-027`/`HU-028` | Tras el flujo no hay `validation_policy_version`, `validation_requirement`, `validation_decision_version`, supervisión ni indicadores. |
| 24 | `S24_SecondCleanRunIsReproducibleWithoutResidue` | `HU-022` a `HU-026`, `HU-030`; todos sus `CA` y `CP` aplicables | Unicidad, idempotencia, historia y limpieza de las Adendas 15–22 | Una segunda ejecución parte de recursos nuevos, reproduce huella y resultados, no observa residuos y no crea duplicados dentro de la corrida. |

La matriz es finita: no se agregan escenarios durante implementación sin revisar y aprobar esta adenda. Si un resultado no puede producirse u observarse mediante contratos existentes sin cambiar `src/`, esquema o semántica, se detiene el escenario y se presenta la contradicción.

## 10. Interfaz, navegador y accesibilidad

`TECH-E2E-CV-03` no requiere ni autoriza interfaz técnica navegable. Las capacidades aceptadas del corte se exponen mediante API y servicios; `HU-030` cerró expresamente sin UI por brecha de diseño. Las páginas de `Sgol.Cv02Demo` no son compatibles contractualmente: representan calendario, recurrencias y plan de `CV-02`, y no contienen componentes aprobados para evidencia, conclusión, bandeja o avisos.

Por ello:

- no se lee ni modifica `docs/design` para esta implementación;
- no se crea Razor, HTML, CSS, JavaScript, ruta de página o recurso estático;
- los estados normal, foco, deshabilitado, error, cargando y vacío no aplican a una superficie inexistente;
- no se instala ni ejecuta navegador;
- Chromium, WebKit, Playwright, teclado, foco, contraste y viewports quedan `NO APLICA`, no `PASSED`; y
- la mención de Playwright en el gate externo se resuelve como no aplicable a este contrato. Ejecutarlo sin UI no aporta evidencia y mezclaría el cierre de `CV-02` con `CV-03`.

Si posteriormente se solicita UI, debe existir diseño aprobado para todos sus componentes, estados y mensajes, aprobarse una revisión de esta adenda y leerse íntegramente la documentación obligatoria de diseño antes de editar.

## 11. Reporte, artefactos y retención

Cada ejecución crea exclusivamente:

```text
.artifacts/cv03/latest/TECH-E2E-CV-03-report.json
.artifacts/cv03/latest/TECH-E2E-CV-03-report.md
```

El JSON usa el identificador de esquema `sgol.tech-e2e-cv03.report` y `schemaVersion: 1`. JSON y Markdown contienen:

- tarea, corte y commit Git exacto;
- sistema operativo y versión .NET;
- imágenes/digests efectivos de PostgreSQL, SeaweedFS y ClamAV;
- instante UTC de inicio/fin y duración total;
- `CV03-SEED-V1` y SHA-256 de las constantes sintéticas canónicas, nunca sus payloads;
- `S01` a `S24`, HU/CA/CP/regla, duración y `PASSED` o `FAILED`;
- conteos y huellas no reversibles estrictamente necesarios para reconciliar ausencia de duplicados;
- rutas relativas a artefactos sanitizados de fallo;
- resultado de limpieza de host, Worker, conexiones, objetos, buckets, temporales, red y contenedores; y
- identificador de defecto cuando exista.

Un artefacto de fallo sólo puede contener código allowlist, fase, escenario, instante, correlación sintética y conteos esperados/observados. Se prohíben logs crudos, dumps, SQL, stack traces, capturas de memoria, cookies, tokens, conexiones, URLs firmadas, claves, hashes de archivo, payloads o contenido de evidencia.

Al iniciar, un `latest` exitoso anterior se elimina. Un `latest` fallido anterior se rota a una única carpeta `.artifacts/cv03/previous-failure/`, sustituyendo la anterior; se conservan como máximo el resultado actual y un fallo previo. `.artifacts/cv03/` permanece fuera de Git. La limpieza de infraestructura ocurre aunque falle la escritura del reporte.

No se modifica CI en esta tarea. Si después se autoriza ejecutar la demo en CI, la retención del reporte y artefactos de fallo será exactamente 14 días mediante una revisión aprobada. La aprobación humana, el run requerido y el merge son evidencia externa separada; el harness no los inventa ni los declara.

## 12. Pruebas posteriores a la aprobación

Las pruebas enfocadas sin Docker ni navegador comprobarán:

- parser que sólo acepta `Automated` y rechazo de parámetros/configuración arbitrarios;
- proyecto separado, referencias cerradas y ausencia de referencias desde `src/`;
- allowlist de escenarios, actores, rutas y métodos;
- determinismo y agotamiento de reloj, UUID, semilla y reporte;
- sanitización de errores y artefactos con corpus adverso sintético no ejecutado;
- guardas que sólo aceptan conexiones/endpoints de contenedores de la ejecución;
- orden de arranque, health checks y limpieza mediante coordinadores sustituibles, sin simular resultados funcionales;
- lector de reconciliación `AsNoTracking`, transacción PostgreSQL `READ ONLY`, sin `SaveChanges` ni comandos;
- ausencia de UI, Playwright, rutas nuevas, cambios productivos y migraciones;
- catálogo cerrado `S01` a `S24` y trazabilidad uno a uno; y
- generación de reportes v1, rotación y eliminación de campos prohibidos.

La única ejecución externa completa cubrirá en conjunto:

- PostgreSQL real desde base vacía y todas las migraciones vigentes;
- SeaweedFS privado, cuarentena/limpio, PUT firmado, CORS fijo y ausencia de acceso anónimo;
- ClamAV real con contenido limpio e EICAR efímero;
- host, servicios, Worker, persistencia, locks, idempotencia y auditoría reales;
- `S01` a `S24`, incluidas carreras reales y negativas sin efectos;
- segundo ciclo con recursos nuevos y misma huella funcional;
- reporte sanitizado; y
- limpieza completa después de éxito y después de una falla controlada de infraestructura.

No se aceptan como sustituto la suma de suites históricas, mocks, SQLite, una ejecución parcial o resultados de otro SHA. El desarrollador debe devolver en un solo mensaje: comando exacto, SHA, total de escenarios/pruebas, pasadas, fallidas, omitidas, advertencias, duración, rutas de reporte y resultado de limpieza.

## 13. Gates y participación en CI

La tarea no modifica `.github/workflows`, scripts de pipeline ni política de checks. `SGOL.slnx` hará que restore, build, pruebas sin infraestructura, arquitectura, formato y análisis de dependencias inspeccionen el proyecto. La demo real continúa como gate externo único.

Después de implementar se ejecutan una sola vez y en este orden:

1. `rtk dotnet restore SGOL.slnx --locked-mode`, fuera del aislamiento;
2. `rtk dotnet build SGOL.slnx --no-restore --configuration Release`;
3. suite unitaria completa;
4. suite de arquitectura completa;
5. pruebas enfocadas de `TECH-E2E-CV-03` sin Docker ni navegador;
6. solicitud y espera de la ejecución externa canónica con PostgreSQL, SeaweedFS, ClamAV y demo completa; Playwright se informa `NO APLICA` por la sección 10;
7. `rtk dotnet format SGOL.slnx --verify-no-changes --no-restore`;
8. `rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ci\Assert-NoVulnerablePackages.ps1`;
9. `rtk dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/Sgol.Web --startup-project src/Sgol.Web --no-build --configuration Release`;
10. `rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ci\verify-fuentes-protection.ps1`;
11. sólo después del anterior, `rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ci\verify-fuentes-mirror.ps1`;
12. `rtk git diff --check`.

Cada gate se reporta por separado. Un gate omitido, pendiente, compuesto, ejecutado fuera de orden o sobre otro SHA queda `NO VERIFICADO`. Los gates 10 y 11 nunca se ejecutan en paralelo.

## 14. Archivos previstos después de la aprobación

La implementación mínima prevista queda limitada a:

- `tests/Sgol.Cv03Demo/**`;
- `scripts/demo/run-cv03.ps1`;
- `SGOL.slnx`;
- pruebas de arquitectura directamente necesarias;
- `.gitignore` sólo si `.artifacts/cv03/` no queda cubierto por la regla existente;
- `docs/operations/cv03-demo.md`;
- `docs/traceability/README.md`;
- `docs/traceability/IMPLEMENTATION_STATUS.md` como propuesta en rama; y
- esta Adenda 23, después de su aprobación íntegra.

No se prevén cambios en `Directory.Packages.props` porque no se agrega paquete. No se modifica `tests/Sgol.Cv02Demo`, salvo que una contradicción futura se presente y apruebe expresamente; esta propuesta no lo autoriza.

`docs/design/logo.svg`, `docs/design/mapa-pantallas.md` y los tres `Designer.cs` ajenos permanecen literalmente intactos y fuera de staging. `docs/design/mapa-pantallas.md` no es fuente operativa. `Fuentes/` no se busca, abre, enumera, modifica ni usa como salida.

## 15. Límites, trazabilidad, eficacia y autorizaciones

`TECH-E2E-CV-03` no puede:

- crear endpoint, página o comportamiento productivo;
- cambiar regla funcional aceptada;
- agregar migración, tabla, entidad o columna;
- implementar `validation_policy_version`, `validation_requirement` o `validation_decision_version`;
- iniciar o adelantar `HU-027`, `HU-028`, `HU-029`, `HU-031`, `HU-032`, `CV-04` o `CV-05`;
- implementar validación, supervisión, indicadores o consulta de Dirección;
- crear correo, SMS, push, webhook u otra mensajería externa;
- agregar scheduler, outbox, Worker, broker, Redis, microservicio o canal;
- modificar adaptadores de SeaweedFS/ClamAV, configuración productiva o CI;
- convertir el harness en producto; ni
- usar datos reales o conservar contenido de evidencia.

Si un escenario exige cualquiera de esos cambios, se detiene la implementación y se presenta la contradicción. Un defecto reproducible en producción tampoco autoriza corregirlo dentro de esta tarea sin ampliación aprobada.

Tras la aprobación e implementación, la trazabilidad debe actualizar en el mismo cambio: inserción formal de `TECH-E2E-CV-03`, matriz `S01` a `S24`, comando, infraestructura, seguridad, reportes, documentación operativa, `docs/traceability/README.md` y `docs/traceability/IMPLEMENTATION_STATUS.md`. En la rama, el estado es sólo propuesta y no habilita `HU-027`.

`TECH-E2E-CV-03` sólo adquiere estado efectivo `Terminada` cuando el commit exacto que contiene implementación y trazabilidad tiene pipeline requerido verde, ejecución externa completa satisfactoria sobre ese SHA, aprobación humana, merge, ascendencia verificada en `origin/master`, protección de `Fuentes/`, todos los gates registrados y cero defectos bloqueantes.

`CV-03` sólo queda cerrado cuando además permanecen aceptadas todas sus historias y se satisface el gate de corte de F07. Sólo entonces se habilita `HU-027` y la entrada de `CV-04`.

La aprobación de esta adenda no autoriza commit. La autorización de commit no autoriza publicación. La publicación no autoriza PR. El PR no autoriza merge. Si cambia el SHA aprobado para merge, se requiere una nueva autorización.

## 16. Decisión íntegra solicitada

Se solicita aprobar o rechazar íntegramente esta Adenda 23. Una aprobación parcial, el nombre del archivo, la revisión de su contenido o la autorización para editar no permiten iniciar la implementación.

Pregunta de aprobación requerida:

> ¿Apruebas íntegramente la Adenda 23?

## 17. Ampliación aprobada por defecto reproducible de composición

El 2026-09-09, durante la ejecución externa aprobada, `S05_CleanBinaryTravelsThroughPrivateInfrastructure` demostró que el host web no registraba `EvidenceInspectionOutboxHandler`. Por ello `OutboxWriter` rechazaba el tipo ya existente `EVIDENCE.FILE_INSPECTION_REQUESTED.V1` al confirmar una carga binaria limpia, antes de que el Worker pudiera procesarla.

El responsable aprobó íntegramente la ampliación el 2026-09-09. Se autoriza exclusivamente:

- registrar el handler existente como `IOutboxHandler` en `AddSgolEvidenceInfrastructure`, de forma idempotente;
- agregar una prueba unitaria de regresión sobre esa composición;
- reanudar la demo externa desde su matriz cerrada.

La ampliación no autoriza cambios en adaptadores SeaweedFS/ClamAV, endpoints, reglas funcionales, migraciones, paquetes, CI ni infraestructura. Tampoco altera las condiciones de eficacia: `TECH-E2E-CV-03` continúa como propuesta hasta el pipeline exacto, ejecución externa satisfactoria, aceptación humana, merge y ascendencia en `origin/master`.
