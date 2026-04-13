# VectorQuay

VectorQuay is an Ubuntu-first Avalonia/.NET 8 desktop shell for the approved Phase 1 baseline. The current implementation keeps trading inactive while establishing configuration, secret handling, operator-safe policy editing, and the reserved top-level views that later phases will extend.

## Current Baseline

- `src/VectorQuay.App`: Avalonia desktop shell
- `src/VectorQuay.Core`: settings, path resolution, and secret-status logic
- `tests/VectorQuay.Core.Tests`: unit tests for config contracts
- `config/templates/appsettings.template.json`: checked-in template only

## Local Settings and Secrets

- Non-secret settings path:
  `${XDG_CONFIG_HOME:-~/.config}/VectorQuay/settings.json`
- External secret file path:
  `${XDG_CONFIG_HOME:-~/.config}/VectorQuay/secrets.env`
- Supported secret names:
  `VECTORQUAY_COINBASE_API_KEY`
  `VECTORQUAY_COINBASE_API_SECRET`
  `VECTORQUAY_OPENAI_API_KEY`

Configuration precedence is:

1. Application defaults
2. Checked-in template
3. App-managed local non-secret settings
4. External secret file values
5. Environment variable secret overrides

## Build and Run

```bash
dotnet restore VectorQuay.sln
dotnet build VectorQuay.sln
dotnet test tests/VectorQuay.Core.Tests/VectorQuay.Core.Tests.csproj
dotnet run --project src/VectorQuay.App
```

The shell is expected to launch successfully even if no secrets are configured yet.

## Notes

- Trading is intentionally inactive in Phase 1.
- The About view performs a manual-only release check against the configured GitHub Releases feed URL. Leave it blank until a real VectorQuay release source exists.
- Secret values are not shown in the UI; only source/presence status is surfaced.
