import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { DRIVER_MODULE, DRIVER_ROUTES, DRIVER_VERBS, type RouteClass } from '../../src/platform/door.ts';
import { desktopFile } from '../_csharp.ts';

/**
 * The routes `daoris` verbs send over the running Daoris's pipe (RPC1f) are classed as the modules' census classes them (RPC1a's
 * `PipeCensus`, the terminal RPC design §4.2): read from its source, name for name, so a route the census moves fails here and names
 * the verb whose door then changes (`twins.md`'s "How to apply": a source-reading test where the two share no code).
 */

/** The driver's routes the census classes, each with its class, read from `PipeCensus.Driver()`'s `Add` calls. */
function censusClasses(): Map<string, RouteClass> {
  const source = readFileSync(desktopFile('Daoris.Desktop.Modules', 'PipeCensus.cs'), 'utf8');
  const driver = source.slice(source.indexOf('private static Dictionary<string, PipeRule> Driver()'));
  const classes = new Map<string, RouteClass>();
  for (const [, named, body] of driver.matchAll(/\bAdd\(PipeClass\.(Open|Person|Window),([\s\S]*?)\);/g)) {
    for (const [, route] of body!.matchAll(/"([A-Z_]+)"/g)) classes.set(route!, named!.toLowerCase() as RouteClass);
  }
  return classes;
}

test('the census is read: its reads are open, its standing choices the person\'s, its pickers the window\'s', () => {
  const classes = censusClasses();
  assert.equal(classes.get('STATE'), 'open');
  assert.equal(classes.get('SET_TREES'), 'person');
  assert.equal(classes.get('TOOLS_PICK'), 'window');
  assert.ok(classes.size > 100, `the census read ${classes.size} routes`);
});

test('each route a `daoris driver` verb sends is the driver\'s, classed as the census classes it', () => {
  const classes = censusClasses();
  for (const [name, route] of Object.entries(DRIVER_ROUTES)) {
    assert.equal(route.module, DRIVER_MODULE, name);
    assert.equal(classes.get(route.route), route.class, `${name}: ${route.route} is ${classes.get(route.route) ?? 'not in the census'} there`);
  }
});

test('each verb that changes the home names its route, or why it acts alone', () => {
  const sent = new Set(Object.values(DRIVER_ROUTES));
  for (const [verb, route, reason] of DRIVER_VERBS) {
    assert.ok(reason.length > 0, verb);
    if (route === null) assert.match(reason, /^alone: /, verb);
    else assert.ok(sent.has(route), `${verb} sends a route the table does not hold`);
  }
  // Every route the table holds is some verb's.
  for (const route of sent) assert.ok(DRIVER_VERBS.some(([, each]) => each === route), `${route.route} is no verb's`);
});
