using System.Text.Json;

namespace HisaWarehouse.Services;

/// <summary>
/// A page reports whether form data has actually changed since it was opened.
/// Merely opening or viewing the editor must never trigger a discard warning.
/// </summary>
public interface IWorkspaceDirtyPage
{
    bool HasUnsavedChanges { get; }
}

/// <summary>
/// Immutable initial snapshot of user-editable form fields and their line items.
/// Capture only business input; never include layout, focus or read-only status.
/// </summary>
public sealed class WorkspaceFormSnapshot
{
    private readonly Func<string> _capture;
    private readonly string _initial;

    public WorkspaceFormSnapshot(Func<string> capture)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _initial = _capture();
    }

    public bool HasChanges => !string.Equals(_initial, _capture(), StringComparison.Ordinal);

    public static string Serialize(params object?[] values) => JsonSerializer.Serialize(values);
}
