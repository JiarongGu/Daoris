namespace Daoris.Driver;

/// <summary>
/// A provider's cheap authenticated read of an API key, at no model spend: <see cref="LoginState.In"/> where it accepts the key,
/// <see cref="LoginState.Out"/> where it refuses it, <see cref="LoginState.Unknown"/> where it could not be asked. Never a refusal
/// made of a network failure.
/// </summary>
public interface IKeyCheck
{
    Task<LoginState> CheckAsync(string key, CancellationToken ct);
}
