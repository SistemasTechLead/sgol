# Infraestructura privada de evidencia para local y CI

`TECH-EVID-001` aporta la infraestructura técnica y `HU-025` la consume para intenciones privadas, inspección y evidencia versionada. No existe UI, descarga, acceso público, conclusión, evaluación de completitud ni validación.

## Servicios fijados

- S3-compatible: `chrislusf/seaweedfs:4.45@sha256:fc9f76fa993ad69966ffeb2f65d0318fcae39c6f8e20cf68ef7b3a5cb97769e5`.
- Escáner multi-arquitectura: `clamav/clamav:1.5.4-debian@sha256:be3cb41d9833ce9ffb98f3d3e1483c35c0d87060c2bda3624d75fd28bbf0b3bd` (`linux/amd64`, `linux/arm64` y `linux/ppc64le`).
- Cliente S3: `AWSSDK.S3` `4.0.102.5`.

La suite `Sgol.EvidenceIntegrationTests` crea ambos contenedores, red y credenciales efímeras. SeaweedFS recibe una identidad de aprovisionamiento separada y otra identidad de SGOL limitada a `Read`, `Write` y `List` para `sgol-evidence-quarantine` y `sgol-evidence-clean`. No se configura identidad anónima y una lectura HTTP sin firma debe responder `403`.

El servicio `clamd` sólo publica TCP `3310` hacia el proceso de prueba. Su contenedor fija `StreamMaxLength=16M`, `MaxFileSize=16M`, `MaxScanSize=32M`, `MaxRecursion=4`, `MaxFiles=64`, `MaxScanTime=30000`, `MaxThreads=2` y `MaxQueue=4`; la actualización de firmas queda desactivada durante la ejecución reproducible y se usa la base incluida en la imagen fijada.

## Configuración de SGOL

La composición futura debe invocar `AddSgolEvidenceInfrastructure` y aportar exclusivamente mediante configuración externa:

```text
Evidence__Storage__Endpoint
Evidence__Storage__Region
Evidence__Storage__QuarantineBucket
Evidence__Storage__CleanBucket
Evidence__Storage__AccessKey
Evidence__Storage__SecretKey
Evidence__Storage__AllowInsecureTransport
Evidence__Storage__AllowedUploadOrigins__0
Evidence__Scanner__Host
Evidence__Scanner__Port
Evidence__Scanner__ConnectTimeoutSeconds
Evidence__Scanner__ScanTimeoutSeconds
```

No hay valores secretos versionados. `AllowedUploadOrigins` requiere al menos un origen absoluto sin ruta, query, fragmento, duplicados ni wildcard. En producción cada origen usa HTTPS; `Development` y `CI` admiten únicamente loopback o red privada. HTTP para S3 sólo se acepta con `AllowInsecureTransport=true` en esos dos ambientes y contra destino privado. La identidad efímera de aprovisionamiento configura una sola regla CORS `sgol-evidence-upload` sobre cuarentena; la credencial operativa sólo la verifica antes de la primera firma y falla cerrada si difiere. Nunca intenta aplicar o reparar CORS ni configura el bucket limpio. Web conserva disponibles las rutas ajenas y falla cerrada al activar evidencia; el Worker falla cerradamente al procesar el manejador o job de evidencia.

SeaweedFS 4.45 protege `PutObject`, `PutBucketCors` y `DeleteBucketCors` con la misma acción gruesa `Write`. La suite no puede demostrar denegación de CORS a la identidad operativa sin impedir también la carga: verifica en cambio que el código productivo no contiene llamadas CORS mutantes, que el aprovisionamiento usa otra identidad y que toda deriva produce fallo cerrado. Antes de producción debe registrarse un control externo que separe esas operaciones o la aceptación explícita del riesgo residual; HU-025 no habilita el despliegue.

## Flujo HU-025

1. `POST /api/v1/files/upload-intents` crea `file_object` y devuelve un `PUT` firmado por diez minutos para una clave exacta de cuarentena.
2. `POST /api/v1/files/{id}/complete` compara longitud y metadatos firmados y agrega `EVIDENCE.FILE_INSPECTION_REQUESTED.V1` al outbox en la misma transacción que auditoría e idempotencia.
3. El único `Sgol.Worker outbox` relee como máximo 15 MiB + 1 byte, valida tipo real y estructura, calcula SHA-256 y consulta ClamAV. Sólo `LIMPIO` se copia al bucket limpio y se verifica de nuevo.
4. El aporte o sustitución vincula exclusivamente un objeto limpio no expirado. `evidence_item` conserva el requisito del snapshot; `evidence_version` conserva toda la cadena y una sola versión `VIGENTE`.
5. `Sgol.Worker run-job --job CLEAN_EXPIRED_EVIDENCE_UPLOADS --scheduled-for <instante-UTC>` limpia lotes de hasta 100 objetos técnicos no vinculados. Nunca borra ítems, versiones ni objetos funcionales.

Las cuatro mutaciones validan antiforgery mediante la cabecera `X-CSRF-TOKEN` y la cookie `__Host-SGOL-CSRF` segura, HttpOnly y SameSite Strict. HU-025 no agrega una ruta de emisión: el par pertenece a la frontera transversal de sesión y las pruebas lo obtienen directamente de `IAntiforgery` dentro del host de prueba.

La migración `20260907203912_AddVersionedEvidenceContribution` crea únicamente `file_object`, `evidence_item` y `evidence_version`, con FK `RESTRICT`, checks, índices parciales y triggers que bloquean borrado, mutaciones históricas, snapshots incoherentes y vínculos con archivos no limpios.

## Ejecución externa

Desde la raíz y fuera del aislamiento de Codex:

```powershell
$env:SGOL_EVIDENCE_EICAR_TESTS = "true"
dotnet test tests/Sgol.EvidenceIntegrationTests/Sgol.EvidenceIntegrationTests.csproj --configuration Release --filter "Category=EvidenceExternal"
Remove-Item Env:SGOL_EVIDENCE_EICAR_TESTS
```

La prueba genera JPEG, PNG, PDF y EICAR únicamente en memoria. El archivo sobredimensionado también se genera durante la prueba unitaria; ninguno se almacena como binario en Git. La suite demuestra PUT firmado limitado a cuarentena, buckets privados, duplicado rechazado, promoción sólo tras `LIMPIO` y resultado real `INFECTADO` de ClamAV. El 2026-09-07 el desarrollador ejecutó la suite externa final: `1/1`, cero errores, cero omitidas y cero advertencias, en 37.9 s.

La suite PostgreSQL afectada se ejecuta también fuera del aislamiento:

```powershell
dotnet test tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~EvidenceContribution|FullyQualifiedName~PostgreSqlPersistenceTests"
```

El 2026-09-07 el desarrollador ejecutó este corte PostgreSQL: `6/6`, cero errores, cero omitidas y cero advertencias, en 55.2 s.

## Operación y observabilidad

Los health checks `evidence-storage` y `evidence-scanner` se registran sólo cuando un consumidor compone la infraestructura; no se agrega endpoint de salud en esta tarea. Las métricas `sgol.evidence` sólo contienen operación, duración y resultado cerrado. No se registran contenido, nombres originales, claves de objeto, credenciales, endpoints, respuestas crudas, cadenas de conexión ni payloads.

Las métricas añaden intenciones, confirmaciones, inspecciones, promociones, vínculos, sustituciones y rechazos con etiquetas cerradas. Ni la limpieza técnica ni otro flujo de `HU-025` realizan borrado funcional de evidencia.
