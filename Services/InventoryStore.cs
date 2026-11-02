using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.Web.WebView2.Core;

namespace HisaWarehouse.Services;

public static class InventoryStore
{
    private const string StorageKey = "hisa.warehouse.state.v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async Task<InventoryState> LoadOrSeedAsync(CoreWebView2 storageCore)
    {
        var encoded = await storageCore.ExecuteScriptAsync($"localStorage.getItem({JsonSerializer.Serialize(StorageKey)});");
        var json = JsonSerializer.Deserialize<string>(encoded);
        if (!string.IsNullOrWhiteSpace(json))
        {
            var saved = JsonSerializer.Deserialize<InventoryState>(json, JsonOptions);
            if (saved is not null)
            {
                var needsMigration = saved.AppVersion < 3;
                if (needsMigration) MigrateCatalogs(saved);
                Normalize(saved);
                if (needsMigration) await SaveAsync(storageCore, saved);
                return saved;
            }
        }

        var seedPath = Path.Combine(AppContext.BaseDirectory, "data", "initial_data.json");
        if (!File.Exists(seedPath))
            throw new FileNotFoundException("Không tìm thấy dữ liệu khởi tạo KHO.xlsx.", seedPath);

        var seeded = JsonSerializer.Deserialize<InventoryState>(await File.ReadAllTextAsync(seedPath), JsonOptions)
            ?? throw new InvalidDataException("Tệp dữ liệu khởi tạo không đọc được.");
        seeded.SelectedWarehouses = [.. seeded.Warehouses];
        Normalize(seeded);
        await SaveAsync(storageCore, seeded);
        return seeded;
    }

    // Serialize writes so two fast UI operations cannot overwrite each other out of order.
    private static readonly SemaphoreSlim SaveGate = new(1, 1);

    public static async Task SaveAsync(CoreWebView2 storageCore, InventoryState state)
    {
        await SaveGate.WaitAsync();
        try
        {
            Normalize(state);
            var json = JsonSerializer.Serialize(state, JsonOptions);
            var key = JsonSerializer.Serialize(StorageKey);
            var value = JsonSerializer.Serialize(json);

            // ExecuteScriptAsync may return "null" when JavaScript fails, without throwing
            // a .NET exception (e.g., when the browser's storage quota is exceeded).
            // Explicitly check that the new data was saved and can be read back.
            var script = $"(() => {{ try {{ localStorage.setItem({key}, {value}); return localStorage.getItem({key}) === {value}; }} catch {{ return false; }} }})()";
            var result = await storageCore.ExecuteScriptAsync(script);
            if (!string.Equals(result, "true", StringComparison.Ordinal))
                throw new IOException("Không ghi được dữ liệu kho vào bộ nhớ máy. Có thể bộ nhớ đã đầy. Hãy xuất bản sao lưu JSON và kiểm tra dung lượng lưu trữ.");
        }
        finally
        {
            SaveGate.Release();
        }
    }

    public static InventoryState? ReadBackup(string path)
    {
        var state = JsonSerializer.Deserialize<InventoryState>(File.ReadAllText(path), JsonOptions);
        if (state is null) return null;
        if (state.AppVersion < 3) MigrateCatalogs(state);
        Normalize(state);
        return state;
    }

    public static string SerializeBackup(InventoryState state) => JsonSerializer.Serialize(state, new JsonSerializerOptions(JsonOptions) { WriteIndented = true });

    private static void Normalize(InventoryState state)
    {
        state.Warehouses ??= [];
        state.Categories ??= [];
        state.Units ??= [];
        state.Departments ??= [];
        state.Recipients ??= [];
        state.SelectedWarehouses ??= [];
        state.Products ??= [];
        state.Transactions ??= [];
        state.PendingMovements ??= [];
        state.MaterialRequests ??= [];
        state.ReferencePath ??= "";
        state.SourceAudit ??= new SourceAudit();
        foreach (var item in state.Products)
        {
            item.WarehouseStocks ??= new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var warehouse in state.Warehouses)
                item.WarehouseStocks.TryAdd(warehouse, 0);
            if (string.IsNullOrWhiteSpace(item.Barcode)) item.Barcode = item.Sku;
        }
        state.Recipients = state.Recipients.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase).ToList();
        state.Departments = state.Departments.Concat(state.Products.Select(p => p.Department))
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase).ToList();
        PrintTemplateService.EnsureDefaults(state);
        state.AppVersion = 3;
    }

    private static void MigrateCatalogs(InventoryState state)
    {
        state.Products ??= [];
        state.Transactions ??= [];
        state.Departments ??= [];
        state.Recipients ??= [];
        var seedPath = Path.Combine(AppContext.BaseDirectory, "data", "initial_data.json");
        if (!File.Exists(seedPath)) return;
        try
        {
            var seed = JsonSerializer.Deserialize<InventoryState>(File.ReadAllText(seedPath), JsonOptions);
            if (seed is null) return;
            var seedBySku = seed.Products.ToDictionary(p => p.Sku, StringComparer.OrdinalIgnoreCase);
            foreach (var item in state.Products)
                if (string.IsNullOrWhiteSpace(item.Department) && seedBySku.TryGetValue(item.Sku, out var source)) item.Department = source.Department;
            if (state.Departments.Count == 0) state.Departments = [.. seed.Departments];
        }
        catch { /* Keep the user's saved inventory if optional migration data is unavailable. */ }
        state.Departments ??= [];
        state.Recipients ??= [];
        state.Recipients = state.Recipients.Concat(state.Transactions.Select(t => t.Partner))
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        state.AppVersion = 3;
    }
}
