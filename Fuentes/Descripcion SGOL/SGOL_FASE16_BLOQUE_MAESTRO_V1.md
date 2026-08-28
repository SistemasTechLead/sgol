# SGOL — Fase 16 · Reportes, dashboards y UX por roles

## Propósito del dominio

Este dominio define la capa de lectura y experiencia de usuario del SGOL. Su responsabilidad es presentar información operativa, ejecutiva y de control derivada de los dominios transaccionales, aplicando el alcance efectivo del usuario y exponiendo únicamente las acciones que corresponden a sus permisos.

La capa de UX no es propietaria del estado de negocio. Ninguna pantalla, matriz, reporte o dashboard crea una segunda fuente de verdad.

## Principios obligatorios

- Toda vista deriva de entidades, hechos y políticas gobernadas por los dominios anteriores.
- No se crean tablas persistentes sólo para alimentar una pantalla cuando la información puede obtenerse mediante una consulta, vista o proyección.
- Toda consulta se ejecuta para un `PeriodoOperativo` explícito; nunca se deduce el período vigente buscando el número de semana máximo disponible.
- El alcance visible resulta de identidad, roles, permisos y ámbito organizativo efectivos. Un nombre de puesto no sustituye un rol de seguridad.
- El filtrado de interfaz no constituye autorización. Todo comando debe volver a validar permisos en su dominio propietario.
- Una misma persona puede tener varios roles o alcances; la interfaz se compone por capacidades y no por copias rígidas de una pantalla.
- Los nombres de personas, puestos o sucursales son datos de presentación, no claves de filtrado técnico.
- Los estados visuales se derivan del modelo canónico de F10-F14. No se infiere que una tarea inició por el hecho de estar publicada.
- Todo indicador debe declarar definición, período, universo, filtros, numerador, denominador y fuente.
- Los porcentajes de cumplimiento no se mezclan con avance operativo, validación, cierre ni impacto económico.
- Las vistas históricas conservan el significado del período consultado y no se recalculan con políticas posteriores salvo que la vista se identifique expresamente como reexpresión.
- Los datos sensibles de incentivos, autorizaciones y auditoría se muestran sólo a usuarios con permiso explícito.
- La UX puede ofrecer acciones, pero la lógica de ejecución, evidencia, validación, excepción, cierre e incentivo permanece en F10-F15.

## Mapa de experiencias de usuario

### Inicio operativo

Vista inicial compuesta según las capacidades del usuario.

Debe mostrar como mínimo:
- período operativo seleccionado y estado;
- obligaciones relevantes para el alcance del usuario;
- pendientes críticos;
- acciones disponibles por permiso;
- alertas de datos o procesos que requieren atención;
- fecha/hora de actualización del modelo de lectura cuando exista procesamiento asíncrono.

No debe mostrar accesos a funciones que el usuario no puede consultar o ejecutar.

### Bandeja personal de trabajo

Vista de obligaciones asignadas al usuario para una fecha o período.

Debe poder mostrar:
- tarea y contexto;
- fecha/hora objetivo;
- prioridad o criticidad;
- estado derivado `PENDIENTE_INICIO`, `EN_PROCESO`, `CONCLUIDA_PENDIENTE_VALIDACION` o resultado final aplicable;
- requisito de evidencia;
- vencimiento/SLA cuando corresponda;
- excepción activa;
- acciones disponibles en ese momento.

La vista no persiste una copia de la obligación.

### Matriz operativa de área

Proyección semanal que agrupa obligaciones por definición de tarea, día y responsable dentro de un alcance autorizado.

La matriz puede presentar:
- filas por tarea o actividad;
- columnas por día;
- uno o varios responsables por celda;
- duración/horario;
- estado visual por obligación;
- resumen de carga y avance del equipo.

Cuando una celda represente varias obligaciones, cada una debe conservar una referencia inequívoca para abrir su detalle. Una abreviatura visual nunca será el identificador de negocio.

### Bandeja de validación

Vista derivada de obligaciones que cumplen las condiciones F12 para requerir una decisión de validación y que están dentro del alcance del validador efectivo.

Debe distinguir:
- pendientes por validar;
- bloqueos por evidencia;
- decisiones previas;
- excepciones activas;
- autoridad requerida.

La acción de validar invoca F12; la vista no escribe el resultado directamente.

### Control semanal

Vista de lectura para seguimiento del `PeriodoOperativo`.

Debe exponer:
- total de obligaciones;
- pendientes de inicio;
- en proceso;
- concluidas pendientes de validación;
- decisiones finales F12;
- excepciones F13;
- obligaciones sin asignación;
- estado de gate de cierre F14;
- causas que impiden cerrar.

El estado `CIERRE_DISPONIBLE` se deriva exclusivamente del gate F14.

### Control intersemanal de excepciones

Vista de continuaciones, arrastres, posposiciones y demás hechos F13 que requieren seguimiento entre períodos.

Debe enlazar siempre la misma identidad de obligación cuando F13 determine que no existe una obligación nueva.

### Dirección

Vista ejecutiva de alcance autorizado que combina indicadores, alertas, autorizaciones, auditoría ejecutiva y accesos a decisiones críticas.

Debe separar claramente:
- indicadores operativos;
- alertas y bloqueos;
- autorizaciones;
- auditoría ejecutiva;
- KPI e incentivos cuando el permiso lo permita.

La ausencia de una alerta no equivale a autorización para ejecutar una acción.

## Modelos de lectura / vistas objetivo

### `ViewInicioUsuario`

Combina período, permisos efectivos, alertas relevantes y accesos disponibles para el usuario.

### `ViewTareasUsuario`

Obligaciones dentro del alcance del usuario con estado operativo, vencimiento, evidencia requerida y acciones permitidas.

Parámetros mínimos:
- `id_usuario`;
- `id_periodo_operativo`;
- fecha o ventana opcional.

### `ViewMatrizArea`

Proyección de obligaciones por área/rol/alcance y período. La agrupación por tarea y día es exclusivamente de presentación.

Parámetros mínimos:
- `id_usuario`;
- `id_periodo_operativo`;
- alcance organizativo;
- rol/capacidad seleccionada cuando exista más de una.

### `ViewResumenCargaColaborador`

Agrega minutos/obligaciones asignadas, iniciadas, concluidas y con decisión final. Los porcentajes se calculan a partir de definiciones explícitas.

### `ViewPendientesValidacion`

Devuelve únicamente obligaciones F12 pendientes dentro del alcance efectivo del validador.

### `ViewPlanPeriodo`

Presenta el `PlanOperativo` y sus partidas F9 sin duplicar los datos de obligación.

### `ViewAsignacionesPeriodo`

Expone asignaciones confirmadas y, cuando esté autorizado, diagnósticos de no asignación. Las propuestas temporales de F8 no se convierten en hechos persistentes por existir en una pantalla.

### `ViewEstadoCierrePeriodo`

Proyección F14 con conteos, pendientes y diagnóstico de cierre.

### `ViewExcepcionesIntersemanales`

Presenta hechos F13 y continuidad entre períodos.

### `ViewDireccionEjecutiva`

Agrega indicadores operativos, alertas, autorizaciones y referencias de auditoría según permisos efectivos.

### `ViewKPIIncentivos`

Presenta `MedicionKPI` y `ResultadoIncentivo` F15 según nivel de sensibilidad y autoridad.

## Indicadores mínimos y semántica

### Total de obligaciones

Número de `InstanciaTrabajo` incluidas en el período y alcance consultados.

### Sin asignación

Obligaciones que requieren responsable y no poseen una asignación efectiva vigente.

### Pendientes de inicio

Obligaciones publicadas y vigentes sin ejecución activa ni tratamiento terminal F12/F13.

### En proceso

Obligaciones con una ejecución F10 activa.

### Concluidas pendientes de validación

Ejecuciones concluidas que requieren decisión F12 y todavía no tienen una decisión final vigente.

### Resultado de validación

Conteos separados por `CUMPLIDA`, `INCOMPLETA` y `NO_CUMPLIDA`. No se combinan en una sola categoría de “cerradas”.

### Excepciones

Conteos separados por tipo F13 y estado de tratamiento.

### Estado de cierre

Resultado del gate F14 para el período consultado, acompañado de causas de bloqueo.

### Avance y cumplimiento

`avance_operativo` y `cumplimiento_validado` son indicadores distintos.

- El avance operativo mide progreso del ciclo de vida de obligaciones.
- El cumplimiento validado mide decisiones F12 sobre el universo definido para validación.

Cada implementación debe publicar la fórmula exacta y no reutilizar un porcentaje con ambos significados.

## Reglas de visibilidad por capacidad

### Personal operativo / Piso

- acceso a sus obligaciones y acciones propias autorizadas;
- vista de equipo sólo cuando exista permiso de consulta para ese alcance;
- sin autorización implícita para validar a otros usuarios.

### Subcoordinación

- obligaciones propias;
- seguimiento del alcance autorizado;
- bandeja de validación únicamente cuando F2/F12 otorguen autoridad efectiva sobre el rol objetivo;
- alertas y excepciones operativas de su ámbito.

### Administración

- obligaciones propias;
- control semanal y reportes dentro de su alcance cuando el permiso lo permita;
- ausencia de autoridad de validación salvo permiso explícito.

### Dirección

- vista ejecutiva de los alcances autorizados;
- autorizaciones y decisiones críticas según F2/F12-F14;
- auditoría ejecutiva F17;
- KPI/incentivos F15 según sensibilidad autorizada.

Estas categorías son experiencias por capacidad, no cuentas, puestos ni claves de seguridad.

## Acciones expuestas por la UX

La interfaz puede invocar, cuando corresponda:
- iniciar ejecución F10;
- concluir ejecución F10;
- registrar evidencia F11;
- validar F12;
- registrar/cambiar tratamiento de excepción F13;
- autorizar una excepción cuando exista política aplicable;
- ejecutar cierre F14;
- aprobar resultados económicos F15;
- abrir detalle, historial o auditoría.

Antes de mostrar una acción, la UX puede consultar disponibilidad. Al ejecutarla, el dominio propietario debe volver a validar permisos, estado y precondiciones.

## Presentación de estados

Los colores, iconos y badges son ayudas visuales y nunca sustituyen el estado textual/canónico.

La presentación debe distinguir al menos:
- futura/no disponible todavía;
- pendiente de inicio;
- vencida o fuera de ventana;
- en proceso;
- concluida pendiente de validación;
- validada con resultado;
- excepción activa;
- bloqueada por regla de negocio.

## Reportes y exportaciones

Un reporte es una representación de un modelo de lectura y puede tener filtros, agrupaciones y columnas específicas para impresión/exportación.

La exportación debe conservar:
- período y filtros utilizados;
- fecha de generación;
- alcance del usuario;
- definición/versiones relevantes del indicador cuando aplique.

No se guarda una tabla duplicada sólo para que exista el archivo exportado.

## Rendimiento y materialización

Las vistas pueden materializarse técnicamente si volumen o latencia lo exigen, pero la materialización:
- no crea una nueva autoridad de negocio;
- debe poder reconstruirse desde fuentes canónicas;
- debe tener versión/fecha de actualización;
- debe invalidarse o actualizarse después de los eventos relevantes;
- no puede utilizarse para decidir una transacción si está fuera de la tolerancia de frescura definida.

## Funciones de lectura

- `obtener_inicio_usuario(id_usuario, id_periodo)`.
- `obtener_tareas_usuario(id_usuario, id_periodo, ventana)`.
- `obtener_matriz_area(id_usuario, id_periodo, alcance)`.
- `obtener_resumen_carga_colaborador(id_empleado, id_periodo)`.
- `obtener_pendientes_validacion(id_usuario, id_periodo)`.
- `obtener_estado_cierre(id_usuario, id_periodo)`.
- `obtener_excepciones_intersemanales(id_usuario, id_periodo)`.
- `obtener_dashboard_direccion(id_usuario, id_periodo, alcance)`.
- `obtener_kpi_incentivos(id_usuario, id_periodo, alcance)`.
- `resolver_acciones_disponibles(id_usuario, objeto, contexto)`.
- `explicar_indicador(codigo_indicador, id_periodo, filtros)`.

Estas funciones no modifican estado.

## Actualización de modelos de lectura

Los modelos de lectura deben reflejar cambios originados, entre otros, por:
- publicación/asignación;
- inicio o conclusión de ejecución;
- registro de evidencia;
- validación;
- excepción;
- cierre;
- cálculo/aprobación de incentivos;
- cambios de permisos o alcance.

La actualización puede ser inmediata o asíncrona según la arquitectura, pero la UX debe conocer la frescura del dato cuando ésta sea relevante.

## Permisos

Permisos de lectura recomendados se definen por recurso y alcance, por ejemplo:
- consultar obligaciones propias;
- consultar obligaciones de equipo;
- consultar control semanal;
- consultar alertas ejecutivas;
- consultar auditoría ejecutiva;
- consultar KPI/incentivos;
- consultar información sensible económica.

Los permisos de ejecutar comandos permanecen separados de los permisos de lectura.

## Errores de dominio / lectura mínimos

- `PERIODO_NO_ESPECIFICADO`.
- `PERIODO_NO_ACCESIBLE`.
- `VISTA_FUERA_DE_ALCANCE`.
- `RECURSO_NO_VISIBLE_POR_PERMISO`.
- `INDICADOR_SIN_DEFINICION`.
- `MODELO_LECTURA_DESACTUALIZADO`.
- `ACCION_NO_DISPONIBLE_EN_ESTADO_ACTUAL`.
- `DATO_SENSIBLE_NO_AUTORIZADO`.
- `FILTRO_DE_ALCANCE_AMBIGUO`.

## Dependencias con otros dominios

- **F1**: empleado, puesto, turno y capacidad para presentación contextual.
- **F2**: identidad, roles, permisos y alcance.
- **F3**: `PeriodoOperativo`, calendario y selección temporal.
- **F4**: nombres y jerarquías de tareas/procesos.
- **F6**: `InstanciaTrabajo` como obligación base.
- **F8**: asignación confirmada y diagnósticos de no asignación.
- **F9**: plan y partidas publicadas.
- **F10**: ejecución y estados operativos.
- **F11**: requisitos/estado de evidencia.
- **F12**: decisiones y pendientes de validación.
- **F13**: excepciones y continuidad.
- **F14**: gate y estado de cierre.
- **F15**: KPI e incentivos.
- **F17**: auditoría ejecutiva y técnica.
- **F19**: estado de integraciones cuando deba exponerse al usuario.

## Pendientes reservados para fases posteriores

- F17 definirá el contrato de auditoría que alimenta las vistas ejecutivas y técnicas.
- F18 reconciliará qué reportes históricos pueden reconstruirse con semántica canónica y cuáles deben conservarse sólo como evidencia legacy.
- F19 definirá indicadores de salud, sincronización y errores de integraciones externas que deban mostrarse en la UX.
