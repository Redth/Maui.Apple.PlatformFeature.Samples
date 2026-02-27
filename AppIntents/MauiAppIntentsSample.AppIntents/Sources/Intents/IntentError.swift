import Foundation

/// Errors that App Intents can throw.
enum IntentError: Error, CustomLocalizedStringResourceConvertible {
    case appNotReady
    case taskNotFound
    case operationFailed

    var localizedStringResource: LocalizedStringResource {
        switch self {
        case .appNotReady:
            return "The app is not ready. Please open TaskTracker first."
        case .taskNotFound:
            return "The task could not be found."
        case .operationFailed:
            return "The operation failed. Please try again."
        }
    }
}
