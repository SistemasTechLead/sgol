# F07 — Definiciones de control para la implementación de SGOL

## 1. Control

| Campo | Valor |
|---|---|
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Aplica a | Tareas técnicas, historias, defectos y cortes de F08 |
| Fuentes | Criterios F05 y estrategia, seguridad y operación F06 |

## 2. Definición de “Listo” (entrada a desarrollo)

Una tarea está **Lista** sólo si cumple todo lo siguiente:

1. tiene ID único, resultado observable y límites explícitos;
2. pertenece al corte activo y sus dependencias están Terminadas;
3. identifica HU/CAP, RN, CA/CP/CAT/NFR aplicables o declara justificadamente que es técnica;
4. cita documentos y secciones autorizadas, sin depender de conversaciones no aprobadas;
5. tiene actor, precondiciones, flujo, errores, permisos, estados y datos definidos en las fuentes aplicables;
6. incluye casos positivos, negativos, de autorización, auditoría y no-efecto proporcionales al riesgo;
7. no contiene una contradicción abierta ni una decisión humana bloqueante;
8. cabe en un incremento revisable; si mezcla resultados independientes, se divide;
9. declara cambios de esquema, API, seguridad, operación y documentación esperados;
10. no requiere modificar `Fuentes/` ni implementar alcance posterior, opcional o excluido.

Si falta una condición, Codex puede investigar y reportar, pero no debe inventar la respuesta ni iniciar una implementación irreversible.

## 3. Definición de “Terminado” (salida de desarrollo)

Una tarea está **Terminada** sólo si:

1. el resultado y todos los criterios de aceptación aplicables están implementados;
2. el cambio mínimo está integrado respetando los límites modulares y sin código muerto deliberado;
3. las pruebas positivas y negativas pasan, incluyendo autorización, auditoría, no-efecto, concurrencia o archivos cuando correspondan;
4. PostgreSQL real valida restricciones y transacciones cuando hay persistencia;
5. el contrato API, OpenAPI, estados, errores, ETag e idempotencia coinciden con F06;
6. no se introducen vulnerabilidades críticas/altas explotables, secretos ni datos reales;
7. build, formato y gates PR aplicables pasan sin advertencias nuevas;
8. migraciones son compatibles hacia adelante y tienen prueba de aplicación; no existe cambio destructivo;
9. trazabilidad HU–CAP–CA/CP/CAT/NFR–prueba queda actualizada;
10. documentación y runbook afectados quedan actualizados;
11. la revisión confirma que `Fuentes/` no fue modificada;
12. la entrega registra comandos, resultados, evidencia, riesgos residuales y límites no comprobados;
13. no queda defecto bloqueante ni comentario de revisión crítico abierto;
14. el cambio está revisado y aceptado por el responsable humano conforme al flujo disponible.

“Compila en mi equipo”, “la ruta feliz funciona” o “se escribió código” no equivalen a Terminado.

## 4. Severidad de defectos

| Nivel | Criterio | Efecto |
|---|---|---|
| Bloqueante | Pérdida/corrupción, duplicado, acceso indebido, omisión de auditoría, incapacidad de ejecutar el flujo del corte o contradicción con alcance aprobado | Impide merge, cierre de historia y cierre de corte |
| Alta | Regla principal, permiso, estado, idempotencia o evidencia incorrectos con alternativa insegura o costosa | Impide liberación y normalmente merge |
| Media | Comportamiento secundario incorrecto sin pérdida ni elevación de acceso | Requiere responsable y fecha; puede impedir historia según CA |
| Baja | Presentación o mantenimiento sin efecto funcional relevante | Backlog controlado |

Una vulnerabilidad crítica o alta explotable siempre bloquea liberación.

## 5. Gates por nivel

### Pull request

- restore bloqueado, formato, compilación sin advertencias nuevas;
- unitarias, arquitectura, integración PostgreSQL y API/contrato aplicables;
- análisis estático, dependencias y secretos;
- trazabilidad completa y revisión de protección de `Fuentes/`.

### Cierre de historia

- Definición de Terminado completa;
- demostración o evidencia reproducible del CA/CP;
- ningún cambio de alcance encubierto;
- revisión humana registrada.

### Cierre de corte

- todas las historias del corte Terminadas;
- E2E del resultado vertical y matriz de permisos del corte;
- accesibilidad crítica y compatibilidad aplicables;
- defectos bloqueantes en cero;
- documentación de demo y trazabilidad consolidadas.

### Preliberación

Aplica íntegramente F06: E2E completo, seguridad dinámica, desempeño, migración, archivos/antimalware/réplica, smoke de staging y restauración válida.

## 6. Control de cambios y decisiones

- Una aclaración que no altera resultado ni CA puede registrarse en la tarea.
- Un cambio de regla, permiso, estado, endpoint incompatible, dato obligatorio o alcance requiere decisión humana y actualización de los documentos rectores antes de implementar.
- Añadir una dependencia importante, proveedor o patrón arquitectónico no previsto requiere evaluación y, si cambia una decisión F06, un ADR nuevo.
- Las capacidades posteriores u opcionales sólo se promueven con el control de alcance de F04; no entran como “preparación futura”.

## 7. Evidencia mínima de entrega

| Campo | Contenido requerido |
|---|---|
| Identidad | ID de tarea/historia, rama, commit o digest |
| Trazabilidad | HU/CAP/RN/CA/CP/CAT/NFR cubiertos |
| Verificación | Comando, entorno, fecha, resultado y artefacto |
| Datos | Semilla sintética y reloj controlado usados |
| Seguridad | Pruebas de permiso y secretos aplicables |
| Persistencia | Migración, restricción y transacción comprobadas |
| Límites | Qué no se probó y por qué |
| Decisión | Aprobación, rechazo o pendiente humano |

Nunca se adjuntan credenciales, semillas TOTP, evidencias reales ni URLs firmadas.

## 8. Condición especial para iniciar F08

F08 no comienza hasta que los seis entregables F07 estén aprobados por el responsable, copiados a `Fuentes/` y verificados idénticos por SHA-256. F07-JP-001 quedó resuelta mediante la aprobación de los seis entregables. La aprobación de contenido sin incorporación documental no habilita la primera tarea.
