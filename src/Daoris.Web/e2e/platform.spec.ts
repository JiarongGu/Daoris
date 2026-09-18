import { test, expect, type Page } from '@playwright/test';

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

test('the console speaks 中文', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: '中文' }).click();
  await expect(page.getByRole('heading', { name: '总览' })).toBeVisible();
  await expect(nav(page, '委托')).toBeVisible();
});
