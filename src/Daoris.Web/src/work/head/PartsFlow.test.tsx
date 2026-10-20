import { act, cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { EVIDENCE_ENTRIES, EVIDENCE_SESSIONS } from '../entries.fixtures';
import type { EntryAcceptPlan } from '../review/EntryAccept';
import { PartsFlow } from './PartsFlow';

// FLOWCHART1b (the flowchart design §2.3, §7 trap 21): the parts flow takes the parts strip's place on the session's page, the
// strip its rail. jsdom lays nothing out, so the pane's width is stood in for, as `Flow.test.tsx` does, and the drawing's own
// geometry is the measured-box check's; here is what the page reads and presses.

const APP = EVIDENCE_ENTRIES[0]!;
const rows = Object.fromEntries(EVIDENCE_SESSIONS.map((session) => [session.id, { session, root: 'C:/work/app' }]));
const LIST_KEY = 'daoris.flow.list';

let paneWidth: number | undefined;
const observers = new Set<ResizeObserverCallback>();
const resizeTo = (width: number) => act(() => {
  paneWidth = width;
  for (const callback of observers) callback([{ contentRect: { width } } as ResizeObserverEntry], {} as ResizeObserver);
});

beforeEach(() => {
  paneWidth = undefined;
  observers.clear();
  localStorage.removeItem(LIST_KEY);
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
    const width = this.hasAttribute('data-flow-pane') ? paneWidth ?? 0 : 0;
    return { width, height: 0, top: 0, left: 0, right: width, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
  });
  vi.stubGlobal('ResizeObserver', class {
    private readonly callback: ResizeObserverCallback;
    constructor(callback: ResizeObserverCallback) { this.callback = callback; observers.add(callback); }
    observe() {}
    unobserve() {}
    disconnect() { observers.delete(this.callback); }
  });
});

afterEach(async () => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  await i18n.changeLanguage('en');
});

const flow = () => screen.getByRole('list', { name: 'How this line of work\'s parts follow one another' });
const node = (name: RegExp) => within(flow()).getByRole('listitem', { name });
const placement = (container: HTMLElement) => container.querySelector('[data-flow-pane]')!.getAttribute('data-placement');
const draw = (over: Partial<Parameters<typeof PartsFlow>[0]> = {}) =>
  render(<PartsFlow entry={APP} attended="ap000002" rows={rows} onShow={() => {}} {...over} />);

describe('the parts flow, in the strip\'s place', () => {
  it('draws nothing for a line of work of one part, as the strip did', () => {
    const lone = { ...APP, parts: [APP.parts[0]!], leading: APP.parts[0]!.session };
    const { container } = draw({ entry: lone, attended: lone.leading });
    expect(container).toBeEmptyDOMElement();
  });

  it('is the strip where nothing measured the pane: the same parts, the same words, the same press, named once', async () => {
    const onShow = vi.fn();
    const { container } = draw({ onShow });

    expect(placement(container)).toBe('rail');
    const strip = screen.getByRole('navigation', { name: 'Parts' });
    expect(screen.getAllByRole('navigation')).toHaveLength(1);
    expect(screen.getAllByText('Part 2 of 3')).toHaveLength(1);
    const parts = within(strip).getAllByRole('button');
    expect(parts).toHaveLength(3);
    expect(parts[1]).toHaveAttribute('aria-current', 'true');
    expect(parts[1]).toHaveTextContent(/completed.*retried after part 1.*in t-one/);

    await userEvent.click(parts[2]!);
    expect(onShow).toHaveBeenCalledWith('ap000003');
  });

  it('draws the flow where it fits: the parts as nodes in reading order, the part shown lit, and the strip not drawn twice', () => {
    paneWidth = 1200;
    const { container } = draw();

    expect(placement(container)).toBe('across');
    // Reading order is the drawing's, rank by rank: the two beginnings first, then what hangs from them.
    expect(within(flow()).getAllByRole('listitem').map((item) => item.getAttribute('data-flow-node'))).toEqual([
      'start', 'asked:db', 'ap000001', 'ap000003', 'ap000002',
    ]);
    expect(node(/^Part 2/)).toHaveAttribute('aria-current', 'step');
    expect(node(/^Part 1/)).not.toHaveAttribute('aria-current');
    expect(screen.getByText('Part 2 of 3')).toBeInTheDocument();
    // The strip is the rail: not drawn beside the nodes it would repeat.
    expect(container.querySelector('nav ol')).toBeNull();
    expect(container.querySelector('svg[data-flow-ink]')).toHaveAttribute('aria-hidden', 'true');
  });

  it('describes a part by how it began and what it holds, in words, and never as a cause or a time between parts (trap 19)', () => {
    paneWidth = 1200;
    draw();

    expect(node(/^Part 2/)).toHaveAccessibleDescription(/^completed\. claude-code, began .*\. From Part 1: retried after part 1\./);
    expect(node(/^asked by db/)).toHaveAccessibleDescription('Next: Part 3, asked by db.');
    expect(within(node(/^Part 3/)).getByText('shown by its row in the list')).toBeInTheDocument();
    expect(within(node(/^Part 1/)).getByText('#q0000a')).toBeInTheDocument();
  });

  it('shows another part on a press, on Enter, and on the part\'s own control, and says the part shown has none', async () => {
    const user = userEvent.setup();
    const onShow = vi.fn();
    paneWidth = 1200;
    draw({ onShow });

    await user.click(within(node(/^Part 3/)).getByRole('button', { name: 'Show part 3' }));
    expect(onShow).toHaveBeenLastCalledWith('ap000003');
    node(/^Part 1/).focus();
    await user.keyboard('{Enter}');
    expect(onShow).toHaveBeenLastCalledWith('ap000001');
    expect(within(node(/^Part 2/)).queryByRole('button', { name: /^Show part/ })).toBeNull();
  });

  it('offers Show as list where it fits, and then reads the strip, the choice kept (trap 21)', async () => {
    paneWidth = 1200;
    const { container } = draw();
    await userEvent.click(screen.getByRole('button', { name: 'Show as list' }));

    expect(placement(container)).toBe('rail');
    expect(screen.getByRole('navigation', { name: 'Parts' })).toBeInTheDocument();
    expect(within(screen.getByRole('navigation', { name: 'Parts' })).getAllByRole('button').filter((button) => button.hasAttribute('aria-current'))).toHaveLength(1);
    expect(localStorage.getItem(LIST_KEY)).toBe('list');
  });

  it('keeps the strip at the side bar\'s floor, and the flow where the main area is wide, as the pane resizes', () => {
    paneWidth = 300;
    const { container } = draw();
    expect(placement(container)).toBe('rail');
    expect(container.querySelector('nav ol')).not.toBeNull();
    resizeTo(1200);
    expect(placement(container)).toBe('across');
    expect(container.querySelector('nav ol')).toBeNull();
  });
});

describe('where Accept would land the parts (§2.3, trap 19)', () => {
  const plan: EntryAcceptPlan = {
    entry: APP.entry, repository: 'app', form: 'branch', target: 'daoris/t-one',
    parts: [{ session: 'ap000001' }, { session: 'ap000002' }, { session: 'ap000003', held: 'a gate holds it' }],
  };

  it('draws a prospective node once the plan is read, dashed, saying would, and no landing as done', () => {
    paneWidth = 1200;
    draw({ plan });

    const landing = node(/^Accept would land these on daoris\/t-one/);
    expect(landing.className).toMatch(/border-dashed/);
    expect(landing).not.toHaveAttribute('aria-current');
    expect(landing).toHaveAccessibleDescription(
      'Not reached. From Part 1: its work would land. From Part 2: its work would land. From Part 3: held: a gate holds it.');
    expect(screen.queryByText(/landed on|merged into/i)).toBeNull();
  });

  it('draws none before the plan is read', () => {
    paneWidth = 1200;
    draw();
    expect(within(flow()).queryByText(/Accept would/)).toBeNull();
  });
});

describe('the parts flow in 中文', () => {
  it('says its parts, the start and the strip as the strip says them', async () => {
    await i18n.changeLanguage('zh');
    paneWidth = 1200;
    draw();

    expect(screen.getByRole('list', { name: '这条工作线的各段如何接续' })).toBeInTheDocument();
    expect(screen.getByText('第 2 段，共 3 段')).toBeInTheDocument();
    expect(screen.getByRole('listitem', { name: /^第 2 段/ })).toHaveAttribute('aria-current', 'step');
    expect(screen.getByRole('listitem', { name: /^由 db 委托/ })).toBeInTheDocument();
    expect(screen.getByRole('listitem', { name: /^起点/ })).toBeInTheDocument();
  });
});
