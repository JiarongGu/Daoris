import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
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
  /** PUSH1b2: the shell pushes a repository whose line is not judged yet with `notReadYet`, read before the source. */
  it('says a line not read yet as that, not as no line', async () => {
    const line = { repository: 'engine', workspace: 'default', source: 'none' as const, notReadYet: true };
    draw({ ...SETUP_DEFAULTS, work: { ...SETUP_DEFAULTS.work, line } });
    expect(head('Line and landing').getAttribute('aria-describedby')).not.toBeNull();
    expect(head('Line and landing')).toHaveAccessibleDescription(/line not read yet/);
    await userEvent.click(head('Line and landing'));
    const says = row(section('Line and landing'), 'Its line');
    expect(within(says).getByText(/Not read yet/)).toBeInTheDocument();
    expect(within(says).queryByText(/No line/)).toBeNull();
  });

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

  /** PLUGUI1c2c: the landing plugin is a door to that plugin's page; absent the opener, its name stays words. */
  it("opens the plugin's page from its name in the landing sentence, and keeps the sentence whole", async () => {
    const onOpenPlugin = vi.fn();
    draw({ ...SETUP_OWN, onOpenPlugin });

    const landing = row(section('Line and landing'), 'How work lands');
    expect(within(landing).getByText(/On a branch/)).toHaveTextContent(
      'On a branch feature/{quest}-{slug}, set for this repository. Then plugin github-pull-request pushes it and opens the pull request. Accepted automatically when its quest is done.');
    await userEvent.click(within(landing).getByRole('button', { name: 'github-pull-request' }));
    expect(onOpenPlugin).toHaveBeenCalledWith('github-pull-request');
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
    expect(onLanding).toHaveBeenLastCalledWith({ form: 'branch', pattern: 'feature/{quest}-{slug}', squash: false });
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
    expect(within(review).getByText(code('daoris driver review engine <environment> --kind local|deployed --procedure <path>|none|--drop <environment>|--clear')))
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
 * carries Clear, which hands it back to its workspace's. Each says what the gate does with it (XAGENT1f4).
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
    expect(within(opinion).getByText(code('daoris driver opinion engine --reviewers <adapter,adapter>|none|--clear'))).toBeInTheDocument();
    expect(within(opinion).getByRole('button', { name: 'Set for this repository' })).toBeInTheDocument();
  });

  it("says its workspace's rule, the working agent's own family and what the gate does with it, and sets none in place", async () => {
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
    // XAGENT1f4: the twins' gate sentence, its terminal doors drawn as code.
    expect(opinion).toHaveTextContent("Where work here waits for another agent's reading, it lands only once that reading is settled");
    expect(within(opinion).getByText(code('daoris-driver opinion anyway <session>'))).toBeInTheDocument();
    expect(opinion).not.toHaveTextContent('Declared only');
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
    expect(opinion).toHaveTextContent("Where a chain's next step here waits for another agent's reading of the step before it");
    expect(opinion).toHaveTextContent('Set for this repository.');
    await userEvent.click(within(opinion).getByRole('button', { name: 'Clear' }));
    expect(onOpinion).toHaveBeenLastCalledWith({ clear: true });
  });

  it('is absent on a shell older than it', () => {
    draw(SETUP_OWN);
    expect(screen.queryByText('Second opinion before landing')).toBeNull();
  });
});

/**
 * DOORS1b (D164's amendment points 1 and 3): *Commits* on *Line and landing*, as Ask Daoris's room names it, as the driver
 * resolves it: Daoris's default where nothing is set, its workspace's with *Set for this repository*, its own with Clear.
 */
describe("a repository's commit rule", () => {
  const withCommits = (commits: NonNullable<RepositorySetupProps['work']>['commits'], onCommits = vi.fn()) => ({
    ...SETUP_DEFAULTS, work: { ...SETUP_DEFAULTS.work, commits, onCommits },
  });

  it("says Daoris's default in its folded line and its row, with its terminal twin, and sets one here", async () => {
    const onCommits = vi.fn();
    draw(withCommits({ repository: 'engine', workspace: 'default', rule: { mode: 'as-you-go' }, source: 'default' }, onCommits));

    expect(head('Line and landing')).toHaveAccessibleDescription(
      "line main (the workspace's default) · lands into its line (Daoris's default) · commits as it goes (Daoris's default)");
    await userEvent.click(head('Line and landing'));
    const commits = row(section('Line and landing'), 'Commits');
    expect(commits).toHaveTextContent("Each piece that passes its checks is committed as the work goes. Daoris's default: nothing is set.");
    expect(within(commits).getByText(code('daoris driver commits engine --mode as-you-go|ask|at-end --convention "…"|--clear')))
      .toBeInTheDocument();
    await userEvent.click(within(commits).getByRole('button', { name: 'Set for this repository' }));
    await userEvent.click(within(commits).getByRole('radio', { name: 'At the end' }));
    await userEvent.click(within(commits).getByRole('button', { name: 'Save' }));
    expect(onCommits).toHaveBeenLastCalledWith({ set: { mode: 'at-end', convention: '' } });
  });

  it("says its workspace's rule with the convention quoted, and its own opens the section with Clear", async () => {
    const onCommits = vi.fn();
    draw(withCommits({ repository: 'engine', workspace: 'work', rule: { mode: 'ask', convention: 'Say why.' }, source: 'repository' }, onCommits));

    expect(head('Line and landing')).toHaveAttribute('aria-expanded', 'true');
    const commits = row(section('Line and landing'), 'Commits');
    expect(commits).toHaveTextContent(
      'Each commit waits for your yes before it is made. Each session is told how a commit message is written: “Say why.” Set for this repository.');
    await userEvent.click(within(commits).getByRole('button', { name: 'Clear' }));
    expect(onCommits).toHaveBeenLastCalledWith({ clear: true });
  });

  it('is absent on a shell older than it', () => {
    draw(SETUP_OWN);
    expect(screen.queryByText('Commits')).toBeNull();
  });
});

/**
 * DOORS1b (D163 point 3, D164's amendment point 3): *Helpers* on *Sessions*, as Ask Daoris's room names it, as the driver
 * resolves it, beside the language its sessions write in.
 */
describe("a repository's helpers rule", () => {
  const withHelpers = (helpers: NonNullable<RepositorySetupProps['sessions']>['helpers'], onHelpers = vi.fn()) => ({
    ...SETUP_DEFAULTS, sessions: { ...SETUP_DEFAULTS.sessions, helpers, onHelpers },
  });

  it("says its workspace's rule in its folded line and its row, and sets one here", async () => {
    const onHelpers = vi.fn();
    draw(withHelpers({ repository: 'engine', workspace: 'work', rule: { mode: 'on', agents: ['codex-acp', 'dsh'] }, source: 'workspace' }, onHelpers));

    expect(head('Sessions')).toHaveAccessibleDescription(
      "no session language (Daoris's default) · helpers start at once (the workspace's default) · no standing answer");
    await userEvent.click(head('Sessions'));
    const helpers = row(section('Sessions'), 'Helpers');
    expect(helpers).toHaveTextContent('A helper an agent asks for starts at once, within the limits: two at once for a session, one turn each.');
    expect(helpers).toHaveTextContent('Only codex-acp, else dsh, may help, tried in that order unless the session names one of them.');
    expect(helpers).toHaveTextContent('From the workspace work.');
    expect(within(helpers).getByText(code('daoris driver helpers engine --mode ask|on|off --agents <adapter,adapter>|--clear')))
      .toBeInTheDocument();
    await userEvent.click(within(helpers).getByRole('button', { name: 'Set for this repository' }));
    await userEvent.click(within(helpers).getByRole('radio', { name: 'Ask me' }));
    await userEvent.click(within(helpers).getByRole('button', { name: 'Save' }));
    expect(onHelpers).toHaveBeenLastCalledWith({ set: { mode: 'ask', agents: ['codex-acp', 'dsh'] } });
  });

  it('opens Sessions on a rule of its own, which carries Clear', async () => {
    const onHelpers = vi.fn();
    draw(withHelpers({ repository: 'engine', workspace: 'work', rule: { mode: 'off', agents: [] }, source: 'repository' }, onHelpers));

    expect(head('Sessions')).toHaveAttribute('aria-expanded', 'true');
    const helpers = row(section('Sessions'), 'Helpers');
    expect(helpers).toHaveTextContent('A helper is not offered to a session here: only your own press asks one. Set for this repository.');
    await userEvent.click(within(helpers).getByRole('button', { name: 'Clear' }));
    expect(onHelpers).toHaveBeenLastCalledWith({ clear: true });
  });

  it('is absent on a shell older than it', () => {
    draw(SETUP_OWN);
    expect(screen.queryByText('Helpers')).toBeNull();
  });
});

/**
 * AUTONOMY1d (D184 points 1-3): *Sessions' access*, beside its line and landing: full access confirmed once, then each pause with
 * its value and where it was set, as the driver resolves them (`LINES`' `access`).
 */
describe("a repository's sessions' access", () => {
  const pauses = (overrides: Record<string, { value: string; source: string }> = {}) => [
    { pause: 'permissions', value: 'auto', source: 'default' },
    { pause: 'routing', value: 'person', source: 'default' },
    { pause: 'landing', value: 'person', source: 'default' },
    { pause: 'goAheads', value: 'named', source: 'default' },
    { pause: 'questions', value: 'claude-code', source: 'default' },
  ].map((each) => ({ ...each, ...overrides[each.pause] }));
  const withAccess = (access: NonNullable<RepositorySetupProps['access']>) => ({ ...SETUP_DEFAULTS, access });

  it("folds beside Line and landing to a line naming each pause, at Daoris's default marked as such", () => {
    draw(withAccess({ workspace: 'default', pauses: pauses(), aboveFull: false, onChange: vi.fn() }));

    const [work, access, sessions] = ['Line and landing', "Sessions' access", 'Sessions'].map(head);
    expect(work!.compareDocumentPosition(access!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(access!.compareDocumentPosition(sessions!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(head("Sessions' access")).toHaveAccessibleDescription(
      "permissions in auto mode, you accept each route, lands on your Accept…, go-aheads before named acts (Daoris's default)");
  });

  it('opens on a pause set for this repository, whose change and Clear go out as the edit', async () => {
    const onChange = vi.fn();
    draw(withAccess({
      workspace: 'aurora',
      pauses: pauses({ permissions: { value: 'asking', source: 'workspace' }, landing: { value: 'rule', source: 'repository' } }),
      aboveFull: false,
      onChange,
    }));

    expect(head("Sessions' access")).toHaveAttribute('aria-expanded', 'true');
    const access = section("Sessions' access");
    expect(row(access, 'Permission prompts')).toHaveTextContent('From the workspace aurora.');
    // ACCESSWORDS1: a merge rule still waits for the person's Accept, and the sentence says so before its source.
    expect(row(access, 'Landing')).toHaveTextContent('Finished work lands by the landing rule, with no press. A merge rule still waits for your Accept…. Set for this repository.');
    await userEvent.click(within(row(access, 'Landing')).getByRole('button', { name: 'Clear' }));
    expect(onChange).toHaveBeenLastCalledWith({ access: { landing: null } });
    await userEvent.click(within(row(access, 'Permission prompts')).getByRole('button', { name: 'Set for this repository' }));
    await userEvent.click(within(row(access, 'Permission prompts')).getByRole('radio', { name: 'Auto mode' }));
    expect(onChange).toHaveBeenLastCalledWith({ access: { permissions: 'auto' } });
  });

  it('turns full access on only through its ask, and folds to full access where it is on', async () => {
    const onChange = vi.fn();
    draw(withAccess({ workspace: 'default', pauses: pauses(), aboveFull: false, onChange }));

    await userEvent.click(head("Sessions' access"));
    const access = section("Sessions' access");
    await userEvent.click(within(access).getByRole('checkbox', { name: 'Full access' }));
    await userEvent.click(within(access).getByRole('button', { name: 'Enable full access' }));
    expect(onChange).toHaveBeenLastCalledWith({ access: 'full' }, expect.anything());
  });

  it("reads full access from the workspace in its folded line, in Chinese too", async () => {
    const full = pauses({
      permissions: { value: 'unrestricted', source: 'workspace' }, routing: { value: 'intake', source: 'workspace' },
      landing: { value: 'rule', source: 'workspace' }, goAheads: { value: 'never', source: 'workspace' },
      questions: { value: 'claude-code', source: 'workspace' },
    });
    const { unmount } = draw(withAccess({ workspace: 'default', pauses: full, aboveFull: true, onChange: vi.fn() }));
    expect(head("Sessions' access")).toHaveAccessibleDescription("full access (the workspace's default)");
    unmount();

    await i18n.changeLanguage('zh');
    draw(withAccess({ workspace: 'default', pauses: full, aboveFull: true, onChange: vi.fn() }));
    expect(head('会话的访问')).toHaveAccessibleDescription('完全访问（工作区的默认）');
  });

  it('is absent on a shell older than it', () => {
    draw(SETUP_OWN);
    expect(screen.queryByRole('button', { name: "Sessions' access" })).toBeNull();
  });
});

/**
 * DOORS1b (D164's amendment point 2): the landing row says what finds its pattern's `{ticket}` and that Accept squashes, and its
 * control sets the key where the pattern says `{ticket}` and the squash on a branch, keeping both where the press does not
 * change them.
 */
describe("a repository's landing, its ticket's key and its squash", () => {
  const withLanding = (landing: NonNullable<RepositorySetupProps['work']>['landing'], onLanding = vi.fn()) => ({
    ...SETUP_DEFAULTS, work: { ...SETUP_DEFAULTS.work, landing, onLanding },
  });

  it('says the key that finds the ticket and the squash, and sends both back as they stand', async () => {
    const onLanding = vi.fn();
    draw(withLanding({
      repository: 'engine', workspace: 'default', form: 'branch', pattern: 'feature/{ticket}-{slug}', key: 'TK-\\d+', squash: true,
      source: 'repository',
    }, onLanding));

    // A rule of its own opens the section; folded, its line names the squash.
    await userEvent.click(head('Line and landing'));
    expect(head('Line and landing')).toHaveAccessibleDescription(
      "line main (the workspace's default) · lands on feature/{ticket}-{slug}, squashed to one commit");
    await userEvent.click(head('Line and landing'));
    const landing = row(section('Line and landing'), 'How work lands');
    expect(landing).toHaveTextContent(
      '{ticket} is the first match of TK-\\d+ in the title, then the request, then its links, as written; where none is found, the '
      + "session's id stands in.");
    expect(landing).toHaveTextContent("Squashed: Accept makes the pull request's branch one commit from the session's work");
    const key = within(landing).getByRole('textbox', { name: "The ticket's key for engine" });
    expect(key).toHaveValue('TK-\\d+');
    expect(within(landing).getByRole('checkbox', { name: 'Squash into one commit' })).toBeChecked();
    await userEvent.click(within(landing).getByRole('checkbox', { name: 'Clean up once landed' }));
    await userEvent.click(within(landing).getByRole('button', { name: 'Save' }));
    expect(onLanding).toHaveBeenLastCalledWith({
      form: 'branch', pattern: 'feature/{ticket}-{slug}', key: 'TK-\\d+', squash: true, tidy: true,
    });
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
