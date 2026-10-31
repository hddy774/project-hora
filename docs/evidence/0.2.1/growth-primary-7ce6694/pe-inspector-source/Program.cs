using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
var folder = Path.GetFullPath(args[0]); var sha = args[1]; var version = args[2]; var output = args[3];
var rows = new List<object>();
foreach (var name in new[] { "Chronicles.Core", "Chronicles.Game", "Chronicles.Hosting", "Chronicles.Game.Tests" })
{
    var path = Path.Combine(folder, name + ".dll");
    using var stream = File.OpenRead(path); using var pe = new PEReader(stream);
    var metadata = pe.GetMetadataReader(); var definition = metadata.GetAssemblyDefinition(); string? information = null; string? configuration = null;
    foreach (var handle in definition.GetCustomAttributes())
    {
        var attribute = metadata.GetCustomAttribute(handle);
        EntityHandle parent = attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
            HandleKind.MethodDefinition => metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
            _ => default
        };
        if (parent.Kind != HandleKind.TypeReference) continue;
        var type = metadata.GetTypeReference((TypeReferenceHandle)parent);
        var attributeName = metadata.GetString(type.Name);
        if (metadata.GetString(type.Namespace) != "System.Reflection" ||
            attributeName is not ("AssemblyInformationalVersionAttribute" or "AssemblyConfigurationAttribute")) continue;
        var value = metadata.GetBlobReader(attribute.Value);
        if (value.ReadUInt16() != 1) throw new InvalidDataException("잘못된 assembly 정보 형식");
        var text = value.ReadSerializedString();
        if (attributeName == "AssemblyInformationalVersionAttribute") information = text; else configuration = text;
    }
    if (metadata.GetString(definition.Name) != name || information != version + "+" + sha || configuration != "Release")
        throw new InvalidDataException(name + " 실제 DLL의 소스 revision이 고정 소스와 다릅니다: " + information);
    rows.Add(new { name, version = definition.Version.ToString(), informationalVersion = information, configuration,
        bytes = new FileInfo(path).Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) });
}
File.WriteAllText(output, JsonSerializer.Serialize(new { source_commit = sha, runtime = RuntimeInformation.FrameworkDescription,
    cpu_count = Environment.ProcessorCount, total_available_memory_bytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
    os = RuntimeInformation.OSDescription, assemblies = rows }, new JsonSerializerOptions { WriteIndented = true }));
if (RuntimeInformation.FrameworkDescription != ".NET 10.0.14")
    throw new InvalidOperationException("실제 runtime이 기준선의 .NET 10.0.14와 다릅니다: " + RuntimeInformation.FrameworkDescription);
