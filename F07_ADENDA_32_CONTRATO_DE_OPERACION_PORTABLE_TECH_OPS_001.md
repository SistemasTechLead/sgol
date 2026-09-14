# F07 Adenda 32 — Contrato de operación portable de TECH-OPS-001

## 1. Control de la propuesta

| Campo | Valor |
|---|---|
| Identificador | `F07_ADENDA_32` |
| Tarea | `TECH-OPS-001` — Imagen OCI, staging, respaldo y réplica verificables |
| Estado | Propuesta; requiere aprobación íntegra antes de crear Dockerfiles, manifiestos, scripts, pipelines o código operativo |
| Fecha | 2026-09-12 |
| Precedencia | Obligatoria antes de `HU-035` |
| Base aceptada | `HU-034`: PR `#53`, commit implementado `718608dd93ed9d6c7f08a97c6c7bebd47f4f9a42`, pipeline `SUCCESS` run `34717877842`, merge `df980342de63b4767866e7b34fb81431b4b73ab7` |
| Alcance | Imagen OCI única para Web, Worker y comandos técnicos; staging declarativo; exportación PostgreSQL portable; réplica S3-compatible; verificación técnica y runbooks reproducibles |
| Exclusión rectora | Sin reconciliador, permiso, endpoint, simulacro funcional ni comparación final de `HU-035`/`CA-035` |

Esta adenda no modifica F00–F07. Cierra solamente las decisiones ejecutables que los documentos aprobados dejaron abiertas para `TECH-OPS-001`. Mientras permanezca como propuesta no autoriza implementación.

## 2. Hechos documentados

1. F07 inserta `TECH-OPS-001` antes de `HU-035` y exige imagen OCI, manifiesto de staging, respaldo y réplica verificables.
2. `ADR-008` adopta imagen OCI, PostgreSQL y S3-compatible; DigitalOcean App Platform en `NYC` es la referencia inicial, no una dependencia de dominio.
3. F06 exige la misma imagen OCI para Web y trabajos, un Worker no enrutable, trabajos programados separados, imagen inmutable por digest, configuración no secreta versionada y secretos externos.
4. La portabilidad se acredita mediante OCI sin buildpack propietario, PostgreSQL estándar, S3 detrás de contrato propio, variables/secretos, `pg_dump`, copia verificable de objetos e infraestructura/procedimientos como código.
5. `ADR-010`, `NFR-005` y seguridad/operación fijan RPO máximo de una hora, RTO máximo de cuatro horas, exportación PostgreSQL diaria, réplica de objetos cada hora, manifiesto y SHA-256 diario y simulacro trimestral.
6. La copia PostgreSQL portable debe cifrarse antes de salir del proceso; el restore se hace sobre un destino nuevo o aislado, nunca sobre la única copia.
7. No hay purga automática durante el piloto. Evidencia, auditoría, versiones, respaldos confirmados y objetos funcionales no se eliminan para ocultar diferencias.
8. Las credenciales de objetos primaria y de respaldo son distintas; producción y staging no comparten secretos.
9. `CAP-047` distingue punto de recuperación, respaldo, restauración, rollback y compensación; no autoriza reescribir historia.
10. `HU-035`/`CA-035` exige después reconciliar IDs, vínculos, versiones, conteos, evidencia y auditoría. Una diferencia debe ser visible y no puede fabricarse información para ocultarla.
11. El repositorio ya aporta `Sgol.Web`, `Sgol.Worker`, `ScheduledJobRunner`, bloqueo advisory PostgreSQL, unicidad por `(job_name,scheduled_for)`, outbox, logs JSON y métricas de baja cardinalidad.
12. Los comandos aceptados existentes son `Sgol.Worker outbox` y `Sgol.Worker run-job --job <NOMBRE> --scheduled-for <UTC-RFC3339-Z>`. Los jobs registrados incluyen `GENERATE_DUE_RECURRENCES` y `CLEAN_EXPIRED_EVIDENCE_UPLOADS`.
13. Web ya expone `/health/live`; F06 exige además readiness con base y almacenamiento. La composición actual no expone esa readiness completa.
14. La configuración vigente de evidencia usa PostgreSQL, endpoint/región/buckets S3, identidad S3 operativa, orígenes permitidos y host/puerto/timeouts del escáner.

## 3. Ambigüedades que impiden implementar sin decisión

1. No se define si el manifiesto es un App Spec de DigitalOcean, Compose, un formato propio o varios manifiestos equivalentes.
2. No se decide qué partes pueden depender de DigitalOcean ni cómo se prueba la ruta de salida.
3. No se fija familia de imagen base, digest, plataformas, paquetes de sistema ni política de actualización.
4. No se establecen los comandos de contenedor exactos para Web, Worker, migración, respaldo, réplica y verificaciones.
5. No se fijan UID/GID, filesystem de sólo lectura, directorios temporales, puerto, liveness/readiness ni tratamiento de Worker y jobs sin HTTP.
6. No existe un inventario contractual que separe configuración no secreta, nombres de secretos y credenciales por responsabilidad.
7. F06 asigna SBOM a Desarrollo, pero no decide formato, procedencia, firma, gate ni qué requiere un registro externo.
8. `pg_dump` diario no tiene formato, versión compatible, cifrado, nombre, manifiesto, integridad, atomicidad ni semántica de retención ejecutable.
9. La réplica horaria no define buckets, algoritmo incremental, manifiesto, verificación real, reintentos, locking, idempotencia, corrupción, faltantes ni borrados.
10. No se decide quién programa las frecuencias ni cómo se representa la programación sin crear recursos externos.
11. No se delimita qué evidencia técnica pertenece a `TECH-OPS-001` y qué reconciliación queda reservada para `HU-035`.
12. No se reparte qué pruebas son estáticas/locales, cuáles requieren Docker y cuáles requieren PostgreSQL/S3-compatible externos.
13. La persistencia y cifrado del key ring de Data Protection están exigidos por F06, pero la composición actual sólo registra Data Protection sin almacenamiento durable ni clave de envoltura.

Estas decisiones afectan seguridad, portabilidad, operación observable y pruebas. No deben resolverse silenciosamente en código.

## 4. Inferencias limitadas

1. La referencia a DigitalOcean permite un adaptador de despliegue, pero no autoriza SDK, buildpack, formato de datos o credencial propietaria dentro de SGOL.
2. “Verificable” requiere evidencia producida por máquina y fallo cerrado; un procedimiento narrativo o la mera existencia de una copia no basta.
3. El RPO/RTO completo sólo puede demostrarse mediante el simulacro de `HU-035`; `TECH-OPS-001` debe dejar probados los componentes técnicos sin afirmar el resultado funcional final.
4. La misma imagen debe contener todas las publicaciones SGOL y utilidades necesarias para sus comandos; no implica que Web reciba secretos de respaldo ni que cada proceso habilite todos los componentes.
5. La unicidad de corrida existente resuelve el mismo slot. Respaldo y réplica requieren además exclusión mutua por tipo de job para impedir solapamiento entre slots distintos.

Estas inferencias se vuelven decisiones sólo mediante la aprobación íntegra de esta adenda.

## 5. Entregables exactos

La implementación aprobada entregará únicamente:

1. `Dockerfile` multi-stage y `.dockerignore` en la raíz.
2. `deploy/staging/compose.yaml`, manifiesto normativo de staging basado en Compose Specification, sin valores secretos.
3. `deploy/staging/staging.env` con configuración no secreta y `deploy/staging/secrets.example` con nombres y marcadores inequívocos, nunca valores utilizables.
4. Un componente técnico pequeño para configuración operativa, backup, réplica, migración y verificaciones, compuesto desde `Sgol.Worker`/una consola `Sgol.Operations` sin entrar al dominio.
5. Validadores reproducibles bajo `scripts/ci/` para OCI/manifest/configuración/secretos y un gate externo orquestado bajo `scripts/operations/` para imagen, PostgreSQL, S3 y restore aislado.
6. Runbooks bajo `docs/operations/` para construir/verificar imagen, desplegar staging, crear/verificar respaldo, verificar réplica y restaurar en aislamiento.
7. Pruebas unitarias, contractuales, negativas y de arquitectura directamente asociadas.
8. Actualización de `SGOL.slnx`, archivos de proyecto/locks si son necesarios, workflow existente de PR sólo si el gate no puede incorporarse sin él, y `docs/traceability/IMPLEMENTATION_STATUS.md` en el mismo cambio de implementación.

No se crea un segundo manifiesto de proveedor. Una futura traducción del Compose normativo a DigitalOcean es mecánica y externa a dominio; si requiere semántica adicional deberá aprobarse antes del despliegue real.

## 6. Imagen OCI

### 6.1 Construcción y portabilidad

1. El `Dockerfile` usa stages separados de restore, publish y runtime; `dotnet restore --locked-mode` precede a `dotnet publish --no-restore`.
2. El stage de SDK usa `mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:<digest-real>` y runtime usa `mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:<digest-real>`. La implementación sustituye ambos marcadores por manifest digests publicados por Microsoft y el validador rechaza tags sin digest o placeholders.
3. La plataforma obligatoria de cierre es `linux/amd64`. El Dockerfile no incorpora instrucciones exclusivas de DigitalOcean y queda preparado para añadir `linux/arm64` sólo después de gate equivalente; no se afirma soporte arm64 en esta tarea.
4. Los únicos paquetes de runtime permitidos son certificados CA, `tzdata`, cliente PostgreSQL compatible con el servidor soportado y la herramienta de cifrado `age`. Versiones y repositorios quedan fijados; agregar otro paquete exige justificarlo en el diff.
5. La imagen contiene las publicaciones Release de Web, Worker, Operations y migraciones EF ya incorporadas. No contiene SDK .NET, código fuente, repositorio Git, tests, caches NuGet, credenciales ni datos.
6. Reproducibilidad significa misma revisión, argumentos documentados, lockfiles, digests base y versiones de herramientas producen el mismo conjunto funcional inspeccionable y una procedencia equivalente. No se promete identidad byte a byte entre builders distintos si el formato OCI incorpora metadatos del builder.
7. Etiquetas OCI mínimas: `org.opencontainers.image.revision`, `version`, `source`, `created` y digests base. `created` deriva de un argumento explícito UTC y no de la hora implícita del build.

### 6.2 Usuario, filesystem y red

1. Todos los procesos finales usan UID/GID numérico `1654:1654`; la imagen no cambia a root en runtime.
2. `WORKDIR=/app`, archivos propiedad root y no escribibles por UID 1654. El manifiesto monta root filesystem read-only, elimina capabilities y activa `no-new-privileges`.
3. Sólo `/tmp` es `tmpfs` con límite de 256 MiB, `noexec`, `nosuid` y `nodev`. Los respaldos temporales usan un subdirectorio creado con permisos `0700`, cuota lógica y eliminación sólo de temporales de la corrida actual.
4. Web escucha HTTP interno en `8080`; TLS termina en la plataforma. Worker y jobs no publican puertos.
5. La imagen no incluye shell de entrada como mecanismo de control. El comando por defecto es Web y cada servicio sobrescribe `command` explícitamente.

### 6.3 Comandos exactos

| Proceso | Comando dentro de la imagen |
|---|---|
| Web | `dotnet Sgol.Web.dll` |
| Worker outbox | `dotnet Sgol.Worker.dll outbox` |
| Recurrencia | `dotnet Sgol.Worker.dll run-job --job GENERATE_DUE_RECURRENCES --scheduled-for <UTC-RFC3339-Z>` |
| Limpieza técnica ya aprobada | `dotnet Sgol.Worker.dll run-job --job CLEAN_EXPIRED_EVIDENCE_UPLOADS --scheduled-for <UTC-RFC3339-Z>` |
| Backup portable | `dotnet Sgol.Worker.dll run-job --job POSTGRESQL_PORTABLE_BACKUP --scheduled-for <UTC-RFC3339-Z>` |
| Réplica de objetos | `dotnet Sgol.Worker.dll run-job --job REPLICATE_EVIDENCE_OBJECTS --scheduled-for <UTC-RFC3339-Z>` |
| Migración expand-only | `dotnet Sgol.Operations.dll migrate --expected-migration <ID>` |
| Probe de contenedor | `dotnet Sgol.Operations.dll probe-http --url http://127.0.0.1:8080/health/live` |
| Verificación de backup/restore | `dotnet Sgol.Operations.dll verify-postgresql-backup --manifest <URI-S3-PRIVADA>` |
| Verificación de réplica | `dotnet Sgol.Operations.dll verify-object-replica --manifest <URI-S3-PRIVADA>` |

`<UTC-RFC3339-Z>`, `<ID>` y `<URI-S3-PRIVADA>` son argumentos de ejecución, no configuración silenciosa. La URI sólo identifica bucket/key privados y nunca contiene credenciales ni firma.

## 7. Salud y fallo cerrado

1. `/health/live` conserva liveness sin dependencias y respuesta mínima; no revela versión, host, configuración ni excepción.
2. Se agrega `/health/ready`, que prueba conexión PostgreSQL y acceso mínimo no mutante al almacenamiento S3 primario. Responde `200` listo o `503` no listo con `status` y `correlationId`, sin nombres de servidor/bucket, latencias por dependencia, secretos o stack trace.
3. El `HEALTHCHECK` de imagen usa sólo liveness. El manifiesto usa readiness para Web y deshabilita health HTTP para Worker/jobs.
4. Web, Worker y cada job validan al inicio únicamente su configuración requerida. Ausencia, placeholder, endpoint inseguro no autorizado, buckets iguales donde deban ser distintos o credenciales reutilizadas produce salida distinta de cero antes de efectos.
5. Un fallo de health no ejecuta migración, reparación, copia, borrado ni bootstrap.

## 8. Manifiesto declarativo de staging

1. `deploy/staging/compose.yaml` es la representación normativa y portable. Se valida con `docker compose config`, además de validadores propios de seguridad y secretos.
2. La imagen se recibe sólo como `SGOL_IMAGE_REF=<registro>/<repositorio>@sha256:<64-hex>`; tag, digest vacío, digest mutable o build local implícito se rechaza.
3. Declara Web, Worker outbox y perfiles one-shot para migración, recurrencia, limpieza, backup, réplica y verificaciones. Todos usan exactamente `SGOL_IMAGE_REF`.
4. Declara redes, puerto interno, health checks, usuario, filesystem read-only, tmpfs, restart policy y límites iniciales. No crea PostgreSQL, S3, scanner ni secretos reales de staging.
5. Una extensión `x-sgol-schedules` documenta slots UTC y propietario, pero Compose no finge ser scheduler. El operador técnico configura el scheduler de la plataforma y verifica que el comando/render coincida con el manifiesto.
6. DigitalOcean queda limitado a ser el primer destino posible y terminación TLS/secret store/scheduler administrados. No hay App Spec, SDK, resource ID, región codificada en aplicación ni aprovisionamiento cloud en esta tarea.

## 9. Programación y propiedad

| Job | Slot contractual | Propietario de programarlo | Identidad de slot |
|---|---|---|---|
| `REPLICATE_EVIDENCE_OBJECTS` | Cada hora, minuto `05`, UTC | Operador técnico | hora UTC truncada |
| `POSTGRESQL_PORTABLE_BACKUP` | Diario, `02:15:00Z` | Operador técnico | día UTC |
| `GENERATE_DUE_RECURRENCES` | Conserva la cadencia ya aprobada; esta adenda no la cambia | Operador técnico | argumento ya contratado |
| `CLEAN_EXPIRED_EVIDENCE_UPLOADS` | Conserva la cadencia ya aprobada; esta adenda no la cambia | Operador técnico | argumento ya contratado |

La adenda versiona intención, comando y frecuencia de continuidad. No crea ni modifica un scheduler externo.

## 10. Configuración no secreta versionada

`staging.env` contiene valores no secretos reales o marcadores de despliegue inequívocos para:

- `ASPNETCORE_ENVIRONMENT=Staging`, `ASPNETCORE_URLS=http://+:8080`, `TZ=Etc/UTC` y desactivación de diagnósticos interactivos;
- `SGOL_IMAGE_REF` sólo como referencia requerida por digest;
- endpoint/región/buckets/orígenes permitidos y transporte seguro de `Evidence__Storage__*`, excepto credenciales;
- host/puerto/timeouts de `Evidence__Scanner__*`;
- nombre de aplicación y ubicación lógica del key ring de `DataProtection__*`, excepto material criptográfico;
- endpoint/región/bucket/prefix y recipient público `age` de `Backup__*`;
- endpoints/regiones, mapeo exacto de buckets y prefixes de `Replica__Source__*`/`Replica__Destination__*`;
- máximo de tres intentos, backoff acotado, tamaños de lote, límites temporales y slots UTC;
- endpoint del colector de telemetría si existe, nunca headers de autenticación.

Los nombres de bucket, endpoints privados y recipient público no se consideran secretos, pero no se escriben en logs de operación ordinaria.

## 11. Inventario de secretos y separación de credenciales

`secrets.example` enumera sólo estos nombres y el texto literal `REQUIRED_EXTERNAL_SECRET`; el validador rechaza cualquier otro valor:

| Responsabilidad | Secretos externos |
|---|---|
| Aplicación PostgreSQL | `ConnectionStrings__Sgol` |
| Aplicación S3 primaria | `Evidence__Storage__AccessKey`, `Evidence__Storage__SecretKey` |
| Exportación PostgreSQL | `Backup__PostgreSql__ConnectionString` |
| Escritura de backups | `Backup__Storage__AccessKey`, `Backup__Storage__SecretKey` |
| Lectura S3 primaria para réplica | `Replica__Source__AccessKey`, `Replica__Source__SecretKey` |
| Escritura/lectura S3 secundaria | `Replica__Destination__AccessKey`, `Replica__Destination__SecretKey` |
| Key ring | `DataProtection__WrappingCertificate`, `DataProtection__WrappingCertificatePassword` |
| Restore aislado | `Restore__PostgreSql__ConnectionString`, `Restore__Encryption__Identity` |
| Telemetría, si aplica | `OTEL_EXPORTER_OTLP_HEADERS` |

Reglas obligatorias:

1. Ningún valor se versiona, hornea, imprime, incorpora a SBOM/procedencia ni pasa como argumento de proceso.
2. La conexión de exportación es distinta de la aplicación y se limita a lectura/metadata requerida por `pg_dump`; no crea, altera ni borra negocio.
3. Las identidades S3 primaria, lectora de réplica y secundaria son distintas. La secundaria no puede borrar ni sobrescribir objetos confirmados.
4. El recipient `age` es público y versionable. La identity privada sólo existe durante restore aislado y no se inyecta en Web, Worker outbox, backup o réplica.
5. Staging y producción usan valores distintos aunque conserven los mismos nombres.
6. El key ring se persiste en PostgreSQL y se cifra con certificado externo; un redeploy sin acceso al key ring o envoltura falla readiness antes de atender tráfico autenticado.

## 12. SBOM, procedencia, vulnerabilidades y firma

1. CI genera SBOM SPDX JSON para el layout OCI y procedencia BuildKit/SLSA asociada al digest construido. Ambos son artefactos del run, no se copian dentro de la imagen.
2. El gate local valida Dockerfile, digests, lockfiles, ausencia de secretos y manifiesto renderizado; no afirma haber construido una imagen.
3. El gate Docker externo construye `linux/amd64`, inspecciona usuario/capas/comandos/health, ejecuta smoke sin secretos y produce digest/SBOM/procedencia sin publicar.
4. CI ejecuta el mismo build y análisis de vulnerabilidades. Una vulnerabilidad crítica o alta explotable bloquea; una excepción requiere decisión separada con mitigación, responsable y vencimiento.
5. Firma del digest sólo ocurre después de una autorización específica para publicar en un registro. La firma es gate de liberación/staging real, no del commit local ni del PR de `TECH-OPS-001`. No se generan ni almacenan llaves de firma en este repositorio.
6. La imagen base se revisa semanalmente por vulnerabilidades y se reconstruye al menos mensualmente. Cambiar digest o paquete exige PR, SBOM/procedencia y gates completos; nunca se actualiza flotando por tag.

## 13. Backup PostgreSQL portable

### 13.1 Formato y cifrado

1. Se usa `pg_dump --format=custom --no-owner --no-privileges --compress=6` con cliente de la misma major soportada que el servidor o una major posterior compatible documentada.
2. El dump fluye directamente a `age` con recipient X25519 aprobado; no existe dump plaintext persistente. El archivo confirmado termina en `.dump.age`.
3. Nombre inmutable: `postgresql/v1/YYYY/MM/DD/sgol-<scheduledFor-YYYYMMDDTHHMMSSZ>.dump.age`; manifiesto hermano con sufijo `.manifest.json`.
4. El manifiesto canónico UTF-8/JCS contiene `schemaVersion`, `kind`, `scheduledFor`, `createdAt`, revisión Git, digest OCI, versiones de PostgreSQL/`pg_dump`/`age`, formato, algoritmo, recipient fingerprint, byte count, SHA-256 del cifrado, key y estado `COMPLETE`. No contiene connection string, host, usuario, password, datos, identity privada ni argumentos secretos.
5. El hash se calcula sobre todos los bytes cifrados y se verifica después de cargar leyendo el objeto secundario. `pg_restore --list` tras descifrado valida estructura en el gate de backup.

### 13.2 Atomicidad, idempotencia, locking y retención

1. El nombre deriva del slot diario. Escritura final y manifiesto usan creación condicional; nunca sobrescriben una clave existente.
2. El job reutiliza `ScheduledJobRunner` y toma un segundo advisory lock estable por `POSTGRESQL_PORTABLE_BACKUP`, independiente de `scheduledFor`, para evitar solapamiento entre slots.
3. Una repetición del mismo slot verifica objeto/manifiesto ya confirmados y devuelve éxito recuperado; contenido distinto bajo la misma key produce fallo de integridad, sin overwrite.
4. Sólo después de `pg_dump=0`, `age=0`, hash local, carga completa, lectura secundaria y hash igual se confirma `COMPLETE` y éxito de job.
5. Una falla deja salida distinta de cero y no publica manifiesto de éxito. Temporales locales de esa corrida se eliminan; uploads multipart no confirmados se abortan. Un objeto final huérfano nunca se borra y puede completarse idempotentemente en el reintento si su hash coincide.
6. Se conserva al menos una copia diaria por 30 días. Esta tarea no implementa purga ni lifecycle destructivo; superar 30 copias no es error. Reducir retención requiere política y autorización posterior.

## 14. Restore y verificación aislada

1. La verificación mensual y el gate de cierre usan PostgreSQL nuevo, vacío y sintético. Nunca aceptan `ConnectionStrings__Sgol` como destino.
2. El comando exige una conexión `Restore__PostgreSql__ConnectionString`, verifica que no existan tablas SGOL y se niega a usar `--clean`, `DROP DATABASE`, `DROP SCHEMA` o sobrescritura.
3. Descarga manifiesto y cifrado privados, valida esquema/JCS, estado, key, tamaño y SHA-256; descifra en streaming y ejecuta `pg_restore --exit-on-error --no-owner --no-privileges`.
4. Luego ejecuta comprobaciones técnicas: migración esperada, capacidad de abrir DbContext, conteos estructurales no negativos, FK/constraints válidas, key ring legible y ausencia de secretos en salida.
5. Usa sólo datos sintéticos y produce evidencia JSON minimizada con digest, timestamps, duración, resultado y códigos estables.
6. No compara identidades, vínculos, versiones, conteos funcionales, evidencia y auditoría antes/después. Esa reconciliación y medición integral RPO/RTO corresponde exclusivamente a `HU-035`.

## 15. Réplica S3-compatible

### 15.1 Universo y semántica incremental

1. El universo son los buckets primarios de cuarentena y limpio ya aprobados, mapeados uno a uno a buckets secundarios distintos. Manifiestos y backups usan un bucket/prefix secundario separado.
2. Cada hora se listan objetos fuente por paginación. Para cada objeto se exige metadata `sgol-sha256` y tamaño; se transmite en streaming y se recalcula SHA-256.
3. Si destino no existe, se crea condicionalmente conservando bytes, media type, tamaño y hash permitido. Luego se relee destino y se recalcula SHA-256 antes de marcarlo verificado.
4. Si destino existe con el mismo hash/tamaño previamente verificado, el reintento es no-op. Si falta metadata, el hash fuente no coincide, destino difiere o la key cambia de contenido, la corrida falla visiblemente y no sobrescribe.
5. Una corrida diaria completa relee y recalcula SHA-256 de todos los objetos fuente/destino; las otras corridas pueden reutilizar el último manifiesto completo para objetos inmutables sin cambios de metadata/tamaño.
6. El manifiesto privado append-only contiene por objeto rol de bucket, key, tamaño, SHA-256 origen/destino, estado y timestamps. Keys pueden aparecer sólo en ese manifiesto privado, nunca como labels métricos o logs ordinarios.

### 15.2 Faltantes, borrados, retries y locking

1. Un objeto presente en un manifiesto anterior y ausente en origen se marca `SOURCE_MISSING`, hace fallar la corrida y permanece en destino. No se propaga borrado.
2. Un objeto ausente o corrupto en destino se marca `DESTINATION_MISSING`/`DESTINATION_CORRUPT`, hace fallar y no se reemplaza silenciosamente. La reparación requiere decisión humana o un comando explícito futuro no incluido.
3. Se reintentan como máximo tres veces sólo timeout, desconexión, throttling y respuestas 5xx, con backoff exponencial acotado y jitter. Autenticación, autorización, hash, metadata, 4xx no transitorio y faltantes no se reintentan como éxito.
4. El slot horario y manifiesto son idempotentes. La corrida reutiliza `ScheduledJobRunner` y toma advisory lock estable por `REPLICATE_EVIDENCE_OBJECTS` para impedir solapamiento entre slots.
5. El manifiesto se publica `COMPLETE` sólo cuando todo el universo termina verificado. Una corrida parcial conserva evidencia `FAILED` separada y salida distinta de cero; nunca presenta el subconjunto como réplica válida.

## 16. Telemetría y minimización

1. Logs JSON registran timestamp UTC, servicio, revisión/digest, operación, slot, intento, resultado, código de error y `correlationId` técnico.
2. Métricas permitidas: corridas, duración, resultado, bytes/objetos totales, antigüedad del último backup verificado y atraso de réplica. Labels se limitan a `operation`, `result`, `errorClass` y rol fijo de bucket.
3. Se prohíben como logs/labels: object key, bucket concreto, URI completa, host, actor funcional, ID de negocio, connection string, access key, hash individual, recipient, payload o contenido de evidencia.
4. Los manifiestos privados contienen el detalle necesario; consola y evidencia de CI sólo contienen agregados y digests de artefactos técnicos.
5. Errores usan códigos estables y preservan la excepción internamente sin imprimir secretos, stderr completo de herramientas o datos.

## 17. Evidencia verificable de TECH-OPS-001

La tarea sólo puede proponer cierre cuando existan:

1. digest del build OCI local/CI y reporte de inspección de usuario, puerto, comandos, health, filesystem y ausencia de secretos;
2. SBOM SPDX, procedencia y resultado de vulnerabilidades ligados al mismo digest;
3. `docker compose config` renderizado con valores sintéticos y referencia inmutable;
4. resultados unitarios/contractuales de configuración, fallo cerrado, locking, idempotencia, hash, reintentos, faltantes, corrupción, no-overwrite y no-delete;
5. ejecución externa con PostgreSQL real que produzca dump cifrado, manifiesto, restore en destino aislado y validación técnica;
6. ejecución externa con dos servicios/buckets S3-compatible sintéticos que copie incrementalmente, verifique SHA-256, recupere reintento y falle ante faltante/corrupción sin borrar;
7. runbooks ejecutados desde un checkout limpio con comandos y salidas minimizadas;
8. validadores de secretos, arquitectura/portabilidad, `Fuentes/` y rutas de diseño protegidas;
9. trazabilidad propuesta en el mismo cambio que la implementación.

Estas evidencias prueban prerrequisitos técnicos. No prueban `CA-035`, RPO/RTO integral trimestral, despliegue real, firma publicada ni recuperación funcional.

## 18. Reparto de pruebas y gates

### 18.1 Dentro de la sesión

- pruebas unitarias y de arquitectura nuevas;
- validación estática de Dockerfile, manifiesto, configuración y nombres de secretos;
- parser/contrato de comandos, manifiestos JCS, hashes, errores, retries, locking abstraído y no-efecto;
- restore/replica con dobles en memoria sólo para ramas puras, sin presentarlos como infraestructura real;
- gates .NET, vulnerabilidades NuGet, protección de `Fuentes/`, rutas de diseño y `git diff --check`.

### 18.2 A cargo del desarrollador fuera de la sesión

- build/inspect/smoke Docker real de la imagen `linux/amd64` y validación de Compose;
- PostgreSQL real para dump cifrado, `pg_restore` aislado, migración y locking concurrente;
- dos endpoints/buckets S3-compatible para copia, igualdad SHA-256, retries, faltantes, corrupción y no propagación de borrados;
- generación de SBOM/procedencia del layout OCI y escaneo de la imagen;
- cualquier prueba que requiera daemon Docker, Testcontainers o infraestructura externa.

Firma, push de imagen, registro, staging real, DigitalOcean, DNS y secretos reales no se ejecutan sin autorización separada.

## 19. Pruebas contractuales obligatorias

1. Dockerfile multi-stage usa SDK/runtime fijados por digest, restore locked, Release y no contiene secretos.
2. Imagen corre como `1654:1654`, no permite escalamiento, usa filesystem read-only y sólo Web expone `8080`.
3. Una imagen ejecuta todos los comandos exactos de la sección 6.3; comando inválido falla con uso minimizado.
4. Liveness no consulta dependencias; readiness falla ante PostgreSQL o S3 no disponible sin revelar detalle.
5. Compose valida, usa la misma referencia por digest, no contiene valores secretos y separa secretos por proceso.
6. Cada configuración requerida falta/corrupta/placeholder produce fallo antes de efectos.
7. Key ring persiste cifrado y un redeploy controlado puede leerlo; envoltura incorrecta falla cerrada.
8. Backup con datos sintéticos produce custom dump cifrado, manifiesto JCS, tamaño/hash iguales y `pg_restore --list` válido.
9. Restore a PostgreSQL nuevo funciona; destino no vacío o conexión primaria se rechaza sin cambios.
10. Fallo de `pg_dump`, cifrado, upload, hash o manifiesto no declara éxito parcial ni sobrescribe.
11. Dos ejecuciones concurrentes del mismo/diferente slot producen una sola corrida activa por tipo y un resultado idempotente recuperable.
12. Réplica copia incrementalmente cuarentena y limpio, preserva bytes/metadata permitida y acredita SHA-256 origen=destino.
13. Reintento después de interrupción no duplica ni sobrescribe; completa sólo artefactos coincidentes.
14. Fuente sin hash, objeto fuente faltante, destino faltante/corrupto o credencial cruzada produce fallo visible.
15. Ningún faltante fuente borra destino y ningún proceso posee operación de purga de objetos/backups confirmados.
16. Retry se limita a tres fallos transitorios y no convierte integridad/autorización en éxito.
17. Logs, métricas, SBOM, procedencia, manifiesto renderizado y evidencia pública no contienen secretos ni labels de alta cardinalidad.
18. Runbooks pueden repetirse con datos sintéticos y rutas temporales sin tocar la única copia.
19. Arquitectura confirma ausencia de SDK DigitalOcean, Kubernetes, microservicio, broker, Redis, endpoint funcional, UI y reconciliador `HU-035`.
20. `Fuentes/`, `docs/design/logo.svg`, `docs/design/mapa-pantallas.md` y cambios ajenos permanecen intactos.

## 20. Gates previstos

Al final de la implementación, una sola vez y después de pruebas enfocadas:

```text
rtk dotnet restore --locked-mode
rtk dotnet build --no-restore --configuration Release
rtk dotnet test --no-build --configuration Release
rtk dotnet format --no-restore --verify-no-changes
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File scripts/ci/Assert-NoVulnerablePackages.ps1
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File scripts/ci/verify-fuentes-mirror.ps1
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File scripts/ci/verify-fuentes-protection.ps1
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File scripts/ci/validate-tech-ops.ps1
rtk git diff --check
```

El gate externo consolidado ejecutará los controles Docker/PostgreSQL/S3/restore de la sección 18.2. Ningún resultado no ejecutado se presentará como aprobado.

## 21. Exclusiones

- reconciliador, permiso `PER-CONTINUIDAD-VER`, endpoint o reporte funcional de `HU-035`;
- comparación completa de `CA-035`, simulacro trimestral completo y declaración de RPO/RTO observado;
- UI o prueba de navegador;
- despliegue o recurso real en DigitalOcean/otro proveedor, App Spec, compra, dominio, DNS o login externo;
- valor o rotación de secretos reales, material criptográfico real, firma o publicación de imagen;
- datos reales o copia libre de producción;
- restore sobre origen/única copia, `--clean`, drop, rollback destructivo, compensación o fabricación de datos;
- overwrite o purga de evidencia, auditoría, versiones, respaldos, manifiestos u objetos confirmados;
- Kubernetes, microservicios, Redis, broker o SDK cloud dentro de dominio;
- cambios a reglas funcionales, outbox, jobs existentes o reconciliación más allá de componerlos;
- soporte `linux/arm64` no probado y alta disponibilidad real.

## 22. Riesgos y límites aceptados por la propuesta

1. Compose es portable pero no configura por sí solo scheduler, TLS ni secret store de DigitalOcean; el operador debe hacer una traducción verificable antes de staging real.
2. `TECH-OPS-001` no demuestra el RPO/RTO integral: dependerá además de PITR administrado, scheduler efectivo, red y reconciliación `HU-035`.
3. La imagen única aumenta superficie por cliente PostgreSQL y `age`; se mitiga con packages fijados, no-root, read-only, SBOM y escaneo.
4. No purgar conserva historia y evita destrucción, pero el almacenamiento puede superar 30 copias; una política posterior deberá resolver crecimiento sin aplicarse retroactivamente.
5. El manifiesto de réplica contiene keys y hashes de alta cardinalidad; debe permanecer privado y no viajar como artefacto público de CI.
6. La firma queda pendiente de publicación autorizada; antes de staging real debe ligarse al digest que use el manifiesto.
7. El restore técnico puede pasar aunque exista una diferencia funcional; sólo `HU-035` puede detectar y reportar esa diferencia.

## 23. Archivos previstos

Sin comprometer nombres internos que dependan del diseño posterior, se prevén exclusivamente estas rutas o familias:

- `Dockerfile`, `.dockerignore`;
- `deploy/staging/compose.yaml`, `deploy/staging/staging.env`, `deploy/staging/secrets.example`;
- `src/Sgol.Operations/**`;
- composición operativa mínima en `src/Sgol.Worker/**` y `src/Sgol.Web/Infrastructure/**`;
- health/readiness mínima en `src/Sgol.Web/Program.cs` y componentes directamente asociados;
- `tests/Sgol.UnitTests/**`, `tests/Sgol.ArchitectureTests/**` y proyectos de integración técnica específicos si resultan necesarios;
- `scripts/ci/validate-tech-ops.ps1`, `scripts/operations/**`;
- `docs/operations/oci-and-staging.md`, `docs/operations/postgresql-portable-backup.md`, `docs/operations/object-replication.md`, `docs/operations/isolated-restore.md`;
- `SGOL.slnx`, archivos `.csproj`/`packages.lock.json`, workflow de PR si aplica y `docs/traceability/IMPLEMENTATION_STATUS.md`.

No se prevé modificación de `Fuentes/`, `docs/design/logo.svg`, `docs/design/mapa-pantallas.md`, F00–F07 aprobados ni historias funcionales cerradas.

## 24. Eficacia y cierre

1. Aprobar esta adenda autoriza únicamente la implementación local y gates ya autorizados por el prompt; no autoriza commit, push, PR, publicación de imagen, staging real ni merge.
2. La implementación actualizará `docs/traceability/IMPLEMENTATION_STATUS.md` en el mismo cambio como propuesta, nunca como tarea ya Terminada.
3. Antes de commit se presentarán rama/base, status, rutas exactas para staging, diff/stat, gates/límites y confirmación de rutas protegidas.
4. `TECH-OPS-001` sólo será Terminada después de contrato aprobado, implementación y trazabilidad en el mismo commit, pipeline verde para el SHA exacto, evidencia externa obligatoria, aprobación humana, merge, ascendencia en `origin/master`, protección de `Fuentes/` y cero defectos bloqueantes.
5. Esta adenda no habilita ni inicia automáticamente `HU-035`.

## 25. Decisiones solicitadas

La aprobación íntegra decide como una unidad:

1. entregables y rutas de la sección 5;
2. Compose como manifiesto normativo y DigitalOcean sólo como adaptador externo;
3. bases .NET 10 Noble fijadas por digest, `linux/amd64`, paquetes mínimos y actualización por PR;
4. comandos exactos, UID/GID, filesystem, puerto y health checks;
5. inventario de configuración/secretos, key ring PostgreSQL cifrado y separación de credenciales;
6. SBOM SPDX + procedencia en CI, firma sólo después de publicación autorizada;
7. backup `pg_dump` custom + `age`, nombre/manifiesto/hash, no-overwrite y retención mínima sin purga;
8. réplica horaria incremental, verificación SHA-256, manifiesto privado, retries, locks, no-overwrite y no-delete;
9. slots UTC y operador técnico como propietario del scheduler;
10. evidencia técnica y reparto de gates;
11. frontera estricta con `HU-035`/`CA-035` y riesgos de la sección 22.

No existe aprobación parcial implícita. Si una decisión no es aceptable, debe corregirse esta propuesta antes de cualquier implementación.

La pregunta de aprobación es: **¿se aprueba íntegramente `F07_ADENDA_32_CONTRATO_DE_OPERACION_PORTABLE_TECH_OPS_001.md`, sin cambios, para autorizar la implementación local de `TECH-OPS-001` bajo este contrato?**
