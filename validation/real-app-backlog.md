# Real App Backlog

This backlog turns real desktop app validation into the main product-driving loop for Allyflow.

## Product Goal

- Use the current Allyflow product path directly on real desktop apps.
- Let real operator flows expose weaknesses in query, locate, action, verification, and recovery.
- Convert each weakness into a concrete product improvement or an explicit accessibility boundary.

## Execution Order

Validation should move from easier to harder:

1. Notepad
2. Calculator
3. Standard file dialogs
4. Settings and shell surfaces
5. User-provided complex apps

## Issue Taxonomy

Every real-app finding should be classified into one primary bucket:

1. Window discovery
2. Snapshot readability
3. Selector portability
4. Ref stability
5. Action support
6. Verification quality
7. Recovery quality
8. Accessibility boundary

## Acceptance Gates

A real-app task is only considered complete when all of the following are true:

1. The target app and user task are named explicitly.
2. The exact product chain is recorded: `windows_list -> windows_snapshot -> windows_locate -> windows_act`.
3. Successes and failures are both written down.
4. At least one concrete product improvement or boundary statement is captured.
5. The validated flow can be rerun through an automated integration entry point when feasible.

## App Backlog

### Notepad

- Priority: now
- Status: validated baseline
- Goals:
  - Validate text surface discovery on modern Notepad.
  - Validate direct text entry through `set_value`.
  - Expand later to safe end-to-end file flows.
- Current known findings:
  - Main editor is exposed as `document`, not `edit`.
  - Product-native window discovery is more reliable than external process metadata.
  - The `文件` menu and `另存为` flow are visible through the same structured accessibility path.
- Next tasks:
  - Capture selector refinement heuristics for text surfaces and nested dialog surfaces.
  - Add one follow-up task that exercises saved-file overwrite or filename conflict handling.

### Calculator

- Priority: now
- Status: validated baseline
- Goals:
  - Keep button invocation and result reading stable on this machine.
  - Expand later to richer mode and navigation flows.
- Current known findings:
  - Current platform can expose Calculator through localized title and framework-based discovery rather than one stable English title.
  - A simple `1 + 2 = 3` button chain already works through `invoke` plus result reading.
- Next tasks:
  - Add one richer Calculator task that exercises more than four button presses.
  - Capture reusable selector guidance for localized Calculator controls.

### Standard File Dialogs

- Priority: next
- Status: validated baseline
- Goals:
  - Validate common edit, tree, and button controls.
  - Validate focus refresh and selector ambiguity handling.
- Current known findings:
  - The real `另存为` dialog can appear as a nested `Window` inside the parent Notepad snapshot instead of surfacing as a separate top-level window.
  - File-name input, save button, cancel button, navigation tree, and file list are all visible through the current accessibility-tree path.
  - Real validation exposed and required a product fix in `press_keys` letter-key mapping.
  - A safe end-to-end save into a disposable path now works through the current product path.
  - A real ambiguity baseline now exists inside the nested dialog: `button[name="打开"]` can legitimately return multiple candidates, and the navigation tree supports a stable focus flow on `treeitem[name="此电脑"]`.
  - A real non-visual ambiguity-recovery baseline now exists: ambiguous `windows_locate` results can be resolved through candidate refs, `windows_describe_ref`, and exact-ref `windows_act` without any visual fallback.
  - The nested save dialog also exposes `ComboBox "保存类型:"`, but on this machine the live accessibility tree did not yield stable descendant selection items or visible option labels for a generic real `select` baseline.
- Next tasks:
  - Turn the current ambiguity-recovery chain into a more reusable operator workflow and guidance surface, not just a single validated example.
  - Clarify the non-visual product boundary for standard dialogs before pushing deeper tree/list navigation on host-specific surfaces.

### Settings And Shell Surfaces

- Priority: later
- Status: validated baseline
- Goals:
  - Validate mixed Win32/UWP shell surfaces.
  - Stress window discovery and focus continuity.
- Current known findings:
  - Explorer can be launched directly on a real workspace folder through `RealAppTestDriver.Launch`.
  - The real Explorer surface exposes stable controls such as `edit[name="地址栏"]` and refresh buttons matched with `button[name~="刷新"]`.
  - Explorer now provides a reusable shell-side regression target for both `invoke` and `press_keys(F5)`.
  - Explorer also exposes a stable non-Settings `select` surface through `List "项目视图"` and visible `ListItem` descendants, giving the substrate a second real value-selection baseline without app-specific logic.
  - Explorer now also supports a deeper breadcrumb continuity regression: invoking the `tests` breadcrumb can move to an ancestor directory, invoking a visible child can descend back into the project subtree, invoking a second breadcrumb step can move up again, and repeated `后退` can restore the original folder while the original window-scoped breadcrumb/tab surface becomes visible again.
  - `ms-settings:` launch now works through `RealAppTestDriver.Launch`, and `ms-settings:display` exposes a real `设置` window with a searchable `edit[name="搜索框，查找设置"]` control.
  - Settings may reuse an existing top-level `设置` window instead of always appearing as a brand-new window, so discovery helpers cannot assume a strict new-window pattern for every Settings task.
  - Real Settings now supports a reversible `夜间模式` toggle round trip through `windows_act(action="toggle")` when the flow includes an explicit restore step.
  - Real Settings also supports low-risk combobox validation through `expand` and `collapse` on `combobox[name="缩放"]` without changing the selected value.
  - Real Settings now also supports a reversible `select` flow on `combobox[name="缩放"]`, making `select` a real bottom-layer value primitive rather than a fixture-only action.
  - Some Settings controls need ambiguity resolution at the candidate level even when the visible name matches exactly, as shown by `夜间模式`.
  - Real Settings now supports a deeper `设置 -> 系统 -> 声音` navigation flow and a second reversible toggle round trip on `单声道音频`.
  - Real Settings now also supports a safe persistent value-change round trip through `invoke` on the `静音音量` control on the `声音` page, with an explicit restore step in the same flow.
  - Real Settings now also supports a true `range_value` round trip on the `调整输出音量` slider when the helper waits for actual Sound-page content instead of accepting an empty shell window as ready.
  - Stable Settings automation required true window-appearance polling and content-readiness waits because `ms-settings:display` can briefly expose no Settings window or only an almost-empty shell window before the page tree loads.
  - Real Notepad shortcut validation is not uniformly foreground-insensitive: the `Ctrl+Shift+S` save-dialog baseline was only stable on this machine after keeping an explicit focus step and enabling `AutoActivateWindow` for the shortcut action itself.
- Next tasks:
  - Expand real `select` coverage beyond Settings and Explorer without assuming that every visible combobox is a usable selection surface; the standard save dialog `保存类型:` combobox still does not expose stable live options on this machine.
  - Compare shortcut behavior across shell surfaces with and without forced activation.
  - Expand shell continuity coverage beyond the current Explorer multi-step breadcrumb sample.

### User-Provided Complex Apps

- Priority: later
- Status: validated expanded baseline
- Current known findings:
  - `Clash Verge` currently exposes mostly shell/window chrome through the accessibility tree and should be treated as an accessibility boundary for now.
  - `Codex` exposes a rich business tree and is the current next complex-app target.
  - Top-level window discovery can miss some real apps when it relies only on `Desktop -> UIA child windows`; `Qwen` on this machine required Win32 top-level enumeration plus UIA `FromHandle(...)` attachment before it became visible to `windows_list` and `windows_snapshot`.
  - Codex snapshot construction now survives dynamic COM failures while reading children, roles, bounds, and related UIA properties, which was required before deeper live-app simulation could continue.
  - The focus baseline now targets the current snapshot tree directly instead of assuming that an older visible label such as `完全访问权限` will still be present by the time a second locate pass runs.
  - A stable Codex invoke baseline proves structured `invoke` on a current snapshot-stable toolbar action, using live visible controls such as `button[name="搜索"]`, `button[name="新对话"]`, or `button[name="隐藏边栏"]` rather than stale remembered targets.
  - A second invoke-style Codex baseline now proves richer live simulation by invoking a current snapshot-visible action from the active live state, including command-palette-adjacent surfaces when that is what the app currently exposes.
  - The current Codex focus and invoke baselines now also prove stronger post-action semantics: the relevant live surface still exists after execution, and the post-action target is re-resolved from the new snapshot before `windows_describe_ref` verification, instead of assuming the original pre-action ref survives tree drift.
  - Qwen now also provides a real complex-app `set_value` baseline through the composer input `Edit "有什么我能帮您的吗？"`, plus a second `invoke` baseline on `新建对话` when the business surface is currently visible, confirming that the window discovery fix recovers a usable non-mouse product path for that app family.
  - Qwen is strongly state-dependent: when it falls back to a shell-like menu surface, the business controls disappear and even previously valid baselines should be treated as temporarily unavailable rather than as substrate failures.
  - Qwen's currently visible anonymous mode combobox is not yet a stable `expand/collapse` baseline on this machine because observed `expand_state` stays `Collapsed` after repeated expand attempts.
  - `AK` now becomes discoverable through the same Win32-backed window discovery path, but its current snapshot is effectively empty and should be treated as an accessibility boundary until a richer actionable tree appears.
  - `CC Switch` is likewise discoverable through the same top-level window path, but its current snapshot exposes only shell/window chrome and should also be treated as an accessibility boundary until business controls appear in the live tree.
  - NetEase Cloud Music is likewise discoverable through the same top-level window path, but its current snapshot exposes only `Window`/`Pane`/`Document` shell surfaces and should also be treated as an accessibility boundary until business controls appear in the live tree.
  - WeChat is likewise discoverable through the same top-level window path, and it now already supports three low-side-effect real baselines through the current substrate: chat-composer `set_value`, conversation-surface `select`, and navigation-button `invoke`.
  - The current WeChat `set_value` and navigation `invoke` baselines now also prove stronger post-action semantics: the relevant business surface remains present after execution, and the invoked navigation target remains describe-able through `windows_describe_ref`.
- Next tasks:
  - Expand beyond the current focus/invoke/set_value/select baselines only when the next target is visible in the live snapshot at execution time.
  - Decide whether the next improvement should prioritize stronger verification depth, state transitions, or richer selection-style interactions on current command-palette/list surfaces.
- Entry rule:
  - Continue only where the baseline backlog above has already yielded reusable discovery, selector, and action guidance.

## Implementation Backlog

### Validation Infrastructure

- [ ] Keep reusable real-app launch/discovery helpers in integration tests.
- [ ] Standardize per-app task naming and result capture.
- [ ] Keep lightweight real-app tests serial and deterministic.

### Query Improvements

- [ ] Improve selector portability for real text surfaces (`edit`, `document`, similar equivalents).
- [ ] Improve snapshot-to-selector operator guidance.
- [ ] Improve handling of localized and framework-backed app discovery.
- [ ] Improve guidance for nested dialog windows that do not appear as separate top-level entries.
- [ ] Improve readiness guidance for shell-protocol surfaces that first appear as empty or near-empty windows before the content tree loads.
- [ ] Improve ambiguity guidance so `windows_locate` directly points operators toward `windows_describe_ref` and candidate-based refinement.
- [ ] Improve control-target stability guidance for complex apps whose visible actionable surface drifts between runs.

### Action Improvements

- [ ] Expand real-app coverage for `invoke`.
- [ ] Expand real-app coverage for focus-sensitive flows.
- [ ] Expand real-app coverage for `press_keys` beyond the current Notepad `Ctrl+Shift+S` and Explorer `F5` baselines.
- [ ] Expand real-app coverage for `select` beyond the current fixture combobox and Settings `缩放` baseline.
- [ ] Expand verification quality on non-fixture apps.
- [ ] Separate actions that truly require foreground activation from actions that can remain stable through direct UIA patterns without forced activation.
- [ ] Keep `ExecutionPolicy` constrained to single-action execution semantics; do not grow it into a workflow planning surface.
- [ ] Keep `dry_run` constrained to execution preview semantics rather than multi-step task planning.
- [ ] Keep `AutoActivateWindow` limited to foreground-sensitive execution paths rather than unconditional pre-activation.
- [ ] Expand complex-app coverage beyond conservative focus baselines once current live targets stay visible across runs.

### Recovery Improvements

- [ ] Make failure outputs easier to classify by taxonomy.
- [ ] Standardize ambiguity and stale-target recovery diagnostics across `windows_locate` and `windows_act`.
- [ ] Keep `surface_recovery` and `target_recovery` semantics consistent across query and action failures.
- [ ] Add more real-app stale-ref and ambiguity recovery samples.
- [ ] Add more real-app synchronization recovery samples for delayed shell or Settings surfaces.
- [ ] Add more reusable recovery examples that continue from ambiguous locate results by exact ref rather than only by selector refinement.
