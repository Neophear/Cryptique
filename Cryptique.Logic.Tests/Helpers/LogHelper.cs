using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Cryptique.Logic.Tests.Helpers;

public class LogHelper<T>(ITestOutputHelper testOutputHelper) : ILogger<T>
{
    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => throw new NotImplementedException();

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception, string> formatter) => testOutputHelper.WriteLine(formatter(state, exception));
}
