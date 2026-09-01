# SGOL — Adenda 05 a F07: plantilla de tarea vigente

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tarea de planeación | `TOOL-PLAN-001` |
| Tipo | Adenda de actualización de la plantilla de implementación de F07 |
| Estado | PROPUESTA |
| Fecha | 2026-08-31 |
| Efecto | Amplía F07 mediante una plantilla vigente; no modifica ni sobrescribe el documento aprobado |
| Conservación | Mantiene sin cambios `F07_PLANTILLAS_PARA_CODEX.md` y los demás entregables F00–F07 aprobados |
| Eficacia | Pasa a `APROBADA` al incorporarse a `master`; desde ese momento su contenido es obligatorio |

## 2. Motivo y sección sustituida

La plantilla original de `F07_PLANTILLAS_PARA_CODEX.md`, sección `2. Plantilla de implementación` (líneas 12–50 de la copia congelada), no contempla el preflight, el índice de IDs, las autorizaciones previas de rama y pull request ni la ejecución externa de las pruebas de integración. Esta adenda conserva el documento aprobado y sustituye únicamente el uso operativo de esa sección a partir de su eficacia.

## 3. Plantilla rescatada literal

```text
Implementa exclusivamente [ID] — [resultado concreto].

Ejecuta scripts/ci/preflight.ps1 y aplica el inicio incremental de AGENTS.md.
Lee docs/traceability/IMPLEMENTATION_STATUS.md y localiza en docs/INDICE_IDS.md
únicamente [ID] y sus referencias autorizadas. No leas ningún documento completo.

Acepta como evidencia previa todas las tareas registradas como Terminadas. No
repitas sus análisis ni sus gates. No recalcules la huella de Fuentes/.

Rama: codex/[slug].

Alcance:

- [resultado observable único]
- [segundo elemento si el incremento vertical lo exige]

Fuera de alcance: [lo adyacente que NO debe tocarse].
No inventes campos, estados, permisos, eventos ni contratos. Si un dato
indispensable no está definido en los documentos aprobados, detén esa parte y
preséntame la contradicción.

Pruebas exigidas: [positiva, negativa, autorización, auditoría, no-efecto según
aplique].

Autorizaciones previas de esta tarea, no vuelvas a pedirlas:
publicar la rama en SistemasTechLead/sgol y crear el pull request con gh.
Detente únicamente antes del merge y espera mi aprobación explícita.

Gates: restore bloqueado, build Release, suite unitaria y de arquitectura,
dotnet format, dependencias y secretos. Una sola ejecución, al final.
Las pruebas de integración con PostgreSQL NO las ejecutas: escríbelas y pídeme
su resultado en un único mensaje cuando llegues a los gates.

Antes de editar, resume en máximo seis puntos: alcance, dependencias, fuentes,
archivos previstos y gates. No narres comprobaciones de entorno.

Entrega un solo bloque final: archivos cambiados, criterios satisfechos, gates
con resultado, número de PR, riesgos y decisiones humanas necesarias. Sin
confirmaciones intermedias.
```

## 4. Regla de precedencia

Para toda tarea F08 iniciada a partir de la incorporación de esta adenda, la plantilla vigente es la de `F07_ADENDA_05_PLANTILLA_DE_TAREA_VIGENTE.md`. La sección `2. Plantilla de implementación` de `F07_PLANTILLAS_PARA_CODEX.md` queda como referencia histórica y no se usa.

## 5. Aprobación y eficacia

Esta adenda permanece como `PROPUESTA` mientras no esté incorporada a `master`. Al incorporarse a `master`, pasa a `APROBADA` y desde ese momento su contenido es obligatorio para las tareas F08 alcanzadas por la regla de precedencia.
