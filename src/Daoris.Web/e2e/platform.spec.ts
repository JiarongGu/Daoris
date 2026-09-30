import { test, expect, type Page } from '@playwright/test';
import { execSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = dirname(dirname(dirname(dirname(fileURLToPath(import.meta.url)))));

// The platform, driven over the example family (D39, D42) — the shipped bundle, the real host, the
// real QuestExchange. One store per run; the tests tell one story in order.
test.describe.configure({ mode: 'serial' });

/** The sidebar item, not any same-named button inside a view. */
const nav = (page: Page, name: string) =>
  page.getByRole('navigation').getByRole('button', { name });

test('the overview shows the example family', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();
  await expect(page.getByText('of 2 in the family')).toBeVisible();
  await expect(page.getByText('engine').first()).toBeVisible();
  await expect(page.getByText('game').first()).toBeVisible();
});

test('repositories lists both members with their declarations', async ({ page }) => {
  await page.goto('/');
  await nav(page, 'Repositories').click();
  await expect(page.getByText('the engine runtime — simulation, rendering, assets')).toBeVisible();
  await expect(page.getByText('a playtest finding, with the reproduction')).toBeVisible();
});

/**
 * **A quest carries a link and files, and the files stay on this machine** (D65 §2). Composed through
 * the real drawer, kept by the real host under its home, opened through the host's own route — and
 * an attached PAGE comes back as a sandboxed download: served from the platform's origin, anything
 * else would be a script with every route this host answers.
 */
test('a quest carries a link and files: kept here, opened here, never run as the platform', async ({ page, request }) => {
  const title = 'Read the media field names from config';
  const png = Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
    'base64');

  await page.goto('/');
  await nav(page, 'Quests').click();
  await page.getByRole('button', { name: 'New quest' }).click();
  await page.getByLabel('from', { exact: true }).click();
  await page.getByRole('option', { name: 'game' }).click();
  await page.getByLabel('to', { exact: true }).click();
  await page.getByRole('option', { name: 'engine' }).click();
  await page.getByLabel('what is wanted, in one line').fill(title);
  await page.getByLabel('why, and the evidence').fill('The video and image field names are hard-coded; the ticket and a screenshot say where.');
  await page.getByLabel(/^links/).fill('https://tickets.example/T-7');
  await page.getByLabel('choose files…').setInputFiles([
    { name: 'before.png', mimeType: 'image/png', buffer: png },
    { name: 'page.html', mimeType: 'text/html', buffer: Buffer.from('<script>parent.document.title = "owned"</script>') },
  ]);
  await page.getByRole('button', { name: 'Publish quest' }).click();
  // The service's sentence, as it reads: a toast sets its backticked names as code, so the words
  // are the service's and the backticks are gone (`Inline`).
  await expect(page.getByText(/Published quest #[0-9a-f]{12} to engine/).first()).toBeVisible();

  // The card counts what it carries; the drawer holds the things themselves.
  await expect(page.getByLabel('1 link · 2 files').first()).toBeVisible();
  await page.getByText(title).first().click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('link', { name: /tickets\.example\/T-7/ }))
    .toHaveAttribute('href', 'https://tickets.example/T-7');

  // The picture is the real bytes, through the host's route — decoded, so it is the file and not an
  // error page wearing an image tag.
  const picture = dialog.getByRole('img', { name: 'before.png' });
  await expect(picture).toBeVisible();
  await expect.poll(() => picture.evaluate((image: HTMLImageElement) => image.naturalWidth)).toBe(1);

  // 🔴 Where it lies is never shown: the host answers this machine a path, and the page must not
  // print it.
  await expect(dialog).not.toContainText('_fixtures');
  await expect(dialog).not.toContainText('attachments');

  // 🔴 An attached page is a sandboxed download, never a document on the platform's origin.
  const href = await dialog.getByRole('link', { name: /page\.html/ }).getAttribute('href');
  const served = await request.get(href!);
  expect(served.status()).toBe(200);
  expect(served.headers()['content-security-policy']).toContain('sandbox');
  expect(served.headers()['x-content-type-options']).toBe('nosniff');
  expect(served.headers()['content-disposition']).toContain('attachment');

  // Leave the family as it was found: the suite is serial, and the next test expects nothing open.
  await dialog.getByRole('button', { name: 'done', exact: true }).click();
  await expect(page.getByText(/is now Done/).first()).toBeVisible();
});

/**
 * **A chain moves on when its quest closes done** (D65 §4) — over the real host, whose close publishes
 * the next step in the same transaction. The drawer shows what is coming before it comes, the close's
 * toast names the step it published, and the step says which quest it follows.
 */
test('a chain moves on when its quest closes done', async ({ page, request }) => {
  const published = await request.post('/api/quests', {
    data: {
      from: 'game', to: 'engine', title: 'Develop the streaming cap', body: 'Cap hydration per frame.',
      then: [{ to: 'engine', title: 'Verify {parent} in a playtest', body: 'Stream the world and watch for seams.' }],
    },
  });
  const parent = (await published.json() as { quest: { id: string } }).quest.id;

  await page.goto('/');
  await nav(page, 'Quests').click();
  await page.getByText('Develop the streaming cap').first().click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByText(/Verify \{parent\} in a playtest/)).toBeVisible();
  await dialog.getByRole('button', { name: 'done', exact: true }).click();
  await expect(page.getByText(/Then: published #[0-9a-f]{12} to engine/).first()).toBeVisible();

  // The step is an ordinary open quest, named with the id of the one it follows.
  const step = page.getByText(`Verify #${parent} in a playtest`).first();
  await expect(step).toBeVisible();
  await expect(page.getByText(`follows #${parent}`).first()).toBeVisible();

  // MAP1: the step's drawer carries the chain — the quest it follows, done, and a door back to it;
  // and from there, a door forward again. Real records, the real host's parent link.
  await step.click();
  const chain = () => page.getByRole('dialog').getByRole('region', { name: 'How this work ran' });
  await expect(chain().getByText('this quest')).toBeVisible();
  await chain().getByRole('button', { name: 'Develop the streaming cap' }).click();
  await expect(page.getByRole('dialog').getByRole('heading', { name: 'Develop the streaming cap' })).toBeVisible();
  await chain().getByRole('button', { name: `Verify #${parent} in a playtest` }).click();

  // Leave the family as it was found: the suite is serial, and a later test expects nothing open.
  await page.getByRole('dialog').getByRole('button', { name: 'done', exact: true }).click();
  await expect(page.getByText(/is now Done/).first()).toBeVisible();
});

/**
 * **The workspace map** (MAP2, D67 §3) — a view of its own, over the real host: the family's two
 * repositories as nodes, and the quests the tests above sent from the game to the engine as one line.
 */
test('the map draws the family and the quests that moved between them', async ({ page }) => {
  await page.goto('/');
  await nav(page, 'Map').click();
  await expect(page.getByRole('heading', { name: 'Map' })).toBeVisible();
  await expect(page.getByRole('button', { name: /^engine, \d+ open/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /^game, \d+ open/ })).toBeVisible();

  // The line's count sits on the line, and is where a person aims.
  await page.getByRole('button', { name: /quests? from game to engine$/ }).locator('circle').click();
  await expect(page.getByText('game → engine')).toBeVisible();
  await expect(page.getByText('Develop the streaming cap')).toBeVisible();
});

/**
 * **One level in: a repository's own code map** (MAP3a) — over the real host, read from the example
 * engine's committed `docs/code-map.json`. The game keeps none, and says so.
 */
test('a repository opens its own code map, and one without says where it would go', async ({ page }) => {
  await page.goto('/');
  await nav(page, 'Map').click();
  await page.getByRole('button', { name: /^engine, \d+ open/ }).click();
  await page.getByRole('button', { name: 'Open code map' }).click();

  await expect(page.getByRole('heading', { name: 'engine: code map' })).toBeVisible();
  await page.getByRole('button', { name: 'chunks, depends on 1' }).click();
  await expect(page.getByText('streams the world in chunks, within a per-frame budget')).toBeVisible();
  // Its neighbours are doors: what uses it, and what it uses.
  await page.getByRole('button', { name: 'media', exact: true }).click();
  await expect(page.getByText('loads video and image fields named by the media config')).toBeVisible();

  await page.getByRole('button', { name: 'Back to the workspace' }).click();
  await page.getByRole('button', { name: /^game, \d+ open/ }).click();
  await page.getByRole('button', { name: 'Open code map' }).click();
  await expect(page.getByText('game keeps no code map')).toBeVisible();
});

test('a quest travels: composed, published, taken, finished', async ({ page }) => {
  await page.goto('/');
  await nav(page, 'Quests').click();
  await page.getByRole('button', { name: 'New quest' }).click();

  await page.getByLabel('from', { exact: true }).click();
  await page.getByRole('option', { name: 'game' }).click();
  await page.getByLabel('to', { exact: true }).click();
  await page.getByRole('option', { name: 'engine' }).click();
  await page.getByLabel('what is wanted, in one line').fill('Expose a streaming budget on the chunk API');
  await page.getByLabel('why, and the evidence').fill(
    'World streaming needs a per-frame cap; today hydration is unbounded. Evidence: seams whenever more than three chunks hydrate in one frame.',
  );
  await page.getByRole('button', { name: 'Publish quest' }).click();

  // The toast carries the service's sentence verbatim — that text IS the contract. (.first(): Radix
  // renders each toast twice — the visible element and its aria-live announcer.)
  await expect(page.getByText(/Published quest #[0-9a-f]{12} to engine/).first()).toBeVisible();

  // It sits in Open; its card is a door to the detail drawer, where the acting happens.
  await page.getByText('Expose a streaming budget on the chunk API').first().click();
  await page.getByRole('button', { name: 'take', exact: true }).click();
  await expect(page.getByText(/is now Taken/).first()).toBeVisible();

  await page.getByText('Expose a streaming budget on the chunk API').first().click();
  await page.getByRole('button', { name: 'done', exact: true }).click();
  await expect(page.getByText(/is now Done/).first()).toBeVisible();

  // Closed work leaves the default list and returns on request.
  await expect(page.getByText('No open quests anywhere')).toBeVisible();
  await page.getByText('Include closed').click();
  await expect(page.getByText('Expose a streaming budget on the chunk API')).toBeVisible();
});

test('a refusal reaches the person verbatim', async ({ page }) => {
  await page.goto('/');
  await nav(page, 'Quests').click();
  await page.getByRole('button', { name: 'New quest' }).click();

  // Self-addressed: the one refusal the form cannot prevent, because the judgement is the service's.
  await page.getByLabel('from', { exact: true }).click();
  await page.getByRole('option', { name: 'engine' }).click();
  await page.getByLabel('to', { exact: true }).click();
  await page.getByRole('option', { name: 'engine' }).click();
  await page.getByLabel('what is wanted, in one line').fill('x');
  await page.getByLabel('why, and the evidence').fill('y');
  await page.getByRole('button', { name: 'Publish quest' }).click();

  await expect(page.getByText('a quest is work for someone else', { exact: false }).first()).toBeVisible();
});

/**
 * **An ask, from the screen** (INT4c) — the twin of `daoris-driver ask` (D50), over the real host.
 * Opened from the palette, made in the family's one workspace without being asked which, answered by the
 * declarations tier in the service's own words; a proposal accepted is a quest asked BY the ask,
 * carrying its link and its file; and the person closes it with the reason. The kept file is named
 * and never located (D47 §4, D65 §2).
 */
test('an ask is proposed by declarations, published by a person, and closed with its reason (INT4c)', async ({ page }) => {
  const sentence = 'the rendering of the asset pipeline stalls whenever the simulation runs';

  await page.goto('/');
  // A browser on this machine has this door: an ask is this host's HTTP, not the shell's bridge.
  await page.getByRole('button', { name: 'Commands (Ctrl+K)' }).click();
  await page.getByRole('dialog').getByRole('option', { name: /Ask the workspace/ }).click();

  // Every drawer is addressed by its title: one closes as the next opens, and "the dialog" is ambiguous
  // for that moment.
  const composer = page.getByRole('dialog', { name: 'Ask the workspace' });
  await expect(composer).toBeVisible();
  // One workspace held, so it is the workspace — said, and not asked.
  await expect(composer.getByText('Asked in workspace default')).toBeVisible();
  await expect(composer.getByRole('combobox', { name: 'workspace' })).toHaveCount(0);
  await composer.getByLabel('what is wanted, and why').fill(sentence);
  await composer.getByLabel(/^links/).fill('https://tickets.example/T-8');
  await composer.getByLabel('choose files…').setInputFiles([
    { name: 'trace.log', mimeType: 'text/plain', buffer: Buffer.from('frame 212: hydrate stalls 38ms\n') },
  ]);
  await composer.getByRole('button', { name: 'ask', exact: true }).click();

  // The service's sentence, verbatim: which tier answered, what it proposed, and that nothing went out.
  await expect(page.getByText(/Asked as #[0-9a-f]{6} in default — by declarations only; no intake harness ran — proposed, best first: engine/).first()).toBeVisible();

  // The record opens on the answer: the tier in words, the proposal as a verb, the file by name only.
  // (The quest it becomes takes the same words for its title, so each drawer is told apart by what it holds.)
  const record = page.getByRole('dialog', { name: sentence });
  await expect(record.getByRole('region', { name: 'Where it belongs' })).toBeVisible();
  await expect(record.getByText('by declarations only; no intake harness ran')).toBeVisible();
  await expect(record.getByText('trace.log')).toBeVisible();
  await expect(record).not.toContainText('_fixtures');

  await record.getByRole('button', { name: 'publish to engine' }).click();
  await expect(page.getByText(/Published quest #[0-9a-f]{12} to engine/).first()).toBeVisible();

  // The quest it became is a door into the quest's own drawer — once the page holds it — asked BY the
  // ask, carrying its link and its file.
  await record.getByRole('region', { name: 'Became' }).getByRole('button', { name: new RegExp(sentence) }).click();
  const quest = page.getByRole('dialog', { name: sentence });
  await expect(quest.getByRole('region', { name: 'Where it belongs' })).toHaveCount(0);
  await expect(quest.getByText(/^ask #[0-9a-f]{6}$/).first()).toBeVisible();
  await expect(quest.getByRole('link', { name: /tickets\.example\/T-8/ })).toBeVisible();
  await expect(quest.getByRole('link', { name: /trace\.log/ })).toBeVisible();

  // Its only quest done, the ask's work is finished (USE1c): it is DONE, and leaves the list by itself,
  // as a closed ask and a closed quest do. Nobody has to close it, and the family is left as found.
  await quest.getByRole('button', { name: 'done', exact: true }).click();
  await expect(page.getByText(/is now Done/).first()).toBeVisible();
  await expect(page.getByText(/^Asks \(/)).toHaveCount(0);

  // With closed ones included it comes back, wearing its done pill.
  await page.getByText('Include closed').click();
  await expect(page.getByText(/^Asks \(/)).toBeVisible();
  await expect(page.getByText('done', { exact: true }).first()).toBeVisible();
});

/**
 * **An ask that waits on a person is in *What needs you*, and its record says who answered** (INT4d),
 * over the real host. A proposal waits until a person settles it. An intake that parks asking is ONE
 * row, the ask's, because the answer is on the ask. A browser opens the ask's record from the band,
 * and names the intake session there without a door, because a browser has no Sessions. The intake is
 * opened and parked through the doors the driver uses. The driver is not running here, and the
 * record is what a page reads.
 */
test('an ask waiting on a person is in What needs you, and its record names its intake (INT4d)', async ({ page, request }) => {
  const sentence = 'the release notes should explain the streaming budget';
  const asked = await request.post('/api/asks', { data: { workspace: 'default', sentence } });
  expect(asked.ok(), await asked.text()).toBe(true);
  const ask = (await asked.json() as { ask: { id: string; state: string } }).ask;
  expect(ask.state).toBe('Proposed');

  await page.goto('/');
  const band = page.getByRole('region', { name: 'What needs you' });
  const proposed = band.getByRole('button', { name: new RegExp(sentence) });
  await expect(proposed).toContainText('proposed, not yet published');
  await expect(proposed).toContainText('workspace default');
  await proposed.click();
  await expect(page.getByRole('dialog', { name: sentence }).getByRole('region', { name: 'Where it belongs' })).toBeVisible();

  // Its intake opens, works, and parks asking the person.
  const room = join(repoRoot, '_fixtures', 'web-e2e', 'intake', 'default');
  const opened = await request.post('/api/sessions/intake', { data: { ask: ask.id, adapter: 'stub', room } });
  expect(opened.ok(), await opened.text()).toBe(true);
  const session = (await opened.json() as { session: { id: string } }).session;
  for (const state of ['starting', 'working']) {
    await request.post(`/api/sessions/${session.id}/state`, { data: { state } });
  }
  const parked = await request.post(`/api/sessions/${session.id}/state`, {
    data: { state: 'awaiting-person', note: 'published nothing: it asks you rather than guess.' },
  });
  expect(parked.ok(), await parked.text()).toBe(true);

  await page.goto('/');
  const asking = page.getByRole('region', { name: 'What needs you' }).getByRole('button', { name: new RegExp(sentence) });
  await expect(asking).toContainText('its intake asked you');
  await expect(asking).toContainText('published nothing: it asks you rather than guess.');
  // One thing, one row: the parked intake is the ask's row, not a parked session beside it.
  await expect(page.getByRole('region', { name: 'What needs you' }).getByText('parked at a checkpoint')).toHaveCount(0);

  await asking.click();
  const intake = page.getByRole('dialog', { name: sentence }).getByRole('region', { name: 'Intake session' });
  await expect(intake.getByText('awaiting person')).toBeVisible();
  await expect(intake.getByText('stub', { exact: true })).toBeVisible();
  await expect(intake.getByRole('button')).toHaveCount(0);

  // Leave the family as found: the ask closed, and its parked intake ended the way the driver's next
  // tick would end it.
  const closed = await request.post(`/api/asks/${ask.id}/close`, { data: { reason: 'Rehearsed only.' } });
  expect(closed.ok(), await closed.text()).toBe(true);
  await request.post(`/api/sessions/${session.id}/state`, { data: { state: 'stopped', note: 'the person closed it.' } });
});

test('a project created mid-run joins, and the platform shows it (D44)', async ({ page }) => {
  // The lifecycle the next real family needs proven: a project that did not exist when the host
  // started is born, joins through the REAL CLI — init, declare, sync, check, connect — and becomes a
  // member in the UI, quest-addressable at once. The host roots at a scratch family, so nothing
  // tracked is touched.
  //
  // `connect` is what JOINS (D48 §3): the registry is an explicit list now, so being in the folder is
  // not being a member. Before that it was the scan that admitted this newcomer — which is exactly the
  // silence the managed registry removed, and this test was reading it as a pass.
  const newcomer = join(repoRoot, '_fixtures', 'web-e2e', 'family', 'newcomer');
  const cli = join(repoRoot, 'src', 'Daoris.Cli', 'bin', 'daoris.mjs');
  mkdirSync(newcomer, { recursive: true });
  writeFileSync(join(newcomer, 'README.md'), '# newcomer\n\nBorn during the test run.\n');
  execSync(`node "${cli}" init`, { cwd: newcomer });
  const manifestPath = join(newcomer, 'daoris.json');
  const manifest = JSON.parse(readFileSync(manifestPath, 'utf8')) as { domain: unknown };
  manifest.domain = {
    summary: 'Born during the test run.',
    owns: ['its own birth'],
    accepts: ['a first quest'],
  };
  writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
  execSync(`node "${cli}" sync`, { cwd: newcomer });
  execSync(`node "${cli}" check`, { cwd: newcomer });
  execSync(`node "${cli}" connect`, {
    cwd: newcomer,
    env: { ...process.env, DAORIS_SERVICE_URL: 'http://localhost:5196' },
  });

  await page.goto('/');
  await nav(page, 'Repositories').click();
  await expect(page.getByText('Born during the test run.')).toBeVisible();

  await nav(page, 'Quests').click();
  await page.getByRole('button', { name: 'New quest' }).click();
  await page.getByLabel('from', { exact: true }).click();
  await page.getByRole('option', { name: 'game' }).click();
  await page.getByLabel('to', { exact: true }).click();
  await page.getByRole('option', { name: 'newcomer' }).click();
  await page.getByLabel('what is wanted, in one line').fill('A first quest for the newcomer');
  await page.getByLabel('why, and the evidence').fill('Joining means being askable — prove it.');
  await page.getByRole('button', { name: 'Publish quest' }).click();
  await expect(page.getByText(/Published quest #[0-9a-f]{12} to newcomer/).first()).toBeVisible();
});

/**
 * The workspace scope (WSP5; D48 §4), over the real host and the real bundle.
 *
 * The family so far is ONE circle, so the switcher has been absent from every test above — that
 * absence is a claim too, and it is asserted first. Then the newcomer is re-wired into a second
 * circle through the same door the desktop's management form uses, and the console gains the scope
 * control: every workspace, stated with its count; one circle chosen, and every view scoped to it;
 * the choice surviving a reload; every again afterwards. Re-wired back at the end, so the tests after
 * this inherit the family they were written against.
 */
test('the console scopes by workspace once the family holds two (WSP5)', async ({ page, request }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();
  await expect(page.getByRole('combobox', { name: 'workspace' })).toHaveCount(0);

  const wired = await request.post('/api/registry/newcomer/workspace', { data: { workspace: 'studio' } });
  expect(wired.ok(), await wired.text()).toBe(true);

  await page.goto('/');
  const scope = page.getByRole('combobox', { name: 'workspace' });
  await expect(scope).toHaveText(/every workspace · 2/);
  await expect(page.getByText('of 3 in the family')).toBeVisible();

  await scope.click();
  await page.getByRole('option', { name: 'studio' }).click();
  await expect(scope).toHaveText('studio');
  await expect(page.getByText('of 1 in the family')).toBeVisible();
  await nav(page, 'Repositories').click();
  await expect(page.getByText('Born during the test run.')).toBeVisible();
  await expect(page.getByText('the engine runtime — simulation, rendering, assets')).toHaveCount(0);

  // Remembered per browser, like the language: a reload keeps the circle.
  await page.reload();
  await expect(page.getByRole('combobox', { name: 'workspace' })).toHaveText('studio');

  await page.getByRole('combobox', { name: 'workspace' }).click();
  await page.getByRole('option', { name: /every workspace/ }).click();
  await nav(page, 'Repositories').click();
  await expect(page.getByText('the engine runtime — simulation, rendering, assets')).toBeVisible();

  const back = await request.post('/api/registry/newcomer/workspace', { data: { workspace: 'default' } });
  expect(back.ok(), await back.text()).toBe(true);
});

test("a driven session's record reaches the drawer (D46)", async ({ page, request }) => {
  // The driver is not running here — the RECORD is service state, so seeding it through the same
  // doors the driver uses is exactly what the platform will see in real use: the session surface is
  // read-only in a browser, and the controls live where a driver is attached.
  const published = await request.post('/api/quests', {
    data: {
      from: 'game', to: 'engine',
      title: 'Drive the streaming budget work',
      body: 'Seeded to prove the session surface renders the record.',
    },
  });
  const quest = (await published.json() as { quest: { id: string } }).quest;
  const opened = await request.post('/api/sessions', { data: { quest: quest.id, adapter: 'stub' } });
  const session = (await opened.json() as { session: { id: string } }).session;
  await request.post(`/api/sessions/${session.id}/state`, { data: { state: 'starting' } });
  await request.post(`/api/sessions/${session.id}/state`, {
    data: { state: 'working', note: 'the process is alive' },
  });

  await page.goto('/');
  await nav(page, 'Quests').click();

  // The card wears the live session's state beside the quest's own status…
  const card = page.getByText('Drive the streaming budget work').first();
  await expect(card).toBeVisible();
  await card.click();

  // …and the drawer carries the record: state, id · adapter, and the driver's note, verbatim.
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByText('working')).toBeVisible();
  await expect(dialog.getByText(new RegExp(`${session.id} · stub`))).toBeVisible();
  await expect(dialog.getByText('the process is alive')).toBeVisible();

  // Read-only is the arc's central claim: the record renders, the control does not — stop reaches a
  // PROCESS, and a browser has none to reach (D46 §6).
  await expect(dialog.getByRole('button', { name: 'Stop session' })).toHaveCount(0);
});

/**
 * Which tool, and which account, did the work (D49 §4).
 *
 * The inner loop holds the formatter with a mocked record; this holds the whole chain over the real
 * artefact — the request contract, the ledger, the two store columns, the response shape, and the
 * drawer's line — because every one of those is a place a nullable field quietly stops arriving and
 * no test that mocks the host would notice.
 */
test('a session record names the tool version and the account it ran as (D49 §4)', async ({ page, request }) => {
  // `game`, not `engine`: the test above leaves a working session holding `engine`, and one session
  // per repository is the whole point of that lock. A suite that runs in order inherits its own state.
  const published = await request.post('/api/quests', {
    data: {
      from: 'engine', to: 'game',
      title: 'Prove the record names its tool',
      body: 'Seeded to prove the harness version and the profile survive the round trip.',
    },
  });
  const quest = (await published.json() as { quest: { id: string } }).quest;

  // Through the same door the driver uses: it observes both before it spawns, because the service has
  // no binaries to look at (D46 §7) and records what it is told.
  const opened = await request.post('/api/sessions', {
    data: {
      quest: quest.id, adapter: 'stub',
      harnessVersion: 'stub-harness 9.9.9', profile: 'work',
    },
  });
  // Asserted before it is used: a refusal here is a sentence worth reading, and `undefined.id` is not.
  expect(opened.ok(), await opened.text()).toBe(true);
  const session = (await opened.json() as { session: { id: string } }).session;
  await request.post(`/api/sessions/${session.id}/state`, { data: { state: 'starting' } });
  await request.post(`/api/sessions/${session.id}/state`, { data: { state: 'working' } });

  await page.goto('/');
  await nav(page, 'Quests').click();
  await page.getByText('Prove the record names its tool').first().click();

  const dialog = page.getByRole('dialog');
  await expect(dialog.getByText(new RegExp(`${session.id} · stub · stub-harness 9\\.9\\.9 · as work`))).toBeVisible();
});

/**
 * **A browser must never learn what is installed on somebody's machine, or which accounts it holds.**
 *
 * The roster and the per-conversation profile picker ride the shell's bridge and have no HTTP route
 * at all (D47 §4) — a profile home is a filesystem path. In a browser the bridge is absent, so the
 * queries never fire and the surfaces never render; asserting that here, over the real bundle, is the
 * only place the guarantee is checked against a real browser rather than against a mock that was told
 * to be absent.
 */
test('a browser learns nothing about this machine’s harnesses (D49 §4)', async ({ page }) => {
  await page.goto('/');

  // Settings is here (D66) — a browser has appearance to set — and it holds NOTHING of a machine:
  // the machine's half is absent, not disabled. Asserted on the page itself, after checking the page
  // really is Settings, because an absence asserted on the wrong page is a check that cannot fail.
  await nav(page, 'Settings').click();
  await expect(page.getByRole('radiogroup', { name: 'Theme' })).toBeVisible();
  // 🔴 REV3: these used to look for a "This machine" heading and a "Harnesses" string that no longer
  // exist anywhere, on the Appearance page where no machine setting would render anyway — so they
  // passed whatever a browser was shown. The domain list is what a browser is OFFERED, so it is
  // asserted whole: a machine domain appearing here fails this line.
  const domains = page.getByRole('navigation', { name: 'Settings domains' }).getByRole('button');
  // Setup leads (SETUP1a, D97; named so by NAME1b), holding in a browser only the registry's step.
  await expect(domains).toHaveText([/Setup/, /Appearance/, /AI features/, /Workspace/]);

  // Workspace is the one domain with a machine half (its wiring), so it is opened and that half's
  // absence asserted where it would render, after its browser half is seen.
  await domains.filter({ hasText: 'Workspace' }).click();
  await expect(page.getByText('default').first()).toBeVisible();
  await expect(page.getByText(/remotes\.json|harnesses\.json|driver\.json/)).toHaveCount(0);

  await nav(page, 'Repositories').click();
  await expect(page.getByText('the engine runtime — simulation, rendering, assets')).toBeVisible();
  // Nothing that would name a configuration home: a registration's root is a machine path, and a
  // browser on the host's own machine is still not told another's (D47 §4).
  await expect(page.getByText(/[\\/]harnesses[\\/]/)).toHaveCount(0);
});

/**
 * Daoris's own AI (AGT6): a browser is told which tier answers its search — the service's own words,
 * over the real bundle and the real host — and nothing of the intake, which is a machine's
 * `driver.json` (D47 §4). The tier on the status bar leads there.
 */
test('a browser is told which tier answers search, and nothing of the intake (AGT6)', async ({ page, request }) => {
  // Whatever this host answers, asked first: the page must say THAT, verbatim, not a tier of its own.
  const status = await (await request.get('/api/status')).json() as { tier: string; note?: string | null };
  await page.goto('/');

  await page.getByLabel('state of this machine').getByRole('button', { name: 'recall' }).click();
  await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
  await expect(page.getByText('AI features')).toBeVisible();
  await expect(page.getByText('Search and convergence')).toBeVisible();
  await expect(page.getByText(status.tier, { exact: true }).first()).toBeVisible();
  if (status.note) await expect(page.getByText(status.note, { exact: true })).toBeVisible();
  await expect(page.getByText(/DAORIS_EMBED_MODEL \(a model's name\)/)).toBeVisible();

  // The intake is absent, not disabled: no row, no control, no agent named.
  await expect(page.getByText('Intake', { exact: true })).toHaveCount(0);
  await expect(page.getByRole('combobox', { name: 'the intake agent' })).toHaveCount(0);
});

/**
 * The Work frame is not rendered in a browser at all (D55), which is the strongest form the
 * disclosure rule takes: its centre is a stream, its rows carry tree paths, and its panel is a
 * transcript — none of which may leave the machine that produced them (D47 §4). Playwright holds
 * the NEGATIVE, over the shipped bundle, because the positive is only reachable from the desktop.
 */
test('a browser has no Sessions, and no mode to switch (D55, D66)', async ({ page }) => {
  await page.goto('/');

  // One navigation, and Sessions is not on it — absent rather than disabled.
  await expect(page.getByRole('group', { name: /mode/i })).toHaveCount(0);
  await expect(nav(page, 'Sessions')).toHaveCount(0);
  await expect(nav(page, 'Quests')).toBeVisible();

  // A browser that remembers Sessions still lands on Overview: the fallback is not cosmetic.
  await page.evaluate(() => window.localStorage.setItem('daoris.view', 'sessions'));
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'sessions' })).toHaveCount(0);
  await expect(page.getByLabel('panel height')).toHaveCount(0);
  await expect(page.getByLabel('message')).toHaveCount(0);
  await page.evaluate(() => window.localStorage.removeItem('daoris.view'));

  // The status bar IS here — it belongs to the application — and it says what a browser is.
  await expect(page.getByLabel('state of this machine')).toContainText('none here');
  // …and whether this circle syncs, which this machine's host answers over HTTP (SYNC6b): the example
  // family has no remote, so it is local — said, rather than left out for want of a bridge.
  await expect(page.getByLabel('state of this machine')).toContainText('local only');
  await expect(page.getByRole('button', { name: 'sync' })).toHaveCount(0);

  // And no review (SURF6). A diff is read off a checkout on the machine that ran the session, so a
  // browser has no door onto one — the dock that would hold it is part of Sessions, and Sessions is
  // not rendered here at all.
  await expect(page.getByRole('tab', { name: 'Review' })).toHaveCount(0);
  await expect(page.getByRole('tablist', { name: 'right side bar' })).toHaveCount(0);
});

/**
 * A secondary window's ROUTE is in the same bundle a browser is served (SURF8) — that is what makes
 * the monitor cost the components the Work frame already has. So the URL is reachable here, and the
 * window must not be: both of them are a rail and a live stream, and a stream has no HTTP route at
 * all (D47 §4). A pasted link lands on the platform rather than on an empty imitation of a desktop.
 */
test('a browser that asks for a secondary window gets the platform (SURF8)', async ({ page }) => {
  await page.goto('/?window=monitor');

  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Monitor' })).toHaveCount(0);

  // Including one naming a session, which is the shape somebody would actually paste.
  await page.goto('/?window=session:a1b2c3d4');
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();
  await expect(page.getByText('No record of that session')).toHaveCount(0);
});

/**
 * The command palette lists nothing a browser cannot do (SURF9). A palette is a PROMISE that what it
 * lists can be done, so the disclosure boundary is enforced by omission rather than by a disabled
 * row — and this is the assertion that the omission actually reaches the page, not just the registry.
 */
test('the palette offers a browser nothing that needs this machine (SURF9)', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'Commands (Ctrl+K)' }).click();

  const palette = page.getByRole('dialog');
  await expect(palette).toBeVisible();

  // The views a browser has are here — every one but the view already in front of the person
  // (Overview, on landing) — Settings among them, since a browser has appearance to set (D66).
  await expect(palette.getByRole('option', { name: /Quests/ })).toBeVisible();
  await expect(palette.getByRole('option', { name: /Search/ })).toBeVisible();
  await expect(palette.getByRole('option', { name: /Settings/ })).toBeVisible();
  await expect(palette.getByRole('option', { name: /^Overview/ })).toHaveCount(0);

  // Nothing that needs a shell is — not disabled, ABSENT.
  await expect(palette.getByRole('option', { name: /Sessions/ })).toHaveCount(0);
  await expect(palette.getByRole('option', { name: /Start a session/ })).toHaveCount(0);
  await expect(palette.getByRole('option', { name: /Review what/ })).toHaveCount(0);
  // A window is the shell's to open, so neither of SURF8's is offered here.
  await expect(palette.getByRole('option', { name: /monitor window/ })).toHaveCount(0);
  await expect(palette.getByRole('option', { name: /its own window/ })).toHaveCount(0);

  // It opens on the keyboard too, which is the affordance it exists to be.
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.keyboard.press('Control+k');
  await expect(page.getByRole('dialog')).toBeVisible();
});

/**
 * The chrome holds its shape on a narrow window (SURF10/SURF7).
 *
 * This is here because it was BROKEN and nothing noticed. The 15rem sidebar the activity bar
 * replaced turned into a top bar under 768px (D41 §2), and the rule that stacked it outlived it:
 * at 686px the 48px icon rail became a 276px-tall column ABOVE the content, leaving the page 105px.
 * Neither suite could see it — vitest has no layout and every other case here runs at one width —
 * so the viewport is the assertion.
 */
test('the chrome stays beside the content on a narrow window, never above it', async ({ page }) => {
  await page.setViewportSize({ width: 680, height: 800 });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();

  const bar = await page.getByRole('navigation', { name: 'Views' }).boundingBox();
  const main = await page.locator('main').boundingBox();
  if (!bar || !main) throw new Error('the activity bar and the content column must both be laid out');

  // Beside, not above: the content starts to the RIGHT of the rail and at the same height.
  expect(main.x).toBeGreaterThanOrEqual(bar.x + bar.width - 1);
  expect(Math.abs(main.y - bar.y)).toBeLessThan(4);
  // And the rail is still a rail rather than a block that ate the window.
  expect(bar.width).toBeLessThan(60);
  // The content gets the height, which is the thing the bug actually cost.
  expect(main.height).toBeGreaterThan(400);
});

test('the console speaks 中文', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: '中文' }).click();
  await expect(page.getByRole('heading', { name: '总览' })).toBeVisible();
  await expect(nav(page, '委托')).toBeVisible();
});
