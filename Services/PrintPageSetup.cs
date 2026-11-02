namespace HisaWarehouse.Services;

/// <summary>
/// Pure page-setup calculations. The paper/driver is validated separately at
/// print time; this service never changes the source KHO.xlsx workbook.
/// </summary>
public static class PrintPageSetup
{
    public const double DipPerMm = 96.0 / 25.4;
    public static readonly string[] PaperNames = ["A4", "A5", "A6", "Letter", "58mm", "80mm", "Tùy chỉnh"];

    public sealed record Dimensions(double WidthMm, double HeightMm, double TopMm,
        double BottomMm, double LeftMm, double RightMm)
    {
        public double WidthDip => WidthMm * DipPerMm;
        public double HeightDip => HeightMm * DipPerMm;
        public double ContentWidthDip => (WidthMm - LeftMm - RightMm) * DipPerMm;
        public double ContentHeightDip => (HeightMm - TopMm - BottomMm) * DipPerMm;
    }

    public static Dimensions Resolve(PrintTemplate template)
    {
        var (width, height) = template.PaperName switch
        {
            "A4" => (210d, 297d),
            "A5" => (148d, 210d),
            "A6" => (105d, 148d),
            "Letter" => (215.9d, 279.4d),
            "58mm" => (58d, 200d),
            "80mm" => (80d, 200d),
            "Tùy chỉnh" => (template.CustomWidthMm, template.CustomHeightMm),
            _ => throw new ArgumentException("Khổ giấy không được hỗ trợ.")
        };
        if (template.Landscape) (width, height) = (height, width);
        var d = new Dimensions(width, height,
            template.MarginTopMm ?? template.MarginMm,
            template.MarginBottomMm ?? template.MarginMm,
            template.MarginLeftMm ?? template.MarginMm,
            template.MarginRightMm ?? template.MarginMm);
        if (!double.IsFinite(width) || !double.IsFinite(height) ||
            width < 40 || width > 420 || height < 40 || height > 600)
            throw new ArgumentException("Khổ giấy phải nằm trong khoảng 40–420 mm rộng và 40–600 mm cao.");
        foreach (var margin in new[] { d.TopMm, d.BottomMm, d.LeftMm, d.RightMm })
            if (!double.IsFinite(margin) || margin < 2 || margin > 40)
                throw new ArgumentException("Lề giấy phải từ 2 đến 40 mm.");
        if (d.WidthMm - d.LeftMm - d.RightMm < 25 ||
            d.HeightMm - d.TopMm - d.BottomMm < 25)
            throw new ArgumentException("Lề quá lớn so với khổ giấy; vùng in cần rộng và cao ít nhất 25 mm.");
        if (template.Copies is < 1 or > 99)
            throw new ArgumentException("Số bản in phải từ 1 đến 99.");
        if (template.PrintScalePercent is < 30 or > 200)
            throw new ArgumentException("Tỷ lệ in phải từ 30% đến 200%.");
        if (template.PrinterName.Length > 250)
            throw new ArgumentException("Tên máy in quá dài.");
        return d;
    }
}
