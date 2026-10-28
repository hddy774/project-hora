// 경계 조건을 직접 검사하는 테스트 어셈블리에만 내부 상태 접근을 허용한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Hora.Simulation.Tests")]
