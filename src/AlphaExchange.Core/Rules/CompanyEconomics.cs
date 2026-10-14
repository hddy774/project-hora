namespace AlphaExchange.Core;

public readonly record struct EconomicExposure(double Demand, double Policy, double Financing, double Inflation);

public static class CompanyEconomics
{
    public static EconomicExposure For(string symbol) => symbol switch
    {
        "NOVA" => new(1.35, .9, 1.2, .7),
        "AURA" => new(1.15, 1.25, 1.3, .8),
        "CLUD" => new(.85, .6, .9, 1.2),
        "VOLT" => new(.7, 1.5, 1.5, .6),
        "ORBT" => new(1.4, 1.7, 1.4, 1.0),
        "GRID" => new(.35, 1.2, 1.6, .4),
        "HELX" => new(1.1, 1.35, 1.7, .6),
        "CARE" => new(.4, .9, .5, 1.1),
        "PHAR" => new(.6, 1.1, .7, .8),
        "MINT" => new(1.2, .8, .6, .4),
        "BNKR" => new(.8, .7, -.7, -.2),
        "INSR" => new(.5, 1.0, -.3, 1.3),
        "FOOD" => new(.3, .5, .4, 1.6),
        "WAVE" => new(1.4, .6, .8, .8),
        "GAME" => new(1.5, 1.4, 1.1, .5),
        _ => throw new ArgumentException("회사 경제 노출 없음", nameof(symbol))
    };
}
