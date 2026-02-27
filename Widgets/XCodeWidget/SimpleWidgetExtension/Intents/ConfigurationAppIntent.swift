import WidgetKit
import AppIntents

struct ConfigurationAppIntent: WidgetConfigurationIntent {
    static var title: LocalizedStringResource { "Widget Configuration" }
    static var description: IntentDescription { "Configure your widget display." }

    @Parameter(title: "Display Title", default: "My Widget")
    var displayTitle: String

    @Parameter(title: "Favorite Emoji", default: "🚀")
    var favoriteEmoji: String
}
