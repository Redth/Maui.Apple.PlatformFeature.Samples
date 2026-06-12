using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Maui.AppIntents.Generator;

[Generator]
public sealed class AppIntentManifestGenerator : IIncrementalGenerator
{
    private const string AppIntentAttributeName = "Maui.AppIntents.AppIntentAttribute";
    private const string AppIntentSummaryAttributeName = "Maui.AppIntents.AppIntentSummaryAttribute";
    private const string AppShortcutAttributeName = "Maui.AppIntents.AppShortcutAttribute";
    private const string IntentParameterAttributeName = "Maui.AppIntents.IntentParameterAttribute";
    private const string AppEnumAttributeName = "Maui.AppIntents.AppEnumAttribute";
    private const string AppEnumCaseAttributeName = "Maui.AppIntents.AppEnumCaseAttribute";
    private const string AppEntityAttributeName = "Maui.AppIntents.AppEntityAttribute";
    private const string AppEntityIdentifierAttributeName = "Maui.AppIntents.AppEntityIdentifierAttribute";
    private const string AppEntityDisplayAttributeName = "Maui.AppIntents.AppEntityDisplayAttribute";
    private const string AppEntitySubtitleAttributeName = "Maui.AppIntents.AppEntitySubtitleAttribute";
    private const string AppEntityPropertyAttributeName = "Maui.AppIntents.AppEntityPropertyAttribute";
    private const string AppEntityQueryHandlerAttributeName = "Maui.AppIntents.AppEntityQueryHandlerAttribute";
    private const string AppEntityReferenceTypeName = "Maui.AppIntents.AppEntityReference<TEntity>";
    private const string AppIntentHandlerTypeName = "Maui.AppIntents.IAppIntentHandler<TRequest>";
    private const string AppIntentHandlerWithResultTypeName = "Maui.AppIntents.IAppIntentHandler<TRequest, TResult>";

    private static readonly DiagnosticDescriptor MissingRequestDiagnostic = new DiagnosticDescriptor(
        "MAUIAI001",
        "App intent is missing a Request type",
        "App intent '{0}' must declare a nested Request type",
        "Maui.AppIntents",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingEntityShapeDiagnostic = new DiagnosticDescriptor(
        "MAUIAI002",
        "App entity is missing identifier or display property",
        "App entity '{0}' must expose an identifier property and a display property; use Id/Title conventions or [AppEntityIdentifier]/[AppEntityDisplay]",
        "Maui.AppIntents",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingEntityQueryDiagnostic = new DiagnosticDescriptor(
        "MAUIAI003",
        "App entity parameter has no query handler",
        "App entity '{0}' is used by an intent parameter but has no [AppEntityQueryHandler(typeof({0}))] handler",
        "Maui.AppIntents",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateEntityQueryDiagnostic = new DiagnosticDescriptor(
        "MAUIAI004",
        "App entity has multiple query handlers",
        "App entity '{0}' has multiple query handlers; only one [AppEntityQueryHandler] may target an entity",
        "Maui.AppIntents",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedParameterDiagnostic = new DiagnosticDescriptor(
        "MAUIAI005",
        "Intent parameter type is not supported",
        "Intent parameter '{0}' has unsupported type '{1}'",
        "Maui.AppIntents",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidIdentifierDiagnostic = new DiagnosticDescriptor(
        "MAUIAI006",
        "Generated identifier is invalid",
        "'{0}' should contain at least one letter or digit so it can generate a stable Swift identifier",
        "Maui.AppIntents",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var typeSymbols = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                static (syntaxContext, _) => syntaxContext.SemanticModel.GetDeclaredSymbol((TypeDeclarationSyntax)syntaxContext.Node) as INamedTypeSymbol)
            .Where(static symbol => symbol is not null);

        var enumSymbols = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is EnumDeclarationSyntax { AttributeLists.Count: > 0 },
                static (syntaxContext, _) => syntaxContext.SemanticModel.GetDeclaredSymbol((EnumDeclarationSyntax)syntaxContext.Node) as INamedTypeSymbol)
            .Where(static symbol => symbol is not null);

        var moduleName = context.AnalyzerConfigOptionsProvider.Select(static (options, _) => GetModuleName(options));
        var input = typeSymbols.Collect().Combine(enumSymbols.Collect()).Combine(moduleName);

        context.RegisterSourceOutput(input, static (sourceContext, values) =>
        {
            var ((types, enums), configuredModuleName) = values;
            var model = BuildModel(types, enums, sourceContext);
            var manifestJson = ManifestJsonWriter.Write(model);

            sourceContext.AddSource(
                "MauiAppIntentManifest.g.cs",
                SourceText.From(WriteManifestAttribute(manifestJson), Encoding.UTF8));
            sourceContext.AddSource(
                "MauiAppIntentGeneratedRegistration.g.cs",
                SourceText.From(WriteManagedRegistration(model), Encoding.UTF8));
            sourceContext.AddSource(
                "MauiAppIntentNativeBridge.g.cs",
                SourceText.From(WriteNativeBridge(configuredModuleName), Encoding.UTF8));
        });
    }

    private static AppIntentsModel BuildModel(
        ImmutableArray<INamedTypeSymbol?> typeSymbols,
        ImmutableArray<INamedTypeSymbol?> enumSymbols,
        SourceProductionContext sourceContext)
    {
        var enums = new Dictionary<ITypeSymbol, AppEnumModel>(SymbolEqualityComparer.Default);
        foreach (var enumSymbol in enumSymbols.OfType<INamedTypeSymbol>())
        {
            var appEnumAttribute = GetAttribute(enumSymbol, AppEnumAttributeName);
            if (appEnumAttribute is null)
            {
                continue;
            }

            var enumModel = new AppEnumModel
            {
                Name = enumSymbol.Name,
                FullName = DisplayName(enumSymbol),
                TypeDisplayName = GetConstructorString(appEnumAttribute, 0) ?? SplitWords(enumSymbol.Name),
                UrlRepresentation = GetNamedString(appEnumAttribute, "UrlRepresentation") ?? ""
            };

            foreach (var field in enumSymbol.GetMembers().OfType<IFieldSymbol>().Where(static f => !f.IsImplicitlyDeclared))
            {
                var caseAttribute = GetAttribute(field, AppEnumCaseAttributeName);
                enumModel.Cases.Add(new AppEnumCaseModel
                {
                    Name = field.Name,
                    Title = caseAttribute is null ? SplitWords(field.Name) : GetConstructorString(caseAttribute, 0) ?? SplitWords(field.Name),
                    Subtitle = caseAttribute is null ? "" : GetNamedString(caseAttribute, "Subtitle") ?? "",
                    RawValue = field.HasConstantValue ? Convert.ToInt32(field.ConstantValue, CultureInfo.InvariantCulture) : enumModel.Cases.Count
                });
            }

            enums[enumSymbol] = enumModel;
        }

        var appModel = new AppIntentsModel();
        appModel.AppEnums.AddRange(enums.Values.OrderBy(static e => e.FullName, StringComparer.Ordinal));

        var appEntities = new Dictionary<ITypeSymbol, AppEntityModel>(SymbolEqualityComparer.Default);
        foreach (var typeSymbol in typeSymbols.OfType<INamedTypeSymbol>())
        {
            var appEntityAttribute = GetAttribute(typeSymbol, AppEntityAttributeName);
            if (appEntityAttribute is null)
            {
                continue;
            }

            var entityIdentifier = GetConstructorString(appEntityAttribute, 0) ?? typeSymbol.Name;
            if (!CanGenerateIdentifier(entityIdentifier))
            {
                Report(sourceContext, InvalidIdentifierDiagnostic, typeSymbol, entityIdentifier);
                continue;
            }

            var idProperty = ResolveEntityProperty(typeSymbol, AppEntityIdentifierAttributeName, "Id", "Identifier");
            var displayProperty = ResolveEntityProperty(typeSymbol, AppEntityDisplayAttributeName, "Title", "Name", "DisplayName");
            if (idProperty is null || displayProperty is null)
            {
                Report(sourceContext, MissingEntityShapeDiagnostic, typeSymbol, DisplayName(typeSymbol));
                continue;
            }

            var subtitleProperty = ResolveEntityProperty(typeSymbol, AppEntitySubtitleAttributeName, "Subtitle", "Notes", "Description");
            var entity = new AppEntityModel
            {
                Identifier = entityIdentifier,
                Name = typeSymbol.Name,
                FullName = DisplayName(typeSymbol),
                TypeDisplayName = GetNamedString(appEntityAttribute, "TypeDisplayName") ?? SplitWords(typeSymbol.Name),
                IdProperty = idProperty.Name,
                DisplayProperty = displayProperty.Name,
                SubtitleProperty = subtitleProperty?.Name ?? "",
                Indexed = GetNamedBool(appEntityAttribute, "Indexed"),
                Unique = GetNamedBool(appEntityAttribute, "Unique"),
                UrlRepresentation = GetNamedString(appEntityAttribute, "UrlRepresentation") ?? ""
            };

            foreach (var property in typeSymbol.GetMembers().OfType<IPropertySymbol>().Where(static property => !property.IsImplicitlyDeclared))
            {
                var propertyAttribute = GetAttribute(property, AppEntityPropertyAttributeName);
                if (propertyAttribute is null)
                {
                    continue;
                }

                var (propertyType, isCollection) = UnwrapCollection(UnwrapNullable(property.Type));
                var propertyModel = new AppEntityPropertyModel
                {
                    Name = GetNamedString(propertyAttribute, "Name") ?? LowerFirst(property.Name),
                    SourceProperty = property.Name,
                    SwiftName = LowerFirst(property.Name),
                    Title = GetConstructorString(propertyAttribute, 0) ?? SplitWords(property.Name),
                    TypeName = DisplayName(propertyType),
                    Kind = ParameterKind(propertyType),
                    IsOptional = IsNullableValueType(property.Type) || property.NullableAnnotation == NullableAnnotation.Annotated,
                    IsCollection = isCollection,
                    IndexingKey = NormalizeIndexingKey(GetNamedString(propertyAttribute, "IndexingKey"))
                };

                if (enums.TryGetValue(propertyType, out var appEnum))
                {
                    propertyModel.Kind = "Enum";
                    propertyModel.EnumTypeName = appEnum.FullName;
                }
                else if (!IsSupportedScalarKind(propertyModel.Kind))
                {
                    Report(sourceContext, UnsupportedParameterDiagnostic, property, property.Name, DisplayName(property.Type));
                    continue;
                }

                entity.Properties.Add(propertyModel);
            }

            appEntities[typeSymbol] = entity;
        }

        var entityHandlerCounts = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
        foreach (var typeSymbol in typeSymbols.OfType<INamedTypeSymbol>())
        {
            var queryHandlerAttribute = GetAttribute(typeSymbol, AppEntityQueryHandlerAttributeName);
            var entityType = GetConstructorType(queryHandlerAttribute, 0);
            if (entityType is not null &&
                appEntities.TryGetValue(entityType, out var entity))
            {
                entityHandlerCounts[entityType] = entityHandlerCounts.TryGetValue(entityType, out var count) ? count + 1 : 1;
                if (string.IsNullOrWhiteSpace(entity.QueryHandlerType))
                {
                    entity.QueryHandlerType = DisplayName(typeSymbol);
                }
                else
                {
                    Report(sourceContext, DuplicateEntityQueryDiagnostic, typeSymbol, entity.FullName);
                }
            }
        }

        appModel.AppEntities.AddRange(appEntities.Values.OrderBy(static e => e.FullName, StringComparer.Ordinal));

        foreach (var classSymbol in typeSymbols.OfType<INamedTypeSymbol>())
        {
            var appIntentAttribute = GetAttribute(classSymbol, AppIntentAttributeName);
            if (appIntentAttribute is null)
            {
                continue;
            }

            var intentIdentifier = GetConstructorString(appIntentAttribute, 0) ?? classSymbol.Name;
            if (!CanGenerateIdentifier(intentIdentifier))
            {
                Report(sourceContext, InvalidIdentifierDiagnostic, classSymbol, intentIdentifier);
                continue;
            }

            var requestType = classSymbol.GetTypeMembers("Request").FirstOrDefault();
            if (requestType is null)
            {
                Report(sourceContext, MissingRequestDiagnostic, classSymbol, DisplayName(classSymbol));
                continue;
            }

            var supportedModes = NormalizeSupportedModes(GetNamedInt(appIntentAttribute, "SupportedModes"));
            var requiresConfirmation = GetNamedBool(appIntentAttribute, "RequiresConfirmation");
            var intent = new IntentModel
            {
                Identifier = intentIdentifier,
                Title = GetNamedString(appIntentAttribute, "Title") ?? SplitWords(classSymbol.Name),
                Description = GetNamedString(appIntentAttribute, "Description") ?? "",
                ParameterSummary = GetNamedString(appIntentAttribute, "ParameterSummary") ?? "",
                SupportedModes = supportedModes,
                OpenAppWhenRun = GetNamedBool(appIntentAttribute, "OpenAppWhenRun") || supportedModes.Contains("Foreground"),
                RequiresConfirmation = requiresConfirmation,
                ConfirmationDialog = GetNamedString(appIntentAttribute, "ConfirmationDialog") ?? "",
                ConfirmationActionName = NormalizeConfirmationAction(GetNamedInt(appIntentAttribute, "ConfirmationActionName")),
                HandlerType = DisplayName(classSymbol),
                RequestType = DisplayName(requestType)
            };

            foreach (var summaryAttribute in GetAttributes(classSymbol, AppIntentSummaryAttributeName))
            {
                var format = GetConstructorString(summaryAttribute, 0) ?? "";
                if (string.IsNullOrWhiteSpace(format))
                {
                    continue;
                }

                intent.SummaryCases.Add(new SummaryCaseModel
                {
                    Format = format,
                    WhenParameter = GetNamedString(summaryAttribute, "WhenParameter") ?? "",
                    EqualsValue = GetNamedString(summaryAttribute, "EqualsValue") ?? ""
                });
            }

            ApplyResultModel(classSymbol, intent, enums, appEntities, sourceContext);

            foreach (var shortcutAttribute in GetAttributes(classSymbol, AppShortcutAttributeName))
            {
                intent.Shortcuts.Add(new ShortcutModel
                {
                    Phrase = GetConstructorString(shortcutAttribute, 0) ?? "",
                    ShortTitle = GetNamedString(shortcutAttribute, "ShortTitle") ?? intent.Title,
                    SystemImageName = GetNamedString(shortcutAttribute, "SystemImageName") ?? "sparkles"
                });
            }

            foreach (var property in requestType.GetMembers().OfType<IPropertySymbol>().Where(static p => !p.IsImplicitlyDeclared))
            {
                var parameterAttribute = GetAttribute(property, IntentParameterAttributeName);
                if (parameterAttribute is null)
                {
                    continue;
                }

                var (unwrappedType, isCollection) = UnwrapCollection(UnwrapNullable(property.Type));
                var parameter = new ParameterModel
                {
                    Name = property.Name,
                    SwiftName = LowerFirst(property.Name),
                    Title = GetConstructorString(parameterAttribute, 0) ?? SplitWords(property.Name),
                    TypeName = DisplayName(unwrappedType),
                    IsOptional = IsOptional(property, parameterAttribute),
                    IsCollection = isCollection,
                    Kind = ParameterKind(unwrappedType),
                    Minimum = GetNamedDouble(parameterAttribute, "InclusiveMinimum"),
                    Maximum = GetNamedDouble(parameterAttribute, "InclusiveMaximum")
                };

                if (enums.TryGetValue(unwrappedType, out var appEnum))
                {
                    parameter.Kind = "Enum";
                    parameter.EnumTypeName = appEnum.FullName;
                }
                else if (TryGetEntityReference(unwrappedType, appEntities, out var appEntity))
                {
                    parameter.Kind = "Entity";
                    parameter.EntityTypeName = appEntity.FullName;
                    if (string.IsNullOrWhiteSpace(appEntity.QueryHandlerType))
                    {
                        Report(sourceContext, MissingEntityQueryDiagnostic, property, appEntity.FullName);
                    }
                }
                else if (DisplayName(unwrappedType) == "Maui.AppIntents.AppIntentFile")
                {
                    parameter.Kind = "File";
                }
                else if (!IsSupportedScalarKind(parameter.Kind))
                {
                    Report(sourceContext, UnsupportedParameterDiagnostic, property, property.Name, DisplayName(property.Type));
                    continue;
                }

                intent.Parameters.Add(parameter);
            }

            appModel.Intents.Add(intent);
        }

        appModel.Intents.Sort(static (left, right) => string.CompareOrdinal(left.Identifier, right.Identifier));
        return appModel;
    }

    private static string WriteManifestAttribute(string json)
    {
        return @"// <auto-generated />
[assembly: global::Maui.AppIntents.AppIntentManifestAttribute(0, 1, " + CSharpString(json) + @")]
";
    }

    private static string WriteManagedRegistration(AppIntentsModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace Maui.AppIntents.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class MauiAppIntentGeneratedRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    public static void Register(global::System.IServiceProvider services, global::Maui.AppIntents.MauiAppIntentRegistry registry)");
        sb.AppendLine("    {");
        foreach (var entity in model.AppEntities.Where(static entity => !string.IsNullOrWhiteSpace(entity.QueryHandlerType)))
        {
            sb.AppendLine("        registry.MapEntity<global::" + entity.FullName + ", global::" + entity.QueryHandlerType + ">(\"" + EscapeCSharp(entity.Identifier) + "\", services, entity => new global::Maui.AppIntents.AppIntentEntityValue");
            sb.AppendLine("        {");
            sb.AppendLine("            Id = global::System.Convert.ToString(entity." + entity.IdProperty + ", global::System.Globalization.CultureInfo.InvariantCulture) ?? \"\",");
            sb.AppendLine("            Display = global::System.Convert.ToString(entity." + entity.DisplayProperty + ", global::System.Globalization.CultureInfo.InvariantCulture) ?? \"\",");
            var suffixAfterSubtitle = entity.Properties.Count > 0 ? "," : "";
            if (string.IsNullOrWhiteSpace(entity.SubtitleProperty))
            {
                sb.AppendLine("            Subtitle = null" + suffixAfterSubtitle);
            }
            else
            {
                sb.AppendLine("            Subtitle = global::System.Convert.ToString(entity." + entity.SubtitleProperty + ", global::System.Globalization.CultureInfo.InvariantCulture)" + suffixAfterSubtitle);
            }
            if (entity.Properties.Count > 0)
            {
                sb.AppendLine("            Properties = new global::System.Collections.Generic.Dictionary<string, object?>");
                sb.AppendLine("            {");
                for (var i = 0; i < entity.Properties.Count; i++)
                {
                    var property = entity.Properties[i];
                    var suffix = i == entity.Properties.Count - 1 ? "" : ",";
                    sb.AppendLine("                [\"" + EscapeCSharp(property.Name) + "\"] = entity." + property.SourceProperty + suffix);
                }
                sb.AppendLine("            }");
            }
            sb.AppendLine("        });");
        }
        foreach (var intent in model.Intents)
        {
            if (string.IsNullOrWhiteSpace(intent.ResultKind))
            {
                sb.AppendLine("        registry.Map<global::" + intent.RequestType + ", global::" + intent.HandlerType + ">(\"" + EscapeCSharp(intent.Identifier) + "\", services);");
            }
            else
            {
                sb.AppendLine("        registry.Map<global::" + intent.RequestType + ", global::" + intent.HandlerType + ", " + SourceTypeName(intent.ResultClrTypeName) + ">(\"" + EscapeCSharp(intent.Identifier) + "\", services);");
            }
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string WriteNativeBridge(string moduleName)
    {
        return @"// <auto-generated />
#if IOS || MACCATALYST
using System;
using Foundation;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using ObjCRuntime;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Maui.AppIntents;

public static class MauiAppIntentsNative
{
    private const int RTLD_NOW = 2;
    private const string ModuleName = """ + EscapeCSharp(moduleName) + @""";
    private const string BridgeSetDispatcherSymbol = ""MauiAppIntentBridgeSetDispatcher"";
    private const string BridgeDonateSymbol = ""MauiAppIntentBridgeDonate"";

    private static MauiAppIntentDispatcherAdapter s_adapter;
    private static IntPtr s_nativeLibraryHandle;
    private static MauiAppIntentBridgeSetDispatcherDelegate s_setDispatcher;
    private static MauiAppIntentBridgeDonateDelegate s_donate;
    private static readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void WireUp(IServiceProvider services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var dispatcher = services.GetRequiredService<MauiAppIntentDispatcher>();
        s_adapter = new MauiAppIntentDispatcherAdapter(dispatcher);
        s_setDispatcher ??= ResolveBridgeSetDispatcher();

        if (s_setDispatcher(s_adapter.Handle) == 0)
        {
            throw new InvalidOperationException(""The generated Swift bridge rejected the managed dispatcher. Ensure the generated adapter conforms to MauiAppIntentDispatching."");
        }
    }

    public static bool Donate(string identifier, object payload)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException(""Intent identifier cannot be empty."", nameof(identifier));
        }

        s_donate ??= ResolveBridgeDonate();
        var payloadJson = JsonSerializer.Serialize(payload ?? new { }, s_jsonOptions);
        return s_donate(identifier, payloadJson) != 0;
    }

    private static MauiAppIntentBridgeSetDispatcherDelegate ResolveBridgeSetDispatcher()
    {
        var privateFrameworksPath = NSBundle.MainBundle.PrivateFrameworksPath;
        if (string.IsNullOrWhiteSpace(privateFrameworksPath))
        {
            throw new InvalidOperationException(""The app bundle does not expose a private Frameworks path for the generated App Intents framework."");
        }

        var libraryPath = Path.Combine(privateFrameworksPath, ModuleName + "".framework"", ModuleName);
        s_nativeLibraryHandle = dlopen(libraryPath, RTLD_NOW);
        if (s_nativeLibraryHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException(""Could not load the generated App Intents framework at '"" + libraryPath + ""'. "" + GetDlError());
        }

        var symbol = dlsym(s_nativeLibraryHandle, BridgeSetDispatcherSymbol);
        if (symbol == IntPtr.Zero)
        {
            throw new InvalidOperationException(""Could not find the generated App Intents bridge symbol '"" + BridgeSetDispatcherSymbol + ""' in '"" + libraryPath + ""'. "" + GetDlError());
        }

        return Marshal.GetDelegateForFunctionPointer<MauiAppIntentBridgeSetDispatcherDelegate>(symbol);
    }

    private static MauiAppIntentBridgeDonateDelegate ResolveBridgeDonate()
    {
        EnsureNativeLibraryLoaded();
        var symbol = dlsym(s_nativeLibraryHandle, BridgeDonateSymbol);
        if (symbol == IntPtr.Zero)
        {
            throw new InvalidOperationException(""Could not find the generated App Intents donation symbol '"" + BridgeDonateSymbol + ""'. "" + GetDlError());
        }

        return Marshal.GetDelegateForFunctionPointer<MauiAppIntentBridgeDonateDelegate>(symbol);
    }

    private static void EnsureNativeLibraryLoaded()
    {
        if (s_nativeLibraryHandle != IntPtr.Zero)
        {
            return;
        }

        _ = ResolveBridgeSetDispatcher();
    }

    private static string GetDlError()
    {
        var error = dlerror();
        return error == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(error) ?? string.Empty;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int MauiAppIntentBridgeSetDispatcherDelegate(IntPtr dispatcher);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int MauiAppIntentBridgeDonateDelegate(string identifier, string payload);

    [DllImport(""/usr/lib/libSystem.dylib"", EntryPoint = ""dlopen"")]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport(""/usr/lib/libSystem.dylib"", EntryPoint = ""dlsym"")]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);

    [DllImport(""/usr/lib/libSystem.dylib"", EntryPoint = ""dlerror"")]
    private static extern IntPtr dlerror();
}

[Protocol(Name = ""MauiAppIntentDispatching"")]
public interface IMauiAppIntentDispatching
{
    [Export(""dispatchIntentWithIdentifier:payload:"")]
    string DispatchIntent(string identifier, string payload);

    [Export(""dispatchEntityQueryWithIdentifier:operation:payload:"")]
    string DispatchEntityQuery(string identifier, string operation, string payload);
}

[Register(""MauiAppIntentDispatcherAdapter"")]
internal sealed class MauiAppIntentDispatcherAdapter : NSObject, IMauiAppIntentDispatching
{
    private readonly MauiAppIntentDispatcher dispatcher;

    public MauiAppIntentDispatcherAdapter(MauiAppIntentDispatcher dispatcher)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [Export(""dispatchIntentWithIdentifier:payload:"")]
    public string DispatchIntent(string identifier, string payload)
    {
        return dispatcher.Dispatch(identifier, payload);
    }

    [Export(""dispatchEntityQueryWithIdentifier:operation:payload:"")]
    public string DispatchEntityQuery(string identifier, string operation, string payload)
    {
        return dispatcher.DispatchEntityQuery(identifier, operation, payload);
    }
}
#else
using System;

namespace Maui.AppIntents;

public static class MauiAppIntentsNative
{
    public static void WireUp(IServiceProvider services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }
    }

    public static bool Donate(string identifier, object payload)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException(""Intent identifier cannot be empty."", nameof(identifier));
        }

        return false;
    }
}
#endif
";
    }

    private static string GetModuleName(AnalyzerConfigOptionsProvider optionsProvider)
    {
        if (optionsProvider.GlobalOptions.TryGetValue("build_property.MauiAppIntentsModuleName", out var configured) &&
            IsSwiftIdentifier(configured))
        {
            return configured;
        }

        if (optionsProvider.GlobalOptions.TryGetValue("build_property.AssemblyName", out var assemblyName))
        {
            return SanitizeIdentifier(assemblyName + "AppIntents");
        }

        return "GeneratedAppIntents";
    }

    private static void ApplyResultModel(
        INamedTypeSymbol handlerType,
        IntentModel intent,
        Dictionary<ITypeSymbol, AppEnumModel> enums,
        Dictionary<ITypeSymbol, AppEntityModel> entities,
        SourceProductionContext sourceContext)
    {
        var resultType = handlerType.AllInterfaces.FirstOrDefault(i =>
            string.Equals(i.OriginalDefinition.ToDisplayString(), AppIntentHandlerWithResultTypeName, StringComparison.Ordinal));
        if (resultType is null || resultType.TypeArguments.Length != 2)
        {
            return;
        }

        var (unwrappedType, isCollection) = UnwrapCollection(UnwrapNullable(resultType.TypeArguments[1]));
        intent.ResultClrTypeName = DisplayName(resultType.TypeArguments[1]);
        intent.ResultTypeName = DisplayName(unwrappedType);
        intent.ResultKind = ParameterKind(unwrappedType);
        intent.ResultIsCollection = isCollection;

        if (enums.TryGetValue(unwrappedType, out var appEnum))
        {
            intent.ResultKind = "Enum";
            intent.ResultEnumTypeName = appEnum.FullName;
        }
        else if (TryGetEntityReference(unwrappedType, entities, out var appEntity))
        {
            intent.ResultKind = "Entity";
            intent.ResultEntityTypeName = appEntity.FullName;
            if (string.IsNullOrWhiteSpace(appEntity.QueryHandlerType))
            {
                Report(sourceContext, MissingEntityQueryDiagnostic, handlerType, appEntity.FullName);
            }
        }
        else if (!IsSupportedScalarKind(intent.ResultKind))
        {
            Report(sourceContext, UnsupportedParameterDiagnostic, handlerType, "result", DisplayName(resultType.TypeArguments[1]));
            intent.ResultKind = "";
            intent.ResultTypeName = "";
            intent.ResultIsCollection = false;
        }
    }

    private static bool IsOptional(IPropertySymbol property, AttributeData parameterAttribute)
    {
        return IsNullableValueType(property.Type) ||
            property.NullableAnnotation == NullableAnnotation.Annotated ||
            GetNamedBool(parameterAttribute, "IsOptional");
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        return IsNullableValueType(type) && type is INamedTypeSymbol namedType
            ? namedType.TypeArguments[0]
            : type;
    }

    private static (ITypeSymbol Type, bool IsCollection) UnwrapCollection(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            return (arrayType.ElementType, true);
        }

        if (type is INamedTypeSymbol namedType && namedType.TypeArguments.Length == 1)
        {
            var original = namedType.OriginalDefinition.ToDisplayString();
            if (original is "System.Collections.Generic.IReadOnlyList<T>" or
                "System.Collections.Generic.IList<T>" or
                "System.Collections.Generic.IEnumerable<T>" or
                "System.Collections.Generic.List<T>")
            {
                return (namedType.TypeArguments[0], true);
            }
        }

        return (type, false);
    }

    private static IPropertySymbol? ResolveEntityProperty(INamedTypeSymbol entityType, string attributeName, params string[] fallbackNames)
    {
        var properties = entityType.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static property => !property.IsImplicitlyDeclared)
            .ToList();

        var attributed = properties.FirstOrDefault(property => GetAttribute(property, attributeName) is not null);
        if (attributed is not null)
        {
            return attributed;
        }

        foreach (var fallbackName in fallbackNames)
        {
            var property = properties.FirstOrDefault(property => string.Equals(property.Name, fallbackName, StringComparison.Ordinal));
            if (property is not null)
            {
                return property;
            }
        }

        return null;
    }

    private static bool TryGetEntityReference(
        ITypeSymbol type,
        Dictionary<ITypeSymbol, AppEntityModel> entities,
        out AppEntityModel entity)
    {
        entity = null!;
        if (type is not INamedTypeSymbol namedType ||
            namedType.TypeArguments.Length != 1 ||
            !string.Equals(namedType.OriginalDefinition.ToDisplayString(), AppEntityReferenceTypeName, StringComparison.Ordinal))
        {
            return false;
        }

        return entities.TryGetValue(namedType.TypeArguments[0], out entity);
    }

    private static bool IsNullableValueType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    private static string ParameterKind(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_String => "String",
            SpecialType.System_Int16 or SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_UInt16 or SpecialType.System_UInt32 => "Int",
            SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal => "Double",
            SpecialType.System_Boolean => "Bool",
            _ => DisplayName(type) is "System.DateTime" or "System.DateTimeOffset" ? "Date" : "Unsupported"
        };
    }

    private static bool IsSupportedScalarKind(string kind)
    {
        return kind is "String" or "Int" or "Double" or "Bool" or "Date";
    }

    private static string DisplayName(ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
    }

    private static string SourceTypeName(string displayName)
    {
        return displayName switch
        {
            "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or
            "float" or "double" or "decimal" or "string" or "object" => displayName,
            _ when displayName.StartsWith("global::", StringComparison.Ordinal) => displayName,
            _ => "global::" + displayName
        };
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string fullName)
    {
        return GetAttributes(symbol, fullName).FirstOrDefault();
    }

    private static IEnumerable<AttributeData> GetAttributes(ISymbol symbol, string fullName)
    {
        return symbol.GetAttributes()
            .Where(a => string.Equals(a.AttributeClass?.ToDisplayString(), fullName, StringComparison.Ordinal));
    }

    private static string? GetConstructorString(AttributeData attribute, int index)
    {
        return attribute.ConstructorArguments.Length > index
            ? attribute.ConstructorArguments[index].Value as string
            : null;
    }

    private static ITypeSymbol? GetConstructorType(AttributeData? attribute, int index)
    {
        return attribute is not null && attribute.ConstructorArguments.Length > index
            ? attribute.ConstructorArguments[index].Value as ITypeSymbol
            : null;
    }

    private static string? GetNamedString(AttributeData attribute, string name)
    {
        foreach (var pair in attribute.NamedArguments)
        {
            if (string.Equals(pair.Key, name, StringComparison.Ordinal))
            {
                return pair.Value.Value as string;
            }
        }

        return null;
    }

    private static bool GetNamedBool(AttributeData attribute, string name)
    {
        foreach (var pair in attribute.NamedArguments)
        {
            if (string.Equals(pair.Key, name, StringComparison.Ordinal) && pair.Value.Value is bool value)
            {
                return value;
            }
        }

        return false;
    }

    private static double? GetNamedDouble(AttributeData attribute, string name)
    {
        foreach (var pair in attribute.NamedArguments)
        {
            if (string.Equals(pair.Key, name, StringComparison.Ordinal) &&
                pair.Value.Value is double value &&
                !double.IsNaN(value))
            {
                return value;
            }
        }

        return null;
    }

    private static int GetNamedInt(AttributeData attribute, string name)
    {
        foreach (var pair in attribute.NamedArguments)
        {
            if (string.Equals(pair.Key, name, StringComparison.Ordinal) && pair.Value.Value is not null)
            {
                try
                {
                    return Convert.ToInt32(pair.Value.Value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return 0;
                }
            }
        }

        return 0;
    }

    private static string NormalizeSupportedModes(int flags)
    {
        var foreground = (flags & 1) != 0;
        var background = (flags & 2) != 0;
        if (foreground && background)
        {
            return "ForegroundAndBackground";
        }

        if (foreground)
        {
            return "Foreground";
        }

        if (background)
        {
            return "Background";
        }

        return "";
    }

    private static string NormalizeConfirmationAction(int value)
    {
        // Maps AppIntentConfirmationAction to the Swift ConfirmationActionName member token,
        // including backticks for Swift reserved words.
        switch (value)
        {
            case 0: return "`continue`";
            case 1: return "add";
            case 2: return "book";
            case 3: return "buy";
            case 4: return "call";
            case 5: return "create";
            case 6: return "`do`";
            case 7: return "download";
            case 8: return "go";
            case 9: return "open";
            case 10: return "order";
            case 11: return "pay";
            case 12: return "post";
            case 13: return "run";
            case 14: return "search";
            case 15: return "send";
            case 16: return "set";
            case 17: return "share";
            case 18: return "start";
            case 19: return "toggle";
            case 20: return "turnOff";
            case 21: return "turnOn";
            case 22: return "view";
            default: return "`continue`";
        }
    }

    private static string NormalizeIndexingKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "";
        }

        return key!.Trim() switch
        {
            "contentDescription" => "contentDescription",
            "title" => "title",
            "keywords" => "keywords",
            _ => ""
        };
    }

    private static string LowerFirst(string value)
    {
        return string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string SplitWords(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var sb = new StringBuilder();
        sb.Append(value[0]);
        for (var i = 1; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && char.IsLower(value[i - 1]))
            {
                sb.Append(' ');
            }
            sb.Append(value[i]);
        }
        return sb.ToString();
    }

    private static string SanitizeIdentifier(string value)
    {
        var chars = (value ?? "").Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 || char.IsDigit(chars[0]) ? "GeneratedAppIntents" : new string(chars);
    }

    private static bool IsSwiftIdentifier(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            (char.IsLetter(value[0]) || value[0] == '_') &&
            value.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
    }

    private static bool CanGenerateIdentifier(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Any(char.IsLetterOrDigit);
    }

    private static void Report(SourceProductionContext context, DiagnosticDescriptor descriptor, ISymbol symbol, params object[] args)
    {
        context.ReportDiagnostic(Diagnostic.Create(descriptor, symbol.Locations.FirstOrDefault(), args));
    }

    private static string CSharpString(string value)
    {
        return "@\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    }

    private static string EscapeCSharp(string value)
    {
        return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

}

internal sealed class AppIntentsModel
{
    public List<IntentModel> Intents { get; } = new();

    public List<AppEnumModel> AppEnums { get; } = new();

    public List<AppEntityModel> AppEntities { get; } = new();
}

internal sealed class IntentModel
{
    public string Identifier { get; set; } = "";

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public string ParameterSummary { get; set; } = "";

    public string SupportedModes { get; set; } = "";

    public bool OpenAppWhenRun { get; set; }

    public bool RequiresConfirmation { get; set; }

    public string ConfirmationDialog { get; set; } = "";

    public string ConfirmationActionName { get; set; } = "";

    public string HandlerType { get; set; } = "";

    public string RequestType { get; set; } = "";

    public string ResultKind { get; set; } = "";

    public string ResultTypeName { get; set; } = "";

    public string ResultClrTypeName { get; set; } = "";

    public string ResultEnumTypeName { get; set; } = "";

    public string ResultEntityTypeName { get; set; } = "";

    public bool ResultIsCollection { get; set; }

    public List<ParameterModel> Parameters { get; } = new();

    public List<ShortcutModel> Shortcuts { get; } = new();

    public List<SummaryCaseModel> SummaryCases { get; } = new();
}

internal sealed class SummaryCaseModel
{
    public string Format { get; set; } = "";

    public string WhenParameter { get; set; } = "";

    public string EqualsValue { get; set; } = "";
}

internal sealed class ParameterModel
{
    public string Name { get; set; } = "";

    public string SwiftName { get; set; } = "";

    public string Title { get; set; } = "";

    public string TypeName { get; set; } = "";

    public string Kind { get; set; } = "String";

    public bool IsOptional { get; set; }

    public bool IsCollection { get; set; }

    public double? Minimum { get; set; }

    public double? Maximum { get; set; }

    public string EnumTypeName { get; set; } = "";

    public string EntityTypeName { get; set; } = "";
}

internal sealed class ShortcutModel
{
    public string Phrase { get; set; } = "";

    public string ShortTitle { get; set; } = "";

    public string SystemImageName { get; set; } = "";
}

internal sealed class AppEnumModel
{
    public string Name { get; set; } = "";

    public string FullName { get; set; } = "";

    public string TypeDisplayName { get; set; } = "";

    public string UrlRepresentation { get; set; } = "";

    public List<AppEnumCaseModel> Cases { get; } = new();
}

internal sealed class AppEnumCaseModel
{
    public string Name { get; set; } = "";

    public string Title { get; set; } = "";

    public string Subtitle { get; set; } = "";

    public int RawValue { get; set; }
}

internal sealed class AppEntityModel
{
    public string Identifier { get; set; } = "";

    public string Name { get; set; } = "";

    public string FullName { get; set; } = "";

    public string TypeDisplayName { get; set; } = "";

    public string IdProperty { get; set; } = "";

    public string DisplayProperty { get; set; } = "";

    public string SubtitleProperty { get; set; } = "";

    public string QueryHandlerType { get; set; } = "";

    public bool Indexed { get; set; }

    public bool Unique { get; set; }

    public string UrlRepresentation { get; set; } = "";

    public List<AppEntityPropertyModel> Properties { get; } = new();
}

internal sealed class AppEntityPropertyModel
{
    public string Name { get; set; } = "";

    public string SourceProperty { get; set; } = "";

    public string SwiftName { get; set; } = "";

    public string Title { get; set; } = "";

    public string TypeName { get; set; } = "";

    public string Kind { get; set; } = "String";

    public bool IsOptional { get; set; }

    public bool IsCollection { get; set; }

    public string IndexingKey { get; set; } = "";

    public string EnumTypeName { get; set; } = "";
}

internal static class ManifestJsonWriter
{
    public static string Write(AppIntentsModel model)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        WriteArray(sb, "intents", model.Intents, WriteIntent);
        sb.Append(',');
        WriteArray(sb, "appEnums", model.AppEnums, WriteAppEnum);
        sb.Append(',');
        WriteArray(sb, "appEntities", model.AppEntities, WriteAppEntity);
        sb.Append('}');
        return sb.ToString();
    }

    private static void WriteIntent(StringBuilder sb, IntentModel intent)
    {
        sb.Append('{');
        WriteProperty(sb, "identifier", intent.Identifier);
        sb.Append(',');
        WriteProperty(sb, "title", intent.Title);
        sb.Append(',');
        WriteProperty(sb, "description", intent.Description);
        sb.Append(',');
        WriteProperty(sb, "parameterSummary", intent.ParameterSummary);
        sb.Append(',');
        WriteProperty(sb, "supportedModes", intent.SupportedModes);
        sb.Append(',');
        WriteProperty(sb, "openAppWhenRun", intent.OpenAppWhenRun);
        sb.Append(',');
        WriteProperty(sb, "requiresConfirmation", intent.RequiresConfirmation);
        sb.Append(',');
        WriteProperty(sb, "confirmationDialog", intent.ConfirmationDialog);
        sb.Append(',');
        WriteProperty(sb, "confirmationActionName", intent.ConfirmationActionName);
        sb.Append(',');
        WriteProperty(sb, "handlerType", intent.HandlerType);
        sb.Append(',');
        WriteProperty(sb, "requestType", intent.RequestType);
        sb.Append(',');
        WriteProperty(sb, "resultKind", intent.ResultKind);
        sb.Append(',');
        WriteProperty(sb, "resultTypeName", intent.ResultTypeName);
        sb.Append(',');
        WriteProperty(sb, "resultClrTypeName", intent.ResultClrTypeName);
        sb.Append(',');
        WriteProperty(sb, "resultEnumTypeName", intent.ResultEnumTypeName);
        sb.Append(',');
        WriteProperty(sb, "resultEntityTypeName", intent.ResultEntityTypeName);
        sb.Append(',');
        WriteProperty(sb, "resultIsCollection", intent.ResultIsCollection);
        sb.Append(',');
        WriteArray(sb, "parameters", intent.Parameters, WriteParameter);
        sb.Append(',');
        WriteArray(sb, "shortcuts", intent.Shortcuts, WriteShortcut);
        sb.Append(',');
        WriteArray(sb, "summaryCases", intent.SummaryCases, WriteSummaryCase);
        sb.Append('}');
    }

    private static void WriteSummaryCase(StringBuilder sb, SummaryCaseModel summaryCase)
    {
        sb.Append('{');
        WriteProperty(sb, "format", summaryCase.Format);
        sb.Append(',');
        WriteProperty(sb, "whenParameter", summaryCase.WhenParameter);
        sb.Append(',');
        WriteProperty(sb, "equalsValue", summaryCase.EqualsValue);
        sb.Append('}');
    }

    private static void WriteParameter(StringBuilder sb, ParameterModel parameter)
    {
        sb.Append('{');
        WriteProperty(sb, "name", parameter.Name);
        sb.Append(',');
        WriteProperty(sb, "swiftName", parameter.SwiftName);
        sb.Append(',');
        WriteProperty(sb, "title", parameter.Title);
        sb.Append(',');
        WriteProperty(sb, "typeName", parameter.TypeName);
        sb.Append(',');
        WriteProperty(sb, "kind", parameter.Kind);
        sb.Append(',');
        WriteProperty(sb, "isOptional", parameter.IsOptional);
        sb.Append(',');
        WriteProperty(sb, "isCollection", parameter.IsCollection);
        sb.Append(',');
        WriteProperty(sb, "minimum", parameter.Minimum);
        sb.Append(',');
        WriteProperty(sb, "maximum", parameter.Maximum);
        sb.Append(',');
        WriteProperty(sb, "enumTypeName", parameter.EnumTypeName);
        sb.Append(',');
        WriteProperty(sb, "entityTypeName", parameter.EntityTypeName);
        sb.Append('}');
    }

    private static void WriteShortcut(StringBuilder sb, ShortcutModel shortcut)
    {
        sb.Append('{');
        WriteProperty(sb, "phrase", shortcut.Phrase);
        sb.Append(',');
        WriteProperty(sb, "shortTitle", shortcut.ShortTitle);
        sb.Append(',');
        WriteProperty(sb, "systemImageName", shortcut.SystemImageName);
        sb.Append('}');
    }

    private static void WriteAppEnum(StringBuilder sb, AppEnumModel appEnum)
    {
        sb.Append('{');
        WriteProperty(sb, "name", appEnum.Name);
        sb.Append(',');
        WriteProperty(sb, "fullName", appEnum.FullName);
        sb.Append(',');
        WriteProperty(sb, "typeDisplayName", appEnum.TypeDisplayName);
        sb.Append(',');
        WriteProperty(sb, "urlRepresentation", appEnum.UrlRepresentation);
        sb.Append(',');
        WriteArray(sb, "cases", appEnum.Cases, WriteAppEnumCase);
        sb.Append('}');
    }

    private static void WriteAppEnumCase(StringBuilder sb, AppEnumCaseModel appEnumCase)
    {
        sb.Append('{');
        WriteProperty(sb, "name", appEnumCase.Name);
        sb.Append(',');
        WriteProperty(sb, "title", appEnumCase.Title);
        sb.Append(',');
        WriteProperty(sb, "subtitle", appEnumCase.Subtitle);
        sb.Append(',');
        WriteProperty(sb, "rawValue", appEnumCase.RawValue);
        sb.Append('}');
    }

    private static void WriteAppEntity(StringBuilder sb, AppEntityModel appEntity)
    {
        sb.Append('{');
        WriteProperty(sb, "identifier", appEntity.Identifier);
        sb.Append(',');
        WriteProperty(sb, "name", appEntity.Name);
        sb.Append(',');
        WriteProperty(sb, "fullName", appEntity.FullName);
        sb.Append(',');
        WriteProperty(sb, "typeDisplayName", appEntity.TypeDisplayName);
        sb.Append(',');
        WriteProperty(sb, "idProperty", appEntity.IdProperty);
        sb.Append(',');
        WriteProperty(sb, "displayProperty", appEntity.DisplayProperty);
        sb.Append(',');
        WriteProperty(sb, "subtitleProperty", appEntity.SubtitleProperty);
        sb.Append(',');
        WriteProperty(sb, "queryHandlerType", appEntity.QueryHandlerType);
        sb.Append(',');
        WriteProperty(sb, "indexed", appEntity.Indexed);
        sb.Append(',');
        WriteProperty(sb, "unique", appEntity.Unique);
        sb.Append(',');
        WriteProperty(sb, "urlRepresentation", appEntity.UrlRepresentation);
        sb.Append(',');
        WriteArray(sb, "properties", appEntity.Properties, WriteAppEntityProperty);
        sb.Append('}');
    }

    private static void WriteAppEntityProperty(StringBuilder sb, AppEntityPropertyModel property)
    {
        sb.Append('{');
        WriteProperty(sb, "name", property.Name);
        sb.Append(',');
        WriteProperty(sb, "sourceProperty", property.SourceProperty);
        sb.Append(',');
        WriteProperty(sb, "swiftName", property.SwiftName);
        sb.Append(',');
        WriteProperty(sb, "title", property.Title);
        sb.Append(',');
        WriteProperty(sb, "typeName", property.TypeName);
        sb.Append(',');
        WriteProperty(sb, "kind", property.Kind);
        sb.Append(',');
        WriteProperty(sb, "isOptional", property.IsOptional);
        sb.Append(',');
        WriteProperty(sb, "isCollection", property.IsCollection);
        sb.Append(',');
        WriteProperty(sb, "indexingKey", property.IndexingKey);
        sb.Append(',');
        WriteProperty(sb, "enumTypeName", property.EnumTypeName);
        sb.Append('}');
    }

    private static void WriteArray<T>(StringBuilder sb, string name, IReadOnlyList<T> values, Action<StringBuilder, T> writer)
    {
        WriteName(sb, name);
        sb.Append('[');
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            writer(sb, values[i]);
        }
        sb.Append(']');
    }

    private static void WriteProperty(StringBuilder sb, string name, string value)
    {
        WriteName(sb, name);
        WriteString(sb, value);
    }

    private static void WriteProperty(StringBuilder sb, string name, bool value)
    {
        WriteName(sb, name);
        sb.Append(value ? "true" : "false");
    }

    private static void WriteProperty(StringBuilder sb, string name, int value)
    {
        WriteName(sb, name);
        sb.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void WriteProperty(StringBuilder sb, string name, double? value)
    {
        WriteName(sb, name);
        if (value.HasValue)
        {
            sb.Append(value.Value.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append("null");
        }
    }

    private static void WriteName(StringBuilder sb, string name)
    {
        WriteString(sb, name);
        sb.Append(':');
    }

    private static void WriteString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (var ch in value ?? "")
        {
            switch (ch)
            {
                case '\\':
                    sb.Append(@"\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\b':
                    sb.Append(@"\b");
                    break;
                case '\f':
                    sb.Append(@"\f");
                    break;
                case '\n':
                    sb.Append(@"\n");
                    break;
                case '\r':
                    sb.Append(@"\r");
                    break;
                case '\t':
                    sb.Append(@"\t");
                    break;
                default:
                    if (char.IsControl(ch))
                    {
                        sb.Append("\\u");
                        sb.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                    break;
            }
        }
        sb.Append('"');
    }
}
