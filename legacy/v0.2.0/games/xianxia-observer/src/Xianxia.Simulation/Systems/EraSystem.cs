// 천지 대세(시대). 3천~8천 년마다 바뀌며 수련 속도와 비경·수조 빈도를 조절한다.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>시대 규칙.</summary>
    public static class EraSystem
    {
        private static readonly string[] s_descriptions =
        {
            "천지가 고요하다.",
            "영맥이 다시 솟아 수련이 빨라지고 비경이 자주 열린다.",
            "영기가 메말라 수련과 돌파가 어려워진다.",
            "붉은 달이 떠 마도가 기세를 얻고 영수가 날뛴다.",
            "영수들이 미쳐 날뛰어 수조가 잦다.",
        };

        /// <summary>시대 설명.</summary>
        /// <param name="kind">시대.</param>
        /// <returns>설명.</returns>
        public static string Describe(EraKind kind)
        {
            return s_descriptions[(int)kind];
        }

        /// <summary>새 시대를 시작한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="kind">시대.</param>
        public static void Begin(World world, EraKind kind)
        {
            ArgumentNullException.ThrowIfNull(world);
            world.Era.Kind = kind;
            world.Era.StartYear = world.Year;
            world.Era.EndYear = world.Year + world.Random.Range(3000, 8000);
            world.Era.Count++;
        }

        /// <summary>시대가 끝났으면 다음 시대로 넘어간다.</summary>
        /// <param name="world">세계.</param>
        public static void Update(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            if (world.Year < world.Era.EndYear)
            {
                return;
            }

            // 평온이 가장 흔하고, 특별한 시대는 같은 시대가 연달아 오지 않게 한다.
            Span<double> weights = stackalloc double[] { 45, 15, 15, 12, 13 };
            if (world.Era.Kind != EraKind.Calm)
            {
                weights[(int)world.Era.Kind] = 0;
            }

            EraKind next = (EraKind)world.Random.PickWeighted(weights);
            Begin(world, next);
            string text = Labels.Of(next) + "의 시대가 열렸다. " + Describe(next);
            Narrator.Log(world, LogKind.Era, text);
            Narrator.Chronicle(world, ChronicleKind.Era, text);
        }

        /// <summary>시대의 수련 배율. 혈월에는 마도가 강해지고 정도가 약해진다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="sect">소속 종문(산수는 null).</param>
        /// <returns>배율.</returns>
        public static double GrowthFactor(World world, Sect? sect)
        {
            ArgumentNullException.ThrowIfNull(world);
            return world.Era.Kind switch
            {
                EraKind.Resurgence => 1.25,
                EraKind.Decline => 0.75,
                EraKind.BloodMoon => sect is null ? 1.0 : sect.Orthodox ? 0.95 : 1.15,
                _ => 1.0,
            };
        }

        /// <summary>시대의 대경지 돌파 배율.</summary>
        /// <param name="world">세계.</param>
        /// <returns>배율.</returns>
        public static double BreakthroughFactor(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            return world.Era.Kind switch
            {
                EraKind.Resurgence => 1.1,
                EraKind.Decline => 0.85,
                _ => 1.0,
            };
        }

        /// <summary>비경 출현 배율.</summary>
        /// <param name="world">세계.</param>
        /// <returns>배율.</returns>
        public static double SecretRealmFactor(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            return world.Era.Kind == EraKind.Resurgence ? 2.0 : world.Era.Kind == EraKind.Decline ? 0.5 : 1.0;
        }

        /// <summary>수조 출현 배율.</summary>
        /// <param name="world">세계.</param>
        /// <returns>배율.</returns>
        public static double BeastTideFactor(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            return world.Era.Kind switch
            {
                EraKind.BeastFrenzy => 4.0,
                EraKind.BloodMoon => 2.0,
                _ => 1.0,
            };
        }
    }
}
