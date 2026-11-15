import { join } from 'node:path';
import { MODELS, PROMPT, STARTUP, planOf, runStartup, sharesOf } from './startup.mjs';

/** `--startup [--plan] [--models a,b] [--repo <checkout>] [--json]`: the startup floor and each source's share, from headless one-line runs in a throwaway worktree; `--plan` lists the runs and starts nothing. */
export function startupMode(o) {
  const home = o.repo ?? process.cwd();
  const models = o.models ? o.models.split(',') : MODELS;
  if (o.plan) {
    const plan = planOf(models);
    console.log(o.json ? JSON.stringify(plan, null, 2) : [`Startup runs (${plan.length}), one prompt each: ${PROMPT}`, ...plan.map(p => `  ${p.model.padEnd(8)} ${p.variant}`)].join('\n'));
    return 0;
  }
  const rows = runStartup(home, models);
  const shares = sharesOf(rows);
  console.log(JSON.stringify(shares, null, 2));
  console.log(`rows in ${join(home, STARTUP)}: ${rows.length}, ${rows.filter(r => r.failed).length} failed`);
  return rows.some(r => r.failed) ? 1 : 0;
}
