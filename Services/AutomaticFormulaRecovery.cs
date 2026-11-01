using System.IO;

namespace HisaWarehouse.Services;

/// <summary>
/// On explicit HISA warehouse receipt/shipment save, restore missing or
/// overwritten Excel formulas ONLY from an independently verified HISA backup.
/// Never synthesize a formula from neighboring cells or reuse an unrelated
/// workbook; never restore when the primary workbook changed since sync.
/// </summary>
public static class AutomaticFormulaRecovery
{
    public sealed record Outcome(bool Recovered, string WorkbookHash, int RestoredCells, string? DonorPath);

    public static Outcome EnsureProtectedFormulas(string workbook, string expectedFingerprint)
    {
        if (string.IsNullOrWhiteSpace(expectedFingerprint))
            throw new InvalidDataException("Chưa đồng bộ file KHO.xlsx chính.");
        var path = Path.GetFullPath(workbook);
        var existingHash = PrimaryWorkbookReconciler.Fingerprint(path);
        if (!existingHash.Equals(expectedFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new IOException(
                "File KHO.xlsx đã thay đổi từ lần đồng bộ gần nhất. " +
                "Hãy lưu, đồng bộ từ Excel rồi thử lại; không tự khôi phục trên dữ liệu chưa đồng bộ.");
        var profile = PrimaryWorkbookInspector.Inspect(path);
        if (profile.MissingProtectedFormulas == 0)
            return new Outcome(false, existingHash, 0, null);

        var folder = Path.GetDirectoryName(path)!;
        var filename = Path.GetFileName(path);
        // The donor is a HISA-generated backup for THIS workbook, not an
        // arbitrary downloaded Excel file. Every donor goes through the
        // strict structural/SKU/formula compatibility verifier.
        var candidates = Directory.EnumerateFiles(folder, filename + ".HISA-*.bak",
                SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        foreach (var donor in candidates)
        {
            try
            {
                var plan = PrimaryFormulaRecovery.Inspect(path, donor);
                if (plan.Missing.Count != profile.MissingProtectedFormulas)
                    continue;
                PrimaryFormulaRecovery.Restore(plan);
                var newHash = PrimaryWorkbookReconciler.Fingerprint(path);
                if (PrimaryWorkbookInspector.Inspect(path).MissingProtectedFormulas != 0)
                    throw new InvalidDataException("Công thức vẫn thiếu sau khi khôi phục.");
                return new Outcome(true, newHash, plan.Missing.Count, donor);
            }
            catch (InvalidDataException)
            {
                // A backup may be old, inconsistent, or missing the exact
                // formula row. Never force it into the live workbook.
            }
        }
        throw new InvalidDataException(
            $"Phát hiện {profile.MissingProtectedFormulas} công thức bị xóa/ghi đè nhưng " +
            "không có bản sao lưu HISA tương thích đã xác minh. " +
            "Đã dừng ghi để bảo vệ file. Hãy khôi phục từ bản sao hợp lệ trong phần Dữ liệu Excel.");
    }
}
