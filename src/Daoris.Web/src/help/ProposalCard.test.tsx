import { describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { code } from '../test/code';
import { RELEASE_CARD } from '../workflow/edit/proposalFixtures';
import { type HelpProposal, ProposalCard } from './ProposalCard';

// A change Ask Daoris proposes, and the person's two presses.

const LANDING = {
  id: 'p1a2b3c4', kind: 'workflow' as const,
  describe: 'Choose workflow `docs-to-pr` for `web-app`, its work kept on branch `feature/{quest}-{slug}`.',
  terminal: 'daoris workflow use docs-to-pr --scope web-app',
  why: 'the person asked for feature branches',
};

describe('a proposal of Ask Daoris\'s', () => {
  it('says what it changes, the command that does the same, and why', () => {
    render(<ul><ProposalCard proposal={LANDING} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(screen.getByText('Ask Daoris proposes')).toBeInTheDocument();
    expect(screen.getByText(code('feature/{quest}-{slug}'))).toBeInTheDocument();
    expect(screen.getByText(code(LANDING.terminal))).toBeInTheDocument();
    expect(screen.getByText('Why: the person asked for feature branches')).toBeInTheDocument();
  });

  it('is applied or not by the person, and does neither while a press is on its way', async () => {
    const onApply = vi.fn();
    const onDismiss = vi.fn();
    const { rerender } = render(<ul><ProposalCard proposal={LANDING} onApply={onApply} onDismiss={onDismiss} /></ul>);

    await userEvent.click(screen.getByRole('button', { name: 'Apply' }));
    await userEvent.click(screen.getByRole('button', { name: 'Not now' }));
    expect(onApply).toHaveBeenCalledWith('p1a2b3c4');
    expect(onDismiss).toHaveBeenCalledWith('p1a2b3c4');

    rerender(<ul><ProposalCard proposal={LANDING} pending onApply={onApply} onDismiss={onDismiss} /></ul>);
    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled();
  });
});


const GO: HelpProposal = {
  id: 'p5', kind: 'go', describe: 'Open Settings → Get started at step 2, Ask Daoris\'s agent.', terminal: '',
  why: 'the person asked where to name its agent',
};

const press = async (proposal: HelpProposal, apply: string, dismiss = 'Not now') => {
  const onApply = vi.fn();
  const onDismiss = vi.fn();
  render(<ul><ProposalCard proposal={proposal} onApply={onApply} onDismiss={onDismiss} /></ul>);
  await userEvent.click(screen.getByRole('button', { name: apply }));
  await userEvent.click(screen.getByRole('button', { name: dismiss }));
  expect(onApply).toHaveBeenCalledWith(proposal.id);
  expect(onDismiss).toHaveBeenCalledWith(proposal.id);
};

describe('the kinds that reach every door', () => {
  it('a go names the place, carries no command since it changes nothing, and its Apply is a go', async () => {
    await press(GO, 'Go there', 'Dismiss');

    expect(screen.getByText('Ask Daoris suggests a screen')).toBeInTheDocument();
    expect(screen.getByText(GO.describe)).toBeInTheDocument();
    expect(screen.getByText(/changes nothing/)).toBeInTheDocument();
    expect(screen.queryByText(/the same at a terminal/)).not.toBeInTheDocument();
  });

  /**
   * A go's second press only takes the card away, so it is worded as that, never as Not now, which answers a change;
   * and a go to a quest a session took says so in the driver's sentence. The card offers no act on the quest.
   */
  it('a go to a taken quest says it is taken, and its second press is a dismiss, no act on the quest', async () => {
    const taken: HelpProposal = {
      id: 'g6t7k8n9', kind: 'go', terminal: '', why: 'the person asked where their quest went',
      describe: 'Open Quests → quest `#q2taken0` “Stream the tiles”, taken.',
    };
    await press(taken, 'Go there', 'Dismiss');

    expect(screen.getByText(/, taken\.$/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Not now' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('button').map((button) => button.textContent)).toEqual(['Go there', 'Dismiss']);
  });

  // The design's card (§8.2): the title by version, Your part first, the diff, the outward acts, needs, applies to.
  const WORKFLOW: HelpProposal = {
    id: 'p21', kind: 'workflow', terminal: 'daoris workflow edit docs-to-pr --base 1', why: 'the person asked',
    describe: 'Save version 2 of workflow `docs-to-pr` for `web-app`: new work only.',
    workflow: {
      id: 'docs-to-pr', name: 'Documentation to a pull request', base: 1, version: 2, scope: 'web-app',
      diff: ['− Your look in local', '~ Landing: you accept'], yourPart: ['= You still merge its pull request'],
      outward: ['azure-devops-pull-request pushes the branch and opens a pull request.'], needs: [],
      appliesTo: 'New work only; 2 runs keep the version they started on.', choose: false,
    },
  };
  const headings = () => screen.getAllByRole('heading').map((each) => each.textContent);

  it('a workflow card reads the sections in the design\'s order, the driver\'s lines as sent, with Apply and Not now', async () => {
    await press(WORKFLOW, 'Apply');

    expect(screen.getByText('v2 of Documentation to a pull request, from v1')).toBeInTheDocument();
    expect(headings()).toEqual(['Your part', 'Steps', 'Without asking you each time', 'Applies to']);
    expect(screen.getByText('= You still merge its pull request')).toBeInTheDocument();
    expect(screen.getByText('− Your look in local')).toBeInTheDocument();
    expect(screen.getByText(/azure-devops-pull-request pushes the branch/)).toBeInTheDocument();
    expect(screen.getByText(/2 runs keep the version/)).toBeInTheDocument();
    expect(screen.queryByText(/chosen for/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /editor/ })).not.toBeInTheDocument();
  });

  it('a new workflow\'s card is titled v1 and says nothing leaves the machine without the person when no act is outward', () => {
    const made = { ...WORKFLOW.workflow!, base: null, version: 1, outward: [] };
    render(<ul><ProposalCard proposal={{ ...WORKFLOW, workflow: made }} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(screen.getByText('v1 of Documentation to a pull request')).toBeInTheDocument();
    expect(screen.getByText('Nothing new leaves this machine without you.')).toBeInTheDocument();
  });

  it('a draft shows Needs first and offers only Not now', () => {
    const draft = { ...WORKFLOW.workflow!, needs: ['The plugin `azure-devops-pull-request` is not installed.'] };
    render(<ul><ProposalCard proposal={{ ...WORKFLOW, workflow: draft }} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(headings()).toContain('Needs first');
    expect(screen.getByText(code('azure-devops-pull-request'), { exact: false })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Apply' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Not now' })).toBeInTheDocument();
  });

  // A change to the open draft saves and chooses nothing: its changes, what to put right, and one press opening the editor.
  const DRAFTED: HelpProposal = {
    ...RELEASE_CARD,
    workflow: {
      ...RELEASE_CARD.workflow!,
      problems: [
        { code: 'land-tail', node: 'landing', wire: null, words: 'node `landing`: a `merge` land brings no `pushed`.' },
        { code: 'a-rule-newer-than-this-page', node: null, wire: null, words: 'a rule this page has no words for refuses `publish`.' },
      ],
    },
  };

  it('a change to the open draft lists its changes and what to put right, saves nothing, and its one press opens the editor', async () => {
    const onApply = vi.fn();
    render(<ul><ProposalCard proposal={DRAFTED} onApply={onApply} onDismiss={vi.fn()} /></ul>);

    expect(screen.getByText('A change to your draft')).toBeInTheDocument();
    expect(screen.getByText('For the workflow draft open for engine')).toBeInTheDocument();
    expect(headings()).toEqual(['Its changes', 'To put right before it saves']);
    expect(within(screen.getByRole('region', { name: 'Its changes' })).getAllByRole('listitem')).toHaveLength(16);
    expect(screen.getByText('This step comes with a landing method: choose that method on the landing and it brings the step.')).toBeInTheDocument();
    expect(screen.getByText(/a rule this page has no words for refuses/)).toBeInTheDocument();
    expect(screen.getByText(/^Nothing is saved or chosen: you take its changes into the draft in its editor/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Apply' })).not.toBeInTheDocument();
    expect(screen.queryByText(/^v0 of/)).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Open in the editor' }));
    expect(onApply).toHaveBeenCalledWith('p7');
    expect(screen.getByRole('button', { name: 'Not now' })).toBeInTheDocument();
  });

  it('a change to the open draft in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<ul><ProposalCard proposal={DRAFTED} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(screen.getByText('对你草稿的更改')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '在编辑器中打开' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  // A repository whose landing rule declares no gates is said in the page's words; a cannot is a need, the driver's sentence as sent.
  it('says, per repository it reaches, that the version integrates with no gates, in English and in 中文', async () => {
    const chosen = { ...WORKFLOW.workflow!, choose: true, noGates: ['engine', 'game'] };
    const { default: i18n } = await import('../locales/i18n');
    try {
      render(<ul><ProposalCard proposal={{ ...WORKFLOW, workflow: chosen }} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(headings()).toEqual(['Your part', 'Steps', 'Without asking you each time', 'Integrates with no gates', 'Applies to']);
      const lines = screen.getAllByRole('listitem').map((each) => each.textContent).filter((text) => /^\w+ integrates with no gates/.test(text ?? ''));
      expect(lines).toEqual([
        'engine integrates with no gates: its landing rule declares none, so the line moves on your Accept as a merge does today, and a version with no press is refused there.',
        'game integrates with no gates: its landing rule declares none, so the line moves on your Accept as a merge does today, and a version with no press is refused there.',
      ]);
      cleanup();

      await i18n.changeLanguage('zh');
      render(<ul><ProposalCard proposal={{ ...WORKFLOW, workflow: chosen }} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(headings()).toEqual(['你的部分', '步骤', '不再每次询问你', '集成不跑检查项', '适用于']);
      const said = screen.getAllByRole('listitem').map((each) => each.textContent).filter((text) => /^\w+ 集成时不跑检查项/.test(text ?? ''));
      expect(said).toHaveLength(2);
      expect(said[0]).toMatch(/^engine 集成时不跑检查项：/);
    } finally {
      cleanup();
      await i18n.changeLanguage('en');
    }
  });

  it('draws the cannot sentences as needs, as the driver sent them, and no section for a card with no gates said', () => {
    const cannot = '`engine`: its `merge-in` (`integrate`) integrates the branch the landing `land` makes, and the landing rule names a pattern in Line and landing.';
    render(<ul><ProposalCard proposal={{ ...WORKFLOW, workflow: { ...WORKFLOW.workflow!, needs: [cannot] } }} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(headings()).toContain('Needs first');
    expect(headings()).not.toContain('Integrates with no gates');
    expect(screen.getByText(/integrates the branch the landing/)).toBeInTheDocument();
  });

  it('says the scope also chooses the workflow when it does', () => {
    const chosen = { ...WORKFLOW.workflow!, choose: true };
    render(<ul><ProposalCard proposal={{ ...WORKFLOW, workflow: chosen }} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(screen.getByText('It is also chosen for web-app.')).toBeInTheDocument();
  });

  // A route with no version, a verification pipeline's graph, and what a change asks less of the person.
  const VERIFY = {
    ...WORKFLOW.workflow!, id: 'line-verify', name: 'Verify the line', base: 1, version: 2, choose: true, route: 'landed', saves: true,
    verifies: true, diff: ['~ gates · check'], yourPart: [], outward: ['`gates` asks for a fix of each failed gate with no press of yours.'],
    lowers: ['it moves the check `gates`\'s failure off your press, to `ask`'],
  };

  it('a verification pipeline\'s card leads with what it asks less of you, its nodes, and the work that lands', () => {
    render(<ul><ProposalCard proposal={{ ...WORKFLOW, workflow: VERIFY }} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(headings()).toEqual(['Asks less of you', 'Nodes', 'Without asking you each time', 'Applies to']);
    expect(screen.getByText('A verification pipeline: it checks the work that lands, and lands nothing itself.')).toBeInTheDocument();
    expect(screen.getByText('Only your Apply makes this change.')).toBeInTheDocument();
    expect(screen.getByText('It is chosen for the work that lands in web-app.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Apply' })).toBeInTheDocument();
  });

  it('a route with no version says nothing is saved, and names the kind of task it is chosen for', () => {
    const kept = { ...WORKFLOW.workflow!, base: null, version: 3, choose: true, route: 'kind:docs', saves: false, diff: [], yourPart: [] };
    render(<ul><ProposalCard proposal={{ ...WORKFLOW, workflow: kept }} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(screen.getByText('v3 of Documentation to a pull request, as it is: no version is saved')).toBeInTheDocument();
    expect(screen.getByText('It is chosen for web-app\'s kind of task docs.')).toBeInTheDocument();
    expect(screen.queryByText('Asks less of you')).not.toBeInTheDocument();
  });

  it('speaks 中文 on a verification pipeline\'s and a route\'s card', async () => {
    const { default: i18n } = await import('../locales/i18n');
    await i18n.changeLanguage('zh');
    try {
      render(<ul><ProposalCard proposal={{ ...WORKFLOW, workflow: VERIFY }} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(headings()).toEqual(['减少你的参与', '节点', '不再每次询问你', '适用于']);
      expect(screen.getByText('选用于 web-app 中落地的工作。')).toBeInTheDocument();
      expect(screen.getByText('只有你按下“应用”才会做出这项更改。')).toBeInTheDocument();
      cleanup();
      const kept = { ...WORKFLOW.workflow!, version: 3, choose: true, route: 'kind:docs', saves: false };
      render(<ul><ProposalCard proposal={{ ...WORKFLOW, workflow: kept }} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(screen.getByText('Documentation to a pull request 的 v3，原样选用：不保存新版本')).toBeInTheDocument();
      expect(screen.getByText('选用于 web-app 的工作类型 docs。')).toBeInTheDocument();
    } finally {
      cleanup();
      await i18n.changeLanguage('en');
    }
  });

  it('speaks 中文 in the headings and leaves the driver\'s lines as sent', async () => {
    const { default: i18n } = await import('../locales/i18n');
    await i18n.changeLanguage('zh');
    try {
      render(<ul><ProposalCard proposal={WORKFLOW} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(headings()).toEqual(['你的部分', '步骤', '不再每次询问你', '适用于']);
      expect(screen.getByText('= You still merge its pull request')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '应用' })).toBeInTheDocument();
    } finally {
      cleanup();
      await i18n.changeLanguage('en');
    }
  });

  it('speaks 中文 for every kind, the driver\'s sentence left as it said it', async () => {
    const { default: i18n } = await import('../locales/i18n');
    await i18n.changeLanguage('zh');
    try {
      render(<ul><ProposalCard proposal={LANDING} onApply={vi.fn()} onDismiss={vi.fn()} /><ProposalCard proposal={GO} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(screen.getByText('问道衍提议')).toBeInTheDocument();
      expect(screen.getByText('问道衍建议打开一个界面')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '前往' })).toBeInTheDocument();
      // The go card's dismiss takes it out of the list, as a notice's does; the change's card keeps its 暂不.
      expect(screen.getByRole('button', { name: '移除' })).toBeInTheDocument();
      expect(screen.getAllByRole('button', { name: '暂不' })).toHaveLength(1);
      expect(screen.getByText(GO.describe)).toBeInTheDocument();
    } finally {
      cleanup();
      await i18n.changeLanguage('en');
    }
  });
});
