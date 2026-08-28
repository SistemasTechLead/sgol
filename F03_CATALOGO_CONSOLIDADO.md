# F03 — Catálogo funcional consolidado de SGOL

## 1. Control del entregable

| Campo | Valor |
|---|---|
| Fase | 03 — Resolución de dudas y consolidación |
| Estado | APROBADO E INCORPORADO A LAS FUENTES DEL PROYECTO |
| Fecha de consolidación | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Catálogo base | `F02_CATALOGO_FUNCIONAL.md`, CAP-001 a CAP-050 |
| Decisiones aplicadas | `F03_REGISTRO_DE_DECISIONES.md`, DEC-001 a DEC-069 |

## 2. Convenciones

- **Conservada:** la necesidad funcional está comprobada y su definición F02 continúa vigente con las decisiones F03.
- **Simplificada:** se conserva la necesidad, pero se retira complejidad legacy no comprobada.
- **Redefinida:** la decisión humana sustituyó una regla o alcance de F02.
- **Condicional:** sólo se especificará o implementará si Fase 04 la incluye en alcance.
- **No comprobada:** no se promueve a requisito; es candidata a descartar o diferir.
- Este documento consolida capacidades, no aprueba todavía el MVP ni selecciona tecnología.
- Las fichas detalladas de F02 continúan siendo antecedente trazable; esta tabla establece su interpretación vigente.

## 3. Catálogo consolidado

| ID | Estado F03 | Definición consolidada | Decisiones principales | Fuentes base |
|---|---|---|---|---|
| CAP-001 | Conservada | Administrar identidad y vigencia laboral con código estable; identidades legacy ambiguas permanecen sin fusionar. | DEC-048, DEC-052 | FTE-005/FTE-006 |
| CAP-002 | Simplificada | Administrar puesto y turno como datos laborales; no conceden roles, permisos ni autoridad automáticamente. | DEC-032, DEC-052, DEC-057 | FTE-005/FTE-006, FTE-007/FTE-008 |
| CAP-003 | Redefinida | Dirección registra disponibilidad diaria binaria: disponible o no disponible. | DEC-052, DEC-054, DEC-064 | FTE-005/FTE-006 |
| CAP-004 | Redefinida | La carga operativa usada para asignación es el número de tareas activas; no se conservan porcentajes o límites legacy sin necesidad comprobada. | DEC-035, DEC-064 | FTE-005/FTE-006, FTE-019/FTE-020 |
| CAP-005 | Conservada | Administrar organización y sucursales; catálogo inicial: `LOR-001` / `Loretta`; `TODAS` sólo representa alcance. | DEC-031 | FTE-007/FTE-008 |
| CAP-006 | Conservada | Administrar cuentas individuales; no se permiten cuentas compartidas y sólo Dirección administra usuarios. | DEC-043, DEC-050 | FTE-007/FTE-008 |
| CAP-007 | Redefinida | Jerarquía: `DIRECCION > ADMINISTRACION > SUBCOORDINACION > PISO_VENTAS`; un rol activo por usuario y sucursal; autoridad descendente y alcance jerárquico. | DEC-001, DEC-004, DEC-009, DEC-032, DEC-044, DEC-049 | FTE-007/FTE-008, FTE-027/FTE-028 |
| CAP-008 | Conservada | Registrar autorizaciones explícitas y auditables; las excepciones se solicitan al superior inmediato, salvo aut autorización aprobada de Dirección. | DEC-005, DEC-007, DEC-051 | FTE-007/FTE-008 |
| CAP-009 | Conservada | Dirección administra configuración versionada y auditable de calendario, tareas y políticas. | DEC-053, DEC-059 | FTE-009/FTE-010 |
| CAP-010 | Redefinida | Semana ISO lunes–domingo, zona `America/Mexico_City`, calendario laboral administrado por Dirección. | DEC-015, DEC-058, DEC-059, DEC-065 | FTE-009/FTE-010 |
| CAP-011 | Redefinida | Administrar períodos semanales por sucursal; Dirección puede reabrirlos sin borrar cierres anteriores. | DEC-012, DEC-013, DEC-015 | FTE-009/FTE-010, FTE-031/FTE-032 |
| CAP-012 | Condicional | Dirección crea y versiona definiciones de tarea; ninguna de las 202 definiciones legacy es requisito automático. | DEC-046, DEC-047, DEC-053 | FTE-011/FTE-012 |
| CAP-013 | Simplificada | Mantener dependencias y componentes internos sólo cuando exista necesidad aprobada; `AUT-*` no se hereda y checklists sólo aplican si se configuran como evidencia. | DEC-021, DEC-042, DEC-046, DEC-053 | FTE-011/FTE-012 |
| CAP-014 | Conservada | Administrar reglas manuales, recurrentes y por eventos o condiciones internas configuradas por Dirección. | DEC-053, DEC-060 a DEC-062 | FTE-013/FTE-014 |
| CAP-015 | Redefinida | Generar recurrencias idempotentes; una ocurrencia en día no laborable se omite. | DEC-059, DEC-063, DEC-065 | FTE-013/FTE-014 |
| CAP-016 | Redefinida | Activar por eventos internos de SGOL; no se requieren eventos de sistemas externos. | DEC-040, DEC-060, DEC-061, DEC-063 | FTE-013/FTE-014, FTE-041/FTE-042 |
| CAP-017 | Conservada | Evaluar condiciones internas configuradas sobre fecha, estado, conteos o umbrales operativos. | DEC-060, DEC-062, DEC-063 | FTE-013/FTE-014 |
| CAP-018 | Conservada | Emitir solicitudes de generación idempotentes: una obligación por regla, alcance, período y hecho originador. | DEC-063 | FTE-013/FTE-014 |
| CAP-019 | Conservada | Crear o recuperar una instancia de trabajo sin duplicarla; el traslado conserva identidad de negocio. | DEC-017, DEC-063 | FTE-015/FTE-016, FTE-029/FTE-030 |
| CAP-020 | Redefinida | Elegibilidad exige persona activa, sucursal `LOR-001`, rol requerido y disponibilidad; turno sólo cuando la tarea lo restringe. | DEC-032, DEC-034, DEC-057, DEC-064 | FTE-017/FTE-018 |
| CAP-021 | Simplificada | Dirección administra políticas explícitas de rol, disponibilidad y turno; no se heredan equivalencias textuales ni configuraciones de tareas excluidas. | DEC-034, DEC-047, DEC-053 | FTE-017/FTE-018 |
| CAP-022 | Redefinida | Asignación automática por menor número de tareas activas; empate por mayor tiempo sin asignación y luego código estable. | DEC-033 a DEC-035 | FTE-019/FTE-020 |
| CAP-023 | Redefinida | La asignación automática no requiere confirmación humana; superiores pueden corregirla con motivo e historial. | DEC-033, DEC-036 | FTE-019/FTE-020 |
| CAP-024 | Redefinida | Un plan único por sucursal y semana operativa; las áreas y niveles forman parte del mismo plan. | DEC-010, DEC-015, DEC-031 | FTE-021/FTE-022 |
| CAP-025 | Redefinida | Publicación jerárquica por alcance; obligaciones tardías se agregan incrementalmente al mismo plan con trazabilidad. | DEC-009, DEC-011 | FTE-021/FTE-022 |
| CAP-026 | Redefinida | Sólo tareas configuradas para medir tiempo exigen inicio explícito; se permite inicio anticipado salvo restricción expresa. | DEC-019, DEC-020 | FTE-023/FTE-024 |
| CAP-027 | Redefinida | Concluir una tarea exige toda evidencia configurada; una tarea vencida sigue activa hasta conclusión o excepción. | DEC-021, DEC-022, DEC-066 | FTE-023/FTE-024, FTE-025/FTE-026 |
| CAP-028 | Conservada | Consultar trabajo, historial, vencimientos y procedencia; datos legacy ambiguos se muestran con advertencia y no se presentan como hechos canónicos. | DEC-028, DEC-044, DEC-055, DEC-056 | FTE-023/FTE-024 |
| CAP-029 | Simplificada | Dirección configura evidencia sólo para tareas que la necesiten; si hay varios tipos, todos son obligatorios. | DEC-021, DEC-046, DEC-053 | FTE-025/FTE-026 |
| CAP-030 | Redefinida | Registrar y sustituir evidencia con control por estado; todas las versiones se conservan. | DEC-067, DEC-068 | FTE-025/FTE-026 |
| CAP-031 | Conservada | Evaluar cumplimiento estructural: falta de evidencia obligatoria bloquea conclusión cumplida. | DEC-022, DEC-023 | FTE-025/FTE-026 |
| CAP-032 | Redefinida | Dirección configura qué tareas requieren validación; autoridad por superior inmediato, con escalamiento jerárquico. | DEC-004, DEC-008, DEC-053 | FTE-027/FTE-028 |
| CAP-033 | Redefinida | Validación independiente de ejecución con resultados `CUMPLIDA`, `INCOMPLETA` y `NO_CUMPLIDA`; sin autovalidación salvo Dirección. | DEC-003, DEC-006, DEC-024 a DEC-026 | FTE-027/FTE-028 |
| CAP-034 | Conservada | Dirección administra tipos y causas de excepción; configuraciones legacy no se heredan automáticamente. | DEC-046, DEC-051, DEC-053 | FTE-029/FTE-030 |
| CAP-035 | Redefinida | Cancelar, posponer, trasladar o reasignar mediante excepción; traslado conserva identidad y reasignación conserva ejecución e historial de responsables. | DEC-016 a DEC-018 | FTE-029/FTE-030 |
| CAP-036 | Redefinida | Responsable solicita; superior inmediato autoriza o rechaza; escalamiento con motivo; Dirección puede autorizar su propia excepción. | DEC-005, DEC-007, DEC-051 | FTE-029/FTE-030 |
| CAP-037 | Redefinida | Evaluar pendientes y gates; Dirección puede forzar cierre con motivo, lista y destino individual de cada pendiente. | DEC-014, DEC-016 | FTE-031/FTE-032 |
| CAP-038 | Redefinida | Cerrar, reabrir y volver a cerrar sin sobrescribir hechos previos; resultados anteriores quedan sustituidos, no eliminados. | DEC-012, DEC-013, DEC-029 | FTE-031/FTE-032 |
| CAP-039 | Simplificada | Mostrar indicadores operativos derivados: pendientes, concluidas, validadas, incumplidas y carga por persona; sin motor KPI configurable. | DEC-030, DEC-038, DEC-048, DEC-055 | FTE-033/FTE-034 |
| CAP-040 | No comprobada | No calcular ni liquidar incentivos monetarios derivados de tareas. Candidata a descartar. | DEC-037 | FTE-033/FTE-034 |
| CAP-041 | No comprobada | SGOL no prepara, autoriza ni envía movimientos de nómina. Candidata a descartar. | DEC-039, DEC-040 | FTE-033/FTE-034, FTE-041/FTE-042 |
| CAP-042 | Conservada | Bandeja interna personal con tareas, vencimientos, evidencias y avisos; sin mensajería externa. | DEC-041, DEC-066, DEC-069 | FTE-035/FTE-036 |
| CAP-043 | Redefinida | Supervisión jerárquica de equipo, validaciones, cierres e indicadores dentro del alcance. | DEC-001, DEC-004, DEC-044, DEC-069 | FTE-035/FTE-036 |
| CAP-044 | Simplificada | Dirección consulta operación e indicadores derivados; no KPI monetarios ni salud de integraciones externas. | DEC-038, DEC-040, DEC-044 | FTE-035/FTE-036 |
| CAP-045 | Conservada | Auditoría funcional inmutable para usuarios, con visibilidad jerárquica y trazabilidad de decisiones, configuración y evidencia. | DEC-013, DEC-043 a DEC-045, DEC-068, DEC-069 | FTE-037/FTE-038 |
| CAP-046 | Conservada | Gestionar idempotencia y errores; la semántica funcional evita duplicados, mientras reintentos físicos se definen en Fase 06. | DEC-063 | FTE-037/FTE-038 |
| CAP-047 | Conservada / técnica diferida | Respaldar, restaurar y compensar sin borrar historial; RPO, RTO, cifrado y almacenamiento se definen en Fase 06. | DEC-013, DEC-045, DEC-068 | FTE-037/FTE-038 |
| CAP-048 | Condicional | Migrar conservando procedencia: ambigüedades no se resuelven por similitud, hechos huérfanos permanecen advertidos y referencias excluidas no se cargan. | DEC-023, DEC-027 a DEC-030, DEC-047, DEC-048, DEC-055 a DEC-057 | FTE-039/FTE-040 |
| CAP-049 | Condicional | Gobernar coexistencia y retiro legacy sólo si Fase 04 incluye migración; gates y cutover se definen después del alcance. | DEC-046 a DEC-048 | FTE-039/FTE-040 |
| CAP-050 | No comprobada | No se requieren integraciones externas, mensajería externa ni proyectos `AUT-*` para la operación inicial. Candidata a diferir o descartar. | DEC-039 a DEC-042 | FTE-041/FTE-042 |

## 4. Reglas transversales consolidadas

1. La jerarquía funcional es `DIRECCION > ADMINISTRACION > SUBCOORDINACION > PISO_VENTAS`.
2. Cada usuario tiene una cuenta individual y un solo rol activo por sucursal; el puesto no concede autoridad.
3. El superior inmediato valida y autoriza excepciones ordinarias; niveles posteriores intervienen por escalamiento trazado.
4. No existe autovalidación salvo para Dirección. Dirección también puede autorizar su propia excepción y cerrar un período en el que participó.
5. Cada plan se identifica por sucursal y semana ISO. Para el alcance inicial: `LOR-001`, `America/Mexico_City`.
6. La publicación incremental modifica el mismo plan y conserva versiones o eventos de publicación.
7. La asignación es automática, determinista, explicable y corregible con motivo.
8. Una tarea sólo exige inicio, evidencia, validación, turno o restricción temporal cuando su definición vigente lo configure.
9. Ejecución, evidencia, validación, excepción y cierre son hechos separados; ninguno sobrescribe a otro.
10. El cierre puede forzarse y reabrirse por Dirección, pero nunca borra cierres, resultados o cambios anteriores.
11. Ninguna evidencia, decisión o auditoría se elimina; se sustituye preservando versiones y vigencia.
12. Los registros legacy ambiguos conservan su procedencia y no participan en reglas canónicas hasta quedar inequívocamente reconciliados.
13. Macros, botones, tablas, procedimientos, tareas y automatizaciones legacy no prueban necesidad operativa.
14. No se incluyen incentivos monetarios, nómina, integraciones externas, mensajería externa ni automatizaciones `AUT-*` por herencia.

## 5. Validación de consolidación

| Criterio | Resultado |
|---|---|
| CAP-001 a CAP-050 conservan ID estable | Cumplido |
| Las 69 decisiones aprobadas tienen efecto trazable | Cumplido |
| Contradicciones F02-CON-002 a F02-CON-012 resueltas explícitamente | Cumplido |
| Capacidades no comprobadas visibles | Cumplido: CAP-040, CAP-041 y CAP-050; elementos legacy específicos bajo CAP-012/CAP-013 |
| Decisiones exclusivamente técnicas diferidas | Cumplido; véase `F03_PENDIENTES_NO_BLOQUEANTES.md` |
| Contradicciones funcionales bloqueantes restantes | Ninguna |
