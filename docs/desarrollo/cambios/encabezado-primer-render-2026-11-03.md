# Encabezado durante la navegación — 03/11/2026

Cambio de interfaz. El layout entrega SGOL visible y el título de pantalla oculto; `desktop-workspace.js`, diferido, intercambia su visibilidad después. Ese estado inicial explica el destello informado, sin evidencia de una versión antigua del servidor.

`desktop-workspace.css` muestra el título que Razor ya entrega desde el primer renderizado en el contenedor de escritorio y oculta la marca y el título duplicado del contenido, excluyendo los diálogos. La tipografía del título tampoco depende de la inicialización. Se reutiliza `sgol-viewport`, incluidos los anchos de apariencia guardados. Móvil, impresión, acceso y errores conservan su comportamiento. Sin JavaScript, escritorio muestra el título de Razor arriba; ayudas y controles originales siguen disponibles. El plegado de filtros continúa siendo una mejora JavaScript: esta corrección elimina el destello del encabezado, no promete eliminar toda recomposición ni reducir la latencia de las consultas.

## Evidencia

Prueba mantenible: `tools/Sgol.MobileChecks/desktop-first-paint-checks.cjs`. Páginas Razor reales en HTTPS localhost:7284, copia aislada existente `sgol_task_checks_desktop_20261103`. Intercepta únicamente el CSS candidato y retiene la descarga de la mejora JavaScript para observar el estado inicial. No escribe datos operativos.

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Mi trabajo, Tareas, Validaciones y Tablero con script retenido | Título visible y SGOL oculto antes de inicializar | Edge y WebKit, host aislado | Aprobado |
| Tres temas | Encabezado inicial visible | DOM real; captura Edge de Tareas inspeccionada | Aprobado |
| 320, 768, 1024 y 1440 px | Un título visible tras adaptar el ancho | Ambos motores, páginas reales | Aprobado |
| Impresión | Título del encabezado de escritorio oculto | Ambos motores, emulación print | Aprobado |
| CSS disponible en local y LAN | Respuesta 200 con regla corregida | HTTP tras copiar el único CSS al runtime activo | Aprobado |
| Teclado, foco, datos y permisos | Sin cambios de controles o contratos | Inspección del cambio solo CSS | No aplicable a nuevas interacciones; no se repitió suite de formularios |
| Dispositivo físico y lector de pantalla | Verificación específica | No disponible en esta ejecución | No ejecutado |
| Compilación y migración | Sin cambios compilados ni esquema | Solo CSS y prueba de navegador | No aplicable |

La primera comprobación de impresión necesitó esperar el evento de adaptación. La captura WebKit con el script retenido esperaba fuentes hasta timeout; se retiró esa captura del escenario artificialmente bloqueado, conservando las aserciones de DOM y la captura Edge. La ejecución final aprobó ambos motores.

Se verificó el proceso de 7241 sirviendo `.local/runtime/desktop-workspace-delivery-20261103/Sgol.Web.dll` y que el diff del CSS desplegado contra el candidato incluía solo esta corrección. Se respaldó el CSS anterior en `.local/desktop-workspace-20261103/desktop-workspace-before-first-paint.css` y se actualizó ese recurso sin reinicio. Los cambios previos del workspace se conservaron. Sin commit, push ni cambios en la simulación compartida.
