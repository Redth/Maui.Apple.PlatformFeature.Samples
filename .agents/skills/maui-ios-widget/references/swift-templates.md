# Swift Templates for iOS Widget Extension

All code below uses placeholder identifiers. Replace these:
- `{GroupId}` — The App Group ID (e.g., `group.com.example.myapp`)
- `{UrlScheme}` — The custom URL scheme (e.g., `myapp`)
- `{UrlHost}` — The URL host (typically `widget`)
- `{WidgetKind}` — The widget kind string (e.g., `MyWidget`)
- `{WidgetName}` — Display name (e.g., `My Widget`)
- `{ExtensionName}` — The Xcode extension target name (e.g., `SimpleWidgetExtension`)

## Table of Contents

1. [Settings & Data Layer](#settings--data-layer)
   - Settings.swift
   - WidgetData.swift
   - SharedStorage.swift
2. [Timeline Provider](#timeline-provider)
   - SimpleEntry.swift
   - Provider.swift
3. [Widget View](#widget-view)
   - SimpleWidgetView.swift
4. [Widget Configuration](#widget-configuration)
   - SimpleWidget.swift
   - SimpleWidgetBundle.swift
5. [Intents (Interactive Buttons)](#intents-interactive-buttons)
   - ConfigurationAppIntent.swift
   - IncrementCounterIntent.swift
   - DecrementCounterIntent.swift
6. [Services](#services)
   - SilentNotificationService.swift

---

## Settings & Data Layer

### Settings.swift

Mirror of `WidgetConstants.cs`. Every value must match exactly.

```swift
import WidgetKit
import SwiftUI

struct Settings {
    static let groupId = "{GroupId}"
    static let fromAppFile = "widget_data_fromapp.json"
    static let fromWidgetFile = "widget_data_fromwidget.json"
    static let widgetKind = "{WidgetKind}"
    static let urlScheme = "{UrlScheme}"
    static let urlHost = "{UrlHost}"
}
```

### WidgetData.swift

Must match the C# `WidgetData` record. Property names must be identical (camelCase).

```swift
import Foundation

struct WidgetData: Codable {
    var version: Int = 1
    var title: String = ""
    var message: String = ""
    var counter: Int = 0
    var updatedAt: String = ""
    var extras: [String: String] = [:]
}
```

**Customization:** Add the same properties you added to the C# side. Swift's `Codable` uses camelCase by default, which matches the `[JsonPropertyName]` attributes on the C# side.

### SharedStorage.swift

Central data access layer. All widget code reads/writes through this class. Uses **file-based I/O** via the App Group container directory for reliable cross-process communication.

```swift
import Foundation
import os

class SharedStorage {
    private let logger = Logger(subsystem: "com.example.myapp", category: "SharedStorage")

    private func containerURL() -> URL? {
        return FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: Settings.groupId)
    }

    private func fileURL(for filename: String) -> URL? {
        return containerURL()?.appendingPathComponent(filename)
    }

    func readAppData() -> WidgetData? {
        guard let url = fileURL(for: Settings.fromAppFile) else { return nil }
        guard FileManager.default.fileExists(atPath: url.path) else { return nil }
        do {
            let data = try Data(contentsOf: url)
            return try JSONDecoder().decode(WidgetData.self, from: data)
        } catch {
            logger.error("Failed to read app data: \(error.localizedDescription)")
            return nil
        }
    }

    func readWidgetData() -> WidgetData? {
        guard let url = fileURL(for: Settings.fromWidgetFile) else { return nil }
        guard FileManager.default.fileExists(atPath: url.path) else { return nil }
        do {
            let data = try Data(contentsOf: url)
            return try JSONDecoder().decode(WidgetData.self, from: data)
        } catch {
            return nil
        }
    }

    func writeWidgetData(_ data: WidgetData) {
        guard let url = fileURL(for: Settings.fromWidgetFile) else { return }
        do {
            let jsonData = try JSONEncoder().encode(data)
            try jsonData.write(to: url, options: .atomic)
        } catch {
            logger.error("Failed to write widget data: \(error.localizedDescription)")
        }
    }

    /// Returns the counter from whichever source was updated most recently.
    func getBestCounter() -> Int {
        let appData = readAppData()
        let widgetData = readWidgetData()

        switch (appData, widgetData) {
        case let (app?, widget?):
            // Both exist — use the one updated most recently
            return app.updatedAt >= widget.updatedAt ? app.counter : widget.counter
        case let (app?, nil):
            return app.counter
        case let (nil, widget?):
            return widget.counter
        default:
            return 0
        }
    }

    func getBestMessage() -> String {
        let appData = readAppData()
        let widgetData = readWidgetData()

        switch (appData, widgetData) {
        case let (app?, widget?):
            return app.updatedAt >= widget.updatedAt ? app.message : widget.message
        case let (app?, nil):
            return app.message
        case let (nil, widget?):
            return widget.message
        default:
            return ""
        }
    }
}
```

**Why files instead of UserDefaults?** `UserDefaults(suiteName:)` can resolve to different plist files for the app vs. the widget extension process, especially on simulator or with ad-hoc code signing. The app writes to the App Group container's `Library/Preferences/`, but the widget extension may write to the system-level `Library/Preferences/` instead. File-based I/O via `FileManager.containerURL(forSecurityApplicationGroupIdentifier:)` always resolves to the correct shared container.

**Why two data files?** The app writes to `fromAppFile` and the widget writes to `fromWidgetFile`. This avoids race conditions — each side only writes to its own file and reads from the other's.

**Why timestamp-based priority?** If you always prefer app data over widget data (or vice versa), widget button presses get overridden on the next timeline reload. Comparing `updatedAt` timestamps ensures whichever side wrote most recently wins. Note: C# `DateTime.ToString("o")` includes fractional seconds while Swift `ISO8601DateFormatter` doesn't, but string comparison still produces correct ordering.

---

## Timeline Provider

### SimpleEntry.swift

The data model for a single timeline snapshot. Add fields for whatever data your view needs.

```swift
import WidgetKit
import SwiftUI

struct SimpleEntry: TimelineEntry {
    let date: Date
    let title: String
    let counter: Int
    let message: String
    let emoji: String
    let widgetUrl: String
}
```

### Provider.swift

The heart of the widget — provides data for every refresh cycle. iOS calls these functions to get data:

- `placeholder()` — shown briefly while loading (rarely visible)
- `snapshot()` — shown in the widget gallery preview
- `timeline()` — the main data for normal display

```swift
import WidgetKit
import SwiftUI

struct Provider: AppIntentTimelineProvider {

    func placeholder(in context: Context) -> SimpleEntry {
        createPreviewEntry()
    }

    func snapshot(for configuration: ConfigurationAppIntent, in context: Context) async -> SimpleEntry {
        createEntry(for: configuration, in: context)
    }

    func timeline(for configuration: ConfigurationAppIntent, in context: Context) async -> Timeline<SimpleEntry> {
        let entry = createEntry(for: configuration, in: context)
        // .never = don't auto-refresh; we refresh manually via WidgetCenter.reloadTimelines
        return Timeline(entries: [entry], policy: .never)
    }

    private func createEntry(for configuration: ConfigurationAppIntent, in context: Context) -> SimpleEntry {
        if context.isPreview {
            return createPreviewEntry()
        }

        let storage = SharedStorage()
        let counter = storage.getBestCounter()
        let message = storage.getBestMessage()
        let title = configuration.displayTitle.isEmpty ? "{WidgetName}" : configuration.displayTitle
        let emoji = configuration.favoriteEmoji

        let deepLink = "\(Settings.urlScheme)://\(Settings.urlHost)?counter=\(counter)"

        return SimpleEntry(
            date: Date(),
            title: title,
            counter: counter,
            message: message,
            emoji: emoji,
            widgetUrl: deepLink
        )
    }

    private func createPreviewEntry() -> SimpleEntry {
        SimpleEntry(
            date: Date(),
            title: "{WidgetName}",
            counter: 42,
            message: "Preview",
            emoji: "🚀",
            widgetUrl: ""
        )
    }
}
```

**Timeline policy options:**
- `.never` — Only refreshes when you call `WidgetCenter.shared.reloadTimelines`. Best for data that changes on user action.
- `.atEnd` — Refreshes after the last entry's date. Useful for time-based data (e.g., weather forecasts with hourly entries).
- `.after(date)` — Refreshes at a specific future time.

**Deep link URL:** The `widgetUrl` encodes data into the URL that the app can parse when the user taps the widget. Customize the query parameters to pass whatever data is useful.

---

## Widget View

### SimpleWidgetView.swift

SwiftUI view for the widget. iOS widgets use SwiftUI exclusively — no UIKit.

```swift
import WidgetKit
import SwiftUI
import AppIntents

struct SimpleWidgetView: View {
    var entry: Provider.Entry

    var body: some View {
        VStack(spacing: 8) {
            // Title row — tapping opens the app via deep link
            Link(destination: URL(string: entry.widgetUrl)!) {
                HStack {
                    Text(entry.emoji)
                    Text(entry.title)
                        .font(.headline)
                        .lineLimit(1)
                    Spacer()
                    Text(entry.emoji)
                }
            }

            // Counter display — tapping opens the app
            Link(destination: URL(string: entry.widgetUrl)!) {
                Text("\(entry.counter)")
                    .font(.system(size: 48, weight: .bold, design: .rounded))
                    .frame(maxWidth: .infinity)
            }

            // Interactive buttons — must NOT be inside widgetURL or Link
            HStack(spacing: 12) {
                Button(intent: DecrementCounterIntent()) {
                    Text("−")
                        .font(.system(size: 32, weight: .medium))
                        .frame(maxWidth: .infinity, minHeight: 44)
                }
                .buttonStyle(.borderedProminent)

                Button(intent: IncrementCounterIntent()) {
                    Text("+")
                        .font(.system(size: 32, weight: .medium))
                        .frame(maxWidth: .infinity, minHeight: 44)
                }
                .buttonStyle(.borderedProminent)
            }

            if !entry.message.isEmpty {
                Text(entry.message)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
        }
        .padding()
    }
}
```

**Important notes:**
- `Button(intent:)` wires a button to an AppIntent — the button runs the intent's `perform()` method
- **Do NOT use `.widgetURL()` on a parent container that wraps interactive buttons** — it can intercept button taps. Use `Link(destination:)` on individual non-interactive areas instead.
- `.systemSmall` widgets don't support interactive buttons — the whole widget is a single tap target. For small widgets, use `.widgetURL()` on the entire view.
- Keep the view simple — widgets have strict memory limits

---

## Widget Configuration

### SimpleWidget.swift

The Widget struct declares the kind, provider, view, and supported sizes.

```swift
import WidgetKit
import SwiftUI

struct SimpleWidget: Widget {
    let kind: String = Settings.widgetKind

    var body: some WidgetConfiguration {
        AppIntentConfiguration(
            kind: kind,
            intent: ConfigurationAppIntent.self,
            provider: Provider()
        ) { entry in
            SimpleWidgetView(entry: entry)
                .containerBackground(.fill.tertiary, for: .widget)
        }
        .configurationDisplayName("{WidgetName}")
        .description("Displays data from the app.")
        .supportedFamilies([.systemMedium, .systemLarge])
    }
}

#Preview(as: .systemMedium) {
    SimpleWidget()
} timeline: {
    SimpleEntry(date: .now, title: "Preview", counter: 7, message: "preview", emoji: "🚀", widgetUrl: "")
}
```

### SimpleWidgetBundle.swift

The entry point that exposes one or more widgets. If you have multiple widgets, list them all here.

```swift
import WidgetKit
import SwiftUI

@main
struct SimpleWidgetBundle: WidgetBundle {
    var body: some Widget {
        SimpleWidget()
        // Add more widgets here:
        // AnotherWidget()
    }
}
```

---

## Intents (Interactive Buttons)

### ConfigurationAppIntent.swift

Allows users to configure the widget (long-press → Edit Widget). The `@Parameter` fields automatically generate a configuration UI.

```swift
import WidgetKit
import AppIntents

struct ConfigurationAppIntent: WidgetConfigurationIntent {
    static var title: LocalizedStringResource { "Widget Configuration" }
    static var description: IntentDescription { "Configure your widget display." }

    @Parameter(title: "Display Title", default: "{WidgetName}")
    var displayTitle: String

    @Parameter(title: "Favorite Emoji", default: "🚀")
    var favoriteEmoji: String
}
```

### IncrementCounterIntent.swift

An AppIntent that runs when the user taps the "+" button. Reads current state, modifies it, writes it back, and triggers a widget refresh.

```swift
import WidgetKit
import AppIntents

struct IncrementCounterIntent: AppIntent {
    static var title: LocalizedStringResource { "Increment Counter" }
    static var description: IntentDescription { "Increments the counter by 1" }

    func perform() async throws -> some IntentResult {
        let storage = SharedStorage()
        let currentCount = storage.getBestCounter()
        let newCount = currentCount + 1

        let data = WidgetData(
            version: 1,
            title: "",
            message: "incremented via widget",
            counter: newCount,
            updatedAt: ISO8601DateFormatter().string(from: Date()),
            extras: [:]
        )
        storage.writeWidgetData(data)

        // Trigger widget refresh — the provider will re-read from SharedStorage
        WidgetCenter.shared.reloadTimelines(ofKind: Settings.widgetKind)

        return .result()
    }
}
```

### DecrementCounterIntent.swift

Same pattern, different operation.

```swift
import WidgetKit
import AppIntents

struct DecrementCounterIntent: AppIntent {
    static var title: LocalizedStringResource { "Decrement Counter" }
    static var description: IntentDescription { "Decrements the counter by 1" }

    func perform() async throws -> some IntentResult {
        let storage = SharedStorage()
        let currentCount = storage.getBestCounter()
        let newCount = currentCount - 1

        let data = WidgetData(
            version: 1,
            title: "",
            message: "decremented via widget",
            counter: newCount,
            updatedAt: ISO8601DateFormatter().string(from: Date()),
            extras: [:]
        )
        storage.writeWidgetData(data)

        WidgetCenter.shared.reloadTimelines(ofKind: Settings.widgetKind)

        return .result()
    }
}
```

**Customization:** Replace the counter logic with whatever action your widget needs. AppIntents can also make network calls (iOS gives them a brief window to complete async work), which is useful for refreshing data from a backend.

---

## Services

### SilentNotificationService.swift

A stub for background communication. In production, this would call your backend API, which could then send a silent push notification to wake the app.

```swift
import Foundation

class SilentNotificationService {
    func sendDataWithoutOpeningApp() async throws {
        // Replace with your actual backend API call
        try await Task.sleep(nanoseconds: 100_000_000)
        print("📡 Silent notification stub — implement your backend call here")
    }
}
```

To use from an AppIntent's `perform()`:
```swift
do {
    try await SilentNotificationService().sendDataWithoutOpeningApp()
} catch {
    print("Error: \(error)")
}
```
