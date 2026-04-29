# Real App Acceptance

This file defines the operating rules for large-scale real-app-driven product iteration.

## Main Loop

Every validation cycle should follow the same sequence:

1. Pick one app and one concrete operator task.
2. Execute the task through the current Allyflow product path.
3. Record the exact product chain and observed behavior.
4. Classify the first blocking issue.
5. Make the smallest useful product improvement.
6. Re-run the validated baseline apps.
7. Record remaining limitations and next candidates.

## Required Record Fields

Each real-app entry should capture:

- app name
- task name
- query chain used
- selector used
- action used
- verification used
- success summary
- failure summary
- optimization candidates
- current limitations
- issue taxonomy bucket

## Optimization Rules

- Prefer improving the main `accessibility_tree` path before introducing new fallback behavior.
- Prefer product-level improvements that help more than one app.
- Keep changes minimal and observable.
- If a failure is clearly caused by inaccessible app surfaces, record it as a boundary instead of forcing hidden fallback behavior into the mainline.

## Exit Criteria For Baseline Readiness

The baseline is considered ready for harder user-provided apps when:

1. At least two easy real apps have automated validation coverage.
2. At least one button-driven flow and one text-entry flow are validated.
3. The backlog and log clearly separate product problems from accessibility boundaries.
4. The validation helpers are reusable enough that new app tasks do not require custom harness code each time.
