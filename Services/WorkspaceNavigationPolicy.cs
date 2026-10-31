namespace HisaWarehouse.Services;

/// <summary>
/// Protects the one-visible-workspace rule. Opening a different root page is
/// blocked until the current page closes. Only an explicit request editor
/// from the request list can replace its parent temporarily.
/// </summary>
public static class WorkspaceNavigationPolicy
{
    public static bool CanOpenRootPage(bool hasActivePage) => !hasActivePage;

    public static bool CanOpenRequestEditor(bool parentIsRequestList, int parentStackDepth) =>
        parentIsRequestList && parentStackDepth == 0;
}
