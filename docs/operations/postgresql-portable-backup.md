# Backup PostgreSQL portable

## Creación diaria

El scheduler del operador ejecuta exactamente a `02:15:00Z`:

```text
dotnet Sgol.Worker.dll run-job --job POSTGRESQL_PORTABLE_BACKUP --scheduled-for <YYYY-MM-DDT02:15:00Z>
```

El proceso necesita la conexión de control del Worker, una conexión de exportación con usuario distinto y de sólo lectura, credenciales S3 distintas de la aplicación primaria y un recipient público `age` válido. El marcador versionado `REQUIRED_PUBLIC_AGE_RECIPIENT` debe reemplazarse externamente; nunca se acepta como configuración ejecutable. El backup no recibe la identity privada. `pg_dump` produce formato custom sin owner ni privilegios, fluye a `age` sin dump plaintext y publica por creación condicional:

```text
postgresql/v1/YYYY/MM/DD/sgol-YYYYMMDDT021500Z.dump.age
postgresql/v1/YYYY/MM/DD/sgol-YYYYMMDDT021500Z.dump.age.manifest.json
```

El éxito requiere cliente `pg_dump` de la misma major o posterior, tamaño no cero, SHA-256 local, upload completo, relectura secundaria y SHA-256 idéntico antes de publicar el manifiesto JCS `COMPLETE`. Un manifiesto ya confirmado para el slot hace que la repetición relea y valide el cifrado y termine como recuperación idempotente, sin ejecutar otro dump. Contenido diferente falla sin overwrite. El proceso limita herramientas y storage a los timeouts versionados y el cliente S3 a tres intentos totales. Un temporal cifrado de la corrida puede eliminarse; un objeto confirmado nunca se purga desde este mecanismo.

## Verificación y restore aislado

1. Aprovisionar PostgreSQL nuevo y vacío con datos exclusivamente sintéticos.
2. Confirmar que su connection string no es `ConnectionStrings__Sgol`.
3. Inyectar `Restore__PostgreSql__ConnectionString` y `Restore__Encryption__Identity` sólo al proceso de restore.
4. Ejecutar:

```text
dotnet Sgol.Operations.dll verify-postgresql-backup --manifest s3://<bucket-secundario>/<key>.manifest.json
```

El comando valida canonicalización del manifiesto, recipient fingerprint, tamaño y SHA-256, rechaza incluso la misma base primaria expresada con otro usuario, exige un destino sin tablas y valida primero el archivo con `pg_restore --list`. Después vuelve a descifrar en streaming hacia `pg_restore --exit-on-error --no-owner --no-privileges`, confirma la migración esperada, constraints validadas y acceso al key ring. No usa `--clean`, no elimina esquema/base y borra únicamente sus temporales privados. La evidencia JSON minimizada con resultado `BACKUP_RESTORE_VERIFIED` prueba restauración técnica; no prueba `CA-035`.

## Gate externo reproducible

El desarrollador prepara fuera del repositorio un archivo de entorno marcado `SGOL_SYNTHETIC_ONLY=true`, combina la configuración no secreta con secretos efímeros, reemplaza endpoints/recipient de marcador y conecta PostgreSQL primario, PostgreSQL de restore vacío y ambos S3-compatible a la red privada de Compose. Debe incluir exactamente un `SGOL_BACKUP_SCHEDULED_FOR=<YYYY-MM-DDT02:15:00Z>` y un `SGOL_REPLICA_SCHEDULED_FOR=<YYYY-MM-DDTHH:05:00Z>`.

Con los manifiestos esperados calculados a partir de esos slots ejecuta una sola vez el gate consolidado:

```powershell
scripts/operations/verify-tech-ops-external.ps1 -ImageRef 'sha256:<image-id-local>' -RuntimeEnvironmentFile '<ruta-fuera-del-repositorio>' -BackupManifestUri 's3://<bucket>/postgresql/v1/<YYYY/MM/DD>/sgol-<slot>.dump.age.manifest.json' -ReplicaManifestUri 's3://<bucket>/objects/v1/<YYYY/MM/DD>/objects-<slot>.manifest.json'
```

El script acepta el image ID local inmutable exclusivamente para el gate sin publicación, o una referencia `<registry/repository>@sha256:<digest>` ya autorizada. El manifiesto real de staging sigue exigiendo la segunda forma. El script se niega a usar un archivo de entorno dentro del checkout. Ejecuta migración expand-only, backup y reintento del mismo slot, réplica y reintento, restore aislado, verificación independiente, pruebas PostgreSQL de key ring/locking y las pruebas con dos S3-compatible. No publica imágenes ni conserva secretos en su directorio de evidencia.

## Fallos

Salida distinta de cero, ausencia de manifiesto `COMPLETE`, hash diferente, herramienta fallida o target no vacío bloquean la evidencia. No se crea un registro de éxito parcial ni se fabrican datos. Se conserva al menos una copia diaria durante 30 días; esta tarea no implementa lifecycle ni purga.
