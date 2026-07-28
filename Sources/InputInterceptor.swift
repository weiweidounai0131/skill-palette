import AppKit
import ApplicationServices

final class InputInterceptor {
    static let shared = InputInterceptor()

    private var eventTap: CFMachPort?
    private var source: CFRunLoopSource?

    private init() {}

    func start() {
        RuntimeStatus.shared.refreshPermissions()
        // The overlay needs both permissions as a single capability: input
        // monitoring observes the trigger, and Accessibility returns focus
        // and inserts the chosen Skill. Do not create a partial event tap
        // during onboarding; it can repeatedly contend with the permission
        // guide while macOS is restarting the app after a grant.
        guard RuntimeStatus.shared.inputMonitoringGranted,
              RuntimeStatus.shared.accessibilityGranted else {
            RuntimeStatus.shared.setEventTapRunning(false)
            DispatchQueue.main.async { PermissionGuide.show(requestAccessibility: false) }
            return
        }
        let mask = CGEventMask(1 << CGEventType.keyDown.rawValue)
        guard let tap = CGEvent.tapCreate(
            tap: .cgSessionEventTap,
            place: .headInsertEventTap,
            options: .defaultTap,
            eventsOfInterest: mask,
            callback: { _, type, event, _ in
                guard type == .keyDown else { return Unmanaged.passUnretained(event) }
                let character = event.unicodeCharacter
                guard !character.isEmpty else { return Unmanaged.passUnretained(event) }
                guard InputInterceptor.shared.shouldOpenPicker(for: character) else {
                    return Unmanaged.passUnretained(event)
                }
                let application = NSWorkspace.shared.frontmostApplication
                DispatchQueue.main.async {
                    // Accessibility queries can block while Electron updates
                    // its editor tree. Keep that work out of the event-tap
                    // callback so typing # never stalls the input pipeline.
                    let target = FocusedElement.current()
                    PickerController.shared.open(
                        targetElement: target,
                        targetApplication: application,
                        triggerCharacter: character
                    )
                }
                return nil
            },
            userInfo: nil
        ) else {
            RuntimeStatus.shared.setEventTapRunning(false)
            DispatchQueue.main.async { PermissionGuide.show(requestAccessibility: false) }
            return
        }

        eventTap = tap
        let runLoopSource = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        source = runLoopSource
        CFRunLoopAddSource(CFRunLoopGetMain(), runLoopSource, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
        RuntimeStatus.shared.setEventTapRunning(true)
    }

    func restart() {
        if let source {
            CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes)
        }
        eventTap = nil
        source = nil
        RuntimeStatus.shared.setEventTapRunning(false)
        start()
    }

    private func shouldOpenPicker(for character: String) -> Bool {
        let settings = OverlaySettings.shared
        let application = NSWorkspace.shared.frontmostApplication
        let identity = "\(application?.bundleIdentifier ?? "") \(application?.localizedName ?? "")".lowercased()
        let displayName = application?.localizedName ?? application?.bundleIdentifier ?? "未知应用"
        DispatchQueue.main.async {
            RuntimeStatus.shared.recordKeyboardEvent(frontmostApplication: displayName)
        }
        guard settings.normalizedTriggers.contains(character) else { return false }
        // Electron-based Codex editors do not always expose their composing
        // field through Accessibility, even when the user has granted the
        // permission. Do not make that optional integration a prerequisite for
        // showing the picker: text insertion already has a paste fallback.
        guard settings.codexOnly else {
            DispatchQueue.main.async { RuntimeStatus.shared.recordTrigger(outcome: "已打开搜索浮窗") }
            return true
        }
        let matched = settings.normalizedMatchers.contains { identity.contains($0) }
        DispatchQueue.main.async {
            RuntimeStatus.shared.recordTrigger(outcome: matched ? "已打开搜索浮窗" : "触发符已收到，但当前应用不匹配：\(displayName)")
        }
        return matched
    }
}

private extension CGEvent {
    var unicodeCharacter: String {
        var length = 0
        keyboardGetUnicodeString(maxStringLength: 0, actualStringLength: &length, unicodeString: nil)
        guard length > 0 else { return "" }
        var characters = Array(repeating: UniChar(0), count: length)
        keyboardGetUnicodeString(maxStringLength: length, actualStringLength: &length, unicodeString: &characters)
        return String(utf16CodeUnits: characters, count: length)
    }
}

enum FocusedElement {
    static func current() -> AXUIElement? {
        let system = AXUIElementCreateSystemWide()
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(system, kAXFocusedUIElementAttribute as CFString, &value) == .success,
              let value else { return nil }
        return unsafeBitCast(value, to: AXUIElement.self)
    }
}

enum TextInsertion {
    static func insert(_ text: String, into element: AXUIElement?) -> Bool {
        guard let element else { return false }
        return AXUIElementSetAttributeValue(element, kAXSelectedTextAttribute as CFString, text as CFTypeRef) == .success
    }

    static func paste(_ text: String, into application: NSRunningApplication?) {
        let pasteboard = NSPasteboard.general
        // `pasteboardItems` may contain provider-backed objects that cannot be
        // written back through `writeObjects(_:)`. Rewriting them caused an
        // Objective-C exception and terminated the whole app after choosing a
        // Skill. Preserve the common text clipboard safely instead.
        let previousText = pasteboard.string(forType: .string)
        pasteboard.clearContents()
        pasteboard.setString(text, forType: .string)
        let insertionChangeCount = pasteboard.changeCount
        application?.activate(options: [.activateIgnoringOtherApps])

        DispatchQueue.main.asyncAfter(deadline: .now() + 0.08) {
            let source = CGEventSource(stateID: .hidSystemState)
            let down = CGEvent(keyboardEventSource: source, virtualKey: 9, keyDown: true)
            let up = CGEvent(keyboardEventSource: source, virtualKey: 9, keyDown: false)
            down?.flags = .maskCommand
            up?.flags = .maskCommand
            down?.post(tap: .cghidEventTap)
            up?.post(tap: .cghidEventTap)
        }

        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
            // Never overwrite a clipboard change made by the user or another
            // app while the simulated paste was in progress.
            guard pasteboard.changeCount == insertionChangeCount, let previousText else { return }
            pasteboard.clearContents()
            pasteboard.setString(previousText, forType: .string)
        }
    }
}
