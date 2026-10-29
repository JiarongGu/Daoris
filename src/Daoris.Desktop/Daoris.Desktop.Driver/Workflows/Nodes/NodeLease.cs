using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The workflows' answer to the reading the instance's leases ask of two verbs' words (FOUNDATION1 mod-vi, the design §14.3), the words
/// of a run's node that the person says at its door. <c>workflow keep</c>, and the person's skip and stop of a node (FABRIC-CUTOVER1f), keep
/// a word on the run's record, which only the lease's holder writes; <c>work</c> holds the lease for <c>recovery</c>, which deletes refs
/// (RECOVERYDROP1), and for <c>held</c> with a resolution of a held attempt (<see cref="HeldResolutions"/>), while <c>work state</c> is a look.
/// Installed when the driver's assembly loads, before any host reads a lease.
/// </summary>
internal static class NodeLease
{
    [ModuleInitializer]
    internal static void Install()
    {
        VerbLeases.Answer("workflow", new(words => words is ["keep", ..] or [NodeWordActs.Skip, ..] or [NodeWordActs.Stop, ..]));
        VerbLeases.Answer("work", new(words =>
            words is ["recovery", ..] || (words is ["held", ..] && words.Skip(1).Any(word => HeldResolutions.All.Contains(word)))));
    }
}
