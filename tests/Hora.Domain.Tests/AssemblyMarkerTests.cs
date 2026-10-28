// 도메인 라이브러리가 엔진 없이 로드되는지 검증한다.
// 관련 명세: docs/tasks/M0-01-solution-scaffold.md
using NUnit.Framework;

namespace Hora.Domain.Tests
{
    /// <summary>도메인 어셈블리 참조에 대한 최소 실행 검사.</summary>
    public sealed class AssemblyMarkerTests
    {
        /// <summary>도메인 표식 타입을 .NET 테스트 호스트에서 로드한다.</summary>
        [Test]
        public void AssemblyMarker_Exists_IsLoadable()
        {
            // Arrange
            var marker = new AssemblyMarker();

            // Act
            string? assemblyName = marker.GetType().Assembly.GetName().Name;

            // Assert
            Assert.That(assemblyName, Is.EqualTo("Hora.Domain"));
        }
    }
}
