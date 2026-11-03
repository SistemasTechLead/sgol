---
categoria: calidad
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: inspeccion-estatica
---

# Validación y diagnóstico del sistema

## Alcance de las comprobaciones

La revisión estática identifica contratos y fuentes. Compilar comprueba compatibilidad. Las pruebas de reglas comprueban casos del motor; integración verifica persistencia y transacciones; HTTP comprueba formularios y autorización; navegador verifica interacción y presentación reales. Ninguna sustituye automáticamente las demás.

El resultado de una simulación es evidencia de esa ejecución, no definición funcional del sistema. Esta bóveda conserva referencias actuales; informes de implementación y respaldos quedan fuera de la red cotidiana.

## Entornos y escenarios

La app compartida es desarrollo y simulación. Local y LAN pueden compartir aplicación y datos. Regresiones automatizadas/destructivas usan bases aisladas; no reutilizan indiscriminadamente la simulación diaria. Antes de escribir se identifica publicación, conexión efectiva y fecha.

Según el cambio, verificar creación y registros existentes, ejecución/revisión, permisos, concurrencia, reintentos, atomicidad e historial. Para interfaz, cubrir tamaños, temas, teclado, foco y estados pertinentes en pantalla real. Los conflictos deben identificar causa, entidad y acción.

Un conflicto que impide llegar al caso no cuenta como prueba aprobada. Lo no ejecutado se declara. Las herramientas bajo tools contienen escenarios mantenibles; revisar opciones y requisitos antes de invocarlas.

## Esta consolidación

Se revisaron reglas y fuentes y se comprobaron destinos de enlaces. Se conserva respaldo externo de las notas retiradas. No se ejecutaron nuevas pruebas funcionales ni se modificó la app. La matriz y reglas de trabajo están en el [protocolo](../desarrollo/PROTOCOLO_IMPLEMENTACION.md).

Relacionados: [Seguridad](seguridad.md), [Planificación](planificacion.md), [Desarrollo](../../mapas/desarrollo.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Revisión estática; no constituye una nueva prueba funcional, visual o de datos del host.

- [PROTOCOLO_IMPLEMENTACION.md](../../../desarrollo/PROTOCOLO_IMPLEMENTACION.md)
- [Program.cs](../../../../tools/Sgol.TaskChecks/Program.cs)

[Mapa de calidad](../../mapas/calidad.md) · [Inicio](../../Inicio.md)
