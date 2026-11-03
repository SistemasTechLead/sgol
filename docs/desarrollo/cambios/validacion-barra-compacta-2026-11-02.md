# Barra compacta de Validación — 2026-11-02

Cambio de interfaz solicitado sobre `/validaciones?Validation=true`.

Se sustituye el encabezado repetido por los filtros desplegables y las acciones Filtrar/Limpiar. El apartado mantiene su nombre visible. El resumen de tareas se compacta; se elimina el texto de página/registros conservando los enlaces Anterior/Siguiente cuando correspondan.

Implementación: `_ValidationCards.cshtml`, nuevo `_ValidationFilters.cshtml` y `validation-cards.css`. El formulario GET conserva nombres, valores, opciones, filtros ajenos, limpieza y cambio de apartado. No se modifican servicios, persistencia, revisiones ni permisos. El parcial original de filtros sigue atendiendo Supervisión. Los cambios anteriores del checkout se conservaron.

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Compilar vistas y aplicación | Razor válido | dotnet build src/Sgol.Web/Sgol.Web.csproj --artifacts-path artifacts/validation-bar-build -p:UseSharedCompilation=false --no-restore | Aprobado, cero errores/advertencias |
| Contrato GET y revisión | Conservar campos y POST de revisión | Inspección comparativa de parciales | Aprobado por inspección; integración no ejecutada |
| Una o varias páginas | Sin leyenda redundante; enlaces cuando corresponda | Inspección condicional Pages > 1 | Aprobado por inspección; navegador pendiente |
| Escritorio/móvil, temas, teclado, filtros combinados, vacío/error | Desplegables accesibles sin desbordamiento y filtros conservados | Pantalla real autenticada | No ejecutado; validación visual pendiente |
| Host actualizado | Nueva DLL y aplicación disponible | PID 13168, artifacts/validation-bar-build/bin/Sgol.Web/debug/Sgol.Web.dll; HTTP local /health/ready 200 ready; LAN /acceso 200 | Aprobado |
| Datos e historial | Sin modificaciones por este ajuste | No se ejecutan migraciones ni escrituras de prueba | No aplicable a presentación |

El host utiliza el entorno local/LAN existente y el contenido estático de src/Sgol.Web. El diff de los archivos modificados de este cambio pasa git diff --check; existen avisos de espacios en cambios previos ajenos al alcance.
