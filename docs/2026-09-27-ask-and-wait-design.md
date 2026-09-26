# Ask and wait — a session that needs another repository asks it, and is resumed with the answer

> Written 2026-09-27 from the first real run (FG5). The development session in the front end needed
> three facts about the backend's note API. It tried to read the backend's code, was refused, and
> then wrote the front end on assumptions. Its own proposal named the right move, *"publish a quest
> to [the backend] and block"*, and declined it because blocking was all the workflow offered. The
> owner: *"so the issue here is it should request to [the backend]"*. The decision is **D79**.

## 0. What the run showed

- **Asking was possible and expensive.** `quest_publish` reaches the backend's own agent, and
  `quest_respond done` carries a written answer, which a later session can read from the quest. But
  the asking session had no way to stop its own work and be picked up again once answered. So
  asking meant giving up the quest, and reading the sibling's code looked cheaper.
- **Reading across is the wrong reach.** The front end's own rule required checking the backend's
  data shape. What the backend is for, and whether a new kind of report needs a change there, is
  its agent's to say (D32's reason, applied to knowledge). A read gets the code without the why.
  A guess gets neither.

## 1. The shape

1. **`quest_respond wait`, with `on`:** a session that has taken a quest, and has published a
   question to another repository, parks its quest on that question. The quest **stays Taken**,
   marked as awaiting the question. The move is a quest operation like the others (`Waited`), so it
   is logged and syncs (D68). The take remains the only lock, and it stays with the asker. The
   first draft sent the quest back to *Open*. That let anyone take it fresh, without the tree that
   asked, so it was dropped while building.
2. **The driver holds a waiting quest and resumes it.** A taken quest that awaits an open quest
   sits, and the reason names what it waits on. Once the awaited quest closes (done or declined),
   the machine whose session asked resumes it. That machine is the one whose records hold a session
   on the quest, other than a stand-down; a quest no session here ran is somebody else's take. The
   resume runs in the same tree when it still stands, and its instruction says the quest is already
   the session's own, so it must not take it again or stand down. It carries the answer: the awaited
   quest's repository, its outcome and its closing words. The ledger opens a session on a taken
   quest only in this case.
3. **The session that waited ended well.** Its record concludes `completed`, noting that it asked
   and waits. It is not a failure, and it costs no strike. A resumed session that ends with the old
   wait still standing concludes `failed`. As a stand-down it would be resumed on every tick, since
   nothing about the quest changed; as a failure the strikes bound it.
4. **Agents are told.** The driven session's instruction, and the descriptions of `quest_publish`
   and `permission_propose`, say it plainly: what belongs to another repository, a change or a fact
   about it, is asked of that repository, and a session whose work depends on the answer waits on
   it. Reading into a sibling is not what a rule change is for.
5. **The person sees it.** A waiting quest says what it waits on, in the list and the drawer, with a
   door to the awaited quest.

## 2. What is deliberately not in it

- **Waiting inside a session.** A session is one turn on either door. It ends, and a fresh session
  resumes. Its tree is kept (D51), so the work in progress is still there.
- **A wait on several questions.** One `on` per wait, as a chain step is one `to`. Ask a second
  question when the first is answered, or put both in one quest.
- **Reading a sibling allowed by default.** Considered and not taken here (the owner's answer).
  A read reaches the code without the why.

## 3. Build order

| Item | What lands | Proven by |
|---|---|---|
| **ASK1** | `wait` in the exchange: the `Waited` operation, `awaits` on the quest, the store's column, the log's replay, and the HTTP and MCP doors | service tests over the exchange, the store and the log |
| **ASK2** | The driver: a waiting quest sits and says why, resumes when answered, and the instruction carries the answer. The waiting session concludes `completed` | driver tests over the planner and a stub session that waits |
| **ASK3** | The words: the driven instruction and both tool descriptions. The page: *waiting on #q* in the list and the drawer, in both catalogues | driver and web tests, and a look on the window |
