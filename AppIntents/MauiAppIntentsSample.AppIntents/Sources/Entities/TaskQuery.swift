import AppIntents

/// Provides query capabilities for TaskEntity — used by Siri and Shortcuts to find tasks.
/// Supports lookup by ID, string search, and suggested results.
struct TaskEntityQuery: EntityStringQuery {

    /// Look up specific tasks by their IDs.
    func entities(for identifiers: [String]) async throws -> [TaskEntity] {
        guard let provider = TaskBridgeManager.shared.provider else { return [] }

        return identifiers.compactMap { id in
            guard let item = provider.getTask(withId: id) else { return nil }
            return TaskEntity(from: item)
        }
    }

    /// Search tasks by a text string (used when Siri asks "which task?").
    func entities(matching string: String) async throws -> IntentItemCollection<TaskEntity> {
        guard let provider = TaskBridgeManager.shared.provider else {
            return IntentItemCollection(items: [])
        }

        let items = provider.searchTasks(query: string)
        let entities = items.map { TaskEntity(from: $0) }
        return IntentItemCollection(items: entities)
    }

    /// Provide suggested entities for disambiguation (shown when Siri needs the user to pick).
    func suggestedEntities() async throws -> IntentItemCollection<TaskEntity> {
        guard let provider = TaskBridgeManager.shared.provider else {
            return IntentItemCollection(items: [])
        }

        let items = provider.getAllTasks()
        let entities = items.map { TaskEntity(from: $0) }
        return IntentItemCollection(items: entities)
    }
}
