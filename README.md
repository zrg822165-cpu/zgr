# OpenClaw Work Directory

个人的创新项目基地

This directory contains focused working artifacts for the OpenClaw desktop automation architecture.

## Contents

- `architecture/mvp-implementation-plan.md`: MVP scope and delivery plan
- `architecture/implementation-task-breakdown-phase-1.md`: implementation task breakdown and phase tracking
- `validation/real-app-acceptance.md`: real-app validation operating rules
- `validation/real-app-backlog.md`: next validation targets and product backlog
- `validation/real-app-validation-log.md`: validated findings and current product boundaries
- `src/`: implementation projects
- `tests/`: dedicated test directory for validating the architecture
- `tests/test-matrix-phase-1.md`: phase 1 test matrix

## Notes

- The current implementation layout is intentionally slimmed down to two source projects: `OpenClaw.Core` and `OpenClaw.Infrastructure.Windows`.
- Tool request/result types and tool-service orchestration now live inside `OpenClaw.Core`; they are no longer split into a separate `OpenClaw.Protocol` project.
- The remaining reference docs are intentionally current-state oriented. Older speculative draft splits were removed once the implementation and validation loop became the real source of truth.
- UI inspector surfaces are intentionally out of scope unless they are needed only for internal debugging.
- OpenClaw's current product flag is now explicit: it is an `accessibility_tree`-first desktop automation product, not a screenshot/OCR-first product.
- The current implementation has already crossed the "architecture only" stage and now contains a live `.NET 8` product slice with real WinForms/UIA-backed query, action, scenario, and failure-path coverage.
- The core product loop is in place: `windows_list -> windows_snapshot -> windows_locate -> ref -> windows_act -> verification -> diagnostics/recovery`.
- The structured query surface is available through `windows_list`, `windows_active`, `windows_snapshot`, `windows_describe_ref`, `windows_locate`, and `windows_refresh_focus`.
- The structured action surface is available through `windows_act`, with real UIA-backed support for `invoke`, `focus`, `set_value`, `toggle`, `expand`, `collapse`, `click`, and `press_keys`.
- The mainline contract remains text-structured and explainable: `tool_name`, `interaction_model=text_structured`, and `primary_interface=accessibility_tree` are carried through query and action results, including several failure paths.
- Real fixture-backed scenario coverage now proves three minimal product tasks end to end: `I-001 设置页填写并保存`, `I-002 登录表单输入与提交`, and `I-003 对话框歧义消解`.
- Real protocol and recovery checks also exist for `dry_run`, `TargetNotFound`, `VerificationFailed`, `FallbackBlocked`, stale-ref recovery, focus refresh continuity, and action timing monotonicity.
- `T5-007` has been intentionally kept lightweight: the project now uses repeated real WinForms/UIA runs to validate product stability instead of introducing a heavy benchmark harness too early.
- The next stage is no longer "expand interfaces" or "start vision fallback". The next stage is product usability validation on real desktop software, using OpenClaw itself as the operator.
- Screenshot, OCR, and vision fallback are intentionally not part of the current product line. They remain future options only if accessibility-tree automation proves insufficient on real software.
