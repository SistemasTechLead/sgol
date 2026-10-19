"""Cruce documental; conserva IDs del Excel y no carga ni activa tareas."""
import csv, hashlib, json, re
from pathlib import Path
import openpyxl

excel=Path('C:/Users/siste/Desktop/Tareas_SGOL_Crear_Tarea_actualizadoversion vale.xlsx')
md=Path('C:/Users/siste/Desktop/ESPECIFICACION_TECNICA_PRIMERA_CARGA_001_025.md')
out=Path('docs/secuencias/tareas-definidas'); out.mkdir(parents=True,exist_ok=True)
before={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (excel,md)}
w=openpyxl.load_workbook(excel,data_only=False); sheet=w['Tareas']
catalog={row[0].value:{'row':row[0].row,'values':[c.value for c in row]} for row in sheet.iter_rows(min_row=6) if row[0].value}
assert len(catalog)==202 and len(set(catalog))==202
definitions=[]
for number,line in enumerate(md.read_text(encoding='utf-8-sig').splitlines(),1):
    cells=[s.strip() for s in line.strip().strip('|').split('|')]
    if len(cells)==8 and re.fullmatch(r'T\d{3}',cells[0]):
        definitions.append(dict(zip(['ID_MD','ID_anterior_MD','Nombre_MD','Calendario_MD','Carga_min_MD','Responsable_MD','Evidencia_MD','Estado_MD'],cells))|{'Linea_MD':number})
assert len(definitions)==25
# Explicit semantic review. No numeric-ID join and no approximate matches treated as equality.
matches={
 'T002':('TAR-0039','RELACIONADA','Monitorear morosidad es más amplio que gestionar llamadas de crédito directo.'),
 'T003':('TAR-0024','RELACIONADA','Solicitar cambios en la lista no equivale a validar la lista y enviar evidencia.'),
 'T004':('TAR-0026','DESGLOSE','Telefonía es una parte del pago de servicios básicos; el Excel tiene una tarea conjunta.'),
 'T005':('TAR-0026','DESGLOSE','Agua es una parte del pago de servicios básicos; no se crean tres tareas por inferencia.'),
 'T006':('TAR-0026','DESGLOSE','Electricidad es una parte del pago de servicios básicos; 10 min es por servicio.'),
 'T007':('TAR-0032','RELACIONADA','Conciliar totales de un checklist mensual no equivale a ejecutar el cierre de caja.'),
 'T008':('TAR-0195','COINCIDENCIA_FUNCIONAL','Ambas comparan apartados del sistema con sus expedientes físicos.'),
 'T009':('TAR-0076','RELACIONADA','La reunión de cierre y planeación del Excel tiene alcance más amplio.'),
 'T013':('TAR-0003','RELACIONADA','La propuesta para vales es solo un caso de acciones comerciales locales.'),
 'T017':('TAR-0039','RELACIONADA','Seguimiento a distribuidoras y monitoreo general de morosidad comparten área, no alcance exacto.'),
 'T018':('TAR-0037','RELACIONADA','Validar cierre operativo incluye controles de cierre; registrar avance diario no es identidad exacta.'),
 'T023':('TAR-0062','RELACIONADA','Inspeccionar antes de abrir es solo parte de ejecutar la apertura operativa.'),
 'T024':('TAR-0037','RELACIONADA','También apunta al cierre; no se aplica la fusión técnica indicada por el MD.'),
}
records=[]
for d in definitions:
    code,status,reason=matches.get(d['ID_MD'],('','SIN_EQUIVALENTE_CONFIRMADO','No hay una tarea con el mismo resultado identificable en el Excel. Se conserva la definición sin inventar ID.'))
    row=catalog.get(code); values=row['values'] if row else [None]*26
    functional={k:d[k] for k in ('ID_MD','ID_anterior_MD','Nombre_MD','Calendario_MD','Carga_min_MD','Evidencia_MD','Linea_MD')}
    records.append({'CodigoExcel':code,'NombreExcel':values[1] or '', 'FilaExcel':row['row'] if row else '',
        'TipoCruce':status,'MotivoCruce':reason, **functional,
        'PeriodicidadExcel':values[8] or '', 'RepeticionesExcel':values[9], 'CalendarioExcel':values[10] or '',
        'HoraExcel':values[11], 'PlazoHorasExcel':values[22], 'CargaMinutosExcel':values[23],
        'RolExcel':values[2] or '', 'RequisitoExcel':values[5] or '', 'DudasExcel':values[25] or '',
        'Aplicacion':'Propuesta de llenado' if status=='COINCIDENCIA_FUNCIONAL' else 'Revisión de alcance; no trasladar valores automáticamente'})
with (out/'cruce-md-excel.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=list(records[0])); writer.writeheader(); writer.writerows(records)
allrows=[]
for code,row in catalog.items():
    v=row['values']; linked=[r['ID_MD'] for r in records if r['CodigoExcel']==code]
    allrows.append({'CodigoExcel':code,'Nombre':v[1],'FilaExcel':row['row'],'Rol':v[2],
        'RequiereEspecializacion':v[3],'Especializacion':v[4],'Requisito':v[5],
        'Periodicidad':v[8],'Repeticiones':v[9],'Calendario':v[10],'HoraCDMX':v[11],
        'Dias':{name:v[i] for i,name in enumerate(['Lunes','Martes','Miercoles','Jueves','Viernes','Sabado','Domingo'],12)},
        'PersonasDistintas':v[19],'Meta':v[20],'Unidad':v[21],'PlazoHoras':v[22],'CargaMinutos':v[23],
        'Activa':v[24],'Dudas':v[25],'ReferenciasMD':linked,
        'Pendientes':([name for index,name in [(2,'rol'),(5,'requisito'),(8,'periodicidad'),(22,'plazo'),(23,'carga')] if v[index] is None]
            + (['especialización marcada Sí pero valor NO'] if v[3]=='Sí' and v[4]=='NO' else []))})
(out/'catalogo-excel-extraido.json').write_text(json.dumps(allrows,ensure_ascii=False,indent=2,default=str)+'\n',encoding='utf-8')
(out/'evidencia-fuentes.json').write_text(json.dumps({'sha256':before,'filasExcel':len(catalog),'definicionesMD':len(records),
    'tiposCruce':{s:sum(r['TipoCruce']==s for r in records) for s in sorted({r['TipoCruce'] for r in records})},
    'cargasExcelInformadas':sum(r['CargaMinutos'] is not None for r in allrows),
    'plazosExcelInformados':sum(r['PlazoHoras'] is not None for r in allrows),
    'fuentesModificadas':False},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
report=['# Cruce de tareas definidas','',
    'Se conservan los códigos y nombres del Excel. T001–T025 del MD son referencias de fuente y no IDs del catálogo SGOL.',
    'Se extrajeron tiempos y comportamiento. Las jerarquías de asignación, presupuestos, macroprocesos, estados de integración y fusión del MD no se convierten en requisitos del sistema.', '',
    'El campo Min del MD es carga estimada, no plazo. Sus fechas son calendario; antes/después del cierre necesitan la hora real de apertura/cierre.', '',
    '| Referencia MD | Código del Excel | Correspondencia | Calendario del MD | Carga del MD |',
    '| --- | --- | --- | --- | --- |']
for r in records:
    report.append(f"| {r['ID_MD']} | {r['CodigoExcel'] or 'Sin equivalente confirmado'} | {r['TipoCruce']} | {r['Calendario_MD']} | {r['Carga_min_MD']} min |")
report += ['', '## Datos aprovechables y límites', '',
    '- TAR-0195: propuesta de martes, carga de 15 minutos y comparación sistema contra físico. El Excel solo aporta código y nombre; no informa plazo ni hora.',
    '- TAR-0026: el MD describe tres pagos el día 24, con 10 minutos por servicio. El Excel concentra servicios básicos: mensual, dos repeticiones distribuidas, plazo de 1 hora y carga de 45 minutos. Se conservan esos valores del Excel; no se sustituyen por 30 minutos ni se crean tres tareas.',
    '- TAR-0037: T018 y T024 se relacionan con cierre operativo, pero no se fusionan ni se eliminan tareas por la especificación descartada.',
    '- TAR-0018: el Excel pide aproximadamente 15 fotografías; SGOL permite máximo 5. No reducir ese requisito silenciosamente ni cambiar el límite como parte de este cruce.',
    '- Hay celdas con especialización marcada Sí y valor NO, calendarios distribuidos con días marcados y cargas/plazos vacíos. Requieren aclaración de la tarea concreta antes de publicarla.',
    '- La primera carga del MD no identifica las cuatro etapas del inventario semestral ni su hora. T025 asigna marcas para una auditoría; no sustituye registro, revisión ni aceptación del piloto.', '',
    'El CSV conserva valores de ambas fuentes y la línea del MD. El JSON conserva las 202 filas del Excel, sus datos de calendario y los campos faltantes. No se modificaron fuentes ni se escribieron tareas en la base.', '']
(out/'lectura-y-cruce.md').write_text('\n'.join(report),encoding='utf-8')
assert all(hashlib.sha256(p.read_bytes()).hexdigest()==before[str(p)] for p in (excel,md))
print((out/'evidencia-fuentes.json').read_text(encoding='utf-8'))
print(json.dumps([r for r in records if r['ID_MD'] in ('T004','T008','T018')],ensure_ascii=True))
