# SGOL — Adenda 17 a F07: contrato de infraestructura segura de evidencia para `TECH-EVID-001`

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | `APROBADA ÍNTEGRAMENTE` |
| Fecha | 2026-09-05 |
| Tarea | `TECH-EVID-001 — S3 privado local/CI, escáner adaptado y corpus seguro` |
| Corte y épica | Infraestructura previa a `HU-025`; `CV-03` / `EP-07` |
| Efecto | Inserta y precisa el contrato ejecutable de `TECH-EVID-001` inmediatamente antes de `HU-025`, sin alterar el orden del backlog |
| Precedencia | `HU-024` reconocida como `Terminada` efectiva por la regla condicional de su registro: PR `#39`, commit `9543d417e0515b5de6bad5f10898cd262b44d1fb`, pipeline requerido `SUCCESS` run `33994007606`, aprobación humana, merge `8e534ddd4d6ca2ab6ce1a678beead9c2cbc7aec3`, ambos SHA ancestros de `origin/master`, PostgreSQL `17/17` y cero defectos bloqueantes conocidos |
| Conservación | Mantiene sin cambios los documentos F00–F07 aprobados, `Fuentes/`, `docs/design/logo.svg`, `docs/design/mapa-pantallas.md` y cambios ajenos del checkout |
| Aprobación | Aprobada íntegramente por el responsable el 2026-09-05 antes de iniciar la implementación |

La aprobación contractual autoriza exclusivamente la implementación descrita. No autoriza commit, publicación de rama, pull request ni merge, que permanecen como decisiones independientes.

## 2. Hechos aprobados y decisiones que cierra esta adenda

Las fuentes aprobadas ya exigen objetos S3-compatible privados, cuarentena, JPEG/PNG/PDF, tamaño máximo de 15 MiB, verificación de tipo real y SHA-256, escaneo antimalware y promoción lógica únicamente tras `LIMPIO`. También exigen contrato S3 propio, Worker interno no enrutable, secretos externos, logs sin contenido y pruebas con corpus sintético.

Esta adenda propone y, sólo después de su aprobación íntegra, decide los elementos que esas fuentes no cerraron: producto local/CI, adaptadores, topología, claves, operaciones, protocolo de escaneo, timeouts, recursos, validación estructural, corpus, configuración, telemetría, limpieza y límites con `HU-025`.

## 3. Resultado cerrado y exclusiones

`TECH-EVID-001` entrega una infraestructura técnica comprobable, sin flujo funcional: contratos propios del módulo `Evidence`, validación acotada, adaptadores SeaweedFS/S3 y ClamAV, composición reutilizable, entorno local/CI aislado y pruebas unitarias, de arquitectura e integración externa.

Quedan expresamente excluidos:

- `HU-025`, `HU-026`, `HU-022` y toda historia posterior de `CV-03` o `CV-04`;
- intenciones de carga, URLs firmadas, aportación, sustitución, descarga, asociación con obligaciones, revisión, conclusión o validación;
- endpoints, UI, permisos, autenticación o datos funcionales nuevos;
- `file_object`, `evidence_item`, `evidence_version`, `evidence_review_snapshot`, cualquier otra tabla y toda migración;
- réplica, respaldo, restauración y despliegue productivo;
- segundo Worker, broker, Redis, microservicio, scheduler o Kubernetes.

La infraestructura no crea evidencia ni afirma que un archivo pertenezca a una obligación. Sus pruebas usan únicamente objetos sintéticos efímeros.

## 4. Propiedad modular y dirección de dependencias

1. Se crea el módulo `Sgol.Evidence`, propietario de los contratos técnicos de almacenamiento, validación, hash, escaneo y resultado de inspección. No contiene ASP.NET Core, SDK S3, sockets de ClamAV, EF Core ni clases del proveedor.
2. Las implementaciones S3 y ClamAV pertenecen a infraestructura y dependen de `Sgol.Evidence`; el módulo no depende de ellas.
3. `Sgol.Web` y `Sgol.Worker` son raíces de composición. Esta tarea aporta una extensión de composición verificable, pero no la activa en los hosts productivos mientras no exista un consumidor funcional aprobado. Así, una configuración ausente no degrada ni bloquea los modos de Worker ya aceptados.
4. En el flujo futuro, `Sgol.Web` sólo podrá validar metadatos declarados y solicitar trabajo. La lectura real acotada, tipo real, estructura, SHA-256, escaneo y promoción pertenecen al Worker no enrutable.
5. `TECH-EVID-001` no registra un comando, job, tipo de outbox ni manejador sin productor real. `HU-025` deberá aprobar el productor y el consumidor; entonces reutilizará el `Sgol.Worker outbox`, su entrega al menos una vez y `outbox_event.id` como clave idempotente.
6. Los adaptadores no hacen reintentos ocultos. Una invocación realiza un intento de red por operación. Cuando exista el consumidor de `HU-025`, los reintentos serán los cinco intentos ya aprobados del outbox, con sus demoras de 1, 5, 15 y 60 minutos; no se agrega una segunda política multiplicativa.

## 5. Almacenamiento privado local y CI

### 5.1 Implementación aprobada por esta propuesta

Local y CI usan SeaweedFS `4.45`, imagen `chrislusf/seaweedfs:4.45@sha256:fc9f76fa993ad69966ffeb2f65d0318fcae39c6f8e20cf68ef7b3a5cb97769e5`, en modo `weed mini` con API S3 y volumen efímero. SeaweedFS se ejecuta como proceso separado y no se convierte en dependencia del dominio. Su licencia es Apache-2.0.

MinIO Community no se aprueba: a la fecha de esta adenda su repositorio está archivado, la distribución comunitaria es sólo código fuente, los binarios históricos no se mantienen y la licencia AGPL-3.0 exige una evaluación jurídica innecesaria para este alcance.

El cliente .NET es `AWSSDK.S3` `4.0.102.5`, Apache-2.0, centralizado y bloqueado por lockfile. Usa `ServiceURL`, firma SigV4 y direccionamiento path-style. No se usa SDK SeaweedFS/MinIO ni SDK de proveedor en el dominio.

Antes de incorporar la implementación, las dos imágenes y los paquetes resueltos deben pasar el gate de vulnerabilidades disponible en CI. Una vulnerabilidad conocida crítica o alta sin mitigación documentada bloquea el cambio; la selección de versión no equivale a aceptar vulnerabilidades futuras.

### 5.2 Aislamiento y acceso

- Existen exactamente dos buckets físicos y distintos: `sgol-evidence-quarantine` y `sgol-evidence-clean`. Los nombres son opciones no secretas y deben cumplir sintaxis S3; no pueden ser iguales.
- No existe identidad anónima. Una petición S3 sin firma o con credencial incorrecta para listar, leer, escribir o consultar un objeto debe recibir denegación.
- La identidad de la aplicación sólo recibe `Read`, `Write` y `List` limitados a ambos buckets. No recibe `Admin`, `WriteAcp`, capacidad de política pública ni borrado de bucket.
- Local publica el puerto S3 únicamente en `127.0.0.1`; CI lo mantiene en su red privada de servicios y sólo lo expone al runner. Nunca se publica hacia Internet.
- Se prohíben website hosting, CDN, ACL pública, listado público y URL permanente. `TECH-EVID-001` no implementa URLs firmadas.
- TLS es obligatorio fuera de local/CI. Dentro de local/CI se permite HTTP sólo cuando `AllowInsecureTransport=true`, el ambiente sea exactamente `Development` o `CI` y el endpoint sea loopback o nombre de la red privada de contenedores. Cualquier otra combinación falla al iniciar la composición.

### 5.3 Claves y operaciones mínimas

Cada objeto usa `v1/{aa}/{bb}/{token}`, donde `token` son 64 caracteres hexadecimales minúsculos producidos con 32 bytes de un generador criptográfico, y `aa`/`bb` son sus primeros cuatro caracteres. La clave no incluye nombre original, usuario, obligación, TAR, fecha, secuencia ni UUID v7. Tres colisiones consecutivas producen error cerrado; nunca se sobrescribe un objeto existente.

El contrato `IPrivateObjectStorage` expone únicamente:

- crear un objeto nuevo en cuarentena sin overwrite;
- obtener metadatos técnicos y abrir lectura acotada por bucket lógico y clave validada;
- copiar de cuarentena a limpio sin hacerlo público;
- eliminar una copia técnica concreta de cuarentena o una copia limpia parcial inconsistente; y
- comprobar disponibilidad autenticada de ambos buckets.

No expone listado funcional, ACL, bucket administration, URL, nombre físico de bucket ni credenciales. `Put`, lectura/copia y borrado observan cancelación. Conexión tiene timeout de 3 segundos; metadatos, copia y borrado, 10 segundos; transferencia, 30 segundos. El SDK usa cero reintentos automáticos.

Objeto ausente produce `ObjectNotFound`; objeto ya existente produce `ObjectAlreadyExists`; respuesta, tamaño, hash o metadato técnico inconsistente produce `ObjectIntegrityError`. Ninguno revela bucket, clave, URL o credencial en mensaje o log. Un error nunca se interpreta como ausencia recuperable ni como éxito.

### 5.4 Promoción y no efectos parciales

La promoción es lógica y de infraestructura: sólo una atestación inmutable de validación satisfactoria y resultado `LIMPIO` permite copiar al bucket limpio. Después de copiar, se relee el objeto limpio, se verifica tamaño y SHA-256 y sólo entonces se elimina la copia de cuarentena. Si cualquier paso falla, se elimina únicamente la copia limpia parcial cuando se pueda identificar con certeza y se conserva cuarentena; nunca se devuelve un recibo limpio.

`INFECTADO`, `INVALIDO` y `ERROR_ESCANEO` no invocan la operación de promoción. Una repetición con la misma clave y hash converge sobre el mismo resultado técnico; misma clave con hash distinto es inconsistencia terminal. El hash no deduplica archivos distintos.

Sin `file_object` no existe retención funcional ni limpieza programada. Esta tarea sólo elimina objetos sintéticos en `finally` al terminar las pruebas y expone la operación técnica concreta para que `HU-025` defina retención con estado persistido. No se registra un job de limpieza sin consumidor ni autoridad funcional.

## 6. Validación técnica y SHA-256

### 6.1 Límites comunes

- Tamaño real permitido: de `1` a `15,728,640` bytes inclusive. Se lee como máximo `15,728,641` bytes para detectar exceso y abortar.
- La lectura usa bloques de 64 KiB, cancelación en cada iteración y un archivo temporal privado, exclusivo y `DeleteOnClose`; nunca carga el archivo completo en memoria.
- El archivo temporal queda limitado a un elemento por procesamiento en esta tarea. El Worker futuro podrá aumentar concurrencia sólo mediante decisión y prueba de recursos.
- SHA-256 se calcula incrementalmente sobre los bytes exactos durante la primera lectura y se expresa como 64 caracteres hexadecimales minúsculos.
- Antes de escanear se relee el objeto inmutable desde cuarentena y se recalculan tamaño y hash. Una diferencia produce `INVALIDO`, no escaneo ni promoción.
- No se extrae texto, imagen, metadata funcional, formularios, adjuntos ni contenido incrustado. No se transforma el original y no existe deduplicación implícita.

### 6.2 Tipo declarado, extensión y tipo real

Sólo se aceptan las parejas `image/jpeg` con `.jpg` o `.jpeg`, `image/png` con `.png`, y `application/pdf` con `.pdf`, sin distinguir mayúsculas en extensión o tipo declarado. El nombre se reduce al basename sólo para comprobar la extensión; nunca forma parte de la clave o de los logs. Tipo declarado, extensión o firma discordante produce `INVALIDO`.

La validación estructural cerrada es:

- JPEG: `FF D8` inicial; secuencia de marcadores y longitudes dentro del archivo; al menos un marcador SOF válido, un SOS y `FF D9` terminal; ancho y alto positivos, cada uno hasta 20,000 y producto hasta 40,000,000 píxeles.
- PNG: firma exacta de ocho bytes; `IHDR` primero y de longitud 13; ancho y alto positivos con los mismos límites; longitudes y CRC de todos los chunks válidos; al menos un `IDAT`; un solo `IEND` terminal y sin bytes posteriores.
- PDF: cabecera exacta `%PDF-1.0` a `%PDF-1.7` o `%PDF-2.0` al inicio; al menos un objeto indirecto; `startxref` decimal dentro del archivo apuntando a una tabla `xref` o a un objeto `/Type /XRef`; `%%EOF` dentro de los últimos 1,024 bytes y sólo espacio ASCII después. No se interpretan streams ni objetos funcionales.

Todo desbordamiento, lectura truncada, longitud imposible, CRC incorrecto, estructura incompleta, archivo vacío o exceso de límites produce `INVALIDO` sin excepción libre hacia el llamador.

## 7. Escáner antimalware adaptado

### 7.1 Motor, ubicación y protocolo

El motor es ClamAV `1.5.4`, imagen oficial multi-arquitectura `clamav/clamav:1.5.4-debian@sha256:be3cb41d9833ce9ffb98f3d3e1483c35c0d87060c2bda3624d75fd28bbf0b3bd`, licencia GPL-2.0. El digest de índice incluye manifiestos nativos para `linux/amd64`, `linux/arm64` y `linux/ppc64le`. Se elige `1.5.4` porque corrige vulnerabilidades publicadas que afectan `1.5.3` y anteriores. ClamAV permanece como servicio/proceso separado; no se enlaza con dominio ni se distribuye como biblioteca SGOL.

`clamd` vive en la misma red privada que el Worker, sin exposición a Internet. Para la suite externa local, el puerto `3310` se enlaza sólo a `127.0.0.1`; en CI sólo es alcanzable desde la red de servicios. El Worker es el único host productivo futuro autorizado para escanear.

`IFileMalwareScanner` recibe un stream seekable ya validado y su tamaño, nunca bucket, clave, nombre original ni datos funcionales. El adaptador usa el protocolo `clamd` `zINSTREAM\0`, chunks máximos de 64 KiB con longitud de cuatro bytes big-endian y terminador de longitud cero. Conexión: 3 segundos. Respuesta total: 30 segundos. Máximo transmitido: 15 MiB. Toda espera y envío observan cancelación.

### 7.2 Recursos del servicio

El contenedor se limita a 3 GiB de memoria y 2 CPU; `MaxThreads=2`, `MaxQueue=4`, `StreamMaxLength=16M`, `MaxFileSize=16M`, `MaxScanSize=32M`, `MaxRecursion=4`, `MaxFiles=64` y `MaxScanTime=30000`. La base de firmas incluida se actualiza sólo por el mecanismo oficial del contenedor; el gate registra versión de motor y fecha de firmas, nunca contenido escaneado.

Archivos comprimidos, Office, ejecutables y cualquier tipo distinto de JPEG/PNG/PDF ya son `INVALIDO`; el escáner no amplía los tipos admitidos. Alcanzar un límite del motor no equivale a limpio.

### 7.3 Catálogo cerrado y denegación

`ScanResult` contiene exclusivamente el enum siguiente, `engineVersion` sanitizado y un `errorCode` allowlist sólo para `ERROR_ESCANEO`; nunca incluye la respuesta cruda:

| Resultado | Condición exacta |
|---|---|
| `LIMPIO` | Únicamente respuesta `stream: OK` completa tras transmitir el archivo validado |
| `INFECTADO` | Respuesta completa `stream: <firma> FOUND`; la firma no se propaga ni registra |
| `INVALIDO` | Validación técnica previa o verificación desde cuarentena fallida; no es una conclusión de ClamAV |
| `ERROR_ESCANEO` | Timeout interno, conexión rechazada/rota, indisponibilidad, límite de motor, respuesta `ERROR`, vacía, truncada, múltiple o desconocida |

No existe resultado pendiente en el adaptador ni “permitir por falla”. `ERROR_ESCANEO` e `INFECTADO` son terminales para el intento actual y permanecen en cuarentena. Un timeout interno se convierte en `ERROR_ESCANEO`; una cancelación solicitada por el llamador propaga `OperationCanceledException` para que el Worker revierta el intento y no invente un veredicto.

El adaptador realiza un intento. No reintenta TCP por sí mismo. El futuro consumidor de outbox repetirá el objeto inmutable conforme a la política única indicada en la sección 4; agotarla deja `ERROR_ESCANEO` y alerta, nunca `LIMPIO`.

## 8. Configuración cerrada, secretos y arranque

La composición usa opciones tipadas con `ValidateOnStart`. Las secciones y variables exactas son:

| Opción | Variable de entorno | Regla |
|---|---|---|
| `Evidence:Storage:Endpoint` | `Evidence__Storage__Endpoint` | URI absoluta; HTTPS salvo excepción local/CI de 5.2 |
| `Evidence:Storage:Region` | `Evidence__Storage__Region` | `us-east-1` en local/CI |
| `Evidence:Storage:QuarantineBucket` | `Evidence__Storage__QuarantineBucket` | requerido; distinto de limpio |
| `Evidence:Storage:CleanBucket` | `Evidence__Storage__CleanBucket` | requerido; distinto de cuarentena |
| `Evidence:Storage:AccessKey` | `Evidence__Storage__AccessKey` | secreto requerido, no vacío |
| `Evidence:Storage:SecretKey` | `Evidence__Storage__SecretKey` | secreto requerido, no vacío |
| `Evidence:Storage:AllowInsecureTransport` | `Evidence__Storage__AllowInsecureTransport` | `false` por defecto; excepción cerrada de 5.2 |
| `Evidence:Scanner:Host` | `Evidence__Scanner__Host` | requerido; loopback o red privada en local/CI |
| `Evidence:Scanner:Port` | `Evidence__Scanner__Port` | `3310`, rango válido TCP |
| `Evidence:Scanner:ConnectTimeoutSeconds` | `Evidence__Scanner__ConnectTimeoutSeconds` | exactamente `3` |
| `Evidence:Scanner:ScanTimeoutSeconds` | `Evidence__Scanner__ScanTimeoutSeconds` | exactamente `30` |

Tamaño, buffers, catálogo de tipos, política de claves y reintentos son invariantes de código, no configuración ampliable. Endpoint, credenciales, host o buckets faltantes, duplicados o inválidos impiden construir una composición de evidencia. No existe fallback en memoria, filesystem o scanner simulado en producción.

La configuración versionada contiene sólo nombres y valores no secretos. El script local y el workflow CI generan credenciales efímeras criptográficas para el proceso, generan fuera de Git la configuración limitada de SeaweedFS y la eliminan al terminar. Ningún valor predeterminado, `.env`, JSON generado, certificado privado o credencial se incorpora al repositorio. Local y CI usan nombres lógicos `sgol-evidence-s3` y `sgol-evidence-scanner`.

## 9. Salud, métricas y logs

- `EvidenceStorageHealthCheck` hace operaciones autenticadas de disponibilidad sobre ambos buckets sin listar claves; falla si falta uno, si son el mismo o si el proveedor no responde.
- `ClamAvHealthCheck` usa `zPING\0` y exige una única respuesta `PONG` dentro de 3 segundos.
- Esta tarea no crea un endpoint HTTP. Los checks se registran bajo la etiqueta `evidence-ready` sólo cuando la composición de evidencia es solicitada; `HU-025` decidirá su exposición sin modificar `/health/live`.
- `ActivitySource` y `Meter` usan `Sgol.Evidence`. Contadores: `sgol.evidence.quarantine.entered`, `sgol.evidence.quarantine.exited`, `sgol.evidence.scans` y `sgol.evidence.scan_failures`; histogramas: `sgol.evidence.storage.duration` y `sgol.evidence.scan.duration` en milisegundos.
- Etiquetas allowlist: `operation`, `mediaType`, `result`, `attempt` y `service`. Se prohíben clave, bucket físico, nombre original, hash, firma detectada, URL, host, puerto, credencial, contenido y excepción cruda.
- Logs JSON usan EventId estable y sólo `correlationId`, operación, duración y resultado cerrado. Errores externos se traducen a `errorCode` allowlist: `CONFIGURATION`, `TIMEOUT`, `UNAVAILABLE`, `PROTOCOL`, `INTEGRITY` o `CANCELLED`.

## 10. Corpus sintético y seguro

El corpus versionable se expresa mediante fábricas deterministas de bytes y un manifiesto de casos; no usa documentos reales ni contenido de `Fuentes/`:

1. JPEG mínimo válido, PNG mínimo válido y PDF mínimo válido, todos generados específicamente para SGOL y sin metadata personal.
2. Cada tipo con `Content-Type` o extensión discordante.
3. Firma inválida para cada tipo y truncamientos en cada estructura obligatoria.
4. Archivo vacío.
5. Archivo de `15,728,641` bytes generado durante la prueba y nunca guardado en Git.
6. PDF con `startxref` fuera de rango y PDF sin `%%EOF`.
7. Servidor TCP sintético que devuelve respuesta ClamAV desconocida, respuesta truncada, timeout e indisponibilidad.
8. EICAR sólo en la suite externa aislada, con `SGOL_EVIDENCE_EICAR_TESTS=true`. La cadena oficial se reconstruye en memoria a partir de fragmentos no contiguos, se escribe únicamente en un temporal privado, se envía a ClamAV y se destruye en `finally`. La cadena completa y su codificación no se almacenan en Git ni se registran.

No se incorporan malware real, datos reales, macros, Office, ZIP, ejecutables ni archivos de marca. Todas las pruebas limpian buckets, volúmenes y temporales incluso al fallar.

## 11. Pruebas posteriores a la aprobación

### 11.1 Sin Docker

- unitarias de límites `1..15 MiB`, lectura `+1`, cancelación y ausencia de buffering completo;
- JPEG/PNG/PDF válidos y todos los casos inválidos de la sección 10;
- SHA-256 minúsculo, estable y verificado nuevamente;
- claves criptográficas con formato cerrado, sin nombre original y sin valores predecibles inyectando entropía determinista de prueba;
- catálogo exacto de resultados y prueba de que excepción, timeout y respuesta desconocida jamás producen `LIMPIO`;
- opciones incompletas o transporte inseguro fuera de local/CI fallan de forma cerrada;
- logs, errores y telemetría no contienen secretos, contenido, nombre, clave, hash ni respuesta del scanner;
- arquitectura: adaptadores fuera del dominio, dependencias hacia `Sgol.Evidence`, Web/Worker como composición y ausencia de EF;
- no existen endpoints, páginas, permisos, entidades, tablas o servicios funcionales de `HU-025`.

### 11.2 Integración externa local/CI

Un proyecto de integración afectado, marcado para ejecución externa, levanta por Testcontainers genérico las dos imágenes fijadas y prueba:

- ambos buckets privados, acceso firmado satisfactorio y acceso anónimo/público denegado;
- separación física, no overwrite, ausencia, inconsistencia, copia limpia verificada y limpieza de parciales;
- timeouts, cancelación y exactamente un intento por invocación de adaptador;
- `LIMPIO`, `INFECTADO` mediante EICAR opt-in, `ERROR_ESCANEO` por timeout/caída/respuesta desconocida e `INVALIDO` por validación previa;
- ningún resultado distinto de `LIMPIO` crea objeto en el bucket limpio;
- health checks de S3 y ClamAV y configuración reproducible sin secretos versionados.

La suite externa no usa PostgreSQL. El desarrollador la ejecuta una sola vez fuera de la sesión y comunica comando, total, errores, omitidas, duración, versiones de imágenes y fecha de firmas. Una prueba EICAR omitida, una imagen distinta o un acceso anónimo permitido deja el gate pendiente.

## 12. Archivos previstos después de la aprobación

La implementación podrá modificar únicamente:

- nuevo `src/Modules/Evidence/` y su proyecto;
- infraestructura de evidencia aislada bajo `src/Sgol.Web/Infrastructure/Evidence/`, sin endpoints;
- composición reutilizable directamente necesaria en Web/Worker, sin activar consumidor funcional;
- proyectos de pruebas unitarias, arquitectura e integración directamente afectados y el corpus sintético;
- manifiesto local/CI y scripts mínimos de arranque/limpieza de evidencia;
- `Directory.Packages.props`, lockfiles y solución para las dependencias aprobadas;
- documentación operativa mínima, `docs/traceability/README.md`, `docs/traceability/IMPLEMENTATION_STATUS.md` y esta adenda ya aprobada.

No se modifica `SgolDbContext`, el snapshot EF ni el inventario de migraciones. Si durante la implementación apareciera una necesidad real de persistencia, se detiene el trabajo y se solicita otra aprobación explícita con tabla, columnas, FK, restricciones e índices; esta adenda no la autoriza.

## 13. Gates y cierre

Después de la aprobación y de implementar, se ejecutan una sola vez y en el orden indicado por el prompt de `TECH-EVID-001`: restore locked fuera del aislamiento, build Release, unitarias completas, arquitectura completa, enfocadas sin Docker, suite S3/ClamAV externa, formato, vulnerabilidades, evidencia de modelo EF sin cambios, protección de `Fuentes/`, espejo de `Fuentes/` y `git diff --check`. Los dos gates de `Fuentes/` son secuenciales.

La rama registra `TECH-EVID-001` como propuesta. Ese registro adquiere estado efectivo `Terminada` en `master`, sin commit administrativo posterior, sólo cuando el commit exacto tenga pipeline requerido verde, aprobación humana, merge, ascendencia en `origin/master`, suite externa S3/ClamAV satisfactoria, PostgreSQL satisfactorio sólo si una adenda posterior autorizara persistencia y cero defectos bloqueantes.

La aprobación de esta adenda no autoriza iniciar `HU-025` ni omitir sus propias decisiones funcionales.

## 14. Decisión solicitada

Se solicita aprobar o rechazar íntegramente esta propuesta. No se aceptan como aprobación el nombre del archivo, recomendaciones previas, memoria de chat, disponibilidad local de SeaweedFS/ClamAV ni el inicio de una revisión. Cualquier cambio material en proveedor, digest, licencia, buckets, clave, protocolo, resultado, límite, retry, persistencia o frontera Web/Worker requiere una revisión agrupada antes de editar implementación.

## 15. Revisión multi-arquitectura aprobada

La ejecución externa del 2026-09-05 demostró que el digest Alpine inicialmente aprobado no contenía manifiesto `linux/arm64/v8`: SeaweedFS inició y la creación del contenedor ClamAV falló antes de ejecutar aserciones. Para eliminar la dependencia de emulación y conservar ejecución nativa tanto en el host local ARM64 como en CI AMD64, se sustituye exclusivamente:

- anterior: `clamav/clamav:1.5.4@sha256:f0954d679017eb6d48221e2b2be3ac5457bf278a844f39b672376f55a085f591`;
- aprobado: `clamav/clamav:1.5.4-debian@sha256:be3cb41d9833ce9ffb98f3d3e1483c35c0d87060c2bda3624d75fd28bbf0b3bd`.

La revisión fue aprobada íntegramente por el responsable el 2026-09-05. No cambia motor, versión, licencia, protocolo `zINSTREAM`, puerto privado, límites, configuración, catálogo de resultados, política de denegación, corpus, alcance ni demás decisiones de esta adenda.

La reejecución externa posterior resultó satisfactoria: `1/1`, 0 errores, 0 omitidas y 41.4 s. La prueba cubrió el manifiesto nativo, S3 privado, separación de cuarentena, promoción limpia, duplicado, inconsistencia, health checks y EICAR aislado. La salida comunicada no incluyó la fecha de las firmas empacadas; ese dato operativo permanece no capturado y no se inventa en el registro.
