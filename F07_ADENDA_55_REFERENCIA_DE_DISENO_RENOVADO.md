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

## 5. Precisión visual v2 y correcciones de revisión — 2026-09-30

El responsable acepta las demás pantallas de la propuesta externa v2 y ordena actualizar esta referencia para futuras historias. Se incorpora docs/design/ESTILO_VISUAL_V2.md con tokens y reglas sincronizados: logo e iconos propios, Poppins local, The Seasons en bienvenida, sesión a la derecha, semana legible, secundario neutro/textual y acceso de dos paneles. Estos criterios visuales precisan los ejemplos anteriores que imponían borde rojo, formulario estrecho o sólo fallback. No cambian contratos.

Detalle de persona, selector/acción de release TAR, conflicto/resultado incierto del plan y marca/versionado/ubicación de acceso requieren la revisión visual solicitada antes de interfaz productiva. El texto Delicias, Chihuahua no modifica America/Mexico_City ni datos contractuales de LOR-001. La versión procede del ensamblado Web; no se inventa una release comercial.

Las ocho TAR se presentan mediante una plantilla única seleccionada por taskCode; las variantes de maqueta no son nuevas rutas. Crear más TAR permanece fuera del MVP y no se implementa en este hito. La base documentada permite consumir el estilo en próximas historias sin migración completa previa. Fuentes y originales congelados intactos; no hay autorización de publicación, merge o despliegue. La aprobación visual aún requerida de las correcciones no invalida las pantallas de base ya aceptadas.

## 6. Aprobación de correcciones e implementación v2 — 2026-09-30

El responsable aprueba expresamente las correcciones y el complemento del único plan: «Apruebo las correcciones y el complemento del plan para implementar». Se autoriza adaptar las pantallas existentes de FRONT-001..016 a ESTILO_VISUAL_V2.md, conservando contratos y exclusiones. Los §§4–5 conservan el registro documental histórico; su revisión pendiente quedó superada por esta aprobación. Código y validaciones se acreditan por grupo en docs/traceability/DISENO_RENOVADO_V2_IMPLEMENTACION.md, sin reaprobar historias anteriores.

La instrucción vigente exige capturas implementadas y concordancia con la guía antes de una aprobación explícita posterior para nuevos commits, push y un único PR de todo el hito. No hay autorización de publicación, merge ni despliegue. Las próximas historias consumen estos componentes y tokens dentro de su propio alcance, sin migración completa como dependencia.
