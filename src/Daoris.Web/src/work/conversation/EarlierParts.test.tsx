import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createRef } from 'react';
import i18n from '../../locales/i18n';

// SWITCHCTX1c-i (the switch-context design §2.7): the parts a session's page draws above the attended one, at most three nearest and
// a press for up to three more, each read-only with its own record's liveness. The bridge is mocked; the records are the test's.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
  useShenoraEvent: () => {},
}));

import { EarlierParts, type EarlierPart } from './EarlierParts';

const at = '2026-10-25T00:00:00Z';
const part = (n: number, over: Partial<EarlierPart> = {}): EarlierPart => ({ id: `p${n}`, adapter: 'claude-code', chat: true, live: false, ...over });

/** Each part says its own number, so a test reads which are drawn. */
function answer(open: string[] = []) {
  invoke.mockImplementation(async (_module: string, type: string, body?: { payload?: { id?: string } }) => {
    if (type === 'SESSION_HISTORY') {
      const id = body?.payload?.id ?? '';
      return {
        session: id, earlier: false, latest: 2,
        events: [
          { seq: 1, at, kind: 'user', origin: 'person', text: `Asked in ${id}.` },
          { seq: 2, at, kind: 'message', id: 'm1', text: `Said in ${id}.` },
          ...(open.includes(id) ? [{ seq: 3, at, kind: 'tool', id: 'x1', title: 'npm test', status: 'in_progress' }] : []),
        ],
      };
    }
    if (type === 'HARNESSES') return { harnesses: [] };
    return {};
  });
}

function draw(line: EarlierPart[]) {
  const scroller = createRef<HTMLDivElement>();
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <Tooltip.Provider>
        <div ref={scroller}>
          <EarlierParts line={line} scroller={scroller} />
        </div>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

const drawn = () => [...document.querySelectorAll('[data-part]')].map((each) => each.getAttribute('data-part'));

afterEach(async () => {
  invoke.mockReset();
  await i18n.changeLanguage('en');
});

describe('the earlier parts of a line', () => {
  it('draws each part’s conversation, oldest first, and no press for a short line', async () => {
    answer();
    draw([part(1), part(2)]);
    await screen.findByText('Said in p2.');
    expect(drawn()).toEqual(['p1', 'p2']);
    expect(screen.getByText('Said in p1.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /earlier part|part before/ })).toBeNull();
  });

  it('draws a line of five earlier parts as three, the press saying how many more, and draws the other two', async () => {
    answer();
    draw([1, 2, 3, 4, 5].map((n) => part(n)));
    await screen.findByText('Said in p5.');
    expect(drawn()).toEqual(['p3', 'p4', 'p5']);
    expect(screen.queryByText('Said in p2.')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Show 2 earlier parts' }));
    await screen.findByText('Said in p1.');
    expect(drawn()).toEqual(['p1', 'p2', 'p3', 'p4', 'p5']);
    expect(screen.queryByRole('button', { name: /earlier parts?|part before/ })).toBeNull();
  });

  it('says a single part more as the part before, and a press draws up to three at a time', async () => {
    answer();
    draw([1, 2, 3, 4].map((n) => part(n)));
    await screen.findByText('Said in p4.');
    expect(screen.getByRole('button', { name: 'Show the part before' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Show the part before' }));
    await screen.findByText('Said in p1.');
    expect(drawn()).toEqual(['p1', 'p2', 'p3', 'p4']);
  });

  it('draws the press above the oldest part drawn, so the parts stay in order', async () => {
    answer();
    draw([1, 2, 3, 4, 5].map((n) => part(n)));
    await screen.findByText('Said in p5.');
    const press = screen.getByRole('button', { name: 'Show 2 earlier parts' });
    const first = document.querySelector('[data-part="p3"]')!;
    expect(press.compareDocumentPosition(first) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('words the press in Chinese', async () => {
    await i18n.changeLanguage('zh');
    answer();
    draw([1, 2, 3, 4, 5].map((n) => part(n)));
    await screen.findByText('Said in p5.');
    await userEvent.click(screen.getByRole('button', { name: '显示更早的 2 段' }));
    await screen.findByText('Said in p1.');

    answer();
    draw([1, 2, 3, 4].map((n) => part(n)));
    expect(await screen.findByRole('button', { name: '显示前一段' })).toBeInTheDocument();
  });

  it('draws a part still live as live, and a part ended as ended, each by its own record', async () => {
    answer(['p1', 'p2']);
    draw([part(1, { live: true }), part(2)]);
    await screen.findByText('Said in p2.');
    const live = within(document.querySelector<HTMLElement>('[data-part="p1"]')!);
    const ended = within(document.querySelector<HTMLElement>('[data-part="p2"]')!);
    expect(live.getByText('working…')).toBeInTheDocument();
    expect(live.queryByText('The session ended here, before this turn did.')).toBeNull();
    expect(ended.getByText('The session ended here, before this turn did.')).toBeInTheDocument();
  });

  it('bounds each part by the words of how it began', async () => {
    answer();
    draw([part(1), part(2, { began: 'asked by db' })]);
    await screen.findByText('Said in p2.');
    expect(within(document.querySelector<HTMLElement>('[data-part="p2"]')!).getByText('asked by db')).toBeInTheDocument();
    expect(within(document.querySelector<HTMLElement>('[data-part="p1"]')!).queryByText('asked by db')).toBeNull();
  });

});
