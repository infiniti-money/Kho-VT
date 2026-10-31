namespace HisaWarehouse.Services;

/// <summary>Một dòng gợi ý cho một kho cụ thể; chỉ tính các mã có khai báo tồn tối thiểu.</summary>
public sealed class ReplenishmentSuggestion
{
    public string Sku { get; init; } = "";
    public string Name { get; init; } = "";
    public string Unit { get; init; } = "";
    public double CurrentStock { get; init; }
    public double MinimumStock { get; init; }
    public double PendingQuantity { get; init; }
    public double SuggestedQuantity { get; init; }
    public bool Selected { get; set; } = true;
    public double RequestedQuantity { get; set; }
    public string CurrentText => InventoryRow.FormatQuantity(CurrentStock);
    public string MinimumText => InventoryRow.FormatQuantity(MinimumStock);
    public string PendingText => InventoryRow.FormatQuantity(PendingQuantity);
}

public static class ReplenishmentService
{
    public static List<ReplenishmentSuggestion> Generate(InventoryState state, string warehouse)
    {
        if (!state.Warehouses.Contains(warehouse, StringComparer.Ordinal)) return [];

        // Chỉ trừ các phiếu đã được tạo bởi màn hình đề xuất cho chính kho này.
        // Những yêu cầu nhập tay chưa có kho được giữ nguyên và cần kiểm tra thủ công.
        var pendingBySku = state.MaterialRequests
            .Where(request => request.Type == "Yêu cầu nhập vật tư" &&
                              request.Status == "Chờ xử lý" &&
                              string.Equals(request.Warehouse, warehouse, StringComparison.Ordinal))
            .SelectMany(request => request.Lines)
            .Where(line => double.IsFinite(line.Quantity) && line.Quantity > 0)
            .GroupBy(line => line.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity), StringComparer.OrdinalIgnoreCase);

        return state.Products
            .Where(item => item.Active && item.MinStockDefined &&
                           double.IsFinite(item.MinStock) && item.MinStock > 0)
            .Select(item =>
            {
                var current = item.WarehouseStocks.GetValueOrDefault(warehouse);
                var pending = pendingBySku.GetValueOrDefault(item.Sku);
                var shortage = item.MinStock - current - pending;
                return new ReplenishmentSuggestion
                {
                    Sku = item.Sku, Name = item.Name, Unit = item.Unit,
                    CurrentStock = current, MinimumStock = item.MinStock,
                    PendingQuantity = pending,
                    SuggestedQuantity = shortage,
                    RequestedQuantity = shortage
                };
            })
            .Where(suggestion => double.IsFinite(suggestion.CurrentStock) &&
                                 double.IsFinite(suggestion.SuggestedQuantity) &&
                                 suggestion.SuggestedQuantity > 0)
            .OrderBy(suggestion => suggestion.CurrentStock <= 0 ? 0 : 1)
            .ThenBy(suggestion => suggestion.CurrentStock / suggestion.MinimumStock)
            .ThenBy(suggestion => suggestion.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
