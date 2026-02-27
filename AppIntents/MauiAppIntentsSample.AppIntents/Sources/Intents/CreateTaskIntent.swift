import AppIntents

/// Creates a new task with various parameter types.
/// Demonstrates: String, AppEnum, optional Date, optional Int, optional String parameters.
struct CreateTaskIntent: AppIntent, PredictableIntent {
    static var title: LocalizedStringResource = "Create Task"
    static var description = IntentDescription("Creates a new task in TaskTracker")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Title")
    var taskTitle: String

    @Parameter(title: "Priority")
    var priority: TaskPriority

    @Parameter(title: "Category")
    var category: TaskCategory

    @Parameter(title: "Due Date")
    var dueDate: Date?

    @Parameter(title: "Estimated Minutes", inclusiveRange: (1, 480))
    var estimatedMinutes: Int?

    @Parameter(title: "Notes")
    var notes: String?

    static var parameterSummary: some ParameterSummary {
        Summary("Create \(\.$taskTitle) task") {
            \.$priority
            \.$category
            \.$dueDate
            \.$estimatedMinutes
            \.$notes
        }
    }

    static var predictionConfiguration: some IntentPredictionConfiguration {
        IntentPrediction(parameters: (\.$priority, \.$category)) { priority, category in
            DisplayRepresentation(
                title: "Create a \(priority) \(category) task",
                subtitle: "Add a new task to TaskTracker"
            )
        }
    }

    func perform() async throws -> some IntentResult & ReturnsValue<TaskEntity> & ProvidesDialog {
        guard let provider = TaskBridgeManager.shared.provider else {
            throw IntentError.appNotReady
        }

        // Request required values if not provided via Siri dialog
        let title = taskTitle
        let taskPriority = priority
        let taskCategory = category

        guard let created = provider.createTask(
            title: title,
            priorityRawValue: taskPriority.rawValue,
            categoryRawValue: taskCategory.rawValue,
            dueDate: dueDate,
            estimatedMinutes: estimatedMinutes ?? -1,
            notes: notes ?? ""
        ) else {
            throw IntentError.operationFailed
        }

        let entity = TaskEntity(from: created)
        return .result(
            value: entity,
            dialog: "Created '\(title)' with \(TaskPriority.caseDisplayRepresentations[taskPriority]?.title ?? "medium") priority in \(TaskCategory.caseDisplayRepresentations[taskCategory]?.title ?? "personal")"
        )
    }
}
