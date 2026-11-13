#!/usr/bin/env node
/**
 * The window's first load of an installed Daoris, stage by stage, read from its machine log and nothing else.
 *
 *   node tools/start-load-bench.mjs --logs <logs folder> [--day YYYY-MM-DD] [--last N] [--json]
 *
 * `--logs` is an install's `logs` folder (the files `<date>.<source>.jsonl`). The bench reads only; it opens no store, starts no
 * process and writes nothing, so it can be run against the install the person is using. Each start (`app.started` of the desktop's
 * log) is read to the next, and says what a first load's measurement reads by hand:
 *
 * - `start.stages`: the process, the home, the driver, the host, the window, the browser, the page's first screen with its data and
 *   the splash, each in ms from the process's start.
 * - the first look (`watch.looked` by `start`): its length and each of its stages over a second, with the ms of them spent waiting
 *   for a process start's place (`.queue`), and the look's slow stages (`look.slow`).
 * - the room's first session look (`help.room.look`), the host's requests that took over two seconds (`request.failed`) and the
 *   window's stalls (`ui.stalled`) in the first minute and a half.
 *
 * The times to read are the page stage, the first look's length and the host's slow requests. `--json` prints the same rows as one
 * JSON document.
 */
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { isMain } from './fsx.mjs';

/** The window in which a start's first load is read, ms from its `app.started`. */
export const FIRST_LOAD_MS = 90_000;

/** A stage of the first look is named from this long. */
export const STAGE_FLOOR_MS = 1000;

const STAGES = ['main', 'home', 'driver', 'pipe', 'host', 'shown', 'browser', 'page', 'splash'];

/** The lines of one log file, an unreadable one skipped. */
function linesOf(file) {
  try {
    return readFileSync(file, 'utf8').split('\n').filter(Boolean).flatMap((line) => {
      try { return [JSON.parse(line)]; } catch { return []; }
    });
  } catch { return []; }
}

/** Every event of the folder's `.jsonl` files for the days asked (all when none), in time order. */
export function readEvents(logs, day) {
  if (!existsSync(logs)) throw new Error(`no logs folder at ${logs}`);
  return readdirSync(logs).filter((name) => name.endsWith('.jsonl') && (!day || name.startsWith(day)))
    .flatMap((name) => linesOf(join(logs, name)).map((event) => ({ ...event, file: name })))
    .filter((event) => typeof event.time === 'string')
    .sort((a, b) => a.time.localeCompare(b.time));
}

/** A `watch.looked` line's `stages` as entries: `~nodes=4100x2@id nodes.queue=3400` -> `{ name, beside, ms, queue }`. */
export function parseStages(text) {
  const stages = new Map();
  for (const part of String(text ?? '').split(' ').filter(Boolean)) {
    const [name, value] = part.split('=');
    if (value === undefined) continue;
    const queue = name.endsWith('.queue');
    const key = queue ? name.slice(0, -'.queue'.length) : name;
    const entry = stages.get(key) ?? { name: key.replace(/^~/, ''), beside: key.startsWith('~'), ms: 0, queue: 0, subjects: 1 };
    const ms = Number.parseInt(value, 10) || 0;
    if (queue) entry.queue = ms;
    else {
      entry.ms = ms;
      const times = /x(\d+)/.exec(value);
      if (times) entry.subjects = Number.parseInt(times[1], 10);
    }
    stages.set(key, entry);
  }
  return [...stages.values()];
}

/** The starts of the events: each desktop `app.started` to the next, with what the first load wrote. */
export function startsOf(events) {
  const desktop = events.filter((event) => event.source === 'desktop');
  const begins = desktop.filter((event) => event.event === 'app.started');
  return begins.map((began, at) => {
    const from = Date.parse(began.time);
    const until = Math.min(from + FIRST_LOAD_MS, at + 1 < begins.length ? Date.parse(begins[at + 1].time) : Infinity);
    const within = (event) => { const time = Date.parse(event.time); return time >= from && time < until; };
    const ofDesktop = desktop.filter(within);
    const hostRequests = events.filter((event) => event.source === 'host' && event.event === 'request.failed' && within(event));
    const stagesLine = desktop.filter((event) => event.event === 'start.stages' && Date.parse(event.time) >= from
      && Date.parse(event.time) < (at + 1 < begins.length ? Date.parse(begins[at + 1].time) : Infinity))[0];
    const look = ofDesktop.find((event) => event.event === 'watch.looked' && event.data?.by === 'start');
    return {
      time: began.time,
      version: began.data?.version ?? null,
      stages: stagesLine ? Object.fromEntries(STAGES.map((stage) => [stage, stagesLine.data?.[stage] ?? null]))
        : null,
      lifted: stagesLine?.data?.by ?? null,
      look: look ? { ms: look.data?.ms ?? null, stages: parseStages(look.data?.stages) } : null,
      slow: ofDesktop.filter((event) => event.event === 'look.slow').map((event) => event.data),
      roomLook: ofDesktop.filter((event) => event.event === 'help.room.look').map((event) => event.data),
      requests: hostRequests.map((event) => event.data),
      stalls: ofDesktop.filter((event) => event.event === 'ui.stalled').map((event) => event.data),
    };
  });
}

const sec = (ms) => (typeof ms === 'number' ? `${(ms / 1000).toFixed(1)} s` : 'none');

/** One start as the lines a person reads. */
export function render(start) {
  const out = [`start ${start.time}${start.version ? ` (${start.version})` : ''}`];
  if (start.stages) {
    out.push(`  stages  ${STAGES.map((stage) => `${stage} ${sec(start.stages[stage])}`).join('  ')}${start.lifted ? `  (splash by ${start.lifted})` : ''}`);
  } else out.push('  stages  none written');
  if (start.look) {
    out.push(`  first look ${sec(start.look.ms)}`);
    for (const stage of start.look.stages.filter((each) => each.ms >= STAGE_FLOOR_MS || each.queue >= STAGE_FLOOR_MS)) {
      out.push(`    ${stage.beside ? '~' : ''}${stage.name}${stage.subjects > 1 ? ` x${stage.subjects}` : ''}  ${sec(stage.ms)}${stage.queue ? `  (${sec(stage.queue)} queued for a process start)` : ''}`);
    }
    const queued = start.look.stages.reduce((sum, each) => sum + each.queue, 0);
    out.push(`    queued for process starts, over every stage of it: ${sec(queued)}`);
  } else out.push('  first look  none written');
  for (const slow of start.slow) out.push(`  look.slow  ${slow.beside ? '~' : ''}${slow.stage} ${sec(slow.ms)}, ${sec(slow.queued)} queued`);
  for (const room of start.roomLook) out.push(`  help.room.look  ${sec(room.ms)}${room.trees ? `, ${room.trees} trees ${sec(room.treesMs)}` : ''}${room.kept ? ' (kept)' : ''}`);
  for (const request of start.requests) out.push(`  slow request  ${request.method} ${request.route} ${sec(request.ms)} (${request.status})`);
  for (const stall of start.stalls) out.push(`  window stalled  ${stall.ms} ms during ${stall.during ?? 'unknown'}`);
  return out.join('\n');
}

export function main(argv) {
  const option = (name) => { const at = argv.indexOf(name); return at >= 0 ? argv[at + 1] : undefined; };
  const logs = option('--logs');
  if (!logs) {
    console.error('usage: node tools/start-load-bench.mjs --logs <logs folder> [--day YYYY-MM-DD] [--last N] [--json]');
    return 2;
  }

  let starts;
  try { starts = startsOf(readEvents(logs, option('--day'))); } catch (error) { console.error(error.message); return 2; }
  const last = Number.parseInt(option('--last') ?? '5', 10);
  starts = starts.slice(-Math.max(1, last));
  if (argv.includes('--json')) console.log(JSON.stringify(starts, null, 2));
  else console.log(starts.length ? starts.map(render).join('\n\n') : 'no start in these logs');
  return 0;
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));
