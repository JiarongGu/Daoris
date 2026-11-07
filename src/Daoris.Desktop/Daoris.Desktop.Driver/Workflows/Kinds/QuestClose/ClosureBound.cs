using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Whether a quest is bound to its closure graph at its take, and what is said (<see cref="ClosureBinding.AtTakeAsync"/>).</summary>
public sealed record ClosureBound(bool Bound, string Said);
