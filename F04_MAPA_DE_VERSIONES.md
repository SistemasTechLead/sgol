# F04 — Mapa aprobado de versiones de SGOL

## 1. Control del entregable

| Campo | Valor |
|---|---|
| Fase | 04 — Alcance, prioridades y MVP |
| Estado | APROBADO E INCORPORADO A LAS FUENTES DEL PROYECTO |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Fuente de alcance | `F04_ALCANCE_Y_MVP.md` |
| Regla | Una versión posterior no es un compromiso hasta recibir aprobación explícita |
| Aprobación del responsable | `Apruebo F04-JP-001 a F04-JP-005 y los tres entregables F04` |

## 2. Criterio de ordenamiento

El orden se basa en dependencias funcionales y en la posibilidad de validar un corte vertical, no en la estructura del legacy ni en una estimación técnica:

1. primero identidad, autoridad y gobierno mínimo;
2. después generación, asignación y plan;
3. después ejecución, evidencia, validación y visibilidad;
4. posteriormente excepciones, cierre formal y activaciones avanzadas;
5. migración, coexistencia y tareas adicionales sólo tras comprobar valor.

## 3. Mapa de versiones

| Versión de producto | Objetivo | Capacidades | Definiciones de tarea | Condición de entrada | Condición de salida |
|---|---|---|---|---|---|
| MVP / V1 | Probar una semana operativa útil en `LOR-001` con ciclo trazable de tarea. | CAP-001 a CAP-007; CAP-009 a CAP-012; CAP-014, CAP-015; CAP-018 a CAP-025; CAP-027 a CAP-033; CAP-039; CAP-042 a CAP-047. | Ocho aprobadas: TAR-0005, TAR-0007, TAR-0008, TAR-0011, TAR-0018, TAR-0026, TAR-0092, TAR-0093. | F04 aprobada e incorporada. | EXI-001 a EXI-011 comprobados; aceptación en F09. |
| Posterior / V1.x | Completar manejo de casos no ordinarios y activación interna avanzada. | CAP-008; CAP-016, CAP-017; CAP-026; CAP-034 a CAP-038. | Ninguna definición adicional por defecto. | MVP aceptado y necesidad priorizada. | Excepciones, cierre/reapertura, medición temporal y disparadores internos tienen criterios verificables. |
| Opcional | Ampliar estructura o preservar historia sólo cuando exista beneficio aprobado. | CAP-013; CAP-048; CAP-049. | Las 186 definiciones opcionales se evalúan una por una. | Evidencia de uso, responsable, resultado y prioridad. | Decisión de alcance específica; no se asigna versión automáticamente. |
| Fuera de alcance | Mantener límites aprobados. | CAP-040, CAP-041, CAP-050. | TAR-0195 a TAR-0202 y T221. | Sólo podría cambiar mediante control formal de alcance y nueva evidencia. | No aplica dentro del alcance vigente. |

## 4. Cortes funcionales internos del MVP

Estos cortes ordenan la futura especificación e implementación, pero no constituyen arquitectura ni autorizan código:

| Corte | Resultado demostrable | Capacidades | Depende de |
|---|---|---|---|
| CV-01 — Gobierno mínimo | Dirección mantiene personas, cuenta, rol, disponibilidad, calendario y tareas aprobadas. | CAP-001 a CAP-007; CAP-009 a CAP-012 | F04-JP-001 y F04-JP-002 |
| CV-02 — Trabajo planificado | Una obligación manual o recurrente se crea sin duplicarse, se asigna y aparece en el plan semanal. | CAP-014, CAP-015, CAP-018 a CAP-025, CAP-046 | CV-01 |
| CV-03 — Ejecución comprobable | El responsable consulta, concluye y aporta evidencia; las reglas bloquean cumplimiento incompleto. | CAP-027 a CAP-031, CAP-042 | CV-02 |
| CV-04 — Validación y supervisión | El superior valida sin autovalidación indebida y consulta su equipo. | CAP-032, CAP-033, CAP-043 | CV-03 |
| CV-05 — Control y aceptación | Dirección consulta indicadores básicos; historial, auditoría y conservación permiten reconstruir la prueba. | CAP-039, CAP-044, CAP-045, CAP-047 | CV-01 a CV-04 |

Ningún corte se considera aceptable si usa datos o permisos falsos, duplica obligaciones, elimina historia o introduce una capacidad fuera del alcance aprobado.

## 5. Dependencias entre versiones

| ID | Origen | Destino | Dependencia |
|---|---|---|---|
| DV-001 | MVP | Posterior | El flujo ordinario debe estar aceptado antes de añadir excepciones y cierres forzados. |
| DV-002 | MVP | Posterior | Las tareas y eventos internos reales deben observarse antes de configurar activaciones automáticas. |
| DV-003 | MVP | Opcional | Los datos del piloto deben identificar qué definiciones adicionales tienen uso y valor comprobado. |
| DV-004 | Decisión de migración | CAP-048 | Debe existir un beneficio explícito de consulta o continuidad histórica. |
| DV-005 | CAP-048 | CAP-049 | No tiene sentido diseñar coexistencia o retiro si no se aprueba migración. |
| DV-006 | Cualquier versión | Cambio de alcance | Incentivos, nómina, integraciones externas o una tarea excluida exigen decisión nueva; nunca entran por arrastre. |

## 6. Reglas de promoción

Una capacidad o tarea opcional sólo puede promoverse si se registra:

1. necesidad operativa actual;
2. actor responsable y alcance;
3. disparador o frecuencia real;
4. resultado verificable;
5. dependencia con las capacidades ya aprobadas;
6. efecto sobre evidencia, validación, excepciones, indicadores y pruebas;
7. decisión humana de versión.

El estado legacy `BLOQUEADA_HASTA_ADAPTADOR`, `DIFERIDA_PREPROD`, un nombre `AUT-*` o una macro no satisface estos criterios.

## 7. Trazabilidad de pendientes F03

| Pendiente F03 | Tratamiento F04 aprobado | Destino restante |
|---|---|---|
| PNB-001 | 202 TAR clasificadas: 8 MVP aprobadas, 186 opcionales y 8 fuera de alcance. | Resuelto por F04-JP-002. |
| PNB-002 | Sólo las tareas MVP aprobadas se detallarán; activación automática avanzada queda posterior. | Configuración por tarea en F05. |
| PNB-003 | Sólo tareas MVP con validación requieren criterios verificables. | F05. |
| PNB-004 | Tipos `POR_CLASIFICAR` se revisan sólo para tareas MVP. | F05. |
| PNB-005 | Las causas y efectos detallados acompañan CAP-034 a CAP-036. | Versión posterior/F05 cuando se promueva. |
| PNB-006 | Cierre forzado y reapertura acompañan CAP-037/CAP-038. | Versión posterior/F05 cuando se promueva. |
| PNB-007, PNB-008 | No afectan MVP porque CAP-048 queda opcional. | Sólo si se aprueba migración. |
| PNB-009 | CAP-049 queda opcional y depende de CAP-048. | F04/F06 tras decisión futura. |
| PNB-010 a PNB-014 | Permanecen en F06 o asesoría correspondiente; F04 no selecciona solución. | F06. |
| PNB-015 | El MVP limita indicadores a los conceptos de DEC-038. | Criterios y presentación verificable en F05. |

## 8. Estado del mapa

Este mapa fue aprobado mediante F04-JP-001 a F04-JP-005 e incorporado a las fuentes del proyecto. Habilita Fase 05 exclusivamente para especificar las capacidades y tareas clasificadas como MVP.
