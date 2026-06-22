# AGENTS.md

## Project

This is a Unity game project.

The main goal is to implement gameplay features in small, reviewable steps while preserving existing behavior.

Prioritize:

* Small working changes
* Clear responsibility boundaries
* Reviewable diffs
* Manual verification steps
* Preserving existing Unity scene, prefab, and asset references
* Keeping the project owner able to explain the implementation and design decisions

---

## Default Behavior

If the user's request is ambiguous, default to Research Mode.

Do not edit files unless the user explicitly asks to implement, edit, fix, or modify something.

Do not create commits, branches, pull requests, tags, or releases unless explicitly requested.

Do not stage files automatically unless explicitly requested.

Do not perform unrelated cleanup, formatting, or refactoring unless explicitly requested.

Follow existing project patterns before introducing new abstractions, naming conventions, or architectural styles.

---

## Active Mode Declaration

For Research Mode, Edit Mode, and Review Mode work, state the active mode at the start of the response:

* Active mode: Research
* Active mode: Edit
* Active mode: Review

For small code-understanding, terminology, syntax, or explanation questions covered by the Small Question Rule, do not need to state the active mode.

If the mode is ambiguous, choose Research Mode and state why.

Do not proceed with file edits when the active mode is Research or Review unless the user explicitly requests implementation, editing, fixing, or modification.

---

## Operation Modes

### Research Mode

Use Research Mode when the user asks to investigate, inspect, research, analyze, design, or plan.

In Research Mode:

* Read the related files first.
* Do not edit any files.
* Do not create, delete, move, or rename files.
* Explain the current responsibility structure.
* Identify relevant files, risks, dependencies, and likely change points.
* Suggest an implementation plan using Event / Slice units.
* If the requested change is risky or unclear, explain the risk before suggesting edits.

Research output should include:

* Files inspected
* Current responsibility structure
* Relevant dependencies
* Risks or assumptions
* Suggested Event / Slice breakdown
* Files likely to be modified later

---

### Bug Investigation Rule

If the user asks to find, investigate, inspect, or analyze a bug, default to Research Mode.

Do not edit files during bug investigation unless the user explicitly asks to fix, edit, or modify the code.

Bug investigation output should include:

* Observed or suspected symptom
* Relevant files inspected
* Likely cause
* Responsibility boundary involved
* Minimal fix proposal
* Risks of the fix
* Suggested Event / Slice for implementation

If the cause is uncertain, state the uncertainty clearly and suggest the smallest next inspection or verification step.

---

### Edit Mode

Use Edit Mode only when the user explicitly asks to implement, edit, fix, or modify.

In Edit Mode:

* Implement only the requested Event / Slice.
* Keep the diff small and reviewable.
* Do not silently implement later Events.
* Do not perform unrelated refactors.
* Preserve existing behavior unless the requested change explicitly requires behavior changes.
* If the requested implementation requires a larger design change, stop and explain the reason before editing.
* If existing responsibilities are unclear, inspect first and explain the proposed responsibility boundary before editing.

Before editing, report:

* Files inspected
* Current responsibility structure
* Files planned to modify
* Risks or assumptions

After editing, report:

* Changed files
* What changed
* Why it changed
* Possible risks
* Manual test steps in Unity Editor
* Any required Inspector setup
* Tests or verification actually performed
* Tests or verification not performed
* Explainability check

---

### Review Mode

Use Review Mode when the user asks to review, check, audit, inspect a diff, or evaluate Codex output.

In Review Mode:

* Do not edit files unless explicitly requested.
* Inspect the current diff first.
* Check whether the change satisfies the requested Event / Slice.
* Check for unrelated changes.
* Check for responsibility boundary violations.
* Check for Unity-specific risks such as broken serialized references, Inspector setup requirements, scene dependencies, prefab dependencies, and lifecycle timing issues.
* Identify whether the implementation is explainable by the project owner.
* Suggest follow-up fixes only when necessary.

Review output should include:

* Diff summary
* Event / Slice completion judgment
* Responsibility boundary check
* Unity-specific risks
* Unrelated or suspicious changes
* Manual verification checklist
* Recommended follow-up actions
* Explainability check

---

## Core Workflow

For heavy features:

1. Read the related files first.
2. Do not edit files during the research step.
3. Explain the current responsibility structure before editing.
4. Implement one Event / Slice at a time.
5. Avoid completing a large feature in one uncontrolled change.
6. After editing, summarize changed files, behavior changes, risks, and manual test steps.
7. After editing, inspect the diff when possible.
8. Do not claim behavior was verified unless it was actually verified.
9. Confirm that the final implementation is explainable by the project owner.

Heavy features include, but are not limited to:

* Enemy AI
* Combat
* Camera behavior
* Player state transitions
* Game feel systems
* Input handling
* Collision behavior
* Room / stage progression
* Save, checkpoint, or respawn systems

---

## Unity Safety Rules

Do not modify the following files unless explicitly requested:

* `*.unity`
* `*.prefab`
* `*.asset`
* `*.meta`
* `ProjectSettings/**`
* `Packages/manifest.json`
* `Packages/packages-lock.json`

Do not modify Unity serialized asset files unless explicitly requested, including but not limited to:

* `*.controller`
* `*.overrideController`
* `*.anim`
* `*.mat`
* `*.physicsMaterial2D`
* `*.renderTexture`
* `*.inputactions`

Do not modify generated, local, build, or IDE files:

* `Library/**`
* `Temp/**`
* `Obj/**`
* `Build/**`
* `Builds/**`
* `Logs/**`
* `UserSettings/**`
* `.vs/**`
* `.idea/**`
* `*.csproj`
* `*.sln`

Prefer script-only changes unless the task explicitly requires scene, prefab, asset, package, or project setting changes.

If a change requires Inspector setup, do not edit the scene or prefab automatically. Instead, explain exactly which GameObject, Component, and serialized fields must be assigned in the Unity Editor.

Be careful when renaming `[SerializeField]` fields, public fields, MonoBehaviour classes, or script filenames because Unity serialization and component references may break.

Do not rename public or serialized fields unless necessary. If renaming is required, explain the migration risk and how to verify existing Inspector references.

If a serialized field must be renamed, prefer using `[FormerlySerializedAs]` when appropriate and explain the migration risk.

---

## New Script and Meta File Handling

Prefer modifying existing scripts over creating new Unity asset files.

If a new C# script under `Assets/**` is required:

* Explain why a new file is necessary before creating it.
* Explain the responsibility of the new script.
* Explain which existing scripts will depend on it.
* Explain whether Inspector setup is required.
* Keep the new script focused on one clear responsibility.

Do not manually create or edit `.meta` files.

If a new file under `Assets/**` is created, Unity may generate a corresponding `.meta` file later.

When a new Unity asset or script is intended to be committed, its generated `.meta` file should usually be included in version control after Unity creates it.

Do not invent or hand-write Unity GUIDs.

Do not create new folders under `Assets/**` unless explicitly requested.

Do not move scripts between folders unless explicitly requested.

---

## C# / Gameplay Architecture Rules

* Keep responsibilities explicit.
* Follow existing project patterns before introducing a new architecture.
* Do not put unrelated logic into one large Manager class.
* Avoid mixing input, movement, collision, UI, camera, debug, and game state responsibilities.
* Prefer small components with clear responsibilities.
* Use `[SerializeField] private` for Inspector-exposed fields when appropriate.
* For every Inspector-exposed `[SerializeField] private` field that is intended to be configured in the Unity Editor, add both Japanese `[Header]` and Japanese `[Tooltip]` attributes.
* Do not add only `[Header]` or only `[Tooltip]` for such fields; use both together.
* `[Header]` text should group fields by responsibility or behavior in Japanese.
* `[Tooltip]` text should explain what the field controls, when it is used, and any important range or setup notes in Japanese.
* If a field is exposed in the Inspector but does not need `[Header]` and `[Tooltip]`, explain why it is intentionally exempt.
* Do not rename serialized fields only to improve Inspector wording.
* Do not add noisy headers or tooltips to purely internal fields that are not intended to be configured in the Unity Editor.
* When adding or modifying non-trivial C# gameplay code, add concise Japanese comments that explain responsibility, intent, side effects, or important assumptions.
* Prefer comments that explain why the code exists or what responsibility it owns, not line-by-line explanations of obvious syntax.
* For Unity gameplay scripts, use Japanese comments when they help explain serialized settings, state transitions, lifecycle methods, collision conditions, time control, camera behavior, or game feel behavior.
* Do not add noisy comments to self-explanatory code.
* Do not leave commented-out old code unless explicitly requested.
* Do not paste AI reasoning logs, Codex logs, or raw conversation text into code comments.
* After implementation, the project owner should be able to explain the new behavior from names, Inspector headers/tooltips, and concise Japanese comments.
* Do not introduce singletons, static mutable state, or global state without explaining why.
* Preserve existing public APIs unless explicitly requested.
* Do not rename public or serialized fields unless necessary, because it can break Inspector references.
* Do not hide configuration errors with broad fallback logic unless the behavior is intentional and explained.
* Prefer explicit null checks with clear failure behavior for serialized references.
* Prefer explicit dependencies over hidden scene searches such as broad `FindObjectOfType`, unless the existing project already uses that pattern or the trade-off is explained.
* Avoid adding Update-loop work that scales poorly without explaining the cost.
* Do not change MonoBehaviour lifecycle methods such as `Awake`, `Start`, `Update`, `FixedUpdate`, `OnEnable`, or `OnDisable` without considering existing execution order and side effects.
* Keep gameplay logic explainable by the project owner.

---

## Event / Slice Implementation Rules

When implementing an Event / Slice:

* Implement only the requested Event / Slice.
* Do not silently implement later Events.
* Do not perform unrelated refactors.
* Keep the diff small and reviewable.
* Make the change easy to test manually.
* Make the change easy to revert if it fails.
* If the requested implementation requires a larger design change, stop and explain the reason before editing.
* If existing responsibilities are unclear, inspect first and explain the proposed responsibility boundary before editing.

A good Event / Slice should:

* Have one clear purpose.
* Be testable in Unity Editor.
* Be suitable for a meaningful commit.
* Avoid changing unrelated behavior.
* Leave the project in a working state.
* Be explainable as one step of progress.

A good Event / Slice is usually close to one meaningful commit, but this is not an absolute rule.

---

## Diff / Version Control Safety Rules

Before editing:

* Check the current working tree status when possible.
* Inspect the relevant files.
* Keep the planned change narrow.
* Identify files that are likely to change.

If there are existing user changes:

* Do not overwrite them.
* Report them clearly.
* Avoid touching those files unless they are part of the requested task.

After editing:

* Run `git diff` or equivalent diff inspection when available.
* Report all changed files.
* Do not include unrelated formatting-only changes.
* Do not reformat entire files unless explicitly requested.
* Do not rename, move, or delete files unless explicitly requested.
* Do not change line endings or file encoding intentionally unless required.
* Do not modify generated project files.
* If unexpected files changed, stop and report them.

Do not create commits automatically unless explicitly requested.

Do not stage files automatically unless explicitly requested.

Do not discard or overwrite existing user changes unless explicitly requested.

---

## Command Execution Safety Rules

Do not run destructive commands unless explicitly requested.

Avoid commands that may delete, overwrite, reset, or mass-modify files, including but not limited to:

* `git reset --hard`
* `git clean`
* Mass rename commands
* Mass formatting commands
* Package upgrade commands
* Dependency update commands

Do not install, update, or remove packages unless explicitly requested.

Do not run long or environment-dependent commands unless they are necessary for the requested task.

If a command cannot be run or its result is uncertain, state that clearly.

---

## Debug / Verification Rules

For gameplay behavior changes, provide a manual Unity Editor test plan.

When useful, suggest temporary verification methods such as:

* `Debug.Log`
* Gizmos
* Serialized debug fields
* On-screen debug text

Do not leave noisy debug logs unless requested.

If debug logs are added for verification, mention whether they should be removed before merge.

If Unity Editor tests, Play Mode tests, builds, or runtime checks were not actually run, state that clearly.

Do not claim that behavior was verified unless it was actually verified.

---

## Explainability Check

After implementing or reviewing a change, include a short explainability check.

The explainability check should answer:

* Responsibility: Which script or component owns this behavior?
* Dependency: Which other scripts, components, or Inspector references does it depend on?
* Design reason: Why was this approach chosen over a larger refactor or different design?
* Verification: How can the project owner confirm the behavior in Unity?

If the implementation is too complex for the project owner to explain, stop and suggest a smaller or clearer design.

---

## AI Usage and Documentation Rule

Do not paste AI conversation logs, Codex reasoning logs, or raw research transcripts into code comments, Issues, PR descriptions, or documentation.

When preparing GitHub-facing text, summarize the result as the project owner's design decision.

Good documentation should explain:

* What was changed
* Why the change was needed
* What alternatives or risks were considered
* How the behavior was verified
* What remains as a future task

Do not write documentation that makes the change look like it was blindly generated by AI.

---

## Output Style

Keep reports concise and action-oriented.

Do not paste large code blocks unless the user asks for them.

Prefer summaries, file paths, responsibility notes, risks, and verification steps.

When reporting diffs, summarize the meaningful changes instead of reproducing the entire diff.

When preparing GitHub-facing text, write it as a project decision, not as an AI transcript.

---

## Small Question Rule

For small code-understanding, terminology, syntax, or explanation questions, answer directly and briefly.

Do not use the full Research / Edit / Review report format for small questions.

Do not inspect unrelated files or suggest implementation plans unless the user asks for them.

Do not edit files unless the user explicitly asks to modify code.

---

## Reporting Format

Use the full reporting formats only for heavy, risky, or implementation-related work.

For small explanation questions, terminology questions, or simple code-understanding questions, answer directly without the full report format.

### Before editing

Report:

* Active mode
* Files inspected
* Current responsibility structure
* Files planned to modify
* Risks or assumptions

### After editing

Report:

* Active mode
* Changed files
* What changed
* Why it changed
* Possible risks
* Manual test steps in Unity Editor
* Any required Inspector setup
* Tests or verification actually performed
* Tests or verification not performed
* Explainability check

### During review

Report:

* Active mode
* Diff summary
* Event / Slice completion judgment
* Responsibility boundary check
* Unity-specific risks
* Unrelated or suspicious changes
* Manual verification checklist
* Recommended follow-up actions
* Explainability check

---

## Log Output Rule

When adding or modifying runtime logs such as `Debug.Log`, `Debug.LogWarning`, or `Debug.LogError`:

* Use English log tags.
* Log messages may be written in short Japanese if it helps the project owner and team understand the runtime behavior quickly.
* Use a consistent tag format such as `[Collectible]`, `[PlayerState]`, `[RoomTransition]`, or `[Camera]`.
* The tag should identify the system, feature, or responsibility that owns the log.
* Keep log messages short and easy to understand.
* If extra explanation is needed, add a concise Japanese code comment near the logic instead of making the log message too long.
* Do not use Japanese log tags.
* Do not write long explanation-style logs.

Good examples:

```csharp
Debug.Log("[Collectible] 仮取得状態に追加しました");
Debug.Log("[Collectible] 死亡したので仮取得状態を破棄しました");
Debug.Log("[Collectible] 部屋突破により収集状態を保存しました");
Debug.LogWarning("[Collectible] Session manager が見つかりません");
```

Japanese comments may be used to explain intent:

```csharp
// 死亡時は仮取得を破棄し、次の挑戦で再取得できる状態に戻す。
Debug.Log("[Collectible] 死亡したので仮取得状態を破棄しました");
```

