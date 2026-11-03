---
categoria: dominio
tipo: referencia
estado_documental: inspeccionado
ultima_verificacion: 2026-11-03
verificado_en: inspeccion-estatica
---

# Ejecución, progreso y evidencias

## Trabajo asignado

Mi trabajo combina asignaciones efectivas y reservas de etapas de ejecuciones en curso. Una reserva muestra espera, programación o falta de asignación y acceso al proceso; no permite entregar como si ya existiera asignación. Al activarse se sustituye por el intento ejecutable sin duplicarse. Las reservas de planes todavía no activados no son trabajo en ejecución.

El colaborador consulta instrucciones, registra avance cuando aplica y entrega según el requisito capturado. Un borrador no es un envío a revisión. La entrega anticipada conserva las validaciones del requisito y del flujo y no altera por sí sola el vencimiento.

No satisfecha es un cierre operativo: se conserva para consulta, al final del listado y fuera del trabajo pendiente del día. Una corrección vinculada sigue su propio recorrido.

## Requisitos

Los requisitos admitidos son NINGUNO, COMENTARIO, FOTOGRAFIA, CAPTURA_PANTALLA, DOCUMENTO, EXCEL y CHECKLIST. El servidor valida contrato, identidad y revisión. Una meta progresiva debe alcanzar las unidades exigidas para entregar. El bono no añade requisitos de adjuntos.

La captura fotográfica usa una sesión autorizada vinculada a la asignación y controles de cantidad y contenido. FOTOGRAFIA abre la cámara, permite revisar, repetir y conservar tomas; no selecciona fotos de galería. Subir fotografías verifica el material; Enviar a revisión confirma la entrega con el conjunto. El navegador necesita HTTPS confiable y permiso de cámara. La autoría anterior no se sustituye por el responsable actual.

## Checklist

Contiene casillas con identidad, texto y orden y puede incluir títulos opcionales. Necesita al menos una casilla; los títulos no se marcan ni cuentan como pendientes. Cada texto admite hasta 1.000 caracteres. Las filas vacías sugeridas no se guardan.

El borrador se guarda en servidor con autor, fecha y control de versión. La entrega exige todas las casillas esperadas, sin IDs ajenos o duplicados. Cambiar la definición conserva marcas de casillas intactas; las nuevas o modificadas quedan desmarcadas. Renombrar un título no borra las marcas intactas. Una transferencia conserva borrador y autor; una corrección crea su propio intento sin casillas completadas.

## Conservación y consulta

La entrega y evidencia se guardan con el cambio de estado. Supervisión consulta lo entregado; los procesos añaden decisiones, documentos y cantidades según su versión. Editar catálogo no reescribe evidencia histórica. Reportes puede incluir el checklist entregado.

Relacionados: [Supervisión](supervision.md), [Modelo e historial](modelo-historial.md), [Procesos](procesos.md), [Componentes visuales](../design/03-componentes.md).

## Fuentes y alcance

Código asociado a la app compartida según la [base documental](../../organizacion/base-documental.md). Revisión estática; no constituye una nueva prueba funcional, visual o de datos del host.

- [OperationalTaskService.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTaskService.cs)
- [OperationalTaskService.Checklist.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTaskService.Checklist.cs)
- [OperationalChecklist.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalChecklist.cs)
- [OperationalTaskService.Camera.cs](../../../../src/Sgol.Web/Infrastructure/Persistence/Planning/OperationalTaskService.Camera.cs)

[Mapa de dominio](../../mapas/dominio.md) · [Inicio](../../Inicio.md)
