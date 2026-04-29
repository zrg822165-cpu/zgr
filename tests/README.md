# A11yFlow Test Directory

This directory contains the automated tests for the current A11yFlow implementation.

## Current contents

- `A11yFlow.Tests.Unit/`: unit coverage for selector parsing, locating, action orchestration, diagnostics, and recovery behavior
- `A11yFlow.Tests.Integration/`: real WinForms/UIA-backed integration coverage for query, action, scenario, cache/refresh, and tool-contract failure behavior
- `test-matrix-phase-1.md`: the phase 1 and phase 5-oriented acceptance matrix used to drive implementation slices

## Current status

- MVP plan: `../architecture/mvp-implementation-plan.md`
- Implementation breakdown: `../architecture/implementation-task-breakdown-phase-1.md`
- Real-app acceptance rules: `../validation/real-app-acceptance.md`
- Phase 1 test matrix: `./test-matrix-phase-1.md`

- The integration suite is intentionally serialized for real desktop/UIA runs.
- The current fixture-backed tests validate the text-structured product path: snapshots, `ref`, selectors, structured actions, explainable diagnostics, and recoverable failure surfaces. The shared WinForms fixture host now lives directly inside `A11yFlow.Tests.Integration/`.
- Screenshot/OCR/coordinate-driven automation remains out of the current mainline test contract and is intentionally reserved for a future product line only if accessibility-tree automation proves insufficient on real software.
