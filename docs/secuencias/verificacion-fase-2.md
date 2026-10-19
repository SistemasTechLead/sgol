# Evidencia de fase 2

Fecha: 13 de octubre de 2026. Estado: esquema y reglas verificados en copias dedicadas.

## Resultados

| Verificación | Resultado | Evidencia |
| --- | --- | --- |
| Compilación de Web y TaskChecks | 0 advertencias, 0 errores | [Log](../../outputs/secuencias/task-build.log) |
| Reglas y esquema | 122 líneas PASS, incluido calendario, Dirección 168 h, intervalos, concurrencia estructural, FKs, inmutabilidad y reversión protegida | [Log](../../outputs/secuencias/schema-checks.log) |
| Tareas, cámara, supervisión, capacidad, especialización, borrado y ocasiones | 359 líneas PASS, incluyendo las reglas nuevas | [Log](../../outputs/secuencias/task-regression-checks.log) |
| Tablero Vivo y Mi trabajo | 54 comprobaciones | [Log](../../outputs/secuencias/board-checks.log) |
| Reportes | 75 comprobaciones | [Log](../../outputs/secuencias/report-checks.log) |
| Matriz de pantallas por rol | Cuatro roles y acceso anónimo pasan | [Log](../../outputs/secuencias/role-checks.log) |
| Código del prototipo | 19 simulaciones aisladas; sin navegador ni layout | [Log](../../outputs/secuencias/prototype-source-checks.log) |
| Servicio existente | `/health/ready`: `ready` durante la verificación | Lectura local HTTPS; no es activación del piloto |

El conteo de reglas/esquema y el de tareas comparten las reglas puras. No se suman
como comprobaciones distintas. La matriz de pantallas existente no prueba todavía
la autorización contextual de documentos de secuencias, que se implementará en fase 5.

## Preservación y recuperación

Se recuperó el respaldo de `sgol_local` en
`sgol_task_checks_sequences_20261013_1947`. Los fixtures de integridad se ejecutaron
en transacciones con rollback, y las filas sintéticas de la prueba de reversión
protegida se retiraron de esa copia por sus IDs exactos.

Las [huellas anteriores](../../outputs/secuencias/historical-before-upgrade.json)
y [posteriores](../../outputs/secuencias/historical-after-checks.json) coinciden.
Cubren cada fila y los bytes de las tablas operativas enumeradas en la prueba.
El respaldo contiene las 35 asignaciones del origen al capturarlo.

Respaldo: `outputs/secuencias/sgol-local-before-sequences.dump`.
SHA-256: `b915df8725350a6d6df807030548f2171f9b4aa52f755591d131c4864007e3ec`.
Es un archivo local de recuperación con datos del sistema; no se publica junto al prototipo.

Las pruebas generales usaron una base vacía dedicada:
`sgol_task_checks_sequences_regression_20261013_r4`. Las bases de pruebas se conservaron
para revisar la evidencia. Ninguna prueba de migración se dirigió a `sgol_local`.

## Límites de este avance

La migración no está aplicada al entorno de uso diario. El piloto no se configuró
con tareas o personas reales ni hay APIs o pantallas que ejecuten el proceso completo.
Faltan integración de reservas, transacciones operativas, temporizador recuperable,
permisos contextuales, descarga por versión e interfaz final.

El esquema demuestra que las referencias, fechas y transacciones pueden conservarse;
las pruebas de reglas demuestran sus decisiones puras. No se ha demostrado todavía
el flujo concurrente completo de cuatro roles, los reinicios del temporizador,
la falta de personal en una ejecución real ni la descarga autenticada de versiones.

El prototipo es una propuesta anticipada. El servidor local fue rechazado por revisión
automática y el navegador no admite abrir un archivo `file:`. Su aspecto en escritorio
y pantalla estrecha necesita revisión manual; no se declara validación visual, hardware o LAN.

Posteriormente, por solicitud del usuario, se generó una alternativa visual en
`output/pdf/secuencias-inventario/propuesta-visual-inventario.pdf`. Sus cuatro páginas
se renderizaron a PNG y se inspeccionaron. Esta comprobación valida la presentación
del PDF; no demuestra comportamiento de navegador, autenticación, hardware o LAN.
El usuario aprobó después el diseño como plantilla para cada proceso. La interfaz
real y su comportamiento siguen pendientes. La pertenencia de tareas, General y
el editor de procesos se incorporan al [plan unificado](../plan-procesos-de-trabajo.md);
las verificaciones de este registro corresponden al motor base anterior a ese ajuste.
