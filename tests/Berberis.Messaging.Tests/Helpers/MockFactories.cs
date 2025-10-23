using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Berberis.Messaging.Tests.Helpers;

public static class MockFactories
{
    /// <summary>
    /// Creates a mock ILoggerFactory that captures log messages
    /// </summary>
    public static ILoggerFactory CreateMockLoggerFactory(
        out List<string> capturedLogs)
    {
        var logs = new List<string>();
        capturedLogs = logs;

        var mockFactory = Substitute.For<ILoggerFactory>();
        var mockLogger = Substitute.For<ILogger>();

        mockLogger
            .When(x => x.Log(
                Arg.Any<LogLevel>(),
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>()))
            .Do(ci => logs.Add(ci.ArgAt<object>(2).ToString()!));

        mockFactory.CreateLogger(Arg.Any<string>()).Returns(mockLogger);

        return mockFactory;
    }
}
