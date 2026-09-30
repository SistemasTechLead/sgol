# F07 Adenda 54 — Contrato consumidor de FRONT-016

Estado: **APROBADA ÍNTEGRAMENTE — INCORPORADA LOCALMENTE**.

El responsable ordenó «Bueno sigue con la tarea, apruebo la adenda integramente» después de revisar `docs/traceability/FRONT_016_PLAN_DE_IMPLEMENTACION.md`. Se incorporan íntegramente por referencia sus §§2–8, especialmente las decisiones de composición, navegación, período sin escrituras, estados y catálogo BR-M10 de §§4–6. La solicitud inicial limitó el alcance a FRONT-016, fila 94 de Adenda 45, HU-023/HU-030 y CA/CP-023/030. No modifica contratos backend de Adendas 15/22 ni documentos congelados.

UI-E01/E02 se componen en `/mi-trabajo`; UI-E03 usa GET `/mi-trabajo/tareas/{obligationId:guid}`. Mi trabajo aparece en navegación con PER-BANDEJA-PROPIA, consulta con PER-TAREA-VER. Bandeja/avisos son propios para cuatro roles; el listado/detalle respeta la jerarquía vigente existente. BR-D13 se consume y BR-N04 se aplica. BR-M10 y las variantes Futura/Disponible/Sin leer/Leído se resuelven exclusivamente para esta historia con los tokens existentes.

Las consultas usan únicamente inbox y obligations; no invocan GET /weeks, ensure, generación ni otras operaciones que escriban. Los períodos se eligen desde filas autorizadas. Marcar leído exige CSRF y reautorización, sin cuerpo API, If-Match ni Idempotency-Key; no cambia la tarea. GET no marca leído ni inserta auditoría. Historia paginada y anti-IDOR conservan los contratos aceptados.

No se habilitan aportación, descarga, conclusión, validación ni FRONT-017. La aprobación permite implementar, validar proporcionalmente, actualizar trazabilidad y crear commit local. No autoriza push, PR, merge o despliegue. La autorización futura de publicación conserva el seguimiento y reparación del mismo hito definidos en INSTRUCCIONES_EJECUCION_TAREAS_FRONT.md.
