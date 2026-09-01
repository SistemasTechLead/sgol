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
Implementa [ID — resultado concreto] del backlog aprobado de SGOL.

Corte y épica:
- [CV-##]
- [EP-##]

Fuentes autorizadas:
- [documento, sección y IDs]
- [documento, sección y IDs]

Resultado requerido:
- [resultado observable único]

Criterios de aceptación y pruebas:
- [CA/CP/CAT/NFR]
- [caso positivo]
- [caso negativo, autorización, auditoría y no-efecto aplicables]

Dependencias terminadas:
- [IDs]

Límites:
- No implementes capacidades posteriores, opcionales o fuera de alcance.
- No renombres, muevas, sobrescribas ni elimines archivos de Fuentes/.
- No cambies decisiones aprobadas; si detectas contradicción, detén esa parte y repórtala.

Trabajo esperado:
1. Inspecciona el repositorio y confirma brevemente alcance, dependencias y plan de verificación.
2. Implementa el incremento vertical mínimo, incluidos persistencia, autorización, auditoría, API/UI y documentación sólo donde apliquen.
3. Añade las pruebas y trazabilidad requeridas.
4. Ejecuta los gates aplicables de F07_DEFINICIONES_DE_CONTROL.md.
5. Entrega archivos cambiados, criterios satisfechos, comandos/resultados, riesgos y pendientes.

No declares Terminado si falta un gate o aprobación requerida.
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
