"""Complemento de diseño del editor; no contiene configuración real del piloto."""
from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.lib.colors import toColor as HexColor
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle
from pypdf import PdfReader
import pypdfium2 as pdfium

out = Path('output/pdf/procesos-editor'); out.mkdir(parents=True, exist_ok=True)
for name, file in [('UI','arial.ttf'),('UIBold','arialbd.ttf')]:
    pdfmetrics.registerFont(TTFont(name, 'C:/Windows/Fonts/'+file))
c = canvas.Canvas(str(out/'editor-procesos.pdf'), pagesize=(1440,1060))
c.setTitle('SGOL - Editor de procesos desde Tareas')
H = 1060
def box(x,y,w,h,fill='white',stroke='#E4DDD5'):
    c.setFillColor(HexColor(fill)); c.setStrokeColor(HexColor(stroke)); c.roundRect(x,H-y-h,w,h,8,fill=1,stroke=1)
def text(x,y,s,size=16,bold=False,color='#2B2521',width=1300):
    p=Paragraph(s,ParagraphStyle('ui',fontName='UIBold' if bold else 'UI',fontSize=size,leading=size*1.35,textColor=HexColor(color)))
    _,h=p.wrap(width,900); p.drawOn(c,x,H-y-h)
def field(x,y,label,value,w):
    text(x,y,label,13,True); box(x,y+25,w,44); text(x+12,y+35,value,14,width=w-24)
def button(x,y,s,w,primary=False):
    box(x,y,w,44,'#B64120' if primary else 'white'); text(x+12,y+11,s,14,True,'white' if primary else '#2B2521',w-24)
def base(title):
    box(0,0,1440,H,'#F6F1EC'); box(0,0,1440,78)
    text(40,18,'SGOL',26,True); text(1140,24,'Dirección',16,True)
    text(40,95,'DISEÑO DEL EDITOR - SIN DATOS OPERATIVOS REALES',11,True,color='#6B6055')
    text(40,127,'Tareas / Procesos',14,color='#6B6055'); text(40,161,title,29,True)

base('Crear proceso')
box(40,230,870,550); text(65,253,'Identidad del proceso',22,True)
field(65,305,'Nombre','Chequeo de inventario',815)
field(65,401,'Descripción','Conteo, revisión manual y aceptación del resultado.',815)
text(65,505,'Crear conserva un borrador con identidad estable. Todavía no genera trabajo.',16,width=800)
button(65,610,'Crear borrador',220,True); button(310,610,'Cancelar',160)
box(940,230,460,550); text(965,253,'Acciones distintas',22,True)
for i,s in enumerate(['1. Crear: guardar nombre y descripción.','2. Guardar borrador: conservar tareas y reglas.','3. Publicar: validar y congelar una versión.','4. Activar: permitir nuevos inicios.']):
    text(965,315+i*79,s,16,width=410)
text(40,986,'General conserva los calendarios de cada tarea independiente.',14,color='#6B6055')
c.showPage()
base('Chequeo de inventario · Borrador')
box(40,230,435,702); text(62,252,'Proceso e inicio',21,True)
field(62,297,'Nombre','Chequeo de inventario',391)
field(62,383,'Descripción','Conteo y revisión manual.',391)
field(62,469,'Calendario','Febrero y agosto · lunes del 20 al 25',391)
text(62,545,'Si no hay lunes, día 20.',14,width=390)
field(62,583,'Hora local · Ciudad de México','Pendiente de definir',391)
text(62,684,'Las cargas provienen de las tareas. Los plazos cuentan horas transcurridas.',15,width=386)
text(62,790,'Publicar conserva una versión; activar permite nuevos inicios. Las ejecuciones anteriores conservan sus reglas.',15,width=386)
box(500,230,900,702); text(524,252,'Tareas y etapas',21,True)
field(524,291,'Buscar por nombre o código','Buscar tarea de General',548)
button(1095,316,'Crear tarea aquí',276)
text(524,382,'Añadir suspende la generación independiente futura; conserva asignaciones e historia.',14,width=824)
for i,(name,hours,entry) in enumerate([('Piso de ventas','6','Inicio'),('Subcoordinación','1','Entrega anterior'),('Administración','1','Aprobación anterior'),('Dirección','168','Aprobación anterior')]):
    y=431+i*105; box(524,y,848,94)
    text(541,y+12,f'{i+1}. {name} · Tarea por seleccionar',16,True,width=480)
    text(541,y+43,f'Entrada: {entry} · Plazo: {hours} h',14,width=480)
    text(1030,y+14,'Orden: subir / bajar',13,width=310)
    text(1030,y+44,'Reglas e instrucciones',13,width=310)
button(40,952,'Guardar borrador',240,True); button(300,952,'Revisar y publicar',250)
button(570,952,'Activar versión publicada',320); button(910,952,'Cerrar editor',180)
text(40,1014,'Abandonar ofrece devolver a General solo tareas sin compromiso publicado; no elimina tareas ni evidencias.',12)
c.showPage(); c.save()
pdf=PdfReader(str(out/'editor-procesos.pdf')); assert len(pdf.pages)==2
document=pdfium.PdfDocument(str(out/'editor-procesos.pdf'))
for i,page in enumerate(document):
    page.render(scale=1).to_pil().save(out/f'editor-{i+1}.png')
print(out.resolve())
