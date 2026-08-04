# Canon changelog

**Why each version of the doctrine changed.** `daoris status` prints the entries between a repository's
locked version and the version shipping in the package, so a consumer sees not only *which* documents
moved but whether the change matters to them.

Write one entry per version, newest first, under a `## <version>` heading. Say what changed and what an
adopting repository should do about it — a line that only repeats the filename adds nothing that the
`changed` list did not already say. This file ships inside the package, so nothing here touches the
network.

## 0.0.1

- The first canon: six workflow rules, and the discovery, logging, and output skills that go with them.
  Nothing to upgrade from yet.
