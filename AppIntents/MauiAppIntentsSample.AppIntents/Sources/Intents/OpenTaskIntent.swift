import AppIntents

/// Opens a specific task in the app.
/// Demonstrates: AppEntity parameter with entity query disambiguation.
struct OpenTaskIntent: AppIntent {
    static var title: LocalizedStringResource = "Open Task"
    static var description = IntentDescription("Opens a task in TaskTracker")
    static var openAppWhenRun: Bool = true

    @Parameter(title: "Task")
    var target: TaskEntity

    static var parameterSummary: some ParameterSummary {
        Summary("Open \(\.$target)")
    }

    func perform() async throws -> some IntentResult {
        // The app will open (openAppWhenRun = true) and can use the task ID
        // to navigate to the detail page via deep linking
        return .result()
    }
}
