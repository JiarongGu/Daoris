// The kit's half of a plugin that works (FABRIC-PERFORM1e; the fabric events design §D3, §D6, §D9): what Daoris's plugin kit
// promises every plugin at the work points, so its author writes only the external calls, in plugin.mjs.
//
// Daoris's plugin kit wrote this file, and `daoris-driver plugins new` writes it again for a newer build: change plugin.mjs, not
// this. README.md says what each part promises; `node --test` here checks it (plugin.test.mjs, plugin.contract.test.mjs).
//
// What it keeps, it keeps in the plugin's data folder, in `work-journal.json`, written whole before anything it records is said:
//   - each attempt by its idempotency key, written BEFORE the external call, so a redelivered key never calls it twice;
//   - each attempt's outcome, so `work/reconcile` answers from it and a resync sends the same event again;
//   - each resource's cleanup, so a repeated cleanup is `already-gone`, never a second removal;
//   - each observation it reported, so the same one is never reported under a new id.
// A journal that does not read stops the plugin rather than starting afresh: forgetting an attempt is how an effect happens twice.
import { createHash } from 'node:crypto';
import { appendFileSync, mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/** The capability a performer's handshake names: without it, Daoris never asks it to perform. */
export const CAPABILITY = 'work.perform@1';

const folder = dirname(fileURLToPath(import.meta.url));
const manifest = JSON.parse(readFileSync(join(folder, 'plugin.json'), 'utf8'));

/**
 * The work points' handlers, built around the author's external calls:
 *   act(call)          does the work once: `{ state: 'reached', evidence, outputs?, resources? }` or `{ state: 'failed', code, evidence? }`
 *   check(attempt)     what became of an attempt an earlier process began: `{ state: 'done' | 'failed', outcome }`, `{ state: 'not-started' }`,
 *                      or null when the platform cannot say
 *   remove(resource)   removes one thing `act` made: `{ disposition: 'removed' | 'already-gone' | 'retained' | 'failed', proof?, message? }`
 *   observe(unit, step) what an observed step's platform says of a unit's tip: an outcome, or null while it says nothing yet
 * Under the kit's stand-in world (DAORIS_PLUGIN_WORLD, which its tests and `plugins try` set), none of them is called: the world
 * answers in their place, so nothing a test or a trial runs reaches a platform.
 */
export function work({ data, send, say, act, check, remove, observe }) {
  const world = process.env.DAORIS_PLUGIN_WORLD ? standIn(process.env.DAORIS_PLUGIN_WORLD) : null;
  const calls = world ?? { act, check, remove, observe };
  // This process's life: an attempt begun under another was begun by a process that has since gone.
  const life = `${process.pid}-${Date.now()}`;

  mkdirSync(data, { recursive: true });
  const file = join(data, 'work-journal.json');
  let journal;
  try {
    journal = JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    if (error.code !== 'ENOENT') throw new Error(`${file} does not read (${error.message}); it is kept as it is, and nothing is performed until it reads`);
    journal = {};
  }
  journal.attempts ??= {};
  journal.cleanups ??= {};
  journal.observed ??= {};
  const keep = () => {
    const written = `${file}.${process.pid}.tmp`;
    writeFileSync(written, `${JSON.stringify(journal, null, 2)}\n`);
    renameSync(written, file);
  };

  /** A completion as the one event Daoris takes: bound to its invocation, its id the invocation, so a second sending is the same event. */
  const tell = (attempt) => send({
    jsonrpc: '2.0',
    method: 'work/step',
    params: { step: attempt.step, invocation: attempt.invocation, ...attempt.outcome, at: attempt.at, id: attempt.invocation },
  });

  const settle = (attempt, outcome) => {
    attempt.state = outcome.state;
    attempt.outcome = outcome;
    attempt.at = new Date().toISOString();
    keep();
    tell(attempt);
  };

  const run = async (attempt) => {
    let outcome;
    try {
      outcome = outcomeOf(await calls.act(attempt));
    } catch (error) {
      // It threw, so whether the effect happened is not known: said so, and asked of `check` before anything else is tried.
      outcome = { state: 'unknown', evidence: { message: words(error) } };
    }
    settle(attempt, outcome);
  };

  /** An attempt an earlier process began, or one it could not tell: only the platform can say what became of it. */
  const asked = async (attempt) => {
    const said = calls.check ? await calls.check(attempt) : null;
    if (said?.state === 'done' || said?.state === 'failed') settle(attempt, outcomeOf(said.outcome));
    if (said?.state === 'not-started') {
      delete journal.attempts[attempt.idempotencyKey];
      keep();
    }
    return said;
  };

  const points = {
    // The awaited set, answered at once and never read. Asked to resync, every outcome and observation it knows goes again, the same
    // event under the same id, so Daoris takes it once however often it is sent.
    'work/watch': async (frame) => {
      setImmediate(async () => {
        if (frame.resync) {
          for (const attempt of Object.values(journal.attempts)) if (attempt.outcome) tell(attempt);
          for (const seen of Object.values(journal.observed)) send(seen.event);
        }
        if (calls.observe) await observed(frame.units ?? []);
      });
      return {};
    },

    // One attempt: written down before it is acknowledged, acknowledged before the work, its outcome sent at `work/step` once known.
    'work/perform': async (call) => {
      const known = journal.attempts[call.idempotencyKey];
      if (!known) {
        const attempt = { idempotencyKey: call.idempotencyKey, invocation: call.invocation, unit: call.unit, step: call.step,
          stepVersion: call.stepVersion, subjectRevision: call.subjectRevision, inputs: call.inputs, deadlineAt: call.deadlineAt,
          state: 'begun', life };
        journal.attempts[call.idempotencyKey] = attempt;
        keep();
        setImmediate(() => run(attempt));
      } else if (known.outcome && known.state !== 'unknown') {
        // Delivered again: the same key never calls the platform twice. Its outcome goes again, which Daoris takes once.
        setImmediate(() => tell(known));
      } else if (known.life !== life || known.state === 'unknown') {
        setImmediate(async () => {
          if ((await asked(known))?.state === 'not-started') await points['work/perform'](call);
        });
      }
      return { invocation: call.invocation };
    },
  };

  const answers = {
    // What became of an attempt: from the journal where it knows, else asked of the platform.
    'work/reconcile': async (call) => {
      const attempt = journal.attempts[call.idempotencyKey];
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

    // One resource removed, once: the same resource asked again is `already-gone`, never a second removal.
    'work/cleanup': async (call) => {
      const done = journal.cleanups[call.resource];
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
      journal.cleanups[call.resource] = answer;
      keep();
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
        const seen = journal.observed[where];
        if (seen && JSON.stringify(seen.outcome) === JSON.stringify(outcome)) continue;
        const changes = (seen?.changes ?? 0) + 1;
        const id = `${createHash('sha256').update(where).digest('hex').slice(0, 32)}-${changes}`;
        const event = { jsonrpc: '2.0', method: 'work/step', params: { step, repository: unit.repository, branch: unit.branch,
          commit: unit.tip, ...outcome, at: new Date().toISOString(), id } };
        journal.observed[where] = { outcome, changes, event };
        keep();
        send(event);
      }
    }
  };

  // The requests a performer answers beside its points: no manifest names them, and Daoris asks them only of a performer.
  return { points, calls: answers, capabilities: [CAPABILITY] };
}

/** An outcome as the wire carries it: its state, a failure's code, evidence, outputs and resources, each only where said. */
function outcomeOf(said) {
  const outcome = { state: said?.state ?? 'unknown' };
  for (const name of ['code', 'evidence', 'outputs', 'resources']) if (said?.[name] !== undefined && said[name] !== null) outcome[name] = said[name];
  return outcome;
}

function words(error) {
  return String(error?.message ?? error).slice(0, 300);
}

/**
 * The kit's stand-in world (DAORIS_PLUGIN_WORLD): a JSON file the kit's tests and `plugins try` write, answering in the platform's
 * place, read again at every call. Each external effect is one line in `<world>.effects`, so a test counts them; `<world>.taken` says
 * this plugin took the world, before anything is sent it. Scripted per invocation, or per resource:
 *   { "acts": { "<invocation>": { "state": "failed", "code": "unavailable" } | { "hold": true } | { "exit": true } },
 *     "checks": { "<invocation>": { "state": "done", "outcome": { … } } },
 *     "removes": { "<resource>": { "disposition": "retained", "message": "…" } },
 *     "observed": { "<step>": { "state": "reached" } } }
 * Anything not scripted is reached, removed, or not known.
 */
function standIn(file) {
  writeFileSync(`${file}.taken`, `${process.pid}\n`);
  const script = () => JSON.parse(readFileSync(file, 'utf8'));
  const effect = (line) => appendFileSync(`${file}.effects`, `${line}\n`);
  const step = (kind) => (manifest.steps ?? []).find((each) => `${manifest.id}/${each.id}` === kind || each.id === kind);
  const made = (manifest.resources ?? [])[0]?.schema;
  return {
    act: async (call) => {
      const said = script().acts?.[call.invocation] ?? {};
      effect(`act ${call.idempotencyKey}`);
      if (said.exit) process.exit(3);
      if (said.hold) return new Promise(() => {});
      const schema = step(call.step)?.evidence?.schema;
      const evidence = { ...(schema ? { ref: `${schema}:${call.invocation}` } : {}), message: "the kit's stand-in world" };
      if (said.state === 'failed') return { state: 'failed', code: said.code ?? 'unavailable', evidence };
      return { state: 'reached', evidence, ...(made ? { resources: [{ schema: made, id: `r-${call.invocation}` }] } : {}) };
    },
    check: async (attempt) => script().checks?.[attempt.invocation] ?? null,
    remove: async (call) => {
      effect(`remove ${call.resource}`);
      return script().removes?.[call.resource] ?? { disposition: 'removed', proof: `removed ${call.resource}` };
    },
    observe: async (unit, id) => script().observed?.[id] ?? null,
  };
}
