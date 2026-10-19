# Cruce de tareas definidas

Se conservan los códigos y nombres del Excel. T001–T025 del MD son referencias de fuente y no IDs del catálogo SGOL.
Se extrajeron tiempos y comportamiento. Las jerarquías de asignación, presupuestos, macroprocesos, estados de integración y fusión del MD no se convierten en requisitos del sistema.

El campo Min del MD es carga estimada, no plazo. Sus fechas son calendario; antes/después del cierre necesitan la hora real de apertura/cierre.

| Referencia MD | Código del Excel | Correspondencia | Calendario del MD | Carga del MD |
| --- | --- | --- | --- | --- |
| T001 | Sin equivalente confirmado | SIN_EQUIVALENTE_CONFIRMADO | Diaria, días de operación | 15 min |
| T002 | TAR-0039 | RELACIONADA | Iniciar cuatro días antes de cada corte y continuar hasta registrar compromiso o siguiente acción | 120 min |
| T003 | TAR-0024 | RELACIONADA | Dos veces al mes, días 8 y 23 | 10 min |
| T004 | TAR-0026 | DESGLOSE | Mensual, día 24 | 10 min |
| T005 | TAR-0026 | DESGLOSE | Mensual, día 24 | 10 min |
| T006 | TAR-0026 | DESGLOSE | Mensual, día 24 | 10 min |
| T007 | TAR-0032 | RELACIONADA | Mensual, día 7 del mes siguiente | 10 min |
| T008 | TAR-0195 | COINCIDENCIA_FUNCIONAL | Semanal, martes | 15 min |
| T009 | TAR-0076 | RELACIONADA | Mensual, día 27 | 60 min |
| T010 | Sin equivalente confirmado | SIN_EQUIVALENTE_CONFIRMADO | Trimestral, primer lunes del trimestre | 30 min |
| T011 | Sin equivalente confirmado | SIN_EQUIVALENTE_CONFIRMADO | Mensual, día 10 | 15 min |
| T012 | Sin equivalente confirmado | SIN_EQUIVALENTE_CONFIRMADO | Semanal, lunes | 20 min |
| T013 | TAR-0003 | RELACIONADA | Mensual, día 24 | 5 min |
| T014 | Sin equivalente confirmado | SIN_EQUIVALENTE_CONFIRMADO | Mensual, día 26 | 15 min |
| T015 | Sin equivalente confirmado | SIN_EQUIVALENTE_CONFIRMADO | Diaria, días de operación | 20 min |
| T016 | Sin equivalente confirmado | SIN_EQUIVALENTE_CONFIRMADO | Mensual, día 24 | 10 min |
| T017 | TAR-0039 | RELACIONADA | Días 3, 4, 5, 19, 20 y 21 | 30 min |
| T018 | TAR-0037 | RELACIONADA | Diaria, veinte minutos antes del cierre | 10 min |
| T019 | Sin equivalente confirmado | SIN_EQUIVALENTE_CONFIRMADO | Martes y jueves | 25 min |
| T020 | Sin equivalente confirmado | SIN_EQUIVALENTE_CONFIRMADO | Días 10 y 20 de cada mes | 5 min |
| T021 | Sin equivalente confirmado | SIN_EQUIVALENTE_CONFIRMADO | Semanal, lunes | 5 min |
| T022 | Sin equivalente confirmado | SIN_EQUIVALENTE_CONFIRMADO | Diaria, después de las 17:00 | 7 min |
| T023 | TAR-0062 | RELACIONADA | Diaria, antes de la apertura | 10 min |
| T024 | TAR-0037 | RELACIONADA | No aplica como tarea independiente | 0 min |
| T025 | Sin equivalente confirmado | SIN_EQUIVALENTE_CONFIRMADO | Por evento: inicio de auditoría o revisión de inventario | 15 min |

## Datos aprovechables y límites

- TAR-0195: propuesta de martes, carga de 15 minutos y comparación sistema contra físico. El Excel solo aporta código y nombre; no informa plazo ni hora.
- TAR-0026: el MD describe tres pagos el día 24, con 10 minutos por servicio. El Excel concentra servicios básicos: mensual, dos repeticiones distribuidas, plazo de 1 hora y carga de 45 minutos. Se conservan esos valores del Excel; no se sustituyen por 30 minutos ni se crean tres tareas.
- TAR-0037: T018 y T024 se relacionan con cierre operativo, pero no se fusionan ni se eliminan tareas por la especificación descartada.
- TAR-0018: el Excel pide aproximadamente 15 fotografías; SGOL permite máximo 5. No reducir ese requisito silenciosamente ni cambiar el límite como parte de este cruce.
- Hay celdas con especialización marcada Sí y valor NO, calendarios distribuidos con días marcados y cargas/plazos vacíos. Requieren aclaración de la tarea concreta antes de publicarla.
- La primera carga del MD no identifica las cuatro etapas del inventario semestral ni su hora. T025 asigna marcas para una auditoría; no sustituye registro, revisión ni aceptación del piloto.

El CSV conserva valores de ambas fuentes y la línea del MD. El JSON conserva las 202 filas del Excel, sus datos de calendario y los campos faltantes. No se modificaron fuentes ni se escribieron tareas en la base.
