using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class IncidenceChecks
{
    public static async Task EvidenceIntegrationChecks(SgolDbContext source)
    {
        var clock=new Clock();
        await using var db=new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(source.Database.GetConnectionString()).Options,bonusClock:clock);
        var f=await QaFixtures.CreateAsync(db);var row=await Row(db,f,clock,clock.UtcNow.AddMinutes(-30));
        using var stream=new MemoryStream();using(var pdf=SKDocument.CreatePdf(stream)){pdf.BeginPage(100,100).Clear(SKColors.White);pdf.EndPage();pdf.Close();}
        var bytes=stream.ToArray();
        db.Add(new OperationalAttachment{AssignmentId=row.Id,Content=bytes,ContentType="application/pdf",FileName="histórico.pdf",CreatedAt=clock.UtcNow.AddMinutes(-20)});
        var supervisorRole=await db.RoleAssignmentVersions.SingleAsync(r=>r.UserId==f.Other && r.Status==RoleAssignmentStatus.Active);
        db.Entry(supervisorRole).Property(r=>r.RoleCode).CurrentValue=CanonicalRole.Subcoordination;
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var service=new OperationalIncidenceService(db,clock,CorrectionSchedulingChecks.Service(db,clock));
        var day=WeekContract.LocalToday(clock.UtcNow);var request=new IncidenceRequest(f.Person,day,Guid.NewGuid(),"Retiro con adjunto histórico",clock.UtcNow.AddMinutes(2),[]);
        var preview=await service.PreviewAsync(f.Director,request,default);var receipt=await service.ApplyAsync(f.Director,request,preview.Fingerprint,default);db.ChangeTracker.Clear();
        var evidence=await service.EvidenceAsync(f.Other,day,receipt.Id,default);
        Check(evidence.Length==1 && evidence[0].PhotoId==null && evidence[0].FileName=="histórico.pdf","evidence integration scoped supervisor lists legacy retired attachment");
        var file=await service.EvidenceFileAsync(f.Other,day,receipt.Id,row.Id,null,default);
        Check(file.Content.SequenceEqual(bytes) && file.ContentType=="application/pdf","evidence integration legacy retired PDF preserves exact content");
        var originalRole=await db.RoleAssignmentVersions.SingleAsync(r=>r.UserId==f.Worker && r.Status==RoleAssignmentStatus.Active);
        db.Entry(originalRole).Property(r=>r.RoleCode).CurrentValue=CanonicalRole.Administration;
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        await Reject(async()=>await service.EvidenceFileAsync(f.Other,day,receipt.Id,row.Id,null,default),"evidence integration supervisor cannot read receipt outside current scope");
        Check((await service.EvidenceFileAsync(f.Director,day,receipt.Id,row.Id,null,default)).Content.SequenceEqual(bytes),"evidence integration direction retains authorized historical access after role change");
    }
}
