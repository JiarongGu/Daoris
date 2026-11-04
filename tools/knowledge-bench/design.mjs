import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const REPO = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

/** The tools a run may call, and the ones it is refused (the brief's flags). */
export const ALLOWED_TOOLS = 'Read,Grep,Glob,Skill';
export const DISALLOWED_TOOLS = 'Edit,Write,Bash,WebFetch,WebSearch';

/** The five-hour window's share at which the bench stops, to leave the owner's own sessions room. */
export const WINDOW_CEILING = 0.85;

/** Router entries that are not knowledge: the router itself is an index, and the records are read by lookup. */
export const NOT_CORPUS = new Set(['README.md', 'DECISIONS.md', 'FIX-LOG.md', 'task-archive.md']);

/** The one brief every design shares. It names the repository and says nothing about where knowledge lives. */
export const BRIEF = [
  '# Brief',
  '',
  'This is the documentation repository of Daoris, a desktop application that drives coding-agent sessions,',
  'one per repository, together with the command-line tool and the service around it.',
  '',
].join('\n');

/** Each design's own part of `AGENTS.md`, after the brief. */
export const PARTS = {
  '0': () => '',
  A: (corpus) => [
    '## Read on demand',
    '',
    'Before a non-trivial task, scan the *Applies when* column and read every document that matches.',
    '',
    '| Knowledge | Applies when | Enforces |',
    '|---|---|---|',
    ...corpus.map((d) => `| [${d.name}](knowledge/${d.name}.md) | ${d.applies_when} | ${d.enforces} |`),
    '',
  ].join('\n'),
  B: () => [
    '## Read on demand',
    '',
    'The knowledge, each document with when it applies, is listed in `INDEX.md`, generated from the files: read it',
    'before a non-trivial task, and search it when it is long.',
    '',
  ].join('\n'),
  C: () => [
    '## Read on demand',
    '',
    'The knowledge is packaged as skills in `.claude/skills/`, one per document, each described by when it applies:',
    'before a non-trivial task, use every skill whose description matches.',
    '',
  ].join('\n'),
  E: () => [
    '## Read on demand',
    '',
    'The knowledge lives in `knowledge/`, one document per topic, each opening with frontmatter (`name`,',
    '`applies_when`, `enforces`). There is no index: before a non-trivial task, search `knowledge/` for the task\'s',
    'words and read every document that matches.',
    '',
  ].join('\n'),
  G: () => [
    '## Read on demand',
    '',
    'The knowledge lives in `knowledge/`. Each prompt arrives with the documents a local search ranked for it: read',
    'the ones that apply.',
    '',
  ].join('\n'),
};

export const DESIGNS = {
  '0': 'Control: the files, no guidance',
  A: 'Index table in the always-loaded AGENTS.md',
  B: 'Generated INDEX.md, read on demand (D128)',
  C: 'Knowledge as skills (progressive disclosure)',
  E: 'Search-first, no index',
  G: 'Push: a prompt hook prints a local BM25 top 5',
};

/**
 * The questions. Each is phrased in different words from its target, because synonymy is what keyword
 * discovery fails on; the comment after each says which of the target's own words it avoids.
 */
export const TASKS = [
  { id: 'T1', truth: ['2026-09-23-plugin-design'], // avoids: plugin, harness, declare, manifest
    question: 'An add-on that someone drops into the app wants to supply its own coding-assistant program for the work to run on. What is such an add-on allowed to state about that program, and does any of its code get loaded into the app itself?' },
  { id: 'T2', truth: ['2026-10-01-account-rotation-design'], // avoids: account, limit, cool-off, rotation
    question: 'My subscription ran out of its weekly quota halfway through a job. Which sign-in picks up the next job, and when does the exhausted one get used again?' },
  { id: 'T3', truth: ['2026-10-01-naming-design'], // avoids: name, label, glossary, budget
    question: 'How should the words on a settings screen be chosen for English and Chinese readers: is it just translation, and is there a cap on how long each one may be?' },
  { id: 'T4', truth: ['2026-09-30-machine-log-design'], // avoids: log, machine, event
    question: 'What does the desktop app record locally about how I use it, so the product can be improved, and what is it forbidden to keep in that record?' },
  { id: 'T5', truth: ['leak-repair'], // avoids: credential, leak, history, scrub
    question: 'Last week I pushed a commit that contains an access token for our cloud account. Is deleting the line in a new commit enough? What does a proper cleanup involve?' },
  { id: 'T6', truth: ['claims-need-checks'], // avoids: claim, check, guarantee, verify
    question: 'Our readme now promises the program never opens a network connection. Before we ship, what should stand behind a sentence like that?' },
  { id: 'T7', truth: ['2026-09-30-terminal-design'], // avoids: terminal, console, shell, input line
    question: 'Can I type my own commands in the panel at the bottom of the window where an agent\'s output scrolls, the way I would in a normal command window? How does that work?' },
  { id: 'T8', truth: ['2026-09-27-ask-and-wait-design'], // avoids: quest, repository, resume, block
    question: 'An agent working on the web front end finds it needs facts about an API that a different team\'s codebase owns. Instead of guessing, how is it supposed to pause, get the answer, and carry on?' },
  { id: 'T9', truth: ['2026-10-01-tools-design'], // avoids: tool, resource, managed
    question: 'Which copy of git and of Node does the app use for the programs it runs alongside the agents, and can I make it use one I installed myself?' },
  { id: 'T10', truth: ['2026-09-27-in-app-browser-design', '2026-09-28-chromium-host-design'], // avoids: browser MCP, CDP, Chromium
    question: 'When an agent needs to click through a web page and sign in to a site, whose web browser is it driving, and how does it get hold of it?' },
  { id: 'T11', truth: ['model-decoupling'], // avoids: model, provider, tier, AI
    question: 'We want to add "find similar notes" using an embeddings vendor. What rules govern which vendor we pick, and what should happen when none is configured?' },
  { id: 'T12', truth: ['2026-09-22-first-deployment-case-study'], // avoids: deploy, install, case study, checkout
    question: 'The first time the product ran as a real copy on someone\'s computer instead of from the developers\' working folder, what turned out to be broken?' },
];

/** One task of each kind: a design contract, a canon knowledge document, and an evidence record. */
export const PILOT_TASKS = ['T1', 'T5', 'T12'];

/** What every run is asked, around its question: discovery only. */
export function taskPrompt(question) {
  return [
    'This is a discovery task. Find and read the documents in this repository that govern the question below, then',
    'say in two lines what they require, and name the documents you used. Change nothing.',
    '',
    `Question: ${question}`,
  ].join('\n');
}

/** The canary's question: the session reports what it was handed, and the stream says the same. */
export const CANARY_PROMPT = [
  'Without calling any tool, list what your context holds, one line per item: every instruction file (CLAUDE.md,',
  'AGENTS.md, rules, memory or any other) with its first heading; every tool; every MCP server; every skill; and',
  'any text a hook added to this prompt. Write NONE for an empty list.',
].join('\n');

// ---------------------------------------------------------------------------------------------------------
// The corpus
