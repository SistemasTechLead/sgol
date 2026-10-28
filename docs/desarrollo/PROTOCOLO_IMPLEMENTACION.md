# Protocolo de investigación, implementación y validación de SGOL

## Propósito y alcance

Convertir una petición del usuario en un cambio que considere sus interacciones reales con el sistema, mantenga la interfaz existente y tenga evidencia de validación. Se aplica a correcciones, funcionalidades nuevas y cambios visuales. Complementa `AGENTS.md`; no reemplaza las instrucciones actuales del usuario.

Ninguna lista garantiza cobertura de todas las combinaciones posibles. El resultado exigible es un análisis trazable de impacto, escenarios relevantes explícitos y pruebas verificables, con incertidumbres y omisiones declaradas.

## 1. Entender la petición y clasificarla

Antes de diseñar la solución, identificar:

- Objetivo observable para el usuario y criterios de aceptación.
- Reglas que cambian y reglas que deben conservarse.
- Actores, pantallas, entidades y estados involucrados.
- Supuestos débiles, consecuencias no mencionadas y decisiones pendientes.

Separar dos dimensiones:

| Dimensión | Tratamiento |
|---|---|
| Solo planificación | Investigar el código y las pruebas existentes; reproducir el problema de forma aislada cuando corresponda. Entregar un plan con escenarios y resultados esperados. No implementar el producto por anticipado. |
| Implementación autorizada | Ejecutar el cambio, las pruebas aplicables y la revisión del resultado; actualizar el plan con evidencia real. |
| Cambio funcional | Revisar contratos, persistencia, reglas, permisos, estados, servicios, datos existentes e integraciones. |
| Cambio de interfaz | Revisar composición, componentes, accesibilidad, interacción, temas y adaptación; comprobar que no cambia contratos funcionales por accidente. |
| Cambio mixto | Aplicar ambas validaciones y probar el recorrido desde la pantalla hasta los datos persistidos. |

Una petición visual puede afectar comportamiento si mueve controles, modifica campos enviados, oculta filtros o altera foco y navegación. Clasificar según el impacto descubierto, no solo según las palabras de la petición.

## 2. Reconocer el entorno antes de ejecutar

### Situación declarada del proyecto

El 28/10/2026 el usuario aclaró que el entorno actual se dedica a desarrollo y pruebas de la operación. Sus datos son simulados y se agregan diariamente para probar variables y pulir el sistema antes de producción. No deben describirse como datos de producción real.

La simulación compartida puede contener casos de prueba en curso. Su valor es permitir reproducir y comparar comportamientos. Evitar interferencias no implica prohibir pruebas operativas autorizadas ni pedir confirmaciones rutinarias.

### Identificación efectiva

Antes de escrituras o migraciones, comprobar lo necesario para el cambio:

- Rama, estado del repositorio y modificaciones previas.
- Aplicación y proceso que sirven la pantalla; ensamblado o compilación realmente cargada.
- Dirección y modo de acceso: local, LAN, proxy y origen de autenticación.
- Servidor y nombre de base de datos, migraciones aplicadas y destino de cualquier herramienta.
- Dependencias necesarias: PostgreSQL, almacenamiento, escáner, certificados y servicios auxiliares.
- Fecha y zona operativa; SGOL utiliza `America/Mexico_City` para la operación.

No copiar secretos en planes, salidas, pruebas guardadas ni documentación. Una IP o un proceso anotado anteriormente no demuestra su estado actual.

Referencias iniciales: [instalación local](../instalacion-local.md), [acceso LAN](../acceso-red.md), [normalización de base de datos](../normalizacion-bd.md), `scripts/local-env.ps1`, `scripts/red-env.ps1` y `compose.local.yaml`. Consultar su contenido vigente sin ejecutar iniciadores o reinicios innecesarios.

### Elegir dónde probar

| Entorno | Uso |
|---|---|
| Prueba aislada | Casos repetibles, datos inventados, pruebas negativas, concurrencia, borrados, reinicios y regresiones automatizadas. Usar bases identificadas expresamente como pruebas; las herramientas actuales de tareas exigen `sgol_task_checks_*` en localhost. |
| Copia aislada de la simulación | Reproducir problemas dependientes de la agenda, historial o combinación de datos actuales. Verificar que la conexión apunte a la copia antes de escribir. |
| Simulación operativa compartida | Verificación de operación solicitada o incluida en el alcance. Identificar qué registros se crearán o modificarán y conservar el contexto necesario de las pruebas diarias. No usarla indiscriminadamente como base de una suite automatizada. |
| Producción futura | Cuando exista, verificar su identificación y aplicar las condiciones de despliegue y protección acordadas entonces. No asumir que una dirección actual representa esa producción. |

Los datos simulados se prevén eliminar al salir a producción. La limpieza debe tener un alcance concreto: qué datos se eliminan, qué catálogo y configuración se conservan o inicializan, qué archivos y credenciales se gestionan y cómo se verifica un arranque limpio. No ejecutar esa limpieza como consecuencia automática de estas instrucciones.

## 3. Investigar el impacto siguiendo los datos

Comenzar por búsquedas dirigidas con `rg` y lecturas del código propietario. No limitarse al nombre visible del control: buscar códigos internos, propiedades, tablas, contratos y constructores relacionados.

Seguir cada entidad o campo modificado a través de:

1. Creación, edición, importación y validación del catálogo.
2. Almacenamiento, migraciones, valores predeterminados y compatibilidad de registros existentes.
3. Copias de definición: asignaciones, snapshots, etapas, reservas y correcciones.
4. Generación de planes, asignación directa y modalidades de programación aplicables.
5. Actualización de asignaciones y reacomodo de tareas vecinas.
6. Ejecución, borradores, evidencia, progreso y entrega.
7. Revisión individual o masiva, devoluciones, transferencias y nuevas correcciones.
8. Consultas, filtros, etiquetas, tablero, reportes, exportaciones y archivos históricos.
9. Autorización, auditoría, reintentos, concurrencia y procesos automáticos que interactúen con el cambio.

Registrar los archivos, funciones y rutas concretas encontrados. Una coincidencia de texto es una pista, no prueba de cobertura. Confirmar dónde se escribe el dato, dónde se copia y qué regla lo valida en cada ruta.

En SGOL, revisar los módulos y contratos pertinentes de `src/Modules/`, los servicios de `src/Sgol.Web/Infrastructure/Persistence/`, las rutas de `src/Sgol.Web/Interface/` y `src/Sgol.Web/Pages/`, y las herramientas de comprobación existentes en `tools/`. No tratar documentos antiguos como prueba de la implementación actual.

## 4. Construir el plan y la matriz de escenarios

Para cambios con varias rutas, persistencia, reglas operativas o interfaz relevante, guardar un documento de trabajo en `docs/desarrollo/cambios/<nombre-del-cambio>.md`. Una corrección pequeña puede documentarse en la respuesta si conserva la misma trazabilidad sin crear burocracia.

El plan debe contener objetivo, alcance, comportamiento actual, impacto descubierto, invariantes, decisiones pendientes, alternativas con consecuencias, implementación propuesta, validación y riesgos. No presentar como segura una solución que depende de una decisión de negocio aún no resuelta.

Usar esta matriz como punto de partida, adaptándola al impacto:

| Recorrido | Escenarios a considerar |
|---|---|
| Crear y editar | Registro nuevo; existente; guardar y reabrir; cambiar un parámetro y varios relacionados; quitar o sustituir el requisito. |
| Asignar y planificar | Asignación directa; plan nuevo; copia de parámetros; periodos y modalidades aplicables; reserva y activación de procesos. |
| Actualizar existentes | Programadas, activas, vencidas de hoy y de días anteriores; con o sin avance, evidencia, checklist marcado, transferencia o arrastre. |
| Ejecutar | Estado vacío y parcial; borrador y recarga; entrega incompleta y completa; errores y reintento. |
| Supervisar | Aprobación, rechazo, revisión masiva, devolución, corrección y transferencia. |
| Historial | Entregadas, pendientes de revisión, aprobadas, no satisfechas, sustituidas y retiradas; conservación de autoría y evidencia. |
| Consultar | Listados, filtros, detalle, tablero, reportes y exportaciones realmente consumidores del dato. |
| Integridad | Permisos por rol, acceso ajeno, envío manipulado, cambio simultáneo, propuesta vencida, reintento y rollback de una operación fallida. |
| Presentación | Escritorio y móvil, temas aplicables, teclado, foco, listas o textos largos, estados vacíos y errores comprensibles. |
| Entorno | Servicios disponibles o caídos, migración requerida, caché, versión de la aplicación y diferencias de acceso local/LAN relevantes. |

Cada escenario seleccionado debe indicar: precondiciones, acción, resultado esperado, entorno, forma de prueba y estado con evidencia. Los estados son **propuesto**, **aprobado**, **fallido**, **no ejecutado** o **no aplicable**. Justificar los dos últimos.

Cubrir combinaciones dirigidas por riesgo. Por ejemplo, Checklist requiere evaluar no solo su editor y entrega, sino también cambiar requisito más carga sobre una tarea crítica con asignaciones vencidas y futuras. Comprobar qué debe pasar con el horario, la carga histórica, los vecinos y el flujo de revisión. No intentar todas las combinaciones indiscriminadamente ni omitir las combinaciones críticas porque cada campo se haya probado por separado.

Preguntar cuando las fuentes y la petición no resuelvan una consecuencia de negocio, explicando opciones y efecto observable. No convertir decisiones técnicas rutinarias en solicitudes de aprobación. Las autorizaciones ya dadas siguen vigentes dentro de su alcance.

## 5. Implementar con coherencia funcional y visual

### Funcionalidad

- Centralizar reglas compartidas cuando varias rutas deban cumplirlas; verificar cada integración que las consume.
- Validar en el servidor. Deshabilitar un botón no es una garantía de integridad.
- Definir explícitamente qué adoptan los registros nuevos, pendientes y ya entregados; conservar sus invariantes y snapshots acordados.
- Evitar que la corrección de un caso especial flexibilice involuntariamente las reglas de otros estados, modalidades o roles.
- Los mensajes deben indicar qué ocurrió, a qué tarea o registro corresponde y qué puede hacer el usuario. Si la causa no se conoce, declarar esa incertidumbre en lugar de atribuirla a una regla no comprobada.

### Interfaz

Antes de crear una interfaz, inspeccionar una pantalla equivalente en el código actual y, cuando corresponda, su resultado renderizado. Identificar el componente o patrón que se reutilizará y justificar cualquier diferencia necesaria.

Consultar [ESTILO_VISUAL_V2.md](../design/ESTILO_VISUAL_V2.md) y las precisiones aplicables. Usar los tokens y componentes vigentes de `wwwroot/css/`, `wwwroot/js/` y `Pages/Shared/`. Mantener identidad, tipografía, colores, espaciado, controles, navegación, diálogos, validaciones y adaptación existentes. No añadir una biblioteca o un estilo paralelo sin necesidad demostrada.

La documentación visual contiene decisiones de distintas fechas. Contrastar su alcance con el código y las instrucciones actuales; no reintroducir límites funcionales antiguos solo porque aparecen en una referencia de diseño.

Verificar el contenido completo, tamaños pequeños pertinentes —incluidos 320 px cuando aplique—, escritorio, temas usados, foco, teclado, etiquetas accesibles, textos largos y estados de carga, bloqueo, error y éxito. No sustituir una prueba de la pantalla real por una maqueta que omita estilos, autenticación o validaciones relevantes.

## 6. Ejecutar pruebas y registrar evidencia

Elegir las comprobaciones que respondan al riesgo identificado:

- Pruebas de reglas para validación y casos límite.
- Integración con base aislada para persistencia, transacciones, historial y generación de trabajo.
- Formularios HTTP reales para binding, CSRF, autorización, errores y payloads enviados por las páginas.
- Navegador para interacción y presentación; identificar si se probó la aplicación real o un componente aislado.
- Regresiones pertinentes de los flujos que comparten la lógica modificada.
- Compilación de los proyectos afectados y comprobación de migración/modelo cuando cambie el esquema.

Reutilizar las herramientas existentes de `tools/` y revisar sus requisitos antes de ejecutarlas. Consultar opciones, conexiones y fixtures en su código; no inventar comandos ni suponer que una suite amplia llega al escenario de interés. Una suite que se detiene antes por un conflicto de capacidad no valida los pasos posteriores.

Comparar datos antes y después cuando se prometa conservación de historial, cargas, fechas, responsables, vecinos o evidencia. Probar también la ausencia de efectos secundarios y que una operación rechazada no deje cambios parciales.

Añadir una regresión significativa para cada fallo corregido. No multiplicar pruebas que solo repitan la implementación ni ejecutar suites ajenas a un cambio documental. Una vez que las verificaciones pertinentes pasen, ampliar o repetir solo ante nuevos cambios, fallos o dudas concretas.

Guardar comandos, resultados y capturas en el lugar acordado, sin secretos ni datos privados. `artifacts/` y `.local/` son destinos locales ignorados; los tests mantenibles y la documentación del comportamiento pertenecen al repositorio.

## 7. Criterio de finalización

Antes de dar el trabajo por validado:

1. Revisar el diff y confirmar que corresponde al alcance, sin atribuirse cambios previos.
2. Comparar los resultados con los criterios de aceptación y la matriz; explicar cada escenario no aplicable o no ejecutado.
3. Corregir los fallos encontrados y volver a ejecutar las pruebas afectadas.
4. Verificar que el entorno que verá el usuario carga la versión correcta cuando la petición incluya dejarlo disponible allí.
5. Informar qué cambió, cómo se probó, qué datos se modificaron realmente y qué limitaciones quedan.

Si falta una prueba crítica, comunicar «implementado, con validación pendiente» y su causa; no afirmar cobertura completa. Un problema difícil o una suite inconclusa requiere una explicación concreta, no un resultado favorable supuesto.

No confundir guardar código, subir al repositorio, aplicar una migración, actualizar asignaciones y desplegar a producción. Son acciones diferentes: ejecutar las ya autorizadas y necesarias, respetando las decisiones que aún correspondan al usuario. Mantener la vista previa y confirmación operativa cuando forman parte del flujo del producto.
