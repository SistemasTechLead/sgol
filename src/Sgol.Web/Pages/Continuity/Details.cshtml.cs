using Microsoft.AspNetCore.DataProtection;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Continuity;

public sealed class DetailsModel(IRazorSessionState sessionState, ISgolApiClient apiClient, IDataProtectionProvider protection)
    : IndexModel(sessionState, apiClient, protection);
