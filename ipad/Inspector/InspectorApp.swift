import AppKit
import UniformTypeIdentifiers

@MainActor
final class InspectorDelegate: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    let status = NSTextField(wrappingLabelWithString: "This checks an installed Among Us build for the iPad port. It does not install, change or upload anything.")
    var report = InspectorCore.report(note: "No game selected. This is not an installer.")
    let inspectButton = NSButton(title: "Choose Among Us…", target: nil, action: nil)
    let saveButton = NSButton(title: "Save report…", target: nil, action: nil)
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)
        window = NSWindow(contentRect: NSRect(x:0,y:0,width:530,height:300), styleMask:[.titled,.closable,.miniaturizable], backing:.buffered, defer:false)
        window.title = "Among Us — iPad build check"
        window.center(); window.isReleasedWhenClosed = false
        let title = NSTextField(labelWithString: "Check the game build")
        title.font = .boldSystemFont(ofSize:22)
        let subtitle = NSTextField(wrappingLabelWithString: "Preparation tool only • No iPad mod is installed")
        subtitle.textColor = .secondaryLabelColor
        status.font = .systemFont(ofSize:14)
        status.preferredMaxLayoutWidth = 470
        inspectButton.target = self; inspectButton.action = #selector(choose)
        saveButton.target = self; saveButton.action = #selector(save)
        let buttons = NSStackView(views:[inspectButton,saveButton]); buttons.orientation = .horizontal; buttons.spacing = 12
        let stack = NSStackView(views:[title,subtitle,status,buttons])
        stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 18
        stack.translatesAutoresizingMaskIntoConstraints = false
        window.contentView!.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.leadingAnchor.constraint(equalTo:window.contentView!.leadingAnchor,constant:28),
            stack.trailingAnchor.constraint(equalTo:window.contentView!.trailingAnchor,constant:-28),
            stack.topAnchor.constraint(equalTo:window.contentView!.topAnchor,constant:28),
            stack.bottomAnchor.constraint(lessThanOrEqualTo:window.contentView!.bottomAnchor,constant:-24)
        ])
        let menu = NSMenu(); let app = NSMenuItem(); menu.addItem(app)
        app.submenu = NSMenu(); app.submenu!.addItem(withTitle:"Quit",action:#selector(NSApplication.terminate(_:)),keyEquivalent:"q"); NSApp.mainMenu = menu
        window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps:true)
    }
    @objc func choose() {
        let panel = NSOpenPanel(); panel.allowedContentTypes = [.applicationBundle]
        panel.canChooseDirectories = false; panel.canChooseFiles = true
        panel.allowsMultipleSelection = false; panel.directoryURL = URL(fileURLWithPath:"/Applications",isDirectory:true)
        panel.prompt = "Check game"
        guard panel.runModal() == .OK, let selected = panel.url else { return }
        status.stringValue = "Checking game files…"; inspectButton.isEnabled = false
        Task {
            do {
                let game = try await Task.detached { try InspectorCore.inspectGame(selected) }.value
                let detail = game.framework.encrypted ? "The framework is encrypted; it cannot be used by the current port tools." : "Game fingerprint recorded. Compatible mod bindings still need to be developed."
                report = InspectorCore.report(game:game,note:detail)
                status.stringValue = "Among Us \(game.version ?? "unknown version") (\(game.build ?? "unknown build"))\n\(detail)\nUse Save report to send the small report back."
            } catch {
                report = InspectorCore.report(note:error.localizedDescription)
                status.stringValue = error.localizedDescription+"\nYou can still save the Mac information report."
            }
            inspectButton.isEnabled = true
        }
    }
    @objc func save() {
        let panel = NSSavePanel(); panel.allowedContentTypes = [.json]; panel.nameFieldStringValue = "Among-Us-iPad-build-report.json"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            let encoder = JSONEncoder(); encoder.outputFormatting = [.prettyPrinted,.sortedKeys]
            try encoder.encode(report).write(to:url,options:.atomic)
            status.stringValue = "Report saved. Send that JSON file back. It contains build/version fingerprints, not game files, account details or passwords."
        } catch { status.stringValue = "Could not save the report: "+error.localizedDescription }
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender:NSApplication)->Bool { true }
}

@main
struct InspectorMain {
    @MainActor static func main() {
        let app = NSApplication.shared
        let delegate = InspectorDelegate(); app.delegate = delegate
        withExtendedLifetime(delegate) { app.run() }
    }
}
