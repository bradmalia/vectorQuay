#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="/home/brad/vectorquay"
cd "$REPO_ROOT"

exec dotnet run --project "$REPO_ROOT/src/VectorQuay.App/VectorQuay.App.csproj"
