const TARGET_EVENTS = /"Target\.(attachedToTarget|targetInfoChanged|detachedFromTarget)"/;

/**
 * What the host says to Playwright, with one change: a target the host announces as `other` is held until the host says it is a
 * page, and then announced as a page. Some hosts announce every new tab as `other` and turn it into a `page` a few tens of
 * milliseconds later; Playwright decides what a target is when it is announced and drops `other`, so such a tab would never
 * exist for it. A target that stays `other` (the browser's own toolbar and the like) is announced unchanged once `patience` ms are
 * out, and held no longer than that, since a target the host started paused waits for Playwright to let it run.
 *
 * @param send Sends one message on to Playwright.
 * @returns A function taking each message the host sent, as its text; `.end()` drops what is held.
 */
export function holdOthers(send, { patience = 500, setTimer = setTimeout, clearTimer = clearTimeout } = {}) {
  const held = new Map();

  const drop = targetId => {
    const entry = held.get(targetId);
    if (entry) clearTimer(entry.timer);
    held.delete(targetId);
    return entry;
  };

  const onHost = text => {
    if (!TARGET_EVENTS.test(text)) return send(text);
    const message = JSON.parse(text);
    const info = message.params?.targetInfo;
    const targetId = info?.targetId ?? message.params?.targetId;

    if (message.method === 'Target.attachedToTarget' && info?.type === 'other') {
      const timer = setTimer(() => {
        if (drop(targetId)) send(text);
      }, patience);
      held.set(targetId, { message, timer });
      return undefined;
    }
    if (message.method === 'Target.targetInfoChanged' && held.has(targetId)) {
      if (info.type !== 'page') return undefined;
      const { message: attachment } = drop(targetId);
      attachment.params.targetInfo = { ...attachment.params.targetInfo, ...info };
      send(JSON.stringify(attachment));
      return send(text);
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
