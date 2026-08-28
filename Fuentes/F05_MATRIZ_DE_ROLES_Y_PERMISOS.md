# F05 — Matriz de roles y permisos del MVP de SGOL

## 1. Control

| Campo | Valor |
|---|---|
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Aprobación del responsable | `Apruebo los cinco entregables` |
| Alcance | `LOR-001`; cuatro roles canónicos; sólo capacidades MVP |
| Fuente | DEC-001, DEC-003, DEC-004, DEC-006, DEC-009, DEC-032, DEC-043, DEC-044, DEC-049, DEC-050, DEC-052 a DEC-054, DEC-067, DEC-069; F05-JP-001 a F05-JP-009 |

## 2. Reglas de autoridad

1. Jerarquía: `DIRECCION > ADMINISTRACION > SUBCOORDINACION > PISO_VENTAS`.
2. Una cuenta individual y un rol activo por usuario en `LOR-001`.
3. Puesto y turno son datos; no conceden permisos.
4. La autoridad descendente permite actuar sólo donde la operación lo indique; no acumula roles inferiores ni convierte al superior en ejecutor elegible.
5. El superior inmediato valida normalmente; niveles posteriores requieren escalamiento y motivo.
6. No hay autovalidación salvo Dirección.
7. Ningún usuario elimina auditoría, evidencias, versiones, asignaciones o validaciones.

## 3. Leyenda

- `A`: administra o ejecuta la operación completa.
- `P`: puede realizarla dentro de su nivel y niveles inferiores.
- `O`: sólo sobre registros propios.
- `I`: sólo sobre niveles inferiores dentro de `LOR-001`.
- `V`: sólo consulta dentro del alcance indicado.
- `—`: denegado.

## 4. Matriz consolidada

| Permiso estable | Operación | DIRECCION | ADMINISTRACION | SUBCOORDINACION | PISO_VENTAS | Restricción objetiva |
|---|---|---:|---:|---:|---:|---|
| PER-PERSONA-ADMIN | Altas, bajas, puesto, turno, sucursal | A | — | — | — | Conserva vigencia e historia. |
| PER-DISPONIBILIDAD-ADMIN | Capturar/corregir disponibilidad | A | — | — | — | Valor binario por fecha. |
| PER-USUARIO-ADMIN | Crear, activar/desactivar cuentas | A | — | — | — | Cuenta individual vinculada a persona. |
| PER-ROL-ADMIN | Asignar/cambiar/revocar rol | A | — | — | — | Un rol activo; nunca por puesto. |
| PER-SUCURSAL-ADMIN | Mantener `LOR-001` | A | — | — | — | No crea otra sucursal en MVP. |
| PER-CALENDARIO-ADMIN | Laborables, festivos, cierres extraordinarios | A | — | — | — | Versionado y auditado. |
| PER-CONFIG-ADMIN | Configuración general versionada | A | — | — | — | Publicación con vigencia. |
| PER-DEFINICION-ADMIN | Definiciones de las ocho TAR | A | — | — | — | No admite novena definición. |
| PER-ACTIVACION-ADMIN | Políticas manuales y recurrentes | A | — | — | — | Rechaza eventos/condiciones automáticos. |
| PER-POLITICA-ADMIN | Rol, disponibilidad y turno por TAR | A | — | — | — | Rol canónico exacto. |
| PER-EVIDENCIA-CONFIG | Evidencia por TAR | A | — | — | — | Versionada; no retroactiva. |
| PER-VALIDACION-CONFIG | Requisito y autoridad de validación | A | — | — | — | Aplica F05-JP-001 a 008. |
| PER-OBLIGACION-CREAR | Crear obligación manual | P | P | P | — | Sólo para el propio nivel o inferiores; TAR activa. |
| PER-CARGA-VER | Consultar carga activa | V | V | V | O | Dirección todo; otros, propia e inferior. |
| PER-ASIGNACION-EXPLICAR | Consultar explicación automática | V | V | V | O | Mismo alcance jerárquico. |
| PER-ASIGNACION-CORREGIR | Sustituir responsable | I | I | I | — | Sólo nivel inferior, nuevo elegible y motivo. |
| PER-PLAN-VER | Consultar plan | V | V | V | O | Dirección todo; otros propio e inferior. |
| PER-PLAN-PUBLICAR | Publicar plan/versiones | P | P | P | — | Propio nivel e inferiores; nunca nivel superior. |
| PER-TAREA-VER | Consultar tarea e historia | V | V | V | O | Sin acceso a pares ajenos ni superiores. |
| PER-TAREA-EJECUTAR | Concluir tarea | O | O | O | O | Sólo obligación actualmente asignada. |
| PER-EVIDENCIA-APORTAR | Registrar/sustituir antes de concluir | O | O | O | O | Sólo evidencia de tarea propia. |
| PER-EVIDENCIA-SUSTITUIR | Sustituir después de conclusión | I | I | I | — | Superior del responsable y motivo. |
| PER-VALIDACION-EMITIR | Emitir validación ordinaria | I/O* | I | I | — | Sólo superior inmediato; `O*` permite autovalidación de Dirección. |
| PER-VALIDACION-ESCALAR | Validar por escalamiento | A | I | I | — | Nivel posterior, motivo obligatorio. |
| PER-VALIDACION-SUSTITUIR | Sustituir validación | A | I | I | — | Validador original o superior; motivo. |
| PER-BANDEJA-PROPIA | Consultar bandeja | O | O | O | O | Sin mensajería externa. |
| PER-SUPERVISION-VER | Trabajo, evidencia y validaciones inferiores | A | I | I | — | No expone pares ni superiores. |
| PER-INDICADOR-VER | Indicadores básicos | V | V | V | O | Sólo datos del alcance; sin monetarios. |
| PER-DIRECCION-VER | Vista integral `LOR-001` | A | — | — | — | Incluye todos los niveles. |
| PER-AUDITORIA-VER | Consultar auditoría | V | V | V | O | Misma regla jerárquica. |
| PER-AUDITORIA-ELIMINAR | Eliminar auditoría o versiones | — | — | — | — | Prohibido a todo usuario. |
| PER-CONTINUIDAD-VER | Ver resultado de recuperación | A | — | — | — | Ejecutar recuperación es decisión técnica F06. |

## 5. Actor por tarea MVP

| Tarea | Rol ejecutor exacto | Creador manual permitido | Validador ordinario | Escalamiento permitido |
|---|---|---|---|---|
| TAR-0005 | SUBCOORDINACION | No aplica; recurrente | ADMINISTRACION | DIRECCION con motivo |
| TAR-0007 | PISO_VENTAS | SUBCOORDINACION, ADMINISTRACION o DIRECCION | SUBCOORDINACION | ADMINISTRACION o DIRECCION con motivo |
| TAR-0008 | SUBCOORDINACION | SUBCOORDINACION, ADMINISTRACION o DIRECCION | ADMINISTRACION | DIRECCION con motivo |
| TAR-0011 | SUBCOORDINACION | SUBCOORDINACION, ADMINISTRACION o DIRECCION | ADMINISTRACION | DIRECCION con motivo |
| TAR-0018 | PISO_VENTAS | SUBCOORDINACION, ADMINISTRACION o DIRECCION | SUBCOORDINACION | ADMINISTRACION o DIRECCION con motivo |
| TAR-0026 | ADMINISTRACION | No aplica; recurrente | DIRECCION | No aplica; Dirección es nivel máximo |
| TAR-0092 | SUBCOORDINACION | SUBCOORDINACION, ADMINISTRACION o DIRECCION | ADMINISTRACION | DIRECCION con motivo |
| TAR-0093 | SUBCOORDINACION | SUBCOORDINACION, ADMINISTRACION o DIRECCION | ADMINISTRACION | DIRECCION con motivo |

## 6. Pruebas mínimas de autorización

| ID | Caso | Resultado esperado |
|---|---|---|
| PRM-001 | Administración intenta crear usuario o asignar rol. | `ACCESO_DENEGADO`; sin cambio; intento auditado. |
| PRM-002 | Piso intenta publicar plan. | Denegado; plan sin nueva versión. |
| PRM-003 | Subcoordinación consulta tarea de un par no subordinado. | Denegado. |
| PRM-004 | Administración corrige asignación de Subcoordinación con motivo y candidato elegible. | Nueva asignación vigente; anterior histórica. |
| PRM-005 | Subcoordinación intenta corregir asignación de Administración. | Denegado. |
| PRM-006 | Responsable no Dirección intenta validar su tarea. | `AUTOVALIDACION_NO_PERMITIDA`. |
| PRM-007 | Dirección valida tarea propia. | Permitido y auditado como autovalidación. |
| PRM-008 | Superior sustituye evidencia posterior a conclusión sin motivo. | Rechazado. |
| PRM-009 | Cualquier rol intenta eliminar auditoría. | Rechazado sin pérdida de registros. |
| PRM-010 | Puesto textual coincide con un rol no asignado. | No concede permiso ni elegibilidad. |

## 7. Vacíos deliberadamente diferidos

Autenticación, credenciales, sesiones, MFA, cifrado, almacenamiento de permisos y evaluación técnica de políticas se definen en F06. Esta matriz define el resultado autorizado y denegado que la solución técnica deberá preservar.
