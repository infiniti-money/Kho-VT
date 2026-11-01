using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public sealed class InventoryState
{
    public int AppVersion { get; set; } = 3;
    public string SourceFile { get; set; } = "KHO.xlsx";
    public string SourceLastModified { get; set; } = "";
    public string SourceImportedOn { get; set; } = "";
    public List<string> Warehouses { get; set; } = [];
    public List<string> Categories { get; set; } = [];
    public List<string> Units { get; set; } = [];
    public List<string> Departments { get; set; } = [];
    public List<string> Recipients { get; set; } = [];
    public List<string> SelectedWarehouses { get; set; } = [];
    public ObservableCollection<InventoryItem> Products { get; set; } = [];
    public ObservableCollection<InventoryTransaction> Transactions { get; set; } = [];
    public ObservableCollection<PendingMovement> PendingMovements { get; set; } = [];
    public ObservableCollection<MaterialRequest> MaterialRequests { get; set; } = [];
    public List<PrintTemplate> PrintTemplates { get; set; } = [];
    public bool PrintTemplatesConfigured { get; set; }
    public string PrimaryExcelPath { get; set; } = "";
    public string PrimarySyncFingerprint { get; set; } = "";
    public DateTime PrimaryExcelSyncedWriteUtc { get; set; } = DateTime.MinValue;
    public string ReferencePath { get; set; } = "";
    public SourceAudit SourceAudit { get; set; } = new();
}

public sealed class InventoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Sku { get; set; } = "";
    public string Barcode { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Vật tư khác";
    public string Unit { get; set; } = "Cái";
    public string Department { get; set; } = "";
    public string? ImageDataUrl { get; set; }
    public double MinStock { get; set; }
    public bool MinStockDefined { get; set; }
    public Dictionary<string, double> WarehouseStocks { get; set; } = new(StringComparer.Ordinal);
    public bool Active { get; set; } = true;
    public bool Legacy { get; set; }
    public int? SourceRow { get; set; }
    public double? SourceCurrentStock { get; set; }
    public double? SourceOpeningStock { get; set; }
    public string? SourceStatus { get; set; }

    public double StockAt(IEnumerable<string> warehouses) => warehouses.Sum(name => WarehouseStocks.TryGetValue(name, out var value) ? value : 0);
    public double TotalStock => StockAt(WarehouseStocks.Keys);
}

public sealed class InventoryTransaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DocNo { get; set; } = "";
    public string ReferenceNo { get; set; } = "";
    public DateOnly? Date { get; set; }
    public string Type { get; set; } = "";
    public string Sku { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Unit { get; set; } = "";
    public double? Quantity { get; set; }
    public string Warehouse { get; set; } = "";
    public string? FromWarehouse { get; set; }
    public string? ToWarehouse { get; set; }
    public string Partner { get; set; } = "";
    public string Operator { get; set; } = "";
    public string Note { get; set; } = "";
    public string SourceAlert { get; set; } = "";
    public double? SourceBefore { get; set; }
    public double? SourceAfter { get; set; }
    public int? SourceRow { get; set; }
    public bool Legacy { get; set; }
    public bool AffectsStock { get; set; }
    public string? QualityFlag { get; set; }
}

public sealed class SourceAudit
{
    public int CatalogCount { get; set; }
    public int TypedTransactionCount { get; set; }
    public int HistoryRowsWithMissingQuantity { get; set; }
    public int HistoryRowsWithMissingType { get; set; }
    public List<StockMismatch> SnapshotLedgerMismatches { get; set; } = [];
    public int ExternalWorkbookLinks { get; set; }
    public int DashboardFormulaErrors { get; set; }
    public int FormulaCount { get; set; }
    public bool LegacyTransactionsDoNotAffectSnapshot { get; set; }
}

public sealed class StockMismatch
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public double Snapshot { get; set; }
    public double LedgerCalculated { get; set; }
    public double Difference { get; set; }
}

public sealed class InventoryRow
{
    public InventoryItem Item { get; init; } = new();
    public string Sku => Item.Sku;
    public string Name => Item.Name;
    public string Category => Item.Category;
    public string Unit => Item.Unit;
    public string Department => Item.Department;
    public string? ImageDataUrl => Item.ImageDataUrl;
    public BitmapImage? Photo => ImageDataService.Decode(Item.ImageDataUrl);
    public double Stock { get; init; }
    public string StockText => FormatQuantity(Stock);
    public string TotalStockText => StockText;
    public double MinStock => Item.MinStock;
    public string Status => Stock < 0 ? "Âm kho" : Stock == 0 ? "Hết hàng" : Item.MinStockDefined && Item.MinStock > 0 && Stock < Item.MinStock ? "Tồn thấp" : "Đủ hàng";
    public bool SelectedForLabel { get; set; }
    public static string FormatQuantity(double value) => value.ToString("#,##0.##", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
}

public sealed class MovementLine
{
    public string Sku { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string Unit { get; set; } = "";
    public double Quantity { get; set; } = 1;
}

public sealed class MovementDraft
{
    public string Type { get; set; } = "Nhập";
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Warehouse { get; set; } = "";
    public string ToWarehouse { get; set; } = "";
    public string ReferenceNo { get; set; } = "";
    public string Partner { get; set; } = "";
    public string Operator { get; set; } = "";
    public string Note { get; set; } = "";
    public bool Pending { get; set; }
    public List<MovementLine> Lines { get; set; } = [];
}

public sealed class PendingMovement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DocNo { get; set; } = "";
    public string ReferenceNo { get; set; } = "";
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Type { get; set; } = "Nhập";
    public string Warehouse { get; set; } = "";
    public string ToWarehouse { get; set; } = "";
    public string Partner { get; set; } = "";
    public string Operator { get; set; } = "";
    public string Note { get; set; } = "";
    public string Status { get; set; } = "Chờ xác nhận";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<MovementLine> Lines { get; set; } = [];
}

public sealed class MaterialRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DocNo { get; set; } = "";
    public string Type { get; set; } = "Yêu cầu cấp vật tư";
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? NeededBy { get; set; }
    public string Department { get; set; } = "";
    public string Warehouse { get; set; } = "";
    public string Requester { get; set; } = "";
    public string Recipient { get; set; } = "";
    public string Note { get; set; } = "";
    public string Status { get; set; } = "Chờ xử lý";
    public List<MovementLine> Lines { get; set; } = [];
}

public sealed class PrintTemplate
{
    public string Type { get; set; } = "Nhập";
    public string Organization { get; set; } = "CÔNG TY TNHH THANG MÁY VÀ KỸ THUẬT ĐIỆN HISA";
    public string Title { get; set; } = "PHIẾU NHẬP KHO";
    public string HeaderNote { get; set; } = "KHO HISA HƯNG YÊN";
    public string FooterNote { get; set; } = "";
    public string Signer1 { get; set; } = "NGƯỜI GIAO";
    public string Signer2 { get; set; } = "NGƯỜI NHẬN";
    public string Signer3 { get; set; } = "THỦ KHO";
    public string FontName { get; set; } = "Arial";
    public double FontSize { get; set; } = 10;
    public double MarginMm { get; set; } = 15;
    public double CellPadding { get; set; } = 5;
    public string HeaderAlignment { get; set; } = "Center";
}

public sealed class ChartPayload
{
    public List<string> CategoryLabels { get; set; } = [];
    public List<int> CategoryCounts { get; set; } = [];
    public List<string> TrendLabels { get; set; } = [];
    public List<double> Receipts { get; set; } = [];
    public List<double> Shipments { get; set; } = [];
    public List<double> Transfers { get; set; } = [];
    public List<double> Adjustments { get; set; } = [];
    public string TrendUnit { get; set; } = "Số dòng giao dịch";
    public List<TrendUnitOption> AvailableUnits { get; set; } = [];
    public string SelectedTrendUnit { get; set; } = "*";
    public int PeriodDays { get; set; } = 180;
}

public sealed class TrendUnitOption
{
    public string Value { get; set; } = "*";
    public string Label { get; set; } = "Tất cả đơn vị";
}

public sealed class AuditRow
{
    public InventoryTransaction Transaction { get; init; } = new();
    public string DateText => Transaction.Date?.ToString("dd/MM/yyyy") ?? "—";
    public string DocNo => Transaction.DocNo;
    public string Type => Transaction.Type;
    public string Sku => Transaction.Sku;
    public string ItemName => Transaction.ItemName;
    public string QuantityText => Transaction.Quantity is double q ? $"{(Transaction.Type == "Điều chỉnh" && q > 0 ? "+" : "")}{InventoryRow.FormatQuantity(q)} {Transaction.Unit}" : "—";
    public string Operator => Transaction.Operator;
    public string LocationText => Transaction.Type == "Điều chuyển"
        ? $"{Transaction.FromWarehouse} → {Transaction.ToWarehouse}"
        : Transaction.Type == "Nhập"
            ? $"{Transaction.ToWarehouse ?? Transaction.Warehouse} · {Transaction.Partner}".Trim(' ', '·')
            : $"{Transaction.Warehouse} · {Transaction.Partner}".Trim(' ', '·');
}
