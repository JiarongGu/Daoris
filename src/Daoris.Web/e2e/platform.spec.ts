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

test('projects lists both members with their declarations', async ({ page }) => {
  await page.goto('/');
  await nav(page, 'Projects').click();
  await expect(page.getByText('the engine runtime — simulation, rendering, assets')).toBeVisible();
  await expect(page.getByText('a playtest finding, with the reproduction')).toBeVisible();
});

test('a quest travels: composed, published, taken, finished', async ({ page }) => {
  await page.goto('/');
  await nav(page, 'Quests').click();
  await page.getByRole('button', { name: 'new quest' }).click();

  await page.getByLabel('from', { exact: true }).click();
  await page.getByRole('option', { name: 'game' }).click();
  await page.getByLabel('to', { exact: true }).click();
  await page.getByRole('option', { name: 'engine' }).click();
  await page.getByLabel('what is wanted, in one line').fill('Expose a streaming budget on the chunk API');
  await page.getByLabel('why, and the evidence').fill(
    'World streaming needs a per-frame cap; today hydration is unbounded. Evidence: seams whenever more than three chunks hydrate in one frame.',
  );
  await page.getByRole('button', { name: 'publish quest' }).click();

  // The toast carries the service's sentence verbatim — that text IS the contract. (.first(): Radix
  // renders each toast twice — the visible element and its aria-live announcer.)
  await expect(page.getByText(/Published quest `#[0-9a-f]{6}` to `engine`/).first()).toBeVisible();

  // It sits in Open; its card is a door to the detail drawer, where the acting happens.
  await page.getByText('Expose a streaming budget on the chunk API').first().click();
  await page.getByRole('button', { name: 'take', exact: true }).click();
  await expect(page.getByText(/is now Taken/).first()).toBeVisible();

  await page.getByText('Expose a streaming budget on the chunk API').first().click();
  await page.getByRole('button', { name: 'done', exact: true }).click();
  await expect(page.getByText(/is now Done/).first()).toBeVisible();

  // Closed work leaves the default list and returns on request.
  await expect(page.getByText('No open quests anywhere')).toBeVisible();
  await page.getByText('include closed').click();
  await expect(page.getByText('Expose a streaming budget on the chunk API')).toBeVisible();
});

test('a refusal reaches the person verbatim', async ({ page }) => {
  await page.goto('/');
  await nav(page, 'Quests').click();
  await page.getByRole('button', { name: 'new quest' }).click();

  // Self-addressed: the one refusal the form cannot prevent, because the judgement is the service's.
  await page.getByLabel('from', { exact: true }).click();
  await page.getByRole('option', { name: 'engine' }).click();
  await page.getByLabel('to', { exact: true }).click();
  await page.getByRole('option', { name: 'engine' }).click();
  await page.getByLabel('what is wanted, in one line').fill('x');
  await page.getByLabel('why, and the evidence').fill('y');
  await page.getByRole('button', { name: 'publish quest' }).click();

  await expect(page.getByText('a quest is work for someone else', { exact: false }).first()).toBeVisible();
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
  await nav(page, 'Projects').click();
  await expect(page.getByText('Born during the test run.')).toBeVisible();

  await nav(page, 'Quests').click();
  await page.getByRole('button', { name: 'new quest' }).click();
  await page.getByLabel('from', { exact: true }).click();
  await page.getByRole('option', { name: 'game' }).click();
  await page.getByLabel('to', { exact: true }).click();
  await page.getByRole('option', { name: 'newcomer' }).click();
  await page.getByLabel('what is wanted, in one line').fill('A first quest for the newcomer');
  await page.getByLabel('why, and the evidence').fill('Joining means being askable — prove it.');
  await page.getByRole('button', { name: 'publish quest' }).click();
  await expect(page.getByText(/Published quest `#[0-9a-f]{6}` to `newcomer`/).first()).toBeVisible();
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
  await nav(page, 'Projects').click();
  await expect(page.getByText('Born during the test run.')).toBeVisible();
  await expect(page.getByText('the engine runtime — simulation, rendering, assets')).toHaveCount(0);

  // Remembered per browser, like the language: a reload keeps the circle.
  await page.reload();
  await expect(page.getByRole('combobox', { name: 'workspace' })).toHaveText('studio');

  await page.getByRole('combobox', { name: 'workspace' }).click();
  await page.getByRole('option', { name: /every workspace/ }).click();
  await nav(page, 'Projects').click();
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
  await expect(dialog.getByRole('button', { name: 'stop session' })).toHaveCount(0);
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

  // The machine's settings are not even reachable: the tab is shell-only. Its SIDEBAR label is
  // `Machine` — the page heading reads "This machine", and asserting on that one would have been a
  // check that could never fail, which is worse than no check at all.
  await expect(nav(page, 'Machine')).toHaveCount(0);

  await nav(page, 'Projects').click();
  await expect(page.getByText('the engine runtime — simulation, rendering, assets')).toBeVisible();
  // No roster, and nothing that would name a configuration home. The per-conversation account
  // picker moved to the Work frame with the rest of starting a session (D55) — and that frame is
  // itself absent here, which the test below is about.
  await expect(page.getByText('Harnesses')).toHaveCount(0);
  await expect(page.getByText(/\.daoris[\\/]harnesses/)).toHaveCount(0);
});

/**
 * The Work frame is not rendered in a browser at all (D55), which is the strongest form the
 * disclosure rule takes: its centre is a stream, its rows carry tree paths, and its panel is a
 * transcript — none of which may leave the machine that produced them (D47 §4). Playwright holds
 * the NEGATIVE, over the shipped bundle, because the positive is only reachable from the desktop.
 */
test('a browser has one frame, and it is Manage (D55)', async ({ page }) => {
  await page.goto('/');

  // No switch, because there is nothing to switch to — absent rather than disabled.
  await expect(page.getByRole('group', { name: 'mode' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Work', exact: true })).toHaveCount(0);

  // A browser that remembers the other frame still gets this one: the fallback is not cosmetic.
  await page.evaluate(() => window.localStorage.setItem('daoris.mode', 'work'));
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Overview' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'sessions' })).toHaveCount(0);
  await expect(page.getByLabel('console height')).toHaveCount(0);
  await expect(page.getByLabel('message')).toHaveCount(0);
  await page.evaluate(() => window.localStorage.removeItem('daoris.mode'));

  // The status bar IS here — it belongs to the application — and it says what a browser is.
  await expect(page.getByLabel('state of this machine')).toContainText('none here');

  // And no review (SURF6). A diff is read off a checkout on the machine that ran the session, so a
  // browser has no door onto one — the dock that would hold it is part of the Work frame and the
  // Work frame is not rendered here at all.
  await expect(page.getByRole('tab', { name: 'Review' })).toHaveCount(0);
  await expect(page.getByRole('tablist', { name: 'Session surfaces' })).toHaveCount(0);
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
