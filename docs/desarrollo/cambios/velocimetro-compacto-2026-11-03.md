# Velocímetro compacto — 03/11/2026

Cambio mixto de presentación y selección del periodo en Mi trabajo. Se elimina el selector actual/anterior, se muestra solo el porcentaje junto al velocímetro y la i en todas las instancias del componente compartido. La ayuda agrupa resultado, cálculo del bono, periodo/cierre y reglas de puntos. La consulta histórica de Supervisión conserva su propósito y sus permisos.

## Rutas y alcance

- `Pages/MyWork/Index.Bonus.cs`: carga siempre la semana actual, incluso con BonusWeek antiguo o inválido. El refresco usa la misma política.
- `Pages/MyWork/Index.cshtml`: elimina el formulario de periodo y la semana fija del feed.
- `Pages/Shared/_TaskBonusGauge.cshtml`: una cifra en el activador; resultado económico, fechas, estado e incidencias dentro del diálogo. Cerrar queda al inicio para que el foco no desplace la explicación al final.
- `wwwroot/js/task-bonus.js`: actualiza resultado y fechas al cambiar de semana sin recargar ni borrar borradores; conserva actualización, errores, recuperación, control de acceso y foco.
- `wwwroot/css/task-bonus.css`: composición compacta, secciones semánticas, reflujo y ayuda visible también en Tablero.

El productor sigue siendo TaskBonusService/TaskBonusCalculator; no cambian obligaciones, snapshots, reglas de puntos, cierres, exclusiones, asignaciones ni historial. No hay migración. El cálculo conserva las condiciones especiales sin puntos evaluables/incidencias. La petición actual sustituye la presentación anterior del selector y de las dos cifras.

## Entorno y preservación

Checkout vigente con modificaciones previas, staged y unstaged, conservadas. Simulación compartida en PostgreSQL localhost:5440/sgol_local, backend LAN en 7241 y proxy existente. Las pruebas usan exclusivamente bases nuevas `sgol_task_checks_gauge_*` y HTTPS localhost:7296, con reloj controlado en noviembre de 2026. No se escriben datos operativos compartidos. No se solicita commit/push.

## Matriz de validación

| Escenario | Esperado | Prueba y entorno | Estado |
|---|---|---|---|
| Integración de datos, formularios y permisos | Cálculo y aislamiento conservados | TaskBonusChecks --bonus-http --ui; 23 comprobaciones, base aislada | Aprobado |
| Mi trabajo, Supervisión y Tablero | Un porcentaje; ayuda accesible y sin desplazamiento de página | task-bonus-checks.cjs, Edge/WebKit, aplicación Razor real | Aprobado |
| 320/375/768/1280/1920, tres temas | Sin desbordamiento; diálogo dentro del viewport | Misma suite y capturas | Aprobado |
| Texto 200 %, movimiento reducido | Reflujo y ausencia de animación | Misma suite | Aprobado |
| Enter/Tab/Escape/Cerrar | Foco en diálogo y retorno al activador | Misma suite | Aprobado |
| URL de semana anterior | Mostrar semana actual | Navegador real y handler | Aprobado |
| Actualización/error/red/sesión/quiosco/SSR | Mantener contratos anteriores | Misma suite | Aprobado |
| Cambio de semana con borrador | Actualizar fechas y porcentaje sin navegación | task-bonus-current-checks.cjs, Edge/WebKit y reloj aislado | Aprobado |
| Compilación/publicación | Sin errores | build y publish sin restore | Aprobado |
| Dispositivo físico/lector de pantalla | Comprobación específica | No disponibles | No ejecutado |
| Datos históricos/migración/concurrencia de escrituras | No se modifican productores ni persistencia | Revisión del diff | No aplicable |

Evidencia local privada: `.local/gauge-compact-20261103/`; fixture/capturas de la suite existente en `.local/bonus-20261102/`. La primera pasada detectó desbordamiento al 200 %; corregido con columna completa del velocímetro en contenedores estrechos. La revisión visual detectó apertura del diálogo desplazada al final; corregida moviendo Cerrar a la cabecera. Los resultados finales se registran al concluir.

## Cierre

98 casos de navegador aprobados en Edge y WebKit; ambas comprobaciones adicionales de cambio de semana conservaron el borrador real de entrega sin navegación, actualizaron fechas y validaron URL antigua/inválida y estado sin tareas. Capturas revisadas de Mi trabajo y diálogo móvil; 23 comprobaciones de integración aprobadas. Build: cero errores y advertencias. Diff del alcance sin errores de whitespace; modificaciones ajenas preservadas.

Aplicación compartida reiniciada con .local/runtime/gauge-compact-20261103/Sgol.Web.dll. Fuentes verificadas estables tras publicación, readiness interno ready, acceso LAN 200 y CSS vigente comprobados. Se conservó el proxy y no se modificaron registros operativos compartidos. Evidencia: deployment.json, browser.log, current.log, build.log, publish.log y browser-results.json en el directorio de evidencia. Sin commit ni push.
