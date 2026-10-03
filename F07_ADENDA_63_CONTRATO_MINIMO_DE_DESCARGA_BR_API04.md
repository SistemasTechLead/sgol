# SGOL — Adenda 63: contrato mínimo de descarga de BR-API04

## 1. Aprobación e incorporación

2026-10-03. **CONTRATO APROBADO ÍNTEGRAMENTE E INCORPORADO LOCALMENTE.** Aprobación literal: «La apruebo integramente», en respuesta a la solicitud de aprobación íntegra de §15 de `docs/traceability/PROPUESTA_POST_TECH_FRONT_005.md`. La resolución global previa está aprobada e incorporada por Adenda 62. El número 63 se comprobó libre entre las adendas operativas de raíz antes de crear este archivo.

Se incorpora por referencia **exclusivamente §§15.1–15.8** del documento citado, con el contenido revisado en commit histórico `710dff1ee34f895b552d8854e363ed0cd324b1f9`. Sus expresiones de propuesta identifican el estado previo a la aprobación y ahora son reglas/criterios aprobados dentro de ese alcance. Las estimaciones permanecen estimaciones; la matriz de pruebas sigue siendo aceptación futura, no resultado ejecutado. No se extiende la aprobación a código, ensayos o secciones que exigen autorización separada. No se modifican F00–F07 congelados ni Fuentes/.

## 2. Contrato incorporado y límites aceptados

El contrato define únicamente `GET /api/v1/files/{id}/download`, ruta declarada en F06 §5.6, para binario original LIMPIO/CLEAN vinculado a versión funcional. Conserva S3 privado, cuarentena, tipo real, SHA-256, antimalware, vínculo exacto e historia inmutable. PER-TAREA-VER y universo vigente de HU-023 autorizan VIGENTE/SUSTITUIDA, con instantánea final de autoridad y sin heredar permisos de carga o actores históricos. DTO, cabeceras, nombre seguro, errores exactos y matriz por capa son los de §15, sin variantes ni ampliaciones.

Se aceptan expresamente los límites revisados: URL al portador de duración máxima cinco minutos, sin revocación inmediata garantizada tras pérdida de autoridad; sin promesa de detener transferencias iniciadas o retirar copias descargadas; componentes técnicos visibles dentro de la URI firmada al receptor autorizado, sin registrarla ni persistirla. GET puro, sin evento de descarga/lectura ni mutación de PostgreSQL/S3; emitir token no demuestra lectura. No proxy, preview, transformación, UI de lectura, URL permanente, CDN o acceso público.

BR-API04 permanece **ABIERTA globalmente**. Contrato aprobado no equivale a descarga Implementada localmente ni cierra preview por implicación. D4/TAR-0026/CAT-006, D5/Adenda 61 y todos los diferidos anteriores conservan su causa/alcance. No se acreditan ensayos nuevos ni se repiten los ciclos/gates aceptados.

## 3. Dependencias y punto de parada

La dependencia documental de aprobación/incorporación del contrato queda satisfecha localmente. No existe todavía tarea/ID ni plan de implementación aprobados. La diferencia estática de lectura Dirección sin asignación de §10.2 exige verificación/resolución separada antes de reutilizar ese helper como autoridad de descarga; esta aprobación no autoriza ese ensayo o corrección. No se siembra una asignación ni se amplía autoridad para ocultar la diferencia.

Se mantiene el punto de parada de §15.8: esperar definición y autorización expresa de la tarea/plan de código y de la verificación separada que corresponda. No asignar un nuevo ID por inferencia ni iniciar otro hito. Sin push, PR, checks remotos, merge, despliegue, cloud o cuentas externas por esta aprobación. La publicación documental futura también requiere autorización; **incorporada localmente** no significa Publicada/Integrada.

Fuentes: Adenda 62 y §§10–15 del documento citado, con F06_CONTRATO_DE_API.md §5.6, ADR-007, Adendas 15/17/18 y exclusiones consumidoras 57..60. El único documento de trabajo conserva la aprobación/trazabilidad; INDICE_IDS y IMPLEMENTATION_STATUS remiten a esta incorporación. La Adenda 62 conserva su estado histórico de propuesta del contrato, superado específicamente por esta aprobación; no se reescribe.
