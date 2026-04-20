# Phase 2 Manual UI Checklist

Date: 2026-04-19
Author/Owner: Brad Malia
Prepared by: Codex
Scope: manual checklist artifact for the current Phase 2 read-only operator shell.

## Screen Checklist

- `PASS` Overview: real Coinbase connection state, live summary cards, donut allocation, recent live activity, and approved trade assets are present.
- `PASS` Portfolio: live holdings, allocation, largest exposure, and current-state risk summary are present without implying historical accounting.
- `PASS` Activity: live Coinbase account-event ledger is present with filters, empty-state handling, and a bounded scrollable history view.
- `PASS` Assets: live Coinbase product universe renders with local overlays, a bounded scroll area, and a display count summary.
- `PASS` Policies: approved assets render in a scrollable list with multi-select bulk apply and launch-baseline undo behavior.
- `PASS` Risk & Thresholds: profile selection, custom-edit boundary, and advanced-threshold intent are present and readable.
- `PASS` Sources: source/watcher registry is scrollable and supports modal add flows plus local apply/remove actions.
- `PASS` Alerts: structured alert categories, all-alerts filtering, and local preference controls render without implying autonomous trading alerts.
- `PASS` Configuration: Coinbase and OpenAI connection setup is simplified around credentials, external key storage, connectivity tests, and refresh actions.
- `PASS` About: informational/support/update surface still exists and does not interfere with core operator workflows.

## Notes

- This artifact records a manual checklist/status review rather than a screenshot bundle.
- The latest pass focused on moving the Phase 2 shell away from obvious placeholder behavior and toward a truthful read-only operator console.
- The most mature read-only surfaces are now `Overview`, `Assets`, `Portfolio`, `Activity`, and `Configuration`.
- `Sources` remains the main page that could still benefit from one more optional visual/interaction polish pass, but it is no longer a functional blocker for Phase 2.
