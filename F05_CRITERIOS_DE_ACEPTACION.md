# F05 — Criterios de aceptación y casos de prueba del MVP de SGOL

## 1. Control

| Campo | Valor |
|---|---|
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Aprobación del responsable | `Apruebo los cinco entregables` |
| Cobertura | 35 historias/capacidades y 8 políticas de tarea |
| Convención | `P` prueba positiva; `N` prueba negativa o de borde |

## 2. Condiciones comunes de prueba

Salvo que un caso indique otra cosa:

- sucursal `LOR-001`, zona `America/Mexico_City` y semana ISO conocida;
- cuentas individuales para los cuatro roles;
- calendario con al menos un día laborable y uno no laborable;
- dos personas elegibles por rol con cargas y fechas de última asignación controladas;
- ocho definiciones vigentes con políticas F05-JP-001 a F05-JP-008;
- reloj de prueba controlable;
- cada prueba verifica tanto el resultado visible como los eventos de auditoría;
- una prueba negativa debe confirmar ausencia de efectos parciales o pérdida histórica.

## 3. Criterios y pruebas por capacidad

| ID | Criterio objetivo | Caso positivo | Caso negativo o borde |
|---|---|---|---|
| CA-001 | Dirección crea una persona con código único y cambia su vigencia conservando ambos hechos. | CP-001-P: dada Dirección y código nuevo, al crear y dar baja quedan ACTIVA→INACTIVA y dos eventos. | CP-001-N: dado código existente, al crear otra persona se rechaza y no cambia el conteo. |
| CA-002 | Puesto/turno pueden cambiar sin modificar rol, permisos o elegibilidad por rol. | CP-002-P: cambiar turno conserva el rol activo y audita antes/después. | CP-002-N: asignar puesto “Director” a Piso no concede `PER-DIRECCION-VER`. |
| CA-003 | Existe exactamente un valor binario de disponibilidad vigente por persona y fecha. | CP-003-P: Dirección registra disponible y luego corrige a no disponible conservando versión. | CP-003-N: porcentaje, intervalo o valor vacío se rechaza. |
| CA-004 | Carga activa cuenta sólo obligaciones PENDIENTE actualmente asignadas. | CP-004-P: con 3 pendientes y 2 concluidas muestra 3. | CP-004-N: sustituir una asignación no cuenta la misma obligación para ambos responsables. |
| CA-005 | El catálogo MVP contiene `LOR-001`/Loretta y nunca una sucursal `TODAS`. | CP-005-P: consulta global usa alcance TODAS y devuelve Loretta. | CP-005-N: intentar crear `TODAS` u otra sucursal se rechaza. |
| CA-006 | Cada cuenta activa pertenece a una persona y no puede compartirse. | CP-006-P: Dirección crea y desactiva una cuenta individual conservando historia. | CP-006-N: vincular una cuenta a dos personas se rechaza. |
| CA-007 | Un usuario tiene un rol activo y la visibilidad sigue la jerarquía. | CP-007-P: Administración ve lo propio e inferior, no Dirección. | CP-007-N: segundo rol simultáneo o acceso a un par se rechaza. |
| CA-008 | Publicar configuración crea una versión vigente y vuelve histórica la anterior sin afectar hechos previos. | CP-008-P: publicar V2 deja V1 SUSTITUIDA y nuevas operaciones usan V2. | CP-008-N: vigencias solapadas o publicación no Dirección se rechazan. |
| CA-009 | Semana, laborabilidad y fechas se resuelven en ISO y `America/Mexico_City`. | CP-009-P: lunes inicia semana y festivo configurado resulta no laborable. | CP-009-N: zona distinta o fecha inválida no se publica. |
| CA-010 | Existe un período único por sucursal/semana y el paso del tiempo no ejecuta cierre/reapertura. | CP-010-P: obtener dos veces la misma semana devuelve el mismo período. | CP-010-N: intentar reabrir/cerrar formalmente devuelve operación fuera del MVP. |
| CA-011 | Sólo ocho TAR aprobadas y activas generan trabajo; versiones previas permanecen consultables. | CP-011-P: versionar TAR-0005 conserva V1 y deja V2 vigente. | CP-011-N: activar TAR-0001 o TAR-0195 para generar se rechaza. |
| CA-012 | Las reglas MVP aceptan sólo alta manual y recurrencia. | CP-012-P: configurar manual para TAR-0007 y recurrente para TAR-0005 es válido. | CP-012-N: configurar evento, condición o sistema externo se rechaza. |
| CA-013 | Cada recurrencia laborable produce una ocurrencia; inhábil omite y reintento no duplica. | CP-013-P: evaluar TAR-0005 en laborable genera exactamente ventanas 12:00/17:00. | CP-013-N: en inhábil genera cero; repetir evaluación conserva cero o recupera existentes. |
| CA-014 | La solicitud usa una clave única de regla+alcance+período+origen. | CP-014-P: solicitud completa queda ACEPTADA y auditada. | CP-014-N: reenvío idéntico queda RECUPERADA; origen distinto con misma clave se rechaza como conflicto. |
| CA-015 | Una solicitud aceptada materializa una sola obligación con identidad estable y versión de TAR. | CP-015-P: crear y reintentar devuelve el mismo ID de obligación. | CP-015-N: una falla funcional no deja dos obligaciones ni una obligación parcial. |
| CA-016 | Elegibles cumplen simultáneamente vigencia, sucursal, rol exacto, disponibilidad y turno aplicable. | CP-016-P: sólo la persona que cumple los cuatro gates entra a candidatos. | CP-016-N: puesto parecido, rol superior o no disponible queda excluido con razón. |
| CA-017 | Cada TAR tiene política versionada con rol canónico exacto y turno no restrictivo salvo configuración expresa. | CP-017-P: consultar ocho TAR devuelve roles de la tabla F05 y sin turno requerido. | CP-017-N: política con texto de puesto o TAR no MVP no se publica. |
| CA-018 | La asignación selecciona menor carga; desempata por mayor espera y luego código. | CP-018-P: datos controlados producen el candidato esperado y explicación completa. | CP-018-N: repetir con mismos datos no cambia ganador ni crea segunda asignación. |
| CA-019 | Un superior corrige a un candidato elegible con motivo y conserva ambas asignaciones. | CP-019-P: Administración corrige tarea de Subcoordinación y queda historial antes/después. | CP-019-N: par, inferior, motivo vacío o candidato inelegible se rechaza. |
| CA-020 | Hay un solo plan por `LOR-001` y semana, con todas las áreas/niveles incluidos. | CP-020-P: obligaciones de dos niveles aparecen en el mismo plan. | CP-020-N: segundo intento devuelve el mismo plan y no crea otro. |
| CA-021 | Publicación respeta jerarquía y una obligación tardía crea versión del mismo plan. | CP-021-P: Subcoordinación publica Piso; obligación tardía produce V2 con V1 histórica. | CP-021-N: Piso publica o Subcoordinación publica Administración y se rechaza. |
| CA-022 | Sólo el responsable concluye una tarea y únicamente con evidencia completa. | CP-022-P: responsable con evidencia completa cambia PENDIENTE→CONCLUIDA. | CP-022-N: faltante, actor distinto o vencimiento solo deja PENDIENTE. |
| CA-023 | Consulta muestra origen, fechas, bandera vencida e historia sólo dentro del alcance. | CP-023-P: superior abre tarea inferior y reconstruye sus eventos. | CP-023-N: usuario par/superior fuera de alcance recibe acceso denegado sin filtración. |
| CA-024 | La política de evidencia de cada TAR coincide con sección 4 y aplica la versión de creación. | CP-024-P: las ocho TAR devuelven requisitos y condiciones aprobados. | CP-024-N: publicar cambio no altera requisitos de una obligación ya creada. |
| CA-025 | Toda sustitución conserva versiones; después de concluir requiere superior y motivo. | CP-025-P: superior sustituye evidencia concluida y ambas versiones son consultables. | CP-025-N: responsable sustituye después de concluir o superior omite motivo y se rechaza. |
| CA-026 | La revisión identifica exactamente cada evidencia faltante y bloquea conclusión. | CP-026-P: todos los requisitos aplicables producen COMPLETA. | CP-026-N: falta uno de varios tipos produce INCOMPLETA y señala el faltante. |
| CA-027 | Las ocho TAR requieren validación y resuelven superior inmediato canónico. | CP-027-P: cada rol ejecutor produce el validador ordinario de la matriz. | CP-027-N: puesto textual, par o rol inferior no se acepta como autoridad. |
| CA-028 | Una ejecución concluye separadamente y recibe una sola decisión vigente con resultado permitido. | CP-028-P: superior emite CUMPLIDA; ejecución sigue CONCLUIDA; sustitución motivada conserva ambas. | CP-028-N: autovalidación no Dirección, resultado desconocido o segunda decisión sin sustitución se rechaza. |
| CA-029 | Indicadores reproducen conteos definidos con período, alcance y denominador visibles. | CP-029-P: conjunto conocido concilia pendientes, concluidas, validadas, incumplidas y carga. | CP-029-N: vencida sin NO_CUMPLIDA no entra en incumplidas; no aparece monto. |
| CA-030 | Bandeja personal muestra sólo tareas propias y avisos internos sin alterar estados. | CP-030-P: responsable ve futura, disponible, vencida y evidencia faltante según datos. | CP-030-N: tarea ajena no aparece y no se emite correo/SMS. |
| CA-031 | Superior ve únicamente niveles inferiores y puede identificar validaciones pendientes. | CP-031-P: Administración ve Subcoordinación/Piso y sus evidencias. | CP-031-N: no ve Dirección ni Administración par. |
| CA-032 | Dirección ve toda la operación y sólo cinco indicadores aprobados. | CP-032-P: tablero concilia con datos base de todos los roles. | CP-032-N: búsqueda de incentivo, nómina o salud de integración no devuelve indicador. |
| CA-033 | Toda operación crítica queda atribuida y ningún usuario elimina auditoría/versiones. | CP-033-P: reconstruir configuración→asignación→evidencia→validación devuelve actor/fecha/cambio. | CP-033-N: intento de eliminación por Dirección también se rechaza y audita. |
| CA-034 | Repetir una operación idempotente recupera el resultado; un conflicto se registra sin duplicar. | CP-034-P: dos solicitudes idénticas devuelven mismo ID y una obligación. | CP-034-N: misma clave con contenido distinto queda RECHAZADA y audita el conflicto. |
| CA-035 | Una recuperación funcional conserva IDs, vínculos, versiones y conteos; diferencias quedan visibles. | CP-035-P: comparar conjunto antes/después produce igualdad funcional completa. | CP-035-N: evidencia o auditoría faltante hace fallar la reconciliación; no se oculta ni recrea como falsa. |

## 4. Criterios y pruebas por tarea

| ID | Tarea y criterio objetivo | Caso positivo | Caso negativo o borde |
|---|---|---|---|
| CAT-001 | TAR-0005 crea dos revisiones por laborable; cálculo `venta/meta`; <90 % exige acción; ≥90 % acepta conformidad. | CPT-001-P: meta 100, venta 85 y acción completa concluyen y pasan a validación. | CPT-001-N: 85 % sin acción o fuente no concluye; inhábil no genera. |
| CAT-002 | TAR-0007 usa separado y vencimiento documentados; no concluye antes; acredita retorno a exhibición. | CPT-002-P: al vencer 24 h, responsable registra liberación y evidencia completa. | CPT-002-N: antes del vencimiento, sin referencia o duplicando separado+vencimiento se bloquea/recupera. |
| CAT-003 | TAR-0008 exige operación, detección, al menos dos reclamantes, evidencia, decisión fundada y aviso privado; objetivo 30 min. | CPT-003-P: expediente completo resuelto en 25 min concluye y admite `CUMPLIDA`. | CPT-003-N: un reclamante o decisión sin fundamento no concluye; expediente completo a 31 min puede concluir, queda vencido y el criterio temporal admite `NO_CUMPLIDA`. |
| CAT-004 | TAR-0011 exige autorización previa y correspondencia entre solución, documentos, entrega y aviso; objetivo 7 hábiles. | CPT-004-P: expediente autorizado y conciliado concluye dentro de siete hábiles y admite `CUMPLIDA`. | CPT-004-N: sin autorización, comprobante o entrega no concluye; expediente completo posterior al objetivo admite `NO_CUMPLIDA`. |
| CAT-005 | TAR-0018 exige diez ítems conformes, foto final y planograma/lista vigente. | CPT-005-P: todos los ítems `SI` y ambas evidencias permiten concluir. | CPT-005-N: un ítem `NO`, foto o planograma faltante bloquea conclusión. |
| CAT-006 | TAR-0026 genera una obligación por servicio+vencimiento tres hábiles antes; acredita pago antes del vencimiento ajustado. | CPT-006-P: vencimiento inhábil se ajusta al hábil anterior y comprobante+FORM-ADM-02 oportunos permiten concluir y admitir `CUMPLIDA`. | CPT-006-N: reintento no duplica; sin comprobante queda pendiente; con comprobante posterior puede concluir y admite `NO_CUMPLIDA`. |
| CAT-007 | TAR-0092 exige ID único, documentos y F-ENT-001; si hay diferencia/daño, también foto. | CPT-007-P: recepción conforme sin diferencia concluye sin foto; recepción con daño concluye con foto y diferencia documentada. | CPT-007-N: daño sin foto o segundo caso activo del mismo ID no concluye/duplica. |
| CAT-008 | TAR-0093 se vincula a TAR-0092 y exige foto, anotación y aviso interno. | CPT-008-P: incidente físico vinculado con tres evidencias concluye y se valida. | CPT-008-N: sin padre, sin foto o intentando aviso externo se rechaza. |

## 5. Pruebas transversales de estados

| ID | Preparación y acción | Resultado esperado |
|---|---|---|
| CPE-001 | Dejar una obligación pendiente más allá de su vencimiento. | Continúa `PENDIENTE`, bandera `VENCIDA=true`, sin validación automática. |
| CPE-002 | Emitir `NO_CUMPLIDA` sobre ejecución concluida. | Ejecución sigue `CONCLUIDA`; decisión queda separada. |
| CPE-003 | Sustituir evidencia después de una validación. | Evidencia anterior histórica; validación no cambia automáticamente. |
| CPE-004 | Intentar cancelar, trasladar, posponer, cerrar o reabrir. | Operación no disponible en MVP; ningún estado cambia. |
| CPE-005 | Publicar obligación tardía. | Mismo plan, nueva versión vigente, versión anterior histórica. |

## 6. Gate de objetividad

Una historia se considera probada sólo si:

1. se ejecutan su caso positivo y negativo;
2. el resultado observable coincide con el criterio;
3. se verifica la ausencia de efectos no autorizados;
4. la auditoría permite identificar actor, fecha, alcance y cambio;
5. los datos usados y la evidencia obtenida quedan vinculados al caso;
6. no se usa una integración, tarea, permiso o estado fuera del MVP.

No hay historias sin prueba objetiva en esta versión de los entregables.
