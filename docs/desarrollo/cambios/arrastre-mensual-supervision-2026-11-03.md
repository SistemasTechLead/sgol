# Arrastre mensual desde Supervisión — 2026-11-03

## Alcance y decisión
Cambio mixto solicitado: las mensuales pueden buscar y resolverse hasta un mes de calendario desde la solicitud, inclusive; confirmado por el usuario. Aplica a pendientes existentes sin reescribir su snapshot. Del 3/11 al 3/12; 31/1 se ajusta al último día de febrero. Las demás conservan siete días. Los límites explícitos «antes del» siguen excluyendo ese día.

## Recorrido e integración
- `OperationalCarryover.Postponement.cs`: productor, previsión de carga, solicitud, aprobación integral y asignador. PeriodType pertenece a la asignación; no se consulta el catálogo mutable para reclasificarla. La selección mixta usa un horizonte común y un límite por tarea.
- `OperationalCarryover.Resolution.cs`: vista previa y confirmación individual comparten el límite efectivo con `_PostponementRequest.cshtml`. El snapshot original permanece visible como ventana original.
- `MyWork/Carryover.cshtml`: explica ambos rangos y previsión.
- La previsión repite el día equivalente más reciente hasta hoy si no existe plan semanal de destino. No convierte una semana futura vacía en capacidad libre. Mantiene demanda mensual/procesos adicional y excluye arrastres reconocidos de la demanda semanal repetida.
- Migración `20261104010000_AddMonthlyCarryoverWindow`: amplía únicamente la restricción de intervalo para versión de algoritmo >=4. Sin actualización de filas; reversión bloqueada si existe historia mensual incompatible.
- Se mantienen autorización, elegibilidad, capacidad, horarios, progreso/evidencias, día original del bono, concurrencia, transacción y reintentos. No cambia el circuito de correcciones ni procesos.

## Entorno
Rama master con cambios previos en actualización, validación y documentación; conservarlos. Host observado: lanzador 18060, Web 37352 en 127.0.0.1:7241, publicación `.local/runtime/validation-conclusions-20261103`, proxy 20936. Base compartida sgol_local en localhost:5440. Pruebas en copia independiente `sgol_task_checks_monthly_20261103`; no asignar tareas de la simulación compartida.

## Matriz
| Escenario | Esperado | Método/entorno | Estado |
|---|---|---|---|
| Mes calendario, febrero y bisiesto | Fecha inclusiva correcta | Reglas locales | Aprobado |
| Límite explícito exclusivo | No ampliarlo | Reglas locales | Aprobado |
| Selección mixta y sin capacidad inicial | Solo mensual puede salir de siete días | Asignador real/local | Aprobado |
| Pendiente histórico | Extender límite efectivo sin modificar original | Reglas locales | Aprobado |
| Alta mensual y persistencia | Solicitud hasta 3/12 | Servicios/SQL aislados | Aprobado tras migración; fallo inicial CK_carryover_interval corregido |
| Reprogramar/reasignar al 30/11 | Guardar con capacidad y horario | Servicios/SQL aislados | Aprobado |
| Envío fuera de mes y semanal fuera de semana | Rechazo sin mutación | Servicios/SQL aislados | Aprobado |
| Reintento, concurrencia, permisos y entrega durante espera | Conservar invariantes | Suite de resolución/SQL aislado | Aprobado |
| Formularios, CSRF y fecha efectiva | Vista previa y confirmar | Host aislado con autenticación sustituida | Aprobado |
| Tamaños/temas y teclado | Mantener editor funcional | Chromium/WebKit aislados | Aprobado |
| Migración repetida e historia | Sin cambio de registros | SQL aislado | Aprobado |
| App compartida | Artefacto y acceso verificados | Publicación, readiness y acceso local/LAN | Aprobado; flujo autenticado compartido no ejecutado |

## Documentación vigente
Actualizadas en la bóveda las referencias de arrastres, patrones del editor, instalación y base documental tras activar. Índice actualizado: 42 notas, 286 nodos y 542 relaciones. La copia versionada de arrastres coincidía antes de editar y se actualizó con enlaces relativos. Las otras tres copias presentan divergencias previas: no se sobrescribieron. No se realizó commit/push.

## Resultado final y evidencia
- Compilación: 12 proyectos, 0 errores y 0 advertencias; publicación Release correcta.
- Reglas: 16 aprobadas (`--postponement-rules`). Resolución: 30 regresiones y 16 comprobaciones mensuales aprobadas en `.local/monthly-carryover-checks-final2.log` (`--carryover-resolution`).
- Solicitud histórica versión 2: resolución exactamente el 3/12 persistida, ventana original y snapshot conservados. La primera aserción comparó JSON antes de su normalización jsonb; se corrigió para comparar lectura SQL antes/después y pasó. No fue una mutación del producto.
- Chromium/Edge y WebKit: 320/768/1440 px, light/dark/special, teclado, selector mensual/semanal, sin desbordamiento ni errores JS. 18 combinaciones y un recorrido real de revisión/confirmación al 30/11 con rechazo CSRF: `.local/monthly-carryover-ui/results.json`, capturas en la misma carpeta. Formularios reales con identidad de prueba; no prueba de login/MFA.
- Migración aplicada tras respaldo privado `.local/monthly-carryover-before-activation.dump`. Solo actualiza constraint y registro de migración. Huellas completas idénticas de 789 asignaciones y 13 solicitudes: `.local/monthly-carryover-before.txt` y `-after.txt`.
- Publicación activa: `.local/runtime/monthly-carryover-20261103/Sgol.Web.dll`, lanzador 30188, proceso 38004. Proxy 31004/lanzador 20936 conservado. Readiness interno 200, acceso LAN 200 y localhost 302 seguido de 200, TLS normal. Logs privados de migración y arranque en `.local/monthly-carryover-*.log`.
- No se asignó ni reprogramó ninguna tarea compartida. Pantalla autenticada compartida y dispositivo físico: no ejecutados; la comprobación interactiva se realizó en el host aislado. No cambios en catálogo, bono, evidencia, autores o procesos.