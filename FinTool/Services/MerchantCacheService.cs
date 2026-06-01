namespace FinTool.Services;

public class MerchantCacheService(LocalStorageService storage)
{
    private const string Key = "fintool_merchant_cache";
    private Dictionary<string, string>? _cache;   // normalised description → category name

    public async Task<string?> GetCategoryAsync(string description)
    {
        _cache ??= await storage.GetAsync<Dictionary<string, string>>(Key) ?? [];
        return _cache.GetValueOrDefault(Normalise(description));
    }

    public async Task SetCategoryAsync(string description, string category)
    {
        _cache ??= await storage.GetAsync<Dictionary<string, string>>(Key) ?? [];
        _cache[Normalise(description)] = category;
        await storage.SetAsync(Key, _cache);
    }

    private static string Normalise(string s) => s.Trim().ToUpperInvariant();
}
