# F07 Adenda 36 — Prueba enfocada de stream de réplica HU-035

## Decisión aprobada

El responsable aprobó el 2026-09-15 materializar este alcance, implementar la prueba y ejecutar sus pruebas puras. El identificador 36 se verificó libre antes de crear este archivo. Complementa las Adendas 33–35; no cambia CA-035, CP-035-P, CP-035-N ni declara HU-035 terminada.

El run 35031599495, intento 1, SHA 048f6c6a20e4a20b63316fa9b8ee199850c702e4 pasó TECH-OPS y falló HU-035 en REFERENCE / REPLICATE_CLEAN / WRITE_AND_VERIFY_DESTINATION / AmazonS3Exception / 500 / InternalError. La operación agrupada no identifica aún PUT, GET o HEAD. La prueba es diagnóstica, no una corrección del HTTP 500.

## Alcance y conservación

Se autoriza una clase independiente en ObjectReplicaExternalTests.cs con dos SeaweedFS nuevos, imagen fijada y recursos exclusivamente sintéticos. No reutiliza un entorno anterior. Conserva las políticas de aprovisionamiento vigentes, aplicadas a recursos propios, con identidades REPLICA_SOURCE y REPLICA_DESTINATION separadas de los administradores de preparación.

Usa S3EndpointOptions.CreateClient(): Standard, tres intentos totales, timeout 60 segundos y ForcePathStyle; HTTP sólo en el entorno sintético privado. Usa S3OperationStore.PutStreamVerifiedAsync real: GET de origen, su ResponseStream intacto, PUT condicional, GET/hash y HEAD. No copia ni envuelve el stream, no reproduce la lógica de almacenamiento, no agrega reintentos ni cambia solicitudes, metadata o excepciones productivas.

Un adaptador de prueba IAmazonS3 delega exactamente una vez cada llamada con el mismo request y stream por referencia y el mismo CancellationToken (tipo valor). Marca PUT_DESTINATION, GET_DESTINATION_VERIFY o HEAD_DESTINATION_METADATA; sólo restablece el marcador tras éxito. Conserva la secuencia de consulta de metadata que producción realiza después de un conflicto de PUT, sin decidir su resultado. Conserva además la última llamada completada para atribuir fallos durante el consumo/verificación posterior de su respuesta, sin afirmar que el servidor rechazó esa llamada.

El GET de origen y la preparación tienen marcadores separados. Al fallar se captura inmediatamente un texto con operación cerrada, tipo sanitizado, HTTP 100–599 o NONE y S3CODE sanitizado o UNKNOWN. Tokens ASCII de 1–64 caracteres: letras, dígitos, punto, guion y guion bajo. No se registra Exception, mensaje crudo, stack trace original, inner exception, endpoint, bucket, clave, metadata, hash, contenido ni credencial. El fallo exterior no adjunta la excepción original. La limpieza sólo elimina contenedores y archivos creados por esta prueba; su error sanitizado se conserva sin reemplazar el primero.

## Archivos autorizados

- F07_ADENDA_36_PRUEBA_ENFOCADA_DE_STREAM_DE_REPLICA_HU_035.md.
- tests/Sgol.OperationsIntegrationTests/ObjectReplicaExternalTests.cs.
- docs/traceability/IMPLEMENTATION_STATUS.md: sólo referencia contractual mínima.
- .github/workflows/pull-request.yml: una ejecución enfocada en Linux AMD64 nativo, después de limpiar TECH-OPS y antes de HU-035, y conservación del TRX.

No se autoriza modificar producción, dependencias, políticas existentes, proyectos ni otras rutas. El workflow sólo admite el paso enfocado descrito, cuya implementación local fue aprobada el 2026-09-17.

## Gates y límites

### Ajuste enfocado aprobado el 2026-09-17: retirada del administrador y reinicio

Se autoriza exclusivamente en esta adenda y ObjectReplicaExternalTests.cs incorporar el ciclo de preparación omitido por la sonda inicial: después de crear ambos conjuntos de buckets y sembrar el objeto, retirar sólo el administrador de cada configuración propia. Se reutilizan literalmente las mismas identidades operativas, credenciales y permisos. Se actualiza el archivo montado in situ mediante File.WriteAllText con UTF-8 sin BOM, como Write-Utf8File/New-S3Configuration del aprovisionamiento HU-035, sin reemplazo ni renombrado del archivo.

Cada contenedor propio realiza una sola secuencia StopAsync/StartAsync, con un límite de 60 segundos por contenedor que incluye la espera TCP de disponibilidad ya configurada. Después de ambos reinicios se reconstruyen los endpoints con el Hostname y GetMappedPublicPort(8333) actuales de cada contenedor, actualizando exclusivamente Endpoint en las opciones existentes. Se conservan credenciales, región, permisos, reintentos, timeout y las demás opciones. Sólo después se crean los clientes operativos. El ciclo completo conserva el marcador PROVISION; los fallos de transferencia mantienen los marcadores existentes. Se conserva el primer error sanitizado y la limpieza propia incluso si falla el reinicio.

No se cambian objeto, solicitudes, stream, reintentos, recorrido PUT/GET/HEAD, redes ni slots. El ajuste de endpoints maneja el cambio de mapeo observado tras Stop/Start; no declara equivalencia con docker restart. La variante local pasó 1/1 y no reprodujo el fallo original de CI. No corrige ni atribuye la causa del HTTP 500. No se repite esa prueba externa ya aprobada; la revisión de coherencia sólo ejecuta git diff --check y las pruebas puras de diagnóstico afectadas. No autoriza commit, push, CI ni merge.

Primero se ejecuta Category=Hu035ReplicaStreamPure con -p:SGOL_TECH_OPS_EXTERNAL_TESTS=true. Debe probar delegación única, identidad de argumentos/respuestas, fallos asíncronos, conservación del marcador, conflictos gestionados por el almacenamiento real y sanitización. No inicializa Docker ni requiere variables externas. Se revisa git diff --check y el alcance de las rutas.

Después, el responsable ejecutará exclusivamente ReplicaResponseStreamIsConditionallyWrittenAndVerified con la misma propiedad de compilación. Se proporciona un solo comando y se espera su salida. La prueba crea y limpia sus recursos; no necesita PostgreSQL ni el pipeline completo.

La ejecución CI seleccionará por nombre completo exclusivamente la prueba ReplicaResponseStreamIsConditionallyWrittenAndVerified y comprobará en el TRX que se ejecutó y aprobó exactamente una prueba. No añadirá reejecuciones. Conservará el diagnóstico sanitizado y la limpieza implementados en la prueba. El fallo será bloqueante para HU-035 y el paso existente de artifacts conservará if: always() para recoger replica-stream.trx cuando falle la sonda, si el archivo se generó. Usará puertos publicados: tampoco reproduce la ruta privada Worker–SeaweedFS. La ejecución efectiva de CI seguirá requiriendo autorización separada.

La validación local del bloque TRX usará sólo datos sintéticos: una prueba aprobada acepta; cero pruebas, una fallida u omitida, más de una prueba y TRX ausente o inválido rechazan. No se repiten la prueba externa ni los gates ya aprobados.

Windows/ARM64, Docker Desktop y puertos publicados no reproducen Linux AMD64 nativo ni la ruta privada Worker–SeaweedFS. Se conserva la imagen fijada sin forzar emulación. Si la plataforma no puede ejecutarla, se reporta el límite sin alterar configuración. Un resultado local no acepta el gate AMD64, no acredita RPO/RTO ni identifica por sí solo la causa del HTTP 500.

No se autoriza ejecutar la prueba externa en la sesión del agente, commit, push, CI, nuevo PR, merge ni tareas posteriores.
