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
