---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: inspeccion-estatica
---

# Carga de procesos a partir de cantidades

## Contrato

Un catálogo compartido define ítems y minutos por unidad. Una etapa puede reportar un entero no negativo y alimentar exclusivamente la siguiente; vacío no equivale a cero. Una etapa puede consumir un conteo y producir otro. La cantidad no es avance por unidades ni cambia las fotografías exigidas.

Carga efectiva = cantidad × minutos por unidad; sustituye la estimación. Antes del conteo se reserva carga estimada; la duración usa además el margen capturado. Las tarifas se congelan al preparar/reservar: cambiarlas no reescribe planes preparados o ejecuciones iniciadas. Retirar un ítem impide nuevas selecciones; los procesos que ya lo tenían conservan la compatibilidad definida.

## Cero y corrección

Cero salta a la auditoría final configurada, libera reservas intermedias y registra omisiones sin inventar entregas. La auditoría conserva carga y evidencia.

Un conteo incorrecto se devuelve al productor con motivo. Se preserva el trabajo del receptor y la corrección adopta el total nuevo completo; no descuenta trabajo que el sistema no ha registrado con esa semántica. Si el receptor todavía no tiene asignación, su responsable reservado o Dirección pueden solicitar la devolución autorizada.

## Falta de capacidad

La entrega muestra causa y alternativas disponibles: dejar siguiente etapa pendiente, buscar otro responsable compatible o forzar cuando rol de la tarea origen y autoridad lo permiten. La búsqueda ampliada no garantiza encontrar hueco.

Buscar otro responsable o forzar desde este aviso se limita a tareas origen de Subcoordinación o Dirección. La excepción puede admitir sobrecarga, solapamiento o descanso dentro del turno, pero conserva ausencias, empleo, rol, especialidad, dependencias y anclas protegidas. Se audita y no cambia el horario general. No todo conflicto admite forzado.

Un volumen que no cabe no se divide, trunca ni resuelve ampliando turno automáticamente. Los reintentos conservan entrega y archivo conforme a la operación confirmada; no simulan cumplimiento de la siguiente etapa.

Relacionados: [Procesos](procesos.md), [Planificación](planificacion.md), [Evidencias](ejecucion-evidencias.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Revisión estática; no constituye una nueva prueba funcional, visual o de datos del host.

- [ProcessItems.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/ProcessItems.cs)
- [OperationalProcessService.Items.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessService.Items.cs)
- [OperationalProcessService.Continuation.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessService.Continuation.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
