
namespace AlphaExchange.Core;

public sealed class Abilities
{
    public int Valuation { get; set; }
    public int Technical { get; set; }
    public int News { get; set; }
    public int RiskManagement { get; set; }
    public int Diversification { get; set; }
    public int Execution { get; set; }
    public int SwingTrading { get; set; }
    public int Scalping { get; set; }
    public int Macro { get; set; }
    public int Negotiation { get; set; }
    public int[] Values() => [Valuation, Technical, News, RiskManagement, Diversification, Execution, SwingTrading, Scalping, Macro, Negotiation];
    public int Get(AbilityKind kind) => kind switch
    {
        AbilityKind.Valuation => Valuation,
        AbilityKind.Technical => Technical,
        AbilityKind.News => News,
        AbilityKind.RiskManagement => RiskManagement,
        AbilityKind.Diversification => Diversification,
        AbilityKind.Execution => Execution,
        AbilityKind.SwingTrading => SwingTrading,
        AbilityKind.Scalping => Scalping,
        AbilityKind.Macro => Macro,
        AbilityKind.Negotiation => Negotiation,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    public void Set(AbilityKind kind, int value)
    {
        switch (kind)
        {
            case AbilityKind.Valuation: Valuation = value; break;
            case AbilityKind.Technical: Technical = value; break;
            case AbilityKind.News: News = value; break;
            case AbilityKind.RiskManagement: RiskManagement = value; break;
            case AbilityKind.Diversification: Diversification = value; break;
            case AbilityKind.Execution: Execution = value; break;
            case AbilityKind.SwingTrading: SwingTrading = value; break;
            case AbilityKind.Scalping: Scalping = value; break;
            case AbilityKind.Macro: Macro = value; break;
            case AbilityKind.Negotiation: Negotiation = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
    public static Abilities Uniform(int value)
    { var result = new Abilities(); for (int i = 0; i < 10; i++) result.Set((AbilityKind)i, value); return result; }
}
