---
categoria: producto
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: inspeccion-estatica
---

# Propósito y recorrido de SGOL

SGOL organiza trabajo operativo: definir tareas y procesos, planificar, asignar, ejecutar, entregar evidencia, supervisar y consultar resultados. La instalación compartida actual se usa para desarrollo y simulaciones operativas.

## Recorrido

1. [Catálogo](catalogo.md): define trabajo y requisitos.
2. [Horarios y cobertura](horarios-cobertura.md): determina disponibilidad y elegibilidad.
3. [Planificación](planificacion.md): calcula compromisos y los confirma.
4. [Procesos](procesos.md): coordina etapas y reservas.
5. [Ejecución](ejecucion-evidencias.md): registra avance y entrega.
6. [Supervisión](supervision.md): revisa y gestiona correcciones y responsabilidad.
7. [Posposiciones](arrastres-posposiciones.md): resuelve solicitudes con sus restricciones.
8. [Bono](bono-tareas.md): evalúa tareas sin reemplazar la operación.

## Distinciones necesarias

- Definición de catálogo y compromiso asignado.
- Reserva de capacidad y asignación ejecutable.
- Carga de trabajo y duración con margen.
- Entrega, aprobación y conclusión administrativa.
- Corrección con nuevo intento y transferencia de la misma asignación.
- Vencimiento operativo y día de evaluación del bono.

Dirección, Subcoordinación, Piso y Asesor Externo participan según permisos de cada operación. Los códigos internos pueden diferir del texto visible. El modo pantalla tiene propósito y acceso separados del personal.

La presentación se define en [Diseño](../design/00-diseno-sgol.md); la conservación de hechos, en [Modelo e historial](modelo-historial.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Revisión estática; no constituye una nueva prueba funcional, visual o de datos del host.

- [RoleScreenAccess.cs](../../../../src/Sgol.Web/Interface/Navigation/RoleScreenAccess.cs)
- [OperationalTasks.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTasks.cs)
- [OperationalSequences.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalSequences.cs)

[Mapa de producto](../../mapas/producto.md) · [Inicio](../../Inicio.md)
