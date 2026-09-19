# F07 Adenda 38 — Experimento A/B de red de réplica HU-035

## Decisión y alcance

El responsable aprueba el 2026-09-17 la materialización, implementación local y validaciones puras de este experimento. El identificador 38 se verificó libre. Complementa las Adendas 33–37; no corrige el HTTP 500 ni acredita CA-035, CP-035-P, CP-035-N o cierre de HU-035. No se amplía el parser del servidor.

Hipótesis refutable: retirar la red primaria de arranque después de inicializar SeaweedFS contribuye al fallo de la primera réplica en REPLICATE_CLEAN / PUT_DESTINATION / AmazonS3Exception / 500 / InternalError. Una diferencia A/B no prueba por sí sola el mecanismo interno ni la conservación de una dirección obsoleta.

## Cinco rutas autorizadas

- F07_ADENDA_38_EXPERIMENTO_AB_DE_RED_DE_REPLICA_HU_035.md.
- scripts/operations/invoke-hu-035-replica-network-ab.ps1.
- tests/Sgol.OperationsIntegrationTests/FunctionalRecoveryAmd64GateTests.cs.
- .github/workflows/pull-request.yml.
- docs/traceability/IMPLEMENTATION_STATUS.md.

No se modifica producción, dependencias, políticas operativas, solicitudes, streams, reintentos, timeout, parser ni gates de pull_request. Las pruebas puras del orquestador residen en su modo SelfTest para no ampliar rutas.

## Entorno y mecanismo manual

El entorno acreditado es GitHub-hosted ubuntu-24.04, Linux AMD64 nativo: run 35271294462, intento 1, job 105371100525. No se presupone acceso a otro equipo ni se permite aceptar emulación ARM64. Se comprueban Linux/X64 del host y arquitectura AMD64 del daemon antes de construir o ejecutar.

El workflow pull-request.yml existe en master y está registrado con ID 345003964. Se añade workflow_dispatch con expected_sha obligatorio. El job verify se selecciona exclusivamente por pull_request y conserva todos sus pasos; hu035-network-ab exclusivamente por workflow_dispatch, sin needs, con concurrencia hu035-network-ab-manual y cancel-in-progress false. Permisos contents: read, checkout sin credenciales persistentes, sin secretos productivos ni publicación de imágenes.

Antes de cualquier publicación se debe volver a inventariar los workflows de la rama predeterminada y del commit propuesto para detectar eventos push/create de tags u otros disparadores. La inspección inicial sólo encontró pull-request.yml con pull_request en master. Este hecho puede cambiar.

Una futura autorización separada permitiría crear un commit local, publicarlo sólo mediante un tag diagnóstico nuevo e inmutable y despachar el workflow registrado contra ese tag por API/CLI. No se actualiza la rama remota del PR para llegar al experimento, ni se hace merge a master. La documentación oficial permite dispatch por ref de workflows ya ejecutados: https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_dispatch. Si el servidor rechaza el dispatch, se detiene; no se usa el pipeline integral como alternativa automática.

expected_sha se transmite por variable de entorno, nunca como código de shell; debe ser exactamente 40 caracteres hexadecimales minúsculos y coincidir con HEAD antes de build/ejecución. SDK fijado, restore locked y build del proyecto de integración con SGOL_TECH_OPS_PROVISIONING_TESTS=true y SGOL_HU035_AMD64_TESTS=true. Se construye una sola imagen local desde ese SHA para su identidad real; ambos brazos usan exactamente ese digest. No se ejecutan la suite completa, TECH-OPS ni HU-035 integral.

## Preparación y variable experimental

Dos SeaweedFS nuevos por variante, imagen 4.45 fijada por el mismo digest del aprovisionamiento existente, mini -dir=/data -s3.config=/run/sgol/s3.json, archivos propios y recursos con nombres únicos. A y B se ejecutan secuencialmente, sin compartir almacenamiento. Las credenciales sintéticas se generan una vez para ambos brazos, con identidades EVIDENCE, REPLICA_SOURCE, BACKUP y REPLICA_DESTINATION y permisos existentes. Cinco buckets y SyntheticEvidenceFixture idénticos, destino inicialmente sin objetos de réplica ni manifiestos COMPLETE.

Se reutilizan las dos pruebas existentes de aprovisionamiento de buckets/semilla y BACKUP en create y verify. Se conserva el objeto de BACKUP fuera del prefijo de manifiestos de réplica. Se retira sólo el administrador mediante File.WriteAllText in situ, se ejecuta docker restart una vez y se espera disponibilidad acotada. Se conserva en ambos brazos el ciclo del bridge temporal; no se afirma equivalencia entre docker restart y StopAsync/StartAsync.

A arranca en una red bootstrap interna, luego renombra contenedores, conecta la red definitiva interna con los aliases originales, desconecta bootstrap y la elimina. B arranca directamente en la red definitiva interna y se renombra igualmente; no sustituye su red primaria. Los endpoints de réplica usan las IP privadas finales, no los puertos de aprovisionamiento. La única diferencia de tratamiento es la transición de red primaria; nombres/IP efímeros son identificadores de recursos aislados.

No se necesita PostgreSQL: la primera llamada de HU-035 es ObjectReplica.ExecuteAsync directo. La siembra de base anterior sólo registra la evidencia ya sembrada en S3. No se ejecutan Worker, backup PostgreSQL, segunda réplica ni reconciliación. El slot se calcula una sola vez con la misma regla staleReplicaSlot de HU-035 y se comparte entre A/B.

## Recorrido, aceptación y observación

La nueva prueba invoca ObjectReplica real y el helper exterior existente, conservando hasta tres intentos sólo para REPLICA_INFRASTRUCTURE_FAILED y pausas de 250/500 ms. Integridad y cancelación no se reintentan. Los clientes productivos conservan Standard, MaxErrorRetry=2, timeout 60, ForcePathStyle y HTTP privado. La secuencia de manifiestos, cuarentena, clean, hash de origen, ResponseStream y PUT/GET/HEAD permanece productiva, sin sustituirla por la sonda simplificada.

Se observa cada intento exterior por separado con número, inicio/fin, resultado y diagnóstico cerrado existente. Se distingue PASSED_INITIAL de PASSED_AFTER_RETRY. No se confunden los intentos internos del SDK con los exteriores. La observación no se habilita en el recorrido integral existente.

Después del retorno exitoso se lee el manifiesto exacto del slot, con identidad REPLICA_DESTINATION y cliente productivo, fuera del bucle de reintentos. Se exige representación canónica, SchemaVersion=1, Kind=SGOL_EVIDENCE_OBJECT_REPLICA, ScheduledFor exacto, Status=COMPLETE, ErrorClass nulo, revisión/digest correspondientes, exactamente una entrada clean con clave/tamaño/hashes de SyntheticEvidenceFixture y Status=VERIFIED. Esa lectura posterior de aceptación no es una sonda previa. Un fallo de lectura o validación no repite la réplica.

El TRX de cada réplica debe tener total=1, executed=1, passed=1 para aprobar. Cero pruebas, omitidas, más de una, TRX ausente/inválido y retorno sin manifiesto válido no cuentan como éxito. Un TRX con una prueba ejecutada y fallida puede documentar un fallo del experimento, nunca una réplica aprobada.

## Resultados y limpieza

Preparación fallida, evidencia inválida, cancelación, fallo de réplica, fallo de manifiesto y limpieza no confirmada quedan separados. Un fallo de réplica A permite B sólo después de confirmar limpieza. Si la limpieza A falla o no puede verificarse, B no arranca y el experimento queda INCONCLUSIVE. El bloque finally intenta únicamente limpieza de contenedores, redes y directorio propios; verifica ausencia y conserva el resultado de réplica. Una interrupción del runner no permite afirmar limpieza confirmada.

Se conservan resultados parciales y finales de ambos brazos aunque el job termine fallido. Artifacts if: always() publican exclusivamente resúmenes sanitizados y TRX de la prueba enfocada; no publican TRX privados del aprovisionamiento, configuraciones, endpoints, claves, credenciales, objetos, manifiestos completos ni logs crudos. hu035Accepted es siempre false.

Interpretación: A con fallo objetivo/B inicial aprobado apoya efecto de migración; ambos con fallo objetivo muestran que evitarla no es suficiente; ambos inicialmente aprobados no reproducen; A inicial aprobado/B con fallo objetivo contradice la predicción. Éxitos tras reintento quedan como RETRY_DEPENDENT, sin equipararlos a éxito inicial. Otros fallos o limpieza incierta son INCONCLUSIVE. EXECUTED sólo describe una comparación completada, no que ambas réplicas aprobaron ni aceptación HU-035. Si alguna réplica no aprueba o falta evidencia, el job termina fallido después de preservar resultados y limpiar.

## Validación local autorizada y límites

Sólo sintaxis PowerShell/YAML, compilación necesaria, Category=Hu035ReplicaNetworkAbPure, modo SelfTest del orquestador y git diff --check. Pruebas puras de guards por evento, SHA, manifiestos, TRX, intentos, interpretación y limpieza con Docker simulado. No se ejecuta el experimento local ni pruebas externas/integrales.

Esta aprobación no autoriza commit, creación/publicación de tag, push de rama, dispatch, reejecución CI ni merge. Cada acción requiere autorización humana posterior. Fuentes/ y diseño protegido permanecen intactos.
