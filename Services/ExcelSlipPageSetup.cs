namespace HisaWarehouse.Services;

/// <summary>
/// Validation and conversion only. Excel COM is invoked against a temporary
/// working copy in ExcelSlipTemplatePrinter, never the original KHO.xlsx.
/// </summary>
public static class ExcelSlipPageSetup
{
    // Microsoft Excel XlPaperSize values: Letter 1, A4 9, A5 11, A6 70.
    // Driver-specific receipt/custom papers require a validated driver path
    // and must not silently be substituted with another size.
    public sealed record Settings(int PaperSize, int Orientation, double TopPoints,
        double BottomPoints, double LeftPoints, double RightPoints,
        bool FitToPage, int ScalePercent);

    public static Settings ForTemplate(PrintTemplate template)
    {
        var dimensions = PrintPageSetup.Resolve(template);
        var paperSize = template.PaperName switch
        {
            "A4" => 9,
            "A5" => 11,
            "A6" => 70,
            "Letter" => 1,
            _ => throw new NotSupportedException(
                "Excel chưa thể xác nhận khổ " + template.PaperName +
                " trên driver máy in. Hãy dùng A4, A5, A6 hoặc Letter cho PDF từ mẫu Excel; " +
                "khổ 58/80 mm và tùy chỉnh vẫn sử dụng được với mẫu phiếu HISA.")
        };
        return new Settings(paperSize, template.Landscape ? 2 : 1,
            dimensions.TopMm * 72 / 25.4, dimensions.BottomMm * 72 / 25.4,
            dimensions.LeftMm * 72 / 25.4, dimensions.RightMm * 72 / 25.4,
            template.FitToPage, template.PrintScalePercent);
    }
}
