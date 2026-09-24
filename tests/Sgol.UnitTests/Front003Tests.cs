using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Pages.People;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Navigation;
using Xunit;

namespace Sgol.UnitTests;

public sealed class Front003Tests
{
    [Fact]
    public async Task EmptyPeopleCollection_ShowsAuthorizedEmptyStateWithoutAnError()
    {
        var model = new IndexModel(new DirectionSession(), new EmptyPeopleClient(), null!)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
        };

        Assert.IsType<PageResult>(await model.OnGetAsync(CancellationToken.None));
        Assert.True(model.CanShowPeople);
        Assert.Empty(model.People);
        Assert.Null(model.Error);
        Assert.False(model.IsDenied);
    }

    private sealed class DirectionSession : IRazorSessionState
    {
        public bool IsInvalid => false;
        public void Invalidate() { }
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<SessionSnapshot?>(new(Guid.CreateVersion7(), Guid.CreateVersion7(),
                "direction.synthetic", "Dirección sintética", "LOR-001", "DIRECCION",
                ["PER-PERSONA-ADMIN"], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10),
                DateTimeOffset.UtcNow.AddHours(1)));
    }

    private sealed class EmptyPeopleClient : ISgolApiClient
    {
        public Task<ApiResponse<T>> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/v1/people", request.Path);
            return Task.FromResult((ApiResponse<T>)(object)new ApiResponse<PersonSummary>(200,
                null, [], Guid.CreateVersion7().ToString("D"), null, 0, null, false, null, null));
        }
    }
}
