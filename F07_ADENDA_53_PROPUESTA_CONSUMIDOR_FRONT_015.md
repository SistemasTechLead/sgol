# SGOL — Propuesta de Adenda 53 a F07 para FRONT-015

## 1. Estado y decisión requerida

Fecha: 2026-09-29, America/Mexico_City. **SECCIONES 2–8 APROBADAS ÍNTEGRAMENTE POR EL RESPONSABLE** mediante «Apruebo íntegramente las secciones 2–8». Incorporación local para FRONT-015; sin publicación, merge ni despliegue.

Este documento conserva su nombre de propuesta para mantener la referencia revisada por el responsable. La aprobación recibida convierte las secciones 2–8 en el contrato consumidor operativo de FRONT-015. Es la Adenda 53 incorporada localmente; las referencias a «propuesta» dentro de las secciones aprobadas identifican su redacción revisada, no una decisión aún pendiente.

Hechos comprobados: fila 88 de la Adenda 45; HU-020/HU-021 y CA/CP-020/021; backends de ensure y publications existentes; FRONT-008/013 integradas y FRONT-014 Implementada localmente. Las lecturas de F06 §5.5 no están implementadas. Los cinco documentos operativos de diseño no incluyen la composición específica UI-G05/G06 ni el badge PUBLICADO. La proyección de sesión incluye PER-PLAN-VER, pero aún no PER-PLAN-PUBLICAR.

Base local: master, HEAD 9b1c2f07d83b7c2370326b3e817d63a533900b4e, árbol y Fuentes limpios según preflight. Rama de trabajo: codex/front-015. El SDK exacto 10.0.400 está ausente; preflight informa 10.0.401 instalado. No se cambia global.json ni se acredita compilación.

Fuentes: F07_ADENDA_45_BACKLOG_FRONTEND_DEFINITIVO.md, F05_ESPECIFICACION_FUNCIONAL_MVP.md filas HU-020/HU-021 y RN-014/015/025/027/028, F05_CRITERIOS_DE_ACEPTACION.md filas CA-020/021, F06_CONTRATO_DE_API.md §§5.5/7, Adendas 10/11/15/46, docs/design y contratos productivos actuales. La Adenda 15 se usa sólo para reutilizar el alcance de lectura existente; no autoriza adelantar FRONT-016.

## 2. Alcance y decisiones conservadas

Sólo FRONT-015, UI-G05/G06, HU-020/HU-021 y BR-API02/03. No se agregan entidades, migraciones, dependencias NuGet, estados de dominio, permisos nuevos, Worker, bandeja, evidencia, conclusión, validación, indicadores ni cierre de período.

Se conserva literalmente la decisión aprobada de la Adenda 10: ensure exige PER-PLAN-VER, no PER-PLAN-PUBLICAR, aunque F06 congelado diga lo contrario. Los cuatro roles canónicos pueden crear/recuperar la identidad única. La Adenda 11 conserva publicación sólo para Dirección, Administración y Subcoordinación, con nivel propio y todos sus inferiores. El cliente no selecciona scopeRole ni obligationIds.

El plan es único para sucursal/período y puede estar vacío. PUBLICADO significa que existe una publicación efectiva; no asegura que todo el contenido de todos los niveles se haya publicado. Cada alcance mantiene su propia versión VIGENTE. La numeración de versiones es global por plan: una sucesora no necesariamente tiene el número inmediatamente posterior a la anterior del mismo alcance.

## 3. Propuesta para BR-API02 y lectura de plan

Agregar exclusivamente GET /api/v1/plans/{isoYear}/{isoWeek}. La ruta acepta enteros decimales sin signo ni espacios, con las mismas reglas ISO de ensure. No recibe cuerpo, branchId, rol, nivel, ETag del cliente ni intención de mutación.

Respuesta 200 con ETag fuerte del work_plan.row_version y exactamente:

```text
data: {
  planId: uuid,
  branchId: uuid,
  branchCode: "LOR-001",
  periodId: uuid,
  isoYear: integer,
  isoWeek: integer,
  status: "BORRADOR" | "PUBLICADO",
  rowVersion: integer
}
meta: { queriedAt: instant UTC, correlationId: uuid }
```

No se agrega result: un GET no crea ni recupera una intención. Ausencia de período responde 404 PERIODO_NO_ENCONTRADO; período existente sin plan responde 404 PLAN_NO_ENCONTRADO. GET no materializa período ni plan. Período incoherente responde 409 PERIODO_INCOMPATIBLE. Ruta/formato inválido: 400 SOLICITUD_PLAN_INVALIDA; combinación ISO inválida: 422 SEMANA_ISO_INVALIDA.

El contenido operativo se obtiene mediante GET /api/v1/obligations?periodId=<periodId>, ya aprobado e implementado por HU-023. Se reutilizan su DTO, cursor, orden, errores y filtrado. UI-G05 lo muestra dentro del mismo plan sin crear planes separados por área o nivel. Se distingue expresamente este contenido actual del snapshot histórico publicado. No se implementa la bandeja ni un detalle de tarea nuevo. La consulta del plan y la de obligaciones son respuestas independientes; la interfaz no las anuncia como snapshot atómico ni como prevalidación de publicación.

## 4. Propuesta para BR-API03 e historia paginada

Agregar exclusivamente GET /api/v1/plans/{planId}/versions. Sin publicationId, devuelve una página de versiones autorizadas. Admite limit entero 1..100, predeterminado 50, y cursor opaco opcional. Orden: versionNo descendente y publicationId como desempate estable. Cada elemento contiene exactamente:

```text
publicationId: uuid
planId: uuid
versionNo: integer
versionStatus: "VIGENTE" | "SUSTITUIDA"
scopeRole: "DIRECCION" | "ADMINISTRACION" | "SUBCOORDINACION"
publishedBy: uuid
publishedAt: instant UTC
supersedesId: uuid | null
planRowVersion: integer
```

La respuesta usa data como array y meta: { nextCursor, count, queriedAt, correlationId }. count cuenta sólo la página visible. Una historia visible vacía devuelve 200 con data: [], count: 0 y nextCursor: null. No se revelan totales globales.

En la misma ruta, publicationId=<uuid> selecciona el contenido de una versión del mismo plan. limit y cursor paginan sus pares históricos por obligationId ascendente. La respuesta usa data: { publication: <elemento anterior>, obligations: [{ obligationId, assignmentVersionId }] } y los mismos metadatos; count corresponde a los pares visibles de la página. No se agregan nombres, payloads, archivos ni datos de evidencia. La UI muestra esos IDs como referencias históricas, sin convertirlos en enlaces a pantallas aún no implementadas.

El snapshot se lee de plan_version_obligation y conserva assignmentVersionId congelado; nunca se reemplaza con la asignación vigente. No se reconstruye historia usando la lista operativa actual. supersedesId sólo se devuelve si esa versión anterior también es visible al actor; en otro caso se proyecta null, sin modificar la fila persistida. No se fabrica versión consecutiva ni historia ausente.

Ambas variantes llevan el ETag actual del plan, obtenido junto con su proyección en una transacción de lectura consistente. planRowVersion conserva el ETag histórico de la publicación y nunca se usa como ETag vigente de publicación nueva. Cache-Control: no-store. Un cursor se liga a actor, rol vigente, plan, variante, publicationId y limit; cambio de esos parámetros o cursor alterado/inválido responde 400 CURSOR_INVALIDO. No concede autoridad; cada página reautoriza. Se reutiliza la protección opaca disponible y, si hace falta, Data Protection existente sin paquete nuevo.

Errores propuestos adicionales: UUID planId inválido, query desconocida/duplicada, publicationId inválido o limit inválido: 400 CONSULTA_PLAN_INVALIDA. Plan inexistente u otra sucursal: 404 PLAN_NO_ENCONTRADO. Publicación inexistente, de otro plan o sin contenido visible: 404 PUBLICACION_NO_ENCONTRADA. Todos usan application/problem+json, code y correlationId, sin datos parciales.

## 5. Autorización, coherencia y no efecto

Para las lecturas se captura una vez queriedAt mediante IClock.UtcNow. Se comprueban cuenta activa, empleo activo y temporalmente vigente en LOR-001 y exactamente un rol canónico activo y vigente. Falta de sesión/MFA pleno: 401 AUTENTICACION_REQUERIDA; actor identificado sin autoridad: 403 ACCESO_DENEGADO. Se exige PER-PLAN-VER. Puesto, parámetros, historial y permiso proyectado no autorizan.

La identidad del plan es visible a los cuatro roles autorizados, aun sin contenido visible. Contenido operativo e historia de pares se filtran según RN-025 y el universo vigente de la Adenda 15: Piso sólo asignaciones propias vigentes; Subcoordinación propias y Piso; Administración propias, Subcoordinación y Piso; Dirección todo LOR-001, incluidas obligaciones sin asignación vigente. Los pares e IDs históricos se filtran por visibilidad actual de la obligación antes de paginar en PostgreSQL. Una versión se lista sólo si tiene al menos una obligación actualmente visible. La historia no concede acceso al antiguo responsable ni permite ver pares/superiores.

Una versión puede mostrar sólo parte de su snapshot. La UI lo dice expresamente; no afirma completitud global, no devuelve conteos ocultos y no deduce falta de obligaciones a partir de una página. El estado global y la numeración histórica del plan son metadatos compartidos; no conceden acceso al contenido oculto.

Las lecturas usan transacciones read-only consistentes, sin escrituras de negocio, auditoría funcional o idempotencia. Se conserva telemetría HTTP sanitizada. Las mutaciones reutilizan íntegramente Adendas 10/11: auditoría e idempotencia transaccionales, unicidad, replay, rechazo terminal y concurrencia. No se modifica su selección de obligaciones ni sus cuerpos cerrados.

Se proyecta PER-PLAN-PUBLICAR exclusivamente para los tres roles superiores canónicos mediante el mecanismo de sesión existente; sólo sirve para presentación. Una revocación o cambio de rol se revalida en cada petición productiva.

## 6. Propuesta de composición UI-G05 y UI-G06

Ambas unidades se agregan a /planificacion, sin ruta web adicional. Comparten la selección existente de año y semana ISO. Los cuatro roles autorizados pueden consultar y crear/recuperar el plan. Esto autoriza expresamente la presentación de UI-G05 a Piso, sin controles de publicación.

UI-G05: sección «Plan semanal», identidad, sucursal, semana, estado y tabla de obligaciones actuales autorizadas. Columnas: TAR, identificador, estado de ejecución y responsable vigente según DTO de HU-023. Paginación Anterior/Siguiente con cursor, sin editor, bandeja ni selector de nivel. Acción POST «Crear o recuperar plan» separada de GET «Consultar plan». Antes de ensure se consulta/materializa el período mediante el contrato existente de semanas; GET de plan permanece sin efecto.

UI-G06: sección «Publicaciones del plan» con tabla de número de versión, estado, alcance, instante e identificador del actor, y acción GET «Ver contenido publicado». La selección muestra los pares históricos paginados y «Sólo se muestra el contenido permitido por tu alcance actual». Alcances y roles se traducen con el catálogo existente. Instantes se presentan en America/Mexico_City, conservando UTC en contrato. La historia no se mezcla con contenido actual ni con el resultado de una intención recuperada.

Publicación usa una confirmación contextual corta: plan/semana, alcance derivado del rol y consecuencia acumulativa. No exige motivo: el POST existente no lo admite. Se propone una variante sin textarea del dialog base, con «Cancelar» como foco inicial y «Publicar alcance» como acción primaria. Escape cancela y el foco vuelve al disparador. No preselecciona obligaciones ni promete resultado antes de confirmar.

La intención de ensure/publicación se protege con el mecanismo existente, ligada a usuario, operación, plan o semana y ETag de publicación. Conserva clave y contenido en reenvío de la misma intención. GET, reload y cambio de semana no envían mutaciones. Después de 412 no reenvía ni cambia automáticamente el ETag: bloquea publicar, ofrece recarga explícita y exige preparar otra intención. Un error de red no afirma éxito ni ausencia de efecto; permite reenviar sólo la intención original con sus encabezados originales, sin retry automático.

## 7. Estados y mensajes propuestos para incorporar a docs/design

Se reutilizan tablas, botones, filtros, badges, skeleton, alertas y resumen enfocable. Todas las propiedades visuales usan variables CSS. Se propone PUBLICADO con texto «Publicado», icono de documento con check y --color-info / --color-info-fondo; comunica publicación, sin afirmar validación o conclusión. BORRADOR, VIGENTE y SUSTITUIDA conservan sus presentaciones aprobadas. CREADA y los resultados de publicación son mensajes de operación confirmada, no nuevos estados ni equivalencias inventadas con ACEPTADA.

| Situación | Mensaje o acción propuesta |
|---|---|
| No hay plan para el período | «Aún no existe un plan para esta semana». Acción autorizada «Crear o recuperar plan». |
| Plan sin filas visibles | «No hay obligaciones visibles en este plan». No afirma que no existan obligaciones fuera del alcance. |
| Historia sin versiones visibles | «No hay publicaciones visibles en tu alcance». |
| Creación confirmada CREADA | «Plan semanal creado». |
| Ensure RECUPERADA | «Se recuperó el plan semanal existente». |
| PUBLICADA_INICIAL | «Primera publicación de tu alcance confirmada». |
| PUBLICADA_INCREMENTAL | «Nueva versión de tu alcance publicada». |
| Publicación RECUPERADA | «Se recuperó la publicación ya confirmada». Conserva sus IDs y ETag originales; no anuncia otra versión. |
| Confirmación | «Se publicarán las obligaciones aplicables a tu nivel y niveles inferiores. El servidor volverá a comprobar tu autoridad y las asignaciones». |
| Cargando | «Consultando plan…», «Consultando publicaciones…», «Preparando plan…», «Publicando alcance…». aria-busy en región, controles de envío deshabilitados. |
| 403 | «No tienes permiso para consultar este plan» o «No tienes permiso para publicar este plan», según operación. Sin datos residuales. |
| 404 PLAN_NO_ENCONTRADO | Vacío de plan sólo en consulta por semana; en publicación/historia «El plan no existe o no está disponible». |
| 404 PUBLICACION_NO_ENCONTRADA | «La publicación no existe o no está disponible en tu alcance». |
| 404 PERIODO_NO_ENCONTRADO | «Consulta primero el período semanal». No crea un plan implícitamente. |
| 400 SOLICITUD_PLAN_INVALIDA / CONSULTA_PLAN_INVALIDA / 422 SEMANA_ISO_INVALIDA | «Revisa el año, la semana y los datos de consulta». Error asociado a campo cuando corresponda. |
| 400 SOLICITUD_PUBLICACION_INVALIDA | «No se pudo verificar la publicación. Recarga el plan antes de preparar otra solicitud». |
| 400 CURSOR_INVALIDO | «La consulta cambió. Vuelve a consultar desde el inicio». |
| 412 / IF_MATCH_REQUERIDO / IF_MATCH_INVALIDO | Mensaje común aprobado de versión y «Recargar plan»; publicación bloqueada hasta recarga y nueva decisión. |
| 422 SIN_OBLIGACIONES_PUBLICABLES | «No hay obligaciones publicables en tu alcance». No anuncia plan global vacío. |
| 422 SIN_NOVEDADES_PUBLICABLES | «No hay nuevas obligaciones publicables en tu alcance». |
| 422 OBLIGACION_SIN_ASIGNACION | «Hay obligaciones de tu alcance que necesitan asignación antes de publicar». Sin inventar una asignación. |
| 422 ASIGNACION_NO_PUBLICABLE | «Una asignación de tu alcance dejó de ser publicable. Revisa las asignaciones antes de preparar otra publicación». |
| 409 PERIODO_INCOMPATIBLE / CONTENIDO_PLAN_INCOMPATIBLE / ESTADO_PLAN_INCOMPATIBLE | «Los datos del plan no permiten continuar. Vuelve a consultarlo». No refleja SQL ni contenido. |
| 409 IDEMPOTENCY_CONFLICT | «Esta solicitud ya se usó con otros datos. Consulta el plan antes de preparar otra intención». |
| 400 IDEMPOTENCY_KEY_INVALIDA | «No se pudo verificar la solicitud. Recarga antes de continuar». |
| Otros 409 de ensure/publicación | «No se pudo confirmar la operación. Consulta el plan antes de decidir otro intento». |
| Red o resultado incierto | «No se pudo confirmar el resultado. Puedes reenviar la misma solicitud». No cambia el resultado mostrado a éxito. |

401 elimina datos de sesión según contrato común; CSRF y demás combinaciones usan traducción común segura. Nunca se muestra title/detail crudo. Los rechazos llevan correlationId cuando exista; intención protegida y headers no se exponen como datos técnicos al usuario.

Normal: sólo respuestas confirmadas actualizan datos. Foco: orden DOM, :focus-visible, resumen de errores enfocable y retorno de foco del dialog. Deshabilitado: sin plan no se publica, mientras se envía no se duplica y tras conflicto se exige recarga. Error: cada sección distingue fallo de vacío sin conservar datos no autorizados. Cargando: anuncia la operación y conserva información legible. Vacío: cada caso anterior tiene texto propio; no induce publicación de versión vacía. Móvil: reflow sin desbordamiento de página, tabla semántica con desplazamiento interno, etiquetas asociadas y área mínima vigente. Se verifica WCAG 2.2 AA, teclado y texto/icono sin depender del color.

## 8. Implementación y evidencia después de aprobar

1. Incorporar esta adenda como aprobada, con la respuesta explícita del responsable; añadir las extensiones de §§6/7 a docs/design y mantener los documentos congelados intactos.
2. Implementar contratos de lectura en Planning, endpoints, lectores read-only y composición Razor; reutilizar la consulta HU-023 mediante contrato explícito sin escritura entre módulos. Registrar composición y permisos de presentación en sus mecanismos existentes.
3. Añadir pruebas positivas/negativas de contrato y presentación: los cuatro roles, plan único/vacío, CA/CP-020/021, contenido actual de dos niveles, historia y pares congelados, filtro antes de paginar, anti-IDOR, cursor ligado, ausencia de escrituras GET, replay y 412 sin retry. Conservar rechazo de alcance superior y auditoría/no efecto de publicaciones, sin reimplementar reglas.
4. Ejecutar build Release --no-restore, pruebas nuevas/afectadas --filter y git diff --check cuando el SDK fijado esté disponible. Restore locked sólo si falta restauración necesaria. No modificar global.json para eludir la versión. Suites integrales y navegador/PostgreSQL costosos se reservan al hito solicitado; toda comprobación no ejecutada queda identificada por causa exacta, sin atribuir éxito.
5. Actualizar docs/traceability/FRONT_015_PLAN_SEMANAL.md e IMPLEMENTATION_STATUS.md con criterios, archivos, comandos/resultados y límites. Un fallo de build/prueba compatible impide declarar Implementada localmente. No declarar Publicada, Integrada o Terminada sin evidencia correspondiente.

La aprobación requerida es de este contrato y diseño concretos, no una nueva autorización de push, PR, merge o despliegue. La orden de implementación ya recibida permite continuar localmente después de resolver estas decisiones.
