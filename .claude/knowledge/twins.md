---
name: twins
applies_when: two artefacts must agree on a file, a layout or a rule, or you are changing one side of an existing twin
enforces: the artefacts share no code; the file is the contract; each side reads it with its own code and carries a test table the other matches; the twins change together
---

# Twins — how two artefacts agree without sharing code

**The CLI, the service and the driver share no code, by design. Where two of them must agree on a
fact, the FILE is the contract: each reads it with its own code, each carries a test table the other
must keep matching, and a change to one side is a change to both, in the same commit.**

## Why

The artefacts are different runtimes shipped to different places. The CLI is a zero-dependency Node
package, the service a .NET host that may run on a server, and the driver a .NET library the desktop
and the headless host carry. A shared library would make every one of them ship the others'
dependencies, and it would make the CLI's zero-dependency guarantee a claim about code it does not
own. So a machine-local file is read twice, or three times, on purpose.

The risk of that is drift, and it is the only risk. A rule enforced in one reader and not the other
is a rule the other will contradict, in front of a person, on a machine where both run. The remotes
map found this first (WSP3), and every twin since was built the same way for the same reason.

## How to apply

- **Name the file and its rules once, in the design or decision that introduces them.** Each reader's
  header names its twins by file.
- **Each twin carries a test table of the rules**, written so the other side's table can be compared
  line by line: the same cases, the same answers, including what absence means. A rule one table
  has and the other lacks is the drift, found before a person meets it.
- **Change both sides in the same commit**, tables included. A commit that changes one reader of a
  twinned file is incomplete, whatever its tests say.
- **Absence is a rule, not a default.** "No profile means the harness's own home", "no home means
  refuse", "half a remote pair is no remote": the answers for a missing file or field are where the
  twins drifted most.
- **An editor preserves what it has no field for.** A twin that rewrote the file from its own idea of
  the shape would delete what the other side wrote.
- **A constant both sides need is duplicated deliberately**, with a comment naming the other copy,
  never imported across.

## The twins today

| The file | The CLI | The driver | The service |
|---|---|---|---|
| The home (`DAORIS_HOME`, D63) | `home.ts` | `DaorisHome.cs` | `DaorisHome.cs` |
| The driver's choices (D46 §6), with each repository's line and its branch-name rule (WSR2), and how its work lands with the pattern rule (WSR1) | `driverconfig.ts` | `DriverConfig.cs`, `CanonicalLine.cs`, `Landing.cs` | — |
| The remotes map (WSP3) | `remotemap.ts` | `RemoteTarget.cs` | `RemoteConfig.cs` |
| Harnesses, pins and accounts (D57, D67) | `toolchain.ts` | `Harnesses.cs` | — |
| A maker's release channel (AGT2b) | `channels.ts` | `ReleaseChannel.cs` | — |
| Permission rules (D72) | `permissions.ts` | `Permissions.cs` | — |
| Rule proposals (D74) | `ruleproposals.ts` | `RuleProposals.cs` | `RuleProposals.cs` |
| A folder's trust (D73) | `trust.ts` | `ClaudeTrust.cs` | — |
| Plugins (D64) | `plugins.ts` | `Plugins.cs` | — |
| The in-app browser's favorites and settings (BRW5, CHR7) | `browser.ts` | `BrowserFavorites.cs`, `BrowserSettings.cs` (the desktop modules, read by `daoris-browser` at each start) | — |
| The code map (MAP3) | — | `CodeMapFile.cs` | `CodeMap.cs` (the devkit produces it) |
| The desktop page's origin on the engine it ships, `https://daoris.localhost` (D92) | — | the shell's `DesktopPage.VirtualHost` (CHR2b) | `DesktopPage.cs` (a local host allows it); the family rehearsal spells it a third time and asks with it |
| What a repository says it uses, `domain.uses` (D91) | `connect.ts` (`usesOf`) | `RemoteSyncPayloads.cs` carries it; `RegistryModule.cs` preserves it | `Registry.cs` (`Declared.Uses`), `RegistryImport.cs` |

A twin not in this table is still a twin: the rule is the arrangement, and the table is where to
look first.
