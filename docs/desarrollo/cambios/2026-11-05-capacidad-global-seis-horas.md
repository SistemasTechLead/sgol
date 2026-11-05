# Capacidad global de seis horas

Cambio funcional solicitado el 05/11/2026: establecer 360 minutos diarios como estándar de colaboradores actuales y nuevos.

## Alcance e impacto

`OperationalCapacity.DefaultDailyMinutes` pasa de 240 a 360. `CapacityLedger.CalculateDayCapacity` usa esa referencia cuando no existe ajuste individual, con límite por horario disponible, ausencias y vigencia laboral/rol. `PlanningCapacity.ReadAsync`, la consulta de capacidad y transferencias, y `CapacityForm` consumen la misma constante. Las huellas de planificación y redistribución incluyen el estándar y exigen recalcular propuestas anteriores.

Se conservan ajustes explícitos históricos, porcentajes, máximo configurable de 480 minutos, asignaciones, responsables, cargas, evidencia e historial. No se generan tareas ni se redistribuyen automáticamente. Las consultas que calculan capacidad sin ajuste usan la nueva referencia incluso para fechas anteriores; no existe una versión fechada del estándar. No se modifican los snapshots históricos de asignaciones o ajustes de planes.

Inspección del destino compartido: PostgreSQL local, puerto 5440, base `sgol_local`; ninguna fila de `operational_capacity` vigente o futura. Publicación inicial: `carryover-auto-direction-20261105`, Web 37956, lanzador 35604, proxy 20652. No se requiere migración.

Se reutiliza la copia de compilación de la publicación activa para conservar sus cambios previos sin incorporar trabajo concurrente del checkout. Se copian únicamente la constante y las dos pruebas modificadas. No se realiza commit ni push.

## Matriz de validación

| Escenario | Resultado esperado | Entorno/prueba | Estado |
|---|---|---|---|
| Base y límite | 360 disponibles; 360 caben, 361 se rechazan | CapacityRuleChecks | Aprobado |
| Ausencia y ajuste histórico | Ausencia 0; ajuste 50% de 240 conserva 120; al vencer vuelve a 360 | CapacityRuleChecks | Aprobado |
| Nuevos ajustes y formulario | Base inicial 360 | Reglas y GET Razor real | Aprobado |
| Confirmación y conservación | Ajuste se persiste; redistribución elegible, progreso e historial conservados | WorkloadHttpChecks, PostgreSQL aislado y sesión sintética | Aprobado |
| Integridad | Reintento, concurrencia, CSRF y permisos siguen vigentes | WorkloadHttpChecks | Aprobado |
| Publicación | DLL probada, salud interna y acceso HTTPS local/LAN | Host compartido | Aprobado |
| Presentación visual | Sin cambio de estructura o estilos | No aplicable; cambia un valor numérico renderizado | No aplicable |
| Sesión compartida autenticada | No se introducen ajustes de prueba compartidos | Host compartido | No ejecutado |

Evidencia local: `.local/capacity6-20261105/`. Activado y bóveda reconciliada. Compilación: 0 errores y 0 advertencias; ocho comprobaciones de reglas y 25 HTTP/SQL aprobadas. Web 22788, lanzador 27128, proxy 20652 conservado. SHA-256 de publicación igual a DLL probada: 9443553FA1114F59DD27EC6819F95564EF73A1F466B4152DD35E6558ABBA429A. Salud interna y acceso local/LAN: HTTP 200. Sin migración ni escrituras operativas compartidas.
