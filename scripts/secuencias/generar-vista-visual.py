"""Produce vistas de diseño en PDF, sin ejecutar HTML ni abrir un navegador."""
from pathlib import Path
from html import escape

from reportlab.pdfgen import canvas
from reportlab.lib.colors import toColor
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle
from pypdf import PdfReader

OUT = Path("output/pdf/secuencias-inventario")
OUT.mkdir(parents=True, exist_ok=True)
PDF = OUT / "propuesta-visual-inventario.pdf"
for name, filename in [("UI", "arial.ttf"), ("UIBold", "arialbd.ttf")]:
    pdfmetrics.registerFont(TTFont(name, str(Path("C:/Windows/Fonts") / filename)))

INK, MUTED, LINE, STRONG = "#2B2521", "#6B6055", "#E4DDD5", "#9E9285"
WARM, ACCENT, GREEN, GRAY = "#F6F1EC", "#B64120", "#2F7A4F", "#EFEDE9"
c = canvas.Canvas(str(PDF), pagesize=(1440, 1060))
c.setTitle("SGOL - Propuesta visual de chequeo semestral de inventario")
c.setAuthor("SGOL")
H = 1060


def box(x, top, w, h, fill="white", stroke=LINE, radius=8, line=1):
    c.setFillColor(toColor(fill)); c.setStrokeColor(toColor(stroke)); c.setLineWidth(line)
    c.roundRect(x, H-top-h, w, h, radius, fill=1, stroke=1)


def text(x, top, value, size=16, color=INK, bold=False, max_width=None):
    font = "UIBold" if bold else "UI"
    if max_width is not None:
        assert pdfmetrics.stringWidth(value, font, size) <= max_width, (value, max_width)
    c.setFont(font, size); c.setFillColor(toColor(color)); c.drawString(x, H-top-size, value)


def paragraph(x, top, value, w, size=16, color=INK, bold=False, max_h=None):
    p = Paragraph(escape(value), ParagraphStyle("ui", fontName="UIBold" if bold else "UI", fontSize=size,
        leading=size*1.38, textColor=toColor(color)))
    _, height = p.wrap(w, 2000)
    if max_h is not None:
        assert height <= max_h, (value, height, max_h)
    p.drawOn(c, x, H-top-height)
    return height


def badge(x, top, label, kind="wait", size=12):
    fill, fg = {"wait": ("#ECE9E5", "#514A43"), "active": ("#FAE6DF", ACCENT), "done": ("#E7F1E9", GREEN)}[kind]
    width = pdfmetrics.stringWidth(label, "UIBold", size)+20
    box(x, top, width, 25, fill, fill, 4)
    text(x+10, top+4, label, size, fg, True)
    return width


def button(x, top, label, w, primary=False, size=15, h=47):
    box(x, top, w, h, ACCENT if primary else "white", ACCENT if primary else STRONG, 6)
    tw = pdfmetrics.stringWidth(label, "UIBold", size)
    assert tw <= w-22, (label, tw, w)
    text(x+(w-tw)/2, top+(h-size)/2-2, label, size, "white" if primary else INK, True)


def checkbox(x, top, label, w, checked=True, size=15):
    box(x, top+3, 18, 18, ACCENT if checked else "white", ACCENT if checked else STRONG, 3)
    if checked:
        c.setStrokeColor(toColor("white")); c.setLineWidth(2)
        p=c.beginPath(); p.moveTo(x+4,H-top-12); p.lineTo(x+8,H-top-16); p.lineTo(x+15,H-top-7); c.drawPath(p)
    return paragraph(x+29, top, label, w-29, size)


def field(x, top, label, value, w, h=47, size=15):
    text(x, top, label, 13, MUTED, True)
    box(x, top+25, w, h, "white", STRONG, 5)
    paragraph(x+13, top+37, value, w-26, size, max_h=h-18)


def header(role, mobile=False):
    width = 430 if mobile else 1440
    box(0,0,width,H,WARM,WARM,0)
    box(0,0,width,78,"white",LINE,0)
    text(22 if mobile else 40, 17, "SGOL", 26, INK, True)
    text(22 if mobile else 40, 48, "LORETTA - ZAPATERÍA", 10, MUTED)
    if mobile:
        text(252,24,role,12,MUTED,max_width=155)
    else:
        text(1140,22,role,16,INK,True,max_width=255)
        text(1140,47,"Mi trabajo",12,MUTED)
    text(22 if mobile else 40,91,"PROPUESTA VISUAL - DATOS DE EJEMPLO",10,MUTED,True)


ROLES=["Piso de ventas","Subcoordinación","Administración","Dirección"]
CAPTIONS=["Registrar inventario completo","Revisar hoja, sistema y zapatos","Chequear hoja e irregularidades","Aceptar resultado del inventario"]


def flow(active, mobile=False):
    for i, role in enumerate(ROLES):
        if mobile:
            x=22+(i%2)*198; top=199+(i//2)*100; w=188; h=90
        else:
            x=40+i*343.3; top=210; w=330.3; h=119
        kind="done" if i<active else "active" if i==active else "wait"
        box(x,top,w,h,GRAY if i>active else "white",ACCENT if i==active else LINE,8,2 if i==active else 1)
        text(x+14,top+12,role,14 if mobile else 17,INK,True,max_width=w-28)
        if mobile:
            badge(x+14,top+40,"Entregada" if i==0 and i<active else "Revisada" if i<active else "Disponible" if i==active else "En espera",kind,10)
        else:
            text(x+14,top+42,CAPTIONS[i],13,MUTED,max_width=w-28)
            badge(x+14,top+75,"Entregada" if i==0 and i<active else "Revisada" if i<active else "Disponible" if i==active else "En espera",kind)
            text(x+150,top+78,["6 horas","1 hora","1 hora","1 semana"][i],12,MUTED)


def doc_card(x, top, width, version):
    box(x,top,width,86,WARM,LINE,7)
    box(x+16,top+17,46,52,"white",LINE,5)
    text(x+25,top+33,"PDF",12,ACCENT,True)
    text(x+77,top+18,f"Hoja de inventario - V{version}",17,INK,True)
    text(x+77,top+47,f"Documento recibido · Versión {version}",13,MUTED)
    button(x+width-161,top+21,"Abrir PDF",143,size=14,h=43)


def histories(x, top, w, direction=False):
    box(x,top,w,392)
    text(x+23,top+23,"Historial del trámite",22,INK,True)
    paragraph(x+23,top+62,"Cada revisión conserva la versión utilizada.",w-46,14,MUTED)
    rows = [
        ("Dirección recibió V3","22 feb · 14:10 · Chequeo administrativo recibido."),
        ("Administración revisó V3","22 feb · 14:10 · Hoja e irregularidades revisadas."),
        ("Subcoordinación entregó V3","22 feb · 13:42 · Hoja marcada con diferencias pendientes."),
        ("Piso entregó la hoja corregida V2","22 feb · 13:00 · Búsqueda y sobrantes documentados."),
    ] if direction else [
        ("Subcoordinación recibió V2","22 feb · 13:00 · Nueva revisión de la hoja y los zapatos."),
        ("Piso entregó la hoja corregida V2","22 feb · 13:00 · Resultado de búsqueda documentado."),
        ("Subcoordinación devolvió V1","22 feb · 11:45 · Buscar faltantes y presentar sobrantes."),
        ("Piso entregó el inventario V1","22 feb · 11:30 · Registro completo declarado."),
    ]
    for i,(title,note) in enumerate(rows):
        y=top+103+i*68
        c.setFillColor(toColor(STRONG)); c.circle(x+26,H-y-10,3.3,fill=1,stroke=0)
        paragraph(x+42,y,title,w-65,14,INK,True,max_h=22)
        paragraph(x+42,y+24,note,w-65,12,MUTED,max_h=35)


def differences(x, top, w, h=226):
    box(x,top,w,h)
    text(x+23,top+22,"Diferencias pendientes",20,INK,True)
    text(x+23,top+59,"SKU-DEMO-01 · Talla 24",14,INK,True)
    text(x+23,top+83,"Faltan 2 pares después de buscar.",14,MUTED)
    badge(x+23,top+111,"Pendiente de aclarar")
    text(x+23,top+150,"SKU-DEMO-02 · Talla 26",14,INK,True)
    text(x+23,top+174,"Sobra 1 par; contrastar con la hoja.",14,MUTED)


def desktop(direction=False):
    global H
    H=1060; c.setPageSize((1440,H)); active=3 if direction else 1
    header(ROLES[active]); text(40,119,"Mi trabajo / Secuencias",13,MUTED)
    text(40,147,"Chequeo de inventario",32,INK,True)
    text(40,186,"Febrero 2027 · Ejecución de ejemplo INV-2027-02",14,MUTED)
    flow(active)
    x,top,w=40,355,870
    box(x,top,w,646)
    text(x+25,top+21,f"ETAPA {active+1} DE 4",12,MUTED,True)
    text(x+25,top+47,"Aceptar resultado del inventario" if direction else "Revisar el inventario",25,INK,True)
    instruction = "Revisa el PDF recibido y sus irregularidades. Puedes devolverlo o aceptar esta versión con una conclusión expresa." if direction else "Contrasta la hoja con Sizes & Colors y los zapatos. Marca las diferencias que no se resolvieron después de buscar."
    paragraph(x+25,top+86,instruction,w-50,16,max_h=48)
    doc_card(x+25,top+147,w-50,3 if direction else 2)
    text(x+25,top+257,"DISPONIBLE DESDE",12,MUTED,True)
    text(x+25,top+280,"22 feb 2027 · 14:10" if direction else "22 feb 2027 · 13:00",16,INK,True)
    text(x+470,top+257,"VENCE",12,MUTED,True)
    text(x+470,top+280,"01 mar 2027 · 14:10" if direction else "22 feb 2027 · 14:00",16,INK,True)
    if direction:
        field(x+25,top+321,"Resultado que acepta Dirección","Aceptado con diferencias pendientes",w-50)
        checkbox(x+25,top+413,"Revisé la versión V3 y sus irregularidades.",w-50)
        text(x+25,top+457,"Conclusión final",14,INK,True)
        box(x+25,top+484,w-50,79,"white",STRONG,5)
        paragraph(x+39,top+497,"Acepto el resultado de V3. Quedan documentados los dos pares faltantes y el par sobrante. Estoy satisfecho con el resultado de la revisión.",w-78,15,max_h=62)
        button(x+25,top+581,"Aceptar V3 y cerrar el proceso",375,True)
        button(x+416,top+581,"Devolver a Administración",300)
    else:
        text(x+25,top+321,"PDF corregido (si cambió la hoja)",14,INK,True)
        button(x+25,top+349,"Adjuntar PDF actualizado",248,size=14,h=43)
        text(x+291,top+359,"Sin cambios, la revisión conserva V2.",14,MUTED)
        checkbox(x+25,top+417,"Revisé la hoja contra Sizes & Colors y junto con los zapatos.",w-50)
        text(x+25,top+461,"Conclusión de la revisión",14,INK,True)
        box(x+25,top+488,w-50,75,"white",STRONG,5)
        paragraph(x+39,top+501,"Se revisó el resultado de la búsqueda. Persisten 2 pares faltantes y 1 sobrante; las diferencias quedan marcadas en la hoja.",w-78,15,max_h=60)
        button(x+25,top+581,"Revisar y pasar a Administración",382,True)
        button(x+423,top+581,"Devolver a Piso",220)
    histories(934,355,466,direction); differences(934,775,466)
    text(40,1022,"La aceptación conserva las diferencias pendientes y el PDF autorizado." if direction else "Una entrega o revisión de etapa no equivale al cierre del proceso.",13,MUTED)
    text(1260,1022,"02 / Dirección" if direction else "01 / Escritorio",12,MUTED)
    c.showPage()


def mobile():
    global H
    H=1510; c.setPageSize((430,H)); header("Subcoordinación",True)
    text(22,122,"Mi trabajo / Secuencias",12,MUTED)
    text(22,147,"Chequeo de inventario",25,INK,True)
    text(22,179,"Febrero 2027 · Hoja V2",13,MUTED)
    flow(1,True)
    box(22,415,386,797)
    text(40,435,"ETAPA 2 DE 4",11,MUTED,True)
    text(40,458,"Revisar el inventario",23,INK,True)
    paragraph(40,497,"Revisa la hoja contra Sizes & Colors y los zapatos. Marca lo que siga pendiente.",350,15,max_h=70)
    box(40,579,350,130,WARM,LINE,6)
    text(55,594,"PDF · Hoja de inventario V2",16,INK,True)
    text(55,622,"Documento recibido · Versión 2",13,MUTED)
    button(55,645,"Abrir PDF",153,size=14,h=48)
    text(40,728,"Disponible desde",12,MUTED,True); text(40,750,"22 feb · 13:00",16,INK,True)
    text(232,728,"Vence",12,MUTED,True); text(232,750,"22 feb · 14:00",16,INK,True)
    text(40,781,"Si cambió la hoja, entrega otro PDF.",14,MUTED)
    button(40,811,"Adjuntar PDF actualizado",350,size=14,h=48)
    checkbox(40,879,"Revisé la hoja, Sizes & Colors y los zapatos presentados.",350,True,15)
    text(40,947,"Conclusión de la revisión",14,INK,True)
    box(40,974,350,100,"white",STRONG,5)
    paragraph(54,987,"Persisten 2 pares faltantes y 1 sobrante. Las diferencias quedan marcadas en la hoja.",322,15,max_h=73)
    button(40,1097,"Revisar y pasar a Administración",350,True,size=14,h=48)
    button(40,1154,"Devolver a Piso",350,size=14,h=48)
    differences(22,1230,386,226)
    text(22,1480,"03 / Pantalla estrecha · Propuesta de diseño",11,MUTED)
    c.showPage()


def configuration():
    global H
    H=1060; c.setPageSize((1440,H)); header("Dirección")
    text(40,122,"Configuración / Secuencias de trabajo",13,MUTED)
    text(40,151,"Chequeo semestral de inventario",32,INK,True)
    text(40,195,"Versión 1 propuesta · Las ejecuciones existentes conservan su configuración",14,MUTED)
    box(40,244,430,740)
    text(64,268,"Calendario de la secuencia",22,INK,True)
    field(64,324,"Meses","Febrero y agosto",382)
    field(64,429,"Ventana de inicio","Del día 20 al 25",382)
    field(64,534,"Día preferido","Lunes",382)
    paragraph(64,641,"Si no hay lunes entre el 20 y el 25, iniciar el día 20.",382,16,max_h=52)
    field(64,722,"Hora de inicio","Elegir hora al configurar",382)
    paragraph(64,848,"La carga y la especialización provienen de la tarea elegida. Los plazos se cuentan en horas transcurridas.",382,15,MUTED,max_h=85)
    box(494,244,906,740)
    text(518,268,"Etapas y condiciones de avance",22,INK,True)
    for i,role in enumerate(ROLES):
        y=320+i*152
        box(518,y,858,136,"white",LINE,6)
        text(538,y+15,f"{i+1}. {role}",18,INK,True)
        text(538,y+44,CAPTIONS[i],15,MUTED)
        box(538,y+76,380,40,WARM,LINE,5)
        text(551,y+87,"Seleccionar tarea del catálogo",14,MUTED)
        text(946,y+12,"Plazo (horas)",12,MUTED,True)
        box(946,y+36,110,41,"white",STRONG,5)
        text(965,y+45,str([6,1,1,168][i]),17,INK,True)
        text(1080,y+12,"Devolución permitida",12,MUTED,True)
        paragraph(1080,y+40,"Sin etapa anterior" if i==0 else ROLES[i-1],266,14,max_h=37)
        text(946,y+92,"Inicio del proceso" if i==0 else "Tras entrega de Piso" if i==1 else "Tras aprobación anterior",12,MUTED)
    text(40,1018,"Las decisiones y versiones se conservan en el historial de cada ejecución.",13,MUTED)
    text(1230,1018,"04 / Configuración",12,MUTED)
    c.showPage()


desktop(); desktop(True); mobile(); configuration(); c.save()
reader=PdfReader(str(PDF))
assert len(reader.pages)==4
all_text="\n".join(page.extract_text() or "" for page in reader.pages)
for phrase in ["Sizes & Colors", "Devolver a Piso", "Devolver a Administración", "168", "Aceptado con diferencias pendientes", "Febrero y agosto"]:
    assert phrase in all_text, phrase
print(f"Created {PDF.resolve()} - 4 pages verified; visual rendering pending.")
