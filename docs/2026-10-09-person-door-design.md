# A door only the person may use

> PERSONDOOR1, decided as D156 (2026-10-09). Nothing is built. `Dnn` is `docs/decisions/Dnn.md`. A second agent's
> (Codex's) read-only review of D154's record claimed seven defects. REVIEWENV1b3 reproduced and fixed six, and left the
> seventh to this design: the local host trusts the loopback, so any process on the machine, a driven session's shell
> among them, can call the doors that are the person's alone, and the record cannot tell an agent from the person.

## 0. The case

**The local host checks nothing on a write.** `Program.cs:309` says so on purpose: local mode trusts the loopback
outright, the OS account being the boundary (D21), and shared mode gates every route with minted keys (D47 §7, as
amended). That was written when the callers on the loopback were the person's own: their window, their terminal and
their driver. Since D46 they include every session Daoris starts, and every session runs as the same account.

**Every session is told where the host is.** `Spawning.Shell` (`Adapters.cs:1149`) puts `DAORIS_SERVICE_URL` into the
environment of every session, conversation, intake and helper, beside the whole environment of the process that started
it: no start removes a variable today. The family rehearsal's stub sessions use it as an agent's door, as D46 intends.
They take and close their quest at `/respond`, an intake publishes onto its ask at `/api/asks/{id}/publish` naming its
session, and a conversation publishes at `POST /api/quests`.

**So one command is the person.** From a session's shell:

```
curl -X POST "$DAORIS_SERVICE_URL/api/quests/12/review" -H "content-type: application/json" -d "{\"verdict\":\"skipped\"}"
```

keeps a skip of the review as the person's, and the gate lets the work land (D154 point 7). The same reach answers a
go-ahead (D135 §2), gives the yes to a departure (`/accept`, D133 §4), marks a quest done as the person (`/done`),
records the person's own set-up at any commit (`/set-up` with `look`), publishes a set-up step (`/set-up-step`), sets a
chain's or an ask's review to `off` (`POST /api/quests` and `POST /api/asks` with `review`, `/api/asks/{id}/review`, an
ask's publish naming no session), keeps words on an ask as the person's (`POST /api/asks`, `/added`, `/say`,
`/answer`), and deletes, clears and re-wires. It also posts what only the driver reads: a done's evidence verdict
(`/evidence`, which D144 says the judged session never writes), a set-up with a commit the driver never read, and a
session's own state (D46: observed, never self-reported).

**A web page reaches some of it too.** A page open in any browser on the machine may send a POST with no custom header
and no body to the loopback; CORS stops it reading the answer, not sending the request. `/accept` binds no body, so it
answers such a request. Whether a browser asks the person first is the browser's, and nothing here relies on it. This
was read from the route, not tried.

**Who calls the host today**, read at `4004d041`:

| Caller | How | As whom |
|---|---|---|
| The page in Daoris's window | `fetch` from the engine's app origin to the loopback address the shell writes into its URL (`api.ts`, `host.ts`, D92) | the person's presses |
| The shell's modules and its driver loop | one `ServiceClient` (`DriverLoop.cs:403`) | the person's presses a module carries (Ask Daoris's apply, an abandon, a chat), and the driver's own facts |
| `daoris-driver` | `ServiceClient.FromEnvironment()`: `DAORIS_SERVICE_URL`, `DAORIS_SERVICE_KEY` | the person at a terminal, and a headless driver |
| `daoris connect`, `retire`, `import` | `service.ts` | the person at a terminal (D50) |
| Rehearsals and `test:web` | their own requests | the person, the driver and stub sessions, as each phase plays them |
| Any session | `DAORIS_SERVICE_URL`, and any command it can run | the agent's doors, and every other door |

The connector is not in the table. It opens the store itself (D36), which §7 comes back to.

## 1. What this defends, and what it does not

**The threat is an agent acting within its ordinary reach**: the commands and scripts it runs as the person's account,
with what its environment, its tree, the repository, its tools and the home's files hand it, against the host's
documented doors. That is the reach the review found, the reach a session is told about in its own environment, and
the reach a stub uses on purpose. Raising it from one command to a deliberate attack on the account is what this design
buys.

**It does not defend against the account's own reach.** A process of the person's account may read another process's
memory, inject code into the window or the host, drive the window by UI automation, attach to a console, or open the
store's file and write it (§7). Windows and POSIX both let an account do all of that to itself: that is what an account
is. Only a boundary between the person and their agents stops it (another account, an AppContainer, a lower integrity
level, a container or a machine of its own). Nothing here builds one (§7, §8).

**A harness's permission rules are not this boundary.** A Claude Code session Daoris starts runs only the commands a
rule allows (D72): by default `cd` and `git add|commit|mv`, and the connector's tools; in a folder the person trusted,
the repository's own allow-list as well. That keeps `curl` out of a default session. But a session that can edit a file
which a command it may run will execute (a test, a build script, a commit hook) runs whatever it wrote. The repositories
Daoris drives allow such commands routinely, and Codex and dsh are handed no rules at all (permission-scopes design §4).
The rules narrow ordinary reach. They never tell the person from an agent.

## 2. The person key

### 2.1 What it is

A random 256-bit value, written in base64url, **minted for each start of the host** by whoever starts it, and held in
memory by that starter and by the host. It is presented in a header, `Daoris-Person: <key>`, compared in constant time,
and Daoris never writes it: not to a file, an environment, a command line, a URL, the page's storage, a cookie or the
machine log. Only a person with no window, or a gate playing one, puts it in a terminal's environment (§4.3).

- **The starter hands it on the host's standard input.** A host told `DAORIS_PERSON_KEY_ON_INPUT=1` reads its key as
  the first line of its input, then watches the rest of the input for its end, as LOG2a does. Standard input is the one
  channel only the starter holds. The shell already starts the host with it redirected, and closes it to stop the host,
  because *a route to stop the host would be one any caller on the machine could press* (`HostSupervisor.Stop`). The
  environment was weighed and lost: on Linux any process of the account reads `/proc/<pid>/environ`, and a starter that
  set the variable on itself would hand it to every child.
- **A host handed none mints its own and prints it once**, on the first line of its standard output, saying that it
  lasts until this start stops, and enforces it (built last, §10). Whoever saw that output holds it: the person in the
  terminal that started it, or a service manager's log, which on Linux the account reads.
- **Per start, not per boot.** A key that a process taking the port between two starts managed to learn is worth
  nothing to the next start.

### 2.2 Who holds it

- **The window.** The shell mints the key, starts the host, hands the key over and keeps it for that host's life. Its
  modules present it on every gated write they make, the driver loop's facts and the person's presses alike. **The
  page** asks the shell for it over the bridge as it loads, keeps it in memory, and sends it on every write (`post` in
  `api.ts`); a page that reloads asks again. A script running in the page already holds the bridge, through which the
  window lands, abandons and applies Ask Daoris's cards, so the key adds nothing to what that script could already do.
- **A terminal the person gives it to, on a machine with no window**: `DAORIS_PERSON_KEY` in the environment of the
  processes they run, a headless `daoris-driver drive` among them. On a machine with a window the terminal holds
  nothing, and asks (§4).
- **A gate that starts its own host**: the rehearsals and `test:web` hand their hosts a key, and present it as the
  person and the driver they play.

**Never a session.**

- The key is not in the shell's environment, so nothing the shell starts inherits it.
- Every start Daoris makes removes `DAORIS_PERSON_KEY` from the child's environment, whatever its parent held:
  `Tools.Hand`, which every process the driver starts is handed (sessions on both doors, conversations, intakes, Ask
  Daoris, probes, hooks and git among them), the terminal panel's environment block (`Tools.ChildEnvironment`), the
  modules' own starts, and the starts exempt from the tools' environment. A source scan holds it beside
  `EveryChildIsHandedTheToolsTests`, because the next start written will not know to. A connector inherits its
  harness's environment and adds only `KnowledgeConnector.Passed`, which never names the key.
- It is never in a file, so no tree, no repository and no home file holds it, whatever a session's tools can read.
- A client sends it only to a loopback address, so a `DAORIS_SERVICE_URL` naming a remote never carries it off the
  machine (§6).

### 2.3 The window and the host it started

- **Proof of possession before use.** The shell's readiness probe asks `GET /api/status?prove=<nonce>`, and a host
  holding a key answers `proof`, an HMAC of the nonce under the key. Asked without `prove`, the status answer is
  unchanged. The shell gives its modules and the page the key only once its own host has proved it holds it, so a
  process that took the port first never learns it.
- **An adopted host gets nothing.** The supervisor adopts a Daoris host it did not start (*adopt, never double-start*).
  It holds no key for one, so the bridge answers the page none, and the window says so once, beside the adoption
  notice: *Daoris is using a service it did not start, so your own acts (a review, a yes, an answer) are refused there.
  Close what started it and restart Daoris.* Reads and the agent's doors work as before.
- **A host the shell starts again** gets a fresh key. The page asks the bridge again when the host answers `stale`
  (§5.2).

### 2.4 Weighed

| Holder | Defends against | Does not defend against |
|---|---|---|
| **A key minted per start, in the starter's and the host's memory, handed on standard input** (chosen) | everything a session reads (its environment, its tree, the repository, the home's files, command lines, `/proc/<pid>/environ`), and every command it runs against the doors | the account's own reach (§1): another process's memory, injection into the shell or the host, UI automation of the window, a dev run's debug port; and the store's file, which bypasses every door (§7) |
| **A file under the home, with an ACL** | a harness's own file tools, which refuse a path outside the session's tree unless a rule allows it (D72) | any command or script a session runs. An ACL names an account, and every process of the account is that account. Sealing it with DPAPI changes nothing: it seals to the account (`DpapiSeal`, the browser's kept sign-in), and any process of the account opens what it sealed |
| **An OS identity: the named pipe's client, or the loopback socket's owning process** | a process of another account | sessions, which run as the same account, so the pipe's client is the person; the calling process is no better, since the page's requests come from the engine's network process, on the desktop every session descends from the shell, and a process of the account can inject into an allowed one. Windows-only, and racy when a process id is reused |

A different identity for sessions is what the third row lacks, and it would close the first row's gaps too. It changes
where every tree, harness sign-in, tool and browser profile lives, so it is PERSONDOOR2's to design (§7), not this one's.

## 3. Which doors are whose

### 3.1 The rule

- **Authority is the key's; attribution is the body's.** A body can narrow who acted (a `session`, `byAgent`), and never
  widen it. REVIEWENV1b3 found attribution standing in for authority once (`PublishedBy` absent was read as the
  person). Here the key is the only way to the person's authority.
- **Without the key, the caller is an agent.** A door with an agent's form judges the call as an agent's. A door with
  none refuses it (§5.2).
- **An unclassified write is refused.** Every route the local host maps carries its class, and a test lists them all.
  A write with none is refused without the key, so a new door is the person's until someone classes it otherwise.
- **Reads stay open.** A session reads the same store through its connector, and a machine path is still answered to a
  loopback caller only (D46).

### 3.2 The local host's routes

Every route in `docs/index/routes.md` at `4004d041`: 59, of which 7 are mapped on a shared host only (§6).

**Open: reads (18).** `GET` `/api/status`, `/api/repositories`, `/api/search`, `/api/entry`, `/api/entries`,
`/api/convergence`, `/api/quests`, `/api/quests/{id}/attachments/{sha256}`, `/api/quests/{id}/claim`, `/api/asks`,
`/api/asks/{id}`, `/api/sessions`, `/api/sessions/{id}/deletable`, `/api/history` (a plan, which deletes nothing),
`/api/registry`, `/api/registry/retired`, `/api/code-map/{repository}` and `/api/sync`. And the page and its assets,
which are no `/api` route.

**Open: what an agent may do (3 routes, judged as an agent's).** The exchange the connector's tools use, so the two
doors cannot drift:

- `POST /api/quests/{id}/respond` with `take`, `done` (with its answers) or `decline`, and no `whileOpen`. The stubs take
  and close here.
- `POST /api/quests` without the key: an agent's publish, `byAgent`, as the connector's with no session. Its `off` is
  judged as REVIEWENV1b3 judges an agent's. With the key it is the person's publish.
- `POST /api/asks/{id}/publish` without the key: an agent's publish onto the ask, crediting the `session` it names. With
  the key and no session, it is the person's choice of receiver.

**The driver's own (11 routes and one form, the key; recorded as what the driver read).** What the gate and the record
read as observed facts, which a session saying them of itself would turn into self-reports (D46, D144):

- `POST /api/quests/{id}/evidence`: a done's evidence, read.
- `POST /api/quests/{id}/set-up` naming a `session`: a set-up that session said, with the commit the driver read from
  its tree. REVIEWENV1b3's hand-back keeps this the driver's own door.
- `POST /api/sessions`, `/api/sessions/chat`, `/api/sessions/intake` and `/api/sessions/help`: a record for a process
  the driver starts, on its own plan or the person's press.
- `POST /api/sessions/{id}/state` and `/api/sessions/{id}/taken`: what it observed of a session.
- `POST /api/sync` and `DELETE /api/registry/retired/{repository}`: a sync pass, and a retire it carried.
- `POST /api/registry`: a registration followed from a repository's line (WSSETUP5), and the person's `connect` and
  *add*. It sets a row's root and workspace, which decide where sessions run and which circle sees a quest (WSP1,
  WSP2), so no agent sets it.
- `POST /api/refresh`: the index rebuilt from disk, the machine's job and not a session's, which is why the `connector`
  default leaves `knowledge_refresh` out.

**The person's alone (19 routes and four forms, the key; recorded as the person's).** REVIEWENV1b3's hand-back, and
the rest of the same kind:

- The review (D154 point 8): `POST /api/quests/{id}/review`; `POST /api/quests/{id}/set-up` with `look`, their own
  set-up; `POST /api/quests/{id}/set-up-step`.
- What a review is set to: `POST /api/asks` with `review`, `POST /api/asks/{id}/review`, and the person's forms of the
  two publishes above, which set a chain's review with the person's authority.
- The yes and the done: `POST /api/quests/{id}/accept`, `POST /api/quests/{id}/done`, and `respond` with `whileOpen` (an
  abandon's decline, which the driver sends on the person's press, PAUSE1c).
- A go-ahead's answer: `POST /api/asks/{id}/go-aheads/{number}` (D135 §2).
- The person's words: `POST /api/asks` (an ask is their sentence, D133 §1), and `POST /api/sessions/{id}/added`, `/say`
  and `/answer`. Each keeps words on an ask as theirs, and every later session is handed them as the person's.
- Closing and deleting: `POST /api/asks/{id}/close`, `DELETE /api/asks/{id}`, `DELETE /api/quests/{id}`,
  `DELETE /api/sessions/{id}`, `POST /api/history/clear` and `POST /api/quests/{id}/conflicts/dismiss`.
- The registry's verbs (D48 §3): `DELETE /api/registry/{repository}`, `POST /api/registry/{repository}/workspace` and
  `POST /api/registry/import`.

### 3.3 What an agent does instead

An agent that needs one of the person's acts asks for it: a go-ahead through its connector (`go_ahead_ask`, D135), a
proposal where one is designed (a review's `off`, REVIEWENV1b3; a rule, D74; Ask Daoris's cards, D89), or its closing
note. Never a confirmation (§4.2), which is a terminal's.

## 4. The terminal

### 4.1 It cannot be told apart

A session can run `daoris-driver` and `daoris` too, whenever a command it may run reaches them. Nothing a binary can
read tells the person's run from a session's. The environment is whatever its caller set, and the parent process
whatever started it. A console is one a process can make for itself, or a pseudo-terminal it drives. The files under the
home are the account's. So **neither binary holds the person's authority by itself.** It presents a key the person gave
it, or it asks the window.

### 4.2 With a window: the terminal asks, the window confirms

A gated write sent without the key is refused `person-only` or `driver-only` (§5.2). The client then asks, for either:

1. It makes a secret of its own, and sends `POST /api/confirmations` with the request it was refused (method, path and
   body) and the secret's SHA-256. The host keeps the confirmation in memory, never in the store.
2. The window shows it, on the app strip and in What needs you, by the route's name in the host's table and the
   request's own fields, never by a sentence the asker wrote: *A terminal asks for a review's verdict on #12: `reviewed`,
   set-up 3 at `a1b2c3d`, with the words "…". Daoris cannot tell whether you ran this or an agent did. Confirm only what
   you ran.* **Confirm** and **Refuse** each need the key.
3. The client says *Waiting for you to confirm this in Daoris's window…* and asks `GET /api/confirmations/{id}` until it
   is answered or two minutes pass.
4. Confirmed, it sends the same request again with `Daoris-Person-Grant: <secret>`. The host takes the grant once, for
   that method, path and body only, within the two minutes.

- **A session can ask, and cannot answer.** What it asks is a card the person refuses, or lets expire, which refuses it.
- **A web page cannot ask.** The door takes a JSON body, which a page on another origin sends only with CORS's leave,
  and only the desktop's origin has it (and a development UI's, where `DAORIS_WEB_ORIGIN` names one).
- **Bounded**: five waiting at once, two minutes each, one use. A sixth is refused with a sentence and asks nothing.
- **The page in a plain browser asks the same way**, and so does the person's own shell in the window's terminal panel
  (CONSOLE4), whose environment is scrubbed like every start's.
- **It is not an approval surface for agents.** A better approval surface must not widen autonomy (D52, D37). Agents ask
  through go-aheads, a card is the person's own act from another place, and its default is no.

### 4.3 With no window: the key in the terminal

On a machine with no window (a rehearsal, or a headless machine, D50), `daoris-driver` and `daoris` present
`DAORIS_PERSON_KEY` from their environment: the key the host's starter holds. What that costs, said where the host prints
the key and in the verbs' usage:

- Anything started from that terminal holds the key, an agent the person starts there by hand included. The driver
  removes it from every start it makes, so a driven session never holds it.
- On Linux any process of the account reads it at `/proc/<pid>/environ`, so there it keeps the key out of a session's
  own environment and little more.
- A key the person keeps in a profile script is a file a session can read.

### 4.4 What this leaves

The person may confirm a card an agent sent, though the card says Daoris cannot tell and names the act in the host's
terms. A session may press **Confirm** by UI automation, which is the account's reach (§1). A machine with no window has
only §4.3's floor. That is the honest edge of a terminal's door on one account.

## 5. The record and the refusal

### 5.1 The record

- **An act recorded as the person's was taken behind the key.** A verdict, the yes, the person's done, their own set-up,
  a go-ahead's answer, their words on an ask, a chain's or an ask's review choice: each is written only by a door that
  checked the key, or by a grant the window confirmed. Their shapes do not change. What changes is that the record's
  claim is now checked.
- **A publish says who asked.** A quest keeps `publishedBy` when a session is named, and gains `byAgent: true` when an
  agent named none (a keyless publish, or a connector with no session), so an agent's publish no longer reads as the
  person's. The quest's page and its trace say *Asked by an agent*. The field is additive: a record from before carries
  neither, and reads as it did.
- **The driver's facts are the driver's**, as today: an evidence verdict, a session's set-up with the commit read, a
  record's state.
- **Records kept before this build** were kept by doors that checked nothing. Nothing marks them, and nothing can tell
  now.
- **The machine log** (D94) gains codes only: `person.refused` (the route's pattern, and `none`, `stale` or `grant`) and
  `person.confirmation` (`asked`, `confirmed`, `refused` or `expired`, with the route's pattern). Never a key, a body or
  the caller.
- **No field says the act came from a terminal.** It would travel with the quests (D68) and tell a teammate nothing they
  can act on. The machine log keeps it on the machine.

### 5.2 How a refusal reads

`403`, in the house's error shape with a code a client reads; never a `500`, and never a bare status:

```json
{ "error": "<sentence>", "code": "person-only" }
```

`code` is `person-only`, `driver-only`, `stale`, `grant` or `confirmations-full`. The sentences, where `<act>` is the
route's own name in the host's table (*a review's verdict*, *the yes to a departure*, *a go-ahead's answer*):

- **The person's door, without the key**: *Only the person can give <act>, and this call carried no key of theirs.
  Nothing was kept. In Daoris's window, press it there; from a terminal, run it again and confirm it in the window. An
  agent asks the person instead: a go-ahead, or its closing note.*
- **The driver's door, without the key**: *Only Daoris's driver posts <act>: it is what the driver read for itself,
  never what a session says of its own work. Nothing was kept.*
- **A key from another start**: *This call carried a key from an earlier start of this service, and each start has its
  own. Nothing was kept. Daoris's window asks for the new one by itself; a terminal takes the one this start's starter
  holds.*
- **A grant for another act**: *This confirmation was for another act: its address or its words differ from what was
  confirmed, or it was used already. Nothing was kept.*
- **Too many waiting**: *Five acts already wait for the person's confirmation, so this one was not asked. Nothing was
  kept.*

A shared host's `401` (D47 §7) is unchanged, and says something else: ask an operator for a key.

## 6. A shared host

- **It has no door that is the person's alone.** The review, the yes, the person's done, the asks, go-aheads, deletes and
  history are mapped on a local host only (`Program.cs`). A person's act reaches a remote as an operation their machine's
  host recorded behind the key, pushed by that machine's sync with its minted key, and the remote judges it by the table
  (D68), never by who pressed.
- **D47's gate stands as it is.** Every route needs a minted key, and the key's name, per person per machine, is the
  caller (D47 §7). A shared host neither reads nor needs a person key, and a client never sends one to an address that
  is not loopback (§2.2).
- **What a remote trusts is a machine's key, and today a session can reach it.** `KnowledgeConnector.Passed` hands
  `DAORIS_REMOTE_URL` and `DAORIS_REMOTE_KEY` to every connector, and the connector reads the home's `remotes.json` to
  claim a shared quest by push (D69). A session holding that key can push operations straight to the remote, a verdict
  among them. This design does not move the key; PERSONDOOR2 does (§7).

## 7. Beyond the doors

What this design leaves open, each within a session's reach and outside every door:

- **The store's file.** The connector opens the store itself, as a child of the session's harness (D36,
  `KnowledgeConnector`), so the store's path is in that harness's reach, and so is the file. A session with `sqlite3`,
  or a second host it starts over the same home, writes any operation it likes. A key on the doors cannot hold a writer
  that never uses them.
- **The home's files.** The person's terminal verbs that edit a file (D50: `daoris driver …`, `daoris agent rules
  accept`, `daoris remote …`) never pass the host. A session that runs `daoris agent rules accept <id>` widens its own
  rules, which D74 says never happens without the person, and one that edits `driver.json` sets a repository's review
  rule to `false`.
- **The remote key** (§6), and **the account's own reach** (§1).

**PERSONDOOR2 designs them**: the connector as a client of the host, with a session's own token in place of the store's
path; a take's claim by push made through the local host, so the remote key leaves a session's environment and files;
the person's file verbs either through a host door that takes the key or said plainly as the account's; and a boundary
between the person and their agents (another account, an AppContainer, a lower integrity level), with what each moves:
trees, sign-ins, tools and the browser's profile.

**The dev loop.** `npm run desktop -- run` starts the window with a debug port so the instruments reach it, and any
process on the machine can drive the page over that port, key and bridge included. That is the instrument's purpose on a
scratch machine. The person's own start of Daoris opens no port on the window: the shell holds the bridge and no port
(`EngineBrowserHost`).

## 8. Rejected

- **Loopback trust as it is** (`Program.cs:309`). D47's amendment retired D36's single configured write key because
  *two credential stories would drift, and the weaker would win*. That key was one static secret in the environment, for
  every writer. The person key is not a second story beside the minted keys: it is minted per start, never configured,
  held only where §2.2 says, and read only by a local host, which never reads a minted key. With nothing on the
  loopback, the person's gate is one command away from every session.
- **A key in a file under the home, with an ACL, or sealed with DPAPI** (§2.4).
- **An OS identity check**: the named pipe's client, the socket's owning process, or the caller's ancestry (§2.4). The
  real OS boundary is PERSONDOOR2's.
- **Trusting the `Origin` header.** A page cannot forge it, which is why the CORS policy names the desktop's origin; a
  process sets any header it likes.
- **Positive identity for agents instead**: a token per session, and everyone else is the person. Absence would mean the
  person, which is the defect REVIEWENV1b3 found in `PublishedBy`, and a new start that forgot its token would make an
  agent the person.
- **The key in an environment variable the shell sets on itself, or on the host's start.** `Spawning.Shell` copies the
  parent's environment into every start, and on Linux any process's environment is readable by the account.
- **The key in the page's storage, a cookie or its URL.** Each is kept in the engine's profile on disk, which the
  account reads.
- **The engine adding the header to the page's requests**, so that no script holds the key. It was not measured in the
  engine's kit, and it defends against nothing the bridge does not already give a script in that page.
- **Signing each request with an HMAC and a nonce, instead of a bearer key.** It guards a key sent to whatever took the
  port; the proof of possession (§2.3) and a key per start already do.
- **Showing this start's key in Settings, to copy into a terminal.** A key in a terminal's environment reaches every
  process started from it, an agent the person runs there by hand included, and on Linux every process of the account.
  The window's confirmation keeps the key in one process.
- **Asking for the account's password at the terminal** (a credential prompt, or PAM). It is a factor an agent lacks,
  but it hands the account's password to Daoris, needs code on each platform, and costs a password for every verdict.
- **A keyless call to a person's door becoming a confirmation by itself.** Every session's attempt would put a card in
  front of the person. The host refuses, the client asks explicitly, and the refusal is logged.
- **Showing every refused call in the window.** A session that tries can try every second. The log keeps the count.
- **Turning every keyless act of the person's into a proposal.** A verdict, a yes or a go-ahead's answer has no meaning
  a proposal box could hold, and agents have go-aheads. Only the publishes have an agent's form, and they keep it.
- **Gating reads.** A session reads the same store through its connector, and machine paths go to the loopback only, as
  before.
- **A field on every operation saying it came from a terminal** (§5.1).

## 9. Against the decisions

- **Amended.** D47 §7, as amended: a local host gates its writes on the person key, the loopback alone no longer
  sufficing, and the shared host's gate is unchanged. D46: the driver presents the key on its own doors, so a session
  cannot report its own state or evidence; it still holds no identity on the record. D50: a terminal on a machine with a
  window acts through the window's confirmation, and on one without through the key in its environment. D92: the page
  holds a key the bridge gives it. LOG2a: the host's input carries its key first. REVIEWENV1b3's `ByAgent`: a keyless
  publish is an agent's at every door, and the record keeps it.
- **Standing.** D21 (local first, and the account as the boundary beyond the doors, now said rather than assumed), D37
  and D52 (a confirmation is no approval surface for agents), D53 (the protocol door's refused permission requests; dsh's
  scrub of names holding `KEY` agrees with §2.2), D72 and D74 (the rules stay the harness's; PERSONDOOR2 takes their
  file), D78 (the browser has no bridge, and a page in it holds no key), and D133, D135 and D154 point 8, whose doors
  are now the person's in fact.
- **The doors REVIEWENV1c–g build** present the key: the strip's chip and the quest's page through the page,
  `daoris-driver quest review` through §4, and the driver's set-up post as its own.

**What the checks do not cover.** Documents only, and nothing is built. The routes, the clients, the spawn code and the
stub sessions were read at `4004d041`. No session was driven at a door, nothing measured how a harness's own sandbox
treats the loopback, the engine's kit was not read for request interception, and no browser was shown a page that posts
to the loopback. Whether a process of the account reads another's memory or drives the window was not tried; §1 says
what the platforms allow, not what was shown here. `verify` checks D156's place, that it names what it rejected, and
this design's links, and none of their words.

## 10. The build

Each row leaves main working. A host enforces only once a starter hands it a key (a). The clients learn to present one
(d, e) and the page to send one (f) before the shell hands it (g). The gates hand their hosts one (h), and a bare host
enforces last (i).

- [ ] **PERSONDOOR1a — the key and the gate** (service). The key from the host's first input line, checked by route
  class (§3.2); keyless publishes as an agent's; `403` with §5.2's codes; the log lines; status's proof. A host handed
  none keeps today's trust. Contract: §2.1, §2.3, §3, §5. Proof: `PersonDoorHostTests`, keyless and keyed per route;
  the class table.
- [ ] **PERSONDOOR1b — confirmed in the window** (service; after a). `/api/confirmations`: the refused request and a
  secret's hash, in memory; the route's name and fields; confirm and refuse take the key; a grant used once, for that
  request, within two minutes; five waiting. Contract: §4.2. Proof: `ConfirmationTests` (a changed body, expiry,
  reuse, the sixth).
- [ ] **PERSONDOOR1c — a publish says who asked** (service; after a). `byAgent` on a quest an agent published naming no
  session, in every place `quest-operations.md` lists, census first; a record from before reads as it did. Contract:
  §5.1. Proof: the census (`QuestOperationKindsTests`), `QuestSyncTests` across the wire, both hosts' answers.
- [ ] **PERSONDOOR1d — the driver** (driver; after b). Every start removes `DAORIS_PERSON_KEY` (`Tools.Hand` before
  its home check, the terminal's block, exempt starts); `ServiceClient` sends it to loopback only, and on `person-only`
  or `driver-only` asks the window and waits, saying so. Contract: §2.2, §4. Proof: a source scan beside
  `EveryChildIsHandedTheToolsTests`; client tests on a stub host.
- [ ] **PERSONDOOR1e — the CLI's management verbs** (cli; after b). `service.ts` sends `DAORIS_PERSON_KEY` to loopback
  only, and on `person-only` or `driver-only` asks the window and waits, for `connect`, `retire` and `import`: d's twin,
  on one fixture of codes and sentences. Contract: §2.2, §4. Proof: `service.ts`'s tests on a stub host; the fixture
  held by both suites.
- [ ] **PERSONDOOR1f — the page** (web-shell; after b, c). The bridge's key in memory, sent on writes, none before
  the route exists; `stale` asks again; a refused press asks the window; the window's card; the adoption notice;
  *Asked by an agent*. Contract: §2.2–§2.3, §4.2, §5.1. Proof: vitest, stories, `i18n:check`, `names:check --strict`,
  the look in both themes and 中文.
- [ ] **PERSONDOOR1g — the shell** (modules; after d, f). A key per host start, handed on input, proved before use,
  presented by the modules; a bridge route answers it to the page, none for an adopted host; the route and the card
  named to `HelpCoverageTests` as exempt. Contract: §2.2–§2.3. Proof: `HostSupervisorTests` (a squatter, adoption),
  the modules' tests.
- [ ] **PERSONDOOR1h — the gates hand it** (tools, web-shell; after d, e). Rehearsals and `test:web` hand their hosts a
  key, presented as the person and the driver. A family phase: a stub's `/review`, `/accept`, go-ahead answer and
  keyless `quest accept` refused; its own doors answered. Contract: §3, §4. Proof: `rehearse:family`'s phase,
  `test:web`, `rehearse:deploy`.
- [ ] **PERSONDOOR1i — every host enforces** (service; after h). A host handed no key mints one, prints it once with
  what it lasts for, and enforces it; `Program.cs`'s *no third gate* says where the key stands. Contract: §2.1, §4.3,
  §9. Proof: the host tests on a bare host: keyless refused, the printed key answered.
- [ ] **PERSONDOOR2 — beyond the doors** (design; after PERSONDOOR1). No door holds the store's file, the home's files
  or the remote key. Design the connector as the host's client on a session's token, the claim by push through the
  host, the file verbs' door, and a boundary between person and agents. Contract: §7. Proof: its decision.
