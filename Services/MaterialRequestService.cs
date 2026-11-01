namespace HisaWarehouse.Services;

public static class MaterialRequestService
{
    public static void Add(InventoryState state, MaterialRequest request)
    {
        var type = request.Type;
        if (type is not ("Yêu cầu cấp vật tư" or "Yêu cầu nhập vật tư" or "Yêu cầu cấp vật tư dự phòng"))
            throw new ArgumentException("Loại phiếu yêu cầu không hợp lệ.", nameof(request));

        // Existing request types and document numbers retain their original IDs.
        var prefix = type == "Yêu cầu cấp vật tư dự phòng" ? "YC-DP" :
            type == "Yêu cầu cấp vật tư" ? "YC-CAP" : "YC-NHAP";
        if (request.Lines.Count == 0 ||
            request.Lines.Any(line => string.IsNullOrWhiteSpace(line.ItemName) ||
                string.IsNullOrWhiteSpace(line.Unit) ||
                !double.IsFinite(line.Quantity) || line.Quantity <= 0))
            throw new ArgumentException("Phiếu phải có vật tư, đơn vị tính và số lượng hợp lệ.", nameof(request));

        var prefixWithDate = $"{prefix}-{request.Date:yyyyMMdd}-";
        var highest = state.MaterialRequests.Select(item => item.DocNo)
            .Where(number => number.StartsWith(prefixWithDate, StringComparison.OrdinalIgnoreCase))
            .Select(number => int.TryParse(number[(number.LastIndexOf('-') + 1)..], out var value) ? value : 0)
            .DefaultIfEmpty(0).Max();

        request.DocNo = $"{prefixWithDate}{highest + 1:000}";
        state.MaterialRequests.Add(request);
        if (!string.IsNullOrWhiteSpace(request.Department) &&
            !state.Departments.Contains(request.Department, StringComparer.OrdinalIgnoreCase))
            state.Departments.Add(request.Department);
        if (!string.IsNullOrWhiteSpace(request.Recipient) &&
            !state.Recipients.Contains(request.Recipient, StringComparer.OrdinalIgnoreCase))
            state.Recipients.Add(request.Recipient);
    }
}
