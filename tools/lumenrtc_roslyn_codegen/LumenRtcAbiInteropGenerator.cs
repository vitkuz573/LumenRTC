using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace LumenRTC.Abi.RoslynGenerator;

[Generator(LanguageNames.CSharp)]
public sealed class LumenRtcAbiInteropGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MissingIdlDescriptor = new(
        id: "LRTCABI001",
        title: "ABI IDL file not found",
        messageFormat: "ABI IDL file '{0}' was not found in AdditionalFiles; configure AdditionalFiles and LumenRtcAbiIdlPath",
        category: "LumenRTC.Abi.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor MultipleIdlDescriptor = new(
        id: "LRTCABI002",
        title: "Multiple ABI IDL files matched",
        messageFormat: "Multiple AdditionalFiles match ABI IDL path '{0}': {1}; keep exactly one match",
        category: "LumenRTC.Abi.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor EmptyIdlDescriptor = new(
        id: "LRTCABI003",
        title: "ABI IDL file is empty",
        messageFormat: "ABI IDL file '{0}' is empty or unreadable",
        category: "LumenRTC.Abi.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor GenerationFailedDescriptor = new(
        id: "LRTCABI004",
        title: "ABI source generation failed",
        messageFormat: "Failed to generate interop from '{0}' because {1}",
        category: "LumenRTC.Abi.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var optionsProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => GeneratorOptions.From(provider.GlobalOptions));

        var additionalFilesProvider = context.AdditionalTextsProvider
            .Select(static (file, cancellationToken) => new AdditionalFileSnapshot(
                file.Path,
                file.GetText(cancellationToken)?.ToString()
            ))
            .Collect();

        var generationInputProvider = additionalFilesProvider.Combine(optionsProvider);
        context.RegisterSourceOutput(generationInputProvider, static (spc, input) =>
        {
            Execute(spc, input.Left, input.Right);
        });
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<AdditionalFileSnapshot> files,
        GeneratorOptions options)
    {
        var matches = files
            .Where(file => options.MatchesIdlPath(file.Path))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();

        if (matches.Length == 0)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(MissingIdlDescriptor, Location.None, options.IdlPath)
            );
            return;
        }

        if (matches.Length > 1)
        {
            var preview = string.Join(", ", matches.Take(3).Select(item => item.Path));
            if (matches.Length > 3)
            {
                preview += ", ...";
            }

            context.ReportDiagnostic(
                Diagnostic.Create(MultipleIdlDescriptor, Location.None, options.IdlPath, preview)
            );
            return;
        }

        var matchedFile = matches[0];
        if (string.IsNullOrWhiteSpace(matchedFile.Content))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(EmptyIdlDescriptor, Location.None, matchedFile.Path)
            );
            return;
        }

        try
        {
            var model = AbiInteropSourceEmitter.ParseIdl(matchedFile.Content!);
            var source = AbiInteropSourceEmitter.RenderCode(model, options);
            context.AddSource(
                BuildHintName(options.ClassName),
                SourceText.From(source, Encoding.UTF8)
            );
        }
        catch (GeneratorException ex)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(GenerationFailedDescriptor, Location.None, matchedFile.Path, ex.Message)
            );
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(GenerationFailedDescriptor, Location.None, matchedFile.Path, ex.Message)
            );
        }
    }

    private static string BuildHintName(string className)
    {
        var effectiveClassName = string.IsNullOrWhiteSpace(className)
            ? "NativeMethods"
            : className.Trim();
        var sanitized = Regex.Replace(effectiveClassName, "[^A-Za-z0-9_]+", "_");
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "NativeMethods";
        }

        return sanitized + ".Abi.g.cs";
    }

    private readonly struct AdditionalFileSnapshot
    {
        public AdditionalFileSnapshot(string path, string? content)
        {
            Path = path;
            Content = content;
        }

        public string Path { get; }

        public string? Content { get; }
    }
}
