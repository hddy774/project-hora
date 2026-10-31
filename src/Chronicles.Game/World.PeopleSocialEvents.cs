using System.Globalization;
using System.Text.Json;
using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{

    // Root calls this only AFTER the real event has reached its actual witnesses' journals.
    internal void RecordPeopleSocialEvent(GameEvent entry)
    {
        if (entry.Id <= state.PeopleDomain.Social.LastEvent || entry.Month != Month || entry.Id <= 0 || entry.Id >= state.NextId ||
            !state.Events.Any(e => e == entry) && !state.People.Any(p => p.Journal.Contains(entry))) return;
        EnsurePeopleSocialBaselines();
        var witnesses = state.People.Where(p => p.Status == LifeStatus.Alive && p.Journal.Contains(entry)).OrderBy(p => p.Id).ToArray();
        if (entry.Kind == "개인 요청")
        {
            var parts = entry.Source.Split(':');
            if (parts.Length == 3 && parts[0] == "people" && long.TryParse(parts[2], CultureInfo.InvariantCulture, out var requestId) &&
                state.PeopleDomain.Social.Sessions.FirstOrDefault(s => s.Id == requestId) is { } request)
            {
                var status = parts[1] switch { "request" => PeopleSessionStatus.Proposed, "accepted" => PeopleSessionStatus.Active,
                    "declined" => PeopleSessionStatus.Cancelled, _ => (PeopleSessionStatus?)null };
                if (status is { } received)
                    foreach (var witness in witnesses.Where(w => w.Id == request.Proposer || w.Id == request.Recipient))
                        RecordPeopleRequestObservation(witness, request.Id, received, $"event:{entry.Id}");
            }
        }
        if (entry.Kind == "인물 평가" && entry.Source.StartsWith("people:assessment:", StringComparison.Ordinal))
        {
            var notice = JsonSerializer.Deserialize<PeopleAssessmentNotice>(entry.Source.AsSpan("people:assessment:".Length))
                ?? throw new InvalidDataException("Missing actual assessment result.");
            var session = state.PeopleDomain.Social.Sessions.Single(s => s.Id == notice.Session && s.Status == PeopleSessionStatus.Completed);
            if (session.Proposer != entry.Actor || session.Recipient != notice.Subject || entry.Target != notice.Subject)
                throw new InvalidDataException("Assessment result and actual participants disagree.");
            foreach (var observer in witnesses)
            {
                ReceivePeopleClaim(new($"event:{entry.Id}:talent:{observer.Id}", observer.Id, notice.Subject, ReputationTopic.Talent, true,
                    notice.Statement, notice.Minimum, notice.Maximum, notice.Confidence, Month, Month, Month + state.PeopleDomain.Social.Policy.ClaimLifetimeMonths,
                    $"assessment:{session.Id}", notice.Talent, [new($"event:{entry.Id}", ClaimOrigin.DirectObservation, entry.Actor, observer.Id, Month, Month)]));
                if (notice.Opinion != ReputationTopic.Talent)
                    ReceivePeopleClaim(new($"event:{entry.Id}:opinion:{observer.Id}", observer.Id, notice.Subject, notice.Opinion, true,
                        "현재 실연에 근거한 잠정 천교/후보 평판·실제 범위별 천교 좌석과 별도", null, null, notice.Confidence,
                        Month, Month, Month + state.PeopleDomain.Social.Policy.ClaimLifetimeMonths, $"assessment:{session.Id}", null,
                        [new($"event:{entry.Id}", ClaimOrigin.DirectObservation, entry.Actor, observer.Id, Month, Month)]));
            }
        }
        else if (entry.Kind == "진술 전달" && entry.Source.StartsWith("people:relay:", StringComparison.Ordinal) &&
            long.TryParse(entry.Source.AsSpan("people:relay:".Length), CultureInfo.InvariantCulture, out var relayId))
        {
            var session = state.PeopleDomain.Social.Sessions.Single(s => s.Id == relayId && s.Status == PeopleSessionStatus.Completed && s.SharedClaim is not null);
            if (witnesses.Any(p => p.Id == session.Recipient))
            {
                var claim = session.SharedClaim!;
                ReceivePeopleClaim(claim with { Id = $"relay:{entry.Id}:{session.Recipient}", ObserverId = session.Recipient,
                    Confidence = claim.Confidence * 8 / 10, ReceivedMonth = Month, ExpiresMonth = Month + state.PeopleDomain.Social.Policy.ClaimLifetimeMonths,
                    Provenance = [.. claim.Provenance, new($"event:{entry.Id}", ClaimOrigin.Conversation, session.Proposer, session.Recipient, Month, Month)] });
            }
        }
        else if (entry.Actor > 0 && people.TryGetValue(entry.Actor, out var actor))
        {
            foreach (var observer in witnesses)
            {
                if (entry.Kind == "전투" && observer.Id == entry.Target)
                {
                    ReceivePeopleEventClaim(entry, observer, actor.Id, ReputationTopic.Threat, true, "직접 겪은 실제 공격", 9500);
                    RecordPeopleTrait(observer, "relation.grievance", PeopleEventSource(entry, observer), [TraitFact.ActualGrievance], true, actor.Id,
                        Month + state.PeopleDomain.Social.Policy.ClaimLifetimeMonths);
                }
                if (entry.Kind is "돌봄" or "전수" or "거래")
                    ReceivePeopleEventClaim(entry, observer, actor.Id, ReputationTopic.Reliability, true, "직접 참여/관측한 실제 돌봄·전수·거래", 9000);
                if (entry.Kind == "서약 파기")
                    ReceivePeopleEventClaim(entry, observer, actor.Id, ReputationTopic.Reliability, false, "대면하여 들은 실제 서약 파기", 9500);
            }
            if (entry.Kind == "돌봄" && people.TryGetValue(entry.Target, out var child) && child.Guardian == actor.Id)
                RecordPeopleTrait(actor, "dao.protection", PeopleEventSource(entry, actor), [TraitFact.ProtectionValue], true);
            if (entry.Kind is "돌봄" or "입양" && people.TryGetValue(entry.Target, out var familyChild) && familyChild.Guardian == actor.Id)
            {
                RecordPeopleTrait(actor, "relation.family", PeopleEventSource(entry, actor), [TraitFact.ActualFamilyRelationship], true, familyChild.Id);
                RecordPeopleTrait(familyChild, "relation.family", PeopleEventSource(entry, familyChild), [TraitFact.ActualFamilyRelationship], true, actor.Id);
            }
            if (entry.Kind == "전수" && people.TryGetValue(entry.Target, out var pupil))
                RecordPeopleTrait(pupil, "relation.teacher", PeopleEventSource(entry, pupil), [TraitFact.TeacherDebt], true, actor.Id,
                    Month + state.PeopleDomain.Social.Policy.ClaimLifetimeMonths);
            if (entry.Kind == "서약 성립" && entry.Source.StartsWith("people:oath:", StringComparison.Ordinal) &&
                long.TryParse(entry.Source.AsSpan("people:oath:".Length), CultureInfo.InvariantCulture, out var oathId))
            {
                var oath = state.PeopleDomain.Social.Oaths.Single(o => o.Id == oathId);
                foreach (var participant in new[] { people[oath.First], people[oath.Second] })
                {
                    var other = participant.Id == oath.First ? oath.Second : oath.First;
                    RecordPeopleTrait(participant, "relation.oath", PeopleEventSource(entry, participant), [TraitFact.ActualOath], true, other, oath.Until);
                    RecordPeopleTrait(participant, "dao.promise", PeopleEventSource(entry, participant), [TraitFact.PromiseValue], true);
                    if (witnesses.Any(w => w.Id == participant.Id)) RecordPeopleOathObservation(participant, oath, true, $"event:{entry.Id}", entry.Month);
                }
            }
            if (entry.Kind == "서약 파기") ReleasePeopleTraits(actor, "dao.promise", null, $"event:{entry.Id}:promise-breach");
            if (entry.Kind == "발견") RecordPeopleTrait(actor, "fortune.good", PeopleEventSource(entry, actor), [TraitFact.GoodFortuneEvent], true,
                expiresMonth: Month + 6);
            if (entry.Kind == "겁난 통과") RecordPeopleTrait(actor, "fortune.escape", PeopleEventSource(entry, actor), [TraitFact.EscapeOpportunity], true,
                expiresMonth: Month + 3);
            if (entry.Kind == "도려" && entry.Target > 0)
                RecordPeopleTrait(actor, "fortune.encounter", PeopleEventSource(entry, actor), [TraitFact.EncounterResonance], true, entry.Target, Month + 12);
            RecordPeoplePoliticalTitles(entry, actor);
        }
        if (entry.Kind == "재해")
            foreach (var witness in witnesses) RecordPeopleTrait(witness, "fortune.bad", PeopleEventSource(entry, witness), [TraitFact.BadFortuneEvent], true,
                expiresMonth: Month + 6);
        ReconcilePeopleAuthority(entry);
        RecordPeopleStandingClaims(entry, witnesses);
        SetPeopleSocial(state.PeopleDomain.Social with { LastEvent = entry.Id,
            Groups = state.Groups.Select(g => new PeopleGroupSnapshot(g.Id, g.Kind, g.Leader, g.Provinces.Count > 0, Month)).ToArray() });
    }

    private PeopleSource PeopleEventSource(GameEvent entry, Person person) => new($"event:{entry.Id}", PeopleSourceKind.Development,
        $"실제 {entry.Kind}·사건 {entry.Id}·{entry.Source}", Month, state.LawRevision, person.Id, person.BodyId, person.SoulId);
}
