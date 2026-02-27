import AppIntents

/// Searches tasks by a keyword.
/// Demonstrates: String query parameter, returning multiple entities.
struct SearchTasksIntent: AppIntent, PredictableIntent {
    static var title: LocalizedStringResource = "Search Tasks"
    static var description = IntentDescription("Searches for tasks by keyword in TaskTracker")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Search Query")
    var query: String

    static var parameterSummary: some ParameterSummary {
        Summary("Search for \(\.$query)")
    }

    static var predictionConfiguration: some IntentPredictionConfiguration {
        IntentPrediction(parameters: (\.$query)) { query in
            DisplayRepresentation(
                title: "Search for \(query)",
                subtitle: "Find tasks in TaskTracker"
            )
        }
    }

    func perform() async throws -> some IntentResult & ReturnsValue<[TaskEntity]> & ProvidesDialog {
        guard let provider = TaskBridgeManager.shared.provider else {
            throw IntentError.appNotReady
        }

        let items = provider.searchTasks(query: query)
        let entities = items.map { TaskEntity(from: $0) }

        if entities.isEmpty {
            return .result(value: entities, dialog: "No tasks found matching '\(query)'")
        }

        return .result(
            value: entities,
            dialog: "Found \(entities.count) task\(entities.count == 1 ? "" : "s") matching '\(query)'"
        )
    }
}
