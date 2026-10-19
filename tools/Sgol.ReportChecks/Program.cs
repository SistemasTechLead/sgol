using Sgol.Web.Infrastructure.Persistence.Reporting;
using Sgol.Web.Infrastructure.Persistence.Planning;

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
var now = new DateTimeOffset(2026,10,10,18,0,0,TimeSpan.Zero);
var today = ReportCatalog.Day(now);
var filter = new ReportFilter();
Check(ReportAnalysis.Range(filter,today) == (new DateOnly(2026,10,5),new DateOnly(2026,10,11)),"Current week uses Mexico City calendar");
filter.Range="last-month";
Check(ReportAnalysis.Range(filter,today) == (new DateOnly(2026,9,1),new DateOnly(2026,9,30)),"Relative previous month");
filter.Range="custom";filter.From=new(2026,10,11);filter.To=new(2026,10,10);
try {ReportAnalysis.Range(filter,today);throw new Exception("Invalid dates accepted");} catch(ArgumentException) {checks++;}
filter.From=new(2025,1,1);filter.To=new(2026,10,10);
try {ReportAnalysis.Range(filter,today);throw new Exception("Unbounded range accepted");} catch(ArgumentException) {checks++;}
filter=new();filter.Columns="task,unknown";
try {ReportAnalysis.Range(filter,today);throw new Exception("Unknown column accepted");} catch(ArgumentException) {checks++;}
ReportRow Row(string status, Guid? from=null) => new(Guid.NewGuid(),Guid.Parse("11111111-1111-1111-1111-111111111111"),Guid.NewGuid(),"Tarea","T-1","Persona","PISO_VENTAS","Plan","Área",status,"COMENTARIO","PROGRESO","SEMANAL",1,24,60,30,now.AddDays(-3),now.AddDays(-2),status is "CONCLUIDA" or "PENDIENTE_REVISION"?now.AddDays(-1):null,null,100,50,"unidades","","",from);
var done=Row("CONCLUIDA");var parent=Row("REASIGNADA");var pending=Row("VENCIDA",parent.Id);var delivered=Row("PENDIENTE_REVISION");
var rows=new[]{done,parent,pending,delivered};
var metrics=ReportAnalysis.Metrics(rows,"overview",now);
Check(metrics[0].Value=="3" && metrics[1].Value=="33.3 %","Reassignment attempts excluded from fulfillment denominator");
Check(metrics[2].Value=="1" && metrics[3].Value=="1","Delivery does not imply successful review");
Check(ReportAnalysis.Filter(rows,new(){Kind="overdue"},now).Single().Id==pending.Id,"Overdue report excludes terminal and delivered tasks");
Check(ReportAnalysis.Filter(rows,new(){Kind="review"},now).Single().Id==delivered.Id,"Review backlog contains only delivered pending decisions");
Check(ReportAnalysis.Filter(rows,new(){Kind="load"},now).Single().Id==pending.Id,"Load report excludes deliveries and reassigned attempts");
Check(ReportAnalysis.Filter(rows,new(){Kind="goals",ProgressMin=60},now).Length==0,"Progress thresholds apply only to matching goals");
var chart=ReportAnalysis.Charts(rows,new(){Kind="compliance"},now,new Dictionary<Guid,decimal>());
Check(chart[1].Points.Single().Value==33.3m,"Chart fulfillment agrees with metrics");
Check(Math.Abs(chart[1].Points.Single().Segments!.Sum(s=>s.Value)-100m)<0.01m,"Stacked states account for every non-reassigned task");
var assignment=new OperationalAssignment{StartsAt=now.AddDays(-1),DueAt=now.AddHours(-1)};
Check(ReportCatalog.Status(assignment,now)=="VENCIDA","Pending deadline is derived at snapshot time");
assignment.CompletedAt=now;
Check(ReportCatalog.Status(assignment,now)=="PENDIENTE_REVISION","Submission remains pending review");
assignment.ReviewStatus="CONCLUIDA";
Check(ReportCatalog.Status(assignment,now)=="CONCLUIDA","Final review takes precedence over overdue deadline");
Check(ReportCatalog.Time(now)=="10/10/2026 12:00","Timestamp displayed in Mexico City timezone");
Check(ReportAnalysis.Metrics([],"overview",now)[1].Value=="Sin datos","Empty results do not claim zero-percent failure");
// Each decoy differs in exactly one field, so a missing predicate broadens the result.
var target = Row("VENCIDA") with { Role="ADMINISTRACION", Area="Contabilidad", Code="T-EXACT",
    Specialization="Contaduría", Requirement="DOCUMENTO", Schedule="DIAS_FIJOS", Frequency="MENSUAL",
    Repetitions=3, Hours=48, Minutes=90, Goal=200, Units=120 };
var cases = new (string Name, ReportFilter Filter, ReportRow Decoy)[] {
    ("person", new(){Person=target.PersonId.ToString()}, target with {Id=Guid.NewGuid(),PersonId=Guid.NewGuid()}),
    ("role", new(){Role=target.Role}, target with {Id=Guid.NewGuid(),Role="PISO_VENTAS"}),
    ("plan", new(){Plan=target.PlanId.ToString()}, target with {Id=Guid.NewGuid(),PlanId=Guid.NewGuid()}),
    ("area", new(){Area=target.Area}, target with {Id=Guid.NewGuid(),Area="Ventas"}),
    ("task", new(){Task=target.Code}, target with {Id=Guid.NewGuid(),Code="OTHER"}),
    ("specialization", new(){Specialization=target.Specialization}, target with {Id=Guid.NewGuid(),Specialization="Marketing"}),
    ("status", new(){Status=target.Status}, target with {Id=Guid.NewGuid(),Status="ASIGNADA"}),
    ("requirement", new(){Requirement=target.Requirement}, target with {Id=Guid.NewGuid(),Requirement="COMENTARIO"}),
    ("schedule", new(){Schedule=target.Schedule}, target with {Id=Guid.NewGuid(),Schedule="PROGRESO"}),
    ("frequency", new(){Frequency=target.Frequency}, target with {Id=Guid.NewGuid(),Frequency="SEMANAL"}),
    ("repetitions", new(){Repetitions=3}, target with {Id=Guid.NewGuid(),Repetitions=2}),
    ("hours", new(){Hours=48}, target with {Id=Guid.NewGuid(),Hours=24}),
    ("minutes", new(){Minutes=90}, target with {Id=Guid.NewGuid(),Minutes=30}),
    ("minimum progress", new(){ProgressMin=60}, target with {Id=Guid.NewGuid(),Units=119}),
    ("maximum progress", new(){ProgressMax=60}, target with {Id=Guid.NewGuid(),Units=121})
};
foreach (var item in cases)
    Check(ReportAnalysis.Filter([target,item.Decoy],item.Filter,now).Single().Id==target.Id,"Exact filter: "+item.Name);
var combined = new ReportFilter { Person=target.PersonId.ToString(),Role=target.Role,Plan=target.PlanId.ToString(),
    Area=target.Area,Task=target.Code,Specialization=target.Specialization,Status=target.Status,Requirement=target.Requirement,
    Schedule=target.Schedule,Frequency=target.Frequency,Repetitions=3,Hours=48,Minutes=90,ProgressMin=60,ProgressMax=60 };
Check(ReportAnalysis.Filter(cases.Select(c=>c.Decoy).Append(target),combined,now).Single().Id==target.Id,
    "Combined filters intersect; no parameter is ignored");
foreach (var kind in ReportCatalog.Definitions.Select(d=>d.Id)) {
    var preset=ReportCatalog.Preset(kind);
    Check(preset.Kind==kind && preset.Range=="week" && preset.Group==(kind=="plans"?"plan":"person") &&
        preset.DateBasis==(kind=="results"?"reviewed":kind=="overdue"?"due":kind=="review"?"delivered":"start"),"Preset settings: "+kind);
    ReportAnalysis.Range(preset,today);
    Check(ReportCatalog.SameConfiguration(preset,ReportCatalog.Preset(kind)),"Template identity matches applied settings: "+kind);
    Check(!ReportCatalog.SameConfiguration(preset,new ReportFilter{Kind=kind,Range="month"}),"Changed period clears template identity: "+kind);
}
var sortRows = new[]{target with {Id=Guid.NewGuid(),Person="Z",Due=now.AddDays(2),Units=100},
    target with {Id=Guid.NewGuid(),Person="A",Due=now.AddDays(1),Units=20},target with {Id=Guid.NewGuid(),Person="B",Due=now,Units=180}};
Check(ReportAnalysis.Filter(sortRows,new(){Order="due"},now).Select(r=>r.Person).SequenceEqual(["B","A","Z"]),"Detail sorted by due date");
Check(ReportAnalysis.Filter(sortRows,new(){Order="person"},now).Select(r=>r.Person).SequenceEqual(["A","B","Z"]),"Detail sorted by person");
Check(ReportAnalysis.Filter(sortRows,new(){Order="progress"},now).Select(r=>r.Person).SequenceEqual(["A","Z","B"]),"Detail sorted by progress");
Check(ReportAnalysis.Filter(sortRows,new(){Order="delay"},now).Select(r=>r.Person).SequenceEqual(["B","A","Z"]),"Detail sorted by oldest deadline");
var groups = new[]{target,target with{Id=Guid.NewGuid(),Person="Otra persona",PersonId=Guid.NewGuid(),Plan="Otro plan",
    PlanId=Guid.NewGuid(),Role="PISO_VENTAS",Task="Otra tarea",Code="OTHER"}};
foreach(var group in new[]{"person","plan","role","task"})
    Check(ReportAnalysis.Charts(groups,new(){Kind="plans",Group=group},now,new Dictionary<Guid,decimal>())[1].Points.Count==2,"Chart grouping: "+group);
Check(ReportAnalysis.Range(new(){Range="custom",From=today,To=today},today)==(today,today),"Custom dates inclusive for a single day");
var sameLabels = new[]{target,target with{Id=Guid.NewGuid(),PersonId=Guid.NewGuid(),PlanId=Guid.NewGuid(),Code="OTHER"}};
foreach(var group in new[]{"person","plan","task"}) {
    var points=ReportAnalysis.Charts(sameLabels,new(){Kind="plans",Group=group},now,new Dictionary<Guid,decimal>())[1].Points;
    Check(points.Count==2 && points.Select(p=>p.Key).Distinct().Count()==2,"Equal labels retain distinct groups and filter keys: "+group);
}
foreach(var (range,from,to) in new (string,DateOnly,DateOnly)[]{
    ("last-week",new(2026,9,28),new(2026,10,4)),("month",new(2026,10,1),new(2026,10,31)),
    ("quarter",new(2026,10,1),new(2026,12,31)),("semester",new(2026,7,1),new(2026,12,31)),
    ("year",new(2026,1,1),new(2026,12,31))})
    Check(ReportAnalysis.Range(new(){Range=range},today)==(from,to),"Calendar range: "+range);
Console.WriteLine($"{checks} report checks passed.");
