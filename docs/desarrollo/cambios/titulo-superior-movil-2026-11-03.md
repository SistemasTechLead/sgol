# Título superior móvil — 2026-11-03

Cambio de interfaz solicitado: título e información en la barra superior también en móvil. La instrucción actual sustituye la cabecera móvil anterior de la guía visual. Se mantiene el comportamiento de filtros, formularios, navegación, roles y datos.

## Implementación

- `_Layout.cshtml`: no emite la marca SGOL en las cabeceras operativas; entrega el título superior desde el servidor, sin esperar JavaScript.
- `desktop-workspace.css`: aplica la cabecera única desde la primera pintura a todos los anchos. Los títulos originales del contenido no se renderizan en pantalla; conservan sus IDs para las asociaciones accesibles y su presentación impresa. El grid móvil reserva espacio para navegación, título/ayuda, cuenta e identidad sin recortar títulos largos.
- `desktop-workspace.js`: separa inicialización del título/ayuda de los filtros exclusivos de escritorio. No reemplaza el texto del título al inicializar. La ayuda funciona también en móvil, se acota al contenedor elegido y evita duplicar descripciones que la adaptación del catálogo ya anida.
- `mobile.js`: evita crear la segunda ayuda de cabecera en pantallas que utilizan la cabecera compartida.
- `People/Details.cshtml`: proporciona desde el servidor el título visible específico de la persona o error, conservando el título documental Historial laboral. Evita alternar texto después de cargar.

Acceso, errores globales y quiosco conservan sus layouts. No se alteran productores/consumidores de datos operativos, persistencia, historial, permisos ni contratos HTTP.

## Entorno y preservación

Trabajo sobre el checkout vigente, conservando cambios previos de otros chats. Prueba real autenticada en HTTPS localhost:7284, copia aislada `sgol_task_checks_desktop_20261103` en PostgreSQL localhost:5440. Fecha operativa: 03/11/2026, America/Mexico_City. Sin escrituras de tareas, planes ni asignaciones; sin migraciones.

## Escenarios y evidencia

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| 20 rutas; script de mejora retenido | Un h1 superior, sin SGOL en el HTML operativo ni título alternativo visible | `mobile-title-checks.cjs`, Edge y WebKit, páginas Razor reales | Aprobado |
| 320, 390, 768, 1024, 1440 y regreso a 320 px | Mismo título superior; filtros móviles sin wrappers de escritorio | 40 recorridos ruta/motor, seis pasos de ancho | Aprobado |
| Claro, oscuro y especial | Ayuda acotada al viewport sin desplazar main | Tres temas en cada ruta/motor; capturas privadas | Aprobado |
| Enter, Escape y toque exterior | Abrir, cerrar y recuperar foco; sin segunda i en el contenido | DOM e interacción reales | Aprobado |
| Móvil centrado en pantalla amplia | Ayuda dentro del contenedor elegido | `.local/mobile-title-20261103/extras.cjs`, ambos motores | Aprobado |
| Título largo al 200 %, ancho 320 | Sin overflow horizontal; menú abre y cierra con Escape | Porcentaje para el bono, ambos motores | Aprobado |
| Impresión | Cabecera original para impresión, sin título superior duplicado | Emulación print por ruta/motor | Aprobado |
| Compilación | Razor y código válidos | Publicación sin restore | Aprobado |
| Safari físico y lector de pantalla | Verificación específica | No disponibles en esta ejecución | No ejecutado |
| Integridad de asignaciones/migración | Ningún cambio operativo | Alcance y diff | No aplicable: presentación sin cambios de datos |

Evidencia: `.local/mobile-title-20261103/checks.log`, `results.json`, `extras.log` y capturas. Se detectó y corrigió duplicación de párrafos en ayuda móvil. Un intento inicial usó una sesión anterior al cambio de host y se repitió con login real; la prueba de primera pintura espera las hojas de estilo bloqueantes sin liberar el script retenido.

La entrega requiere comprobar estabilidad de las fuentes al publicar, equivalencia con el artefacto probado y readiness/acceso LAN tras reiniciar el host. No hay commit ni push solicitado.

## Activación completada

Host reiniciado con `.local/runtime/mobile-title-delivery-20261103/Sgol.Web.dll`. Las fuentes permanecieron estables durante la publicación y el ensamblado/recursos modificados coinciden por hash con la versión probada. Readiness interno `ready`, acceso LAN HTTP 200 y CSS vigente comprobados; se preservó el proxy y los cambios de otros chats. Evidencia privada: `.local/mobile-title-20261103/deployment.json`. No se modificaron datos operativos compartidos.

## Ajuste de identidad y altura móvil

Petición posterior: nombre/puesto a la izquierda del avatar y cabecera delgada. Se cambió únicamente el grid móvil de `desktop-workspace.css` a cuatro columnas: navegación, título/ayuda, identidad y avatar. Identidad en columna, centrada verticalmente en la misma fila; se elimina la segunda fila anterior. El ancho se limita sin truncar texto.

Validación aprobada en páginas reales de Supervisión y Porcentaje para el bono, Edge y WebKit, anchos 320/390/418/768 y temas claro/oscuro/especial (48 combinaciones). Se comprobaron identidad a la izquierda del avatar, alineación vertical, ausencia de solapamiento/desbordamiento y altura compacta en Supervisión desde 390 px (60 px en la captura de 418). Evidencia privada: `.local/mobile-identity-20261103/checks.log` y capturas; pruebas físicas/lector de pantalla no ejecutadas. No se requieren pruebas operativas ni migraciones: solo CSS.

Publicación y reinicio completados con `.local/runtime/mobile-identity-20261103/Sgol.Web.dll`. Fuentes estables, ensamblado y CSS idénticos a los probados; readiness y LAN aprobados. Se conservaron cambios de otros chats y el proxy. No se modificaron datos ni se alteró el staging existente.
