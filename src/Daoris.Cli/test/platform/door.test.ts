import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import {
  AGENT_ROUTES, AGENT_VERBS, DRIVER_MODULE, PLUGIN_ROUTES, PLUGIN_VERBS, TOOL_ROUTES, TOOL_VERBS, type InstanceRoute, type RouteClass,
} from '../../src/platform/door.ts';
import { desktopFile } from '../_csharp.ts';

/**
 * The routes `daoris` verbs send over the running Daoris's pipe (RPC1f) are classed as the modules' census classes them (RPC1a's
 * `PipeCensus`, the terminal RPC design §4.2): read from its source, name for name, so a route the census moves fails here and names
 * the verb whose door then changes (`twins.md`'s "How to apply": a source-reading test where the two share no code).
 */

/**
 * The driver's routes the census classes, each with its class, read from `PipeCensus.Driver()`'s `Add` calls; and a route classed
 * by its payload (`PipeRule.Of(field, when, otherwise, values…)`), with the class the payload a verb sends (`PAYLOADS`) is given.
 */
function censusClasses(): Map<string, RouteClass> {
  const source = readFileSync(desktopFile('Daoris.Desktop.Modules', 'PipeCensus.cs'), 'utf8');
  const driver = source.slice(source.indexOf('private static Dictionary<string, PipeRule> Driver()'));
  const classes = new Map<string, RouteClass>();
  for (const [, named, body] of driver.matchAll(/\bAdd\(PipeClass\.(Open|Person|Window),([\s\S]*?)\);/g)) {
    for (const [, route] of body!.matchAll(/"([A-Z_]+)"/g)) classes.set(route!, named!.toLowerCase() as RouteClass);
  }
  const rule = /table\.Add\("([A-Z_]+)", PipeRule\.Of\("(\w+)", PipeClass\.(\w+), PipeClass\.(\w+), ([^)]*)\)\);/g;
  for (const [, route, field, when, otherwise, values] of driver.matchAll(rule)) {
    const sent = PAYLOADS[route!]?.[field!];
    const named = [...values!.matchAll(/"((?:\\"|[^"])*)"/g)].map(([, value]) => value!.replaceAll('\\"', '"'));
    classes.set(route!, (sent !== undefined && named.includes(sent) ? when! : otherwise!).toLowerCase() as RouteClass);
  }
  return classes;
}

/** The payload field a route classed by its payload is sent with by a verb, as JSON: `plugin update --yes` sends `apply: true`. */
const PAYLOADS: Record<string, Record<string, string>> = { PLUGIN_UPDATE: { apply: 'true' } };

// PUSH1f2b: `daoris driver`'s routes are Daoris's driver program's (`ChoiceRoutes`), held to the census by `PipeCensusTests`.
const SENT: readonly InstanceRoute[] = [...Object.values(PLUGIN_ROUTES), ...Object.values(TOOL_ROUTES), ...Object.values(AGENT_ROUTES)];
const VERBS = [...PLUGIN_VERBS, ...TOOL_VERBS, ...AGENT_VERBS];

test('the census is read: its reads are open, its standing choices the person\'s, its pickers the window\'s', () => {
  const classes = censusClasses();
  assert.equal(classes.get('STATE'), 'open');
  assert.equal(classes.get('SET_TREES'), 'person');
  assert.equal(classes.get('TOOLS_PICK'), 'window');
  // A route classed by its payload: an update's preview is open, and the press a verb sends, `apply: true`, the person's.
  assert.equal(classes.get('PLUGIN_UPDATE'), 'person');
  assert.ok(classes.size > 100, `the census read ${classes.size} routes`);
});

test('each route a `daoris` verb sends is the driver\'s, classed as the census classes it', () => {
  const classes = censusClasses();
  for (const route of SENT) {
    assert.equal(route.module, DRIVER_MODULE, route.route);
    assert.equal(classes.get(route.route), route.class, `${route.route} is ${classes.get(route.route) ?? 'not in the census'} there`);
  }
});

test('each verb that changes the home names its route, or why it acts alone', () => {
  const sent = new Set(SENT);
  for (const [verb, route, reason] of VERBS) {
    assert.ok(reason.length > 0, verb);
    if (route === null) assert.match(reason, /^alone: /, verb);
    else assert.ok(sent.has(route), `${verb} sends a route the table does not hold`);
  }
  // Every route the tables hold is some verb's.
  for (const route of sent) assert.ok(VERBS.some(([, each]) => each === route), `${route.route} is no verb's`);
});
