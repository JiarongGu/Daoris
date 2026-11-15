import { daorisHome } from '../../src/Daoris.Cli/src/platform/home.ts';
import { landingsSince } from './landing.mjs';
import { inWindow, readOpinions, summarizeFamilies, tokensBySession } from './families.mjs';
import { missing } from './records.mjs';

const text = value => typeof value === 'object' && value !== null ? JSON.stringify(value) : String(value ?? 'n/a');
const percent = value => value === null || value === undefined ? 'n/a' : `${Math.round(value * 100)}%`;

function renderRanking(ranking) {
  const lines = ranking.ranked.length
    ? ranking.ranked.map(r => `  ${r.rank}. ${r.family} (${r.score})`)
    : [`  not ranked: ${ranking.note}`];
  if (ranking.ranked.length && ranking.note) lines.push(`  ${ranking.note}`);
  if (!ranking.ranked.length) lines.push(`  samples: ${ranking.unranked.map(u => `${u.family} ${u.samples}`).join(', ')}`);
  return lines;
}

export function renderFamilies(m) {
  const opinion = Object.entries(m.secondOpinion.families).map(([family, s]) =>
    `  ${family.padEnd(12)} ${String(s.given).padStart(3)}/${s.passes} passes given  ${String(s.findingsPerPass ?? 'n/a').padStart(5)} findings a pass  `
    + `${s.handedPasses} handed: fixed ${percent(s.fixedShare)}, rejected ${percent(s.rejectedShare)}, unresolved ${percent(s.unresolvedShare)}  `
    + `median ${s.medianMinutes ?? 'n/a'} min  tokens ${text(s.tokens)}`);
  const work = Object.entries(m.work.families).map(([family, s]) =>
    `  ${family.padEnd(12)} ${String(s.landings).padStart(3)} landings  first pass ${s.firstPass}/${s.withGates} (${percent(s.firstPassRate)})  `
    + `mean ${s.meanWallMinutes ?? 'n/a'} min  tokens ${text(s.tokens)}`);
  return [
    'Second opinions, per family',
    ...(m.secondOpinion.missing ? [`  ${m.secondOpinion.missing}`] : []),
    ...opinion,
    'Ranking by the share of findings the working session fixed',
    ...renderRanking(m.secondOpinion.ranking),
    '',
    `Work, per family (${m.work.unattributed} landings name no family: no builder transcript)`,
    ...work,
    'Ranking by first-pass rate, then wall time',
    ...renderRanking(m.work.ranking),
    '',
    `Spend: ${m.spend}`,
  ].join('\n');
}

/** `--families [--since D] [--until D] [--home <data folder>] [--repo <checkout>] [--json]`: the agent families per role, over the opinion passes of the home (read only) and the landing records of the repository. */
export async function familiesMode(o) {
  const window = { since: o.since, until: o.until };
  const home = o.home ?? daorisHome();
  const read = home ? await readOpinions(home) : { rows: [], missing: missing('no Daoris home: name --home or set DAORIS_HOME') };
  const opinions = read.rows.filter(r => inWindow(r.asked, window));
  const tokens = home ? tokensBySession(home, new Set(opinions.map(r => r.session))) : new Map();
  const repo = o.repo ?? process.cwd();
  const landings = landingsSince(repo, { since: o.since }).filter(r => !o.until || String(r.at).slice(0, 10) <= o.until);
  const measures = summarizeFamilies({ opinions, tokens, landings, opinionsMissing: read.missing ?? null });
  console.log(o.json ? JSON.stringify(measures, null, 2) : renderFamilies(measures));
  return 0;
}
