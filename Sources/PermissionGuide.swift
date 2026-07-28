import AppKit
import SwiftUI

final class PermissionGuideController: NSObject, NSWindowDelegate {
    static let shared = PermissionGuideController()

    // This is deliberately an ordinary NSWindow instead of NSPanel. NSPanel
    // has utility/floating behaviours that are useful for the Skill picker,
    // but make a permission guide unreliable while the user moves between
    // this app and System Settings.
    private let panel: NSWindow
    private var activationObserver: NSObjectProtocol?
    /// True only while the user is changing a privacy setting in System
    /// Settings. Keeping this state separate prevents the guide from
    /// immediately stealing focus back from macOS.
    private var awaitingSystemSettings = false

    private override init() {
        panel = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 460, height: 428),
            styleMask: [.titled, .closable, .fullSizeContentView],
            backing: .buffered,
            defer: false
        )
        super.init()

        panel.title = "权限设置"
        panel.titleVisibility = .hidden
        panel.titlebarAppearsTransparent = true
        panel.isMovableByWindowBackground = true
        panel.isReleasedWhenClosed = false
        // Do not let AppKit hide this window on a brief focus change while it
        // is being dragged. System Settings is handled explicitly below.
        panel.hidesOnDeactivate = false
        // This guide must never sit above System Settings.
        panel.level = .normal
        panel.delegate = self
        panel.contentView = NSHostingView(
            rootView: PermissionGuideView(
                dismiss: { [weak panel] in panel?.close() },
                complete: { [weak panel] in
                    PermissionGuide.completePendingGuide()
                    panel?.close()
                }
            )
        )
        activationObserver = NotificationCenter.default.addObserver(
            forName: NSApplication.didBecomeActiveNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            self?.restoreAfterReturningFromSystemSettings()
        }
    }

    deinit {
        if let activationObserver {
            NotificationCenter.default.removeObserver(activationObserver)
        }
    }

    func show() {
        guard !awaitingSystemSettings else { return }
        refreshPermissionState()
        NSRunningApplication.current.activate(options: [.activateIgnoringOtherApps])
        if !panel.isVisible {
            panel.center()
        }
        panel.makeKeyAndOrderFront(nil)
    }

    /// Hide the guide before opening a privacy page. `orderBack` only changes
    /// stacking among this app's windows, so it is not enough when System
    /// Settings is in another application.
    func suspendForSystemSettings() {
        awaitingSystemSettings = true
        panel.orderOut(nil)
    }

    private func restoreAfterReturningFromSystemSettings() {
        refreshPermissionState()
        guard awaitingSystemSettings else { return }
        awaitingSystemSettings = false
        panel.center()
        panel.makeKeyAndOrderFront(nil)
    }

    private func refreshPermissionState() {
        RuntimeStatus.shared.refreshPermissions()
        if RuntimeStatus.shared.inputMonitoringGranted,
           RuntimeStatus.shared.accessibilityGranted,
           !RuntimeStatus.shared.eventTapRunning {
            InputInterceptor.shared.restart()
        }
        if NSApp.isActive, panel.isVisible, !awaitingSystemSettings {
            panel.makeKeyAndOrderFront(nil)
        }
    }

    func windowWillClose(_ notification: Notification) {
        awaitingSystemSettings = false
    }
}

private struct PermissionGuideView: View {
    @ObservedObject private var status = RuntimeStatus.shared
    let dismiss: () -> Void
    let complete: () -> Void

    private var allPermissionsGranted: Bool {
        status.accessibilityGranted && status.inputMonitoringGranted
    }

    var body: some View {
        VStack(spacing: 0) {
            Image(nsImage: AppIcon.current ?? NSImage())
                .resizable()
                .interpolation(.high)
                .frame(width: 92, height: 92)
                .accessibilityHidden(true)
                .padding(.top, 22)

            Text("开启两项权限")
                .font(.system(size: 22, weight: .semibold))
                .foregroundStyle(.primary)
                .padding(.top, 12)

            Text("让 Skill Palette 在 Codex 中唤起搜索，并插入选中的 Skill。")
                .font(.system(size: 14))
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
                .lineSpacing(3)
                .frame(maxWidth: 350)
                .padding(.top, 6)

            VStack(spacing: 0) {
                PermissionRequirementRow(
                    symbol: "hand.raised.fill",
                    title: "辅助功能",
                    description: "将选中的 Skill 插回 Codex 输入框",
                    isGranted: status.accessibilityGranted,
                    openSettings: PermissionGuide.requestAccessibility
                )

                Divider().padding(.leading, 54)

                PermissionRequirementRow(
                    symbol: "keyboard",
                    title: "输入监控",
                    description: "监听 # 或 @，即时打开 Skill 搜索",
                    isGranted: status.inputMonitoringGranted,
                    openSettings: RuntimeStatus.shared.requestInputMonitoring
                )
            }
            .background(Color(nsColor: .controlBackgroundColor))
            .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 14, style: .continuous)
                    .strokeBorder(Color(nsColor: .separatorColor).opacity(0.45), lineWidth: 1)
            )
            .padding(.horizontal, 24)
            .padding(.top, 16)

            Text("点击每一项右侧的“打开设置”，开启后返回此窗口即可继续。")
                .font(.system(size: 12))
                .foregroundStyle(.secondary)
                .padding(.top, 10)

            Group {
                if allPermissionsGranted {
                    Button("继续") { complete() }
                        .buttonStyle(.borderedProminent)
                        .keyboardShortcut(.defaultAction)
                } else {
                Button("稍后") { dismiss() }
                    .buttonStyle(.bordered)
                }
            }
            .controlSize(.large)
            .padding(.top, 14)
            .padding(.bottom, 20)
        }
        .frame(width: 460, height: 428, alignment: .top)
        .background(Color(nsColor: .windowBackgroundColor))
    }
}

private struct PermissionRequirementRow: View {
    let symbol: String
    let title: String
    let description: String
    let isGranted: Bool
    let openSettings: () -> Void

    var body: some View {
        HStack(spacing: 11) {
            Image(systemName: symbol)
                .font(.system(size: 16, weight: .medium))
                .foregroundStyle(Color.accentColor)
                .frame(width: 26, height: 26)
                .background(Color.accentColor.opacity(0.10), in: RoundedRectangle(cornerRadius: 7, style: .continuous))

            VStack(alignment: .leading, spacing: 3) {
                Text(title)
                    .font(.system(size: 14, weight: .semibold))
                Text(description)
                    .font(.system(size: 12))
                    .foregroundStyle(.secondary)
            }

            Spacer(minLength: 8)

            if isGranted {
                Label("已开启", systemImage: "checkmark.circle.fill")
                    .labelStyle(.titleAndIcon)
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(Color.green)
            } else {
                Button("打开设置", action: openSettings)
                    .buttonStyle(.bordered)
                    .controlSize(.small)
                    .accessibilityLabel("打开\(title)设置")
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 12)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(title)，\(description)，\(isGranted ? "已开启" : "需要通过按钮打开设置")")
    }
}
