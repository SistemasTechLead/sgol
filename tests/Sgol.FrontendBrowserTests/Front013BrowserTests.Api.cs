using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

public sealed partial class Front013BrowserTests
{
    private static async Task VerifyApiContractAsync(IBrowserContext context, BrowserFixture fixture, IPage page)
    {
        var options = await context.APIRequest.GetAsync(new Uri(fixture.BaseAddress, "/api/v1/generation-requests/options").AbsoluteUri);
        using var optionsJson = JsonDocument.Parse(await options.TextAsync());
        var rule = optionsJson.RootElement.GetProperty("data").EnumerateArray().Single(o => o.GetProperty("taskCode").GetString() == "TAR-0018");
        var year = await page.Locator("#iso-year").InputValueAsync();
        var week = await page.Locator("#iso-week").InputValueAsync();
        var period = await context.APIRequest.GetAsync(new Uri(fixture.BaseAddress, $"/api/v1/weeks/{year}/{week}").AbsoluteUri);
        using var periodJson = JsonDocument.Parse(await period.TextAsync());
        var periodId = periodJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var csrfResponse = await context.APIRequest.GetAsync(new Uri(fixture.BaseAddress, "/api/v1/auth/csrf").AbsoluteUri);
        using var csrfJson = JsonDocument.Parse(await csrfResponse.TextAsync());
        var csrf = csrfJson.RootElement.GetProperty("data").GetProperty("requestToken").GetString()!;
        var key = Guid.CreateVersion7();
        object Body(Guid branch, Guid selectedPeriod, object input) => new
        {
            schemaVersion = 2,
            ruleVersionId = rule.GetProperty("ruleVersionId").GetGuid(),
            branchId = branch,
            periodId = selectedPeriod,
            originType = "MANUAL_REFERENCE_V1",
            inputPayload = input
        };
        async Task<IAPIResponse> SendAsync(object body, Guid intent)
        {
            try
            {
                return await context.APIRequest.PostAsync(new Uri(fixture.BaseAddress, "/api/v1/generation-requests").AbsoluteUri,
                    new() { DataObject = body, Headers = new Dictionary<string, string> { ["X-CSRF-TOKEN"] = csrf, ["Idempotency-Key"] = intent.ToString("D") } });
            }
            catch (PlaywrightException) { throw new InvalidOperationException("Synthetic API request failed; private headers omitted."); }
        }
        var branch = rule.GetProperty("branchId").GetGuid();
        var input = new { taskCode = "TAR-0018", eventReference = "SYN-API-EVENT", zoneReference = "SYN-API-ZONE", planogramReference = "SYN-API-PLAN" };
        Assert.Equal(422, (await SendAsync(Body(branch, periodId, new { taskCode = "TAR-0018", eventReference = "INCOMPLETE" }), Guid.CreateVersion7())).Status);
        Assert.Equal(403, (await SendAsync(Body(Guid.CreateVersion7(), periodId, input), Guid.CreateVersion7())).Status);
        Assert.Equal(422, (await SendAsync(Body(branch, Guid.CreateVersion7(), input), Guid.CreateVersion7())).Status);
        var first = await SendAsync(Body(branch, periodId, input), key);
        Assert.Equal(201, first.Status);
        using var firstJson = JsonDocument.Parse(await first.TextAsync());
        var firstId = firstJson.RootElement.GetProperty("data").GetProperty("generationRequestId").GetGuid();
        Assert.Equal(JsonValueKind.Null, firstJson.RootElement.GetProperty("data").GetProperty("dueAt").ValueKind);
        Assert.False(firstJson.RootElement.GetProperty("data").TryGetProperty("inputPayload", out _));
        var replay = await SendAsync(Body(branch, periodId, input), key);
        Assert.Equal(201, replay.Status);
        using var replayJson = JsonDocument.Parse(await replay.TextAsync());
        Assert.Equal(firstId, replayJson.RootElement.GetProperty("data").GetProperty("generationRequestId").GetGuid());
        Assert.Equal("RECUPERADA", replayJson.RootElement.GetProperty("data").GetProperty("result").GetString());
        Assert.Equal(409, (await SendAsync(Body(branch, periodId,
            new { taskCode = "TAR-0018", eventReference = "SYN-API-DIFFERENT", zoneReference = "SYN-API-ZONE", planogramReference = "SYN-API-PLAN" }), key)).Status);
    }
    private static async Task VerifyDeniedApiAsync(IBrowserContext context, BrowserFixture fixture)
    {
        Assert.Equal(403, (await context.APIRequest.GetAsync(new Uri(fixture.BaseAddress, "/api/v1/generation-requests/options").AbsoluteUri)).Status);
        var csrfResponse = await context.APIRequest.GetAsync(new Uri(fixture.BaseAddress, "/api/v1/auth/csrf").AbsoluteUri);
        using var csrf = JsonDocument.Parse(await csrfResponse.TextAsync());
        IAPIResponse response;
        try
        {
            response = await context.APIRequest.PostAsync(new Uri(fixture.BaseAddress, "/api/v1/generation-requests").AbsoluteUri,
                new()
                {
                    DataObject = new
                    {
                        schemaVersion = 2,
                        ruleVersionId = Guid.CreateVersion7(),
                        branchId = Guid.CreateVersion7(),
                        periodId = Guid.CreateVersion7(),
                        originType = "MANUAL_REFERENCE_V1",
                        inputPayload = new
                        {
                            taskCode = "TAR-0018",
                            eventReference = "SYN-DENIED",
                            zoneReference = "SYN-ZONE",
                            planogramReference = "SYN-PLAN"
                        }
                    },
                    Headers = new Dictionary<string, string>
                    {
                        ["X-CSRF-TOKEN"] = csrf.RootElement.GetProperty("data").GetProperty("requestToken").GetString()!,
                        ["Idempotency-Key"] = Guid.CreateVersion7().ToString("D")
                    }
                });
        }
        catch (PlaywrightException) { throw new InvalidOperationException("Synthetic denied API request failed; private headers omitted."); }
        Assert.Equal(403, response.Status);
    }
}
