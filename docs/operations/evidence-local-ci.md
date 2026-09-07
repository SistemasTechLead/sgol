# Infraestructura privada de evidencia para local y CI

`TECH-EVID-001` incorpora únicamente infraestructura técnica. No habilita intenciones de carga, URLs firmadas, endpoints, UI, asociación con obligaciones ni entidades de evidencia.

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
Evidence__Scanner__Host
Evidence__Scanner__Port
Evidence__Scanner__ConnectTimeoutSeconds
Evidence__Scanner__ScanTimeoutSeconds
```

No hay valores secretos versionados. HTTP sólo se acepta con `AllowInsecureTransport=true` en `Development` o `CI` y contra loopback o una dirección privada; en los demás ambientes se exige HTTPS. Las opciones faltantes o contradictorias fallan al iniciar el consumidor.

## Ejecución externa

Desde la raíz y fuera del aislamiento de Codex:

```powershell
$env:SGOL_EVIDENCE_EICAR_TESTS = "true"
dotnet test tests/Sgol.EvidenceIntegrationTests/Sgol.EvidenceIntegrationTests.csproj --configuration Release --filter "Category=EvidenceExternal"
Remove-Item Env:SGOL_EVIDENCE_EICAR_TESTS
```

La prueba genera JPEG, PNG, PDF y EICAR únicamente en memoria. El archivo sobredimensionado también se genera durante la prueba unitaria; ninguno se almacena como binario en Git. La suite demuestra buckets privados, cuarentena separada, duplicado rechazado, promoción sólo tras `LIMPIO` y resultado real `INFECTADO` de ClamAV.

## Operación y observabilidad

Los health checks `evidence-storage` y `evidence-scanner` se registran sólo cuando un consumidor compone la infraestructura; no se agrega endpoint de salud en esta tarea. Las métricas `sgol.evidence` sólo contienen operación, duración y resultado cerrado. No se registran contenido, nombres originales, claves de objeto, credenciales, endpoints, respuestas crudas, cadenas de conexión ni payloads.

La limpieza técnica futura puede eliminar temporales y objetos huérfanos de cuarentena mediante el Worker y outbox existentes con reintentos acotados, pero `TECH-EVID-001` no crea un job sin consumidor ni realiza borrado funcional de evidencia.
