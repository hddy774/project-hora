namespace Chronicles.Game;

public sealed partial class GameWorld
{
    internal long EcologyFoodTarget(Person person) =>
        checked(EcologyMonthlyFoodNeed(person) * CurrentEcologyRules().FoodReserveMonths);

    private long EcologyMonthlyFoodNeed(Person person)
    {
        var children = dependents.GetValueOrDefault(person.Id) ?? [];
        var count = children.Count(p => p.Status == LifeStatus.Alive && p.Place == person.Place &&
            Month - p.Born < PeopleMaturityMonths(p));
        return checked(Law("ecology.foodUse") * (count + 1L));
    }
}
