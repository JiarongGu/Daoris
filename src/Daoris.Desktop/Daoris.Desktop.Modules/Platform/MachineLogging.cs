using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Daoris.Desktop;

/// <summary>
/// The container's loggers, writing to the machine log (LOG1a, D94; ONEINSTANCE2, D171's note): what the kit, the engine and the
/// modules ask for as an <see cref="ILogger{TCategoryName}"/> or an <see cref="ILoggerFactory"/>, made of
/// <see cref="MachineLogProvider"/>.
/// </summary>
/// <remarks>
/// <para><b>Why a factory of its own.</b> LOG1a registered the provider and nothing else, and a provider is only what a factory
/// makes loggers of: the kit calls no <c>AddLogging</c> (its loggers are optional "so composition works without it"), and asks
/// the container for <c>ILogger&lt;T&gt;</c>, which nothing answered. So every warning the kit wrote, the single-instance
/// guard's among them, went nowhere, and a later launch that opened no window left no line saying why (ONEINSTANCE2). The
/// application references the logging abstractions alone, whose <see cref="Logger{T}"/> takes any factory, so this is the
/// factory: one provider, no filters but its own, no scopes.</para>
/// </remarks>
public static class MachineLogging
{
    /// <summary>
    /// Answer <see cref="ILoggerFactory"/> and every <see cref="ILogger{TCategoryName}"/> with loggers that write warnings and
    /// errors to <paramref name="log"/>, and information too from the categories <paramref name="informationFrom"/> names.
    /// </summary>
    public static IServiceCollection AddMachineLog(this IServiceCollection services, MachineLog log, params string[] informationFrom)
    {
        var provider = new MachineLogProvider(log, informationFrom);
        services.AddSingleton<ILoggerProvider>(provider);
        services.AddSingleton<ILoggerFactory>(new Factory(provider));
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        return services;
    }

    private sealed class Factory(MachineLogProvider provider) : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => provider.CreateLogger(categoryName);

        public void AddProvider(ILoggerProvider provider) =>
            throw new NotSupportedException("The machine log's factory has one provider, the machine log's.");

        public void Dispose()
        {
            // The log belongs to whoever opened it.
        }
    }
}
