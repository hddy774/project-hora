using Chronicles.Core;

if (ProductInfo.DefaultPopulation != 10_000)
{
    Console.Error.WriteLine("FAIL: 기본 인구는 10,000명이어야 합니다.");
    return 1;
}

Console.WriteLine("PASS: 저장소 골격 계약");
return 0;
