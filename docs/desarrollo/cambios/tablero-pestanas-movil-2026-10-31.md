# Tablero: navegación móvil y filtros

Cambio de interfaz exclusivamente. La cuadrícula móvil reservaba columnas para Filtros y Personas en la fila de `.board-heading`, reduciendo el ancho disponible de la navegación. Se ajustó `wwwroot/css/mobile-hierarchy.css` dentro del contenedor móvil de 48rem: encabezado completo, dos destinos en columnas iguales, desplegables en la fila siguiente, formulario y fecha debajo. Alcance limitado a `.board-shell .board-header` con navegación; no cambia quiosco, consultas, permisos ni datos.

Se conservaron las modificaciones previas del repositorio. Se reutilizan navegación compartida, tokens, controles y lógica de plegado existentes. No requiere compilación por ser CSS. El servidor actual en localhost:7240 entrega el CSS corregido (comprobación HTTP); no se reinició.

## Validación

Página Razor real `/tablero` en instancia aislada localhost:7284, base `sgol_task_checks_mobile_ios_20261030`, con sesión Dirección vigente verificada por acceso efectivo. Chromium/Edge y WebKit. Ejecutor: `tools/Sgol.MobileChecks/board-header-checks.cjs`; resultados y capturas en `.local/mobile-hierarchy/board-header-*`.

| Escenario | Resultado esperado | Prueba / estado |
|---|---|---|
| 320, 390, 430 y 768 px; claro, oscuro y especial | Pestañas alineadas, controles debajo, sin desbordamiento | Geometría DOM, 24 casos aprobados |
| Abrir/cerrar filtros con Enter | Formulario debajo de controles, estado accesible correcto | Aprobado en ambos motores; valores conservados |
| Texto al 200% en 320 px | Misma fila de destinos, sin desbordamiento global | Aprobado en ambos motores |
| Volver a 1440 px | Formulario visible y desplegables móviles ocultos | Aprobado en ambos motores |
| Presentación oscura a 390 px | Separación y etiquetas legibles | Captura WebKit inspeccionada |
| Safari físico, lector de pantalla | Operación equivalente | No ejecutado; sin dispositivo o lector en esta prueba |
| Persistencia, concurrencia, cargas e historial | Sin cambios | No aplicable: solo estilos de disposición |

La comprobación global `git diff --check` señala espacios finales en cambios previos de la guía visual y `_Layout.cshtml`; estos archivos no se modificaron en esta corrección.
