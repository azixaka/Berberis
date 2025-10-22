# Contributing to Berberis CrossBar

Thank you for your interest in contributing to Berberis CrossBar! This document provides guidelines for contributing to the project.

## Development Setup

### Prerequisites
- .NET 8.0 SDK or later
- Git

### Getting Started
1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR-USERNAME/Berberis.git`
3. Create a feature branch: `git checkout -b feature/your-feature-name`
4. Make your changes
5. Test your changes locally
6. Submit a pull request

## Building the Project

```bash
# Restore dependencies
dotnet restore Berberis.Messaging/Berberis.Messaging.csproj
dotnet restore tests/Berberis.Messaging.Tests/Berberis.Messaging.Tests.csproj

# Build (warnings as errors)
dotnet build Berberis.Messaging/Berberis.Messaging.csproj --warnaserror
dotnet build tests/Berberis.Messaging.Tests/Berberis.Messaging.Tests.csproj --warnaserror

# Run tests
dotnet test tests/Berberis.Messaging.Tests/Berberis.Messaging.Tests.csproj

# Run tests with coverage
dotnet test tests/Berberis.Messaging.Tests/Berberis.Messaging.Tests.csproj --collect:"XPlat Code Coverage"
```

## Coding Standards

### General Guidelines
- Write clear, readable code
- Follow existing code style and patterns
- Add XML documentation for public APIs
- Keep methods focused and concise
- Use meaningful variable and method names

### Code Quality Requirements
- Zero warnings - Build must pass with `--warnaserror`
- Test coverage - Maintain high code coverage
- All tests passing - No failing tests
- No compiler errors - Clean compilation

### Performance Considerations
This is a high-performance library. Please consider:
- Allocation-free hot paths where possible
- Use of `ValueTask` over `Task` when appropriate
- Lock-free concurrent operations where feasible
- Benchmarking performance-critical changes

## Testing

### Writing Tests
- Use xUnit for test framework
- Use FluentAssertions for assertions
- Follow Arrange-Act-Assert pattern
- Name tests: `MethodName_Scenario_ExpectedBehavior`
- Test both success and failure cases
- Add concurrency tests for thread-safe code

### Test Structure
```csharp
[Fact]
public async Task Publish_SingleSubscriber_ReceivesMessage()
{
    // Arrange
    var crossBar = new CrossBar(NullLoggerFactory.Instance);
    var received = false;

    crossBar.Subscribe<string>("test", msg =>
    {
        received = true;
        return ValueTask.CompletedTask;
    }, default);

    // Act
    await crossBar.Publish("test", "test message");
    await Task.Delay(50);

    // Assert
    received.Should().BeTrue();
}
```

## Pull Request Process

### Before Submitting
1. Ensure all tests pass locally: `dotnet test`
2. Verify build with warnings as errors: `dotnet build --warnaserror`
3. Run code coverage and check results
4. Update documentation if needed
5. Add tests for new functionality

### PR Guidelines
- Write a clear title and description
- Reference related issues (e.g., "Fixes #123")
- Keep PRs focused and reasonably sized
- Respond to review feedback promptly
- Ensure CI passes before requesting review

### PR Checklist
- [ ] Code builds without warnings
- [ ] All tests pass
- [ ] New tests added for new functionality
- [ ] Documentation updated if needed
- [ ] No unresolved TODO comments in critical code

## Continuous Integration

Our CI pipeline runs automatically on:
- All pushes to `master` branch
- All pull requests

### CI Checks
1. **Build** - Compiles with `--warnaserror`
2. **Tests** - Runs all unit tests
3. **Coverage** - Collects code coverage

### Local CI Validation
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

## Getting Help

- Documentation: See [README.md](README.md)
- Issues: https://github.com/azixaka/Berberis/issues
- Discussions: https://github.com/azixaka/Berberis/discussions

## Code of Conduct

- Be respectful and inclusive
- Focus on constructive feedback
- Help create a welcoming community
- Assume good intentions

## License

By contributing, you agree that your contributions will be licensed under the same license as the project.

---

Thank you for contributing to Berberis CrossBar!
