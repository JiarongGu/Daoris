import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../i18n';
import { ReviewField, type ReviewRule, reviewRowSays, reviewSays, reviewSummary, reviewToast } from './Reviews';

// REVIEWENV1a (D154 point 2, the review-environment design §1.7–§1.8): where a repository's work is shown to the person before
// it lands, set on a repository's Setup and a workspace's Defaults as `daoris driver review` sets it from a terminal (D50).
// The words each door says are the twins' (`reviews.ts`, `ReviewRules.cs`), held here in English to rows of their one table
// (the driver suite's `fixtures/review-rules.json`, `says`), copied, and the field sends the twins' edit, which the driver judges.

type SaysRow = [name: string, rule: ReviewRule, sentences: string[]];

/** Rows of the twins' `says` table, copied: a rule as the bridge carries it, and what each door says of it. */
const SAYS: SaysRow[] = [
  ['none here', { none: true, environments: [] },
    ['No review environment here, whatever its workspace says: work is offered to land once its quest is done.']],
  ['a local environment that runs a process', {
    required: true,
    environments: [{ name: 'local', kind: 'local', procedure: 'README.md', address: 'http://localhost:4200', run: 'npm run serve' }],
  }, [
    'Before work here lands, it is shown to you in `local` and waits for you to say it is right.',
    "A set-up step here builds the work in its own tree and shows it in Daoris's browser at `http://localhost:4200`; your own "
      + 'servers and processes are not touched.',
    'A set-up step here may run `npm run serve` in its tree on a port nobody holds, without asking you each time; it stops once '
      + 'you have reviewed.',
  ]],
  ['two environments, the first the default', {
    required: true,
    environments: [
      { name: 'local', kind: 'local', procedure: 'README.md', address: 'http://localhost:4200' },
      { name: 'dev', kind: 'deployed', procedure: 'docs/deploying-to-dev.md', address: 'https://dev.example.test' },
    ],
  }, [
    'Before work here lands, it is shown to you in `local` and waits for you to say it is right.',
    'A task may ask for `dev` instead.',
    "A set-up step here builds the work in its own tree and shows it in Daoris's browser at `http://localhost:4200`; your own "
      + 'servers and processes are not touched.',
    'A set-up step here follows `docs/deploying-to-dev.md` toward `dev`; each deploy or write there asks your go-ahead once per ask.',
  ]],
  ['three environments nobody requires', {
    environments: [
      { name: 'local', kind: 'local', procedure: 'README.md', address: 'http://localhost:4200' },
      { name: 'dev', kind: 'deployed', procedure: 'README.md' },
      { name: 'test', kind: 'deployed', procedure: 'README.md' },
    ],
  }, [
    'When a task asks for it, work here may be shown to you in `local`, `dev` or `test`; nothing waits for it.',
    "A set-up step here builds the work in its own tree and shows it in Daoris's browser at `http://localhost:4200`; your own "
      + 'servers and processes are not touched.',
    'A set-up step here follows `README.md` toward `dev`; each deploy or write there asks your go-ahead once per ask.',
    'A set-up step here follows `README.md` toward `test`; each deploy or write there asks your go-ahead once per ask.',
  ]],
];

const t = i18n.t.bind(i18n);

const LOCAL: ReviewRule = {
  required: true,
  environments: [{ name: 'dev', kind: 'local', procedure: 'README.md', address: 'http://localhost:4200' }],
};

const draw = (set: ReviewRule | undefined, owner: 'repository' | 'workspace' = 'repository', onChange = vi.fn()) => {
  render(
    <Tooltip.Provider>
      <ReviewField name="storefront" owner={owner} set={set} onChange={onChange} />
    </Tooltip.Provider>,
  );
  return onChange;
};

describe('what a review rule says', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("says, in English, the twins' sentences for each rule, word for word", () => {
    for (const [name, rule, sentences] of SAYS) expect(reviewSays(t, rule), name).toEqual(sentences);
  });

  it("says nothing set as today's behaviour, and a rule as declared only, with where it was set", () => {
    expect(reviewRowSays(t, null)).toBe('None: work is offered to land once its quest is done.');
    expect(reviewRowSays(t, LOCAL, { source: 'workspace', workspace: 'work' })).toBe(
      'Before work here lands, it is shown to you in `dev` and waits for you to say it is right. A set-up step here builds the '
      + "work in its own tree and shows it in Daoris's browser at `http://localhost:4200`; your own servers and processes are not "
      + 'touched. Declared only: nothing reads it yet, so no set-up step is composed and no landing waits for it. From the '
      + 'workspace work.');
    expect(reviewSummary(t, null)).toBe('no review environment');
    expect(reviewSummary(t, { none: true, environments: [] })).toBe('no review environment here');
    expect(reviewSummary(t, LOCAL)).toBe('reviewed in `dev` before landing');
  });

  it('says it in Chinese, the code and the address kept as written', async () => {
    await i18n.changeLanguage('zh');

    const said = reviewSays(t, LOCAL);
    expect(said[0]).toBe('这里的工作落地之前，会在 `dev` 中展示给你，并等你确认无误。');
    expect(said[1]).toContain('`http://localhost:4200`');
    expect(reviewSummary(t, { environments: [LOCAL.environments[0]!, { name: 'test', kind: 'deployed', procedure: 'README.md' }] }))
      .toBe('要求时在 `dev` 或 `test` 中展示');
  });

  it('toasts what a change did, and for a put what its procedure look found', () => {
    const put = { put: { name: 'dev', kind: 'deployed', procedure: 'README.md' } };
    expect(reviewToast(t, 'work', put, { lacking: ['media-api', 'storefront'], unchecked: false })).toBe(
      'work declares the review environment dev. Declared only: nothing reads it yet. media-api, storefront hold no README.md '
      + 'here: their set-up steps sit until they do.');
    expect(reviewToast(t, 'storefront', put, { lacking: [], unchecked: true })).toContain('README.md was not looked for');
    expect(reviewToast(t, 'storefront', { none: true })).toBe('storefront has no review environment now, whatever its workspace says.');
    expect(reviewToast(t, 'storefront', { drop: 'dev' })).toBe('storefront no longer declares dev.');
    expect(reviewToast(t, 'storefront', { required: false })).toBe('In storefront, work no longer waits for your review.');
    expect(reviewToast(t, 'storefront', { clear: true })).toBe('storefront takes its review rule from what stands above it again.');
  });
});

describe("a review rule's field", () => {
  it('opens on the form where nothing of its own is set, and sends a local environment with its address, required', async () => {
    const onChange = draw(undefined);
    const user = userEvent.setup();

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    await user.type(screen.getByRole('textbox', { name: "The environment's name for storefront" }), 'dev');
    await user.type(screen.getByRole('textbox', { name: 'Its procedure in storefront, a path in the repository' }), 'README.md');
    // A local environment's address is required: the only place a set-up step may show the work.
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    await user.type(screen.getByRole('textbox', { name: 'Where the app runs, for storefront' }), 'http://localhost:4200');
    await user.type(screen.getByRole('textbox', { name: 'A command for a process of its own, for storefront' }), 'npm run serve');
    await user.click(screen.getByRole('checkbox', { name: 'Wait for my review before landing' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onChange).toHaveBeenLastCalledWith({
      put: { name: 'dev', kind: 'local', procedure: 'README.md', address: 'http://localhost:4200', run: 'npm run serve' },
      required: true,
    });
  });

  it('sends a deployed environment with no address and no command, and a repository may say it has none', async () => {
    const onChange = draw(undefined);
    const user = userEvent.setup();

    await user.type(screen.getByRole('textbox', { name: "The environment's name for storefront" }), 'dev');
    await user.click(screen.getByRole('radio', { name: 'Deployed' }));
    expect(screen.queryByRole('textbox', { name: 'A command for a process of its own, for storefront' })).toBeNull();
    await user.type(screen.getByRole('textbox', { name: 'Its procedure in storefront, a path in the repository' }), 'docs/deploying-to-dev.md');
    await user.click(screen.getByRole('button', { name: 'Save' }));
    expect(onChange).toHaveBeenLastCalledWith({ put: { name: 'dev', kind: 'deployed', procedure: 'docs/deploying-to-dev.md' }, required: false });

    await user.click(screen.getByRole('button', { name: 'None here' }));
    expect(onChange).toHaveBeenLastCalledWith({ none: true });
  });

  it('lists what is set here, the first the default, drops one, says whether work waits, and adds another', async () => {
    const onChange = draw({ ...LOCAL, environments: [...LOCAL.environments, { name: 'test', kind: 'deployed', procedure: 'README.md' }] });
    const user = userEvent.setup();

    const list = screen.getByRole('list', { name: 'The review environments of storefront' });
    expect(list).toHaveTextContent('dev · Local, the default');
    expect(list).toHaveTextContent('test · Deployed');
    expect(screen.queryByRole('textbox', { name: "The environment's name for storefront" })).toBeNull();

    await user.click(screen.getByRole('button', { name: 'Stop declaring test for storefront' }));
    expect(onChange).toHaveBeenLastCalledWith({ drop: 'test' });
    await user.click(screen.getByRole('checkbox', { name: 'Wait for my review before landing' }));
    expect(onChange).toHaveBeenLastCalledWith({ required: false });

    await user.click(screen.getByRole('button', { name: 'Add an environment' }));
    // An added environment leaves whether work waits as it stands.
    expect(screen.getAllByRole('checkbox', { name: 'Wait for my review before landing' })).toHaveLength(1);
    await user.type(screen.getByRole('textbox', { name: "The environment's name for storefront" }), 'stage');
    await user.click(screen.getByRole('radio', { name: 'Deployed' }));
    await user.type(screen.getByRole('textbox', { name: 'Its procedure in storefront, a path in the repository' }), 'README.md');
    await user.click(screen.getByRole('button', { name: 'Save' }));
    expect(onChange).toHaveBeenLastCalledWith({ put: { name: 'stage', kind: 'deployed', procedure: 'README.md' } });
  });

  it('offers a workspace no none: a workspace with no review environment simply sets none', () => {
    draw(undefined, 'workspace');

    expect(screen.queryByRole('button', { name: 'None here' })).toBeNull();
  });
});
