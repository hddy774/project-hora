using System.Globalization;
using System.Text.Json;
using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private const string PeopleActionPrefix = "people:";
    private sealed record PeopleAction(long Session, long Body);
    private sealed record PeopleAssessmentNotice(long Session, long Subject, int Talent, int Minimum, int Maximum, int Confidence,
        ReputationTopic Opinion, string Statement);

    internal bool TryParsePeopleCreation(GameCommand command, out PeopleCreationProfile profile, out string? error) =>
        PeopleCreationParser.TryParse(command, state.Laws, out profile, out error);

    internal string? PeopleActionError(Person person, string kind)
    {
        var profile = state.PeopleDomain.Profiles.FirstOrDefault(p => p.PersonId == person.Id);
        if (profile is null) return "현재 육신 프로필이 없습니다.";
        var body = profile.Body; var species = SpeciesCatalog.Get(body.SpeciesId);
        if (kind == "travel" && !body.Capabilities.Mobility) return "현재 육신에는 실제 이동 능력/운반 수단이 없습니다.";
        var participation = kind switch
        {
            "craft" or "repair" or "transcribe" or "installArray" or "upkeepArray" or "equip" or "store" or "redeemCurrency" => ParticipationKind.Craft,
            "farm" => ParticipationKind.Agriculture,
            "teach" or "requestTeaching" or "requestAssessment" or "requestReputation" or "requestOath" or "acceptPeopleRequest" => ParticipationKind.Conversation,
            "govern" or "policy" or "adoptPolicy" or "preparePolicy" or "implementPolicy" or "found" => ParticipationKind.Governance,
            "martial" => ParticipationKind.Martial,
            _ => (ParticipationKind?)null
        };
        return participation is { } activity && !Embodiments.CanParticipate(species, body, activity)
            ? "현재 육신의 실제 조작·의사소통 능력으로 수행할 수 없습니다. 명시적인 신체/기운 적응이 필요합니다." : null;
    }

    /// <summary>Only this person's active source-backed preferences enter their goal score.</summary>
    internal int PeoplePreference(Person person, string goal)
    {
        var preferences = Traits.ActivePreferences(state.PeopleDomain.Traits, person.Id, Month);
        int Weight(GoalPreference axis) => goal switch
        {
            "production" => axis switch { GoalPreference.Patience or GoalPreference.Discipline => 3, GoalPreference.Curiosity or GoalPreference.Autonomy => 1, _ => 0 },
            "information" => axis switch { GoalPreference.Curiosity => 4, GoalPreference.Autonomy => 2, GoalPreference.Risk or GoalPreference.SeekingDao => 1, _ => 0 },
            "trade" => axis switch { GoalPreference.Cooperation or GoalPreference.Trust => 2, GoalPreference.Ambition or GoalPreference.Honour => 1, _ => 0 },
            "learning" => axis switch { GoalPreference.SeekingDao or GoalPreference.Curiosity => 3, GoalPreference.Patience or GoalPreference.Discipline => 2, _ => 0 },
            "cooperation" => axis switch { GoalPreference.Family => 4, GoalPreference.Cooperation or GoalPreference.Empathy => 3, GoalPreference.Trust => 2, GoalPreference.Honour => 1, _ => 0 },
            "retreat" => axis switch { GoalPreference.Survival => 4, GoalPreference.Patience => 1, GoalPreference.Risk => -2, GoalPreference.Competition => -1, _ => 0 },
            "politics" => axis switch { GoalPreference.Ambition => 4, GoalPreference.Honour or GoalPreference.Discipline => 2, GoalPreference.Competition or GoalPreference.Family => 1, _ => 0 },
            "cultivation" => axis switch { GoalPreference.SeekingDao => 4, GoalPreference.Discipline or GoalPreference.Patience => 3, GoalPreference.Survival => 1, _ => 0 },
            "martial" => axis switch { GoalPreference.Competition or GoalPreference.Discipline => 3, GoalPreference.SeekingDao => 2, GoalPreference.Risk => 1, _ => 0 },
            _ => 0
        };
        return Math.Clamp(preferences.Sum(p => p.Amount * Weight(p.Preference)) / 100, -500, 500);
    }

    /// <summary>No actual other-person aptitude, title, body, wealth or world ranking is read here.</summary>
    internal SocialJudgment PeopleSocialJudgment(Person observer, long subjectId) =>
        Reputation.Judge(state.PeopleDomain.Reputation, new(observer.Id, subjectId, Month));

    internal int PeopleFortuneModifier(Person person, FortuneEventKind eventKind) => Math.Clamp(
        Traits.ActiveFortune(state.PeopleDomain.Traits, person.Id, Month, eventKind).Sum(e => e.ModifierBasisPoints), -10000, 10000);

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

    public ReputationClaim[] PeopleReceivedClaims(long personId)
    {
        if (state.Controlled != 0 && state.Controlled != personId) return [];
        return state.PeopleDomain.Reputation.Claims.Where(c => (personId == 0 || c.ObserverId == personId) &&
            c.ReceivedMonth <= Month && c.ObservedMonth <= Month && (c.ExpiresMonth is null || c.ExpiresMonth > Month))
            .OrderBy(c => c.ObserverId).ThenBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => c with { Provenance = c.Provenance.ToArray() }).ToArray();
    }

    public PeopleOathView[] PeopleKnownOaths(long personId)
    {
        if (state.Controlled != 0 && state.Controlled != personId) return [];
        return state.PeopleDomain.Social.OathObservations.Where(o => (personId == 0 || o.Person == personId) && o.ReceivedMonth <= Month)
            .OrderBy(o => o.Person).ThenBy(o => o.Oath).Select(observation =>
            {
                var oath = state.PeopleDomain.Social.Oaths.Single(o => o.Id == observation.Oath);
                return new PeopleOathView(oath.Id, observation.Person, oath.First == observation.Person ? oath.Second : oath.First,
                    oath.Began, oath.Until, observation.Active && oath.Until > Month, oath.Terms, observation.ObservedMonth, observation.EvidenceId);
            }).ToArray();
    }

    private bool PeopleObservedHere(Person observer, Person subject) => observer.Status == LifeStatus.Alive && subject.Status == LifeStatus.Alive &&
        observer.Place == subject.Place && observer.ObservedPeople.TryGetValue(subject.Id, out var known) && known.Place == observer.Place &&
        known.Month <= Month && Month - known.Month <= Law("ai.beliefLifetime");

    private static bool PeopleSessionParticipantsAvailable(PeopleSession session, Person proposer, Person recipient) =>
        proposer.Status == LifeStatus.Alive && recipient.Status == LifeStatus.Alive && proposer.Health >= 10 && recipient.Health >= 10 &&
        proposer.BodyId == session.ProposerBody && recipient.BodyId == session.RecipientBody && proposer.Place == session.Place && recipient.Place == session.Place;

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
    private void SetPeopleSocial(PeopleSocialState social) => state.PeopleDomain = state.PeopleDomain with { Social = social };

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

    private void RecordPeopleStandingClaims(GameEvent entry, Person[] witnesses)
    {
        foreach (var trait in state.PeopleDomain.Traits.Traits.Where(t => t.History.Any(h => h.Source.CauseId == $"event:{entry.Id}") &&
            TraitCatalog.Get(t.DefinitionId).Category == TraitCategory.Status))
            foreach (var witness in witnesses)
            {
                var active = Traits.IsActive(trait, Month); var name = TraitCatalog.Get(trait.DefinitionId).Name;
                ReceivePeopleEventClaim(entry, witness, trait.PersonId, ReputationTopic.SocialStanding, active,
                    $"직접 관측한 {name} {(active ? "성립" : "종료")}·기관 {trait.RelatedEntityId}", 9500, $":trait:{trait.Id}");
                state.PeopleDomain = state.PeopleDomain with { TraitObservations = state.PeopleDomain.TraitObservations.Where(o =>
                    o.ObserverId != witness.Id || o.SubjectId != trait.PersonId || o.DefinitionId != trait.DefinitionId)
                    .Append(new(witness.Id, trait.PersonId, trait.DefinitionId, trait.Status, Month, Month, trait.ExpiresMonth, $"event:{entry.Id}")).ToArray() };
            }
    }

    private void ReceivePeopleEventClaim(GameEvent entry, Person observer, long subject, ReputationTopic topic, bool affirmed, string statement, int confidence, string key = "") =>
        ReceivePeopleClaim(new($"event:{entry.Id}:{observer.Id}:{(int)topic}{key}", observer.Id, subject, topic, affirmed, statement, null, null,
            confidence, entry.Month, Month, Month + state.PeopleDomain.Social.Policy.ClaimLifetimeMonths, $"witnessed-event:{entry.Id}", null,
            [new($"event:{entry.Id}", ClaimOrigin.DirectObservation, entry.Actor > 0 ? entry.Actor : subject, observer.Id, entry.Month, Month)]));

    private void ReceivePeopleClaim(ReputationClaim claim)
    {
        var policy = state.PeopleDomain.ReputationPolicy with { MaximumClaimsPerObserver = Math.Min(state.PeopleDomain.ReputationPolicy.MaximumClaimsPerObserver,
            Math.Min(state.PeopleDomain.Social.Policy.ClaimBudget, Math.Max(1, IntLaw("ai.beliefBudget") / 4))) };
        var result = Reputation.Receive(state.PeopleDomain.Reputation, claim, policy);
        if (!result.Accepted) throw new InvalidDataException(result.Code);
        state.PeopleDomain = state.PeopleDomain with { Reputation = result.State };
        TrimCognition(people[claim.ObserverId]);
    }

    internal int PeopleCognitiveLoad(Person person) => state.PeopleDomain.Reputation.Claims.Count(c => c.ObserverId == person.Id &&
        (c.ExpiresMonth is null || c.ExpiresMonth > Month));

    internal void TrimPeopleCognition(Person person)
    {
        var maximum = Math.Min(state.PeopleDomain.ReputationPolicy.MaximumClaimsPerObserver,
            Math.Min(state.PeopleDomain.Social.Policy.ClaimBudget, Math.Max(1, IntLaw("ai.beliefBudget") / 4)));
        var claims = state.PeopleDomain.Reputation.Claims;
        var retained = claims.Where(c => c.ObserverId == person.Id && (c.ExpiresMonth is null || c.ExpiresMonth > Month))
            .OrderByDescending(c => c.Confidence).ThenByDescending(c => c.ReceivedMonth).ThenBy(c => c.Id, StringComparer.Ordinal).Take(maximum).ToArray();
        state.PeopleDomain = state.PeopleDomain with { Reputation = new(claims.Where(c => c.ObserverId != person.Id).Concat(retained)
            .OrderBy(c => c.ObserverId).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray()) };
    }

    // Call immediately after the shared completed-action counter. Unlike its capped 0..100 preference counter,
    // this dated record continues to observe real practice and therefore cannot lapse while practice continues.
    internal void RecordPeopleCompletedAction(Person person, ActionKind kind)
    {
        if (kind == ActionKind.Idle) return;
        EnsurePeopleSocialBaselines();
        var key = kind.ToString(); var habits = state.PeopleDomain.Social.Habits.ToList();
        var index = habits.FindIndex(h => h.Person == person.Id && h.Habit == key);
        if (index < 0) habits.Add(new(person.Id, person.BodyId, key, 1, Month, Month, person.Habits.GetValueOrDefault(key)));
        else habits[index] = habits[index] with { Count = checked(habits[index].Count + 1), LastCompleted = Month,
            LastObservedCounter = person.Habits.GetValueOrDefault(key) };
        SetPeopleSocial(state.PeopleDomain.Social with { Habits = habits.OrderBy(h => h.Person).ThenBy(h => h.Habit, StringComparer.Ordinal).ToArray() });
    }

    private void EnsurePeopleSocialBaselines()
    {
        var social = state.PeopleDomain.Social;
        if (social.Groups.Length == 0) social = social with { Groups = state.Groups.Select(g => new PeopleGroupSnapshot(g.Id, g.Kind, g.Leader, g.Provinces.Count > 0, Month)).ToArray() };
        var known = social.Habits.Select(h => h.Person).ToHashSet();
        var habits = social.Habits.ToList();
        foreach (var person in state.People.Where(p => !known.Contains(p.Id)))
        {
            habits.Add(new(person.Id, person.BodyId, "baseline", 0, Month, Month));
            habits.AddRange(person.Habits.Select(h => new PeopleHabitSnapshot(person.Id, person.BodyId, h.Key, 0, Month, Month, h.Value)));
        }
        SetPeopleSocial(social with { Habits = habits.ToArray() });
    }

    private void RecordPeoplePoliticalTitles(GameEvent entry, Person actor)
    {
        if (entry.Kind != "정치 명령") return;
        var before = state.PeopleDomain.Social.Groups.ToDictionary(g => g.Group);
        if (entry.Text.StartsWith("Founded an actual organization", StringComparison.Ordinal))
            foreach (var group in state.Groups.Where(g => g.Id == actor.Sect && g.Kind == GroupKind.Sect && g.Leader == actor.Id && g.Founded == Month))
                RecordPeopleTrait(actor, "status.sect-founder", PeopleEventSource(entry, actor), [TraitFact.ActualSectFounded], true, group.Id);
        foreach (var group in state.Groups.Where(g => g.Kind == GroupKind.Dynasty && g.Leader == actor.Id && g.Provinces.Count > 0 &&
            before.TryGetValue(g.Id, out var prior) && prior.Kind != GroupKind.Dynasty))
            RecordPeopleTrait(actor, "status.dynasty-founder", PeopleEventSource(entry, actor), [TraitFact.ActualDynastyFounded], true, group.Id);
    }

    private void ReconcilePeopleAuthority(GameEvent? evidence = null)
    {
        foreach (var group in state.Groups)
        {
            var holder = !group.Dissolved && group.Provinces.Count > 0 && people.TryGetValue(group.Leader, out var current) && current.Status == LifeStatus.Alive ? current : null;
            foreach (var trait in state.PeopleDomain.Traits.Traits.Where(t => t.DefinitionId == "status.landlord" && t.RelatedEntityId == group.Id &&
                t.Status is TraitStatus.Active or TraitStatus.Latent && t.PersonId != holder?.Id).ToArray())
                ReleasePeopleTraits(people[trait.PersonId], "status.landlord", group.Id, evidence is null ? $"authority:{group.Id}:changed:{Month}" : $"event:{evidence.Id}",
                    evidence is not null && people[trait.PersonId].Journal.Contains(evidence));
            if (holder is not null && !state.PeopleDomain.Traits.Traits.Any(t => t.PersonId == holder.Id && t.DefinitionId == "status.landlord" &&
                t.RelatedEntityId == group.Id && t.Status == TraitStatus.Active))
            {
                var source = evidence is null ? new PeopleSource($"authority:{group.Id}:{holder.Id}", PeopleSourceKind.Development,
                    "실제 현재 주권/직책 참조·호칭은 세금 권한을 발급하지 않음", Month, state.LawRevision, holder.Id, holder.BodyId, holder.SoulId) : PeopleEventSource(evidence, holder);
                RecordPeopleTrait(holder, "status.landlord", source, [TraitFact.ActualLandAuthority], true, group.Id,
                    observeSelf: evidence is not null && holder.Journal.Contains(evidence));
            }
            if (!group.Dissolved && group.Kind == GroupKind.Dynasty && group.Provinces.Count == 0 && evidence?.Actor == group.Leader &&
                people.TryGetValue(group.Leader, out var exile) && exile.Status == LifeStatus.Alive &&
                state.PeopleDomain.Social.Groups.Any(g => g.Group == group.Id && g.Territory))
                RecordPeopleTrait(exile, "status.exile", PeopleEventSource(evidence, exile), [TraitFact.ActualExile], true, group.Id);
        }
    }

    private void ReleasePeopleTraits(Person person, string definition, long? related, string evidence, bool personallyKnown = true)
    {
        foreach (var trait in state.PeopleDomain.Traits.Traits.Where(t => t.PersonId == person.Id && t.DefinitionId == definition &&
            (related is null || t.RelatedEntityId == related) && t.Status is TraitStatus.Active or TraitStatus.Latent or TraitStatus.Suppressed).ToArray())
        {
            var source = new PeopleSource(evidence, PeopleSourceKind.Development, "실제 조건/권한/관계의 종료", Month, state.LawRevision,
                person.Id, person.BodyId, person.SoulId);
            var released = Traits.Transition(state.PeopleDomain.Traits, new(trait.Id, $"{evidence}:trait:{trait.Id}", source,
                [TraitCatalog.Get(definition).Release]), TraitChangeKind.Released);
            if (released.Accepted)
            {
                state.PeopleDomain = state.PeopleDomain with { Traits = released.State };
                if (personallyKnown) state.PeopleDomain = state.PeopleDomain with
                {
                    TraitObservations = state.PeopleDomain.TraitObservations.Where(o => o.ObserverId != person.Id || o.SubjectId != person.Id || o.DefinitionId != definition)
                        .Append(new(person.Id, person.Id, definition, TraitStatus.Released, Month, Month, trait.ExpiresMonth, evidence)).ToArray()
                };
            }
        }
        ProjectPeopleTraits(person);
    }

    private void ReconcilePeopleOaths()
    {
        foreach (var oath in state.PeopleDomain.Social.Oaths)
            if (!oath.Active || oath.Until <= Month || people[oath.First].Status != LifeStatus.Alive || people[oath.Second].Status != LifeStatus.Alive)
            {
                foreach (var person in new[] { people[oath.First], people[oath.Second] })
                {
                    var other = people[person.Id == oath.First ? oath.Second : oath.First];
                    var breach = person.Journal.LastOrDefault(e => e.Source == $"people:oath-break:{oath.Id}");
                    var known = oath.Until <= Month || breach is not null || other.Ended == Month && other.Place == person.Place;
                    ReleasePeopleTraits(person, "relation.oath", other.Id, $"oath:{oath.Id}:ended:{Month}", known);
                    if (known) RecordPeopleOathObservation(person, oath, false, breach is not null ? $"event:{breach.Id}" :
                        oath.Until <= Month ? $"known-oath-term:{oath.Id}:{oath.Until}" : $"observed-local-end:{oath.Id}:{Month}",
                        oath.Until <= Month ? oath.Until : Month);
                }
            }
        SetPeopleSocial(state.PeopleDomain.Social with { Oaths = state.PeopleDomain.Social.Oaths.Select(o => o.Active &&
            (o.Until <= Month || people[o.First].Status != LifeStatus.Alive || people[o.Second].Status != LifeStatus.Alive)
                ? o with { Active = false, Ended = Month, EndSource = "actual term or participant life ended" } : o).ToArray() });
    }

    private void RecordPeopleOathObservation(Person person, PeopleOath oath, bool active, string evidenceId, long observedMonth)
    {
        var observations = state.PeopleDomain.Social.OathObservations;
        if (observations.Any(o => o.Person == person.Id && o.Oath == oath.Id && o.Active == active)) return;
        SetPeopleSocial(state.PeopleDomain.Social with { OathObservations = observations.Where(o => o.Person != person.Id || o.Oath != oath.Id)
            .Append(new(person.Id, oath.Id, active, observedMonth, Month, evidenceId)).OrderBy(o => o.Person).ThenBy(o => o.Oath).ToArray() });
    }

    private void RecordPeopleRequestObservation(Person person, long session, PeopleSessionStatus status, string evidenceId)
    {
        var observations = state.PeopleDomain.Social.RequestObservations;
        if (observations.Any(o => o.Person == person.Id && o.Session == session && o.Status == status)) return;
        SetPeopleSocial(state.PeopleDomain.Social with { RequestObservations = observations.Where(o => o.Person != person.Id || o.Session != session)
            .Append(new(person.Id, session, status, Month, Month, evidenceId)).OrderBy(o => o.Person).ThenBy(o => o.Session).ToArray() });
    }

    internal void SettlePeopleSocial()
    {
        if (state.PeopleDomain.Social.SettledThrough >= Month) return;
        EnsurePeopleSocialBaselines();
        SetPeopleSocial(state.PeopleDomain.Social with { SettledThrough = Month });
        foreach (var session in state.PeopleDomain.Social.Sessions.Where(s => s.Status == PeopleSessionStatus.Proposed && s.Expires <= Month).ToArray())
            StorePeopleSession(session with { Status = PeopleSessionStatus.Expired, Ended = Month, CompletionSource = "actual request deadline" });
        foreach (var session in state.PeopleDomain.Social.Sessions.Where(s => s.Status == PeopleSessionStatus.Active).ToArray())
            if (!PeopleSessionParticipantsAvailable(session, people[session.Proposer], people[session.Recipient]))
                CancelPeopleSession(session.Id, "actual participant condition changed");
        foreach (var lesson in state.Knowledge.Activities.Where(a => a.Kind == Knowledge.KnowledgeActivityKind.Teaching &&
            a.Status == Knowledge.KnowledgeActivityStatus.Completed && a.EndedMonth == Month).OrderBy(a => a.Id))
            if (!state.Events.Any(e => e.Source == $"people:teaching:{lesson.Id}"))
                Event("전수", lesson.TeacherId, lesson.ActorId, "양측의 실제 시간을 쓴 수업을 완료했다.", $"people:teaching:{lesson.Id}");
        var snapshots = state.PeopleDomain.Social.Habits.ToList();
        foreach (var person in state.People.Where(p => p.Status == LifeStatus.Alive).OrderBy(p => p.Id))
        {
            foreach (var (habit, count) in person.Habits)
            {
                var index = snapshots.FindIndex(h => h.Person == person.Id && h.Habit == habit);
                if (index < 0) snapshots.Add(new(person.Id, person.BodyId, habit, count, Month, Month, count));
                else if (count > snapshots[index].LastObservedCounter) snapshots[index] = snapshots[index] with
                { Count = snapshots[index].Count + count - snapshots[index].LastObservedCounter, LastObservedCounter = count, LastCompleted = Month };
            }
        }
        SetPeopleSocial(state.PeopleDomain.Social with { Habits = snapshots.OrderBy(h => h.Person).ThenBy(h => h.Habit, StringComparer.Ordinal).ToArray() });
        foreach (var person in state.People.Where(p => p.Status == LifeStatus.Alive).OrderBy(p => p.Id))
        {
            FormPeopleExperiencedTraits(person);
            AwakenPeoplePrototypes(person);
            ReconcilePeopleMarker(person, "body.biased", person.Roots.Max() - person.Roots.Min() >= 3000, TraitFact.ElementBiased, $"body.{person.BodyId}.element-balance");
            ReconcilePeopleMarker(person, "scar.meridian", state.Cultivation.TryGetValue(person.BodyId, out var structure) && structure.RootDamage.Total > 0,
                TraitFact.MeridianDamage, $"body.{person.BodyId}.channel-damage");
            ReconcilePeopleMarker(person, "scar.core", person.Realm == Realm.GoldenCore && state.Cultivation.TryGetValue(person.BodyId, out structure) && structure.Damage > 2000,
                TraitFact.CoreDefect, $"body.{person.BodyId}.core-defect");
            ReconcilePeopleMarker(person, "soul.stable", souls[person.SoulId].Integrity == 100 &&
                state.PeopleDomain.Souls.Single(s => s.SoulId == person.SoulId).Foundation is { Value: >= 7000 } &&
                PeopleProfile(person).Learning.Any(l => l.TalentIndex == 10), TraitFact.SoulStable, $"soul.{person.SoulId}.measured-stability");
            SettlePeopleEcology(person);
        }
        ReconcilePeopleAuthority(); ReconcilePeopleOaths();
        state.PeopleDomain = state.PeopleDomain with { Reputation = new(state.PeopleDomain.Reputation.Claims
            .Where(c => c.ExpiresMonth is null || c.ExpiresMonth > Month).ToArray()) };
        foreach (var person in state.People) TrimCognition(person);
    }

    private void FormPeopleExperiencedTraits(Person person)
    {
        var policy = state.PeopleDomain.Social.Policy;
        var habits = state.PeopleDomain.Social.Habits.Where(h => h.Person == person.Id && h.Habit != "baseline").ToArray();
        int Count(params string[] kinds) => habits.Where(h => kinds.Contains(h.Habit, StringComparer.Ordinal) && Month - h.LastCompleted <= policy.HabitLapseMonths).Sum(h => h.Count);
        void Form(string id, TraitFact fact, bool condition, bool lapse = false)
        {
            var active = state.PeopleDomain.Traits.Traits.FirstOrDefault(t => t.PersonId == person.Id && t.DefinitionId == id && t.Status is TraitStatus.Active or TraitStatus.Latent or TraitStatus.Suppressed);
            if (condition && active is null)
                RecordPeopleTrait(person, id, new($"experience:{person.BodyId}:{id}:{Month}", PeopleSourceKind.Development,
                    $"실제 완료 습성/개인 경험·{string.Join(',', habits.Select(h => $"{h.Habit}:{h.Count}@{h.LastCompleted}"))}", Month,
                    state.LawRevision, person.Id, person.BodyId, person.SoulId), [fact], true);
            else if (!condition && lapse && active is not null) ReleasePeopleTraits(person, id, null, $"actual-habit-lapse:{person.BodyId}:{id}:{Month}");
        }
        var practice = Count("Cultivate", "Martial", "Study");
        Form("habit.routine", TraitFact.RoutineExperience, practice >= policy.HabitThreshold, lapse: true);
        Form("habit.planning", TraitFact.PlanningExperience, Count("Craft", "Farm", "Teach") >= policy.HabitThreshold, lapse: true);
        Form("habit.adaptable", TraitFact.AdaptationExperience, habits.Count(h => h.Count > 0 && Month - h.LastCompleted <= policy.HabitLapseMonths) >= 4, lapse: true);
        Form("habit.storage", TraitFact.StorageExperience, Count("Gather", "Farm") >= policy.HabitThreshold, lapse: true);
        Form("temperament.curious", TraitFact.CuriosityExperience, Count("Explore", "Study") >= policy.HabitThreshold && person.Personality[3] >= 50);
        Form("temperament.competitive", TraitFact.CompetitionExperience, Count("Martial") >= policy.HabitThreshold && person.Personality[13] >= 50);
        Form("temperament.calm", TraitFact.CalmExperience, Count("Heal", "Guard") >= policy.HabitThreshold && Math.Abs(person.Mood) <= 10);
        Form("temperament.resolve", TraitFact.ResolveExperience, practice >= policy.HabitThreshold && person.Journal.Any(e => e.Kind.Contains("실패", StringComparison.Ordinal)));
        Form("dao.seeking", TraitFact.DaoValue, Count("Cultivate", "Study") >= policy.HabitThreshold && person.Personality[1] >= 60);
        Form("dao.longevity", TraitFact.SurvivalValue, Count("Heal", "Gather", "Care") >= policy.HabitThreshold && person.Personality[0] >= 60);
        Form("dao.protection", TraitFact.ProtectionValue, Count("Care") >= policy.HabitThreshold);
    }

    private void AwakenPeoplePrototypes(Person person)
    {
        var profile = PeopleProfile(person);
        var latent = state.PeopleDomain.Traits.Traits.Where(t => t.PersonId == person.Id && t.Status == TraitStatus.Latent &&
            TraitCatalog.Get(t.DefinitionId).Layer is TraitLayer.Body or TraitLayer.Bloodline).ToArray();
        foreach (var trait in latent)
        {
            var facts = trait.DefinitionId switch
            {
                "body.meridian" when profile.Learning.Any(l => l.TalentIndex == 7) => new[] { TraitFact.BodyPrototype, TraitFact.MeridianFit },
                "body.earth" when state.Places[person.Place].Element == 4 && profile.Learning.Any(l => l.TalentIndex is 4 or 7 or 18) => [TraitFact.BodyPrototype, TraitFact.EarthAffinity],
                "bloodline.latent" when profile.Learning.Count(l => l.TalentIndex is 7 or 10) >= 3 => [TraitFact.BloodlinePrototype, TraitFact.BloodlineAwakened],
                "bloodline.resonance" when person.Journal.Any(e => e.Kind == "전수" && e.Month == Month &&
                    people.TryGetValue(e.Actor, out var teacher) && Related(person, teacher)) => [TraitFact.BloodlinePrototype, TraitFact.BloodlineResonating],
                _ => Array.Empty<TraitFact>()
            };
            if (facts.Length > 0)
                RecordPeopleTrait(person, trait.DefinitionId, trait.Source with { Month = Month, Reference = "실제 학습/협력에서 확인한 기존 육신·혈맥 원형의 발현" }, facts, true,
                    trait.RelatedEntityId, trait.ExpiresMonth);
        }
    }

    public PeopleEcologyObservation? PeopleEcology(long personId)
    {
        if (state.Controlled != 0 && state.Controlled != personId) return null;
        return state.PeopleDomain.Social.Ecology.LastOrDefault(e => e.Person == personId);
    }

    internal int PeoplePhysiologyPreference(Person person, string goal)
    {
        var own = state.PeopleDomain.Social.Ecology.LastOrDefault(e => e.Person == person.Id && e.Body == person.BodyId);
        if (own is null || own.Month > Month || Month - own.Month > 2 || own.HealthCost == 0) return 0;
        return goal switch { "retreat" => 180, "information" => 120, "production" => own.ActualAccommodation ? 0 : 100, _ => 0 };
    }

    private void SettlePeopleEcology(Person person)
    {
        if (person.Species == "human") return;
        var profile = PeopleProfile(person); var species = SpeciesCatalog.Get(profile.Body.SpeciesId); var place = state.Places[person.Place];
        var habitat = place.Terrain switch
        {
            "숲" => HabitatKind.Forest, "산지" => HabitatKind.Mountain, "하천" => HabitatKind.Water,
            "습지" => HabitatKind.Marsh, "사막" => HabitatKind.Desert, _ => HabitatKind.Grassland
        };
        var suitable = Embodiments.AssessHabitat(species, habitat).Suitable;
        // A completed farm is an actual soil/water-care accommodation, not a newly generated resource.
        var accommodation = profile.Body.CurrentForm == BodyForm.RootedPlant && place.Facilities.Contains("경작지");
        var qiNeeded = species.ResourceNeeds.Contains("ambient-qi", StringComparer.Ordinal);
        var qiConsumed = qiNeeded && place.Qi > 0 ? 1 : 0;
        if (qiConsumed != 0) place.Qi--;
        var cost = (suitable || accommodation ? 0 : 1) + (qiNeeded && qiConsumed == 0 ? 1 : 0);
        var source = new PeopleSource($"physiology:{person.BodyId}:{Month}", PeopleSourceKind.Development,
            "현재 몸이 직접 겪은 서식지. 기존 유한 food 소모는 먹이·물·식물 관리의 보편 유지 자원 추상화; 별도 물/토양 재고를 생성하지 않음. " +
            (qiNeeded ? $"수령 유지에 실제 현지 기운 {qiConsumed} 사용." : ""), Month, state.LawRevision, person.Id, person.BodyId, person.SoulId);
        var observation = new PeopleEcologyObservation(person.Id, person.BodyId, Month, person.Place, habitat, suitable,
            accommodation, true, qiConsumed, cost, source);
        SetPeopleSocial(state.PeopleDomain.Social with { Ecology = state.PeopleDomain.Social.Ecology.Where(e => e.Person != person.Id || Month - e.Month < 12)
            .Append(observation).OrderBy(e => e.Month).ThenBy(e => e.Person).ToArray() });
        if (cost > 0)
        {
            person.Health = Math.Max(0, person.Health - cost);
            person.Reason = "직접 겪은 서식지/기운 유지 조건 부족·실제 보호 환경 또는 이동 수단 필요";
            Remember(person, $"physiology:{person.BodyId}", $"현지 {habitat} 적합={suitable}, 실제 적응시설={accommodation}, 유지 건강비용={cost}", source.CauseId, 100);
            if (person.Health == 0) EndLife(person, 3, "실제 서식지·기운 유지 조건 부족으로 육신 붕괴");
        }
    }

    private void ValidatePeopleSocial()
    {
        foreach (var session in state.PeopleDomain.Social.Sessions)
        {
            if (people[session.Proposer].BodyId != session.ProposerBody || people[session.Recipient].BodyId != session.RecipientBody)
                throw new InvalidDataException("People session cannot reuse another body's identity.");
            if (session.Status == PeopleSessionStatus.Active)
                foreach (var participant in new[] { people[session.Proposer], people[session.Recipient] })
                    if (!TryPeopleAction(participant, out var action) || action.Session != session.Id || participant.Due != session.Due)
                        throw new InvalidDataException("An active personal session must retain both actual action reservations.");
        }
        foreach (var person in state.People)
            if (PeopleCognitiveLoad(person) > Math.Min(state.PeopleDomain.Social.Policy.ClaimBudget, Math.Max(1, IntLaw("ai.beliefBudget") / 4)))
                throw new InvalidDataException("Personal reputation exceeds its bounded observation budget.");
    }
}
