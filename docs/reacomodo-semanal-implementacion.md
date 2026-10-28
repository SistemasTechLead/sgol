# Reacomodo semanal por criticidad: implementación

21 de octubre de 2026. Política de agenda 2. El motor y la agenda futura de Semana 43 están aplicados. Por instrucción expresa posterior, se conservaron las 122 asignaciones anteriores sin cierre ni reprogramación y se adoptaron exclusivamente las futuras del 22 al 25. La aplicación delimitada conserva una reserva de planificación de TAR-0179 (jueves 09:15–09:38), capturada en auditoría y snapshots futuros, sin modificar su ID, plazo ni estado pendiente. Se actualizaron 10 definiciones de catálogo y 120 asignaciones futuras; se agregó el precorte dominical y se retiró únicamente TAR-0063 sin actividad. Véase el [informe aplicado por ID](../outputs/adopcion-futuras-20261021/informe.md).

## Contrato

- Las políticas son inmutables y las asignaciones conservan su referencia, margen, nominal, activación y cierre persistidos. La migración agrega una política de agenda 2 con el margen vigente; no cambia las filas históricas ni el contrato del plan padre.
- Las colocaciones de agenda 2 usan horarios base y excepciones guardadas, sin la hora implícita de cobertura. Capacidad, planificación, ocasionales y Supervisión comparten el contexto; las simulaciones de cambios de horario conservan sus identificadores de política.
- Críticas: ancla exacta. Importantes: desde nominal. Flexibles: preferencia nominal y alternativas anteriores/posteriores dentro del mismo día. PROGRESO mantiene su contrato semanal y su capacidad.
- Circuito explícito de la política: TAR-0004 → TAR-0032 → TAR-0200 por día, también con distintos responsables. Las reservas de procesos y otros planes permanecen como compromisos.

## Búsqueda y publicación

`OperationalPlanGenerator` normaliza las repeticiones semanales distribuidas considerando la semana completa y las ocurrencias ya conservadas. No descarta una obligación pendiente porque su fecha de referencia quedó antes del inicio parcial del plan.

La búsqueda usa criticidad, escasez de candidatos, duración y desempates estables; explora minutos/personas/fechas permitidas y deshace decisiones anteriores. Propaga la disponibilidad de obligaciones largas. Las críticas sin candidato se diagnostican antes de explorar alternativas irrelevantes. Una validación completa vuelve a comprobar capacidad, bloque, calendario y circuito antes de guardar. El balance es un desempate de candidatos en agenda 2.

La reparación usa `OperationalAgendaSearch.SolveFixed`, con días y responsables fijos, clones de entradas y componentes conectados por capacidad y circuito. No mueve compromisos ajenos al conjunto solicitado. Un contrato legado sin bloque verificable se informa como reserva protegida, no como una demostración de imposibilidad física.

Límites base: 100.000 estados de búsqueda y 2.000.000 de candidatos; protección de profundidad de 1.000 ocurrencias por búsqueda/componente. Los límites forman parte de la huella. Alcanzarlos devuelve `SCHEDULING_SEARCH_INCONCLUSIVE`; no guarda resultados parciales. La preparación permite repetir con multiplicador 2 y 4 sobre las mismas restricciones, conservando el multiplicador en la intención y la huella. El límite de profundidad protege la pila y no se amplía automáticamente.

## Adopción controlada de Semana 43

`OperationalWeeklyRepairService` aplica exclusivamente el alcance del 22–25 de octubre de 2026 del plan semanal 43. Su vista previa separa cambios de catálogo, cambios por ID, alta del precorte dominical y retiro de TAR-0063. Se presenta en Dirección, dentro del detalle del plan.

La confirmación vuelve a capturar el estado bajo el bloqueo transaccional común y autorización de Dirección. Rechaza huellas obsoletas y cambios de actividad; protege entregas, revisión, avances, archivos, reasignaciones y procesos. Conserva IDs, responsables y días de las filas adoptadas. El retiro usa eliminación de una ocurrencia sin actividad con archivo completo en auditoría; no inventa una reasignación. Una confirmación repetida se recupera mediante su huella auditada. El historial del colaborador muestra la adopción de agenda.

Los cambios propuestos del catálogo están centralizados en `OperationalWeeklyRepairService.Configure`. No incluyen reducir cobranza ni extender el horario de Mariana. La agenda comprobada reserva TAR-0204 el sábado de 11:00 a 14:00, con carga de 120 minutos y margen del 50%.

## Evidencia y operación pendiente

El [informe local](../outputs/reacomodo-semanal-20261021/informe.md) contiene conteos, tiempos, diferencias y limitaciones. La reparación condicionada conserva 120 asignaciones y agrega una; la regeneración reproduce las 121 obligaciones. El miércoles de Semana 44 requiere resolver una cobertura real hasta las 20:00. Las pruebas no cambian jornadas ni dan por entregado trabajo operativo.

Pruebas reproducibles mediante `tools/Sgol.TaskChecks`: `--scheduling-rules-only`, `--weekly-reflow`, `--weekly-repair-lifecycle` y `--scheduling-integration-only`. Las tres últimas requieren una copia PostgreSQL restaurada y dedicada, con nombre `sgol_task_checks_*`, indicada en `SGOL_TASK_CHECKS_CONNECTION`. La prueba de ciclo de reparación simula la resolución del compromiso anterior exclusivamente en esa copia; nunca debe ejecutarse contra la base de operación.

El diagnóstico inicial exigía resolver TAR-0179 antes de usar la reparación genérica. La instrucción posterior fue conservar anteriores y cambiar solo futuras: se aplicó un corte operativo explícito, probado en copia, manteniendo la carga del compromiso todavía vigente mediante la reserva indicada. Las vencidas anteriores conservaron sus plazos y no se trasladaron a mañana; tampoco se dieron por completadas. Este corte no habilita una renovación automática de atrasos ni cambia el funcionamiento conservador del flujo genérico. El respaldo de aplicación y las comparaciones de integridad están en `.local/adopcion-futuras-20261021/`; los de publicación, en `.local/reacomodo-semanal-20261021/`. La lectura real del Tablero verificó las 121 agendas persistidas. Las rutas protegidas requieren renovar la sesión de pruebas para completar la comprobación visual autenticada.
