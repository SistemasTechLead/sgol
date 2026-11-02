# Selección de Operaciones semanales — 02/11/2026

Cambio de interfaz y navegación. Al entrar a `/planificacion?mode=operations` con un plan semanal existente, `LoadDirectionPlansAsync` redirigía al detalle sin `mode`, por lo que `SectionNavigation` seleccionaba Planes. Ahora resuelve el identificador internamente y usa la consulta habitual del detalle conservando la URL y su `aria-current`. Desde Planes, el detalle sigue perteneciendo a Planes.

Se modifica solamente la resolución de lectura en `src/Sgol.Web/Pages/Planning/Index.DirectionPlans.cs`. No cambia la generación, almacenamiento, asignación, revisión ni historial. Se conservan permisos, validación de parámetros, componentes y estilos. Los cambios previos de resolución de conflictos y filtros del catálogo estaban presentes y se conservaron.

Entorno: copia aislada `localhost:5440/sgol_task_checks_weekly_nav_20261102`, obtenida de la simulación `sgol_local`; host de pruebas HTTPS 7257 con autenticación sustituida por Dirección, páginas Razor y API reales. La instancia compartida observada ejecutaba `.local/runtime/task-filters-20261102/Sgol.Web.dll`; no se reinició ni desplegó este cambio.

| Escenario y resultado esperado | Prueba / entorno | Estado |
|---|---|---|
| Plan semanal existente: consultar sin redirección y sin formulario de creación; Operaciones semanales activo | `--weekly-navigation-http`, copia aislada | Aprobado |
| Lista y detalle desde Planes: Planes activo, un único `aria-current` | HTTP y navegador, misma copia | Aprobado |
| Entrar con teclado, recargar y cambiar entre apartados | Chromium/Edge y WebKit; 320, 390, 768 y 1440 px; temas light, dark y special | Aprobado: 24 combinaciones |
| Consultar conserva todas las asignaciones | Comparación serializada antes/después HTTP | Aprobado |
| Compilación | `dotnet build tools/Sgol.TaskChecks --no-restore -o .local/weekly-nav-build -v quiet` | Aprobado, sin advertencias |
| Semana sin plan, errores de API y otros roles | Ramas existentes inspeccionadas; no se ejecutaron en esta regresión | No ejecutado |
| Safari físico y lector de pantalla | Sin dispositivos en esta ejecución | No ejecutado |
| Migraciones, concurrencia de escritura, cargas, plazos e historial | No cambia contratos ni escrituras | No aplicable |

Regresiones: `tools/Sgol.TaskChecks/WeeklyPlanHttpChecks.cs`, opción `--weekly-navigation-http` (requiere copia con plan de la semana actual); `tools/Sgol.MobileChecks/weekly-navigation-checks.cjs` contra ese host con `--ui`. Capturas y resultados locales: `.local/weekly-navigation-20261102/`. Inspección visual de captura móvil confirma el subrayado de Operaciones semanales junto al detalle real. No se probó autenticación real ni se publicó el cambio. Revisión del diff: los avisos de espacios restantes corresponden a modificaciones previas del catálogo.

## Carga local autorizada posteriormente

El usuario pidió cargarlo en la aplicación local. Se publicó y activó `.local/runtime/weekly-navigation-20261102/Sgol.Web.dll`, versión informativa `1.0.0+weekly-navigation-20261102`, sustituyendo la instancia `task-filters-20261102`. Compilación publicada sin errores; hash del ensamblado y PID activo verificados. Salud interna `alive`/`ready` y salud LAN `alive` aprobadas. Evidencia local en `.local/deploy-weekly-navigation-20261102/`. No se ejecutaron migraciones ni modificaciones operativas de datos. La comprobación visual autenticada corresponde al host aislado descrito arriba.
