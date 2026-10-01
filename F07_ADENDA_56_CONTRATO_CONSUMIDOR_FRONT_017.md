# F07 Adenda 56 — Contrato consumidor FRONT-017

## Aprobación y alcance

2026-09-30. **APROBADA ÍNTEGRAMENTE** mediante «Apruebo integramente el plan, recuerda seguir los documentos de diseño para crear las pantallas a como están diseñadas las anteriores». Incorpora íntegramente `docs/traceability/FRONT_017_PLAN_DE_IMPLEMENTACION.md`, especialmente §§3–7, antes de código. Número 56 libre comprobado en las adendas de raíz. Sólo FRONT-017/UI-E04/E05; BR-D07 se consume y BR-D08 se resuelve por sus formularios, etiquetas, mensajes y estados cerrados. Sin FRONT-018..020, sustitución, conclusión, revisión agregada, preview, descarga ni despliegue.

## Lectura aprobada de política capturada

GET `/api/v1/obligations/{id}` añade `evidencePolicy` nullable: `{ evidencePolicyVersionId, requirements: [{ requirementVersionId, requirementCode, kind, conditionCode, ordinal }] }`. Proviene exclusivamente de la versión almacenada en la obligación y se ordena por ordinal. Null significa ausencia de política capturada. Misma autorización, alcance, ETag y 404 convergente; incoherencia falla cerrado. Lectura mediante contrato interno del dueño, dentro del snapshot de consulta existente, sin escrituras/snapshots/auditoría de consulta. Sin endpoint, permiso, tabla o migración nuevos.

## Composición y conservación

Las secciones y formularios viven en el detalle existente; consumen CSS propio, variables y componentes compartidos del estilo v2 integrado por PR #85. Poppins local OFL para operación, Georgia del sistema sólo bienvenida; The Seasons excluida. Los textos exactos y asociaciones visuales de §6 y los 18 esquemas/etiquetas de §5 quedan aprobados por referencia. False se distingue de ausencia; LIMPIO sólo habilita la decisión posterior de aportar, no acredita aportación ni COMPLETA. Nunca URL firmada en DOM/log/captura/almacenamiento del navegador. El transporte temporal en memoria conserva PUT directo y su firma exacta.

## Flujo y autorización

Se conservan literalmente upload-intents, PUT firmado, complete, status y evidence POST de Adendas 18/20. Sólo primera aportación del responsable vigente sobre PENDIENTE; servidor reautoriza cada paso. CSRF, idempotencia, auditoría/transacción y no-efecto conservados. If-Match de sustitución no se consume ni altera. Cancelación sólo detiene transporte local; no borra historia. Polling secuencial cada 5 segundos visible, detenido ante error/navegación/veredicto y actualización manual disponible. No se añaden reintentos automáticos de escritura ni del Worker.

## Validación y cierre

§§8–9 del plan rigen archivos, pruebas enfocadas, PostgreSQL real/S3/ClamAV proporcional, revisión visual, capturas sintéticas y límites. Zoom nativo/lector/dispositivos siguen diferidos. Aprobación permite implementar/validar/commits locales; no push/PR/checks remotos/merge/despliegue.
