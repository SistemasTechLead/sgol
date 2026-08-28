# SGOL — Fase 19 · Integraciones y automatizaciones externas

## Propósito del dominio

Este dominio define cómo el SGOL intercambia información con sistemas, archivos, servicios y canales externos sin convertirlos automáticamente en fuentes de verdad internas ni duplicar información que pertenece a otra plataforma.

Una integración existe únicamente cuando hay un contrato identificable de entrada o salida, reglas de validación, identidad externa, trazabilidad, idempotencia y tratamiento de errores. La sola mención de un sistema dentro de una tarea o flujo no constituye una integración implementada.

## Principios obligatorios

- Cada fuente externa debe declarar qué sistema es dueño del dato y qué parte del dato necesita realmente SGOL.
- SGOL no debe copiar una base externa completa cuando basta una referencia, un evento, un snapshot mínimo o una vista derivada.
- Toda integración tiene un contrato versionado de entrada y/o salida.
- Un evento externo nunca evita permisos, validaciones, gates ni máquinas de estado de los dominios F2-F18.
- Recibir un dato no equivale a autorizar una acción de negocio.
- Las credenciales, secretos y tokens de integración no forman parte de tablas de negocio ni de documentos visibles para usuarios finales.
- Un reintento no puede producir dos obligaciones, dos movimientos ni dos efectos externos equivalentes.
- Todo mensaje entrante debe disponer de una clave de idempotencia proveniente del sistema origen o de una huella estable definida por contrato.
- Todo comando saliente debe usar un identificador de operación estable y registrar el resultado del proveedor externo cuando sea necesario para recuperación.
- Los errores de transporte se distinguen de los errores de dominio.
- Los mensajes rechazados por esquema o reglas de negocio no se reintentan indefinidamente.
- Los reintentos sólo aplican a fallos transitorios y siguen una política explícita de intentos, espera y escalamiento.
- Cuando se use sincronización por lotes, cada lote conserva versión de esquema, origen, huella, conteos y estado de reconciliación.
- Cuando se use sincronización incremental, el checkpoint se avanza únicamente después de aplicar correctamente el lote o evento confirmado.
- Mensajería, correo o WhatsApp son canales; no sustituyen la entidad, estado o evidencia del proceso que comunican.
- Una plataforma futura de comercio electrónico no se considera activa hasta que exista plataforma seleccionada, contrato, política, inventario y protección de datos aprobados.
- Los proyectos `AUT-*` describen oportunidades de automatización y motores futuros. No equivalen por sí mismos a adaptadores externos disponibles.
- F17 gobierna corridas, auditoría, correlación, errores e idempotencia transversal. F19 agrega la semántica específica de intercambio externo.

## Modelo de integración

### `SistemaExterno`

Catálogo de sistemas, servicios, fuentes o canales con los que SGOL puede intercambiar información.

Atributos mínimos:
- `id_sistema_externo`;
- `codigo`;
- `nombre`;
- `tipo`;
- `dueno_dato`;
- `estado`;
- `clasificacion_datos`;
- `responsable_negocio`;
- `responsable_tecnico`.

No almacena credenciales.

### `ConfiguracionIntegracion`

Contrato lógico y versionado de una integración concreta.

Atributos mínimos:
- `id_integracion`;
- `id_sistema_externo`;
- `nombre`;
- `direccion` (`ENTRADA`, `SALIDA`, `BIDIRECCIONAL`);
- `modo_intercambio`;
- `version_contrato`;
- `version_esquema`;
- `estado`;
- `politica_idempotencia`;
- `politica_reintento`;
- `vigente_desde`;
- `vigente_hasta`.

La ubicación física, endpoint y referencias de secretos pertenecen a configuración técnica segura, no a la lógica de negocio.

### `LoteIntegracion`

Representa una importación o exportación por lote cuando su recuperación o reconciliación necesita persistencia.

Atributos mínimos:
- `id_lote_integracion`;
- `id_integracion`;
- `id_corrida_sistema`;
- `identificador_origen`;
- `huella_contenido`;
- `version_esquema`;
- `recibido_en`;
- `estado`;
- `registros_recibidos`;
- `registros_aplicados`;
- `registros_rechazados`;
- `checkpoint_origen` cuando corresponda.

### `MensajeIntegracion`

Se persiste únicamente cuando un mensaje individual necesita reintento, recuperación, correlación o auditoría.

Atributos mínimos:
- `id_mensaje_integracion`;
- `id_integracion`;
- `direccion`;
- `id_mensaje_externo` cuando exista;
- `id_operacion` F17;
- `clave_idempotencia`;
- `tipo_mensaje`;
- `version_esquema`;
- `estado`;
- `recibido_o_emitido_en`;
- `numero_intentos`;
- `ultimo_error` o referencia al error F17;
- `correlacion_negocio`.

El payload completo sólo se conserva cuando sea necesario y permitido por la política de datos.

### `ReferenciaExterna`

Vincula una entidad SGOL con la identidad asignada por otro sistema.

Atributos mínimos:
- `id_referencia_externa`;
- `id_sistema_externo`;
- `tipo_entidad_sgol`;
- `id_entidad_sgol`;
- `tipo_objeto_externo`;
- `id_objeto_externo`;
- `vigente_desde`;
- `vigente_hasta`.

Una referencia externa no sustituye la clave primaria canónica de SGOL.

### `CheckpointSincronizacion`

Se utiliza únicamente en integraciones que leen cambios incrementales de una fuente.

Atributos mínimos:
- `id_integracion`;
- `particion` cuando aplique;
- `cursor_confirmado`;
- `confirmado_en`;
- `id_lote_integracion`.

## Componentes lógicos

### `adaptador_sistema_externo`
Traduce el protocolo o formato específico de un proveedor hacia contratos internos estables. La lógica de negocio no conoce detalles del proveedor.

### `validar_mensaje_integracion(mensaje)`
Valida versión de esquema, campos obligatorios, tipos, integridad, permisos técnicos y reglas mínimas de recepción antes de entregar el mensaje al dominio.

### `normalizar_evento_externo(mensaje)`
Convierte el mensaje del proveedor en un evento interno canónico sin modificar estado de negocio.

### `procesar_evento_externo(evento)`
Handler que entrega el evento al dominio correspondiente. Si el evento activa una obligación, utiliza F5/F6; si actualiza una referencia, respeta el dominio dueño de esa entidad.

### `importar_lote(id_integracion, origen)`
Importa un lote de forma idempotente, valida estructura, crea la corrida F17 y produce conciliación de conteos.

### `exportar_lote(id_integracion, alcance)`
Genera una salida controlada y versionada sin modificar directamente la fuente externa hasta que el adaptador confirme la entrega.

### `enviar_comando_externo(comando)`
Reserva la idempotencia F17, registra correlación y entrega el comando al adaptador. Nunca se considera completado únicamente porque se intentó enviar.

### `reintentar_mensaje(id_mensaje)`
Reintenta únicamente errores clasificados como transitorios y respeta el máximo de intentos y la ventana de reintento.

### `reconciliar_integracion(alcance)`
Compara origen y destino mediante referencias, conteos y estados esperados. Una sincronización no se declara correcta sólo porque el transporte terminó sin excepción.

## Estados de mensajes entrantes

- `RECIBIDO`;
- `DUPLICADO`;
- `VALIDADO`;
- `RECHAZADO_ESQUEMA`;
- `RECHAZADO_DOMINIO`;
- `APLICADO`;
- `ERROR_REINTENTABLE`;
- `ERROR_FINAL`.

## Estados de mensajes salientes

- `PENDIENTE_ENVIO`;
- `ENVIANDO`;
- `ENVIADO`;
- `CONFIRMADO`;
- `ERROR_REINTENTABLE`;
- `ERROR_FINAL`;
- `CANCELADO`.

`ENVIADO` no implica que el efecto de negocio externo haya sido confirmado si el proveedor distingue aceptación de procesamiento.

## Reglas de idempotencia

- Si el proveedor entrega un identificador de evento estable, la unicidad se controla por `id_integracion + id_mensaje_externo`.
- Si no existe identificador externo, el contrato debe definir una huella estable. No se usa un timestamp de recepción como sustituto.
- Una misma operación saliente reutiliza el mismo `id_operacion` en sus reintentos.
- Un mensaje duplicado puede registrarse para auditoría, pero no vuelve a aplicar el efecto de negocio.
- La creación de obligaciones por eventos externos conserva la clave idempotente F5/F6 además de la idempotencia del mensaje de integración.

## Automatizaciones y handlers candidatos

### Personal y nómina
Una actualización válida de la fuente de personal puede alimentar F1 y preparar salidas F15. La importación no autoriza cambios laborales ni económicos por sí sola.

### Inventario y recepción
Una diferencia de inventario, recepción o proveedor puede generar un evento interno. Las altas, bajas, ajustes, traspasos o descuentos continúan sujetos a autoridad de negocio y no se ejecutan sólo por recibir el evento.

### Comercio electrónico
Cuando exista plataforma aprobada, pedidos e incidencias podrán producir eventos con referencia al pedido externo. La ausencia de plataforma mantiene deshabilitada esta integración.

### Mensajería y CRM
Los mensajes pueden notificar, recibir respuestas o abrir casos según contrato. El estado del caso vive en SGOL y no en el hilo de mensajería.

### Facturación
Una solicitud digital puede crear un caso, medir SLA y registrar la respuesta del proveedor/contador. SGOL no sustituye al sistema fiscal que emite el CFDI.

### Conciliación financiera
Movimientos o documentos externos pueden alimentar reglas de conciliación. Los efectos económicos continúan bajo F15 y los permisos/autorizaciones aplicables.

## Dependencias con otros dominios

- F1: personal, puestos, disponibilidad y capacidad provenientes de fuente externa.
- F2: permisos y autoridad para operaciones que impliquen lectura o escritura sensible.
- F5-F6: eventos externos que pueden activar y generar obligaciones.
- F10-F13: casos y excepciones originados por eventos externos.
- F11: evidencias asociadas a archivos o recursos externos.
- F12: validaciones y autorizaciones previas a efectos sensibles.
- F15: integración económica y movimientos hacia nómina.
- F17: corridas, idempotencia, correlación, errores, transacciones y auditoría.
- F18: coexistencia temporal cuando una fuente legacy y un adaptador nuevo convivan.
- F16: vistas que muestran estado de sincronización o incidencias, nunca tablas duplicadas de la fuente externa.

## Condiciones excepcionales

- `INTEGRACION_NO_CONFIGURADA`;
- `VERSION_ESQUEMA_NO_SOPORTADA`;
- `MENSAJE_DUPLICADO`;
- `IDENTIDAD_EXTERNA_DESCONOCIDA`;
- `REFERENCIA_EXTERNA_AMBIGUA`;
- `FUENTE_NO_DISPONIBLE`;
- `AUTENTICACION_FALLIDA`;
- `PERMISO_EXTERNO_DENEGADO`;
- `ERROR_TRANSPORTE_REINTENTABLE`;
- `ERROR_TRANSPORTE_FINAL`;
- `RECHAZO_DE_DOMINIO`;
- `CONFLICTO_DE_SINCRONIZACION`;
- `CHECKPOINT_INCONSISTENTE`;
- `CONTRATO_NO_APROBADO`.

## Límites del dominio

F19 no define las reglas internas de inventario, facturación, crédito, nómina, recepción o CRM. Sólo define cómo SGOL intercambia información con sus fuentes o servicios externos.

Una automatización interna que no intercambia datos con sistemas externos pertenece al dominio funcional que automatiza y utiliza F17 para su operación técnica. Un proyecto `AUT-*` sólo se convierte en integración F19 cuando realmente exista una frontera externa y un contrato de intercambio.
