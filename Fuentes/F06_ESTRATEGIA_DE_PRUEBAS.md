# F06 — Estrategia de pruebas del MVP de SGOL

## 1. Control

| Campo | Valor |
|---|---|
| Estado | APROBADO E INCORPORADO A `Fuentes` |
| Fecha | 2026-08-27 |
| Fecha de aprobación e incorporación | 2026-08-27 |
| Aprobación literal del responsable | `Apruebo los seis entregables de Fase 06 y autorizo su incorporación a Fuentes` |
| Alcance | 35 historias/capacidades, 8 tareas, permisos, estados y NFR F06 |
| Fuente de aceptación | `F05_CRITERIOS_DE_ACEPTACION.md`, `F05_MATRIZ_DE_TRAZABILIDAD.md`, `F05_MATRIZ_DE_ROLES_Y_PERMISOS.md`, `F05_MODELO_DE_ESTADOS.md` |
| Principio | Una cobertura porcentual no sustituye las pruebas positivas, negativas, de auditoría y ausencia de efectos |

## 2. Objetivos

1. Demostrar CA-001 a CA-035, CAT-001 a CAT-008, CP/CPT y CPE de F05.
2. Verificar autoridad y aislamiento de alcance en cada endpoint y descarga.
3. Comprobar estados separados, versionado e historia no eliminable.
4. Probar idempotencia y atomicidad bajo reintento, concurrencia y falla.
5. Probar archivos, escaneo, privacidad, réplica y conciliación.
6. Demostrar NFR-001 a NFR-010, RPO ≤1 h y RTO ≤4 h.
7. Producir evidencia reproducible para Fase 09.

## 3. Pirámide y tipos de prueba

| Nivel | Propósito | Herramientas de referencia | Frecuencia |
|---|---|---|---|
| Unitarias de dominio | Reglas, estados, ranking, tiempo, permisos puros y esquemas TAR. | xUnit + aserciones expresivas; reloj/UUID inyectables | Cada cambio |
| Arquitectura | Dependencias entre módulos y ausencia de accesos prohibidos. | NetArchTest o equivalente | Cada cambio |
| Integración | PostgreSQL real, EF Core, restricciones, transacciones, S3 y escáner adaptado. | Testcontainers, PostgreSQL, emulador S3 sólo donde no cambie semántica | Cada PR |
| Componente/API | HTTP completo, cookie, CSRF, autorización, errores, ETag e idempotencia. | `WebApplicationFactory`, OpenAPI y pruebas de contrato | Cada PR |
| Navegador E2E | Flujos críticos desde la interfaz, accesibilidad y sesión. | Playwright | Cada PR para smoke; suite completa diaria/preliberación |
| Seguridad | SAST, dependencias, secretos, DAST, permisos y archivos hostiles. | analizadores .NET, scanner de dependencias, Gitleaks, ZAP baseline, corpus de archivos | Cada PR/diaria/preliberación |
| Desempeño | Carga objetivo, p95 y carreras. | k6 o equivalente contra staging | Preliberación y cambio de consultas |
| Continuidad | Respaldo, restauración, reconciliación y runbook. | Utilidades PostgreSQL/S3 + verificador SGOL | Trimestral y antes del piloto |

SQLite y mocks de base no sustituyen integración PostgreSQL porque no reproducen índices parciales, bloqueos ni concurrencia.

## 4. Datos y reloj de prueba

- Fábricas deterministas crean `LOR-001`, cuatro roles, cuentas individuales con MFA de prueba, calendario laborable/inhábil y ocho TAR.
- Dos personas elegibles por rol permiten controlar carga, última asignación y desempate.
- El reloj se inyecta y se fija en límites de día, semana ISO, vencimiento y horarios 12:00/17:00.
- UUID y claves idempotentes se controlan donde el caso exige repetir exactamente la operación.
- Archivos de prueba incluyen JPEG/PNG/PDF válidos, firma/tipo discordante, archivo sobredimensionado, PDF malformado y muestra antimalware segura EICAR sólo en ambiente aislado.
- Staging usa datos sintéticos o anonimizados; los datos reales no se copian libremente.

## 5. Matriz de cobertura funcional

| Grupo | Pruebas obligatorias | Evidencia esperada |
|---|---|---|
| HU-001 a HU-007 | CA/CP 001–007 + PRM-001,003,005,010 | Estado/versiones, denegaciones y auditoría |
| HU-008 a HU-012 | CA/CP 008–012 | Publicación versionada, zona/calendario, sólo ocho TAR |
| HU-013 a HU-015 | CA/CP 013–015 + concurrencia | Generada/omitida/recuperada/rechazada; un ID |
| HU-016 a HU-019 | CA/CP 016–019 + ranking repetible | Candidatos, razones, ganador, corrección histórica |
| HU-020 a HU-021 | CA/CP 020–021 + PRM-002 | Un plan, versiones incrementales y alcance |
| HU-022 a HU-026 | CA/CP 022–026 + CPE-001,003 | Gate de evidencia, vencimiento calculado y versiones |
| HU-027 a HU-028 | CA/CP 027–028 + PRM-006,007 + CPE-002 | Autoridad, no autovalidación y separación de estado |
| HU-029 a HU-032 | CA/CP 029–032 | Cinco conteos conciliados y ausencia de datos fuera de alcance |
| HU-033 a HU-035 | CA/CP 033–035 + PRM-009 | Auditoría append-only, idempotencia y restauración conciliada |
| TAR-0005 a TAR-0093 | CAT/CPT 001–008 | Datos, evidencia, temporalidad y resultado por TAR |
| Estados | CPE-001 a CPE-005 y TP-001 a TP-008 | Transición permitida o rechazo sin efecto |

Cada prueba negativa confirma simultáneamente: código de error, ausencia de cambio de negocio, ausencia de fila parcial/objeto enlazado, conservación histórica y auditoría del intento cuando corresponda.

## 6. Pruebas de autorización

Se aplica una matriz generada por:

```text
permiso × rol × relación con recurso × estado × sucursal × resultado esperado
```

Cobertura mínima:

- propietario, inferior inmediato, inferior remoto, par, superior y Dirección;
- cuenta activa/inactiva y persona activa/inactiva;
- rol vigente/sustituido;
- obligación propia/ajena y pendiente/concluida;
- evidencia antes/después de conclusión;
- validación ordinaria/escalada/sustituida;
- consulta, mutación y descarga de archivo.

Toda ruta protegida tiene al menos un caso permitido y uno denegado. Los listados prueban filtración: no basta con que el detalle devuelva `403`; el recurso ajeno tampoco aparece en conteos, sugerencias, errores o tiempos distinguibles de manera evidente.

## 7. Pruebas de estados y versionado

1. Recorrer TR-001 a TR-019 con actor y guardas válidas.
2. Intentar TP-001 a TP-008 y comprobar que nada cambia.
3. Verificar una sola versión vigente y todas las sustituidas consultables.
4. Sustituir evidencia después de validar y comprobar que la decisión no cambia automáticamente.
5. Emitir `NO_CUMPLIDA` y comprobar que ejecución sigue `CONCLUIDA`.
6. Superar vencimiento y comprobar `PENDIENTE` + bandera `VENCIDA`.
7. Publicar una obligación tardía y comprobar mismo plan/nueva versión.

## 8. Pruebas de idempotencia, transacción y concurrencia

| ID | Escenario | Resultado esperado |
|---|---|---|
| TEC-IDEM-001 | 20 solicitudes concurrentes con misma clave/cuerpo. | Una solicitud/obligación; todas referencian mismo ID. |
| TEC-IDEM-002 | Misma clave con cuerpos distintos. | Un éxito y conflictos `409`; auditoría; sin segundo recurso. |
| TEC-IDEM-003 | Falla después de validar y antes de commit. | Cero cambios visibles. |
| TEC-IDEM-004 | Commit confirmado y respuesta perdida. | Reintento recupera respuesta/recurso. |
| TEC-CONC-001 | Dos correcciones simultáneas con mismo ETag. | Una gana; otra `412`; una asignación vigente. |
| TEC-CONC-002 | Dos publicaciones simultáneas. | Versiones serializadas o una rechazada; un plan. |
| TEC-CONC-003 | Conclusión simultánea con sustitución de evidencia. | Resultado consistente; ninguna conclusión con snapshot incompleto. |
| TEC-JOB-001 | Planificador cae después de generar parte del lote. | Reejecución completa faltantes sin duplicar existentes. |
| TEC-OUT-001 | Procesador `outbox` falla repetidamente. | Reintentos acotados, alerta y un efecto lógico. |

Las pruebas inyectan fallos en límites transaccionales y red; no se consideran válidas si sólo repiten llamadas secuenciales.

## 9. Pruebas de archivos

- Rechazo antes de carga por tamaño declarado >15 MiB.
- Rechazo posterior si tamaño real excede límite.
- Rechazo por extensión permitida con firma real distinta.
- URL firmada sólo permite clave, método, tamaño y ventana acordados.
- Objeto en cuarentena no se descarga ni vincula.
- Archivo limpio conserva SHA-256 y puede descargarse sólo con autorización vigente.
- Archivo detectado como malware se aísla, alerta y nunca se enlaza.
- Error de escaneo no se convierte en aprobación.
- Sustitución conserva ambas versiones y hashes.
- Réplica horaria copia objeto y metadatos; conciliación detecta falta/corrupción.
- Restauración desde réplica conserva hash y vínculo.
- Nombre de archivo no permite traversal, ejecución, encabezados peligrosos ni XSS.

## 10. Pruebas de API y contrato

- OpenAPI se compara con un snapshot aprobado; ruptura incompatible falla CI.
- Cada endpoint valida método, status, esquema, código funcional, `correlationId`, ETag e idempotencia.
- Mutación sin CSRF falla; lectura no produce cambios.
- Sesión expirada, cookie alterada y MFA incompleto fallan.
- CORS no autoriza origen externo.
- Paginación no omite/duplica elementos bajo orden estable.
- Filtros inválidos se rechazan; no amplían alcance.
- Error 500 no expone stack, SQL, rutas o secretos.
- No existe método `DELETE` funcional sobre historia.

## 11. Pruebas de seguridad

| Área | Casos mínimos |
|---|---|
| Autenticación | fuerza bruta, bloqueo, enumeración, TOTP repetido, código de recuperación de un uso, invalidación de sesiones y reset MFA |
| Autorización | matriz completa, IDOR, cambio de ID, filtros, descarga, auditoría y administración exclusiva de Dirección |
| Sesión | fijación, rotación tras login/MFA, expiración, logout, cookies y CSRF |
| Entrada | inyección SQL, XSS almacenado/reflejado, JSON excesivo, cabeceras, filenames y payload TAR |
| Dependencias | SCA sin vulnerabilidades críticas/altas sin excepción aprobada y fecha de vencimiento |
| Secretos | escaneo de repositorio/imagen/logs; rotación probada |
| Infraestructura | TLS, encabezados, puertos, bucket privado, base sólo desde trusted source y menor privilegio |
| Auditoría | intento de UPDATE/DELETE con usuario de aplicación y detección de manipulación |

Una vulnerabilidad crítica o alta explotable bloquea liberación. Una media necesita tratamiento, responsable y fecha aprobada; no se acepta por silencio.

## 12. Desempeño y capacidad

### 12.1 Perfil

- 100 cuentas activas; 25 sesiones concurrentes.
- Mezcla: 70 % lecturas/bandejas, 20 % mutaciones, 10 % indicadores/archivos.
- Datos de un año: 10 GB de archivos y volumen de obligaciones derivado de las ocho TAR.
- Escenario de pico: 25 usuarios consultan plan; 10 cargan evidencia; trabajo recurrente evalúa ocurrencias.

### 12.2 Umbrales

- NFR-002: p95 <2 s en lectura y <3 s en escritura ordinaria.
- Error HTTP inesperado <1 % durante prueba; cero pérdida/duplicado.
- Consulta de auditoría paginada p95 <3 s para rango máximo permitido.
- El planificador completa una ventana antes de 15 minutos y ninguna recurrencia queda retrasada >30 minutos.
- Carga de archivo no consume memoria proporcional al archivo dentro de la app porque usa URL firmada.

Se conserva reporte con versión, datos, infraestructura, percentiles y errores. Promedio sin percentiles no es suficiente.

## 13. Continuidad y recuperación

Simulacro trimestral en entorno aislado:

1. seleccionar punto de recuperación y registrar última transacción/objeto esperado;
2. restaurar PostgreSQL a cluster nuevo;
3. restaurar/usar réplica de objetos en bucket nuevo;
4. desplegar imagen y configuración conocidas;
5. ejecutar migraciones seguras;
6. conciliar IDs, vínculos, versiones, conteos, hashes y auditoría;
7. ejecutar smoke funcional y autorización;
8. medir pérdida observada y tiempo total;
9. aprobar o registrar defecto.

Pasa sólo si RPO observado ≤1 hora, RTO ≤4 horas y CA-035 queda satisfecho. El respaldo no cuenta como válido por existir: debe restaurarse y conciliarse.

## 14. Accesibilidad y compatibilidad

- Navegadores objetivo: dos últimas versiones estables de Chrome, Edge y Safari móvil disponibles al iniciar F08.
- Diseño responsivo para teléfono y escritorio.
- Navegación por teclado, foco visible, etiquetas, errores asociados y contraste conforme WCAG 2.2 AA en flujos críticos.
- Fotografías desde móvil se cargan sin hacer público el objeto.
- Zona/fecha se muestran sin ambigüedad y nunca dependen de zona del navegador para reglas de negocio.

## 15. Gates de CI/CD

### Pull request

1. formato/compilación sin advertencias nuevas;
2. unitarias y arquitectura;
3. integración PostgreSQL;
4. API/contrato;
5. SAST, dependencias y secretos;
6. trazabilidad de historia/criterio/prueba.

### Preliberación

1. suite E2E completa;
2. matriz de permisos;
3. seguridad dinámica;
4. desempeño;
5. migración desde versión previa;
6. archivos/antimalware/réplica;
7. smoke de staging;
8. respaldo reciente y restauración válida dentro del trimestre.

## 16. Evidencia de prueba

Cada ejecución registra: ID de prueba, HU/CA/CP/CAT/NFR, versión de aplicación, commit/digest, entorno, fecha, datos semilla, resultado, artefactos y defecto relacionado. No se guardan contraseñas, semillas TOTP ni evidencias reales en reportes de CI.

## 17. Criterios de salida hacia F09

- 35 CA con positivo y negativo aprobados.
- 8 CAT con positivo y negativo aprobados.
- PRM y CPE completos.
- NFR demostrados.
- Cero defecto bloqueante o vulnerabilidad alta/crítica abierta.
- Restauración trimestral vigente y conciliada.
- Trazabilidad fuente → HU → CA → prueba → evidencia sin huecos.
