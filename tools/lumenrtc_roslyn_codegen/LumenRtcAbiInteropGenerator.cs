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

    private static readonly DiagnosticDescriptor MissingManagedMetadataDescriptor = new(
        id: "LRTCABI005",
        title: "Managed metadata file not found",
        messageFormat: "Managed metadata file '{0}' was not found in AdditionalFiles; configure AdditionalFiles and LumenRtcAbiManagedMetadataPath",
        category: "LumenRTC.Abi.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor MultipleManagedMetadataDescriptor = new(
        id: "LRTCABI006",
        title: "Multiple managed metadata files matched",
        messageFormat: "Multiple AdditionalFiles match managed metadata path '{0}': {1}; keep exactly one match",
        category: "LumenRTC.Abi.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor EmptyManagedMetadataDescriptor = new(
        id: "LRTCABI007",
        title: "Managed metadata file is empty",
        messageFormat: "Managed metadata file '{0}' is empty or unreadable",
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
        var matchedIdlFile = ResolveSingleAdditionalFile(
            context,
            files,
            options.IdlPath,
            options.MatchesIdlPath,
            MissingIdlDescriptor,
            MultipleIdlDescriptor,
            EmptyIdlDescriptor);
        if (!matchedIdlFile.HasValue)
        {
            return;
        }

        var matchedManagedFile = ResolveSingleAdditionalFile(
            context,
            files,
            options.ManagedMetadataPath,
            options.MatchesManagedMetadataPath,
            MissingManagedMetadataDescriptor,
            MultipleManagedMetadataDescriptor,
            EmptyManagedMetadataDescriptor);
        if (!matchedManagedFile.HasValue)
        {
            return;
        }

        try
        {
            var model = AbiInteropSourceEmitter.ParseIdl(matchedIdlFile.Value.Content!);
            var source = AbiInteropSourceEmitter.RenderCode(model, options);
            context.AddSource(
                BuildHintName(options.ClassName, "Abi"),
                SourceText.From(source, Encoding.UTF8)
            );

            var typeModel = AbiInteropTypesSourceEmitter.ParseIdl(matchedIdlFile.Value.Content!);
            var typesSource = AbiInteropTypesSourceEmitter.RenderTypesCode(typeModel, options);
            context.AddSource(
                BuildHintName(options.ClassName, "Types"),
                SourceText.From(typesSource, Encoding.UTF8)
            );

            var managedHandlesModel = AbiInteropTypesSourceEmitter.ParseManagedMetadata(matchedManagedFile.Value.Content!);
            var handlesSource = AbiInteropTypesSourceEmitter.RenderHandlesCode(typeModel, managedHandlesModel, options);
            context.AddSource(
                BuildHintName(options.ClassName, "Handles"),
                SourceText.From(handlesSource, Encoding.UTF8)
            );
        }
        catch (GeneratorException ex)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(GenerationFailedDescriptor, Location.None, matchedIdlFile.Value.Path, ex.Message)
            );
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(GenerationFailedDescriptor, Location.None, matchedIdlFile.Value.Path, ex.Message)
            );
        }
    }

    private static AdditionalFileSnapshot? ResolveSingleAdditionalFile(
        SourceProductionContext context,
        ImmutableArray<AdditionalFileSnapshot> files,
        string configuredPath,
        Func<string, bool> matcher,
        DiagnosticDescriptor missingDescriptor,
        DiagnosticDescriptor multipleDescriptor,
        DiagnosticDescriptor emptyDescriptor)
    {
        var matches = files
            .Where(file => matcher(file.Path))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();

        if (matches.Length == 0)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(missingDescriptor, Location.None, configuredPath)
            );
            return null;
        }

        if (matches.Length > 1)
        {
            var preview = string.Join(", ", matches.Take(3).Select(item => item.Path));
            if (matches.Length > 3)
            {
                preview += ", ...";
            }

            context.ReportDiagnostic(
                Diagnostic.Create(multipleDescriptor, Location.None, configuredPath, preview)
            );
            return null;
        }

        var match = matches[0];
        if (string.IsNullOrWhiteSpace(match.Content))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(emptyDescriptor, Location.None, match.Path)
            );
            return null;
        }

        return match;
    }

    private static string BuildHintName(string className, string suffix)
    {
        var effectiveClassName = string.IsNullOrWhiteSpace(className)
            ? "NativeMethods"
            : className.Trim();
        var sanitized = Regex.Replace(effectiveClassName, "[^A-Za-z0-9_]+", "_");
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "NativeMethods";
        }

        return sanitized + "." + suffix + ".g.cs";
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
