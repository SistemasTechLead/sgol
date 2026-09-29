# F07 Adenda 52 — Optimización del pipeline TECH-BASE-003

## Control y autorización

| Campo | Valor |
|---|---|
| Alcance | Optimización técnica de `TECH-BASE-003`, fuera del orden de historias funcionales |
| Fecha | 2026-09-29 |
| Autorización de ejecución | El responsable solicitó un plan para optimizar el pipeline sin cambiar la construcción de tareas y con la misma seguridad; después del plan ordenó literalmente: «Vamos con la ejecucion entera del plan» |
| Estado | Implementación autorizada en esta conversación; propuesta de integración hasta revisión humana y merge |
| Base | `origin/master` `9518597dcb4e4d321a82285f31a3b4c22773b536` |
| Fuentes | F07 fila `TECH-BASE-003`; F06 estrategia de pruebas sección 3; `ADR-015`; Adendas 32, 34, 36, 42 y 43; workflow vigente |
| Protección | F00–F07 congelados y `Fuentes/` permanecen sin cambios |

Esta adenda documenta únicamente la reorganización autorizada del pipeline. No cambia la lógica funcional, contratos, dependencias de historias, requisitos de evidencia, aprobación humana ni condiciones de cierre formal. No modifica `AGENTS.md` ni adopta mediante este cambio otro flujo de construcción de tareas.

## Hechos y medición de base

Tres jobs verdes: run `36639787899`, 1 508 segundos; run `36609761032`, 1 609 segundos; run `36495586807`, 1 515 segundos. El workflow consultado coincidía con `master` remoto. En el primero: suite servidor 426 s, navegador 410 s, HU-035 219 s, OCI 76 s, Trivy 30 s, TECH-OPS 72 s, CV-04 42 s. Las ramas históricas PR #58/CV-05 y el experimento manual no ejecutaron sus caminos condicionales en esos PR ordinarios.

El navegador y las suites que administran redes Docker deben tener runners distintos. El historial de `TECH_FRONT_004_HARNESS.md` identifica `ERR_NETWORK_CHANGED` al compartir ejecución. No se habilita paralelismo entre estos bloques dentro de un mismo runner.

## Decisión de organización

1. `controls` verifica checkout exacto, protección de fuentes, secretos, contrato portátil, controles condicionales CV-05 y las pruebas puras del consolidador.
2. Tras `controls`, `server` y `browser` corren en runners separados. Ambos restauran con lock y construyen Release con el SDK exacto; no transfieren binarios ni credenciales entre runners.
3. `server` conserva la suite general, EICAR, sus filtros, formato y consulta de vulnerabilidades directas/transitivas.
4. `browser` conserva instalación Chromium/WebKit, toda la categoría `FRONT_BROWSER`, datos sintéticos y cleanup. No se reduce a un smoke parcial ni se cambia su frecuencia.
5. `operations` espera `controls`, `server` y `browser`. Restaura/construye en su propio runner y ejecuta CV-04 después de pruebas y antes de los gates operativos, una sola vez y sin parámetros adicionales.
6. La imagen, SBOM/procedencia, Trivy, TECH-OPS, cleanup, probe y HU-035 se ejecutan en ese mismo runner AMD64. Se conserva imagen → TECH-OPS → HU-035 y la identidad exacta de la imagen.
7. Las condiciones de PR #58 y rama CV-05, comandos, reportes, inventario, digest, retención y verificación de artifacts permanecen íntegros en `operations`. CV-04 y CV-05 no requieren transferencia de evidencia entre jobs.
8. `verify` conserva el nombre `TECH-BASE-003 / PR gates`, se evalúa con `always()` para PR y sólo aprueba cuando los cuatro jobs exigidos son `success` y sus outputs `validated_sha` coinciden literalmente con la cabeza exacta. Fallo, cancelación, omisión, output ausente, otro SHA o conjunto incompleto fallan cerrado.
9. Concurrencia por número de PR cancela ejecuciones de cabezas sustituidas. Cada nueva cabeza ejecuta sus propios gates. El experimento manual mantiene su grupo y secuencia A/B; no acredita aceptación.
10. Todos los checkouts conservan `persist-credentials: false`, SHA exacto y permisos mínimos. Sólo `operations` requiere `actions: read` para verificar artifacts CV-05; no se añaden permisos de escritura, secretos ni despliegue.

La restricción contractual de CV-04 permanece: operaciones no comienza antes de las suites. Por ello 10–14 minutos no es un compromiso. El ahorro esperado principal corresponde al solapamiento de servidor y navegador, descontando preparación y colas adicionales; debe medirse en CI.

## Costos secundarios y límites

HU-035 invoca múltiples procesos `dotnet test`; los logs muestran aserciones de aproximadamente 200 ms con varios segundos entre invocaciones. Esa separación también contiene operaciones de restauración/contenedores: no se atribuye íntegramente al arranque del testhost. Consolidar mutaciones exige cambiar la orquestación y su evidencia por fase y no es necesario para la reorganización mínima. Se conserva todo el orquestador y su matriz.

Restore tarda 10–13 s y SDK 6–9 s. No se agregan cachés, dependencias ni acciones nuevas: su ahorro potencial es menor y no se ha demostrado que compense nuevas superficies de confianza. Las dos salidas OCI actuales usan el mismo builder y sus capas; no se elimina una salida ni su contrato de evidencia. La limpieza de caminos históricos no aporta ahorro medible y no forma parte del cambio.

## Verificación y cierre

Pruebas puras obligatorias para el consolidador: todos verdes con el mismo SHA; fallo/cancelación/omisión/pendiente en cada job; SHA distinto o ausente; JSON inválido; jobs faltantes y adicionales. Un inventario comprueba preservación de cada paso previo y comandos/filtros de seguridad, orden operativo, condiciones de evidencia, permisos y flujo manual.

Los gates .NET completos se verifican en el pipeline del commit publicado. El preflight local informa SDK `10.0.401` instalado y falta del exacto `10.0.400`; no se modifica `global.json` ni se simula un pase local. Las pruebas PostgreSQL locales permanecen a cargo del desarrollador conforme a las instrucciones expresas de esta conversación. El resultado remoto no convierte una prueba local omitida en ejecutada localmente.

Implementación, esta adenda, documentación y registro viajan en el mismo PR. El registro usa `commit que contiene esta actualización`, sin SHA autorreferencial. No se declara Terminada ni se habilitan dependencias mediante este registro antes de pipeline válido, aprobación humana y merge conforme a la Enmienda 001.
