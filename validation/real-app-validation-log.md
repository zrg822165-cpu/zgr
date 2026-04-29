# Real App Validation Log

This file records real desktop app validation using Allyflow's current product path:

- `windows_list`
- `windows_snapshot`
- `windows_locate`
- `windows_act`

It focuses on two kinds of findings:

- product optimizations needed on the current `accessibility_tree` mainline
- current product limitations or external accessibility boundaries

## App Order

Validation order should move from easier to harder:

1. Notepad
2. Calculator
3. Standard file dialogs
4. User-provided harder apps

## Findings

### Notepad

- Status: validated
- Query path: `windows_list -> windows_snapshot -> windows_locate` succeeded on real Notepad
- Action path: `windows_act(action="set_value")` succeeded on the real editor surface, and `windows_act(action="invoke")` plus `windows_act(action="set_value")` succeeded through a real `文件 -> 另存为` save flow
- Optimizations needed:
  - The first selector guess `edit` failed even though the product path was healthy. Real Notepad exposes the main editor as `Document "文本编辑器"`, not a classic `Edit` control. This means product guidance and selector strategy need stronger role adaptation for real apps.
  - Real-app validation should prefer product-native discovery first. Using external process metadata such as `Process.MainWindowTitle` was less reliable than `windows_list` and led to false setup failures.
  - Snapshot summaries are already useful for diagnosis. The next product improvement should expose an easier operator workflow for turning snapshot observations into refined selectors without needing a test-only debug step.
  - Real Notepad window titles are not stable after text changes. A durable operator flow should stay anchored to `WindowRef` and refresh the current title when selectors still need a title-based scope.
- Current limitations:
  - The current mainline does not automatically bridge equivalent editor surfaces across apps. A user or operator still needs to refine selectors when a text surface is exposed as `document`, `pane`, or another non-`edit` role.
  - This validation now proves real text entry, menu navigation, nested save-dialog discovery, and safe file persistence on modern Notepad. It still does not prove broader tab handling or markdown toolbar operations.
  - The current product still depends on accessibility-tree exposure. If an app hides its editable surface from UIA or exposes text only through richer text patterns that are not yet normalized into the selector/action model, the current path may stall.
  - Real-app baseline execution can be disrupted by foreground-window activation rules. At least for Notepad text entry, the baseline should not depend on forced activation when direct UIA `ValuePattern` execution already works.

### Calculator

- Status: validated
- Query path: `windows_list -> windows_snapshot -> windows_locate` succeeded on real Calculator using title or framework-based discovery
- Action path: `windows_act(action="invoke")` succeeded on real Calculator buttons for a simple `1 + 2 = 3` flow
- Optimizations needed:
  - Keep `windows_list` discovery tolerant of localized titles and framework-backed app windows because the first reliable Calculator match came from a mix of localized title and framework heuristics rather than a single stable English title.
  - Snapshot summaries should continue surfacing actionable button names clearly because button-driven real-app validation depends on quickly turning visible labels into selectors.
- Current limitations:
  - The current baseline only proves a simple button sequence. It does not yet validate navigation drawer flows, mode switches, history interactions, or more complex expression editing.
  - Calculator localization still matters. Selector guidance and reusable heuristics will need to handle localized labels if this baseline is to generalize across machines.

### Standard File Dialogs

- Status: validated
- Query path: `windows_list -> windows_snapshot -> windows_locate` succeeded when the dialog was opened from real Notepad through the current product path
- Action path: `windows_act(action="invoke")` succeeded for the `文件` menu, `另存为` menu item, recovered ambiguous `打开` button, `保存(S)` button, and `取消` button; `windows_act(action="set_value")` also succeeded on the nested file-name input; `windows_act(action="focus")` succeeded on the navigation tree during a real dialog ambiguity/tree-focus flow
- Optimizations needed:
  - Standard file dialogs should not be modeled only as new top-level windows. On this machine, the real `另存为` dialog was exposed inside the parent Notepad snapshot as a nested `Window "另存为"`, so operator guidance and future selector helpers should account for embedded dialog subtrees.
  - `press_keys` had a real product bug on letter keys. The current implementation incorrectly assumed enum names like `S`; the fix was to map single-character letters and digits directly to virtual key codes.
  - `press_keys` also needed function-key coverage. The product now supports `F1` through `F24`, and real validation uses `F5` on Explorer as the first shell-side regression.
  - Dialog selector guidance should explicitly call out reusable control names such as `edit[name="文件名:"]`, `button[name="保存(S)"]`, and `button[name="取消"]` for standard save flows.
  - Real dialog menu entry should prefer the currently active window and stable refs over stale title-only assumptions. The modern Notepad `文件 -> 另存为` chain was more reliable once the helper followed the active live surface and then refreshed the parent window title by `WindowRef`.
  - Dialog ambiguity is a real product condition, not just a synthetic test case. On this machine, `button[name="打开"]` inside the nested dialog produced multiple candidates, so ambiguity reporting and follow-up selection/focus workflows are part of the baseline surface.
  - Ambiguity guidance should not stop at “refine the selector.” The product now needs to actively guide the operator toward `windows_describe_ref` on returned candidate refs, then refinement by role, `automation_id`, `name`, or structure.
- Current limitations:
  - This baseline now proves dialog discovery, control location, a safe end-to-end save to a disposable path, one ambiguity-plus-tree-focus flow inside the nested dialog, and one end-to-end non-visual ambiguity-recovery flow through candidate refs plus `windows_describe_ref`.
  - The dialog surface is localized on this machine, so direct selector reuse across locales will still need heuristics or operator refinement.
  - The standard `另存为` dialog currently exposes `ComboBox "保存类型:"` through the accessibility tree, but the live surface did not expose descendant selection items or option labels strongly enough to establish a stable real `select` baseline without product-specific assumptions. For now this should be treated as a current surface boundary, not as a substrate execution gap.
  - Deeper tree/list navigation inside the standard dialog is intentionally deferred for now. The current product mainline is still explicitly accessibility-tree-first and has no visual fallback, so pushing farther into semantic navigation on host-specific tree/list surfaces would be premature before the non-visual product boundary is clearer.

### Settings And Shell Surfaces

- Status: validated baseline
- Query path: `windows_list -> windows_snapshot -> windows_locate` succeeded on both a real Explorer window and a real Settings window opened through `ms-settings:display`
- Action path: `windows_act(action="invoke")` succeeded on the real Explorer refresh button, `windows_act(action="press_keys")` succeeded on the Explorer address bar with `F5`, and `windows_act(action="set_value")`, `windows_act(action="toggle")`, `windows_act(action="expand"/"collapse")`, `windows_act(action="focus")`, and deeper Settings navigation via `windows_act(action="invoke")` all succeeded on real Settings controls
- Optimizations needed:
  - Real shell windows are a practical baseline for mixed navigation, focus, and shortcut validation before moving to harder shell or Settings surfaces.
  - `press_keys` validation should distinguish true key-sending coverage from foreground-activation coverage. On this machine, forcing `AutoActivateWindow` was less stable than relying on an explicit prior `focus` step for shortcut tests.
  - Not all shortcut paths have the same foreground sensitivity. Explorer `F5` remains stable without forced activation, but the real Notepad `Ctrl+Shift+S` save-dialog shortcut was only stable after treating it as a true foreground-sensitive path and enabling `AutoActivateWindow` for that specific baseline.
  - Explorer also exposes a genuine non-Settings `select` surface through `List "项目视图"` plus descendant `ListItem` nodes. This is a better second real `select` baseline than the standard save dialog because the live tree already exposes stable selectable items without any app-specific execution logic.
  - Explorer also now provides a deeper shell continuity baseline through breadcrumb navigation: invoking the `tests` breadcrumb moves to the ancestor directory, invoking a visible child returns to the project subtree, invoking a second breadcrumb step moves up again, and repeated `后退` actions restore the original folder while the same window-scoped breadcrumb/tab surface becomes visible again.
  - `RealAppTestDriver.Launch` needed explicit `ms-settings:` protocol support through the system shell. That launch path should remain a first-class helper branch for future Settings tasks.
  - Settings should not be modeled only as a newly created top-level window. On this machine, opening `ms-settings:display` reused an existing `设置` window, so some validation flows need "find current matching window after launch" rather than strict new-window detection.
  - Settings toggle targets can be semantically ambiguous even when names match exactly. On this machine, `夜间模式` exposed more than one actionable control, so the current operator workflow sometimes needs candidate-level refinement based on role/action capability.
  - Real reversible Settings tasks are practical and should be preferred. The current Night Light flow proved that a system toggle can be safely validated when the test explicitly restores the original-like end state within the same flow.
  - Real Settings also exposes a safe persistent value-change surface through the `静音音量` button on the Sound page. On this machine, that control is a practical round-trip target because it changes a real persistent system value while still allowing explicit restoration inside the same validation flow.
  - Settings synchronization needed two concrete hardenings before deeper flows stabilized: first, success-path UIA property reads in `UiaActionExecutor` had to tolerate unsupported properties after `invoke`; second, Settings discovery had to become a true non-asserting poll with content-readiness checks because `ms-settings:display` can briefly expose no Settings window or only an almost-empty shell window before the real page tree loads.
  - Settings entry helpers should prefer content-readiness and active-window tracking over fixed sleeps. The stable Sound-page flow now depends on waiting for a meaningful Settings snapshot and tolerating role drift in the `声音` entry (`listitem`, `button`, or broader named element forms).
  - Real Settings now also validates a true `range_value` path on the `调整输出音量` slider, but stable regression still depends on treating empty-shell `设置` windows as not-ready and continuing readiness polling until the Sound-page content is actually present.
  - The product now also supports a true `select` action on UIA selection surfaces. On this machine, the first reversible real-app `select` baseline is currently the `缩放` combobox in Settings rather than a lower-risk standard dialog surface, because that Settings control already has stable expand/collapse coverage and exposes a usable selection path through the current accessibility-tree mainline.
- Current limitations:
  - This baseline now proves Explorer refresh/shortcut interaction, a real non-Settings `select` flow on the visible file `List "项目视图"`, and a reversible multi-step breadcrumb continuity flow (`tests` ancestor -> descendant re-entry -> second breadcrumb up -> repeated `后退` return) in the same Explorer window, plus Settings search-box text entry, Night Light round-trip toggling, a reversible `select` round trip on `combobox[name="缩放"]`, deeper `设置 -> 系统 -> 声音` navigation, a reversible `单声道音频` round trip, a safe persistent mute/unmute value-change round trip on the Sound page, and a real `range_value` round trip on `调整输出音量`.
  - Broader shell continuity is still only moderately sampled. Current coverage now proves one multi-step Explorer breadcrumb sequence, not arbitrary cross-window continuity or more dynamic shell surfaces.
  - Explorer and Settings titles and visible labels remain localized and path-dependent, so selector guidance will need flexible heuristics for reuse across machines.

### Complex Apps

- Status: validated expanded baseline
- Query path: `windows_list -> windows_snapshot -> windows_locate` succeeded on live complex-app candidates already open on this machine.
- Action path: `windows_act(action="focus")` now succeeds on a live Codex control selected directly from the current snapshot tree, and Codex `windows_act(action="invoke")` succeeds both on a current snapshot-stable toolbar action and on a richer live simulation action selected from the current visible state rather than from a remembered historical target. `windows_act(action="set_value")` plus a second `windows_act(action="invoke")` baseline now also succeed on live Qwen controls discovered through the same mainline substrate, and WeChat now also exposes low-side-effect real baselines for `set_value`, `select`, and `invoke` through the same product path.
- Optimizations needed:
  - Complex apps should be selected from controls that are stably visible in the *current* live tree, not from controls remembered from an earlier snapshot. Codex changed enough between runs that older targets such as `Go to next slide` disappeared from later snapshots, while stable toolbar actions remained available.
  - Snapshot/actionability inference needed broader COM-tolerant probing before Codex snapshots became stable again. The current substrate now tolerates COM failures while reading child collections, control roles, bounds, and selected state metadata, which was necessary before live Codex simulation could progress.
  - Top-level window discovery also needed a Win32-backed fallback before some desktop apps could even enter the product path. On this machine, `Qwen` was visible to the OS as a normal top-level window but did not reliably appear as a direct UIA desktop child, so `windows_list` and snapshot handle resolution had to move from UIA-only top-level discovery to Win32 top-level enumeration plus UIA `FromHandle(...)` attachment.
  - For highly dynamic apps such as Codex, snapshot-based targeting is more stable than a second locate pass when the visible surface can drift between readiness and action execution.
  - Codex also needed a weaker notion of post-action identity than WeChat. Reusing the exact pre-action ref or demanding a durable `focus_changed` observation was too strong for its dynamic tree; stable verification instead comes from re-reading the post-action snapshot, confirming the live surface still exists, and then describing a freshly resolved current ref from that surface.
  - Qwen currently exhibits strong state-dependent surface drift. In one state it exposes a usable business tree with composer input and `新建对话`; in another it collapses to a shell-like menu surface with only `Qwen` / `编辑` menu items and title-bar chrome. Additional Qwen baselines should only be added when the target is visible in the current snapshot.
  - WeChat now demonstrates a stronger verification shape than a pure action-smoke baseline: its current `set_value` flow is verified by waiting for the same chat-composer surface to remain present after execution, and its navigation `invoke` flow is verified both by post-action surface readiness and by `windows_describe_ref` on the still-visible target control.
- Current limitations:
  - `Clash Verge` is a current accessibility boundary on this machine. Its top-level snapshot exposed mostly shell/window chrome and no useful business controls through the current accessibility-tree mainline.
  - Codex now has a live-state-aware focus baseline plus two invoke baselines, Qwen now has composer-input `set_value` plus `新建对话` `invoke` baselines, and WeChat now has low-side-effect `set_value`, `select`, and navigation `invoke` baselines, but deeper coverage still depends on picking controls that remain visible across stronger application state transitions.
  - Qwen's currently visible anonymous mode combobox is not yet a stable real `expand/collapse` baseline on this machine: it advertises `expand/collapse`, but repeated `expand` attempts still observe `expand_state = Collapsed`.
  - `AK` now enters the product path through Win32-backed top-level discovery, but its current snapshot is effectively an empty shell surface with no actionable elements, so it remains an accessibility boundary for now rather than a usable non-mouse baseline.
  - `CC Switch` also enters the product path through the same top-level discovery path, but its current snapshot is likewise shell-heavy and only exposes window chrome plus system-menu surfaces, not useful business controls.
  - NetEase Cloud Music also enters the product path through the same discovery path, but its current snapshot only exposes `Window`/`Pane`/`Document`-level shell surfaces with no business controls, so it is also a current accessibility boundary rather than a usable baseline.
  - WeChat, by contrast, now appears as a positive counterexample for the same discovery path: its current snapshot exposes a rich business tree with navigation controls, searchable text inputs, selectable conversation surfaces, and chat composition surfaces. Future WeChat baselines should still avoid sensitive or high-side-effect actions unless the target is explicitly chosen for that purpose.

## Current Summary

- Confirmed: Allyflow can already operate a real system app directly through the current product path without any extra mock layer.
- Confirmed: the first real-app bottleneck was not action execution, but selector portability across real UIA role shapes.
- Confirmed: a second real-app class already exposes a different failure mode, where hosted-window discovery or localization can block progress before selector/action validation becomes straightforward.
- Confirmed: baseline real-app tests should be conservative about `AutoActivateWindow`; foreground activation is itself a product-risk dimension and should not be forced where unnecessary.
- Confirmed: `AutoActivateWindow` now belongs only on foreground-sensitive execution paths such as explicit focus, key sending, or input-simulation fallback, not as a blanket pre-activation step.
- Confirmed: standard file dialogs may appear as nested dialog windows inside a parent app snapshot rather than as separately discoverable top-level windows.
- Confirmed: real-app validation surfaced a concrete product defect in `press_keys` key mapping, and fixing it was necessary before file-flow coverage could progress.
- Confirmed: a real end-to-end save to a disposable path now works through the current product path on Notepad.
- Confirmed: the standard save dialog's `保存类型:` combobox is visible and expandable, but on this machine it is not yet a stable non-Settings real `select` baseline because the live accessibility surface does not reliably expose selectable option items after discovery.
- Confirmed: Explorer is a viable shell-surface baseline and can already validate both structured `invoke` and shortcut-driven `press_keys` flows.
- Confirmed: Explorer now also provides the second real `select` baseline outside Settings through the visible file `List "项目视图"`, proving that `select` is not anchored to a single app family.
- Confirmed: Explorer now also proves a deeper shell continuity path through multi-step breadcrumb navigation and repeated `后退` recovery within the same live window, without any Explorer-specific substrate logic.
- Confirmed: Settings is also a viable real baseline and already supports structured `set_value` on the built-in search box.
- Confirmed: Settings can now validate a real reversible `toggle` flow and a real `expand/collapse` combobox flow through the current product path.
- Confirmed: Settings can now validate a deeper real navigation transition (`设置 -> 系统 -> 声音`) and a second reversible system-setting round trip (`单声道音频`).
- Confirmed: Settings can now validate a real `range_value` round trip on the `调整输出音量` slider when the helper waits for Sound-page content rather than accepting an empty shell window as ready.
- Confirmed: Settings can now validate a real `select` flow on `combobox[name="缩放"]`, giving the substrate a second value-oriented primitive beyond `range_value` without adding app-specific execution logic.
- Confirmed: Standard file dialogs now include a real ambiguity-handling baseline plus navigation-tree focus inside the nested dialog surface.
- Confirmed: Standard file dialogs now also include a full non-visual ambiguity-recovery baseline: ambiguous locate -> candidate refs -> `windows_describe_ref` -> recovered exact-ref action.
- Confirmed: recovery diagnostics are now converging toward a stable machine-consumable taxonomy: `ambiguity_resolution`, `target_recovery`, `surface_recovery`, and `verification_recheck`.
- Confirmed: for real shortcut validation, an explicit `focus` step can be more reliable than forced window activation, but some shortcuts such as Notepad `Ctrl+Shift+S` still behave as genuinely foreground-sensitive paths and should keep `AutoActivateWindow` enabled.
- Confirmed: `Clash Verge` currently behaves as an accessibility boundary rather than a productive next target for the accessibility-tree-first path.
- Confirmed: Codex is a viable complex-app target when baselines are anchored to controls that are visible in the live tree at execution time rather than to remembered historical controls.
- Confirmed: Codex complex-app coverage now includes snapshot-aware focus, stable toolbar invoke, and a richer live simulation invoke chosen directly from the current snapshot tree.
- Confirmed: Codex verification depth is now closer to the current WeChat level: the focus baseline proves the named focusable surface still exists after execution, and both invoke baselines prove the post-action live surface remains present and yields a freshly describe-able current ref.
- Confirmed: broader COM-tolerant snapshot construction was a prerequisite for continuing complex-app validation on dynamic UIA trees such as Codex.
- Confirmed: some desktop apps such as Qwen can be invisible to UIA-only top-level window discovery while still being normal visible Win32 top-level windows; Win32-backed window enumeration plus UIA `FromHandle(...)` attachment fixes that class of discovery failure without introducing app-specific logic.
- Confirmed: Qwen is now a viable complex-app target through the same product path, with a real `set_value` baseline on the composer input `Edit "有什么我能帮您的吗？"` and a second `invoke` baseline on `新建对话` when that business surface is currently visible.
- Confirmed: Win32-backed top-level discovery also pulls `AK` into `windows_list` / `windows_snapshot`, but the current `AK` surface is still a near-empty boundary rather than a usable actionable tree.
- Confirmed: `CC Switch` is also now discoverable through the same mainline window path, but its currently exposed accessibility tree is limited to shell/window chrome and should be treated as a boundary for now.
- Confirmed: NetEase Cloud Music is likewise discoverable through the same mainline window path, but its currently exposed accessibility tree is limited to `Window`/`Pane`/`Document` shell surfaces and should also be treated as a boundary for now.
- Confirmed: WeChat is also discoverable through the same mainline window path, and unlike the current `AK`/`CC Switch`/NetEase Cloud Music surfaces, it already exposes a genuinely actionable business tree rather than only shell/window chrome.
- Confirmed: WeChat now has three low-side-effect real complex-app baselines through the same substrate: chat-composer `set_value`, conversation-surface `select`, and navigation-button `invoke`.
- Confirmed: WeChat verification depth is now stronger than simple action success alone: the composer-input `set_value` baseline proves the chat surface remains present after execution, and the navigation `invoke` baseline proves the post-action control remains describe-able through `windows_describe_ref`.
- Next recommended targets:
  1. Qwen or Codex: extend beyond the current focus/invoke/set_value baselines only when the next target is visible in the current snapshot tree, without introducing app-specific substrate logic.
  2. Shell-adjacent surfaces: expand continuity coverage beyond the current Explorer multi-step breadcrumb sample toward cross-window or more dynamic shell flows, and continue looking for additional low-risk `select` surfaces where the live tree already exposes stable selectable descendants.
