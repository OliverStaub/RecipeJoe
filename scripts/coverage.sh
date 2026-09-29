#!/usr/bin/env bash
# Backend unit + integration tests with coverage, merged by ReportGenerator. Run via `just cov-backend`.
# Extra args go to dotnet test. The 80 % line gate applies only when CI is set: `CI=1 just cov-backend`.
set -euo pipefail

cd "$(dirname "$0")/../backend"
rm -rf TestResults/coverage CoverageReport
for project in UnitTests IntegrationTests; do
    dotnet test --project "tests/RecipeJoe.$project" "$@" \
        --coverage --coverage-output-format cobertura \
        --coverage-settings CodeCoverage.config \
        --results-directory TestResults/coverage
done
dotnet reportgenerator \
    -reports:"TestResults/coverage/*.cobertura.xml" \
    -targetdir:CoverageReport \
    -reporttypes:"TextSummary;MarkdownSummaryGithub"
cat CoverageReport/Summary.txt
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then cat CoverageReport/SummaryGithub.md >> "$GITHUB_STEP_SUMMARY"; fi

# ReportGenerator's threshold option is PRO-only, so the gate is checked here.
if [ -n "${CI:-}" ]; then
    line=$(sed -n 's/^ *Line coverage: *\([0-9.]*\)%.*/\1/p' CoverageReport/Summary.txt)
    awk -v c="$line" 'BEGIN { exit !(c != "" && c >= 80) }' \
        || { echo "Line coverage ${line:-unknown}% is below the 80% gate" >&2; exit 1; }
fi
