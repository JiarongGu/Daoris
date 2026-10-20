import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { EVIDENCE_ENTRIES, EVIDENCE_SESSIONS } from '../entries.fixtures';
import type { EntryAcceptPlan } from '../review/EntryAccept';
import { PartsFold } from './PartsFold';

// HEADFIT1 (the UI/UX design §3.7 and R4, the UX7 design §6.2): the parts' flowchart cost 339 to 484 px above the conversation, more
// than the whole budget, so the page folds it to one line (how many parts, which the page is on, how the newest began) and opens it
// in place. jsdom lays nothing out, so the pane's width is stood in for as `PartsFlow.test.tsx` does; the line's own height is the
// measured check's (`measured/head.spec.ts`).

const APP = EVIDENCE_ENTRIES[0]!;
const rows = Object.fromEntries(EVIDENCE_SESSIONS.map((session) => [session.id, { session, root: 'C:/work/app' }]));
const LIST_KEY = 'daoris.flow.list';

let paneWidth: number | undefined;
const observers = new Set<ResizeObserverCallback>();

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

const draw = (over: Partial<Parameters<typeof PartsFold>[0]> = {}) =>
  render(<PartsFold entry={APP} attended="ap000002" rows={rows} onShow={() => {}} {...over} />);
const flow = () => screen.getByRole('list', { name: 'How this line of work\'s parts follow one another' });

describe('the parts, folded to one line', () => {
  it('draws nothing for a line of work of one part, as the strip and the flow do', () => {
    const lone = { ...APP, parts: [APP.parts[0]!], leading: APP.parts[0]!.session };
    const { container } = draw({ entry: lone, attended: lone.leading });
    expect(container).toBeEmptyDOMElement();
  });

  it('is one line that says how many parts, which the page is on and how the newest began, and draws no part', () => {
    paneWidth = 1200;
    draw();

    const line = screen.getByRole('navigation', { name: 'Parts' });
    expect(line).toHaveTextContent('Part 2 of 3');
    expect(line).toHaveTextContent('newest: asked by db');
    // Its one control is the press that opens it: no part is a door while it is folded, and no drawing is made.
    expect(within(line).getAllByRole('button')).toHaveLength(1);
    expect(screen.getByRole('button', { name: 'Show the parts' })).toBeInTheDocument();
    expect(screen.queryByRole('list')).toBeNull();
    expect(document.querySelector('[data-flow-pane]')).toBeNull();
  });

  it('says the newest part by how it began, the last of the oldest-first list, in every way a part begins', () => {
    const entry = { ...APP, parts: [APP.parts[0]!, { ...APP.parts[1]! }] };
    const { rerender } = draw({ entry, attended: 'ap000001' });
    expect(screen.getByRole('navigation', { name: 'Parts' })).toHaveTextContent('Part 1 of 2 · newest: retried after part 1');

    rerender(<PartsFold entry={{ ...entry, parts: [entry.parts[0]!, { ...entry.parts[1]!, began: 'carried-on', after: null }] }} attended="ap000001" rows={rows} onShow={() => {}} />);
    expect(screen.getByRole('navigation', { name: 'Parts' })).toHaveTextContent('newest: carried on');
    rerender(<PartsFold entry={{ ...entry, parts: [entry.parts[0]!, { ...entry.parts[1]!, began: 'opened', after: null }] }} attended="ap000001" rows={rows} onShow={() => {}} />);
    expect(screen.getByRole('navigation', { name: 'Parts' })).toHaveTextContent('newest: started');
  });

  it('opens in place on the press, where the flow fits, and folds again on Hide the parts', async () => {
    const user = userEvent.setup();
    paneWidth = 1200;
    const { container } = draw();

    await user.click(screen.getByRole('button', { name: 'Show the parts' }));
    expect(container.querySelector('[data-flow-pane]')).toHaveAttribute('data-placement', 'across');
    expect(within(flow()).getAllByRole('listitem').map((item) => item.getAttribute('data-flow-node'))).toEqual([
      'start', 'asked:db', 'ap000001', 'ap000003', 'ap000002',
    ]);
    // The line gives way to the flow's own heading, so the parts are named once and the page is on one part of three.
    expect(screen.getAllByRole('navigation')).toHaveLength(1);
    expect(screen.getAllByText('Part 2 of 3')).toHaveLength(1);
    expect(screen.queryByText(/newest:/)).toBeNull();
    expect(screen.getByRole('button', { name: 'Hide the parts' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Hide the parts' }));
    expect(screen.queryByRole('list')).toBeNull();
    expect(screen.getByRole('button', { name: 'Show the parts' })).toBeInTheDocument();
  });

  it('opens to the strip where only the rail fits, and a press on a part shows it as it did', async () => {
    const user = userEvent.setup();
    const onShow = vi.fn();
    draw({ onShow });

    await user.click(screen.getByRole('button', { name: 'Show the parts' }));
    const parts = within(screen.getByRole('navigation', { name: 'Parts' })).getAllByRole('button');
    expect(parts).toHaveLength(3);
    await user.click(parts[2]!);
    expect(onShow).toHaveBeenCalledWith('ap000003');
  });

  it('opens drawn where a story or a test asks, and reads Accept\'s plan in the drawing only once opened', () => {
    const plan: EntryAcceptPlan = {
      entry: APP.entry, repository: 'app', form: 'branch', target: 'daoris/t-one',
      parts: [{ session: 'ap000001' }, { session: 'ap000002' }, { session: 'ap000003' }],
    };
    paneWidth = 1200;
    const folded = draw({ plan });
    expect(screen.queryByText(/Accept would land/)).toBeNull();
    folded.unmount();

    draw({ plan, defaultOpen: true });
    expect(screen.getByRole('listitem', { name: /^Accept would land these on daoris\/t-one/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Hide the parts' })).toBeInTheDocument();
  });

  it('is said in 中文, the line and its two presses', async () => {
    const user = userEvent.setup();
    await i18n.changeLanguage('zh');
    paneWidth = 1200;
    draw();

    const line = screen.getByRole('navigation', { name: '各段' });
    expect(line).toHaveTextContent('第 2 段，共 3 段');
    expect(line).toHaveTextContent('最新一段：由 db 委托');
    await user.click(screen.getByRole('button', { name: '展开各段' }));
    expect(screen.getByRole('button', { name: '收起各段' })).toBeInTheDocument();
  });

  it('keeps its state in itself: a rerender for another part of the same line leaves it as the person left it', async () => {
    const user = userEvent.setup();
    paneWidth = 1200;
    const { rerender } = draw();
    await user.click(screen.getByRole('button', { name: 'Show the parts' }));
    rerender(<PartsFold entry={APP} attended="ap000003" rows={rows} onShow={() => {}} />);
    expect(screen.getByRole('button', { name: 'Hide the parts' })).toBeInTheDocument();
    expect(screen.getByText('Part 3 of 3')).toBeInTheDocument();
  });
});
