# Base documental aceptada

[Inicio](../Inicio.md) · [Infraestructura](../mapas/infraestructura.md) · [Inventario](inventario.md)

## Fuente y vigencia

La referencia describe el comportamiento de la aplicación compartida. La bóveda y su copia en `docs/boveda/` son ubicaciones independientes. Los planes y resultados detallados de ingeniería permanecen fuera de la bóveda, en `docs/desarrollo/cambios/` del repositorio.

## Publicación efectiva

- Checkout: `C:\Users\siste\Dev\SistemaSGOL`, rama `master`. Base publicada: commit `400b6a3`, subido a `origin/master`, más el retiro local de excepciones por fecha en Personas, la validación directa de tarjetas y las descripciones del catálogo en columnas (sin commit).
- Web: `.local/runtime/supervision-direct-20261104/Sgol.Web.dll`, puerto interno `127.0.0.1:7241`. Lanzador 36580, observado el 04/11/2026. Los IDs son observaciones temporales, no garantías para operaciones posteriores.
- Proxy conservado: proceso 20652, lanzador 23392, acceso local `https://localhost:7240` y LAN `https://192.168.1.92:7440`; almacenamiento por 7443. Confirmar dirección y procesos antes de actuar.
- Esquema aplicado: incluye `20261104171654_AddOperationalIncidences` y `20261104173103_AddIncidencePreparedHold`, sobre `20261104010000_AddMonthlyCarryoverWindow`. Incidencias forma parte del artefacto activo; no se registró ninguna ausencia al publicarlo.
- El despliegue incluye los avances previos conservados en `8ee3330`: actualización de asignaciones, posposiciones, búsquedas visibles y ajustes de interfaz. El host no usa watch. Los recursos estáticos pueden servirse desde `src/Sgol.Web/wwwroot` por el directorio de trabajo del lanzador.

La composición compacta de Incidencias está incluida en esta publicación: registro antes de consulta y decisiones antes de retiros. La actualización del selector filtra destinatarias incompatibles y explica los bloqueos de rol y capacidad. Las opciones de destinataria y sobrecarga aparecen solo al transferir y se descartan al retirar. El reparto automático admite sobrecarga revisada y el reacomodo prioriza conservar agendas. No requiere migraciones ni registra ausencias al publicarse.

El recibo incluye descarga histórica de evidencia con autorización por alcance. La intención de una incidencia normaliza decisiones equivalentes, incluidos recibos anteriores; el temporizador no cambia la revisión de etapas sin modificaciones persistidas. Esta actualización de Web no requiere migraciones.

Personas ya no ofrece el apartado de excepciones por fecha ni acepta sus acciones de consulta, revisión y guardado. Las excepciones históricas siguen aplicándose; no se cambiaron sus registros ni el esquema. El horario semanal permanece disponible. Este retiro se comprobó con formularios HTTP y Chromium en una base aislada, utilizando sesión sintética; no acredita una sesión autenticada compartida.

Validación y la revisión individual de Supervisión guardan las decisiones con Validar en un solo envío, sin vista previa ni segunda confirmación. Conserva permisos, revisión concurrente, programación y atomicidad. El recorrido se comprobó con formularios y PostgreSQL aislados y con Chromium/WebKit; la publicación conserva el mismo hash del ensamblado probado. No acredita una sesión autenticada compartida.

Las descripciones del catálogo se despliegan en una franja de ancho completo con columnas automáticas en escritorio; en móvil conservan una columna. La publicación del 04/11/2026 usa Web 18972 y lanzador 31084, conserva el proxy 20652 y el artefacto anterior. Se comprobaron hash del ensamblado, script servido y respuestas HTTP 200 de salud interna y acceso local/LAN. Las pruebas visuales de este ajuste son de componente aislado; la comprobación autenticada compartida queda a cargo de la revisión de pantalla.

Carga y reasignación incluye el ajuste temporal de capacidad antes ubicado en Supervisión. La publicación .local/runtime/workload-capacity-20261104/Sgol.Web.dll coincide por SHA-256 con la DLL probada. Web 18972, lanzador 31084 y proxy conservado 20652 se comprobaron el 04/11/2026. Se realizaron 24 comprobaciones HTTP/SQL y 26 escenarios de navegador en copia aislada con sesión sintética; en el host compartido se verificaron salud y acceso local/LAN, sin ajustes de carga ni migraciones.

Reportes prioriza el resumen semanal con filtros plegados y acciones de plantilla/PDF al final. Las dos colas de revisión comparten la cuadrícula de ocho tarjetas, con su alcance temporal indicado dentro. Las tarjetas de estado enfocan solo las gráficas y conservan los indicadores; el tercer panel desglosa por persona. Más filtros y presentación se alinea con los demás filtros en escritorio. La selección, conservación de totales/detalle, PDF y adaptación se comprobaron en Chromium/WebKit con una copia aislada; el artefacto publicado coincide con la DLL probada. Añade colas por nivel revisor enlazadas a Validación, con todas las fechas y alcance autorizado. La publicación conserva el artefacto anterior workload-capacity-20261104. Se comprobaron el hash de la DLL, salud interna y acceso local/LAN; los flujos y la presentación se probaron en una copia aislada con sesión sintética. No requiere migraciones ni escrituras operativas compartidas.

## Alcance de comprobación

La publicación actual se verificó con proceso/argumentos y correspondencia SHA-256 con la DLL Debug probada. Se conservó el artefacto anterior y no se ejecutaron migraciones ni cambios operativos manuales. La comprobación histórica de las migraciones de Incidencias conservó las huellas de 95 tablas, incluidas 791 asignaciones; no se repitió esa migración en esta actualización. La cuenta runtime mantiene acceso a las tablas de Incidencias.

Salud interna live/ready y acceso local/LAN respondieron 200 con validación TLS normal. Incidencias sin sesión redirige a Acceso; esto no acredita un recorrido autenticado del host. Sus reglas y formulario se probaron en bases aisladas, con autenticación real del producto y cuentas sintéticas aisladas, Chromium/WebKit, tamaños y temas; esos resultados no equivalen a registrar una ausencia en la simulación compartida.

Las referencias funcionales mantienen sus límites específicos: la reprogramación posterior de procesos retirados por incidencia aún no dispone de formulario, y no hay reversión automática ni ausencias parciales. La revisión estática de las otras reglas no constituye una nueva validación funcional completa.

## Antes de operar

Seguir [instalación local](../docs/instalacion-local.md) y [acceso LAN](../docs/acceso-red.md), verificar procesos, listeners, publicación y destino de base. Una actualización de Web conserva proxy y dependencias cuando ese es el alcance. Los respaldos, secretos y evidencias privadas permanecen fuera de esta bóveda y de Git.
