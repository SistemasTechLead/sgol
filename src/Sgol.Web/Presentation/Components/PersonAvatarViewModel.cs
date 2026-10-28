namespace Sgol.Web.Presentation.Components;

public sealed record PersonAvatarViewModel(Guid PersonId, string Name, string? UserName = null)
{
    public string Initial => System.Globalization.StringInfo.GetNextTextElement(
        string.IsNullOrWhiteSpace(UserName) ? Name.Trim() : UserName.Trim()).ToUpperInvariant();
}
