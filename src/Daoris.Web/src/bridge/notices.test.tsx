import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

const { call } = vi.hoisted(() => ({ call: vi.fn(async (_type: string, _payload?: Record<string, unknown>) => ({ written: true })) }));
vi.mock('./call', () => ({ call }));

import { composeNotices, noticeOf, useDismissNotice, useMarkNoticesRead } from './notices';

// Done and failed as the shell pushes it (NOTIFY2f, the notification design §2.5): the `news` partition composed newest first, a
// notice of a shape the page cannot read dropped, nothing until the owner judged; and the person's read and dismissal as presses.

describe('the notices slice on the page', () => {
  it('composes the news newest first, drops what it cannot read, and holds nothing until judged', () => {
    const news = {
      'ended-s1': { id: 'ended-s1', kind: 'ended', at: '2026-10-22T08:00:00.000Z', read: true, state: 'failed' },
      'expired-p1': { id: 'expired-p1', kind: 'expired', at: '2026-10-22T09:00:00.000Z', route: 'SESSION_INPUT' },
      odd: { kind: 'ended' },
    };
    expect(composeNotices(new Map([['news', news]]), false)).toBeUndefined();
    expect(composeNotices(new Map(), true)).toBeUndefined();

    const composed = composeNotices(new Map([['news', news]]), true)!;
    expect(composed.map((notice) => notice.id)).toEqual(['expired-p1', 'ended-s1']);
    // An absent read is unread.
    expect(composed[0]!.read).toBe(false);
    expect(noticeOf('x')).toBeNull();
  });

  it("presses NOTICES_READ with the ids and NOTICE_DISMISS with one, each answering whether it was written", async () => {
    const client = new QueryClient();
    const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
    const read = renderHook(() => useMarkNoticesRead(), { wrapper });
    const dismiss = renderHook(() => useDismissNotice(), { wrapper });

    expect(await read.result.current.mutateAsync(['ended-s1', 'expired-p1'])).toBe(true);
    expect(call).toHaveBeenLastCalledWith('NOTICES_READ', { ids: ['ended-s1', 'expired-p1'] });
    call.mockResolvedValueOnce({ written: false });
    expect(await dismiss.result.current.mutateAsync('ended-s1')).toBe(false);
    expect(call).toHaveBeenLastCalledWith('NOTICE_DISMISS', { id: 'ended-s1' });
    await waitFor(() => expect(dismiss.result.current.isSuccess).toBe(true));
  });
});
