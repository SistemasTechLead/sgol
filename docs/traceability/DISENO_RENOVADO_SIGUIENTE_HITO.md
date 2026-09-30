# SGOL — Siguiente hito: adaptación de pantallas existentes

## Control

Nombre exacto del siguiente chat: **SGOL — Diseño renovado — Adaptación de pantallas existentes**.

Este mensaje prepara un hito posterior; no lo ejecuta, no crea un chat y no autoriza publicar. La referencia fue aprobada mediante «Apruebo las secciones y la adenda», §§3–9 del plan documental y Adenda 55. El resultado previo es referencia documentada, sin adaptación productiva.

## Prompt completo listo para copiar

Trabaja en SGOL siguiendo AGENTS.md e INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md, con este alcance especial aprobado para agrupar la adaptación visual de las pantallas ya existentes en un único hito.

Objetivo: adaptar las pantallas existentes a la referencia oficial «CSS propio renovado». Lee primero docs/traceability/IMPLEMENTATION_STATUS.md, ejecuta scripts/ci/preflight.ps1 como única comprobación inicial, inspecciona Git y preserva cambios ajenos. Acepta el registro vigente y la evidencia previa de las tareas implementadas, incluida FRONT-016, sin repetir sus análisis ni gates para volver a aprobarlas. No declares integración sin evidencia.

Lee y acepta la aprobación de F07_ADENDA_55_REFERENCIA_DE_DISENO_RENOVADO.md y docs/design/referencia-renovada.md, junto con docs/design/tokens.md, componentes.md, estados-y-mensajes.md, estados-de-dominio.md y accesibilidad.md, además de navegacion.md y el plan documental aprobado. La aprobación «Apruebo las secciones y la adenda» cubre §§3–9 de docs/design/PLAN_DISENO_RENOVADO.md. No vuelvas a proponer Tailwind, daisyUI ni bibliotecas de componentes. No dependas de la maqueta externa como contrato ni copies sus acciones simuladas, barra de comparación, selectores de muestra, navegación móvil abierta o cambios funcionales.

Prepara un único plan de implementación en Markdown y solicita mi aprobación antes de modificar código. Inventaría únicamente las pantallas y componentes ya existentes, sus rutas/contratos, las diferencias visuales y las pruebas directamente afectadas. Propón grupos concretos de adaptación, archivos previstos, estrategia de consumo de variables, validaciones enfocadas y revisión visual proporcional. Localiza IDs con docs/INDICE_IDS.md y lee sólo las filas/criterios F05 y decisiones F06 necesarios. No reanalices íntegramente F00–F07. Si falta un componente o mensaje, presenta su carencia y propuesta exacta; no lo improvises.

Después de aprobar el plan, renueva primero el layout y los componentes compartidos existentes, incluida la hoja que declara las variables aprobadas. Adapta después las pantallas por grupos coherentes. Usa CSS propio, variables oficiales y parciales compartidos; evita otra biblioteca paralela. Conserva identidad Loretta, semántica HTML y navegación móvil modal. Respeta la transición: no exijas migrar todo como dependencia de FRONT-017..020; no implementes esas historias ni otras capacidades pendientes en este hito.

Preserva rutas, permisos, jerarquía, sesión, contratos, mensajes aprobados, auditoría y comportamiento: no cambies endpoints, reglas de negocio, escrituras, CSRF, idempotencia, If-Match, cursores ni autorización en servidor. Toda agrupación visual debe mantener datos, etiquetas y acciones autorizadas. No conviertas tablas a tarjetas ni ocultes información esencial. Cubrir normal, foco, deshabilitado, error, cargando y vacío según la matriz. No toques Fuentes/ ni F00–F07 congelados; cualquier cambio contractual necesita decisión y adenda, sin inventar IDs.

Crea commits locales pequeños por componente o grupo de pantallas dentro de este único hito. Compila y ejecuta pruebas nuevas o directamente afectadas, de forma enfocada y proporcional conforme a AGENTS.md. Haz revisión visual de escritorio/móvil y de estados pertinentes con datos sintéticos; verifica contraste efectivo, teclado, foco/retorno, área mínima, texto ampliado, reflow y movimiento reducido. Conserva las pruebas funcionales vigentes; sólo actualiza expectativas visuales obsoletas dentro del plan aprobado. Una prueba enfocada compatible fallida bloquea marcar esa superficie implementada. Registra validaciones diferidas con causa exacta, sin presentarlas como éxito.

Actualiza trazabilidad por grupo, separando referencia documentada de diseño implementado y preservando evidencia histórica. Entrega capturas sintéticas y explica qué se renovó y cómo se usa cada pantalla; no incluyas secretos, evidencia real o datos de sesión sensibles. Las capturas deben identificarse como aplicación funcionando o previsualización, sin confundir ambas. No publiques por cada pantalla ni por historias anteriores.

Cuando yo autorice expresamente la publicación de este hito, prepara rama codex/, commits necesarios y un único PR con el pipeline final agrupado. No hagas push, PR ni checks remotos antes de esa autorización. No reabras PR por cada pantalla o tarea anterior. Ejecuta las validaciones de integración correspondientes al hito autorizado y acredita la cabeza exacta vigente.

La autorización de publicación incluye seguir el pipeline y corregir defectos del mismo hito, validar y subir las correcciones al mismo PR sin nuevas confirmaciones de publicación. Mantén el seguimiento hasta que la cabeza actual tenga todos los checks requeridos correctos y se cumplan los requisitos previos, o exista una decisión necesaria. Si el turno termina con pipeline pendiente, configura y verifica seguimiento mediante heartbeat en este mismo chat, sin avisos repetidos de estado sin cambios. Entrega o actualiza las capturas mientras continúa el seguimiento. Al quedar validada la cabeza actual, solicita mi aprobación expresa del merge, indicando PR, SHA y evidencia. No hagas merge ni despliegue por la autorización de publicación.
