using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

// Historical records are still supported, but the application no longer creates them.
internal static class LegacyScheduleExceptionFixture
{
    public static async Task RestAsync(SgolDbContext db, Guid actor, Guid user, DateOnly day, DateTimeOffset now)
    {
        var person = await db.AppUsers.Where(u => u.Id == user).Select(u => u.PersonId).SingleAsync();
        var revision = await db.Set<PersonalScheduleException>().Where(e => e.PersonId == person && e.Day == day)
            .Select(e => (long?)e.Revision).MaxAsync() ?? 0;
        db.Add(new PersonalScheduleException { Id = Guid.CreateVersion7(), PersonId = person, Day = day,
            Revision = revision + 1, IntervalsJson = "[]", Reason = "Historical exception fixture",
            ChangedBy = actor, ChangedAt = now, IntentKey = Guid.CreateVersion7(), RequestHash = "legacy-fixture" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
}
