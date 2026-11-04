---
categoria: desarrollo
tipo: procedimiento
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: CLI local Windows; Git, rg y compilacion aislada
---

# Reducir las salidas de comandos con RTK

[Desarrollo](../../mapas/desarrollo.md) · [Reglas documentales](../../organizacion/reglas.md)

RTK 1.1.0 está instalado mediante WinGet (`rtk-ai.rtk`) y disponible como `rtk` en PATH. El proyecto registra el hook nativo `rtk hook codex` en `.codex/hooks.json`, creado por `rtk init --codex`. No modifica el perfil global de PowerShell ni la aplicación SGOL.

## Uso por el agente

Las instrucciones del proyecto requieren invocar explícitamente `rtk` para comandos compatibles. El hook generado usa `PreToolUse` con matcher `Bash`; su ejecución depende de que el cliente lo cargue y del nombre de la herramienta. No se da por interceptado `exec_command`. Después de registrar el hook puede ser necesario reabrir el chat o el cliente y aceptar la confianza si el cliente la solicita; el uso explícito funciona sin esa interceptación.

```powershell
rtk git status
rtk git diff --stat
rtk rg -n 'AssignmentRefreshService' src
rtk dotnet build src/Sgol.Web/Sgol.Web.csproj --no-restore
rtk gain -p
```

Los ejemplos de compilación no autorizan publicar ni reiniciar la app. Conservar los argumentos adecuados a la tarea y comprobar `$LASTEXITCODE` después de ejecutar. `rtk dotnet test` y `rtk dotnet restore` también tienen filtros propios; la integración no reemplaza las pruebas de SGOL.

## Límites y recuperación

- Para revisar un diff completo, leer instrucciones, contar coincidencias o procesar salida estructurada, usar el comando original o `rtk proxy <comando>`. El filtro puede omitir información útil aunque devuelva éxito.
- Recuperar detalles ante errores, ambigüedad o truncamiento. No volver a ejecutar operaciones con efectos únicamente para recuperar su salida: guardar logs completos privados desde el inicio cuando el recorrido lo requiera.
- No usar `--skip-env`. No filtrar de nuevo `consultar-boveda.ps1`: [Graphify](consulta-boveda-graphify.md) ya entrega contexto acotado y realiza comprobaciones de vigencia.
- `rtk gain -p` muestra estimaciones de ahorro de salidas para el directorio actual; no es una medida de tokens totales del chat. Una salida ya breve puede no reducirse.
- En 1.1.0 RTK todavía puede emitir una recomendación de instalación global aunque exista el archivo de hook del proyecto. Ese mensaje no demuestra que el cliente haya cargado o ejecutado el hook local; comprobar las llamadas reales.

## Mantener la integración

La configuración propietaria es [RTK.md](../../../../RTK.md), enlazada desde [AGENTS.md](../../../../AGENTS.md). RTK.md adapta la plantilla oficial para exigir uso explícito y evitar tratar resúmenes como evidencia completa. Al volver a ejecutar `rtk init --codex`, revisar el diff porque puede regenerar la plantilla y añadir su referencia automática.

Si el ejecutable falta en otra máquina, instalar `winget install --id rtk-ai.rtk -e` y verificar `rtk --version`. No reinstalar ni actualizar automáticamente si ya funciona. No cambiar permisos ni instalar integración global para resolver un aviso del hook local.

Fuentes: [repositorio oficial](https://github.com/rtk-ai/rtk), [integración de agentes y Windows](https://www.rtk-ai.app/docs/getting-started/supported-agents/) y ayuda del ejecutable instalado. La compilación aislada correcta y el fallo MSBuild conservaron sus resultados; no se afirma validación de todos los filtros ni ahorro neto de conversaciones.
