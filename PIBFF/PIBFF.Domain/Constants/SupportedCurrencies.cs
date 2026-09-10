namespace PIBFF.Domain.Constants;

/// <summary>
/// Curated allow-list of currency codes accepted from partners.
/// </summary>
public static class SupportedCurrencies
{
    public static readonly IReadOnlySet<string> Codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF", "CNY",
        "HKD", "NZD", "SEK", "KRW", "SGD", "NOK", "MXN", "INR",
        "VND", "THB", "MYR", "IDR", "PHP", "ZAR", "BRL", "AED"
    };

    public static bool IsSupported(string? currencyCode) =>
        !string.IsNullOrWhiteSpace(currencyCode) && Codes.Contains(currencyCode);
}
