namespace Chronicles.Game.Politics;

public sealed record PolicyDefinition(string Category, IReadOnlyList<string> Choices, string Input, string Effect);

public static class PolicyCatalog
{
    public static IReadOnlyList<PolicyDefinition> All { get; } = Array.AsReadOnly<PolicyDefinition>(
    [
        new("입문·선발", Array.AsReadOnly(new[] { "성년 공개", "영근 심사", "수행자 선발" }),
            "실제 성년·영근·경지·상호 대면", "현재 가문/종문 회원 변경; 숨은 입문자 생성 없음"),
        new("교육·전승", Array.AsReadOnly(new[] { "기초 전수", "집중 사사", "장기 전승" }),
            "현재 지식을 가진 실제 교사와 제자·교사/제자 노동·금고", "교사에게 1/2/3배 실제 교육비 이전과 전수 지시; 지식 자동 복제 없음"),
        new("수행·자원", Array.AsReadOnly(new[] { "기초 지원", "집중 지원", "중점 지원" }),
            "현재 회원·양수 수행 자질·금고", "수행 용도 실제 보조금 1/2/3배 이전"),
        new("기예·산업", Array.AsReadOnly(new[] { "개방 생산", "숙련 심사", "고급 공방" }),
            "현재 회원·실제 해당 기예 숙련·행정 관할·노동·금고", "기예 조건 충족 시 실제 생산 예산 1/2/3배 이전"),
        new("경제·분배", Array.AsReadOnly(new[] { "기본 급여", "균등 확대", "후한 급여" }),
            "살아 있는 현재 회원·실제 공금", "월 기본급 1/2/3배 이전; 부족분은 단일 급여 채권"),
        new("규율·상벌", Array.AsReadOnly(new[] { "경징계", "보통 징계", "엄격 징계" }),
            "실제 회원·현재 내부 권한·개인 잔액", "개인→조직 실제 과태료, 상한은 기본 보조금 1/2/3배"),
        new("대외·외교", Array.AsReadOnly(new[] { "교류", "통상", "동맹" }),
            "두 조직의 현 수장 명시 동의", "단일 0/1/2단계 외교 협정; 재산/주권/채권 이전 없음"),
        new("영토·방위", Array.AsReadOnly(new[] { "소규모 경계", "정규 수비", "집중 수비" }),
            "실제 주권/대리 행정 관할·살아 있는 1/2/3명 노동·예산", "현재 관할 지점의 한시 수비 지시와 실제 급부"),
        new("민생·구휼", Array.AsReadOnly(new[] { "최저 구휼", "가계 구휼", "확대 구휼" }),
            "현재 관할의 실제 빈곤 주민·금고", "빈곤 기준 1/2/3배 대상에게 실제 구호금 1/2/3배 이전"),
        new("승계·인사", Array.AsReadOnly(new[] { "장유 우선", "기여 우선", "수장 지명" }),
            "살아 있는 성년 회원·실제 출생/기여 기록", "기준에 맞는 현 인물에게 수장 직위 이전; 전생혼 권한 없음")
    ]);

    public static bool IsChoice(string category, int choice) => choice is >= 0 and <= 2 &&
        All.Any(p => string.Equals(p.Category, category, StringComparison.Ordinal));
}
