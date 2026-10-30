# Supervisión: mostrar diez tareas por apartado

Cambio de interfaz con lectura acumulativa en servidor. `/validaciones`, en su vista Supervisar, mostraba hasta 50 tareas por apartado mediante `OperationalSupervisionService.PageAsync`. Ahora muestra las primeras diez; una flecha al pie permite mostrar hasta diez adicionales conservando las anteriores. El enlace desaparece al alcanzar el total filtrado. No modifica asignaciones, revisiones ni datos simulados.

## Recorrido e invariantes

- `Pages/Validations/Index.cshtml.cs`, `LoadAsync`: solicita lectura acumulativa solo para Supervisar y construye el enlace GET conservando filtros y expansión de otros apartados. `_Filters.cshtml` ya elimina los parámetros del apartado filtrado, reiniciando su expansión.
- `Infrastructure/Persistence/Planning/OperationalSupervisionService.Paging.cs`, `PageAsync`: conserva autorización, agrupación, filtros y orden estable por fecha e ID, incluidos registros históricos. El modo acumulativo toma desde el inicio; los consumidores anteriores y Validación conservan páginas de 50.
- `Pages/Validations/Index.cshtml`: contador sobre coincidencias filtradas, flecha con texto y nombre accesible, ancla en la primera fila recién mostrada. `TaskHref` conserva la expansión al abrir y guardar una revisión.
- `wwwroot/css/components.css`: botón secundario centrado con tokens existentes; reutiliza tabla, iconografía SVG, foco y adaptación existentes. No añade dependencias ni migraciones.

## Matriz de verificación

| Escenario / precondición | Acción y resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| 0, 1 o 10 coincidencias | Abrir: mostrar todas, sin flecha; conservar estado vacío | Inspección de condiciones Razor y consulta | Aprobado por inspección; navegador no ejecutado |
| 11, 20, 21 y más de 50 coincidencias | Mostrar diez adicionales, conservar anteriores y orden, desaparecer al final | Inspección de Take acumulativo y límite de página | Aprobado por inspección; integración no ejecutada |
| Varios apartados desplegados y filtros activos | Desplegar uno: conservar los demás; filtrar uno: reiniciar ese apartado a diez | Inspección de MoreHref y formulario `_Filters` | Aprobado por inspección; formulario real no ejecutado |
| Tareas históricas y revisión individual | Abrir/cerrar detalle o guardar: conservar filtros y expansión; no cambiar permisos | Inspección de TaskHref y consulta autorizada existente | Aprobado por inspección; recorrido real no ejecutado |
| Validación masiva y otros consumidores | Conservar paginación de 50 | Inspección del parámetro opcional y rama Validation | Aprobado por inspección; integración no ejecutada |
| Escritorio, 320 px, temas, teclado y foco | Flecha visible, accesible y sin desbordamiento; ancla en filas nuevas | Navegador real Brave | No ejecutado: Computer Use se detuvo al no poder determinar la URL con confianza suficiente |
| Compilación y diff | Compilar Web y verificar ausencia de errores de diff | Salida aislada `.local/supervision-expand-build`; `git diff --check` | Aprobado: compilación sin errores ni advertencias |

La compilación inicial en la salida compartida falló por CS2012: otro proceso tenía bloqueado `obj/Debug/net10.0/Sgol.Web.dll`. Se inició una compilación con `--artifacts-path .local/supervision-expand-build -p:UseSharedCompilation=false`. La validación visual e integrada queda pendiente; la inspección no las sustituye.
