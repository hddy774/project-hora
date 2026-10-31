using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length != 4 || args[0] is not ("split" or "verify"))
    throw new ArgumentException("split|verify baseline.cs worktree report.json");
var root = Path.GetFullPath(args[2]);
var baseline = Path.GetFullPath(args[1]);
// The baseline is an external, immutable git-show copy; never read it from the edited destination.
var original = File.ReadAllText(baseline);
if (Sha(original) != "b3f7855d524f3a9fed32a3b184357659a5f0d4b1499b8c89968e260db1deb8ca")
    throw new InvalidDataException("The baseline must be the exact 404767a source file.");
var originalClass = Parse(original, baseline);
var prefix = original[..originalClass.Members[0].FullSpan.Start];
var suffix = original[originalClass.Members[^1].FullSpan.End..];
if (prefix + string.Concat(originalClass.Members.Select(m => m.ToFullString())) + suffix != original)
    throw new InvalidDataException("Member FullSpan does not reconstruct the source exactly.");

var assignments = new Dictionary<string, string>(StringComparer.Ordinal);
void Group(string file, params string[] names)
{
    foreach (var name in names) assignments.Add(name, "src/Chronicles.Game/" + file);
}
Group("World.PeopleSocialBridge.cs", "PeopleActionPrefix", "PeopleAction", "PeopleAssessmentNotice",
    "TryParsePeopleCreation", "SetPeopleSocial", "EnsurePeopleSocialBaselines", "SettlePeopleSocial", "ValidatePeopleSocial");
Group("World.PeopleSocialRequests.cs", "TryPeopleConsentPlan", "TryPeopleSocialCommand", "CompletePeopleAction", "PeopleRequests",
    "PeopleObservedHere", "PeopleSessionParticipantsAvailable", "TryPeopleAction", "ClearPeopleAction", "CancelPeopleSession",
    "StorePeopleSession", "RecordPeopleRequestObservation");
Group("World.PeopleSocialEvents.cs", "RecordPeopleSocialEvent", "PeopleEventSource");
Group("World.PeopleSocialReputation.cs", "PeopleSocialJudgment", "PeopleReceivedClaims", "RecordPeopleStandingClaims",
    "ReceivePeopleEventClaim", "ReceivePeopleClaim", "PeopleCognitiveLoad", "TrimPeopleCognition");
Group("World.PeopleSocialTraits.cs", "PeoplePreference", "PeopleFortuneModifier", "RecordPeopleCompletedAction",
    "RecordPeoplePoliticalTitles", "ReconcilePeopleAuthority", "ReleasePeopleTraits", "FormPeopleExperiencedTraits", "AwakenPeoplePrototypes");
Group("World.PeopleSocialOaths.cs", "PeopleKnownOaths", "ReconcilePeopleOaths", "RecordPeopleOathObservation");
Group("World.PeopleSocialEcology.cs", "PeopleActionError", "PeopleEcology", "PeoplePhysiologyPreference", "SettlePeopleEcology");
if (assignments.Count != originalClass.Members.Count || originalClass.Members.Any(m => !assignments.ContainsKey(Name(m))))
    throw new InvalidDataException("Every original member must have exactly one assignment.");
foreach (var member in originalClass.Members)
    if (member is FieldDeclarationSyntax or ConstructorDeclarationSyntax && assignments[Name(member)] != "src/Chronicles.Game/World.PeopleSocialBridge.cs")
        throw new InvalidDataException("Fields and constructors must stay in the original file.");

if (args[0] == "split")
    foreach (var file in assignments.Values.Distinct(StringComparer.Ordinal))
    {
        var members = originalClass.Members.Where(m => assignments[Name(m)] == file);
        File.WriteAllText(Path.Combine(root, file), prefix + string.Concat(members.Select(m => m.ToFullString())) + suffix,
            new UTF8Encoding(false));
    }

var moved = new Dictionary<string, (string File, MemberDeclarationSyntax Node)>(StringComparer.Ordinal);
var files = new List<object>();
foreach (var file in assignments.Values.Distinct(StringComparer.Ordinal))
{
    var path = Path.Combine(root, file); var source = File.ReadAllText(path); var declaration = Parse(source, path);
    if (source[..declaration.Members[0].FullSpan.Start] != prefix || source[declaration.Members[^1].FullSpan.End..] != suffix)
        throw new InvalidDataException("The original using/namespace/partial wrapper changed: " + file);
    foreach (var member in declaration.Members) moved.Add(Name(member), (file, member));
    files.Add(new { path = file, bytes = File.ReadAllBytes(path).Length, sha256 = Sha(source), lines = source.Count(c => c == '\n'),
        members = declaration.Members.Count });
}
if (moved.Count != originalClass.Members.Count) throw new InvalidDataException("Added or missing members.");
var rows = new List<object>();
foreach (var old in originalClass.Members)
{
    var name = Name(old); var (file, current) = moved[name];
    var rawSame = old.ToFullString() == current.ToFullString();
    var tokensSame = Tokens(old).SequenceEqual(Tokens(current));
    var oldBody = Body(old); var currentBody = Body(current);
    var bodySame = oldBody == currentBody;
    if (!rawSame || !tokensSame || !bodySame || file != assignments[name])
        throw new InvalidDataException("Member/signature/body/token/trivia changed: " + name);
    rows.Add(new { member = name, kind = old.Kind().ToString(), signature = Signature(old), original_line = old.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
        destination = file, destination_line = current.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
        full_member_sha256 = Sha(old.ToFullString()), body_sha256 = oldBody is null ? null : Sha(oldBody),
        token_sha256 = Sha(string.Join("\n", Tokens(old))), full_member_bytes_identical = rawSame, body_identical = bodySame, tokens_identical = tokensSame });
}
var report = new { schema = 1, baseline_commit = "404767ad4dc576ea3669cddd42529a50741cbf6f",
    baseline_path = "src/Chronicles.Game/World.PeopleSocialBridge.cs", baseline_sha256 = Sha(original),
    original_members = originalClass.Members.Count, resulting_members = moved.Count, added_members = 0, removed_members = 0,
    method_bodies_changed = 0, member_tokens_changed = 0, member_trivia_changed = 0, wrappers_identical = true,
    fields_and_constructors_kept_in_original_file = true,
    original_fields = originalClass.Members.OfType<FieldDeclarationSyntax>().Select(Name).ToArray(),
    original_constructors = originalClass.Members.OfType<ConstructorDeclarationSyntax>().Select(Name).ToArray(),
    files, members = rows };
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);
File.WriteAllText(args[3], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine(JsonSerializer.Serialize(new { members = moved.Count, files = files.Count, raw_members_identical = true,
    tokens_and_bodies_identical = true, fields_stay = true, report = args[3] }));

static ClassDeclarationSyntax Parse(string source, string path)
{
    var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), path);
    var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    if (errors.Length != 0) throw new InvalidDataException(string.Join("\n", errors.Select(e => e.ToString())));
    var type = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "GameWorld");
    if (type.Parent is not FileScopedNamespaceDeclarationSyntax ns || ns.Name.ToString() != "Chronicles.Game" ||
        string.Join(" ", type.Modifiers.Select(m => m.Text)) != "public sealed partial")
        throw new InvalidDataException("Wrong namespace/class wrapper.");
    return type;
}
static string Name(MemberDeclarationSyntax member) => member switch
{
    MethodDeclarationSyntax method => method.Identifier.ValueText,
    FieldDeclarationSyntax field => field.Declaration.Variables.Single().Identifier.ValueText,
    RecordDeclarationSyntax record => record.Identifier.ValueText,
    ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
    _ => throw new InvalidDataException("Unreviewed member kind: " + member.Kind())
};
static string? Body(MemberDeclarationSyntax member) => member is MethodDeclarationSyntax method
    ? (SyntaxNode?)method.Body is { } block ? block.ToFullString() : method.ExpressionBody?.ToFullString() : null;
static IEnumerable<string> Tokens(SyntaxNode node) => node.DescendantTokens().Select(t => t.RawKind + ":" + t.Text.Length + ":" + t.Text);
static string Signature(MemberDeclarationSyntax member)
{
    if (member is not MethodDeclarationSyntax method) return member.WithoutTrivia().ToString();
    var end = method.Body?.SpanStart ?? method.ExpressionBody?.SpanStart ?? method.SemicolonToken.SpanStart;
    return member.SyntaxTree.GetText().ToString(TextSpanFrom(member.SpanStart, end)).Trim();
}
static Microsoft.CodeAnalysis.Text.TextSpan TextSpanFrom(int start, int end) => Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(start, end);
static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
