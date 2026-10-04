namespace AlphaExchange.Core;

public sealed record CompanyDefinition(string SecurityId, string Symbol, string Name, string Sector,
    string Business, string Driver, string NewsExposure, string CapitalPolicy, int InitialPrice, bool Existing);
public static class CompanyCatalog
{
    public static readonly CompanyDefinition[] Companies =
    [
        new("company-nova", "NOVA", "노바 테크", "기술", "반도체", "설비 가동률과 반도체 수요", "설비 투자와 공급망", "성장 투자 우선", 52400, true),
        new("company-volt", "VOLT", "볼트 에너지", "산업·에너지", "재생에너지", "발전량과 전력 판매 단가", "날씨와 친환경 정책", "설비 투자와 부채 관리", 31200, true),
        new("company-helx", "HELX", "헬릭스 바이오", "바이오·헬스", "신약 연구", "연구 성과와 임상 비용", "임상 승인과 특허", "연구 투자 우선", 78600, true),
        new("company-orbt", "ORBT", "오비트 우주", "산업·에너지", "우주·항공", "발사 수주와 프로젝트 비용", "발사 성공과 정부 계약", "프로젝트 투자 우선", 45300, true),
        new("company-mint", "MINT", "민트 파이낸스", "금융", "결제·핀테크", "결제 건수와 연체 비용", "소비와 결제 규제", "기술 투자 이후 배당", 22800, true),
        new("company-wave", "WAVE", "웨이브 미디어", "미디어", "콘텐츠 제작", "콘텐츠 매출과 제작비", "흥행과 판권", "콘텐츠 투자 우선", 18700, true),
        new("company-food", "FOOD", "그린 푸드", "소비재", "식품", "식품 수요와 원재료비", "곡물 가격과 식품 안전", "안정 배당 지향", 35600, true),
        new("company-aura", "AURA", "아우라 로보틱스", "기술", "로봇·자동화", "자동화 수주와 생산 효율", "산업 투자와 기술 인증", "연구·설비 투자 우선", 64200, true),
        new("company-clud", "CLUD", "클라우드 네트웍스", "기술", "클라우드", "구독 유지율과 데이터센터 비용", "기업 IT 지출과 전력비", "현금 여력 기반 배당", 14600, true),
        new("company-care", "CARE", "케어 메디컬", "바이오·헬스", "의료 서비스", "진료 수요와 인력 비용", "보험 수가와 인구 변화", "시설 투자 이후 배당", 28100, true),
        new("company-cybr", "CYBR", "사이버 쉴드", "기술", "보안", "보안 구독과 사고 대응 비용", "보안 사고와 규제", "연구 투자 우선", 18000, false),
        new("company-data", "DATA", "데이터 링크", "기술", "기업 소프트웨어", "라이선스와 서비스 계약", "기업 투자와 경쟁 제품", "안정 배당과 연구 병행", 19200, false),
        new("company-grid", "GRID", "그리드 전력", "산업·에너지", "전력·유틸리티", "전력 수요와 규제 요금", "금리와 요금 정책", "유지보수 이후 배당", 20400, false),
        new("company-stee", "STEE", "스틸 웍스", "산업·에너지", "철강·소재", "산업 수요와 원재료 가격", "건설 경기와 원자재", "경기 대응 투자", 21600, false),
        new("company-logi", "LOGI", "트랜스 물류", "산업·에너지", "운송·물류", "운송량과 연료비", "교역과 유가", "현금 여력 기반 배당", 22800, false),
        new("company-phar", "PHAR", "메디 파마", "바이오·헬스", "제약", "의약품 판매와 생산 원가", "특허 만료와 의약 규제", "연구와 안정 배당 병행", 24000, false),
        new("company-devc", "DEVC", "메디 디바이스", "바이오·헬스", "의료기기", "기기 수주와 인증 비용", "제품 승인과 리콜", "제품 개발 우선", 25200, false),
        new("company-diag", "DIAG", "바이오 진단", "바이오·헬스", "진단", "검사 수요와 시약 비용", "감염병과 보험 정책", "수요 변동 대비 현금", 26400, false),
        new("company-bnkr", "BNKR", "브릭 은행", "금융", "상업은행", "이자 마진과 신용 손실", "금리와 신용 경기", "자본 적정성 우선", 27600, false),
        new("company-insr", "INSR", "세이프 보험", "금융", "보험", "보험료와 보험금", "재해와 보험 규제", "지급 준비금 우선", 28800, false),
        new("company-asst", "ASST", "오로라 자산운용", "금융", "자산운용", "운용 보수와 수탁 자산", "시장 수익과 자금 유출입", "현금 여력 기반 배당", 30000, false),
        new("company-leas", "LEAS", "플렉스 리스", "금융", "리스·여신", "리스 계약과 조달 비용", "금리와 설비 투자", "부채 관리 우선", 31200, false),
        new("company-game", "GAME", "루프 게임", "미디어", "게임", "이용자 유지와 개발비", "출시 성과와 게임 규제", "개발 투자 우선", 32400, false),
        new("company-advt", "ADVT", "애드 브릿지", "미디어", "광고", "광고 집행과 매체 비용", "소비 경기와 광고 규제", "현금 여력 기반 배당", 33600, false),
        new("company-stre", "STRE", "스트림 플러스", "미디어", "스트리밍", "구독과 콘텐츠 확보 비용", "구독 경쟁과 판권", "성장 투자 우선", 34800, false),
        new("company-publ", "PUBL", "페이지 퍼블리싱", "미디어", "출판·교육", "출판 판매와 교육 수요", "교육 정책과 저작권", "안정 배당 지향", 36000, false),
        new("company-rtal", "RTAL", "데일리 리테일", "소비재", "유통", "소비 지출과 재고 회전", "물가와 소비 정책", "운전자본 우선", 37200, false),
        new("company-beau", "BEAU", "루미 뷰티", "소비재", "화장품", "브랜드 수요와 수출", "소비 트렌드와 교역", "제품 개발과 배당 병행", 38400, false),
        new("company-appl", "APPL", "라이프 가전", "소비재", "생활가전", "교체 수요와 생산 원가", "소비 경기와 원자재", "설비 효율 투자", 39600, false),
        new("company-trvl", "TRVL", "오션 트래블", "소비재", "여행·레저", "관광 수요와 운영 비용", "교통과 재난", "현금 완충 우선", 40800, false),
    ];
    public static Stock Create(CompanyDefinition c, string? id = null) => new()
    {
        SecurityId = id ?? c.SecurityId, Symbol = c.Symbol, Name = c.Name, Sector = c.Sector,
        Business = c.Business, Driver = c.Driver, NewsExposure = c.NewsExposure, CapitalPolicy = c.CapitalPolicy,
        CostRatio = .72 + c.Symbol.Sum(ch => (int)ch) % 13 / 100.0,
        DemandSensitivity = c.Business == "식품" || c.Business == "전력·유틸리티" ? .35 : .6 + c.Symbol.Sum(ch => (int)ch) % 9 / 10.0,
        DividendPayout = c.CapitalPolicy.Contains("배당") ? .50 : .20,
        Price = c.InitialPrice, PreviousPrice = c.InitialPrice, DayOpenPrice = c.InitialPrice,
        StartPrice = c.InitialPrice, FairValue = c.InitialPrice, LastTradePrice = c.InitialPrice,
        History = [c.InitialPrice, c.InitialPrice]
    };
}
