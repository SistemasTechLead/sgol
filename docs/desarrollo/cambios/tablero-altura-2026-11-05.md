# Altura disponible del tablero

Cambio de interfaz aplicado el 05/11/2026. La sesión ordinaria de `/tablero` ya no tiene cabecera superior, pero `live-board.css` restaba todavía `--alto-encabezado` (88 px). Se reemplaza ese cálculo por `100dvh`; se conservan el margen interior de 24 px, el scroll de tarjetas y las reglas móviles de altura automática. La vista semanal comparte el contenedor; no cambian contratos, datos, permisos ni actualización automática.

## Comprobación

App compartida por HTTPS LAN, sesión existente de Dirección, Brave. Web usa `.local/runtime/images50-20261105/Sgol.Web.dll`; el CSS servido coincidió con el checkout antes de editar. Sin reinicio ni escrituras operativas. Antes: ventana de 1023 px y borde inferior del listado en 911,2 px. Después: 999,2 px; se recuperan 88 px.

| Escenario | Resultado esperado | Prueba y entorno | Estado |
|---|---|---|---|
| Escritorio 1440×900 y 1024×768, claro/oscuro/especial | Margen inferior de 24 px y sin desbordamiento horizontal | Medición DOM de pantalla real compartida; seis combinaciones | Aprobado |
| Móvil 390×844 y 320×700, claro/oscuro/especial | Altura natural, desplazamiento del documento y sin desbordamiento horizontal | Medición DOM de pantalla real con modo auto temporal; seis combinaciones | Aprobado |
| Preferencia escritorio forzado | Recuperar 88 px sin cambiar la preferencia | Sesión original, medición y captura | Aprobado |
| Filtro abierto y cerrado con Enter | Mantener borde inferior y control por teclado | Pantalla real, filtro Consultar tarea | Aprobado |
| Desplazamiento del listado por teclado | Foco en listado y movimiento del scroll | Tecla End; foco y scrollTop observado | Aprobado |
| Vista semanal y sesión dedicada | Conservar presentación propia | Inspección del alcance CSS; sin recorrido visual | No ejecutado en navegador |
| Vacío, error de conexión, actualización automática | Conservar los componentes existentes | Sin cambios en productores ni JS | No ejecutado; no se simularon fallos en el entorno compartido |
| Persistencia y concurrencia | Sin cambios | CSS únicamente | No aplicable |

Se restauraron dimensiones y atributos temporales de tema/vista recargando la pestaña; no se modificaron preferencias guardadas. El modo escritorio forzado mantiene su ancho mínimo existente cuando la ventana es estrecha; las comprobaciones móviles usan modo auto.

Se añadió una aserción de regresión en `tools/Sgol.MobileChecks/board-header-checks.cjs` para detectar espacio reservado bajo el listado. La suite completa no se ejecutó; la comprobación equivalente se realizó en el navegador compartido. No se requiere compilación por el cambio CSS. La referencia de pantallas se actualiza en la bóveda y en su copia versionada.
