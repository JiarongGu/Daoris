import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import { ACCEPTED, DEPARTED_WHY, HELD, HELD_CJK, MET, OPEN, REQUIRING } from './fixtures';
import { QuestPage } from './QuestPage';

// DRIFT1d2 (D133 §3–§4): a quest's page quotes what the person required, each requirement with its check and how the done
// answered it, met or departed with the reason and the person's words it relied on; a held departure is accepted there in
// one press, and an accepted one says when.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const page = (over: Partial<Parameters<typeof QuestPage>[0]> = {}) => {
  const props = { quest: MET, onRespond: vi.fn(), onDismiss: vi.fn(), onOpenQuest: vi.fn(), ...over };
  render(<QuestPage {...props} />);
  return props;
};

const required = () => screen.getByRole('region', { name: 'What you required' });
const items = () => within(required()).getAllByRole('listitem');

describe("a quest's requirements and how its done answered them", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('quotes each requirement in the person’s words, with its check and how the done met it', () => {
    page({ quest: MET });

    expect(items()).toHaveLength(2);
    const [first, second] = items();
    expect(within(first).getByText('1')).toBeInTheDocument();
    expect(within(first).getByText('use the v3 bridge')).toBeInTheDocument();
    expect(within(first).getByText('Check')).toBeInTheDocument();
    expect(within(first).getByText('the tiles arrive through the v3 bridge, as its trace shows')).toBeInTheDocument();
    expect(within(first).getByText('met')).toBeInTheDocument();
    expect(within(first).getByText(MET.answers![0].met!)).toBeInTheDocument();
    expect(within(second).getByText('a playtest held every frame under the ceiling')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Accept the departure' })).toBeNull();
  });

  it('shows a held departure with its reason and the words it relied on, and takes the yes in one press', async () => {
    const props = page({ quest: HELD, onAccept: vi.fn() });

    expect(required()).toHaveTextContent('Its done departed from what you required, so it is held for your yes');
    // What the yes lets go on: its next step, as the service will publish it.
    expect(required()).toHaveTextContent('Accepting publishes its next step, “Verify he0he0 in a playtest” to game.');
    const departed = items()[1];
    expect(within(departed).getByText('departed')).toBeInTheDocument();
    expect(within(departed).getByText(DEPARTED_WHY)).toBeInTheDocument();
    expect(within(departed).getByText('Relied on')).toBeInTheDocument();
    // The requirement's words, and the same words the departure turned on, each quoted as said.
    expect(within(departed).getAllByText('keep the budget under the ceiling the engine sets')).toHaveLength(2);

    await userEvent.click(within(required()).getByRole('button', { name: 'Accept the departure' }));
    expect(props.onAccept).toHaveBeenCalledOnce();
  });

  it('says in its header that it awaits the person’s yes', () => {
    page({ quest: HELD, onAccept: vi.fn() });

    expect(within(screen.getByRole('main').querySelector('header')!).getByText('awaits your yes')).toBeInTheDocument();
  });

  it('waits while the yes is on its way, and offers none where there is no door', () => {
    page({ quest: HELD, onAccept: vi.fn(), accepting: true });
    expect(screen.getByRole('button', { name: 'Accept the departure' })).toBeDisabled();
  });

  it('still says it is held where nothing can say yes', () => {
    page({ quest: HELD });

    expect(required()).toHaveTextContent('held for your yes');
    expect(screen.queryByRole('button', { name: 'Accept the departure' })).toBeNull();
  });

  it('says when the departure was accepted, and offers no second yes', () => {
    page({ quest: ACCEPTED, onAccept: vi.fn() });

    expect(required()).toHaveTextContent('You accepted the departure 1h ago.');
    expect(within(items()[1]).getByText('departed')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Accept the departure' })).toBeNull();
    expect(within(screen.getByRole('main').querySelector('header')!).queryByText('awaits your yes')).toBeNull();
  });

  it('lists what a quest still to close requires, and how its done will answer', () => {
    page({ quest: REQUIRING });

    expect(items()).toHaveLength(2);
    expect(required()).toHaveTextContent('Its done answers each: met, or departed with the reason and your words it relied on.');
    expect(within(required()).queryByText('met')).toBeNull();
  });

  it('says a done that answered none of them did so', () => {
    page({ quest: { ...MET, answers: [] } });

    expect(required()).toHaveTextContent('Its done answered none of these');
  });

  it('draws nothing for a quest that names no requirement', () => {
    page({ quest: OPEN });

    expect(screen.queryByRole('region', { name: 'What you required' })).toBeNull();
  });

  it('offers the yes on a right-click, after the header’s acts', async () => {
    const props = page({ quest: HELD, onAccept: vi.fn() });
    render(<ContextMenus doors={{ copy: vi.fn() }} />);

    rightClick(screen.getByText(/World streaming needs a per-frame cap/));
    expect(await menuActs(`Actions for ${HELD.title}`)).toEqual(['Accept the departure', 'Copy quest ID']);
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Accept the departure' }));
    expect(props.onAccept).toHaveBeenCalledOnce();
  });

  it('reads in 中文, the person’s words and the done’s as they were written', async () => {
    await i18n.changeLanguage('zh');
    page({ quest: HELD_CJK, onAccept: vi.fn() });

    const section = screen.getByRole('region', { name: '你的要求' });
    expect(within(section).getAllByText('每帧的加载量不能超过引擎设定的上限')).toHaveLength(2);
    expect(within(section).getByText('偏离')).toBeInTheDocument();
    expect(within(section).getByText('已满足')).toBeInTheDocument();
    expect(within(section).getAllByText('检验')).toHaveLength(2);
    expect(within(section).getByRole('button', { name: '采纳偏离' })).toBeInTheDocument();
  });
});
