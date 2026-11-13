const TARGET_EVENTS = /"Target\.(attachedToTarget|detachedFromTarget)"/;

/** Where the relay's own questions to the browser count from: far above the ids Playwright uses. */
const FIRST_OWN_ID = 2_000_000_000;

/**
 * What the host says to Playwright, with one change: a target the host announces as `other` is held, and asked about until it is a
 * page, and then announced as a page. Some hosts announce every new tab as `other` and turn it into a `page` a few tens of
 * milliseconds later, telling only a client that enabled target discovery; Playwright decides what a target is when it is
 * announced and drops `other`, so such a tab would never exist for it. Asking (`Target.getTargetInfo`, answered to the relay and
 * not forwarded) needs no discovery, and changes no state in the browser that Playwright could see. A target that stays `other`
 * (the browser's own toolbar and the like) is announced unchanged once `patience` ms are out, since a target the host started
 * paused waits for Playwright to let it run.
 *
 * @param send Sends one message on to Playwright.
 * @param options.toHost Sends one message of the relay's own to the browser.
 * @returns A function taking each message the host sent, as its text; `.end()` drops what is held.
 */
export function holdOthers(send, { toHost, patience = 1000, interval = 25, setTimer = setTimeout, clearTimer = clearTimeout }) {
  const held = new Map();
  const asking = new Map();
  let nextId = FIRST_OWN_ID;

  const drop = targetId => {
    const entry = held.get(targetId);
    if (entry?.timer) clearTimer(entry.timer);
    held.delete(targetId);
    return entry;
  };

  const ask = targetId => {
    const entry = held.get(targetId);
    if (!entry) return;
    const id = nextId++;
    asking.set(id, targetId);
    toHost(JSON.stringify({ id, method: 'Target.getTargetInfo', params: { targetId } }));
  };

  const hold = (targetId, message, text) => {
    const entry = { message, text, left: Math.max(1, Math.ceil(patience / interval)), timer: undefined };
    held.set(targetId, entry);
    ask(targetId);
  };

  const release = (targetId, announce) => {
    if (drop(targetId)) announce();
  };

  const answered = (id, reply) => {
    const targetId = asking.get(id);
    asking.delete(id);
    const entry = held.get(targetId);
    if (!entry) return;
    const info = reply.result?.targetInfo;
    if (info?.type === 'page') {
      release(targetId, () => {
        entry.message.params.targetInfo = { ...entry.message.params.targetInfo, ...info };
        send(JSON.stringify(entry.message));
      });
      return;
    }
    if (!info || --entry.left <= 0) {
      release(targetId, () => send(entry.text));
      return;
    }
    entry.timer = setTimer(() => ask(targetId), interval);
  };

  const onHost = text => {
    if (!TARGET_EVENTS.test(text)) {
      if (asking.size === 0) return send(text);
      const reply = JSON.parse(text);
      if (asking.has(reply.id)) return answered(reply.id, reply);
      return send(text);
    }
    const message = JSON.parse(text);
    const info = message.params?.targetInfo;
    const targetId = info?.targetId ?? message.params?.targetId;

    if (message.method === 'Target.attachedToTarget' && info?.type === 'other') {
      hold(targetId, message, text);
      return undefined;
    }
    if (message.method === 'Target.detachedFromTarget' && held.has(targetId)) {
      drop(targetId);
      return undefined;
    }
    return send(text);
  };
  onHost.end = () => [...held.keys()].forEach(drop);
  return onHost;
}
