# FRONT-017 — Carga privada y primera aportación de evidencia

Fecha: 2026-09-30. Estado: **Publicada**, pendiente de validación remota y merge, con revisión visual del responsable aprobada el 2026-10-01 y validaciones diferidas enumeradas. Rama `codex/front-017`, base integrada de PR #85 `754029d7f3fce8f5eec9961824ad336930d235dc`. Publicación autorizada el 2026-10-01 mediante «Autorizo su publicación»; evidencia en el PR que incorpora esta actualización y sus checks de cabeza. Sin merge ni despliegue autorizados.

2026-10-01. El responsable revisó pantallas y mensajes y aprobó la implementación mediante «Ya revisé las pantallas y sus mensajes, apruebo su implementación», sobre el código local `60c9ed9`. La revisión visual queda aprobada; las validaciones diferidas conservan sus causas. Esta aprobación no autoriza publicación, checks remotos, merge ni despliegue.

## Decisiones y dependencias

Plan único `FRONT_017_PLAN_DE_IMPLEMENTACION.md` aprobado íntegramente mediante «Apruebo integramente el plan, recuerda seguir los documentos de diseño para crear las pantallas a como están diseñadas las anteriores». Adenda 56 incorporada antes del código: resolución expresa de BR-D08, composición cerrada de 18 formularios, mensajes/estados y lectura de la política capturada. BR-D07 consume la base visual vigente. FRONT-012/016 y TECH-EVID-001/002 disponibles; evidencia anterior aceptada sin repetir gates. La integración del diseño se registra dentro de este hito, sin PR administrativo ni reapertura.

## Resultado y uso

En Mi trabajo, «Aportar evidencia» conduce al detalle existente conservando el contexto de regreso. El responsable vigente de una tarea PENDIENTE selecciona un requisito de su política capturada. Los requisitos con primera versión vigente no permiten otra primera aportación.

Para un archivo: seleccionar o arrastrar PDF, JPEG o PNG según el requisito, revisar nombre y, en recepción, elegir NOTA/REMISION/FACTURA; «Cargar archivo» calcula SHA-256 en memoria, solicita upload-intents, realiza el PUT firmado exacto y confirma complete. El progreso mide bytes de transporte. PENDIENTE_ESCANEO mantiene el archivo sin vincular; sólo LIMPIO habilita «Aportar evidencia». La aportación necesita una respuesta confirmada de primera versión VIGENTE. INFECTADO/INVALIDO/ERROR_ESCANEO conservan el rechazo y nunca habilitan aportar.

Para evidencia estructurada: llenar el formulario cerrado del requisito y pulsar «Aportar evidencia»; el servidor valida y prepara una intención protegida, y la confirmación posterior aporta el mismo cuerpo con clave estable. Campos booleanos no toman false por defecto. Conformidad no lleva campos de acción; fecha/hora operativa se convierte de America/Mexico_City a UTC. Un checklist con No puede guardarse y advierte que no acredita conformidad. No hay JSON libre, esquema editable, picker de personas inventado ni referencia URL/HTML. FOTO_DIFERENCIA_DANO requiere F-ENT-001 vigente válido y diferencia o daño verdadero.

«Actualizar estado» consulta sin escribir; seguimiento secuencial cada cinco segundos, visible y pendiente, suspendido ante error, navegación o veredicto. «Cancelar carga» detiene el transporte local, no borra archivos/historia. «Recuperar resultado» usa las claves originales; no retransmite un PUT incierto ni repite automáticamente una escritura. Una respuesta perdida de aporte se recupera sin una segunda versión o auditoría duplicada. «Preparar otra carga» es explícito y no rehabilita un veredicto terminal.

## Contratos y archivos

- Execution: `ObligationDetail.evidencePolicy` nullable, requisitos ordenados del snapshot capturado. Configuration expone su lector interno; consulta read-only en la transacción existente, acepta la versión capturada SUSTITUIDA y falla cerrado ante incoherencia. Sin tablas ni migraciones.
- Identity: la sesión proyecta el permiso ya contratado PER-EVIDENCIA-APORTAR para los cuatro roles canónicos. No es autorización suficiente: Evidence mantiene persona/rol/empleo/sucursal, asignación vigente, jerarquía, sesión y estado en servidor.
- Consumidor: `Details.Evidence.cs`, `_EvidenceContribution.cshtml`, presentación de formularios e intenciones protegidas, `evidence-contribution.js`, entrada de bandeja y detalle. Sólo upload-intents, PUT firmado, complete, status y primera evidence POST; GET evidence autorizado verifica versiones y condición por cursor.
- Componente compartido de upload: accept, nombre/ayuda accesible, input detrás del área con foco visible, deshabilitado y estado dinámico único. CSS propio y variables oficiales; shell, Poppins OFL/Georgia y referencia v2 conservados. Sin bibliotecas añadidas ni activos tipográficos nuevos.
- Cliente API: reconoce 410 y transforma timeout no solicitado por el usuario en resultado no confirmado, sin reintento automático. Formularios y transporte no-store/no-referrer; actor/obligación/operación/cuerpo/claves protegidos. URL firmada sólo en transporte temporal, nunca DOM, storage, capturas o logs. PUT omite cookies y CSRF de SGOL.
- Pruebas enfocadas de presentación/cliente, proyección PostgreSQL, arquitectura y navegador/servicios de evidencia. Harness sintético aislado; handler de inspección productivo invocado en transacción con auditoría/outbox, sin simular LIMPIO con SQL. EICAR opt-in se envía en memoria por INSTREAM, sin fichero de estación de trabajo ni objeto S3.

If-Match de sustitución se conserva y no se consume: primera aportación no lo exige. No se implementan FRONT-018..020, revisión agregada, conclusión, sustitución, preview o descarga. Fuentes/ y F00–F07 congelados sin cambios.

## Validación local

SDK aislado 10.0.400, global.json conservado. Preflight ejecutado una sola vez al preparar el plan. Sin restore: artefactos presentes, sin cambios de dependencias o lock.

Comandos de pruebas: `dotnet test <proyecto> --no-build --configuration Release --filter <filtro>`, con los proyectos de `tests/` y filtros siguientes. Se ejecutó el binario del SDK aislado; la generación final de capturas activó `SGOL_FRONT017_CAPTURE_DIR=.artifacts/front-017`.

```text
Sgol.UnitTests: FullyQualifiedName~Front017PresentationTests|FullyQualifiedName~Front016PresentationTests|FullyQualifiedName~EvidenceApiEndpointTests|FullyQualifiedName~StructuredEvidencePayloadValidatorTests|FullyQualifiedName~SgolApiClientTests|FullyQualifiedName~ComponentRenderTests.Upload_|FullyQualifiedName~Front002Tests|FullyQualifiedName~EvidenceContributionDomainTests|FullyQualifiedName~EvidenceInfrastructureTests
Sgol.IntegrationTests: FullyQualifiedName~ObligationQueryPersistenceTests
Sgol.ArchitectureTests: FullyQualifiedName~Front016ArchitectureTests|FullyQualifiedName~Front017ArchitectureTests|FullyQualifiedName~ArchitectureBoundaryTests.Repository
Sgol.EvidenceIntegrationTests: FullyQualifiedName~PrivateStorageAndRealScannerKeepNonCleanContentInQuarantine
Sgol.FrontendBrowserTests: FullyQualifiedName~Front017BrowserTests
```

| Comprobación | Resultado vigente |
|---|---|
| `dotnet build --no-restore --configuration Release` | PASS, 0 errores/advertencias, después de cambios de interfaz y pruebas |
| Unitarias enfocadas: FRONT-017/016, EvidenceApiEndpointTests, StructuredEvidencePayloadValidatorTests, SgolApiClientTests, Upload render, FRONT-002, dominio e infraestructura de evidencia | 207/207 PASS; 18 formularios, booleanos/UTC/UUID/referencias, intención vinculada y expirada/manipulada, errores/timeout, CSRF, tipos reales, firma y límites |
| ObligationQueryPersistenceTests con PostgreSQL real | 5/5 PASS; snapshot capturado ordenado/SUSTITUIDA, versión incoherente, autorización/cursor/historia y no-efecto |
| Arquitectura FRONT-016/017 y dirección de dependencias | 3/3 PASS; frontera API, alcance cerrado, ausencia de preview/descarga/conclusión/storage/logs |
| PrivateStorageAndRealScannerKeepNonCleanContentInQuarantine, SeaweedFS/ClamAV reales, `SGOL_EVIDENCE_EICAR_TESTS=true` | 1/1 PASS; PUT firmado, JPEG/PNG/PDF, almacenamiento privado, cuarentena, integridad y EICAR INFECTADO. Intento inicial sin opt-in rechazado; segundo intento local en archivo fue bloqueado por Defender; fixture corregido a INSTREAM en memoria y nueva ejecución correcta |
| FRONT-017 navegador, PostgreSQL/S3/ClamAV reales | 2/2 PASS en la cabeza local de implementación. Chromium escritorio 1440×900 y móvil emulado 390×844; positivo, negativos, CSRF, cuatro roles, tarea ajena/concluida, auditoría/rollback, repetición y respuesta perdida, no vínculo antes de LIMPIO y no-efecto de lecturas |
| `git diff --check` | PASS, sin errores de espacios; avisos de normalización LF/CRLF no son fallos |

Durante el desarrollo se corrigieron expectativas de UTC canónico, semilla sintética de recepción, sincronización de navegación del test y contador de intento de outbox del harness. Un build durante testhost activo falló por bloqueo de copia MSB3021; al terminar ese proceso, build nuevamente PASS. La revisión de capturas detectó un borde de error persistente después de recuperar una aportación confirmada; se limpia el error y se restaura el aviso/icono de éxito, con aserción de navegador específica. Ningún fallo se presenta como éxito. No se modificó una restricción productiva para acomodar un test.

## Capturas y revisión visual

Capturas locales sintéticas en `.artifacts/front-017`, ignoradas por Git; corresponden a la aplicación HTTPS funcionando con PostgreSQL y servicios reales, no a previsualización o maqueta externa. 19 capturas verificadas, con manifiesto de archivos/SHA-256 en FRONT_017_CAPTURAS.md. Revisión visual local realizada sobre escritorio LIMPIO/aporte/INVALIDO y móvil formulario/vacío; revisión del responsable aprobada el 2026-10-01. Estados: selección/vacío de requisito, formulario, condición sin resolver, error, pendiente de escaneo, LIMPIO y aportación.

Revisión proporcional PASS: normal, foco, deshabilitado, error, ocupado y vacío; contraste computado, teclado, foco al resultado/error, objetivo mínimo, texto ampliado 200 %, reflow de 320 px y movimiento reducido. El aumento por prueba de estilos no acredita zoom nativo ni lector de pantalla.

## Validaciones diferidas y límites

- Zoom nativo, lector de pantalla y dispositivos físicos del hito anterior siguen diferidos: requieren una sesión manual/dispositivo real y no se sustituyen por emulación.
- PUT firmado de WebKit Windows frente al S3 local HTTP desde página HTTPS: no obtuvo respuesta HTTP (HTTP observado 0; no evento RequestFailed); terminó como resultado no confirmado. No se relajó la firma ni se retransmitió. Validación diferida con proveedor TLS compatible; no se atribuye una causa raíz adicional ni compatibilidad Safari demostrada. `SGOL_FRONT017_WEBKIT=true` conserva el recorrido opt-in para esa comprobación.
- INFECTADO/ERROR_ESCANEO tienen mensajes/estados cerrados y pruebas de presentación/infraestructura; no se afirma un recorrido visual físico de cada veredicto o timeout real de 30 s.
- CORS SeaweedFS y privilegios gruesos conservan la limitación residual de Adenda 18 §25: la prueba local no demuestra aislamiento productivo completo.
- Publicación autorizada: pipeline remoto y suites integrales del PR en seguimiento; resultado resoluble en los checks de su cabeza. Si el turno termina con checks pendientes, activar y verificar heartbeat en este mismo chat. No se consideran aprobados antes de obtener el resultado vigente.

Revisión visual y autorización de publicación siguen siendo decisiones distintas. No se solicita ni ejecuta despliegue.
