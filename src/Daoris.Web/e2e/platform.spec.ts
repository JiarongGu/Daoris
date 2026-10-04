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

/** Quests' list pane, which a browser keeps beside the main area (FRAME1d, D118 §4). */
const questList = (page: Page) => page.getByRole('complementary', { name: 'Quests' });

/**
 * The main area, where a quest's record and an ask's open since FRAME1d (D118 §3d): a record is the main area, and
 * only a form is a drawer. A locator, so it follows the page as the main area changes what it holds.
 */
const record = (page: Page) => page.getByRole('main');

/**
 * Repositories' list pane (FRAME1e): a group per workspace, headed by its row (UX6g), then its adopted and its registered
 * not adopted, each row named by its repository.
 */
const repositoryList = (page: Page) => page.getByRole('complementary', { name: 'Repositories' });

/** A repository's row in that list, by its name exactly: `engine` is not `newcomer`'s substring, but say so anyway. */
const repositoryRow = (page: Page, name: string) => repositoryList(page).getByRole('listitem', { name, exact: true });

/** A repository chosen from its row, and its page in the main area, found by its title (D118 §3d). */
async function chooseRepository(page: Page, name: string) {
  await repositoryRow(page, name).getByRole('button').first().click();
  await expect(record(page).getByRole('heading', { level: 1, name, exact: true })).toBeVisible();
}

/** Search's list pane (FRAME1f): the box, *local only* and the hits. Its place is named exactly: the command center says *Search* too. */
const searchList = (page: Page) => page.getByRole('complementary', { name: 'Search', exact: true });
const toSearch = (page: Page) =>
  page.getByRole('navigation', { name: 'Views' }).getByRole('button', { name: 'Search', exact: true }).click();

/** Convergence's list pane (FRAME1f): the similarity, then the findings. */
const convergenceList = (page: Page) => page.getByRole('complementary', { name: 'Convergence' });

/** Quests' ＋, one control with two kinds, Ask first, then New quest (D118 §2). */
async function make(page: Page, kind: 'Ask' | 'New quest') {
  await questList(page).getByRole('button', { name: 'New ask or quest' }).click();
  await page.getByRole('menuitem', { name: kind, exact: true }).click();
}

/**
 * *Mark done* on a quest nobody has taken: its head keeps it in its ⋯ (UX7c, D152 §7), since taking is the open quest's
 * next step; a taken quest's *Mark done* is its loud button.
 */
async function markDoneOpen(page: Page) {
  await record(page).getByRole('button', { name: 'More actions' }).click();
  await page.getByRole('menuitem', { name: 'Mark done', exact: true }).click();
}

/** Quests' ⋯ holds its filters: *Include closed* is a toggle there (D118 §2). */
async function includeClosed(page: Page) {
  await questList(page).getByRole('button', { name: 'Filter the list' }).click();
  await page.getByRole('menuitemcheckbox', { name: 'Include closed' }).click();
}

test('the overview shows the example family', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();
  await expect(page.getByText('of 2 in the family')).toBeVisible();
  await expect(page.getByText('engine').first()).toBeVisible();
  await expect(page.getByText('game').first()).toBeVisible();
});

/**
 * Repositories on the frame (FRAME1e, D118 §2): both members in the list, each opening its page with its declaration,
 * the chosen one remembered across a reload, and the page's door to its code map, one level into the Map (MAP3a).
 */
test('repositories lists both members, and each opens its page with its declaration', async ({ page }) => {
  await page.goto('/');
  await nav(page, 'Repositories').click();
  // A group per workspace (UX6g, D150 §4.1): the family's one, headed by its row.
  await expect(repositoryList(page).getByRole('heading', { level: 3, name: 'default 2 repositories' })).toBeVisible();

  await chooseRepository(page, 'engine');
  await expect(record(page).getByText('the engine runtime — simulation, rendering, assets')).toBeVisible();
  await chooseRepository(page, 'game');
  await expect(record(page).getByText('a playtest finding, with the reproduction')).toBeVisible();

  // The list remembers the repository chosen (D118 §3f): after a reload, which lands on Overview (D40), its page is
  // the one the view reopens on.
  await page.reload();
  await nav(page, 'Repositories').click();
  await expect(record(page).getByRole('heading', { level: 1, name: 'game', exact: true })).toBeVisible();

  await chooseRepository(page, 'engine');
  await record(page).getByRole('button', { name: 'Open code map' }).click();
  await expect(page.getByRole('heading', { name: 'engine: code map' })).toBeVisible();
});

/**
 * **A quest carries a link and files, and the files stay on this machine** (D65 §2). Composed through
 * the real composer's drawer, kept by the real host under its home, opened through the host's own route
 * on the quest's page — and an attached PAGE comes back as a sandboxed download: served from the
 * platform's origin, anything else would be a script with every route this host answers.
 */
test('a quest carries a link and files: kept here, opened here, never run as the platform', async ({ page, request }) => {
  const title = 'Read the media field names from config';
  const png = Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
    'base64');

  await page.goto('/');
  await nav(page, 'Quests').click();
  await make(page, 'New quest');
  await page.getByLabel('From', { exact: true }).click();
  await page.getByRole('option', { name: 'game' }).click();
  await page.getByLabel('To', { exact: true }).click();
  await page.getByRole('option', { name: 'engine' }).click();
  await page.getByLabel('what is wanted, in one line').fill(title);
  await page.getByLabel('why, and the evidence').fill('The video and image field names are hard-coded; the ticket and a screenshot say where.');
  await page.getByLabel(/^Links/).fill('https://tickets.example/T-7');
  await page.getByLabel('choose files…').setInputFiles([
    { name: 'before.png', mimeType: 'image/png', buffer: png },
    { name: 'page.html', mimeType: 'text/html', buffer: Buffer.from('<script>parent.document.title = "owned"</script>') },
  ]);
  await page.getByRole('button', { name: 'Publish quest' }).click();
  // The service's sentence, as it reads: a toast sets its backticked names as code, so the words
  // are the service's and the backticks are gone (`Inline`).
  await expect(page.getByText(/Published quest #[0-9a-f]{12} to engine/).first()).toBeVisible();

  // The list's row counts what it carries; the quest's page, in the main area, holds the things themselves.
  await expect(questList(page).getByLabel('1 link · 2 files')).toBeVisible();
  await questList(page).getByText(title).click();
  const quest = record(page);
  await expect(quest.getByRole('heading', { level: 1, name: title })).toBeVisible();
  await expect(quest.getByRole('link', { name: /tickets\.example\/T-7/ }))
    .toHaveAttribute('href', 'https://tickets.example/T-7');

  // The picture is the real bytes, through the host's route — decoded, so it is the file and not an
  // error page wearing an image tag.
  const picture = quest.getByRole('img', { name: 'before.png' });
  await expect(picture).toBeVisible();
  await expect.poll(() => picture.evaluate((image: HTMLImageElement) => image.naturalWidth)).toBe(1);

  // 🔴 Where it lies is never shown: the host answers this machine a path, and the page must not
  // print it.
  await expect(quest).not.toContainText('_fixtures');
  await expect(quest).not.toContainText('attachments');

  // 🔴 An attached page is a sandboxed download, never a document on the platform's origin.
  const href = await quest.getByRole('link', { name: /page\.html/ }).getAttribute('href');
  const served = await request.get(href!);
  expect(served.status()).toBe(200);
  expect(served.headers()['content-security-policy']).toContain('sandbox');
  expect(served.headers()['x-content-type-options']).toBe('nosniff');
  expect(served.headers()['content-disposition']).toContain('attachment');

  // Leave the family as it was found: the suite is serial, and the next test expects nothing open.
  await markDoneOpen(page);
  await expect(page.getByText(/is now Done/).first()).toBeVisible();
});

/**
 * **A chain moves on when its quest closes done** (D65 §4) — over the real host, whose close publishes
 * the next step in the same transaction. The quest's page shows what is coming before it comes, the
 * close's toast names the step it published, and the step says which quest it follows.
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
  await questList(page).getByText('Develop the streaming cap').click();
  const quest = record(page);
  await expect(quest.getByText(/Verify \{parent\} in a playtest/)).toBeVisible();
  await markDoneOpen(page);
  await expect(page.getByText(/Then: published #[0-9a-f]{12} to engine/).first()).toBeVisible();

  // The step is an ordinary open quest in the list, named with the id of the one it follows.
  const step = questList(page).getByText(`Verify #${parent} in a playtest`);
  await expect(step).toBeVisible();
  await expect(questList(page).getByText(`follows #${parent}`)).toBeVisible();

  // MAP1: the step's page carries the chain — the quest it follows, done, and a door back to it;
  // and from there, a door forward again. Real records, the real host's parent link.
  await step.click();
  const chain = () => record(page).getByRole('region', { name: 'How this work ran' });
  await expect(chain().getByText('this quest')).toBeVisible();
  await chain().getByRole('button', { name: 'Develop the streaming cap' }).click();
  await expect(record(page).getByRole('heading', { level: 1, name: 'Develop the streaming cap' })).toBeVisible();
  await chain().getByRole('button', { name: `Verify #${parent} in a playtest` }).click();
  await expect(record(page).getByRole('heading', { level: 1, name: `Verify #${parent} in a playtest` })).toBeVisible();

  // Leave the family as it was found: the suite is serial, and a later test expects nothing open.
  await markDoneOpen(page);
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
  // The composer is a form, so a drawer still (D118 §3d), opened from the list's ＋.
  await make(page, 'New quest');

  await page.getByLabel('From', { exact: true }).click();
  await page.getByRole('option', { name: 'game' }).click();
  await page.getByLabel('To', { exact: true }).click();
  await page.getByRole('option', { name: 'engine' }).click();
  await page.getByLabel('what is wanted, in one line').fill('Expose a streaming budget on the chunk API');
  // Its short title (SESSUX1j): the few words a list and its head name it by, kept by the real host.
  await page.getByLabel('Short title').fill('Streaming budget');
  await page.getByLabel('why, and the evidence').fill(
    'World streaming needs a per-frame cap; today hydration is unbounded. Evidence: seams whenever more than three chunks hydrate in one frame.',
  );
  await page.getByRole('button', { name: 'Publish quest' }).click();

  // The toast carries the service's sentence verbatim — that text IS the contract. (.first(): Radix
  // renders each toast twice — the visible element and its aria-live announcer.)
  await expect(page.getByText(/Published quest #[0-9a-f]{12} to engine/).first()).toBeVisible();

  // It sits in Open, and the quest just published opens on its page in the main area, where the acting
  // happens; its row in the list is a door to the same page (D118 §3d). No drawer is left over it.
  // Its head and its row say its short title; the whole title is said once, opening its body (UX7c, D152 §7).
  const title = 'Streaming budget';
  await expect(record(page).getByRole('heading', { level: 1, name: title })).toBeVisible();
  await expect(record(page).getByText(/^Expose a streaming budget on the chunk API/)).toHaveCount(1);
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await questList(page).getByText(title).click();
  await record(page).getByRole('button', { name: 'Take', exact: true }).click();
  await expect(page.getByText(/is now Taken/).first()).toBeVisible();

  // The page stays on the quest as it now stands: taken, so *Mark done* is its next step.
  await expect(record(page).getByRole('button', { name: 'Take', exact: true })).toHaveCount(0);
  await record(page).getByRole('button', { name: 'Mark done', exact: true }).click();
  await expect(page.getByText(/is now Done/).first()).toBeVisible();

  // Closed work leaves the default list and returns on request, from the list's ⋯; its page stays, done.
  await expect(questList(page).getByText('No open quests anywhere')).toBeVisible();
  await expect(record(page).getByRole('heading', { level: 1, name: title })).toBeVisible();
  await includeClosed(page);
  await expect(questList(page).getByText(title)).toBeVisible();

  // The filter is the list's own, remembered for the next visit (D118 §3f). The quest it chose has closed, so the view
  // opens with nothing chosen rather than on yesterday's done quest (UX6b, D150 §8).
  await page.reload();
  await nav(page, 'Quests').click();
  await expect(questList(page).getByText(title)).toBeVisible();
  await expect(record(page).getByText('Choose a quest or an ask')).toBeVisible();
  await expect(record(page).getByRole('heading', { level: 1, name: title })).toHaveCount(0);
});

test('a refusal reaches the person verbatim', async ({ page }) => {
  await page.goto('/');
  await nav(page, 'Quests').click();
  await make(page, 'New quest');

  // Self-addressed: the one refusal the form cannot prevent, because the judgement is the service's.
  await page.getByLabel('From', { exact: true }).click();
  await page.getByRole('option', { name: 'engine' }).click();
  await page.getByLabel('To', { exact: true }).click();
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

  // The composer is a form, so a drawer still (D118 §3d), addressed by its title.
  const composer = page.getByRole('dialog', { name: 'Ask the workspace' });
  await expect(composer).toBeVisible();
  // One workspace held, so it is the workspace — said, and not asked.
  await expect(composer.getByText('Asked in workspace default')).toBeVisible();
  await expect(composer.getByRole('combobox', { name: 'workspace' })).toHaveCount(0);
  await composer.getByLabel('what is wanted, and why').fill(sentence);
  await composer.getByLabel(/^Links/).fill('https://tickets.example/T-8');
  await composer.getByLabel('choose files…').setInputFiles([
    { name: 'trace.log', mimeType: 'text/plain', buffer: Buffer.from('frame 212: hydrate stalls 38ms\n') },
  ]);
  await composer.getByRole('button', { name: 'Ask', exact: true }).click();

  // The service's sentence, verbatim: which tier answered, what it proposed, and that nothing went out.
  await expect(page.getByText(/Asked as #[0-9a-f]{6} in default — by declarations only; no intake agent ran — proposed, best first: engine/).first()).toBeVisible();

  // The ask's page opens on the answer, in Quests' main area: the tier in words, the proposal as a verb,
  // the file by name only. (The quest it becomes takes the same words for its title, so each page is told
  // apart by what it holds.)
  const ask = record(page);
  await expect(ask.getByRole('heading', { level: 1, name: sentence })).toBeVisible();
  await expect(ask.getByRole('region', { name: 'Where it belongs' })).toBeVisible();
  await expect(ask.getByText('by declarations only; no intake agent ran')).toBeVisible();
  await expect(ask.getByText('trace.log')).toBeVisible();
  await expect(ask).not.toContainText('_fixtures');
  // It is in the list too, at the head, above the quests (D118 §2).
  await expect(questList(page).getByText(/^Asks \(/)).toBeVisible();

  await ask.getByRole('button', { name: 'publish to engine' }).click();
  await expect(page.getByText(/Published quest #[0-9a-f]{12} to engine/).first()).toBeVisible();

  // The quest it became is a door into the quest's own page — once the page holds it — asked BY the
  // ask, carrying its link and its file.
  // Named as every list names it (SESSUX1j): the service read a name from its words, whole words, since nobody gave one.
  await ask.getByRole('region', { name: 'Became' }).getByRole('button', { name: /^#[0-9a-f]{6} the rendering of the asset pipeline…$/ }).click();
  const quest = record(page);
  await expect(quest.getByRole('region', { name: 'Where it belongs' })).toHaveCount(0);
  // Asked by the ask, on its head's facts line (UX7c).
  await expect(quest.getByText(/from ask #[0-9a-f]{6}/).first()).toBeVisible();
  await expect(quest.getByRole('link', { name: /tickets\.example\/T-8/ })).toBeVisible();
  await expect(quest.getByRole('link', { name: /trace\.log/ })).toBeVisible();

  // Its only quest done, the ask's work is finished (USE1c): it is DONE, and leaves the list by itself,
  // as a closed ask and a closed quest do. Nobody has to close it, and the family is left as found.
  await markDoneOpen(page);
  await expect(page.getByText(/is now Done/).first()).toBeVisible();
  await expect(questList(page).getByText(/^Asks \(/)).toHaveCount(0);

  // With closed ones included, from the list's ⋯, it comes back, wearing its done pill.
  await includeClosed(page);
  await expect(questList(page).getByText(/^Asks \(/)).toBeVisible();
  await expect(questList(page).getByText('done', { exact: true }).first()).toBeVisible();
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
  // UX6c: a row is named by its title, and its door is the press at its end, beside the acts it settles in place.
  const proposed = band.getByRole('listitem', { name: sentence });
  await expect(proposed).toContainText('proposed, not yet published');
  await expect(proposed).toContainText('workspace default');
  await proposed.getByRole('button', { name: 'Open' }).click();
  // Its page, in Quests' main area (FRAME1d), with the ask chosen in the list beside it.
  await expect(record(page).getByRole('heading', { level: 1, name: sentence })).toBeVisible();
  await expect(record(page).getByRole('region', { name: 'Where it belongs' })).toBeVisible();

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
  const asking = page.getByRole('region', { name: 'What needs you' }).getByRole('listitem', { name: sentence });
  await expect(asking).toContainText('its intake asked you');
  await expect(asking).toContainText('published nothing: it asks you rather than guess.');
  // One thing, one row: the parked intake is the ask's row, not a parked session beside it.
  await expect(page.getByRole('region', { name: 'What needs you' }).getByText('parked at a checkpoint')).toHaveCount(0);

  // An intake's question needs reading, so its door is the row's one control, naming the ask it opens.
  await asking.getByRole('button', { name: `Answer ask #${ask.id}` }).click();
  await expect(record(page).getByRole('heading', { level: 1, name: sentence })).toBeVisible();
  const intake = record(page).getByRole('region', { name: 'Intake session' });
  await expect(intake.getByText('waiting on you')).toBeVisible();
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
  // Its summary is its row's one line (FRAME1e).
  await expect(repositoryRow(page, 'newcomer').getByText('Born during the test run.')).toBeVisible();

  await nav(page, 'Quests').click();
  await make(page, 'New quest');
  await page.getByLabel('From', { exact: true }).click();
  await page.getByRole('option', { name: 'game' }).click();
  await page.getByLabel('To', { exact: true }).click();
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
  await expect(scope).toHaveText(/Every workspace · 2/);
  await expect(page.getByText('of 3 in the family')).toBeVisible();

  await scope.click();
  await page.getByRole('option', { name: 'studio' }).click();
  await expect(scope).toHaveText('studio');
  await expect(page.getByText('of 1 in the family')).toBeVisible();
  await nav(page, 'Repositories').click();
  await expect(repositoryRow(page, 'newcomer').getByText('Born during the test run.')).toBeVisible();
  await expect(repositoryRow(page, 'engine')).toHaveCount(0);

  // Remembered per browser, like the language: a reload keeps the circle.
  await page.reload();
  await expect(page.getByRole('combobox', { name: 'workspace' })).toHaveText('studio');

  await page.getByRole('combobox', { name: 'workspace' }).click();
  await page.getByRole('option', { name: /Every workspace/ }).click();
  await nav(page, 'Repositories').click();
  await expect(repositoryRow(page, 'engine')).toBeVisible();

  const back = await request.post('/api/registry/newcomer/workspace', { data: { workspace: 'default' } });
  expect(back.ok(), await back.text()).toBe(true);
});

test("a driven session's record reaches the quest's page (D46)", async ({ page, request }) => {
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

  // The row wears the live session's state beside the quest's own status…
  const row = questList(page).getByRole('button', { name: /Drive the streaming budget work/ });
  await expect(row).toBeVisible();
  await expect(row.getByText('working', { exact: true })).toBeVisible();
  await row.click();

  // …and the quest's page carries the record: state, id · adapter, and the driver's note, verbatim.
  const shown = record(page);
  await expect(shown.getByRole('heading', { level: 1, name: 'Drive the streaming budget work' })).toBeVisible();
  const ran = shown.getByRole('region', { name: 'Session', exact: true });
  await expect(ran.getByText('working', { exact: true })).toBeVisible();
  await expect(ran.getByText(new RegExp(`${session.id} · stub`))).toBeVisible();
  await expect(ran.getByText('the process is alive')).toBeVisible();

  // Read-only is the arc's central claim: the record renders, the control does not — stop reaches a
  // PROCESS, and a browser has none to reach (D46 §6). Since SESSUX1d no quest page carries a stop at all:
  // its one owner is the session's page header in Sessions, which a browser has not got (D126 §3.3).
  await expect(shown.getByRole('button', { name: 'Stop session' })).toHaveCount(0);
  await expect(shown.getByRole('button', { name: 'Stop…' })).toHaveCount(0);
});

/**
 * Which tool, and which account, did the work (D49 §4).
 *
 * The inner loop holds the formatter with a mocked record; this holds the whole chain over the real
 * artefact — the request contract, the ledger, the two store columns, the response shape, and the
 * page's line — because every one of those is a place a nullable field quietly stops arriving and
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
  await questList(page).getByText('Prove the record names its tool').click();

  // The quest's page, in the main area (FRAME1d), carries the session's line.
  const shown = record(page);
  await expect(shown.getByRole('heading', { level: 1, name: 'Prove the record names its tool' })).toBeVisible();
  await expect(shown.getByText(new RegExp(`${session.id} · stub · stub-harness 9\\.9\\.9 · as work`))).toBeVisible();
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
  await expect(domains).toHaveText([/Setup/, /Appearance/, /AI features/]);

  // A workspace's page (UX6g, D150 §4.3) has a machine half, its branches, setup and accounts, so it is opened and that
  // half's absence asserted where it would render, after its browser half is seen.
  await nav(page, 'Repositories').click();
  // However many it holds by now: a newcomer joins the family earlier in this file.
  await repositoryList(page).getByRole('button', { name: /^default \d+ repositor/ }).click();
  await expect(record(page).getByRole('heading', { level: 1, name: 'default', exact: true })).toBeVisible();
  await expect(record(page).getByRole('button', { name: 'Open engine' })).toBeVisible();
  await expect(record(page).getByRole('tablist')).toHaveCount(0);
  await expect(page.getByText(/remotes\.json|harnesses\.json|driver\.json/)).toHaveCount(0);

  // A repository's page, where its facts render (FRAME1e): the absence is asserted where a path would be. Repositories is
  // already the place, and pressing its icon again would fold its list pane away.
  await chooseRepository(page, 'engine');
  await expect(record(page).getByText('the engine runtime — simulation, rendering, assets')).toBeVisible();
  // Nothing that would name a configuration home: a registration's root is a machine path, and a
  // browser on the host's own machine is still not told another's (D47 §4).
  await expect(page.getByText(/[\\/]harnesses[\\/]/)).toHaveCount(0);
  // Nor anything a browser cannot do: adding and importing touch machine paths (D48 §7), and so does Manage.
  await expect(repositoryList(page).getByRole('button', { name: 'Add repository' })).toHaveCount(0);
  await expect(record(page).getByRole('button', { name: 'Manage' })).toHaveCount(0);
  // A repository's Setup is this machine's (UX6f, D47 §4): a browser's page is Details alone, with no tab row.
  await expect(record(page).getByRole('tab', { name: 'Setup' })).toHaveCount(0);
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
  // Settings' main area is named for the domain shown (FRAME1g), and its list has that domain chosen.
  await expect(page.getByRole('heading', { level: 1, name: 'AI features', exact: true })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Settings domains' }).getByRole('button', { name: 'AI features' }))
    .toHaveAttribute('aria-current', 'page');
  await expect(page.getByText('Search and convergence')).toBeVisible();
  await expect(page.getByText(status.tier, { exact: true }).first()).toBeVisible();
  if (status.note) await expect(page.getByText(status.note, { exact: true })).toBeVisible();
  await expect(page.getByText(/DAORIS_EMBED_MODEL \(a model's name\)/)).toBeVisible();

  // The intake is absent, not disabled: no row, no control, no agent named.
  await expect(page.getByText('Intake', { exact: true })).toHaveCount(0);
  await expect(page.getByRole('combobox', { name: 'the intake agent' })).toHaveCount(0);
});

/**
 * Search on the frame (FRAME1f, D118 §2): the box, *local only* and the hits are its list, and the entry a hit names is
 * read in the main area, as it is written, where it was the reader drawer over the side bar and the panel (audit SR4).
 * The game's own knowledge answers a search made from outside it, as the family rehearsal's search does over HTTP.
 */
test('search finds what the game knows, and its entry reads whole in the main area (FRAME1f)', async ({ page }) => {
  await page.goto('/');
  await toSearch(page);
  const box = searchList(page).getByRole('searchbox', { name: 'search knowledge' });
  await expect(box).toBeFocused();
  await box.fill('chunk hydration');
  const hit = searchList(page).getByRole('listitem', { name: 'world-streaming', exact: true });
  await expect(hit).toBeVisible();
  await expect(hit.locator('mark').first()).toBeVisible();

  // ↓ goes from the box into the hits, and Enter opens the row (D118 §3e).
  await box.press('ArrowDown');
  await expect(hit.getByRole('button')).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(record(page).getByRole('heading', { level: 1, name: 'world-streaming', exact: true })).toBeVisible();
  await expect(record(page).locator('pre')).toContainText('**chunk hydration** runs neighbours-first');
  await expect(page.getByRole('dialog')).toHaveCount(0);

  // The list remembers *local only* and the entry chosen across a reload (§3f), and not what was typed.
  const localOnly = () => searchList(page).getByRole('checkbox', { name: "Each repository's own only" });
  await localOnly().click();
  await expect(localOnly()).not.toBeChecked();
  await page.reload();
  await toSearch(page);
  await expect(localOnly()).not.toBeChecked();
  await expect(searchList(page).getByRole('searchbox', { name: 'search knowledge' })).toHaveValue('');
  await expect(record(page).getByRole('heading', { level: 1, name: 'world-streaming', exact: true })).toBeVisible();
});

/**
 * Convergence on the frame (FRAME1f, D118 §2): the similarity and the findings are its list, and a finding is read in
 * the main area, the service's sentence and then each entry whole (audit CO4). Whatever this host answers at the list's
 * start is asked first, and the page must say THAT: the example family's own entries may share nothing.
 */
test("convergence keeps its similarity, and a finding reads under the service's sentence (FRAME1f)", async ({ page, request }) => {
  const findings = await (await request.get('/api/convergence?minimumSimilarity=0.75')).json() as
    { suggestion: string; entries: { title: string }[] }[];
  await page.goto('/');
  await nav(page, 'Convergence').click();
  const slider = () => convergenceList(page).getByRole('slider');
  await expect(slider()).toHaveValue('0.75');

  if (findings.length === 0) {
    await expect(convergenceList(page).getByText('Nothing converges at 0.75 or above')).toBeVisible();
    await expect(record(page).getByText('Choose a finding')).toBeVisible();
  } else {
    const first = findings[0]!;
    const title = [...new Set(first.entries.map((entry) => entry.title))].join(' · ');
    await convergenceList(page).getByRole('listitem', { name: title, exact: true }).first().getByRole('button').click();
    await expect(record(page).getByRole('heading', { level: 1, name: title, exact: true })).toBeVisible();
    // Verbatim, its backticked command set as code (`Inline`): the words are the service's.
    await expect(record(page).getByText(first.suggestion.replace(/`/g, ''))).toBeVisible();
    await expect(record(page).locator('pre')).toHaveCount(first.entries.length);
    await expect(page.getByRole('dialog')).toHaveCount(0);
  }

  // The similarity is the list's memory (§3f): moved by its keys, and kept across a reload.
  await slider().focus();
  for (let step = 0; step < 5; step += 1) await page.keyboard.press('ArrowLeft');
  await expect(convergenceList(page).getByText('0.70', { exact: true })).toBeVisible();
  // Kept once it has held still, not at every step: a reload before then would reopen the last value kept.
  await expect.poll(() => page.evaluate(() => window.localStorage.getItem('daoris.list.convergence.filters')))
    .toBe('{"threshold":0.7}');
  await page.reload();
  await nav(page, 'Convergence').click();
  await expect(slider()).toHaveValue('0.7');
});

/**
 * The Work frame is not rendered in a browser at all (D55), which is the strongest form the
 * disclosure rule takes: its centre is a stream, its rows carry tree paths, and its panel is a
 * transcript — none of which may leave the machine that produced them (D47 §4). Playwright holds
 * the NEGATIVE, over the shipped bundle, because the positive is only reachable from the desktop.
 */
test('a browser has no Sessions and no Plugins, and no mode to switch (D55, D66, D119)', async ({ page }) => {
  await page.goto('/');

  // One navigation, and Sessions is not on it — absent rather than disabled.
  await expect(page.getByRole('group', { name: /mode/i })).toHaveCount(0);
  await expect(nav(page, 'Sessions')).toHaveCount(0);
  // Nor Plugins (PLUGUI1b, D119 §3.7): a plugin is this machine's, so its view is shell-only, as Sessions is.
  await expect(nav(page, 'Plugins')).toHaveCount(0);
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
  // Plugins is this machine's (D119 §3.7), so its palette row is not offered here.
  await expect(palette.getByRole('option', { name: /Plugins/ })).toHaveCount(0);
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

/**
 * A view's columns follow its main area, never the window (D118 §3b, FRAME1c): the side bar and a list
 * narrow the main area while the window stays as wide, so Overview's two cards and the map's detail beside its
 * canvas are laid out by the main area's own width. The repositories' cards went with FRAME1e, whose list holds
 * them and whose page is one column. A browser has no side bar
 * (§4), so the main area is narrowed here by hand at a window that stays 1280 px wide: a viewport breakpoint
 * would keep two columns, and only a container query stacks them. vitest has no layout to see this with.
 */
test("a browser's main area lays its columns out by its own width, beside no side bar and no panel (D118)", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();
  // The main area and the view's list are a browser's; the side bar and the panel never are.
  await expect(page.getByRole('complementary', { name: 'right side bar' })).toHaveCount(0);
  await expect(page.getByRole('region', { name: 'the panel' })).toHaveCount(0);

  const main = page.locator('main');
  /** The two columns of the split that holds `text`, and whether the second sits beside the first. */
  const beside = async (text: string) => {
    const columns = page.locator('main section > div.grid', { hasText: text }).locator(':scope > *');
    const first = await columns.nth(0).boundingBox();
    const second = await columns.nth(1).boundingBox();
    if (!first || !second) throw new Error(`the split holding "${text}" must have two columns laid out`);
    return Math.abs(first.y - second.y) < 4 && second.x >= first.x + first.width - 1;
  };
  const narrowed = (width: string | null) => main.evaluate((element, to) => { element.style.maxWidth = to ?? ''; }, width);

  // Overview: Outstanding beside the repositories by index size, and above them in a narrow main area.
  expect(await beside('Outstanding — oldest first')).toBe(true);
  await narrowed('600px');
  await expect.poll(() => beside('Outstanding — oldest first')).toBe(false);
  await narrowed(null);

  // The map: its detail beside the canvas, and under it.
  await nav(page, 'Map').click();
  await expect(page.getByRole('group', { name: 'the workspace map' })).toBeVisible();
  await expect.poll(() => beside('the number in a repository')).toBe(true);
  await narrowed('600px');
  await expect.poll(() => beside('the number in a repository')).toBe(false);
  await narrowed(null);
});

test('the console speaks 中文', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: '中文' }).click();
  await expect(page.getByRole('heading', { name: '总览' })).toBeVisible();
  await expect(nav(page, '委托')).toBeVisible();
});
