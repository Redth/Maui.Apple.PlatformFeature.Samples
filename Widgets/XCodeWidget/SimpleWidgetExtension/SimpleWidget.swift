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
        .configurationDisplayName("Simple Widget")
        .description("A counter widget that syncs with the MAUI app.")
        .supportedFamilies([.systemMedium, .systemLarge])
    }
}

#Preview(as: .systemMedium) {
    SimpleWidget()
} timeline: {
    SimpleEntry(date: .now, title: "Preview", counter: 7, message: "preview data", emoji: "🚀", widgetUrl: "")
}
