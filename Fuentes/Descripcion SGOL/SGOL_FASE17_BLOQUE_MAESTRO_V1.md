# SGOL — Fase 17 · Auditoría, logs, corridas, transacciones, respaldo y rollback

## Propósito del dominio

Este dominio define la trazabilidad técnica y operativa transversal del SGOL. Su responsabilidad es permitir reconstruir quién ejecutó una operación, qué entidad fue afectada, con qué resultado, dentro de qué corrida o correlación, qué error ocurrió y qué mecanismos de integridad o recuperación fueron aplicados.

La auditoría no sustituye el estado de negocio de los dominios F1-F16. El estado canónico vive en sus entidades propietarias; F17 conserva la evidencia de cómo cambió y de cómo se ejecutaron los procesos que lo modificaron.

## Principios obligatorios

- Auditoría de negocio, telemetría técnica, corrida de proceso, transacción e infraestructura de respaldo son conceptos distintos.
- Todo comando que cambie estado debe ejecutarse dentro de una frontera transaccional explícita.
- Una operación confirmada no puede quedar sin trazabilidad de auditoría cuando la política del dominio exija auditarla.
- Un fallo anterior al commit revierte la transacción; no se corrige mediante edición manual de filas ya confirmadas.
- Un respaldo es un mecanismo de recuperación ante desastre o corrupción; no es el mecanismo normal de rollback de un comando.
- La restauración desde respaldo es una operación administrativa extraordinaria, autorizada, verificada y auditada.
- Las corridas sólo se persisten cuando representan una ejecución con ciclo de vida propio: batch, job, importación, exportación, reconciliación, migración u orquestación relevante.
- Una función pura o consulta no crea una corrida por el simple hecho de ejecutarse.
- Toda operación modificadora recibe un `id_operacion` opaco y único. Las operaciones relacionadas comparten un `id_correlacion` cuando exista una cadena de acciones.
- Los identificadores no se derivan únicamente de fecha, nombre de usuario, número de fila o posición en una tabla.
- Los comandos reintentables deben soportar idempotencia explícita. Repetir la misma solicitud con la misma clave no puede duplicar efectos.
- Un mismo `idempotency_key` reutilizado con un contenido diferente debe rechazarse.
- Los errores de dominio y los errores técnicos se distinguen. Un rechazo por regla de negocio no se trata como corrupción técnica.
- Los errores críticos se propagan al llamador después de restaurar recursos temporales; no se silencian.
- Los logs y eventos de auditoría son append-only para usuarios normales. Correcciones de auditoría se realizan mediante nuevos eventos, nunca reescribiendo el histórico.
- Las vistas ejecutivas de auditoría son proyecciones de las fuentes canónicas de F17; no almacenan copias independientes.
- La información sensible se minimiza. No se guardan contraseñas, tokens ni secretos en logs o payloads de auditoría.
- Toda marca temporal persistente utiliza una convención temporal única y conserva la zona horaria o un estándar inequívoco.

## Entidades persistentes

### `EventoAuditoria`

Representa un hecho auditable sobre una entidad o decisión de negocio.

Atributos mínimos:
- `id_evento_auditoria`;
- `fecha_hora`;
- `id_operacion`;
- `id_correlacion` cuando aplique;
- `id_usuario` o identidad del actor técnico;
- `tipo_actor`;
- `accion`;
- `tipo_entidad`;
- `id_entidad`;
- `id_periodo_operativo` cuando aplique;
- `resultado`;
- `motivo` cuando sea requerido;
- `id_autorizacion` cuando exista autorización F12/F13/F14/F15;
- referencia o resumen controlado del estado anterior;
- referencia o resumen controlado del estado posterior;
- versión del servicio/componente;
- metadatos técnicos no sensibles.

Resultados mínimos:
- `OK`;
- `RECHAZADO`;
- `ERROR`.

`EventoAuditoria` es append-only.

### `CorridaSistema`

Existe sólo cuando una ejecución técnica necesita conservar inicio, fin, parámetros, resultado, conteos o error como una unidad persistente.

Atributos mínimos:
- `id_corrida`;
- `tipo_corrida`;
- `id_operacion` raíz;
- `id_correlacion`;
- `fecha_hora_inicio`;
- `fecha_hora_fin`;
- `estado_corrida`;
- `tipo_disparador`;
- `id_usuario_iniciador` o identidad de servicio;
- versión del componente;
- parámetros normalizados o referencia segura a ellos;
- hash de entrada cuando sea útil;
- conteos de entrada/salida;
- `id_corrida_origen` cuando sea un reintento;
- resumen del resultado;
- referencia al error principal cuando exista.

Estados:
- `INICIADA`;
- `COMPLETADA`;
- `FALLIDA`;
- `CANCELADA`.

Un reintento es una nueva `CorridaSistema` relacionada con la anterior; no se sobreescribe la corrida fallida.

### `RegistroIdempotencia`

Persiste el control de una solicitud que puede reintentarse o recibirse más de una vez.

Atributos mínimos:
- `idempotency_key`;
- `tipo_operacion`;
- `request_hash`;
- `estado`;
- `id_operacion`;
- referencia al resultado confirmado;
- `creado_en`;
- `actualizado_en`;
- expiración cuando la política lo permita.

Estados mínimos:
- `EN_PROCESO`;
- `COMPLETADA`;
- `FALLIDA`.

La política de reintento define cuándo una clave fallida puede reutilizarse o debe reemplazarse.

### `ErrorSistema`

Se persiste cuando el error necesita investigación, correlación, reintento, alerta o histórico operativo.

Atributos mínimos:
- `id_error`;
- `fecha_hora`;
- `id_operacion`;
- `id_correlacion`;
- `id_corrida` cuando aplique;
- `codigo_error`;
- `categoria_error`;
- `componente`;
- mensaje seguro;
- detalle técnico sanitizado o referencia a telemetría externa;
- `reintentable`;
- severidad;
- referencia a entidad afectada cuando sea conocida.

Los rechazos esperados por reglas de negocio pueden registrarse como `EventoAuditoria` con resultado `RECHAZADO` sin crear necesariamente un `ErrorSistema`.

### `PuntoRecuperacion`

Es metadato de infraestructura, no una copia del contenido de negocio dentro del mismo esquema.

Se persiste únicamente cuando el sistema necesita gobernar y verificar sus puntos de recuperación.

Atributos mínimos:
- `id_punto_recuperacion`;
- `fecha_hora_creacion`;
- tipo;
- referencia segura al almacenamiento;
- versión/esquema;
- checksum o evidencia de integridad;
- estado de verificación;
- política de retención;
- `creado_por`;
- última prueba de restauración cuando corresponda.

El contenido físico del respaldo vive fuera de la base de datos protegida por ese respaldo.

## Componentes no persistentes

### Frontera transaccional

Una transacción no es una tabla de negocio. Es la unidad atómica dentro de la cual se ejecutan los cambios que deben confirmarse o revertirse como conjunto.

Toda operación crítica debe definir:
- qué entidades modifica;
- qué validaciones ocurren antes del commit;
- qué evento de auditoría acompaña el cambio;
- qué eventos externos deben diferirse hasta después del commit o publicarse mediante un mecanismo transaccional seguro;
- qué resultado recibe el llamador ante fallo.

### `id_operacion`

Identificador opaco generado al inicio de cada comando modificador. Permite correlacionar auditoría, errores y, cuando exista, la corrida.

### `id_correlacion`

Agrupa múltiples operaciones relacionadas que forman un mismo flujo de negocio o técnico sin exigir que formen una única transacción.

## Servicios y funciones

### `generar_id_operacion()`
Genera un identificador único y opaco para una operación.

### `generar_id_corrida()`
Genera un identificador único únicamente cuando se creará una `CorridaSistema` persistente.

### `iniciar_corrida(tipo_corrida, contexto)`
Crea la corrida en estado `INICIADA` y devuelve `id_corrida`.

### `completar_corrida(id_corrida, resultado, conteos)`
Cierra una corrida confirmada como `COMPLETADA`.

### `fallar_corrida(id_corrida, error)`
Cierra una corrida como `FALLIDA` y la enlaza con `ErrorSistema` cuando corresponda.

### `registrar_evento_auditoria(evento)`
Registra de forma append-only un evento autorizado por la política de auditoría.

### `reservar_idempotencia(clave, tipo_operacion, request_hash)`
Determina si la solicitud puede ejecutarse, si ya fue completada o si la misma clave fue reutilizada con un contenido incompatible.

### `confirmar_idempotencia(clave, resultado)`
Asocia la clave con el resultado confirmado de la operación.

### `registrar_error(error)`
Persiste un error técnico cuando requiere trazabilidad operativa y devuelve `id_error`.

### `crear_punto_recuperacion()`
Solicita a la infraestructura un respaldo según la política vigente y registra su metadato cuando corresponda.

### `verificar_punto_recuperacion(id_punto_recuperacion)`
Verifica integridad y capacidad de restauración conforme a la política de continuidad.

### `restaurar_punto_recuperacion(id_punto_recuperacion, autorizacion)`
Ejecuta una recuperación administrativa controlada. No sustituye el rollback transaccional de comandos ordinarios.

## Patrón transaccional de comandos

Todo comando modificador sigue esta secuencia conceptual:

1. recibir actor, contexto, `idempotency_key` cuando aplique e `id_correlacion` opcional;
2. generar `id_operacion`;
3. validar autenticación, permisos y precondiciones de dominio;
4. reservar idempotencia cuando corresponda;
5. iniciar transacción;
6. leer y bloquear únicamente los recursos que requieran consistencia;
7. validar nuevamente las condiciones susceptibles de carrera;
8. aplicar cambios de negocio;
9. registrar dentro de la misma garantía transaccional la auditoría obligatoria o preparar su publicación segura;
10. confirmar transacción;
11. confirmar idempotencia;
12. publicar efectos externos posteriores al commit mediante un mecanismo confiable;
13. ante error previo al commit, hacer rollback automático, registrar el error fuera de la transacción fallida cuando corresponda y propagar el resultado.

Un comando no puede devolver éxito si una parte obligatoria de su cambio quedó sin confirmar.

## Política de auditoría por dominio

Debe auditarse como mínimo:
- creación de obligaciones F6;
- asignación y reasignación confirmadas F8/F13;
- publicación de planes F9;
- inicio y conclusión de ejecución F10;
- alta, sustitución o invalidación de evidencia F11 cuando sea relevante;
- decisiones y autorizaciones F12;
- excepciones F13;
- cierre extraordinario o corrección postcierre F14;
- aprobación o corrección de resultados económicos F15;
- cambios de configuración, roles, permisos o políticas que alteren comportamiento;
- restauraciones administrativas;
- migraciones y reconciliaciones F18;
- integraciones F19 cuando produzcan efectos persistentes.

Las lecturas ordinarias no requieren auditoría individual salvo que la política de seguridad determine que el acceso a información sensible debe registrarse.

## Reglas de error

- `ERROR_DOMINIO`: la solicitud viola una regla de negocio; no implica fallo de infraestructura.
- `ERROR_CONCURRENCIA`: el estado cambió entre lectura y commit y la operación no puede aplicarse con seguridad.
- `ERROR_IDEMPOTENCIA`: la clave ya representa otra solicitud o existe una ejecución incompatible.
- `ERROR_INTEGRACION`: una dependencia externa falló o devolvió un contrato inválido.
- `ERROR_INFRAESTRUCTURA`: base de datos, almacenamiento, red o servicio no disponible.
- `ERROR_DATOS`: información persistente incompatible con invariantes requeridas.
- `ERROR_INTERNO`: fallo no clasificado del componente.

Los mensajes mostrados al usuario deben ser seguros; el detalle técnico queda restringido a perfiles autorizados.

## Respaldo, recuperación y rollback

### Rollback transaccional

Aplica a cambios no confirmados dentro de una operación. Es automático y no requiere reconstruir manualmente los valores anteriores.

### Compensación

Cuando una acción externa ya fue confirmada y no puede revertirse con la misma transacción, el dominio propietario define una operación compensatoria explícita. La compensación es un nuevo hecho auditable; no se denomina rollback técnico.

### Respaldo

Protege frente a pérdida, corrupción o incidentes mayores. La política debe definir:
- alcance;
- frecuencia;
- retención;
- cifrado;
- almacenamiento independiente;
- integridad;
- RPO/RTO requeridos;
- pruebas periódicas de restauración.

### Restauración

Una restauración requiere autorización, ventana controlada, verificación posterior y auditoría completa. La restauración no se ejecuta como respuesta ordinaria a una validación fallida de negocio.

## Permisos

- Los servicios del sistema pueden escribir auditoría sólo mediante interfaces controladas.
- Ningún usuario operativo puede modificar o borrar eventos de auditoría.
- La consulta de auditoría detallada requiere permiso específico F2.
- Dirección puede consumir una vista ejecutiva autorizada sin obtener acceso irrestricto a información técnica sensible.
- La creación o restauración de puntos de recuperación requiere privilegio administrativo separado del permiso operativo ordinario.

## Vistas de lectura

### `ViewAuditoriaEjecutiva`
Proyección de eventos relevantes para Dirección según permisos y alcance.

### `ViewCorridasSistema`
Consulta de corridas por tipo, estado, período, iniciador, duración y resultado.

### `ViewErroresOperativos`
Consulta restringida de errores técnicos con correlación a corrida/operación.

### `ViewTrazabilidadEntidad`
Reconstruye los eventos auditables asociados a una entidad de negocio sin convertir la auditoría en su fuente de estado actual.

## Dependencias

- F2 aporta identidad, roles, permisos y autoridad de consulta.
- F3 aporta `PeriodoOperativo` y contexto temporal.
- F5-F15 aportan los comandos y hechos que deben correlacionarse y auditarse.
- F19 define correlación, reintentos, outbox/inbox y trazabilidad de integraciones externas.
- F20 consolida retención, índices, claves, particionamiento y políticas de integridad del modelo definitivo.

## Errores y condiciones excepcionales

- `AUDITORIA_REQUERIDA_NO_DISPONIBLE`: un comando crítico no puede garantizar la auditoría obligatoria.
- `IDEMPOTENCY_KEY_REUSED`: una clave ya pertenece a una solicitud diferente.
- `CORRIDA_NO_ENCONTRADA`: se intenta finalizar o consultar una corrida inexistente.
- `TRANSACCION_CONFLICTO`: la operación no puede confirmar por concurrencia o invariantes.
- `RESPALDO_NO_VERIFICADO`: un punto de recuperación no cumple la verificación requerida.
- `RESTAURACION_NO_AUTORIZADA`: falta autoridad para iniciar recuperación.
- `ERROR_NO_CORRELACIONABLE`: un error crítico no pudo asociarse con una operación válida; debe escalarse como defecto de observabilidad.
