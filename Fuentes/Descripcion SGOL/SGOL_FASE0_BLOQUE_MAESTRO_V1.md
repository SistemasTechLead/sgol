# SGOL — Fase 0 · Bloque limpio para Documento Maestro

## Propósito del inventario semántico

El SGOL se compone de dominios operativos que deben mantenerse separados de las estructuras técnicas utilizadas para implementarlos. El inventario semántico identifica responsabilidades de negocio, datos persistentes, cálculos, reglas, transacciones, eventos, permisos, auditoría, integraciones y vistas de usuario sin fijar todavía un esquema relacional definitivo.

## Mapa funcional identificado

### 1. Configuración y calendario
El sistema mantiene parámetros operativos que gobiernan año y semana activa, estado del sistema, estado del ciclo semanal, fuentes de datos y reglas de validación. El calendario operativo aporta fechas, semana, día, mes, condición de fin de mes y tratamiento de días no laborables. Los atributos que puedan calcularse de forma determinista no requieren persistencia independiente salvo que exista valor de auditoría o una excepción administrada por el negocio.

### 2. Definición canónica de tareas
Existe un catálogo de obligaciones operativas con identidad estable. Cada tarea puede incluir responsable o puesto requerido, supervisión, categoría y proceso, duración, frecuencia o condición de activación, ventana u hora sugerida, criticidad, evidencia, validación, diferimiento, automatización, KPI y vigencia. La definición de una tarea no equivale a una obligación activa.

### 3. Activación y generación de obligaciones
El sistema distingue activaciones programadas, por evento y condicionales. La activación debe evaluar calendario, SLA, parámetros, contexto, duplicidad y coexistencia antes de crear una obligación real. La generación de una instancia debe ser idempotente y conservar el vínculo con la definición, el período y el contexto que originó la obligación.

### 4. Personal, disponibilidad, capacidad y elegibilidad
La disponibilidad del personal se obtiene de una fuente operativa de nómina/capacidad y se transforma en información utilizable por SGOL. El sistema determina personal activo, puesto, turno, porcentaje disponible, minutos disponibles, minutos asignados y minutos libres. La elegibilidad combina compatibilidad de puesto, turno, disponibilidad diaria y capacidad suficiente, y debe poder explicar el motivo de inclusión o exclusión de cada candidato.

### 5. Balanceo y asignación
La asignación selecciona un responsable entre candidatos elegibles mediante reglas de prioridad y capacidad. El cálculo de candidatos, scoring y ranking es derivable; el resultado de asignación debe persistirse únicamente cuando representa una decisión operativa que deba conservarse.

### 6. Planificación y publicación
El plan publicado representa obligaciones operativas aceptadas para un período. Conserva identidad, tarea de origen, período, responsable, supervisor, estado, evidencia requerida, reglas de validación, trazabilidad de publicación, modificaciones, reprogramaciones, no asignación y protecciones operativas. La publicación es el punto en el que una propuesta pasa a ser una obligación operativa.

### 7. Ejecución, evidencia y validación
La ejecución conserva el ciclo real de una obligación: inicio, conclusión, evidencia, responsable, validación, resultado, cierre, excepciones y continuidad. Las evidencias tienen tipos administrados y pueden requerir vínculo, folio u observación. La validación debe quedar asociada a autoridad, resultado y momento de la decisión.

### 8. Excepciones y continuidad
El sistema contempla no cumplimiento, cancelación justificada, posposición, arrastre y derivación intersemanal. Las causas de arrastre determinan tratamiento, necesidad de acción correctiva, escalamiento y posible impacto económico. Las transiciones deben validarse contra el estado actual y la autoridad del actor.

### 9. Cierre operativo
El cierre semanal valida condiciones previas, pendientes, permisos y bloqueos antes de congelar el período. Después del cierre se restringen modificaciones y cualquier excepción debe seguir una ruta autorizada y auditable.

### 10. Seguridad, roles y autorizaciones
El sistema identifica usuarios, roles, permisos, alcance de rol objetivo y autorizaciones especiales. Las operaciones críticas requieren validación de autoridad y trazabilidad. La resolución de permisos es una regla de negocio separada de la interfaz que inicia la acción.

### 11. Auditoría y trazabilidad
Se registran ejecuciones del sistema, transiciones, errores, autorizaciones y cambios críticos. La auditoría debe permitir correlacionar una operación con usuario, momento, proceso, resultado y error, sin mezclarse con los datos funcionales de la tarea.

### 12. Integraciones
La operación consume información externa de personal y capacidad. La integración debe validar la fuente, período y estructura antes de exponer datos al motor. Las estructuras intermedias utilizadas únicamente para importar, refrescar o adaptar datos no constituyen entidades de negocio.

### 13. Reportes y vistas de usuario
Piso, Subcoordinación, Administración y Dirección consumen vistas diferentes del mismo modelo operativo. Los reportes de tareas del día, pendientes de validación, cierre, asignación y no asignadas son modelos de lectura. No deben originar copias persistentes de información que ya pueda derivarse de los hechos operativos.

## Estados operativos identificados

El ciclo de una obligación utiliza, entre otros, los estados: PROPUESTA, NO_ASIGNADA, EN_REVISION, PUBLICADA, REASIGNADA, EN_PROCESO, CONCLUIDA_PENDIENTE_VALIDACION, VALIDADA_CUMPLIDA, VALIDADA_INCOMPLETA, NO_CUMPLIDA, CANCELADA_JUSTIFICADA, POSPUESTA, ARRASTRADA y CERRADA. La semántica de cada transición deberá formalizarse en las fases de planificación, ejecución, validación, excepciones y cierre.

## Principios que quedan fijados desde Fase 0

- Definición de tarea y obligación activa son conceptos distintos.
- Un cálculo derivable no se persistirá sólo porque hoy exista una estructura que lo materializa.
- Evento, condición y calendario son mecanismos de activación distintos.
- Elegibilidad y balanceo se modelarán como lógica calculable; la asignación efectiva podrá conservarse como hecho.
- Reportes y dashboards se construirán como modelos de lectura.
- Auditoría, permisos, integraciones y respaldo son capacidades transversales, no extensiones implícitas de una entidad operativa.
- La coexistencia y equivalencia legacy/canónica pertenecen a migración y no al modelo funcional permanente.

## Conceptos candidatos identificados

Sin fijar todavía tablas definitivas, el inventario identifica como conceptos con posible identidad o persistencia: definición de tarea, período operativo, persona/colaborador, puesto, turno, disponibilidad/capacidad, usuario, rol, permiso, regla de activación, evento/caso origen, instancia de trabajo, asignación efectiva, plan publicado, ejecución, evidencia, validación, autorización, excepción operativa, cierre de período, medición/KPI y evento de auditoría o corrida. La equivalencia legacy/canónica se considera un concepto temporal de migración.

## Relaciones conceptuales principales

La definición de tarea se vincula con una o más reglas de activación. Una regla, evaluada contra calendario, evento, condición o SLA, puede originar una instancia de trabajo. La instancia se somete a elegibilidad y asignación; la decisión aprobada se publica en un plan y origina una ejecución. La ejecución puede tener múltiples evidencias, validaciones y excepciones. Los planes y ejecuciones pertenecen a un período operativo que termina mediante un cierre. Personas, puestos, turnos y disponibilidad alimentan elegibilidad; usuarios, roles y permisos gobiernan quién puede ejecutar o autorizar cada transición.

## Funciones y transacciones identificadas

**Funciones/reglas calculables:** determinar período activo, calcular atributos de calendario, calcular vencimiento por SLA, determinar personal activo, calcular disponibilidad y capacidad, evaluar elegibilidad, explicar exclusiones, rankear candidatos, calcular capacidad restante, validar permisos, validar transición de estado, calcular agregados y KPI.

**Transacciones/comandos:** generar instancia idempotente, publicar plan, reasignar, iniciar o concluir ejecución, registrar evidencia, validar cumplimiento, autorizar excepción, cancelar, posponer, arrastrar, derivar, cerrar período y registrar auditoría crítica.

**Eventos/handlers:** recepción de eventos externos u operativos que puedan activar obligaciones; actualmente no se identifican jobs horarios ni eventos automáticos de interfaz como requisito funcional permanente.

## Reglas transversales identificadas

La generación debe evitar duplicados; la publicación marca el nacimiento de una obligación; eventos y condiciones no se convierten en recurrencia por conveniencia; la elegibilidad exige compatibilidad y capacidad; la evidencia y validación se exigen según la definición; el postcierre bloquea cambios salvo autorización; las excepciones conservan causa y autoridad; el retiro legacy requiere equivalencia probada y rollback.

## Dependencias diferidas

La Fase 0 no fija todavía tablas SQL, claves definitivas ni contratos de API. La definición exacta se resolverá por dominio en las fases 1 a 19 y se consolidará en la Fase 20.
