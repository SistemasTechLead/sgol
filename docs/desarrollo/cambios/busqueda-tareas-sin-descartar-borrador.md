# Buscar tareas sin descartar el editor de procesos

29/10/2026. Cambio mixto: interacción y envío del formulario, sin cambio de modelo o regla de negocio.

El formulario GET del buscador recargaba todo el editor. `process-rules.js` clasificaba ese envío como salida con cambios pendientes y pedía confirmación de descarte. No bastaba con suprimir el aviso: la recarga habría perdido las ediciones.

Se mantiene el GET real de `/tareas/procesos?id=...&Search=...`, su autorización y filtrado existente; `process-search.js` consulta y reemplaza solamente `data-process-search-results`. No guarda el borrador, cambia etapas, navega ni escribe en la base. Conserva foco, campos, diálogo abierto y estado de cambios. Muestra carga, vacío, error recuperable y éxito; cancela solicitudes anteriores para que no reemplacen resultados nuevos. No inserta HTML de rutas de acceso ni errores. El control de otros formularios se delega en la página para incluir los botones Añadir recién cargados. Añadir sigue solicitando confirmación si existen cambios locales.

Se reutilizaron el diálogo, campo, búsqueda, botones, tokens y patrón móvil existentes, según protocolo y referencia visual. No se añadió biblioteca ni estilos nuevos.

| Escenario | Resultado observado | Entorno / evidencia | Estado |
|---|---|---|---|
| Buscar nombre sin acento y código en minúsculas | Solo candidato correspondiente; sin modificar proceso/revisión/asignaciones/reservas | Razor HTTP real, PostgreSQL `sgol_task_checks_process_search_20261029`; `artifacts/process-items/search-tests.log` | Aprobado |
| Cero coincidencias | Mensaje explícito, resultados anteriores retirados | HTTP y navegador real | Aprobado |
| Instrucciones modificadas y búsqueda por botón | Sin confirmación ni navegación; instrucciones, guardar habilitado, diálogo y foco conservados | Navegador contra host real7258, escritorio | Aprobado |
| Añadir tras búsqueda con cambios pendientes | Continúa la confirmación de descarte; Cancelar conserva 4 etapas y texto local | Navegador, aviso nativo cancelado con Escape | Aprobado |
| Móvil320×740 y Enter | Búsqueda completa; foco vuelve a Search, sin desbordamiento (documento305px, diálogo270.8px) | Navegador real, patrón móvil desplegado | Aprobado |
| Servicio de prueba caído y reintento | Mensaje comprensible, resultados/texto conservados; al restaurar host muestra candidato y recupera foco | Se detuvo/reinició únicamente host aislado; captura observada en herramienta | Aprobado |
| Guardar después de buscar y después del reintento | Texto persiste; borrador guardado | Formulario real y navegador | Aprobado |
| Compilación y sintaxis JS | Build y `node --check` | `search-build.log`, ambos scripts | Aprobado |

Datos: dos tareas candidatas inventadas y ediciones de un borrador exclusivamente en la base aislada indicada. No se alteró ningún proceso del usuario ni se recargó su pestaña con cambios pendientes. La primera compilación detectó una referencia errónea a Id en el fixture; se corrigió al identificador real VersionId/Position. No se ajustaron reglas de producto para ocultarlo.

Publicación: en preparación desde el árbol confirmado de Git, preservando los cambios locales ajenos. No requiere migración. Recorrido autenticado en simulación compartida no ejecutado; se verificará disponibilidad y coincidencia de recursos publicados. El usuario debe guardar los cambios de una pestaña anterior antes de recargarla para cargar JavaScript nuevo.
