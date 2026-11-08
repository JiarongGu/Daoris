# Local release (`local-release`)

A Daoris plugin that **speaks**: a process of its own, which the Daoris driver starts and asks at
`work/perform` (and `work/watch`), over JSON-RPC 2.0 on its stdin and stdout. It was made by Daoris's
plugin kit (`daoris plugin new local-release --point work/perform`); what it does is what
`plugin.mjs` does. Its wire, the rules a plugin is held to, the tools it runs and the stand-in world are
those of [local-preview](../local-preview/README.md), which this folder is built the same way as.

> An example of recording and copying a release as a plugin's performed steps: it finds a pending release for the
> attempt's revision, records the release, and copies the built package into a registry folder, all inside
> its own data folder. It never pushes.

## The tail

A workflow that publishes ends in these steps, after the work has landed. The shipped *Release workflow* preset (`release`) is that graph, with a
`check` before the review and a merge into the line; with no release pending it ends at the line:

```
land -> line -> find -> release -> publish
```

Each step is `performed`, `idempotent` and reconciled (`work/reconcile`), with the transient codes
`unavailable`, `rate-limited` and `timeout`, a retry of three attempts, and evidence
`local-release/receipt@1:<invocation>`.

| Step | Takes | Does | Outputs |
|---|---|---|---|
| `find` | nothing | reads `pending/<revision>.json` (`{ "version", "notes" }`) for the attempt's revision and records it as found | `release: local-release/release@1:<version>` on `release`; none on `none` |
| `release` | `release` | records the release, its tag name `v<version>` and its notes, in `releases/<version>.json` | the same `release` reference |
| `publish` | `release` | copies the built package `incoming/<version>/` into `registry/<version>/`, whole or not at all | none |

Each step declares a `title` and an `about`, each `{ "en", "zh" }`, which the step editor shows where it
lists the plugin's steps: *Find a pending release*, *Record the release* and *Copy to its registry*. Their
words say what the step does here, a copy into the plugin's own registry folder, and never that it sends a
release to an outside registry. The step id `publish` is a name in the manifest, not a promise.

`find` is a decision (`outcomes`: `release` | `none`): it reaches with `release` and the output where a
release is pending, and with `none` and no output where not. Its `outputsOn` (`{ "release": ["release"] }`)
says so: Daoris requires the `release` output on `release` alone, and a graph is refused where a step that
takes it can start after `none`. What a graph does on `none` is its own (a `when` wire on each route). `release` fails `unknown-release` for a version `find` never recorded;
`publish` fails `not-released` for one not recorded as released and `no-package` for one with no built
package.

## What a downstream node may rely on

- An output is a **reference**, `<schema>:<id>`, never a payload: `local-release/release@1:<version>`,
  where the id is the version. The release's notes and tag are in the plugin's data folder, not on the wire.
- A reached `release` or `publish` means its effect is in the data folder; a reached `find` means the
  version is recorded as found. Each is the same however often Daoris sends the attempt: one effect per
  `idempotencyKey`, and a lost reply is answered by `work/reconcile` from the journal, or from the
  platform for an attempt an earlier process began.
- Each attempt makes one **temporary** resource, `local-release/resource@1` `<invocation>` (its staging
  folder), which Daoris removes through `work/cleanup`, once; asked again it is `already-gone`. Cleanup
  leaves what was recorded and published.
- It touches nothing outside `DAORIS_PLUGIN_DATA`, runs no tool, and never pushes: "publish" here is
  a copy into its own registry folder, and a tag is a name in a file.

## The data folder

| Path | What it holds |
|---|---|
| `pending/<revision>.json` | a release waiting at a revision; put there by whoever prepares it |
| `incoming/<version>/` | the built package; put there by the build |
| `found/<version>.json`, `releases/<version>.json` | what `find` and `release` recorded |
| `registry/<version>/` | the published copy |
| `staging/<invocation>/` | one step's work in progress, its last file `done`; the temporary resource |
| `work-journal.json` | the kit's journal of attempts, outcomes and cleanups |

## The folder

| File | What it is |
|---|---|
| `plugin.json` | the manifest: three performed steps, the resource, the points |
| `plugin.mjs` | the wire script and the three steps' effects |
| `work.mjs` | the kit's half of working: the journal and the work points; written by the kit, not by hand |
| `plugin.test.mjs`, `plugin.contract.test.mjs` | the kit's wire and contract tests, in the stand-in world |
| `release.test.mjs` | the tail on its real folders, with no world |
| `cycle.json` | the cycle `daoris plugin try --cycle` drives: the `publish` step, given the release reference it takes. Its `tail` field names the order above and nothing reads it |

## Testing it

```sh
node --test                                        # in this folder, nothing of Daoris's installed
daoris plugin try <this folder> --cycle cycle.json
```

## Installing it

Once it is reviewed and landed: `daoris plugin add <this folder>`, it is on once added (`daoris plugin disable local-release`
or the Plugins view turns it off), and a workflow names its steps, as the *Release workflow* preset does.
