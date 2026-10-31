using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var oldText = File.ReadAllText(args[0]);
var newText = File.ReadAllText(args[1]);
var oldRoot = CSharpSyntaxTree.ParseText(oldText).GetCompilationUnitRoot();
var newRoot = CSharpSyntaxTree.ParseText(newText).GetCompilationUnitRoot();
Require(!oldRoot.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error) &&
    !newRoot.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error), "syntax errors");
var oldMain = Method(oldRoot, "Stability");
var newMain = Method(newRoot, "Stability");
var helper = Method(newRoot, "MeasureStabilityCase");
var oldLoop = oldMain.DescendantNodes().OfType<ForEachStatementSyntax>().Single();
var newLoop = newMain.DescendantNodes().OfType<ForEachStatementSyntax>().Single();
var oldStatements = ((BlockSyntax)oldLoop.Statement).Statements;
var newStatements = ((BlockSyntax)newLoop.Statement).Statements;
var start = oldStatements.ToList().FindIndex(s => Variable(s, "watch"));
var end = oldStatements.ToList().FindIndex(s => s.ToString() == "allPassed &= passed;");
var oldCase = oldStatements.Skip(start).Take(end - start).ToArray();
var newCase = helper.Body!.Statements.TakeWhile(s => !Variable(s, "report")).ToArray();
Require(Tokens(oldCase) == Tokens(newCase), "case statements changed");
Require(Comments(oldCase).SequenceEqual(Comments(newCase)), "case comments changed");
var oldReport = oldStatements.OfType<ExpressionStatementSyntax>().Select(s => s.Expression)
    .OfType<InvocationExpressionSyntax>().Single(n => n.Expression.ToString() == "reports.Add")
    .ArgumentList.Arguments.Single().Expression;
var newReport = helper.Body.Statements.Single(s => Variable(s, "report"))
    .DescendantNodes().OfType<VariableDeclaratorSyntax>().Single().Initializer!.Value;
Require(Tokens([oldReport]) == Tokens([newReport]), "report fields/order changed");
var oldCompletion = oldStatements.OfType<ExpressionStatementSyntax>().Select(s => s.Expression)
    .OfType<InvocationExpressionSyntax>().Single(n => n.Expression.ToString() == "Console.WriteLine")
    .ArgumentList.Arguments.Single().Expression;
var newCompletion = helper.Body.Statements.Single(s => Variable(s, "completionLog"))
    .DescendantNodes().OfType<VariableDeclaratorSyntax>().Single().Initializer!.Value;
Require(Tokens([oldCompletion]) == Tokens([newCompletion]), "completion format changed");
Require(Tokens(oldStatements.Take(start)) == Tokens(newStatements.Take(start)), "start progress changed");
var oldTailStart = oldStatements.ToList().FindIndex(s => s.ToString().StartsWith("finishedCases.Add", StringComparison.Ordinal));
var newTailStart = newStatements.ToList().FindIndex(s => s.ToString().StartsWith("finishedCases.Add", StringComparison.Ordinal));
Require(Tokens(oldStatements.Skip(oldTailStart).Take(3)) == Tokens(newStatements.Skip(newTailStart).Take(3)), "finish/progress order changed");
Require(newStatements.Last().ToString() == "Console.WriteLine(result.CompletionLog);", "completion must follow SaveProgress");
Require(helper.AttributeLists.ToString() == "[MethodImpl(MethodImplOptions.NoInlining)]", "NoInlining missing");
var result = newRoot.DescendantNodes().OfType<RecordDeclarationSyntax>().Single(n => n.Identifier.ValueText == "StabilityCaseResult");
Require(result.ParameterList!.Parameters.Select(p => p.Type!.ToString()).SequenceEqual(new[] { "object", "bool", "string" }), "large graph in transport contract");
Require(helper.Body.Statements.Last().ToString() == "return new(report, passed, completionLog);", "unexpected return");
var outerOld = oldMain.Body!.Statements.OfType<TryStatementSyntax>().Single();
var outerNew = newMain.Body!.Statements.OfType<TryStatementSyntax>().Single();
Require(Tokens(outerOld.Catches) == Tokens(outerNew.Catches), "outer failure reporting changed");
var oldBefore = oldMain.DescendantTokens().TakeWhile(t => t.SpanStart < oldLoop.SpanStart);
var newBefore = newMain.DescendantTokens().TakeWhile(t => t.SpanStart < newLoop.SpanStart);
Require(TokenValues(oldBefore) == TokenValues(newBefore), "plan/environment/report setup changed");
foreach (var name in new[] { "MeasurementEnvironment", "MeasurementGit" })
    Require(Method(oldRoot, name).ToFullString() == Method(newRoot, name).ToFullString(), "unrelated method changed");
var report = new
{
    baseline = "b15992b6150afb8e4f3348501c51051a7f7733d0",
    beforeSha256 = Hash(oldText), afterSha256 = Hash(newText), passed = true,
    caseStatements = oldCase.Length, caseTokensSha256 = Hash(Tokens(oldCase)),
    reportTokensSha256 = Hash(Tokens([oldReport])), completionTokensSha256 = Hash(Tokens([oldCompletion])),
    caseStatementsTokensAndCommentsEqual = true, reportFieldsAndOrderEqual = true,
    stopwatchAndCheckpointOrderEqual = true, outerFailureReportingEqual = true,
    progressBeforeCompletionConsole = true, helperNoInlining = true,
    returnedFields = new[] { "object Report (existing small projection)", "bool Passed", "string CompletionLog" },
    unrelatedMethodsRawTextEqual = true, performanceOrLongRunExecuted = false
};
var assembly = System.Reflection.Assembly.LoadFrom(args[3]);
var compiled = assembly.GetType("PerformanceScenarios")!.GetMethod("MeasureStabilityCase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
Require((compiled.GetMethodImplementationFlags() & System.Reflection.MethodImplAttributes.NoInlining) != 0, "compiled NoInlining missing");
var fields = compiled.ReturnType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
Require(fields.Select(f => f.FieldType).SequenceEqual(new[] { typeof(object), typeof(bool), typeof(string) }), "compiled result fields changed");
File.WriteAllText(args[2] + ".compiled.json", JsonSerializer.Serialize(new { noInlining = true, fields = fields.Select(f => new { f.Name, type = f.FieldType.FullName }), invokedWorldOrStability = false }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
File.WriteAllText(args[2], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine("PASS case statements/report/console/catch/setup token comparison");

static MethodDeclarationSyntax Method(SyntaxNode root, string name) => root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(n => n.Identifier.ValueText == name);
static bool Variable(StatementSyntax statement, string name) => statement is LocalDeclarationStatementSyntax local && local.Declaration.Variables.Any(v => v.Identifier.ValueText == name);
static string Tokens(IEnumerable<SyntaxNode> nodes) => TokenValues(nodes.SelectMany(n => n.DescendantTokens()));
static string TokenValues(IEnumerable<SyntaxToken> tokens) => JsonSerializer.Serialize(tokens.Select(t => new { t.RawKind, t.Text }));
static IEnumerable<string> Comments(IEnumerable<SyntaxNode> nodes) => nodes.SelectMany(n => n.DescendantTrivia()).Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia)).Select(t => t.ToString());
static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
