using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    // Retail has no individual charts, representative or ranking snapshots. Omit
    // those copies and default-valued fields; retain every non-default cash-flow,
    // debt and position needed for exact continuation and reconciliation.
    internal static readonly JsonSerializerOptions StateJson = CreateStateJson();
    static JsonSerializerOptions CreateStateJson()
    {
        var defaults=new Trader();
        var resolver=new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info=>
        {
            if(info.Type==typeof(GameState))
            {
                info.Properties.Single(p=>p.Name==nameof(GameState.Retail)).CustomConverter=new RetailColumnsConverter();
                return;
            }
            if(info.Type!=typeof(Trader)) return;
            foreach(var property in info.Properties)
            {
                string name=property.Name; object? baseline=property.Get?.Invoke(defaults);
                bool retailUnused=name is "OpeningSnapshot" or "SeasonSnapshot" or "EquityHistory" or "SeasonRanks" or
                    "Decision" or "ActiveMethods" or "LastAction";
                bool bankAlias=name is "Cash" or "Shares" or "ReservedCash" or "ReservedShares" or "ShortShares" or "ReservedCovers";
                property.ShouldSerialize=(owner,value)=>
                {
                    var t=(Trader)owner;
                    if(t.Id==0 && bankAlias) return false;
                    if(!t.IsRetail) return true;
                    if(retailUnused) return false;
                    if(value is long[] longs) return Array.Exists(longs,v=>v!=0);
                    if(value is double[] doubles) return Array.Exists(doubles,v=>v!=0);
                    if(value is Abilities a) return a.Values().Any(v=>v!=0);
                    return !Equals(value,baseline);
                };
            }
        });
        return new JsonSerializerOptions { TypeInfoResolver=resolver };
    }
    static void NormalizeCompactTrader(Trader t,int count)
    {
        if(!t.IsRetail) return;
        static T[] Zeros<T>(T[] values,int size) where T:struct
            => values.Length==size ? values : values.All(v=>v.Equals(default(T))) ? new T[size] : values;
        t.Shares=Zeros(t.Shares,count); t.AverageCost=Zeros(t.AverageCost,count);
        t.ShortShares=Zeros(t.ShortShares,count); t.ShortAveragePrice=Zeros(t.ShortAveragePrice,count);
        t.ReservedShares=Zeros(t.ReservedShares,count); t.ReservedCovers=Zeros(t.ReservedCovers,count);
    }
}
