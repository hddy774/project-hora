// 세계 저장·불러오기. System.Text.Json 소스 생성기를 써서 반사(reflection) 없이 동작한다(Android 트리밍 안전).
// 저장 파일은 gzip으로 압축한 JSON이다.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Persistence
{
    /// <summary>세계 직렬화 문맥(소스 생성).</summary>
    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(World))]
    internal sealed partial class WorldJsonContext : JsonSerializerContext
    {
    }

    /// <summary>저장 파일을 읽을 수 없을 때의 예외.</summary>
    public sealed class SaveFormatException : Exception
    {
        /// <summary>기본 생성자.</summary>
        public SaveFormatException()
        {
        }

        /// <summary>메시지로 만든다.</summary>
        /// <param name="message">메시지.</param>
        public SaveFormatException(string message)
            : base(message)
        {
        }

        /// <summary>메시지와 원인으로 만든다.</summary>
        /// <param name="message">메시지.</param>
        /// <param name="innerException">원인.</param>
        public SaveFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>세계 저장·불러오기.</summary>
    public static class WorldSerializer
    {
        /// <summary>현재 저장 형식 버전.</summary>
        public const int CurrentFormatVersion = 1;

        /// <summary>압축 저장의 최대 크기(32MiB).</summary>
        public const int MaxCompressedBytes = 32 * 1024 * 1024;

        /// <summary>압축 해제 본문의 최대 크기(256MiB).</summary>
        public const int MaxExpandedBytes = 256 * 1024 * 1024;

        /// <summary>세계를 gzip JSON으로 쓴다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="destination">쓸 스트림.</param>
        public static void Save(World world, Stream destination)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(destination);
            world.CaptureRandomState();
            world.FormatVersion = CurrentFormatVersion;
            using GZipStream zip = new(destination, CompressionLevel.Fastest, leaveOpen: true);
            JsonSerializer.Serialize(zip, world, WorldJsonContext.Default.World);
        }

        /// <summary>gzip JSON에서 세계를 읽고 색인을 다시 만든다.</summary>
        /// <param name="source">읽을 스트림.</param>
        /// <returns>세계.</returns>
        /// <exception cref="SaveFormatException">형식이 다르거나 손상된 경우.</exception>
        public static World Load(Stream source)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (source.CanSeek && source.Length - source.Position > MaxCompressedBytes)
            {
                throw new SaveFormatException("저장 파일이 허용 크기를 초과했습니다.");
            }

            World? world = ReadWorld(source);
            if (world is null)
            {
                throw new SaveFormatException("이 버전에서 열 수 없는 저장 파일입니다.");
            }

            string? problem = WorldValidator.FindProblem(world);
            if (problem is not null)
            {
                throw new SaveFormatException(problem);
            }

            Normalize(world);
            world.RebuildIndexes();
            return world;
        }

        /// <summary>입력 데이터의 파싱·속성 설정 오류를 저장 형식 오류로 묶는다.</summary>
        private static World? ReadWorld(Stream source)
        {
            try
            {
                using LimitedReadStream compressed = new(source, MaxCompressedBytes);
                using GZipStream zip = new(compressed, CompressionMode.Decompress, leaveOpen: true);
                using LimitedReadStream expanded = new(zip, MaxExpandedBytes);
                return JsonSerializer.Deserialize(expanded, WorldJsonContext.Default.World);
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException
                or ArgumentException or NullReferenceException or OverflowException)
            {
                throw new SaveFormatException("저장 파일을 읽을 수 없습니다.", exception);
            }

        }

        /// <summary>바이트 배열로 저장한다(테스트·비교용).</summary>
        /// <param name="world">세계.</param>
        /// <returns>gzip JSON 바이트.</returns>
        public static byte[] ToBytes(World world)
        {
            using MemoryStream stream = new();
            Save(world, stream);
            return stream.ToArray();
        }

        /// <summary>바이트 배열에서 읽는다.</summary>
        /// <param name="data">gzip JSON 바이트.</param>
        /// <returns>세계.</returns>
        public static World FromBytes(byte[] data)
        {
            using MemoryStream stream = new(data);
            return Load(stream);
        }

        /// <summary>
        /// 이전 버전 저장 파일처럼 목록 길이가 모자라면 0으로 채운다(새 분류·시설을 끝에 추가해도 열린다).
        /// </summary>
        private static void Normalize(World world)
        {
            Pad(world.Log.Counts, Labels.LogKindCount);
            Pad(world.Ledger.Created, Enum.GetValues<StoneSource>().Length);
            Pad(world.Ledger.Destroyed, Enum.GetValues<StoneSink>().Length);
            foreach (Sect sect in world.Sects)
            {
                Pad(sect.Facilities, SectRules.FacilityCount);
                Pad(sect.RankCounts, (int)SectRank.Leader + 1);
                Pad(sect.PillStock, 4);
            }

            foreach (Region region in world.Regions)
            {
                Pad(region.MarketPills, 4);
                Pad(region.MarketTalismans, 2);
            }

            foreach (Cultivator cultivator in world.Cultivators)
            {
                Pad(cultivator.Pills, 4);
                Pad(cultivator.Talismans, 2);
            }

            foreach (StatSample sample in world.Statistics.Samples)
            {
                Pad(sample.RealmCounts, Realms.Count);
            }

            while (world.Records.FirstReach.Count < Realms.Count)
            {
                world.Records.FirstReach.Add(null);
            }

            while (world.Records.YoungestReach.Count < Realms.Count)
            {
                world.Records.YoungestReach.Add(null);
            }
        }

        private static void Pad<T>(List<T> list, int length)
            where T : struct
        {
            while (list.Count < length)
            {
                list.Add(default);
            }
        }
    }
}
