// 실제 USD 장부를 변경하지 않는 표시 통화 선택이다.
// 관련 기획: docs/tasks/M0-02-money-quantity.md, R26.
namespace Hora.Domain.Values
{
    /// <summary>USD 금액을 보여 주는 방식. 환전 거래나 별도 원화 계정이 아니다.</summary>
    public enum CurrencyView
    {
        /// <summary>미국 달러. 내부 금액 100센트가 1달러다.</summary>
        Usd,

        /// <summary>1달러당 1,000원으로 고정 환산한 원화 보기.</summary>
        Krw,
    }
}
