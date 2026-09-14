# Réplica verificable de objetos S3-compatible

## Ejecución

El operador programa cada hora, minuto `05`, UTC:

```text
dotnet Sgol.Worker.dll run-job --job REPLICATE_EVIDENCE_OBJECTS --scheduled-for <YYYY-MM-DDTHH:05:00Z>
```

La identidad fuente sólo lee/lista cuarentena y limpio. La identidad secundaria crea/lista/lee en buckets separados y no debe poder borrar ni sobrescribir. Ambas son distintas de la identidad Web.

## Semántica

- cada objeto exige metadata `sgol-sha256`, `sgol-size-bytes` y `sgol-media-type`;
- objeto nuevo se lee, recalcula y crea condicionalmente en destino;
- objeto ya verificado es no-op; a las `00:05Z` se relee para verificación completa;
- un destino ausente se crea sólo si la key no figuraba en el último manifiesto completo; si antes figuraba, falla como `DESTINATION_OBJECT_MISSING` y no se repara silenciosamente;
- objeto destino con tamaño/hash distinto falla y nunca se sobrescribe;
- cada manifiesto JCS `COMPLETE` es privado, append-only y ordenado por rol/key; incluye timestamp de verificación por entrada;
- se compara el manifiesto anterior: una key antes presente y ahora ausente produce `SOURCE_OBJECT_MISSING` y permanece en destino;
- el cliente limita a tres intentos totales de errores transitorios; integridad/autorización no se convierte en éxito.

El manifiesto se guarda bajo `objects/v1/YYYY/MM/DD/`. Si una corrida alcanza parcialmente el destino y luego falla, intenta conservar un manifiesto `FAILED` separado bajo `objects/v1/failures/`; la imposibilidad de escribirlo nunca convierte la corrida en éxito. Keys e hashes individuales sólo aparecen en esos manifiestos privados; logs y métricas contienen agregados de baja cardinalidad.

## Verificación independiente

```text
dotnet Sgol.Operations.dll verify-object-replica --manifest s3://<bucket-manifiestos>/<key>.manifest.json
```

El verificador relee cada objeto secundario, recalcula SHA-256 y exige igualdad origen/destino. Cualquier faltante o corrupción falla visiblemente. No existe reparación, overwrite ni propagación de borrado automática.
