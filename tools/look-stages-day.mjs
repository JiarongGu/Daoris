#!/usr/bin/env node
/**
 * A day of an installed Daoris's looks, stage by stage: the seconds each stage held and the seconds it queued for a process start's
 * place, summed over every `watch.looked` line of the day, read from its machine log and nothing else.
 *
 *   node tools/look-stages-day.mjs --logs <logs folder> --day YYYY-MM-DD [--stages ~opinions,owed,~nodes] [--json]
 *
 * `--logs` is an install's `logs` folder. It reads only, so it can run against the install the person is using. A stage beside the look
 * keeps its `~`; a stage's held time sums every span of it, so work beside the look that ran side by side counts each span. The figure
 * a change to the look's queue is judged by is the day's totals before it and after it lands on the install.
 */
import { isMain } from './fsx.mjs';
import { parseStages, readEvents } from './start-load-bench.mjs';

/** Each stage's day: held and queued ms, the looks that named it and the spans it ran, largest held and queued first. */
export function dayOf(events) {
  const stages = new Map();
  let looks = 0;
  for (const event of events) {
    if (event.source !== 'desktop' || event.event !== 'watch.looked') continue;
    looks += 1;
    for (const stage of parseStages(event.data?.stages)) {
      const key = `${stage.beside ? '~' : ''}${stage.name}`;
      const day = stages.get(key) ?? { stage: key, held: 0, queued: 0, looks: 0, spans: 0 };
      day.held += stage.ms;
      day.queued += stage.queue;
      day.looks += 1;
      if (stage.ms > 0) day.spans += stage.subjects;
      stages.set(key, day);
    }
  }

  return { looks, stages: [...stages.values()].sort((a, b) => (b.held + b.queued) - (a.held + a.queued)) };
}

const seconds = (ms) => Math.round(ms / 1000).toLocaleString('en-US');

/** The day as the lines a person reads: one per stage, `held/queued` in seconds. */
export function render(day, only) {
  const rows = only ? day.stages.filter((each) => only.includes(each.stage)) : day.stages;
  return [`${day.looks} looks`, ...rows.map((each) => `${each.stage}  held ${seconds(each.held)} s / queued ${seconds(each.queued)} s  (${each.looks} looks, ${each.spans} spans)`)].join('\n');
}

export function main(argv) {
  const option = (name) => { const at = argv.indexOf(name); return at >= 0 ? argv[at + 1] : undefined; };
  const logs = option('--logs');
  const day = option('--day');
  if (!logs || !day) {
    console.error('usage: node tools/look-stages-day.mjs --logs <logs folder> --day YYYY-MM-DD [--stages a,b] [--json]');
    return 2;
  }

  let read;
  try { read = dayOf(readEvents(logs, day)); } catch (error) { console.error(error.message); return 2; }
  const only = option('--stages')?.split(',').map((each) => each.trim()).filter(Boolean);
  if (argv.includes('--json')) console.log(JSON.stringify(read, null, 2));
  else console.log(render(read, only));
  return 0;
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));
