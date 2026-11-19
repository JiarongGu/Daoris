namespace Daoris.Driver;

/// <summary>A wait of one part of an ask's plan on another's receipt: the plan's <c>after</c> edge naming the fact (<c>receipt</c>).</summary>
/// <param name="On">The part whose run's receipt it waits on.</param>
/// <param name="Fact">The fact that receipt names, reached.</param>
public sealed record PartWait(string On, string Fact);
