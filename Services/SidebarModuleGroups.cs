namespace HisaWarehouse.Services;

/// <summary>
/// Related warehouse functions appear under a small number of sidebar modules.
/// No function is deleted or routed to multiple modules.
/// </summary>
public static class SidebarModuleGroups
{
    public const string Stock = "NavGroupStock";
    public const string Documents = "NavGroupDocuments";
    public const string Proposals = "NavGroupProposals";
    public const string Reports = "NavGroupReports";
    public const string System = "NavGroupSystem";

    private static readonly Dictionary<string, string> Groups = new(StringComparer.Ordinal)
    {
        ["NavInventory"] = Stock,
        ["NavQuickSearch"] = Stock,
        ["NavNegativeStock"] = Stock,
        ["NavLabels"] = Stock,
        ["NavCatalogs"] = Stock,

        ["NavMovement"] = Documents,
        ["NavPending"] = Documents,
        ["NavRequests"] = Documents,
        ["NavReplenishment"] = Proposals,
        ["NavPrintTemplates"] = Proposals,

        ["NavAudit"] = Reports,
        ["NavReports"] = Reports,
        ["NavAi"] = Reports,

        ["NavSettings"] = System,
        ["NavSoftwareInfo"] = System
    };

    public static IReadOnlyDictionary<string, string> Membership => Groups;

    public static string? ModuleFor(string? navButtonName) =>
        navButtonName is not null && Groups.TryGetValue(navButtonName, out var module)
            ? module : null;
}
