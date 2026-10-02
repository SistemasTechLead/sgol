# TECH-FRONT-005 — Informe de demo integral

2026-10-01. **Implementación y validación en curso.** No acredita demo completada ni Implementada localmente. Plan único aprobado e incorporado por Adenda 60; D4 conserva la exclusión heredada de TAR-0026, sin fuente ni cadena CAT-006. BR-API04 continúa abierta globalmente; descarga/preview fuera de esta consumidora.

## Evidencia de desarrollo e incidencias

SDK fijado 10.0.400. Build Release de solución y build enfocado: 0 errores/advertencias. TechFront005ReportTests: 3/3, sin omisiones; rechaza ejecución parcial, cleanup fallido, perfiles/ciclos incompletos y cabeza distinta. Son comprobaciones de desarrollo, no dos ciclos integrales.

| Cabeza y ejecución | Resultado y alcance | Causa / corrección |
|---|---|---|
| 206bcfcf411d81a842d4ab1d32fdc37ffa986dee; desarrollo, Category=TECH_FRONT005; Chromium 151.0.7922.34, Windows AMD64/Docker Linux AMD64, HTTPS; variables SGOL_FRONT*_CAPTURE_DIR ausentes | 0/2, sin omisiones, exit 1; R1 timeout en escritorio/móvil; R2..R9 no ejecutados; ambos reportes HARNESS_FAILED y cleanup=false. No contabiliza un ciclo final. Contenedores/redes propios eliminados según log, indicador agregado de cleanup no satisfactorio. | Diferencia de infraestructura: CV-05 no instala confianza de certificado para la llamada HTTPS interna de Razor (las API directas sí usan pinning). Adaptación de prueba instala sólo su certificado efímero y verifica su retirada. Además, usar IPlaywright dentro del try cerraba el driver antes del finally de contextos/browser: propiedad y orden de cierre corregidos; cleanup secundario ya no sustituye la causa primaria. Pendiente reproducción de las correcciones. |

Los logs sensibles de navegador/procesos no se exportan: diagnóstico de etapa, tipo de fallo, handler y estado HTTP; reporte cerrado sin cookies, tokens, credenciales, URLs firmadas, conexión ni contenido de evidencia. El reporte conserva cada ejecución fallida y su cleanup.

## Pendiente de cierre

Dos ciclos obligatorios sobre la cabeza vigente: 36 celdas nuevas R1..R9 × escritorio/móvil × dos ciclos, regresión de Category=FRONT_BROWSER y selección PostgreSQL/API/S3/ClamAV fijada en el runner. Ningún pase histórico ni reporte parcial los sustituye. Mapa final de cobertura, verificaciones de auditoría/no-efecto, capturas sanitizadas y revisión visual pendientes. Sin push, PR, checks remotos, merge ni despliegue.

2026-10-02. Desarrollo sobre 8e2748639404f02bb4e4c100dd8e50d5ab7b328f: móvil R1 FAIL (timeout de login, 30484 ms), cleanup=true. Confianza de certificado y orden de cierre no bastaron para resolver el login; la hipótesis de certificado no se presenta como causa primaria confirmada. Escritorio empezó a provisionar recursos y se interrumpió sin reporte ni exit code recuperable; sesión de ejecución desaparecida. No acredita perfil/ciclo. Cleanup asistido verificó y retiró exactamente sus cinco contenedores, red, imagen y certificado de confianza. Eliminación de su directorio temporal privado y PFX rechazada por revisión automática (`blocked by policy`); pendiente confirmación externa, sin abrir contenido ni modificar otros temporales/certificados.

Corrección siguiente de infraestructura: proceso web del consumidor usa src/Sgol.Web como directorio de contenido, igual que BrowserFixture existente (CV-05 era API directo desde raíz). Hook opcional en soporte compartido, sin cambio de comportamiento CV-05 cuando no se usa; CORS de PUT sólo en bucket de cuarentena propio y para origen HTTPS exacto. Diagnóstico de login con status HTTP, número de campos y código cerrado derivado de título público; ningún valor de formulario. Selección de borrador/publicación desde enlace/control real, no por texto de un ID que la tabla no expone. Pendiente ejecución enfocada de escritorio antes de los dos ciclos.

2026-10-02. Cleanup de la ejecución interrumpida cerrado: el responsable confirmó retirada del directorio temporal y ausencia del PFX; Test-Path posterior devolvió false para ambos recursos identificados. Conserva el resultado histórico de interrupción.

Desarrollo sobre cfd7ffd (referencia resoluble en Git): escritorio, Chromium 151.0.7922.34, Windows AMD64/Docker Linux AMD64, HTTPS, variables opcionales de capturas ausentes; 0/1, sin omisiones, exit 1. R1 rechazó login con HTTP 200 de Razor y título público clasificado PROTOCOL_OR_CONTRACT_FAILURE; cleanup=true, R2..R9 no ejecutados. No acredita ciclo. La corrección de contenido no resuelve esa causa. Se alinea ahora el momento de instalación de confianza con BrowserFixture (antes de iniciar el proceso web) y se añade un diagnóstico TLS que exige validación normal y registra únicamente enums de política/cadena. La causa primaria permanece bajo diagnóstico; no se cambia autenticación productiva.
