// 실제 화면 전환과 접근 가능한 조작 좌표를 기록한다. 세계·난수·연간 갱신에는 관여하지 않는다.
using System;
using System.Text.Json;

namespace Xianxia.App.Services
{
    /// <summary>플랫폼이 수집하는 사용자 입력 수용 근거.</summary>
    internal static class UiTrace
    {
        /// <summary>플랫폼 출력. 미지정이면 콘솔에 전환 로그를 남긴다.</summary>
        internal static Action<string>? Sink { get; set; }

        /// <summary>실제 선택된 탭.</summary>
        /// <param name="index">기존 데이터 경로 번호.</param>
        /// <param name="name">게임 화면 이름.</param>
        internal static void Tab(int index, string name) => Write("tab " + index + " " + name);

        /// <summary>실제로 열린 시트.</summary>
        /// <param name="kind">종류.</param>
        /// <param name="id">게임 내부 대상 번호.</param>
        internal static void Sheet(string kind, int id) => Write("sheet " + kind + " " + id);

        /// <summary>실제로 닫힌 시트.</summary>
        /// <param name="kind">종류.</param>
        internal static void SheetClosed(string kind) => Write("close " + kind);

        /// <summary>실제 하위 분류 선택.</summary>
        /// <param name="index">선택 번호.</param>
        internal static void Choice(int index) => Write("choice " + index);

        /// <summary>저장 복구 또는 새 세계의 시작.</summary>
        /// <param name="year">현재 게임 연도.</param>
        /// <param name="loaded">저장에서 복구했는가.</param>
        internal static void Session(int year, bool loaded)
            => Write("session " + (loaded ? "loaded " : "created ") + year);

        /// <summary>현재 보이는 조작의 실제 화면 중심 좌표.</summary>
        /// <param name="id">안정된 ID 또는 게임 동작 이름.</param>
        /// <param name="x">물리 화면 x.</param>
        /// <param name="y">물리 화면 y.</param>
        internal static void Control(string id, int x, int y) => Sink?.Invoke("[xo-ui] control "
            + JsonSerializer.Serialize(new { id, x, y }));

        /// <summary>현재 스크롤 가능한 실제 화면 영역.</summary>
        /// <param name="left">왼쪽.</param>
        /// <param name="top">위.</param>
        /// <param name="right">오른쪽.</param>
        /// <param name="bottom">아래.</param>
        internal static void Viewport(int left, int top, int right, int bottom) => Sink?.Invoke("[xo-ui] viewport "
            + JsonSerializer.Serialize(new { left, top, right, bottom }));

        /// <summary>입력 뒤 확인하는 현재 게임 상태. 연간 루프에서는 출력하지 않는다.</summary>
        /// <param name="year">게임 연도.</param>
        /// <param name="favor">천기.</param>
        /// <param name="running">진행 중.</param>
        internal static void Context(int year, int favor, bool running) => Sink?.Invoke("[xo-ui] context "
            + JsonSerializer.Serialize(new { year, favor, running }));

        private static void Write(string line) => (Sink ?? Console.WriteLine)("[xo-ui] " + line);
    }
}
