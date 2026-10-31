using System.Globalization;

namespace Chronicles.Game.Tests.Experiments;

// Explicit final-stage CLI. Ordinary feature discovery never calls a measurement runner.
internal static class ExperimentArguments
{
    internal static void Run(string[] args)
    {
        string Value(string option, string? fallback = null)
        {
            var index = Array.IndexOf(args, option);
            if (index < 0) return fallback ?? throw new ArgumentException(option + "을 명시하세요.", nameof(args));
            if (index != Array.LastIndexOf(args, option) || index + 1 >= args.Length ||
                args[index + 1].StartsWith("--", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(args[index + 1]))
                throw new ArgumentException(option + " 뒤에 중복되지 않은 값을 지정하세요.", nameof(args));
            return args[index + 1];
        }
        int Number(string option, int fallback)
        {
            if (!int.TryParse(Value(option, fallback.ToString(CultureInfo.InvariantCulture)), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var number) || number < 1)
                throw new ArgumentException(option + "에는 양의 정수가 필요합니다.", nameof(args));
            return number;
        }

        var experiment = Value("--execution-experiment");
        var output = Value("--execution-output");
        if (experiment is "EX-01" or "EX-02" or "EX-04")
        {
            var populations = Value("--execution-populations", "2,100,1000,10000").Split(',').Select(part =>
                int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var size) && size > 0
                    ? size : throw new ArgumentException("인구 규모를 쉼표로 구분한 양의 정수로 지정하세요.", nameof(args))).ToArray();
            ExecutionMeasurements.Run(new(output, populations, Number("--execution-repetitions", 1), experiment));
            return;
        }
        if (experiment is not ("EX-03" or "EX-05" or "EX-06"))
            throw new ArgumentException("비교 실험은 EX-01부터 EX-06까지 한 개를 지정하세요.", nameof(args));
        var fingerprint = Value("--execution-source-fingerprint");
        if (fingerprint.Length != 64 || fingerprint.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new ArgumentException("실제 제품 소스 지문은 소문자 SHA-256 값이어야 합니다.", nameof(args));
        var workspace = Value("--execution-workspace", Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!,
            "experiment-work-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)));
        ExecutionExperimentScenarios.Run(output, fingerprint, Value("--execution-device", Environment.MachineName),
            Number("--execution-listings", 32), Number("--execution-repetitions", 25),
            Number("--execution-warmups", 2), workspace, experiment);
    }
}
