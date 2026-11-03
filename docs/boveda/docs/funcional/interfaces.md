---
categoria: integraciones
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: inspeccion-estatica
---

# Interfaces y límites de integración

## Interfaz operativa

La web usa Razor Pages y handlers autenticados para catálogo, planificación, ejecución y supervisión. Un handler de interfaz no es por sí solo contrato público para otro sistema. Existen además endpoints con políticas propias; cada integración debe identificar su ruta propietaria.

Las instrucciones de conversión de un Excel histórico no definen una función general de importación de la app. Los cruces y archivos de aquel ejercicio no forman parte de la referencia funcional.

## Bono y evidencia

El bono tiene cálculo, persistencia y consultas internas autorizadas. El contrato externo propuesto no es una API de bono publicada y no se conserva como capacidad disponible.

Evidencia y cámara tienen autorización y contratos propios; un enlace a archivo local de prueba no es endpoint de descarga. Reportes y exportaciones conservan el significado de sus datos y no cambian reglas de historial.

Relacionados: [Bono](bono-tareas.md), [Evidencias](ejecucion-evidencias.md), [Seguridad](seguridad.md), [Modelo](modelo-historial.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Revisión estática; no constituye una nueva prueba funcional, visual o de datos del host.

- [EvidenceApiEndpoints.cs](../../../../src/Sgol.Web/Interface/Endpoints/EvidenceApiEndpoints.cs)
- [TaskBonusService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/TaskBonusService.cs)
- [ReportData.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Reporting/ReportData.cs)

[Mapa de integraciones](../../mapas/integraciones.md) · [Inicio](../../Inicio.md)
