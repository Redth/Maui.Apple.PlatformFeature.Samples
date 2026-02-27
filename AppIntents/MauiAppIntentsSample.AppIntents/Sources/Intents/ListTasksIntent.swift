import AppIntents

/// Lists tasks with optional filters.
/// Demonstrates: Optional AppEnum parameters, Bool parameter, returning multiple entities.
struct ListTasksIntent: AppIntent, PredictableIntent {
    static var title: LocalizedStringResource = "List Tasks"
    static var description = IntentDescription("Lists tasks in TaskTracker, optionally filtered by category or priority")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Category")
    var category: TaskCategory?

    @Parameter(title: "Priority")
    var priority: TaskPriority?

    @Parameter(title: "Show Completed", default: false)
    var showCompleted: Bool

    static var parameterSummary: some ParameterSummary {
        Summary("List tasks") {
            \.$category
            \.$priority
            \.$showCompleted
        }
    }

    static var predictionConfiguration: some IntentPredictionConfiguration {
        IntentPrediction(parameters: (\.$category, \.$priority)) { category, priority in
            DisplayRepresentation(
                title: "Show my tasks",
                subtitle: "List tasks in TaskTracker"
            )
        }
    }

    func perform() async throws -> some IntentResult & ReturnsValue<[TaskEntity]> & ProvidesDialog {
        guard let provider = TaskBridgeManager.shared.provider else {
            throw IntentError.appNotReady
        }

        let items = provider.getTasksByFilter(
            categoryRawValue: category?.rawValue ?? -1,
            priorityRawValue: priority?.rawValue ?? -1,
            showCompleted: showCompleted
        )

        let entities = items.map { TaskEntity(from: $0) }

        let filterDesc: String
        if let cat = category, let pri = priority {
            filterDesc = "\(TaskCategory.caseDisplayRepresentations[cat]?.title ?? "") \(TaskPriority.caseDisplayRepresentations[pri]?.title ?? "")"
        } else if let cat = category {
            filterDesc = "\(TaskCategory.caseDisplayRepresentations[cat]?.title ?? "")"
        } else if let pri = priority {
            filterDesc = "\(TaskPriority.caseDisplayRepresentations[pri]?.title ?? "")"
        } else {
            filterDesc = "all"
        }

        return .result(
            value: entities,
            dialog: "Found \(entities.count) \(filterDesc) task\(entities.count == 1 ? "" : "s")"
        )
    }
}
