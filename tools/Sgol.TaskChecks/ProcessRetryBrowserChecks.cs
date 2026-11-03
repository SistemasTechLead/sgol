using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class ProcessRetryBrowserChecks
{
    internal static async Task Run(SgolDbContext db,Guid run,string script)
    {
        var stage=await db.Set<OperationalSequenceStage>().SingleAsync(s=>s.RunId==run && s.Position==1);
        var attempt=await db.Set<OperationalSequenceAttempt>().SingleAsync(a=>a.Id==stage.CurrentAttemptId);
        var assignment=await db.OperationalAssignments.SingleAsync(a=>a.Id==attempt.AssignmentId);
        stage.Requirement="EXCEL";assignment.Requirement="EXCEL";
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var folder=Path.GetFullPath("artifacts/process-retry");Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,"evidence.xlsx");
        using(var stream=File.Create(path)) using(var zip=new System.IO.Compression.ZipArchive(stream,System.IO.Compression.ZipArchiveMode.Create)) {
            var parts=new Dictionary<string,string> {
                ["[Content_Types].xml"]="<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Override PartName='/xl/workbook.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml'/></Types>",
                ["xl/workbook.xml"]="<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Entrega' sheetId='1' r:id='rId1'/></sheets></workbook>",
                ["xl/_rels/workbook.xml.rels"]="<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet' Target='worksheets/sheet1.xml'/></Relationships>",
                ["xl/worksheets/sheet1.xml"]="<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData/></worksheet>" };
            foreach(var part in parts){using var writer=new StreamWriter(zip.CreateEntry(part.Key).Open());writer.Write(part.Value);}
        }
        var start=new System.Diagnostics.ProcessStartInfo("node"){UseShellExecute=false};
        foreach(var arg in new[]{script,assignment.Id.ToString(),path,folder})start.ArgumentList.Add(arg);
        using var browser=System.Diagnostics.Process.Start(start)!;await browser.WaitForExitAsync();
        if(browser.ExitCode!=0)throw new Exception("Process file retry browser checks failed");
        stage=await db.Set<OperationalSequenceStage>().SingleAsync(s=>s.Id==stage.Id);
        assignment=await db.OperationalAssignments.SingleAsync(a=>a.Id==assignment.Id);
        if(assignment.CompletedAt!=null)throw new Exception("Browser rejection unexpectedly delivered the task");
        stage.Requirement="COMENTARIO";assignment.Requirement="COMENTARIO";
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
    }
}
