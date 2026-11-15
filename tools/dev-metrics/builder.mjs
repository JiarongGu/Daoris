import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';
import { builderMeasures } from './first-edit.mjs';
import { json, missing } from './records.mjs';

const BRANCH = /^worktree-agent-([A-Za-z0-9]+)$/;

/** Where the harness keeps this repository's sessions: the option, `DAORIS_TRANSCRIPTS`, or the user's home and the repository's project slug. */
export function transcriptsRoot(home, { env = process.env, option } = {}) {
  return option || env.DAORIS_TRANSCRIPTS || join(homedir(), '.claude', 'projects', home.replace(/[^A-Za-z0-9]/g, '-'));
}

/** The subagent transcript of `agent-<id>`, confirmed by its meta's branch; null where none holds it. */
export function findTranscript(root, id, branch) {
  if (!existsSync(root)) return null;
  for (const session of readdirSync(root, { withFileTypes: true })) {
    if (!session.isDirectory()) continue;
    const base = join(root, session.name, 'subagents', `agent-${id}`);
    if (!existsSync(`${base}.jsonl`)) continue;
    const meta = json(`${base}.meta.json`);
    if (meta?.worktreeBranch === branch) return `${base}.jsonl`;
  }
  return null;
}

/** The builder's record for a branch: its measures, or null with the reason no transcript stood behind it. */
export function builderOf(branch, home, options = {}) {
  const id = BRANCH.exec(branch)?.[1];
  if (!id) return { builder: null, missing: missing(`${branch} is not a builder's branch (worktree-agent-<id>), so no subagent transcript is logged for it`) };
  const file = findTranscript(transcriptsRoot(home, options), id, branch);
  if (!file) return { builder: null, missing: missing(`the subagent transcript agent-${id}.jsonl, whose meta names ${branch}, is not under the transcripts folder (DAORIS_TRANSCRIPTS)`) };
  return { builder: builderMeasures(readFileSync(file, 'utf8'), id) };
}
