# Reportes: peticiones de arrastre

Cambio mixto: sustituye la tarjeta Cubiertas por arrastre por la cola actual de peticiones pendientes que el usuario puede supervisar. Cada petición cuenta una vez, sin filtros de fecha del reporte. Ir lleva a `/validaciones#carryovers`. Conserva las reglas de resolución, permisos, snapshots y datos históricos. Sin migraciones ni escrituras operativas compartidas.

## Fuentes y recorrido

- `OperationalCarryover.cs`: solicitud persistida con snapshot de tareas y estado.
- `OperationalCarryover.Paging.cs`: `AuthorizedInboxAsync` aplica sucursal, tareas visibles, exclusión propia y supervisor inmediato. `PendingInboxCountAsync` reutiliza esta consulta y cuenta estado PENDIENTE.
- `Pages/Validations/Index.Carryover.cs`: `LoadCarryoversAsync` usa el total de la misma bandeja, con diez solicitudes por página; la resolución parcial conserva el estado pendiente.
- `Pages/Reports/Index.cshtml.cs` y `Index.cshtml`: lectura independiente de filtros, tarjeta con patrón de las colas de revisión y enlace al apartado existente.
- Exportación: se retira la métrica antigua; las colas operativas no se exportan, siguiendo el patrón de revisión existente.

## Matriz y evidencia

| Escenario | Esperado | Prueba / entorno | Estado |
|---|---|---|---|
| 12 peticiones históricas con varias tareas y una resuelta | 12, sin contar tareas ni limitar a primera página | SQL y Razor reales, base sgol_task_checks_report_carryovers_ui_20261105, sesión sintética | Aprobado |
| Filtro de fechas sin tareas | Mantiene conteo de bandeja | HTTP real | Aprobado |
| Usuario trabajador | No revela cola ni enlace de supervisión | HTTP real con sesión sintética | Aprobado |
| Ir mediante teclado | Abre Supervisión, apartado arrastres, mismo total | Chromium y WebKit | Aprobado |
| 320, 390, 768, 1440 px; claro, oscuro y especial | Tarjeta legible, botón contenido, sin desborde horizontal | 24 recorridos de navegador con CSS real | Aprobado |
| Bandeja vacía y resolución parcial | Cero o una solicitud mientras permanezca pendiente | Predicado inspeccionado; sin escenario integrado específico | No ejecutado |
| Compilación y publicación | Sin errores; DLL publicada igual a la probada | dotnet build/publish y SHA-256 | Aprobado |
| Salud compartida y acceso local/LAN | HTTP 200 y nuevo proceso Web | Readiness interno y HTTPS con TLS normal | Aprobado |
| Sesión autenticada compartida | Conteo real y navegación | Se verificó en entorno aislado; no se abrió sesión compartida | No ejecutado |
| Persistencia de decisiones y concurrencia | Sin cambios en las reglas | Solo nueva consulta de lectura | No aplicable |

Evidencia privada: `.local/report-carryovers-20261105/`. El primer intento del fixture falló por intervalo inválido; se corrigió sin desactivar validaciones. El primer host de navegador se inició desde un directorio incorrecto y no cargó CSS: resultados descartados y pruebas repetidas desde la raíz. `browser-final.log` acredita los 24 casos válidos; capturas de escritorio y móvil inspeccionadas visualmente.

Publicación: `.local/runtime/report-carryovers-20261105/Sgol.Web.dll`, SHA-256 `4D7E553CAC90C886E1B3E3CCB48F4B1CB1CA64240EB4F0629F51495AF48D5D7A`. Lanzador 24436, Web 7044; proxy 21788 conservado. Artefacto anterior images50-20261105 conservado. Referencia de Reportes reconciliada en la bóveda y su copia; se preservaron los cambios previos del Tablero. Sin commit ni push.
