using Chronicles.Game.Tests.Experiments;

namespace Chronicles.Game.Tests;

public sealed class MarketCandidateIndexCorrectnessScenario : IFeatureScenario
{
    public string Name => "INV-005/008/009/017/018 EX03 준비 기능: 실제 공개 광고·사용권의 scan/index 선택·누락·철회·정산 정합";
    public void Run() => AuthorizedReadExperiment.VerifyMarket(PublicReadFixture.Create(4));
}

public sealed class SnapshotCandidateCorrectnessScenario : IFeatureScenario
{
    public string Name => "INV-004/005/006/022 EX05 준비 기능: 동일 GameCodec 바이트·선택 압축 원자파일·중단 복구·원본 보존";
    public void Run() => SnapshotStorageExperiment.Verify(PublicReadFixture.Create(2));
}

public sealed class AuthorizedReadCacheCorrectnessScenario : IFeatureScenario
{
    public string Name => "INV-007/008/009/010/016/024 EX06 준비 기능: 권한 선필터 scan/index/cache·숨은 변화 비간섭·실제 장비 무효화";
    public void Run() => AuthorizedReadExperiment.VerifyQueries(PublicReadFixture.Create(4));
}
