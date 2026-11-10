// The stand-in system: a small key-value store with an etag on each key, kept whole in one file of the plugin's data folder. It is
// what a console-reached system (a cloud's resources, a ticket system's fields) is to Daoris: something a session never changes
// directly, planned against, reviewed, and applied under its own precondition by one applier.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

/** The file the store is kept in, under the plugin's data folder. */
export const STATE = 'state.json';

/** The store as it stands: `{ items: { [key]: { value, etag } }, serial }`; empty where nothing was written yet. */
export function read(data) {
  const file = join(data, STATE);
  if (!existsSync(file)) return { items: {}, serial: 0 };
  const kept = JSON.parse(readFileSync(file, 'utf8'));
  return { items: kept.items ?? {}, serial: kept.serial ?? 0 };
}

/** Keep the store whole: written beside, then moved in, so a reader never sees half of it. */
export function keep(data, state) {
  const file = join(data, STATE);
  const written = `${file}.${process.pid}.tmp`;
  writeFileSync(written, `${JSON.stringify(state, null, 2)}\n`);
  renameSync(written, file);
}

/** A key's etag: changes with every write, so a plan that read an older one is refused. */
export function etagOf(key, value, serial) {
  return createHash('sha256').update(`${key}\n${value}\n${serial}`).digest('hex').slice(0, 16);
}

/**
 * The typed operations a set of intended changes comes to, each with the precondition it is applied under: `create` where the key
 * is absent (it must still be absent), `update` and `delete` under the etag read now. A change that changes nothing is left out.
 */
export function plan(state, changes) {
  const operations = [];
  for (const change of changes) {
    const present = state.items[change.resource];
    const remove = change.remove === true;
    if (remove && !present) continue;
    if (!remove && present && present.value === change.value) continue;
    operations.push({
      resource: change.resource,
      op: remove ? 'delete' : present ? 'update' : 'create',
      before: present ? present.value : null,
      after: remove ? null : change.value,
      precondition: { kind: 'etag', version: present ? present.etag : null },
    });
  }
  return { operations };
}

/** Whether `operation`'s precondition holds against the store as it stands. */
function holds(state, operation) {
  const present = state.items[operation.resource];
  const version = operation.precondition?.version ?? null;
  return version === null ? !present : present?.etag === version;
}

/** What a stale resource is now: its value and etag, or absent. */
function presentOf(state, resource) {
  const present = state.items[resource];
  return present ? { resource, exists: true, value: present.value, version: present.etag } : { resource, exists: false, value: null, version: null };
}

/**
 * Apply `operations` in order under their preconditions. Every precondition is checked first, so a stale plan changes nothing and
 * is answered `stale` with what each stale resource is now; otherwise each is written and its new etag minted.
 */
export function apply(state, operations) {
  const stale = operations.filter((operation) => !holds(state, operation));
  if (stale.length > 0) {
    return { state, answer: { state: 'stale', applied: [], refused: stale[0].resource, present: stale.map((operation) => presentOf(state, operation.resource)) } };
  }

  const items = { ...state.items };
  let serial = state.serial;
  for (const operation of operations) {
    serial += 1;
    if (operation.op === 'delete') delete items[operation.resource];
    else items[operation.resource] = { value: operation.after, etag: etagOf(operation.resource, operation.after, serial) };
  }

  return { state: { items, serial }, answer: { state: 'applied', applied: operations.map((operation) => operation.resource) } };
}
