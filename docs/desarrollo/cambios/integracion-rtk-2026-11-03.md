# RTK en SistemaSGOL — 2026-11-03

Cambio de herramientas de desarrollo y documentación. RTK 0.50.0 ya estaba instalado por WinGet (`rtk-ai.rtk`) y disponible en PATH; no había referencias locales al proyecto ni configuración local de hook. Se reutilizó la instalación.

`rtk init --codex` creó RTK.md, `.codex/hooks.json` y una referencia en AGENTS.md. Se adaptó RTK.md y se sustituyó la referencia automática por instrucciones explícitas del proyecto: Git/rg/.NET mediante RTK, salida original para comprobaciones exactas, sin `--skip-env` y sin doble filtro de Graphify. La plantilla oficial afirmaba que la salida condensada era completa; esa garantía no se mantiene. `--auto-patch` no es compatible con `--codex` en esta versión; el intento fue rechazado sin cambios y se ejecutó la forma válida.

Se agregó el procedimiento vigente a la bóveda, mapa e inventario, y se actualizó su copia versionada tras revisar las notas actuales. No se tocaron cambios funcionales previos, perfiles globales, app compartida ni datos. Sin commit/push.

| Escenario | Esperado / prueba | Entorno | Estado |
|---|---|---|---|
| Instalación existente | rtk --version: 0.50.0; resolución desde paquete WinGet | Local | Aprobado |
| Configuración | dry-run y creación de hook local + instrucciones | Proyecto | Aprobado |
| Reescritura | hook check --agent codex convierte git status y dotnet build | CLI local | Aprobado |
| Git/rg | Salida legible; coincidencia real con línea | Repositorio | Aprobado |
| Compilación correcta | Resumen 1 proyecto, 0 errores/advertencias, exit 0 | .local/rtk/fixture | Aprobado |
| Compilación fallida | MSB1009 visible y exit 1 | Ruta de proyecto inexistente aislada | Aprobado |
| Métricas | gain -p registra comandos del directorio actual | Local | Aprobado |
| Activación automática en chat nuevo | Comprobar ejecución efectiva del hook por cliente | Codex tras recarga | No ejecutado; invocación explícita configurada |
| Todos los filtros / ahorro neto | Benchmark de conversaciones y filtros no usados | No realizado | No ejecutado |
| UI/BD/despliegue | Sin impacto funcional en SGOL | No aplica | No aplicable |

Las invocaciones manuales del procesador del hook con payload mínimo no devolvieron reescritura; no se presentan como prueba de interceptación del cliente. El aviso de RTK sobre falta de hook global persiste pese al registro local. La integración efectiva disponible en este chat es el prefijo explícito, que ya se utilizó.

Referencias: [RTK oficial](https://github.com/rtk-ai/rtk), [agentes soportados](https://www.rtk-ai.app/docs/getting-started/supported-agents/) y ayuda local de 0.50.0.

## Actualización autorizada a 1.1.0

WinGet actualizó el paquete desde la publicación oficial y verificó el hash del instalador. rtk --version confirma 1.1.0. La configuración local y RTK.md se conservaron. Se repitieron satisfactoriamente la reescritura de git status, compilación aislada correcta (exit 0), compilación inexistente (MSB1009, exit 1) y búsqueda rg. El aviso de falta de hook sigue apareciendo en comandos normales; la activación automática del cliente continúa sin verificarse. Se actualizó el procedimiento vigente en la bóveda y su copia versionada.
