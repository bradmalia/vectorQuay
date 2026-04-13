# Phase 1 Ignored-File Enforcement Proof

Date: 2026-04-13
Author/Owner: Brad Malia
Prepared by: Codex
Scope: verify that app-managed local settings and secret files under the approved config home path do not change normal git status behavior for the repository.

## Command Pattern

```bash
before=$(mktemp)
after=$(mktemp)
tmpdir=$(mktemp -d)

git -C /home/brad/vectorquay status --short --untracked-files=all > "$before"

mkdir -p "$tmpdir/VectorQuay"
printf '{...valid settings json...}' > "$tmpdir/VectorQuay/settings.json"
printf 'VECTORQUAY_COINBASE_API_KEY=test\n' > "$tmpdir/VectorQuay/secrets.env"

XDG_CONFIG_HOME="$tmpdir" git -C /home/brad/vectorquay status --short --untracked-files=all > "$after"

cmp -s "$before" "$after" && echo yes || echo no
```

## Captured Result

Before:

```text
 M docs/roadmap-v4.html
 M src/VectorQuay.App/ViewModels/MainWindowViewModel.cs
 M src/VectorQuay.Core/Configuration/SecretFileParser.cs
 M src/VectorQuay.Core/Configuration/SettingsService.cs
 M tests/VectorQuay.Core.Tests/SettingsServiceTests.cs
?? docs/technical/gui-traceability-matrix.html
```

After:

```text
 M docs/roadmap-v4.html
 M src/VectorQuay.App/ViewModels/MainWindowViewModel.cs
 M src/VectorQuay.Core/Configuration/SecretFileParser.cs
 M src/VectorQuay.Core/Configuration/SettingsService.cs
 M tests/VectorQuay.Core.Tests/SettingsServiceTests.cs
?? docs/technical/gui-traceability-matrix.html
```

Comparison:

```text
UNCHANGED_STATUS=yes
```

## Conclusion

Creating representative app-managed files at the approved external config path did not change the repository status output. This is consistent with the documented design that `settings.json` and `secrets.env` live outside the repository and therefore do not become normal commit candidates.
