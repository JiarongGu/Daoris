---
name: held-clocks
applies_when: writing or changing a fast-half test that gives code a wait (a `Wait`, a bound, a patience) within which a real loop, timer or other process must act, or looking at a sighting that fails under load and passes alone
enforces: the waiting side reads its waits off a clock it is handed; the test hands it one held until the test's own condition has held, bounded so a condition that never holds fails; the loop is kept late in every run so a quiet run has the load's shape; a wider window is no fix
---

# Held clocks — a test that waits on a real loop holds its clock

**A test that gives code N ms of the machine's clock for a real loop to act measures the machine, not the code. Hand
the waiting side a clock that stands still until the test's condition has held, and keep the loop late in every run, so
the quiet run fails where the loaded one did.**

## Why

FLAKE5 (`docs/FIX-LOG.md`, its paragraph under FLAKE1's entry): `SessionsSayCommandTests` gave the say verb a 300 or
400 ms `Wait`, read off the machine's clock from the verb's start, within which a real `SessionRequestWatch`, ticking every
20 ms on a `PeriodicTimer`, had to take the words. Under four workers' load the pool ran the tick late; the verb's wait
ended first, it withdrew the say and printed the answer for *no loop took them*. Three cases failed that way and passed
alone. FLAKE1's sightings had pointed there already: the same class's held-words case exited 2 under its 600 ms wait in a
full fast run, and the family rehearsal failed twice at the same check, "a fast-half test waiting on a clock is the shape
to look at".

This trap succeeds wrongly. On a quiet machine the loop always beats the clock, so the test is green every time its author
runs it and its CI runs it alone; it fails only beside other work, as a sighting nobody can reproduce, and it reads as the
product being flaky. A wider window only moves the load at which it fails, and makes every honest failure wait longer.

## How to apply

- **The code reads every wait it measures off a clock it is handed.** A `TimeProvider`, `TimeProvider.System` in
  production: the say verb's `SessionsWorld.Clock` (`SessionsCommand.cs`), read through `Waited` for both `Wait` and
  `Answer`. A wait read anywhere off `DateTime.Now`, a `Stopwatch` or `Task.Delay` is one the test cannot hold; move it to
  the clock in the same change.
- **The test hands it a clock held until the test's condition has held, then running as the machine's.** The condition is
  the moment the wait means to count from: the loop entered its say (`clock.Run()` first thing in the test's `Say`), or the
  stand-in door has heard the verb's second ask (`Room.Heard`), never "some milliseconds after the start". The verb's wait
  then counts from the take, however late the load made it.
- **Bound the hold.** The clock runs by itself after a minute (`HeldClock.Bound`), so a condition that never holds fails
  its test on the code's own wait rather than hanging the run.
- **Keep the loop late in every run.** The test's loop looks only after the wait would have run out on the machine's clock
  (`Late(world)`, the wait twice, as the loop's `every:`), and a late pass delays inside its `Say`. Every run then has the
  load's shape, so the test is red on the machine's clock before the fix, and stays meaningful on a quiet machine.
- **Wait on an answer the other side always writes, where it writes one** (FLAKE3): a loop that took a say always answers
  it, so the verb waits on that answer, bounded by the loop's own bound on a say, and on the clock only for whether it took
  the words at all.
- **Two clocks, two jobs.** One that *runs from the condition* (the driver tests' `HeldClock` in `SessionsSayCommandTests`)
  where the code's wait should pass in real time once the thing it waits on has started. One that *moves only when the test
  moves it* (the modules tests' `HeldClock` in `Platform/PipeTestKit.cs`, `Advance`, RPC1c) where the test spends the wait
  itself, such as a confirmation's two minutes, and fires the timers it made.
- **A bound a timer ends is read again where the person acts** (NOTIFY2a): a timer under load fires late, so an answer taken while
  it has not fired must read the clock itself, or the bound the person was told is not the bound kept. The modules' `HeldClock.Pass`
  moves the time and holds every timer late; a test passes the bound with it, acts, and shows the act refused
  (`PipeConfirmationsTests`), which `Advance` alone, firing the timer first, can never show.
- **A claim of "at once" or "within its bound" is a bound the test cannot reach** (TESTCLOCK2): give the code a bound far past the
  test's own patience (an hour's `Answer`, an infinite patience, a ten-minute grace past the run's limit), so a call that waited it
  out never ends, rather than timing the call with a `Stopwatch`. Where the bound is the code's constant, read it on a handed clock
  and move it (`HookPeer`'s and `HookProcess`'s grace, the driver tests' `Plugins/MovedClock.cs`, whose `Timed` says the code is
  waiting on it); where the claim is two constants, compare the constants.
- **A wait the code holds is a wait on its clock** (TESTCLOCK3): the change stream reads a wait's bound on a handed `TimeProvider`,
  and a test hands it one that stands still (`MovedClock` in the service tests, the HTTP tests' `MovableClock` through a host's
  services). A wait that never ends by itself makes "at once" and "woken" mean an answer at all. To end it, move the clock past it.
  Where the code waits on something it holds (a semaphore, a judge), the test holds that thing (`ChangeStream.HoldSamplerAsync`), or
  hands a judge that never answers with no bound. A real process's clock cannot be handed: judge against the code's own bound (the
  wait's 30 s, counted from the ask), never a few seconds of the test's.
- **A negative wait is a signal** (TESTCLOCK2): "the edit has not finished within 400 ms" passes when a loaded edit has not begun.
  Wait instead for the code to say it is waiting (`RecordLock.Waiting`), then assert it has not finished.
- **A bound that elapses only on failure is not a window: it reads `Poll.Patience`.** A test waiting on a signal (an answer, a
  fact, an exit) bounds it by its project's two-minute `Poll.Patience` (driver, modules, service and HTTP tests each have one;
  TESTPATIENCE1, TESTCLOCK1), never a few seconds of its own. A window in which nothing should happen, and an elapsed bound that
  states the claim, are not such bounds.
- **Not a fix:** a wider window, a sleep before the assertion, or a retry. A retry (`Held_words_name_what_holds_them`,
  TESTWAIT5) is a stopgap that makes a cause visible until it is named, and stays a backlog row until it is dropped.
- **Prove it the way FLAKE5 did:** red with the late loop on the machine's clock, green with the held one, and the class
  green repeatedly beside load (twelve runs, five beside a full build of another project).

## The exemplar

`src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Sessions/SessionsSayCommandTests.cs`: `Late` and `HeldClock` near the top;
`A_loop_that_took_the_words_and_answers_after_the_wait_is_waited_for` (a late loop that answers), `…went_away_unanswered_is_said_at_once`
and `…never_answers_is_said_after_its_bound` (a late loop that does not), and
`A_session_still_winding_up_after_the_wait_could_not_take_them` (the clock run by the stand-in door's count, not the loop).
