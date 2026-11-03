# Tareas: renglones compactos — 2026-11-03

Cambio de interfaz en `/tareas`. Se elimina la columna informativa «Plazo nuevo», se reduce el padding de escritorio de 16 a 8 px verticales y 12 horizontales, se agrupa código y descripción y se eliminan saltos de línea redundantes. Se conserva el contenido completo, checklist, calendario, selección, permisos y formularios. «No requiere especialización» se abrevia como «No requiere» bajo su encabezado.

La petición actual sustituye el espaciado histórico de la guía visual únicamente en esta tabla. Se reutilizan tabla, tokens y disclosures existentes; los controles desplegables conservan su mínimo de 44 px. La adaptación móvil sigue calculando asociaciones desde los encabezados, sin índices fijos. El margen global configurable y el cierre semanal por progreso no cambian.

Archivos: `src/Sgol.Web/Pages/Tasks/Index.cshtml` y `src/Sgol.Web/wwwroot/css/task-actions.css`. Se preservaron modificaciones previas de la vista (incluido Markdown). No se modifican datos ni esquema.

| Escenario | Resultado esperado | Prueba / entorno | Estado |
|---|---|---|---|
| Compilar Razor y estilos | Compilación correcta | `dotnet build src/Sgol.Web/Sgol.Web.csproj --no-restore -o .local/runtime/tasks-compact-20261103 -v minimal`; log `.local/tasks-compact-build.log` | Aprobado: 0 errores, 0 advertencias |
| Correspondencia de columnas y controles | 10 encabezados y celdas; mismos selectores y formularios | Revisión del diff respecto a copia anterior local | Aprobado por inspección, sin interacción |
| Escritorio/móvil, temas, teclado, textos largos y desplegables | Menor altura sin recortes ni pérdida de controles | Navegador | No ejecutado: usuario pidió continuar sin entrar al navegador |
| Reglas, datos e historial | Sin cambios | Diff de presentación | No aplicable: no hay cambios funcionales |
| Host actualizado | Ejecutar el nuevo ensamblado | Sustitución del proceso web de simulación | No ejecutado: revisión automática rechazó el comando con «blocked by policy», sin detalle adicional |

Host observado: runner 26316, proceso 23780, `.local/runtime/refresh-compact-20261103/Sgol.Web.dll`, LAN `https://192.168.1.92:7440`, backend `127.0.0.1:7241`. Antes del intento respondió readiness `ready`. El intento rechazado no ejecutó comandos; permanece el ensamblado anterior. Artefacto nuevo preparado en `.local/runtime/tasks-compact-20261103/`. No se afirma validación visual ni despliegue completo.

## Activación posterior — 2026-11-03

Tras la nueva instrucción explícita de reiniciar solo el host, la sustitución se ejecutó correctamente. Runner web 32012 y proceso dotnet 31084 sirven `.local/runtime/tasks-compact-20261103/Sgol.Web.dll`; se conservó el proxy. Comprobaciones HTTP aprobadas: backend `/health/ready` devuelve `ready`, LAN `/acceso` responde 200 y `/css/task-actions.css` contiene los estilos compactos. Esto sustituye el estado de despliegue pendiente de la tabla anterior. No se abrió navegador ni se ejecutó validación visual.
