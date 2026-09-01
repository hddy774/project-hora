# PC 0.0.17 Atlas·도시·시장 수직 계약

## 범위

Alpha 1.1.0 M08의 공간 권위와 M10의 경제 권위를 PC 관찰형 실시간 시뮬레이션에
연결한다. 기존 3×3 지도는 삭제하지 않고 제1층천의 호환 출발 구역으로 편입하며,
제2층천까지 동일한 계층·이동·영맥 계약으로 실제 동작한다.

## Atlas와 이동 권위

- `WorldAtlasRules`는 5개 층천 Definition, Production 1·2층천, 계층
  `층천 → 대륙 → 주 → 권역 → 지점`, 18개 실제 지점과 900개 영맥 Source를 제공한다.
- `SpatialAuthorityStore`는 10,000명의 개인별 발견 bitset, 최근 발견, 이동 경로,
  지역별 인구 index와 영맥 잔여 공급을 평탄 배열로 보관한다.
- 이동은 후보 인지 → 접근 조건 검증 → 계획 → 도착/차단/중단/재경로 Typed Result
  순서다. AI는 자신이 발견한 연결 지점만 후보로 사용한다.
- 축기경 이상 AI는 제1층천 호환 구역에서 제2층천으로 실제 확산한다. 관찰 조회는
  발견 상태, RNG 순서와 세계 hash를 변경하지 않는다.
- 900개 영맥의 공급량은 유한하며 도시 채굴 임무의 실제 생산 상한으로 사용된다.

## 시장 권위

- `MarketAuthorityModel`은 경제 Owner, Holder, Trade Channel, Offer, Reservation,
  Settlement Result를 서로 다른 안정 타입으로 구분한다.
- 도시 공급은 변경된 상품의 Offer만 갱신한다. 예약은 재고를 소유권 이전 전에
  잠그고, 만료·취소·체결 시 정확히 한 번 해제한다.
- 체결은 Preflight 뒤 영석과 상품을 한 번에 이전하고 source result 중복을 거부한다.
  파생 가격·정렬 행은 저장하지 않고 권위 재고·예약·거래 기록에서 다시 만든다.
- 조직 내부·동맹 채널은 Organization Core의 실제 권리가 생기기 전까지
  `ChannelUnavailable`을 반환하며 가짜 중립 권리를 만들지 않는다.

## 관찰 UI와 성능

- 좌측 `지도`와 `경제` 경로가 기존 우측 작은 탭을 대신해 전체 작업공간을 사용한다.
- 지도는 층천 토글·검색과 개요/요수/수선자/계층·영맥 내부 탭을 제공한다.
- 시장은 요약/상품/생산/제안·예약/최근 거래 내부 탭을 제공한다.
- 닫힌 작업공간은 해당 query/DTO/bind를 실행하지 않는다. 펼친 내부 탭만 상세 행을
  materialize하며 표는 가상화를 유지하고 스크롤 막대는 숨긴다.

## 임시 검토 ID

- `PC-TEMP-ATLAS-NAMES-113`: PC Production 1·2층천의 임시 지명
- `PC-TEMP-VEIN-PROFILES-113`: 영맥 품질·월 생산·초기 공급 calibration
- `PC-TEMP-LAYER-ACCESS-113`: 제2층천 진입을 축기경 이상으로 둔 임시 접근 계약
- `PC-TEMP-MARKET-CONTRACT-LIFETIME-113`: Offer 12개월·Reservation 2개월
- `PC-TEMP-MARKET-ORGANIZATION-CHANNEL-113`: 0.0.20 권리 연동 전 명시적 비활성

위 값은 승인값으로 위장하지 않는다. 기능과 저장·결정론 검증은 동작하지만 다음 기획
검토에서 revision과 함께 교체할 수 있다.

## 자동 검증 진입점

- `5층천 Atlas·1·2층천·900 영맥·개인 공간 지식`
- `3×3 지도와 지형별 요수 생태`
- `지도 내부 탭 지연 투영 성능`
- `시장 Offer→Reservation→만료·원자 체결`
- `시장 원자 정산·중복 source·품질 개선 구매`
- `시장 소유자 용량 초과 원자 거부`
- `최근 12개월 수급 창·관찰 snapshot 재사용`
- `관찰 조회 무영향`
