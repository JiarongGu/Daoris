/**
 * The census of timing assertions (a test asserts a relation, a moved clock or nothing, never a wall-clock duration).
 *
 *   node --test tools/timing-census.test.mjs
 *
 * `timingAsserts` reads one test's text; `checkTimingTests` reads a source tree and its kept list. The real tree passes.
 */
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkTimingTests, timingAsserts } from './timing-census.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(root, '_fixtures', 'timing-census-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const lines = (found) => found.map((each) => each.line);

test('the tree passes', () => {
  assert.deepEqual(checkTimingTests(root).problems, []);
});

test('an assertion on the difference of two clock reads is named, in script and in C#', () => {
  const script = ['const began = Date.now();', 'await run();', 'assert.ok(Date.now() - began < 1000, "slow");'].join('\n');
  assert.deepEqual(lines(timingAsserts(script, 'script')), [3]);
  const web = ['const start = performance.now();', 'layout();', 'expect(performance.now() - start).toBeLessThan(750);'].join('\n');
  assert.deepEqual(lines(timingAsserts(web, 'script')), [3]);
  const dotnet = ['var watch = Stopwatch.StartNew();', 'await Run();', 'Assert.True(watch.Elapsed < Going, "slow");'].join('\n');
  assert.deepEqual(lines(timingAsserts(dotnet, 'csharp')), [3]);
});

test('an assertion on a value taken from a clock is named where it stands, however the value was kept', () => {
  const script = ['let stoppedAt = -1;', 'const stop = () => { stoppedAt = Date.now() - began; };', 'assert.ok(stoppedAt >= 150);'].join('\n');
  assert.deepEqual(lines(timingAsserts(script, 'script')), [3]);
  const taken = ['var took = Stopwatch.GetElapsedTime(given, at);', 'Assert.True(took < TimeSpan.FromSeconds(1));'].join('\n');
  assert.deepEqual(lines(timingAsserts(taken, 'csharp')), [2]);
  const listed = ['times.Add(clock.Elapsed.TotalMilliseconds);', 'Assert.True(Median(times) < 50);'].join('\n');
  assert.deepEqual(lines(timingAsserts(listed, 'csharp')), [2]);
  const spread = ['Assert.True(', '    clock.Elapsed < TimeSpan.FromSeconds(20),', '    "slow");'].join('\n');
  assert.deepEqual(lines(timingAsserts(spread, 'csharp')), [1]);
});

test('a clock read that is a stamp for the fixture, a wait on a stopwatch or a report is none', () => {
  const stamp = ['const earlier = new Date(Date.now() - 3_600_000);', 'assert.equal(stampLoad(dir, 4, Date.now() - 10_000).length, 1);'].join('\n');
  assert.deepEqual(timingAsserts(stamp, 'script'), []);
  const wait = ['var watch = Stopwatch.StartNew();', 'while (!done() && watch.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(10);', 'Assert.True(done());'].join('\n');
  assert.deepEqual(timingAsserts(wait, 'csharp'), []);
  const report = ['var watch = Stopwatch.StartNew();', 'Go();', 'output.WriteLine($"took {watch.ElapsedMilliseconds} ms");', 'Assert.True(Go2());'].join('\n');
  assert.deepEqual(timingAsserts(report, 'csharp'), []);
  const earlier = ['var then = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);', 'Assert.Equal(Moment(then), Said(file));'].join('\n');
  assert.deepEqual(timingAsserts(earlier, 'csharp'), []);
  const share = 'Assert.Equal(expected, UsageCurves.Elapsed(window, reset, Now)!.Value, 6);';
  assert.deepEqual(timingAsserts(share, 'csharp'), []);
  const words = ['// assert.ok(Date.now() - began < 5)', 'const say = "Assert.True(watch.Elapsed < x)";'].join('\n');
  assert.deepEqual(timingAsserts(words, 'script'), []);
});

test('a moved clock\'s reading is no wall clock', () => {
  const moved = ['var clock = new MovedClock();', 'clock.Advance(TimeSpan.FromSeconds(5));', 'Assert.Equal(TimeSpan.FromSeconds(5), clock.GetElapsedTime(start));'].join('\n');
  assert.deepEqual(timingAsserts(moved, 'csharp'), []);
});

let n = 0;
/** A fresh tree with `files`, and the kept list `kept` as `tools/timing-census.json`; returns its root. */
function tree(files, kept = {}) {
  const dir = join(scratch, `t${n++}`);
  const put = (rel, text) => {
    mkdirSync(dirname(join(dir, rel)), { recursive: true });
    writeFileSync(join(dir, rel), text);
  };
  for (const [file, text] of Object.entries(files)) put(file, text);
  put('tools/timing-census.json', JSON.stringify({ kept }));
  return dir;
}
const SLOW = 'const t = Date.now();\nassert.ok(Date.now() - t < 5);\n';

test('a timing assertion in a test file is a problem naming its line and what to do', () => {
  const { problems } = checkTimingTests(tree({ 'tools/a.test.mjs': SLOW }));
  assert.equal(problems.length, 1);
  assert.match(problems[0], /^tools\/a\.test\.mjs:2: /);
  assert.match(problems[0], /relation|moved clock|ratchet/);
});

test('a timing read outside a test file, or in no test project, is none', () => {
  assert.deepEqual(checkTimingTests(tree({ 'tools/a.mjs': SLOW, 'src/Prod/Thing.cs': 'Assert.True(watch.Elapsed < x);' })).problems, []);
});

test('a kept assertion needs its reason, is held to its count and a stale entry is named', () => {
  const kept = { 'tools/a.test.mjs': { count: 1, why: 'the wait\'s own bound, which a moved clock cannot check' } };
  assert.deepEqual(checkTimingTests(tree({ 'tools/a.test.mjs': SLOW }, kept)).problems, []);
  const twice = checkTimingTests(tree({ 'tools/a.test.mjs': `${SLOW}${SLOW}` }, kept)).problems;
  assert.equal(twice.length, 1);
  assert.match(twice[0], /tools\/a\.test\.mjs: 2 timing assertions, 1 kept/);
  const noReason = checkTimingTests(tree({ 'tools/a.test.mjs': SLOW }, { 'tools/a.test.mjs': { count: 1, why: '' } })).problems;
  assert.match(noReason.join('\n'), /no reason/);
  const stale = checkTimingTests(tree({ 'tools/a.test.mjs': 'assert.ok(true);\n' }, kept)).problems;
  assert.match(stale.join('\n'), /tools\/a\.test\.mjs: 0 timing assertions, 1 kept/);
});
