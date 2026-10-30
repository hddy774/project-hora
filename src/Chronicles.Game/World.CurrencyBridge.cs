using Chronicles.Game.Currency;
using Chronicles.Game.Inventory;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private void InitializeCurrency()
    {
        if (state.Currency.Initialized) { ValidateCurrency(); return; }
        if (Month != 0 || state.People.Count != 2 || state.People.Any(p => p.Realm != Realm.HalfImmortal || p.ParentOne != 0 || p.ParentTwo != 0) ||
            state.Currency.Receipts.Count != 0 || state.Currency.NextReceiptId != 1 || state.People.Sum(p => p.Money) != state.CreatedMoney)
            throw new InvalidDataException("화폐 창세는 두 반선 시조의 실제 초기 잔액에 한 번만 연결할 수 있습니다.");
        foreach (var person in state.People.OrderBy(p => p.Id))
        {
            var source = $"창세 초기 화폐·실제 시조 {person.Id}·육신 {person.BodyId}·혼 {person.SoulId}";
            state.Currency.Receipts.Add(new(state.Currency.NextReceiptId++, "", "", CurrencyOriginKind.Genesis, 0,
                person.Id, person.BodyId, person.SoulId, "", 0, 0, 0, person.Money, 1, person.Money, 0, 0, ItemDefect.None, 0,
                state.LawRevision, CurrencyPolicy.GenesisPolicy, source, [source]));
        }
        state.Currency.Initialized = true; ValidateCurrency();
    }

    private bool TryCurrencyCommand(GameCommand command, Person actor, out string? error)
    {
        error = null; if (command.Kind != "redeemCurrency") return false;
        try
        {
            ValidateCurrency();
            var signature = GameCodec.CommandHash(command);
            var prior = state.Currency.Receipts.SingleOrDefault(r => r.Kind == CurrencyOriginKind.PhysicalStone && r.CommandId == command.Id);
            if (prior is not null) { error = prior.Signature == signature ? null : "같은 환전 명령 식별자의 내용이 다릅니다."; return true; }
            var rate = Law("currency.stoneValue");
            if (rate == 0) { error = "현재 실물 영석의 잔액 전환이 비활성화되어 있습니다."; return true; }
            if (!InventoryAccessible(actor, command.Target, out var item) || !CurrencyPolicy.Functional(item))
            { error = "현지에서 접근 가능한 본인 소유의 정상 영석 실물이 필요합니다."; return true; }
            var quantity = command.Value == 0 ? 1 : checked((int)command.Value);
            if (quantity <= 0 || quantity > item.Quantity) { error = "전환할 실제 영석 수량을 확인하세요."; return true; }
            var amount = checked((long)quantity * rate); var balance = checked(actor.Money + amount); var created = checked(state.CreatedMoney + amount);
            if (string.IsNullOrWhiteSpace(item.Source)) { error = "실물 영석의 생성 출처가 필요합니다."; return true; }
            var nextReceiptId = checked(state.Currency.NextReceiptId + 1);
            var quality = item.Quality; var damage = item.Damage; var defects = item.Defects; var soul = item.BoundSoul; var source = item.Source;
            var provenance = state.Inventory.Metadata.FirstOrDefault(m => m.Item == item.Id)?.Provenance.ToList() ?? [];
            if (!provenance.Contains(source, StringComparer.Ordinal)) provenance.Add(source);
            var reserved = InventoryEngine.Apply(CurrentInventory(), new ReserveInventoryPhysical(command.Id + ":reserve", actor.Id, "currency-redemption", item.Id, quantity));
            if (!reserved.Accepted) { error = reserved.Message; return true; }
            var physical = reserved.State.Ledger.Reservations.Single(r => r.Id == reserved.Subject).Items.Single();
            var consumed = InventoryEngine.Apply(reserved.State, new ConsumeInventoryAssets(command.Id + ":consume", actor.Id, reserved.Subject));
            if (!consumed.Accepted) { error = consumed.Message; return true; }
            var evidence = consumed.State.Ledger.Events.Last(e => e.Kind == "physical-consumption" && e.Subject == physical && e.Actor == actor.Id);
            var receipt = new CurrencyReceipt(state.Currency.NextReceiptId, command.Id, signature, CurrencyOriginKind.PhysicalStone, Month,
                actor.Id, actor.BodyId, actor.SoulId, CurrencyPolicy.StoneDefinition, item.Id, physical, evidence.Sequence,
                quantity, rate, amount, quality, damage, defects, soul, state.LawRevision, CurrencyPolicy.StonePolicy, source, [.. provenance]);
            ApplyInventory(consumed.State); actor.Money = balance; state.CreatedMoney = created;
            state.Currency.Receipts.Add(receipt); state.Currency.NextReceiptId = nextReceiptId;
            ValidateCurrency();
            Event("환전", actor.Id, physical, $"실물 영석 {quantity}개 → 잔액 {amount}·단위 {rate}", $"currency:{receipt.Id}");
        }
        catch (OverflowException) { error = "환전 수량이나 잔액이 허용 범위를 넘었습니다."; }
        return true;
    }

    private CurrencyExchangeInfo CurrencyExchangeInfo() => CurrencyPolicy.ExchangeInfo(Law("currency.stoneValue"));
    private void ValidateCurrency() => CurrencyPolicy.Validate(state.Currency, state);
}
