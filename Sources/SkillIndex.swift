import Foundation

struct SkillScanResult {
    let addedCount: Int
    let totalCount: Int
}

final class SkillIndex: ObservableObject {
    static let shared = SkillIndex()

    @Published private(set) var skills: [Skill] = []
    @Published private(set) var categoryNames: [String] = []
    @Published private(set) var lastScanMessage = "尚未扫描"

    private init() {}

    @discardableResult
    func rescan() -> SkillScanResult {
        let existingIDs = Set(skills.map(\.id))
        let home = FileManager.default.homeDirectoryForCurrentUser
        let roots = [
            home.appendingPathComponent(".codex/skills"),
            home.appendingPathComponent(".agents/skills")
        ]

        var found: [Skill] = []
        let manager = FileManager.default
        for root in roots where manager.fileExists(atPath: root.path) {
            guard let enumerator = manager.enumerator(
                at: root,
                includingPropertiesForKeys: [.isRegularFileKey],
                // Built-in Codex skills can live under a hidden `.system`
                // directory, so hidden descendants must stay searchable.
                options: [.skipsPackageDescendants]
            ) else { continue }

            for case let url as URL in enumerator where url.lastPathComponent == "SKILL.md" {
                if let skill = parseSkill(at: url) { found.append(skill) }
            }
        }

        let unique = Dictionary(grouping: found, by: \.id).compactMap { $0.value.first }
        let categorized = categorize(unique, roots: roots)
        skills = categorized.sorted { $0.displayName.localizedCaseInsensitiveCompare($1.displayName) == .orderedAscending }
        categoryNames = Array(Set(categorized.map(\.category))).sorted {
            $0.localizedCaseInsensitiveCompare($1) == .orderedAscending
        }
        lastScanMessage = "已索引 \(skills.count) 个 Skill"
        let addedCount = Set(skills.map(\.id)).subtracting(existingIDs).count
        return SkillScanResult(addedCount: addedCount, totalCount: skills.count)
    }

    func availableScopes() -> [SkillScope] {
        let favorites = skills.contains { OverlaySettings.shared.isFavorite($0) }
        var scopes: [SkillScope] = favorites ? [.favorites, .all] : [.all]
        scopes.append(contentsOf: categoryNames.map(SkillScope.category))
        return scopes
    }

    func skills(in scope: SkillScope, matching query: String = "") -> [Skill] {
        let normalized = query.trimmingCharacters(in: .whitespacesAndNewlines)
        // Typing is intentionally global: a tag such as "PPT" should find a
        // matching Skill no matter which category was selected previously.
        guard normalized.isEmpty else { return search(normalized) }

        switch scope {
        case .all:
            return search("")
        case .favorites:
            return search("").filter { OverlaySettings.shared.isFavorite($0) }
        case let .category(name):
            return search("").filter { $0.category == name }
        }
    }

    func search(_ query: String) -> [Skill] {
        let settings = OverlaySettings.shared
        let normalized = query.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        let ranked = skills.map { skill -> (Skill, Int) in
            let name = skill.displayName.lowercased()
            let description = skill.description.lowercased()
            let tags = settings.tags(for: skill).lowercased()
            let haystack = "\(name) \(description) \(tags)"
            var score = 0
            if normalized.isEmpty {
                if settings.isFavorite(skill) { score += 30 }
                if let recent = settings.recentNames.firstIndex(of: skill.id) { score += 20 - recent }
            } else if name == normalized {
                score = 100
            } else if name.contains(normalized) {
                score = 80
            } else if tags.contains(normalized) {
                score = 70
            } else if description.contains(normalized) {
                score = 50
            } else if haystack.contains(normalized) {
                score = 20
            } else if let fuzzyScore = fuzzyMatchScore(query: normalized, name: name, tags: tags) {
                score = fuzzyScore
            } else {
                score = -1
            }
            if settings.isFavorite(skill) { score += 5 }
            return (skill, score)
        }
        return ranked
            .filter { $0.1 >= 0 }
            .sorted { lhs, rhs in lhs.1 == rhs.1 ? lhs.0.displayName < rhs.0.displayName : lhs.1 > rhs.1 }
            .map(\.0)
    }

    /// A light-weight spelling-tolerance pass for names and user tags. Exact
    /// matches always rank above this path; it only supplements a search when
    /// the normal substring search found nothing for a particular Skill.
    private func fuzzyMatchScore(query: String, name: String, tags: String) -> Int? {
        guard query.count >= 3 else { return nil }
        let candidates = searchTokens(in: name) + searchTokens(in: tags)
        guard !candidates.isEmpty else { return nil }

        let maximumDistance: Int
        switch query.count {
        case 3...4: maximumDistance = 1
        case 5...7: maximumDistance = 2
        default: maximumDistance = 3
        }

        guard let distance = candidates.map({ levenshteinDistance(query, $0) }).min(), distance <= maximumDistance else {
            return nil
        }
        return 38 - distance * 6
    }

    private func searchTokens(in value: String) -> [String] {
        let normalized = value.lowercased()
        let words = normalized.split { !$0.isLetter && !$0.isNumber }.map(String.init)
        return words + [normalized]
    }

    private func levenshteinDistance(_ lhs: String, _ rhs: String) -> Int {
        let left = Array(lhs)
        let right = Array(rhs)
        guard !left.isEmpty else { return right.count }
        guard !right.isEmpty else { return left.count }

        var previous = Array(0...right.count)
        for (leftIndex, leftCharacter) in left.enumerated() {
            var current = [leftIndex + 1] + Array(repeating: 0, count: right.count)
            for (rightIndex, rightCharacter) in right.enumerated() {
                let substitution = previous[rightIndex] + (leftCharacter == rightCharacter ? 0 : 1)
                let insertion = current[rightIndex] + 1
                let deletion = previous[rightIndex + 1] + 1
                current[rightIndex + 1] = min(substitution, insertion, deletion)
            }
            previous = current
        }
        return previous[right.count]
    }

    private func parseSkill(at url: URL) -> Skill? {
        guard let data = try? Data(contentsOf: url),
              let text = String(data: data.prefix(16_000), encoding: .utf8) else { return nil }

        let lines = text.components(separatedBy: .newlines)
        var name = ""
        var description = ""
        var withinFrontMatter = false
        var sawOpening = false
        for line in lines {
            if line == "---" {
                if sawOpening { break }
                sawOpening = true
                withinFrontMatter = true
                continue
            }
            guard withinFrontMatter else { continue }
            if line.hasPrefix("name:") {
                name = String(line.dropFirst(5)).trimmingCharacters(in: .whitespacesAndNewlines).trimmingCharacters(in: CharacterSet(charactersIn: "\"'"))
            } else if line.hasPrefix("description:") {
                description = String(line.dropFirst(12)).trimmingCharacters(in: .whitespacesAndNewlines).trimmingCharacters(in: CharacterSet(charactersIn: "\"'"))
            }
        }

        let fallback = url.deletingLastPathComponent().lastPathComponent
        let identifier = name.isEmpty ? fallback : name
        return Skill(
            id: url.standardizedFileURL.path,
            name: identifier,
            description: description,
            path: url.path,
            category: "其他"
        )
    }

    private func categorize(_ source: [Skill], roots: [URL]) -> [Skill] {
        let prefixCounts = Dictionary(grouping: source.compactMap { familyPrefix(for: $0.name) }, by: { $0 })
            .mapValues(\.count)

        return source.map { skill in
            let category = categoryName(for: skill, roots: roots, prefixCounts: prefixCounts)
            return Skill(
                id: skill.id,
                name: skill.name,
                description: skill.description,
                path: skill.path,
                category: category
            )
        }
    }

    private func categoryName(for skill: Skill, roots: [URL], prefixCounts: [String: Int]) -> String {
        let skillURL = URL(fileURLWithPath: skill.path)
        if let root = roots.first(where: { skillURL.path.hasPrefix($0.path + "/") }) {
            let relative = skillURL.path.dropFirst(root.path.count + 1)
            let folders = relative.split(separator: "/").dropLast()
            if folders.count > 1, let project = folders.first {
                return project == ".system" ? "内置" : String(project)
            }
        }

        if let prefix = familyPrefix(for: skill.name), (prefixCounts[prefix] ?? 0) > 1 {
            return prefix
        }
        return "其他"
    }

    private func familyPrefix(for name: String) -> String? {
        let delimiters = CharacterSet(charactersIn: "-:")
        guard let first = name.rangeOfCharacter(from: delimiters), first.lowerBound > name.startIndex else { return nil }
        return String(name[..<first.lowerBound]).lowercased()
    }
}
