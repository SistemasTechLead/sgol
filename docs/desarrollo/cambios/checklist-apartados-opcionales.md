# Checklist con apartados opcionales — 28/10/2026

## Alcance y decisión

Cambio mixto. Tras la corrección del usuario, el editor muestra directamente dos campos: título opcional y casilla circular con sangría, sin selector de tipo. Los placeholders son exactamente «Agrega un Titulo» y «Agrega una Casilla». Al escribir una casilla aparece debajo otra casilla sombreada del mismo apartado y, al final, un apartado sugerido con título y casilla sombreados. Continuar un apartado no duplica la sugerencia del siguiente. Los títulos pueden escribirse después de las casillas y se guardan antes de ellas. No se guarda contenido vacío ni se predefine contenido o numeración. Un checklist requiere al menos una casilla. No se modifican automáticamente las definiciones de asignaciones existentes.

## Recorrido e invariantes

- `Pages/Shared/_ChecklistEditor.cshtml` y `wwwroot/js/checklist.js`: editor compartido por tareas de catálogo y ocasionales; orden, tipo e identificador estable por fila.
- `Infrastructure/Persistence/Planning/OperationalChecklist.cs`: `ChecklistPoint.IsTitle` opcional, compatible con JSON anterior; normalización en servidor, validación de respuesta, reconciliación de borradores y diferencias. Los títulos no admiten marcas ni cuentan para completar.
- `OperationalTaskService.SaveAsync` y creación ocasional normalizan y guardan el mismo JSON. Las asignaciones directas, generación de planes y snapshots de procesos transportan `ChecklistJson` completo, sin proyectar las filas. No cambia el esquema de base de datos.
- `AssignmentDefinition` adopta cambios mediante el flujo existente de actualizar asignaciones; `Reconcile` conserva marcas de casillas sin cambios. Renombrar un título conserva marcas; convertir una casilla en título elimina su marca. Se mantiene el conflicto por huella ante respuestas de una definición anterior.
- `_ChecklistEvidence.cshtml`: títulos y casillas circulares en ejecución y revisión, con contadores limitados a casillas. Las entregas conservan snapshot y marcas históricos.
- `_ChecklistDefinition.cshtml`: presentación común en catálogos de tareas y ocasionales. `Reporting/ReportData.Cell` presenta títulos sin símbolo de casilla.
- Permisos, CSRF, responsables, plazos, cargas y mecanismo de actualización existentes se conservan.

## Entornos

Repositorio inicialmente limpio. PostgreSQL local identificado en el contenedor `sgol-local-postgres-1`, puerto local 5440. Bases nuevas exclusivas `sgol_task_checks_sections_http_20261028`, `sgol_task_checks_sections_flow_20261028`, `sgol_task_checks_sections_browser_20261028` y `sgol_task_checks_sections_visual_20261028`; nunca se ejecuta la suite contra `sgol_local`. Aplicación HTTP de prueba en 7259/7258 con Razor, servicios, PostgreSQL y antiforgery reales, identidad de prueba inyectada por el arnés existente. Esto no verifica el inicio de sesión real ni la aplicación compartida por LAN.

## Matriz de validación

| Escenario | Esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Checklist sin títulos / formato anterior | Guardar, reabrir y completar | Reglas + navegador de componente | Aprobado |
| Títulos y casillas intercalados | Conservar tipo y orden | Formulario Razor HTTP + componente | Aprobado |
| Campos sombreados | Escribir una casilla genera continuación y próximo apartado, sin selector ni pérdida de foco | Playwright de componente | Aprobado |
| Título escrito después de las casillas | Guardar el título antes de sus casillas; excluir campos vacíos | Playwright de componente | Aprobado |
| Error de checklist con solo títulos | Agregar casilla en cualquier apartado permite continuar | Playwright de componente | Aprobado |
| Reordenar y eliminar filas | Persistencia y marcas coherentes; quitar título conserva casillas | Componente + reglas | Aprobado |
| Solo títulos, IDs duplicados, filas inválidas | Rechazo en servidor | `ChecklistChecks.Rules` | Aprobado |
| Marcar un título mediante envío manipulado | Rechazo en servidor | `ChecklistChecks.Rules` | Aprobado |
| Renombrar título / convertir casilla | Conservar marcas válidas / quitar marca convertida | `ChecklistChecks.Rules` | Aprobado |
| Guardar borrador y recargar | Restaurar marcas sin contar títulos | Formulario HTTP real, base aislada | Aprobado |
| Entrega parcial y completa | Rechazar parcial, aceptar todas las casillas | Formulario HTTP real, base aislada | Aprobado |
| Actualización, concurrencia y autoría | Respuesta obsoleta rechazada, permisos y marcas conservados | Suite `--checklist-only` | Aprobado |
| Correcciones, transferencia, ocasionales y procesos | Conservar evidencia y snapshots | Suite `--checklist-only` y `--process-editor-only` | Aprobado |
| Pantallas reales en escritorio, 320 px y temas | Contenido legible y sin desbordamiento del editor | `checklist-sections-browser.cjs` | Aprobado |
| Edición posterior frente a entrega histórica | Mantener título y casillas originales de la entrega | Navegador contra aplicación aislada | Aprobado |
| Inicio de sesión y LAN compartida | Versión y sesión efectivas verificadas | Fuera del arnés aislado | No ejecutado |
| Migración | No cambia esquema | Inspección del contrato text/JSON | No aplicable |

## Evidencia

Compilación de `tools/Sgol.TaskChecks` (incluye Web) sin errores ni advertencias. Suites HTTP y ciclo de vida completas aprobadas. Capturas y registros locales ignorados en `artifacts/checklist-ui/`. Las pruebas de componente no sustituyen la verificación de las pantallas reales, registrada por separado en la matriz.

Comandos: `dotnet build tools/Sgol.TaskChecks/Sgol.TaskChecks.csproj -o .local/checklist-sections-tools`; ejecutable construido con `--checklist-only`, `--process-editor-only` y `--checklist-http-only` (este último permite repetir solo los formularios del checklist). Para navegador real, `SGOL_CHECKLIST_BROWSER_CHECK` apunta a `tools/Sgol.TaskChecks/checklist-sections-browser.cjs` y `NODE_PATH` a las dependencias Playwright del runtime. `SGOL_TASK_CHECKS_CONNECTION` debe apuntar siempre a una base vacía exclusiva localhost `sgol_task_checks_*`. Registros: `sections-flow.log`, `sections-http.log`, `sections-browser.log`, `sections-visual.log`.

Revisión del editor sin selector: compilación aprobada; pruebas de componente y formularios reales más navegador aprobadas con base exclusiva `sgol_task_checks_progressive_fields_20261028`. Registro `artifacts/checklist-ui/progressive-fields-http.log`; capturas `actual-editor-detail.png`, `actual-editor-320.png`, `actual-editor-dark-320.png`. Se revisaron visualmente las capturas del editor. No se modificó la simulación compartida para estas comprobaciones.
