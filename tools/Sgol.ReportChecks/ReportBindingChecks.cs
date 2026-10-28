using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Web.Infrastructure.Persistence.Reporting;

internal static class ReportBindingChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvc();
        using var provider = services.BuildServiceProvider();
        var metadataProvider = provider.GetRequiredService<IModelMetadataProvider>();
        var metadata = metadataProvider.GetMetadataForType(typeof(ReportFilter));
        var binder = provider.GetRequiredService<IModelBinderFactory>().CreateBinder(new() { Metadata = metadata });

        async Task<(ReportFilter Filter, ModelStateDictionary State)> Bind(string query)
        {
            var http = new DefaultHttpContext { RequestServices = provider };
            http.Request.QueryString = new QueryString(query);
            var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
            var context = DefaultModelBindingContext.CreateBindingContext(action,
                new QueryStringValueProvider(BindingSource.Query, http.Request.Query, CultureInfo.InvariantCulture),
                metadata, null, "F");
            await binder.BindModelAsync(context);
            var filter = (ReportFilter)context.Result.Model!;
            provider.GetRequiredService<IObjectModelValidator>().Validate(action, null, "F", filter);
            return (filter, action.ModelState);
        }

        foreach (var definition in ReportCatalog.Definitions)
        {
            var result = await Bind($"?F.Kind={definition.Id}&F.Range=week&F.Carryover=&F.Person=&F.From=&F.Minutes=");
            check(result.State.IsValid, "Report query accepts empty optional filters: " + definition.Id + " " +
                string.Join(", ", result.State.Where(e => e.Value!.Errors.Count > 0).Select(e => e.Key)));
            check(result.Filter.Carryover == "", "All carryovers binds as empty string: " + definition.Id);
            ReportAnalysis.Range(result.Filter, new DateOnly(2026, 10, 23));
        }
        foreach (var value in new[] { "approved", "covered", "pending", "late" })
        {
            var result = await Bind("?F.Carryover=" + value);
            check(result.State.IsValid && result.Filter.Carryover == value, "Carryover selection binds: " + value);
        }
        foreach (var query in new[] { "?F.Minutes=invalid", "?F.From=invalid" })
            check(!(await Bind(query)).State.IsValid, "Malformed report value remains invalid: " + query);
    }
}
