using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Organization.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Generation;

public sealed partial class EfGenerationRequestService
{
    private async Task<Guid?> ValidateParentAsync(CreateGenerationRequestCommand command, JsonElement input, CancellationToken token)
    {
        if (ManualGenerationInput.TaskCode(input) != "TAR-0093") return null;
        var id = input.GetProperty("parentObligationId").GetGuid();
        // This row lock also serializes the check with conclusion's UPDATE.
        var parent = await dbContext.WorkObligations
            .FromSqlInterpolated($"SELECT * FROM work_obligation WHERE id = {id} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(token);
        if (parent?.ManualTaskCode != "TAR-0092" || parent.BranchId != BranchScope.LorettaId || parent.InputPayload is null)
            throw new GenerationRequestNotFoundException();
        await RequireVisibleAsync(command.ActorUserId, parent.GenerationRequestId, token);
        if (parent.ExecutionStatus != WorkObligationStatuses.Pending)
            throw new ManualGenerationException("ORIGEN_PADRE_NO_DISPONIBLE", 409);
        if (parent.PeriodId != command.PeriodId)
            throw new ManualGenerationException("PERIODO_INVALIDO", 422, new GenerationFieldError("periodId", "MISMATCH"));
        var started = ManualGenerationInput.Date(parent.InputPayload.RootElement.GetProperty("input"), "startedAt");
        if (ManualGenerationInput.Date(input, "occurredAt") < started)
            throw ManualGenerationInput.Invalid("occurredAt", "OUT_OF_RANGE");
        return id;
    }

    private async Task<(DateTimeOffset? DueAt, Guid[] CalendarIds)> DeadlineAsync(JsonElement input, DateTimeOffset now, CancellationToken token)
    {
        var task = ManualGenerationInput.TaskCode(input);
        if (task == "TAR-0007") return (ManualGenerationInput.Date(input, "expiresAt"), []);
        if (task == "TAR-0008") return (ManualGenerationInput.Date(input, "detectedAt").AddMinutes(30), []);
        if (task != "TAR-0011") return (null, []);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(CalendarContract.TimeZone);
        var local = TimeZoneInfo.ConvertTime(ManualGenerationInput.Date(input, "authorizedAt"), zone).DateTime;
        var first = DateOnly.FromDateTime(local).AddDays(1);
        var last = first.AddDays(365);
        var branch = BranchScope.LorettaId;
        var days = await dbContext.CalendarDayVersions
            .FromSqlInterpolated($"SELECT * FROM calendar_day_version WHERE branch_id = {branch} AND local_date >= {first} AND local_date <= {last} AND effective_from <= {now} AND (effective_to IS NULL OR effective_to > {now}) FOR SHARE")
            .AsNoTracking().ToListAsync(token);
        var ids = new List<Guid>();
        var count = 0;
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            var matches = days.Where(d => d.LocalDate == date).ToArray();
            if (matches.Length != 1) throw IncompleteCalendar();
            ids.Add(matches[0].Id);
            if (matches[0].IsWorkingDay) count++;
            if (count != 7) continue;
            var dueLocal = date.ToDateTime(TimeOnly.FromDateTime(local), DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(dueLocal) || zone.IsAmbiguousTime(dueLocal)) throw IncompleteCalendar();
            return (new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(dueLocal, zone)), ids.ToArray());
        }
        throw IncompleteCalendar();
    }
    private static ManualGenerationException IncompleteCalendar() => new("CALENDARIO_GENERACION_INCOMPLETO");
}
