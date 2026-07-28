import AppKit
import SwiftUI

@MainActor
final class PickerController: NSWindowController, NSWindowDelegate {
    static let shared = PickerController()

    private var targetElement: AXUIElement?
    private var targetApplication: NSRunningApplication?
    private let pickerState = PickerState()
    private var keyMonitor: Any?

    private init() {
        let panel = PickerPanel(
            contentRect: NSRect(x: 0, y: 0, width: 560, height: 460),
            styleMask: [.titled, .fullSizeContentView, .utilityWindow],
            backing: .buffered,
            defer: false
        )
        panel.title = "选择 Skill"
        panel.titlebarAppearsTransparent = true
        panel.isMovableByWindowBackground = true
        panel.level = .floating
        panel.isReleasedWhenClosed = false
        panel.hidesOnDeactivate = false
        super.init(window: panel)
        panel.delegate = self
        panel.contentView = NSHostingView(rootView: SkillPickerView(state: pickerState, onChoose: choose, onCancel: cancel))
    }

    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    func open(targetElement: AXUIElement?, targetApplication: NSRunningApplication?) {
        self.targetElement = targetElement
        self.targetApplication = targetApplication
        pickerState.query = ""
        pickerState.selection = 0
        pickerState.scope = SkillIndex.shared.availableScopes().contains(.favorites) ? .favorites : .all
        positionPanel(at: NSEvent.mouseLocation)
        NSApp.activate(ignoringOtherApps: true)
        showWindow(nil)
        window?.makeKeyAndOrderFront(nil)
        installKeyMonitor()
    }

    private func positionPanel(at mouseLocation: NSPoint) {
        guard let panel = window else { return }
        let screen = NSScreen.screens.first { $0.frame.contains(mouseLocation) } ?? NSScreen.main
        guard let screen else { return }

        let visibleFrame = screen.visibleFrame
        let size = panel.frame.size
        let margin: CGFloat = 12
        let preferredX = mouseLocation.x - size.width / 2
        // In AppKit coordinates the origin is bottom-left. Prefer displaying
        // above the pointer, then fall back below it near the top edge.
        let preferredY = mouseLocation.y - size.height - margin
        let fallbackY = mouseLocation.y + margin
        let desiredY = preferredY >= visibleFrame.minY + margin ? preferredY : fallbackY
        let maxX = max(visibleFrame.minX + margin, visibleFrame.maxX - size.width - margin)
        let maxY = max(visibleFrame.minY + margin, visibleFrame.maxY - size.height - margin)
        let origin = NSPoint(
            x: min(max(preferredX, visibleFrame.minX + margin), maxX),
            y: min(max(desiredY, visibleFrame.minY + margin), maxY)
        )
        panel.setFrameOrigin(origin)
    }

    private func choose(_ skill: Skill) {
        let text = OverlaySettings.shared.renderedInvocation(for: skill)
        OverlaySettings.shared.recordUse(skill)
        closePicker()

        // Wait until the floating picker has resigned focus before updating the
        // original AX text element. This avoids putting text in the picker.
        let target = targetElement
        let application = targetApplication
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.08) {
            application?.activate(options: [.activateIgnoringOtherApps])
            if !TextInsertion.insert(text, into: target) {
                TextInsertion.paste(text, into: application)
            }
        }
    }

    private func cancel() {
        let application = targetApplication
        closePicker()
        // Cancelling only dismisses this temporary panel. The menu-bar app
        // stays alive and focus returns to the original editor.
        DispatchQueue.main.async {
            application?.activate(options: [.activateIgnoringOtherApps])
        }
    }

    private func closePicker() {
        if let keyMonitor {
            NSEvent.removeMonitor(keyMonitor)
            self.keyMonitor = nil
        }
        window?.orderOut(nil)
    }

    func windowDidResignKey(_ notification: Notification) {
        // A click outside the panel should behave like Escape. Defer one turn
        // so controls that are opening their own menus keep working normally.
        DispatchQueue.main.async { [weak self] in
            guard let self, self.window?.isVisible == true, self.window?.isKeyWindow == false else { return }
            self.closePicker()
        }
    }

    private func installKeyMonitor() {
        if let keyMonitor { NSEvent.removeMonitor(keyMonitor) }
        keyMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            guard let self, self.window?.isVisible == true else { return event }
            let results = SkillIndex.shared.skills(in: self.pickerState.scope, matching: self.pickerState.query)
            switch event.keyCode {
            case 126:
                self.pickerState.selection = max(0, self.pickerState.selection - 1)
                return nil
            case 125:
                self.pickerState.selection = min(max(0, results.count - 1), self.pickerState.selection + 1)
                return nil
            case 36, 76:
                if results.indices.contains(self.pickerState.selection) {
                    self.choose(results[self.pickerState.selection])
                }
                return nil
            case 53:
                self.cancel()
                return nil
            default:
                return event
            }
        }
    }
}

final class PickerPanel: NSPanel {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { true }
}

final class PickerState: ObservableObject {
    @Published var query = ""
    @Published var selection = 0
    @Published var scope: SkillScope = .all
}

struct SkillPickerView: View {
    @ObservedObject private var index = SkillIndex.shared
    @ObservedObject private var settings = OverlaySettings.shared
    @ObservedObject var state: PickerState
    @FocusState private var searchFocused: Bool

    let onChoose: (Skill) -> Void
    let onCancel: () -> Void

    private var results: [Skill] { index.skills(in: state.scope, matching: state.query) }

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 10) {
                Image(systemName: "magnifyingglass")
                    .foregroundStyle(.secondary)
                TextField("搜索 Skill：例如 PPT、飞书、图片…", text: $state.query)
                    .textFieldStyle(.plain)
                    .focused($searchFocused)
                    .onChange(of: state.query) { _ in state.selection = 0 }
                Text("Esc 取消")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            .padding(14)

            Divider()

            SkillScopePicker(
                scopes: index.availableScopes(),
                selection: $state.scope,
                count: { index.skills(in: $0).count }
            )
            .padding(.horizontal, 14)
            .padding(.vertical, 10)
            .onChange(of: state.scope) { _ in state.selection = 0 }

            if !state.query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                HStack(spacing: 5) {
                    Image(systemName: "magnifyingglass")
                    Text("全部分类中找到 \(results.count) 个结果")
                }
                .font(.caption)
                .foregroundStyle(.secondary)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.horizontal, 14)
                .padding(.bottom, 7)
            }

            if results.isEmpty {
                VStack(spacing: 10) {
                    Image(systemName: "rectangle.and.text.magnifyingglass")
                        .font(.largeTitle)
                        .foregroundStyle(.secondary)
                    Text("没有找到 Skill").font(.headline)
                    Text("可以在设置中为 Skill 添加中文标签。")
                        .font(.caption).foregroundStyle(.secondary)
                }
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                // List preserves a previous scroll anchor as its rows change,
                // which feels like locating an old row rather than showing the
                // new query result. A dedicated result scroller starts each
                // query at the first matching row instead.
                ScrollView {
                    LazyVStack(spacing: 0) {
                        ForEach(Array(results.enumerated()), id: \.element.id) { offset, skill in
                            Button {
                                onChoose(skill)
                            } label: {
                                HStack(alignment: .top, spacing: 10) {
                                    Image(systemName: OverlaySettings.shared.isFavorite(skill) ? "star.fill" : "wand.and.stars")
                                        .foregroundStyle(OverlaySettings.shared.isFavorite(skill) ? Color.yellow : Color.accentColor)
                                    VStack(alignment: .leading, spacing: 3) {
                                        Text(skill.displayName).font(.headline)
                                        if !skill.description.isEmpty {
                                            Text(skill.description).font(.caption).foregroundStyle(.secondary).lineLimit(2)
                                        }
                                        if skill.category != "其他" {
                                            Label(skill.category, systemImage: "folder")
                                                .font(.caption2)
                                                .foregroundStyle(.secondary)
                                        }
                                    }
                                    Spacer()
                                    if offset == state.selection { Text("↵").foregroundStyle(.secondary) }
                                }
                                .padding(.horizontal, 14)
                                .padding(.vertical, 9)
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .background(offset == state.selection ? Color.accentColor.opacity(0.12) : Color.clear)
                                .contentShape(Rectangle())
                            }
                            .buttonStyle(.plain)
                            .accessibilityLabel("选择 \(skill.displayName)")

                            if offset < results.count - 1 {
                                Divider().padding(.leading, 45)
                            }
                        }
                    }
                }
                .id(state.query)
            }

            Divider()
            HStack {
                Text(index.lastScanMessage).foregroundStyle(.secondary)
                Spacer()
                Text("↑↓ 选择 · Enter 插入")
                    .foregroundStyle(.secondary)
            }
            .font(.caption)
            .padding(10)
        }
        .frame(width: 560, height: 460)
        .onAppear {
            searchFocused = true
        }
    }
}
