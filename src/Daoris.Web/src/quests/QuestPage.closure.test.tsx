import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import { REOPENED, SEALED, SOURCE_REOPENED, SUBMITTED, TAKEN, WITHDRAWN } from './fixtures';
import { QuestPage } from './QuestPage';

// FABRIC-CLOSE1d and CLOSE1c3 (the fabric events design §E5; D173's CLOSE1a, CLOSE1c and WORKFLOW2d notes; D156): a closure
// graph's quest reads *Submitted*, then the gate or hold it waits on, then done; the person reopens a sealed submission here,
// and a step its source's reopen holds says so in its own words, with go on and abandon and never the yes.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const page = (over: Partial<Parameters<typeof QuestPage>[0]> = {}) => {
  const props = { quest: SUBMITTED, onRespond: vi.fn(), onDismiss: vi.fn(), onOpenQuest: vi.fn(), ...over };
  render(<QuestPage {...props} />);
  return props;
};

const header = () => screen.getByRole('main').querySelector('header')!;

describe("a closure graph's quest on its page", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('reads Submitted, then the hold it waits on, and takes the yes for the submission', async () => {
    const props = page({ quest: SUBMITTED, onAccept: vi.fn() });

    expect(within(header()).getByText('submitted')).toBeInTheDocument();
    expect(within(header()).queryByText('taken')).toBeNull();
    expect(header()).toHaveTextContent(/awaits your yes · .* · submitted 2h ago/);
    await userEvent.click(within(screen.getByRole('region', { name: 'What you required' })).getByRole('button', { name: 'Accept the departure' }));
    expect(props.onAccept).toHaveBeenCalledOnce();
  });

  it('says Submitted in Chinese with the glossary’s 交付', async () => {
    await i18n.changeLanguage('zh');
    page({ quest: SUBMITTED, onAccept: vi.fn() });

    expect(within(header()).getByText('已交付')).toBeInTheDocument();
    expect(header()).toHaveTextContent(/待你采纳 · .*交付/);
  });

  it('leaves an unbound quest’s page as it was: taken, and nothing to reopen', () => {
    page({ quest: TAKEN, reopen: { step: 'st0st0', onReopen: vi.fn() } });

    expect(within(header()).getByText('taken')).toBeInTheDocument();
    expect(within(header()).queryByText('submitted')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Reopen…' })).toBeNull();
    expect(screen.queryByRole('region', { name: 'Its source was reopened' })).toBeNull();
  });

  it('reopens a sealed submission on a second press, with the person’s words and the step it names', async () => {
    const onReopen = vi.fn();
    page({ quest: SEALED, reopen: { step: 'st0st0', onReopen } });

    await userEvent.click(screen.getByRole('button', { name: 'Reopen…' }));
    const ask = screen.getByRole('group', { name: 'reopen this submission' });
    expect(ask).toHaveTextContent('#st0st0');
    await userEvent.type(within(ask).getByRole('textbox'), 'The cold cache still seams.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Reopen submission' }));

    expect(onReopen).toHaveBeenCalledWith('The cold cache still seams.', expect.anything());
  });

  it('offers no reopen where no step stands on its submission', () => {
    page({ quest: SEALED });

    expect(screen.queryByRole('button', { name: 'Reopen…' })).toBeNull();
  });
});

// FABRIC-CLOSE1d3 (after CLOSE1c4 and CLOSE1c8): a reopened source, taken with no submission, says the reopen with the person's
// words as they wrote them and the session told of it, and a submission names the session that said its ready.
describe('a source the person reopened', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const reopenedFact = () => screen.getByText('Reopened').nextElementSibling as HTMLElement;

  it('says the reopen, the person’s words as written, and the session it is told to', () => {
    page({ quest: SOURCE_REOPENED });

    expect(within(header()).getByText('taken')).toBeInTheDocument();
    const fact = reopenedFact();
    expect(fact).toHaveTextContent('1h ago');
    expect(fact).toHaveTextContent('it waits for its agent to make it ready again');
    expect(within(fact).getByText('The cold cache still seams.')).toBeInTheDocument();
    expect(fact).toHaveTextContent('The session that submitted it, s-7f3a21, is told.');
    expect(screen.queryByText('Submitted')).toBeNull();
  });

  it('says a reopen given without words, and names no session where none said the ready', () => {
    page({ quest: { ...SOURCE_REOPENED, reopened: { ...SOURCE_REOPENED.reopened!, note: null, session: null } } });

    const fact = reopenedFact();
    expect(fact).toHaveTextContent('You gave no words with it.');
    expect(fact).not.toHaveTextContent('is told');
  });

  it('says it in Chinese, the person’s words kept as they wrote them', async () => {
    await i18n.changeLanguage('zh');
    page({ quest: SOURCE_REOPENED });

    const fact = screen.getByText('重新打开').nextElementSibling as HTMLElement;
    expect(fact).toHaveTextContent('它等它的智能体再次交付');
    expect(within(fact).getByText('The cold cache still seams.')).toBeInTheDocument();
    expect(fact).toHaveTextContent('交付它的会话 s-7f3a21 会被告知。');
  });

  it('names the session that said a submission’s ready, and none from an older host', async () => {
    page({ quest: { ...SEALED, submission: { ...SEALED.submission!, session: 's-7f3a21' } } });
    expect(screen.getByText('Submitted').nextElementSibling).toHaveTextContent('by session s-7f3a21');
    cleanup();

    page({ quest: SEALED });
    expect(screen.getByText('Submitted').nextElementSibling).not.toHaveTextContent('by session');
    expect(screen.queryByText('Reopened')).toBeNull();
    cleanup();

    await i18n.changeLanguage('zh');
    page({ quest: { ...SEALED, submission: { ...SEALED.submission!, session: 's-7f3a21' } } });
    expect(screen.getByText('交付').nextElementSibling).toHaveTextContent('由会话 s-7f3a21 交付');
  });
});

describe('a step its source’s reopen holds', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('has its own pill and words, never the yes, and goes on once its source is ready again', async () => {
    const onGoOn = vi.fn();
    const onAccept = vi.fn();
    page({ quest: REOPENED, onAccept, goOn: { ready: true, onGoOn } });

    expect(within(header()).getByText(/^source reopened · /)).toBeInTheDocument();
    expect(within(header()).queryByText(/awaits your yes/)).toBeNull();
    const reopened = screen.getByRole('region', { name: 'Its source was reopened' });
    expect(reopened).toHaveTextContent('#se0se0');
    expect(reopened).toHaveTextContent('ready again');
    expect(screen.queryByRole('button', { name: 'Accept the departure' })).toBeNull();

    await userEvent.click(within(reopened).getByRole('button', { name: 'Go on with it' }));
    expect(onGoOn).toHaveBeenCalledOnce();
    await userEvent.click(within(reopened).getByRole('button', { name: 'Abandon…' }));
    expect(screen.getByRole('group', { name: 'decline this quest' })).toBeInTheDocument();
    expect(onAccept).not.toHaveBeenCalled();
  });

  it('waits for its source before going on, and says so', () => {
    page({ quest: REOPENED, goOn: { ready: false, onGoOn: vi.fn() } });

    const reopened = screen.getByRole('region', { name: 'Its source was reopened' });
    expect(reopened).toHaveTextContent('made ready again');
    expect(within(reopened).queryByRole('button', { name: 'Go on with it' })).toBeNull();
    expect(within(reopened).getByRole('button', { name: 'Abandon…' })).toBeInTheDocument();
  });

  it('says a withdrawn step is taken by nobody, and offers no take', () => {
    page({ quest: WITHDRAWN, goOn: { ready: false, onGoOn: vi.fn() } });

    const reopened = screen.getByRole('region', { name: 'Its source was reopened' });
    expect(reopened).toHaveTextContent('withdrawn');
    expect(within(reopened).queryByRole('button', { name: 'Go on with it' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Take' })).toBeNull();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    page({ quest: REOPENED, goOn: { ready: true, onGoOn: vi.fn() } });

    expect(within(header()).getByText(/^来源已重新打开 · /)).toBeInTheDocument();
    const reopened = screen.getByRole('region', { name: '其来源已重新打开' });
    expect(within(reopened).getByRole('button', { name: '在新的工作上继续' })).toBeInTheDocument();
    expect(within(reopened).getByRole('button', { name: '放弃…' })).toBeInTheDocument();
  });
});
