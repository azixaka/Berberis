# CI/CD Setup

## Code Coverage

Code coverage is collected automatically during CI runs using `coverlet.collector`. Coverage reports are available as artifacts in the GitHub Actions workflow runs.

### Viewing Coverage Reports

1. Go to the [Actions tab](https://github.com/azixaka/Berberis/actions) in your repository
2. Click on a workflow run
3. Download the `coverage-reports` artifact
4. Open the `coverage.cobertura.xml` file with a coverage viewer or IDE integration

## Branch Protection (Optional)

To enforce quality gates, configure branch protection:

1. Go to: Repository Settings → Branches → Add rule
2. Branch name pattern: `master`
3. Enable:
   - ✅ Require a pull request before merging
   - ✅ Require status checks to pass before merging
     - Select: `build` (from CI workflow)
     - Select: `quality` (from Quality Gates workflow)
   - ✅ Require branches to be up to date before merging
4. Save changes

This ensures:
- All code goes through pull requests
- CI must pass before merge
- Quality checks must pass before merge

## Local CI Validation

Run the same checks locally before pushing:
```bash
# Full CI validation
dotnet restore Berberis.Messaging/Berberis.Messaging.csproj
dotnet restore tests/Berberis.Messaging.Tests/Berberis.Messaging.Tests.csproj
dotnet build Berberis.Messaging/Berberis.Messaging.csproj --configuration Release --warnaserror
dotnet build tests/Berberis.Messaging.Tests/Berberis.Messaging.Tests.csproj --configuration Release --warnaserror
dotnet test tests/Berberis.Messaging.Tests/Berberis.Messaging.Tests.csproj --configuration Release --collect:"XPlat Code Coverage"
```

If these pass locally, CI should pass on GitHub.
