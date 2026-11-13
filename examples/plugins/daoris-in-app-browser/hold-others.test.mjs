import { test } from 'node:test';
import assert from 'node:assert/strict';
import { holdOthers } from './hold-others.mjs';

const attached = (targetId, type, sessionId = `s-${targetId}`) => JSON.stringify({
  method: 'Target.attachedToTarget',
  params: { sessionId, waitingForDebugger: true, targetInfo: { targetId, type, url: 'about:blank', attached: true } },
});
const changed = (targetId, type) => JSON.stringify({ method: 'Target.targetInfoChanged', params: { targetInfo: { targetId, type, url: 'about:blank' } } });
const detached = (targetId, sessionId = `s-${targetId}`) => JSON.stringify({ method: 'Target.detachedFromTarget', params: { sessionId, targetId } });

/** The filter with timers the test fires by hand. */
function filter(patience = 500) {
  const sent = [];
  const timers = [];
  const onHost = holdOthers(text => sent.push(JSON.parse(text)), {
    patience,
    setTimer: (run, ms) => { const timer = { run, ms, cleared: false }; timers.push(timer); return timer; },
    clearTimer: timer => { timer.cleared = true; },
  });
  return { onHost, sent, timers };
}

test('a target announced as a page, and every other message, passes as it is', () => {
  const { onHost, sent } = filter();
  onHost(attached('t1', 'page'));
  onHost(JSON.stringify({ id: 3, result: {} }));
  assert.deepEqual(sent.map(message => message.params?.targetInfo?.type ?? 'reply'), ['page', 'reply']);
});

test('a target announced as other waits, and is announced as a page once the host says it is one', () => {
  const { onHost, sent } = filter();
  onHost(attached('t2', 'other'));
  assert.equal(sent.length, 0, 'nothing is announced while it may still become a page');

  onHost(changed('t2', 'page'));

  assert.deepEqual(sent.map(message => [message.method, message.params.targetInfo.type]), [
    ['Target.attachedToTarget', 'page'],
    ['Target.targetInfoChanged', 'page'],
  ]);
  assert.equal(sent[0].params.sessionId, 's-t2');
  assert.equal(sent[0].params.waitingForDebugger, true);
});

test('a target that stays other is announced as it was once the patience is out', () => {
  const { onHost, sent, timers } = filter(500);
  onHost(attached('t3', 'other'));
  assert.equal(timers[0].ms, 500);
  timers[0].run();
  assert.deepEqual(sent.map(message => message.params.targetInfo.type), ['other']);
  onHost(changed('t3', 'page'));
  assert.equal(sent.length, 2, 'a later change passes on as the host said it');
});

test('a held target that goes before it is a page is never announced', () => {
  const { onHost, sent, timers } = filter();
  onHost(attached('t4', 'other'));
  onHost(detached('t4'));
  assert.equal(sent.length, 0);
  assert.equal(timers[0].cleared, true);
});

test('a popup that is announced as other and becomes a page is released the same way', () => {
  const { onHost, sent } = filter();
  onHost(attached('page-1', 'page'));
  onHost(attached('popup-1', 'other'));
  onHost(changed('popup-1', 'page'));
  assert.deepEqual(sent.filter(m => m.method === 'Target.attachedToTarget').map(m => m.params.targetInfo.targetId), ['page-1', 'popup-1']);
});

test('ending the filter clears what it holds', () => {
  const { onHost, timers } = filter();
  onHost(attached('t5', 'other'));
  onHost.end();
  assert.equal(timers[0].cleared, true);
});
