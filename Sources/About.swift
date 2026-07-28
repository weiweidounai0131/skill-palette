import AppKit
import SwiftUI

@MainActor
final class AboutWindowController: NSWindowController {
    init() {
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 420, height: 430),
            styleMask: [.titled, .closable, .miniaturizable, .fullSizeContentView],
            backing: .buffered,
            defer: false
        )
        // Match the quiet native "About" windows in macOS utilities: keep
        // the traffic lights, but leave the titlebar itself unlabelled.
        window.title = ""
        window.titleVisibility = .hidden
        window.titlebarAppearsTransparent = true
        window.isMovableByWindowBackground = true
        window.isReleasedWhenClosed = false
        window.level = .normal
        window.miniwindowImage = AppIcon.current
        window.contentView = NSHostingView(rootView: AboutSkillPaletteView())
        super.init(window: window)
    }

    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    func show() {
        NSApp.setActivationPolicy(.regular)
        NSRunningApplication.current.activate(options: [.activateIgnoringOtherApps])
        window?.center()
        showWindow(nil)
        window?.makeKeyAndOrderFront(nil)
    }
}

private struct AboutSkillPaletteView: View {
    private let repositoryURL = URL(string: "https://github.com/weiweidounai0131/skill-palette")!
    private let releaseURL = URL(string: "https://github.com/weiweidounai0131/skill-palette/releases/latest")!

    var body: some View {
        VStack(spacing: 0) {
            Spacer(minLength: 38)

            Image(nsImage: AppIcon.current ?? NSImage())
                .resizable()
                .interpolation(.high)
                .frame(width: 138, height: 138)
                .accessibilityHidden(true)

            Text("Skill Palette")
                .font(.system(size: 26, weight: .semibold))
                .padding(.top, 15)

            Text("版本 1.1.2")
                .font(.system(size: 14))
                .foregroundStyle(.secondary)
                .padding(.top, 4)

            Text("在 Codex 中搜索并调用本机 Skills。")
                .font(.system(size: 15))
                .foregroundStyle(.secondary)
                .padding(.top, 22)

            HStack(spacing: 24) {
                Link(destination: repositoryURL) {
                    Text("GitHub")
                }
                .accessibilityLabel("在 GitHub 打开 Skill Palette 项目")

                Link(destination: releaseURL) {
                    Text("更新")
                }
                .accessibilityLabel("在 GitHub 查看 Skill Palette 更新")
            }
            .font(.system(size: 16, weight: .medium))
            .padding(.top, 20)

            Spacer(minLength: 24)

            Text("Copyright © 2026 Jiang Jiawei. All rights reserved.")
                .font(.system(size: 11))
                .foregroundStyle(.tertiary)
                .multilineTextAlignment(.center)
                .padding(.bottom, 20)
        }
        .frame(width: 420, height: 430)
        .background(Color(nsColor: .windowBackgroundColor))
        .accessibilityElement(children: .contain)
    }
}
