namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private void ResolveWorldConditions(Person p)
    {
        if (p.Realm == Realm.HalfImmortal && (int)p.Realm > Law("world.capacityRealm"))
        {
            EndLife(p, 12, "계역 수용 법칙 초과·반선 배척/복귀 불가", LifeStatus.Departed); return;
        }
        if (p.Realm >= Realm.Integration && p.GreatPerfection && (int)p.Realm > Law("world.capacityRealm"))
        {
            EndLife(p, 7, "계역 수용 한계·실제 세계 이탈", LifeStatus.Departed); return;
        }
        if (p.Sealed && p.SealedUntil <= Month)
        {
            var recovery = Owned(p.Id).FirstOrDefault(i => i.Definition == "array" && i.Quantity > 0 && i.Reserved == 0 && i.Quality >= 1000);
            if (recovery is not null)
            {
                Material(recovery.Definition).Consumption++;
                recovery.Quantity--; if (recovery.Quantity == 0) Remove(recovery);
                p.Sealed = false;
                Event("봉인 해제", p.Id, 0, $"{p.Name} 실제 진기로 귀환 경로 복구", recovery.Source);
            }
            else { EndLife(p, 4, "장기 금제·육신 단절·알려진 귀환 수단 소진"); return; }
        }
        if (p.SoulPactUntil > 0 && p.SoulPactUntil <= Month)
        {
            p.SoulPactUntil = 0;
            if (p.Health < 20 && p.StructureDamage >= 90)
            { EndLife(p, 11, "보호 혼약 기한 도래·현생 유지 불가·" + p.SoulPactSource); return; }
            Event("혼약 만료", p.Id, 0, $"{p.Name} 양혼부 보호 종료·현생 존속", p.SoulPactSource);
        }
    }

}
