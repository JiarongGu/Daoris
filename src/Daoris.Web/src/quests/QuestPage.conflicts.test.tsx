import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import {
  BROUGHT_FORWARD, BROUGHT_FORWARD_UNCONFIRMED, CONFLICTED, DROPPED_FIRST, HELD_ELSEWHERE, HELD_HERE, LOST_AGAIN, LOST_CLOSED,
  LOST_TO_REOPEN, LOST_WITHDRAWN, MINE_LOST, REOPENED, REOPENED_HERE, SETTLED_DROPPED, THEIRS_LOST,
} from './fixtures';
import { QuestPage } from './QuestPage';

// SYNCMERGE1c2 (D178 points 2, 7 and 8): a conflict's choices on the quest's page. This machine's own lost take offers *Drop mine* (the
// dismissal) and *Bring mine forward*, and says why the second is not offered where it cannot stand; another machine's offers its
// dismissal only. A second loss is said as the bring-forward lost again, a settled conflict says how it ended, and an unconfirmed
// bring-forward is never said as settled. A held step's go-on and abandon are this machine's same two choices.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const page = (over: Partial<Parameters<typeof QuestPage>[0]> = {}) => {
  const props = { quest: MINE_LOST, onRespond: vi.fn(), onDismiss: vi.fn(), onForward: vi.fn(), onOpenQuest: vi.fn(), ...over };
  render(<QuestPage {...props} />);
  return props;
};

const conflicts = () => screen.getByRole('region', { name: 'Conflicts' });

describe("a conflict's choices", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("offers this machine's lost take both choices, each pressed by the conflict's name", async () => {
    const props = page();

    const section = conflicts();
    expect(section).toHaveTextContent("This machine's take lost the race to the remote");
    expect(section).toHaveTextContent('desk, on the train');
    // What each choice does, the dismissal's apart from the bring-forward's.
    expect(section).toHaveTextContent(/Drop mine puts this conflict away on every machine and moves nothing else/);
    await userEvent.click(within(section).getByRole('button', { name: 'Bring mine forward' }));
    expect(props.onForward).toHaveBeenCalledWith('desk', 7);
    await userEvent.click(within(section).getByRole('button', { name: 'Drop mine' }));
    expect(props.onDismiss).toHaveBeenCalledWith('desk', 7);
    expect(within(section).queryByRole('button', { name: 'Dismiss' })).toBeNull();
  });

  it("offers another machine's conflict only its dismissal, as before", async () => {
    const props = page({ quest: THEIRS_LOST });

    const section = conflicts();
    expect(section).toHaveTextContent('Machine b7f2c9d1 tried to mark it taken');
    expect(within(section).queryByRole('button', { name: 'Bring mine forward' })).toBeNull();
    expect(within(section).queryByRole('button', { name: 'Drop mine' })).toBeNull();
    await userEvent.click(within(section).getByRole('button', { name: 'Dismiss' }));
    expect(props.onDismiss).toHaveBeenCalledWith('b7f2c9d1', 12);
  });

  it('reads a host that names no machine as before: a dismissal, and nothing brought forward', () => {
    page({ quest: CONFLICTED });

    expect(within(conflicts()).getByRole('button', { name: 'Dismiss' })).toBeInTheDocument();
    expect(within(conflicts()).queryByRole('button', { name: 'Bring mine forward' })).toBeNull();
  });

  it('says a take lost to the reopen as that, this machine’s and another’s', () => {
    page({ quest: LOST_TO_REOPEN });

    const section = conflicts();
    expect(section).toHaveTextContent("This machine's take lost when the work it grew from was reopened");
    expect(section).toHaveTextContent("Machine b7f2c9d1's take lost when the work it grew from was reopened");
    expect(within(section).getAllByRole('button', { name: 'Bring mine forward' })).toHaveLength(1);
  });

  it('does not offer the bring-forward while another machine holds the step, and says which holds it', () => {
    page({ quest: HELD_ELSEWHERE });

    const section = conflicts();
    expect(within(section).queryByRole('button', { name: 'Bring mine forward' })).toBeNull();
    expect(section).toHaveTextContent('machine b7f2c9d1 holds the step now');
    expect(within(section).getByRole('button', { name: 'Drop mine' })).toBeInTheDocument();
  });

  it('says nothing is left to bring forward where this machine holds the step again', () => {
    page({ quest: HELD_HERE });

    expect(within(conflicts()).queryByRole('button', { name: 'Bring mine forward' })).toBeNull();
    expect(conflicts()).toHaveTextContent('this machine holds the step already, by a later take');
  });

  it('says a closed step takes no take, and a withdrawn one waits for its source', () => {
    page({ quest: LOST_CLOSED });
    expect(within(conflicts()).queryByRole('button', { name: 'Bring mine forward' })).toBeNull();
    expect(conflicts()).toHaveTextContent('the step is closed');
    expect(within(conflicts()).getByRole('button', { name: 'Drop mine' })).toBeInTheDocument();
  });

  it('waits for a reopened source before offering the bring-forward', () => {
    page({ quest: LOST_WITHDRAWN });

    expect(within(conflicts()).queryByRole('button', { name: 'Bring mine forward' })).toBeNull();
    expect(conflicts()).toHaveTextContent('the work it grows from is reopened');
  });

  it('says a second loss as the bring-forward lost again, not as a new conflict, and drops both together', async () => {
    const props = page({ quest: LOST_AGAIN });

    const section = conflicts();
    expect(within(section).getAllByRole('listitem')).toHaveLength(1);
    expect(section).toHaveTextContent('Brought forward, and lost again: another machine\'s take reached the remote first');
    expect(within(section).queryByRole('button', { name: 'Bring mine forward' })).toBeNull();
    await userEvent.click(within(section).getByRole('button', { name: 'Drop mine' }));
    expect(props.onDismiss).toHaveBeenCalledWith('desk', 7, [{ machine: 'desk', sequence: 9 }]);
  });

  it('holds both presses while either is on its way', () => {
    page({ forwarding: true });

    expect(within(conflicts()).getByRole('button', { name: 'Bring mine forward' })).toBeDisabled();
    expect(within(conflicts()).getByRole('button', { name: 'Drop mine' })).toBeDisabled();
  });

  it('offers no bring-forward where there is no door to press it through', () => {
    page({ onForward: undefined });

    expect(within(conflicts()).queryByRole('button', { name: 'Bring mine forward' })).toBeNull();
    expect(within(conflicts()).getByRole('button', { name: 'Drop mine' })).toBeInTheDocument();
  });

  it('says the choices in Chinese', async () => {
    await i18n.changeLanguage('zh');
    page();

    const section = screen.getByRole('region', { name: '冲突' });
    expect(within(section).getByRole('button', { name: '放下本机的' })).toBeInTheDocument();
    expect(within(section).getByRole('button', { name: '继续本机的' })).toBeInTheDocument();
    expect(section).toHaveTextContent('本机的接下');
  });
});

describe('how a conflict ended', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says a dropped conflict ended dropped, and nothing waits on it', () => {
    page({ quest: SETTLED_DROPPED });

    expect(screen.queryByRole('region', { name: 'Conflicts' })).toBeNull();
    const settled = screen.getByRole('region', { name: 'Settled conflicts' });
    expect(settled).toHaveTextContent("This machine's take lost the race to the remote");
    expect(settled).toHaveTextContent('dropped');
    expect(within(settled).queryByRole('button')).toBeNull();
  });

  it('says a confirmed bring-forward as brought forward and confirmed', () => {
    page({ quest: BROUGHT_FORWARD });

    expect(screen.getByRole('region', { name: 'Settled conflicts' })).toHaveTextContent('brought forward, and the remote confirmed it');
  });

  it('never says an unconfirmed bring-forward is settled', () => {
    page({ quest: BROUGHT_FORWARD_UNCONFIRMED });

    expect(screen.queryByRole('region', { name: 'Settled conflicts' })).toBeNull();
    const live = conflicts();
    expect(live).toHaveTextContent(/Brought forward, unconfirmed/);
    expect(live).not.toHaveTextContent(/settled/i);
    expect(within(live).queryByRole('button')).toBeNull();
  });

  it('says a bring-forward that lost to a drop, and offers the fresh take its own choices', () => {
    page({ quest: DROPPED_FIRST });

    expect(screen.getByRole('region', { name: 'Settled conflicts' }))
      .toHaveTextContent('dropped on another machine before this machine\'s bring-forward reached the remote');
    expect(within(conflicts()).getByRole('button', { name: 'Bring mine forward' })).toBeInTheDocument();
  });

  it('says how they ended in Chinese', async () => {
    await i18n.changeLanguage('zh');
    page({ quest: BROUGHT_FORWARD });

    expect(screen.getByRole('region', { name: '已了结的冲突' })).toHaveTextContent('已继续，远端已确认');
  });
});

describe("a held step's two choices", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("names this machine's go-on and abandon as its two choices, and says a decline closes the step for every machine", async () => {
    const onGoOn = vi.fn();
    page({ quest: REOPENED_HERE, goOn: { ready: true, onGoOn } });

    const held = screen.getByRole('region', { name: 'Its source was reopened' });
    await userEvent.click(within(held).getByRole('button', { name: 'Bring mine forward' }));
    expect(onGoOn).toHaveBeenCalledOnce();
    expect(held).toHaveTextContent(/Dropping it is its decline: the step closes for every machine/);
    expect(held).toHaveTextContent(/dropping a lost take puts away only its conflict/);
    await userEvent.click(within(held).getByRole('button', { name: 'Drop mine…' }));
    expect(screen.getByRole('group', { name: 'decline this quest' })).toBeInTheDocument();
  });

  it("keeps the plain names where the step is not this machine's to bring forward, and says the same choice", () => {
    page({ quest: REOPENED, goOn: { ready: true, onGoOn: vi.fn() } });

    const held = screen.getByRole('region', { name: 'Its source was reopened' });
    expect(within(held).getByRole('button', { name: 'Go on with it' })).toBeInTheDocument();
    expect(within(held).getByRole('button', { name: 'Abandon…' })).toBeInTheDocument();
    expect(held).toHaveTextContent(/brings it forward/);
    expect(held).toHaveTextContent(/Dropping it is its decline/);
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    page({ quest: REOPENED_HERE, goOn: { ready: true, onGoOn: vi.fn() } });

    const held = screen.getByRole('region', { name: '其来源已重新打开' });
    expect(within(held).getByRole('button', { name: '继续本机的' })).toBeInTheDocument();
    expect(within(held).getByRole('button', { name: '放下本机的…' })).toBeInTheDocument();
  });
});

describe('a done on a step this machine lost', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('names the choices in its ask', async () => {
    page({ quest: HELD_ELSEWHERE });

    await userEvent.click(within(screen.getByRole('main').querySelector('header')!).getByRole('button', { name: 'Mark done…' }));
    expect(screen.getByRole('group', { name: 'mark this quest done' }))
      .toHaveTextContent(/This machine's take of it lost.*drop mine or bring mine forward/);
  });
});
