// The kit's half of a plugin that works, so its author writes only the external calls, in plugin.mjs; `daoris plugin new` writes
// it again for a newer build, so change plugin.mjs, not this. It keeps `work-journal.json` in the data folder through storage.mjs, a
// record at a time and before anything it records is said: each attempt by its key, and the attempt that makes each effect's act by the
// effect's key, BEFORE the external call, and its outcome; each cleanup; each observation reported. Several processes of one plugin share that folder, and each record they write is kept. A
// journal that does not read stops the plugin rather than starting afresh: forgetting an attempt is how an effect happens twice.
import { createHash } from 'node:crypto';
import { appendFileSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { records } from './storage.mjs';

/** The capability a performer's handshake names: without it, Daoris never asks it to perform. */
export const CAPABILITY = 'work.perform@1';

const folder = dirname(fileURLToPath(import.meta.url));
const manifest = JSON.parse(readFileSync(join(folder, 'plugin.json'), 'utf8'));

/**
 * The work points' handlers, built around the author's external calls:
 *   act(call)          does the work once: `{ state: 'reached', evidence, outputs?, resources?, outcome? }` or `{ state: 'failed', code, evidence? }`; `call.options` holds the step's graph options by name, `{}` if none;
 *                      `call.effectKey`, where Daoris sends one, names the act whatever attempt asks it, a mark to leave on what it makes for `check` to find;
 *                      `call.signal` is aborted when Daoris stands the attempt down (a skip, a stop, its holder's Done outside Daoris): read it before
 *                      every write and in every wait, and answer `{ state: 'failed', code: 'stood-down' }` once it is, making nothing after it
 *   check(attempt)     what became of an attempt an earlier process began: `{ state: 'done' | 'failed', outcome }`, `{ state: 'not-started' }`,
 *                      `{ state: 'running' }` while its platform cannot be asked (down, rate-limited; `say` why), or null (a refused credential) to stop;
 *                      given `attempt.link` (a holder's Done outside Daoris), it reads the thing named, never acting, and answers done or failed with `link`.
 *                      A plugin with no `check` cannot tell what a gone process's act came to, so the next attempt on its effect makes it afresh:
 *                      write `check` wherever an act made twice does harm
 *   remove(resource)   removes one thing `act` made: `{ disposition: 'removed' | 'already-gone' | 'retained' | 'failed', proof?, message? }`
 *   observe(unit, step) what an observed step's platform says of a unit's tip: an outcome, or null while it says nothing yet
 * Under the kit's stand-in world (DAORIS_PLUGIN_WORLD, which its tests and `plugins try` set), none of them is called: the world
 * answers in their place, so nothing a test or a trial runs reaches a platform.
 */
export function work({ data, send, say, act, check, remove, observe }) {
  const world = process.env.DAORIS_PLUGIN_WORLD ? standIn(process.env.DAORIS_PLUGIN_WORLD) : null;
  const calls = world ?? { act, check, remove, observe };
  // This process's life: an attempt begun under another is one begun by a process that has since gone.
  const life = `${process.pid}-${Date.now()}`;

  const journal = records(join(data, 'work-journal.json'));
  journal.read();
  const keep = (attempt) => journal.put('attempts', attempt.idempotencyKey, attempt);

  /** A completion as the one event Daoris takes: bound to its invocation, its id the invocation, so a second sending is the same event. */
  const tell = (attempt) => send({
    jsonrpc: '2.0',
    method: 'work/step',
    params: { step: attempt.step, invocation: attempt.invocation, ...attempt.outcome, at: attempt.at, id: attempt.invocation },
  });

  /** This process's attempts not yet settled, by key, each with the abort its stand-down pulls. */
  const live = new Map();

  const settle = (attempt, outcome) => {
    attempt.state = outcome.state;
    attempt.outcome = outcome;
    attempt.at = new Date().toISOString();
    keep(attempt);
    if (live.get(attempt.idempotencyKey)?.attempt === attempt) live.delete(attempt.idempotencyKey);
    tell(attempt);
  };

  const run = async (attempt) => {
    // Stood down before its act began: nothing is made.
    if (attempt.stoodDown) return settle(attempt, stoodDown(attempt.stoodDown));
    const stop = live.get(attempt.idempotencyKey)?.stop ?? new AbortController();
    let outcome;
    try {
      outcome = outcomeOf(await calls.act({ ...attempt, signal: stop.signal }));
    } catch (error) {
      // It threw, so whether the effect happened is not known: said so, and asked of `check` before anything else is tried.
      outcome = { state: 'unknown', evidence: { message: words(error) } };
    }
    return settle(attempt, outcome);
  };

  /** The effect an attempt is for: its `effectKey`, the same for every attempt on its target, else its own key. */
  const effectOf = (attempt) => attempt.effectKey ?? attempt.idempotencyKey;

  /** The attempt that made, or is making, the act an attempt is for, where it is another attempt: null where this one is to make it. */
  const holderOf = (attempt) => {
    const key = journal.get('effects', effectOf(attempt));
    return key && key !== attempt.idempotencyKey ? journal.get('attempts', key) ?? null : null;
  };

  /** This process's acts under way, by effect: an attempt on the same effect waits for its end rather than acting beside it. */
  const acting = new Map();

  /**
   * An attempt an earlier process began, or one it could not tell: only the platform can say what became of it, asked of the attempt that
   * holds its act. Its answer settles that attempt, and this one where it was not known only for waiting on it.
   */
  const asked = async (attempt) => {
    const holder = holderOf(attempt) ?? attempt;
    const waited = holder !== attempt && attempt.state === 'unknown';
    const said = calls.check ? await calls.check(holder) : null;
    if (said?.state === 'done' || said?.state === 'failed') {
      settle(holder, outcomeOf(said.outcome));
      if (waited) settle(attempt, outcomeOf(said.outcome));
    }
    if (said?.state === 'not-started') {
      for (const each of waited ? [holder, attempt] : [holder]) journal.remove('attempts', each.idempotencyKey);
      if (journal.get('effects', effectOf(holder)) === holder.idempotencyKey) journal.remove('effects', effectOf(holder));
    }
    return said;
  };

  /**
   * One attempt's act, made once per effect: an act already made answers it; one under way here is waited for; one failed, lost or begun
   * by a process since gone is asked of the platform first, and its done answer stands for this attempt. Only where none was made, or the
   * platform says it failed or never started, is the act made, its effect written down first.
   */
  const perform = async (attempt) => {
    for (let holder = holderOf(attempt); holder; holder = holderOf(attempt)) {
      if (holder.state === 'reached') return settle(attempt, holder.outcome);
      if (holder.state === 'begun' && holder.life === life && acting.has(effectOf(attempt))) {
        await acting.get(effectOf(attempt));
        continue;
      }
      // With no check to ask, a gone process's act is not known, and this attempt makes it afresh rather than waiting on it for ever.
      if (!calls.check && holder.life !== life) break;
      const said = await asked(attempt);
      if (said?.state === 'done' && said.outcome?.state === 'reached') return settle(attempt, outcomeOf(said.outcome));
      if (said?.state === 'done' || said?.state === 'failed' || said?.state === 'not-started' || (!said && holder.state === 'failed')) break;
      // Running, or not to be told: this attempt is not known either, and is asked after with the attempt that holds its act.
      return settle(attempt, { state: 'unknown', evidence: { message: `the act on this target is still ${holder.invocation}'s, whose outcome is not known` } });
    }
    journal.put('effects', effectOf(attempt), attempt.idempotencyKey);
    const made = run(attempt);
    acting.set(effectOf(attempt), made);
    await made;
    if (acting.get(effectOf(attempt)) === made) acting.delete(effectOf(attempt));
    return undefined;
  };

  const points = {
    // The awaited set, answered at once and never read. Asked to resync, every outcome and observation it knows goes again, the same
    // event under the same id, so Daoris takes it once however often it is sent.
    'work/watch': async (frame) => {
      setImmediate(async () => {
        if (frame.resync) {
          for (const attempt of Object.values(journal.all('attempts'))) if (attempt.outcome) tell(attempt);
          for (const seen of Object.values(journal.all('observed'))) send(seen.event);
        }
        if (calls.observe) await observed(frame.units ?? []);
      });
      return {};
    },

    // One attempt: written down before it is acknowledged, acknowledged before the work, its outcome sent at `work/step` once known. Its act
    // is made once per effect (`effectKey`): another attempt on the same effect is answered with the act already made.
    'work/perform': async (call) => {
      const known = journal.get('attempts', call.idempotencyKey);
      if (!known) {
        const attempt = { idempotencyKey: call.idempotencyKey, ...(call.effectKey ? { effectKey: call.effectKey } : {}), invocation: call.invocation,
          unit: call.unit, step: call.step, stepVersion: call.stepVersion, subjectRevision: call.subjectRevision, inputs: call.inputs,
          options: call.options ?? {}, deadlineAt: call.deadlineAt, state: 'begun', life };
        keep(attempt);
        live.set(attempt.idempotencyKey, { attempt, stop: new AbortController() });
        setImmediate(() => perform(attempt));
      } else if (known.outcome && known.state !== 'unknown') {
        // Delivered again: the same key never calls the platform twice. Its outcome goes again, which Daoris takes once.
        setImmediate(() => tell(known));
      } else if (known.stoodDown) {
        setImmediate(() => settle(known, stoodDown(known.stoodDown)));
      } else if (!calls.check && known.life !== life) {
        live.set(known.idempotencyKey, { attempt: known, stop: new AbortController() });
        setImmediate(() => perform(known));
      } else if (known.life !== life || known.state === 'unknown') {
        setImmediate(async () => {
          if ((await asked(known))?.state === 'not-started') await points['work/perform'](call);
        });
      }
      return { invocation: call.invocation };
    },
  };

  const answers = {
    // What became of an attempt: from the journal where it knows, else asked of the platform. A holder's Done outside Daoris link is read by
    // `check` first, which settles or refuses it: its answer names the link it read, and one that names none leaves the word its holder's.
    'work/reconcile': async (call) => {
      if (typeof call.link === 'string' && calls.check) {
        const known = journal.get('attempts', call.idempotencyKey);
        const said = await calls.check({ ...(known ?? { idempotencyKey: call.idempotencyKey, invocation: call.invocation, unit: call.unit, step: call.step,
          subjectRevision: call.subjectRevision }), options: call.options ?? known?.options ?? {}, link: call.link });
        if ((said?.state === 'done' || said?.state === 'failed') && said.link === call.link) return { state: said.state, outcome: outcomeOf(said.outcome), link: call.link };
      }
      const attempt = journal.get('attempts', call.idempotencyKey);
      if (!attempt) return { state: 'not-started' };
      if (attempt.state === 'reached') return { state: 'done', outcome: attempt.outcome };
      if (attempt.state === 'failed') return { state: 'failed', outcome: attempt.outcome };
      if (attempt.state === 'begun' && attempt.life === life) return { state: 'running' };
      const said = await asked(attempt);
      if (said?.state === 'done' || said?.state === 'failed' || said?.state === 'running' || said?.state === 'not-started') {
        return said.outcome ? { state: said.state, outcome: outcomeOf(said.outcome) } : { state: said.state };
      }
      return { state: 'unknown' };
    },

    // An attempt stood down (a skip, a stop, its holder's Done outside Daoris), told as a notification and answered by nothing: written down
    // first, so no later delivery of its key acts, then its act's signal aborted, so the act makes nothing after it. One not known here yet is
    // written down settled, and its key never acts.
    'work/stand-down': async (call) => {
      const key = call.idempotencyKey;
      if (typeof key !== 'string') return undefined;
      const why = typeof call.why === 'string' && call.why.length > 0 ? call.why : 'stood-down';
      const running = live.get(key);
      const known = running?.attempt ?? journal.get('attempts', key);
      if (known) {
        known.stoodDown = why;
        keep(known);
      } else {
        journal.put('attempts', key, { idempotencyKey: key, invocation: call.invocation, unit: call.unit, step: call.step, stoodDown: why,
          state: 'failed', outcome: stoodDown(why), at: new Date().toISOString() });
      }
      running?.stop.abort(why);
      return undefined;
    },

    // One resource removed, once: the same resource asked again is `already-gone`, never a second removal.
    'work/cleanup': async (call) => {
      const done = journal.get('cleanups', call.resource);
      if (done?.disposition === 'removed' || done?.disposition === 'already-gone') {
        return { disposition: 'already-gone', ...(done.proof ? { proof: done.proof } : {}) };
      }
      if (done?.disposition === 'retained') return done;
      let answer;
      try {
        answer = await calls.remove(call);
      } catch (error) {
        answer = { disposition: 'failed', message: words(error) };
      }
      journal.put('cleanups', call.resource, answer);
      return answer;
    },
  };

  /** Each awaited observed step asked of the platform at the unit's tip; a new answer reported once, under an id of its own. */
  const observed = async (units) => {
    const ours = new Set((manifest.steps ?? []).filter((step) => step.reach === 'observed').map((step) => step.id));
    for (const unit of units) {
      for (const step of unit.awaiting ?? []) {
        if (!ours.has(step)) continue;
        let outcome;
        try {
          outcome = await calls.observe(unit, step);
        } catch (error) {
          say(`${step} at ${unit.branch}: ${words(error)}`);
          continue;
        }
        if (!outcome) continue;
        outcome = outcomeOf(outcome);
        const where = [unit.repository, unit.branch, unit.tip, step].join('\n');
        const seen = journal.get('observed', where);
        if (seen && JSON.stringify(seen.outcome) === JSON.stringify(outcome)) continue;
        const changes = (seen?.changes ?? 0) + 1;
        const id = `${createHash('sha256').update(where).digest('hex').slice(0, 32)}-${changes}`;
        const event = { jsonrpc: '2.0', method: 'work/step', params: { step, repository: unit.repository, branch: unit.branch,
          commit: unit.tip, ...outcome, at: new Date().toISOString(), id } };
        journal.put('observed', where, { outcome, changes, event });
        send(event);
      }
    }
  };

  // The requests a performer answers beside its points: no manifest names them, and Daoris asks them only of a performer.
  return { points, calls: answers, capabilities: [CAPABILITY] };
}

/** An outcome as the wire carries it: its state, a failure's code, evidence, outputs, resources and a decision's route, each only where said. */
function outcomeOf(said) {
  const outcome = { state: said?.state ?? 'unknown' };
  for (const name of ['code', 'evidence', 'outputs', 'resources', 'outcome']) if (said?.[name] !== undefined && said[name] !== null) outcome[name] = said[name];
  return outcome;
}

function words(error) {
  return String(error?.message ?? error).slice(0, 300);
}

/** What a stood-down attempt came to where its act made nothing: failed, `stood-down`, saying why. */
function stoodDown(why) {
  return { state: 'failed', code: 'stood-down', evidence: { message: `stood down (${why}) before its act made anything` } };
}

/**
 * The kit's stand-in world (DAORIS_PLUGIN_WORLD): a JSON file the kit's tests and `plugins try` write, answering in the platform's
 * place, read again at every call. Each external effect is one line in `<world>.effects`, so a test counts them; `<world>.taken` says
 * this plugin took the world, before anything is sent it. Scripted per invocation, or per resource:
 *   { "acts": { "<invocation>": { "state": "failed", "code": "unavailable" } | { "hold": true } | { "exit": true } | { "waits": <ms> } },
 *     "checks": { "<invocation>": { "state": "done", "outcome": { … } } | { "hold": true } },
 *     "removes": { "<resource>": { "disposition": "retained", "message": "…" } },
 *     "observed": { "<step>": { "state": "reached" } },
 *     "check": false }
 * Anything not scripted is reached, removed, or not known. `waits` waits that long before its act, as a wait for a fact does, and makes
 * nothing once stood down; `check: false` is a plugin written with no `check`.
 */
function standIn(file) {
  writeFileSync(`${file}.taken`, `${process.pid}\n`);
  const script = () => JSON.parse(readFileSync(file, 'utf8'));
  const effect = (line) => appendFileSync(`${file}.effects`, `${line}\n`);
  const step = (kind) => (manifest.steps ?? []).find((each) => `${manifest.id}/${each.id}` === kind || each.id === kind);
  const made = (manifest.resources ?? [])[0]?.schema;
  const check = async (attempt) => {
    const said = script().checks?.[attempt.invocation] ?? null;
    return said?.hold ? new Promise(() => {}) : said;
  };
  return {
    ...(script().check === false ? {} : { check }),
    act: async (call) => {
      const said = script().acts?.[call.invocation] ?? {};
      if (said.waits) {
        await new Promise((done) => {
          const timer = setTimeout(done, said.waits);
          call.signal?.addEventListener('abort', () => { clearTimeout(timer); done(); }, { once: true });
        });
        if (call.signal?.aborted) return { state: 'failed', code: 'stood-down', evidence: { message: "the kit's stand-in world: stood down while it waited" } };
      }
      effect(`act ${call.idempotencyKey}`);
      if (said.exit) process.exit(3);
      if (said.hold) return new Promise(() => {});
      const schema = step(call.step)?.evidence?.schema;
      const evidence = { ...(schema ? { ref: `${schema}:${call.invocation}` } : {}), message: "the kit's stand-in world" };
      if (said.state === 'failed') return { state: 'failed', code: said.code ?? 'unavailable', evidence };
      return { state: 'reached', evidence, ...(made ? { resources: [{ schema: made, id: `r-${call.invocation}` }] } : {}) };
    },
    remove: async (call) => {
      effect(`remove ${call.resource}`);
      return script().removes?.[call.resource] ?? { disposition: 'removed', proof: `removed ${call.resource}` };
    },
    observe: async (unit, id) => script().observed?.[id] ?? null,
  };
}
