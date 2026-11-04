# Base documental aceptada

[Inicio](../Inicio.md) · [Infraestructura](../mapas/infraestructura.md) · [Inventario](inventario.md)

## Fuente y vigencia

La referencia describe el comportamiento de la aplicación compartida. La bóveda y su copia en `docs/boveda/` son ubicaciones independientes. Los planes y resultados detallados de ingeniería permanecen fuera de la bóveda, en `docs/desarrollo/cambios/` del repositorio.

## Publicación efectiva

- Checkout: `C:\Users\siste\Dev\SistemaSGOL`, rama `master`. Código publicado: commit `e9273b1`, subido a `origin/master`.
- Web: `.local/runtime/incidencias-20261104/Sgol.Web.dll`, puerto interno `127.0.0.1:7241`. Proceso 28840, lanzador 19696, observados el 04/11/2026. Los IDs son observaciones temporales, no garantías para operaciones posteriores.
- Proxy conservado: proceso 20652, lanzador 23392, acceso local `https://localhost:7240` y LAN `https://192.168.1.92:7440`; almacenamiento por 7443. Confirmar dirección y procesos antes de actuar.
- Esquema aplicado: incluye `20261104171654_AddOperationalIncidences` y `20261104173103_AddIncidencePreparedHold`, sobre `20261104010000_AddMonthlyCarryoverWindow`. Incidencias forma parte del artefacto activo; no se registró ninguna ausencia al publicarlo.
- El despliegue incluye los avances previos conservados en `8ee3330`: actualización de asignaciones, posposiciones, búsquedas visibles y ajustes de interfaz. El host no usa watch. Los recursos estáticos pueden servirse desde `src/Sgol.Web/wwwroot` por el directorio de trabajo del lanzador.

## Alcance de comprobación

La publicación y su procedencia se verificaron con proceso/argumentos, compilación Release y migraciones. Las huellas de las 95 tablas anteriores, incluidas 791 asignaciones, fueron idénticas antes y después de migrar, antes de iniciar Web. Se conservó respaldo privado y el artefacto anterior. La cuenta runtime tiene acceso a las tablas nuevas.

Salud interna live/ready y acceso local/LAN respondieron 200 con validación TLS normal. Incidencias sin sesión redirige a Acceso; esto no acredita un recorrido autenticado del host. Sus reglas y formulario se probaron en bases aisladas, con sesiones sintéticas, Chromium/WebKit, tamaños y temas; esos resultados no equivalen a registrar una ausencia en la simulación compartida.

Las referencias funcionales mantienen sus límites específicos: la reprogramación posterior de procesos retirados por incidencia aún no dispone de formulario, y no hay reversión automática ni ausencias parciales. La revisión estática de las otras reglas no constituye una nueva validación funcional completa.

## Antes de operar

Seguir [instalación local](../docs/instalacion-local.md) y [acceso LAN](../docs/acceso-red.md), verificar procesos, listeners, publicación y destino de base. Una actualización de Web conserva proxy y dependencias cuando ese es el alcance. Los respaldos, secretos y evidencias privadas permanecen fuera de esta bóveda y de Git.
