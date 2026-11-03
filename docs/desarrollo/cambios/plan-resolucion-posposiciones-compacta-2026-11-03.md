# Plan de resolución compacta de peticiones de posposición

Fecha: 03/11/2026, America/Mexico_City. Cambio mixto: interfaz y reglas de resolución. Implementación y recarga compartida autorizadas posteriormente por el usuario; evidencia de ejecución al final. Las secciones de investigación inicial y matriz propuesta conservan su contexto histórico.

## Objetivo y decisiones acordadas

Revisar cada tarea sin abrir varios desplegables: identificar tarea, explicación, restricciones y efecto de decidir; aprobar la propuesta, cambiar responsable, cambiar día o rechazar desde la misma bandeja.

El usuario confirmó resolver **por tarea** y exigir **capacidad y horario disponibles**, sin excepción de sobrecarga. La petición conserva causa, explicación y fotografía originales como contexto común. Reasignar y reprogramar son alternativas a aprobar la propuesta original, no operaciones adicionales ejecutadas después de aprobarla.

La falta de capacitación declarada no demuestra una inhabilitación técnica registrada. Mostrar «Reporta: aún no se me capacita», sin inferir competencias del texto ni modificar automáticamente el perfil de la persona. Rol/especialidad elegibles tampoco prueban por sí solos formación práctica. La persona revisora elige al destinatario entre los elegibles y documenta el motivo.

## Investigación realizada y límites

Se inspeccionaron las dos capturas del usuario, AGENTS.md, el protocolo de implementación, ESTILO_VISUAL_V2 y código actual en master. Existen numerosos cambios staged y unstaged, incluidos `_Carryovers.cshtml`, `_CarryoverHistory.cshtml` y estilos compartidos; deben conservarse e integrarse. Solo se añade este plan.

La captura muestra TAR-0379, «Capturar pedidos», explicación «Aún no se me capacita», cierre al enviar 03/11/2026 10:30 y propuesta 10/11/2026 09:30–11:00. Es evidencia visual de un registro existente; no se verificó su estado vigente en base de datos. Las cifras históricas de carga no deben presentarse como disponibilidad actual.

No se consultó ni modificó la base, no se verificó el binario activo y no se ejecutaron pruebas HTTP, navegador ni compilación. Antes de implementar/probar, identificar aplicación, proceso, compilación, base efectiva y zona operativa; local y LAN pueden compartir destino. Usar copia aislada para reproducir registros existentes y bases `sgol_task_checks_*` para regresiones. Los resultados históricos de otras tareas no validan este cambio.

## Comportamiento actual y trazabilidad

Todas las rutas de archivo siguientes son relativas al repositorio.

| Recorrido | Fuente inspeccionada | Comportamiento observado / impacto |
|---|---|---|
| Solicitar | `src/Sgol.Web/Pages/MyWork/Carryover.cshtml.cs`: `Postpone`; `Infrastructure/Persistence/Planning/OperationalCarryover.Postponement.cs`: `OverdueAsync`, `PreviewPostponementAsync`, `SubmitPostponementAsync` | Selecciona vencidas elegibles, calcula propuesta, guarda solicitud con snapshot. Excluye procesos, PROGRESO y casos de caja específicos. No ampliar esa elegibilidad en este trabajo. |
| Persistir | `src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCarryover.cs`: `OperationalCarryoverRequest`, `CarryoverItem`, `CarryoverResult` | Estado, revisión y comentario globales; snapshot y resultados JSON. No existe aún resolución independiente por ítem. |
| Revisar | `src/Sgol.Web/Pages/Validations/_Carryovers.cshtml`, `Index.Carryover.cs`: `LoadCarryoversAsync`, `OnPostCarryoverAsync` | Usa el parcial histórico para pendientes, oculta tarea/explicación y efecto en dos desplegables, deja textarea abierto. POST `/validaciones?handler=Carryover` recibe aprobación booleana de toda la petición. |
| Aprobar | `OperationalCarryover.cs`: `DecideAsync`; `OperationalCarryover.Postponement.cs`: `ApplyPostponement` | Autoriza alcance/supervisor inmediato; valida revisión, estructura, agenda, carga y fechas antes de modificar. Entregadas conservan fechas. Conflictos se agrupan en `ARRASTRE_CONFLICTO`. |
| Transferir | `src/Sgol.Web/Pages/Validations/Workload.cshtml.cs`; `OperationalSupervisionService.Transfer.cs`: `TransferRecipientsAsync`, `BuildTransferAsync`, `TransferAsync` | `/supervision/carga` tiene vista previa y conservación de avance/autoría/evidencia. La transferencia parte de ahora y exige presencia al recibir; no equivale a reasignación futura. |
| Correcciones | `src/Sgol.Web/Pages/Validations/_ReassignmentSchedule.cshtml` | Ofrece cuanto antes, espacio libre y personalizada, pero permite superar carga diaria. Reutilizar solo patrones visuales y lógica compatible; no heredar esa excepción. |
| Consultar e historial | `OperationalCarryover.Paging.cs`, `OperationalCarryoverReader.cs`, `OperationalCarryover.Restoration.cs`, `Pages/Shared/_CarryoverHistory.cshtml`, `_CarryoverTiming.cshtml` | Consultas y lector dependen del estado global; restauración exige solicitud aprobada. Deben consumir resultados por tarea sin otorgar reconocimiento de arrastre a rechazos o reasignaciones. |
| Presentación equivalente | `src/Sgol.Web/Pages/Tasks/Refresh.cshtml`, `wwwroot/css/assignment-refresh.css`, `interactive-controls.css`, `components.css` | Patrón existente de tarea → motivo → efecto → detalles. Actualmente textarea de arrastre tiene mínimo de 88px. Reutilizar tokens y controles distinguibles, con estilos acotados a la bandeja. |

Antes de editar consumidores, completar búsqueda de usos del estado global, `ForAsync`, `PendingPostponement`, `Results()` y restauración en Mi trabajo, tablero, planificación, reportes y herramientas de mantenimiento. Verificar productores y consumidores de cargas, evidencias, contribuciones y reservas, no solo etiquetas de pantalla.

## Interfaz propuesta

Una lista compacta, con encabezado común discreto por petición y un renglón/bloque por tarea. Mantener contexto compartido visible sin repetir íntegra la explicación por cada tarea. No usar el parcial histórico como formulario de pendientes.

Orden de lectura:

1. **Nombre de tarea**, código secundario, responsable y estado. Una restricción que impida actuar aparece inmediatamente junto a esa identidad, nunca escondida.
2. **Comentario de quien solicita**, visible; causa del catálogo como apoyo. Texto breve completo; texto largo con extracto y «Leer completo», accesible antes de decidir. Los bloqueos calculados no se truncan.
3. **Situación → efecto** en una línea o dos: cierre vigente y responsable → responsable/horario resultantes; duración de trabajo etiquetada como tal. No confundir minutos de carga con duración del intervalo que incluye margen.
4. **Aprobar propuesta · Reasignar · Cambiar día · Rechazar**, con «Ver detalles» secundario. El alcance por tarea queda explícito.

Ejemplo de contenido basado en la captura, no verificado como estado vigente:

> **Capturar pedidos** · TAR-0379 · Viri  
> Motivo declarado: «Aún no se me capacita».  
> Cierre al enviar: 03 nov, 10:30 → Propuesta: 10 nov, 09:30–11:00.  
> Aprobar propuesta · Reasignar · Cambiar día · Rechazar · Ver detalles

En ejecución, mostrar el cierre **vigente**, no sustituirlo silenciosamente por la fotografía al enviar. Si difieren, explicarlo en el resumen. Una tarea entregada muestra «Ya entregada; conserva fechas y decisión» y no ofrece operaciones de agenda.

«Ver detalles» contiene UUID, fecha de solicitud, fotografía original, ventana autorizable, desglose de carga, cálculo y auditoría. Las restricciones de fecha relevantes también se muestran junto al selector al cambiar día. El detalle no oculta bloqueos ni el resultado de confirmar.

Al elegir acción se abre un editor local a esa tarea. El comentario de decisión sigue obligatorio y conserva el límite actual; se muestra solo al decidir, con altura inicial compacta y crecimiento. Abrir una acción no guarda. Confirmación explícita y botón con resultado concreto: «Confirmar reasignación», por ejemplo. Conservar borrador, filtros, página y posición ante errores; devolver foco a la tarea siguiente o al resumen de resultado tras éxito. No descartar borradores al cambiar de tarea.

Separación objetivo: tokens de 8–12px entre elementos relacionados y 12–16px entre tareas, sin paneles decorativos anidados ni alturas fijas. Controles de al menos 44px, texto y foco legibles. En móvil las acciones refluyen. Comparar altura con mismas tareas, texto, ancho y tema; no fijar una cantidad de tarjetas por pantalla que falle con textos largos.

## Acciones y reglas

| Acción | Resultado esperado |
|---|---|
| Aprobar propuesta | Aplica a esa tarea las fechas propuestas si siguen siendo válidas. No recalcula silenciosamente otra fecha. |
| Reasignar | Elegir persona elegible y horario; mostrar carga antes/después de origen y destino. Conservar identidad de trabajo, avance, evidencia y autoría; registrar quién decidió y qué cambió. Admitir programación futura mediante lógica explícita, sin reutilizar la recepción inmediata como si fuera futura. |
| Cambiar día | Mantener responsable y elegir fecha dentro de la ventana autorizable original y límites de la tarea. Calcular tramo completo, mostrar resultado y confirmar. No ampliar plazos implícitamente. |
| Rechazar | Resolver ese ítem sin mover la tarea, borrar avances ni resolver hermanas. Explicar que la obligación permanece pendiente con sus fechas vigentes. |

Reasignación puede incluir cambio de día en la misma operación. Capacidad, horario, ausencias, vigencias, especialidad, dependencias y límites se verifican en servidor para toda la propuesta. Sin hueco: explicar la tarea, persona, fecha y causa; ofrecer revisar otra persona/fecha dentro del alcance. No permitir sobrecarga ni ampliar jornada. No mover tareas vecinas automáticamente para hacer espacio.

No añadir decisiones masivas inicialmente. Mantener la distinción entre POSPONER e INCIDENTE: los incidentes tienen propagación y reconocimiento histórico propios y conservan su decisión integral; la resolución por tarea de este plan corresponde a posposiciones. La misma mejora de lectura puede aplicarse a incidentes sin modificar su semántica.

## Implementación por etapas

1. **Modelo de lectura y diagnóstico.** Crear DTO por tarea con snapshot, estado actual, explicación, efecto y acciones elegibles. Extraer validación compartida de vista previa/aplicación para devolver conflictos estructurados (código, tarea, causa, acción), evitando duplicar reglas en Razor. El chequeo visual actual solo compara algunas fechas y persona: no basta para afirmar «se puede aprobar».
2. **Persistencia por ítem.** Añadir entidad de resolución vinculada a solicitud/asignación, con revisión, acción, motivo, actor/fecha, antes/después, identificador de intención y vínculo a transferencia cuando aplique. Unicidad por ítem y operación idempotente. Estados propuestos: pendiente, propuesta aprobada, reasignada, reprogramada, rechazada y sin cambio por entrega. La petición deriva pendientes/resultados mixtos; no fingir que todas las tareas fueron aprobadas.
3. **Compatibilidad y migración.** Conservar snapshot original inmutable. Crear ítems pendientes para solicitudes abiertas y traducir resultados históricos sin inventar comentarios individuales: marcar decisiones heredadas como globales. Mantener originales aprobadas/rechazadas, restauraciones y entregadas sin cambios. Versionar contratos y adaptar lectores antes de activar escritura nueva; una migración es probable y debe validarse en copia aislada, con conteos y respaldo del destino autorizado antes de aplicarla.
4. **Servicio de resolución.** Vista previa y aplicación de una acción por tarea; transacción única para agenda/responsable, carga, resolución e historial. Extraer funciones compatibles de transferencia/programación, sin encadenar servicios que confirmen transacciones independientes. Bloqueo/revisión de solicitud, ítem, asignación y contexto de capacidad; revalidar en confirmación. Reintentar la misma intención devuelve el mismo resultado; una decisión contradictoria genera conflicto sin efectos parciales.
5. **Consultas y ejecución.** Actualizar bandeja, contadores, bloqueo de nuevas peticiones, historial de solicitante/supervisor, tiempos reconocidos y restauración por ítem. Una reasignación no suma arrastre aprobado ni bonifica retroactivamente. Mi trabajo debe retirar el pendiente al origen y mostrarlo al destino con horario correcto; conservar historial accesible al origen. Verificar tablero, reportes y cargas mediante sus consumidores reales. No cambiar fórmulas de bono o calidad.
6. **Interfaz.** Nuevo parcial de pendientes; adaptar `Index.Carryover.cs` con acciones explícitas y vista previa; mantener antiforgery, autorización y validación servidor. CSS específico para la lista; reutilizar controles y patrón de casos de Actualizar Asignaciones. Separar presentación de historial de edición sin duplicar reglas.
7. **Validación y entrega posterior.** Ejecutar matriz, inspeccionar diff contra cambios preexistentes y actualizar este documento con evidencia real. Publicar o desplegar solo dentro de autorización posterior de implementación; este pedido únicamente crea el plan.

La resolución de una hermana puede alterar la carga prevista para las demás. Recalcular su diagnóstico actual y validar individualmente cada propuesta pendiente, conservando su fotografía original; no mantener una huella global que invalide irreversiblemente a todas después de la primera decisión. Dos tareas distintas también pueden competir por el último hueco, por lo que una revisión por ítem sola no basta.

## Matriz de validación propuesta

Todos los escenarios siguientes tienen estado **propuesto**, no ejecutado. Integración/HTTP usarán base aislada; escenarios históricos usarán copia aislada verificada. Las pruebas de navegador serán sobre Razor autenticado y servicios reales, no maqueta.

| Precondición / acción | Resultado esperado | Prueba y entorno | Estado |
|---|---|---|---|
| Petición nueva de una tarea; aprobar | Cambiar solo fechas previstas; comentario y auditoría; Mi trabajo actualizado | Formulario real + comparación BD, aislado | Propuesto |
| Tres tareas: aprobar, reasignar y rechazar | Tres resultados independientes; contador correcto; petición sin pendientes al terminar | HTTP + BD + historial, aislado | Propuesto |
| Resolver una y dejar hermanas pendientes | Hermanas visibles y resolubles; no repetir ni bloquear la ya resuelta | Servicio + HTTP, aislado | Propuesto |
| Reasignar y cambiar día simultáneamente | Capacidad/horario del nuevo destino válidos; liberar carga origen una vez | Servicio + navegador + BD, aislado | Propuesto |
| Reasignación inmediata/futura con avance y archivos | Conservar autoría, evidencia, progreso e historial; tarea visible a destinatario correcto | Recorrido Mi trabajo → resolución → entrega, aislado | Propuesto |
| Sin capacidad, ausencia, rol/especialidad inválidos o límite exclusivo | Causa concreta; confirmar no escribe; otra opción puede revisarse | Payload manipulado + formulario, aislado | Propuesto |
| Cambio de día dentro/fuera de ventana y propuesta pasada | Aceptar solo tramo vigente permitido, sin desplazar vecinas | Servicio + HTTP, aislado | Propuesto |
| Entrega mientras se revisa; estado final o asignación retirada | No mover ni reasignar entrega; mostrar cambio y conservar calidad/fechas | Dos sesiones + BD, aislado | Propuesto |
| Cambian carga, turno, responsable y plazo tras vista previa | Invalidar confirmación y diagnosticar; conservar comentario | Dos sesiones, aislado | Propuesto |
| Dos revisores sobre misma tarea; dos tareas por último hueco | Una resolución compatible; ninguna sobreasignación | Integración concurrente, aislado | Propuesto |
| Doble clic, reenvío tras timeout y fallo intermedio | Una sola operación o rollback completo; reintento seguro | HTTP + fallo controlado, aislado | Propuesto |
| Supervisor ajeno, autoaprobación, pérdida de permiso y CSRF | Rechazo servidor sin modificaciones | Formularios y HTTP, aislado | Propuesto |
| Peticiones antiguas pendientes/aprobadas/rechazadas/restauradas | Migración fiel; mismas fechas reconocidas y auditoría; sin resultados inventados | Migración + comparación antes/después, copia aislada | Propuesto |
| Reasignada/rechazada junto a aprobada en historial | Solo aprobación/reprogramación pertinente computa; pendiente individual correcto | Lector, reportes, tablero, restauración, aislado | Propuesto |
| Incidente, proceso, PROGRESO y caja excluida | Conservar reglas y exclusiones previas, sin nuevas rutas indirectas | Regresión servicios/formularios, aislado | Propuesto |
| Varias peticiones, comentarios largos, vacío/error/cargando/éxito | Identidad, motivo y bloqueo visibles; sin salto ni pérdida de borrador | Edge/WebKit, Razor aislado | Propuesto |
| 320/768/1440px; claro/oscuro/especial; zoom 200%; teclado | Reflujo, controles ≥44px, foco/lectura coherentes; sin overflow global | Navegador y revisión visual, aislado | Propuesto |

Reutilizar/ampliar `tools/Sgol.TaskChecks/CarryoverChecks.cs`, `TransferChecks.cs`, `CurrentAssignmentTransferChecks.cs` y `ReassignmentLifecycleChecks.cs` según alcance real. Revisar fixtures y opciones antes de ejecutar. Agregar cobertura específica de resolución por ítem y migración, y adaptar `tools/Sgol.MobileChecks/interactive-controls-checks.cjs` sin perder comprobaciones de controles compartidos. Compilar proyectos afectados al implementar; no corresponde ejecutar build por este documento.

## Criterios de aceptación

- Tarea, comentario del solicitante, restricción verificable y efecto se entienden sin abrir detalles técnicos.
- No hay textarea vacío grande por cada petición; editor solo al actuar, sin pérdida de borrador.
- Cada tarea admite decisiones independientes; las demás conservan su estado.
- Responsable y fecha se pueden cambiar juntos, siempre con capacidad y horario disponibles.
- Ninguna decisión altera entregas, autoría o evidencia histórica ni deja cambios parciales.
- Consultas, contadores, restauración y reconocimiento distinguen acciones por ítem y registros heredados.
- La lista reduce separación y paneles repetidos, mantiene accesibilidad y conserva controles distinguibles ya incorporados.

No quedan pendientes las dos decisiones consultadas al usuario: ambas están incorporadas. La ampliación de ventanas, excepciones de sobrecarga, catálogo de capacitación y decisiones parciales de incidentes quedan fuera del alcance de este plan.

## Implementación del 03/11/2026 y ajuste solicitado durante la ejecución

El usuario pidió sustituir los botones repetidos por una barra flotante como la de Tareas/Validación, limitada al apartado. Se reutiliza `task-action-panel` con selección individual por radio/fila, nombre de tarea en la barra y acciones habilitadas según su estado. `position: sticky` queda contenido en `postponement-review-scope`; la barra no sigue al usuario al resto de Supervisión. Seleccionar otra tarea conserva el borrador anterior, cierra su editor y cambia el destino explícito de la barra. No introduce aprobación masiva.

Implementación principal: `OperationalCarryover.Resolution.cs`, `Index.Carryover.cs`, `_PostponementRequest.cshtml`, `_Carryovers.cshtml`, `postponement-review.css` y `postponement-review.js`. Se adaptaron lectores, bloqueo de nuevas solicitudes, actualización de asignaciones, restauración, historial y consumo de fechas originales del bono. Las fórmulas del bono no cambian.

Precisión respecto al diseño inicial: la entidad de resolución se conserva embebida en `ResultsJson`, aprovechando la persistencia JSON atómica existente. Incluye intención, hash del comando, actor, fecha, comentario completo, acción, persona/horario resultantes, revisión y recibo de transferencia. No se crea una tabla paralela ni se reescriben peticiones históricas. `PendingItems()` deriva pendientes por tarea; los resultados antiguos conservan su comentario global. La migración `20261103220000_AddCarryoverItemResolution` agrega el estado agregado RESUELTA y actualiza la protección SQL: solo permite anexar un resultado único, no modificar decisiones previas, snapshots o solicitudes terminales. Un downgrade con decisiones nuevas se bloquea para no reinterpretarlas como aprobaciones integrales.

Las propuestas y confirmaciones validan horario, capacidad efectiva más previsión, vigencias y elegibilidad; las confirmaciones se serializan con el bloqueo operativo existente y revalidan huella/revisión dentro de una transacción. La reasignación utiliza el recibo `OperationalTransfer` conservando la misma asignación, evidencia y autoría; no reutiliza la excepción de sobrecarga de correcciones. Una decisión no cambia las hermanas. La restauración admite el resultado individual aplicable en una petición mixta.

### Evidencia ejecutada

Base aislada: `localhost:5440/sgol_task_checks_resolution_20261103`, restaurada de la simulación. Host de formularios: `https://localhost:7291`, autenticación sustituida exclusivamente en el runner; Razor, CSRF, autorización de servicio y persistencia reales. Las pruebas crean actores/asignaciones propios. Copia original observada: 13 solicitudes, cuatro posposiciones pendientes. No se resolvieron solicitudes compartidas como parte de las pruebas.

| Escenario ejecutado | Resultado / evidencia | Estado |
|---|---|---|
| Productor real de petición de cinco tareas y resoluciones mixtas | `CarryoverResolutionChecks`: aprobación, rechazo, reasignación, cambio de día; hermanas pendientes y cierre final correcto | Aprobado |
| Reenvío idéntico, propuesta obsoleta y competencia por último cupo | Una decisión por ítem; segunda propuesta no sobreasigna; rollback conserva pendientes | Aprobado |
| Fuera de ventana, destinatario inválido, capacidad insuficiente y autoaprobación | Bloqueo servidor sin excepción de carga | Aprobado |
| Entrega durante espera | Cierre individual sin mover fechas ni responsable | Aprobado |
| Reasignación e historial | Recibo conserva origen; lector no cuenta reasignación como arrastre aprobado | Aprobado |
| Restauración de una reprogramación en petición mixta | Revierte solo fechas elegibles; petición/decisiones conservadas | Aprobado |
| Migración repetida/modelo | Peticiones heredadas conservadas, modelo sin diferencias pendientes | Aprobado |
| Formularios de las cuatro acciones desde la barra | `postponement-resolution-flows.cjs`: revisar, editar/inutilizar huella, revisar otra vez y confirmar; comentario e historial del solicitante | Aprobado |
| POST sin antiforgery | HTTP 400 | Aprobado |
| 320/768/1440, claro/oscuro/especial, Edge/WebKit | `postponement-resolution-checks.cjs`: 18 combinaciones; teclado, borrador y texto 200%; capturas inspeccionadas | Aprobado |
| Barra única y límites del apartado | Una serie de botones; selección habilita acciones; permanece visible dentro y sale del viewport al abandonar el apartado | Aprobado |
| Compilación y publicación | Publicación final sin errores; registros privados | Aprobado |
| Dispositivo físico, lector de pantalla, caída real de red y transacciones simultáneas en distintos procesos | No ejecutados; competencia cubierta mediante dos vistas previas y confirmaciones intercaladas, no una prueba de estrés multiproceso | No ejecutado |
| Todas las combinaciones de procesos/PROGRESO/caja, baja de empleo y permisos revocados | Exclusiones y validaciones inspeccionadas; no se ejecutó toda la matriz combinatoria inicial | No ejecutado |

Fallos intermedios corregidos: la protección SQL antigua impedía resolver parcialmente; el primer host aislado no encontraba los estáticos; el binding mezclaba validaciones de formularios independientes. La aserción inicial de historial leía solo texto visible de un desplegable cerrado: se corrigió para inspeccionar su contenido. Ninguno se presenta como una prueba aprobada antes de corregirlo.

Evidencias privadas en `.local/postponement-resolution-20261103/`: registros de servicios, `browser-results.json`, `flows-results.json`, capturas, publicación, migración, respaldo y verificación del despliegue. No se adjuntan datos privados al repositorio. Sin commit ni push.

### Entrega compartida completada

Migración aplicada con `sgol_migrator` en `127.0.0.1:5440/sgol_local`, después de respaldo privado. Se comparó el hash del contenido completo de las 13 peticiones antes/después: sin cambios de snapshots, estados o decisiones. Se reinició únicamente el backend compartido, conservando el proxy, con `.local/runtime/postponement-resolution-20261103-final/Sgol.Web.dll`. Se verificaron proceso/binario activo, `/health/ready` = ready, acceso LAN HTTP 200 y entrega del CSS/JS de la barra, coincidentes con el artefacto final. Registro: `deployment.json`. No se ejecutaron decisiones operativas en la simulación compartida.

### Revisión visual y claridad de bloqueos — 03/11/2026

El usuario señaló repeticiones del título y fecha, desalineación de selector/responsable y una presentación demasiado parecida a un párrafo. Se sustituye por cabecera alineada (selector de 44px, título, responsable, código/carga y estado), motivo a la izquierda y comparación visual «Cierre actual → Horario solicitado» a la derecha. En móvil los bloques refluyen y conservan dos columnas para la comparación temporal. El catálogo de causas queda en detalles; explicaciones extensas tienen acceso al contenido completo. La barra única conserva la misma selección y acciones.

La inspección de los registros existentes identificó un bloqueo concreto: TAR-0193, «Dar seguimiento a pedidos por recibir y actualizar fechas de compromiso», ocupa 09:30–10:15 y se cruza con las propuestas de Viri. El diagnóstico ahora utiliza `CarryoverBlock` (título, detalle, acción), identifica las reservas efectivamente superpuestas desde el mismo ledger que valida la propuesta y muestra «Cruce de horario», el horario/nombre de la otra tarea y la acción posible. No repite el nombre de la tarea solicitada ni su fecha propuesta. La capacidad insuficiente se explica una sola vez con carga/capacidad; las cadenas completas con contexto se conservan para consumidores externos a la tarjeta. No se cambiaron las reglas de elegibilidad, transacciones ni fechas.

| Escenario | Prueba / entorno | Estado |
|---|---|---|
| Diagnóstico del registro histórico TAR-0379 | Copia aislada: identifica TAR-0193 y 09:30–10:15; texto de tarjeta sin título/fecha repetidos | Aprobado |
| Capacidad insuficiente | Una explicación estructurada con cifras; sigue bloqueando aprobación | Aprobado |
| Alineación selector/título y comparación de fechas | Edge/WebKit, 320/768/1440, claro/oscuro/especial: 18 combinaciones, medición de primera línea | Aprobado |
| Reflujo, teclado, borradores y barra limitada al apartado | Razor real aislado, texto al 200 %, selección/cierre de editor | Aprobado |
| Cuatro acciones con vista previa, invalidación al editar, confirmación y CSRF | Formularios reales; historial del solicitante conservado | Aprobado |
| Regresiones por ítem, competencia por capacidad, entrega durante espera y restauración | `CarryoverResolutionChecks`, misma base aislada | Aprobado |
| Compilación/publicación | Artefacto `.local/runtime/postponement-visual-20261103/`; CSS final comprobado por hash | Aprobado |
| Dispositivo físico y lector de pantalla | No disponibles en esta revisión | No ejecutado |

Una regla compartida de etiquetas de radio anulaba la alineación inicial: la prueba midió 11px de desfase y falló. Se acotó el selector CSS a la cabecera y se repitió la matriz hasta aprobar. Evidencia de esta revisión: `.local/postponement-visual-20261103/`, con registros, resultados y capturas inspeccionadas. Sin migración nueva ni escrituras de prueba en la base compartida. Se mantiene la autorización previa para actualizar la app compartida; `deployment.json` registra proceso/binario, readiness y acceso LAN de esta revisión.

## Ajuste del editor y búsqueda de horario — 03/11/2026

Cambio de interfaz e interacción: editor alineado con la tarjeta, destino y búsqueda en columnas, comentario a todo el ancho y resultado en datos separados (responsable, horario y cargas). Se muestra código → especialidad de la asignación → minutos; los registros sin especialidad lo indican expresamente.

«Cambiar propuesta» conserva RESCHEDULE y permite buscar un hueco en el día solicitado o elegir otra fecha dentro de la ventana. Usa el mismo asignador real y exige horario/capacidad; no altera la jerarquía ni el historial. Cambiar el modo o la fecha invalida la confirmación anterior. La fecha viaja en ItemDecision.Day en ambos casos.

Regla investigada: CapacityWorker.EligibleFor exige especialidad activa y fuera de prueba si existe especialidad; en su ausencia exige igualdad de rol. TransferRecipientsAsync además excluye al responsable actual y limita los destinos por RolesFor del revisor. No existe herencia ascendente de roles. Lectura de la simulación compartida: las cuatro peticiones pendientes de Viri llevan Administración; person_specialization solo vincula esa especialidad activa a Viri. Por eso no existe otro destinatario, independientemente de Subcoordinación. El usuario solicitó mantener esta regla.

Evidencia en `.local/postponement-editor-20261103/`. La autenticación del host aislado es de pruebas; formularios, antifalsificación, servicios y persistencia son reales. No se resuelven solicitudes compartidas durante las pruebas.

| Escenario | Esperado | Entorno / prueba | Estado |
|---|---|---|---|
| Editor, especialidad, selección y barra | Sin desbordamiento, accesible por teclado | Edge/WebKit; 320,768,1440; claro,oscuro,especial; 18 combinaciones | Aprobado |
| Texto ampliado y borrador | Reflujo 200%, conserva comentario al cerrar/abrir | Navegadores anteriores | Aprobado |
| Reglas de resolución | Capacidad, concurrencia, permisos, historial y entrega durante espera preservados | Copia aislada, CarryoverResolutionChecks | Aprobado |
| Localización de fixture con paginación | Encontrar la nueva petición sin asumir última página | Primer intento de flujos | Falló por supuesto de prueba; recorrido corregido para buscar páginas |
| Jerarquía nueva | No modificar elegibilidad | Inspección de diff | No aplica por indicación del usuario |
| Formularios reales | Reasignar, cambiar propuesta (mismo día y otro día), aprobar y rechazar; invalida vista previa al editar | Host aislado, postponement-resolution-flows.cjs | Aprobado; comentarios, CSRF e historial del solicitante verificados |
| Publicación compartida | Cargar ensamblado nuevo, salud y LAN disponibles | .local/runtime/postponement-editor-20261103/Sgol.Web.dll; deployment.json | Aprobado; readiness ready, LAN 200, recursos CSS/JS verificados |
| Sesión autenticada compartida | Comprobar la pantalla dentro de la sesión del usuario | No se usó ni modificó una sesión compartida | No ejecutado; renderizado y flujos verificados en copia aislada |

Build/publish final aprobado. No migraciones adicionales, cambios de jerarquía, commit ni push. La consulta compartida fue de solo lectura. Revisión visual de editor móvil/escritorio y resultado de reprogramación efectuada sobre capturas reales del host aislado. El primer intento visual encontró acciones plegadas por el patrón móvil existente; se adaptó la prueba para abrir «Más acciones», conservando ese patrón.
