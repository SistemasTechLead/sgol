# F07 Adenda 40 — Diagnóstico causal sanitizado del PUT de destino HU-035

## Aprobación y propósito

El responsable aprueba íntegramente el 2026-09-18 esta ampliación diagnóstica mínima después de que el gate integral de HU-035 volviera a fallar en `REPLICATE_CLEAN / PUT_DESTINATION / AmazonS3Exception / HTTP 500 / InternalError`. La prueba enfocada real y cinco reproducciones locales exactas aprobaron, por lo que no se atribuye el fallo a red, capacidad, disco, SDK, stream o SeaweedFS sin evidencia adicional.

La captura anterior registró 191 líneas no reconocidas y ningún evento reconocido. Ausencia de eventos reconocidos no demuestra ausencia de error del servidor. Esta adenda permite clasificar de forma cerrada el punto interno y el estado de runtime inmediatamente después del fallo y antes de la limpieza. No autoriza todavía una corrección funcional.

## Rutas autorizadas

- `F07_ADENDA_40_DIAGNOSTICO_CAUSAL_SANITIZADO_PUT_HU_035.md`.
- `scripts/operations/invoke-hu-035-amd64-gate.ps1`.
- `scripts/ci/test-hu-035-server-diagnostics.ps1`.
- `scripts/ci/validate-hu-035.ps1`.
- `tests/Sgol.OperationsIntegrationTests/FunctionalRecoveryAmd64GateTests.cs`.
- `docs/traceability/IMPLEMENTATION_STATUS.md`.

## Evidencia cerrada

El artifact público conserva el archivo existente `hu-035-server-diagnostic.json` y eleva su esquema a versión 2. Las líneas originales continúan siendo privadas y transitorias. Los eventos del archivo oficial SeaweedFS 4.45, commit `79b87202136cebdaaa7db4d94eaa5915ad381276`, se clasifican únicamente como:

- `VOLUME_ASSIGNMENT_FAILED`, `VOLUME_UPLOAD_FAILED`, `REQUEST_BODY_READ_FAILED` o `CHUNK_UPLOAD_FAILED` para el upload por chunks;
- `CREATE_ENTRY_FAILED`, `CONDITIONAL_LOOKUP_FAILED`, `OBJECT_LOCK_LOOKUP_FAILED`, `VERSIONING_LOOKUP_FAILED`, `ENCRYPTION_LOOKUP_FAILED` o `VERSIONED_WRITE_FAILED` para los demás puntos cerrados de PUT que pueden devolver error interno.

El estado de runtime se obtiene mediante lecturas acotadas de `docker inspect` y `/dir/status` ejecutadas dentro del contenedor de destino antes de limpiarlo. Sólo se publican clasificaciones cerradas:

- red: `FINAL_ONLY`, `TRANSIENT_PRESENT`, `FINAL_MISSING` o `UNKNOWN`;
- dirección anunciada: `FINAL_NETWORK`, `OUTSIDE_FINAL_NETWORK`, `UNRESOLVED` o `UNKNOWN`;
- nodo de datos: `PRESENT`, `NONE` o `UNKNOWN`;
- capacidad escribible: `POSITIVE`, `ZERO` o `UNKNOWN`;
- captura: `OK`, `INVALID` o `CAPTURE_FAILED`.

No se publican nombres de contenedores o redes, direcciones, subredes, puertos, URLs, buckets, claves, credenciales, metadata, contenido, líneas crudas, fingerprints, mensajes ni excepciones originales. Fallos, timeouts, truncamiento o JSON inválido quedan visibles como captura incompleta; nunca sustituyen el fallo original ni impiden la limpieza.

## Complemento aprobado para el despacho REFERENCE

El responsable aprobó el 2026-09-18 ampliar esta misma adenda después de que el run diagnóstico `35383233043`, sobre el SHA exacto `e3a31ebdc2527ef4a720dd9447828f3c00b5b075`, no reprodujera el HTTP 500 original y fallara antes en el despacho `REFERENCE`. La ampliación sólo sustituye la aserción opaca del procesador por un error de gate sanitizado con el caso aprobado, resultado cerrado del outbox, error cerrado del outbox y estado/error cerrado de `scheduled_job_run`.

Los valores no reconocidos se reducen a `UNKNOWN`; la ausencia comprobada se expresa como `ABSENT` o `NONE`. No se publican identificadores, checkpoints, timestamps, payloads, mensajes, excepciones, endpoints ni secretos. Si la consulta diagnóstica falla, prevalece una salida cerrada con `UNKNOWN`; el resultado original del procesador no se modifica ni se reintenta por esta captura.

El run `35386054702` sobre el SHA exacto `e4f64b7abe2174f36d7836aabd7481339d42652a` demostró `CASE=positive`, `OUTBOX_RESULT=RETRY_SCHEDULED`, `OUTBOX_ERROR=RECOVERY_REFERENCE_JOB_FAILED`, `JOB_STATUS=ABSENT` y `JOB_ERROR=NONE`. La correlación con el contrato persistente demuestra que el handler entregaba un UUID plano como checkpoint mientras `CK_scheduled_job_run_checkpoint` exige un objeto JSON. La inserción se revertía antes de dejar fila del job y el runner devolvía fallo al outbox. La corrección fiel serializa únicamente `reconciliationId` dentro de un objeto JSON y exige ese mismo esquema cerrado al leerlo; no altera la semántica del despacho, sus reintentos ni sus estados.

## Invariantes conservadas

No cambian producción, topología, ciclo de vida, imagen, configuración, permisos, credenciales, PUT/GET/HEAD, streams, objetos, sondas S3, outbox, job, timeouts, reintentos, limpieza, matriz de 18 casos, RPO/RTO, condiciones de aprobación ni workflow. Las consultas diagnósticas sólo ocurren después de que REFERENCE ya falló.

La validación exige sintaxis PowerShell, pruebas puras sobre funciones reales, categorías exactas, límites de proceso, ausencia de datos privados, conservación del fallo original y ejecución literal de la limpieza existente. Se ejecutan además el validador HU-035 y `git diff --check`.

## Uso y límite de decisión

Se autoriza publicar esta instrumentación únicamente en `codex/hu-035` y ejecutar una vez el gate integral del PR #55 sobre su SHA exacto. El resultado diagnóstico no acepta HU-035 ni habilita merge. Si la evidencia exige cambiar alguno de los invariantes anteriores, se presentará la decisión contractual mínima antes de corregir código.
