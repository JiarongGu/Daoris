---
name: session-record-fields
applies_when: adding a field to the session record that this machine answers and the sync wire never carries (the way `opinion`, `serves` and `from`, `fromWhy`, `fromBy` were added), or one that travels with the record to a teammate's mirror (the way `entry` and `tookQuest` were)
enforces: a new field goes into every place of its chain in the same change, the service's record and store, the ledger's opens, the host's request and response, the driver's write and read, the three stand-ins, the web's type; a machine-only field is kept off the wire by having no field there, and a test holds that
---

# The session record's field chain — every place a new field goes

**A session record is spelled in two runtimes that share no code (the service's `Session` and the driver's
`PriorSession`, `twins`), in a SQLite table, in a request and a response, in the sync wire and in the web's type. A field
answered on this machine only (D47 §4) goes into the places below, and nothing but this list ties them together. Put a new
field in every row of its kind in the same change.**

## Why

MIXAGENT1a-iii (`from`, `fromWhy`, `fromBy`; D163's note, service `5e44747b7`, driver `05d6529ad`) is the worked walk. A miss
fails in a different room from the one that missed it, or fails nothing:

- **The column is not ensured.** The INSERT names a column an existing database lacks, and every open on a machine that
  upgraded fails. A record from before the field must read as null, which is the old reading.
- **The reader is not told.** `Read` builds the record from the row; a field written and not read comes back null from
  every later `FindAsync`, and the test that wrote it through the store passes while the host answers nothing.
- **The loopback guard is forgotten.** `ToSession` answers a machine-only field `loopback ? s.X : null`. Without it a
  keyed remote or a teammate is told what is on this machine (D47 §4, D49 §4).
- **A stand-in does not echo it.** The driver's tests run against stand-in ledgers that build the record by hand. A field
  the stand-in drops reads back null, and the driver's test of the read passes on a record the real service would have
  filled, or fails with no hint that the stand-in is what is missing.
- **The driver writes it and never reads it back** (or the reverse): the open names `from`, `Prior` does not read it, and the
  next run is compared with a record that looks unlinked.

## How to apply

**Work test first.** Write the store's test (the field written at the open, read back, null for a record from before and
for a mirror), the ledger's, the host's (answered on loopback, absent off it) and the driver's (the write's body, the read),
watch them fail, then place the field one row at a time.

### The places for a field this machine answers only

Paths are under `src/`. A field set **at the open** (`from`, `serves`, `opinion`, `harnessVersion`, `profile`, `tree`) goes
in all rows. A field set **later** (`took`, `said`, `interrupted`, `limited`, `review`) skips rows 3 to 5 and 7 to 8 and has
its own update (`SessionStore.MarkTookAsync` and the `UPDATE` in the state move), with the row 2 reader and the rows 6, 9, 10.

| # | Place | Where | What goes in |
|---|---|---|---|
| 1 | The record's property | `Session`, `Daoris.Service/Daoris.Service.Core/Sessions/Sessions.cs`, a `{ get; init; }` beside `Opinion`, `Serves`, `From` with a summary naming the row and the decision point, and a `<remarks>` saying it is this machine's own as `Opinion` is | Positional only for the first dozen fields; a new one is an `init` property. A single field that other fields only mean with (a why with no session) says so and is kept together with it. |
| 2 | The column | `SessionStore.EnsureAsync`, the same file: `SchemaColumns.EnsureAsync(_db, "sessions", "<col>", "<col> TEXT NULL", ct)`, with a comment naming the row and "a record from before says none, which is the old reading" | Always nullable, never a default that changes an old record's meaning. A column named for an SQL word takes another (`from_session`, `limited`, `took_quest`); the property keeps the plain name. |
| 3 | The create | `SessionStore.CreateAsync`, same file: the parameter, the property set in the initialiser through `Blank(...)`, the column in the `INSERT` and in `VALUES`, the `AddWithValue` | A value that means nothing alone is dropped with the one it depends on (`continues is null ? null : Blank(fromWhy)`). The **mirror's** insert (`MirrorAsync`) names its columns apart and writes nothing for a machine-only field, and must stay so: do not add the column there. |
| 4 | The reader | `SessionStore.Read`, same file: `X = reader.IsDBNull(reader.GetOrdinal("<col>")) ? null : reader.GetString(...)` | Every query that reads a record is `SELECT *`, so no column list needs the new name; the one list that names columns is the old migration that rebuilt the table to let `quest` be null (near `SchemaColumns`' first block), which runs on tables from before and never gains a new column. |
| 5 | The ledger's opens | `SessionLedger.OpenAsync` (a driven session for a quest) and `OpenChatAsync` (a conversation), `Daoris.Service/Daoris.Service.Core/Sessions/SessionLedger.cs`: the parameter at the end of each, the `<param>` sentence, and the argument to `CreateAsync` | The ledger records what the driver tells it and judges none of it (D46 §7). Other opens write their own: the intake's, and `HelperDesk.OpenAsync` (`serves: asker.Id`), which is where a helper's record is made and not through either request. |
| 6 | The request records | `OpenSessionRequest` and `OpenChatRequest`, `Daoris.Service/Daoris.Service.Http/ApiContracts.cs`: an optional record member at the end, defaulted null, and the comment above them | Optional and last, so a driver from before still posts. A body from a driver from after the host is read the same way. |
| 7 | The routes | `app.MapPost("/api/sessions", ...)` and `("/api/sessions/chat", ...)`, `Daoris.Service/Daoris.Service.Http/Program.cs`: the new member passed to the ledger's open | One line each. The host judges none of it. |
| 8 | The response | `SessionResponse` in `ApiContracts.cs` (an optional member at the end, with a comment saying whose it is) and `ToSession` in `Program.cs` | **`X: loopback ? s.X : null`** for a machine-only field; a travelling one is answered to every caller (`Entry: s.Entry`, `Limit: s.Limit`). `loopback` is `MachineLocal(http)`, so a keyed remote is not loopback. |
| 9 | The driver's write | `ServiceClient.OpenSessionAsync` and `OpenChatAsync`, `Daoris.Desktop/Daoris.Desktop.Driver/Service/ServiceClient.cs`, through one private writer (`WriteFrom`) both call | Omitted when none, as every optional field of an open is. The value the driver knows is a record (`ContinuedFrom`, in `Conversations/Domain/HandOverRecords.cs`) so an open takes it as one argument, not three strings. The callers that know it (`Driver.ContinuesFrom` in `Loop/Driver.cs`, `HandOver.cs`'s start, `ChatRunner.StartAsync`) hand it down. |
| 10 | The driver's read | `ServiceClient.Prior(JsonElement)`, same file, and `PriorSession`, `Daoris.Desktop/Daoris.Desktop.Driver/Quests/Domain/Planner.cs`: the `init` property with its summary, and `X = Text(session, "<name>")` | `PriorSession` is the driver's whole picture of a record, read from `/api/sessions`; one without the member reads the field as unsaid whatever the service answered. Absent (an older host, a teammate's record) is null, and every reader of it handles null. |
| 11 | The stand-ins | `StandInLedger` (`Daoris.Desktop.Driver.Tests/StandInLedger.cs`, its `Continues(session, open)` helper copies the open's fields onto the record the way the service answers them on loopback), `ChatLedger` (`Conversations/ChatGoOnTests.cs`, its `Chat(...)` builder and its open at `StandInLedger.Continues(session, asked.AsObject())`) and `AskAndWaitTickTests.StandIn` (`Loop/AskAndWaitTickTests.cs`, its open at `StandInLedger.Continues(session, body.AsObject())`) | Three hand-built ledgers answer the record the driver reads back. A new field is added to each, through the one helper where the field is copied from an open (`Continues`), or in the builder where a test sets it. A fourth stand-in (a private `StandIn` in a test class) needs it only if its test reads the field. |
| 12 | The web's type | `Session` in `Daoris.Web/src/data/api.ts`: `x?: string \| null` with a comment saying whose it is | Optional and nullable: a host from before, a teammate's record and a browser have none. A page that uses it gets its words in both locales (`translation-parity`) and its test's `session({...})` builder already takes an override. |
| 13 | The records | The decision's dated note (D163's MIXAGENT1a-iii note is the worked example), `Daoris.Service/README.md`'s row for the route when the contract changes, `CHANGELOG.md` for what a person sees | Say what is this machine's own, who writes it, what a record from before reads, and what is left. |

### What does **not** go in for a machine-only field

**The sync wire has no row, and that is the guard.** `FedSessionRecord` (`Sessions/SessionFeed.cs`), `SessionWire`'s
`Feed`/`ReadFeed`/`Page`/`ReadPage` (`Sessions/SessionWire.cs`), `SessionSync` (`Remote/SessionSync.cs`) and the mirror's
insert in `SessionStore.MirrorAsync` carry the fields they spell and no others, so a field that is in none of them cannot
leave the machine, nor arrive on another: the profile name and the tree are kept off the same way, "a field that does not
exist cannot be filled in by accident" (the `FedSessionRecord` summary). Do not add the member to a wire record to make a
test pass. The text cleaners (`SessionNote.ForAnotherMachine`, `NoteParts.ForAnotherMachine`) clean **free text** on its way
out and are no guard for a field: a note that names the field's value is cleaned there; the field itself is kept off by
having no place on the wire.

Hold it with a test: a mirrored record reads null (`SessionStoreTests`, the `mirrored` case beside the `plain` and `unlinked`
ones), the host answers it on loopback and not off it (`Daoris.Service.Http.Tests/ContinuesHostTests.cs`), and a record
from before the column reads null (`SessionStoreTests`, the `elder` case).

### The places for a field that travels

Rows 1 to 4 and 12, 13 as above, then the wire, which a machine-only field leaves alone (`entry` SESSONE1a and `tookQuest`
CHATTAKE1 are the worked examples; both are keys that name no path and no account):

| # | Place | Where | What goes in |
|---|---|---|---|
| T1 | The fed record | `FedSessionRecord`, `Sessions/SessionFeed.cs`: an optional last member and its `<param>` sentence on why it may travel | Only a fact that names no path, no account and no word the person typed (D47 §4). |
| T2 | The wire, both ways | `SessionWire.cs`: `Feed` and `Page` pass it to `Write`; `ReadFeed` and `ReadPage` read it with `Text(item, "<name>")` | Written and read in the same change: a field only written is dropped at the door. A blank reads as none. |
| T3 | The push | `SessionSync`, `Remote/SessionSync.cs`: the `FedSessionRecord` constructed for each own record that changed | The value from the record, or the rule's (`SessionEntries.KeyAsync`) when the record is from before. Free text goes through `ForAnotherMachine`. |
| T4 | The mirror | `SessionFeed.FeedAsync` (the `Session` it builds) and `SessionStore.MirrorAsync` (the column in the `INSERT` and in `DO UPDATE`) | `COALESCE($x, x)` in the update, so a machine from before that sends none keeps what this one holds. |
| T5 | The response | `ToSession`: `X: s.X` with no guard | Answered to every caller. |

A travelling field is a **stored shape** that other machines read: add it, never change a value's meaning in place, and keep
a record from before read as it was (the brief's rule on stored shapes).

## Check before saying done

1. `dotnet test src/Daoris.Service/Daoris.Service.Tests`: rows 1 to 5 and T1 to T4 (`SessionStoreTests`, `SessionLedgerTests`,
   `SessionSyncTests`, `SessionEntryTests`).
2. `dotnet test src/Daoris.Service/Daoris.Service.Http.Tests`: rows 6 to 8 (`ContinuesHostTests`).
3. `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process`: rows 9 to 11 (`HandOverTests`,
   `ChatGoOnTests`, `AskAndWaitTickTests`, `CurveMoveTests`).
4. `npm --prefix src/Daoris.Web run test` when row 12 reads the field on a page, and `i18n:check` and `names:check -- --strict`
   when it shows a word.
5. `npm run -s verify`, with the index regenerated (`node tools/orient-index.mjs`).

A branch in one lane cannot run the others; the hand-back then names the rows left for the parent.

## Worked examples

- **MIXAGENT1a-iii** (`5e44747b7` service, `05d6529ad` driver; D163's note of 2026-10-13): `from`, `fromWhy`, `fromBy`,
  machine-only, set at the open, rows 1 to 13. Its paths in those commits are from before the folders (`Sessions.cs`,
  `ServiceClient.cs` at the project's top); the members kept their names.
- **MIXAGENT2a-i** (D163): `serves`, machine-only, set by the helper's own open (`HelperDesk.OpenAsync`) and not through the
  two requests, so rows 6 and 7 do not apply.
- **XAGENT1c** (D155): `opinion`, machine-only, set at the open; `said`'s `by` is a word's field, not a record's.
- **SESSONE1a** (D167) and **CHATTAKE1**: `entry` and `tookQuest`, the travelling fields.
- **HELPMODE1a2** (D183's note of 2026-10-19): `helpMode`, machine-only, on one kind of record. It is set at Ask Daoris's open
  (`OpenHelpAsync`, `OpenHelpRequest`, `POST /api/sessions/help`), and later through a driver's door of its own
  (`POST /api/sessions/{id}/help-mode`, `SessionStore.KeepHelpModeAsync`, no revision). That door is a new route, so it takes a row
  in `PersonDoors.Table` and in `PersonDoorTableTests` with its class's count. The reader answers a default for that kind alone
  (`restricted` for a help record from before, null for every other record), so a record from before reads as what it ran.
  The driver reads it twice, on `PriorSession` (`Prior`) and on `SessionRecord` (`SessionRecords.Parse`), for the history's rows.

## What this does not cover

A column on another table (`quests`, `asks`) has its own store and feed (`twins`). A word on a record (`SaidWord`'s
`By`, `Helper`, `From`, `Key`) is a member of the word's own records and the `said` JSON the store keeps, not a column. COWORK1b's
`From` and `Key` (D188's COWORK1b note) walked that chain: `SaidWord` and `SaidWord.Persons`, the store's `SaidWords` read and
`Write`, the say door's `SayRequest` and `SaidWordResponse` with `ToSaid`, then the driver's `ServiceClient.SayAsync` and
`ReadWord` into `SaidWordView`, and every reader of `Persons` on both sides. A record's *state*
or a new `SessionKind` changes the ledger's rules (D46) and is wider than a field.
