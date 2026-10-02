# TECH-FRONT-005 — Capturas y guía de recorridos

**Pendiente de entrega visual final.** Las capturas de desarrollo corresponden a la aplicación real con datos sintéticos; sus pases son parciales y no acreditan la demo integral. No se presentan como maquetas ni como validación de dos ciclos. El manifiesto final será `captures.json` junto al `report.json` de cada ciclo/perfil, bajo el directorio propio indicado por el runner. La cabeza, navegador, plataforma y resultados se consultan en ese reporte y en `summary.json`.

Las capturas ocultan activación, contraseña, configuración MFA/códigos y contenido de evidencia. No incluyen archivo binario, URLs firmadas, cookies, tokens ni conexiones. Mantienen etiquetas, acciones, requisito, versión y estado para revisar la aplicación. Poppins local OFL, Georgia del sistema y estilos integrados conservados; no hay cambios incidentales de interfaz para acomodar la prueba.

## Qué muestra cada recorrido

| Recorrido / capturas | Roles y uso de la pantalla | Estado que se revisa |
|---|---|---|
| R1 — acceso y menú | Los cuatro puestos entran con su cuenta, cambian la contraseña y confirman el segundo factor. El menú muestra sólo sus rutas. En móvil se abre y se cierra con Escape. | Sesión iniciada y foco de vuelta en el botón del menú. No se captura la pantalla de secretos. |
| R2 — personas y accesos | Dirección registra una persona y cuenta auxiliar, modifica empleo/disponibilidad, cambia y revoca roles, desactiva/reactiva y reinicia MFA. | Confirmaciones y versiones anteriores conservadas. La cuenta/rol inactivo no da acceso. |
| R3 — calendario y releases | Dirección guarda fechas en un borrador y lo publica. Los cuatro puestos consultan la semana vigente. | Borrador vacío, fechas laborables/festivo/cierre extraordinario y publicación efectiva. |
| R4 — tareas y políticas | Dirección prepara y publica las ocho TAR y sus políticas de activación, elegibilidad, evidencia y validación. | Definiciones y políticas vigentes. TAR-0026 sólo participa en configuración y negativos conforme a D4. |
| R5 — generación y asignación | Dirección crea las seis tareas manuales y recupera su resultado sin duplicarlo. La recurrencia TAR-0005 pertenece al sistema. Dirección corrige responsables desde Planificación. | Solicitudes aceptadas/recuperadas y asignaciones vigentes/sustituidas, con explicación e historia. |
| R6 — plan semanal | Dirección crea o recupera el plan, prepara/cancela y confirma publicación. Todos los puestos consultan el resultado permitido. | Plan publicado y versiones conservadas. Cancelar no publica. |
| R7 — trabajo y evidencia | Subcoordinación y Piso aportan los requisitos de sus tareas; leen avisos propios y consultan la revisión. Los archivos se cargan, analizan y sólo se aportan cuando están limpios. | Evidencia incompleta/completa, versión vigente/sustituida y tarea concluida. Concluida todavía puede estar pendiente de validación. |
| R8 — validación y supervisión | Subcoordinación valida tareas de Piso; Administración las de Subcoordinación. Emitir y sustituir una decisión requiere fundamento/motivo. Dirección consulta supervisión. | Una decisión vigente y versiones sustituidas conservadas. Piso recibe denegación de supervisión. |
| R9 — indicadores, auditoría y continuidad | Los cuatro puestos consultan sus conteos y eventos permitidos. Dirección consulta la cadena y solicita un simulacro de recuperación. Sólo un resultado coincidente admisible puede aprobarse. | Ceros y denominadores reales; cadena por vínculos; MATCHED aprobado y DIFFERENT sin aprobar. La traza integral está pendiente de D5: no se presenta una salida fallida como completa. |

Pendientes de revisión final: escritorio/móvil, etiquetas próximas, separación de filtros/acciones, teclado, foco/retorno, controles, contraste 4.5:1, texto ampliado, reflow y movimiento reducido. Las aserciones automáticas acompañan cada captura; la aprobación visual no las sustituye. Zoom nativo, lector y dispositivos físicos siguen sin ensayo, y los demás límites se conservan en el informe.

Al terminar los dos ciclos se entregarán las rutas absolutas del manifiesto y las capturas representativas sanitizadas, junto con la solicitud expresa de revisión visual. Hasta entonces no se declara la demo completada.
