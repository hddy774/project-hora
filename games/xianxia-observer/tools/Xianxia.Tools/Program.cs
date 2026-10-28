// 헤드리스 도구 진입점. 이후 단계에서 밸런스 보고서·성능 측정 명령을 추가한다.
using System;

namespace Xianxia.Tools
{
    /// <summary>명령줄 도구.</summary>
    public static class Program
    {
        /// <summary>프로그램 진입점.</summary>
        /// <param name="args">명령줄 인자.</param>
        /// <returns>종료 코드.</returns>
        public static int Main(string[] args)
        {
            Console.WriteLine("선협 관찰 도구");
            return args.Length;
        }
    }
}
