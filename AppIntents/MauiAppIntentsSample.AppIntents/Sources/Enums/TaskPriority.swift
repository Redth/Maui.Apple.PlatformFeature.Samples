import AppIntents

/// Represents task priority levels as an AppEnum for Siri/Shortcuts integration.
enum TaskPriority: Int, AppEnum {
    case low = 0
    case medium = 1
    case high = 2
    case urgent = 3

    static var typeDisplayRepresentation: TypeDisplayRepresentation = "Priority"

    static var caseDisplayRepresentations: [TaskPriority: DisplayRepresentation] = [
        .low: DisplayRepresentation(title: "Low", subtitle: "Not time-sensitive"),
        .medium: DisplayRepresentation(title: "Medium", subtitle: "Normal priority"),
        .high: DisplayRepresentation(title: "High", subtitle: "Important"),
        .urgent: DisplayRepresentation(title: "Urgent", subtitle: "Needs immediate attention")
    ]
}
