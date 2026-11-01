# COMMS1 — the brief: one model for how sessions, agents and the person talk and hand work to each other

Status: brief, 2026-11-02. Co-designed under D191: sealed proposals from this brief, one synthesis, then a read. Decision number
reserved: D225. It sits on D212's foundation (a participant read from the records, its work a capability, its role a configured
record) and takes COWORKVIEW1's routing half (round 11, its rows building now) as given.

## 1. Why

Daoris grew its ways of talking one at a time, each with its own record, tool, delivery and words: the ask's channel (D167) and its
message levels (D188), quests and lanes (D32, D65, D115), a helper (D163), a partner and its piece (D199), the team (D203), a
reviewer's opinion (D155), a brainstorm (D176) and a meeting (D177), a hand-over and its context (D189, SESSCTX1), a handback
(D219), a session waiting on the person (D83, D135), Ask Daoris's conversations and proposals (D89, D158, D170), and the roles
that act for the owner (D208, D209). Each works; together they disagree. The owner wants communication and co-working made solid:
one model a session, a person and the code can all read, with today's mechanisms as its cases rather than its rivals.

The co-working view's design found the disagreement while reading it (`docs/2026-10-24-cowork-view-design.md` §1.9, §6): seven ways to
hand out work, each recorded differently and seen in a different place; a repository asking itself refused until SELFASK1; a partner
only the person can start; a native child recorded and never read. The routing half settles how a session is *told* the ways. This
chunk settles what the ways *are*.

## 2. What to design

1. **The census.** Every way a message or a piece of work moves today, between whom, through which tool, route or door, kept in
   which record, delivered how (D188's levels, a wake, a poll, a resume), answered how, and shown where. Where two ways do the same
   job differently, or one job has no way, say so with evidence.
2. **The model.** The few kinds the census reduces to: who may address whom (a participant, a role, a repository, a lane, the
   person), what is sent (a message, a question, a request for work, a piece, an opinion, a hand-over), what it expects back (nothing,
   an answer, a done, a decision), and the one delivery rule each follows (D188's rule, extended or kept). Which of today's records
   each kind lives in, and what stays a case of its own and why.
3. **Waiting and answering.** How a sender waits (holds, carries on, is woken), how an answer finds it after a restart, a hand-over to
   another agent or account, or a move to another machine (D178), and how an unanswered message is said and bounded. The person as a
   participant: what reaches them, where, at which level, and how their answer returns.
4. **Co-working.** Lead, part, helper, partner, reviewer, native child, another repository's session and the roles acting for the
   owner: how each joins work, what it may send and to whom, and how its work returns, with D203's team and COWORKVIEW1's read as
   the view of it.
5. **The surfaces.** The session's conversation and head, the ask's channel, the co-working view, *What needs you*, Ask Daoris and the
   terminal twins: what each shows of the model, so one message is drawn once and found where the person looks.
6. **What changes in each named decision,** what stays, and how older records read.

## 3. Constraints

- Ownership stays local: a session changes its own repository and requests work from another through a quest (D32); nothing reaches
  across.
- A message never stops a working agent; only a stop stops a turn (D188).
- The view is a read, never a store, and reads records, never the conversation's text (COWORKVIEW1 §2).
- Local operation needs no remote; a shared host exchanges permitted records and answers no machine paths.
- Model-dependent behaviour states the tier that ran; D222's words; the routes' authority unchanged unless the design says why.

## 4. What a proposal returns

- The census, as a table with evidence.
- The model: its kinds, its records, its delivery rule, each with what it replaces or keeps.
- Waiting and answering across restart, hand-over and machines.
- The surfaces, as wireframes at 1546 and 680 px where a screen changes.
- What changes in today's code, file by file, with evidence.
- Rows in build order, each with its contract and proof, sized as one wave where they change the foundation.

A proposal reads the code it names, never `local/`, and writes nothing into the repository.
