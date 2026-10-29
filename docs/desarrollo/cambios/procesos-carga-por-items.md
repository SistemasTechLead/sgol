# Procesos: carga calculada a partir de cantidades reportadas

Fecha: 29/10/2026. Estado: análisis previo conservado; función implementada en código. Resultados ejecutados y límites de cobertura en el plan asociado.
Tipo: mixto (reglas, persistencia, agenda e interfaz). Se ejecutaron pruebas en bases aisladas; no se migró ni desplegó la simulación compartida.

Plan ejecutable asociado: [Plan de implementación: procesos con carga por ítems](plan-implementacion-procesos-items.md). Contiene fases, contratos, escenarios y criterios de cierre basados en las decisiones actuales del usuario.

## Objetivo

Una tarea reporta una cantidad junto con su evidencia habitual. Esa cantidad calcula la carga de la tarea siguiente del mismo proceso. Ejemplo: identificar pendientes reporta 20 fotografías; a 3 minutos por fotografía, la siguiente tarea recibe 60 minutos. La cantidad no altera su entregable ni su mínimo/máximo de archivos.

## Comportamiento observado y fuentes

| Fuente | Comportamiento observado / punto de integración |
|---|---|
| `src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalSequences.cs` | Versiones publicadas inmutables; reglas por etapa; ejecución, etapa, intento y eventos separados. La etapa copia carga, evidencia y agenda. Su restricción actual exige carga entre 1 y 4800 minutos. |
| `src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessCatalogService.cs` | `SaveAsync`, publicación y `CopyStep` guardan/copían reglas y snapshots. Agregar aquí catálogo y enlaces versionados, sin modificar versiones publicadas. |
| `src/Sgol.Web/Pages/Tasks/Processes.cshtml` y `.cshtml.cs` | `/tareas/procesos`: tarjetas reordenables, diálogo «Editar detalles», guardar borrador/publicar. Reutilizar diálogos y campos existentes. |
| `src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/ProcessPlanningService.cs` | `Reservations` calcula reservas desde snapshots antes de conocer la cantidad real; `ReadAsync` muestra minutos de configuración. La selección de personas también debe usar la estimación definida. |
| `src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessService.cs` | `StartAsync` copia carga a etapas y reserva responsables; `DecideAsync` guarda evidencia, completa intento y habilita siguiente etapa en transacción. La revisión del trabajo anterior puede concluir al entregar la siguiente tarea. `DEVOLUCION` sustituye intentos desde el destino y reinicia etapas posteriores. `Reservation` copia carga de etapa a asignación/reserva. |
| `src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessService.Scheduling.cs` | `EnableScheduledStage` comprueba capacidad, responsable reservado y anclas críticas; puede programar o dejar pendiente. Recalcula reservas posteriores compatibles. |
| `src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalCapacityService.cs` | `LedgerAsync` suma asignaciones y reservas de etapas y planes preparados: las etapas que aún no comienzan consumen capacidad estimada. |
| `src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTasks.cs` | `OperationalAssignment.LoadMinutes` y `RemainingMinutes` alimentan carga; `GoalUnits`/`ProgressUnits` tienen semántica de avance y transferencias. No reutilizarlos para el conteo solicitado. |
| `src/Sgol.Web/Pages/MyWork/_ProcessActionForm.cshtml`, `src/Sgol.Web/Interface/MyWork/ProcessActionForm.cs` | Formulario compartido de entrega/revisión/devolución y consulta del historial. La fotografía actual utiliza cámara; subir una foto previamente guardada requiere confirmar la experiencia deseada. |
| `src/Sgol.Web/Pages/MyWork/Index.Operational.cs`, `src/Sgol.Web/Pages/MyWork/Processes.cshtml.cs` | Revisar y extender ambos puntos de entrada de acciones de proceso al implementar. |
| `src/Sgol.Web/Infrastructure/Persistence/Planning/AssignmentRefreshService.cs`, `AssignmentDefinition.cs` | Actualización de definiciones toca etapas y asignaciones: impedir que una carga calculada vuelva accidentalmente al valor fijo del catálogo. Hay modificaciones previas en estos archivos; no atribuirlas a esta propuesta. |
| `src/Sgol.Web/Infrastructure/Persistence/Planning/LiveBoardReader.cs`, `WeeklyBoardSnapshot.cs` | Consumen `LoadMinutes` para filas y sumas. La integración debe comprobar que se lee carga efectiva y conservar interpretación histórica. |

Hechos obtenidos por lectura del código actual, incluyendo cambios locales previos. No se ejecutaron formularios, servicios ni consultas a la base. Reportes/exportaciones adicionales necesitan inventario dirigido durante implementación; no se declara cobertura total de esos consumidores.

## Propuesta de operación

1. Botón «Ítems y tiempos» en el proceso, con filas nombre/minutos por unidad, altas y retiro de uso futuro. Catálogo compartido entre procesos, según respuesta del usuario. Conservar revisiones de tarifas y snapshots para no cambiar ejecuciones existentes.
2. En «Editar detalles», separar «Reportar cantidad» (ítem que cuenta esta tarea) de «Calcular mi carga» (cantidad de una etapa fuente). Permite que una tarea consuma un conteo y produzca otro sin confundirse. Para la primera versión, proponer exclusivamente la siguiente tarea como destino.
3. Mostrar al configurar el enlace una frase inequívoca: «La cantidad reportada aquí calculará la carga de Tomar y cargar fotografías faltantes». No enlazar por texto o índice mutable: usar identidad estable y validar al reordenar/eliminar etapas.
4. En Mi trabajo: cantidad obligatoria al entregar si está configurada; conservar evidencia existente. Mostrar antes de enviar «20 fotografías × 3 min = 60 min de carga siguiente». El servidor calcula el resultado.
5. Guardar cantidad y procedencia por intento, y carga aplicada en la etapa/asignación destino. Congelar nombre, tarifa, fórmula y versión para explicar el cálculo histórico.
6. Aplicar el cálculo al liberar la siguiente etapa. Si no cabe, mostrar causa real y las opciones autorizadas: buscar persona compatible de menor carga, guardar entrega y dejar siguiente etapa pendiente, o asignar de todos modos permitiendo sobrecarga/solapamiento. Reasignar o forzar solo se permite para entregas de tareas de Subcoordinación o Dirección. Interpretación comunicada: se comprueba el rol de origen de la tarea que libera la siguiente etapa, además de la autorización para entregarla; no se deduce del rol del destinatario. La excepción permite usar descansos dentro del turno, además de sobrecarga y solapamiento. Conserva turno, ausencias, rol, especialidad, empleo vigente y dependencias; se registra actor, efecto e intervalo de descanso utilizado. La búsqueda normal de menor carga sigue respetando descansos. No confundir falta de capacidad con ausencia, falta de rol o etapa no habilitada.
7. Mostrar el cálculo recibido al destinatario y en historial; la cantidad es declarada, no una verificación automática de cuántos objetos aparecen en una foto.

## Decisiones confirmadas por el usuario

- La carga calculada sustituye la estimación: 20 × 3 = 60 minutos, sin sumar carga fija previa.
- Antes de conocer la cantidad se usa la carga actual como estimación de reserva.
- Conflictos de capacidad ofrecen las tres opciones descritas arriba; no resolverlos silenciosamente.
- Un conteo de cero salta a auditoría final, que debe configurarse explícitamente y exigirse al publicar procesos con esta función. Registrar etapas omitidas y liberar sus reservas; no marcarlas como ejecutadas/aprobadas. Auditoría conserva su carga y entregable.
- Un conteo erróneo se devuelve al responsable anterior. El trabajo ya realizado por quien devuelve debe permanecer disponible en esa misma tarea para continuar; el código actual sustituye intentos y no proporciona por sí solo esa reanudación.
- Los enlaces van a la siguiente tarea y pueden encadenarse con unidades distintas: A informa cajas grandes; B recibe esa carga y reporta cajas pequeñas/etiquetas para C. Un conteo no se propaga automáticamente a todas las etapas.
- Un conteo por tarea; no se necesitan fracciones. Propuesta de validación: cantidad entera no negativa y minutos por unidad enteros positivos.
- Volúmenes que no caben se mantienen como una tarea con resolución de planificación; división automática entre días/lotes fuera de alcance.
- Catálogo de ítems compartido por procesos. La función solo aplica a nuevas ejecuciones; no migrar ejecuciones iniciadas.
- Mantener captura por cámara y captura de pantalla actuales; no agregar selección de foto de galería.
- Reasignar por menor carga o forzar sobrecarga/solapamiento solo está permitido cuando la tarea que entrega procede de Subcoordinación o Dirección. Para otros orígenes se conserva guardar y dejar pendiente, con resolución por los mecanismos autorizados existentes.
- Aclaración del usuario: forzar permite utilizar parte o todo un descanso dentro del turno como caso especial. El aviso debe indicar el descanso afectado y registrar la excepción. No permite asignar fuera de turno o durante una ausencia, sin rol/especialidad o antes de cumplir dependencias. La excepción no modifica el calendario base ni habilita descansos para otras asignaciones.
- Tras corregir el conteo, la carga adopta íntegramente el nuevo total sin descontar trabajo previo. Ejemplo: 20 se corrige a 12, con 5 ya realizadas: carga resultante 12 × 3 = 36 min, no 21. Trabajo y evidencia previa se conservan para continuar; esta regla puede reservar más tiempo que el esfuerzo que realmente falta, por decisión del usuario.
- Cambiar una tarifa compartida se aplica automáticamente a futuras ejecuciones de todos los procesos que usan el ítem, sin republicar cada uno. Capturar revisión al preparar/reservar; planes ya preparados y ejecuciones iniciadas conservan su tarifa. Una nueva preparación adopta la tarifa vigente.

## Reglas de excepción, auditoría y reanudación

1. **Permisos de excepción:** validar origen Subcoordinación/Dirección en servidor, además de acceso, intento y revisión vigentes. Otros orígenes no pueden forzar ni cambiar destinatario desde este aviso. Registrar la decisión explícita y revalidar capacidad/compatibilidad al confirmar.
2. **Límites de «Asignar de todos modos»:** ignorar capacidad, solapamiento y descansos exclusivamente para la asignación forzada; conservar rol, especialidad, empleo vigente, ausencias, límites del turno y dependencias. Mostrar qué descanso se usa y registrar la decisión y sus intervalos, sin modificar el calendario ni la elegibilidad de otras tareas. La corrección de Supervisión ofrece un precedente en `OperationalSupervisionService.cs` y `OperationalSupervisionService.Reassignment.cs`; no trasladar ciegamente su política de vencimiento al proceso. Volúmenes imposibles siguen pendientes de resolución de planificación.
3. **Trabajo conservado versus carga:** si se corrigió 20 a 12 y ya se hicieron 5, reservar 36 min completos sin restar 5. El conteo de salida que alimentará C no puede reutilizarse como progreso de B, pues puede tener otra unidad. No inventar avance a partir del número de fotos adjuntas. Conservar continuidad entre intentos y acceso al trabajo/evidencia, sin duplicar cargas activas.
4. **Auditoría y cero:** marcar explícitamente la etapa final como auditoría y exigirla al publicar procesos con esta función. No reconocerla por su nombre. Recibe reporte de cero y lista de omisiones. Propuesta de borde: el conteo de salida solo se configura donde existe tarea siguiente, por lo que auditoría final no produce un conteo para otra etapa. Si auditoría devuelve, conservar el salto histórico y reabrir el destino de devolución configurado, aplicando de nuevo la regla al entregar el conteo corregido. Son reglas de diseño propuestas para estos bordes, no pruebas ejecutadas.
5. **Cambio de tarifa compartida:** adoptar revisión vigente al preparar/reservar nuevas ejecuciones sin republicar procesos; conservar snapshots en planes preparados y ejecuciones iniciadas. Revalidar la revisión capturada entre vista previa y confirmación. No modificar reservas históricas al editar el catálogo.
6. **Sin alternativa disponible:** buscar menor carga entre personas compatibles que puedan recibir la tarea; si ninguna existe, mantener opciones de pendiente o excepción permitida, sin repetir búsquedas ni cambiar roles.

## Invariantes y diseño técnico propuesto

- No cambiar carga del catálogo de tareas al capturar un conteo de una ejecución.
- No modificar entregable ni mínimo/máximo de fotos. El conteo no sustituye progreso por unidades. El salto por cero omite ejecución de etapas intermedias y requiere estados/auditoría explícitos, sin inventar aprobaciones.
- No mezclar intentos, rondas, procesos o responsables al tomar cantidades.
- Migración aditiva con configuración opcional; registros anteriores conservan operación. Cero requiere tratamiento explícito, dado el mínimo actual de carga.
- Nuevas entidades/campos para catálogo compartido con revisiones, regla fuente/destino, cantidad por intento y procedencia de carga aplicada. Nombres finales por definir durante implementación.
- Cálculo único en servidor compartido por reserva, activación, reasignación, actualización y consultas; distinguir estimación de resultado efectivo.
- Cantidad/tarifa en hash de idempotencia; misma clave con diferente cantidad debe rechazar conflicto. Guardar evidencia, cantidad, evento y resultado de programación en una transacción, bajo controles de revisión/concurrencia existentes.
- Aviso antes de resolver: «[Nombre] no tiene capacidad para recibir [tarea] de [minutos] en este momento», solo si esa es la causa comprobada. Mostrar las tres opciones acordadas. No anunciar «entrega guardada» antes del commit. Toda opción debe revalidarse ante cambios simultáneos de agenda.
- Conservar cargas y cantidades de intentos anteriores al devolver o transferir; separar carga original de trabajo restante. No recalcular por mera apertura de una pantalla.
- Reutilizar `dialog`, clases `campo`, botones, manejo de cambios pendientes y estilos de procesos. Validar móvil, escritorio, temas, teclado y retorno de foco.

## Secuencia de implementación después de resolver decisiones

1. Convertir las decisiones confirmadas en contratos y criterios de aceptación; conservar explícitos los bordes propuestos de auditoría/devolución al implementar.
2. Esquema/versiones/copias y validación servidor; pruebas de migración y compatibilidad.
3. Editor del proceso y enlace entre etapas; guardar, publicar, reabrir, reordenar y retirar ítems.
4. Captura por intento en ambas rutas de Mi trabajo; fórmula/transacción/idempotencia.
5. Agenda, reservas, capacidad, actualización de asignaciones y transferencias; tratamiento de devoluciones.
6. Consultas/historial/tablero/reportes aplicables; integración de formularios y navegador en entorno identificado.

## Matriz de validación propuesta

Todas las pruebas funcionales están **propuestas**, no ejecutadas. No procede compilar ni ejecutar suites por este documento de análisis.

| Escenario / precondición y acción | Resultado esperado | Prueba y entorno | Estado |
|---|---|---|---|
| Configurar Fotografías=3, enlazar, guardar/publicar/reabrir | Persistencia y enlace correctos; evidencia intacta | Formulario HTTP y BD aislada; navegador | Propuesto |
| Reordenar/eliminar origen o destino y retirar ítem usado | Mantener identidad o impedir publicación con causa y tarea | Servicio + formulario real, aislado | Propuesto |
| A entrega foto y cantidad 20; B tenía carga estimada distinta | B recibe 60 min; A conserva carga propia; evidencia de B intacta | Flujo HTTP/servicios/BD aislada | Propuesto |
| B consume conteo de A y declara otro para C | Cada carga utiliza su fuente e intento correctos | Integración aislada | Propuesto |
| Campo vacío, negativo, exceso, fracción y 0 | Rechazo o comportamiento acordado; sin escrituras parciales | HTTP/BD aislada | Propuesto |
| Conteo cambia reserva y supera capacidad, descanso o ancla crítica | Opciones por origen y causa; sobrecarga y uso de descanso solo explícitos al forzar, conservando turno/ausencias/dependencias | Agenda real con fixtures en BD aislada | Propuesto |
| Conteo menor libera capacidad; mayor mueve reservas posteriores | Capacidad y dependencias coherentes; preservar anclas y tareas ajenas | Comparación antes/después en BD aislada | Propuesto |
| Devolver después de reportar 20; nuevo intento informa 12 | Historial conserva 20; nueva carga 36 donde corresponda | Servicios y formulario real aislados | Propuesto |
| Corregir con destinatario iniciado, transferido o con evidencia | Aplicar política acordada sin borrar avance/historial | Integración aislada | Propuesto |
| Conteo cero en etapa inicial o intermedia | Saltar a auditoría final; liberar reservas omitidas; no inventar entregas | HTTP/BD aislada y navegador | Propuesto |
| Auditoría devuelve después de salto por cero | Restaurar recorrido según política acordada sin perder omisiones históricas | Integración aislada | Propuesto |
| Proceso sin auditoría configurada o cero en etapa final | Validación o comportamiento acordado; sin salto a sí misma | Servicio/formulario aislados | Propuesto |
| Buscar receptor de menor carga; ninguno compatible | Selección respeta rol/especialidad/disponibilidad; sin alternativa, opciones pertinentes | Integración aislada | Propuesto |
| Entregar desde Subcoordinación/Dirección u otro rol; manipular origen | Reasignar/forzar solo por origen autorizado comprobado; guardar pendiente disponible | HTTP/BD aislada | Propuesto |
| Forzar durante descanso con origen autorizado | Permitir dentro del turno; advertir descanso afectado y auditar; calendario base y otras tareas conservan sus restricciones | HTTP/BD aislada y navegador | Propuesto |
| Buscar/reasignar normalmente durante descanso | Respetar descanso; la excepción solo existe al elegir forzar | HTTP/BD aislada | Propuesto |
| Forzar con origen permitido pero destinatario ausente/fuera de turno | Rechazo por disponibilidad; no basta el permiso de sobrecarga | HTTP/BD aislada | Propuesto |
| Corregir 20 a 12 tras realizar 5 | Evidencia/trabajo disponible al reanudar; reservar 36 min completos, no 21 | HTTP/BD aislada | Propuesto |
| Cambiar tarifa global con dos procesos y plan preparado | Nuevas preparaciones adoptan tarifa sin republicar; plan preparado y ejecución iniciada conservan snapshot | Integración aislada | Propuesto |
| Dos entregas simultáneas; reintento idéntico o cantidad diferente | Una transición; repetición segura o conflicto; atomicidad | HTTP concurrente/BD aislada | Propuesto |
| Usuario ajeno manipula conteo, destino o tarifa | Rechazo servidor; sin cambios | HTTP con roles/CSRF, aislado | Propuesto |
| Cambiar tarifa más carga de catálogo; Actualizar asignaciones | No sustituir silenciosamente resultado calculado ni historia | Integración aislada | Propuesto |
| Publicar nueva versión con ejecuciones/planes preparados existentes | Adoptar política acordada sin cambios retroactivos accidentales | BD aislada con versiones históricas | Propuesto |
| Consultar tarea/capacidad/tablero/historial/exportación aplicable | Misma carga efectiva; cálculo trazable y estimación diferenciada | Integración y lectura de salidas | Propuesto |
| Proceso sin ítems; flujo legado PDF y evidencia mixta | Comportamiento previo conservado | Adaptar ProcessRuntimeChecks y ProcessMyWorkHttpChecks | Propuesto |
| Diálogos/campos: 320px, escritorio, temas, teclado y textos largos | Sin desbordamiento; foco/etiquetas; errores y éxito visibles | Navegador en aplicación real aislada | Propuesto |
| Investigación de implementación y estilos actuales | Fuentes y riesgos identificados en tablas anteriores | Lectura estática de repositorio | Aprobado: inspección, no prueba funcional |

## Estado del trabajo

Solo se creó y actualizó este documento de análisis. No se alteraron código funcional, datos, servicios ni migraciones. Existen cambios locales previos en editor de procesos, actualización de asignaciones, supervisión y herramientas: preservarlos y revisar el diff antes de implementar. Las cinco precisiones de negocio fueron respondidas; la interpretación del rol de origen y los bordes propuestos se hacen explícitos arriba. Implementación y pruebas funcionales pendientes; todas las pruebas funcionales siguen propuestas.
