// Roles bound to paths: `documents` in the manifest, as the CLI reads it (DOC3; D122, the development
// documents design §2.7).
//
// A table the service's reader (DOC5) can hold its own against, row for row: what each declaration
// becomes, and what is refused at the edge, before a single path is planned. Each row was written before
// the code, and watched failing.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { captureError, makeFixture } from './_fixture.ts';
import { readManifest } from '../src/config.ts';
import { DaorisError } from '../src/errors.ts';

// ---------------------------------------------------------------- the manifest's reading

const MANIFEST_ROWS: { name: string; documents: unknown; harness?: string; read: unknown }[] = [
  { name: 'absent is none', documents: undefined, read: [] },
  { name: 'null is none', documents: null, read: [] },
  { name: 'empty is none', documents: {}, read: [] },
  {
    name: 'a string is a path with no ceiling; an object a path and a ceiling; brief and room a ceiling alone',
    documents: {
      backlog: { path: 'TASKS.md', words: 6600 },
      room: { words: 600 },
      decisions: 'docs/DECISIONS.md',
      brief: { words: 1300 },
      gates: './daoris.gates.json',
      archive: 'docs\\archive\\',
    },
    // In the roles' own order, whatever order the manifest wrote them in; paths as the manifest spells
    // a declared path (forward slashes, no `./`, no trailing slash).
    read: [
      { role: 'brief', path: null, words: 1300 },
      { role: 'room', path: null, words: 600 },
      { role: 'decisions', path: 'docs/DECISIONS.md', words: null },
      { role: 'backlog', path: 'TASKS.md', words: 6600 },
      { role: 'archive', path: 'docs/archive', words: null },
      { role: 'gates', path: 'daoris.gates.json', words: null },
    ],
  },
  { name: 'an object with a path and no ceiling', documents: { router: { path: 'docs/README.md' } }, read: [{ role: 'router', path: 'docs/README.md', words: null }] },
];

const REFUSED_ROWS: { name: string; documents?: unknown; raw?: string; harness?: string; says: RegExp }[] = [
  { name: 'a list', documents: ['docs/DECISIONS.md'], says: /documents as \["docs\/DECISIONS\.md"\] — it is a map from a role to its path/ },
  { name: 'an unknown role, naming the known ones', documents: { roadmap: 'ROADMAP.md' }, says: /'roadmap', which is not a role daoris knows — the roles are brief, room, router, decisions, backlog, archive, fixes, changelog, glossary, gates/ },
  { name: 'a tier the index already lists', documents: { knowledge: 'docs/notes' }, says: /'knowledge', which the index lists from \.claude\/knowledge\/ — it is not declared here/ },
  { name: 'a skill, the same', documents: { skill: 'x' }, says: /'skill', which the index lists from \.claude\/skills\/ — it is not declared here/ },
  { name: 'a path on the brief', documents: { brief: 'BRIEF.md' }, says: /documents\.brief takes a ceiling and no path/ },
  { name: 'a path on the room', documents: { room: { path: 'src', words: 600 } }, says: /documents\.room takes a ceiling and no path/ },
  { name: 'a number for a path', documents: { decisions: 7 }, says: /documents\.decisions is 7 — a document is its path, or \{ "path": "<path>", "words": <ceiling> \}/ },
  { name: 'an object with no path', documents: { decisions: { words: 10 } }, says: /documents\.decisions has no path/ },
  { name: 'a field nobody reads', documents: { backlog: { path: 'TASKS.md', word: 10 } }, says: /documents\.backlog has 'word', which is not a field/ },
  { name: 'a ceiling that is not a whole number above zero', documents: { backlog: { path: 'TASKS.md', words: 0 } }, says: /documents\.backlog has words 0 — a ceiling is a whole number of words above zero/ },
  { name: 'a ceiling as text', documents: { brief: { words: '1500' } }, says: /documents\.brief has words "1500"/ },
  { name: 'a path that climbs out', documents: { decisions: '../elsewhere/DECISIONS.md' }, says: /documents\.decisions '\.\.\/elsewhere\/DECISIONS\.md' leaves the repository/ },
  { name: 'an absolute path', documents: { decisions: '/srv/DECISIONS.md' }, says: /leaves the repository/ },
  { name: 'a drive path', documents: { decisions: 'C:/notes/DECISIONS.md' }, says: /leaves the repository/ },
  { name: "the repository's root", documents: { decisions: './' }, says: /documents\.decisions is the repository's root/ },
  { name: 'inside the doctrine target', documents: { glossary: '.claude/knowledge/glossary.md' }, says: /documents\.glossary '\.claude\/knowledge\/glossary\.md' sits inside \.claude, where daoris writes the on-demand tiers/ },
  { name: 'inside the mirror root', harness: 'agents', documents: { fixes: '.claude/skills/fixes.md' }, says: /sits inside \.claude\/skills, where daoris writes the mirror for Claude Code/ },
  {
    name: 'a role declared twice, which JSON would keep the last of silently',
    raw: '{ "source": "s", "packs": [], "documents": { "decisions": "a.md", "backlog": "b.md", "decisions": "c.md" } }',
    says: /declares documents\.decisions twice/,
  },
  {
    name: 'documents declared twice',
    raw: '{ "source": "s", "documents": { "decisions": "a.md" }, "packs": [], "documents": { "backlog": "b.md" } }',
    says: /declares documents twice/,
  },
];

function manifestAt(tag: string, text: string) {
  const fx = makeFixture(`documents-manifest-${tag}`);
  fx.write('daoris.json', text);
  return fx;
}

for (const [index, row] of MANIFEST_ROWS.entries()) {
  test(`documents in the manifest: ${row.name}`, () => {
    const fields: Record<string, unknown> = { source: 's', packs: [] };
    if (row.documents !== undefined) fields.documents = row.documents;
    const fx = manifestAt(`read-${index}`, JSON.stringify(fields));
    assert.deepEqual(readManifest(fx.root).documents, row.read);
    fx.cleanup();
  });
}

for (const [index, row] of REFUSED_ROWS.entries()) {
  test(`documents in the manifest, refused: ${row.name}`, () => {
    const fields: Record<string, unknown> = { source: 's', packs: [], documents: row.documents };
    if (row.harness) Object.assign(fields, { harness: row.harness, target: '.agents' });
    const fx = manifestAt(`refused-${index}`, row.raw ?? JSON.stringify(fields));
    const error = captureError(() => readManifest(fx.root));
    assert.ok(error instanceof DaorisError, String(error));
    assert.equal(error.exitCode, 2, 'a manifest the tool cannot honour is a tool error, as a room that escapes is');
    assert.match(error.message, row.says);
    fx.cleanup();
  });
}
