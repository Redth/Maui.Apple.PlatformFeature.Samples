import WidgetKit
import SwiftUI
import os.log

private let logger = Logger(subsystem: "com.companyname.mauiapplewidgets.SimpleWidgetExtension", category: "Provider")

struct Provider: AppIntentTimelineProvider {

    /// Minimal data while the widget is loading (almost never visible).
    func placeholder(in context: Context) -> SimpleEntry {
        createPreviewEntry()
    }

    /// Data for gallery preview and when first added to the home screen.
    func snapshot(for configuration: ConfigurationAppIntent, in context: Context) async -> SimpleEntry {
        createEntry(for: configuration, in: context)
    }

    /// Main data source — called when the widget is refreshed.
    func timeline(for configuration: ConfigurationAppIntent, in context: Context) async -> Timeline<SimpleEntry> {
        let entry = createEntry(for: configuration, in: context)
        return Timeline(entries: [entry], policy: .never)
    }

    // MARK: - Helpers

    private func createEntry(for configuration: ConfigurationAppIntent, in context: Context) -> SimpleEntry {
        if context.isPreview {
            return createPreviewEntry()
        }

        let storage = SharedStorage()
        let counter = storage.getBestCounter()
        let message = storage.getBestMessage()
        let title = configuration.displayTitle.isEmpty ? "My Widget" : configuration.displayTitle
        let emoji = configuration.favoriteEmoji

        logger.info("[SimpleWidget] createEntry counter=\(counter) message=\(message)")

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
            title: "My Widget",
            counter: 42,
            message: "Preview",
            emoji: "🚀",
            widgetUrl: ""
        )
    }
}
