# Evidence Daoris checks — a met answer with a fact under it

> EVID1, decided as D144 (2026-10-03). EVID1a and EVID1b are built (their notes under D144); the rest is not. The suggestion is
> the outside analysis's §1 ([`DAORIS_FUTURE_DIRECTIONS.md`](DAORIS_FUTURE_DIRECTIONS.md)), weighed in
> [`2026-10-03-future-directions-review.md`](2026-10-03-future-directions-review.md) (its §1 row and the EVID1 slice).
> `Dnn` is `docs/decisions/Dnn.md`. *The queue* is the self-development design's §4 (DEV5–DEV7, not built).

## 1. What is missing

Since D133 a quest an ask asked carries requirements in the person's words, each with the check that proves it. A done
answers each one: met, or departed with the person's words it turns on, and a departure holds the quest for the person's
yes (DRIFT1c, DRIFT1d). A **met** answer is still the agent's word. The record's evidence is the list of commits the driver
saw the session make, and the gate results D46 §4 put in the evidence bundle were never recorded.

The analysis's rule is that *the model should not be the authority that declares its own success: the session proposes,
Daoris determines whether the declared evidence exists*. This design adopts that rule for facts Daoris can read itself.
It does not adopt the analysis's lifecycle as a new state. A done that waits for its evidence is the analysis's
*candidate completion*, and D133's hold already gives it a shape.

## 2. A requirement names its evidence

**The shape.** A requirement gains an optional `evidence` list beside `quote` and `check`. Each item is exactly one of:

- `{ "path": "docs/report-bridge.md" }`: a file or folder the done's commit must hold, repository-relative.
- `{ "gate": "web" }`: a gate the receiving repository declares by that `name` in its gates file (§4).

The check stays the words a person reads. The evidence is the part of it a fact can settle. A requirement with no
evidence closes exactly as today.

**Judged at publish, for every door** (`JudgeRequirementShape`): at most five items on a requirement, and exactly one of
the two keys on each. A path uses forward slashes and is at most 300 characters. It has no leading `/`, no drive, no `..`
segment, no backslash, no control character and no leading `-`, and it does not reach into `.git`. A gate name is at
most 64 letters, digits, `-`, `_` or `.`. A `gate` is refused until EVID1d, naming the queue it waits for. The list is
written only when there is one, as lanes and requirements are. A remote checks only the shape (`JudgeReceived`).

**Who writes it.** Whoever writes the requirement: the intake, from the person's words, or the person at the ask's
publish door. Never the session that will be judged by it (D133 §3). This already holds by structure: requirements
travel only on a quest an ask asks, `quest_respond` cannot change them, and a verify step inherits them without adding
any. The intake's instruction (`IntakePrompt`) asks for a path only where the work plainly leaves a file the person can
name. It asks for a gate only where the room lists one. The room's `AGENTS.md` gains, per repository, the gate names its
gates file declares at the line's tip, and whether this machine lands it through the queue. Evidence is the intake's
reading of the check, shown with the quest. It is never a requirement of its own, which the review declined (§52).

**A chain step** inherits each requirement whole when it is addressed to the same repository. A step addressed to
another repository inherits the requirement without its evidence, since a path is a fact about one repository's tree.
The step's history says so.

**The working session is told** beneath each requirement (`TargetPrompt.Required`): *Daoris reads `<path>` in your
branch's last commit when you end. A met answer without it holds the quest for the person, so commit it first.* This
lets the session catch a missing file itself, which costs nothing.

## 3. When and where Daoris reads it

**The done holds while it waits.** A met answer on a requirement that names evidence closes the quest done and held, as a
departure does (`Quest.Held`), with the cause *evidence unread*. Two facts force this. The service is spawn-free (D46), so
it cannot read a commit: a file in a working tree is not a file on a branch. And a chain's next step publishes in the
done's own transaction (D65 §4), so a check after the done could not stop it. The hold is the session's own write,
because the done set it.

**The driver reads it when the session ends.** In `conclude`, after it reads the quest and before it writes the record,
the driver checks a done that waits on evidence. Its commit is the tree's `HEAD` at that moment: the session's own tree,
or the root checkout with trees off. That is the moment the record's commits are read (`WorkingTree.CommitsSinceAsync`).
For each path it keeps:

- **found or missing**, matched exactly as git matches;
- **the object id found**;
- **whether this work changed it**, between the tree's base (`BaseCommit`) and the commit read;
- **uncommitted**, when the path is in the working tree but not in the commit;
- **a path that differs only in case**, named, and still missing.

It reads and never writes. Each path goes to git as one argument, never through a shell, and is judged again before it
is read.

**One door takes the verdict.** The driver posts to `POST /api/quests/{id}/evidence` with its own key, as it writes
records. It is a local door alone, as the yes is. The exchange judges the verdict. The quest must be done and waiting on
evidence, and the verdict must name the commit and cover every item of every met requirement. The exchange then appends
an **`Evidenced`** operation. If every item was found, the hold lifts and the held step publishes in the same
transaction, as `AcceptAsync` publishes one. If any is missing, the quest stays held with the cause *evidence missing*.
The session's record concludes `completed` as before (D46 §4). Its evidence bundle gains the verdict beside the commits.

| Who reads | When | At which commit |
|---|---|---|
| The driver | The end of the session that made the done | The tree's `HEAD` then |
| The orphan sweep (D104) | When it concludes a session the driver lost | The tree's `HEAD`, read by the same code |
| `daoris-driver quest check <id> --commit <sha>` | When the person runs it | The commit named. It must be the done's commit or come after it on the same history. With no driven end on record, the commit must be named |

**A done no driven session made here** (an interactive session's, a person's, a teammate's whose machine read nothing)
waits with the cause *evidence unread*, and says why. The person reads it at the terminal door naming its commit, or
accepts the done. Daoris never guesses the commit from what stands now (D143 §3). Driving stays additive (D46 §2): an
outside done is held for the same look a departure gets, never refused.

## 4. A gate as evidence (EVID1d)

**Where, and by whom: the queue, and nothing else.** Under the queue a session readies its branch, the queue runs the
repository's declared gates in a queue tree of its own, and *done means landed* (D115 §5). The landing's entry keeps
each gate's verdict. So a gate named as evidence is read from that entry: the gate was in the entry's set, it passed (a
FLAKE passed on its re-run and is kept as one), and it ran on the merge commit that landed. The gate's command is read
as the gates file declared it at that commit. The queue's set for an entry gains every gate its quest's requirements
name, a narrowed steward set included. Path evidence in a queue repository is read at the landed merge commit.

**The cost.** Path evidence is a few git reads per done. Gate evidence under the queue runs nothing extra, because every
entry already runs its declared gates. Running a gate for evidence anywhere else would cost what a landing costs (about
an hour for this repository's set). It would add the load FLAKE1 measured beside running sessions, and it would duplicate
the queue's tree, scratch home, flake rule and logs.

**A repository that does not land through the queue** has no run of the gate that is Daoris's. Its gate evidence waits
with the cause *evidence unread*, reason *no queue*, for the person.

**Rejected:** the driver running the gate in the session's tree when it ends; a gate run the driver saw in the session's
events, because the agent spells its command line and a pipe hides its exit code; and the session's report that it ran
the gate, which is the word this removes.

## 5. What it keeps, and where it is read (D143)

The check chooses whether a done waits, so it keeps the facts it chose by.

- **On the quest**, in the `Evidenced` operation, which travels like every verb (D68): the commit read and how it was
  chosen (a session's end, the sweep or the terminal), who read it and when, and per item its requirement's number, its
  kind and its path or gate, a result code, the object id, whether the work changed it, and for a gate the entry, its
  verdict and when it ran. Codes, not sentences (LANG1a): `found`, `missing`, `uncommitted`, `case`, `not-declared`,
  `not-run`, `failed`, `no-queue`. It holds no machine path (D47 §4).
- **On the session record**: the verdict in the evidence bundle (D46 §4), and the tree and a gate log's place, which stay
  machine-local.
- **In the machine log** (D94): one `evidence.checked` line with counts and no words.

**The doors** (D50, D110): the quest page shows each requirement's evidence with its result. A met answer always says
whether Daoris read it or it stands on the session's word. `daoris-driver quest check` reads again and prints the verdict,
exit 0 when all is found, 1 when anything is missing or unread, and 2 when a store did not answer. `daoris-driver trace`
prints what was kept. Ask Daoris answers from the quest, and proposes a check as a card.

## 6. A quest held for missing evidence

**The record.** The quest is done and held, as a departure holds it: its step is not published, it stays on the
outstanding list, and its ask does not read done. `Held` reads three causes: `departed`, `evidence-unread` and
`evidence-missing`. While the session that made the done still runs, the planner says its evidence is read when that
session ends, and it is not yet the person's. Missing evidence is no strike and no failure (D58). The record says
`completed`, because the quest reached done.

**The page.** It shows the hold's cause, then each missing item with what was read. For example: *requirement 2 names
`docs/report-bridge.md`; it is not in `a1b2c3d` on `daoris/q-17`, and it is in the tree uncommitted*. Beside it are the
session's own met words. The doors are the departure's yes and *Check again*.

**The terminal.** The planner's sentence and `quest list` name the cause and both doors:
`daoris-driver quest accept <id>` and `daoris-driver quest check <id> --commit <sha>`.

**The yes is reused, unchanged.** `Accepted` accepts the done as it stands: its departures and its missing evidence alike,
with one press. *Check again* is how work that arrived later on the same history lifts the hold, and the verdict says the
evidence was found at a later commit than the done's. **Sending the work back** to the session that can fix it waits on
DRIFT1e, which designs how a done quest reopens (D133 §5).

## 7. What is not checked, and when it might be

| The analysis's kind | Here | When it might be |
|---|---|---|
| A file's content meeting the requirement | A judgement (D54). The verify step reads it (DRIFT1e) | A model's review may report it, never gate it (D24) |
| `test`, `browser` | A gate when the repository declares one (this repository's `web` gate is a browser run), so EVID1d. A browser the session drove is the session's account | Already covered by EVID1d |
| A screenshot or artefact | Path evidence for its presence. What it shows is a judgement | — |
| `human` | The person's yes (D133), and a go-ahead for an act (D135) | A named confirmation before the close, if an ask needs one |
| `deployment`, an API response, `performance` | Outside the repository. Reading them is an outward act with a credential (D37, D87) | A plugin point that speaks a fact (plugin design §4), with the first outside plugin (PLUGDIST1f) and a real ask |
| `command` | Refused: a quest that names a command makes the receiver run the asker's verb. Gates stay the repository's declared verbs (D26, D32) | Never |
| `diff`, `api-contract`, cross-repository compatibility | *Changed by this work* is kept, not gated. The rest needs the contract layer (review §9) | With the first contract break seen across two repositories |

## 8. Against the decisions

- **D46**: *the driver never writes quest state*, which D115 §10 upheld when it rejected the queue closing a quest. This
  is amended narrowly, and the amendment is the one judgement here the owner may want to reverse. The driver still
  moves no quest: it takes, closes, declines and reopens nothing. It may record on a done a fact it read, through one
  door the exchange judges, and that record lifts only a hold the session's own done set. Closing on landing stays
  rejected, because a close writes the outcome, which is the session's judgement.
- **D46 §4** is unchanged and completed: the record moves on the exit code and the quest, and gate results are evidence
  in the record, never a state.
- **D133 §4** is amended: a met answer on a requirement that names evidence holds until Daoris finds the evidence, and
  missing evidence holds it as a departure does. D133 §3 gains the evidence beside the check. DRIFT1d's choices stand.
- **D54**: a path in a commit and a queue's verdict are facts, so they gate. *Changed by this work* is a fact too, and it
  is reported, because whether the requirement needed a change is a judgement.
- **D65 §4**: a done's next step also waits for its evidence, and publishes on the found verdict or the yes.
- **D115**: an entry's gate set gains the gates its requirements name.
- **D68 and D104**: one new operation kind. An older build refuses a page holding it as not whole, as it did `accepted`.
  No new quest status, and no new session state.
- **D57 and D143**: unread is never found. Each unread says why.

## 9. Considered and rejected

- **A candidate status before done.** A new status reaches every surface and every older parser (D104). The held done
  already means *not yet released*.
- **The service reading the branch.** It is spawn-free (D46), and a working tree's file is not a commit's.
- **A carry-on that closes after the check**, as the queue closes after landing. That costs a session per done, while the
  done is already the session's outcome. The queue pays it because its close writes what landed.
- **The person's yes for every done with evidence.** The check would then save the person nothing.
- **The hold derived from the session record.** Every list and the ask's state would join two stores, and a record write
  would publish a quest.
- **Reading a path out of the check's prose.** Reading prose is a judgement. The evidence is a field.
- **Refusing the done.** The service cannot read the evidence when the done arrives.

## 10. The build

EVID1a: the shape, the hold and the door, in the service. EVID1b: the driver's read, the instruction, the intake, the
terminal door and the sweep. EVID1c: the page and Ask Daoris, after DRIFT1d2. EVID1d: gate evidence, after DEV5 and DEV7.
Each row's contract is a section above, and each row lists its proof.
