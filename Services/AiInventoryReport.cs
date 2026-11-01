using System.Globalization;
using System.Text;

namespace HisaWarehouse.Services;

/// <summary>
/// Deterministic, local calculations. No AI request and no workbook write occurs here.
/// Snapshot stock is never recomputed by adding historical transactions a second time.
/// </summary>
public static class AiInventoryReport
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string Build(InventoryState state, DateOnly today)
    {
        var selected = state.SelectedWarehouses.Where(state.Warehouses.Contains)
            .Distinct(StringComparer.Ordinal).ToArray();
        var products = state.Products.Where(p => p.Active).ToList();
        var startWeek = today.AddDays(-6);
        var startMonth = today.AddDays(-29);
        var transactions = state.Transactions
            .Where(t => t.Date.HasValue && t.Date >= startMonth && t.Date <= today &&
                        t.Quantity is double q && double.IsFinite(q) && q > 0 &&
                        (t.Type == "Nhập" || t.Type == "Xuất") &&
                        selected.Contains(t.Warehouse, StringComparer.Ordinal))
            .ToList();

        var lines = new StringBuilder();
        lines.AppendLine($"Nguồn: dữ liệu HISA lưu cục bộ; thời điểm báo cáo {today:dd/MM/yyyy}. Không đảm bảo đã đồng bộ Excel.");
        lines.AppendLine($"Kho được chọn: {(selected.Length == 0 ? "không có" : string.Join(", ", selected))}.");
        lines.AppendLine("Kỳ tuần: 7 ngày gần nhất; kỳ tháng: 30 ngày gần nhất.");
        lines.AppendLine("Số lượng các mặt hàng khác đơn vị không được cộng thành tổng.");
        if (selected.Length == 0)
            return lines.AppendLine("Không có kho được chọn; chưa thể phân tích tồn kho.").ToString();

        var rows = products.Select(p =>
        {
            var histories = transactions.Where(t => t.Sku.Equals(p.Sku, StringComparison.OrdinalIgnoreCase)).ToList();
            var week = histories.Where(t => t.Date >= startWeek).ToList();
            return new
            {
                Item = p,
                Stock = p.StockAt(selected),
                InWeek = week.Where(t => t.Type == "Nhập").Sum(t => t.Quantity ?? 0),
                OutWeek = week.Where(t => t.Type == "Xuất").Sum(t => t.Quantity ?? 0),
                InMonth = histories.Where(t => t.Type == "Nhập").Sum(t => t.Quantity ?? 0),
                OutMonth = histories.Where(t => t.Type == "Xuất").Sum(t => t.Quantity ?? 0),
                OutLines = histories.Count(t => t.Type == "Xuất")
            };
        }).ToList();

        var outCount = rows.Count(x => x.Stock <= 0);
        var lowCount = rows.Count(x => x.Stock > 0 && x.Item.MinStockDefined &&
            x.Item.MinStock > 0 && x.Stock < x.Item.MinStock);
        lines.AppendLine($"Mã vật tư đang quản lý: {rows.Count}. Hết/âm tồn: {outCount}. Tồn dưới tối thiểu: {lowCount}.");
        lines.AppendLine($"Lượt giao dịch có ngày và số lượng hợp lệ trong 30 ngày: {transactions.Count}.");
        lines.AppendLine("Các mã cần quan tâm (tối đa 24, xếp theo hết/âm tồn, dưới tối thiểu và lượt xuất):");
        var important = rows.OrderBy(x => x.Stock <= 0 ? 0 :
                x.Item.MinStockDefined && x.Item.MinStock > 0 && x.Stock < x.Item.MinStock ? 1 : 2)
            .ThenByDescending(x => x.OutLines)
            .Take(24);
        foreach (var x in important)
        {
            var name = x.Item.Name.Replace("\r", " ").Replace("\n", " ");
            if (name.Length > 90) name = name[..90];
            lines.AppendLine(
                $"- {x.Item.Sku} | {name} | {x.Item.Unit} | tồn={x.Stock.ToString("0.##", Vi)}" +
                $" | tối thiểu={(x.Item.MinStockDefined ? x.Item.MinStock.ToString("0.##", Vi) : "không khai báo")}" +
                $" | nhập 7 ngày={x.InWeek.ToString("0.##", Vi)} | xuất 7 ngày={x.OutWeek.ToString("0.##", Vi)}" +
                $" | nhập 30 ngày={x.InMonth.ToString("0.##", Vi)} | xuất 30 ngày={x.OutMonth.ToString("0.##", Vi)}" +
                $" | lượt xuất 30 ngày={x.OutLines}");
        }
        lines.AppendLine("Danh sách chỉ là các mã nổi bật, không phải toàn bộ dữ liệu kho.");
        return lines.ToString();
    }

    /// <summary>
    /// Exact SKU lookup is resolved from the current HISA state rather than a
    /// 24-item truncated Gemini report. No network request and no stock replay.
    /// </summary>
    public static string? TryAnswerStockBySku(InventoryState state, string question)
    {
        if (string.IsNullOrWhiteSpace(question) ||
            !(question.Contains("mã", StringComparison.OrdinalIgnoreCase) ||
              question.Contains("SKU", StringComparison.OrdinalIgnoreCase)))
            return null;
        var match = System.Text.RegularExpressions.Regex.Match(question,
            @"(?:mã|SKU)\s+(?:hàng\s+|vật\s+tư\s+)?(?<sku>[\p{L}\p{N}._-]{2,64})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        var sku = match.Groups["sku"].Value.Trim().TrimEnd('.', '!', '?');
        var product = state.Products.FirstOrDefault(p =>
            p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase) && p.Active);
        if (product is null)
            return $"An chưa tìm thấy mã vật tư {sku} trong dữ liệu HISA đang lưu. " +
                "Hãy đồng bộ Excel nếu mã mới được thêm ngoài ứng dụng.";
        var warehouses = state.SelectedWarehouses.Where(state.Warehouses.Contains)
            .Distinct(StringComparer.Ordinal).ToList();
        if (warehouses.Count == 0)
            return $"An đã tìm thấy {product.Sku} - {product.Name}, " +
                "nhưng chưa có kho nào được chọn để kiểm tra tồn.";
        var entries = warehouses.Select(wh =>
            $"{wh}: {product.WarehouseStocks.GetValueOrDefault(wh).ToString("0.##", Vi)} {product.Unit}");
        var stock = product.StockAt(warehouses);
        var status = stock <= 0 ? "hết hoặc âm tồn" :
            product.MinStockDefined && product.MinStock > 0 && stock < product.MinStock
                ? "dưới mức tối thiểu" : "đủ tồn theo ngưỡng đã khai báo";
        return $"An kiểm tra mã {product.Sku} - {product.Name}. " +
            $"Tổng tồn ở các kho được chọn: {stock.ToString("0.##", Vi)} {product.Unit}; " +
            $"tình trạng: {status}. " +
            $"Tồn tối thiểu: {(product.MinStockDefined ? product.MinStock.ToString("0.##", Vi) : "chưa khai báo")} {product.Unit}. " +
            $"Chi tiết: {string.Join("; ", entries)}. " +
            "Nguồn là bộ nhớ HISA, có thể chưa đồng bộ thay đổi mới trong Excel.";
    }

    public static (int OutOfStock, int LowStock, int MovementsMonth) Metrics(InventoryState state)
    {
        var selected = state.SelectedWarehouses.Where(state.Warehouses.Contains).ToArray();
        if (selected.Length == 0) return (0, 0, 0);
        var rows = state.Products.Where(p => p.Active).Select(item =>
            (item, stock: item.StockAt(selected))).ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = today.AddDays(-29);
        return (
            rows.Count(x => x.stock <= 0),
            rows.Count(x => x.stock > 0 && x.item.MinStockDefined &&
                x.item.MinStock > 0 && x.stock < x.item.MinStock),
            state.Transactions.Count(t => t.Date >= start && t.Date <= today &&
                (t.Type == "Nhập" || t.Type == "Xuất") &&
                selected.Contains(t.Warehouse) && t.Quantity is double q && q > 0)
        );
    }
}
