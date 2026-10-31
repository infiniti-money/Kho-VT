using HisaWarehouse.Services;

var scratch = Path.Combine(Path.GetTempPath(), "hisa-writeguard-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scratch);
var path = Path.Combine(scratch, "KHO.xlsx");
var ownerFile = Path.Combine(scratch, "~$KHO.xlsx");
var checks = 0;

void Expect(WorkbookWriteStatus expected, string label)
{
    var actual = WorkbookWriteSafety.Inspect(path).Status;
    if (actual != expected)
        throw new Exception($"{label}: expected {expected}, actual {actual}");
    Console.WriteLine("OK " + label);
    checks++;
}

try
{
    Expect(WorkbookWriteStatus.Missing, "missing workbook");
    File.WriteAllText(path, "smoke test - real xlsx content not necessary for lock tests");
    Expect(WorkbookWriteStatus.Available, "free workbook");

    using (var otherWriter = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        Expect(WorkbookWriteStatus.BusyOrDenied, "exclusive writer blocks HISA");

    File.WriteAllText(ownerFile, "Microsoft Excel simulated owner file");
    Expect(WorkbookWriteStatus.ExcelLockPresent, "Excel owner file blocks HISA");
    File.Delete(ownerFile);

    File.SetAttributes(path, FileAttributes.ReadOnly);
    Expect(WorkbookWriteStatus.ReadOnly, "read-only workbook blocks HISA");
    File.SetAttributes(path, FileAttributes.Normal);

    var wrong = WorkbookWriteSafety.Inspect(Path.Combine(scratch, "KHO.xls"));
    if (wrong.Status != WorkbookWriteStatus.InvalidFormat)
        throw new Exception("Invalid Excel extension allowed unexpectedly.");
    checks++;
    Console.WriteLine("OK rejects non-xlsx");

    Console.WriteLine($"SUCCESS: {checks} workbook write-safety checks.");
}
finally
{
    if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
    Directory.Delete(scratch, recursive: true);
}
