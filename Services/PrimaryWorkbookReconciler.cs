using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HisaWarehouse.Services;

/// <summary>
/// Excel is the canonical total stock. A reconciliation replaces the cached
/// total and imported history; it NEVER replays Excel movements as stock deltas.
/// Preview/accept is explicit and optimistic-concurrency checked.
/// </summary>
public static class PrimaryWorkbookReconciler
{
    private static readonly Regex HisaToken = new(
        @"\[HISA:(?<number>[A-Za-z0-9_-]+)\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public sealed record Preview(
        string WorkbookPath, string WorkbookHash, string StateHash, DateTime SavedUtc,
        IReadOnlyDictionary<string, PrimaryWorkbookReader.Stock> Stocks,
        IReadOnlyList<PrimaryWorkbookReader.Transaction> Transactions,
        int NewSkus, int ChangedStocks, int ChangedHistory, int UnmatchedLocalSkus)
    {
        public string Summary =>
            $"Excel đã lưu {SavedUtc.ToLocalTime():dd/MM/yyyy HH:mm}. " +
            $"{Stocks.Count:N0} mã vật tư, {Transactions.Count:N0} dòng giao dịch.\n" +
            $"{NewSkus:N0} mã mới · {ChangedStocks:N0} số tồn khác cache · " +
            $"{ChangedHistory:N0} dòng lịch sử nhập từ Excel thay đổi.\n" +
            (UnmatchedLocalSkus > 0
                ? $"{UnmatchedLocalSkus:N0} mã chỉ có ở HISA sẽ được giữ nguyên, cần kiểm tra riêng.\n"
                : "") +
            "Chấp nhận sẽ dùng TỒN HIỆN TẠI tại Danh mục làm số dư chuẩn, " +
            "KHÔNG cộng thêm giao dịch Excel. Nhật ký nội bộ không bị xóa.";
    }

    public static Preview Prepare(string path, InventoryState state)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException("Hãy chọn file KHO.xlsx chính.");
        path = Path.GetFullPath(path);
        var before = Fingerprint(path);
        // A workbook saved by HISA still has pre-write cached formulas. A real
        // Excel recalculation and save is required before accepting F as stock.
        EnsureCalculated(path);
        var audit = PrimaryWorkbookInspector.Inspect(path);
        if (audit.MissingProtectedFormulas > 0)
            throw new InvalidDataException($"Có {audit.MissingProtectedFormulas} ô công thức thiếu. Khôi phục công thức rồi tính lại Excel trước khi đồng bộ.");
        var stocks = PrimaryWorkbookReader.ReadStocks(path);
        var rows = PrimaryWorkbookReader.ReadTransactions(path);
        if (Fingerprint(path) != before)
            throw new IOException("Excel thay đổi trong lúc đối chiếu; hãy lưu xong rồi thử lại.");
        var unknownRow = rows.FirstOrDefault(row => !stocks.ContainsKey(row.Sku));
        if (unknownRow is not null)
            throw new InvalidDataException($"Nhập Xuất dòng {unknownRow.Row} có mã {unknownRow.Sku} không có trong Danh mục. Hãy sửa trong Excel trước khi đồng bộ.");
        VerifyOwnedMovements(rows, state);
        var current = state.Products.ToDictionary(p => p.Sku, StringComparer.OrdinalIgnoreCase);
        var history = state.Transactions.Where(t => t.Legacy && t.SourceRow.HasValue)
            .ToDictionary(t => t.SourceRow!.Value);
        int modifiedHistory = rows.Count(row =>
            !history.TryGetValue(row.Row, out var existing) ||
            existing.Sku != row.Sku || existing.Type != row.Type ||
            existing.Quantity != row.Quantity || existing.Date !=
                (row.Date.HasValue ? DateOnly.FromDateTime(row.Date.Value) : null));
        return new Preview(path, before, StateSignature(state), File.GetLastWriteTimeUtc(path),
            stocks, rows,
            stocks.Keys.Count(sku => !current.ContainsKey(sku)),
            stocks.Values.Count(stock => current.TryGetValue(stock.Sku, out var item) &&
                (Math.Abs(item.TotalStock - stock.Current) > 1e-7 ||
                 item.Name != stock.Name || item.Unit != stock.Unit ||
                 Math.Abs(item.MinStock - stock.Minimum) > 1e-7)),
            modifiedHistory, current.Keys.Count(sku => !stocks.ContainsKey(sku)));
    }

    public static void Apply(Preview preview, InventoryState state)
    {
        if (Fingerprint(preview.WorkbookPath) != preview.WorkbookHash)
            throw new IOException("KHO.xlsx đã thay đổi sau khi xem trước; hủy đồng bộ để tránh ghi đè.");
        if (StateSignature(state) != preview.StateHash)
            throw new InvalidOperationException("Dữ liệu HISA đã thay đổi sau khi xem trước; hãy đối chiếu lại.");
        VerifyOwnedMovements(preview.Transactions, state);

        var warehouse = state.Warehouses.FirstOrDefault(w =>
            w.Equals("Kho tổng", StringComparison.OrdinalIgnoreCase)) ??
            state.Warehouses.FirstOrDefault() ??
            throw new InvalidDataException("HISA chưa có kho để đặt số dư tổng từ Excel.");

        var existingBySku = state.Products.ToDictionary(p => p.Sku, StringComparer.OrdinalIgnoreCase);
        foreach (var stock in preview.Stocks.Values)
        {
            if (!existingBySku.TryGetValue(stock.Sku, out var product))
            {
                product = new InventoryItem { Sku = stock.Sku, Barcode = stock.Sku, Active = true };
                state.Products.Add(product);
                existingBySku.Add(stock.Sku, product);
            }
            product.Name = stock.Name;
            product.Unit = stock.Unit;
            product.MinStock = stock.Minimum;
            product.MinStockDefined = true;
            product.SourceRow = stock.Row;
            product.SourceCurrentStock = stock.Current;
            product.WarehouseStocks.TryAdd(warehouse, 0);
            // Preserve allocations in other HISA warehouses, balance only
            // the general warehouse to the already-computed Excel stock total.
            var other = product.WarehouseStocks.Where(kv => kv.Key != warehouse).Sum(kv => kv.Value);
            product.WarehouseStocks[warehouse] = stock.Current - other;
        }

        // Rebuild the imported rows instead of appending deltas. Keep HISA-owned
        // rows from the local journal; their Excel counterparts carry [HISA:...].
        var ownedDocs = state.Transactions.Where(t => !t.Legacy)
            .Select(t => t.DocNo).ToHashSet(StringComparer.Ordinal);
        var local = state.Transactions.Where(t => !t.Legacy || !t.SourceRow.HasValue).ToList();
        var source = preview.Transactions.Where(t =>
            !ownedDocs.Contains(DocumentMarker(t.Note))).Select(t =>
        {
            var product = existingBySku[t.Sku];
            var document = DocumentMarker(t.Note);
            return new InventoryTransaction
            {
                Id = "excel-row-" + t.Row.ToString(CultureInfo.InvariantCulture),
                DocNo = document.Length > 0 ? document : "Excel-" + t.Row.ToString(CultureInfo.InvariantCulture),
                ReferenceNo = document.Length > 0 ? document : "Excel-" + t.Row.ToString(CultureInfo.InvariantCulture),
                Date = t.Date.HasValue ? DateOnly.FromDateTime(t.Date.Value) : null,
                Type = t.Type, Sku = t.Sku, ItemName = product.Name,
                Category = product.Category, Unit = product.Unit,
                Quantity = t.Quantity, Partner = t.Partner, Note = t.Note,
                Warehouse = warehouse, SourceRow = t.Row, Legacy = true,
                AffectsStock = false, SourceAlert = "Excel đã tính vào tồn; không cộng lại."
            };
        }).ToList();
        state.Transactions = new ObservableCollection<InventoryTransaction>(local.Concat(source));
        state.PrimaryExcelPath = preview.WorkbookPath;
        state.PrimarySyncFingerprint = preview.WorkbookHash;
        state.PrimaryExcelSyncedWriteUtc = preview.SavedUtc;
        state.SourceLastModified = preview.SavedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        state.SourceImportedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
    }

    private static void VerifyOwnedMovements(IReadOnlyList<PrimaryWorkbookReader.Transaction> rows, InventoryState state)
    {
        var owned = state.Transactions.Where(t => !t.Legacy && t.Type is "Nhập" or "Xuất")
            .GroupBy(t => t.DocNo, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        foreach (var group in rows.Where(r => DocumentMarker(r.Note).Length > 0)
            .GroupBy(r => DocumentMarker(r.Note), StringComparer.Ordinal))
        {
            if (!owned.TryGetValue(group.Key, out var local)) continue;
            var actual = group.Select(r => (r.Type, r.Sku, r.Quantity))
                .OrderBy(r => r.Type).ThenBy(r => r.Sku).ThenBy(r => r.Quantity).ToArray();
            var expected = local.Select(r => (r.Type, r.Sku, Quantity: r.Quantity ?? 0))
                .OrderBy(r => r.Type).ThenBy(r => r.Sku).ThenBy(r => r.Quantity).ToArray();
            if (!actual.SequenceEqual(expected))
                throw new InvalidDataException($"Phiếu {group.Key} trong Excel khác nhật ký HISA. Cần xử lý xung đột thủ công, không tự gộp.");
        }
    }

    private static string DocumentMarker(string note) =>
        HisaToken.Match(note ?? "").Groups["number"].Value;

    private static void EnsureCalculated(string path)
    {
        using var zip = new ZipArchive(ExcelReadSnapshot.Capture(path), ZipArchiveMode.Read);
        using var stream = zip.GetEntry("xl/workbook.xml")?.Open()
            ?? throw new InvalidDataException("Excel thiếu workbook.xml.");
        var doc = XDocument.Load(stream);
        var calc = doc.Descendants(M + "calcPr").FirstOrDefault();
        if ((string?)calc?.Attribute("fullCalcOnLoad") == "1" ||
            (string?)calc?.Attribute("forceFullCalc") == "1")
            throw new InvalidDataException("Excel còn yêu cầu tính lại công thức. Hãy mở KHO.xlsx bằng Microsoft Excel, tính lại và lưu trước khi dùng tồn hiện tại F làm nguồn chuẩn.");
    }

    public static string Fingerprint(string path)
    {
        using var snapshot = ExcelReadSnapshot.Capture(path);
        return Convert.ToHexString(SHA256.HashData(snapshot));
    }

    private static string StateSignature(InventoryState state)
    {
        var data = string.Join("\n", state.Products.OrderBy(p => p.Sku).Select(p =>
            p.Sku + "|" + p.Name + "|" + p.Unit + "|" + p.MinStock.ToString("R", CultureInfo.InvariantCulture) +
            "|" + string.Join(",", p.WarehouseStocks.OrderBy(x => x.Key).Select(x =>
                x.Key + "=" + x.Value.ToString("R", CultureInfo.InvariantCulture)))))
            + "\n" + string.Join("\n", state.Transactions.Select(t =>
                t.Id + "|" + t.DocNo + "|" + t.Sku + "|" + t.Quantity?.ToString("R", CultureInfo.InvariantCulture)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
    }
}
