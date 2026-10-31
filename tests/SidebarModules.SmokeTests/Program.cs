using System.Xml.Linq;
using HisaWarehouse.Services;

var file = Path.Combine(Directory.GetCurrentDirectory(), "MainWindow.xaml");
var document = XDocument.Load(file);
XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
var buttons = document.Descendants().Where(el => el.Name.LocalName == "Button").ToList();
var groups = document.Descendants().Where(el => el.Name.LocalName == "Expander")
    .Where(el => ((string?)el.Attribute(x + "Name"))?.StartsWith("NavGroup", StringComparison.Ordinal) == true)
    .ToList();
if (groups.Count != 5) throw new InvalidOperationException($"Expected 5 groups, actual {groups.Count}.");

foreach (var (nav, group) in SidebarModuleGroups.Membership)
{
    var matches = buttons.Where(btn => (string?)btn.Attribute(x + "Name") == nav).ToList();
    if (matches.Count != 1)
        throw new InvalidOperationException($"Navigation '{nav}' occurs {matches.Count} times.");
    var ancestor = matches[0].Ancestors().FirstOrDefault(el => el.Name.LocalName == "Expander");
    if ((string?)ancestor?.Attribute(x + "Name") != group)
        throw new InvalidOperationException($"Navigation '{nav}' is in the wrong group.");
}
var hidden = new HashSet<string>(StringComparer.Ordinal) {
    "NavQuickSearch", "NavNegativeStock", "NavLabels", "NavCatalogs", "NavAudit", "NavSoftwareInfo"
};
foreach (var nav in hidden)
{
    var button = buttons.Single(item => (string?)item.Attribute(x + "Name") == nav);
    if ((string?)button.Attribute("Visibility") != "Collapsed")
        throw new InvalidOperationException($"Duplicate menu '{nav}' is still visible.");
}
foreach (var nav in SidebarModuleGroups.Membership.Keys.Except(hidden))
{
    var button = buttons.Single(item => (string?)item.Attribute(x + "Name") == nav);
    if ((string?)button.Attribute("Visibility") == "Collapsed")
        throw new InvalidOperationException($"Main menu '{nav}' disappeared.");
}

var inventoryTabs = document.Descendants().Single(el => (string?)el.Attribute(x + "Name") == "InventoryWorkspaceTabs");
var reportTabs = document.Descendants().Single(el => (string?)el.Attribute(x + "Name") == "AuditReportTabs");
if (inventoryTabs.Elements().Count(el => el.Name.LocalName == "TabItem") != 2 ||
    reportTabs.Elements().Count(el => el.Name.LocalName == "TabItem") != 2)
    throw new InvalidOperationException("Unified inventory/report tab layout not found.");
var dashboard = buttons.Single(btn => (string?)btn.Attribute(x + "Name") == "NavDashboard");
if (dashboard.Ancestors().Any(el => el.Name.LocalName == "Expander"))
    throw new InvalidOperationException("Dashboard shortcut should stay outside all groups.");

if (SidebarModuleGroups.ModuleFor("NavDashboard") is not null ||
    SidebarModuleGroups.ModuleFor("Unknown") is not null)
    throw new InvalidOperationException("Unknown/root navigation cannot be assigned to a group.");

var expected = new HashSet<string>(SidebarModuleGroups.Membership.Keys, StringComparer.Ordinal)
    { "NavDashboard" };
var actual = buttons.Select(el => (string?)el.Attribute(x + "Name"))
    .Where(name => name?.StartsWith("Nav", StringComparison.Ordinal) == true).ToHashSet();
if (!actual.SetEquals(expected))
    throw new InvalidOperationException("A navigation item was lost or duplicated in the grouping.");
if (groups.Count(g => (string?)g.Attribute("IsExpanded") == "True") != 1)
    throw new InvalidOperationException("Exactly one module must be expanded on first launch.");
Console.WriteLine($"PASS: grouped navigation hides 6 duplicate pages and combines inventory/reports into tabs.");
