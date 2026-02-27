import SwiftUI

struct ContentView: View {
    var body: some View {
        VStack {
            Image(systemName: "widget.small")
                .imageScale(.large)
                .foregroundStyle(.tint)
            Text("Widget Host App")
                .font(.title2)
            Text("This app exists only to host the Widget Extension.\nBuild the .NET MAUI app instead.")
                .multilineTextAlignment(.center)
                .foregroundStyle(.secondary)
                .padding()
        }
        .padding()
    }
}

#Preview {
    ContentView()
}
