---
categoria: datos
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: inspeccion-estatica
---

# Modelo operativo e historial

## Entidades y capturas

El catálogo describe tareas. Los planes organizan compromisos; las asignaciones capturan responsable, requisito, carga y agenda. Los procesos separan definición/versiones, planes preparados, ejecuciones, etapas, intentos y eventos. Las reservas representan demanda futura sin fingir asignación ejecutable.

Cambiar catálogo o tarifas no reescribe todas esas capturas. [Actualizar pendientes](actualizacion-asignaciones.md) adopta cambios explícitos; versiones publicadas y entregas conservan significado.

## Entrega y revisión

Son hechos separados con fecha, autor y comentarios propios. CompletionKind distingue entrega, conclusión por supervisión e históricos. La corrección crea una cadena mediante su origen; la transferencia conserva identidad de asignación y registra responsable anterior y nuevo.

Un autor histórico desconocido permanece desconocido: el responsable actual no demuestra quién entregó. No se sustituye evidencia antigua por contenido actual de catálogo ni se reabre implícitamente un intento cerrado.

## Retiro y conservación

El retiro lógico aparta pendientes del trabajo activo preservando evidencia y actividad. El borrado de planes/procesos tiene protecciones propias; no equivale a eliminar cualquier fila relacionada. Las migraciones conservan restricciones, snapshots y trazabilidad, incluso cuando el modelo de código no representa todos los controles SQL.

Las evidencias operativas tienen almacenamiento especializado en PostgreSQL: adjuntos, fotografías, sesiones de captura y material de referencia están separados de la asignación. Los listados no necesitan cargar todos sus bytes; la descarga requiere autorización. Las FK y restricciones relacionan fotografía/sesión/asignación y protegen fechas, cantidades y estados.

Los ajustes de capacidad de un plan conservan intervalos previos y revisión de origen para su restauración; una referencia histórica a un segmento mutable no implica que ese segmento siga vigente. Se conservan instantáneas deliberadas, no una promesa de normalización absoluta de toda la base.

Existe además infraestructura del circuito anterior de evidencia S3. LegacyWorkflow está deshabilitado por defecto; su presencia no demuestra que todo adjunto operativo pase por él. Reactivarlo no incorpora automáticamente obligaciones anteriores a las pantallas operativas. El iniciador local arranca Web y, en red, proxy; un Worker permanente requiere la opción correspondiente. Los comandos de mantenimiento no son políticas automáticas de producción.

## Consultas

Mi trabajo, Supervisión, tableros y Reportes consumen hechos con alcances distintos. Avance diario, carga actual, puntualidad original y calificación del bono no son intercambiables. Los datos particulares de la simulación no forman parte de esta referencia.

Relacionados: [Procesos](procesos.md), [Supervisión](supervision.md), [Bono](bono-tareas.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Revisión estática; no constituye una nueva prueba funcional, visual o de datos del host.

- [OperationalTasks.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTasks.cs)
- [OperationalAttachment.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalAttachment.cs)
- [OperationalSequences.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalSequences.cs)
- [SgolDbContext.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/SgolDbContext.cs)

[Mapa de datos](../../mapas/datos.md) · [Inicio](../../Inicio.md)
