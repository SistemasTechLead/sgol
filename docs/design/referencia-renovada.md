# SGOL — Referencia oficial de diseño renovado

## Precisión vigente del estilo v2

La base visual v2 está aceptada y documentada en [ESTILO_VISUAL_V2.md](ESTILO_VISUAL_V2.md), por solicitud expresa de actualización del responsable el 2026-09-30. Sus reglas precisan los ejemplos anteriores: secundario neutro sin borde rojo, fuentes locales, logo/iconos, sesión a la derecha y acceso con composición de marca/formulario. Las cuatro correcciones solicitadas siguen en revisión visual; no hay nueva implementación productiva por este registro. Los contratos y mensajes funcionales se conservan.

## Aprobación y fuente operativa

**Referencia documentada, aprobada e incorporada localmente el 2026-09-30.** El responsable aprobó «Apruebo las secciones y la adenda», cubriendo §§3–9 de [PLAN_DISENO_RENOVADO.md](PLAN_DISENO_RENOVADO.md) y [Adenda 55](../../F07_ADENDA_55_REFERENCIA_DE_DISENO_RENOVADO.md). La elección es exclusivamente CSS propio renovado. La aceptación final del paquete se solicita al entregar; no supone autorización de publicación ni implementación del hito posterior.

Esta guía es la entrada oficial para cualquier tarea frontend pendiente y para la adaptación posterior de pantallas. Las reglas detalladas están en los documentos siguientes, que se leen juntos. No se requiere reabrir la decisión visual en cada historia; una carencia o contradicción específica se presenta para decisión antes de implementarla.

| Fuente | Qué determina |
|---|---|
| [tokens.md](tokens.md) | Valores exactos, bloque root y usos permitidos. |
| [componentes.md](componentes.md) | Layout, encabezados/paneles, controles, densidad, seis estados y composiciones consumidoras. |
| [estados-y-mensajes.md](estados-y-mensajes.md) | Textos aprobados, error/vacío/carga, confirmaciones y respuestas seguras. |
| [estados-de-dominio.md](estados-de-dominio.md) | Texto, icono, color y significado de estados y banderas. |
| [accesibilidad.md](accesibilidad.md) | WCAG 2.2 AA, pares accesibles, foco, área mínima, reflow y movimiento reducido. |
| [navegacion.md](navegacion.md) | Rutas, grupos, permisos de presentación, sesión y recorridos existentes. |

Los documentos anteriores y los contratos específicos de la historia son fuente oficial; la maqueta es procedencia visual, sin autoridad funcional. Los ejemplos antiguos no habilitan ordenación, borrado, páginas numeradas, detalle técnico crudo ni acciones fuera de contrato. Las extensiones aprobadas de cursor, dialog y mensajes seguros prevalecen.

## Dirección visual y reglas de composición

Se conserva identidad Loretta: paleta cálida, acento operativo accesible, familias/fallbacks, radios y sombras actuales. Toda propiedad visual consumidora usa variables; los valores nuevos y el único umbral literal permitido de media query se definen en tokens.md. CSS propio y componentes compartidos existentes; sin Tailwind, daisyUI, otra biblioteca ni dependencia nueva.

Área de trabajo cálida; encabezado, navegación, paneles y tablas blancos. Marca decorativa permanece en identidad, nunca sustituye acento operativo. Los paneles no cambian de color según el estado de una obligación. Un h1 por página, h2 por sección y h3 por agrupación; descripción/contexto sólo si existen y están confirmados. Ningún total se infiere de una página con cursor.

Escritorio usa columna lateral y main con ancho máximo. El orden DOM conserva salto al contenido, identidad/sesión/logout, navegación y main. Acceso/MFA siguen fuera del shell. En teléfono se conserva navegación modal con disparador, Escape, foco contenido y retorno; sesión y logout permanecen disponibles. No se adopta la navegación abierta de la maqueta ni ocultación de información necesaria.

Paneles compartidos separan cabecera, filtros y datos; una columna predeterminada. Paneles independientes pueden compartir fila si caben y refluyen al mismo orden en estrecho. Las tablas densas usan todo el ancho; mantienen semántica y desplazamiento en su propio contenedor. Agrupar datos exige preservar etiquetas, asociación y todos los valores. No convertir filas a tarjetas ni truncar información esencial.

Botones primario/secundario/destructivo y variante textual auxiliar mantienen área, foco y estados. Campos nativos conservan etiquetas/ayudas/errores. Badges llevan texto e icono y pueden agruparse con wrap; banderas no reemplazan estados base. Alertas usan mensajes seguros, resumen enfocable y asociaciones al campo. Dialog conserva Cancelar inicial, Escape, retorno y motivo sólo cuando el contrato lo pide. Upload mantiene cuarentena/escaneo y vínculo sólo con LIMPIO.

Cada pantalla cubre normal, foco, deshabilitado, error, cargando y vacío; la matriz por componente conserva sus «No aplica». Carga es regional y no presupone resultado; vacío distingue ausencia, filtro e historia y sólo ofrece acciones autorizadas. Enlaces de contenido subrayados, foco sin recortes, estados con texto/icono y movimiento reducido. La conformidad productiva se mide al materializar el diseño, incluyendo teclado, contraste, área, texto ampliado y reflow.

## Adopción por FRONT-017..020 y pantallas pendientes

Cada plan lee esta guía, los cinco documentos obligatorios y navegación, acepta su aprobación y consulta únicamente contratos/criterios y archivos de su alcance. Reutiliza y extiende parciales y CSS compartidos existentes; no crea otra biblioteca. Si un componente o mensaje necesario falta, se detiene esa parte y presenta propuesta concreta al responsable.

| Tarea | Consumo visual | Límites y dependencias conservados |
|---|---|---|
| FRONT-017 | Shell, paneles, encabezados, campos/upload y estados. | FRONT-012/016; BR-D07/D08 por resolver en su historia; sólo tipos/payloads contratados, escaneo y privado, CSRF/idempotencia; sin preview/descarga. |
| FRONT-018 | Versiones/historia, badges, alertas y dialog. | FRONT-017; BR-API04 y BR-D04/M08 por resolver; sustitución/conclusión, faltantes, autoridad y concurrencia; descarga sólo si hay contrato. |
| FRONT-019 | Listados/filtros, decisiones, historia y confirmación. | FRONT-018/012/014; BR-D09/M09 por resolver; tres resultados, jerarquía, no autovalidación, fundamento/escalamiento, ETag/idempotencia. |
| FRONT-020 | Paneles, tablas/filtros y estados de indicadores/auditoría/continuidad. | FRONT-019 y TECH-E2E-CV-05 aceptada; BR-API05/D10..D12/M11/M12 por resolver; cinco indicadores, sin KPI nuevo, exportación, reparación o deploy. |

Las filas canónicas siguen en Adenda 45, localizables mediante [INDICE_IDS.md](../INDICE_IDS.md); sus adendas vigentes y contratos funcionales prevalecen. Esta tarea no resuelve esas brechas ni inicia esas historias. Cualquier otra pantalla pendiente sigue el mismo consumo visual y sus propios permisos/dependencias; no aparecen rutas o acciones futuras sólo por el diseño.

## Transición y estado de implementación

Las pantallas existentes conservan código y comportamiento hasta un hito de adaptación aprobado. Las nuevas tareas materializan sólo los componentes compartidos que necesitan en su alcance. No necesitan una migración completa previa. Si una regla compartida afectaría pantallas antiguas, se acota su consumo preservando su presentación hasta su hito, sin duplicar la biblioteca ni ocultar regresiones.

La adaptación tendrá un único plan, layout/componentes primero y pantallas existentes por grupos. Mantendrá rutas, permisos, contratos, mensajes, auditoría y comportamiento. Una aprobación de diseño no autoriza cambiar títulos funcionales, columnas, colapsar consultas o convertir avisos a lista por arrastre de la maqueta; el plan de adaptación debe justificar toda agrupación preservando el contrato.

| Superficie | Estado al cerrar esta tarea documental |
|---|---|
| Referencia operativa y Adenda 55 | Referencia documentada, aprobada e incorporada localmente. |
| Pantallas existentes | Diseño renovado pendiente de implementar; evidencia previa conservada. |
| FRONT-017..020 y demás pantallas pendientes | Consumidoras de la referencia; no implementadas por esta tarea. |
| Publicación/integración de este paquete | Sin push, PR, merge ni despliegue. |

Registrar «Implementada localmente» sólo cuando la superficie de código exista, el alcance esté cubierto y hayan pasado sus comprobaciones enfocadas disponibles. «Validación diferida» no equivale a éxito; «Publicada»/«Integrada» necesitan evidencia real. La evidencia de FRONT-016 se acepta sin repetir sus gates ni resolver desde este hito su pipeline pendiente.

## Procedencia y exclusiones

Selección del responsable: `native.html` en `C:/Users/siste/.codex/visualizations/2026/09/30/01a0f38e-1500-7c02-a157-70773b457fa7/sgol-diseno-front016/`, junto con native.css/shared.css/tokens.css, capturas desktop/mobile, cuatro source-*.cshtml y README.md. Se leyeron únicamente como referencia externa y no se modificaron ni copiaron a producción.

Las reglas oficiales anteriores son autocontenidas: no requieren esa ruta externa ni archivos de demostración. Se excluyen comparación, selectores de muestra, acciones/diálogo simulados, aliases auxiliares innecesarios y toda inferencia funcional. Las capturas son maquetas sintéticas, no evidencia de integración ni WCAG completa. Fuentes/ e identidad congelada permanecen protegidas.

Evidencia de verificación en [DISENO_RENOVADO_REFERENCIA.md](../traceability/DISENO_RENOVADO_REFERENCIA.md). Mensaje completo del siguiente hito en [DISENO_RENOVADO_SIGUIENTE_HITO.md](../traceability/DISENO_RENOVADO_SIGUIENTE_HITO.md). No se ejecuta ese hito ni se crea otro chat desde esta tarea.
