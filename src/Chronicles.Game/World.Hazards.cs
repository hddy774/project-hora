namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private void ResolveWorldConditions(Person p)
    {
        // Read actual simultaneous conditions before applying any recovery/end transition.
        // Order is the existing world-condition priority; eligibility never changes the physical outcome.
        var matches = new List<LifeEndMatch>();
        if (p.Realm == Realm.HalfImmortal && (int)p.Realm > Law("world.capacityRealm"))
            matches.Add(new(12, "계역 수용 법칙 초과·반선 배척/복귀 불가"));
        if (p.Realm >= Realm.Integration && p.GreatPerfection && (int)p.Realm > Law("world.capacityRealm"))
            matches.Add(new(7, "계역 수용 한계·실제 세계 이탈"));
        var sealExpired = p.Sealed && p.SealedUntil <= Month;
        var recovery = sealExpired ? Owned(p.Id).FirstOrDefault(i => i.Definition == "array" && i.Quantity > 0 &&
            i.Reserved == 0 && i.Quality >= 1000) : null;
        if (sealExpired && recovery is null)
            matches.Add(new(4, "장기 금제·육신 단절·알려진 귀환 수단 소진"));
        var pactExpired = p.SoulPactUntil > 0 && p.SoulPactUntil <= Month;
        if (pactExpired && p.Health < 20 && p.StructureDamage >= 90)
            matches.Add(new(11, "보호 혼약 기한 도래·현생 유지 불가·" + p.SoulPactSource));
        if (matches.FirstOrDefault() is { Cause: 12 or 7 } capacity)
        {
            EndLife(p, capacity.Cause, capacity.Evidence, LifeStatus.Departed, matches); return;
        }
        if (sealExpired)
        {
            if (recovery is not null)
            {
                Material(recovery.Definition).Consumption++;
                recovery.Quantity--; if (recovery.Quantity == 0) Remove(recovery);
                p.Sealed = false;
                Event("봉인 해제", p.Id, 0, $"{p.Name} 실제 진기로 귀환 경로 복구", recovery.Source);
            }
            else { EndLife(p, 4, matches[0].Evidence, actualMatches: matches); return; }
        }
        if (pactExpired)
        {
            p.SoulPactUntil = 0;
            if (p.Health < 20 && p.StructureDamage >= 90)
            { EndLife(p, 11, matches[0].Evidence, actualMatches: matches); return; }
            Event("혼약 만료", p.Id, 0, $"{p.Name} 양혼부 보호 종료·현생 존속", p.SoulPactSource);
        }
    }

}
