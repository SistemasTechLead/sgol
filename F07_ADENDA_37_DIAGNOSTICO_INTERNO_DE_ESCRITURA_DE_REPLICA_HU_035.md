# F07 Adenda 37 — Diagnóstico interno de escritura de réplica HU-035

## Aprobación y límites

El responsable aprueba la materialización, implementación local y pruebas puras de este ajuste el 2026-09-17. Se verificó libre el identificador 37. Complementa las Adendas 33–36 y modifica únicamente el límite diagnóstico de la llamada real PutStreamVerifiedAsync. No identifica aún la causa del HTTP 500, no es una corrección funcional y no satisface por sí solo CA-035, CP-035-P ni CP-035-N.

Se conservan intactos los cambios locales ya validados de la Adenda 36 y ObjectReplicaExternalTests.cs. No se añade la sonda previa propuesta ni objetos temporales al entorno de HU-035.

## Recorrido autorizado

S3OperationStore conserva su constructor y API pública. Un estado interno opt-in, habilitado exclusivamente por la instancia de escritura de objeto de ObjectReplica, marca inmediatamente antes de las llamadas reales PUT_DESTINATION, GET_DESTINATION_VERIFY y HEAD_DESTINATION_METADATA. Sin activación conserva UNKNOWN y no registra operaciones. No se añaden callbacks ni infraestructura diagnóstica general.

PUT se restablece a UNKNOWN sólo tras éxito. GET conserva su marcador hasta completar el consumo/hash del ResponseStream y se restablece antes de evaluar discrepancias de integridad. HEAD cubre tanto verificación normal como consulta tras conflicto 409/412; reemplaza el marcador de PUT antes de su propia llamada. Tras HEAD correcto o 404 manejado se restablece UNKNOWN. Ningún finally borra el marcador de una llamada fallida. GET identifica también errores de consumo y no implica por sí solo un rechazo HTTP del servidor.

ObjectReplica copia y normaliza el marcador interno si la llamada agrupada propaga una excepción y utiliza throw; sin envolverla. Se conservan sus siete marcadores exteriores y se amplían sus dos normalizadores con los tres interiores. El diagnóstico final del gate mantiene etapa, operación, tipo sanitizado, HTTP válido o NONE y S3CODE sanitizado o UNKNOWN. No se registran mensajes crudos, stack traces originales, inner exceptions, endpoints, buckets, claves, metadata, contenido ni credenciales.

Para probar la propagación sin fabricar JobExecutionException, la captura, sanitización y clasificación productivas se extraen a un helper privado compartido por el catch real y las pruebas. Se captura el fallo completo antes de TryPublishFailureAsync; la publicación no puede alterar sus valores. Se conserva el filtro de cancelación del catch y el helper no transforma cancelación en fallo de infraestructura. No se amplía API ni se añade InternalsVisibleTo; las pruebas puras acceden por reflexión a la ruta privada existente y al helper de captura.

Se mantienen exactamente solicitudes, stream y su vida, CancellationToken, orden, reintentos, timeouts, tratamiento 409/412/404 y códigos de integridad/autorización/infraestructura. No se agregan lecturas, escrituras, borrados, sondas, infraestructura ni cambios funcionales.

## Rutas autorizadas

1. F07_ADENDA_37_DIAGNOSTICO_INTERNO_DE_ESCRITURA_DE_REPLICA_HU_035.md.
2. src/Sgol.Operations/S3OperationStore.cs.
3. src/Sgol.Operations/ObjectReplica.cs.
4. tests/Sgol.OperationsIntegrationTests/FunctionalRecoveryAmd64GateTests.cs.
5. scripts/ci/validate-hu-035.ps1: marcadores, propagación, captura y conservación de solicitudes/conflictos.
6. docs/traceability/IMPLEMENTATION_STATUS.md: referencia contractual mínima, sin declarar terminada HU-035.

## Evidencia y gates autorizados

Pruebas puras con clientes simulados ejecutan ReplicateBucketAsync real, su almacenamiento real, CaptureFailure productivo y el formateador final del gate. No basta construir manualmente un resultado ni consultar sólo la propiedad diagnóstica. Se prueban fallos asíncronos PUT/GET/HEAD, fallo durante consumo de stream, conflictos 409/412 con HEAD fallido/coincidente/distinto/404, integridad de hash y metadata, orden y argumentos, propagación de cancelación, modo deshabilitado, ausencia de datos sensibles y captura inmutable previa a publicación.

Se ejecuta únicamente Category=Hu035ReplicaDiagnostics con -p:SGOL_HU035_AMD64_TESTS=true, compilación requerida, validador HU-035 y git diff --check. Las pruebas no requieren Docker, PostgreSQL, S3 ni variables del simulacro. No se repiten pruebas externas ni gates integrales. Un futuro run del SHA autorizado será necesario para observar el diagnóstico del fallo real.

No se autoriza commit, push, CI, nuevo PR, merge ni tareas posteriores. La instrumentación validada no equivale a causa identificada.

## Ajuste mínimo aprobado: captura del servidor durante REFERENCE

El responsable aprueba la implementación local y pruebas puras exclusivamente en esta adenda, scripts/operations/invoke-hu-035-amd64-gate.ps1, scripts/ci/test-hu-035-server-diagnostics.ps1 y docs/traceability/IMPLEMENTATION_STATUS.md.

Ante una excepción dentro de la preparación REFERENCE, se capturan los logs existentes del contenedor SeaweedFS de destino antes de la limpieza. No basta el valor residual de stage. Se conservan el error original, el alcance del descriptor y las instrucciones, alcance y resultados de la limpieza. No se añaden sondas, operaciones S3, cambios productivos, redes, permisos, reintentos, dependencias ni cambios de verbosidad.

La lectura de docker logs espera como máximo ocho segundos, sin follow, y retiene como máximo 262144 caracteres por canal. Tras solicitar la terminación del lector, se espera como máximo 500 ms para comprobar su salida. No hay esperas ilimitadas; estos límites no garantizan disponibilidad ante bloqueos del sistema operativo. Las pruebas comprueban además que los procesos simulados terminaron. Los identificadores y detalles de esos procesos no se publican.

Los canales se mantienen separados; sólo se colapsan duplicados exactos entre stdout y stderr. El parser dispone de un segundo, limita cada línea a 16384 caracteres y publica como máximo 32 eventos. Conserva la precisión RFC3339Nano del timestamp Docker. Reconoce exclusivamente CHUNK_UPLOAD_FAILED y CREATE_ENTRY_FAILED mediante los prefijos verificados en weed/s3api/s3api_object_handlers_put.go de la etiqueta oficial 4.45, commit 79b87202136cebdaaa7db4d94eaa5915ad381276. La única correlación permitida es TIME_WINDOW_ONLY. No se atribuye una petición exacta, no se clasifican sockets y no se comparan direcciones antiguas.

El artifact existente recibe exclusivamente hu-035-server-diagnostic.json con campos cerrados de estado, límites, contadores, ventana temporal y eventos sanitizados. No contiene líneas originales, fragmentos dinámicos, endpoints, claves, credenciales, metadata, contenido, fingerprints ni excepciones originales. Los logs crudos sólo permanecen transitoriamente en memoria. Las pruebas comprueban listas exactas de propiedades del JSON y sus eventos y ausencia de centinelas sensibles.

Se distinguen captura fallida, vacía, incompleta y sin eventos reconocidos. Un fallo de captura, parser o escritura no reemplaza el fallo original ni impide intentar la limpieza; sólo se permiten mensajes fijos del capturador. Ninguna indexación de contenedores ni construcción de rutas se evalúa antes de entrar en la finalización protegida. Ausencia de eventos reconocidos no demuestra ausencia de errores del servidor.

El único gate nuevo es scripts/ci/test-hu-035-server-diagnostics.ps1: pruebas puras de formatos temporales, canales, límites, sanitización, capturador simulado y funciones de REFERENCE/finalización realmente usadas por el gate. Se ejecuta también el bloque de limpieza extraído del AST del gate con Docker simulado y rutas temporales propias, verificando sus variables escalares y resumen, fallos anteriores/posteriores a REFERENCE y conservación del fallo original. Se comprueba la sintaxis del gate y git diff --check. No se autoriza ejecutar CI, pruebas externas, commit, push ni merge.

Fuentes oficiales: https://github.com/seaweedfs/seaweedfs/blob/79b87202136cebdaaa7db4d94eaa5915ad381276/weed/s3api/s3api_object_handlers_put.go y https://docs.docker.com/reference/cli/docker/container/logs/.
