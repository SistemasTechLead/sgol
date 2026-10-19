"""Vista previa y huellas de preservación para el retiro autorizado del catálogo de prueba."""
import argparse, json, subprocess
from pathlib import Path
parser=argparse.ArgumentParser(); parser.add_argument('database'); parser.add_argument('output'); parser.add_argument('--after',action='store_true'); args=parser.parse_args()
if args.database!='sgol_local' and not args.database.startswith('sgol_task_checks_processes_cleanup_'): raise RuntimeError('Database outside cleanup scope')
def query(sql):
    return subprocess.check_output(['docker','exec','sgol-local-postgres-1','psql','-U','sgol_local','-d',args.database,'-At','-v','ON_ERROR_STOP=1','-c',sql],text=True,encoding='utf-8').strip()
tables=['operational_assignment','operational_submission','operational_review','operational_attachment','operational_photograph','operational_reference_file','operational_occasion','operational_progress_entry','operational_capacity','direction_prepared_plan','direction_prepared_plan_item']
history={t:json.loads(query('SELECT json_build_object(\'rows\',count(*),\'hash\',md5(coalesce(string_agg(md5(to_jsonb(t)::text),\'\' ORDER BY md5(to_jsonb(t)::text)),\'\'))) FROM '+t+' t')) for t in tables}
out=Path(args.output); out.parent.mkdir(parents=True,exist_ok=True)
catalog_hash=query('SELECT md5(coalesce(string_agg(row_to_json(t)::text,\'\' ORDER BY t."Id"),\'\')) FROM operational_task t WHERE NOT t."Deleted"')
catalog=json.loads(query('SELECT coalesce(json_agg(t),\'[]\') FROM (SELECT "Id","TaskCode","Name","Active","Revision" FROM operational_task WHERE NOT "Deleted" ORDER BY "TaskCode") t'))
payload={'database':args.database,'catalogHash':catalog_hash,'definitionsToRetire':catalog,'history':history}
if args.after:
    before=json.loads(out.read_text(encoding='utf-8')); assert before['history']==history,'Historical data changed during catalog retirement'
    assert not catalog,'Test definitions remain in the catalogue'
    payload['preservationVerified']=True; target=out.with_name(out.stem+'-after.json')
else: target=out
target.write_text(json.dumps(payload,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'database':args.database,'visibleDefinitions':len(catalog),'catalogHash':catalog_hash,'historyPreserved':args.after},ensure_ascii=False))
