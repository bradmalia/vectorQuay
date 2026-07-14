# VectorQuay Refactoring - Implementation Plan

## Objective
Address all 6 critical issues identified during code review through phased autonomous subagent execution.

## Issues Identified

### Issue 1 (CRITICAL): Raw `HttpClient` in `CoinbaseShellDataService`
**File:** `src/VectorQuay.Core/Coinbase/CoinbaseShellDataService.cs:23`
**Severity:** High — raw HttpClient bypasses connection pooling, DNS cache, and lifetime management. Singleton registration compounds the issue.
**Fix:** Change constructor to accept `IHttpClientFactory`. Create per-request clients via `_httpClientFactory.CreateClient()` inside using blocks.

### Issue 2 (CRITICAL): Static `AssetMetadataCatalog` with raw HttpClient  
**File:** `src/VectorQuay.App/Models/AssetMetadataCatalog.cs:139-140`
**Severity:** High — static class cannot be injected, its internal HttpClient leaks, and icon download is not testable.
**Fix:** Convert to injectable service `IAssetMetadataService`. Accept `IHttpClientFactory` via constructor for icon downloads.

### Issue 3 (CRITICAL): DI wiring incomplete in `App.axaml.cs`
**File:** `src/VectorQuay.App/App.axaml.cs:39-44`
**Severity:** High — `AddHttpClient()` is called but `CoinbaseShellDataService` constructor still receives raw `HttpClient`, and `AssetMetadataCatalog` registration doesn't provide its factory dependency.
**Fix:** Wire everything through DI. Provide factory delegate for `ICoinbaseReadOnlyService`. Register `IAssetMetadataService`.

### Issue 4 (HIGH): Two `new HttpClient()` in `MainWindowViewModel`
**Files:** `src/VectorQuay.App/ViewModels/MainWindowViewModel.cs:963, 2137`
**Severity:** High — OpenAI connectivity test and update check both create disposable clients inline. These should use injected factory.
**Fix:** Add `IHttpClientFactory` constructor parameter to `MainWindowViewModel`. Replace both raw client instances with factory-created clients.

### Issue 5 (HIGH): Silent failure in `TryList*` methods hides errors
**File:** `src/VectorQuay.Core/Coinbase/CoinbaseShellDataService.cs:100-135`
**Severity:** High — exceptions in fills/wallet/accounts fetch are swallowed, returning empty lists. Users see no error feedback for Coinbase data failures.
**Fix:** Change `TryList*` methods to return `ServiceResult<T>`. Update `RefreshAsync` to check `.IsSuccess`, collect errors into snapshot messages.

### Issue 6 (MEDIUM): `LoadSnapshot()` fails hard on schema mismatch
**File:** `src/VectorQuay.Core/Persistence/LocalStateStore.cs:158-162`
**Severity:** Medium — version mismatch causes immediate rejection without attempting to deserialize the existing data. Users lose their persisted state during upgrades.
**Fix:** On mismatch, log warning (via Trace or callback), then attempt deserialization with `PropertyNameCaseInsensitive = true`. Use explicit fallback mapping for known schema drift. Only return `CorruptedState` if both version check and deserialization fail.

## Phase Breakdown

### Phase 1: Networking & DI (CRITICAL) — Issues 1-4
**Tasks:**
1a. Refactor `CoinbaseShellDataService` constructor to use `IHttpClientFactory`
1b. Extract `IAssetMetadataService` interface from `AssetMetadataCatalog`  
1c. Update `App.axaml.cs` DI wiring for all services
1d. Add `IHttpClientFactory` to `MainWindowViewModel`, replace raw HttpClient usages

### Phase 2: Error Handling & Persistence (HIGH/MEDIUM) — Issues 5-6
**Tasks:**
2a. Change `TryList*` methods to return `ServiceResult<T>`, update `RefreshAsync`
2b. Update `LocalStateStore.LoadSnapshot()` for graceful fallback on schema mismatch

### Phase 3: ViewModel Decomposition (Deferred)
Defer until Phases 1-2 are validated and working.

## Validation Checklist (per phase)
**Phase 1:**
- [ ] Zero remaining `new HttpClient()` calls in production code
- [ ] DI resolves all services without manual `new` instantiations in App.axaml.cs
- [ ] `CoinbaseShellDataService` accepts `IHttpClientFactory`
- [ ] `AssetMetadataCatalog` implements `IAssetMetadataService` and accepts `IHttpClientFactory`

**Phase 2:**
- [ ] `TryList*` methods return `ServiceResult<T>` with error messages in snapshot
- [ ] `LoadSnapshot()` falls back gracefully on schema mismatch

## Constraints
- No `dotnet restore` (network blocked, NU1301)
- Assume `Avalonia`, `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection` are resolved
- Preserve existing records (`CoinbaseShellSnapshot`, `PersistedHoldingSnapshot`, etc.)
- Preserve Avalonia bindings; only modify DataContext/VM wiring

## Completed Work (Pre-subagent)
- Full source code review of all 6 affected files
- All issues documented with severity, location, impact, and recommended correction
