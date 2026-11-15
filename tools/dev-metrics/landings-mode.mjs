import { landingsSince, summarizeLandings } from './landing.mjs';
import { writeLandingsReceipt, writeLandingsSnapshot } from './snapshot.mjs';

const text = value => typeof value === 'object' && value !== null ? JSON.stringify(value) : String(value);

export function renderLandings(m) {
  const row = (label, value) => `  ${label.padEnd(34)} ${text(value)}`;
  return [
    `Landings (${m.landings}, ${m.withBuilder} with a builder transcript)`,
    row('context read before the first edit', m.preEdit),
    row('pre-edit bytes by class (mean, median)', m.preEditByClass),
    row('change wall time and tokens', m.change),
    row('red rate of main, 7 days', m.main.redRate7d),
    row('red rate of main, 30 days', m.main.redRate30d),
    row('days since the CI was green', m.main.daysSinceGreen),
    row('last green', m.main.lastGreen),
    ...m.withoutBuilder.map(b => row(`no builder: ${b.branch}`, b.missing)),
  ].join('\n');
}

/** `--landings [--since D] [--before ISO] [--receipt name] [--json]`: the three measures over the machine-local landing records of the repository `--repo` (default: here); `--receipt` writes the window to `docs/metrics/<date>-<name>.json`. */
export function landingsMode(o) {
  const home = o.repo ?? process.cwd();
  const measures = summarizeLandings(landingsSince(home, { since: o.since, before: o.before }), home);
  if (o.json) writeLandingsSnapshot(measures);
  if (o.receipt) console.error(`receipt: ${writeLandingsReceipt(measures, o.receipt, { since: o.since, before: o.before })}`);
  console.log(o.json ? JSON.stringify(measures, null, 2) : renderLandings(measures));
  return 0;
}
