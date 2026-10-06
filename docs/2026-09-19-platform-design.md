# The Daoris platform — design

**Status: approved direction from the owner, 2026-09-19; written before the code.** The ask, verbatim
in spirit: a task / knowledge / setup platform for Daoris — the *application* over the substrate —
web or desktop based. This document argues its shape; `docs/decisions/D38.md` records the decisions.

## 1. What it is for

Daoris already holds everything the family coordinates on: what each repository knows (the index),
what each owes another (quests), and who is in the family at all (the registry). Every piece is
reachable — by an agent, over MCP; by a script, over HTTP. What does not exist is the surface a
**person** opens.

That gap matters more under automation-first (D37) than it did before. The person's role is the two
ends: setting targets and verifying outcomes. Both ends need a window — *what is outstanding, what
landed, who is asking whom for what, which projects are in and which are not* — and today that window
is a terminal command or nothing. A multi-project build routed through Daoris (the game and its
subsystems) makes the window load-bearing: the person overseeing five projects' agents cannot be
expected to run `quest_list` in five sessions to learn that nothing is stuck.

So the platform is **the person's window over the family**: knowledge, tasks, and setup, in one place.

## 2. Shape: the existing web app, grown — not a new artefact

`Daoris.Web` is already the UI over the service, and the family's own rule is that a second
hand-written UI is the divergence pathology in a new place. The platform is therefore Daoris.Web with
two more views, not a sibling application:

| View | Half of the ask | What it shows |
|---|---|---|
| **Convergence** | knowledge | unchanged — the landing view (D30 stands) |
| **Search** | knowledge | unchanged |
| **Quests** | task | what has been asked of whom; publish; take / done / decline |
| **Projects** | setup | the registry: who is in, what each owns and accepts, who cannot be asked yet, and how a project joins |

**Web first; desktop is the same build, later.** The desktop brief already says the shell hosts this
exact bundle inside the desktop sibling's runtime, consumed at a released version (D22). Nothing in
this design changes that; a desktop shell gets the platform for free the day it exists.

## 3. The write boundary, stated precisely

The original UI decision said *reads only* (D31), and the reason was doctrine: a web editor would beat
`upstream`'s review path for the wrong reason. That reason is untouched — **no rule, knowledge
document or skill is editable from the platform, ever.** Where doctrine should change, the platform
keeps doing what it does today: it proposes the command to run in the repository that owns the file.

Quests are a different kind of thing. They are **service state** (D32, D33), already writable over the
HTTP surface the platform sits on (D36) — and under D37, *filing a quest is how a person sets a
target*. A platform that could show the person an outstanding quest but not let them answer it, or let
them see a gap but not file the ask, would be a window onto a room whose door is next to it. So:

- **Quest publish and respond are in the UI**, through the same key-gated endpoints and the same
  `QuestExchange` judgement every other door uses. Refusals are surfaced verbatim — the service's
  sentence is the contract, and two phrasings would be two behaviours.
- **Declining requires the reason in the UI too**, because the reason is the part the asker can act
  on. The form does not submit without one.
- **`refresh` stays**, as today: re-reading the repositories is service state, not doctrine.

**Remote honesty.** A shared deployment (D47) serves an API and no page at all — the remote is
API-only until person-auth (OIDC) exists, so the person's window over a remote is the desktop shell
over its own local host, not a browser hitting the remote. Local mode — the default, and the mode the
family actually runs — has the full surface, and a refused write there shows the service's own message
rather than pretending the button never existed. (This supersedes the earlier plan, when the interim
single-key gate left reads open and the browser was honestly read-only against a keyed remote; that
gate was retired with nothing deployed — D47 §7.)

## 4. What each new view owes the person

**Quests** answers D32's own question — *what has been asked of whom, and is anything sitting* — which
no single repository's backlog can answer. Outstanding first (open, then taken), closed on request;
filterable by repository; each quest carries who asked, when, its status, and the note that closed it.
The publish form's `to` list comes from the registry, so the UI cannot invite an ask the service would
refuse — and when the target has declared nothing, the service's caution is shown before the person
relies on it.

**Projects** answers the setup half: who has adopted, who has declared what they own and accept, how
much each contributes to the index — and, for the repositories that have *not* adopted, exactly that,
marked rather than hidden (D34: "who cannot be asked yet" is the same question as "who can"). Each
non-adopter row proposes the join commands as text — `init`, fill the domain, `sync`, `connect` — in
the D31 style: the platform names the change and the person makes it where it belongs.

## 5. How it is verified

The same way the service was: driven, not asserted. The example family (D39) gives the platform a
population with known shape — two projects, declared domains, quests moving between them — and the
family rehearsal drives the HTTP surface the views sit on end to end, including the refusals. The
views themselves are verified by building against the typed API and by opening the page over that
family.

## 6. Open questions, deliberately held

1. **Person-auth for a remote deployment** — OIDC per the service design §5, tracked as SVC2. Until
   then a keyed remote is a read-only platform, and that is stated rather than worked around.
   *(Superseded by §3's "Remote honesty": a shared deployment serves no page at all, so there is no
   read-only platform over a remote. A person's window is the desktop over its own local host.)*
2. **Notification** — whether a sitting quest should reach the person without them opening the window.
   Nothing is built; the ledger answered this need locally and the window answers it now. If polling a
   page proves insufficient in real multi-project use, that evidence decides the mechanism.
   *(Since built for sessions, SURF5b: the desktop raises an OS notification when a session parks or
   ends unasked, behind the Driver card's switch. A sitting quest still notifies nobody.)*
3. **The landing view** — convergence today (D30, measured). If real platform use shows the person
   opening Quests first every time, that is evidence to reopen D30 with, not a reason to preempt it.

---

## Amended 2026-09-19 — the Overview landing (D40)

Open question 3 closed the same day it was written, by the owner: the platform is *mostly for the
person to use*, which makes it a management console before it is a knowledge browser. A management
console lands on the state of the thing being managed — so the landing view is now **Overview**:
family health as stat tiles (adopted projects, open and in-progress quests with how long the oldest
has been sitting, the index's size), the outstanding-quest list oldest-first, and the repositories by
what the index holds.

D30's finding is not overturned — it said *search* must not be the lead, and it is not; **Convergence
remains the lead of the knowledge half**, first among the knowledge views and one click away. What
changed is that the platform's first question is no longer "what does the family know" but "is
anything sitting" — the question a person overseeing several projects' agents actually opens the
window to answer (D37: their job is targets and outcomes).
