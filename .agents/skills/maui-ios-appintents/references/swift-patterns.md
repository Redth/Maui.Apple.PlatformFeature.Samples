# Swift Patterns for MAUI App Intents

Complete code patterns for every Swift type needed in the App Intents framework. Replace `{AppName}` and domain-specific names with the user's actual app.

## Table of Contents
1. [Bridge DTO Class](#bridge-dto-class)
2. [Bridge Protocol & Manager](#bridge-protocol--manager)
3. [AppEnum](#appenum)
4. [AppEntity](#appentity)
5. [EntityStringQuery](#entitystringquery)
6. [AppIntent (Various Patterns)](#appintent)
7. [Intent Errors](#intent-errors)
8. [AppShortcutsProvider](#appshortcutsprovider)
9. [Package.swift](#packageswift)

---

## Bridge DTO Class

The @objc DTO carries data across the Swift↔C# boundary. One class per entity type.

```swift
import Foundation

@objc(Bridge{EntityName}) public class Bridge{EntityName}: NSObject {
    @objc public var id: String
    @objc public var name: String
    @objc public var description_: String     // Avoid Swift keyword conflicts with trailing underscore
    @objc public var typeRawValue: Int         // Enum as raw Int
    @objc public var statusRawValue: Int       // Another enum as raw Int
    @objc public var date: Date?              // Reference type — nullable OK
    @objc public var count: Int               // Use -1 sentinel for "nil"
    @objc public var isActive: Bool           // Can't be optional
    @objc public var createdAt: Date

    @objc public init(
        id: String, name: String, description_: String,
        typeRawValue: Int, statusRawValue: Int,
        date: Date?, count: Int,
        isActive: Bool, createdAt: Date
    ) {
        self.id = id
        self.name = name
        self.description_ = description_
        self.typeRawValue = typeRawValue
        self.statusRawValue = statusRawValue
        self.date = date
        self.count = count
        self.isActive = isActive
        self.createdAt = createdAt
    }
}
```

**Rules:**
- Always add `@objc(ClassName)` on the class to prevent Swift name mangling. Without this, the ObjC name becomes something like `_TtC23YourFrameworkName14BridgeTaskItem` and the .NET binding won't find it.
- Inherit from `NSObject` — required for @objc interop.
- No Swift optionals for value types (`Int?`, `Bool?`). Use sentinel values: `-1` for nil int, empty string for nil string.
- `Date?` is fine because NSDate is a reference type in ObjC.
- All properties and the init must be marked `@objc public`.

---

## Bridge Protocol & Manager

The protocol defines what C# must implement. The manager holds a reference to it.

```swift
import Foundation

@objc({DataProviderName}) public protocol {DataProviderName}: AnyObject {
    func getAll() -> [Bridge{EntityName}]
    func get(withId id: String) -> Bridge{EntityName}?
    func create(name: String, typeRawValue: Int, /* ... */) -> Bridge{EntityName}?
    func delete(withId id: String) -> Bool
    func search(query: String) -> [Bridge{EntityName}]
    func getFiltered(typeRawValue: Int, statusRawValue: Int) -> [Bridge{EntityName}]
}

@objc({BridgeManagerName}) public class {BridgeManagerName}: NSObject {
    @objc public static let shared = {BridgeManagerName}()
    @objc public weak var provider: {DataProviderName}?

    private override init() {
        super.init()
    }
}
```

**Rules:**
- Protocol MUST have `@objc(ProtocolName)` annotation.
- Manager MUST have `@objc(ManagerName)` annotation.
- Provider is `weak` to avoid retain cycles (C# side owns the object).
- Private init on manager — it's a singleton.
- Method names follow ObjC naming conventions: `getTask(withId:)` becomes `getTaskWithId:` in ObjC.

---

## AppEnum

Each C# enum that Siri should understand becomes a Swift `AppEnum`.

```swift
import AppIntents

enum {EnumName}: Int, AppEnum {
    case optionA = 0
    case optionB = 1
    case optionC = 2

    static var typeDisplayRepresentation: TypeDisplayRepresentation = "{Human-Readable Name}"

    static var caseDisplayRepresentations: [{EnumName}: DisplayRepresentation] = [
        .optionA: DisplayRepresentation(title: "Option A", subtitle: "Description of option A"),
        .optionB: DisplayRepresentation(title: "Option B", subtitle: "Description of option B"),
        .optionC: DisplayRepresentation(title: "Option C")  // subtitle is optional
    ]
}
```

**Rules:**
- Use `Int` raw values that match the C# enum values exactly.
- Every case MUST have an entry in `caseDisplayRepresentations`.
- `typeDisplayRepresentation` is what Siri says when asking "What {type}?"
- Subtitles appear in the Shortcuts UI picker — use them for disambiguation.

---

## AppEntity

The entity represents a C# model object in the App Intents system.

```swift
import AppIntents

struct {EntityName}Entity: AppEntity {
    static var defaultQuery = {EntityName}EntityQuery()
    static var typeDisplayRepresentation: TypeDisplayRepresentation = "{Entity Display Name}"

    var id: String

    @Property(title: "Name")
    var name: String

    @Property(title: "Type")
    var type: {EnumName}

    @Property(title: "Date")
    var date: Date?

    @Property(title: "Count")
    var count: Int?

    @Property(title: "Active")
    var isActive: Bool

    var displayRepresentation: DisplayRepresentation {
        let typeText = {EnumName}.caseDisplayRepresentations[type]?.title ?? "Unknown"
        return DisplayRepresentation(
            title: "\(name)",
            subtitle: "\(typeText)"
        )
    }

    /// Create entity from bridge DTO
    init(from bridge: Bridge{EntityName}) {
        self.id = bridge.id
        self.name = bridge.name
        self.type = {EnumName}(rawValue: bridge.typeRawValue) ?? .optionA
        self.date = bridge.date
        self.count = bridge.count >= 0 ? bridge.count : nil
        self.isActive = bridge.isActive
    }
}
```

**Rules:**
- `defaultQuery` connects the entity to its query type.
- Use `@Property(title:)` for properties you want visible in Shortcuts.
- `displayRepresentation` is what Siri shows/reads when presenting the entity.
- The `init(from:)` converts sentinel values back to optionals (e.g., `-1` → `nil`).

---

## EntityStringQuery

Provides lookup capabilities for Siri to find entities.

```swift
import AppIntents

struct {EntityName}EntityQuery: EntityStringQuery {

    /// Look up entities by their IDs
    func entities(for identifiers: [String]) async throws -> [{EntityName}Entity] {
        guard let provider = {BridgeManagerName}.shared.provider else { return [] }
        return identifiers.compactMap { id in
            guard let item = provider.get(withId: id) else { return nil }
            return {EntityName}Entity(from: item)
        }
    }

    /// Search entities by text (Siri asks "which one?" → user types/speaks search text)
    func entities(matching string: String) async throws -> IntentItemCollection<{EntityName}Entity> {
        guard let provider = {BridgeManagerName}.shared.provider else {
            return IntentItemCollection(items: [])
        }
        let items = provider.search(query: string)
        return IntentItemCollection(items: items.map { {EntityName}Entity(from: $0) })
    }

    /// Suggested entities shown when Siri needs disambiguation
    func suggestedEntities() async throws -> IntentItemCollection<{EntityName}Entity> {
        guard let provider = {BridgeManagerName}.shared.provider else {
            return IntentItemCollection(items: [])
        }
        let items = provider.getAll()
        return IntentItemCollection(items: items.map { {EntityName}Entity(from: $0) })
    }
}
```

**Rules:**
- Always guard on `provider` being non-nil — the app may not be fully initialized.
- Return empty collections (not throwing) when provider is nil — Siri handles empty gracefully.
- `suggestedEntities()` is called when Siri shows a picker — return recent/relevant items.
- `entities(matching:)` is called when the user types in the search field.

---

## AppIntent

### Pattern 1: Create intent (returns entity, provides dialog)

```swift
import AppIntents

struct Create{EntityName}Intent: AppIntent {
    static var title: LocalizedStringResource = "Create {Entity Name}"
    static var description = IntentDescription("Creates a new {entity} in {App Name}")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Name")
    var name: String

    @Parameter(title: "Type")
    var type: {EnumName}

    @Parameter(title: "Date")
    var date: Date?

    @Parameter(title: "Count", inclusiveRange: (1, 100))
    var count: Int?

    @Parameter(title: "Notes")
    var notes: String?

    static var parameterSummary: some ParameterSummary {
        Summary("Create \(\.$name) {entity}") {
            \.$type
            \.$date
            \.$count
            \.$notes
        }
    }

    func perform() async throws -> some IntentResult & ReturnsValue<{EntityName}Entity> & ProvidesDialog {
        guard let provider = {BridgeManagerName}.shared.provider else {
            throw IntentError.appNotReady
        }
        guard let created = provider.create(
            name: name, typeRawValue: type.rawValue,
            /* ... map other params ... */
        ) else {
            throw IntentError.operationFailed
        }
        let entity = {EntityName}Entity(from: created)
        return .result(value: entity, dialog: "Created '\(name)'")
    }
}
```

### Pattern 2: Action on existing entity (takes entity parameter)

```swift
struct Complete{EntityName}Intent: AppIntent {
    static var title: LocalizedStringResource = "Complete {Entity Name}"
    static var description = IntentDescription("Marks a {entity} as complete")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "{Entity Name}")
    var target: {EntityName}Entity

    static var parameterSummary: some ParameterSummary {
        Summary("Complete \(\.$target)")
    }

    func perform() async throws -> some IntentResult & ProvidesDialog {
        guard let provider = {BridgeManagerName}.shared.provider else {
            throw IntentError.appNotReady
        }
        guard provider.complete(withId: target.id) else {
            throw IntentError.operationFailed
        }
        return .result(dialog: "Completed '\(target.name)'")
    }
}
```

### Pattern 3: Open in app (uses openAppWhenRun)

```swift
struct Open{EntityName}Intent: AppIntent {
    static var title: LocalizedStringResource = "Open {Entity Name}"
    static var description = IntentDescription("Opens a {entity} in {App Name}")
    static var openAppWhenRun: Bool = true  // Key difference

    @Parameter(title: "{Entity Name}")
    var target: {EntityName}Entity

    func perform() async throws -> some IntentResult {
        // Post notification or use deep link to navigate in MAUI
        return .result()
    }
}
```

### Pattern 4: List/filter (returns array of entities)

```swift
struct List{EntityName}sIntent: AppIntent {
    static var title: LocalizedStringResource = "List {Entity Name}s"
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Type")
    var type: {EnumName}?

    @Parameter(title: "Show Completed")
    var showCompleted: Bool?

    static var parameterSummary: some ParameterSummary {
        Summary("List {entities}") {
            \.$type
            \.$showCompleted
        }
    }

    func perform() async throws -> some IntentResult & ReturnsValue<[{EntityName}Entity]> & ProvidesDialog {
        guard let provider = {BridgeManagerName}.shared.provider else {
            throw IntentError.appNotReady
        }
        let items = provider.getFiltered(
            typeRawValue: type?.rawValue ?? -1,
            showCompleted: showCompleted ?? false
        )
        let entities = items.map { {EntityName}Entity(from: $0) }
        return .result(value: entities, dialog: "Found \(entities.count) {entities}")
    }
}
```

---

## Intent Errors

```swift
import Foundation

enum IntentError: Error, CustomLocalizedStringResourceConvertible {
    case appNotReady
    case notFound
    case operationFailed

    var localizedStringResource: LocalizedStringResource {
        switch self {
        case .appNotReady:
            return "The app is not ready. Please open {App Name} first."
        case .notFound:
            return "The {entity} could not be found."
        case .operationFailed:
            return "The operation failed. Please try again."
        }
    }
}
```

---

## AppShortcutsProvider

Defines Siri phrases that automatically work without user setup.

```swift
import AppIntents

struct {AppName}Shortcuts: AppShortcutsProvider {
    @AppShortcutsBuilder
    static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: Create{EntityName}Intent(),
            phrases: [
                "Create a {entity} in \(.applicationName)",
                "Add a new {entity} in \(.applicationName)",
                "New {entity} in \(.applicationName)"
            ],
            shortTitle: "Create {Entity Name}",
            systemImageName: "plus.circle"
        )

        AppShortcut(
            intent: Open{EntityName}Intent(),
            phrases: [
                "Open a {entity} in \(.applicationName)",
                "Show {entity} in \(.applicationName)"
            ],
            shortTitle: "Open {Entity Name}",
            systemImageName: "eye"
        )

        // Add one AppShortcut per intent...
    }
}
```

**Rules:**
- ALWAYS include `\(.applicationName)` in every phrase — Apple requires it.
- Max 10 AppShortcuts per AppShortcutsProvider.
- Max 10 phrases per AppShortcut (fewer is better — 3-4 is ideal).
- `shortTitle` appears in the Shortcuts app gallery.
- Use SF Symbols names for `systemImageName`.

---

## PredictableIntent

Make intents conform to `PredictableIntent` so the system can proactively suggest them. This is purely Swift-side — no bridge changes needed.

```swift
struct CreateTaskIntent: AppIntent, PredictableIntent {
    static var title: LocalizedStringResource = "Create Task"
    
    @Parameter(title: "Title") var taskTitle: String
    @Parameter(title: "Priority") var priority: TaskPriority
    @Parameter(title: "Category") var category: TaskCategory
    
    // ... parameterSummary, perform() ...
    
    static var predictionConfiguration: some IntentPredictionConfiguration {
        IntentPrediction(parameters: (\.$priority, \.$category)) { priority, category in
            DisplayRepresentation(
                title: "Create a \(priority) \(category) task",
                subtitle: "Add a new task to the app"
            )
        }
    }
}
```

**When to use PredictableIntent:**
- Intents with predictable parameter patterns (e.g., "create a Work task", "show high-priority tasks")
- Intents the user performs frequently
- NOT intents that are too context-dependent (e.g., `OpenTaskIntent` where the specific task varies unpredictably)

---

## IntentDonationBridge

An `@objc` class that allows C# to donate intents to the system via `IntentDonationManager`. This is the reverse direction of the data provider bridge — C# initiates, Swift executes.

```swift
@objc({BridgeDonationClassName})
public class {BridgeDonationClassName}: NSObject {
    @objc public static let shared = {BridgeDonationClassName}()
    private override init() { super.init() }
    
    @objc public func donateCreateTask(title: String, priorityRawValue: Int, categoryRawValue: Int,
                                        dueDate: Date?, estimatedMinutes: Int, notes: String) {
        let intent = CreateTaskIntent()
        intent.$taskTitle.wrappedValue = title
        intent.$priority.wrappedValue = TaskPriority(rawValue: priorityRawValue) ?? .medium
        intent.$category.wrappedValue = TaskCategory(rawValue: categoryRawValue) ?? .personal
        intent.$dueDate.wrappedValue = dueDate
        intent.$estimatedMinutes.wrappedValue = estimatedMinutes >= 0 ? estimatedMinutes : nil
        intent.$notes.wrappedValue = notes.isEmpty ? nil : notes
        donate(intent)
    }
    
    @objc public func donateCompleteTask(taskId: String, taskTitle: String) {
        let intent = CompleteTaskIntent()
        let bridgeItem = BridgeTaskItem(id: taskId, title: taskTitle, /* ... */)
        intent.$target.wrappedValue = TaskEntity(from: bridgeItem)
        donate(intent)
    }
    
    // ... other donate methods for each intent type ...
    
    @objc public func deleteTaskDonations(taskId: String) {
        Task {
            do {
                _ = try await IntentDonationManager.shared.deleteDonations(
                    matching: .intentType(CompleteTaskIntent.self)
                )
                // ... delete for other intent types referencing the task
            } catch {
                print("[AppIntents] Failed to delete donations: \(error)")
            }
        }
    }
    
    private func donate<T: AppIntent>(_ intent: T) {
        Task {
            do {
                _ = try await IntentDonationManager.shared.donate(intent: intent)
                print("[AppIntents] Donated \(T.title) intent")
            } catch {
                print("[AppIntents] Failed to donate \(T.title): \(error)")
            }
        }
    }
}
```

**Key rules:**
- Use `intent.$paramName.wrappedValue = value` (not `intent.paramName = value`) to avoid Swift "never mutated" warnings with `let` constants
- Donation is fire-and-forget — wrap in `Task { }` and catch errors, never crash
- Only donate for user-initiated UI actions — Siri/Shortcuts donations are automatic
- Each donate method mirrors the corresponding intent's parameters
