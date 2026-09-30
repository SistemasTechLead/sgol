# F07 Adenda 55 — Referencia de diseño renovado con CSS propio

## 1. Aprobación y alcance

Estado: **APROBADA — INCORPORADA DOCUMENTALMENTE EN LOCAL**.

Fecha: 2026-09-30. El responsable aprobó «Apruebo las secciones y la adenda» tras revisar los §§3–9 de `docs/design/PLAN_DISENO_RENOVADO.md`. La dirección elegida es exclusivamente «CSS propio renovado». Esta aprobación autoriza incorporar las reglas documentales, actualizar trazabilidad, validar y conservar un commit local; no autoriza push, PR, merge ni despliegue.

Esta adenda complementa el criterio transversal de Adenda 04 y el consumo de diseño del backlog frontend de Adenda 45. No modifica los originales F00–F07 ni Fuentes/. No inserta una tarea, renumera identificadores ni cambia dependencias funcionales, permisos, endpoints, mensajes, estados, auditoría o contratos. FRONT-016 conserva su evidencia vigente aceptada sin repetir gates ni declarar integración nueva.

## 2. Referencia oficial operativa

La referencia oficial de diseño para tareas pendientes es `docs/design/referencia-renovada.md`, junto con `tokens.md`, `componentes.md`, `estados-y-mensajes.md`, `estados-de-dominio.md`, `accesibilidad.md` y `navegacion.md` del mismo directorio. El plan aprobado conserva el detalle exacto y la comparación con la maqueta; las reglas operativas se incorporan en esos documentos, sin dependencia de la ruta externa.

Se conserva identidad Loretta, paleta y semántica. Se adoptan títulos y primitivas por variables, fondo cálido de trabajo, paneles blancos, jerarquía de encabezados, controles/tablas densas y componentes compartidos, seis estados, responsive y WCAG 2.2 AA. La navegación móvil conserva su panel modal, foco y Escape. La barra de comparación, selectores de muestra, acciones simuladas y cambios funcionales de la maqueta se excluyen. No se introducen Tailwind, daisyUI ni bibliotecas de componentes.

## 3. Adopción incremental y transición

FRONT-017..020 y cualquier pantalla pendiente consumen la referencia aprobada. Materializan los componentes compartidos necesarios dentro de su alcance propio, mediante parciales existentes y CSS propio con variables, sin segunda biblioteca ni migración completa previa. Las brechas y dependencias de Adenda 45 siguen pendientes de resolución por su consumidora; esta aprobación no las resuelve ni inicia las historias.

Las pantallas existentes conservan implementación y comportamiento hasta el hito posterior de adaptación. Un cambio compartido nuevo debe acotar su consumo cuando afectaría pantallas aún no adaptadas. No se convierte la renovación completa en dependencia artificial. Las composiciones funcionales y mensajes aprobados prevalecen sobre ejemplos ilustrativos antiguos o simulaciones externas.

## 4. Evidencia, cierre y siguiente hito

Resultado de esta tarea: **referencia documentada**, sin diseño implementado en pantallas productivas. La incorporación local no significa Publicada ni Integrada en GitHub. La verificación documental se registra en `docs/traceability/DISENO_RENOVADO_REFERENCIA.md` y `IMPLEMENTATION_STATUS.md`; la aceptación final del paquete se solicita al entregarlo sin ejecutar el hito siguiente.

El mensaje listo para el siguiente chat se conserva en `docs/traceability/DISENO_RENOVADO_SIGUIENTE_HITO.md`: **SGOL — Diseño renovado — Adaptación de pantallas existentes**. Ese hito requiere un único plan de implementación aprobado, layout/componentes primero y pantallas por grupos; commits locales pequeños, validación enfocada y capturas sintéticas. Sólo ante autorización expresa agrupa publicación en un PR/pipeline final y mantiene su seguimiento hasta pedir aprobación expresa del merge de la cabeza validada. No se crea el chat ni se ejecuta ese hito mediante esta adenda.
