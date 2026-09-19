# Reconciliación funcional de recuperación HU-035

## Propósito y límites

Este runbook ejecuta `HU-035` sólo con datos sintéticos. Verifica que una restauración aislada conserva identidades, vínculos, versiones, conteos, evidencia y auditoría. No repara, compensa, borra, sobrescribe ni fabrica datos. Una diferencia, ausencia, corrupción, inaccesibilidad o evidencia incompleta impide `MATCHED` y `APPROVED`.

No use producción, la única copia, credenciales reales ni archivos dentro de `Fuentes/`. El destino PostgreSQL debe ser nuevo, vacío, descartable y distinto de `ConnectionStrings__Sgol`. El archivo de entorno externo debe contener exactamente el marcador operativo `SGOL_SYNTHETIC_ONLY=true`; este marcador no sustituye las comprobaciones de destino distinto, migración y restore técnico.

## Responsabilidades

- Dirección con cuenta, persona, empleo y rol `DIRECCION` vigentes en `LOR-001` solicita, consulta y aprueba mediante la API. El servidor resuelve `PER-CONTINUIDAD-VER`.
- El operador técnico ejecuta Worker, backup, réplica, restore y comandos de `Sgol.Operations`. No puede aprobar por esa función.
- El responsable de SGOL autoriza previamente el simulacro y conserva la evidencia sanitizada fuera del repositorio.

## Preparación sintética mínima

Prepare personas, cuentas, historia laboral y de roles, configuración versionada, semana/plan/obligaciones, asignaciones, ejecución, evidencia de archivo y estructurada, sustitución, validación, idempotencia, outbox, avisos y auditoría. Incluya por lo menos dos versiones y dos vínculos por agregado relevante. Los objetos deben estar en buckets S3-compatible privados sintéticos y tener tamaño y SHA-256 coherentes con `file_object`.

Use los artefactos de `TECH-OPS-001`: imagen OCI inmutable `linux/amd64`, backup portable cifrado, manifiesto de réplica `COMPLETE`, dos almacenamientos S3-compatible y restore técnico sobre PostgreSQL nuevo/vacío. En el simulacro HU-035, ambos SeaweedFS arrancan y permanecen en la red privada final; anuncian su alias estable de esa red aunque durante el aprovisionamiento exista una conexión temporal adicional. El restore debe producir `SGOL_TECHNICAL_RESTORE_EVIDENCE` con `startedAt`, `completedAt`, duración monotónica, digest de imagen y hashes de backup/manifiesto.

## Camino positivo

1. Dirección ejecuta `POST /api/v1/continuity/reconciliations` con `Idempotency-Key` UUID y motivo sintético. Conserve el `reconciliationId` y el ETag.
2. Ejecute el Worker outbox. El evento `RECOVERY_REFERENCE_REQUESTED` invoca mediante `ScheduledJobRunner` el job `CAPTURE_RECOVERY_REFERENCE`, con advisory lock estable y checkpoint JSON objeto que contiene únicamente `reconciliationId`. La captura usa `REPEATABLE READ, READ ONLY`, UTC, `pg_export_snapshot()` y `pg_dump --snapshot` dentro de la misma transacción; valida la migración vigente mediante la columna estándar `"MigrationId"` de `__EFMigrationsHistory`.
3. Verifique que el estado sea `REFERENCE_CAPTURING`. Un fallo de configuración, integridad o cardinalidad debe producir `FAILED`, nunca referencia lista. Una excepción no contractual de la captura se registra sólo como `REFERENCE_<ETAPA>_<CLASE>` dentro de las etapas y clases cerradas de la Adenda 40; no use ese código para reintentar, omitir el caso ni sustituir la causa antes de corregirla.
4. Tras disponer de los URI privados de referencia, backup y réplica, complete su asociación:

   ```powershell
   dotnet run --project src/Sgol.Operations/Sgol.Operations.csproj --configuration Release --no-build -- complete-functional-reference --reconciliation-id <uuid> --reference <s3-uri> --backup-manifest <s3-uri> --replica-manifest <s3-uri>
   ```

   Sólo `FUNCTIONAL_REFERENCE_READY` permite continuar. Repetir con los mismos hashes no añade otra etapa; cambiar contenido devuelve `RECONCILIATION_IMMUTABLE_CONFLICT`.
5. Restaure con el procedimiento aislado de `TECH-OPS-001`. Mantenga origen y destino sin tráfico funcional. No use `--clean`, drop ni destino primario.
6. Reconcilie:

   ```powershell
   dotnet run --project src/Sgol.Operations/Sgol.Operations.csproj --configuration Release --no-build -- reconcile-functional-restore --reconciliation-id <uuid> --reference-manifest <s3-uri> --restore-evidence <ruta-externa>
   ```

7. `FUNCTIONAL_RECOVERY_MATCHED` exige mismo contrato/revisión/digest/migración, 39 tablas completas, cero diferencias, objetos íntegros, auditoría íntegra, `databaseRpo=0`, `observedRpo<=3600` y `observedRto<=14400`. Los timestamps provienen del snapshot PostgreSQL, manifiesto de réplica, evidencia de restore y reloj UTC de Operations.
8. Dirección consulta el recurso. La consulta se audita antes de entregar datos y no expone URI, keys, nombres, contenido ni secretos. Con el ETag vigente, Dirección puede aprobar mediante `/approval`; sólo `MATCHED` es aprobable.

## Casos negativos obligatorios

Ejecute cada caso con un `reconciliationId` y restore descartable independientes. Conserve origen inalterado y mida conteos antes/después.

- identidad ausente y adicional;
- vínculo ausente o cambiado;
- versión y conteo distintos;
- metadata/evidencia faltante;
- objeto corrupto e inaccesible;
- auditoría faltante y alterada;
- manifiesto de referencia corrupto;
- RPO mayor de 3 600 segundos y RTO mayor de 14 400 segundos;
- intento de usar la conexión primaria como restore;
- repetición idéntica, contenido conflictivo y dos ejecuciones concurrentes.

Una diferencia comparable termina `DIFFERENT`. Ausencia/corrupción que impida comparación completa, cardinalidad excedida, reloj inconsistente o error operativo termina `FAILED` con código estable. Ningún caso puede modificar datos reconciliados, recrear la fila faltante, reemplazar objetos ni declarar éxito parcial.

## Gate externo y evidencia

En el pull request, el runner Linux AMD64 nativo ejecuta el orquestador aprobado por la Adenda 34 sobre la imagen
inmutable del SHA exacto:

```powershell
scripts/operations/invoke-hu-035-amd64-gate.ps1 `
  -ImageRef 'sha256:<image-id-del-sha>' `
  -GateRoot '<directorio-nuevo-fuera-del-repositorio>'
```

El orquestador falla cerrado en ARM64 o bajo emulación, crea credenciales y recursos exclusivamente efímeros con
identidad acotada del run, y materializa las 39 tablas del contrato con identidades, historia, versiones y vínculos.
Prepara 18 reconciliaciones independientes: camino positivo; diferencias y fallos de identidad, vínculo, versión,
conteo, evidencia, auditoría, manifiesto, RPO/RTO y destino; replay idéntico/conflictivo y concurrencia. Cada caso
descarta y recrea sólo la base de restore, conserva el origen, y el positivo termina con consulta y aprobación de
Dirección. Sólo `evidence-public` puede publicarse; `runtime-private` y `evidence-private` se eliminan en `finally`.
Una limpieza incompleta falla el gate. El pipeline no usa secretos de GitHub ni infraestructura persistente.

Ejecute `scripts/operations/verify-hu-035-external.ps1` fuera de la sesión con el archivo de entorno sintético, los manifiestos y la evidencia de restore. Opcionalmente entregue un JSON externo de casos negativos con `name`, `reconciliationId`, `referenceManifestUri`, `restoreEvidencePath` y `expectedCode`. El script exige rutas fuera del repositorio, directorio nuevo/vacío y no sobrescribe evidencia.

La evidencia mínima conservada incluye TRX de PostgreSQL, salidas sanitizadas de asociación/reconciliación, reportes privados, hashes de cuatro manifiestos, revisión Git, digest OCI, migración, conteos, diferencias, RPO/RTO y confirmación de cero cambios funcionales. No conserve cadenas de conexión, credenciales, archivos de evidencia, payloads auditados ni URLs firmadas.

No marque `HU-035` terminada hasta tener contrato aprobado, simulacro integral positivo y negativos aprobados, `CA-035`, `CP-035-P`, `CP-035-N`, pipeline verde del SHA exacto, aprobación humana, merge y ascendencia verificada en `origin/master`.
