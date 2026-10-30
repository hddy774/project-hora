// 저장 검사의 JSON 변조 도우미. 정상 serializer 결과를 바꾸어 실제 로드 경로를 검사한다.
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

namespace Xianxia.Simulation.Tests
{
    /// <summary>gzip JSON 검사 자료.</summary>
    internal static class SaveTestData
    {
        /// <summary>저장 본문을 읽는다.</summary>
        /// <param name="bytes">gzip 바이트.</param>
        /// <returns>JSON 객체.</returns>
        internal static JsonObject Read(byte[] bytes)
        {
            using MemoryStream source = new(bytes);
            using GZipStream zip = new(source, CompressionMode.Decompress);
            using StreamReader reader = new(zip);
            return JsonNode.Parse(reader.ReadToEnd())!.AsObject();
        }

        /// <summary>변조한 본문을 gzip으로 감싼다.</summary>
        /// <param name="json">본문.</param>
        /// <returns>gzip 바이트.</returns>
        internal static byte[] Write(JsonObject json)
        {
            using MemoryStream target = new();
            using (GZipStream zip = new(target, CompressionLevel.Fastest, leaveOpen: true))
            {
                zip.Write(Encoding.UTF8.GetBytes(json.ToJsonString()));
            }

            return target.ToArray();
        }
    }
}
