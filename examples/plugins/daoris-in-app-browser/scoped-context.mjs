import { EventEmitter } from 'node:events';

const PAGE_EVENT_METHODS = new Set(['on', 'once', 'addListener', 'off', 'removeListener', 'removeAllListeners']);

/**
 * A session's view of the person's one browser context. Playwright MCP adopts every page its context lists or announces, and
 * makes the first its current tab; shown this view it sees only the pages the session opened, and the popups those opened,
 * so it never takes, navigates or closes a page of the person's or of another session's. Everything else, cookies and storage
 * included, is the real context's own.
 */
export function scopeContext(real, { patience = 15_000 } = {}) {
  const own = new Set();
  const announcements = new EventEmitter();
  announcements.setMaxListeners(0);

  const adopt = page => {
    if (own.has(page)) return;
    own.add(page);
    page.on('popup', adopt);
    page.once('close', () => own.delete(page));
    announcements.emit('page', page);
  };

  // Playwright's own newPage reads the page back the moment the host answers `Target.createTarget`, and a host whose relay announces
  // the tab afterwards leaves it nothing to read. So the target is made here, and the page is the one announced for that target.
  const targetOf = async page => {
    const session = await real.newCDPSession(page);
    try {
      return (await session.send('Target.getTargetInfo')).targetInfo.targetId;
    } finally {
      await session.detach().catch(() => {});
    }
  };

  const openPage = async () => {
    const browserSession = await real.browser().newBrowserCDPSession();
    const checked = new Set();
    try {
      const { targetId } = await browserSession.send('Target.createTarget', { url: 'about:blank' });
      const deadline = Date.now() + patience;
      while (Date.now() < deadline) {
        for (const page of real.pages()) {
          if (own.has(page) || checked.has(page)) continue;
          checked.add(page);
          const found = await targetOf(page).catch(() => undefined);
          if (found === targetId) return page;
          if (found === undefined) checked.delete(page);
        }
        await new Promise(resolve => setTimeout(resolve, 50));
      }
      throw new Error(`the browser made ${targetId} and it was never announced as a page`);
    } finally {
      await browserSession.detach().catch(() => {});
    }
  };

  const methods = {
    pages: () => real.pages().filter(page => own.has(page)),
    newPage: async () => {
      const page = await openPage();
      adopt(page);
      return page;
    },
    // Only what this session opened closes; the context is the person's.
    close: async () => {
      await Promise.all([...own].map(page => page.close().catch(() => {})));
    },
  };

  const view = new Proxy(real, {
    get(target, key) {
      if (Object.hasOwn(methods, key)) return methods[key];
      if (PAGE_EVENT_METHODS.has(key)) {
        return (event, ...rest) => {
          const emitter = event === 'page' ? announcements : target;
          emitter[key](event, ...rest);
          return view;
        };
      }
      const value = Reflect.get(target, key, target);
      return typeof value === 'function' ? value.bind(target) : value;
    },
  });
  return view;
}
