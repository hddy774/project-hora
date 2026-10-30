using System.Diagnostics.CodeAnalysis;

namespace Chronicles.Game.Currency;

public enum CurrencyOriginKind { Genesis, PhysicalStone }

/// <summary>Creation evidence only. People/treasuries/escrows remain the sole owners of spendable balances.</summary>
public sealed class CurrencyLedger
{
    public bool Initialized { get; set; }
    public long NextReceiptId { get; set; } = 1;
    public List<CurrencyReceipt> Receipts { get; set; } = [];
}

public sealed record CurrencyReceipt(long Id, string CommandId, string Signature, CurrencyOriginKind Kind, long Month,
    long Person, long Body, long Soul, string Definition, long OriginalItem, long ConsumedItem, long ConsumptionSequence,
    long Quantity, long Rate, long Amount, int Quality, int Damage, ItemDefect Defects, long BoundSoul,
    long LawRevision, string Policy, string Source, IReadOnlyList<string> Provenance);

public sealed record CurrencyExchangeInfo(string Definition, string Name, long MoneyPerToken, string TokenUnit,
    string MoneyUnit, bool Enabled, string QualityPolicy);

/// <summary>Provisional developer valuation: denomination is independent of the common quality score.
/// A functional, defect-free resource.02.0 token has its declared face value; other crystals are commodities.</summary>
public static class CurrencyPolicy
{
    public const string StoneDefinition = "resource.02.0";
    public const string StonePolicy = "physical-stone-face-value.v1";
    public const string GenesisPolicy = "two-founder-account-opening.v1";

    public static bool Functional(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Definition == StoneDefinition && item.Quality is >= 0 and <= 10000 && item.Damage is >= 0 and < 100 && item.Defects == ItemDefect.None;
    }
    public static CurrencyExchangeInfo ExchangeInfo(long rate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rate);
        return new(StoneDefinition, "영석", rate, "실물 1개", "영석 잔액", rate > 0,
            "정상 기능 영석의 명목가입니다. 품질 점수는 가산하지 않으며 완전 파손·결손이 있는 실물은 전환하지 않습니다.");
    }

    public static void Validate(CurrencyLedger ledger, GameState world)
    {
        ArgumentNullException.ThrowIfNull(ledger); ArgumentNullException.ThrowIfNull(world);
        try { ValidateCore(ledger, world); }
        catch (OverflowException error) { throw new InvalidDataException("화폐 출처 합계가 허용 범위를 넘었습니다.", error); }
    }
    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    { if (!condition) throw new InvalidDataException(message); }
    private static void ValidateCore(CurrencyLedger ledger, GameState world)
    {
        Require(ledger.Receipts is not null && ledger.Receipts.All(r => r is not null), "화폐 원천 영수증 목록이 손상되었습니다.");
        Require(ledger.Initialized, "화폐 창세 출처가 초기화되지 않았습니다.");
        var founders = world.People.OrderBy(p => p.Id).Take(2).ToArray();
        Require(founders.Length == 2 && founders.All(p => p.ParentOne == 0 && p.ParentTwo == 0 && p.Born <= 0), "실제 두 시조의 화폐 출처가 필요합니다.");
        Require(ledger.Receipts.Select(r => r.Id).All(id => id > 0) && ledger.Receipts.Select(r => r.Id).Distinct().Count() == ledger.Receipts.Count &&
            ledger.NextReceiptId > ledger.Receipts.Select(r => r.Id).DefaultIfEmpty().Max(), "화폐 원천 영수증 식별자가 중복되었습니다.");
        var genesis = ledger.Receipts.Where(r => r.Kind == CurrencyOriginKind.Genesis).OrderBy(r => r.Person).ToArray();
        Require(genesis.Length == 2 && genesis.Select(r => r.Person).SequenceEqual(founders.Select(p => p.Id)), "창세 화폐를 실제 두 시조에게 한 번씩 연결해야 합니다.");
        var redemptions = ledger.Receipts.Where(r => r.Kind == CurrencyOriginKind.PhysicalStone).ToArray();
        Require(redemptions.All(r => !string.IsNullOrWhiteSpace(r.CommandId)) &&
            redemptions.Select(r => r.CommandId).Distinct(StringComparer.Ordinal).Count() == redemptions.Length &&
            redemptions.Select(r => r.ConsumptionSequence).Distinct().Count() == redemptions.Length, "환전 명령이나 실물 소비 증거가 재사용되었습니다.");
        var opening = world.LawHistory.Where(change => change.Key == "founder.initialMoney").OrderBy(change => change.Revision).FirstOrDefault()?.Before
            ?? world.Laws["founder.initialMoney"];
        long created = 0;
        foreach (var receipt in ledger.Receipts)
        {
            Require(receipt.CommandId is not null && receipt.Signature is not null && receipt.Definition is not null && receipt.Policy is not null, "화폐 영수증의 필수 값이 없습니다.");
            var person = world.People.SingleOrDefault(p => p.Id == receipt.Person);
            Require(person is not null && person.BodyId == receipt.Body && person.SoulId == receipt.Soul && receipt.Month >= 0 && receipt.Month <= world.Month &&
                receipt.LawRevision > 0 && receipt.LawRevision <= world.LawRevision && receipt.Amount >= 0 && receipt.Provenance is { Count: > 0 } &&
                !string.IsNullOrWhiteSpace(receipt.Source), "화폐 원천의 실제 신원·시간·출처 증거가 없습니다.");
            created = checked(created + receipt.Amount);
            if (receipt.Kind == CurrencyOriginKind.Genesis)
            {
                Require(receipt.Month == 0 && receipt.Definition.Length == 0 && receipt.OriginalItem == 0 && receipt.ConsumedItem == 0 && receipt.ConsumptionSequence == 0 &&
                    receipt.Quantity == opening && receipt.Rate == 1 && receipt.Amount == opening && receipt.Policy == GenesisPolicy &&
                    receipt.CommandId.Length == 0 && receipt.Signature.Length == 0, "창세 잔액의 원천이 실제 최초 법칙/자산과 다릅니다.");
                continue;
            }
            Require(receipt.Kind == CurrencyOriginKind.PhysicalStone && receipt.Policy == StonePolicy && receipt.Definition == StoneDefinition &&
                receipt.Quantity is > 0 and <= int.MaxValue && receipt.Rate > 0 && receipt.Amount == checked(receipt.Quantity * receipt.Rate) &&
                receipt.Quality is >= 0 and <= 10000 && receipt.Damage is >= 0 and < 100 && receipt.Defects == ItemDefect.None &&
                (receipt.BoundSoul == 0 || receipt.BoundSoul == receipt.Soul) && receipt.OriginalItem > 0 && receipt.ConsumedItem > 0 && receipt.ConsumptionSequence > 0 &&
                receipt.Signature.Length == 64 && receipt.Signature.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "실물 영석의 환전 수량·단위·평가 또는 명령 증거가 잘못되었습니다.");
            var audit = world.Inventory.Events.Where(e => e.Sequence == receipt.ConsumptionSequence).ToArray();
            Require(audit.Length == 1 && audit[0].Kind == "physical-consumption" && !audit[0].IsLoss && audit[0].Actor == receipt.Person &&
                audit[0].Owner == receipt.Person && audit[0].Subject == receipt.ConsumedItem && audit[0].Definition == StoneDefinition &&
                audit[0].Quantity == receipt.Quantity && audit[0].Month == receipt.Month && audit[0].ItemSource == receipt.Source &&
                audit[0].Quality == receipt.Quality && audit[0].Damage == receipt.Damage && audit[0].Defects == receipt.Defects,
                "환전 대금에 대응하는 실제 영석 소비가 없습니다.");
        }
        Require(created == world.CreatedMoney, "총 생성 화폐와 실제 창세/실물 소비 원천이 일치하지 않습니다.");
    }
}
