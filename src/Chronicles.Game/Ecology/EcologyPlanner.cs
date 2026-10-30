using Chronicles.Game.Inventory;

namespace Chronicles.Game.Ecology;

public static class EcologyPlanner
{
    public static EcologyPlan? Decide(EcologyDecisionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var available = input.Stock.Where(s => s.Available && !s.Equipped).ToArray();
        var food = available.Where(s => s.Definition == "food").Sum(s => (long)s.Quantity);
        var target = checked(input.FoodNeed * input.Rules.FoodReserveMonths);
        if (food < target)
        {
            var offer = input.Listings.Where(l => l.Kind == "sale" && l.Place == input.Place && l.Seller != input.Person &&
                l.Definition == "food" && l.Quantity > 0 && l.Price <= input.Money && l.Until > input.Month &&
                l.Quantity <= Math.Max(input.FoodNeed, input.FreeCarryWeight)).OrderBy(l => (decimal)l.Price / l.Quantity).ThenBy(l => l.Contract).FirstOrDefault();
            if (offer is not null) return new("trade", offer.Contract, offer.Quantity, offer.Price, "food", "", "식량 구매",
                $"{offer.Source}:observed:{offer.Observed}:own-food:{food}:own-money:{input.Money}");
            var recipe = input.Recipes.Where(r => r.Output == "food" && r.Inputs.All(material =>
                available.Where(s => s.Definition == material.Key).Sum(s => (long)s.Quantity) >= material.Value))
                .OrderBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
            if (recipe is not null) return new("craft", 0, recipe.Quantity, 0, "food", recipe.Id, "실제 재료로 식량 생산", $"own-known-recipe:{recipe.Id}");
            var knownFood = input.Sources.Where(s => s.Place == input.Place && s.Definition == "food").OrderByDescending(s => s.Observed).FirstOrDefault();
            if (knownFood is null || knownFood.Source == 0 || knownFood.ObservedRemaining is null)
                return new("exploreSource", 0, 0, 0, "food", "", "식량 원천의 실제 관측", "식량별 개인 관측이 없어 현지 식량만 유료 탐색");
            if (knownFood.ObservedRemaining > 0)
                return new("gather", knownFood.Source, 0, 0, "food", "", "현지 식량 확보", knownFood.SourceEvidence);
            var alternative = input.Places.Where(p => p.Adjacent && !p.KnownBlocked)
                .OrderBy(p => p.Observed).ThenBy(p => p.Place).FirstOrDefault();
            if (alternative is not null) return new("travel", alternative.Place, 0, 0, "food", "", "알려진 인접지의 식량 정보 확인",
                $"personal-place:{alternative.Place}:observed:{alternative.Observed}:food-availability-unknown");
            return null; // No money, usable inputs or known source does not create a supplier or recovery event.
        }
        foreach (var need in input.WorkNeeds ?? [])
        {
            var own = available.Where(s => s.Definition == need.Definition).Sum(s => (long)s.Quantity);
            if (own >= need.Quantity) continue;
            var inputOffer = input.Listings.Where(l => l.Kind == "sale" && l.Place == input.Place && l.Seller != input.Person &&
                l.Definition == need.Definition && l.Quantity >= need.Quantity - own && l.Price <= input.Money && l.Until > input.Month &&
                (long)l.Quantity * InventoryDefinitions.Definition(l.Definition).Item.Weight <= input.FreeCarryWeight)
                .OrderBy(l => (decimal)l.Price / l.Quantity).ThenBy(l => l.Contract).FirstOrDefault();
            if (inputOffer is not null) return new("trade", inputOffer.Contract, inputOffer.Quantity, inputOffer.Price, need.Definition,
                need.Recipe, "생산재 매입", $"own-planned-recipe:{need.Recipe}:{inputOffer.Source}:observed:{inputOffer.Observed}");
        }
        foreach (var item in available.Where(s => s.Saleable && s.Definition is "food" or "herb" or "ore" or "wood").OrderBy(s => s.Item))
        {
            if (input.Listings.Any(l => l.Seller == input.Person && l.Definition == item.Definition && l.Until > input.Month)) continue;
            var reserve = item.Definition == "food" ? target : 2;
            var surplus = available.Where(s => s.Definition == item.Definition).Sum(s => (long)s.Quantity) - reserve;
            var quantity = checked((int)Math.Min(item.Quantity, Math.Min(input.Rules.MaximumSaleUnits, surplus)));
            if (quantity <= 0) continue;
            var observed = input.PublicPrices.Where(p => p.Place == input.Place && p.Definition == item.Definition && p.Month <= input.Month).OrderByDescending(p => p.Month).FirstOrDefault();
            var current = input.Listings.Where(l => l.Place == input.Place && l.Definition == item.Definition && l.Quantity > 0 && l.Until > input.Month)
                .OrderBy(l => (decimal)l.Price / l.Quantity).FirstOrDefault();
            var unitMilli = current is not null ? (decimal)current.Price * 1000 / current.Quantity :
                observed?.PaidPerUnitMilli ?? observed?.AskPerUnitMilli ?? checked(input.Rules.DefaultUnitPrice * 1000);
            var quality = item.KnownQuality is { } known ? (decimal)(known + 1000) / 6000 : 1;
            var competition = current is not null ? 0.9m : observed is { TradedQuantity: > 0, PublicSupply: 0 } ? 1.1m : 1m;
            var quoted = unitMilli * quantity * quality * competition / 1000;
            var price = (long)Math.Min(long.MaxValue, Math.Max(1, decimal.Ceiling(quoted)));
            return new("offer", item.Item, quantity, price, item.Definition, "", "실제 잉여품 교환",
                $"own-item:{item.Item}:own-surplus:{surplus}:quality:{item.KnownQuality?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}:public-reference:{current?.Source ?? "recorded-local-prices"}");
        }
        return null;
    }
}
