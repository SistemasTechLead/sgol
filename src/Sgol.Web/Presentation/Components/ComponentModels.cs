namespace Sgol.Web.Presentation.Components;

public sealed record SelectOption(string Value, string Label, bool IsSelected = false);

public sealed record FormFieldViewModel(
    string Id,
    string Label,
    string? Value = null,
    IReadOnlyList<SelectOption>? Options = null,
    string? ErrorMessage = null,
    bool IsDisabled = false,
    bool IsLoading = false);

public sealed record CheckboxViewModel(
    string Id,
    string Label,
    bool IsChecked = false,
    bool IsDisabled = false,
    string? ErrorMessage = null);

public sealed record EmptyStateViewModel(
    string Title,
    string? Message = null,
    string? ActionLabel = null,
    string? ActionHref = null);

public sealed record DataTableViewModel(
    string Caption,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    bool IsLoading = false,
    EmptyStateViewModel? EmptyState = null);
