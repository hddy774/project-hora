
namespace AlphaExchange.Core;

public sealed record BookLevel(int Price, long InstitutionQuantity, long RetailQuantity, long BankQuantity = 0)
{ public long Quantity => InstitutionQuantity + RetailQuantity + BankQuantity; }
