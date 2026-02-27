import Foundation

/// Stub for silent push notification to wake the app in the background.
/// Replace with your actual backend call and push notification provider.
class SilentNotificationService {
    func sendDataWithoutOpeningApp() async throws {
        // Simulate a network call
        try await Task.sleep(nanoseconds: 100_000_000)
        print("📡 Silent notification stub — implement your backend call here")
    }
}
