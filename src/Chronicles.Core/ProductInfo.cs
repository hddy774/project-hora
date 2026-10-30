using System.Reflection;

namespace Chronicles.Core;

public static class ProductInfo
{
    public const string KoreanTitle = "선로만상록";
    public const string EnglishTitle = "Chronicles of Myriad Immortal Paths";
    public static int DefaultPopulation => 2;
    public static string Version =>
        typeof(ProductInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
        ?? "0.0.0";
}
