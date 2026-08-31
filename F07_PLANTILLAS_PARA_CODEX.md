# F07 — Plantillas para tareas de Codex en SGOL

## 1. Control

| Campo | Valor |
|---|---|
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Regla | Sustituir todos los marcadores; no enviar una plantilla incompleta |
| Unidad | Un chat de Codex por historia o incremento de F08 |

## 2. Plantilla de implementación

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

## 3. Plantilla de corrección de defecto

```text
Corrige [DEF-### — síntoma] dentro de [HU/TECH].

Evidencia reproducible:
- Entorno/versión: [dato]
- Pasos: [pasos]
- Resultado observado: [dato]
- Resultado esperado: [CA/CP/NFR]

Fuentes autorizadas:
- [documentos y secciones]

Límites:
- Corrige la causa mínima; no amplíes alcance ni refactorices áreas ajenas.
- Conserva datos, historia y compatibilidad.
- No modifiques Fuentes/.

Primero reproduce el defecto con una prueba fallida. Después corrige, ejecuta regresión y reporta causa, archivos, pruebas, riesgos y trazabilidad actualizada.
```

## 4. Plantilla de revisión independiente

```text
Revisa el cambio [ID/rama/commit] contra las fuentes aprobadas de SGOL.

Verifica específicamente:
- alcance y criterios [IDs];
- permisos, jerarquía e IDOR;
- estados, historia, auditoría y no-efecto;
- idempotencia, concurrencia y transacción cuando apliquen;
- contrato API, migraciones, archivos y secretos cuando apliquen;
- pruebas y trazabilidad;
- ausencia de cambios en Fuentes/ y de capacidades no autorizadas.

No modifiques el código. Entrega hallazgos ordenados por severidad con archivo/línea, evidencia, impacto y criterio incumplido. Si no hay hallazgos, indica los riesgos residuales y verificaciones no ejecutadas; no inventes conformidad.
```

## 5. Plantilla de migración de esquema

```text
Implementa la migración compatible [TECH/ID] requerida por [HU].

Modelo aprobado:
- F06_MODELO_DE_DATOS.md, [secciones/tablas]

Contrato de compatibilidad:
- La versión anterior y la nueva deben coexistir durante el despliegue.
- No elimines ni renombres destructivamente columnas/tablas en esta liberación.
- Conserva IDs, versiones, auditoría e historia.

Incluye migración hacia adelante, restricciones/índices, prueba con PostgreSQL real, aplicación sobre esquema previo, rollback de aplicación cuando sea compatible y documentación del paso operativo. No uses SQLite y no modifiques Fuentes/.
```

## 6. Plantilla de cierre de corte

```text
Valida el cierre de [CV-##] sin implementar funciones nuevas.

Entradas:
- Historias terminadas: [IDs]
- Criterios del corte: [IDs]
- Evidencia disponible: [rutas/ejecuciones]

Ejecuta la demostración vertical y los gates de cierre definidos en F07_DEFINICIONES_DE_CONTROL.md. Comprueba permisos positivos/negativos, auditoría, no-efecto, trazabilidad y ausencia de cambios en Fuentes/.

Entrega un dictamen: APTO, NO APTO o BLOQUEADO, con evidencia y defectos. No cierres el corte por porcentaje de pruebas ni por ausencia de reportes.
```

## 7. Reglas para completar las plantillas

- Citar IDs exactos; no usar “según documentación” sin ubicación.
- Incluir sólo fuentes necesarias para la tarea, manteniendo F04–F06 como autoridad de alcance, función y técnica.
- Convertir cada resultado ambiguo en una comprobación observable antes de abrir el chat.
- Si una tarea no cabe razonablemente en un cambio revisable, dividirla conservando un resultado vertical útil.
