# Plan de implementación: procesos con carga por ítems

Fecha: 29/10/2026. Clasificación: cambio mixto. Estado: implementación en código y validación aislada ejecutadas. Sin aplicar migraciones ni desplegar en la simulación compartida. Véanse matriz y cierre para límites de cobertura.

Este documento convierte las decisiones de la conversación en trabajo ejecutable. Complementa el [análisis de impacto](procesos-carga-por-items.md). En caso de discrepancia, prevalecen las decisiones actuales del usuario y este plan sobre las recomendaciones iniciales del análisis. Se aplica el [protocolo de implementación](../PROTOCOLO_IMPLEMENTACION.md) y la [referencia visual](../../design/ESTILO_VISUAL_V2.md).

## 1. Resultado esperado y alcance

Configurar un catálogo compartido de ítems con minutos por unidad; capturar un conteo por tarea y usarlo para reemplazar la carga estimada de la siguiente tarea del mismo proceso. Conservar entregables, trabajo previo, historial y reglas de compatibilidad. Permitir resolver falta de capacidad con opciones explícitas y saltar a auditoría final al reportar cero.

Ejemplo de aceptación: Subcoordinación entrega una fotografía del reporte y declara 20 fotografías pendientes. Con tarifa de 3 min/unidad, la siguiente tarea recibe 60 min de carga, independientemente de su estimación previa. El cierre sigue calculándose con la política de margen capturada: 60 min de carga no significa necesariamente un intervalo de exactamente 60 min.

Fuera de alcance: varios conteos por tarea, fracciones, deducir cantidades de imágenes, aumentar automáticamente requisitos de evidencia, repartir entre días/lotes, ampliar turnos, modificar procesos ya iniciados, selección de fotografía de galería y limpieza de datos simulados.

## 2. Contrato de negocio confirmado

| ID | Regla y criterio de aceptación |
|---|---|
| R01 | Carga efectiva = cantidad × minutos por unidad. Sustituye la estimación; no suma carga fija previa. |
| R02 | Antes del conteo, reservar con la carga actual de la tarea, identificada como estimación. |
| R03 | Catálogo compartido entre procesos; un conteo entero no negativo por tarea y minutos enteros positivos por unidad. Vacío no equivale a cero. |
| R04 | El conteo solo alimenta la siguiente tarea. Una tarea puede consumir un conteo y producir otro distinto: cajas grandes → cajas pequeñas/etiquetas. |
| R05 | Conservar entregable, evidencia y límites de archivos; el conteo no se convierte en progreso por unidades ni en número requerido de fotografías. |
| R06 | Si la carga no cabe, presentar causa y opciones: buscar persona compatible de menor carga, guardar entrega y dejar siguiente etapa pendiente, asignar de todos modos. No elegir silenciosamente por el usuario. |
| R07 | Buscar otro responsable y forzar desde este aviso solo para entregas provenientes de tareas de Subcoordinación o Dirección. Interpretación comunicada al usuario: rol de la tarea origen, validado en servidor junto con identidad del responsable e intento vigente. Para otros orígenes, guardar pendiente y mecanismos existentes autorizados. |
| R08 | Forzar permite sobrecarga, solapamiento y usar descansos dentro del turno. Mantiene límites de turno, ausencias, empleo vigente, rol, especialidad y dependencias. La excepción es exclusiva de la asignación y se audita; no modifica el horario general. |
| R09 | Cantidad cero salta a auditoría final explícitamente configurada y exigida al publicar procesos que utilizan esta función. Liberar reservas intermedias y registrar omisiones, sin simular entregas/aprobaciones. Auditoría conserva su carga y evidencia. |
| R10 | Conteo erróneo se devuelve para que lo corrija el responsable anterior. Conservar el trabajo del receptor y permitir que continúe. |
| R11 | Tras corrección, adoptar el nuevo total completo sin descontar trabajo previo: 12 × 3 = 36 min aunque se hayan realizado 5. No crear un nuevo campo obligatorio de avance para descontarlas. |
| R12 | Tarifa compartida nueva se usa sin republicar cada proceso en futuras preparaciones/ejecuciones. Congelarla al preparar/reservar; planes preparados y ejecuciones iniciadas conservan su tarifa. |
| R13 | Activar la configuración solo para nuevas ejecuciones. Procesos existentes sin esta configuración y ejecuciones ya iniciadas mantienen su comportamiento. |
| R14 | Volúmenes que no caben permanecen como una tarea pendiente de resolución de planificación; no dividir, truncar carga, ampliar turno ni recortar artificialmente la duración. |

Decisiones adicionales confirmadas durante la implementación:

| ID | Regla confirmada |
|---|---|
| R15 | Retirar un ítem impide nuevas selecciones. Los procesos que ya lo tenían pueden seguir preparando ejecuciones; planes preparados e historial conservan su captura. |
| R16 | Si la receptora aún no tiene asignación, su responsable reservado o Dirección pueden devolver el conteo al productor con motivo obligatorio, sin fingir ejecución. Validar revisión, permiso e idempotencia en servidor. |
| R17 | Si la devolución ocurre otro día, reprogramar las tareas completas flexibles de la nueva ronda desde disponibilidad actual dentro del turno. Conservar anclas críticas y límites fijos; no dividir ni ampliar turnos. |

## 3. Hallazgos verificados y componentes propietarios

Esta sección conserva el diagnóstico previo a editar; la evidencia ejecutada figura en §7 y §9. La rama observada es `master`; existen modificaciones locales previas en asignaciones, procesos, supervisión, estilos y herramientas. Antes de editar implementación, registrar y preservar ese diff. No atribuirse cambios ajenos ni reiniciar el entorno por este plan.

Rutas desde la raíz del repositorio:

| Componente real | Comportamiento observado / intervención necesaria |
|---|---|
| `src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalSequences.cs` | Definición, ejecución, etapa, intento y eventos; snapshots y restricciones. Agregar configuración opcional, procedencia y estados de omisión/reanudación compatibles. |
| `.../Sequences/OperationalProcessCatalogService.cs` | `SaveAsync`, publicación y `CopyStep`. Integrar catálogo/enlaces y validación de auditoría; conservar identidad al copiar/reordenar etapas. |
| `.../Sequences/ProcessPlanningService.cs`, `.Selection.cs`, `PreparedProcessStart.cs` | Reservas, selección de responsables y captura de agenda preparada. Capturar revisiones de tarifa una sola vez y reutilizarlas en activación. |
| `.../Sequences/OperationalProcessService.cs` | `StartAsync`, `DecideAsync`, `EnableActive`, `TickAsync`, `ReassignAsync`, `Reservation`. Integración transaccional del conteo, resolución, omisiones y continuidad. `ReassignAsync` actual exige Dirección: no ampliar ese método globalmente para habilitar este aviso. |
| `.../Sequences/OperationalProcessService.Scheduling.cs` | `EnableScheduledStage` calcula huecos, reservas posteriores y conflictos con etapas críticas. Usar carga calculada y conservar política elegida. |
| `.../Sequences/OperationalProcessTimer.cs` | Reintenta `TickAsync` cada 15 segundos. Nunca puede elegir forzar/reasignar automáticamente ni habilitar etapas omitidas. |
| `src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalScheduling.cs` | `OperationalAgenda.Duration`, `Find`, captura de margen. Actualmente limita carga a 1..4800; preservar distinción carga/duración. |
| `.../Planning/PersonalSchedules.cs` | `TemporalEligibilityContext.Day/Evaluate`: intervalos de presencia y huecos; ausencia y horario faltante tienen causas distintas. Excepción puntual para descanso sin flexibilizar `Evaluate` para todos. |
| `.../Planning/OperationalCapacityService.cs` | `LedgerAsync`, `CheckTime`, `CaptureTime`: suma reservas y tareas. Evitar doble contabilización y conservar demanda calculada pendiente. |
| `.../Planning/OperationalSupervisionService.cs`, `.Reassignment.cs`, `.Transfer.cs` | Corrección admite solapamiento pero sigue comprobando recepción/turno; transferencias tienen restricciones propias. Es referencia, no implementación suficiente del uso de descanso. No copiar el recorte de vencimiento de correcciones. |
| `.../Planning/OperationalTasks.cs`, `OperationalTaskService.cs`, `.Camera.cs` | `RemainingMinutes`, contratos de detalle, entrega de evidencia y fotografías ligadas a asignación/sesión. Reanudación debe conservar acceso al trabajo sin mover evidencia histórica ni reutilizar sesiones vencidas. |
| `.../Planning/AssignmentDefinition.cs`, `AssignmentRefreshService.cs` | Comparan/restablecen carga de catálogo. Separar configuración base, estimación y resultado calculado para que Actualizar asignaciones no borre el cálculo. |
| `.../Planning/OperationalCarryover.cs`, `.Postponement.cs`, `.Restoration.cs` | Copian/comparan estructura temporal y carga. Incorporar procedencia/excepción en copias y propuestas obsoletas donde corresponda. |
| `.../Planning/LiveBoardReader.cs`, `WeeklyBoardSnapshot.cs` | Consumen carga de asignación para filas/sumas; verificar carga efectiva y evitar contar omitidas. |
| `src/Sgol.Web/Infrastructure/Persistence/Reporting/ReportData.cs`, `ReportAnalysis.cs` | `ReportRow`, minutos y restantes alimentan reportes. Seguir productor de filas/exportaciones durante implementación y agregar trazabilidad donde exista detalle del proceso. |
| `src/Sgol.Web/Pages/Tasks/Processes.cshtml`, `.cshtml.cs` | Editor con tarjetas y «Editar detalles». Añadir botón de catálogo compartido, selector de ítem y auditoría. |
| `src/Sgol.Web/Pages/Planning/Processes.cshtml`, `.cshtml.cs` | Mostrar estimación y configuración preparada; no prometer carga definitiva antes del conteo. |
| `src/Sgol.Web/Pages/MyWork/_ProcessActionForm.cshtml`, `Index.Operational.cs`, `Processes.cshtml.cs`, `src/Sgol.Web/Interface/MyWork/ProcessActionForm.cs` | Ambos handlers, `CompleteProcess` y `Decide`, deben aplicar el mismo contrato y mostrar cantidad, cálculo y resolución. |
| `src/Sgol.Web/wwwroot/js/process-rules.js`, `task-process.js`, `src/Sgol.Web/wwwroot/css/processes.css` | Reutilizar diálogos, pendientes de guardado, foco, reordenamiento y estilos; sin nueva biblioteca. |
| `src/Sgol.Web/Infrastructure/Persistence/SgolDbContext.cs`, `Migrations/` | Registro del esquema y migración aditiva; verificación modelo/migración y compatibilidad histórica. |

Los prefijos `.../Sequences` y `.../Planning` de esta tabla corresponden a `src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences` y `src/Sgol.Web/Infrastructure/Persistence/Planning`. El diseño descrito abajo se implementó en `ProcessItems.cs` y las parciales `.Items.cs`, `.Continuation.cs`, `.PendingCounts.cs`, junto con los productores/consumidores citados.

## 4. Modelo de datos propuesto

Separar explícitamente cuatro datos: carga configurada de catálogo, estimación reservada, conteo declarado y carga efectiva calculada. No guardar el conteo dentro del comentario, del JSON de evidencia o de `GoalUnits/ProgressUnits`.

| Entidad/campo propuesto | Responsabilidad e integridad |
|---|---|
| Ítem compartido + revisiones inmutables | ID estable, nombre, activo, revisión y minutos por unidad. Cambiar tarifa crea revisión; retirar impide selección futura sin borrar referencias históricas. Usar autorización vigente del catálogo para configuración. |
| Regla por etapa publicada | Ítem de salida opcional y destino inmediato por identidad estable; auditoría final explícita. Copiar identidades en nueva versión sin enlazar accidentalmente etapas de otra versión. Publicación verifica destino, secuencia y catálogo vigente. |
| Snapshot por preparación/ejecución | Revisión, nombre y tarifa capturados. La preparación almacena snapshot en configuración persistente, no solo en JSON de asignaciones que podría regenerarse. Arranque directo captura en su transacción. |
| Conteo por intento | Cantidad, autor, fecha, etapa, revisión de ítem y evento. Una entrega definitiva no se edita: se corrige con otro intento. Persistir borrador si el formulario lo admite; no prometer guardado de campos que hoy solo se reenvían tras error. |
| Resultado aplicado a destino | Fuente exacta (intento/conteo), tarifa, cálculo, carga estimada anterior y carga efectiva; procedencia también accesible desde la asignación. Cálculo con aritmética comprobada para evitar desbordamiento. |
| Resolución de capacidad | Decisión, destinatario, actor, revisión y datos de comparación de la agenda; estado explícito pendiente/programada/activa, sin asignaciones ficticias. |
| Excepción puntual de asignación | Permite sobrecarga/solapamiento/descanso, con límites y horario capturados; registrar intervalos afectados. Transferir o crear una nueva corrección no hereda automáticamente el permiso. |
| Omisión y continuidad | Motivo cero, intento origen, ronda y etapas omitidas; vínculo de continuación al trabajo suspendido al devolver, sin alterar evidencia/autoría histórica. |

Las restricciones de BD deben reflejar validez estructural y las reglas de unicidad: una aplicación por transición, una resolución vigente por intento, una auditoría final y referencias dentro de la misma ejecución/versión. Evolucionar restricciones de estados solo en entidades necesarias. Campos nuevos opcionales y catálogo inicialmente vacío: no rellenar ítems ni convertir ejecuciones históricas por inferencia de nombres.

Para demanda superior al límite actual de 4800 min, conservar el conteo y resultado completo en un registro de cálculo/demanda de rango amplio y dejar la etapa pendiente de planificación. No introducir ese número en una asignación que rechace el esquema ni mantener una estimación engañosa como carga efectiva. La implementación debe hacer visible la demanda no programable y comprobar cómo la contabiliza capacidad, sin ampliar límites globales o inventar una solución entre días.

## 5. Recorridos y estados

### Configurar y preparar

1. Abrir «Ítems y tiempos» desde el proceso, avisando que el catálogo es compartido. Listar nombre y minutos/unidad; guardar con control de revisión. Cambios de tarifas muestran alcance futuro.
2. En «Editar detalles», seleccionar un único ítem de salida, mostrar tarea receptora inmediata y permitir que esta tarea tenga simultáneamente carga recibida y conteo de salida distinto.
3. Marcar auditoría final. Bloquear publicación sin destino válido o si se reordena/elimina rompiendo un enlace. No detectar auditoría por texto del título.
4. Preparar/reservar con carga estimada y capturar tarifa vigente. Cambios simultáneos de catálogo invalidan la vista previa si cambia la revisión antes de confirmar. Activación posterior reutiliza snapshot.

### Entregar y resolver capacidad

1. Responsable captura evidencia habitual y cantidad. Servidor valida acceso, intento/revisión, cantidad y evidencia antes de completar.
2. Calcular la carga receptora y una propuesta de agenda con revisión de sus insumos. Mostrar fórmula y, si existe conflicto, causa específica (capacidad, solapamiento, descanso, ausencia, turno, especialidad o ancla crítica).
3. La vista previa del conflicto no completa la entrega. Conservar cantidad/conclusión y archivos ya subidos mediante mecanismos existentes; si un archivo no puede persistirse de forma segura, informar que debe seleccionarse otra vez. No enviar repetidamente bytes sin control ni omitir escaneo.
4. Confirmar opción mediante POST con CSRF, revisión e idempotencia. En transacción y bajo bloqueo existente: revalidar insumos, guardar evidencia/conteo, aplicar carga, resolver agenda y avanzar etapa. Si la propuesta cambió, conflicto comprensible sin aplicación parcial.
5. Buscar menor carga solo entre candidatos compatibles con rol/especialidad y horario normal. Orden propuesto: menor ocupación relativa a capacidad disponible en la ventana relevante, desempate por minutos y código estable; no sustituir la compatibilidad por el número de tareas. Si no hay candidatos, informar y conservar alternativas pertinentes.
6. Guardar pendiente confirma la entrega y mantiene siguiente etapa sin asignación efectiva hasta que exista hueco válido. El temporizador conserva responsable y puede reintentar el camino normal existente; nunca cambia persona ni fuerza. Mostrar que la pendiente puede activarse al existir disponibilidad normal.
7. Forzar se permite solo con origen autorizado y confirmación explícita. Validar límite completo del intervalo calculado con margen: no recortar duración para hacerla caber. Si excede salida de turno o incumple otros límites, conservar opción de pendiente con causa exacta.

El uso de descansos debe limitarse a huecos internos identificables entre intervalos del día efectivo; no usar día libre, horario desconocido, ausencia o tramo antes/después de turno como si fueran descanso. Validar horarios partidos y excepciones para no inventar presencia. Mantener calendario/capacidad normales para el resto de tareas y advertir sobrecarga real; la excepción no crea capacidad general adicional.

### Cero y auditoría

Guardar entrega y conteo cero, omitir etapas estrictamente intermedias al destino final, liberar sus reservas y habilitar auditoría con dependencia explícita del evento de cero. Auditoría conserva estimación/carga propia y sigue controles de agenda; si no cabe, usa el mismo mecanismo de resolución autorizado por origen. Si la siguiente ya es auditoría, no omitirla ni darle carga cero.

En primera versión, configurar conteo de salida solo donde exista siguiente etapa; auditoría final no produce conteo de salida. Propuesta para devolución desde auditoría: reabrir destino configurado, conservar salto anterior en historial y reevaluar recorrido según conteo corregido. Probar transiciones sin asumir intentos entregados en etapas omitidas: el código actual de revisión de etapa anterior debe reconocer la entrega efectiva que originó el salto.

### Devolver y continuar

Suspender trabajo del receptor, conservar su material y autoría, y reabrir conteo anterior según destino permitido. Al volver, adoptar carga total corregida y reprogramar respetando la decisión vigente de capacidad. Usar vínculo de continuidad entre intentos para acceso al trabajo previo, sin sobrescribir entregas ni duplicar carga activa. Diferenciar material previo de nueva evidencia definitiva; conservar validación técnica y límites de archivos. Ni retorno, timer, transferencia ni actualización deben reutilizar automáticamente un permiso antiguo de forzar para otra asignación.

## 6. Fases ejecutables y criterios de salida

| Fase | Trabajo y dependencias | Criterio para cerrar |
|---|---|---|
| F0 — Base de trabajo | Registrar cambios previos, fuentes y contratos; antes de escrituras identificar instancia/BD efectiva y servicios. Revisar productores de reportes, copias y temporalidad afectados. | Mapa de cambios delimitado y base aislada identificada; ninguna escritura en simulación por error. |
| F1 — Esquema y reglas | Depende F0. Entidades de ítem/revisión, configuración, snapshot, conteo, resultado, omisión/continuidad y excepción. Migración aditiva y validador central de fórmula/autorización. | Migración sobre BD vacía y con historial conserva datos; reglas negativas y fórmula verificadas. |
| F2 — Catálogo/editor | Depende F1. CRUD compartido según permisos actuales, retiro lógico, selector, auditoría, copiar/reordenar/guardar/publicar. | Formulario real guarda y reabre; no publica enlaces rotos; revisión concurrente protegida. |
| F3 — Preparación y reservas | Depende F1/F2. Captura única de tarifa, estimación, arranque directo y preparado, asignación automática/aleatoria, vistas de plan. | Cambiar tarifa no altera reservas preparadas; nueva preparación usa nueva tarifa; sin doble reserva. |
| F4 — Entrega/cálculo/resolución | Depende F3. Ambos handlers de Mi trabajo; vista previa y confirmación; selección de receptor, pendiente, forzar con descanso. Integración con timer y agenda. | Recorrido 20 × 3 = 60 desde formulario hasta BD/asignación; tres opciones y límites verificados. |
| F5 — Cero y continuidad | Depende F4. Omisión, liberar reservas, auditoría, revisión de origen efectivo, devolución y reanudación conservando material. | Cero llega a auditoría sin entregas ficticias; corrección 20→12 conserva trabajo y asigna 36 min. |
| F6 — Consumidores y compatibilidad | Depende F4/F5. Actualizar asignaciones, supervisión individual/masiva pertinente, transferencias, arrastres, tablero, reportes/exportaciones, detalle/historial. | Carga/procedencia coherentes en consultas; copias preservan datos y no propagan excepciones indebidamente. |
| F7 — Integración y presentación | Depende F2..F6. Regresiones aisladas, HTTP, navegador real, accesibilidad y revisión del diff. | Matriz con evidencia; fallos críticos resueltos; limitaciones informadas. |
| F8 — Disponibilidad operativa | Solo cuando la implementación incluya dejarla disponible. Identificar destino, migración y ensamblado; actualizar sin borrar simulación ni alterar ejecuciones ajenas. | Aplicación carga versión correcta y recorrido nuevo comprobado en entorno autorizado, con registros identificados. |

La implementación seguirá estas dependencias, sin dar por terminadas rutas que no se hayan probado. Este plan no ordena commit, push, despliegue ni aplicación de migraciones en la simulación compartida.

## 7. Matriz de validación

Entornos: **A** = BD aislada `localhost/sgol_task_checks_*`; **H** = host de aplicación real contra A y formularios HTTP; **N** = navegador contra H. Usar copia aislada de simulación solo si se necesita reproducir su agenda. Estados actualizados con ejecución real el 29/10/2026. «Parcial» enumera la parte no cubierta y no significa prueba aprobada de toda la fila. Los valores concretos del fixture pueden diferir del ejemplo, preservando la regla. Evidencias locales en `artifacts/process-items/`, excluidas del control de versiones.

| ID / precondición y acción | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| V01 Catálogo vacío: crear Fotografías=3 y Cajas=2; guardar/reabrir | Ítems compartidos, revisión correcta, sin cambiar tareas existentes | Servicio + formulario H/A | Aprobado: `core-final.log`; catálogo HTTP, guardado, lectura y revisiones. |
| V02 Editar tarifa simultáneamente o retirar ítem usado | Conflicto por revisión; referencias históricas legibles; selección futura controlada | HTTP concurrente H/A | Aprobado: `core-final.log`, `consumers-final-tests.log`; revisión obsoleta, retiro, selección nueva rechazada, proceso previo reutilizable. |
| V03 Configurar salida, auditoría, copiar versión, reordenar/eliminar | Persistencia por identidad; publicación bloquea enlaces/destino inválidos | Servicio + HTTP H/A | Parcial: `core-final.log`, `consumers-final-tests.log`; reglas, versiones y destino estable aprobados. Diálogo real probado; eliminar/reordenar todas las combinaciones no se recorrió en navegador. |
| V04 Preparar con tarifa 3; cambiar a 4; activar y preparar otro proceso | Plan preparado conserva 3; nueva preparación usa 4 sin republicar | Servicio/BD A | Aprobado: `core-final.log`, `consumers-final-tests.log`; captura 3→4 y catálogo compartido 7→8 en dos procesos. |
| V05 Nueva ejecución sin ítems y ejecución antigua tras migrar | Mismo recorrido/agenda/evidencia; sin adopción retroactiva | Snapshot + integración A/H | Aprobado: `upgrade.log`, `common-before.txt`/`common-after.txt`, `legacy-tests.log`, `legacy-replay-final.log`; columnas previas e idempotencia histórica conservadas. |
| V06 Estimación 15; entregar evidencia y 20 con tarifa 3 | Siguiente carga 60, origen mantiene su carga; margen capturado se aplica al cierre | HTTP H/A | Aprobado: `core-final.log`, `pending-overnight-tests.log`; 20×3=60 reemplaza estimación 1; carga propia y margen conservados. |
| V07 B recibe cajas grandes e informa cajas pequeñas para C | Dos cálculos con unidades/fuentes distintas; un conteo por tarea | Recorrido completo H/A | Aprobado: `consumers-final-tests.log`; fotografías×5 recibidas y etiquetas×7 producidas, con tarifas congeladas. |
| V08 Vacío, negativo, fracción, producto desbordado o superior a 4800 | Vacío/negativo/fracción rechazados; exceso no trunca ni crea asignación inválida, demanda pendiente visible | Reglas + HTTP/BD A/H | Aprobado: `core-final.log`, `pending-overnight-tests.log`; entero, validación negativa y demanda 15032385529 sin truncar. |
| V09 Destinatario lleno: buscar menor carga con origen SC/Dirección | Solo candidatos compatibles, orden estable; carga/reservas no duplicadas | Agenda/servicio A/H | Aprobado: `pending-overnight-tests.log`, `consumers-final-tests.log`; selección compatible y revalidación de agenda. |
| V10 Ningún candidato compatible | Causa y opciones restantes; no reintento infinito ni cambio de rol | HTTP H/A | Aprobado: `consumers-final-tests.log`; sin candidato mantiene conflicto y alternativas permitidas. |
| V11 Origen distinto o payload falsea rol/actor/destino/tarifa | Rechazo de reasignar/forzar en servidor; sin cambios parciales | HTTP/roles/CSRF H/A | Aprobado: `pending-overnight-tests.log`, `consumers-final-tests.log`; origen no permitido/actor ajeno, revisión, CSRF y payload manipulados rechazados. |
| V12 Guardar pendiente y ejecutar Tick varias veces | Entrega guardada; intento único; solo activación normal con responsable y capacidad válidos | Servicio/tiempo controlado A | Aprobado: `pending-overnight-tests.log`; demanda pendiente íntegra; timer no cambia persona, divide ni fuerza. |
| V13 Forzar tarea de 60 min que se solapa con descanso y trabajo | Asignación autorizada, cálculo/margen íntegros, aviso e intervalos auditados | HTTP + agenda H/A/N | Aprobado: `pending-overnight-tests.log` verifica descanso y solapamiento. Navegador real confirma fuerza, 80 min íntegros y cierre con margen 12:00–14:00; `browser-checks.md`. |
| V14 Forzar comienza dentro del descanso | Permitido si es descanso interno verificable y respeta turno/límites | Servicio + HTTP A/H | Aprobado: `pending-overnight-tests.log`, `boundary-final.log`; origen SC puede iniciar dentro del descanso interno. |
| V15 Sin forzar: descanso; o forzar en ausencia/día libre/horario faltante/fuera de turno | Camino normal respeta descanso; excepción no autoriza ausencia ni inventa turno | Agenda + HTTP A/H | Aprobado: `boundary-final.log`, `pending-overnight-tests.log`; empleo, rol, especialidad, ausencia, horario y turno se mantienen. |
| V16 Forzar 600 min con turno restante menor o cierre crítico incompatible | Pendiente con causa precisa; no ampliar turno ni recortar duración/ancla | Agenda A/H | Aprobado: `boundary-final.log`, `pending-overnight-tests.log`; duración completa y dependencia crítica bloquean fuerza, sin entrega parcial. |
| V17 Conteo 0 en primera/intermedia o inmediatamente antes de auditoría | Auditoría recibe evento efectivo, carga propia; omisiones/reservas coherentes | HTTP/BD H/A | Aprobado: `core-final.log`, `pending-overnight-tests.log`, `consumers-final-tests.log`; cero inicial/intermedio/antes de auditoría y reservas liberadas. |
| V18 Auditoría después de salto aprueba o devuelve | Revisión apunta al productor efectivo; no busca entrega inexistente; historial del salto conservado | Integración H/A | Aprobado: `core-final.log`, `pending-overnight-tests.log`; aprobación/devolución desde salto usa productor efectivo. |
| V19 Receptor con trabajo previo devuelve; conteo 20→12 | Continuidad accesible, carga 36 completa, sin descuento de 5 ni doble tarea activa | HTTP/evidencia/BD H/A | Aprobado: `core-final.log` (12×3=36), `consumers-final-tests.log` (JPEG real, nueva sesión, escaneo), navegador móvil con fotografía conservada. |
| V20 Corrección 0→positivo o positivo→0 tras trabajo previo | Reactivar/omitir recorrido correcto, conservar trabajo y rondas anteriores | Integración H/A | Aprobado: `pending-overnight-tests.log`, `consumers-final-tests.log`; rondas y omisiones históricas, restauración de carga propia de auditoría. |
| V21 Transferir/supervisar/arrastrar tras cálculo o forzado | Mantener procedencia/carga; permisos puntuales no se heredan a nueva asignación | Servicios reales A/H | Parcial: `pending-overnight-tests.log` prueba transferencia/reasignación reales y no herencia de excepción. Procedencia incluida en copia de arrastre por inspección; arrastre posterior a conteo no ejecutado. |
| V22 Cambiar carga de catálogo + tarifa + evidencia; actualizar asignaciones con pendientes/activas/vencidas | Cálculo capturado no se restablece a carga fija; no actualizar histórico por accidente | Comparación antes/después A | Parcial: `consumers-final-tests.log` comprueba preview de actualización con cálculo 21 frente a catálogo 99; combinación completa de aplicar actualización en pendientes/activas/vencidas no ejecutada. |
| V23 Reintento mismo envío y dos envíos simultáneos; repetir clave con otra cantidad/opción | Una transición y evidencia; diferente payload produce conflicto | HTTP concurrente/BD H/A | Aprobado: `core-final.log`, `pending-overnight-tests.log`, `consumers-final-tests.log`; claves, cantidades distintas y dos solicitudes HTTP concurrentes. |
| V24 Falla escáner/persistencia/agenda o propuesta obsoleta al confirmar | Cantidad/evidencia/etapas/carga atómicas; error explica qué reintentar | Fallos controlados H/A | Aprobado: `consumers-final-tests.log`, `pending-overnight-tests.log`; escáner controlado rechaza y revierte copia/conteo/transición; revisión de agenda obsoleta y ancla crítica atómicas. |
| V25 Consultar Mi trabajo, capacidad, tablero, plan, historial y reportes/exportación aplicables | Carga efectiva consistente, estimación diferenciada, omitidas no cuentan como realizadas | Lecturas/archivos H/A | Parcial: capacidad/plan/historial y Mi trabajo aprobados en servicios y navegador; exportación HTTP en `consumers-final-tests.log`. Tablero y exportaciones PDF/XLSX no recorridos visualmente con ítems. |
| V26 Formulario desde ambas rutas, recarga/error, teclado, foco y textos largos | Conserva campos admitidos, comunica archivos a reseleccionar; sin bypass entre handlers | HTTP y navegador H/N | Aprobado: `core-final.log`, `consumers-final-tests.log`; ambos handlers reales. Navegador: error de capacidad conserva campos/reutilización, motivo requerido, corrección y éxito; cámara verificada conserva IDs ante 409. |
| V27 Diálogos en 320 px/escritorio, temas, 200% texto y estados vacío/carga/error/éxito | Sin desbordamiento global; etiquetas, foco y cierre/retorno accesibles | Navegador N | Parcial: navegador real 320×740 y 1280×900; temas claro/oscuro/especial, contenido largo, foco, Tab/Shift+Tab/Escape, error/éxito sin desbordamiento. Zoom/texto 200% pendiente: navegador integrado no expone control de zoom y atajo no cambió escala. |
| V28 Flujos PDF legados, evidencia mixta, checklist, planes aleatorios y borrado de plan | Sin regresión; historial y reservas conservados donde corresponde | Herramientas existentes ampliadas A/H | Parcial: `legacy-tests.log` (PDF, evidencia mixta, checklist), `auto-tests.log` (aleatorio/menor carga), `consumers-final-tests.log` (borrado auditado con continuidad). No se repitieron suites ajenas sin riesgo nuevo. |
| V29 Conteo enorme pendiente: devolver sin asignación como propietario reservado o Dirección | Motivo obligatorio; tercero/revisión obsoleta rechazados; un retorno ante concurrencia, reserva inválida liberada | Servicio + HTTP + navegador H/A/N | Aprobado: `pending-overnight-tests.log`; navegador 320 px, devolución y corrección 3×7=21 conservando entrada 60 y JPEG. |
| V30 Devolución al día siguiente con fotos de sesión vencida | Nueva ronda flexible dentro del turno; fotografía y autoría conservadas antes/después de limpieza; sesión nueva validada | Servicios reales + HTTP A/H | Aprobado: `consumers-final-tests.log`, `pending-overnight-tests.log`; sin cambiar fechas de intentos originales. |

Se amplió `ProcessEditorChecks` y se añadieron `ProcessItemChecks`, `ProcessItemIntegrityChecks`, `ProcessItemBoundaryChecks` y `ProcessItemLegacyReplayChecks`. Las opciones implementadas están en `tools/Sgol.TaskChecks/Program.cs`; véase §9 para comandos y fixtures. La herramienta exige BD local dedicada y ejecuta migraciones: no apuntarla a la simulación compartida.

Se compilaron proyectos afectados y se comprobó modelo/migración; véase §9. Guardar evidencia sin secretos en `artifacts/`/`.local/` y registrar en la matriz comandos/resultados concretos. Si una suite se detiene por `PLAN_CAPACITY_STALE` u otro conflicto previo, los casos posteriores siguen no ejecutados.

## 8. Riesgos y decisiones de diseño que no deben ocultarse

- Permitir descanso reduce descanso efectivo. Mostrar la franja y registrar quién decidió; no convertirlo en disponibilidad general. Horarios partidos necesitan una prueba que distinga hueco interno de ausencia real.
- La carga corregida completa puede exceder esfuerzo restante. Es una decisión del usuario: no optimizarla descontando fotos hechas ni creando progreso automático.
- Catálogo compartido cambia el tiempo futuro de varios procesos; mostrar alcance y capturar revisiones impide alterar historia. Retiro/nombre también necesitan referencias legibles.
- Saltar etapas afecta revisiones, intentos, reservas y métricas. Un estado omitido no significa aprobación ni finalización del proceso completo.
- Reanudar no puede limitarse a dejar un enlace histórico si el trabajo deja de ser utilizable. Verificar material real del receptor; no reabrir sesiones de cámara vencidas ni mover archivos ocultando autoría.
- Demanda excesiva y conflicto con ancla crítica siguen pendientes de planificación. No resolverlos con un bypass de BD, un cierre recortado o una ampliación de turno.
- Reglas de diseño aplicadas: auditoría sin conteo de salida; devolución desde auditoría usa destino configurado y conserva salto. El retiro y los dos bordes consultados se rigen por R15..R17, decisiones explícitas del usuario. Si la implementación descubre que estas reglas cambian responsables/plazos o impiden un recorrido requerido, presentar el caso concreto antes de sustituirlas.

## 9. Cierre y estado real

Se considerará implementado cuando R01..R17 estén cubiertas, las rutas de ambas pantallas funcionen con formularios/servicios reales, los escenarios críticos de la matriz tengan evidencia aprobada y el diff preserve cambios previos. Una prueba crítica pendiente obliga a informar «implementado, con validación pendiente» y su causa; no equiparar build con integración.

Implementado en código. F0..F6 ejecutadas con pruebas focales; F7 ejecutada con las limitaciones explícitas de las filas parciales. F8 no ejecutada: no se autorizó despliegue ni migración compartida. No se hizo commit ni push.

Cambios: catálogo compartido con revisiones inmutables; configuración por identidad y auditoría final; tarifas congeladas al preparar; cálculo transaccional; alternativas de capacidad según origen; excepción puntual con descanso; cero/omisiones; devolución/continuidad de trabajo; devolución de pendientes y reprogramación de la nueva ronda. Actualización/capacidad/copias conservan procedencia. Limpieza de cámara preserva trabajo de nuevas ejecuciones con ítems sin reabrir sesiones vencidas.

Persistencia: migraciones aditivas `20261029192538_AddProcessItems` y `20261029193317_AddProcessItemIntegrity`. Aplicadas solo en BD dedicadas. Actualización de copia histórica: 11 asignaciones, 9 etapas, 11 intentos, 2 ejecuciones, 25 eventos y 2 preparaciones conservados; comparación de TODAS las columnas preexistentes idéntica. Comparación inicial por filas completas detectó columnas nuevas de migraciones anteriores pendientes, no corrupción: se investigó y se compararon las columnas comunes. `model-final-check.log` confirma modelo sin cambios pendientes.

Validación ejecutada con `dotnet build tools/Sgol.TaskChecks --artifacts-path artifacts/process-items/...` y DLL resultante. Opciones: `--process-items-only`, `--process-items-more`, `--process-items-integrity`, `--process-items-rules`, `--process-items-legacy-replay`, `--process-editor-only`, `--process-plan-auto-only`. Las opciones que crean fixtures requieren BD vacía o copia base según su suite. `--process-items-more` e `--process-items-integrity` parten de copia del fixture core; no mezclar bases con demanda enorme de otra suite. La herramienta rechaza destinos distintos de localhost/`sgol_task_checks_*`. `--process-items-ui-current` abre únicamente el host del fixture existente.

Destino efectivo identificado: PostgreSQL local puerto 5440, contenedor `sgol-local-postgres-1`. La simulación compartida `sgol_local` no recibió migraciones ni escrituras de prueba. Se crearon/conservaron bases `sgol_task_checks_process_items_*` y `sgol_task_checks_items_*`, con catálogos/personas/planes/ejecuciones/evidencia exclusivamente de prueba; también copias aisladas de fixtures históricos. No se borraron bases ni se reiniciaron servicios compartidos. Solo se inició/detuvo el host HTTP de pruebas (7258/7259). El diff previo está guardado en `artifacts/process-items/preexisting.patch`; cambios locales de otros encargos se conservaron.

Fallos investigados y corregidos: replay anterior a checklist tenía hash sin propiedad nula; compatibilidad acotada verificada sin aceptar payload cambiado. Agenda flexible conservaba fecha vieja tras devolución: R17 y regresión al día siguiente. Trabajo de cámara vencido podía desaparecer antes de devolver: se conserva en procesos nuevos con ítems, tokens siguen venciendo. Navegador descubrió foco que salía del diálogo: se corrigió trampa de Tab/Shift+Tab. La comprobación final de fuerza descubrió alerta con hijos en columnas estrechas; se corrigió con `alerta__contenido` existente y se verificó escritorio/móvil y confirmación exitosa. La primera prueba nueva del export falló por esperar «21» donde el contrato existente devuelve «21 min»; se contrastó `ReportCatalog.Cell`, se corrigió esa expectativa de formato y se conservó el log del fallo, sin cambiar datos ni validación de producto para ocultarlo.

Límites de evidencia: HTTP usa PageModels, servicios, transacciones PostgreSQL, validación técnica de archivos y JPEG reales; sesión de usuario y escáner son adaptadores controlados de prueba. No verifica login real, LAN, cámara física ni disponibilidad del escáner instalado. La matriz declara verificaciones de navegador/exportaciones/arrastre/actualización aún parciales. No presentar estas partes como aprobadas. Compilaciones Debug aislada y Release sin apphost: 0 errores y 0 advertencias. JavaScript y diff comprobados según evidencia de cierre.

### Autorización operativa posterior

El 29/10/2026 el usuario autorizó expresamente reiniciar la simulación, commit, push y despliegue. Se identificó el destino compartido `sgol_local`, puerto5440, web interna7241 y proxy local7240/LAN7440. La publicación se prepara a partir del índice de Git, excluyendo y conservando en el checkout los cambios previos de otros encargos. Compilación Release del árbol seleccionado y recorrido core con BD aislada `sgol_task_checks_items_release_core`: aprobados (`staged-tree-build.log`, `release-core-tests.log`). F8 en curso; los resultados efectivos se registrarán tras migración y comprobación.
