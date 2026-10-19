# Captura fotográfica de evidencia

Los requisitos FOTOGRAFIA de Mi trabajo abren la cámara mediante «Tomar Foto».
El usuario revisa la toma, puede reintentar, conservar varias fotos y eliminar
tomas antes de subirlas. No hay selección desde archivos, galería, pegado o arrastre.
Los documentos PDF conservan su entrada de archivos.

En tareas operativas, toma las fotografías requeridas, escribe la conclusión y
pulsa «Enviar a revisión». El botón sube y verifica las fotos antes de confirmar
la entrega; también puedes usar «Subir fotografías» previamente. Las fotografías se adjuntan
juntas al confirmar la entrega. Supervisión ve el conjunto completo y conserva
las entregas anteriores cuando reasigna una tarea.

## Requisitos del navegador

Se requiere HTTPS con un certificado confiable, JavaScript y permiso de cámara.
La primera solicitud de permiso ocurre al pulsar Tomar Foto. El navegador puede
recordar el permiso o volver a pedirlo. No se solicita audio, ubicación ni acceso
a archivos. En la red local utiliza la dirección HTTPS configurada; HTTP con una
IP privada no habilita getUserMedia. La cámara se apaga al cerrar, capturar,
navegar o pasar a segundo plano. Para volver del segundo plano pulsa Reabrir cámara.

## Límites y contexto

En tareas operativas nuevas, el valor inicial es 1–20 fotografías, 15 MiB por fotografía y 30 MiB por conjunto.
Dirección puede editar mínimo y máximo en Tareas, entre 1 y 20. El catálogo existente conserva sus valores hasta que se edite. Los límites se copian a cada
asignación y a sus reasignaciones; cambiar el catálogo no modifica asignaciones
existentes. La captura usa JPEG, hasta 2560 píxeles en el lado mayor y proporción
original. Las fotos sin subir permanecen en memoria del navegador; recargar las
descarta. No existe almacenamiento offline de fotografías.

La sesión de captura dura 30 minutos, está vinculada al usuario y a la tarea y
se consume al entregar. Cada carga tiene un identificador para recuperar el
resultado sin duplicar fotografías. Se permiten hasta el doble del máximo de fotos por sesión (al menos 10 intentos) y 120 intentos por hora en tareas operativas. El conjunto se vincula en una
transacción que comprueba autoría, sesión, cantidad y tamaño; una foto rechazada
no termina la tarea. Para un fallo de red, recupera o reintenta la carga. Si vence
la sesión, vuelve a tomar las fotos. Las fotografías temporales del servidor se
limpian cada 15 minutos al vencer o consumir su sesión.

Al abrir se solicita la cámara trasera con facingMode exacto. Cuando el navegador
no admite esa restricción, se consulta la lista de dispositivos después del permiso
y se intenta identificar la trasera por su nombre. «Cambiar cámara» libera la cámara
activa y solicita otra mediante su deviceId exacto. El selector «Cámara» permite
escoger una lente concreta cuando el teléfono tiene varias o sus nombres no
identifican frontal y trasera. La selección dura únicamente durante esa pantalla.

## Validación y almacenamiento

El servidor verifica tamaño real, MIME, firma, estructura y SHA-256. Escanea el
original con ClamAV antes de decodificar, reconstruye los píxeles en un JPEG nuevo
sin metadatos originales y escanea ese resultado. Si falla el analizador, se
bloquea la carga. SkiaSharp limita el procesamiento a dos imágenes simultáneas,
40 millones de píxeles de entrada y un resultado de 2560 píxeles en el lado mayor;
se comprueba el plazo de procesamiento entre operaciones del decodificador nativo.
Los hashes recibido y normalizado se conservan por separado.

Las tareas operativas guardan únicamente las fotos verificadas en PostgreSQL,
fuera de wwwroot, y las sirven mediante rutas que vuelven a comprobar el acceso.
Las fotos temporales no son descargables. Los accesos tienen no-store y nosniff.
El módulo anterior mantiene S3 privado, cuarentena, URLs temporales y análisis
en el worker; conserva la clave y hash recibidos para auditoría y limpia la copia
original en cuarentena después de un día. La revisión comprueba todos los archivos
de cada versión y la sustitución conserva el conjunto anterior completo.

Las sesiones y horas del servidor acreditan el procedimiento, pero una aplicación
web no puede demostrar que una imagen provino de una cámara física ni que la
escena sea reciente. Las cámaras virtuales y clientes modificados pueden simular
la captura. Los metadatos o el hash no constituyen prueba de autenticidad.

## Migraciones, activación y comprobaciones

AddCameraPhotographSets crea las colecciones y sesiones, convierte las fotos
históricas sin alterar sus bytes y conserva sus fechas y nombre histórico.
HardenCameraPolicyLimits agrega restricciones de límites. El esquema mantiene
compatibilidad con documentos e historial existentes.
PreserveEvidenceFileCounts conserva la cantidad de archivos esperada en cada
versión: una colección incompleta bloquea su revisión aunque la primera foto sea válida.

Para detener nuevas capturas y entregas fotográficas, configura
Evidence:Camera:Enabled=false (variable Evidence__Camera__Enabled=false) y reinicia
la aplicación. Las tareas permanecen pendientes y las evidencias anteriores
continúan disponibles. No se debe revertir el esquema después de crear conjuntos
múltiples; la migración impide un downgrade que perdería fotos o su procedencia.

Comprobaciones:

- `dotnet build SistemaSGOL.slnx -c Release`
- `dotnet run --project tools/Sgol.CameraChecks -- --clamav`
- `dotnet run --project tools/Sgol.TaskChecks`, con SGOL_TASK_CHECKS_CONNECTION
  apuntando a una base vacía local cuyo nombre comience con sgol_task_checks_.
- El ensayo del controlador DOM usa `node tools/Sgol.CameraChecks/camera-dom.cjs`
  y jsdom en .local/camera-dom/node_modules; no usa una cámara real.

La comprobación manual debe cubrir Android, iPhone y escritorio: permitir y
denegar cámara, cámara ocupada, cambiar frontal/trasera, reintentar, varias fotos,
cancelar, segundo plano, red interrumpida, vencimiento y lectura por Supervisión.
