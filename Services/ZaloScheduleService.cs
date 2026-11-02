using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HisaWarehouse.Services;

/// <summary>Only planned work; neither Upsert nor PreparePending touches stock or the Excel ledger.</summary>
public static class ZaloScheduleService
{
    public const string ContainerArrival = "Container về kho";
    public const string Shipment = "Xuất hàng";

    public sealed record ImportResult(int Added, int Updated, int Unchanged);
    public sealed record PendingPlan(MovementDraft Draft, IReadOnlyList<string> PlanIds);

    public static string Fingerprint(byte[] image) => Convert.ToHexString(SHA256.HashData(image));

    public static string Key(ZaloPlannedWork row)
    {
        var contract = Compact(row.ContractNo);
        if (contract.Length == 0) throw new ArgumentException("Thiếu số hợp đồng.");
        if (row.WorkType is not (ContainerArrival or Shipment))
            throw new ArgumentException("Công việc phải là Container về kho hoặc Xuất hàng.");
        // A different package/SKU under the same contract is a separate plan.
        // Deliberately exclude the expected date so rescheduling is an update.
        return row.WorkType + "|" + contract + "|" + Compact(row.PackageNo) + "|" + Compact(row.Sku);
    }

    private static string Compact(string value) =>
        string.Join("", (value ?? "").ToUpperInvariant().Where(c => !char.IsWhiteSpace(c)));

    public static DateOnly? ParseExpectedDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParseExact(value.Trim(), ["yyyy-MM-dd", "dd/MM/yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        throw new ArgumentException("Ngày dự kiến phải theo yyyy-MM-dd hoặc dd/MM/yyyy.");
    }

    public static ImportResult Upsert(ICollection<ZaloPlannedWork> destination,
        IEnumerable<ZaloPlannedWork> selectedRows)
    {
        var candidates = selectedRows.Select(Clone).ToList();
        if (candidates.Count == 0) throw new ArgumentException("Chưa chọn hợp đồng nào.");
        if (candidates.Count > 200) throw new ArgumentException("Mỗi lần chỉ nhập tối đa 200 dòng kế hoạch.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in candidates)
        {
            entry.ContractNo = entry.ContractNo.Trim();
            entry.PackageNo = entry.PackageNo.Trim();
            entry.Sku = entry.Sku.Trim();
            entry.ItemName = entry.ItemName.Trim();
            entry.Unit = entry.Unit.Trim();
            entry.Note = entry.Note.Trim();
            if (!double.IsFinite(entry.Quantity) || entry.Quantity < 0 ||
                entry.ContractNo.Length > 100 || entry.PackageNo.Length > 120 ||
                entry.Note.Length > 1000 || entry.ItemName.Length > 250 || entry.Sku.Length > 80)
                throw new ArgumentException("Có dòng lịch Zalo vượt giới hạn hoặc số lượng không hợp lệ.");
            entry.ExpectedDate = ParseExpectedDate(entry.ExpectedDate)?.ToString("yyyy-MM-dd") ?? "";
            if (!keys.Add(Key(entry)))
                throw new ArgumentException("Ảnh chứa hợp đồng/kiện/mã vật tư trùng nhau. Vui lòng gộp trước khi nhập.");
        }
        // Validate entire batch before mutating the live state.
        var existing = destination.ToDictionary(Key, StringComparer.Ordinal);
        int added = 0, updated = 0, unchanged = 0;
        foreach (var entry in candidates)
        {
            if (!existing.TryGetValue(Key(entry), out var old))
            {
                destination.Add(entry);
                existing.Add(Key(entry), entry);
                added++;
                continue;
            }
            if (old.ExpectedDate == entry.ExpectedDate &&
                old.ItemName == entry.ItemName && old.Unit == entry.Unit &&
                old.Quantity == entry.Quantity && old.Note == entry.Note)
            {
                unchanged++;
                continue;
            }
            if (old.ExpectedDate != entry.ExpectedDate) old.DateRevision++;
            old.ExpectedDate = entry.ExpectedDate;
            // A pending slip is already a saved draft. Never silently alter its
            // material quantity or SKU when an image is imported again.
            if (old.PendingDocNo.Length == 0)
            {
                old.ItemName = entry.ItemName;
                old.Unit = entry.Unit;
                old.Quantity = entry.Quantity;
            }
            old.Note = entry.Note;
            updated++;
        }
        return new ImportResult(added, updated, unchanged);
    }

    public static PendingPlan PreparePending(IReadOnlyList<ZaloPlannedWork> selected,
        IReadOnlyList<InventoryItem> products, string warehouse, string operatorName)
    {
        if (selected.Count == 0) throw new ArgumentException("Chưa chọn hợp đồng để lên phiếu.");
        if (selected.Count > 30) throw new ArgumentException("Mỗi phiếu chờ tối đa 30 dòng kiện.");
        if (string.IsNullOrWhiteSpace(warehouse) || string.IsNullOrWhiteSpace(operatorName))
            throw new ArgumentException("Chưa có kho hoặc người lập phiếu.");
        if (selected.Any(x => x.WorkType != selected[0].WorkType))
            throw new ArgumentException("Không gộp lịch container nhập và lịch xuất vào cùng một phiếu.");
        if (selected.Any(x => !string.IsNullOrWhiteSpace(x.PendingDocNo)))
            throw new InvalidOperationException("Có hợp đồng đã lên phiếu chờ. Không tạo phiếu trùng.");
        if (selected.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != selected.Count)
            throw new ArgumentException("Trùng kế hoạch trong danh sách chọn.");

        var lines = new List<MovementLine>();
        var manifest = new List<string>();
        foreach (var plan in selected)
        {
            var sku = plan.Sku.Trim();
            var product = products.SingleOrDefault(p => p.Active &&
                p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));
            if (product is null || string.IsNullOrWhiteSpace(sku))
                throw new InvalidOperationException(
                    $"Hợp đồng {plan.ContractNo} chưa có mã vật tư hợp lệ. Hãy bổ sung SKU trước khi lập phiếu.");
            if (!double.IsFinite(plan.Quantity) || plan.Quantity <= 0)
                throw new InvalidOperationException(
                    $"Hợp đồng {plan.ContractNo} thiếu số lượng vật tư. Hãy sửa dữ liệu trước.");
            var same = lines.FirstOrDefault(x => x.Sku.Equals(product.Sku, StringComparison.OrdinalIgnoreCase));
            if (same is null) lines.Add(new MovementLine
            { Sku = product.Sku, ItemName = product.Name, Unit = product.Unit, Quantity = plan.Quantity });
            else same.Quantity += plan.Quantity;
            manifest.Add(plan.ContractNo + (plan.PackageNo.Length > 0 ? "/" + plan.PackageNo : ""));
        }
        var type = selected[0].WorkType == Shipment ? "Xuất" : "Nhập";
        var draft = new MovementDraft
        {
            Type = type,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Warehouse = warehouse,
            Operator = operatorName,
            ReferenceNo = "ZALO-" + selected[0].ContractNo,
            Note = "Lịch dự kiến từ Zalo (chưa hoàn thành): " + string.Join("; ", manifest),
            Pending = true,
            Lines = lines
        };
        return new PendingPlan(draft, selected.Select(x => x.Id).ToArray());
    }

    public static void MarkPending(ICollection<ZaloPlannedWork> plans,
        IReadOnlyList<string> ids, string docNo)
    {
        if (string.IsNullOrWhiteSpace(docNo)) throw new ArgumentException("Thiếu số phiếu chờ.");
        var selected = plans.Where(p => ids.Contains(p.Id, StringComparer.Ordinal)).ToArray();
        if (selected.Length != ids.Count ||
            selected.Any(p => p.PendingDocNo.Length > 0))
            throw new InvalidOperationException("Kế hoạch đã được lên phiếu hoặc bị thay đổi.");
        foreach (var p in selected) p.PendingDocNo = docNo;
    }

    private static ZaloPlannedWork Clone(ZaloPlannedWork p) => new()
    {
        Id = p.Id, ContractNo = p.ContractNo, PackageNo = p.PackageNo,
        WorkType = p.WorkType, ExpectedDate = p.ExpectedDate,
        Sku = p.Sku, ItemName = p.ItemName, Unit = p.Unit,
        Quantity = p.Quantity, Note = p.Note,
        ImageFingerprint = p.ImageFingerprint, PendingDocNo = p.PendingDocNo,
        DateRevision = p.DateRevision
    };
}
