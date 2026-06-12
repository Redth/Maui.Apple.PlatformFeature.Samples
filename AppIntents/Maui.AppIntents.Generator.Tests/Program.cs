using System.Reflection;
using Maui.AppIntents;
using Maui.AppIntents.BuildTasks;
using Maui.AppIntents.Generator;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

RunValidAuthoringScenario();
RunUnsupportedParameterDiagnosticScenario();

Console.WriteLine("Maui.AppIntents generator regression checks passed.");

static void RunValidAuthoringScenario()
{
    var result = RunGenerator("""
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Maui.AppIntents;

        namespace Sample;

        [AppEnum("Priority", UrlRepresentation = "app://priority")]
        public enum Priority
        {
            [AppEnumCase("Low")]
            Low = 0,

            [AppEnumCase("High")]
            High = 1
        }

        [AppEntity("TaskItem", TypeDisplayName = "Task", Indexed = true, UrlRepresentation = "app://task/{id}")]
        public sealed class TaskItem
        {
            [AppEntityIdentifier]
            public string Id { get; set; } = "";

            [AppEntityDisplay]
            public string Title { get; set; } = "";

            [AppEntitySubtitle]
            public string? Notes { get; set; }

            [AppEntityProperty("Details", IndexingKey = "contentDescription")]
            public string Details { get; set; } = "";

            [AppEntityProperty("Priority")]
            public Priority Priority { get; set; }
        }

        [AppEntityQueryHandler(typeof(TaskItem))]
        public sealed class TaskQueryHandler : IAppEntityQueryHandler<TaskItem>
        {
            public Task<IReadOnlyList<TaskItem>> GetEntitiesAsync(IReadOnlyList<string> identifiers, CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<TaskItem>>(new List<TaskItem>());

            public Task<IReadOnlyList<TaskItem>> SearchEntitiesAsync(string query, CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<TaskItem>>(new List<TaskItem>());

            public Task<IReadOnlyList<TaskItem>> SuggestedEntitiesAsync(CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<TaskItem>>(new List<TaskItem>());
        }

        [AppEntity("Settings", TypeDisplayName = "Settings", Unique = true)]
        public sealed class Settings
        {
            [AppEntityIdentifier]
            public string Id { get; set; } = "settings";

            [AppEntityDisplay]
            public string Title { get; set; } = "Settings";
        }

        [AppEntityQueryHandler(typeof(Settings))]
        public sealed class SettingsQueryHandler : IAppEntityQueryHandler<Settings>
        {
            public Task<IReadOnlyList<Settings>> GetEntitiesAsync(IReadOnlyList<string> identifiers, CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<Settings>>(new List<Settings> { new() });

            public Task<IReadOnlyList<Settings>> SearchEntitiesAsync(string query, CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<Settings>>(new List<Settings> { new() });

            public Task<IReadOnlyList<Settings>> SuggestedEntitiesAsync(CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<Settings>>(new List<Settings> { new() });
        }

        [AppIntent("ShowSettingsIntent", Title = "Show Settings")]
        public sealed class ShowSettingsIntent : IAppIntentHandler<ShowSettingsIntent.Request>
        {
            public sealed record Request(
                [property: IntentParameter("Settings")] AppEntityReference<Settings> Settings);

            public Task<AppIntentResponse> HandleAsync(Request request, CancellationToken cancellationToken)
                => Task.FromResult(AppIntentResponse.Failed(AppIntentErrorCategory.EntityNotFound, "Missing"));
        }

        [AppIntent("CompleteTasksIntent", Title = "Complete Tasks", SupportedModes = AppIntentExecutionModes.Background,
            RequiresConfirmation = true, ConfirmationDialog = "Complete these tasks?", ConfirmationActionName = AppIntentConfirmationAction.Set)]
        [AppIntentSummary("Complete tasks")]
        [AppIntentSummary("Complete tasks urgently", WhenParameter = "Priority", EqualsValue = "High")]
        [AppShortcut("Complete tasks in ${applicationName}", ShortTitle = "Complete Tasks")]
        public sealed class CompleteTasksIntent : IAppIntentHandler<CompleteTasksIntent.Request, int>
        {
            public sealed record Request(
                [property: IntentParameter("Tasks")] IReadOnlyList<AppEntityReference<TaskItem>> Tasks,
                [property: IntentParameter("Priority", IsOptional = true)] Priority? Priority);

            public Task<AppIntentResponse<int>> HandleAsync(Request request, CancellationToken cancellationToken)
                => Task.FromResult(AppIntentResponse<int>.Succeeded(request.Tasks.Count, "Done"));
        }
        """);

    AssertNoMauiDiagnostics(result.Diagnostics);
    AssertNoCompilationErrors(result.CompilationDiagnostics);

    var generated = string.Join("\n", result.GeneratedSources);
    AssertContains(generated, "CompleteTasksIntent");
    AssertContains(generated, "resultKind");
    AssertContains(generated, "isCollection");
    AssertContains(generated, "priority");
    AssertContains(generated, "registry.Map<global::Sample.CompleteTasksIntent.Request, global::Sample.CompleteTasksIntent, int>");

    RunBuildTaskScenario(result.OutputCompilation);
}

static void RunUnsupportedParameterDiagnosticScenario()
{
    var result = RunGenerator("""
        using System.Threading;
        using System.Threading.Tasks;
        using Maui.AppIntents;

        namespace Sample;

        [AppIntent("BadIntent")]
        public sealed class BadIntent : IAppIntentHandler<BadIntent.Request>
        {
            public sealed record Request([property: IntentParameter("Payload")] object Payload);

            public Task<AppIntentResponse> HandleAsync(Request request, CancellationToken cancellationToken)
                => Task.FromResult(AppIntentResponse.Succeeded());
        }
        """);

    if (!result.Diagnostics.Any(diagnostic => diagnostic.Id == "MAUIAI005"))
    {
        Fail("Expected unsupported parameter diagnostic MAUIAI005.");
    }
}

static GeneratorRun RunGenerator(string source)
{
    var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
    var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
    var compilation = CSharpCompilation.Create(
        "GeneratorRegression",
        [syntaxTree],
        References(),
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    var generator = new AppIntentManifestGenerator().AsSourceGenerator();
    var driver = CSharpGeneratorDriver.Create([generator], parseOptions: parseOptions);
    driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

    var runResult = driver.GetRunResult();
    return new GeneratorRun(
        diagnostics.Concat(runResult.Diagnostics).ToArray(),
        outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray(),
        runResult.Results.SelectMany(result => result.GeneratedSources.Select(source => source.SourceText.ToString())).ToArray(),
        outputCompilation);
}

static void RunBuildTaskScenario(Compilation compilation)
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "maui-appintents-generator-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var assemblyPath = Path.Combine(tempRoot, "GeneratorRegression.dll");
        var emitResult = compilation.Emit(assemblyPath);
        if (!emitResult.Success)
        {
            Fail("Could not emit generator regression assembly: " + string.Join(Environment.NewLine, emitResult.Diagnostics));
        }

        var outputDirectory = Path.Combine(tempRoot, "swiftpm");
        var validationManifest = Path.Combine(tempRoot, "validation", "MauiAppIntentValidationManifest.txt");
        var task = new GenerateMauiAppIntentsSwiftPackage
        {
            BuildEngine = new TestBuildEngine(),
            IntermediateAssembly = [new TaskItem(assemblyPath)],
            OutputDirectory = outputDirectory,
            ValidationManifestOutputFile = validationManifest,
            ModuleName = "RegressionIntents",
            MinimumOSVersion = "17.0",
            ArchivePlatforms = "Simulator"
        };

        if (!task.Execute() || !task.HasIntents)
        {
            Fail("GenerateMauiAppIntentsSwiftPackage did not produce generated intents.");
        }

        var swift = File.ReadAllText(Path.Combine(
            outputDirectory,
            "Sources",
            "RegressionIntents",
            "Generated",
            "AppIntents.generated.swift"));
        AssertContains(swift, "@Property(title: \"Priority\")");
        AssertContains(swift, "var tasks: [TaskItemEntity]");
        AssertContains(swift, "ReturnsValue<Int>");
        AssertContains(swift, "@_cdecl(\"MauiAppIntentBridgeDonate\")");

        AssertContains(swift, "static var supportedModes: IntentModes { [.background] }");
        AssertContains(swift, "try await requestConfirmation(actionName: .set, dialog: IntentDialog(\"Complete these tasks?\"))");
        AssertContains(swift, "extension TaskItemEntity: IndexedEntity");
        AssertContains(swift, "set.contentDescription");
        AssertContains(swift, "extension TaskItemEntity: URLRepresentableEntity");
        AssertContains(swift, "extension Priority: URLRepresentableEnum");
        AssertContains(swift, "static var parameterSummary: some ParameterSummary");
        AssertContains(swift, "When(\\.$priority, .equalTo, Priority.high)");
        AssertContains(swift, "mauiAppIntentError(response.errorCategory");
        AssertContains(swift, "struct SettingsEntity: UniqueAppEntity");
        AssertContains(swift, "UniqueAppEntityQuery");
        AssertContains(swift, "@available(iOS 18.0, *)");

        var runtime = File.ReadAllText(Path.Combine(
            outputDirectory,
            "Sources",
            "RegressionIntents",
            "Runtime",
            "MauiAppIntentsRuntime.swift"));
        AssertContains(runtime, "func mauiAppIntentError");
        AssertContains(runtime, "AppIntentError.UserActionRequired");

        var manifest = File.ReadAllText(validationManifest);
        AssertContains(manifest, "property\tpriority");
        AssertContains(manifest, "property\tPriority");
    }
    finally
    {
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for temporary regression artifacts.
        }
    }
}

static MetadataReference[] References()
{
    var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
        .Split(Path.PathSeparator)
        .Where(File.Exists) ?? [];

    var packageAssemblies = new[]
    {
        typeof(AppIntentAttribute).Assembly.Location,
        typeof(AppIntentManifestGenerator).Assembly.Location
    };

    return trustedPlatformAssemblies
        .Concat(packageAssemblies)
        .Distinct(StringComparer.Ordinal)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();
}

static void AssertNoMauiDiagnostics(IEnumerable<Diagnostic> diagnostics)
{
    var mauiDiagnostics = diagnostics.Where(diagnostic => diagnostic.Id.StartsWith("MAUIAI", StringComparison.Ordinal)).ToArray();
    if (mauiDiagnostics.Length != 0)
    {
        Fail("Unexpected Maui.AppIntents diagnostics: " + string.Join(", ", mauiDiagnostics.Select(diagnostic => diagnostic.ToString())));
    }
}

static void AssertNoCompilationErrors(IEnumerable<Diagnostic> diagnostics)
{
    var errors = diagnostics.ToArray();
    if (errors.Length != 0)
    {
        Fail("Generated compilation failed: " + string.Join(Environment.NewLine, errors.Select(diagnostic => diagnostic.ToString())));
    }
}

static void AssertContains(string text, string expected)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
    {
        Fail("Generated output did not contain: " + expected);
    }
}

static void Fail(string message)
{
    Console.Error.WriteLine(message);
    Environment.Exit(1);
}

internal sealed record GeneratorRun(
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<Diagnostic> CompilationDiagnostics,
    IReadOnlyList<string> GeneratedSources,
    Compilation OutputCompilation);

internal sealed class TestBuildEngine : IBuildEngine
{
    public bool ContinueOnError => false;

    public int LineNumberOfTaskNode => 0;

    public int ColumnNumberOfTaskNode => 0;

    public string ProjectFileOfTaskNode => "";

    public bool BuildProjectFile(
        string projectFileName,
        string[] targetNames,
        System.Collections.IDictionary globalProperties,
        System.Collections.IDictionary targetOutputs)
    {
        throw new NotSupportedException();
    }

    public void LogCustomEvent(CustomBuildEventArgs e)
    {
    }

    public void LogErrorEvent(BuildErrorEventArgs e)
    {
        Console.Error.WriteLine(e.Message);
    }

    public void LogMessageEvent(BuildMessageEventArgs e)
    {
    }

    public void LogWarningEvent(BuildWarningEventArgs e)
    {
        Console.Error.WriteLine(e.Message);
    }
}
