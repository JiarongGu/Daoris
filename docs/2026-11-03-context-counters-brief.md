# CONTEXTDRIVE1 — the brief: behaviors counted in a session's context, and a supervisor that acts on them

Status: brief, 2026-11-03. Designed under D191, then read. Decision number reserved: D229. It extends D208 (the supervisor, the first
role the owner appoints) and D225 (COMMS1; its note of 2026-11-03 makes the answers-alone count a fact a supervisor judges), and it
falls under D228 (one way).

## 1. Why

A session's drive is steered today by numbers fixed in the product, each in its own place:
- the answers-alone bound (D188 point 5);
- strikes that park a quest;
- the staffing policy's most moves a day (D226);
- the quiet window;
- the cap.

Each counts its own behavior its own way and acts its own way. A person cannot see them together, cannot set them where they want a
hard stop, and an agent supervisor has no one place to read what a line has been doing.

The owner's direction is one concept:
- the behaviors a session shows are counted, as facts, in the session's own context;
- a supervisor acts on them;
- a supervisor is either an agent appointed to judge, or a rule the person sets: a supervisor with no agent, a hard limit read with no
  model, such as *on n answers alone, hold for me*;
- collecting the behaviors in one place also makes an agent supervisor's judgement easier, and lets the session see its own course.

## 2. The constraint above every other

**Within the structure, no second route (D228).** Each of these joins what exists; nothing new runs beside it:
- **The counters** live in the session's existing record and context: the course, the desktop store's records, and the brief a start
  and a go-on hand the session (D189, SESSCTX1). They are not a new store or a new feed.
- **A supervisor** is a holder of the existing supervisor role (D208's appointment, its holder and its decision record). A rule is one
  kind of holder, set through the one role door (`SET_ROLE`/`SET_ROLES`, ONEWAY1's staffing) and read by the same judge, with no model.
- **Its actions** are the presses and holds that exist: go on, hold for the person on *What needs you*, move to another choice, hand
  over, stop. No action is new.
- **Today's fixed caps** each become a rule over a counter. They are converted on the person's press through ONEWAY1's one conversion
  door, as a part, and their old readers are deleted with their register entries. Each is a second way the moment the rule exists.
- **Ask Daoris and the terminal** reach rules through the doors the role already has (`role_propose`, the role verb in ONEWAY1's
  command tree).

## 3. What to design

1. **The census.** Every behavior counted or bounded today, and where: the answers-alone count, strikes, the moves a day, quiet stops,
   limits reached and accounts cooled, refusals, transient tries, tokens and minutes, quests handed and results waited on, hand-overs.
   For each, give what it counts, where it is kept, what it resets on, what acts on it, and the code.
2. **The counters.** One shape for a behavior counted in a session's (and a line's) context: what it counts, over which window, kept
   durably across a hand-over and a restart, and how its *unknown* is said where the history is incomplete (COMMS1 §5.3). Say which
   counters belong to a session, a line, a quest and a role.
3. **What the session sees.** How the counters reach the session's own context (the brief, a go-on's words), so the agent knows its
   course, while staying claims, never instructions (D208 §4.4).
4. **The rule supervisor.** Its shape (a behavior, a threshold, a window, a scope of role, place or line, and an action), how it is
   set and inherited by place (D205/D208's scopes), what wins when an agent supervisor and a rule both apply, and how its act is said
   on the line and recorded in the supervisor's decision record.
5. **The agent supervisor's reading.** What it is handed: the counters, the rules in force, and the line's recent course. How its
   decision cites the counters it read.
6. **Defaults.** A fresh home's rules. Do today's caps stay as seeded rules, so that nothing changes until the person changes them?
   Converting an existing home, through the conversion door's new part.
7. **The person's surfaces.** Where the counters and rules are read and set: the role editor's supervisor, a place's Roles fold, the
   line's head and *Details*. What *What needs you* says when a rule holds a line.
8. **Rows,** in waves, with what can be built on main now beside the open waves (WORKFLOWCLEAN1, COMMS1's wave 1, ONEWAY1's waves),
   and how COMMS1g (the answers-alone count) and STAFFPOLICY1's conditions take it up rather than build their own.

## 4. Inputs

- D188, D205, D208 (and the supervisor design `docs/2026-10-24-supervisor-design.md`), D225 and its note of 2026-11-03, D226 (the
  staffing policy and its conditions), D228 (one way) and the ONEWAY1 design (`docs/2026-11-02-oneway-design.md`: the conversion door,
  the register of kept reads, the staffing rows).
- The COMMS1 design §5.3 (the count kept durably) and §11.
- D189 and SESSCTX1 (what a session is handed).
- The code: `Drv/Course/`, `Drv/Roles/`, `Drv/Store/`, `Drv/Loop/`, `Drv/Course/Domain/AnswersAlone.cs`, the strikes, and the staff
  policy's judge.

## 5. Proof

The census, the design accepted after its reads, and its rows filed in the backlog with their contracts and proofs.
