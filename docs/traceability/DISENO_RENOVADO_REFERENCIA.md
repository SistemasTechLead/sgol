# SGOL — Trazabilidad de la referencia de diseño renovado

## Resultado y aprobación

Fecha: 2026-09-30. **Referencia documentada, aprobada e incorporada localmente**. No equivale a diseño implementado en pantallas existentes, ni a Publicada/Integrada en GitHub. La aceptación final del paquete documental se solicita al entregarlo.

El responsable eligió exclusivamente CSS propio renovado y aprobó «Apruebo las secciones y la adenda», cubriendo §§3–9 de [PLAN_DISENO_RENOVADO.md](../design/PLAN_DISENO_RENOVADO.md) y [Adenda 55](../../F07_ADENDA_55_REFERENCIA_DE_DISENO_RENOVADO.md). La numeración se comprobó libre antes de crear la adenda; no se inventó una tarea ni identificadores funcionales.

FRONT-016 conserva su registro vigente inicial en [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md): implementada localmente y Publicada, evidencia aceptada sin repetir gates. No se acredita una nueva integración ni se atiende su pipeline desde este hito documental. Base de trabajo `ce76ab888ec7d8c9d2f0fc2461522d917da61340`; no se revierte ningún cambio ajeno. El borrador del plan era el único archivo nuevo al recibir aprobación.

## Criterios cubiertos y archivos

| Criterio aprobado | Evidencia documental |
|---|---|
| Tokens exactos y usos; identidad Loretta preservada | [tokens.md](../design/tokens.md): un token tipográfico cambiado y doce añadidos; paleta, fuentes, espaciado, radios, sombras y anchos previos conservan valores. |
| Layout, navegación, títulos y agrupación | [componentes.md](../design/componentes.md), [navegacion.md](../design/navegacion.md): fondo cálido/panel blanco, títulos, densidad y navegación móvil modal. |
| Botones/campos/tablas/badges/alertas/dialog y seis estados | Componentes compartidos y matriz conservada; variante textual, errores y vacíos definidos. |
| Mensajes y significados sin cambio | [estados-y-mensajes.md](../design/estados-y-mensajes.md), [estados-de-dominio.md](../design/estados-de-dominio.md): catálogos previos conservados, precedencia y presentación incorporadas. |
| Responsive y accesibilidad | [accesibilidad.md](../design/accesibilidad.md): pares conservados, variables de foco/área, reflow, enlaces y movimiento reducido. |
| Consumo de FRONT-017..020 y demás pantallas pendientes | [referencia-renovada.md](../design/referencia-renovada.md): matriz de consumidoras, contratos/brechas/dependencias intactos. |
| Transición sin migración completa previa | Guía y Adenda 55: pantallas antiguas conservan implementación, nuevas materializan sólo lo necesario. |
| Contenido congelado protegido y referencias localizables | Adenda 55 y complemento incremental de [INDICE_IDS.md](../INDICE_IDS.md); sin cambios a originales ni Fuentes/. |
| Siguiente hito preparado sin ejecutarlo | [DISENO_RENOVADO_SIGUIENTE_HITO.md](DISENO_RENOVADO_SIGUIENTE_HITO.md): chat exacto y prompt completo, plan único, commits pequeños y publicación final agrupada. |

Además se actualizan el plan aprobado y el registro de estado. No hay cambios de vistas, CSS productivo, JavaScript, backend, paquetes o pruebas funcionales. No se copiaron valores/comportamientos completos de la maqueta. Se excluyen comparación, selector de demostración, acciones simuladas, navegación móvil abierta y omisiones de datos.

## Verificaciones locales

La comprobación inicial `scripts/ci/preflight.ps1` informa árbol y Fuentes limpios, HEAD/base y rama `codex/front-016`; falta SDK exacto 10.0.400, frente al 10.0.401 disponible. No se diagnostica ni altera el entorno; una tarea documental no necesita compilación.

La comprobación PowerShell enfocada aprobó: **13 archivos exclusivamente documentales**, **50 enlaces locales válidos**, **59 tokens concordantes entre tablas y root**, **un cambio y doce adiciones exactos según plan**, **86 referencias a variables definidas**, **cuatro catálogos/registros previos conservados** y **cuatro rangos del índice correctos**. No hay anchors locales en este paquete que requieran resolución adicional. La comparación con Git conserva todos los valores anteriores salvo el título aprobado y mantiene la paleta íntegra.

Los catálogos de estados/mensajes, navegación funcional y registro FRONT-016 se compararon con la base: conservados; en mensajes sólo se sustituyeron dos valores de ejemplo CSS por tokens aprobados. Se revisó el diff y `git diff --check` pasó. `git diff --cached --check` pasó para el cambio completo, incluidos archivos nuevos; los avisos LF/CRLF corresponden a la configuración Git existente, sin modificarla.

Conservación en rama documental local `codex/diseno-renovado`, separada de `codex/front-016`; commit: el que contiene este registro y el paquete documental. Esta rama parte del checkout aceptado y no modifica la cabeza de la rama FRONT-016 ni publica sus cambios.

## Pendientes y límites

Build, pruebas funcionales, PostgreSQL, navegador y gates remotos: **NO APLICA** a este cambio exclusivamente documental; no se ejecutan ni se presentan como aprobados. La falta de SDK no se convierte en un bloqueo general.

**Validación diferida al hito de adaptación:** aplicación funcionando, contraste/foco efectivos, reflow, área mínima, teclado, movimiento reducido, capturas y regresiones de componentes. Causa: no se modifica ni ejecuta código productivo en esta tarea. Las capturas externas sólo representan la maqueta seleccionada.

Publicación/integración documental remota: no solicitada, sin push/PR/merge/despliegue. Hito de adaptación pendiente de su propio plan aprobado y posterior autorización de publicación; FRONT-017..020 no iniciadas por esta tarea. No se crea otro chat automáticamente.
