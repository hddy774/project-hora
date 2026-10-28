// 코어 어셈블리가 테스트에서 로드되는지 확인한다.
using NUnit.Framework;

namespace Xianxia.Simulation.Tests
{
    public sealed class SimulationInfoTests
    {
        [Test]
        public void RulesVersion_IsPositive()
        {
            Assert.That(SimulationInfo.RulesVersion, Is.GreaterThan(0));
        }
    }
}
