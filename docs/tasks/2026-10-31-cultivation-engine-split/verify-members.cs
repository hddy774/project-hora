using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length != 3)
    throw new ArgumentException("원본 .cs, 분할 파일 디렉터리, 결과 .json 경로가 필요합니다.");

const string className = "CultivationEngine";
const string expectedNamespace = "Chronicles.Game.Cultivation";
var beforePath = Path.GetFullPath(args[0]);
var afterDirectory = Path.GetFullPath(args[1]);
var before = ReadClass(beforePath);
var afterFiles = Directory.GetFiles(afterDirectory, "CultivationEngine*.cs")
    .Order(StringComparer.Ordinal).ToArray();
if (afterFiles.Length == 0) throw new InvalidDataException("분할 파일이 없습니다.");
var after = afterFiles.Select(path => (Path: path, Class: ReadClass(path))).ToArray();

// 이 작업의 기준에는 필드·생성자가 없다. 이후 추가되면 이 증거를 재사용하지 않는다.
if (before.Members.Any(member => member is not MethodDeclarationSyntax))
    throw new InvalidDataException("기준에 메서드 이외 멤버가 생겼습니다. 초기화 순서를 별도 검토해야 합니다.");
if (after.Any(file => file.Class.Members.Any(member => member is not MethodDeclarationSyntax)))
    throw new InvalidDataException("분할 뒤 메서드 이외 멤버가 추가됐습니다.");
var expectedModifiers = Tokens(before.Modifiers);
foreach (var file in after)
{
    if (!file.Class.Modifiers.Any(SyntaxKind.PartialKeyword) ||
        Tokens(file.Class.Modifiers.Where(token => !token.IsKind(SyntaxKind.PartialKeyword))) != expectedModifiers)
        throw new InvalidDataException($"클래스 한정자가 달라졌습니다: {file.Path}");
}

var beforeMethods = before.Members.Cast<MethodDeclarationSyntax>().ToArray();
var afterMethods = after.SelectMany(file => file.Class.Members.Cast<MethodDeclarationSyntax>()
    .Select(method => (file.Path, Method: method))).ToArray();
if (beforeMethods.Select(Key).Distinct(StringComparer.Ordinal).Count() != beforeMethods.Length ||
    afterMethods.Select(item => Key(item.Method)).Distinct(StringComparer.Ordinal).Count() != afterMethods.Length)
    throw new InvalidDataException("중복 메서드 이름이 있습니다. 이 작업의 대조 키를 재검토해야 합니다.");
if (!beforeMethods.Select(Key).Order(StringComparer.Ordinal)
    .SequenceEqual(afterMethods.Select(item => Key(item.Method)).Order(StringComparer.Ordinal)))
    throw new InvalidDataException("메서드가 추가되거나 사라졌습니다.");

var rows = beforeMethods.Select(method =>
{
    var moved = afterMethods.Single(item => Key(item.Method) == Key(method));
    var originalTokens = Tokens(method.DescendantTokens());
    var movedTokens = Tokens(moved.Method.DescendantTokens());
    var originalSignature = Tokens(Signature(method));
    var movedSignature = Tokens(Signature(moved.Method));
    var originalBody = Tokens(Body(method).DescendantTokens());
    var movedBody = Tokens(Body(moved.Method).DescendantTokens());
    if (originalTokens != movedTokens || originalSignature != movedSignature || originalBody != movedBody)
        throw new InvalidDataException($"서명 또는 본문 토큰이 달라졌습니다: {Key(method)}");
    if (!Comments(method).SequenceEqual(Comments(moved.Method)))
        throw new InvalidDataException($"주석이 달라졌습니다: {Key(method)}");
    return new
    {
        member = Key(method),
        accessibility = method.Modifiers.First(token => token.IsKind(SyntaxKind.PublicKeyword) ||
            token.IsKind(SyntaxKind.InternalKeyword) || token.IsKind(SyntaxKind.PrivateKeyword)).Text,
        file = Path.GetFileName(moved.Path),
        originalLine = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
        movedLine = moved.Method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
        tokenCount = method.DescendantTokens().Count(),
        memberTokenSha256 = Hash(originalTokens),
        signatureTokenSha256 = Hash(originalSignature),
        bodyTokenSha256 = Hash(originalBody),
        identicalIncludingWhitespace = method.ToFullString() == moved.Method.ToFullString()
    };
}).ToArray();

var report = new
{
    baselineCommit = "404767ad4dc576ea3669cddd42529a50741cbf6f",
    baselinePath = "src/Chronicles.Game/Cultivation/CultivationEngine.cs",
    baselineSha256 = Hash(File.ReadAllText(beforePath)),
    roslynVersion = typeof(CSharpSyntaxTree).Assembly.GetName().Version!.ToString(),
    @namespace = expectedNamespace,
    type = "public static partial class CultivationEngine",
    passed = true,
    methodCount = rows.Length,
    fields = 0,
    staticConstructors = 0,
    instanceConstructors = 0,
    allMemberTokensEqual = true,
    allSignatureTokensEqual = true,
    allBodyTokensEqual = true,
    allMethodCommentsEqual = true,
    files = after.Select(file => new
    {
        file = Path.GetFileName(file.Path),
        sha256 = Hash(File.ReadAllText(file.Path)),
        methods = file.Class.Members.Count
    }).ToArray(),
    members = rows
};
File.WriteAllText(args[2], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine($"PASS {rows.Length}개 메서드의 서명·본문 토큰/주석 동일, {after.Length}개 partial, 필드·생성자 0개");

ClassDeclarationSyntax ReadClass(string path)
{
    var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), new CSharpParseOptions(LanguageVersion.Preview))
        .GetCompilationUnitRoot();
    if (root.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ||
        root.ContainsDirectives || root.Usings.Count != 0 || root.AttributeLists.Count != 0 ||
        root.Members.Count != 1 || root.Members[0] is not FileScopedNamespaceDeclarationSyntax ns ||
        ns.Name.ToString() != expectedNamespace || ns.Usings.Count != 0 || ns.Members.Count != 1 ||
        ns.Members[0] is not ClassDeclarationSyntax declaration || declaration.Identifier.ValueText != className ||
        declaration.AttributeLists.Count != 0 || declaration.BaseList is not null ||
        declaration.TypeParameterList is not null || declaration.ConstraintClauses.Count != 0)
        throw new InvalidDataException($"예상 namespace/단일 클래스 구조가 아닙니다: {path}");
    return declaration;
}

static string Key(MethodDeclarationSyntax method) => method.Identifier.ValueText;
static SyntaxNode Body(MethodDeclarationSyntax method) =>
    (SyntaxNode?)method.Body ?? method.ExpressionBody ?? throw new InvalidDataException("메서드 본문이 없습니다.");
static IEnumerable<SyntaxToken> Signature(MethodDeclarationSyntax method) =>
    method.DescendantTokens().TakeWhile(token => token.SpanStart < Body(method).SpanStart);
static string Tokens(IEnumerable<SyntaxToken> tokens) => JsonSerializer.Serialize(tokens.Select(token =>
    new { kind = token.Kind().ToString(), text = token.Text }));
static IEnumerable<string> Comments(SyntaxNode node) => node.DescendantTrivia()
    .Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
        trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
    .Select(trivia => trivia.ToString());
static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
