namespace HisaWarehouse.Services;

public static class PrintTemplateService
{
    public static List<PrintTemplate> CreateDefaults() =>
    [
        new() { Type = "Nhập", Title = "PHIẾU NHẬP KHO", Signer1 = "NGƯỜI GIAO", Signer2 = "NGƯỜI NHẬN", Signer3 = "THỦ KHO" },
        new() { Type = "Xuất", Title = "PHIẾU XUẤT KHO", Signer1 = "NGƯỜI XUẤT", Signer2 = "NGƯỜI NHẬN", Signer3 = "THỦ KHO" },
        new() { Type = "Điều chuyển", Title = "PHIẾU ĐIỀU CHUYỂN KHO", Signer1 = "KHO GIAO", Signer2 = "KHO NHẬN", Signer3 = "THỦ KHO" },
        new() { Type = "Yêu cầu cấp vật tư dự phòng", Title = "PHIẾU YÊU CẦU CẤP VẬT TƯ DỰ PHÒNG", Signer1 = "NGƯỜI YÊU CẦU", Signer2 = "PHỤ TRÁCH BỘ PHẬN", Signer3 = "NGƯỜI DUYỆT" },
        new() { Type = "Yêu cầu cấp vật tư", Title = "PHIẾU YÊU CẦU CẤP VẬT TƯ", Signer1 = "NGƯỜI YÊU CẦU", Signer2 = "TRƯỞNG BỘ PHẬN", Signer3 = "THỦ KHO" },
        new() { Type = "Yêu cầu nhập vật tư", Title = "PHIẾU YÊU CẦU NHẬP VẬT TƯ", Signer1 = "NGƯỜI ĐỀ NGHỊ", Signer2 = "TRƯỞNG BỘ PHẬN", Signer3 = "PHỤ TRÁCH KHO" }
    ];

    public static PrintTemplate Get(InventoryState state, string type)
    {
        EnsureDefaults(state);
        return state.PrintTemplates.FirstOrDefault(template => template.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            ?? CreateDefaults().FirstOrDefault(template => template.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Chưa cấu hình mẫu in cho loại phiếu {type}.");
    }

    public static void EnsureDefaults(InventoryState state)
    {
        state.PrintTemplates ??= [];
        if (state.PrintTemplatesConfigured) return;
        if (state.PrintTemplates.Count == 0)
            state.PrintTemplates.AddRange(CreateDefaults());
        else
            foreach (var template in CreateDefaults())
                if (!state.PrintTemplates.Any(existing => existing.Type.Equals(template.Type, StringComparison.OrdinalIgnoreCase)))
                    state.PrintTemplates.Add(template);
        state.PrintTemplatesConfigured = true;
    }
}
