using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LumenRTC.Abi.RoslynGenerator;

internal static class Program
{
    private const string ToolVersion = "1.0.0";

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
        "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
        "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
        "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
        "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };

    private static readonly Dictionary<string, string> PrimitiveTypeMap = new(StringComparer.Ordinal)
    {
        ["void"] = "void",
        ["bool"] = "bool",
        ["char"] = "byte",
        ["signed char"] = "sbyte",
        ["unsigned char"] = "byte",
        ["short"] = "short",
        ["unsigned short"] = "ushort",
        ["int"] = "int",
        ["unsigned int"] = "uint",
        ["long"] = "nint",
        ["unsigned long"] = "nuint",
        ["long long"] = "long",
        ["unsigned long long"] = "ulong",
        ["int8_t"] = "sbyte",
        ["uint8_t"] = "byte",
        ["int16_t"] = "short",
        ["uint16_t"] = "ushort",
        ["int32_t"] = "int",
        ["uint32_t"] = "uint",
        ["int64_t"] = "long",
        ["uint64_t"] = "ulong",
        ["size_t"] = "nuint",
        ["ssize_t"] = "nint",
        ["float"] = "float",
        ["double"] = "double",
    };

    private static int Main(string[] args)
    {
        try
        {
            var options = GeneratorOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            return Run(options);
        }
        catch (GeneratorException ex)
        {
            Console.Error.WriteLine($"roslyn-codegen error: {ex.Message}");
            return 2;
        }
    }

    private static int Run(GeneratorOptions options)
    {
        var idlText = ReadFileUtf8(options.IdlPath);
        var model = ParseIdl(idlText);
        var generated = RenderCode(model, options);

        var existing = File.Exists(options.OutputPath) ? ReadFileUtf8(options.OutputPath) : string.Empty;
        var isUpToDate = NormalizeEol(existing) == NormalizeEol(generated);

        if (options.Check)
        {
            if (isUpToDate)
            {
                Console.WriteLine($"roslyn-codegen check: up to date ({options.OutputPath})");
                return 0;
            }

            Console.Error.WriteLine($"roslyn-codegen check: drift detected ({options.OutputPath})");
            return 1;
        }

        if (options.DryRun)
        {
            var status = isUpToDate ? "unchanged" : "would_update";
            Console.WriteLine($"roslyn-codegen dry-run: {status} ({options.OutputPath})");
            return 0;
        }

        var outputDir = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        File.WriteAllText(options.OutputPath, generated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var statusLabel = isUpToDate ? "unchanged" : "updated";
        Console.WriteLine(
            $"roslyn-codegen: {statusLabel} {options.OutputPath} " +
            $"(symbols={model.Functions.Count}, version={model.AbiVersion})"
        );
        return 0;
    }

    private static string ReadFileUtf8(string path)
    {
        try
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new GeneratorException($"Unable to read '{path}': {ex.Message}");
        }
    }

    private static string NormalizeEol(string value) => value.Replace("\r\n", "\n");

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Usage:
              dotnet run --project tools/lumenrtc_roslyn_codegen/LumenRTC.Abi.RoslynGenerator.csproj -- \
                --idl <path> --output <path> [options]

            Options:
              --namespace <value>           Default: LumenRTC.Interop
              --class-name <value>          Default: NativeMethods
              --access-modifier <value>     Default: internal
              --calling-convention <value>  Default: Cdecl
              --library-expression <value>  Default: LibName
              --check                       Fail if generated output differs
              --dry-run                     Do not write output
              --help                        Show this help
            """
        );
    }

    private static IdlModel ParseIdl(string text)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new GeneratorException($"IDL JSON is invalid: {ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new GeneratorException("IDL root must be an object.");
            }

            var abiVersion = ParseAbiVersion(root);
            var enumNames = ParseHeaderTypeNames(root, "enums");
            var structNames = ParseHeaderTypeNames(root, "structs");
            var functions = ParseFunctions(root);

            return new IdlModel(functions, enumNames, structNames, abiVersion);
        }
    }

    private static string ParseAbiVersion(JsonElement root)
    {
        if (!root.TryGetProperty("abi_version", out var abiVersionObj) || abiVersionObj.ValueKind != JsonValueKind.Object)
        {
            return "n/a";
        }

        var major = GetIntOrDefault(abiVersionObj, "major");
        var minor = GetIntOrDefault(abiVersionObj, "minor");
        var patch = GetIntOrDefault(abiVersionObj, "patch");
        return $"{major}.{minor}.{patch}";
    }

    private static int GetIntOrDefault(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return 0;
    }

    private static HashSet<string> ParseHeaderTypeNames(JsonElement root, string section)
    {
        if (!root.TryGetProperty("header_types", out var headerTypes) || headerTypes.ValueKind != JsonValueKind.Object)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (!headerTypes.TryGetProperty(section, out var sectionObj) || sectionObj.ValueKind != JsonValueKind.Object)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in sectionObj.EnumerateObject())
        {
            names.Add(property.Name);
        }

        return names;
    }

    private static List<FunctionSpec> ParseFunctions(JsonElement root)
    {
        if (!root.TryGetProperty("functions", out var functionsObj) || functionsObj.ValueKind != JsonValueKind.Array)
        {
            throw new GeneratorException("IDL is missing required array 'functions'.");
        }

        var functions = new List<FunctionSpec>();

        foreach (var item in functionsObj.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetRequiredString(item, "name");
            var cReturnType = GetOptionalString(item, "c_return_type", "void");
            var documentation = GetOptionalString(item, "documentation", string.Empty);
            var deprecated = GetOptionalBool(item, "deprecated", false);

            var parameters = new List<ParameterSpec>();
            if (item.TryGetProperty("parameters", out var paramsObj) && paramsObj.ValueKind == JsonValueKind.Array)
            {
                var paramIndex = 0;
                foreach (var param in paramsObj.EnumerateArray())
                {
                    if (param.ValueKind != JsonValueKind.Object)
                    {
                        paramIndex++;
                        continue;
                    }

                    var paramName = GetOptionalString(param, "name", $"arg{paramIndex}");
                    var cType = GetOptionalString(param, "c_type", "void");
                    var variadic = GetOptionalBool(param, "variadic", false);
                    parameters.Add(new ParameterSpec(paramName, cType, variadic));
                    paramIndex++;
                }
            }

            functions.Add(new FunctionSpec(name, cReturnType, parameters, documentation, deprecated));
        }

        functions.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        return functions;
    }

    private static string GetRequiredString(JsonElement obj, string key)
    {
        if (obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        throw new GeneratorException($"IDL function is missing required string '{key}'.");
    }

    private static string GetOptionalString(JsonElement obj, string key, string fallback)
    {
        if (obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return fallback;
    }

    private static bool GetOptionalBool(JsonElement obj, string key, bool fallback)
    {
        if (obj.TryGetProperty(key, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
        {
            return value.GetBoolean();
        }

        return fallback;
    }

    private static string RenderCode(IdlModel model, GeneratorOptions options)
    {
        var methods = model.Functions.Select(function => BuildMethod(function, model, options)).ToArray();

        var classDeclaration = SyntaxFactory.ClassDeclaration(options.ClassName)
            .AddModifiers(ResolveAccessModifierTokens(options.AccessModifier))
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword)
            )
            .AddMembers(methods);

        var namespaceDeclaration = SyntaxFactory.FileScopedNamespaceDeclaration(
            SyntaxFactory.ParseName(options.NamespaceName)
        ).AddMembers(classDeclaration);

        var compilationUnit = SyntaxFactory.CompilationUnit()
            .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Runtime.InteropServices")))
            .AddMembers(namespaceDeclaration)
            .NormalizeWhitespace(eol: "\n", indentation: "    ");

        return
            $"// <auto-generated />\n" +
            $"// Generated by lumenrtc_roslyn_codegen {ToolVersion}\n" +
            compilationUnit.ToFullString() +
            "\n";
    }

    private static MethodDeclarationSyntax BuildMethod(
        FunctionSpec function,
        IdlModel model,
        GeneratorOptions options
    )
    {
        var returnType = MapManagedType(function.CReturnType, model);
        var parameters = new List<ParameterSyntax>(function.Parameters.Count);

        for (var idx = 0; idx < function.Parameters.Count; idx++)
        {
            var parameter = function.Parameters[idx];
            var spec = MapManagedParameter(function, parameter, model);
            var managedName = SanitizeIdentifier(parameter.Name, $"arg{idx}");
            var parameterSyntax = SyntaxFactory.Parameter(SyntaxFactory.Identifier(managedName))
                .WithType(SyntaxFactory.ParseTypeName(spec.TypeName));

            if (!string.IsNullOrWhiteSpace(spec.Modifier))
            {
                var modifierToken = spec.Modifier switch
                {
                    "ref" => SyntaxFactory.Token(SyntaxKind.RefKeyword),
                    "out" => SyntaxFactory.Token(SyntaxKind.OutKeyword),
                    "in" => SyntaxFactory.Token(SyntaxKind.InKeyword),
                    _ => throw new GeneratorException($"Unsupported parameter modifier '{spec.Modifier}'"),
                };
                parameterSyntax = parameterSyntax.WithModifiers(
                    SyntaxFactory.TokenList(modifierToken)
                );
            }

            if (spec.MarshalAsI1)
            {
                var marshalAsAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("MarshalAs"))
                    .WithArgumentList(
                        SyntaxFactory.AttributeArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.AttributeArgument(
                                    SyntaxFactory.ParseExpression("UnmanagedType.I1")
                                )
                            )
                        )
                    );

                parameterSyntax = parameterSyntax.WithAttributeLists(
                    SyntaxFactory.SingletonList(
                        SyntaxFactory.AttributeList(
                            SyntaxFactory.SingletonSeparatedList(marshalAsAttribute)
                        )
                    )
                );
            }

            parameters.Add(parameterSyntax);
        }

        var libraryExpression = ParseExpression(options.LibraryExpression, "--library-expression");
        var callingConventionExpression = ParseExpression(
            $"CallingConvention.{options.CallingConvention}",
            "--calling-convention"
        );

        var dllImportAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("DllImport"))
            .WithArgumentList(
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SeparatedList(
                        new[]
                        {
                            SyntaxFactory.AttributeArgument(libraryExpression),
                            SyntaxFactory.AttributeArgument(callingConventionExpression)
                                .WithNameEquals(
                                    SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName("CallingConvention"))
                                ),
                            SyntaxFactory.AttributeArgument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(function.Name)
                                )
                            ).WithNameEquals(
                                SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName("EntryPoint"))
                            ),
                        }
                    )
                )
            );

        var attributes = new List<AttributeSyntax> { dllImportAttribute };
        if (function.Deprecated)
        {
            var obsoleteMessage = string.IsNullOrWhiteSpace(function.Documentation)
                ? $"{function.Name} is deprecated in ABI metadata."
                : $"{function.Name} is marked deprecated. {function.Documentation}";
            var obsoleteAttribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::System.Obsolete"))
                .WithArgumentList(
                    SyntaxFactory.AttributeArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.AttributeArgument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(obsoleteMessage)
                                )
                            )
                        )
                    )
                );
            attributes.Add(obsoleteAttribute);
        }

        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SeparatedList(attributes)
        );

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName(returnType),
                SyntaxFactory.Identifier(function.Name)
            )
            .WithAttributeLists(SyntaxFactory.SingletonList(attributeList))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    ResolveAccessModifierTokens(options.AccessModifier)
                        .Concat(
                            new[]
                            {
                                SyntaxFactory.Token(SyntaxKind.StaticKeyword),
                                SyntaxFactory.Token(SyntaxKind.ExternKeyword),
                            }
                        )
                )
            )
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    }

    private static ExpressionSyntax ParseExpression(string expressionText, string optionName)
    {
        var expression = SyntaxFactory.ParseExpression(expressionText);
        if (expression.ContainsDiagnostics)
        {
            throw new GeneratorException($"{optionName} produced invalid expression: {expressionText}");
        }

        return expression;
    }

    private static SyntaxToken[] ResolveAccessModifierTokens(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "public" => [SyntaxFactory.Token(SyntaxKind.PublicKeyword)],
            "internal" => [SyntaxFactory.Token(SyntaxKind.InternalKeyword)],
            "private" => [SyntaxFactory.Token(SyntaxKind.PrivateKeyword)],
            "protected" => [SyntaxFactory.Token(SyntaxKind.ProtectedKeyword)],
            "protectedinternal" or "protected_internal" or "protected-internal" => new[]
            {
                SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
                SyntaxFactory.Token(SyntaxKind.InternalKeyword),
            },
            "privateprotected" or "private_protected" or "private-protected" => new[]
            {
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
            },
            _ => throw new GeneratorException($"Unsupported access modifier: '{value}'"),
        };
    }

    private static string SanitizeIdentifier(string value, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        if (!Regex.IsMatch(candidate, "^[A-Za-z_][A-Za-z0-9_]*$"))
        {
            candidate = Regex.Replace(candidate, "[^A-Za-z0-9_]", "_");
            if (!Regex.IsMatch(candidate, "^[A-Za-z_].*$"))
            {
                candidate = $"_{candidate}";
            }
        }

        if (CSharpKeywords.Contains(candidate))
        {
            return $"@{candidate}";
        }

        return candidate;
    }

    private static ParameterRenderSpec MapManagedParameter(
        FunctionSpec function,
        ParameterSpec parameter,
        IdlModel model)
    {
        if (parameter.Variadic)
        {
            return new ParameterRenderSpec("IntPtr", Modifier: null, MarshalAsI1: false);
        }

        var info = ParseCTypeInfo(parameter.CType);
        if (info.PointerDepth == 0)
        {
            var scalarType = MapManagedBaseType(info.BaseType, model);
            if (IsDirectionEnumParameter(function, parameter, scalarType))
            {
                scalarType = "LrtcRtpTransceiverDirection";
            }

            return new ParameterRenderSpec(
                scalarType,
                Modifier: null,
                MarshalAsI1: string.Equals(scalarType, "bool", StringComparison.Ordinal)
            );
        }

        if (info.PointerDepth > 1)
        {
            return new ParameterRenderSpec("IntPtr", Modifier: null, MarshalAsI1: false);
        }

        if (IsRawPointerType(info.BaseType, parameter.Name))
        {
            return new ParameterRenderSpec("IntPtr", Modifier: null, MarshalAsI1: false);
        }

        if (model.StructNames.Contains(info.BaseType))
        {
            var structType = MapManagedBaseType(info.BaseType, model);
            var modifier = IsOutStructPointer(function, parameter) ? "out" : "ref";
            return new ParameterRenderSpec(structType, modifier, MarshalAsI1: false);
        }

        if (PrimitiveTypeMap.TryGetValue(info.BaseType, out var primitive))
        {
            if (IsOutPrimitivePointer(function, parameter))
            {
                return new ParameterRenderSpec(
                    primitive,
                    Modifier: "out",
                    MarshalAsI1: string.Equals(primitive, "bool", StringComparison.Ordinal)
                );
            }
        }

        return new ParameterRenderSpec("IntPtr", Modifier: null, MarshalAsI1: false);
    }

    private static bool IsDirectionEnumParameter(
        FunctionSpec function,
        ParameterSpec parameter,
        string managedType)
    {
        if (!string.Equals(managedType, "int", StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(function.Name, "lrtc_rtp_transceiver_set_direction", StringComparison.Ordinal)
            && string.Equals(parameter.Name, "direction", StringComparison.Ordinal);
    }

    private static bool IsOutPrimitivePointer(FunctionSpec function, ParameterSpec parameter)
    {
        var paramName = parameter.Name.ToLowerInvariant();
        if (paramName is "volume" or "value")
        {
            return true;
        }

        return false;
    }

    private static bool IsOutStructPointer(FunctionSpec function, ParameterSpec parameter)
    {
        var paramName = parameter.Name.ToLowerInvariant();
        if (paramName is "info")
        {
            return true;
        }

        return false;
    }

    private static bool IsRawPointerType(string baseType, string parameterName)
    {
        var normalizedBase = baseType.ToLowerInvariant();
        if (normalizedBase is "void" or "char" or "signed char" or "unsigned char" or "uint8_t" or "int8_t")
        {
            return true;
        }

        var normalizedName = parameterName.ToLowerInvariant();
        if (normalizedName.Contains("buffer", StringComparison.Ordinal)
            || normalizedName.Contains("name", StringComparison.Ordinal)
            || normalizedName.Contains("guid", StringComparison.Ordinal)
            || normalizedName.Contains("sdp", StringComparison.Ordinal)
            || normalizedName.Contains("candidate", StringComparison.Ordinal)
            || normalizedName.Contains("mime", StringComparison.Ordinal)
            || normalizedName.Contains("stream_ids", StringComparison.Ordinal)
            || normalizedName.Contains("data", StringComparison.Ordinal)
            || normalizedName.Contains("label", StringComparison.Ordinal)
            || normalizedName.Contains("protocol", StringComparison.Ordinal)
            || normalizedName.Equals("config", StringComparison.Ordinal)
            || normalizedName.Contains("error", StringComparison.Ordinal)
            || normalizedName.Contains("tones", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static CTypeInfo ParseCTypeInfo(string cType)
    {
        var canonical = NormalizeCType(cType);
        var stripped = StripCTypeQualifiers(canonical);
        var pointerDepth = stripped.Count(ch => ch == '*');
        var baseType = stripped.Replace("*", string.Empty, StringComparison.Ordinal).Trim();
        var isConst = Regex.IsMatch(canonical, "\\bconst\\b");
        return new CTypeInfo(canonical, baseType, pointerDepth, isConst);
    }

    private static string MapManagedType(string cType, IdlModel model)
    {
        if (cType == "...")
        {
            return "IntPtr";
        }

        var info = ParseCTypeInfo(cType);
        if (info.PointerDepth > 0)
        {
            return "IntPtr";
        }

        return MapManagedBaseType(info.BaseType, model);
    }

    private static string MapManagedBaseType(string cTypeBase, IdlModel model)
    {
        var stripped = StripCTypeQualifiers(cTypeBase);

        if (PrimitiveTypeMap.TryGetValue(stripped, out var primitive))
        {
            return primitive;
        }

        if (string.Equals(stripped, "lrtc_stats_failure_cb", StringComparison.Ordinal))
        {
            return "LrtcStatsFailureCb";
        }

        if (model.EnumNames.Contains(stripped) || model.StructNames.Contains(stripped))
        {
            return ToManagedTypeName(stripped, stripTypedefSuffix: true);
        }

        if (stripped.EndsWith("_t", StringComparison.Ordinal))
        {
            return ToManagedTypeName(stripped, stripTypedefSuffix: true);
        }

        if (stripped.EndsWith("_cb", StringComparison.Ordinal))
        {
            return ToManagedTypeName(stripped, stripTypedefSuffix: false);
        }

        if (Regex.IsMatch(stripped, "^[A-Za-z_][A-Za-z0-9_]*$"))
        {
            return ToManagedTypeName(stripped, stripTypedefSuffix: false);
        }

        return "IntPtr";
    }

    private static string NormalizeCType(string value)
    {
        var text = Regex.Replace(value, "\\s+", " ").Trim();
        text = Regex.Replace(text, "\\s*\\*\\s*", "*");
        return text;
    }

    private static string StripCTypeQualifiers(string value)
    {
        var text = NormalizeCType(value);
        text = Regex.Replace(text, "\\b(const|volatile|restrict)\\b", " ");
        text = Regex.Replace(text, "\\b(struct|enum)\\s+", " ");
        text = Regex.Replace(text, "\\s+", " ").Trim();
        text = Regex.Replace(text, "\\s*\\*\\s*", "*");
        return text;
    }

    private static string ToManagedTypeName(string cIdentifier, bool stripTypedefSuffix)
    {
        var value = cIdentifier;
        if (stripTypedefSuffix && value.EndsWith("_t", StringComparison.Ordinal))
        {
            value = value[..^2];
        }

        var parts = value
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]);

        var joined = string.Concat(parts);
        return string.IsNullOrWhiteSpace(joined) ? "IntPtr" : joined;
    }

    private sealed record CTypeInfo(string Canonical, string BaseType, int PointerDepth, bool IsConst);

    private sealed record ParameterRenderSpec(string TypeName, string? Modifier, bool MarshalAsI1);
}

internal sealed record GeneratorOptions(
    string IdlPath,
    string OutputPath,
    string NamespaceName,
    string ClassName,
    string AccessModifier,
    string CallingConvention,
    string LibraryExpression,
    bool Check,
    bool DryRun,
    bool ShowHelp
)
{
    public static GeneratorOptions Parse(string[] args)
    {
        string? idlPath = null;
        string? outputPath = null;
        var namespaceName = "LumenRTC.Interop";
        var className = "NativeMethods";
        var accessModifier = "internal";
        var callingConvention = "Cdecl";
        var libraryExpression = "LibName";
        var check = false;
        var dryRun = false;
        var showHelp = false;

        for (var idx = 0; idx < args.Length; idx++)
        {
            var arg = args[idx];
            switch (arg)
            {
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
                case "--idl":
                    idlPath = ReadValue(args, ref idx, arg);
                    break;
                case "--output":
                    outputPath = ReadValue(args, ref idx, arg);
                    break;
                case "--namespace":
                    namespaceName = ReadValue(args, ref idx, arg);
                    break;
                case "--class-name":
                    className = ReadValue(args, ref idx, arg);
                    break;
                case "--access-modifier":
                    accessModifier = ReadValue(args, ref idx, arg);
                    break;
                case "--calling-convention":
                    callingConvention = ReadValue(args, ref idx, arg);
                    break;
                case "--library-expression":
                    libraryExpression = ReadValue(args, ref idx, arg);
                    break;
                case "--check":
                    check = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    throw new GeneratorException($"Unknown argument: {arg}");
            }
        }

        if (showHelp)
        {
            return new GeneratorOptions(
                idlPath ?? string.Empty,
                outputPath ?? string.Empty,
                namespaceName,
                className,
                accessModifier,
                callingConvention,
                libraryExpression,
                check,
                dryRun,
                ShowHelp: true
            );
        }

        if (string.IsNullOrWhiteSpace(idlPath))
        {
            throw new GeneratorException("Missing required argument --idl <path>.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new GeneratorException("Missing required argument --output <path>.");
        }

        return new GeneratorOptions(
            Path.GetFullPath(idlPath),
            Path.GetFullPath(outputPath),
            namespaceName,
            className,
            accessModifier,
            callingConvention,
            libraryExpression,
            check,
            dryRun,
            ShowHelp: false
        );
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        var next = index + 1;
        if (next >= args.Length || args[next].StartsWith("-", StringComparison.Ordinal))
        {
            throw new GeneratorException($"Argument {option} requires a value.");
        }

        index = next;
        return args[next];
    }
}

internal sealed record IdlModel(
    IReadOnlyList<FunctionSpec> Functions,
    HashSet<string> EnumNames,
    HashSet<string> StructNames,
    string AbiVersion
);

internal sealed record FunctionSpec(
    string Name,
    string CReturnType,
    IReadOnlyList<ParameterSpec> Parameters,
    string Documentation,
    bool Deprecated
);

internal sealed record ParameterSpec(string Name, string CType, bool Variadic);

internal sealed class GeneratorException : Exception
{
    public GeneratorException(string message)
        : base(message)
    {
    }
}
