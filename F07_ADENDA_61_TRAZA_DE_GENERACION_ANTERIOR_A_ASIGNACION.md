# SGOL — Adenda 61: dependencia de generación anterior a la asignación

2026-10-02. **APROBADA E INCORPORADA LOCALMENTE.** Aprobación literal: «Aprobar D5 y corrección mínima separada», en respuesta a la ampliación expresa limitada a esta adenda y corrección mínima de lectura backend, en commit separado. Incorpora D5 de §14 del único plan TECH_FRONT_005_PLAN_DE_IMPLEMENTACION.md. No altera documentos congelados ni Fuentes/.

## Decisión y límite

Extiende únicamente la clasificación de dependencias de Adenda 29 §§7/8/17 en modo `traceObligationId`. Dirección con PER-AUDITORIA-VER vigente puede recibir hechos persistidos legítimos de creación/generación anteriores a la primera asignación conservada como `scope.relation = DEPENDENCY`, con `subjectPersonId` y `subjectLevel` nulos. No atribuye el hecho al responsable futuro ni al actor histórico.

El conjunto cerrado de pares existentes es GENERATION_REQUEST / GENERATION_REQUEST_ACCEPTED o GENERATION_REQUEST_RECOVERED, y WORK_OBLIGATION / WORK_OBLIGATION_CREATED o WORK_OBLIGATION_RECOVERED. Exige outcome SUCCESS, branchId explícito LOR-001, identificador del recurso exacto, vínculo recíproco request↔obligation inequívoco en LOR-001 y occurredAt estrictamente anterior a la primera assignment_version conservada e inequívoca. No acepta texto JSON, correlación, cercanía temporal, recursos extra, ausencia de asignación ni una relación incompleta como sustitutos del vínculo.

La lista general y el detalle conservan la clasificación histórica vigente; esta extensión sólo reconstruye esa obligación en modo traza para Dirección. Los demás roles conservan la convergencia completa a 404 AUDIT_EVENT_NOT_FOUND cuando algún eslabón no tiene sujeto autorizado. Otros hechos ausentes, desconocidos, ambiguos o contradictorios conservan 500 AUDIT_SCOPE_INCONSISTENT para Dirección. Los hechos posteriores se resuelven con la asignación aplicable al instante; nunca se reclasifican para eludir jerarquía o historia.

## Evidencia y seguridad

La consulta sigue READ ONLY / REPEATABLE READ, sin escritura ni auditoría de GET. Conserva minimización, orden cronológico, cursores vinculados al actor/filtros y cerca temporal, y completeness de configuración/asignación/evidencia/validación. No sintetiza eventos, modifica auditoría, repara datos ni elimina eslabones.

Validar en PostgreSQL real: pares reconocidos anteriores, sujetos nulos, minimización, cadena/paginación completa; tres roles no Dirección denegados sin salida parcial; hechos desconocidos, recurso incompatible, sucursal ausente y vínculo no recíproco rechazados; permisos vigentes y no-efecto. Reutilizar los negativos de jerarquía, historia, cursor y auditoría inmutable existentes. Los dos ciclos integrales siguen pendientes hasta validar la cabeza que contiene esta corrección.

Sin endpoint, permiso, DTO, productor, migración, dependencia, indicador, TAR, exportación, observabilidad productiva, reparación o despliegue nuevos. La autorización no incluye publicación remota ni merge. D4 y BR-API04 global abierta permanecen vigentes.
