# F07 Adenda 39 — Red privada estable del simulacro HU-035

## Aprobación y evidencia

El responsable aprueba íntegramente el 2026-09-17 la implementación local y las validaciones enfocadas de esta corrección del entorno. El identificador 39 se verificó libre antes de materializarla. Complementa las Adendas 33–38, sin modificar los originales F00–F07 ni Fuentes/.

Fundamento: run 35281798132, intento 1, SHA b75ae2712e3733540df11cd50109b9d0f81ab135. El experimento registró MIGRATION_EFFECT_SUPPORTED: A falló en PUT_DESTINATION (timeout inicial y HTTP 500 / InternalError en los intentos exteriores 2 y 3); B aprobó inicialmente con manifiesto exacto y un objeto VERIFIED. Ambas preparaciones y limpiezas se completaron. Evitar la migración resolvió la réplica en el experimento; todavía no está validado como corrección del gate integral ni prueba su mecanismo interno.

## Cinco rutas autorizadas

- F07_ADENDA_39_RED_PRIVADA_ESTABLE_DEL_SIMULACRO_HU_035.md.
- scripts/operations/new-tech-ops-synthetic-environment.ps1.
- scripts/operations/invoke-hu-035-amd64-gate.ps1.
- scripts/ci/test-hu-035-private-network.ps1.
- docs/traceability/IMPLEMENTATION_STATUS.md.

## Cambio acotado

HU-035 crea su red bridge interna definitiva antes de aprovisionar. El aprovisionador compartido recibe StorageNetworkName opcional y verifica que la red existe, es interna y usa bridge. Sólo ambos SeaweedFS arrancan en esa red, conservándola durante preparación y simulacro. PostgreSQL conserva su arranque en bootstrap y su traslado existente.

Los aliases de almacenamiento son exactamente sgol-tech-ops-s3-source y sgol-tech-ops-s3-destination, conservados al renombrar los contenedores. Se mantiene el bridge temporal de aprovisionamiento, retirado después de verificar. Se conservan imagen, configuración, permisos, retirada de administradores, reinicio, datos sintéticos y limpieza.

Después del reinicio en modo HU-035 se reconsultan ambos puertos publicados antes de las esperas existentes. Hay una sola llamada a Set-ProvisionEnvironment 'verify', después de esas esperas. Los consumidores del simulacro continúan usando las IP privadas finales, no puertos publicados. Sin StorageNetworkName el comportamiento predeterminado de TECH-OPS permanece sin cambios.

No cambian producción, ObjectReplica, solicitudes S3, streams, credenciales/políticas, timeouts, reintentos, parser ni workflow. No se añaden sondas ni se repite el A/B aprobado. La limpieza cubre red definitiva creada, aprovisionamiento parcial y renombrados parciales con los nombres iniciales/finales ya registrados.

## Validación y eficacia

Pruebas puras sobre bloques reales con comandos simulados: modo predeterminado y HU-035, orden de creación/arranque, aliases y consumidores, relectura de puertos después del reinicio, espera/verificación y limpieza ante fallos de creación/aprovisionamiento/renombrado. Sintaxis PowerShell, validadores contractuales TECH-OPS/HU-035 y git diff --check. No se ejecutan Docker, PostgreSQL, S3 ni gates integrales en estas pruebas locales.

La comprobación decisiva pendiente es HU-035 en su recorrido real, en Linux AMD64 nativo y sobre el SHA final exacto, con autorización separada. Esta adenda no acepta CA-035/CP-035-P/CP-035-N, no declara HU-035 Terminada y no habilita CV-05. No autoriza commit, tag, push, ejecución/repetición CI ni merge.
