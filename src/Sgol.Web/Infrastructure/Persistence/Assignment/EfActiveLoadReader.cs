using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Time;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Assignment;

public sealed class EfActiveLoadReader(
    SgolDbContext dbContext,
    IClock clock) : IActiveLoadReader
{
    private static readonly JsonSerializerOptions CursorJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ActiveLoadPage> ListAsync(
        ActiveLoadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Limit is < 1 or > 100)
        {
            throw new ActiveLoadFilterInvalidException();
        }

        var cursor = DecodeCursor(request.Cursor);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        _ = await dbContext.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"")
            .SingleAsync(cancellationToken);
        var calculatedAt = clock.UtcNow;

        var actor = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join employment in dbContext.EmploymentVersions.AsNoTracking()
                on user.PersonId equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking()
                on user.Id equals role.UserId
            where user.Id == request.ActorUserId &&
                user.Status == BootstrapContract.ActiveAccountStatus &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= calculatedAt &&
                (employment.ValidTo == null || calculatedAt < employment.ValidTo) &&
                role.BranchId == BranchScope.LorettaId &&
                role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= calculatedAt &&
                (role.ValidTo == null || calculatedAt < role.ValidTo)
            select new ActorAccess(user.PersonId, role.RoleCode))
            .SingleOrDefaultAsync(cancellationToken);
        if (actor is null || !CanonicalRole.IsDefined(actor.RoleCode))
        {
            throw new ActiveLoadAccessDeniedException();
        }

        var lowerRoleCodes = actor.RoleCode switch
        {
            CanonicalRole.Direction => new[]
            {
                CanonicalRole.Direction,
                CanonicalRole.Administration,
                CanonicalRole.Subcoordination,
                CanonicalRole.SalesFloor,
            },
            CanonicalRole.Administration =>
                [CanonicalRole.Subcoordination, CanonicalRole.SalesFloor],
            CanonicalRole.Subcoordination => [CanonicalRole.SalesFloor],
            CanonicalRole.SalesFloor => [],
            _ => throw new ActiveLoadAccessDeniedException(),
        };
        var visibleQuery =
            from person in dbContext.People.AsNoTracking()
            join employment in dbContext.EmploymentVersions.AsNoTracking()
                on person.Id equals employment.PersonId
            join user in dbContext.AppUsers.AsNoTracking()
                on person.Id equals user.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking()
                on user.Id equals role.UserId
            where user.Status == BootstrapContract.ActiveAccountStatus &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= calculatedAt &&
                (employment.ValidTo == null || calculatedAt < employment.ValidTo) &&
                role.BranchId == BranchScope.LorettaId &&
                role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= calculatedAt &&
                (role.ValidTo == null || calculatedAt < role.ValidTo) &&
                (person.Id == actor.PersonId || lowerRoleCodes.Contains(role.RoleCode)) &&
                (request.PersonId == null || person.Id == request.PersonId)
            select new VisiblePerson(person.Id, person.StableCode, person.DisplayName, role.RoleCode);

        var visiblePeople = await visibleQuery.ToListAsync(cancellationToken);
        var orderedPeople = visiblePeople
            .OrderBy(item => item.StableCode, StringComparer.Ordinal)
            .ThenBy(item => item.Id);
        if (cursor is not null)
        {
            orderedPeople = orderedPeople.Where(item =>
                    string.Compare(item.StableCode, cursor.StableCode, StringComparison.Ordinal) > 0 ||
                    (string.Equals(item.StableCode, cursor.StableCode, StringComparison.Ordinal) &&
                     item.Id.CompareTo(cursor.PersonId) > 0))
                .OrderBy(item => item.StableCode, StringComparer.Ordinal)
                .ThenBy(item => item.Id);
        }

        var page = orderedPeople.Take(request.Limit + 1).ToList();
        var hasNextPage = page.Count > request.Limit;
        if (hasNextPage)
        {
            page.RemoveAt(page.Count - 1);
        }

        var personIds = page.Select(item => item.Id).ToArray();
        var metrics = await EfAssignmentMetricsReader.ReadAsync(
            dbContext,
            personIds,
            calculatedAt,
            cancellationToken);

        var items = page.Select(person => new ActiveLoadItem(
            new ActiveLoadPerson(person.Id, person.StableCode, person.DisplayName),
            metrics.GetValueOrDefault(person.Id)?.ActiveLoad ?? 0,
            calculatedAt)).ToArray();
        var nextCursor = hasNextPage ? EncodeCursor(page[^1]) : null;

        await transaction.CommitAsync(cancellationToken);
        return new ActiveLoadPage(items, nextCursor);
    }

    private static string EncodeCursor(VisiblePerson person)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new ActiveLoadCursor(1, person.StableCode, person.Id),
            CursorJsonOptions);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static ActiveLoadCursor? DecodeCursor(string? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length % 4 == 1)
            {
                throw new FormatException();
            }

            var padded = value.Replace('-', '+').Replace('_', '/');
            padded += (value.Length % 4) switch
            {
                2 => "==",
                3 => "=",
                _ => string.Empty,
            };
            var cursor = JsonSerializer.Deserialize<ActiveLoadCursor>(
                Convert.FromBase64String(padded),
                CursorJsonOptions);
            if (cursor is null || cursor.Version != 1 ||
                string.IsNullOrWhiteSpace(cursor.StableCode) || cursor.PersonId == Guid.Empty)
            {
                throw new FormatException();
            }

            return cursor;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or DecoderFallbackException)
        {
            throw new ActiveLoadFilterInvalidException();
        }
    }

    private sealed record ActorAccess(Guid PersonId, string RoleCode);
    private sealed record VisiblePerson(Guid Id, string StableCode, string DisplayName, string RoleCode);
    private sealed record ActiveLoadCursor(int Version, string StableCode, Guid PersonId);
}
