namespace AlphaExchange.Core;

public sealed record CompanyDefinition(string SecurityId, string Symbol, string Name, string Sector,
    string Business, string Driver, string NewsExposure, string CapitalPolicy, int InitialPrice, bool Existing);
public static class CompanyCatalog
{
    public static readonly CompanyDefinition[] Companies =
    [
        new("company-nova", "NOVA", "노바 테크", "기술", "반도체", "설비 가동률과 반도체 수요", "설비 투자와 공급망", "성장 투자 우선", 52400, true),
        new("company-aura", "AURA", "아우라 로보틱스", "기술", "로봇·자동화", "자동화 수주와 생산 효율", "산업 투자와 기술 인증", "연구·설비 투자 우선", 64200, true),
        new("company-clud", "CLUD", "클라우드 네트웍스", "기술", "클라우드", "구독 유지율과 데이터센터 비용", "기업 IT 지출과 전력비", "현금 여력 기반 배당", 14600, true),
        new("company-volt", "VOLT", "볼트 에너지", "산업·에너지", "재생에너지", "발전량과 전력 판매 단가", "날씨와 친환경 정책", "설비 투자와 부채 관리", 31200, true),
        new("company-orbt", "ORBT", "오비트 우주", "산업·에너지", "우주·항공", "발사 수주와 프로젝트 비용", "발사 성공과 정부 계약", "프로젝트 투자 우선", 45300, true),
        new("company-grid", "GRID", "그리드 전력", "산업·에너지", "전력·유틸리티", "전력 수요와 규제 요금", "금리와 요금 정책", "유지보수 이후 배당", 20400, false),
        new("company-helx", "HELX", "헬릭스 바이오", "바이오·헬스", "신약 연구", "연구 성과와 임상 비용", "임상 승인과 특허", "연구 투자 우선", 78600, true),
        new("company-care", "CARE", "케어 메디컬", "바이오·헬스", "의료 서비스", "진료 수요와 인력 비용", "보험 수가와 인구 변화", "시설 투자 이후 배당", 28100, true),
        new("company-phar", "PHAR", "메디 파마", "바이오·헬스", "제약", "의약품 판매와 생산 원가", "특허 만료와 의약 규제", "연구와 안정 배당 병행", 24000, false),
        new("company-mint", "MINT", "민트 파이낸스", "금융", "결제·핀테크", "결제 건수와 연체 비용", "소비와 결제 규제", "기술 투자 이후 배당", 22800, true),
        new("company-bnkr", "BNKR", "브릭 은행", "금융", "상업은행", "이자 마진과 신용 손실", "금리와 신용 경기", "자본 적정성 우선", 27600, false),
        new("company-insr", "INSR", "세이프 보험", "금융", "보험", "보험료와 보험금", "재해와 보험 규제", "지급 준비금 우선", 28800, false),
        new("company-food", "FOOD", "그린 푸드", "소비·미디어", "식품", "식품 수요와 원재료비", "곡물 가격과 식품 안전", "안정 배당 지향", 35600, true),
        new("company-wave", "WAVE", "웨이브 미디어", "소비·미디어", "콘텐츠 제작", "콘텐츠 매출과 제작비", "흥행과 판권", "콘텐츠 투자 우선", 18700, true),
        new("company-game", "GAME", "루프 게임", "소비·미디어", "게임", "이용자 유지와 개발비", "출시 성과와 게임 규제", "개발 투자 우선", 32400, false),
    ];
    public static Stock Create(CompanyDefinition c, string? id = null) => new()
    {
        SecurityId = id ?? c.SecurityId,
        Symbol = c.Symbol,
        Name = c.Name,
        Sector = c.Sector,
        Business = c.Business,
        Driver = c.Driver,
        NewsExposure = c.NewsExposure,
        CapitalPolicy = c.CapitalPolicy,
        CostRatio = .72 + c.Symbol.Sum(ch => (int)ch) % 13 / 100.0,
        DemandSensitivity = c.Business == "식품" || c.Business == "전력·유틸리티" ? .35 : .6 + c.Symbol.Sum(ch => (int)ch) % 9 / 10.0,
        DividendPayout = c.CapitalPolicy.Contains("배당") ? .50 : .20,
        Price = c.InitialPrice,
        PreviousPrice = c.InitialPrice,
        DayOpenPrice = c.InitialPrice,
        StartPrice = c.InitialPrice,
        FairValue = c.InitialPrice,
        LastTradePrice = c.InitialPrice,
        History = [c.InitialPrice, c.InitialPrice]
    };
}
