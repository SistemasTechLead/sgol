# SGOL — Adenda 04 a F07: criterio transversal de interfaz

## 1. Control del documento

| Campo | Valor |
|---|---|
| Tipo | Adenda normativa a `F07_BACKLOG_DE_IMPLEMENTACION.md` |
| Estado | PROPUESTA |
| Fecha | 2026-08-31 |
| Tarea de planeación | `TOOL-PLAN-004` |
| Efecto | Amplía F07 con un criterio de aceptación transversal para toda historia con UI; no modifica ni renumera el backlog aprobado |
| Conservación | Mantiene sin cambios `F07_BACKLOG_DE_IMPLEMENTACION.md` y los demás documentos F00–F07 aprobados |
| Eficacia | Pasa a `APROBADA` al incorporarse a `master`; desde ese momento el criterio es obligatorio |

## 2. Criterio de aceptación transversal

Toda historia con UI se construye exclusivamente desde las definiciones operativas de `docs/design`. La historia no se declara `Terminada` si introduce un valor visual literal, omite un estado exigido por `docs/design/componentes.md` o usa un componente no definido en `docs/design`.

Ante la ausencia de un componente, estado o mensaje necesario, la implementación se detiene y presenta la carencia al responsable. La ausencia no autoriza a inventar el componente, improvisar propiedades visuales ni copiar el estilo de otra pantalla.

## 3. Relación con TECH-UI-001

`TECH-UI-001`, definida por `F07_ADENDA_02_TAREAS_DE_BASE_DE_INTERFAZ.md`, crea la hoja de variables CSS y los componentes base. Esta adenda no implementa esa tarea: establece la regla que gobierna a todas las historias con UI posteriores a `TECH-UI-001`.

La hoja de variables es el único archivo de implementación que puede declarar valores visuales literales. Las vistas, componentes y hojas de estilo consumidoras deben usar las variables allí definidas y cumplir los estados normal, foco, deshabilitado, error, cargando y vacío establecidos en `docs/design/componentes.md`.

## 4. Límite y protección de fuentes

`docs/design` es la única fuente operativa de diseño. `Fuentes/IdentidadMarca/` permanece congelado y no se abre; esta adenda no autoriza leer PDF, `.ai`, `.psd` ni archivos de tipografía, ni crear o modificar contenido dentro de `Fuentes/`.

## 5. Aprobación y eficacia

Esta adenda permanece como `PROPUESTA`. No declara `TOOL-PLAN-004` ni `TECH-UI-001` `Terminada`, no habilita historias bloqueadas y no modifica ningún documento aprobado. Al incorporarse a `master`, pasa a `APROBADA` y el criterio transversal entra en vigor.
