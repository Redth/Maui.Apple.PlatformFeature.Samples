import WidgetKit
import SwiftUI
import AppIntents

struct SimpleWidgetView: View {
    var entry: Provider.Entry

    var body: some View {
        VStack(spacing: 8) {
            // Title row with emoji — tapping here opens the app
            Link(destination: URL(string: entry.widgetUrl)!) {
                HStack {
                    Text(entry.emoji)
                    Text(entry.title)
                        .font(.headline)
                        .lineLimit(1)
                    Spacer()
                    Text(entry.emoji)
                }
            }

            // Counter display — tapping here opens the app
            Link(destination: URL(string: entry.widgetUrl)!) {
                Text("\(entry.counter)")
                    .font(.system(size: 48, weight: .bold, design: .rounded))
                    .frame(maxWidth: .infinity)
            }

            // Interactive buttons — these must NOT be covered by widgetURL
            HStack(spacing: 12) {
                Button(intent: DecrementCounterIntent()) {
                    Text("−")
                        .font(.system(size: 32, weight: .medium))
                        .frame(maxWidth: .infinity, minHeight: 44)
                }
                .buttonStyle(.borderedProminent)

                Button(intent: IncrementCounterIntent()) {
                    Text("+")
                        .font(.system(size: 32, weight: .medium))
                        .frame(maxWidth: .infinity, minHeight: 44)
                }
                .buttonStyle(.borderedProminent)
            }

            // Status message
            if !entry.message.isEmpty {
                Text(entry.message)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
        }
        .padding()
    }
}
