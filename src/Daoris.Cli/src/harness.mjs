import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { listFiles, listMarkdown, readText } from './fsx.mjs';
import { parseFrontmatter, stripHeader, SKILL_FIELDS } from './document.mjs';

/**
 * Which agent harness a repository is set up for.
 *
 * Daoris targets **one** harness today, and says so rather than pretending otherwise. Every tier
 * decision in this tool is that harness's behaviour, not a universal truth: `rules/` is always-loaded
 * and `knowledge/` is not because that harness decides by path (D7), and a skill's `description` is a
 * trigger because that harness parses it. Install the same tree for a harness that reads
 * `AGENTS.md`, and it loads nothing — silently, with every file present and correct.
 *
 * So the others are **detected and reported, never guessed at**. When one of them is actually adopted,
 * this is the seam that grows a second implementation; until then, an honest "not supported" beats a
 * layout that looks installed and does nothing.
 */
const SIGNALS = [
  {
    id: 'claude-code',
    name: 'Claude Code',
    supported: true,
    look: ['.claude', 'CLAUDE.md'],
  },
  { id: 'agents-md', name: 'the AGENTS.md convention', supported: false, look: ['AGENTS.md'] },
  { id: 'cursor', name: 'Cursor', supported: false, look: ['.cursor', '.cursorrules'] },
  { id: 'gemini-cli', name: 'Gemini CLI', supported: false, look: ['GEMINI.md', '.gemini'] },
  { id: 'copilot', name: 'GitHub Copilot', supported: false, look: ['.github/copilot-instructions.md'] },
  { id: 'aider', name: 'Aider', supported: false, look: ['.aider.conf.yml', 'CONVENTIONS.md'] },
];

/** Every harness this repository shows a sign of, with the evidence that said so. */
export function detectHarnesses(root) {
  const found = [];
  for (const signal of SIGNALS) {
    const evidence = signal.look.filter((path) => existsSync(join(root, path)));
    if (evidence.length) {
      found.push({ id: signal.id, name: signal.name, supported: signal.supported, evidence });
    }
  }
  return found;
}

/**
 * What the supported harness requires of a materialized tree, checked rather than assumed.
 *
 * These are the contracts that fail *silently* when broken: a skill whose frontmatter cannot be
 * parsed is not an error anywhere, it simply never fires. Anything that would fail loudly does not
 * need a check here.
 */
export function verifyHarnessContract(root, target) {
  const problems = [];

  const skillsDir = join(root, target, 'skills');
  for (const file of listFiles(skillsDir)) {
    if (!file.endsWith('/SKILL.md')) {
      // A stray markdown file directly under skills/ is a skill nobody can invoke.
      if (file.endsWith('.md') && !file.includes('/')) {
        problems.push(`skills/${file} is not inside a skill directory, so it can never be invoked`);
      }
      continue;
    }

    const text = readText(join(skillsDir, file));
    if (!text.startsWith('---\n')) {
      problems.push(`skills/${file} does not begin with frontmatter, so the harness will not surface it`);
      continue;
    }
    const { meta } = parseFrontmatter(stripHeader(text), SKILL_FIELDS);
    if (!meta) {
      problems.push(`skills/${file} is missing 'name' or 'description' — it installs but never fires`);
    }
  }

  // The always-loaded tier is a directory, so a rule filed under knowledge/ is simply never loaded.
  for (const tier of ['rules', 'knowledge']) {
    if (!existsSync(join(root, target, tier))) continue;
    for (const file of listMarkdown(join(root, target, tier))) {
      if (file.includes('/')) {
        problems.push(`${tier}/${file} is nested, and only the top level of ${tier}/ is read`);
      }
    }
  }

  return problems;
}

/** A one-line verdict for a report: which harness this looks like, and whether that is supported. */
export function harnessVerdict(root) {
  const detected = detectHarnesses(root);
  const supported = detected.filter((h) => h.supported);
  const others = detected.filter((h) => !h.supported);
  return { detected, supported, others };
}
