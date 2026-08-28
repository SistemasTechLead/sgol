# F00 — Control del proyecto SGOL

## 1. Identificación del entregable

| Campo | Valor |
|---|---|
| Fase | 00 — Preparación y control del proyecto |
| Entregable | `F00_CONTROL_DEL_PROYECTO.md` |
| Fecha de verificación | 2026-08-26 |
| Estado del control | Completado, aprobado e incorporado a las fuentes del proyecto |
| Fecha de aprobación e incorporación | 2026-08-26 |
| Fuente normativa | `Fuentes\PLAN_EJECUCION_SGOL_CHATGPT_CODEX.md`, secciones 2, 5, 6, 7 y 8 |

## 2. Alcance y límites aplicados

- Se trabajó exclusivamente en preparación, inventario físico, accesibilidad, identificación estable y control de fases.
- No se analizaron funciones, reglas, datos, permisos, estados ni alcance funcional de SGOL.
- No se buscó, abrió, creó ni descomprimió ningún ZIP.
- No se movió, renombró, sobrescribió ni eliminó ningún archivo original.
- No se repitió la búsqueda de duplicados o faltantes históricos. El control se limitó al contenido presente el 2026-08-26.
- Los archivos cuyo nombre comienza con `~$` se excluyeron del conjunto de fuentes.

## 3. Carpeta de fuentes localizada

**Ruta absoluta:** `C:\Users\loret\OneDrive\Documents\ChatGPT\SGOL\Fuentes`

**Subcarpetas recorridas:**

- `Descripcion SGOL`
- `Matrices`

La versión inicial del entregable se guardó fuera de `Fuentes`, en la raíz documental del proyecto, para conservar los originales sin modificaciones. Tras su aprobación, el 2026-08-26 se incorporó una copia idéntica como `Fuentes\F00_CONTROL_DEL_PROYECTO.md`; la copia de control no altera ni renumera las fuentes originales FTE-001 a FTE-044.

## 4. Método de verificación básica

### 4.1 Markdown

Cada archivo `.md` fue abierto en modo de solo lectura y comprobado con estos criterios:

- acceso al archivo;
- tamaño mayor que cero;
- decodificación UTF-8 válida;
- contenido no vacío;
- presencia de un encabezado Markdown de nivel 1.

### 4.2 Excel

Cada archivo `.xlsx` fue importado en modo de solo lectura y se comprobó que expusiera al menos una hoja identificable. Para el libro `.xlsm` se validó directamente su estructura OOXML, la presencia de hojas y del proyecto VBA, sin ejecutar macros ni guardar cambios. Esta es una comprobación de legibilidad básica, no una revisión funcional ni una validación de fórmulas, datos o automatizaciones.

## 5. Resultado cuantitativo

| Tipo | Expectativa aproximada | Total real válido | Diferencia | Resultado |
|---|---:|---:|---:|---|
| Markdown (`.md`) | 20 | 22 | +2 | Conforme con una expectativa aproximada |
| Excel (`.xlsx` y `.xlsm`) | 20 | 22 | +2 | Conforme con una expectativa aproximada |
| Total de fuentes | 40 aprox. | 44 | +4 | Registrado sin inventar archivos |

Desglose de Excel: 21 archivos `.xlsx` y 1 archivo `.xlsm`.

Archivos temporales excluidos por prefijo `~$`: **0**.

Los totales anteriores corresponden al conjunto original inventariado y numerado FTE-001 a FTE-044. Los entregables aprobados de control que posteriormente se incorporen a `Fuentes` se registran aparte y no reciben ni modifican identificadores FTE retroactivamente.

## 6. Convención de identificadores

- Se usa el prefijo permanente `FTE-`, conforme a la sección 8 del plan maestro.
- Los identificadores no deben reutilizarse ni renumerarse en fases posteriores.
- El orden reservado es: plan maestro, libro principal de respaldo y pares Markdown/Excel por número natural de la serie Fase 0 a Fase 20.
- La ruta registrada es relativa a la carpeta `Fuentes` y debe conservarse exactamente.

## 7. Inventario controlado de fuentes

### 7.1 Fuentes generales

| ID | Ruta relativa a `Fuentes` | Tipo | Tamaño (bytes) | Verificación básica |
|---|---|---|---:|---|
| FTE-001 | `PLAN_EJECUCION_SGOL_CHATGPT_CODEX.md` | Markdown | 24,170 | Legible; 646 líneas; encabezado principal presente |
| FTE-002 | `SGOL v2.0 Sistema de Gestion Operativa Loretta - S050_BKP_PRE_NORMALIZACION_V1.xlsm` | Excel con macros | 3,284,411 | Estructura OOXML legible; 83 partes de hoja; proyecto VBA presente; macros no ejecutadas |

### 7.2 Serie de fuentes por fase documental

| ID | Ruta relativa a `Fuentes` | Tipo | Tamaño (bytes) | Verificación básica |
|---|---|---|---:|---|
| FTE-003 | `Descripcion SGOL\SGOL_FASE0_BLOQUE_MAESTRO_V1.md` | Markdown | 9,421 | Legible; 85 líneas; encabezado principal presente |
| FTE-004 | `Matrices\SGOL_FASE0_MATRIZ_MIGRACION_V1.xlsx` | Excel | 82,497 | Legible; 16 hojas identificadas |
| FTE-005 | `Descripcion SGOL\SGOL_FASE1_BLOQUE_MAESTRO_V1.md` | Markdown | 15,414 | Legible; 350 líneas; encabezado principal presente |
| FTE-006 | `Matrices\SGOL_FASE1_MATRIZ_MIGRACION_V1.xlsx` | Excel | 31,084 | Legible; 10 hojas identificadas |
| FTE-007 | `Descripcion SGOL\SGOL_FASE2_BLOQUE_MAESTRO_V1.md` | Markdown | 17,833 | Legible; 430 líneas; encabezado principal presente |
| FTE-008 | `Matrices\SGOL_FASE2_MATRIZ_MIGRACION_V1.xlsx` | Excel | 40,362 | Legible; 12 hojas identificadas |
| FTE-009 | `Descripcion SGOL\SGOL_FASE3_BLOQUE_MAESTRO_V1.md` | Markdown | 12,736 | Legible; 318 líneas; encabezado principal presente |
| FTE-010 | `Matrices\SGOL_FASE3_MATRIZ_MIGRACION_V1.xlsx` | Excel | 42,608 | Legible; 13 hojas identificadas |
| FTE-011 | `Descripcion SGOL\SGOL_FASE4_BLOQUE_MAESTRO_V1.md` | Markdown | 16,228 | Legible; 407 líneas; encabezado principal presente |
| FTE-012 | `Matrices\SGOL_FASE4_MATRIZ_MIGRACION_V1.xlsx` | Excel | 75,332 | Legible; 13 hojas identificadas |
| FTE-013 | `Descripcion SGOL\SGOL_FASE5_BLOQUE_MAESTRO_V1.md` | Markdown | 11,763 | Legible; 261 líneas; encabezado principal presente |
| FTE-014 | `Matrices\SGOL_FASE5_MATRIZ_MIGRACION_V1.xlsx` | Excel | 62,666 | Legible; 13 hojas identificadas |
| FTE-015 | `Descripcion SGOL\SGOL_FASE6_BLOQUE_MAESTRO_V1.md` | Markdown | 9,727 | Legible; 172 líneas; encabezado principal presente |
| FTE-016 | `Matrices\SGOL_FASE6_MATRIZ_MIGRACION_V1.xlsx` | Excel | 101,119 | Legible; 14 hojas identificadas |
| FTE-017 | `Descripcion SGOL\SGOL_FASE7_BLOQUE_MAESTRO_V1.md` | Markdown | 10,817 | Legible; 252 líneas; encabezado principal presente |
| FTE-018 | `Matrices\SGOL_FASE7_MATRIZ_MIGRACION_V1.xlsx` | Excel | 49,037 | Legible; 11 hojas identificadas |
| FTE-019 | `Descripcion SGOL\SGOL_FASE8_BLOQUE_MAESTRO_V1.md` | Markdown | 13,723 | Legible; 316 líneas; encabezado principal presente |
| FTE-020 | `Matrices\SGOL_FASE8_MATRIZ_MIGRACION_V1.xlsx` | Excel | 55,054 | Legible; 12 hojas identificadas |
| FTE-021 | `Descripcion SGOL\SGOL_FASE9_BLOQUE_MAESTRO_V1.md` | Markdown | 13,580 | Legible; 286 líneas; encabezado principal presente |
| FTE-022 | `Matrices\SGOL_FASE9_MATRIZ_MIGRACION_V1.xlsx` | Excel | 43,852 | Legible; 10 hojas identificadas |
| FTE-023 | `Descripcion SGOL\SGOL_FASE10_BLOQUE_MAESTRO_V1.md` | Markdown | 13,372 | Legible; 246 líneas; encabezado principal presente |
| FTE-024 | `Matrices\SGOL_FASE10_MATRIZ_MIGRACION_V1.xlsx` | Excel | 101,457 | Legible; 11 hojas identificadas |
| FTE-025 | `Descripcion SGOL\SGOL_FASE11_BLOQUE_MAESTRO_V1.md` | Markdown | 11,966 | Legible; 258 líneas; encabezado principal presente |
| FTE-026 | `Matrices\SGOL_FASE11_MATRIZ_MIGRACION_V1.xlsx` | Excel | 120,067 | Legible; 13 hojas identificadas |
| FTE-027 | `Descripcion SGOL\SGOL_FASE12_BLOQUE_MAESTRO_V1.md` | Markdown | 14,023 | Legible; 320 líneas; encabezado principal presente |
| FTE-028 | `Matrices\SGOL_FASE12_MATRIZ_MIGRACION_V1.xlsx` | Excel | 131,216 | Legible; 14 hojas identificadas |
| FTE-029 | `Descripcion SGOL\SGOL_FASE13_BLOQUE_MAESTRO_V1.md` | Markdown | 12,544 | Legible; 270 líneas; encabezado principal presente |
| FTE-030 | `Matrices\SGOL_FASE13_MATRIZ_MIGRACION_V1.xlsx` | Excel | 54,469 | Legible; 12 hojas identificadas |
| FTE-031 | `Descripcion SGOL\SGOL_FASE14_BLOQUE_MAESTRO_V1.md` | Markdown | 11,599 | Legible; 242 líneas; encabezado principal presente |
| FTE-032 | `Matrices\SGOL_FASE14_MATRIZ_MIGRACION_V1.xlsx` | Excel | 20,862 | Legible; 10 hojas identificadas |
| FTE-033 | `Descripcion SGOL\SGOL_FASE15_BLOQUE_MAESTRO_V1.md` | Markdown | 13,467 | Legible; 340 líneas; encabezado principal presente |
| FTE-034 | `Matrices\SGOL_FASE15_MATRIZ_MIGRACION_V1.xlsx` | Excel | 20,863 | Legible; 7 hojas identificadas |
| FTE-035 | `Descripcion SGOL\SGOL_FASE16_BLOQUE_MAESTRO_V1.md` | Markdown | 14,400 | Legible; 378 líneas; encabezado principal presente |
| FTE-036 | `Matrices\SGOL_FASE16_MATRIZ_MIGRACION_V1.xlsx` | Excel | 24,869 | Legible; 9 hojas identificadas |
| FTE-037 | `Descripcion SGOL\SGOL_FASE17_BLOQUE_MAESTRO_V1.md` | Markdown | 14,623 | Legible; 330 líneas; encabezado principal presente |
| FTE-038 | `Matrices\SGOL_FASE17_MATRIZ_MIGRACION_V1.xlsx` | Excel | 25,716 | Legible; 9 hojas identificadas |
| FTE-039 | `Descripcion SGOL\SGOL_FASE18_BLOQUE_MAESTRO_V1.md` | Markdown | 13,132 | Legible; 260 líneas; encabezado principal presente |
| FTE-040 | `Matrices\SGOL_FASE18_MATRIZ_MIGRACION_V1.xlsx` | Excel | 42,481 | Legible; 9 hojas identificadas |
| FTE-041 | `Descripcion SGOL\SGOL_FASE19_BLOQUE_MAESTRO_V1.md` | Markdown | 11,155 | Legible; 250 líneas; encabezado principal presente |
| FTE-042 | `Matrices\SGOL_FASE19_MATRIZ_MIGRACION_V1.xlsx` | Excel | 31,523 | Legible; 9 hojas identificadas |
| FTE-043 | `Descripcion SGOL\SGOL_FASE20_BLOQUE_MAESTRO_V1.md` | Markdown | 179,987 | Legible; 1,851 líneas; encabezado principal presente |
| FTE-044 | `Matrices\SGOL_FASE20_MATRIZ_MIGRACION_V1.xlsx` | Excel | 118,014 | Legible; 15 hojas identificadas |

## 8. Anomalías reales de la carpeta actual

**Ninguna detectada** dentro del alcance de accesibilidad y legibilidad básica de la Fase 00.

No se registraron como anomalías hechos de versiones anteriores que ya no están presentes. Tampoco se realizó una auditoría de duplicados o faltantes históricos.

## 9. Lista de control de fases y entregables

| Fase | Chat exacto o formato | Entregables definidos por el plan | Estado de puerta |
|---|---|---|---|
| 00 | `00 — Preparación y control del proyecto` | `F00_CONTROL_DEL_PROYECTO.md` | Completada, aprobada e incorporada el 2026-08-26 |
| 01 | `01 — Inventario y correspondencia de fuentes` | `F01_INVENTARIO_DE_FUENTES.md` | Completada, aprobada e incorporada el 2026-08-26 |
| 02 | `02 — Normalización del catálogo funcional` | `F02_CATALOGO_FUNCIONAL.md`; `F02_GLOSARIO.md`; `F02_PREGUNTAS_Y_CONTRADICCIONES.md` | Completada con observaciones, aprobada e incorporada el 2026-08-26 |
| 03 | `03 — Resolución de dudas y consolidación` | `F03_REGISTRO_DE_DECISIONES.md`; `F03_CATALOGO_CONSOLIDADO.md`; `F03_PENDIENTES_NO_BLOQUEANTES.md` | Habilitada; pendiente de ejecución |
| 04 | `04 — Alcance, prioridades y MVP` | `F04_ALCANCE_Y_MVP.md`; `F04_MAPA_DE_VERSIONES.md`; `F04_RIESGOS_Y_SUPUESTOS.md` | Pendiente |
| 05 | `05 — Especificación funcional del MVP` | `F05_ESPECIFICACION_FUNCIONAL_MVP.md`; `F05_MATRIZ_DE_ROLES_Y_PERMISOS.md`; `F05_MODELO_DE_ESTADOS.md`; `F05_MATRIZ_DE_TRAZABILIDAD.md`; `F05_CRITERIOS_DE_ACEPTACION.md` | Pendiente |
| 06 | `06 — Diseño técnico y arquitectura` | `F06_ARQUITECTURA.md`; `F06_MODELO_DE_DATOS.md`; `F06_CONTRATO_DE_API.md`; `F06_ESTRATEGIA_DE_PRUEBAS.md`; `F06_SEGURIDAD_Y_OPERACION.md`; `F06_REGISTRO_ADR.md` | Pendiente |
| 07 | `07 — Preparación del repositorio y backlog para Codex` | `F07_ESTRUCTURA_DEL_REPOSITORIO.md`; `F07_AGENTS_MD_PROPUESTO.md`; `F07_BACKLOG_DE_IMPLEMENTACION.md`; `F07_DEFINICIONES_DE_CONTROL.md`; `F07_PLANTILLAS_PARA_CODEX.md`; `F07_PRIMERA_TAREA_CODEX.md` | Pendiente |
| 08 | `08 — [ID] — [resultado concreto]` | El plan no fija un nombre único de archivo; exige un chat por historia o incremento, con código, pruebas, documentación y trazabilidad actualizados | Pendiente |
| 09 | `09 — Validación integral y aceptación del MVP` | `F09_PLAN_Y_RESULTADOS_DE_VALIDACION.md`; `F09_PRUEBAS_DE_ACEPTACION.md`; `F09_REGISTRO_DE_DEFECTOS.md`; `F09_DICTAMEN_DEL_MVP.md` | Pendiente |
| 10 | `10 — Piloto, despliegue y operación` | `F10_PLAN_DE_DESPLIEGUE.md`; `F10_LISTA_DE_VERIFICACION_OPERATIVA.md`; `F10_PLAN_DE_REVERSION.md`; `F10_PLAN_DE_SOPORTE.md`; `F10_CIERRE_Y_BACKLOG_POSTERIOR.md` | Pendiente |

Orden obligatorio de puertas: `00 → 01 → 02 → 03 → 04 → 05 → 06 → 07 → 08 → 09 → 10`.

## 10. Criterios de cierre de la Fase 00

| Criterio | Evidencia | Resultado |
|---|---|---|
| Carpeta correcta localizada sin descomprimir nada | Sección 3 | Cumplido |
| Rutas y originales conservados | Secciones 2 y 3 | Cumplido |
| Archivos actuales enumerados e identificados | Secciones 6 y 7; FTE-001 a FTE-044 | Cumplido |
| Total real de Markdown y Excel registrado | Sección 5 | Cumplido |
| Anomalías reales documentadas con ruta | Sección 8; ninguna detectada | Cumplido |
| Lista de control de fases existente | Sección 9 | Cumplido |

## 11. Condición de avance y constancia de cumplimiento

La puerta de salida de Fase 00 quedó cumplida el 2026-08-26:

1. el responsable humano revisó y aprobó el entregable;
2. se conservó la versión final en la raíz documental;
3. se incorporó una copia idéntica como `Fuentes\F00_CONTROL_DEL_PROYECTO.md`;
4. Fase 01 fue completada, aprobada e incorporada posteriormente.

La puerta de salida de Fase 02 quedó cumplida el 2026-08-26: sus tres entregables fueron aprobados por el responsable e incorporados a `Fuentes`. La Fase 03 puede comenzar.

## 12. Actualización de control del 2026-08-26

- La observación documental `F02-CON-001` queda resuelta: F00 ya refleja su aprobación y dispone de copia dentro de `Fuentes`.
- El conjunto original permanece identificado como FTE-001 a FTE-044, sin renumeración.
- Después de incorporar los entregables aprobados de Fase 02, la carpeta `Fuentes` contiene 49 archivos válidos: 27 Markdown, 21 `.xlsx` y 1 `.xlsm`. De ellos, 44 son las fuentes originales controladas y 5 son entregables aprobados incorporados (`F00_CONTROL_DEL_PROYECTO.md`, `F01_INVENTARIO_DE_FUENTES.md` y los tres documentos F02).
