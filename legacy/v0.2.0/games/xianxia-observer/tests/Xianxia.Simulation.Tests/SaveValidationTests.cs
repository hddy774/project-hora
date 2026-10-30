// 정상 gzip JSON의 필드만 바꿔, 의미 오류가 색인 예외가 아닌 저장 형식 오류로 거부되는지 검사한다.
using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;

namespace Xianxia.Simulation.Tests
{
    /// <summary>신뢰하지 않는 저장의 경계와 구 저장 보완.</summary>
    public sealed class SaveValidationTests
    {
        /// <summary>핵심 인물 필드 범위를 실제 로드로 검사한다.</summary>
        /// <param name="property">필드.</param>
        /// <param name="value">잘못된 값.</param>
        [TestCase("RegionId", 999)]
        [TestCase("SectId", 5)]
        [TestCase("Realm", 9)]
        [TestCase("Stage", 4)]
        [TestCase("TechniqueGrade", 20)]
        [TestCase("ArtifactGrade", 6)]
        [TestCase("Age", -1)]
        [TestCase("Lifespan", 0)]
        public void Load_RejectsInvalidPersonField(string property, int value)
        {
            JsonObject json = Original();
            json["Cultivators"]![0]![property] = value;
            Assert.Throws<SaveFormatException>(() => WorldSerializer.FromBytes(SaveTestData.Write(json)));
        }

        /// <summary>필수 상태·목록의 null도 형식 오류로 통일한다.</summary>
        /// <param name="property">필드.</param>
        [TestCase("Settings")]
        [TestCase("Cultivators")]
        [TestCase("Sects")]
        [TestCase("Regions")]
        [TestCase("Souls")]
        [TestCase("Wars")]
        [TestCase("Tournaments")]
        [TestCase("Immortals")]
        [TestCase("Followed")]
        [TestCase("Log")]
        public void Load_RejectsNullRoot(string property)
        {
            JsonObject json = Original();
            json[property] = null;
            Assert.Throws<SaveFormatException>(() => WorldSerializer.FromBytes(SaveTestData.Write(json)));
        }

        /// <summary>중복 ID·비정상 난수·다음 번호와 자기 참조를 거부한다.</summary>
        /// <param name="fault">변조 종류.</param>
        [TestCase("duplicate")]
        [TestCase("increment")]
        [TestCase("log-increment")]
        [TestCase("next-id")]
        [TestCase("version")]
        [TestCase("self")]
        [TestCase("null-pills")]
        [TestCase("null-log-entries")]
        [TestCase("infinity")]
        public void Load_RejectsSemanticFault(string fault)
        {
            JsonObject json = Original();
            JsonNode person = json["Cultivators"]![0]!;
            switch (fault)
            {
                case "duplicate":
                    json["Cultivators"]![1]!["Id"] = person["Id"]!.GetValue<int>();
                    break;
                case "increment":
                    json["RandomIncrement"] = 2;
                    break;
                case "log-increment":
                    json["LogRandomIncrement"] = 2;
                    break;
                case "next-id":
                    json["NextSoulId"] = 1;
                    break;
                case "version":
                    json["FormatVersion"] = 0;
                    break;
                case "self":
                    person["MasterId"] = person["Id"]!.GetValue<int>();
                    break;
                case "null-pills":
                    person["Pills"] = null;
                    break;
                case "null-log-entries":
                    json["Log"]!["Entries"] = null;
                    break;
                default:
                    person["Qi"] = JsonNode.Parse("1e999");
                    break;
            }

            Assert.Throws<SaveFormatException>(() => WorldSerializer.FromBytes(SaveTestData.Write(json)));
        }

        /// <summary>사라진 인연 대상과 짧은 카운터는 기존 정리·보완 정책을 유지한다.</summary>
        [Test]
        public void Load_AcceptsMissingTargetAndPadsShortArrays()
        {
            JsonObject json = Original();
            json["Cultivators"]![0]!["MasterId"] = 999999;
            json["Log"]!["Counts"] = new JsonArray();
            json["Sects"]![0]!["Facilities"] = new JsonArray();
            World world = WorldSerializer.FromBytes(SaveTestData.Write(json));
            Assert.That(world.Cultivators[0].MasterId, Is.EqualTo(999999));
            Assert.That(world.Log.Counts, Has.Count.EqualTo(19));
            Assert.That(world.Sects[0].Facilities, Has.Count.EqualTo(5));
        }

        /// <summary>JSON 표현과 무관하게 validator 자체도 모든 비유한수를 거부한다.</summary>
        /// <param name="value">비유한 영기.</param>
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Validator_RejectsNonFiniteQi(double value)
        {
            World world = TestWorlds.Run(0, seed: 7, population: 100);
            world.CaptureRandomState();
            world.Cultivators[0].Qi = value;
            Assert.That(WorldValidator.FindProblem(world), Is.Not.Null);
        }

        /// <summary>정상 장기 세계를 검증한 뒤에도 같은 바이트를 이어 만든다.</summary>
        [Test]
        public void Load_ValidWorld_ContinuesExactHistory()
        {
            World world = TestWorlds.Run(1000, seed: 7, population: 300);
            World restored = WorldSerializer.FromBytes(WorldSerializer.ToBytes(world));
            WorldSimulator.Run(world, 100);
            WorldSimulator.Run(restored, 100);
            Assert.That(WorldSerializer.ToBytes(restored), Is.EqualTo(WorldSerializer.ToBytes(world)));
        }

        /// <summary>32MiB 초과 입력은 파싱 전에 거부한다.</summary>
        [Test]
        public void Load_RejectsOversizedCompressedInput()
        {
            byte[] data = new byte[WorldSerializer.MaxCompressedBytes + 1];
            Assert.Throws<SaveFormatException>(() => WorldSerializer.FromBytes(data));
        }

        /// <summary>작게 압축되는 공백 폭탄도256MiB 이후 중단한다.</summary>
        [Test]
        public void Load_RejectsExpandedGzipBomb()
        {
            using MemoryStream target = new();
            using (GZipStream zip = new(target, CompressionLevel.Fastest, leaveOpen: true))
            {
                byte[] spaces = new byte[8192];
                Array.Fill(spaces, (byte)' ');
                for (int left = WorldSerializer.MaxExpandedBytes + 1; left > 0; left -= spaces.Length)
                {
                    zip.Write(spaces.AsSpan(0, Math.Min(left, spaces.Length)));
                }
            }

            Assert.Throws<SaveFormatException>(() => WorldSerializer.FromBytes(target.ToArray()));
        }

        private static JsonObject Original()
        {
            return SaveTestData.Read(WorldSerializer.ToBytes(TestWorlds.Run(0, seed: 7, population: 100)));
        }
    }
}
