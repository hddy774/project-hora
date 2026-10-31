using System.Globalization;
using System.Text.Json;
using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{

    internal bool TryPeopleConsentPlan(Person person)
    {
        if (person.Action != ActionKind.Idle || person.Health < 30 || !PeopleProfile(person).Body.Capabilities.Communication) return false;
        foreach (var request in state.PeopleDomain.Social.Sessions.Where(s => s.Recipient == person.Id && s.Status == PeopleSessionStatus.Proposed && s.Expires > Month)
            .OrderBy(s => s.Requested).ThenBy(s => s.Id))
        {
            if (!people.TryGetValue(request.Proposer, out var proposer) || !PeopleObservedHere(person, proposer)) continue;
            var relation = state.Relations.FirstOrDefault(r => r.Observer == person.Id && r.Other == proposer.Id);
            var judgment = PeopleSocialJudgment(person, proposer.Id);
            var hostile = judgment.Reaction is SocialReaction.Caution or SocialReaction.InvestigateConflict || relation?.Grievance >= 30;
            var willing = !hostile && (request.Kind != PeopleSessionKind.Oath ||
                (relation?.Trust ?? 0) >= 10 && person.Personality[4] + person.Personality[9] >= 100);
            var decision = new GameCommand($"people-consent:{request.Id}:{person.BodyId}:{Month}", person.Id,
                willing ? "acceptPeopleRequest" : "rejectPeopleRequest", request.Id);
            if (!TryPeopleSocialCommand(decision, person, out var error) || error is not null) continue;
            person.Goal = willing ? "대면 요청·자발적 협력" : "개인 약속 검토";
            person.Reason = $"자신에게 실제 전달된 요청 {request.Id}·자기 관측/관계/받은 진술로 동의 판단";
            if (person.Action != ActionKind.Idle) return true;
        }
        return false;
    }

    internal bool TryPeopleSocialCommand(GameCommand command, Person person, out string? error)
    {
        error = null;
        var kind = command.Kind;
        if (kind is not ("requestAssessment" or "requestReputation" or "requestOath" or "acceptPeopleRequest" or
            "rejectPeopleRequest" or "breakOath" or "cancel")) return false;
        if (kind == "cancel")
        {
            if (!TryPeopleAction(person, out var action)) return false;
            CancelPeopleSession(action.Session, "실제 참여자 작업 취소"); return true;
        }
        EnsurePeopleSocialBaselines();
        var social = state.PeopleDomain.Social; var policy = social.Policy;
        if (kind is "acceptPeopleRequest" or "rejectPeopleRequest")
        {
            var request = social.Sessions.FirstOrDefault(s => s.Id == command.Target && s.Recipient == person.Id);
            if (request is null || request.Status != PeopleSessionStatus.Proposed || request.Expires <= Month)
            { error = "현재 자신에게 실제로 도착한 요청을 선택하세요."; return true; }
            var proposer = people[request.Proposer];
            if (kind == "rejectPeopleRequest")
            {
                if (!PeopleObservedHere(person, proposer) || person.BodyId != request.RecipientBody || proposer.BodyId != request.ProposerBody)
                { error = "직접 대면한 현재 요청 당사자에게 응답하세요."; return true; }
                StorePeopleSession(request with { Status = PeopleSessionStatus.Cancelled, Ended = Month, CompletionSource = "personally delivered declined response" });
                Event("개인 요청", person.Id, proposer.Id, "대면하여 요청 거절 의사를 직접 전달했다.", $"people:declined:{request.Id}");
                return true;
            }
            if (!PeopleSessionParticipantsAvailable(request, proposer, person) || proposer.Action != ActionKind.Idle || person.Action != ActionKind.Idle ||
                !PoliticsLaborAvailable(proposer, Month, Month + policy.SessionMonths) || !PoliticsLaborAvailable(person, Month, Month + policy.SessionMonths) ||
                state.Inventory.Labor.Any(l => l.Active && l.Purpose != "world-action" && (l.Person == proposer.Id || l.Person == person.Id) && l.Start < Month + policy.SessionMonths && l.End > Month))
            { error = "대면·살아 있는 현재 육신·양측의 비어 있는 실제 노동 시간을 확인하세요."; return true; }
            request = request with { Status = PeopleSessionStatus.Active, Started = Month, Due = Month + policy.SessionMonths };
            StorePeopleSession(request);
            proposer.Action = ActionKind.Explore; person.Action = ActionKind.Wait;
            foreach (var actor in new[] { proposer, person })
            {
                actor.Due = request.Due; actor.ActionTarget = request.Id; actor.ActionRevision = state.LawRevision;
                actor.ActionData = PeopleActionPrefix + JsonSerializer.Serialize(new PeopleAction(request.Id, actor.BodyId));
            }
            Event("개인 요청", person.Id, proposer.Id, "양측이 대면하여 실제 개인 작업 시간 사용에 동의했다.", $"people:accepted:{request.Id}");
            return true;
        }
        if (kind == "breakOath")
        {
            var oath = social.Oaths.FirstOrDefault(o => o.Id == command.Target && o.Active && (o.First == person.Id || o.Second == person.Id));
            var otherId = oath is null ? 0 : oath.First == person.Id ? oath.Second : oath.First;
            if (oath is null || !people.TryGetValue(otherId, out var other) || person.Action != ActionKind.Idle || !PeopleObservedHere(person, other))
            { error = "직접 대면한 당사자에게 현재 서약의 파기를 통지하세요."; return true; }
            SetPeopleSocial(social with { Oaths = social.Oaths.Select(o => o.Id == oath.Id ? o with
                { Active = false, Ended = Month, EndSource = $"party:{person.Id}:formal-breach" } : o).ToArray() });
            Event("서약 파기", person.Id, other.Id, "당사자가 대면하여 실제 서약 파기를 통지했다.", $"people:oath-break:{oath.Id}");
            ReconcilePeopleOaths(); return true;
        }
        if (!people.TryGetValue(command.Target, out var recipient) || person.Id == recipient.Id || !PeopleObservedHere(person, recipient) ||
            person.Action != ActionKind.Idle || !PeopleProfile(person).Body.Capabilities.Communication || !PeopleProfile(recipient).Body.Capabilities.Communication)
        { error = "실제로 관측하고 대면하여 의사를 전달할 수 있는 상대를 선택하세요."; return true; }
        if (social.Sessions.Count(s => s.Status == PeopleSessionStatus.Proposed && s.Expires > Month &&
            (s.Proposer == person.Id || s.Recipient == person.Id || s.Proposer == recipient.Id || s.Recipient == recipient.Id)) >= policy.MaximumPendingRequests)
        { error = "현재 전달된 개인 요청의 한도에 도달했습니다."; return true; }
        var sessionKind = kind == "requestAssessment" ? PeopleSessionKind.Assessment : kind == "requestReputation" ? PeopleSessionKind.ClaimRelay : PeopleSessionKind.Oath;
        if (sessionKind == PeopleSessionKind.Assessment && command.Value is < 0 or > 23)
        { error = "공개 재능 정의의 실연 영역을 선택하세요."; return true; }
        ReputationClaim? shared = null;
        if (sessionKind == PeopleSessionKind.ClaimRelay)
        {
            shared = state.PeopleDomain.Reputation.Claims.FirstOrDefault(c => c.ObserverId == person.Id && c.Id == command.Data &&
                c.ReceivedMonth <= Month && (c.ExpiresMonth is null || c.ExpiresMonth > Month));
            if (shared is null) { error = "자신이 실제로 받은 현재 진술만 전달할 수 있습니다."; return true; }
        }
        var terms = sessionKind == PeopleSessionKind.Oath ? command.Data.Trim() : "";
        if (sessionKind == PeopleSessionKind.Oath && (Month - person.Born < PeopleMaturityMonths(person) ||
            Month - recipient.Born < PeopleMaturityMonths(recipient)))
        { error = "현재 육신의 성숙 후 양측이 직접 동의한 한정 서약만 체결할 수 있습니다."; return true; }
        if (terms.Length > 80 || sessionKind == PeopleSessionKind.Oath && terms.Length == 0)
        { error = "서약의 한정된 약속 내용을 1~80자로 적으세요."; return true; }
        var session = new PeopleSession(Id(), sessionKind, person.Id, recipient.Id, person.BodyId, recipient.BodyId, person.Place,
            Month, Month + policy.RequestLifetimeMonths, 0, 0, PeopleSessionStatus.Proposed, sessionKind == PeopleSessionKind.Assessment ? (int)command.Value : 21,
            shared is null ? null : shared with { Provenance = shared.Provenance.ToArray() }, terms);
        StorePeopleSession(session);
        Event("개인 요청", person.Id, recipient.Id, sessionKind == PeopleSessionKind.Assessment ? "대면 실연 평가를 요청했다. 아직 동의·평가 결과는 없다." :
            sessionKind == PeopleSessionKind.ClaimRelay ? "자신이 받은 진술을 전달하는 대화를 요청했다." : "조건 있는 실제 서약 체결을 제안했다.", $"people:request:{session.Id}");
        return true;
    }

    internal bool CompletePeopleAction(Person person)
    {
        if (!TryPeopleAction(person, out var action)) return false;
        var session = state.PeopleDomain.Social.Sessions.FirstOrDefault(s => s.Id == action.Session);
        if (session is null || session.Status != PeopleSessionStatus.Active) { ClearPeopleAction(person); return true; }
        if (Month < session.Due) return true;
        var proposer = people[session.Proposer]; var recipient = people[session.Recipient];
        if (!PeopleSessionParticipantsAvailable(session, proposer, recipient) || !TryPeopleAction(proposer, out var one) || !TryPeopleAction(recipient, out var two) ||
            one.Session != session.Id || two.Session != session.Id || one.Body != proposer.BodyId || two.Body != recipient.BodyId)
        { CancelPeopleSession(session.Id, "실제 참여·위치·현재 육신 조건 중단"); return true; }
        foreach (var participant in new[] { proposer, recipient })
        {
            if (participant.Id != person.Id) RecordCompletedHabit(participant, participant.Action);
            ClearPeopleAction(participant);
        }
        StorePeopleSession(session with { Status = PeopleSessionStatus.Completed, Ended = Month, CompletionSource = $"people:session:{session.Id}:completed" });
        foreach (var participant in new[] { proposer, recipient })
            RecordPeopleRequestObservation(participant, session.Id, PeopleSessionStatus.Completed, $"people:session:{session.Id}:completed");
        switch (session.Kind)
        {
            case PeopleSessionKind.Assessment:
                var ability = PeopleAptitude(recipient).Talents[session.TalentIndex].CurrentExpression;
                var appraisal = PeopleAptitude(proposer).Talents[21].CurrentExpression;
                var resolution = Math.Clamp(2500 - appraisal / 5, 1000, 2500);
                var lower = Math.Min(10_000 - resolution, ability / resolution * resolution);
                var confidence = Math.Clamp(7000 + appraisal / 5, 7000, 9000);
                var opinion = lower >= 5000 ? ReputationTopic.Genius : lower >= 3500 ? ReputationTopic.GeniusCandidate : ReputationTopic.Talent;
                var notice = new PeopleAssessmentNotice(session.Id, recipient.Id, session.TalentIndex, lower, lower + resolution, confidence, opinion,
                    $"{AptitudeCatalog.Talents[session.TalentIndex].Name} 현재 실연의 추정 범위 {lower}~{lower + resolution}/10000. 선천 원형·장래 경지 평가는 미확인.");
                Event("인물 평가", proposer.Id, recipient.Id, notice.Statement, "people:assessment:" + JsonSerializer.Serialize(notice));
                break;
            case PeopleSessionKind.ClaimRelay:
                Event("진술 전달", proposer.Id, recipient.Id, session.SharedClaim!.Statement, $"people:relay:{session.Id}");
                break;
            case PeopleSessionKind.Oath:
                var oathId = Id();
                var oath = new PeopleOath(oathId, proposer.Id, recipient.Id, Month, Month + state.PeopleDomain.Social.Policy.OathMonths,
                    true, session.Terms, session.Id);
                SetPeopleSocial(state.PeopleDomain.Social with { Oaths = [.. state.PeopleDomain.Social.Oaths, oath] });
                Event("서약 성립", proposer.Id, recipient.Id, $"양측이 실제 시간을 사용하여 서약을 수락했다: {session.Terms}", $"people:oath:{oathId}");
                break;
        }
        return true;
    }

    public PeopleRequestView[] PeopleRequests(long personId)
    {
        if (state.Controlled != 0 && state.Controlled != personId) return [];
        var sessions = state.PeopleDomain.Social.Sessions.Where(s => personId == 0 || s.Proposer == personId || s.Recipient == personId);
        return sessions.Select(session => (Session: session, Known: state.Controlled == 0 ? session.Status :
                state.PeopleDomain.Social.RequestObservations.FirstOrDefault(o => o.Person == personId && o.Session == session.Id && o.ReceivedMonth <= Month)?.Status))
            .Where(row => row.Known is PeopleSessionStatus.Proposed or PeopleSessionStatus.Active && row.Session.Expires > Month)
            .OrderBy(row => row.Session.Requested).ThenBy(row => row.Session.Id).Select(row =>
                new PeopleRequestView(row.Session.Id, row.Session.Kind, row.Session.Proposer, row.Session.Recipient, row.Session.Requested,
                    row.Session.Expires, row.Known!.Value, row.Session.TalentIndex, row.Session.Terms)).ToArray();
    }

    private bool PeopleObservedHere(Person observer, Person subject) => observer.Status == LifeStatus.Alive && subject.Status == LifeStatus.Alive &&
        ShareExplorationSpace(observer, subject) && observer.ObservedPeople.TryGetValue(subject.Id, out var known) && known.Place == observer.Place &&
        known.Month <= Month && Month - known.Month <= Law("ai.beliefLifetime");

    private bool PeopleSessionParticipantsAvailable(PeopleSession session, Person proposer, Person recipient) =>
        proposer.Status == LifeStatus.Alive && recipient.Status == LifeStatus.Alive && proposer.Health >= 10 && recipient.Health >= 10 &&
        proposer.BodyId == session.ProposerBody && recipient.BodyId == session.RecipientBody && proposer.Place == session.Place &&
        recipient.Place == session.Place && ShareExplorationSpace(proposer, recipient);

    private static bool TryPeopleAction(Person person, out PeopleAction action)
    {
        action = null!;
        if (!person.ActionData.StartsWith(PeopleActionPrefix, StringComparison.Ordinal)) return false;
        action = JsonSerializer.Deserialize<PeopleAction>(person.ActionData.AsSpan(PeopleActionPrefix.Length))
            ?? throw new InvalidDataException("Invalid people action.");
        return true;
    }

    private static void ClearPeopleAction(Person person)
    { person.Action = ActionKind.Idle; person.Due = 0; person.ActionTarget = 0; person.ActionData = ""; }

    private void CancelPeopleSession(long id, string reason)
    {
        var session = state.PeopleDomain.Social.Sessions.FirstOrDefault(s => s.Id == id);
        if (session is null) return;
        StorePeopleSession(session with { Status = PeopleSessionStatus.Cancelled, Ended = Month, CompletionSource = reason });
        foreach (var person in state.People.Where(p => p.Id == session.Proposer || p.Id == session.Recipient))
            if (TryPeopleAction(person, out var action) && action.Session == id)
            {
                RecordPeopleRequestObservation(person, id, PeopleSessionStatus.Cancelled, $"actual-own-action-cancel:{id}:{person.BodyId}:{Month}");
                ClearPeopleAction(person);
            }
    }

    private void StorePeopleSession(PeopleSession session) => SetPeopleSocial(state.PeopleDomain.Social with
    { Sessions = state.PeopleDomain.Social.Sessions.Where(s => s.Id != session.Id).Append(session).OrderBy(s => s.Id).ToArray() });

    private void RecordPeopleRequestObservation(Person person, long session, PeopleSessionStatus status, string evidenceId)
    {
        var observations = state.PeopleDomain.Social.RequestObservations;
        if (observations.Any(o => o.Person == person.Id && o.Session == session && o.Status == status)) return;
        SetPeopleSocial(state.PeopleDomain.Social with { RequestObservations = observations.Where(o => o.Person != person.Id || o.Session != session)
            .Append(new(person.Id, session, status, Month, Month, evidenceId)).OrderBy(o => o.Person).ThenBy(o => o.Session).ToArray() });
    }
}
