# SGOL — Adenda 64: definición y plan de descarga TECH-EVID-003

## 1. Aprobación e incorporación documental

2026-10-03. **ID, DEFINICIÓN Y PLAN APROBADOS E INCORPORADOS LOCALMENTE.** Aprobación literal del responsable: «Lo apruebo», en respuesta a la pregunta «¿Apruebas el identificador, la definición y el plan para su incorporación documental?» sobre `docs/traceability/BR_API04_PLAN_DE_IMPLEMENTACION.md`. Se incorpora por referencia su contenido presentado en §§1–9. Sus expresiones de propuesta/pending identifican el estado anterior a esta aprobación; las estimaciones permanecen estimaciones y los criterios futuros no equivalen a resultados ejecutados.

El número 64 se comprobó libre entre las adendas operativas de raíz antes de crear este documento. No se modifican F00–F07 congelados, Adendas 62/63 ni Fuentes/. Incorporación local no significa Publicada o Integrada en GitHub.

## 2. Definición y precedencia aprobadas

| Campo | Definición aprobada |
|---|---|
| Identificador | TECH-EVID-003 |
| Nombre | Descarga privada del binario original de evidencia mediante autorización temporal |
| Objetivo | Implementar exclusivamente GET /api/v1/files/{id}/download conforme a Adenda 63 / §§15.1–15.8 del contrato incorporado |
| Dependencias disponibles | HU-023, HU-025, TECH-EVID-001/002 y base integrada aceptada TECH-FRONT-005; no requiere repetir sus gates |
| Dependencia previa real | Verificación/resolución separada de Dirección del §4 del plan antes de reutilizar CanViewAsync como autoridad de descarga |
| Entrada a ejecución pendiente | Orden expresa de implementar y autorización separada de verificación Dirección; corrección eventual requiere orden propia |
| Aceptación local | Código y trazabilidad, build/pruebas enfocadas compatibles, criterios de §6 y matriz contractual §15.7 con evidencia real y diferidos precisos |
| Estado | Definida y plan aprobado; implementación no iniciada |

La tarea se incorpora mediante esta adenda, sin reescribir el backlog congelado ni inventar un segundo ID para Dirección. El plan define secuencia, archivos previstos, aceptación, recursos/cleanup y costo estimado; no amplía contrato, permisos o alcance.

## 3. Dirección, límites y punto de parada

La aprobación incluye la **propuesta de verificación separada** del §4 como parte del plan. No constituye orden de ensayarla ni corregirla: permanece pendiente su autorización separada. La verificación presentará resultado y, si confirma la diferencia, propuesta mínima de lectura antes de una orden de corrección. No sembrar asignación vigente para ocultar la diferencia, ampliar autoridad o extender la excepción a mutaciones. No reutilizar el helper mientras esa dependencia siga sin resolver/verificar.

Adendas 62/63 y contrato aprobado conservan vigencia, sin reaprobación: binario original LIMPIO/CLEAN vinculado a versión funcional, ambas versiones, PER-TAREA-VER/universo vigente, instantánea final, URL firmada máxima cinco minutos, límites aceptados de revocación/transferencias/copias/exposición de URI, DTO/cabeceras/errores cerrados y GET puro. BR-API04 sigue **ABIERTA globalmente**. Preview, transformación y UI de lectura siguen diferidas. D4/TAR-0026/CAT-006, D5/Adenda 61 y todos los diferidos con sus causas de §8 del plan permanecen; no PASS nuevo ni cierre global por implicación.

**Parada vigente:** definición e incorporación documental satisfechas; esperar orden expresa antes de implementar o ensayar. Sin código/pruebas de aplicación, publicación, PR/checks remotos, merge, despliegue, preview/UI o siguiente hito automático. La aprobación de este plan no altera esa separación de autorizaciones solicitada por el responsable.
