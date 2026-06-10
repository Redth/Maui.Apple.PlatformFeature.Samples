using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Maui.AppIntents.BuildTasks;

public sealed class GenerateMauiAppIntentsSwiftPackage : Task
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public ITaskItem[] IntermediateAssembly { get; set; } = Array.Empty<ITaskItem>();

    [Required]
    public string OutputDirectory { get; set; } = "";

    [Required]
    public string ValidationManifestOutputFile { get; set; } = "";

    [Required]
    public string ModuleName { get; set; } = "";

    [Required]
    public string MinimumOSVersion { get; set; } = "";

    [Required]
    public string ArchivePlatforms { get; set; } = "";

    [Output]
    public bool HasIntents { get; set; }

    public override bool Execute()
    {
        try
        {
            if (!IsSwiftIdentifier(ModuleName))
            {
                Log.LogError("MauiAppIntentsModuleName must be a valid Swift module identifier. Use only letters, digits, and underscores, and do not start with a digit.");
                return false;
            }

            var assemblyPath = GetIntermediateAssemblyPath();
            var manifest = ReadManifest(assemblyPath);
            HasIntents = manifest.Intents.Count > 0;

            Directory.CreateDirectory(OutputDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(ValidationManifestOutputFile)!);

            WritePackage();
            WriteRuntimeSwift();
            WriteGeneratedSwift(manifest);
            WriteValidationManifest(manifest);
            WriteBuildScript();

            Log.LogMessage(
                MessageImportance.High,
                "Generated SwiftPM package for {0} MAUI App Intent declaration(s), {1} AppEnum declaration(s), and {2} AppEntity declaration(s).",
                manifest.Intents.Count,
                ReferencedEnums(manifest).Count,
                ReferencedEntities(manifest).Count);

            return !Log.HasLoggedErrors;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true);
            return false;
        }
    }

    private string GetIntermediateAssemblyPath()
    {
        var path = IntermediateAssembly
            .Select(item => item.GetMetadata("FullPath"))
            .Concat(IntermediateAssembly.Select(item => item.ItemSpec))
            .FirstOrDefault(File.Exists);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("The app intermediate assembly was not provided to the App Intents Swift package generator.");
        }

        return path;
    }

    private AppIntentsManifest ReadManifest(string assemblyPath)
    {
        using var stream = File.Open(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var peReader = new PEReader(stream);
        var metadataReader = peReader.GetMetadataReader();
        var chunks = new List<ManifestChunk>();

        foreach (var handle in metadataReader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadataReader.GetCustomAttribute(handle);
            if (!IsManifestAttribute(metadataReader, attribute))
            {
                continue;
            }

            var reader = metadataReader.GetBlobReader(attribute.Value);
            if (reader.ReadUInt16() != 1)
            {
                continue;
            }

            chunks.Add(new ManifestChunk(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadSerializedString() ?? ""));
        }

        if (chunks.Count == 0)
        {
            return new AppIntentsManifest();
        }

        var total = chunks[0].Total;
        if (chunks.Any(chunk => chunk.Total != total) || chunks.Select(chunk => chunk.Index).Distinct().Count() != total)
        {
            throw new InvalidOperationException("The generated App Intents manifest attributes are incomplete or inconsistent.");
        }

        var json = string.Concat(chunks.OrderBy(chunk => chunk.Index).Select(chunk => chunk.Json));
        return JsonSerializer.Deserialize<AppIntentsManifest>(json, JsonOptions) ?? new AppIntentsManifest();
    }

    private static bool IsManifestAttribute(MetadataReader reader, CustomAttribute attribute)
    {
        EntityHandle parent;
        switch (attribute.Constructor.Kind)
        {
            case HandleKind.MemberReference:
                parent = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
                break;
            case HandleKind.MethodDefinition:
                parent = reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType();
                break;
            default:
                return false;
        }

        if (parent.Kind == HandleKind.TypeReference)
        {
            var type = reader.GetTypeReference((TypeReferenceHandle)parent);
            return reader.GetString(type.Namespace) == "Maui.AppIntents" &&
                reader.GetString(type.Name) == "AppIntentManifestAttribute";
        }

        if (parent.Kind == HandleKind.TypeDefinition)
        {
            var type = reader.GetTypeDefinition((TypeDefinitionHandle)parent);
            return reader.GetString(type.Namespace) == "Maui.AppIntents" &&
                reader.GetString(type.Name) == "AppIntentManifestAttribute";
        }

        return false;
    }

    private void WritePackage()
    {
        var package = Path.Combine(OutputDirectory, "Package.swift");
        File.WriteAllText(package,
@"// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: """ + EscapeSwift(ModuleName) + @""",
    platforms: [
        .iOS(""" + EscapeSwift(MinimumOSVersion) + @""")
    ],
    products: [
        .library(name: """ + EscapeSwift(ModuleName) + @""", type: .dynamic, targets: [""" + EscapeSwift(ModuleName) + @"""])
    ],
    targets: [
        .target(
            name: """ + EscapeSwift(ModuleName) + @""",
            path: ""Sources/" + EscapeSwift(ModuleName) + @""",
            linkerSettings: [
                .linkedFramework(""AppIntents""),
                .linkedFramework(""Foundation"")
            ]
        )
    ],
    swiftLanguageModes: [.v5]
)
");
    }

    private void WriteRuntimeSwift()
    {
        var runtimeDir = Path.Combine(OutputDirectory, "Sources", ModuleName, "Runtime");
        Directory.CreateDirectory(runtimeDir);
        File.WriteAllText(Path.Combine(runtimeDir, "MauiAppIntentsRuntime.swift"),
@"import AppIntents
import Foundation

@objc(MauiAppIntentDispatching) public protocol MauiAppIntentDispatching: AnyObject {
    func dispatchIntent(identifier: String, payload: String) -> String
    func dispatchEntityQuery(identifier: String, operation: String, payload: String) -> String
}

@objc(MauiAppIntentBridge) public final class MauiAppIntentBridge: NSObject {
    @objc public static let shared = MauiAppIntentBridge()
    @objc public weak var dispatcher: MauiAppIntentDispatching?

    private override init() {
        super.init()
    }

    public func perform(identifier: String, payload: [String: Any]) throws -> MauiAppIntentResponse {
        guard let dispatcher else {
            throw MauiAppIntentRuntimeError.notReady
        }

        let data = try JSONSerialization.data(withJSONObject: payload, options: [])
        let json = String(data: data, encoding: .utf8) ?? ""{}""
        let responseJson = dispatcher.dispatchIntent(identifier: identifier, payload: json)
        let responseData = Data(responseJson.utf8)
        return try JSONDecoder().decode(MauiAppIntentResponse.self, from: responseData)
    }

    public func query(identifier: String, operation: String, payload: [String: Any]) throws -> [MauiAppIntentEntity] {
        guard let dispatcher else {
            throw MauiAppIntentRuntimeError.notReady
        }

        let data = try JSONSerialization.data(withJSONObject: payload, options: [])
        let json = String(data: data, encoding: .utf8) ?? ""{}""
        let responseJson = dispatcher.dispatchEntityQuery(identifier: identifier, operation: operation, payload: json)
        let responseData = Data(responseJson.utf8)
        return try JSONDecoder().decode(MauiAppIntentEntityQueryResponse.self, from: responseData).entities
    }
}

@_cdecl(""MauiAppIntentBridgeSetDispatcher"")
public func MauiAppIntentBridgeSetDispatcher(_ dispatcher: UnsafeMutableRawPointer?) -> Int32 {
    guard let dispatcher else {
        MauiAppIntentBridge.shared.dispatcher = nil
        return 1
    }

    let instance = Unmanaged<AnyObject>.fromOpaque(dispatcher).takeUnretainedValue()
    guard let typedDispatcher = instance as? MauiAppIntentDispatching else {
        return 0
    }

    MauiAppIntentBridge.shared.dispatcher = typedDispatcher
    return 1
}

public struct MauiAppIntentResponse: Decodable {
    public var success: Bool
    public var dialog: String?
    public var error: String?
    public var value: MauiAppIntentJSONValue?
}

public struct MauiAppIntentEntityQueryResponse: Decodable {
    public var entities: [MauiAppIntentEntity]
}

public struct MauiAppIntentEntity: Decodable {
    public var id: String
    public var display: String
    public var subtitle: String?
    public var properties: [String: MauiAppIntentJSONValue]?
}

public enum MauiAppIntentJSONValue: Decodable {
    case string(String)
    case number(Double)
    case bool(Bool)
    case object([String: MauiAppIntentJSONValue])
    case array([MauiAppIntentJSONValue])
    case null

    public init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if container.decodeNil() {
            self = .null
        } else if let value = try? container.decode(Bool.self) {
            self = .bool(value)
        } else if let value = try? container.decode(Double.self) {
            self = .number(value)
        } else if let value = try? container.decode(String.self) {
            self = .string(value)
        } else if let value = try? container.decode([MauiAppIntentJSONValue].self) {
            self = .array(value)
        } else {
            self = .object(try container.decode([String: MauiAppIntentJSONValue].self))
        }
    }

    public var stringValue: String? {
        if case .string(let value) = self { return value }
        return nil
    }

    public var intValue: Int? {
        if case .number(let value) = self { return Int(value) }
        return nil
    }

    public var doubleValue: Double? {
        if case .number(let value) = self { return value }
        return nil
    }

    public var boolValue: Bool? {
        if case .bool(let value) = self { return value }
        return nil
    }

    public var arrayValue: [MauiAppIntentJSONValue]? {
        if case .array(let value) = self { return value }
        return nil
    }

    public var objectValue: [String: MauiAppIntentJSONValue]? {
        if case .object(let value) = self { return value }
        return nil
    }

    public var dateValue: Date? {
        guard let stringValue else { return nil }
        return ISO8601DateFormatter().date(from: stringValue)
    }

    public var entityValue: MauiAppIntentEntity? {
        guard let object = objectValue,
              let id = object[""id""]?.stringValue,
              let display = object[""display""]?.stringValue else {
            return nil
        }

        return MauiAppIntentEntity(
            id: id,
            display: display,
            subtitle: object[""subtitle""]?.stringValue,
            properties: object[""properties""]?.objectValue
        )
    }
}

public enum MauiAppIntentRuntimeError: Error, CustomLocalizedStringResourceConvertible {
    case notReady
    case failed(String)

    public var localizedStringResource: LocalizedStringResource {
        switch self {
        case .notReady:
            return ""The app is not ready.""
        case .failed(let message):
            return LocalizedStringResource(stringLiteral: message)
        }
    }
}
");
    }

    private void WriteGeneratedSwift(AppIntentsManifest manifest)
    {
        var generatedDir = Path.Combine(OutputDirectory, "Sources", ModuleName, "Generated");
        Directory.CreateDirectory(generatedDir);
        var sb = new StringBuilder();
        sb.AppendLine("import AppIntents");
        sb.AppendLine("import Foundation");
        sb.AppendLine();

        var appEnums = ReferencedEnums(manifest);
        foreach (var appEnum in appEnums)
        {
            var swiftName = SwiftTypeName(appEnum.Name);
            sb.AppendLine("enum " + swiftName + ": Int, AppEnum {");
            foreach (var enumCase in appEnum.Cases)
            {
                sb.AppendLine("    case " + SwiftCaseIdentifier(enumCase.Name) + " = " + enumCase.RawValue.ToString(CultureInfo.InvariantCulture));
            }
            sb.AppendLine();
            sb.AppendLine("    static var typeDisplayRepresentation: TypeDisplayRepresentation = TypeDisplayRepresentation(name: \"" + EscapeSwift(appEnum.TypeDisplayName) + "\")");
            sb.AppendLine("    static var caseDisplayRepresentations: [" + swiftName + ": DisplayRepresentation] = [");
            for (var i = 0; i < appEnum.Cases.Count; i++)
            {
                var enumCase = appEnum.Cases[i];
                var suffix = i == appEnum.Cases.Count - 1 ? "" : ",";
                var display = string.IsNullOrWhiteSpace(enumCase.Subtitle)
                    ? "DisplayRepresentation(title: \"" + EscapeSwift(enumCase.Title) + "\")"
                    : "DisplayRepresentation(title: \"" + EscapeSwift(enumCase.Title) + "\", subtitle: \"" + EscapeSwift(enumCase.Subtitle) + "\")";
                sb.AppendLine("        ." + SwiftCaseIdentifier(enumCase.Name) + ": " + display + suffix);
            }
            sb.AppendLine("    ]");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        var appEntities = ReferencedEntities(manifest);
        foreach (var appEntity in appEntities)
        {
            var swiftName = SwiftEntityTypeName(appEntity);
            var queryName = swiftName + "Query";
            sb.AppendLine("struct " + swiftName + ": AppEntity {");
            sb.AppendLine("    static var defaultQuery = " + queryName + "()");
            sb.AppendLine("    static var typeDisplayRepresentation: TypeDisplayRepresentation = TypeDisplayRepresentation(name: \"" + EscapeSwift(appEntity.TypeDisplayName) + "\")");
            sb.AppendLine();
            sb.AppendLine("    var id: String");
            sb.AppendLine("    var displayString: String");
            sb.AppendLine("    var subtitleString: String?");
            foreach (var property in appEntity.Properties)
            {
                sb.AppendLine();
                sb.AppendLine("    @Property(title: \"" + EscapeSwift(property.Title) + "\")");
                sb.AppendLine("    var " + property.SwiftName + ": " + SwiftEntityPropertyType(property, manifest));
            }
            sb.AppendLine();
            sb.AppendLine("    var displayRepresentation: DisplayRepresentation {");
            sb.AppendLine("        if let subtitleString, !subtitleString.isEmpty {");
            sb.AppendLine("            return DisplayRepresentation(title: \"\\(displayString)\", subtitle: \"\\(subtitleString)\")");
            sb.AppendLine("        }");
            sb.AppendLine("        return DisplayRepresentation(title: \"\\(displayString)\")");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    init(id: String, displayString: String, subtitleString: String?) {");
            sb.AppendLine("        self.id = id");
            sb.AppendLine("        self.displayString = displayString");
            sb.AppendLine("        self.subtitleString = subtitleString");
            foreach (var property in appEntity.Properties)
            {
                sb.AppendLine("        self." + property.SwiftName + " = " + SwiftDefaultValue(property, manifest));
            }
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    init(from value: MauiAppIntentEntity) {");
            sb.AppendLine("        self.id = value.id");
            sb.AppendLine("        self.displayString = value.display");
            sb.AppendLine("        self.subtitleString = value.subtitle");
            foreach (var property in appEntity.Properties)
            {
                sb.AppendLine("        self." + property.SwiftName + " = " + JsonValueExpression("value.properties?[\"" + EscapeSwift(property.Name) + "\"]", property, manifest, fallback: SwiftDefaultValue(property, manifest)));
            }
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    func payload() -> [String: Any] {");
            sb.AppendLine("        [\"id\": id, \"display\": displayString, \"subtitle\": subtitleString ?? NSNull()]");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("struct " + queryName + ": EntityStringQuery {");
            sb.AppendLine("    func entities(for identifiers: [String]) async throws -> [" + swiftName + "] {");
            sb.AppendLine("        do {");
            sb.AppendLine("            return try MauiAppIntentBridge.shared.query(identifier: \"" + EscapeSwift(appEntity.Identifier) + "\", operation: \"entities\", payload: [\"identifiers\": identifiers]).map(" + swiftName + ".init)");
            sb.AppendLine("        } catch {");
            sb.AppendLine("            return []");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    func entities(matching string: String) async throws -> IntentItemCollection<" + swiftName + "> {");
            sb.AppendLine("        do {");
            sb.AppendLine("            let entities = try MauiAppIntentBridge.shared.query(identifier: \"" + EscapeSwift(appEntity.Identifier) + "\", operation: \"matching\", payload: [\"query\": string]).map(" + swiftName + ".init)");
            sb.AppendLine("            return IntentItemCollection(items: entities)");
            sb.AppendLine("        } catch {");
            sb.AppendLine("            return IntentItemCollection(items: [])");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    func suggestedEntities() async throws -> IntentItemCollection<" + swiftName + "> {");
            sb.AppendLine("        do {");
            sb.AppendLine("            let entities = try MauiAppIntentBridge.shared.query(identifier: \"" + EscapeSwift(appEntity.Identifier) + "\", operation: \"suggested\", payload: [:]).map(" + swiftName + ".init)");
            sb.AppendLine("            return IntentItemCollection(items: entities)");
            sb.AppendLine("        } catch {");
            sb.AppendLine("            return IntentItemCollection(items: [])");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        foreach (var intent in manifest.Intents)
        {
            sb.AppendLine("struct " + SwiftTypeName(intent.Identifier) + ": AppIntent {");
            sb.AppendLine("    static var title: LocalizedStringResource = \"" + EscapeSwift(intent.Title) + "\"");
            if (!string.IsNullOrWhiteSpace(intent.Description))
            {
                sb.AppendLine("    static var description = IntentDescription(\"" + EscapeSwift(intent.Description) + "\")");
            }
            sb.AppendLine("    static var openAppWhenRun: Bool = " + (intent.OpenAppWhenRun ? "true" : "false"));
            sb.AppendLine();

            foreach (var parameter in intent.Parameters)
            {
                var range = "";
                if (parameter.Minimum.HasValue && parameter.Maximum.HasValue && IsNumeric(parameter))
                {
                    range = ", inclusiveRange: (" + parameter.Minimum.Value.ToString(CultureInfo.InvariantCulture) + ", " + parameter.Maximum.Value.ToString(CultureInfo.InvariantCulture) + ")";
                }
                sb.AppendLine("    @Parameter(title: \"" + EscapeSwift(parameter.Title) + "\"" + range + ")");
                sb.AppendLine("    var " + parameter.SwiftName + ": " + SwiftType(parameter, manifest));
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(intent.ParameterSummary))
            {
                sb.AppendLine("    static var parameterSummary: some ParameterSummary {");
                sb.AppendLine("        Summary(\"" + EscapeSwift(intent.ParameterSummary) + "\")");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            var resultClause = string.IsNullOrWhiteSpace(intent.ResultKind)
                ? "some IntentResult & ProvidesDialog"
                : "some IntentResult & ReturnsValue<" + SwiftResultType(intent, manifest) + "> & ProvidesDialog";
            sb.AppendLine("    func perform() async throws -> " + resultClause + " {");
            sb.AppendLine("        let response = try MauiAppIntentBridge.shared.perform(identifier: \"" + EscapeSwift(intent.Identifier) + "\", payload: [");
            for (var i = 0; i < intent.Parameters.Count; i++)
            {
                var parameter = intent.Parameters[i];
                var suffix = i == intent.Parameters.Count - 1 ? "" : ",";
                sb.AppendLine("            \"" + EscapeSwift(parameter.SwiftName) + "\": " + PayloadExpression(parameter) + suffix);
            }
            sb.AppendLine("        ])");
            sb.AppendLine("        if response.success == false { throw MauiAppIntentRuntimeError.failed(response.error ?? \"Intent failed\") }");
            if (string.IsNullOrWhiteSpace(intent.ResultKind))
            {
                sb.AppendLine("        return .result(dialog: IntentDialog(stringLiteral: response.dialog ?? \"Done\"))");
            }
            else
            {
                sb.AppendLine("        guard let value = " + ResultValueExpression(intent, manifest) + " else { throw MauiAppIntentRuntimeError.failed(\"Intent did not return a value\") }");
                sb.AppendLine("        return .result(value: value, dialog: IntentDialog(stringLiteral: response.dialog ?? \"Done\"))");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        var shortcuts = manifest.Intents.Where(i => i.Shortcuts.Count > 0).ToList();
        if (shortcuts.Count > 0)
        {
            sb.AppendLine("struct GeneratedAppShortcuts: AppShortcutsProvider {");
            sb.AppendLine("    @AppShortcutsBuilder");
            sb.AppendLine("    static var appShortcuts: [AppShortcut] {");
            foreach (var intent in shortcuts)
            {
                var first = intent.Shortcuts[0];
                sb.AppendLine("        AppShortcut(");
                sb.AppendLine("            intent: " + SwiftTypeName(intent.Identifier) + "(),");
                sb.AppendLine("            phrases: [");
                for (var i = 0; i < intent.Shortcuts.Count; i++)
                {
                    var suffix = i == intent.Shortcuts.Count - 1 ? "" : ",";
                    sb.AppendLine("                \"" + EscapeSwift(intent.Shortcuts[i].Phrase).Replace("${applicationName}", "\\(.applicationName)") + "\"" + suffix);
                }
                sb.AppendLine("            ],");
                sb.AppendLine("            shortTitle: \"" + EscapeSwift(first.ShortTitle) + "\",");
                sb.AppendLine("            systemImageName: \"" + EscapeSwift(first.SystemImageName) + "\"");
                sb.AppendLine("        )");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }

        if (manifest.Intents.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(WriteDonationSwift(manifest));
        }

        File.WriteAllText(Path.Combine(generatedDir, "AppIntents.generated.swift"), sb.ToString());
    }

    private string WriteDonationSwift(AppIntentsManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@_cdecl(\"MauiAppIntentBridgeDonate\")");
        sb.AppendLine("public func MauiAppIntentBridgeDonate(_ identifierPointer: UnsafePointer<CChar>?, _ payloadPointer: UnsafePointer<CChar>?) -> Int32 {");
        sb.AppendLine("    guard let identifierPointer else { return 0 }");
        sb.AppendLine("    let identifier = String(cString: identifierPointer)");
        sb.AppendLine("    let payloadJson = payloadPointer.map { String(cString: $0) } ?? \"{}\"");
        sb.AppendLine("    let payloadData = Data(payloadJson.utf8)");
        sb.AppendLine("    let payload = ((try? JSONSerialization.jsonObject(with: payloadData)) as? [String: Any]) ?? [:]");
        sb.AppendLine();
        sb.AppendLine("    switch identifier {");
        foreach (var intent in manifest.Intents)
        {
            sb.AppendLine("    case \"" + EscapeSwift(intent.Identifier) + "\":");
            sb.AppendLine("        let intent = " + SwiftTypeName(intent.Identifier) + "()");
            foreach (var parameter in intent.Parameters)
            {
                sb.AppendLine(DonationAssignment(parameter, manifest));
            }
            sb.AppendLine("        donateGeneratedIntent(intent)");
            sb.AppendLine("        return 1");
        }
        sb.AppendLine("    default:");
        sb.AppendLine("        return 0");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("private func donateGeneratedIntent<T: AppIntent>(_ intent: T) {");
        sb.AppendLine("    Task {");
        sb.AppendLine("        do { _ = try await IntentDonationManager.shared.donate(intent: intent) }");
        sb.AppendLine("        catch { print(\"[AppIntents] Failed to donate generated intent \\(T.title): \\(error)\") }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("private func generatedString(_ value: Any?) -> String? { value as? String }");
        sb.AppendLine("private func generatedInt(_ value: Any?) -> Int? {");
        sb.AppendLine("    if let value = value as? Int { return value }");
        sb.AppendLine("    if let value = value as? Double { return Int(value) }");
        sb.AppendLine("    return nil");
        sb.AppendLine("}");
        sb.AppendLine("private func generatedDouble(_ value: Any?) -> Double? {");
        sb.AppendLine("    if let value = value as? Double { return value }");
        sb.AppendLine("    if let value = value as? Int { return Double(value) }");
        sb.AppendLine("    return nil");
        sb.AppendLine("}");
        sb.AppendLine("private func generatedBool(_ value: Any?) -> Bool? { value as? Bool }");
        sb.AppendLine("private func generatedDate(_ value: Any?) -> Date? {");
        sb.AppendLine("    guard let value = value as? String else { return nil }");
        sb.AppendLine("    return ISO8601DateFormatter().date(from: value)");
        sb.AppendLine("}");
        sb.AppendLine("private func generatedEntity(_ value: Any?) -> MauiAppIntentEntity? {");
        sb.AppendLine("    guard let dictionary = value as? [String: Any],");
        sb.AppendLine("          let id = dictionary[\"id\"] as? String,");
        sb.AppendLine("          let display = dictionary[\"display\"] as? String else { return nil }");
        sb.AppendLine("    return MauiAppIntentEntity(id: id, display: display, subtitle: dictionary[\"subtitle\"] as? String, properties: nil)");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private void WriteValidationManifest(AppIntentsManifest manifest)
    {
        var sb = new StringBuilder();
        foreach (var intent in manifest.Intents)
        {
            sb.AppendLine("intent\t" + SwiftTypeName(intent.Identifier));
            foreach (var shortcut in intent.Shortcuts)
            {
                if (!string.IsNullOrWhiteSpace(shortcut.Phrase))
                {
                    sb.AppendLine("shortcut\t" + shortcut.Phrase);
                }
            }
        }
        foreach (var appEnum in ReferencedEnums(manifest))
        {
            sb.AppendLine("enum\t" + appEnum.TypeDisplayName);
        }
        foreach (var appEntity in ReferencedEntities(manifest))
        {
            sb.AppendLine("entity\t" + appEntity.TypeDisplayName);
            foreach (var property in appEntity.Properties)
            {
                sb.AppendLine("property\t" + property.Name);
                if (!string.Equals(property.Title, property.Name, StringComparison.Ordinal))
                {
                    sb.AppendLine("property\t" + property.Title);
                }
            }
        }

        File.WriteAllText(ValidationManifestOutputFile, sb.ToString());
    }

    private void WriteBuildScript()
    {
        var scriptPath = Path.Combine(OutputDirectory, "build-appintents.sh");
        var script = @"#!/usr/bin/env bash
set -euo pipefail

SCRIPT_PATH=""${BASH_SOURCE[0]}""
SCRIPT_DIR=""${SCRIPT_PATH%/*}""
CALLER_DIR=`pwd`
PACKAGE_DIR=`cd ""$SCRIPT_DIR"" && pwd`
SCHEME=""" + EscapeBash(ModuleName) + @"""
CONFIGURATION=""${CONFIGURATION:-Debug}""
BUILD_DIR=""${BUILD_DIR:-$PACKAGE_DIR/../native}""
case ""$BUILD_DIR"" in
  /*) ;;
  *) BUILD_DIR=""$CALLER_DIR/$BUILD_DIR"" ;;
esac
ARCHIVE_PLATFORMS=""${ARCHIVE_PLATFORMS:-" + EscapeBash(ArchivePlatforms) + @"}""

rm -rf ""$BUILD_DIR""
mkdir -p ""$BUILD_DIR/archives"" ""$BUILD_DIR/xcframeworks""

archive_platform() {
  local name=""$1""
  local sdk=""$2""
  local destination=""$3""
  local archive_path=""$BUILD_DIR/archives/$SCHEME-$name.xcarchive""
  local derived_data=""$BUILD_DIR/DerivedData-$name""
  (
    cd ""$PACKAGE_DIR""
    xcodebuild -scheme ""$SCHEME"" \
      -configuration ""$CONFIGURATION"" \
      -sdk ""$sdk"" \
      -destination ""$destination"" \
      -derivedDataPath ""$derived_data"" \
      CODE_SIGNING_ALLOWED=NO \
      IPHONEOS_DEPLOYMENT_TARGET=""" + EscapeBash(MinimumOSVersion) + @""" \
      SKIP_INSTALL=NO \
      BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
      SWIFT_REFLECTION_METADATA_LEVEL=all \
      archive -archivePath ""$archive_path""
  )
}

case ""$ARCHIVE_PLATFORMS"" in
  Device) archive_platform ios iphoneos generic/platform=iOS ;;
  Simulator) archive_platform iossimulator iphonesimulator ""generic/platform=iOS Simulator"" ;;
  Both)
    archive_platform ios iphoneos generic/platform=iOS
    archive_platform iossimulator iphonesimulator ""generic/platform=iOS Simulator""
    ;;
  *) echo ""Unknown ARCHIVE_PLATFORMS '$ARCHIVE_PLATFORMS'. Use Device, Simulator, or Both."" >&2; exit 2 ;;
esac

args=()
if [[ -d ""$BUILD_DIR/archives/$SCHEME-ios.xcarchive"" ]]; then
  fw=`find ""$BUILD_DIR/archives/$SCHEME-ios.xcarchive/Products"" -name ""$SCHEME.framework"" -type d | head -1`
  args+=( -framework ""$fw"" )
  meta=`find ""$BUILD_DIR/archives/$SCHEME-ios.xcarchive/Products"" -path ""*/Metadata.appintents"" -type d | head -1`
elif [[ -d ""$BUILD_DIR/archives/$SCHEME-iossimulator.xcarchive"" ]]; then
  fw=`find ""$BUILD_DIR/archives/$SCHEME-iossimulator.xcarchive/Products"" -name ""$SCHEME.framework"" -type d | head -1`
  args+=( -framework ""$fw"" )
  meta=`find ""$BUILD_DIR/archives/$SCHEME-iossimulator.xcarchive/Products"" -path ""*/Metadata.appintents"" -type d | head -1`
fi

if [[ -d ""$BUILD_DIR/archives/$SCHEME-iossimulator.xcarchive"" && ""$ARCHIVE_PLATFORMS"" == ""Both"" ]]; then
  sim_fw=`find ""$BUILD_DIR/archives/$SCHEME-iossimulator.xcarchive/Products"" -name ""$SCHEME.framework"" -type d | head -1`
  args+=( -framework ""$sim_fw"" )
fi

xcodebuild -create-xcframework ""${args[@]}"" -output ""$BUILD_DIR/xcframeworks/$SCHEME.xcframework""

if [[ -z ""${meta:-}"" ]]; then
  echo ""Metadata.appintents was not produced."" >&2
  exit 3
fi

cp -R ""$meta"" ""$BUILD_DIR/Metadata.appintents""
date -u +%Y-%m-%dT%H:%M:%SZ > ""$BUILD_DIR/appintents-build.stamp""
";
        File.WriteAllText(scriptPath, script.Replace("\r\n", "\n"));

        var chmod = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/chmod",
            Arguments = "+x " + Quote(scriptPath),
            UseShellExecute = false
        });
        chmod?.WaitForExit();
        if (chmod is null || chmod.ExitCode != 0)
        {
            Log.LogWarning("Could not mark generated App Intents build script executable: {0}", scriptPath);
        }
    }

    private List<AppEnumModel> ReferencedEnums(AppIntentsManifest manifest)
    {
        var referencedEnumNames = new HashSet<string>(
            manifest.Intents
                .SelectMany(intent => intent.Parameters)
                .Where(parameter => parameter.Kind == "Enum" && !string.IsNullOrWhiteSpace(parameter.EnumTypeName))
                .Select(parameter => parameter.EnumTypeName),
            StringComparer.Ordinal);

        return manifest.AppEnums
            .Where(appEnum => referencedEnumNames.Contains(appEnum.FullName))
            .OrderBy(appEnum => appEnum.FullName, StringComparer.Ordinal)
            .ToList();
    }

    private List<AppEntityModel> ReferencedEntities(AppIntentsManifest manifest)
    {
        var referencedEntityNames = new HashSet<string>(
            manifest.Intents
                .SelectMany(intent => intent.Parameters)
                .Where(parameter => parameter.Kind == "Entity" && !string.IsNullOrWhiteSpace(parameter.EntityTypeName))
                .Select(parameter => parameter.EntityTypeName),
            StringComparer.Ordinal);

        return manifest.AppEntities
            .Where(appEntity => referencedEntityNames.Contains(appEntity.FullName))
            .OrderBy(appEntity => appEntity.FullName, StringComparer.Ordinal)
            .ToList();
    }

    private static string SwiftType(ParameterModel parameter, AppIntentsManifest manifest)
    {
        var swift = SwiftScalarType(
            parameter.Kind,
            parameter.EnumTypeName,
            parameter.EntityTypeName,
            manifest);
        if (parameter.IsCollection)
        {
            swift = "[" + swift + "]";
        }

        return parameter.IsOptional ? swift + "?" : swift;
    }

    private static string SwiftResultType(IntentModel intent, AppIntentsManifest manifest)
    {
        var swift = SwiftScalarType(
            intent.ResultKind,
            intent.ResultEnumTypeName,
            intent.ResultEntityTypeName,
            manifest);
        return intent.ResultIsCollection ? "[" + swift + "]" : swift;
    }

    private static string SwiftEntityPropertyType(AppEntityPropertyModel property, AppIntentsManifest manifest)
    {
        var swift = SwiftScalarType(property.Kind, property.EnumTypeName, "", manifest);
        if (property.IsCollection)
        {
            swift = "[" + swift + "]";
        }

        return property.IsOptional ? swift + "?" : swift;
    }

    private static string SwiftScalarType(string kind, string enumTypeName, string entityTypeName, AppIntentsManifest manifest)
    {
        return kind switch
        {
            "Int" => "Int",
            "Double" => "Double",
            "Bool" => "Bool",
            "Date" => "Date",
            "Enum" => SwiftTypeName(manifest.AppEnums.First(e => e.FullName == enumTypeName).Name),
            "Entity" => SwiftEntityTypeName(manifest.AppEntities.First(e => e.FullName == entityTypeName)),
            _ => "String"
        };
    }

    private static string PayloadExpression(ParameterModel parameter)
    {
        if (parameter.IsCollection)
        {
            var mapped = parameter.Kind switch
            {
                "Enum" => parameter.SwiftName + ".map { $0.rawValue }",
                "Entity" => parameter.SwiftName + ".map { $0.payload() }",
                "Date" => parameter.SwiftName + ".map { ISO8601DateFormatter().string(from: $0) }",
                _ => parameter.SwiftName
            };

            return parameter.IsOptional ? mapped + " ?? NSNull()" : mapped;
        }

        if (parameter.Kind == "Enum")
        {
            return parameter.IsOptional
                ? parameter.SwiftName + ".map { $0.rawValue } ?? NSNull()"
                : parameter.SwiftName + ".rawValue";
        }

        if (parameter.Kind == "Entity")
        {
            return parameter.IsOptional
                ? parameter.SwiftName + ".map { $0.payload() } ?? NSNull()"
                : parameter.SwiftName + ".payload()";
        }

        if (parameter.IsOptional)
        {
            if (parameter.Kind == "Date")
            {
                return parameter.SwiftName + ".map { ISO8601DateFormatter().string(from: $0) } ?? NSNull()";
            }

            return parameter.SwiftName + " ?? NSNull()";
        }

        return parameter.Kind == "Date"
            ? "ISO8601DateFormatter().string(from: " + parameter.SwiftName + ")"
            : parameter.SwiftName;
    }

    private static string ResultValueExpression(IntentModel intent, AppIntentsManifest manifest)
    {
        if (intent.ResultIsCollection)
        {
            var array = "response.value?.arrayValue";
            return intent.ResultKind switch
            {
                "Int" => array + "?.compactMap({ $0.intValue })",
                "Double" => array + "?.compactMap({ $0.doubleValue })",
                "Bool" => array + "?.compactMap({ $0.boolValue })",
                "Date" => array + "?.compactMap({ $0.dateValue })",
                "Enum" => array + "?.compactMap({ $0.intValue }).compactMap({ " + SwiftScalarType(intent.ResultKind, intent.ResultEnumTypeName, intent.ResultEntityTypeName, manifest) + "(rawValue: $0) })",
                "Entity" => array + "?.compactMap({ $0.entityValue }).map(" + SwiftScalarType(intent.ResultKind, intent.ResultEnumTypeName, intent.ResultEntityTypeName, manifest) + ".init)",
                _ => array + "?.compactMap({ $0.stringValue })"
            };
        }

        return intent.ResultKind switch
        {
            "Int" => "response.value?.intValue",
            "Double" => "response.value?.doubleValue",
            "Bool" => "response.value?.boolValue",
            "Date" => "response.value?.dateValue",
            "Enum" => "response.value?.intValue.flatMap { " + SwiftScalarType(intent.ResultKind, intent.ResultEnumTypeName, intent.ResultEntityTypeName, manifest) + "(rawValue: $0) }",
            "Entity" => "response.value?.entityValue.map(" + SwiftScalarType(intent.ResultKind, intent.ResultEnumTypeName, intent.ResultEntityTypeName, manifest) + ".init)",
            _ => "response.value?.stringValue"
        };
    }

    private static string JsonValueExpression(string source, AppEntityPropertyModel property, AppIntentsManifest manifest, string fallback)
    {
        if (property.IsCollection)
        {
            var array = source + "?.arrayValue";
            return property.Kind switch
            {
                "Int" => array + "?.compactMap({ $0.intValue }) ?? " + fallback,
                "Double" => array + "?.compactMap({ $0.doubleValue }) ?? " + fallback,
                "Bool" => array + "?.compactMap({ $0.boolValue }) ?? " + fallback,
                "Date" => array + "?.compactMap({ $0.dateValue }) ?? " + fallback,
                "Enum" => array + "?.compactMap({ $0.intValue }).compactMap({ " + SwiftScalarType(property.Kind, property.EnumTypeName, "", manifest) + "(rawValue: $0) }) ?? " + fallback,
                _ => array + "?.compactMap({ $0.stringValue }) ?? " + fallback
            };
        }

        var expression = property.Kind switch
        {
            "Int" => source + "?.intValue",
            "Double" => source + "?.doubleValue",
            "Bool" => source + "?.boolValue",
            "Date" => source + "?.dateValue",
            "Enum" => source + "?.intValue.flatMap { " + SwiftScalarType(property.Kind, property.EnumTypeName, "", manifest) + "(rawValue: $0) }",
            _ => source + "?.stringValue"
        };

        return expression + " ?? " + fallback;
    }

    private static string SwiftDefaultValue(AppEntityPropertyModel property, AppIntentsManifest manifest)
    {
        if (property.IsCollection)
        {
            return "[]";
        }

        if (property.IsOptional)
        {
            return "nil";
        }

        return property.Kind switch
        {
            "Int" => "0",
            "Double" => "0",
            "Bool" => "false",
            "Date" => "Date.distantPast",
            "Enum" => "." + SwiftCaseIdentifier(manifest.AppEnums.First(e => e.FullName == property.EnumTypeName).Cases[0].Name),
            _ => "\"\""
        };
    }

    private static string DonationAssignment(ParameterModel parameter, AppIntentsManifest manifest)
    {
        var key = "\"" + EscapeSwift(parameter.SwiftName) + "\"";
        if (parameter.IsCollection)
        {
            var expression = parameter.Kind switch
            {
                "Entity" => "(payload[" + key + "] as? [Any])?.compactMap({ generatedEntity($0) }).map(" + SwiftScalarType(parameter.Kind, parameter.EnumTypeName, parameter.EntityTypeName, manifest) + ".init)",
                "Enum" => "(payload[" + key + "] as? [Any])?.compactMap({ generatedInt($0) }).compactMap({ " + SwiftScalarType(parameter.Kind, parameter.EnumTypeName, parameter.EntityTypeName, manifest) + "(rawValue: $0) })",
                "Int" => "(payload[" + key + "] as? [Any])?.compactMap({ generatedInt($0) })",
                "Double" => "(payload[" + key + "] as? [Any])?.compactMap({ generatedDouble($0) })",
                "Bool" => "(payload[" + key + "] as? [Any])?.compactMap({ generatedBool($0) })",
                "Date" => "(payload[" + key + "] as? [Any])?.compactMap({ generatedDate($0) })",
                _ => "(payload[" + key + "] as? [Any])?.compactMap({ generatedString($0) })"
            };
            var fallback = parameter.IsOptional ? "nil" : "[]";
            return "        intent.$" + parameter.SwiftName + ".wrappedValue = " + expression + " ?? " + fallback;
        }

        var scalar = parameter.Kind switch
        {
            "Entity" => "generatedEntity(payload[" + key + "]).map(" + SwiftScalarType(parameter.Kind, parameter.EnumTypeName, parameter.EntityTypeName, manifest) + ".init)",
            "Enum" => "generatedInt(payload[" + key + "]).flatMap { " + SwiftScalarType(parameter.Kind, parameter.EnumTypeName, parameter.EntityTypeName, manifest) + "(rawValue: $0) }",
            "Int" => "generatedInt(payload[" + key + "])",
            "Double" => "generatedDouble(payload[" + key + "])",
            "Bool" => "generatedBool(payload[" + key + "])",
            "Date" => "generatedDate(payload[" + key + "])",
            _ => "generatedString(payload[" + key + "])"
        };
        return parameter.IsOptional
            ? "        intent.$" + parameter.SwiftName + ".wrappedValue = " + scalar
            : "        if let value = " + scalar + " { intent.$" + parameter.SwiftName + ".wrappedValue = value }";
    }

    private static bool IsNumeric(ParameterModel parameter)
    {
        return parameter.Kind == "Int" || parameter.Kind == "Double";
    }

    private static string SwiftTypeName(string value)
    {
        return SanitizeIdentifier(value);
    }

    private static string SwiftEntityTypeName(AppEntityModel entity)
    {
        return SwiftTypeName(entity.Identifier) + "Entity";
    }

    private static string SwiftCaseIdentifier(string value)
    {
        return EscapeSwiftIdentifier(LowerFirst(SanitizeIdentifier(value)));
    }

    private static string EscapeSwiftIdentifier(string value)
    {
        return SwiftKeywords.Contains(value) ? "`" + value + "`" : value;
    }

    private static string SanitizeIdentifier(string value)
    {
        var chars = (value ?? "").Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 || char.IsDigit(chars[0]) ? "GeneratedIntent" : new string(chars);
    }

    private static string LowerFirst(string value)
    {
        return string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string EscapeSwift(string value)
    {
        return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string EscapeBash(string value)
    {
        return EscapeSwift(value);
    }

    private static string Quote(string value)
    {
        return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static bool IsSwiftIdentifier(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            (char.IsLetter(value[0]) || value[0] == '_') &&
            value.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
    }

    private static readonly HashSet<string> SwiftKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "associatedtype", "class", "deinit", "enum", "extension", "fileprivate", "func", "import",
        "init", "inout", "internal", "let", "open", "operator", "private", "precedencegroup",
        "protocol", "public", "rethrows", "static", "struct", "subscript", "typealias", "var",
        "break", "case", "catch", "continue", "default", "defer", "do", "else", "fallthrough",
        "for", "guard", "if", "in", "repeat", "return", "throw", "switch", "where", "while",
        "as", "any", "false", "is", "nil", "self", "Self", "super", "throws", "true", "try"
    };

    private sealed class ManifestChunk
    {
        public ManifestChunk(int index, int total, string json)
        {
            Index = index;
            Total = total;
            Json = json;
        }

        public int Index { get; }

        public int Total { get; }

        public string Json { get; }
    }
}

public sealed class AppIntentsManifest
{
    public List<IntentModel> Intents { get; set; } = new List<IntentModel>();

    public List<AppEnumModel> AppEnums { get; set; } = new List<AppEnumModel>();

    public List<AppEntityModel> AppEntities { get; set; } = new List<AppEntityModel>();
}

public sealed class IntentModel
{
    public string Identifier { get; set; } = "";

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public string ParameterSummary { get; set; } = "";

    public bool OpenAppWhenRun { get; set; }

    public string HandlerType { get; set; } = "";

    public string RequestType { get; set; } = "";

    public string ResultKind { get; set; } = "";

    public string ResultTypeName { get; set; } = "";

    public string ResultClrTypeName { get; set; } = "";

    public string ResultEnumTypeName { get; set; } = "";

    public string ResultEntityTypeName { get; set; } = "";

    public bool ResultIsCollection { get; set; }

    public List<ParameterModel> Parameters { get; set; } = new List<ParameterModel>();

    public List<ShortcutModel> Shortcuts { get; set; } = new List<ShortcutModel>();
}

public sealed class ParameterModel
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

public sealed class ShortcutModel
{
    public string Phrase { get; set; } = "";

    public string ShortTitle { get; set; } = "";

    public string SystemImageName { get; set; } = "";
}

public sealed class AppEnumModel
{
    public string Name { get; set; } = "";

    public string FullName { get; set; } = "";

    public string TypeDisplayName { get; set; } = "";

    public List<AppEnumCaseModel> Cases { get; set; } = new List<AppEnumCaseModel>();
}

public sealed class AppEnumCaseModel
{
    public string Name { get; set; } = "";

    public string Title { get; set; } = "";

    public string Subtitle { get; set; } = "";

    public int RawValue { get; set; }
}

public sealed class AppEntityModel
{
    public string Identifier { get; set; } = "";

    public string Name { get; set; } = "";

    public string FullName { get; set; } = "";

    public string TypeDisplayName { get; set; } = "";

    public string IdProperty { get; set; } = "";

    public string DisplayProperty { get; set; } = "";

    public string SubtitleProperty { get; set; } = "";

    public string QueryHandlerType { get; set; } = "";

    public List<AppEntityPropertyModel> Properties { get; set; } = new List<AppEntityPropertyModel>();
}

public sealed class AppEntityPropertyModel
{
    public string Name { get; set; } = "";

    public string SourceProperty { get; set; } = "";

    public string SwiftName { get; set; } = "";

    public string Title { get; set; } = "";

    public string TypeName { get; set; } = "";

    public string Kind { get; set; } = "String";

    public bool IsOptional { get; set; }

    public bool IsCollection { get; set; }

    public string EnumTypeName { get; set; } = "";
}
