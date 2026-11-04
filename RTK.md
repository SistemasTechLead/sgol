<!-- rtk-owned: written by `rtk init --codex`, removed by `rtk init --codex --uninstall` -->

# Salidas de comandos en SGOL

Usar explícitamente `rtk git status`, `rtk git diff --stat`, `rtk rg`,
`rtk dotnet build`, `rtk dotnet test` y `rtk dotnet restore` en comandos
compatibles. Conservar argumentos, directorio y código de salida. El hook
local de Codex es una ayuda; no suponer que intercepta `exec_command`.

RTK filtra la salida, no garantiza que conserve todos los detalles. Para
revisar un diff completo, contar coincidencias, procesar JSON, leer instrucciones
o comprobar evidencia exacta, usar el comando original o `rtk proxy <comando>`.
Ante fallos, ambigüedad o recortes, recuperar el detalle necesario; no repetir
una operación con efectos solo para obtener su salida. Guardar logs privados
en `.local/` cuando se necesite conservar evidencia completa desde el inicio.

No usar `--skip-env` ni resumidores genéricos para ocultar validaciones.
No envolver el script de Graphify: ya limita su salida y verifica vigencia.
Consultar `rtk gain -p` cuando se quiera medir el uso; sus tokens son estimados,
no equivalen al ahorro de una conversación completa.
