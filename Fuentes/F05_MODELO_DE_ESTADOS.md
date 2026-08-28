# F05 — Modelo funcional de estados del MVP de SGOL

## 1. Control

| Campo | Valor |
|---|---|
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Aprobación del responsable | `Apruebo los cinco entregables` |
| Regla central | Estados separados por dominio; ningún hecho sobrescribe otro |
| Exclusiones | Inicio medido, excepciones, cancelación, posposición, traslado, cierre forzado, cierre/reapertura formal y estados legacy ambiguos |

## 2. Principios

1. Ejecución, evidencia, asignación, validación, publicación y auditoría tienen ciclos separados.
2. `VENCIDA` es una bandera calculada cuando la fecha actual supera el vencimiento de una obligación `PENDIENTE`; no es transición.
3. Una validación `NO_CUMPLIDA` no cambia la ejecución `CONCLUIDA`.
4. Sustituir crea una versión vigente nueva y conserva la anterior como `SUSTITUIDA`.
5. No existen eliminaciones funcionales.

## 3. Catálogo por dominio

| Dominio | Estado | Significado | Entra por | Sale por |
|---|---|---|---|---|
| Persona | ACTIVA | Puede tener cuenta, disponibilidad y elegibilidad. | Alta/reactivación por Dirección. | Baja funcional. |
| Persona | INACTIVA | No es elegible; historia permanece. | Baja funcional. | Reactivación por Dirección. |
| Cuenta | ACTIVA | Puede acceder conforme a su rol. | Alta/reactivación por Dirección. | Desactivación. |
| Cuenta | INACTIVA | Acceso denegado; hechos previos conservados. | Desactivación. | Reactivación. |
| Rol asignado | ACTIVO | Único rol vigente del usuario en `LOR-001`. | Asignación por Dirección. | Cambio o revocación. |
| Rol asignado | SUSTITUIDO | Asignación histórica sin autoridad actual. | Cambio/revocación. | Terminal histórico. |
| Configuración/definición/política | BORRADOR | Aún no gobierna operación. | Creación por Dirección. | Publicación o descarte antes de vigencia. |
| Configuración/definición/política | VIGENTE | Gobierna nuevas operaciones desde su vigencia. | Publicación válida. | Nueva versión o desactivación. |
| Configuración/definición/política | SUSTITUIDA | Fue vigente; gobierna sólo historia asociada. | Publicación de nueva versión. | Terminal histórico. |
| Definición de tarea | INACTIVA_NUEVAS | No genera obligaciones nuevas; historia visible. | Desactivación por Dirección. | Nueva versión activa aprobada. |
| Período semanal | VIGENTE | Semana ISO actual según fecha local. | Cálculo por calendario. | Paso del tiempo. |
| Período semanal | TRANSCURRIDA | Semana finalizada por fecha, sin cierre formal. | Paso del tiempo. | No se reabre en MVP. |
| Solicitud de generación | ACEPTADA | Datos válidos; puede producir obligación. | Alta manual o recurrencia válida. | Materialización o recuperación. |
| Solicitud de generación | RECUPERADA | Misma clave ya procesada; referencia existente. | Reintento idempotente. | Terminal. |
| Solicitud de generación | OMITIDA | Recurrencia correspondía a día no laborable. | Evaluación de calendario. | Terminal. |
| Solicitud de generación | RECHAZADA | Regla, alcance u origen inválido. | Validación fallida. | Terminal; un nuevo intento corregido es otra evaluación. |
| Plan | BORRADOR | Plan semanal único aún no publicado. | Creación/recuperación del plan. | Primera publicación. |
| Plan | PUBLICADO | Al menos una versión publicada. | Publicación autorizada. | Permanece; actualizaciones crean versiones. |
| Versión de plan | VIGENTE | Última publicación visible para su alcance. | Publicación inicial o incremental. | Nueva publicación. |
| Versión de plan | SUSTITUIDA | Publicación histórica reconstruible. | Nueva publicación. | Terminal histórico. |
| Obligación/ejecución | PENDIENTE | Trabajo activo, esté futuro, disponible, vencido o bloqueado por evidencia. | Creación única. | Conclusión con evidencia completa. |
| Obligación/ejecución | CONCLUIDA | Responsable registró resultado y evidencia completa. | Conclusión válida. | Terminal en MVP; no hay reapertura. |
| Asignación | VIGENTE | Responsable actual de la obligación. | Asignación automática o corrección. | Corrección posterior. |
| Asignación | SUSTITUIDA | Responsable anterior conservado. | Corrección trazada. | Terminal histórico. |
| Evidencia | VIGENTE | Versión considerada actualmente. | Aporte o sustitución. | Sustitución autorizada. |
| Evidencia | SUSTITUIDA | Versión previa aún consultable. | Sustitución. | Terminal histórico. |
| Revisión de evidencia | COMPLETA | Todos los requisitos aplicables tienen versión vigente. | Evaluación estructural. | Puede cambiar si se sustituye evidencia; se reevalúa. |
| Revisión de evidencia | INCOMPLETA | Falta al menos un requisito aplicable. | Evaluación estructural. | Aporte/sustitución y reevaluación. |
| Validación requerida | PENDIENTE | Ejecución concluida sin decisión vigente. | Conclusión de tarea validable. | Emisión de decisión. |
| Decisión de validación | VIGENTE | Única decisión actual. | Emisión o sustitución autorizada. | Sustitución. |
| Decisión de validación | SUSTITUIDA | Decisión histórica sin vigencia. | Sustitución con motivo. | Terminal histórico. |

## 4. Resultados que no son estados

| Concepto | Valores | Regla |
|---|---|---|
| Resultado de validación | CUMPLIDA, INCOMPLETA, NO_CUMPLIDA | Atributo de la decisión vigente; no cambia la ejecución. |
| Condición temporal | PROGRAMADA, DISPONIBLE, VENCIDA | Derivada de fechas mientras la obligación está `PENDIENTE`. |
| Integridad de evidencia | COMPLETA, INCOMPLETA | Resultado recalculable, no estado de ejecución. |
| Resultado de generación | ACEPTADA, RECUPERADA, OMITIDA, RECHAZADA | Resultado de solicitud; sólo ACEPTADA puede crear. |
| Resultado TAR-0005 | REVISION_CONFORME_SIN_ACCION o ACCION_REGISTRADA | Resultado de negocio de la tarea, no validación. |

## 5. Transiciones permitidas

| ID | Dominio | Origen | Acción/guarda | Destino | Actor |
|---|---|---|---|---|---|
| TR-001 | Persona | ACTIVA | Baja funcional | INACTIVA | DIRECCION |
| TR-002 | Persona | INACTIVA | Reactivar | ACTIVA | DIRECCION |
| TR-003 | Cuenta | ACTIVA | Desactivar | INACTIVA | DIRECCION |
| TR-004 | Cuenta | INACTIVA | Reactivar | ACTIVA | DIRECCION |
| TR-005 | Rol | ACTIVO | Cambiar/revocar | SUSTITUIDO | DIRECCION |
| TR-006 | Configuración | BORRADOR | Validar y publicar | VIGENTE | DIRECCION |
| TR-007 | Configuración | VIGENTE | Publicar sucesora | SUSTITUIDA | DIRECCION |
| TR-008 | Definición | VIGENTE | Desactivar nuevas generaciones | INACTIVA_NUEVAS | DIRECCION |
| TR-009 | Solicitud | — | Datos válidos y clave nueva | ACEPTADA | SISTEMA/creador |
| TR-010 | Solicitud | — | Clave existente idéntica | RECUPERADA | SISTEMA |
| TR-011 | Solicitud | — | Recurrencia en inhábil | OMITIDA | SISTEMA |
| TR-012 | Solicitud | — | Datos/alcance/regla inválidos | RECHAZADA | SISTEMA |
| TR-013 | Plan | BORRADOR | Publicar con autoridad | PUBLICADO | DIRECCION/ADMINISTRACION/SUBCOORDINACION |
| TR-014 | Versión plan | VIGENTE | Publicación incremental | SUSTITUIDA + nueva VIGENTE | Actor autorizado |
| TR-015 | Obligación | PENDIENTE | Evidencia COMPLETA y actor responsable | CONCLUIDA | Responsable |
| TR-016 | Asignación | VIGENTE | Corrección autorizada, elegible y motivada | SUSTITUIDA + nueva VIGENTE | Superior |
| TR-017 | Evidencia | VIGENTE | Sustitución autorizada | SUSTITUIDA + nueva VIGENTE | Responsable antes de concluir; superior después |
| TR-018 | Validación | PENDIENTE | Autoridad válida emite resultado | Decisión VIGENTE | Superior inmediato/escalado |
| TR-019 | Decisión validación | VIGENTE | Validador original o superior, con motivo | SUSTITUIDA + nueva VIGENTE | Autoridad válida |

## 6. Transiciones prohibidas en MVP

| ID | Transición | Motivo |
|---|---|---|
| TP-001 | `PENDIENTE → CANCELADA/POSPUESTA/TRASLADADA` | CAP-034 a CAP-036 son posteriores. |
| TP-002 | `CONCLUIDA → PENDIENTE` | Reapertura funcional no está en MVP. |
| TP-003 | Vencimiento → `NO_CUMPLIDA` | DEC-066 prohíbe resultado automático. |
| TP-004 | Validación desfavorable → nueva tarea/reapertura | DEC-025 exige decisión individual; flujo no incluido. |
| TP-005 | Semana transcurrida → cerrada/reabierta | CAP-037/CAP-038 son posteriores. |
| TP-006 | Cualquier estado → eliminado | Historia y auditoría son inmutables para usuarios. |
| TP-007 | Evidencia/validación VIGENTE → sobrescrita | Debe crearse versión y conservar anterior. |
| TP-008 | `PENDIENTE → INICIADA` | CAP-026 quedó posterior; ninguna tarea MVP mide duración. |

## 7. Guardas críticas

- G-001: sólo una versión vigente por objeto versionado y alcance.
- G-002: sólo una asignación vigente por obligación.
- G-003: conclusión requiere responsable actual y evidencia completa.
- G-004: validación requiere tarea concluida, autoridad válida y ausencia de autovalidación no permitida.
- G-005: sustitución posterior a conclusión requiere superior y motivo.
- G-006: una publicación incremental nunca crea un segundo plan para la semana.
- G-007: una recuperación o reintento nunca crea una segunda identidad de obligación.
