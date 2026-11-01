using System.Xml.Linq;

var mainFile = Path.Combine(Directory.GetCurrentDirectory(), "MainWindow.xaml");
var chatFile = Path.Combine(Directory.GetCurrentDirectory(), "AiAssistantWindow.xaml");
var main = XDocument.Load(mainFile);
var chat = XDocument.Load(chatFile);
XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
string? Name(XElement e) => (string?)e.Attribute(x + "Name");
var required = new[] {
    "NavDashboard", "NavAi", "NavInventory", "NavMovement",
    "NavPending", "NavReplenishment", "NavReports", "NavAudit",
    "NavPrintTemplates", "NavCatalogs", "NavSettings"
};
var sidebar = main.Descendants().Where(el => el.Name.LocalName == "Button")
    .Where(el => Name(el)?.StartsWith("Nav", StringComparison.Ordinal) == true).ToList();
if (sidebar.Count != required.Length ||
    !sidebar.Select(Name).ToHashSet().SetEquals(required))
    throw new InvalidOperationException("Flat sidebar must show 11 core functions exactly once.");

foreach (var button in sidebar)
{
    if (button.Ancestors().Any(el => el.Name.LocalName == "Expander"))
        throw new InvalidOperationException("Flat menu item has a nested Expander: " + Name(button));
    if ((string?)button.Attribute("Visibility") == "Collapsed")
        throw new InvalidOperationException("Main function is hidden: " + Name(button));
    if (string.IsNullOrWhiteSpace((string?)button.Attribute("Click")))
        throw new InvalidOperationException("Flat menu item has no navigation action: " + Name(button));
}
if (main.Descendants().Any(el => Name(el)?.StartsWith("NavGroup", StringComparison.Ordinal) == true))
    throw new InvalidOperationException("Obsolete grouped sidebar remains in layout.");

var dashboard = main.Descendants().Single(el => Name(el) == "DashboardPage");
if (dashboard.Name.LocalName != "ScrollViewer")
    throw new InvalidOperationException("Dashboard should be scrollable on compact screens.");
var dashboardButtons = dashboard.Descendants().Where(el => el.Name.LocalName == "Button").ToList();
var mainActions = new HashSet<string> {
    "NavInventory_Click", "NavMovement_Click", "NavPending_Click",
    "NavReplenishment_Click", "NavReports_Click", "NavAudit_Click",
    "NavPrintTemplates_Click", "NavSettings_Click"
};
foreach (var action in mainActions)
    if (!dashboardButtons.Any(el => (string?)el.Attribute("Click") == action))
        throw new InvalidOperationException("Dashboard is missing direct function shortcut: " + action);

var updater = dashboardButtons.Single(el => Name(el) == "CheckUpdateButton");
if ((string?)updater.Attribute("Click") != "CheckUpdate_Click")
    throw new InvalidOperationException("Homepage update does not invoke secure SHA-256 update workflow.");
if (main.Descendants().Count(el => Name(el) == "CheckUpdateButton") != 1)
    throw new InvalidOperationException("Updater button must not be duplicated in settings.");
if (!dashboard.Descendants().Any(el => Name(el) == "UpdateStatusText"))
    throw new InvalidOperationException("Homepage must show update progress and errors.");
if (!dashboardButtons.Any(el => (string?)el.Attribute("Click") == "HomeHandsFree_Click"))
    throw new InvalidOperationException("Homepage has no hands-free assistant shortcut.");
if (!dashboardButtons.Any(el => (string?)el.Attribute("Click") == "NavAi_Click"))
    throw new InvalidOperationException("Homepage has no normal chat shortcut.");
foreach (var id in new[] { "DashboardDateText", "KpiProductCount", "KpiLowStock",
    "KpiOutOfStock", "KpiOutOfStockCaption", "KpiToday", "KpiTodayCaption",
    "ChartsWebView", "LowStockGrid", "DataQualitySummary", "DataQualityDetail" })
    if (main.Descendants().Count(el => Name(el) == id) != 1)
        throw new InvalidOperationException("Existing dashboard binding missing or duplicated: " + id);

if (chat.Descendants().Any(el => el.Name.LocalName == "Button" &&
    ((string?)el.Attribute("Content") == "Đóng trang" ||
    (string?)el.Attribute("Click") == "Close_Click")))
    throw new InvalidOperationException("An chat should not show a redundant Close page button.");
foreach (var id in new[] { "HandsFreeButton", "InputMicrophoneBox", "MicrophoneTestButton" })
    if (chat.Descendants().Count(el => Name(el) == id) != 1)
        throw new InvalidOperationException("An microphone controls missing: " + id);

var cs = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "MainWindow.xaml.cs"));
if (!cs.Contains("assistant.BeginHandsFreeFromHome()", StringComparison.Ordinal) ||
    !cs.Contains("private void HomeHandsFree_Click", StringComparison.Ordinal))
    throw new InvalidOperationException("Homepage cannot invoke consented An hands-free.");
var handsFree = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(),
    "AiAssistantWindow.HandsFree.cs"));
if (!handsFree.Contains("HandsFree_Click(this, new RoutedEventArgs())", StringComparison.Ordinal))
    throw new InvalidOperationException("Homepage hands-free bypasses the microphone consent workflow.");
if (cs.Contains("NavGroup_Expanded", StringComparison.Ordinal) ||
    cs.Contains("SidebarModuleGroups.ModuleFor", StringComparison.Ordinal))
    throw new InvalidOperationException("Obsolete accordion menu behavior remains.");

var navOrder = sidebar.Select(Name).ToArray();
if (navOrder[0] != "NavDashboard" || navOrder[1] != "NavAi")
    throw new InvalidOperationException("An must be the second page after the homepage.");
foreach (var retired in new[] { "NavQuickSearch", "NavNegativeStock",
    "NavRequests", "NavLabels", "NavSoftwareInfo" })
    if (main.Descendants().Any(el => Name(el) == retired))
        throw new InvalidOperationException("Retired standalone navigation still exists: " + retired);

var inventory = main.Descendants().Single(el => Name(el) == "InventoryPage");
var inlineLabels = inventory.Descendants().Single(el => Name(el) == "LabelsPage");
if ((string?)inlineLabels.Attribute("Visibility") != "Collapsed" ||
    main.Descendants().Count(el => Name(el) == "LabelsPage") != 1)
    throw new InvalidOperationException("Label print/preview must be embedded in the inventory.");
if (!inventory.Descendants().Any(el => (string?)el.Attribute("Click") == "ToggleInventoryLabels_Click"))
    throw new InvalidOperationException("Inventory cannot reveal barcode print/preview.");
var inventoryTabs = main.Descendants().Single(el => Name(el) == "InventoryWorkspaceTabs");
if (inventoryTabs.Elements().Count(el => el.Name.LocalName == "TabItem") != 1)
    throw new InvalidOperationException("Separate barcode page still exists.");

var reports = XDocument.Load(Path.Combine(Directory.GetCurrentDirectory(), "DetailedReportsWindow.xaml"));
if (reports.Descendants().Count(el => Name(el) == "SkuFilterBox") != 1)
    throw new InvalidOperationException("Detailed reports have no multi-SKU input.");

var codes = HisaWarehouse.Services.MaterialCodeFilter.Parse(" vt001,VT002 VT003,,  vt001\\tVT004\\nVT005 ");
if (codes.Count != 5 || !codes.Contains("VT001") || !codes.Contains("vt005") ||
    !HisaWarehouse.Services.MaterialCodeFilter.Matches(codes, "vt002") ||
    HisaWarehouse.Services.MaterialCodeFilter.Matches(codes, "vt0021"))
    throw new InvalidOperationException("Multi-SKU exact matching, mixed case and delimiters failed.");
if (!HisaWarehouse.Services.MaterialCodeFilter.Matches(
    HisaWarehouse.Services.MaterialCodeFilter.Parse(""), "VT001"))
    throw new InvalidOperationException("Empty multi-SKU filter should show all products.");

Console.WriteLine("PASS: core sidebar, An second, unchanged dashboard shell, 8 shortcuts, inline labels and exact multi-SKU filters.");
