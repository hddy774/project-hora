namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private void BindSlot(Person person)
    {
        var index = state.ActiveSlots.FindIndex(slot => slot.Person == 0);
        if (index < 0) { index = state.ActiveSlots.Count; state.ActiveSlots.Add(new()); }
        var slot = state.ActiveSlots[index];
        slot.Person = person.Id; person.Slot = index; person.SlotGeneration = slot.Generation;
    }

    private void ReleaseSlot(Person person)
    {
        var slot = state.ActiveSlots[person.Slot];
        if (slot.Person != person.Id || slot.Generation != person.SlotGeneration)
            throw new InvalidDataException("인구 슬롯 세대가 다릅니다.");
        var nextGeneration = checked(slot.Generation + 1);
        ForgetExecutionPerson(person);
        slot.Person = 0; slot.Generation = nextGeneration;
    }

    public long ResolveSlot(int slot, uint generation) => slot >= 0 && slot < state.ActiveSlots.Count &&
        state.ActiveSlots[slot].Generation == generation ? state.ActiveSlots[slot].Person : 0;
}
