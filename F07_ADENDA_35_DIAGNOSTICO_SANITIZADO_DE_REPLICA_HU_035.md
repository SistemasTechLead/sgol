# F07 Adenda 35 — Diagnóstico sanitizado de réplica para HU-035

## 1. Control de la propuesta

| Campo | Valor |
|---|---|
| Historia | `HU-035` — Dirección verifica recuperación con identidades e historia |
| Capacidad | `CAP-047` |
| Criterios | `CA-035`, `CP-035-P`, `CP-035-N` |
| Contratos base | `F07_ADENDA_33_CONTRATO_DE_RECONCILIACION_Y_SIMULACRO_DE_RECUPERACION_HU_035.md` y `F07_ADENDA_34_CONTRATO_DE_GATE_AMD64_AUTOMATIZADO_HU_035.md` |
| PR vigente | `#55` |
| SHA diagnosticado | `b61f23c5aa777d1a682ba53fac181877d0218e38` |
| Run diagnosticado | `35025380180`, intento 1 |
| Estado | PROPUESTA; sin eficacia hasta aprobación humana íntegra |
| Propósito | Identificar de forma sanitizada la operación exterior exacta que origina el HTTP 500 durante `REPLICATE_CLEAN` |

Esta adenda no modifica F00–F07 ni `Fuentes/`. Complementa exclusivamente el diagnóstico operativo de la réplica utilizada por el simulacro de `HU-035`.

Su aprobación íntegra autoriza su materialización, la implementación local y los gates enfocados descritos aquí. No autoriza commit, push, nuevo PR, reejecución de CI, despliegue ni merge.

## 2. Evidencia confirmada

1. El pipeline `35025380180` evaluó exactamente el SHA `b61f23c5aa777d1a682ba53fac181877d0218e38` en un runner Linux AMD64 nativo.
2. El gate integral `TECH-OPS-001` y su sonda de aprovisionamiento con identidad `BACKUP` finalizaron correctamente.
3. La eliminación de los recursos privados de TECH-OPS antes de `HU-035` finalizó correctamente.
4. El simulacro `HU-035` falló en la etapa exterior `REFERENCE`.
5. El error sanitizado fue:

   `HU035_REPLICA_PREPARATION_FAILED:REPLICA_INFRASTRUCTURE_FAILED:STAGE=REPLICATE_CLEAN:OPERATION=UNKNOWN:TYPE=AmazonS3Exception:HTTP=500`

6. El artifact contiene `hu-035-prepare.trx` y no contiene `hu-035-verify.trx`; la reconciliación funcional no llegó a ejecutarse.
7. La respuesta HTTP 500 confirma que una petición alcanzó el almacenamiento S3-compatible, pero no identifica cuál de las operaciones por objeto la produjo.
8. `REPLICATE_QUARANTINE` puede haber operado sobre un bucket vacío y no demuestra el funcionamiento de las operaciones por objeto.
9. Actualmente sólo los errores internos de `HasDestinationMetadataAsync` se etiquetan como `CHECK_DESTINATION_METADATA`; el resto conserva `OPERATION=UNKNOWN`.
10. Esta evidencia no demuestra todavía un defecto funcional, de política, de credenciales ni de infraestructura. Sólo demuestra una pérdida de precisión diagnóstica.

## 3. Decisión contractual

Se autoriza instrumentar exclusivamente la ruta de réplica por objeto de `ObjectReplica` para conservar una etapa y una operación cerradas cuando una excepción alcance el límite exterior del job.

La instrumentación:

1. será diagnóstica;
2. no corregirá ni reclasificará el HTTP 500;
3. no agregará reintentos;
4. no modificará solicitudes S3;
5. no cambiará el orden ni las condiciones del flujo;
6. no compensará, recreará, sobrescribirá ni fabricará objetos;
7. no alterará códigos contractuales ni la terminalidad del job;
8. no ampliará la API pública.

## 4. Operaciones cerradas

El inventario permitido de operaciones es exactamente:

- `LIST_SOURCE_OBJECTS`
- `HEAD_SOURCE_METADATA`
- `READ_SOURCE_HASH`
- `GET_SOURCE_STREAM`
- `CHECK_DESTINATION_METADATA`
- `WRITE_AND_VERIFY_DESTINATION`
- `READ_DESTINATION_HASH`

`UNKNOWN` se conserva únicamente como valor seguro cuando la excepción ocurre fuera de la ruta por objeto instrumentada o antes de seleccionar una de esas operaciones.

No se autoriza ningún marcador dinámico, derivado de endpoints, buckets, claves, métodos SDK, mensajes de excepción o datos externos.

## 5. Ubicación de los marcadores

El marcador se actualizará inmediatamente antes de iniciar cada operación:

1. `LIST_SOURCE_OBJECTS`: antes de cada `ListObjectsV2Async` del bucket de origen, incluidas sus páginas posteriores.
2. `HEAD_SOURCE_METADATA`: antes de `GetObjectMetadataAsync` sobre el objeto de origen.
3. `CHECK_DESTINATION_METADATA`: antes de entrar en `HasDestinationMetadataAsync`.
4. `READ_SOURCE_HASH`: antes de `ReadObjectHashAsync` cuando se lee y calcula el hash completo del objeto de origen.
5. `GET_SOURCE_STREAM`: antes de obtener el stream de origen que se entregará a la escritura de destino.
6. `WRITE_AND_VERIFY_DESTINATION`: únicamente antes de la llamada agrupada `PutStreamVerifiedAsync`.
7. `READ_DESTINATION_HASH`: inmediatamente antes de `ReadObjectHashAsync` cuando se verifica completamente un objeto ya existente en destino.

Al cambiar de etapa, al comenzar un objeto y después de completar correctamente una operación, el marcador volverá a `UNKNOWN`. Esto evita atribuir a la operación anterior un fallo posterior de integridad, validación o construcción del manifiesto.

`CHECK_DESTINATION_METADATA` continuará agrupando el listado exacto y el `HEAD` de destino de `HasDestinationMetadataAsync`.

`WRITE_AND_VERIFY_DESTINATION` cubrirá exclusivamente la llamada agrupada `PutStreamVerifiedAsync`.

`READ_DESTINATION_HASH` identificará la lectura completa y el cálculo de hash de un objeto ya existente en destino. Esta adenda no autoriza modificar `S3OperationStore.cs` ni distinguir internamente las operaciones realizadas por `PutStreamVerifiedAsync`.

## 6. Datos permitidos en el fallo contractual

El `JobExecutionException` conservará su `ErrorCode` exterior vigente. Su colección `Data` contendrá exclusivamente estas cinco entradas diagnósticas:

- `SGOL_REPLICA_STAGE`
- `SGOL_REPLICA_OPERATION`
- `SGOL_REPLICA_EXCEPTION_TYPE`
- `SGOL_REPLICA_HTTP_STATUS`
- `SGOL_REPLICA_S3_ERROR_CODE`

Reglas:

1. `SGOL_REPLICA_STAGE` aceptará solamente las etapas cerradas ya existentes de `ObjectReplica`; cualquier otro valor será `UNKNOWN`.
2. `SGOL_REPLICA_OPERATION` aceptará solamente las siete operaciones de la sección 4; cualquier otro valor será `UNKNOWN`.
3. `SGOL_REPLICA_EXCEPTION_TYPE` aceptará únicamente un token ASCII de 1 a 64 caracteres formado por letras, dígitos, punto, guion o guion bajo; de lo contrario será `UNKNOWN`.
4. `SGOL_REPLICA_HTTP_STATUS` será la representación decimal de un valor entre 100 y 599. Cualquier valor ausente o fuera de ese intervalo será `NONE`.
5. `SGOL_REPLICA_S3_ERROR_CODE` aplicará el mismo sanitizador cerrado de tokens. Un valor nulo, vacío, mayor de 64 caracteres o con cualquier carácter no permitido será `UNKNOWN`.
6. Para una excepción que no sea `AmazonS3Exception`, HTTP será `NONE` y el código S3 será `UNKNOWN`.
7. La excepción original no se incorporará como `InnerException` del fallo contractual ni del error sanitizado producido por el gate.
8. Inmediatamente al entrar al `catch`, la etapa, la operación, el tipo de excepción, el estado HTTP y el código S3 de la excepción original se capturarán y sanitizarán en variables locales.
9. Esa captura ocurrirá antes de invocar `TryPublishFailureAsync`.
10. `TryPublishFailureAsync`, sus operaciones internas y cualquier excepción que pudiera manejar durante la publicación del manifiesto de fallo no podrán sustituir, recalcular ni modificar los cinco valores capturados de la excepción original.
11. Las cinco entradas de `JobExecutionException.Data` se construirán exclusivamente a partir de esas variables locales ya sanitizadas.

## 7. Información prohibida

Ni producción, ni las pruebas, ni el gate, ni el artifact podrán registrar o incluir:

- `Exception.Message`;
- `StackTrace`;
- `InnerException`;
- `Exception.ToString()`;
- endpoints o URLs;
- nombres de buckets;
- claves o prefijos de objetos;
- metadata S3;
- hashes o contenido de objetos;
- credenciales, tokens o secretos;
- cadenas de conexión;
- cuerpos de solicitud o respuesta;
- mensajes crudos de AWS o del almacenamiento S3-compatible.

No se registrará el objeto `Exception`.

## 8. Conservación del comportamiento

Deben permanecer sin cambios:

1. los inicializadores y parámetros de `ListObjectsV2Request`;
2. los inicializadores y parámetros de `GetObjectMetadataRequest`;
3. los inicializadores y parámetros de `GetObjectRequest`;
4. la llamada y argumentos de `PutStreamVerifiedAsync`;
5. la configuración de los clientes S3;
6. políticas, identidades y credenciales;
7. `RetryMode`, cantidad de reintentos y timeouts;
8. orden de listado, metadata, hash, stream, escritura y verificación;
9. paginación y tamaño de lote;
10. reglas de no-overwrite;
11. verificación de tamaño, SHA-256 y media type;
12. reintento acotado ya existente en el gate;
13. detención inmediata de fallos de integridad;
14. códigos `REPLICA_AUTHORIZATION_FAILED`, `REPLICA_INFRASTRUCTURE_FAILED` y todos los códigos de integridad;
15. manifiestos, telemetría, logging exterior y semántica de réplica.

Una `AmazonS3Exception` HTTP 500 continuará produciendo `REPLICA_INFRASTRUCTURE_FAILED`.

## 9. Exposición en el gate HU-035

`FunctionalRecoveryAmd64GateTests` compondrá el fallo sanitizado de preparación con este formato cerrado:

`HU035_REPLICA_PREPARATION_FAILED:<ERROR_CODE>:STAGE=<STAGE>:OPERATION=<OPERATION>:TYPE=<TYPE>:HTTP=<HTTP>:S3CODE=<S3CODE>`

El texto no incluirá el mensaje de la excepción ni adjuntará la excepción capturada como `InnerException`.

El reintento continuará limitado exclusivamente a `REPLICA_INFRASTRUCTURE_FAILED`. Los fallos de integridad no se reintentarán.

## 10. Pruebas enfocadas obligatorias

Dentro de `FunctionalRecoveryAmd64GateTests.cs` se añadirán pruebas bajo la categoría enfocada `Hu035ReplicaDiagnostics` que demuestren:

1. aceptación exacta de los siete marcadores cerrados;
2. conversión de cualquier operación diferente a `UNKNOWN`;
3. sanitización positiva de `AmazonS3Exception` e `InternalError`;
4. rechazo de tokens vacíos, demasiado largos, con espacios, saltos de línea, URLs o forma de credencial;
5. HTTP válido entre 100 y 599;
6. conversión de cero y valores fuera del rango HTTP a `NONE`;
7. código S3 nulo o inseguro convertido a `UNKNOWN`;
8. presencia exclusiva de las cinco claves permitidas en `JobExecutionException.Data`;
9. conservación de `REPLICA_INFRASTRUCTURE_FAILED` para una `AmazonS3Exception` HTTP 500;
10. ausencia de endpoint, bucket, key, metadata, credenciales, contenido y mensaje centinela tanto en `Data` como en el error compuesto por el gate;
11. ausencia de `InnerException` en la salida sanitizada;
12. conservación del reintento únicamente para `REPLICA_INFRASTRUCTURE_FAILED`;
13. detención inmediata ante un fallo de integridad;
14. diferenciación entre `READ_SOURCE_HASH` y `READ_DESTINATION_HASH`;
15. asociación exclusiva de `WRITE_AND_VERIFY_DESTINATION` con `PutStreamVerifiedAsync`.

Las pruebas diagnósticas puras no requerirán Docker, PostgreSQL, un servicio S3 ni variables del simulacro.

El corte `Category=Hu035ReplicaDiagnostics` no ejecutará la fase `Hu035Amd64`, no invocará el simulacro integral y no accederá a configuración obligatoria de su entorno.

## 11. Validación estática indispensable

Se autoriza modificar `scripts/ci/validate-hu-035.ps1` exclusivamente porque las pruebas puras pueden validar el inventario y la sanitización, pero no demostrar que cada marcador quedó situado inmediatamente antes de la llamada S3 correspondiente.

El validador comprobará solamente:

1. presencia exacta de los siete marcadores;
2. asociación de cada marcador con la llamada autorizada;
3. asociación de `READ_DESTINATION_HASH` con `ReadObjectHashAsync` sobre el destino;
4. asociación exclusiva de `WRITE_AND_VERIFY_DESTINATION` con `PutStreamVerifiedAsync`;
5. presencia de `S3CODE` en el error sanitizado del gate;
6. captura y sanitización de los cinco valores diagnósticos antes de `TryPublishFailureAsync`;
7. ausencia de usos diagnósticos de `Message`, `StackTrace`, `InnerException` y `ToString()`;
8. conservación de los inicializadores y argumentos relevantes de las solicitudes;
9. conservación del número de intentos y del filtro de reintento del gate;
10. inexistencia de otros marcadores de operación.

El validador no ejecutará S3, Docker, PostgreSQL ni el gate integral.

## 12. Archivos autorizados

La aprobación íntegra autorizará exclusivamente:

1. `F07_ADENDA_35_DIAGNOSTICO_SANITIZADO_DE_REPLICA_HU_035.md`;
2. `src/Sgol.Operations/ObjectReplica.cs`;
3. `tests/Sgol.OperationsIntegrationTests/FunctionalRecoveryAmd64GateTests.cs`;
4. `scripts/ci/validate-hu-035.ps1`, únicamente para las comprobaciones estáticas de la sección 11;
5. `docs/traceability/IMPLEMENTATION_STATUS.md`, exclusivamente para referenciar esta adenda como contrato complementario de `HU-035`, conservar el estado no terminado y registrar que el cambio es diagnóstico.

La referencia mínima de trazabilidad es obligatoria porque esta adenda pasa a formar parte del contrato que gobierna la implementación incluida en el PR. No se autoriza modificar estados, dependencias, criterios ni evidencias históricas distintas de esa referencia.

Cualquier archivo adicional requiere una decisión contractual y autorización separadas.

## 13. Gates enfocados

Después de la aprobación íntegra de esta adenda, el orden será:

1. prueba exacta de sanitización y normalización;
2. prueba exacta del inventario cerrado de siete operaciones;
3. prueba exacta de diferenciación entre `READ_SOURCE_HASH` y `READ_DESTINATION_HASH`;
4. prueba exacta de composición del error con `S3CODE` y ausencia de datos sensibles;
5. prueba exacta de conservación de `REPLICA_INFRASTRUCTURE_FAILED`;
6. corte puro:

   ```powershell
   rtk dotnet test tests/Sgol.OperationsIntegrationTests/Sgol.OperationsIntegrationTests.csproj --configuration Release -p:SGOL_HU035_AMD64_TESTS=true --filter "Category=Hu035ReplicaDiagnostics"
   ```

7. compilación Release enfocada de `Sgol.OperationsIntegrationTests`;
8. `scripts/ci/validate-hu-035.ps1`;
9. `git diff --check`;
10. revisión explícita del diff contra `b61f23c5aa777d1a682ba53fac181877d0218e38`;
11. comprobación de que únicamente cambiaron las rutas autorizadas y que `Fuentes/` permanece intacta.

El corte puro incluirá condicionalmente `FunctionalRecoveryAmd64GateTests.cs` mediante `-p:SGOL_HU035_AMD64_TESTS=true`, pero sólo seleccionará `Category=Hu035ReplicaDiagnostics`. No ejecutará la fase `Hu035Amd64` ni requerirá variables del simulacro.

No se repetirá localmente el gate integral TECH-OPS ni el gate integral HU-035. El host local ARM64 no proporciona evidencia de aceptación para el contrato Linux AMD64 nativo.

Una futura ejecución CI sobre un SHA nuevo requerirá autorización separada de commit y publicación. No habrá reejecución automática.

## 14. Criterio de éxito de esta instrumentación

La instrumentación queda validada cuando:

1. todos los gates enfocados pasan;
2. las siete operaciones cerradas quedan normalizadas y probadas;
3. el diff demuestra ausencia de cambios en solicitudes y comportamiento;
4. el diagnóstico exterior sólo puede contener valores cerrados y sanitizados;
5. una futura ejecución AMD64 nativa identifica una de las siete operaciones o `UNKNOWN`;
6. `READ_DESTINATION_HASH` distingue la lectura completa de un objeto ya existente en destino;
7. `WRITE_AND_VERIFY_DESTINATION` identifica exclusivamente la llamada agrupada `PutStreamVerifiedAsync`;
8. no se expone información prohibida;
9. la publicación del manifiesto de fallo no puede alterar el diagnóstico capturado de la excepción original.

Esto no satisface por sí mismo `CA-035`, `CP-035-P` ni `CP-035-N`, no corrige el HTTP 500 y no termina `HU-035`.

## 15. Riesgos y límites

1. `CHECK_DESTINATION_METADATA` no distinguirá entre su listado y su `HEAD` internos.
2. `WRITE_AND_VERIFY_DESTINATION` sólo identificará la llamada agrupada `PutStreamVerifiedAsync`; no distinguirá sus operaciones S3 internas.
3. `READ_DESTINATION_HASH` identificará la llamada exterior que lee y calcula el hash completo de un objeto ya existente, pero no separará internamente la obtención del stream y su cálculo de hash.
4. Las siete operaciones identifican puntos exteriores de `ObjectReplica`; no instrumentan internamente `S3OperationStore`.
5. La operación identificada será la última operación exterior iniciada; el restablecimiento inmediato a `UNKNOWN` evita atribuciones posteriores incorrectas.
6. La captura previa a `TryPublishFailureAsync` preservará el diagnóstico original, pero no hará que la publicación del manifiesto de fallo sea evidencia de la causa.
7. Un HTTP 500 puede seguir siendo transitorio, específico de SeaweedFS o dependiente de la forma de solicitud. Esta adenda no decide la causa.
8. Una futura corrección funcional sólo podrá proponerse después de obtener evidencia causal suficiente y requerirá autorización separada.
9. El pipeline verde seguirá siendo necesario pero no suficiente para cerrar `HU-035`.

## 16. Exclusiones

No se autoriza:

- modificar `S3OperationStore.cs`;
- modificar solicitudes S3;
- agregar o cambiar políticas;
- cambiar credenciales o identidades;
- cambiar reintentos o timeouts;
- modificar infraestructura o datos sintéticos;
- añadir compensación, overwrite o recreación;
- convertir HTTP 500 en ausencia de objeto;
- modificar manifiestos o semántica de réplica;
- ejecutar localmente el gate integral AMD64;
- crear commit o publicar;
- reejecutar CI;
- abrir otro PR;
- hacer merge;
- iniciar `CV-05` u otra historia.

## 17. Eficacia y aprobación

No existe aprobación parcial implícita. Si algún punto no es aceptable, la propuesta debe corregirse antes de modificar archivos.

La aprobación íntegra de esta adenda autoriza su materialización, la implementación local y los gates enfocados definidos en la sección 13. No se requerirá una segunda autorización para comenzar esa implementación.

La aprobación no autoriza commit, push, ejecución o reejecución de CI, nuevo PR ni merge.

La pregunta de aprobación es:

**¿Se aprueba íntegramente `F07_ADENDA_35_DIAGNOSTICO_SANITIZADO_DE_REPLICA_HU_035.md`, sin cambios, para autorizar su materialización, la implementación local diagnóstica y los gates enfocados en los archivos y bajo los límites indicados, sin autorizar commit, push, CI ni merge?**
