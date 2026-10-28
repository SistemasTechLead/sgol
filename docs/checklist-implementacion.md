# Requisito Checklist

El requisito `CHECKLIST` está disponible al crear o editar tareas y al crear tareas ocasionales. Es una alternativa a los demás requisitos de evidencia. Cada punto tiene identidad propia, texto y orden; no hay un máximo funcional de puntos. Cada texto admite 1,000 caracteres. La fila vacía sombreada permite agregar el siguiente punto y no se guarda. El editor permite eliminar y reordenar puntos.

## Asignación, borrador y entrega

La definición se captura en cada asignación, en las reservas de planificación y en las etapas de procesos. Mi trabajo muestra las casillas dentro del panel habitual de evidencia, con contador y guardado de borrador. El borrador se guarda en el servidor por asignación con autor y fecha, sin convertirlo en entrega. El formulario transmite un solo campo JSON para evitar el límite de elementos del enlazador de formularios.

Para entregar, el servidor exige la versión vigente y todos los identificadores esperados, sin duplicados ni puntos ajenos. No basta con habilitar el botón. Las tareas por progreso también deben alcanzar su meta. Se conserva la conclusión opcional de las tareas independientes y las declaraciones y conclusiones existentes de los procesos. Las dependencias y revisiones de procesos mantienen su flujo vigente.

La evidencia y el cambio de estado se guardan en la misma transacción. La aprobación individual o masiva de una tarea Checklist exige una entrega completa. Supervisión muestra los puntos y sus marcas como evidencia de lectura. Mi trabajo y los procesos permiten consultar las entregas anteriores.

## Cambios y correcciones

Editar el catálogo no cambia entregas anteriores. Actualizar asignaciones pendientes compara los puntos y muestra altas, bajas, cambios de texto y orden. Los cambios de checklist se consideran metadatos: no requieren reacomodar horarios. Se conservan las marcas de puntos intactos; los nuevos o modificados quedan desmarcados.

Una transferencia conserva el borrador y su autor, junto con el historial de transferencia existente. Una corrección por rechazo crea otra asignación con el requisito vigente y casillas desmarcadas; la entrega original permanece disponible.

Reportes incorpora el filtro Checklist y la columna opcional «Checklist entregado», disponible también en la exportación. Las consultas de planes y sus archivos conservan la definición y las marcas.

## Esquema y verificación

La migración `20261028163124_AddOperationalChecklist` añade definición de checklist a tareas, asignaciones y etapas, y borrador/evidencia a asignaciones. Actualiza las restricciones de requisitos. Los registros existentes comienzan con listas vacías y sin borrador. La reversión se bloquea si existen definiciones o evidencia que se perderían.

La migración se aplicó al entorno local el 28/10/2026, después de crear un respaldo en `.local/backups/sgol-before-checklist-20261028.dump`. Las huellas de los campos anteriores de 284 tareas, 539 asignaciones, 539 entregas y 539 revisiones coincidieron antes y después. No se crearon tareas de prueba en los datos operativos.

Comprobaciones reproducibles:

- `Sgol.TaskChecks --checklist-only`: reglas, autorización, borradores, envío, aprobación, historial, actualización, correcciones, transferencia, ocasión y proceso en una base aislada `sgol_task_checks_*`.
- `Sgol.TaskChecks --process-editor-only`: formularios HTTP reales con evidencia mixta, incluyendo Checklist, devolución, reintento y regresión del proceso documental PDF.
- `node tools/Sgol.TaskChecks/checklist-ui-checks.cjs`: editor, fila vacía, orden, eliminación, 1,500 puntos, guardado de marcas, envío con el JavaScript existente y presentación móvil de 320 px. Requiere Playwright disponible en Node.

Las capturas y registros de comprobación quedan en `artifacts/` y no se suben al repositorio.
