# Primitivas HTTP y técnicas de TECH-BASE-005

Este corte registra únicamente las primitivas compartidas mínimas. No introduce reglas de negocio, auditoría funcional, Identity, tablas funcionales ni endpoints de módulos.

## Tiempo e identificadores

- `IClock.UtcNow` es el puerto para instantes UTC; `SystemClock` es el adaptador predeterminado y las pruebas pueden sustituirlo por un reloj fijo.
- `IUuidGenerator.NewUuid()` crea UUID v7 usando el reloj inyectado. Los códigos funcionales aprobados siguen siendo texto y no se sustituyen por UUID.

## Correlación y errores HTTP

- El encabezado de entrada y salida es `X-Correlation-ID`.
- Sólo se propaga un UUID v7 válido. Si falta o no es válido, el servidor genera uno nuevo; el valor rechazado no se refleja ni se registra.
- Todas las respuestas devuelven el encabezado. Las respuestas Problem Details incluyen además `correlationId`, `status` e `instance` y usan `application/problem+json`.
- Una excepción no controlada devuelve un mensaje genérico. Su mensaje, stack y datos internos no se escriben en la respuesta ni en el log técnico.

## Logging seguro

La consola usa JSON con tiempo UTC y scopes. El middleware registra servicio, ambiente, versión, `correlationId`, actor técnico disponible, operación definida por el endpoint y resultado HTTP. No registra query string, encabezados, cuerpos ni valores de ruta aportados por el cliente.

Queda prohibido pasar como propiedad o excepción al logger contraseñas, hashes, TOTP, códigos de recuperación, cookies, URLs firmadas, claves, cadenas completas de conexión, binarios o contenido de evidencia. La auditoría funcional transaccional permanece fuera del alcance de esta primitiva.
