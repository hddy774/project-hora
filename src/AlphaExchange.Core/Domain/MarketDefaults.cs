namespace AlphaExchange.Core;

// Shared initial dimensions. Live markets may grow; use state collection counts
// during execution, and these constants only when constructing initial state.
public static class MarketDefaults
{
    public const int InstitutionCount = 100;
    public const int StockCount = 30;
    public const int SeasonDays = 30;
}
