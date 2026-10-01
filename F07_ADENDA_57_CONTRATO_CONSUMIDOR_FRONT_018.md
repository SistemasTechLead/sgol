# F07 Adenda 57 — Contrato consumidor de FRONT-018

2026-10-01. **APROBADA ÍNTEGRAMENTE — INCORPORADA LOCALMENTE**.

El responsable aprobó el único plan `docs/traceability/FRONT_018_PLAN_DE_IMPLEMENTACION.md` mediante «Si apruebo integramente el plan». Se incorporan íntegramente sus §§3–10 como contrato consumidor de FRONT-018: UI-E06/E07/E08, mensajes, estados, proyección opcional evidenceActions, permisos existentes, intenciones y validaciones. La aprobación habilita implementación y commits locales, no publicación, merge ni despliegue.

BR-API04 se resuelve sólo para este consumidor mediante exclusión explícita de descarga/preview; no se declara cerrada globalmente. BR-D04/M08 consumen la composición y catálogo literal de §§5–7. No hay nuevo endpoint, permiso, payload estructurado ni capacidad FRONT-019/020.

Se conserva expresamente la excepción de Adenda 19: GET evidence-review puede crear un snapshot inmutable y EVIDENCE_REVIEW_SNAPSHOT_CREATED en la misma transacción; misma huella reutiliza sin filas nuevas. Las demás lecturas no escriben. Ninguna revisión cambia ejecución, evidencia, asignación, validación, idempotencia u outbox.

Concluir usa cero bytes de cuerpo, CSRF, clave original e If-Match de obligación. Sustituir conserva autoridad/estado/motivo, clave original e If-Match del ítem; sólo LIMPIO puede vincularse. La sustitución no cambia cierre ni decisión anterior. Fuentes y congelados permanecen intactos. La transición documental 96dfe4d se conserva junto a este hito.
