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

## Causa demostrada del PUT y corrección aprobada

El run `35388790792` sobre el SHA exacto `ad8f13abcc31449a2e025b4386ad546961ef9e9a` superó el despacho `REFERENCE` corregido y volvió a reproducir `REPLICATE_CLEAN / PUT_DESTINATION / HTTP 500 / InternalError`. La captura sanitizada demostró simultáneamente red `FINAL_ONLY`, nodo `PRESENT`, capacidad `POSITIVE` y dirección anunciada `OUTSIDE_FINAL_NETWORK`. El aprovisionamiento iniciaba ambos SeaweedFS en la red privada final, añadía temporalmente `bridge`, reiniciaba los procesos mientras ambas redes estaban presentes y retiraba después `bridge`. Como `weed mini` seleccionaba automáticamente la identidad anunciada al reiniciar, conservaba una dirección de la red temporal ya retirada; el PUT S3 podía alcanzar el frontend, pero éste no podía completar la escritura contra la dirección anunciada del nodo de datos.

El responsable aprobó el 2026-09-18 la corrección mínima: conservar el alias ya creado en la red final como identidad anunciada de cada SeaweedFS y mantener el bind en `0.0.0.0`. El diagnóstico considera perteneciente a la red final tanto su dirección como uno de sus aliases exactos. No se añade red, reinicio, servicio, dependencia, credencial, permiso, timeout o reintento, ni cambia ninguna operación PUT/GET/HEAD.

El run `35391823674` sobre el SHA exacto `c86d62b170c68689d18d7755bb72af9433b567c6` confirmó la corrección de red con `FINAL_ONLY / FINAL_NETWORK / PRESENT / POSITIVE`, pero terminó en `REFERENCE` con el evento de reconciliación en `FAILED` después de que el outbox fuera procesado. El artifact no incluyó el código cerrado de ese evento. El responsable aprobó el 2026-09-18 ampliar únicamente el fallo diagnóstico para publicar el caso normalizado, estado/error cerrado de `scheduled_job_run` y estado/error cerrado del evento de reconciliación. Los valores no incluidos en las listas cerradas se reducen a `UNKNOWN`; no se publican excepciones, mensajes, endpoints, identificadores, credenciales ni contenido.

El run `35395241760` sobre el SHA exacto `98e26d17bcb32f1a7a237997d9cdf8eb72d92781` demostró que el job terminó `SUCCEEDED`, mientras el evento de reconciliación quedó `FAILED / UNEXPECTED_RECONCILIATION_FAILURE`. Esto acota el defecto a una excepción no contractual ocultada por el fallback de `CaptureRecoveryReferenceJob`, pero todavía no identifica su operación ni su clase. El responsable aprobó el 2026-09-18 una clasificación causal mínima y cerrada dentro de la captura: etapas `CAPTURE_SNAPSHOT`, `EXPORT_BACKUP`, `PUT_BACKUP`, `PUT_BACKUP_MANIFEST`, `READ_REPLICA_MANIFEST`, `PUT_REFERENCE_SNAPSHOT` y `PUT_REFERENCE_MANIFEST`; clases `S3_INTERNAL_ERROR`, `S3_ERROR`, `POSTGRESQL_ERROR`, `EXTERNAL_PROCESS_ERROR`, `IO_ERROR` y `UNEXPECTED`. El evento persiste únicamente `REFERENCE_<ETAPA>_<CLASE>` sin excepción interna, mensaje, ruta, endpoint, credencial ni contenido. Los errores contractuales existentes conservan su código y la cancelación conserva su propagación.

## Invariantes conservadas

No cambian topología, ciclo de vida, imagen, permisos, credenciales, PUT/GET/HEAD, streams, objetos, sondas S3, outbox, semántica del job, timeouts, reintentos, limpieza, matriz de 18 casos, RPO/RTO, condiciones de aprobación ni workflow. La única configuración corregida fija la identidad anunciada a un alias que ya forma parte de la red privada final. La clasificación causal no añade reintentos ni suprime el fallo: sustituye exclusivamente el fallback genérico de una excepción no contractual por etapa y clase cerradas.

La validación exige sintaxis PowerShell, pruebas puras sobre funciones reales, categorías exactas, límites de proceso, ausencia de datos privados, conservación del fallo original y ejecución literal de la limpieza existente. Se ejecutan además el validador HU-035 y `git diff --check`.

## Uso y límite de decisión

Se autoriza publicar esta instrumentación y la corrección causal únicamente en `codex/hu-035` y ejecutar una vez el gate integral del PR #55 sobre su SHA exacto. El resultado no acepta HU-035 ni habilita merge por sí solo. Si la evidencia exige cambiar alguno de los invariantes anteriores, se presentará la decisión contractual mínima antes de modificarlo.
