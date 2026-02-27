import AppIntents

/// Marks a task as completed.
/// Demonstrates: AppEntity parameter with dialog confirmation response.
struct CompleteTaskIntent: AppIntent, PredictableIntent {
    static var title: LocalizedStringResource = "Complete Task"
    static var description = IntentDescription("Marks a task as completed in TaskTracker")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Task")
    var target: TaskEntity

    static var parameterSummary: some ParameterSummary {
        Summary("Complete \(\.$target)")
    }

    static var predictionConfiguration: some IntentPredictionConfiguration {
        IntentPrediction(parameters: (\.$target)) { target in
            DisplayRepresentation(
                title: "Complete \(target)",
                subtitle: "Mark a task as done"
            )
        }
    }

    func perform() async throws -> some IntentResult & ProvidesDialog {
        guard let provider = TaskBridgeManager.shared.provider else {
            throw IntentError.appNotReady
        }

        let success = provider.completeTask(withId: target.id)
        if !success {
            throw IntentError.taskNotFound
        }

        return .result(dialog: "Done! '\(target.title)' has been marked as completed.")
    }
}
