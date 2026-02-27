import WidgetKit
import AppIntents
import os

struct IncrementCounterIntent: AppIntent {
    static var title: LocalizedStringResource { "Increment Counter" }
    static var description: IntentDescription { "Increments the counter by 1" }

    private static let logger = Logger(subsystem: "com.mauiapplewidgets", category: "IncrementIntent")

    func perform() async throws -> some IntentResult {
        let storage = SharedStorage()
        let currentCount = storage.getBestCounter()
        let newCount = currentCount + 1

        Self.logger.info("Incrementing counter: \(currentCount) -> \(newCount)")

        let data = WidgetData(
            version: 1,
            title: "",
            message: "incremented via widget",
            counter: newCount,
            updatedAt: ISO8601DateFormatter().string(from: Date()),
            extras: [:]
        )
        storage.writeWidgetData(data)

        WidgetCenter.shared.reloadTimelines(ofKind: Settings.widgetKind)

        return .result()
    }
}
