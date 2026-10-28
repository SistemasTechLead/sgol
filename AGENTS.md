# Instrucciones de trabajo para SistemaSGOL

## Criterio y comunicación

- Cuestiona las suposiciones de la petición antes de validarlas. Señala puntos débiles, consecuencias y alternativas con razones concretas.
- Sé directo, sin halagos genéricos, relleno ni repetir la petición como respuesta.
- Investiga el comportamiento actual antes de proponer o implementar cambios. Distingue hechos observados, inferencias y decisiones pendientes.
- Conserva el objetivo y las autorizaciones del usuario entre mensajes. No pidas otra vez permisos ya concedidos; pregunta por decisiones de negocio que no estén resueltas.
- Sigue la petición actual del usuario cuando contradiga documentación histórica y explica las discrepancias relevantes.

## Protocolo del proyecto

Antes de planear o implementar cambios funcionales, de interfaz o mixtos, lee [el protocolo de implementación](docs/desarrollo/PROTOCOLO_IMPLEMENTACION.md). Aplica la profundidad proporcional al impacto; una corrección documental no exige pruebas de navegador ni compilación.

- Clasifica el cambio como funcional, de interfaz o mixto. Los mixtos cumplen ambas validaciones.
- Si se solicita solo un plan, investiga y entrega el plan; separa las pruebas actuales de las propuestas. No presentes escenarios propuestos como ejecutados.
- Sigue el dato desde su creación hasta almacenamiento, copias, asignación, ejecución, revisión, consultas e historial. Busca productores y consumidores, no solo coincidencias de texto.
- Identifica archivos, funciones y rutas reales que sustentan el análisis. Revisa registros existentes además de registros nuevos.
- Incluye una matriz de escenarios con resultado esperado, prueba, entorno y estado. Cubre combinaciones relevantes, especialmente cambios simultáneos y estados históricos.
- Pregunta cuando una decisión no resuelta cambie cargas, responsables, plazos, permisos, historial o comportamiento de registros existentes. Presenta opciones y consecuencias; continúa el trabajo independiente.
- Prueba recorridos completos con formularios y servicios reales. Las pruebas aisladas y la compilación no sustituyen la integración.
- Los conflictos esperados deben identificar su causa y la entidad afectada, con una acción comprensible. Evita mensajes genéricos que oculten la regla aplicada.

## Interfaz existente

- Inspecciona pantallas equivalentes y reutiliza componentes, tokens, estilos, navegación y patrones existentes.
- Consulta [la referencia visual](docs/design/ESTILO_VISUAL_V2.md), sus referencias aplicables y el código actual. Señala documentación desactualizada; no restaures restricciones históricas que el usuario ya haya sustituido.
- Verifica la interfaz renderizada en los tamaños y temas afectados, teclado, foco, contenido largo y estados de carga, vacío, error y éxito aplicables.
- Distingue pruebas de componentes o maquetas de las pruebas de la pantalla y el flujo reales.

## Entornos y datos

- Aclaración del usuario, 28/10/2026: el entorno actual se usa exclusivamente para desarrollo y pruebas de operación. Se introducen datos simulados diariamente para explorar variables del sistema. No es producción real.
- El acceso local y el acceso LAN pueden compartir aplicación y base de datos. Identifica el destino efectivo antes de escribir; no supongas que dos direcciones son dos entornos aislados.
- Las pruebas operativas con datos simulados forman parte del trabajo autorizado cuando la petición las incluye. No trates esos datos como registros productivos reales ni exijas aprobación por esa suposición.
- Usa bases aisladas para regresiones automatizadas, casos destructivos y pruebas que puedan interferir con la simulación diaria. Reutiliza una copia de la simulación cuando se necesite reproducir su estado.
- Conserva el estado necesario para reproducir fallos y no alteres simulaciones ajenas al alcance. No desactives validaciones para que una prueba pase.
- El usuario prevé eliminar los datos simulados al pasar a producción. Es una tarea futura de puesta en producción: esta declaración no ordena borrarlos ahora ni autoriza reinicios automáticos del entorno.
- No publiques credenciales, bases, evidencias, respaldos ni archivos privados. Las rutas y el estado de los servicios deben verificarse, no suponerse a partir de documentación antigua.

## Validación y cierre

- Conserva las invariantes acordadas y verifica permisos, datos históricos, concurrencia, reintentos y atomicidad cuando puedan verse afectados.
- Ejecuta pruebas adecuadas al cambio y añade regresiones para fallos corregidos. Una vez cubierto el riesgo y aprobadas las pruebas, evita repeticiones sin motivo.
- Registra escenarios aprobados, fallidos, no ejecutados y no aplicables, con su evidencia o justificación. No declares validado lo que solo se inspeccionó.
- Si una prueba crítica queda pendiente, comunica la limitación y el estado real; no presentes el cambio como completamente validado.
- Revisa el diff antes de atribuir cambios. Conserva modificaciones previas del usuario. Respeta las autorizaciones vigentes para commit, push y despliegue, sin inventar nuevas aprobaciones.
