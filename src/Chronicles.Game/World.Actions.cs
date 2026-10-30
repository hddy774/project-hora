using System.Globalization;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private void Regenerate()
    {
        foreach (var place in state.Places)
        {
            var regeneration = IntLaw("ecology.regrowth");
            var cap = IntLaw("ecology.sourceCapacity");
            if (!state.Inventory.Initialized) place.Source = (int)Math.Min(cap, (long)place.Source + regeneration);
            place.Qi = (int)Math.Min(cap, (long)place.Qi + regeneration);
            if (place.Facilities.Contains("수행 진법") && place.Qi > 0) place.Qi--;
            if (Month % 12 == 0 && Roll(10000) < Law("ecology.disasterChance"))
            {
                place.Source /= 2; place.Qi /= 2; place.BlockedUntil = Month + 3;
                foreach (var p in state.People.Where(p => p.Status == LifeStatus.Alive && p.Place == place.Id))
                {
                    p.Health = Math.Max(0, p.Health - 10);
                    if (p.Health == 0) EndLife(p, 3, "재해로 육신 붕괴");
                }
                Event("재해", 0, place.Id, $"{place.Name} 자원 감소·통로 임시 봉쇄", "실제 지역 재해");
            }
        }
        if (Month % 12 == 0)
            foreach (var item in state.Items.Where(i => i.Definition == "food" && i.Reserved == 0).ToArray())
            {
                var lost = item.Quantity * IntLaw("ecology.spoilage") / 100;
                RecordInventoryLoss(item, lost);
                Material(item.Definition).Loss += lost; item.Quantity -= lost;
                if (item.Quantity == 0) Remove(item);
            }
    }

    private void Maintain(Person p)
    {
        var foodNeed = IntLaw("ecology.foodUse");
        var provider = p;
        if (Inventory(p.Id, "food") < foodNeed && p.Guardian != 0 && people.TryGetValue(p.Guardian, out var guardian) &&
            guardian.Status == LifeStatus.Alive && ShareExplorationSpace(guardian, p)) provider = guardian;
        if (Inventory(provider.Id, "food") >= foodNeed)
        {
            Consume(provider, "food", foodNeed);
            p.Health = Math.Min(100, p.Health + IntLaw("cultivation.recovery"));
        }
        else
        {
            p.Health = Math.Max(0, p.Health - 2);
            if (p.Health == 0) { EndLife(p, 3, "실제 식량 부족·육신 붕괴"); return; }
        }
        foreach (var key in p.Beliefs.Where(x => x.Value.Expires <= Month).Select(x => x.Key).ToArray()) p.Beliefs.Remove(key);
        p.Mood -= Math.Sign(p.Mood);
        if (Month - p.Born < Law("care.age") && Month - p.LastCare > Law("care.interval") * 2)
        {
            p.Health = Math.Max(1, p.Health - 1);
            p.Reason = "양육 노동 부족·실제 보호자 필요";
        }
        p.Traits.RemoveAll(t => t.Expires <= Month);
    }

    private void Complete(Person p)
    {
        var completed = p.Action;
        if (CompleteExploration(p))
        { if (p.Action == ActionKind.Idle) RecordCompletedHabit(p, completed); return; }
        if (CompletePeopleAction(p) || CompleteCultivation(p) || CompleteKnowledge(p) || CompleteInventory(p))
        { RecordCompletedHabit(p, completed); return; }
        var action = p.Action; var target = p.ActionTarget;
        if (action is not (ActionKind.Travel or ActionKind.Explore or ActionKind.Heal or ActionKind.Care or ActionKind.Guard or ActionKind.Wait) ||
            IsTypedActionMarker(p.ActionData) && !p.ActionData.StartsWith("combat:", StringComparison.Ordinal))
            throw new InvalidDataException("A completed action has no matching authoritative owner.");
        RecordCompletedHabit(p, action);
        p.Action = ActionKind.Idle; p.Due = 0;
        var place = state.Places[p.Place];
        switch (action)
        {
            case ActionKind.Travel:
                if (p.Realm >= Realm.Mahayana && p.Place / 10 != target / 10 && Math.Max(p.Place, target) >= 90 &&
                    Roll(10000) < Law("hazard.portalChance"))
                    EndLife(p, 10, "실제 계역 통로 붕괴·귀환/육신 유지 불가");
                else if (state.Places[(int)target].BlockedUntil <= Month)
                {
                    state.Places[p.Place].PeopleRevision++; p.Place = (int)target;
                    state.Places[p.Place].PeopleRevision++; p.LastPeopleRevision = -1;
                    RefreshPopulationIndices(); Observe(p);
                }
                else Remember(p, $"blocked:{target}", "통로 봉쇄", "실제 이동 실패", 100);
                break;
            case ActionKind.Explore:
                foreach (var id in place.Neighbors) if (!p.KnownPlaces.Contains(id)) p.KnownPlaces.Add(id);
                p.KnownPlaces.Sort();
                Observe(p, true);
                DiscoverExplorationEntrance(p);
                if (place.Id % 10 == 8 && p.Realm >= Realm.Void && Roll(10000) < Law("hazard.sealChance"))
                {
                    p.Sealed = true; p.SealedUntil = Month + Law("hazard.sealDuration");
                    Event("고대 금제", p.Id, place.Id, $"{p.Name} 고대 금제에 봉인·실제 귀환 수단 필요", "실제 비경 탐색");
                }
                if (place.DiscoveryRemaining > 0)
                {
                    place.DiscoveryRemaining--;
                    AddItem(p.Id, "herb", 3, 6000, $"지점 {place.Id} 유한 탐색 보상", "extraction");
                    Event("발견", p.Id, place.Id, $"{p.Name} 숨은 약초 발견·지점 보상 소진", "실제 탐색");
                }
                break;
            case ActionKind.Heal: p.Health = Math.Min(100, p.Health + IntLaw("cultivation.recovery") * 2); break;
            case ActionKind.Care: CompleteCare(p, target); break;

        }
        if (p.Action == ActionKind.Idle) { p.ActionTarget = 0; p.ActionData = ""; }
    }

    private static void Gain(Person p, string skill, int amount) =>
        p.Skills[skill] = Math.Min(10000, p.Skills.GetValueOrDefault(skill) + amount);

    private void Remember(Person p, string key, string value, string source, int confidence)
    {
        p.Beliefs[key] = new(key, value, confidence, Month, Month + Law("ai.beliefLifetime"), source);
        while (p.Beliefs.Count > Law("ai.beliefBudget"))
        {
            var oldest = p.Beliefs.Values.OrderBy(b => b.Observed).ThenBy(b => b.Confidence).ThenBy(b => b.Key, StringComparer.Ordinal).First();
            p.Beliefs.Remove(oldest.Key);
        }
        TrimCognition(p);
    }

    private void Observe(Person p, bool force = false)
    {
        if (!p.KnownPlaces.Contains(p.Place)) { p.KnownPlaces.Add(p.Place); p.KnownPlaces.Sort(); }
        var local = (residents.GetValueOrDefault(p.Place) ?? []).Where(q => ShareExplorationSpace(p, q)).ToArray();
        var place = state.Places[p.Place];
        var observationBudget = IntLaw("ai.observationBudget");
        if (p.LastPeopleRevision != place.PeopleRevision)
        {
            p.LastPeopleRevision = place.PeopleRevision;
            p.ObservationRemaining = Math.Min(local.Length, Math.Max(1,
                IntLaw("ai.beliefBudget") - p.Beliefs.Count - p.ObservedPlaces.Count));
        }
        else if (force) p.ObservationRemaining = Math.Max(p.ObservationRemaining, Math.Min(local.Length, observationBudget));
        var count = Math.Min(p.ObservationRemaining, Math.Min(local.Length, observationBudget));
        for (var n = 0; n < count; n++)
        {
            var q = local[(int)((p.Id + p.ObservationCursor + n) % local.Length)];
            if (q.Status != LifeStatus.Alive || !ShareExplorationSpace(p, q)) continue;
            if (!p.KnownPeople.Contains(q.Id)) p.KnownPeople.Add(q.Id);
            p.ObservedPeople[q.Id] = new(q.Id, q.Name, q.Realm, q.Layer, q.Place, Month,
                q.Species == "human" ? q.Gestates : null, Month - q.Born >= PeopleMaturityMonths(q));
        }
        p.ObservationCursor += count; p.ObservationRemaining -= count;
        if (OccupiedExploration(p) is null)
            p.ObservedPlaces[p.Place] = new(place.Id, place.Source, place.Qi, string.Join(',', place.Facilities), Month,
                place.Sovereign, place.Owner, place.Operator, place.Delegate, place.BlockedUntil);
        TrimCognition(p);
    }

    private void TrimCognition(Person p)
    {
        TrimPlanningCognition(p);
    }

    private void Decide(Person p)
    {
        p.PlanExpansions = 0;
        p.DecisionCandidates.Clear();
        var age = Month - p.Born;
        if (age < 60) { p.Goal = "성장·양육"; return; }
        p.LessonRequests.RemoveAll(r => r.Expires <= Month || !people.TryGetValue(r.Pupil, out var pupil) || pupil.Status != LifeStatus.Alive);
        p.DecisionCandidates.Add(new("회복", p.Health < 25 ? 1200 : 0, $"자기 건강 {p.Health}·안전 선행 조건"));
        if (p.Health < 25) { p.Goal = "회복"; p.Reason = "자기 건강 부족"; Start(p, "heal", 0, ""); return; }
        var children = dependents.GetValueOrDefault(p.Id) ?? [];
        var childCount = children.Count(q => q.Status == LifeStatus.Alive && Month - q.Born < PeopleMaturityMonths(q));
        p.DecisionCandidates.Add(new("생계", Inventory(p.Id, "food") < (childCount + 1) * 12 ? 1000 : 0,
            "자기 재고와 실제 부양 가족 식량 수요"));
        if (Inventory(p.Id, "food") < (childCount + 1) * 12)
        {
            p.Goal = "가계 생계"; p.Reason = "자기 재고와 실제 부양 가족 수요";
            if (TryEcologyPlan(p)) return;
            if (p.Beliefs.TryGetValue($"depleted:{p.Place}", out var b) && b.Expires > Month)
            {
                var next = state.Places[p.Place].Neighbors.FirstOrDefault(id => p.KnownPlaces.Contains(id), -1);
                if (next >= 0 && Start(p, "travel", next, "") is null) return;
            }
            Start(p, "gather", 0, "food"); return;
        }
        var dependent = children.Where(q => q.Status == LifeStatus.Alive && q.Place == p.Place &&
            Month - q.Born < Law("care.age") && Month - q.LastCare >= Law("care.interval"))
            .OrderBy(q => q.LastCare).ThenBy(q => q.Id).FirstOrDefault();
        if (dependent is not null && Inventory(p.Id, "food") >= Law("care.food"))
        {
            p.Goal = "양육·돌봄"; p.Reason = "실제 보호 자녀의 돌봄 기한과 식량·자기 행동 시간";
            if (Start(p, "care", dependent.Id, "") is null) return;
        }
        if (TryPeopleConsentPlan(p)) return;
        if (age >= PeopleMaturityMonths(p) && p.Partner == 0)
        {
            var q = p.ObservedPeople.Values.FirstOrDefault(q => q.Id != p.Id && q.Place == p.Place &&
                q.AppearsAdult && q.ApparentGestates != p.Gestates &&
                !p.Beliefs.ContainsKey($"proposal:{q.Id}"));
            if (q.Id != 0)
            {
                if (Partner(p, q.Id) is not null) Remember(p, $"proposal:{q.Id}", "조건 미충족", "실제 제안 결과", 100);
                if (p.Action != ActionKind.Idle) return;
            }
        }
        if (p.Gestates && p.Partner != 0 && BirthQualified(p) && Roll(10000) < Law("birth.chance")) Conceive(p, p.Partner);
        var child = children.FirstOrDefault(q => q.Status == LifeStatus.Alive &&
            q.Place == p.Place && Month - q.Born >= 60 && (!p.Beliefs.TryGetValue($"pupil:{q.Id}:entry", out var progress) ||
                int.Parse(progress.Value, CultureInfo.InvariantCulture) < 100));
        if (child is not null && p.Understanding.GetValueOrDefault("entry") > 0)
        {
            if (Start(p, "teach", child.Id, "entry") is null)
            { p.Goal = "양육·교육"; p.Reason = "후손 입문 지식 전수"; return; }
        }
        if (p.LessonRequests.FirstOrDefault(r => p.Understanding.GetValueOrDefault(r.Knowledge) > 0) is { } request && p.Personality[4] >= 40)
        {
            if (Start(p, "teach", request.Pupil, request.Knowledge) is null)
            { p.LessonRequests.Remove(request); p.Goal = "전수·협력"; p.Reason = "실제 배움 요청에 동의·교육 시간 예약"; return; }
        }
        var needsMethod = p.Realm == Realm.Mortal ? p.Understanding.GetValueOrDefault("entry") == 0 :
            !Catalog.Paths.Any(path => path.Realm == p.Realm && p.Understanding.GetValueOrDefault(path.Id) > 0);
        if (needsMethod)
        {
            var teacher = p.ObservedPeople.Values.FirstOrDefault(q => q.Id != p.Id && q.Place == p.Place);
            if (teacher.Id != 0 && RequestTeaching(p, teacher.Id, "") is null)
            { p.Goal = "지식 탐색"; p.Reason = "자신에게 부족한 공법의 실제 전수 요청"; return; }
        }
        if (DiversePlan(p)) return;
        var studyScore = p.Personality[1] + Law("ai.studyWeight");
        var martialScore = p.Personality[13] + 50;
        p.DecisionCandidates.Add(new("수행", p.Understanding.GetValueOrDefault("entry") > 0 ? studyScore : 0, "자기 입문 지식·구도 성향·세계 목표 보정"));
        p.DecisionCandidates.Add(new("무예", martialScore, "자기 경쟁 성향·육신 단련"));
        if (p.Understanding.GetValueOrDefault("entry") > 0 && studyScore >= martialScore)
        { p.Goal = "구도"; p.Reason = "자기 지식·경지·건강과 구도 성향"; Start(p, "cultivate", 0, ""); }
        else { p.Goal = "무예"; p.Reason = "자기 기반과 경쟁 성향"; Start(p, "martial", 0, ""); }
    }
}
