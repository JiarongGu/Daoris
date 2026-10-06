# Landing done work automatically, and proof that outlives the environment

> LAND2, decided as D145, and EVID2, decided as D146 (2026-10-04). LAND2a, LAND2b and LAND2c are built (their notes under
> D145; D149 settles §3's related work and a merged pull request); the rest is not. `Dnn` is
> `docs/decisions/Dnn.md`. EVID2 builds on EVID1 ([`2026-10-03-evidence-design.md`](2026-10-03-evidence-design.md),
> D144), of which only EVID1a is built. The owner, looking at a ticket's work in the work repository that Daoris recorded as
> done: *"so the thing is it's closed? but there is no PR branch opened and there is nothing I can really review on and
> there is no proof it's completed if the server is stopped (for local development) it should take some screenshot for
> its completion state since there are tasks not really related to code, it's configuration based"*. Then: *"also I
> found the 审阅 loads really slow and there should be a setup that auto approves since we still have PR as the last
> human step"*, and *"and auto approve should be a configurable setting"*.

## 0. What the install showed

Five driven sessions were done and sat in *To review*. Their commits were only on Daoris's own session branches
(`daoris/s-…`), and one read *1 commit not on any of your branches*. The work workspace's landing rule was
`{ form: branch, pattern: feature/{slug}-{quest}, tidy: true }`, with no plugin. Every repository in that workspace lives
in Azure DevOps. Daoris's own Azure DevOps and GitHub pull-request plugins were offered under Plugins → Daoris's own,
and not installed. Earlier work had landed on `feature/…` branches when the owner pressed Accept (D102's landings
record). So work lands only at a press (D87), and the press had not been made.

Much of the work was configuration made through a running environment's API: records created, and a report
configured. Its proof is that environment's state and how a page looks, not a diff. The environment is local and may
be stopped later. A done of that kind leaves nothing to review, and nothing to look at once the environment is gone.

Two gaps, two designs. **LAND2:** a setting that accepts done work without the person's press, because the pull request
the rule's plugin opens is the last human step. **EVID2:** a done carries captured proof (screenshots and API answers)
that Daoris keeps on the machine. The review's slowness is a row of its own, which the parent measures.

## Part I: LAND2, accept done work automatically

## 1. The setting

**Accept automatically** (自动采纳) is the auto-approve setting the owner asked for: a branch rule's switch,
`"autoAccept": true`. When a quest is done, its work lands as though the person had pressed Accept: the branch the
pattern names is made, and the rule's plugin pushes it and opens the pull request (D100). **It is off until the person
sets it**, so today's Accept stays the default.

**Its scope is the rule's** (D87). It is set per workspace, on the workspace's rule, for every repository there that sets
none of its own. **A repository may override it** with a rule of its own, which replaces the workspace's rule whole, the
switch included: a repository whose own rule leaves the switch off is accepted by hand even in a workspace that accepts
automatically, and the reverse. `--clear` on the repository hands it back to the workspace's rule. The switch lives on
the rule, not beside it as reading across does (D107), because it means nothing apart from the branch and the plugin it
lands through. A setting of its own could read *on* for a repository whose rule merges or names no plugin.

```json
{ "form": "branch", "pattern": "feature/{slug}-{quest}", "tidy": true, "plugin": "azure-devops-pull-request", "autoAccept": true }
```

- **The name.** The owner said *auto approves*. The press it stands in for is the review's *Accept*, which the glossary
  names *accept* (采纳). *Approve* is the go-ahead's press (放行), which is a different yes, so it would give one act two
  names (D116). The glossary gains *accept automatically*, 自动采纳.
- **With it on, the review is optional reading, not a gate.** The pull request is where the work is judged. The review
  still reads the landed branch (D113), for whoever wants to look.
- **The doors** (D50). Settings → Workspace → How work lands gains a switch, *Accept automatically*, beside *Who pushes
  the branch*, on the workspace's rule and on each repository's own. The terminal's verb gains `--auto-accept`, for a
  workspace or a repository alike:
  `daoris driver landing <repository>|--workspace <name> branch <pattern> [--plugin <id>] [--tidy] [--auto-accept]`.
  Ask Daoris's landing proposal takes `--auto-accept` and `--plugin`, which D100 left out of its parser. Each door says
  the sentence in §6 when the switch is set.
- **Only a branch rule takes it.** A merge with `autoAccept` is refused at both doors (`LandingRules.Problem` and its CLI
  twin in `driverconfig.ts`). A merge writes into the person's checkout, and with no press there would be no human step
  between the work and the line (*nothing merges itself*, D51 rule 6). The queue, once built, lands at done by its
  nature (D115 §5).
- **With no plugin, it is allowed and warned.** The branch is made and nothing is pushed, so nothing leaves the machine.
  The person's own push becomes the last human step. Refusing would leave a repository whose platform has no plugin
  without the setting. Both doors warn when it is set: *no plugin opens a pull request, so each done's branch waits here
  for you to push it.* The session's row and its review say *landed on `<branch>`, not pushed*.
- **The session is told** (`Adapters.Landing`): *when you close the quest done, this tree's branch is put on
  `<branch>`* and, with a plugin, *pushed, and a pull request opened*. It still never merges, pushes or opens one itself.

## 2. What lands, and when

**Due.** When the record of a driven session in a tree of its own concludes (the driver's, or the orphan sweep's,
D104), and its quest is done, and its repository's rule accepts automatically, the session is due to land. The due list
is `<home>/sessions/auto-landings.json`. Like D124's moved lines, it is worked at each look and never makes a dead look.

**The look lands each due session**, beside the look as a start runs, so a plugin's two minutes (D100) never hold one.
It reads the session's quest by its id first. It lands only when all of these hold:

- the quest is done and **not held**. A departure waits for the person's yes (D133), and evidence waits for its verdict
  (D144). The landing follows the release at the next look.
- the session is the newest on its tree, and nothing live holds the tree (`ReviewableTree`'s rule).
- the tree holds work no branch of the person's holds (D88's proof, read at that moment).

Then it lands exactly as the press does, along `SessionTrees.LandAsync`'s branch path: the branch, its record (D102),
the plugin, and the rule's tidy behind D88's proof, never forced. The rule set both, so the tidy follows an automatic
acceptance as it follows a press. One thing differs. **A plugin that cannot land here does not stop the branch.** At a
press, D100 refuses before anything is made, because the person is there to fix the plugin. With no press, nobody is
there, and a missing branch is what the owner found. So the branch is made and recorded, the push is not tried, and the
sentence names the plugin's problem and the hand-off that pushes it later (`trees hand`, D102).

**What lands nothing**, each said in the conversation's record as a landing's note is (D100):

- a done with no commits. The note says the work made no commits, and its page shows its proof (§12).
- a session with trees off. Its work is already in the checkout it ran in.
- a conversation. It has no done, so it lands at a press only.
- a session that did not end on a done (declined, failed, stood down, stopped). These keep today's review.

**A refusal is not retried every look.** Each try is kept with the tree's tip and status it was made at. The pass tries
again only when either changes, or when the person presses Accept.

## 3. A chain lands on one branch

**One branch per chain, per repository**, named for the chain's first quest (WSR5's `SubjectAsync`, which already names
a press's landing that way). The first done in that repository makes it. Each later step's done in the same repository,
such as its verify step's, advances it. The pull request opened at the first done grows as the chain goes on, so a
ticket has one pull request.

**Advancing** moves a branch only as a fast-forward, and only a branch Daoris made. It must be recorded (D102), standing
at the recorded tip and checked out in no working tree, and the new tip must descend from it. It is moved with git's
compare-and-swap from the recorded tip (`update-ref <branch> <new> <old>`), never forced. Anything else is refused as
today: *already a branch … Daoris does not move a branch it did not make*. A press advances in the same way. Today a
second Accept on a chain is refused.

- **D82 grows a next step from the chain's landed branch** when the step before's session branch is gone. A tidy after
  the first landing removes it at once, and a step grown from the line could not advance the branch.
- **The plugin is told again** after an advance. The frame (`hook/work/land`) gains `pullRequest`, the one the record
  holds, so the plugin pushes and does not open a second. It also gains `acceptedBy`, `person` or `auto`, because both
  example plugins write *accepted by the person who reviewed it* into the description, which would not be true. Both
  example plugins learn the two fields. A push the platform refuses, because someone moved the remote branch, is the
  plugin's failure, and the branch stands (D100).
- **A step to another repository** lands there, under that repository's rule.
- **A correction** is the person's words to the session (D137). Where its tree is still here, it reopens there, and its
  new commits advance the branch when it ends. With the tidy on, the tree is gone after the landing, so the words take
  D137 §4's fallback. A person who wants corrections made in the session's tree leaves the tidy off.

## 4. What *To review* means

The group's rule does not change (D126 §2.1): an ended session whose tree holds work no branch of the person's holds.
What changes is what lands before anyone looks.

| | Off (the default) | Accept automatically |
|---|---|---|
| Work lands | At the person's Accept | When its quest is done and released (§2) |
| *To review* lists | Every ended session with work not on a branch of the person's | Only what could not land: refused (§5), or waiting on a hold |
| A landed session | Its review reads as landed (D113) | The same. Its row says *landed on `<branch>`* (LOOK2b) and gains its pull request. Reading it is optional |
| The review's acts | Accept, Send back, Discard | D113's for a landed session: the hand-off where unpushed, Discard where a tree is still here. Accept for one that could not land |

The review's note (D113 §3) gains who accepted it: *accepted automatically when its quest was done*, with when, the
plugin and the pull request as a link, or what failed in the plugin's words or Daoris's.

## 5. Refusals

| What | With Accept automatically |
|---|---|
| Uncommitted work in the session's tree | Nothing lands. It stays in *To review* with the count of paths. The person commits them in the tree or tells the session, then Accept lands it |
| The person's checkout dirty, or on another branch | Not a refusal. The branch form touches no checkout (D87) |
| The line moved since the tree grew | The branch is made from the session's branch as it is. There is no rebase (D87), and bringing it up to date is the person's press (D109) |
| A branch of that name that Daoris did not make, or that moved since | Refused, named, and kept in *To review* |
| The plugin cannot land here (D100's four reasons) | The branch is made, and the push is not tried. The sentence names the problem and `trees hand` |
| The plugin fails, or answers that it did not push | As D100: the branch stands, and the sentence says how to push by hand |
| The quest is held | It waits, and says for what. It lands at the next look after the release |
| A merge rule with `autoAccept` | Refused when it is set, at both doors |

## 6. Pushing is outward-facing

D37 keeps a push human. D100 made a rule that names a plugin the person's durable say-so for the push after their
press. *Accept automatically* with a plugin is a push with no press. **The switch is the person's standing say-so, and
every door says so as it is set:** *when a quest here is done, its work is put on its branch, and `<plugin>` pushes it
and opens a pull request without asking you each time. The pull request is where it is judged. Switch it off to accept
each one yourself.* Only the person's doors write it: Settings, the terminal, and an Ask Daoris card the person applies
(D110). An agent never writes `driver.json`.

## 7. The work workspace, worked through

Every repository there lives in Azure DevOps, so the rule names Daoris's own `azure-devops-pull-request` plugin. Its
source is `examples/plugins/azure-devops-pull-request` until PLUGDIST1 moves it, and the install offers it in
`app/plugin-offers/` (D103).

1. **Install it** from Plugins → Daoris's own (*Install*), or `daoris plugin add <its offer folder>`.
2. **Set the rule.** In Settings → Workspace → How work lands, choose Branch, keep the pattern `feature/{slug}-{quest}`
   and *Clean up once landed*, choose the plugin under *Who pushes the branch*, and switch on *Accept automatically*. Or:
   `daoris driver landing --workspace <the work workspace> branch "feature/{slug}-{quest}" --tidy --plugin
   azure-devops-pull-request --auto-accept`.

**Until the plugin is installed and sound, the rule naming it is refused** at both doors, each reason in its own
sentence, and nothing is written (`LandingRules.PluginProblem` and its CLI twin): not installed, switched off,
contributing nothing for a problem the catalogue found, or speaking on no `work/land` point.

**What it needs to push and open a pull request** (its README): `node`; `git`, with `origin` pointing at the Azure
Repos repository and push access for whoever git pushes as; and `az`, signed in with `az login` and carrying the
`azure-devops` extension (`az extension add --name azure-devops`). It finds the organization, project and repository
from `origin`, so the rule names none of them. It runs with the tools' environment (D121). **Daoris holds no credential
for it.** The sign-ins are git's and az's own, kept where those tools keep them, never in the rule, `driver.json` or the
plugin's folder.

**What no door can check when the rule is set**: whether `az` is signed in, or whether the push is allowed. The first
landing finds out. A failure leaves the branch standing and says how to push by hand, and `trees hand` gives the branch
to the plugin again once the sign-in is fixed.

**Then each done there** puts its chain's work on `feature/<slug>-<quest>`, pushes it, and opens a pull request into the
line, titled with the chain's first quest. Its description names the quest, the session, the commits, and that it was
accepted automatically. A verify step's commits advance the same branch and the same pull request. *To review* holds
only what could not land.

## 8. The facts kept, and the doors that read them (D143)

An automatic acceptance is a choice Daoris makes, so it keeps what it chose by:

- **On the landing record** (`landings.json`, D102): `acceptedBy` (`person` or `auto`), the rule as it stood (`plugin`,
  `autoAccept`, and where the rule came from), and each advance (from, to, when, and the session).
- **On the due list**: when the session became due, and each try's tip, tree status and outcome code: `landed`,
  `advanced`, `nothing`, `held`, `uncommitted`, `exists`, `plugin-unready`, `plugin-failed`.
- **In the machine log** (D94): one `landing.auto` line per try, with codes and counts and no words.

They are read on the review's note, by `daoris-driver trees land <session> --plan`, by `trace` (the landing's
`acceptedBy` and its rule), and by Ask Daoris.

## Part II: EVID2, proof that outlives the environment

## 9. Who captures it, and how Daoris gets it

**The session captures, and Daoris keeps.** At a done, a session captures what proves its work: screenshots through the
browser server it is handed (Playwright MCP, from a browser plugin, D78 and D99), and the answers of the API reads it
relied on. It names them in its done, and Daoris keeps them on this machine.

- **`${proof}` is one folder per session**: `<home>/proof/<session>/captured`. The driver makes it at spawn and expands
  it when it hands a session its servers, as it expands `${browser}` (D78 §3.5). The `in-app-browser` plugin, which the
  install offers (D103), and the `browser` example pass `--output-dir ${proof}` where today they pass `${data}/output`,
  so a screenshot lands there.
  The session is told the folder. The rules handed at spawn let it write there and nowhere else outside its tree (D72).
  Nothing lands in the repository.
- **An answer** is a file the session saves into the same folder: the response body as its tool received it, with its
  request line and status. It is the session's capture, kept as it was handed.
- **The done names its proof.** `quest_respond`'s done gains `proof`, at most ten items, each
  `{ "file", "kind": "screenshot"|"answer", "of", "shows", "requirement"? }`. `file` is a name in `${proof}`. `of` is the
  page address or the request line. `shows` is what it shows, in the session's words, at most 300 characters. The
  exchange judges the shape at every door.
- **On demand.** *Capture proof* on a session's or a quest's page sends the session fixed words, which reopen it in its
  tree (D137). The words ask it to capture each requirement's proof and change nothing, and its done names what it
  kept. This works only while the environment still runs, and the press says so.
- **The no-model floor** (D24). Capture needs a browser, not a model. Keeping, checking and showing need neither. On a
  machine with no shell, the in-app browser's server is withheld (D78 §3.6), and the `browser` plugin's own headless
  browser still serves. Where no browser server is handed, the session is told so.

## 10. A requirement may require captured proof

D144's evidence list gains two kinds, words that a session and a person read:

- `{ "screenshot": "<page> showing <state>" }`
- `{ "answer": "<the read it relied on>" }`

Each is at most 300 characters and counts toward D144's five. `JudgeRequirementShape` judges them at publish.

**Who writes them** is D144's rule: the intake or the person, never the session they will judge. The intake asks for one
on configuration work. Where the person's words name a state outside the repository (records in an environment, a
report configured, how a page looks), it writes a `screenshot` or an `answer` item on that requirement. It writes a
`screenshot` only where the room says this machine's sessions are handed a browser, and the room gains that line. The
working session is told beneath the requirement (`TargetPrompt.Required`) to capture before it closes, while the
environment still runs, and to name each capture in its done.

**The hold is D144's.** A met answer on such a requirement closes done and held, *evidence unread*. When the session
ends, the driver reads its proof (§11) and posts the verdict through D144's door, `POST /api/quests/{id}/evidence`, as
an `Evidenced` operation. An item is found when the done names at least one kept capture of its kind for that
requirement. Codes: `kept`, `not-captured`, `missing`, `unreadable`, `too-large`, `wrong-kind`, `no-browser`. A missing
item holds the quest *evidence missing*. The person's yes accepts it as it stands, and *Check again* reads again after
an on-demand capture. A done that no session here made cannot have its captures read, so it waits unread and says why.
A held done does not land until it is released (§2).

**Facts, not judgement** (D54). Daoris checks that a capture is present, its kind by its leading bytes, that it reads
(an image's header and size, or text that is UTF-8), and its size. Whether the screenshot shows the right state is the
person's judgement. It is shown beside the session's `shows` words and never gated. No model reads a screenshot. A
model's reading may later report, and never gate.

## 11. What Daoris keeps, and what it may not keep

- **Kept when the session ends.** The driver copies each named file from `captured/` into
  `<home>/proof/<session>/kept/<sha256>.<ext>`. It writes `proof.json` beside the copies: per item its kind, hash, size,
  dimensions, `of`, `shows`, requirement, and when and by whom it was read. A kept copy never changes.
- **Limits.** A screenshot is PNG or JPEG, at most 5 MB, and at most 8,000 pixels a side, read from its header. An
  answer is UTF-8 text of at most 256 KiB (D111's bound). At most ten per done, and 50 MB per session. A capture over a
  limit is `too-large` and not kept. A name must be a regular file inside `captured/`, with no link and no `..`.
- **What a screenshot may not show.** Daoris cannot read pixels without a model, so a screenshot's content is the
  session's duty and the person's look. The instruction forbids a page that shows a credential, a key or personal data
  beyond what the check needs, and asks for the element rather than the window.
- **What an answer may not keep.** It is text, so Daoris redacts before keeping. Header lines are not kept. Two things
  are replaced by `[redacted]`: a credential-shaped value (an `Authorization` value; a `key`, `token`, `secret`,
  `password`, `sig` or `code` value; a private-key block; a connection string's password) and a machine path (the home,
  the user profile, a drive-rooted path). The count is kept and shown. The same applies to `of`. A redaction is
  reported, never a refusal.
- **Lifetime.** Proof stays until the person removes it (*Remove proof* on the session's page, `daoris-driver proof
  <session> --remove`). Nothing prunes it on its own yet. A bound waits for a real home's measure, as LEFT3's did.

## 12. Where it is shown, and what crosses machines

- **The session's page and its review** gain a *Proof* section: above the diff, or in its place for a done with no
  commits. It shows each capture's requirement, `shows`, `of`, when and size: a screenshot as an image that opens full
  size, an answer as text. The bytes come through the modules' bridge (`SESSION_PROOF`), as a preview's do (D111). The
  row of a done with no commits says how many captures it kept. It is not put in *To review*, which is for work to land.
- **The quest's page** shows each requirement's evidence (D144 §5) with its captures, through the same bridge. A page in
  a browser outside the shell says the proof is kept in Daoris on that machine.
- **The terminal** (D50): `daoris-driver proof <session|quest> [--save <dir>]` lists the captures and copies them out.
  `quest check` and `trace` print codes, hashes and sizes, never bytes.
- **Ask Daoris** answers from `proof.json`, never from the image.
- **Across machines** (D47): never the bytes, the address, nor any path. The `Evidenced` operation carries, per item, its
  kind, requirement, code, hash, size, dimensions, `shows` and which machine keeps it. A teammate sees that proof exists
  and where, not what it shows. No host serves a proof byte over HTTP in either mode, so a browser on a shared host
  learns nothing of this machine (`test:web`'s absence checks gain one).
- **In the machine log**: one `proof.kept` line with counts, sizes and codes.

## 13. Against the decisions

D145 and D146 list what each amends and what each rejected, with the reasons. In short:

- **Amended**: D87 and D100 (a rule may accept automatically, and its plugin's push needs no press), D113 §3 and D87 (a
  branch Daoris made may advance), D82 (a next step grows from the chain's landed branch), D144 (two kinds of evidence,
  and its §7 row for a screenshot), D78 §3.5 (`${proof}`), and D72 (one folder outside the tree).
- **Standing**: D51 rule 6 (nothing merges itself, and the tidy runs only because the rule asks), D46 (a landing writes
  no quest state), D37 (the push is the plugin's), D126 §2.1's rule, D47 §4 and D76 (proof stays on the machine), and
  D24, D54 and D143 as §8 and §10 say.
- **Rejected, among others**: landing every done with no setting, the name *auto-approve*, a setting apart from the
  rule, a merge at done, a branch per quest, landing a held done, a mark that keeps a landed session in *To review*, Daoris taking the screenshot itself,
  proof in the repository or on the remote, and a model gating a screenshot.

## 14. The build

LAND2a: the switch, both doors, the warning, the instruction and Ask Daoris (§1, §6). LAND2b: the landing at done, its
refusals, the facts and the note (§2, §4, §5, §8). LAND2c: a chain on one branch, D82's growth and the frame's two
fields in both example plugins (§3, §7). EVID2a: the shapes in the service, after EVID1a (§9, §10). EVID2b: `${proof}`,
the driver's read and keep, redaction, the instruction and the intake, after EVID1b (§9–§11). EVID2c: the pages, *Capture
proof*, and the terminal, after EVID1c (§12).
