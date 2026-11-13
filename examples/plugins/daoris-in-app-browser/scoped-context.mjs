import { EventEmitter } from 'node:events';

const PAGE_EVENT_METHODS = new Set(['on', 'once', 'addListener', 'off', 'removeListener', 'removeAllListeners']);

/**
 * A session's view of the person's one browser context. Playwright MCP adopts every page its context lists or announces, and
 * makes the first its current tab; shown this view it sees only the pages the session opened, and the popups those opened,
 * so it never takes, navigates or closes a page of the person's or of another session's. Everything else, cookies and storage
 * included, is the real context's own.
 */
export function scopeContext(real) {
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

  const methods = {
    pages: () => real.pages().filter(page => own.has(page)),
    newPage: async (...args) => {
      const page = await real.newPage(...args);
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
