import AppIntents

/// Represents task categories as an AppEnum for Siri/Shortcuts integration.
enum TaskCategory: Int, AppEnum {
    case work = 0
    case personal = 1
    case shopping = 2
    case health = 3
    case fitness = 4

    static var typeDisplayRepresentation: TypeDisplayRepresentation = "Category"

    static var caseDisplayRepresentations: [TaskCategory: DisplayRepresentation] = [
        .work: DisplayRepresentation(title: "Work", subtitle: "Professional tasks"),
        .personal: DisplayRepresentation(title: "Personal", subtitle: "Personal errands"),
        .shopping: DisplayRepresentation(title: "Shopping", subtitle: "Shopping lists"),
        .health: DisplayRepresentation(title: "Health", subtitle: "Health-related tasks"),
        .fitness: DisplayRepresentation(title: "Fitness", subtitle: "Exercise and workouts")
    ]
}
