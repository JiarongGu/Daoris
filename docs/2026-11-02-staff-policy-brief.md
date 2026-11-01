# STAFFPOLICY1 — the brief: a role's staffing as a policy of weighted choices and switching conditions

Status: brief, 2026-11-02. Co-designed under D191: sealed proposals from this brief, one synthesis, then a read. Decision number
reserved: D226. It extends D205 (actors: an ordered list of choices, the first that can run taken, each choice's accounts walked by
D197 before the next) and D206 (a driven line at a limit walks its role's choices, never an agent the role does not list).

## 1. Why

Daoris is a multi-agent system: a role (Build, Design, a second opinion, a helper…) can be staffed by several agents of different
makers and several models. Today that staffing is an ordered list, walked at a start, at a limit (WALKAGENT1), and on a refused model
(MODELREFUSE1); accounts rotate inside a choice by their allowance (D197) and cool on a limit (TOOL4n). The owner wants how they are
used set as a policy: weights that spread work across choices rather than always the first, and the conditions on which a run
switches between them — hitting a limit, a service unavailable, a model refused, repeated errors or a stalled answer — each with what
happens next, and said so a person can see why a run is on the agent it is on.

## 2. What to design

1. **The policy's shape:** per role (and its inheritance by place, D205/D208's scopes): choices (agent, model, effort, in the tool's
   own words), each with a weight or a priority, optional limits (a share, a daily budget, the kinds of work it takes), and the
   selection rule (ordered, weighted, round-robin, by cost or by measured success), with today's ordered list as the default that reads
   unchanged.
2. **Switching conditions and their actions:** the conditions Daoris can observe today (a limit reached and its reset, an account
   cooling, a door's refusal, a model refused, a service not answering, an error class from the agent's own failure words, a stall
   past the inactivity window, a cost bound) and for each what the policy may do: try the next choice, the next by weight, wait for
   the earliest reset, hand over within the turn with its context (D189, SESSCTX1), ask the person, or stop; how many switches a run
   may make before it holds; how a switch is said on the run, the session's head, the role and the record.
3. **Where it is decided:** at a start, at a limit mid-run, at a hand-over, at a helper's or a partner's ask, at a brainstorm's pass;
   one judge in the driver (the foundation's `RoleFor` and the walk), its inputs (accounts' allowance, cooling, measured health, the
   cap), and how it stays predictable for a person and testable.
4. **The person's surfaces:** the one role editor (SETUPUX1l's `RoleEditor`) showing choices with weights and conditions, presets
   (*first that can run*, *spread evenly*, *cheapest first*, …), the run's head saying the choice and why, the place's Roles fold
   summarising the policy; the terminal twin; Ask Daoris proposing a policy as it proposes a role.
5. **What changes in D205, D206, D197 and the hand-over,** what stays, and how older records and roles read.

## 3. Constraints

- No account is assigned to a role (D205); accounts stay the agent's, walked by D197.
- Nothing spends differently until the person sets it (D205's rule); the default policy is today's ordered walk.
- Model-dependent behaviour states the tier that ran (the brief's constraint); D222's words; the routes' authority unchanged (a role
  set stays `SET_ROLE`'s).

## 4. What a proposal returns

- The policy's data shape and its defaults; the judge's rule and inputs; each condition with its observation and its actions.
- The person's surfaces, as wireframes at 1546 and 680 px.
- What changes in today's code (`Roles/`, the walk, the limit's path, the hand-over), file by file, with evidence.
- Rows in build order, each with its contract and proof.

A proposal reads the code it names, never `local/`, and writes nothing into the repository.
