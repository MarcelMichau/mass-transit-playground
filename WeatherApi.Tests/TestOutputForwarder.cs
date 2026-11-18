using Xunit.Abstractions;

namespace WeatherApi.Tests;

/// <summary>
/// An <see cref="ITestOutputHelper"/> for hosts that outlive a single test. Each test points it at
/// its own output while it runs; lines written between tests, or after a test has finished, are dropped.
/// </summary>
public sealed class TestOutputForwarder : ITestOutputHelper
{
    private volatile ITestOutputHelper? _current;

    public ITestOutputHelper? Current
    {
        get => _current;
        set => _current = value;
    }

    public void WriteLine(string message) => Write(output => output.WriteLine(message));

    public void WriteLine(string format, params object[] args) => Write(output => output.WriteLine(format, args));

    private void Write(Action<ITestOutputHelper> write)
    {
        var output = _current;

        if (output is null)
            return;

        try
        {
            write(output);
        }
        catch (InvalidOperationException)
        {
            // The test finished while a background message was still being logged.
        }
    }
}
