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
// Trend charts must be the first dashboard section immediately below the
// dark heading; moving only their XAML keeps the existing WebView2 binding.
var dashboardSections = dashboard.Elements().Single(el => el.Name.LocalName == "StackPanel")
    .Elements().ToArray();
if (dashboardSections.Length < 4 ||
    dashboardSections[0].Name.LocalName != "Border" ||
    dashboardSections[1].Name.LocalName != "TextBlock" ||
    (string?)dashboardSections[1].Attribute("Text") != "Xu hướng nhập xuất và cơ cấu vật tư" ||
    dashboardSections[2].Name.LocalName != "Border" ||
    !dashboardSections[2].Descendants().Any(el => Name(el) == "ChartsWebView"))
    throw new InvalidOperationException("Trends must appear directly after dashboard header, ahead of actions and KPIs.");

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
if (chat.Descendants().Count(el => Name(el) == "ReadWorkbooksCheck") != 1 ||
    !chat.Descendants().Any(el => (string?)el.Attribute("Click") == "CompareBravo_Click"))
    throw new InvalidOperationException("An must have local read permission and a two-file Bravo compare button.");

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

var codes = HisaWarehouse.Services.MaterialCodeFilter.Parse(" vt001,VT002 VT003,,  vt001\tVT004\nVT005 ");
if (codes.Count != 5 || !codes.Contains("VT001") || !codes.Contains("vt005") ||
    !HisaWarehouse.Services.MaterialCodeFilter.Matches(codes, "vt002") ||
    HisaWarehouse.Services.MaterialCodeFilter.Matches(codes, "vt0021"))
    throw new InvalidOperationException("Multi-SKU exact matching, mixed case and delimiters failed.");
if (!HisaWarehouse.Services.MaterialCodeFilter.Matches(
    HisaWarehouse.Services.MaterialCodeFilter.Parse(""), "VT001"))
    throw new InvalidOperationException("Empty multi-SKU filter should show all products.");

foreach (var click in new[] { "QuickReceipt_Click", "QuickShipment_Click" })
    if (dashboardButtons.Count(el => (string?)el.Attribute("Click") == click) != 1)
        throw new InvalidOperationException("Homepage missing independent quick receipt/shipment action: " + click);
var catalog = XDocument.Load(Path.Combine(Directory.GetCurrentDirectory(), "CatalogsWindow.xaml"));
if (!catalog.Descendants().Any(el => el.Name.LocalName == "TabItem" &&
    (string?)el.Attribute("Header") == "Phân loại hàng hóa") ||
    catalog.Descendants().Count(el => Name(el) == "CategoryInput") != 1 ||
    catalog.Descendants().Count(el => Name(el) == "CategoryList") != 1)
    throw new InvalidOperationException("Editable product classification catalog is not available.");
if (!catalog.Descendants().Any(el => el.Name.LocalName == "TabItem" &&
    (string?)el.Attribute("Header") == "Người nhận / gửi"))
    throw new InvalidOperationException("Recipient/sender catalog label is incorrect.");
var movement = XDocument.Load(Path.Combine(Directory.GetCurrentDirectory(), "MovementWindow.xaml"));
if (!movement.Descendants().Any(el => Name(el) == "PartnerLabel" &&
    (string?)el.Attribute("Text") == "NGƯỜI NHẬN / GỬI"))
    throw new InvalidOperationException("Receipt/shipment partner label has not been updated.");
var updaterSource = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(),
    "Services", "WarehouseUpdateService.cs"));
if (!updaterSource.Contains("requiresElevation ? \"runas\" : \"\"", StringComparison.Ordinal))
    throw new InvalidOperationException("One-click updater should request Windows UAC when necessary.");
var checkStart = cs.IndexOf("private async void CheckUpdate_Click", StringComparison.Ordinal);
var installStart = cs.IndexOf("private async Task InstallDownloadedUpdateAsync", StringComparison.Ordinal);
if (checkStart < 0 || installStart <= checkStart)
    throw new InvalidOperationException("One-click updater handler missing.");
var checkBody = cs[checkStart..installStart];
if (checkBody.Contains("ConfirmInApp(", StringComparison.Ordinal) ||
    !checkBody.Contains("await InstallDownloadedUpdateAsync(package)", StringComparison.Ordinal))
    throw new InvalidOperationException("Update button still needs an extra approval click.");
var receiptCode = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "MovementWindow.xaml.cs"));
if (!receiptCode.Contains("TryResolveNewReceiptMaterial", StringComparison.Ordinal) ||
    !receiptCode.Contains("ReferenceMaterialLookup.Read", StringComparison.Ordinal))
    throw new InvalidOperationException("Unknown SKU is not searched in the reference material catalog.");
var bravoView = XDocument.Load(Path.Combine(Directory.GetCurrentDirectory(), "ReferenceInventoryWindow.xaml"));
if (!bravoView.Descendants().Any(el => (string?)el.Attribute("Click") == "ExportBravoComparison_Click"))
    throw new InvalidOperationException("Bravo comparison must offer a complete read-only Excel export.");
var bravoReader = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Services", "BravoStockReader.cs"));
if (!bravoReader.Contains("sheet.Cell(row, 1)", StringComparison.Ordinal) ||
    !bravoReader.Contains("sheet.Cell(row, 2)", StringComparison.Ordinal) ||
    !bravoReader.Contains("sheet.Cell(row, 3)", StringComparison.Ordinal) ||
    !bravoReader.Contains("sheet.Cell(row, 12)", StringComparison.Ordinal))
    throw new InvalidOperationException("Bravo must read exact A/B/C/L columns and signed stock.");
var an = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "AiAssistantWindow.xaml.cs"));
if (!an.Contains("ShareDataCheck.IsChecked != true", StringComparison.Ordinal) ||
    !an.Contains("BravoStockComparison.Read(_state)", StringComparison.Ordinal))
    throw new InvalidOperationException("An must compare local files while requiring separate Gemini data-sharing consent.");
var itemForm = XDocument.Load(Path.Combine(Directory.GetCurrentDirectory(), "ItemEditorWindow.xaml"));
if ((string?)itemForm.Descendants().Single(el => Name(el) == "BarcodeBox").Attribute("IsReadOnly") != "True")
    throw new InvalidOperationException("Barcode field must be locked.");
var editSource = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "ItemEditorWindow.xaml.cs"));
if (!editSource.Contains("SkuBox.IsReadOnly = true;", StringComparison.Ordinal) ||
    !editSource.Contains("GenerateSkuButton.IsEnabled = false;", StringComparison.Ordinal))
    throw new InvalidOperationException("Existing material SKU must be locked.");
var reserveEditor = XDocument.Load(Path.Combine(Directory.GetCurrentDirectory(), "MaterialRequestEditorWindow.xaml"));
foreach (var name in new[] { "ManualEntryCheck", "ManualSkuBox", "ManualNameBox", "ManualUnitBox", "ManualEntryPanel" })
    if (reserveEditor.Descendants().Count(el => Name(el) == name) != 1)
        throw new InvalidOperationException("Manual reserve entry control missing: " + name);
var reserveList = XDocument.Load(Path.Combine(Directory.GetCurrentDirectory(), "MaterialRequestsWindow.xaml"));
if (!reserveList.Descendants().Any(el => (string?)el.Attribute("Click") == "NewReserveRequest_Click"))
    throw new InvalidOperationException("Request list must expose a reserve material request button.");
var navReserve = main.Descendants().Single(el => Name(el) == "NavReplenishment");
if (!((string?)navReserve.Attribute("ToolTip") ?? "").Contains("cấp vật tư dự phòng", StringComparison.Ordinal))
    throw new InvalidOperationException("Sidebar must expose the reserve material request instead of prior entry.");
var requestCode = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "MaterialRequestEditorWindow.xaml.cs"));
if (!requestCode.Contains("new MovementLine { Sku = sku, ItemName = name, Unit = unit, Quantity = quantity }", StringComparison.Ordinal) ||
    !requestCode.Contains("ManualEntryCheck.IsChecked == true", StringComparison.Ordinal))
    throw new InvalidOperationException("Manual lines must preserve name/unit/quantity independently from SKU.");
var requestService = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Services", "MaterialRequestService.cs"));
if (!requestService.Contains("YC-DP", StringComparison.Ordinal))
    throw new InvalidOperationException("Reserve requests must have a distinct document prefix.");
Console.WriteLine("PASS: dashboard, Bravo A/B/C/L, signed balances, An two-file read consent and existing navigation.");
