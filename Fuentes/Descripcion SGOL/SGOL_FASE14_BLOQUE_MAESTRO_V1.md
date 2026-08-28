# SGOL — Fase 14 · Cierre semanal e intersemanal

## Propósito del dominio

Este dominio gobierna el cierre definitivo de un `PeriodoOperativo`, la validación de que no quedan obligaciones sin tratamiento, el congelamiento lógico posterior al cierre y la preparación controlada del período siguiente. Su responsabilidad es convertir un período operativo concluido en un hecho inmutable y auditable sin confundir el cierre del período con el cierre técnico de una ejecución, una excepción o una partida individual.

## Principios obligatorios

- El cierre se aplica al `PeriodoOperativo`, no a filas individuales de ejecución o plan.
- `CERRADA` es un estado terminal del mismo período. La semana siguiente es otro `PeriodoOperativo`; no es una reapertura del período cerrado.
- La preparación del cierre se evalúa sobre todas las `InstanciaTrabajo` pertenecientes al período, aunque alguna no tenga ejecución iniciada.
- Toda obligación del período debe tener un tratamiento terminal válido antes del cierre: resolución de cumplimiento F12 o excepción terminal/continuación controlada F13.
- Una ejecución activa, una validación requerida pendiente, una excepción no resuelta o una obligación sin tratamiento bloquean el cierre.
- El cierre no calcula bonos, KPI ni resultados económicos. F15 consume los hechos ya resueltos.
- El cierre no crea arrastres, posposiciones, cancelaciones ni reasignaciones; esas decisiones pertenecen a F13.
- El bloqueo postcierre se deriva del estado del período. No se duplican banderas de bloqueo por cada partida o ejecución.
- La mutabilidad debe comprobarse en el servidor antes de todo comando que afecte un período cerrado.
- El cierre definitivo es transaccional e idempotente: un período no puede cerrarse dos veces ni quedar parcialmente cerrado.
- Un rollback técnico sólo revierte una transacción de cierre que no llegó a commit. Una vez confirmado el cierre, una corrección posterior no reabre silenciosamente el período.
- Reportes, dashboards y notificaciones son efectos posteriores al commit; un fallo de presentación no invalida un cierre de negocio ya confirmado.

## Mapa del dominio

1. **Período operativo**: entidad F3 cuya vigencia y estado gobiernan la mutabilidad.
2. **Evaluación de cierre**: resultado derivado que explica si el período puede cerrarse y qué lo bloquea.
3. **Cierre de período**: hecho persistente que certifica el cierre definitivo.
4. **Historial de transición del período**: trazabilidad append-only de cambios de estado con valor de auditoría.
5. **Política de cierre**: reglas versionadas que definen gates y autoridad.
6. **Protección postcierre**: regla transversal que impide mutaciones ordinarias sobre objetos del período cerrado.
7. **Preparación intersemanal**: comando que habilita la creación/preparación del siguiente período sin modificar el período cerrado.

## Entidades persistentes

### CierrePeriodo

Representa el hecho de que un período cumplió sus gates y quedó cerrado definitivamente.

**Atributos relevantes**
- `id_cierre_periodo`;
- `id_periodo_operativo` único;
- `cerrado_en`;
- `cerrado_por`;
- `id_politica_cierre` / versión aplicada;
- conteos de control o huella de evaluación cuando se requiera auditoría;
- correlación de transacción/auditoría;
- observación o motivo autorizado nullable.

Debe existir como máximo un cierre definitivo vigente por período.

### PoliticaCierrePeriodo

Configuración versionada de los gates de cierre.

**Atributos relevantes**
- `id_politica_cierre`;
- vigencia;
- estados de período desde los que se permite cerrar;
- tratamientos terminales aceptados;
- regla de validaciones pendientes;
- regla de excepciones pendientes;
- regla de autoridad/autorización;
- controles adicionales configurables.

### TransicionPeriodo

Historial append-only de transiciones de `PeriodoOperativo` cuando la trazabilidad tenga valor de negocio o auditoría.

**Atributos relevantes**
- `id_transicion_periodo`;
- `id_periodo_operativo`;
- estado anterior;
- estado nuevo;
- fecha/hora;
- actor;
- motivo;
- correlación.

El log técnico F17 puede registrar información adicional, pero no sustituye este historial cuando la transición tiene significado de negocio.

## Datos derivados y vistas

### EvaluacionCierrePeriodo

No requiere tabla persistente por defecto. Se calcula a partir del período y sus obligaciones.

Debe devolver al menos:
- total de obligaciones del período;
- obligaciones con ejecución activa;
- obligaciones pendientes de inicio sin tratamiento terminal;
- validaciones requeridas pendientes;
- excepciones pendientes;
- obligaciones con tratamiento terminal válido;
- bloqueos de integridad;
- resultado `LISTO_PARA_CIERRE` / `BLOQUEADO`;
- lista explicable de motivos de bloqueo.

### EstadoPostCierre

Se deriva de `PeriodoOperativo.estado = CERRADA`. No necesita una bandera repetida por partida o ejecución.

## Gates de cierre

Un período sólo puede cerrarse cuando se cumplen simultáneamente:

1. El período existe y está en `EN_EJECUCION` o estado equivalente autorizado por la política.
2. No existe ninguna `InstanciaTrabajo` del período sin tratamiento terminal.
3. No existen ejecuciones activas.
4. No existen validaciones F12 obligatorias pendientes.
5. No existen excepciones F13 pendientes de decisión o aplicación.
6. Toda continuidad a otro período ya está formalizada por F13 sin duplicar la obligación.
7. La integridad entre obligación, plan, asignación, ejecución, validación y excepción no presenta bloqueos críticos.
8. El actor tiene permiso y autoridad para cerrar el período.
9. La solicitud de cierre es idempotente y el período no tiene ya un `CierrePeriodo` confirmado.

Una política puede añadir gates adicionales, pero no puede omitir los controles de integridad e idempotencia.

## Máquina de estado del período

La máquina de estado detallada se define en F3. Para F14 son obligatorias estas reglas:

- `EN_EJECUCION -> CERRADA` sólo mediante `cerrar_periodo` y con todos los gates aprobados.
- `CERRADA` no vuelve a `EN_EJECUCION` ni a un estado de simulación por una transición ordinaria.
- La preparación de la semana siguiente crea o habilita otro `PeriodoOperativo`.
- Un fallo antes del commit no constituye una transición de negocio; la transacción se revierte.
- Una corrección posterior al cierre se registra como operación extraordinaria y auditable, sin borrar el cierre original.

## Funciones y reglas

- `evaluar_cierre_periodo(id_periodo)`.
- `listar_bloqueos_cierre(id_periodo)`.
- `contar_obligaciones_sin_tratamiento(id_periodo)`.
- `contar_ejecuciones_activas(id_periodo)`.
- `contar_validaciones_pendientes(id_periodo)`.
- `contar_excepciones_pendientes(id_periodo)`.
- `validar_integridad_periodo(id_periodo)`.
- `validar_autoridad_cierre(id_periodo, actor)`.
- `es_periodo_mutable(id_periodo)`.
- `obtener_periodo_siguiente(id_periodo)`.

## Comandos transaccionales

### `cerrar_periodo`

1. Bloquea lógicamente el período para evitar carreras concurrentes.
2. Reevalúa todos los gates dentro de la transacción.
3. Verifica idempotencia y ausencia de un cierre previo.
4. Crea `CierrePeriodo`.
5. Transiciona `PeriodoOperativo` a `CERRADA`.
6. Registra `TransicionPeriodo` y auditoría requerida.
7. Confirma todo en un único commit.
8. Emite `PeriodoCerrado` después del commit.

No modifica cada partida o ejecución para marcarla cerrada.

### `preparar_periodo_siguiente`

Sólo puede operar cuando el período origen está cerrado o cuando la política de planificación permita preparación anticipada. Resuelve el siguiente período como una entidad distinta y no altera el estado del período origen.

### `corregir_postcierre`

Ruta extraordinaria, no equivalente a reapertura. Requiere autoridad elevada, motivo, trazabilidad append-only y comandos de corrección específicos del dominio afectado. Nunca elimina ni sobrescribe el hecho `CierrePeriodo`.

## Rollback y manejo de errores

- Los cambios de cierre se ejecutan en una transacción de base de datos.
- Cualquier error previo al commit revierte `CierrePeriodo`, `TransicionPeriodo` y el cambio de estado de forma atómica.
- No se utiliza una transición de negocio `CERRADA -> EN_EJECUCION` para simular rollback técnico.
- Si falla una actualización de reporte, cache, notificación o dashboard después del commit, se reintenta ese efecto sin reabrir el período.
- Los fallos y reintentos técnicos pertenecen a F17.

## Protección postcierre

Todo comando de F6 a F13 que pretenda modificar una obligación, asignación, ejecución, evidencia, validación o excepción debe consultar la mutabilidad del período antes de escribir.

Para un período `CERRADA`:
- se rechazan operaciones ordinarias;
- se permiten lecturas y reportes;
- las correcciones requieren la ruta extraordinaria correspondiente;
- el histórico permanece append-only.

## Eventos

- `PeriodoListoParaCierre` opcional si tiene consumidor real;
- `PeriodoCerrado`;
- `CorreccionPostCierreRegistrada` cuando aplique.

Los eventos se publican después del commit y deben ser idempotentes para sus consumidores.

## Permisos

- Consultar evaluación de cierre puede habilitarse a roles operativos autorizados.
- Ejecutar `cerrar_periodo` requiere permiso explícito de cierre y alcance sobre el período/sucursal correspondiente.
- `corregir_postcierre` requiere una autoridad distinta y superior a la operación ordinaria.
- La identidad del actor y la autorización concreta deben conservarse en auditoría.

## Relaciones principales

- `PeriodoOperativo 1:0..1 CierrePeriodo`.
- `PeriodoOperativo 1:N TransicionPeriodo`.
- `PoliticaCierrePeriodo 1:N CierrePeriodo`.
- `PeriodoOperativo 1:N InstanciaTrabajo`; esta relación es la base de la evaluación del cierre.
- F12 aporta resultados de validación terminales.
- F13 aporta excepciones y continuidades resueltas.
- F15 consume el período cerrado para consolidación económica/KPI.
- F16 consume el estado y las evaluaciones para UX/reportes.
- F17 registra ejecución técnica, errores, correlación y reintentos.

## Errores de dominio mínimos

- `PERIODO_NO_EN_EJECUCION`.
- `PERIODO_YA_CERRADO`.
- `OBLIGACIONES_SIN_TRATAMIENTO`.
- `EJECUCIONES_ACTIVAS`.
- `VALIDACIONES_PENDIENTES`.
- `EXCEPCIONES_PENDIENTES`.
- `INTEGRIDAD_PERIODO_INVALIDA`.
- `SIN_AUTORIDAD_CIERRE`.
- `CONFLICTO_CONCURRENCIA_CIERRE`.
- `CORRECCION_POSTCIERRE_NO_AUTORIZADA`.

## Dependencias con otros dominios

- **F2**: permisos y autoridad.
- **F3**: identidad y estado de `PeriodoOperativo`.
- **F6**: universo de obligaciones a cerrar.
- **F9**: plan y partidas publicadas.
- **F10**: ejecuciones activas/concluidas.
- **F11**: integridad de evidencia exigida por F12.
- **F12**: resolución de cumplimiento.
- **F13**: excepciones, continuidad y tratamiento de obligaciones no concluidas.
- **F15**: consolidación económica posterior al cierre.
- **F16**: vistas de cierre.
- **F17**: transacción técnica, logs, errores y reintentos.
- **F18**: reconciliación histórica de cierres y bloqueos existentes.

## Pendientes reservados para fases posteriores

- F15 definirá qué resultados del período cerrado se consolidan para bonos, KPI y nómina.
- F16 definirá las vistas por rol y los indicadores de preparación/cierre.
- F17 definirá el contrato técnico de transacciones, auditoría, retries y recuperación.
- F18 reconciliará los cierres históricos por registro con el cierre de período y determinará qué evidencias legacy se conservan como referencias.
