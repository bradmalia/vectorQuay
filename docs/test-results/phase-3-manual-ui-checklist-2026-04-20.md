# Phase 3 Manual UI Checklist

Date: 2026-04-20  
Author/Owner: Brad Malia  
Prepared by: Codex  
Scope: manual checklist artifact for the current Phase 3 durable-state shell.

## Screen / Behavior Checklist

- `PASS` Startup Recovery: app can start from a retained local cache and present restored state before live refresh completes.
- `PASS` Footer Status: restored-cache wording is replaced by live Coinbase refresh wording after successful startup refresh.
- `PASS` Overview: restored and live portfolio summaries render without crashing when durable state exists.
- `PASS` Portfolio: restored holdings/allocation can render from cache and then transition to live data.
- `PASS` Activity: retained Coinbase activity survives restart and remains visible before/after refresh attempts.
- `PASS` Alerts: retained local alerts survive restart; test alerts remain visible across relaunch.
- `PASS` Performance: persisted portfolio snapshot history contributes to the current Phase 3 read-only performance summary.
- `PASS` Policies: apply/undo still work and now produce durable audit events without breaking screen behavior.
- `PASS` Sources: add/update/remove flows still work and now produce durable audit events without breaking screen behavior.
- `PASS` Configuration: Coinbase/OpenAI save and connectivity-test flows remain functional after persistence introduction.

## Notes

- This artifact records checklist-level manual validation rather than a screenshot bundle.
- Operator-provided manual verification confirmed the current Phase 3 runtime behavior appeared to work as expected before the final critical review pass.
- The final review pass added:
  - schema-version fallback
  - malformed-line tolerance
  - footer/live-refresh status correction
