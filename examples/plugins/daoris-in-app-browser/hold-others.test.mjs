import { test } from 'node:test';
import assert from 'node:assert/strict';
import { holdOthers } from './hold-others.mjs';

const attached = (targetId, type, sessionId = `s-${targetId}`) => JSON.stringify({
  method: 'Target.attachedToTarget',
  params: { sessionId, waitingForDebugger: true, targetInfo: { targetId, type, url: 'about:blank', attached: true } },
});
const detached = (targetId, sessionId = `s-${targetId}`) => JSON.stringify({ method: 'Target.detachedFromTarget', params: { sessionId, targetId } });

/** The filter with timers the test fires by hand, and the browser's answers to what it asks, given by the test. */
function filter({ patience = 100, interval = 25 } = {}) {
  const sent = [];
  const asked = [];
  const timers = [];
  const onHost = holdOthers(text => sent.push(JSON.parse(text)), {
    toHost: text => asked.push(JSON.parse(text)),
    patience,
    interval,
    setTimer: (run, ms) => { const timer = { run, ms, cleared: false }; timers.push(timer); return timer; },
    clearTimer: timer => { timer.cleared = true; },
  });
  const answer = (question, type) => onHost(JSON.stringify({ id: question.id, result: { targetInfo: { targetId: question.params.targetId, type, url: 'https://x.test/', attached: true } } }));
  const tick = () => timers.filter(timer => !timer.cleared).at(-1).run();
  return { onHost, sent, asked, timers, answer, tick };
}

test('a target announced as a page, and every other message, passes as it is', () => {
  const { onHost, sent, asked } = filter();
  onHost(attached('t1', 'page'));
  onHost(JSON.stringify({ id: 3, result: {} }));
  assert.deepEqual(sent.map(message => message.params?.targetInfo?.type ?? 'reply'), ['page', 'reply']);
  assert.equal(asked.length, 0);
});

test('a target announced as other waits, is asked about, and is announced as a page once the browser says it is one', () => {
  const { onHost, sent, asked, answer, tick } = filter();
  onHost(attached('t2', 'other'));
  assert.equal(sent.length, 0, 'nothing is announced while it may still become a page');
  assert.equal(asked[0].method, 'Target.getTargetInfo');
  assert.equal(asked[0].params.targetId, 't2');

  answer(asked[0], 'other');
  assert.equal(sent.length, 0, 'the answer is not Playwright\'s to read');
  tick();
  assert.equal(asked.length, 2, 'it asks again');
  answer(asked[1], 'page');

  assert.deepEqual(sent.map(message => [message.method, message.params.targetInfo.type, message.params.targetInfo.url]), [
    ['Target.attachedToTarget', 'page', 'https://x.test/'],
  ]);
  assert.equal(sent[0].params.sessionId, 's-t2');
  assert.equal(sent[0].params.waitingForDebugger, true);
});

test('a target that stays other is announced unchanged once the patience is out', () => {
  const { onHost, sent, asked, answer, tick } = filter({ patience: 60, interval: 25 });
  onHost(attached('t3', 'other'));
  for (let i = 0; i < 6 && sent.length === 0; i++) {
    answer(asked.at(-1), 'other');
    tick();
  }
  assert.deepEqual(sent.map(message => message.params.targetInfo.type), ['other']);
  assert.ok(asked.length <= 5, 'it stops asking');
});

test('a held target that goes before it is a page is never announced, and a refused question ends the wait', () => {
  const first = filter();
  first.onHost(attached('t4', 'other'));
  first.onHost(detached('t4'));
  assert.equal(first.sent.length, 0);

  const second = filter();
  second.onHost(attached('t5', 'other'));
  second.onHost(JSON.stringify({ id: second.asked[0].id, error: { message: 'No target with given id' } }));
  assert.equal(second.sent.length, 1, 'announced as it was; Playwright drops it as it always did');
});

test('a popup announced as other and turning into a page is released the same way', () => {
  const { onHost, sent, asked, answer } = filter();
  onHost(attached('page-1', 'page'));
  onHost(attached('popup-1', 'other'));
  answer(asked[0], 'page');
  assert.deepEqual(sent.filter(m => m.method === 'Target.attachedToTarget').map(m => m.params.targetInfo.targetId), ['page-1', 'popup-1']);
});

test('ending the filter clears what it holds', () => {
  const { onHost, timers } = filter();
  onHost(attached('t6', 'other'));
  onHost.end();
  assert.ok(timers.every(timer => timer.cleared));
});
