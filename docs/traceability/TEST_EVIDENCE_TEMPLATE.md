# Plantilla de evidencia y trazabilidad de prueba

Copiar esta plantilla por ejecución o conjunto reproducible de ejecuciones. Sustituir cada marcador por un identificador aprobado o por `No aplica — <causa>` cuando el tipo de criterio no corresponda; no inventar identificadores.

| Campo | Registro |
|---|---|
| ID de prueba | `<TEST-ID estable>` |
| HU | `<HU-nnn>` |
| CAP | `<CAP-nnn>` |
| CA/CP/CAT/NFR | `<uno o más IDs aprobados>` |
| Versión de aplicación | `<versión verificable>` |
| Commit o digest | `<SHA completo o digest inmutable>` |
| Entorno | `<local/CI/staging y versión relevante>` |
| Fecha | `<AAAA-MM-DDThh:mm:ssZ>` |
| Datos sintéticos | `<semilla o descripción reproducible, sin datos reales>` |
| Resultado | `<PASS/FAIL/BLOCKED>` |
| Artefactos | `<ruta o vínculo al log/reporte sin contenido sensible>` |
| Defecto relacionado | `<DEF-nnn o No aplica>` |

## Ejecución

| Comando o paso | Resultado observable |
|---|---|
| `<comando reproducible>` | `<salida resumida y verificable>` |

## Límites y decisión

- No comprobado y causa: `<límite concreto>`
- Riesgo o defecto residual: `<referencia o No aplica>`
- Decisión humana: `<pendiente/aprobada/rechazada; responsable y fecha>`

## Seguridad de la evidencia

No registrar secretos, credenciales, contraseñas, semillas o códigos TOTP, cookies, cadenas de conexión, URLs firmadas, datos personales ni evidencia real. Los datos de prueba deben ser sintéticos y los artefactos no deben revelar valores sensibles.
