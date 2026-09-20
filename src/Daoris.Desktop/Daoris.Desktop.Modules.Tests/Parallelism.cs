using Xunit;

// These modules resolve every path they touch from the ENVIRONMENT — `DAORIS_DRIVER_CONFIG`,
// `DAORIS_REMOTE_CONFIG`, `DAORIS_HARNESS_CONFIG` and the remote env pair — because that is how a
// person redirects them and how every gate in this repository stays hermetic. Environment variables
// are process-global, so two test classes running at once overwrite each other's redirection and the
// failures land in whichever class lost the race.
//
// Found exactly that way: adding a second test class turned two passing remotes tests red, in a run
// where nothing about remotes had changed. A suite whose result depends on scheduling is worse than a
// missing one, because it teaches people to re-run until it is green.
//
// Serialized rather than solved by injecting paths: the environment IS the contract here — it is what
// the family rehearsal and the desktop itself use — and a test that bypassed it would stop testing the
// thing that ships.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
