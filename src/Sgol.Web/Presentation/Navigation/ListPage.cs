using Microsoft.AspNetCore.WebUtilities;
namespace Sgol.Web.Presentation.Navigation;
public sealed record ListPage(string Key, int Number, int Total, int Size, HttpRequest Request, string? Anchor = null)
{
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)Size));
    public string Href(int page) => QueryHelpers.AddQueryString(Request.Path,
        Request.Query.Where(q => q.Key != Key && q.Key != "handler").ToDictionary(q => q.Key, q => (string?)q.Value.ToString())
            .Append(new KeyValuePair<string, string?>(Key, Math.Clamp(page, 1, Pages).ToString())).ToDictionary()) + (Anchor == null ? "" : "#" + Anchor);
    public static int Read(HttpRequest request, string key) => int.TryParse(request.Query[key], out var page) ? Math.Clamp(page, 1, 1000000) : 1;
}
