import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync, readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { planPost, planStatus, readQuests, renderQuest, questId, QUESTS_HEADING } from '../src/quest.ts';
import { DaorisError } from '../src/errors.ts';

const fixtures = join(dirname(dirname(fileURLToPath(import.meta.url))), '_fixtures', 'quest');

function repo(backlog: string | null) {
  const root = join(fixtures, `r${Math.random().toString(36).slice(2, 8)}`);
  mkdirSync(root, { recursive: true });
  if (backlog !== null) writeFileSync(join(root, 'TASKS.md'), backlog, 'utf8');
  return root;
}

const args = { from: 'Asker', title: 'Adopt the canon', body: 'Four rules collide.', date: '2026-08-05' };

test.after(() => rmSync(fixtures, { recursive: true, force: true }));

/**
 * The checkbox is the coarse state every backlog here already reads. That is what lets a repository
 * which knows nothing about Daoris still handle a quest correctly — it looks like an ordinary task.
 */
test('a quest is an ordinary checklist item, with its detail in the italic line', () => {
  const entry = renderQuest(args);

  assert.match(entry, /^- \[ \] \*\*Adopt the canon\*\* `#[0-9a-f]{6}`/);
  assert.match(entry, /Quest from `Asker` · filed 2026-08-05 · \*\*open\*\*/);
});

test('finishing or declining ticks the box; taking does not', () => {
  assert.match(renderQuest({ ...args, status: 'done' }), /^- \[x\]/);
  assert.match(renderQuest({ ...args, status: 'declined' }), /^- \[x\]/);
  assert.match(renderQuest({ ...args, status: 'taken' }), /^- \[ \]/);
});

test('the id is stable for the same quest and differs for another', () => {
  assert.equal(questId(args), questId({ ...args }));
  assert.notEqual(questId(args), questId({ ...args, title: 'Something else' }));
});

test('posting the same quest twice is refused rather than duplicated', () => {
  const root = repo('# TASKS\n');
  const { content } = planPost({ targetRoot: root, ...args });
  writeFileSync(join(root, 'TASKS.md'), content, 'utf8');

  assert.throws(() => planPost({ targetRoot: root, ...args }), DaorisError);
});

test('the section is created without disturbing the repository\'s own work', () => {
  const root = repo('# TASKS\n\n## Backlog\n\n- [ ] something of their own\n');

  const { content } = planPost({ targetRoot: root, ...args });

  assert.ok(content.includes('- [ ] something of their own'));
  assert.ok(content.indexOf('something of their own') < content.indexOf(QUESTS_HEADING));
});

test('headings after the section stay where they were', () => {
  const root = repo(`# TASKS\n\n${QUESTS_HEADING}\n\n- [ ] **First** \`#aaaaaa\`\n  one\n\n## Their theme\n\n- [ ] theirs\n`);

  const { content } = planPost({ targetRoot: root, ...args });

  assert.ok(content.includes('## Their theme'));
  assert.ok(content.trimEnd().endsWith('- [ ] theirs'));
});

test('quests are parsed back out with their status and asker', () => {
  const root = repo('# TASKS\n');
  const { content } = planPost({ targetRoot: root, ...args });

  const quest = readQuests(content)[0]!;

  assert.equal(quest.title, 'Adopt the canon');
  assert.equal(quest.from, 'Asker');
  assert.equal(quest.status, 'open');
});

test('taking a quest moves its status and leaves the box unticked', () => {
  const root = repo('# TASKS\n');
  const posted = planPost({ targetRoot: root, ...args });
  writeFileSync(join(root, 'TASKS.md'), posted.content, 'utf8');

  const { content } = planStatus({ root, id: posted.id, status: 'taken', note: null, date: '2026-08-06' });

  const quest = readQuests(content)[0]!;
  assert.equal(quest.status, 'taken');
  assert.match(quest.text, /^- \[ \]/);
  assert.match(quest.text, /2026-08-06/);
});

/** The reason is the part the asker can act on; a bare refusal tells them nothing. */
test('declining records the reason and ticks the box', () => {
  const root = repo('# TASKS\n');
  const posted = planPost({ targetRoot: root, ...args });
  writeFileSync(join(root, 'TASKS.md'), posted.content, 'utf8');

  const { content } = planStatus({
    root, id: posted.id, status: 'declined', note: 'That rule is deliberately local here.', date: '2026-08-06',
  });

  const quest = readQuests(content)[0]!;
  assert.equal(quest.status, 'declined');
  assert.match(quest.text, /^- \[x\]/);
  assert.match(quest.text, /deliberately local here/);
});

test('an unknown id names what the repository actually holds', () => {
  const root = repo('# TASKS\n');
  const posted = planPost({ targetRoot: root, ...args });
  writeFileSync(join(root, 'TASKS.md'), posted.content, 'utf8');

  assert.throws(
    () => planStatus({ root, id: 'zzzzzz', status: 'taken', note: null, date: '2026-08-06' }),
    (error) => error instanceof DaorisError && error.message.includes(posted.id));
});

test('a repository with no backlog is reported rather than given one', () => {
  assert.throws(() => planPost({ targetRoot: repo(null), ...args }), DaorisError);
});

test('the plan is separate from the write', () => {
  const root = repo('# TASKS\n');
  const before = readFileSync(join(root, 'TASKS.md'), 'utf8');

  planPost({ targetRoot: root, ...args });

  assert.equal(readFileSync(join(root, 'TASKS.md'), 'utf8'), before);
});
