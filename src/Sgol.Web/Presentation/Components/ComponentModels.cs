namespace Sgol.Web.Presentation.Components;

public sealed record SelectOption(string Value, string Label, bool IsSelected = false);

public enum CredentialKind
{
    Password,
    OneTimeCode,
    RecoveryCode,
}

public sealed record CredentialFieldViewModel(
    string Id,
    string Label,
    CredentialKind Kind,
    string? ErrorMessage = null,
    bool IsDisabled = false,
    bool CanReveal = false);

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

public sealed record TextAreaViewModel(
    string Id,
    string Label,
    string? Value = null,
    string? ErrorMessage = null,
    bool IsDisabled = false,
    bool IsRequired = false);

public sealed record RadioOption(string Value, string Label, bool IsSelected = false, bool IsDisabled = false);

public sealed record RadioGroupViewModel(
    string Id,
    string Legend,
    IReadOnlyList<RadioOption> Options,
    string? ErrorMessage = null,
    bool IsDisabled = false);

public enum LocalDateFieldKind
{
    Date,
    DateTime,
}

public sealed record LocalDateFieldViewModel(
    string Id,
    string Label,
    LocalDateFieldKind Kind,
    string TimeZoneLabel = "America/Mexico_City",
    string? Value = null,
    string? ErrorMessage = null,
    bool IsDisabled = false);

public sealed record ValidationSummaryViewModel(
    string Title,
    IReadOnlyList<string> Errors);

public sealed record SuccessAlertViewModel(
    string Title,
    string Message);

public sealed record MotivatedConfirmationViewModel(
    string DialogId,
    string Title,
    string ResourceSummary,
    string ReasonId,
    string ReasonLabel,
    string ConfirmLabel,
    string? ErrorMessage = null,
    bool IsDestructive = false,
    bool IsBusy = false);

public enum UploadPresentationState
{
    Ready,
    Uploading,
    PendingScan,
    Clean,
    Infected,
    Invalid,
    ScanError,
}

public sealed record UploadPresentationViewModel(
    string Id,
    string Label,
    UploadPresentationState State,
    string? FileName = null,
    int? ProgressPercent = null,
    string? Message = null,
    bool IsDisabled = false);

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
    EmptyStateViewModel? EmptyState = null,
    CursorPaginationViewModel? Pagination = null,
    string? FilterAction = null,
    IReadOnlyList<TableFilterViewModel>? Filters = null,
    string? ClearFiltersHref = null,
    IReadOnlyList<IReadOnlyList<DataTableActionViewModel>>? RowActions = null);

public sealed record TableFilterViewModel(
    string Id,
    string Label,
    string? Value = null,
    IReadOnlyList<SelectOption>? Options = null);

public sealed record DataTableActionViewModel(
    string Label,
    string Href);

public sealed record CursorPaginationViewModel(
    string? PreviousHref,
    string? NextHref);
