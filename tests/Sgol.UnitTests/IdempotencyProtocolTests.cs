using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Sgol.Web.Infrastructure.Http;
using Sgol.Web.Infrastructure.Persistence.Idempotency;
using Xunit;

namespace Sgol.UnitTests;

public sealed class IdempotencyProtocolTests
{
    [Theory]
    [InlineData(null, "IDEMPOTENCY_KEY_REQUERIDA")]
    [InlineData("", "IDEMPOTENCY_KEY_INVALIDA")]
    [InlineData("not-a-uuid", "IDEMPOTENCY_KEY_INVALIDA")]
    [InlineData("00000000-0000-0000-0000-000000000000", "IDEMPOTENCY_KEY_INVALIDA")]
    [InlineData(" 019d3a10-0100-7000-8000-000000000001", "IDEMPOTENCY_KEY_INVALIDA")]
    [InlineData("{019d3a10-0100-7000-8000-000000000001}", "IDEMPOTENCY_KEY_INVALIDA")]
    public void HeaderRejectsMissingEmptyAndNonCanonicalValues(string? value, string code)
    {
        var context = new DefaultHttpContext();
        if (value is not null)
        {
            context.Request.Headers["Idempotency-Key"] = value;
        }

        var result = IdempotencyKeyHeader.Parse(context.Request);

        Assert.False(result.IsValid);
        Assert.Equal(code, result.ErrorCode);
    }

    [Fact]
    public void HeaderAcceptsCanonicalUuidCaseInsensitivelyButRejectsMultipleValues()
    {
        var context = new DefaultHttpContext();
        var key = Guid.Parse("A19D3A10-0100-7000-8000-00000000000A");
        context.Request.Headers["idempotency-key"] = key.ToString("D").ToUpperInvariant();

        var accepted = IdempotencyKeyHeader.Parse(context.Request);

        Assert.True(accepted.IsValid);
        Assert.Equal(key, accepted.Key);

        context.Request.Headers["Idempotency-Key"] = new StringValues(
            [key.ToString("D"), Guid.CreateVersion7().ToString("D")]);
        Assert.Equal("IDEMPOTENCY_KEY_INVALIDA", IdempotencyKeyHeader.Parse(context.Request).ErrorCode);
    }

    [Fact]
    public void CanonicalHashIgnoresPropertyOrderAndNormalizesUnicode()
    {
        var first = IdempotencyProtocol.HashCanonical(
            "TEST_OPERATION",
            "actor",
            "resource",
            new { z = "é", a = 7 });
        var second = IdempotencyProtocol.HashCanonical(
            "TEST_OPERATION",
            "actor",
            "resource",
            new { a = 7, z = "e\u0301" });

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Fact]
    public void CanonicalHashIncludesIfMatchAndSemanticValues()
    {
        var baseline = IdempotencyProtocol.HashCanonical("OP", "actor", "resource", new { value = 1 }, 1);

        Assert.NotEqual(baseline, IdempotencyProtocol.HashCanonical("OP", "actor", "resource", new { value = 1 }, 2));
        Assert.NotEqual(baseline, IdempotencyProtocol.HashCanonical("OP", "actor", "resource", new { value = 2 }, 1));
        Assert.NotEqual(baseline, IdempotencyProtocol.HashCanonical("OTHER", "actor", "resource", new { value = 1 }, 1));
    }

    [Fact]
    public void ScopeSeparatesActorOperationAndResource()
    {
        var scope = IdempotencyProtocol.Scope("actor", "operation", "resource");

        Assert.Equal("idem:v1|api:v1|actor:actor|operation:operation|resource:resource", scope);
        Assert.NotEqual(scope, IdempotencyProtocol.Scope("other", "operation", "resource"));
        Assert.NotEqual(scope, IdempotencyProtocol.Scope("actor", "other", "resource"));
        Assert.NotEqual(scope, IdempotencyProtocol.Scope("actor", "operation", "other"));
    }

    [Theory]
    [InlineData(true, "USER")]
    [InlineData(false, "SYSTEM")]
    public void ConflictAuditUsesTheExactMinimizedContract(bool hasActor, string expectedActorType)
    {
        var actor = hasActor ? Guid.CreateVersion7() : (Guid?)null;
        var audit = IdempotencyProtocol.ConflictAudit(
            Guid.CreateVersion7(),
            new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero),
            actor,
            "WORK_OBLIGATION",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "OBLIGATION_CONCLUDE");

        Assert.Equal("IDEMPOTENCY_CONFLICT_REJECTED", audit.Action);
        Assert.Equal(expectedActorType, audit.ActorType);
        Assert.Equal("REJECTED", audit.Outcome);
        var properties = audit.AfterData!.RootElement.EnumerateObject().ToArray();
        Assert.Equal(["operation", "protocolVersion", "reasonCode"], properties.Select(item => item.Name));
        Assert.Equal("OBLIGATION_CONCLUDE", properties[0].Value.GetString());
        Assert.Equal(1, properties[1].Value.GetInt16());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_CONTENT", properties[2].Value.GetString());
    }
}
