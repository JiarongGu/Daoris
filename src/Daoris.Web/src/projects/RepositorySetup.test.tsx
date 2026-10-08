import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { code } from '../test/code';
import { SETUP_DEFAULTS, SETUP_OWN } from './fixtures';
import { RepositorySetup, type RepositorySetupProps } from './RepositorySetup';

// UX6f (D150 §4.2, §1 rule 4): a repository's Setup, every setting it holds on this machine in four sections, each folding
// to a line that names its values, a value at its workspace's or Daoris's default marked as such. A section holding a value
// set for this repository opens on its own, and that value carries Clear; a value from above offers Set for this
// repository. Each row's hint is its terminal twin (D50). Props only: the view above holds the driver and its presses.

const draw = (props: RepositorySetupProps) => render(<Tooltip.Provider><RepositorySetup {...props} /></Tooltip.Provider>);

/** A section's head, the press that opens and folds it, named by the section alone. */
const head = (name: string) => screen.getByRole('button', { name });

/** A section's region once open, found by its name. */
const section = (name: string) => screen.getByRole('region', { name });

/** One row of a section, found by its label. */
const row = (within_: HTMLElement, label: string) => within(within_).getByText(label, { selector: 'span' }).closest('.\\@container') as HTMLElement;

afterEach(async () => { await i18n.changeLanguage('en'); });

describe("a repository's Setup", () => {
  it('folds every section to a line naming its values, each at a default marked as such', () => {
    draw(SETUP_DEFAULTS);

    for (const name of ['Driving', 'Line and landing', 'Sessions', 'Reach']) expect(head(name)).toHaveAttribute('aria-expanded', 'false');
    expect(head('Driving')).toHaveAccessibleDescription('driven here · a tree per session · not held');
    expect(head('Line and landing')).toHaveAccessibleDescription("line main (the workspace's default) · lands into its line (Daoris's default)");
    expect(head('Sessions')).toHaveAccessibleDescription("no session language (Daoris's default) · no standing answer");
    expect(head('Reach')).toHaveAccessibleDescription(
      "read by agents outside it (Daoris's default) · writes into nothing else · no rules of its own");
    // The branch is code in the line, as it is everywhere a branch is named.
    expect(within(head('Line and landing')).getByText('main', { selector: 'code' })).toBeInTheDocument();
    // Folded, a section's rows are one press away and not drawn.
    expect(screen.queryByRole('checkbox')).toBeNull();
  });

  it('opens a section holding a value set for this repository, and folds Driving until it is pressed', () => {
    draw(SETUP_OWN);

    expect(head('Line and landing')).toHaveAttribute('aria-expanded', 'true');
    expect(head('Sessions')).toHaveAttribute('aria-expanded', 'true');
    expect(head('Reach')).toHaveAttribute('aria-expanded', 'true');
    // Driving's three are this repository's own yes or no, with nothing above them to fall back to.
    expect(head('Driving')).toHaveAttribute('aria-expanded', 'false');
  });

  it('opens and folds a section on a press of its head', async () => {
    draw(SETUP_DEFAULTS);

    await userEvent.click(head('Driving'));
    expect(head('Driving')).toHaveAttribute('aria-expanded', 'true');
    expect(within(section('Driving')).getByLabelText('Drive on this machine')).toBeChecked();
    await userEvent.click(head('Driving'));
    expect(screen.queryByLabelText('Drive on this machine')).toBeNull();
  });

  it('opens the section a door asked for', () => {
    draw({ ...SETUP_DEFAULTS, open: 'work' });
    expect(head('Line and landing')).toHaveAttribute('aria-expanded', 'true');
    expect(head('Sessions')).toHaveAttribute('aria-expanded', 'false');
  });

  it("says each row's terminal twin as its hint", async () => {
    draw(SETUP_OWN);
    await userEvent.click(head('Driving'));

    for (const twin of [
      'daoris driver drive|undrive engine', 'daoris driver hold|resume engine', 'daoris driver trees engine on|off',
      'daoris driver line engine <branch>|--clear', 'daoris driver landing engine merge|branch <pattern>|--clear',
      'daoris driver language engine en|zh|--clear', 'daoris driver standing engine "…"|--clear',
      'daoris driver across engine read on|off|--clear', 'daoris driver across engine write-to <other> [--clear]',
      'daoris agent rules allow|ask|deny|remove "<rule>" --repository engine',
    ]) {
      expect(screen.getByText(code(twin))).toBeInTheDocument();
    }
  });

  it('is absent in a section it has nothing for: no Driving where there is nowhere to start', () => {
    draw({ ...SETUP_DEFAULTS, driving: null, sessions: null });
    expect(screen.queryByRole('button', { name: 'Driving' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Sessions' })).toBeNull();
    expect(head('Line and landing')).toBeInTheDocument();
  });
});

describe("Setup's Driving", () => {
  it('drives, holds and gives each session its own tree, a hold only once it is driven', async () => {
    const onDrive = vi.fn();
    const onHold = vi.fn();
    const onTrees = vi.fn();
    const driving = { drivable: false, held: false, ownTree: false, onDrive, onHold, onTrees };
    const { rerender } = draw({ ...SETUP_DEFAULTS, driving });
    expect(head('Driving')).toHaveAccessibleDescription('not driven here · sessions in its checkout');

    await userEvent.click(head('Driving'));
    expect(screen.queryByLabelText('Hold')).toBeNull();
    await userEvent.click(screen.getByLabelText('Drive on this machine'));
    expect(onDrive).toHaveBeenCalledWith(true);
    await userEvent.click(screen.getByLabelText('A tree per session'));
    expect(onTrees).toHaveBeenCalledWith(true);

    rerender(<Tooltip.Provider><RepositorySetup {...SETUP_DEFAULTS} driving={{ ...driving, drivable: true }} /></Tooltip.Provider>);
    await userEvent.click(screen.getByLabelText('Hold'));
    expect(onHold).toHaveBeenCalledWith(true);
  });

  /** INT3c: what driving here does that the choice alone does not say, under it. */
  it('says what driving here does, under the choice', async () => {
    draw({ ...SETUP_DEFAULTS, driving: { ...SETUP_DEFAULTS.driving!, note: 'This machine drives on a direct agent.' } });
    await userEvent.click(head('Driving'));
    expect(within(section('Driving')).getByText('This machine drives on a direct agent.')).toBeInTheDocument();
  });
});

describe("Setup's Work", () => {
  it("offers Set for this repository on a line from its workspace, which opens the line's field in place", async () => {
    const onLine = vi.fn();
    draw({ ...SETUP_DEFAULTS, work: { ...SETUP_DEFAULTS.work, onLine } });
    await userEvent.click(head('Line and landing'));

    const line = row(section('Line and landing'), 'Its line');
    expect(within(line).getByText(/set for the workspace default/)).toBeInTheDocument();
    expect(within(line).queryByRole('button', { name: 'Clear' })).toBeNull();
    await userEvent.click(within(line).getByRole('button', { name: 'Set for this repository' }));
    const field = within(line).getByRole('textbox', { name: 'The line for engine' });
    expect(field).toHaveAttribute('placeholder', 'main');
    await userEvent.type(field, 'feature/x{Enter}');
    expect(onLine).toHaveBeenLastCalledWith('feature/x');

    // Never mind folds the field back to the press that opened it.
    await userEvent.click(within(line).getByRole('button', { name: 'Never mind' }));
    expect(within(line).getByRole('button', { name: 'Set for this repository' })).toBeInTheDocument();
  });

  it('carries Clear on a line set for this repository, which hands it back to what stands above', async () => {
    const onLine = vi.fn();
    draw({ ...SETUP_OWN, work: { ...SETUP_OWN.work, onLine } });

    const line = row(section('Line and landing'), 'Its line');
    expect(within(line).getByRole('textbox', { name: 'The line for engine' })).toHaveValue('develop');
    await userEvent.click(within(line).getByRole('button', { name: 'Clear' }));
    expect(onLine).toHaveBeenLastCalledWith(undefined);
  });

  it("says a rule of its own whole: its branch, the plugin that pushes it, and that it accepts automatically", () => {
    draw(SETUP_OWN);

    const landing = row(section('Line and landing'), 'How work lands');
    expect(within(landing).getByText(/On a branch/)).toHaveTextContent(
      'On a branch feature/{quest}-{slug}, set for this repository. Then plugin github-pull-request pushes it and opens the pull request. Accepted automatically when its quest is done.');
    expect(within(landing).getByRole('radio', { name: 'Branch' })).toHaveAttribute('aria-checked', 'true');
    // While the control accepts automatically, what that gives is said where it is given (LAND2a).
    expect(within(landing).getByText(/without asking you each time/)).toBeInTheDocument();
  });

  it('names a rule of its own in the folded line, unmarked, beside a line from its workspace', async () => {
    draw({ ...SETUP_OWN, work: { ...SETUP_OWN.work, line: SETUP_DEFAULTS.work!.line } });
    // Its own rule opened the section; folded, the line names it.
    expect(head('Line and landing')).toHaveAttribute('aria-expanded', 'true');
    await userEvent.click(head('Line and landing'));
    expect(head('Line and landing')).toHaveAccessibleDescription(
      "line main (the workspace's default) · lands on feature/{quest}-{slug}, pushed by github-pull-request, accepted automatically");
  });

  it('sets a rule for this repository from what stands above, and clears its own', async () => {
    const onLanding = vi.fn();
    draw({ ...SETUP_DEFAULTS, work: { ...SETUP_DEFAULTS.work, onLanding } });
    await userEvent.click(head('Line and landing'));

    const landing = row(section('Line and landing'), 'How work lands');
    expect(within(landing).getByText("Merged into its line — the default.")).toBeInTheDocument();
    await userEvent.click(within(landing).getByRole('button', { name: 'Set for this repository' }));
    await userEvent.click(within(landing).getByRole('radio', { name: 'Branch' }));
    await userEvent.click(within(landing).getByRole('button', { name: 'Save' }));
    expect(onLanding).toHaveBeenLastCalledWith({ form: 'branch', pattern: 'feature/{quest}-{slug}' });
  });

  it('clears a rule of its own', async () => {
    const onLanding = vi.fn();
    draw({ ...SETUP_OWN, work: { ...SETUP_OWN.work, onLanding } });
    await userEvent.click(within(row(section('Line and landing'), 'How work lands')).getByRole('button', { name: 'Clear' }));
    expect(onLanding).toHaveBeenLastCalledWith(undefined);
  });
});

describe("Setup's Sessions", () => {
  const choose = async (option: string) => {
    const user = userEvent.setup();
    screen.getByRole('combobox', { name: 'The session language for engine' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: option }));
  };

  it('sets a session language for this repository, chosen from the driver\'s table', async () => {
    const onSet = vi.fn();
    draw({ ...SETUP_DEFAULTS, sessions: { ...SETUP_DEFAULTS.sessions, language: { resolved: null, table: SETUP_DEFAULTS.sessions!.language!.table, onSet } } });
    await userEvent.click(head('Sessions'));

    const language = row(section('Sessions'), 'Session language');
    expect(within(language).getByText('None: its sessions are asked for no language.')).toBeInTheDocument();
    await userEvent.click(within(language).getByRole('button', { name: 'Set for this repository' }));
    await choose('English');
    expect(onSet).toHaveBeenLastCalledWith('en');
  });

  it("says its own language, and clears it to take its workspace's again", async () => {
    const onSet = vi.fn();
    draw({ ...SETUP_OWN, sessions: { ...SETUP_OWN.sessions, language: { ...SETUP_OWN.sessions!.language!, onSet } } });

    const language = row(section('Sessions'), 'Session language');
    expect(within(language).getByText('Simplified Chinese (简体中文), set for this repository.')).toBeInTheDocument();
    expect(within(language).getByRole('combobox', { name: 'The session language for engine' })).toHaveTextContent('Simplified Chinese (简体中文)');
    await userEvent.click(within(language).getByRole('button', { name: 'Clear' }));
    expect(onSet).toHaveBeenLastCalledWith(null);
  });

  it('marks a language from its workspace, and the window keeps its own, on the info glyph', async () => {
    draw({
      ...SETUP_DEFAULTS,
      sessions: {
        ...SETUP_DEFAULTS.sessions,
        language: {
          resolved: { repository: 'engine', workspace: 'default', language: 'en', name: 'English', source: 'workspace' },
          table: SETUP_DEFAULTS.sessions!.language!.table,
          onSet: vi.fn(),
        },
      },
    });
    expect(head('Sessions')).toHaveAccessibleDescription("English (the workspace's default) · no standing answer");
    await userEvent.click(head('Sessions'));
    expect(screen.getByRole('note', { name: /The window's own language is in Settings → Appearance/ })).toBeInTheDocument();
  });

  it('keeps a standing answer in the person\'s words, and counts its lines in the folded line', async () => {
    const onStanding = vi.fn();
    draw({ ...SETUP_DEFAULTS, sessions: { ...SETUP_DEFAULTS.sessions, onStanding } });
    await userEvent.click(head('Sessions'));

    await userEvent.click(within(section('Sessions')).getByRole('button', { name: 'Add' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Standing answer' }), 'dev only');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(onStanding).toHaveBeenLastCalledWith('dev only');
  });

  it('counts the lines of its standing answer in its folded line', async () => {
    draw(SETUP_OWN);
    await userEvent.click(head('Sessions'));
    expect(head('Sessions')).toHaveAccessibleDescription('Simplified Chinese (简体中文) · a standing answer of 3 lines');
  });
});

describe("Setup's Reach", () => {
  it('sets whether agents outside it read it, from what stands above, and clears its own', async () => {
    const onRead = vi.fn();
    const { unmount } = draw({ ...SETUP_DEFAULTS, reach: { ...SETUP_DEFAULTS.reach, onRead } });
    await userEvent.click(head('Reach'));

    let read = row(section('Reach'), 'Read by agents outside it');
    expect(within(read).getByText(/Read by sessions in default and by Ask Daoris, never written\. The default\./)).toBeInTheDocument();
    await userEvent.click(within(read).getByRole('button', { name: 'Set for this repository' }));
    await userEvent.click(within(read).getByRole('radio', { name: 'Off' }));
    expect(onRead).toHaveBeenLastCalledWith(false);
    unmount();

    draw({ ...SETUP_OWN, reach: { ...SETUP_OWN.reach, onRead } });
    read = row(section('Reach'), 'Read by agents outside it');
    expect(within(read).getByRole('radio', { name: 'Off' })).toHaveAttribute('aria-checked', 'true');
    await userEvent.click(within(read).getByRole('button', { name: 'Clear' }));
    expect(onRead).toHaveBeenLastCalledWith(undefined);
  });

  it('declares what its sessions also write into, toward its own workspace only, and takes one back', async () => {
    const onWrite = vi.fn();
    draw({ ...SETUP_OWN, reach: { ...SETUP_OWN.reach, onWrite } });

    expect(within(screen.getByRole('list', { name: 'engine writes into' })).getByText('game')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Stop engine writing into game' }));
    expect(onWrite).toHaveBeenLastCalledWith('game', false);

    const user = userEvent.setup();
    screen.getByRole('combobox', { name: 'let engine write into…' }).focus();
    await user.keyboard('{Enter}');
    expect((await screen.findAllByRole('option')).map((option) => option.textContent)).toEqual(['sandbox']);
    await user.click(screen.getByRole('option', { name: 'sandbox' }));
    expect(onWrite).toHaveBeenLastCalledWith('sandbox', true);
  });

  it("lists Claude Code's rules for this repository, removes one and adds one", async () => {
    const onAddRule = vi.fn();
    const onRemoveRule = vi.fn();
    draw({ ...SETUP_OWN, reach: { ...SETUP_OWN.reach, onAddRule, onRemoveRule } });
    expect(head('Reach')).toHaveAttribute('aria-expanded', 'true');

    const rules = screen.getByRole('list', { name: "Claude Code's rules here" });
    expect(within(rules).getByText('Bash(npm run test:*)')).toBeInTheDocument();
    expect(within(rules).getByText('Bash(git push:*)')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'remove Bash(git push:*)' }));
    expect(onRemoveRule).toHaveBeenLastCalledWith('Bash(git push:*)');

    await userEvent.click(screen.getByRole('button', { name: 'Add a rule' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'the rule' }), 'Edit(/docs/**){Enter}');
    expect(onAddRule).toHaveBeenLastCalledWith('allow', 'Edit(/docs/**)', expect.any(Function));
  });

  it('says it reads nobody, writes into another and counts its rules in its folded line', async () => {
    draw(SETUP_OWN);
    await userEvent.click(head('Reach'));
    expect(head('Reach')).toHaveAccessibleDescription('read by no agent outside it · writes into game · 2 rules of its own');
  });
});

/**
 * REVIEWENV1a (D154 point 2, design §1.7–§1.8): *Review before landing*, beside how its work lands, as the driver resolves it.
 * Nothing set says today's behaviour; a workspace's rule says so and offers *Set for this repository*; its own carries Clear,
 * which hands it back to its workspace's. Each says it is declared only, until the gate reads it.
 */
describe("a repository's review before landing", () => {
  const withReview = (review: NonNullable<RepositorySetupProps['work']>['review'], onReview = vi.fn()) => ({
    ...SETUP_DEFAULTS, work: { ...SETUP_DEFAULTS.work, review, onReview },
  });

  it("says none set as today's behaviour, Daoris's default in its folded line, and its terminal twin", async () => {
    draw(withReview({ repository: 'engine', workspace: 'default' }));

    expect(head('Line and landing')).toHaveAccessibleDescription(
      "line main (the workspace's default) · lands into its line (Daoris's default) · no review environment (Daoris's default)");
    await userEvent.click(head('Line and landing'));
    const review = row(section('Line and landing'), 'Review before landing');
    expect(review).toHaveTextContent('None: work is offered to land once its quest is done.');
    expect(within(review).getByText(code('daoris driver review engine <environment> --kind local|deployed --procedure <path>')))
      .toBeInTheDocument();
    expect(within(review).getByRole('button', { name: 'Set for this repository' })).toBeInTheDocument();
  });

  it("says its workspace's rule and what the gate does with it, and sets one of its own or none in place", async () => {
    const onReview = vi.fn();
    draw(withReview({
      repository: 'engine', workspace: 'work', source: 'workspace',
      rule: { required: true, environments: [{ name: 'dev', kind: 'local', procedure: 'README.md', address: 'http://localhost:4200' }] },
    }, onReview));
    await userEvent.click(head('Line and landing'));
    const review = row(section('Line and landing'), 'Review before landing');

    expect(review).toHaveTextContent('Before work here lands, it is shown to you in dev and waits for you to say it is right.');
    // What the landing's gate does with it since REVIEWENV1c (REVIEWENV1c2), never that nothing reads it.
    expect(review).toHaveTextContent('Where work here waits for your review, it lands only once you say it is reviewed');
    expect(review).not.toHaveTextContent('Declared only');
    expect(review).toHaveTextContent('From the workspace work.');
    await userEvent.click(within(review).getByRole('button', { name: 'Set for this repository' }));
    await userEvent.click(within(review).getByRole('button', { name: 'None here' }));
    expect(onReview).toHaveBeenLastCalledWith({ none: true });
  });

  it('opens on a rule of its own, which carries Clear, handing it back to its workspace', async () => {
    const onReview = vi.fn();
    draw(withReview({
      repository: 'engine', workspace: 'work', source: 'repository',
      rule: { environments: [{ name: 'dev', kind: 'deployed', procedure: 'docs/deploying-to-dev.md' }] },
    }, onReview));

    expect(head('Line and landing')).toHaveAttribute('aria-expanded', 'true');
    const review = row(section('Line and landing'), 'Review before landing');
    expect(review).toHaveTextContent('A set-up step here follows docs/deploying-to-dev.md toward dev');
    expect(review).toHaveTextContent('Set for this repository.');
    await userEvent.click(within(review).getByRole('button', { name: 'Clear' }));
    expect(onReview).toHaveBeenLastCalledWith({ clear: true });
  });

  it('is absent on a shell older than it', () => {
    draw(SETUP_OWN);
    expect(screen.queryByText('Review before landing')).toBeNull();
  });
});

/**
 * XAGENT1a (D155 point 3, design §2.5–§2.6): *Second opinion before landing*, beside *Review before landing*, as the driver
 * resolves it. Nothing set says today's behaviour; a workspace's rule says so and offers *Set for this repository*; its own
 * carries Clear, which hands it back to its workspace's. Each says it is declared only, until the gate reads it.
 */
describe("a repository's second opinion before landing", () => {
  const withOpinion = (opinion: NonNullable<RepositorySetupProps['work']>['opinion'], onOpinion = vi.fn()) => ({
    ...SETUP_DEFAULTS, work: { ...SETUP_DEFAULTS.work, opinion, onOpinion },
  });

  it("says none set as today's behaviour, Daoris's default in its folded line, and its terminal twin", async () => {
    draw(withOpinion({ repository: 'engine', workspace: 'default' }));

    expect(head('Line and landing')).toHaveAccessibleDescription(
      "line main (the workspace's default) · lands into its line (Daoris's default) · no second opinion (Daoris's default)");
    await userEvent.click(head('Line and landing'));
    const opinion = row(section('Line and landing'), 'Second opinion before landing');
    expect(opinion).toHaveTextContent('None: no other agent reads work here.');
    expect(within(opinion).getByText(code('daoris driver opinion engine --reviewers <adapter,adapter>'))).toBeInTheDocument();
    expect(within(opinion).getByRole('button', { name: 'Set for this repository' })).toBeInTheDocument();
  });

  it("says its workspace's rule, the working agent's own family and that it is declared only, and sets none in place", async () => {
    const onOpinion = vi.fn();
    draw(withOpinion({
      repository: 'engine', workspace: 'work', source: 'workspace',
      rule: { on: ['landing'], reviewers: ['codex-acp', 'claude-code-acp'], required: true, minutes: 20, recheck: true, sameAgent: ['claude-code-acp'] },
    }, onOpinion));
    await userEvent.click(head('Line and landing'));
    const opinion = row(section('Line and landing'), 'Second opinion before landing');

    expect(opinion).toHaveTextContent('Before work here lands, codex-acp, else claude-code-acp, reads it, in a copy of its own');
    expect(opinion).toHaveTextContent('If no reviewer can read it, the work waits for you.');
    expect(opinion).toHaveTextContent('claude-code-acp is the same agent as the one that does the work here');
    expect(opinion).toHaveTextContent('Declared only: nothing reads it yet');
    expect(opinion).toHaveTextContent('From the workspace work.');
    await userEvent.click(within(opinion).getByRole('button', { name: 'Set for this repository' }));
    await userEvent.click(within(opinion).getByRole('button', { name: 'None here' }));
    expect(onOpinion).toHaveBeenLastCalledWith({ none: true });
  });

  it('opens on a rule of its own, which carries Clear, handing it back to its workspace', async () => {
    const onOpinion = vi.fn();
    draw(withOpinion({
      repository: 'engine', workspace: 'work', source: 'repository',
      rule: { on: ['steps'], reviewers: ['dsh'], minutes: 30, recheck: true, sameAgent: [] },
    }, onOpinion));

    expect(head('Line and landing')).toHaveAttribute('aria-expanded', 'true');
    const opinion = row(section('Line and landing'), 'Second opinion before landing');
    expect(opinion).toHaveTextContent("Before a chain's next step starts, dsh reads the work of the step before it here");
    expect(opinion).toHaveTextContent('Set for this repository.');
    await userEvent.click(within(opinion).getByRole('button', { name: 'Clear' }));
    expect(onOpinion).toHaveBeenLastCalledWith({ clear: true });
  });

  it('is absent on a shell older than it', () => {
    draw(SETUP_OWN);
    expect(screen.queryByText('Second opinion before landing')).toBeNull();
  });
});

describe('Setup in 中文', () => {
  it('names its sections and marks a default in Chinese', async () => {
    await i18n.changeLanguage('zh');
    draw(SETUP_DEFAULTS);

    for (const name of ['驱动', '主线与落地', '会话', '边界']) expect(head(name)).toBeInTheDocument();
    expect(head('主线与落地')).toHaveAccessibleDescription('主线 main（工作区的默认） · 落地时并入主线（Daoris 的默认）');
    await userEvent.click(head('主线与落地'));
    // Its line and its landing each stand from above, so each offers to be set here.
    expect(within(section('主线与落地')).getAllByRole('button', { name: '为此仓库设定' })).toHaveLength(2);
  });
});
