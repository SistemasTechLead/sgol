---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-05
verificado_en: codigo-publicado-y-pruebas-aisladas
---

# Carga de procesos a partir de cantidades

## Contrato

Un catálogo compartido define ítems y minutos por unidad. En las nuevas versiones, Editar detalles permite seleccionar «Tomar conteo de» una etapa productora anterior. Varias receptoras no consecutivas pueden usar el mismo origen; sin origen conservan su carga propia. Todas usan la tarifa del origen y mantienen el orden de ejecución. La cantidad es un entero no negativo; vacío no equivale a cero. Las versiones históricas conservan el vínculo exclusivo con la siguiente etapa. Una etapa puede consumir un conteo y producir otro. La cantidad no es avance por unidades ni cambia las fotografías exigidas.

Carga efectiva = cantidad × minutos por unidad; sustituye la estimación. Antes del conteo se reserva carga estimada; la duración usa además el margen capturado. Las tarifas se congelan al preparar/reservar: cambiarlas no reescribe planes preparados o ejecuciones iniciadas. Retirar un ítem impide nuevas selecciones; los procesos que ya lo tenían conservan la compatibilidad definida.

## Cero y corrección

En nuevas versiones, cero omite solo las consumidoras de ese origen y libera sus reservas. Las independientes continúan en orden; una revisión omitida satisface el paso secuencial sin inventar aprobación. Si la omitida también producía un conteo, su consumidora queda bloqueada hasta resolver el origen. La auditoría final no consume conteo, conserva carga propia y siempre se ejecuta. Las versiones históricas mantienen el salto global hasta auditoría.

Un conteo incorrecto se devuelve al productor con motivo; su responsable reporta nuevamente. En nuevas versiones se revisa el impacto antes de crear correcciones de consumidoras con trabajo o entregas. Las independientes conservan sus intentos; si una entrega independiente quedó pendiente de revisión, la receptora revisa conjuntamente esa entrega conservada y la corrección recibida, identificadas en el formulario. Se preserva el trabajo del receptor y la corrección adopta el total nuevo completo; no descuenta trabajo que el sistema no ha registrado con esa semántica. Si el receptor todavía no tiene asignación, su responsable reservado o Dirección pueden solicitar la devolución autorizada.

## Falta de capacidad

En nuevas versiones, la entrega identifica conflictos por tarea y permite guardar pendiente; responsables y capacidad se resuelven desde planificación. En versiones históricas, la entrega muestra causa y alternativas disponibles: dejar siguiente etapa pendiente, buscar otro responsable compatible o forzar cuando rol de la tarea origen y autoridad lo permiten. La búsqueda ampliada no garantiza encontrar hueco.

Buscar otro responsable o forzar desde este aviso se limita a tareas origen de Subcoordinación o Dirección. La excepción puede admitir sobrecarga, solapamiento o descanso dentro del turno, pero conserva ausencias, empleo, rol, especialidad, dependencias y anclas protegidas. Se audita y no cambia el horario general. No todo conflicto admite forzado.

Un volumen que no cabe no se divide, trunca ni resuelve ampliando turno automáticamente. Los reintentos conservan entrega y archivo conforme a la operación confirmada; no simulan cumplimiento de la siguiente etapa.

Relacionados: [Procesos](procesos.md), [Planificación](planificacion.md), [Evidencias](ejecucion-evidencias.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Publicación identificada por proceso y hash; formularios, servicios y navegador probados en bases aisladas. Salud y acceso local/LAN comprobados en el host compartido, sin recorrido autenticado compartido.

- [ProcessItems.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/ProcessItems.cs)
- [OperationalProcessService.Items.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessService.Items.cs)
- [OperationalProcessService.Continuation.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/Sequences/OperationalProcessService.Continuation.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
