/**
 * A `daoris` verb's door to the running Daoris (RPC1f, D171 point 3, the terminal RPC design §7 and §9): the shapes a verb hands
 * its door, and the routes the management verbs send. The door itself, which reaches the pipe, is `service.ts`'s `instanceDoor`,
 * handed in by each verb's row as the records and the fetcher are, so the module that edits a file reaches no network (D50).
 */
import type { ExitCode } from './errors.ts';

/**
 * How the running Daoris's pipe treats a route (RPC1a's census, the terminal RPC design §4.2): the driver's `RouteClass`, twinned
 * from the modules' `PipeCensus`, which `door.test.ts` reads to hold each route here to its class, name for name.
 */
export type RouteClass = 'open' | 'person' | 'window';

/** A route a verb sends over the pipe, with the class the census gives it. */
export interface InstanceRoute {
  /** The module, as the kit's dispatcher names it. */
  readonly module: string;
  /** The route, as its module's table names it. */
  readonly route: string;
  readonly class: RouteClass;
}

/** The driver's module, as the desktop's `DriverModule.Name` spells it. */
export const DRIVER_MODULE = 'DAORIS.DRIVER';

const person = (route: string): InstanceRoute => ({ module: DRIVER_MODULE, route, class: 'person' });

/**
 * The routes `daoris driver` sends while Daoris runs, each the window twin of the choice it edits (§9): every one is the person's
 * (D171's RPC1a note: every standing choice widens what an agent may do in one direction), so each waits for the window's Confirm
 * (RPC1c) before the instance's own handler judges, writes and publishes it.
 */
export const DRIVER_ROUTES = {
  drivable: person('SET_DRIVABLE'),
  hold: person('SET_HOLD'),
  trees: person('SET_TREES'),
  notify: person('SET_NOTIFY'),
  strikes: person('SET_STRIKES'),
  cooloff: person('SET_COOLOFF'),
  intake: person('SET_INTAKE'),
  helper: person('SET_HELPER'),
  retry: person('RETRY_QUEST'),
  line: person('SET_LINE'),
  language: person('SET_LANGUAGE'),
  landing: person('SET_LANDING'),
  readAcross: person('SET_READ_ACROSS'),
  writeAcross: person('SET_WRITE_ACROSS'),
  standing: person('SET_STANDING'),
  review: person('SET_REVIEW'),
  opinion: person('SET_OPINION'),
  helpers: person('SET_HELPERS'),
  commits: person('SET_COMMITS'),
} as const satisfies Record<string, InstanceRoute>;

/**
 * Each `daoris driver` verb that changes the home, in the verb's words: the route it sends while Daoris runs, or null with the reason
 * it acts alone (under the home's lease where none runs, beside the instance as it always did where one does), as the driver's
 * `OpenRoutes.Verbs` names its own.
 */
export const DRIVER_VERBS: readonly (readonly [verb: string, route: InstanceRoute | null, reason: string])[] = [
  ['driver drive|undrive <repo>', DRIVER_ROUTES.drivable, 'the window\'s drive switch'],
  ['driver hold|resume <repo>', DRIVER_ROUTES.hold, 'the window\'s hold'],
  ['driver trees <repo> on|off', DRIVER_ROUTES.trees, 'the window\'s own-tree switch'],
  ['driver notify on|off', DRIVER_ROUTES.notify, 'the window\'s notify switch'],
  ['driver strikes <n>', DRIVER_ROUTES.strikes, 'the window\'s strike limit'],
  ['driver cooloff <minutes>', DRIVER_ROUTES.cooloff, 'the window\'s cool-off'],
  ['driver intake <adapter>|off', DRIVER_ROUTES.intake, 'the window\'s intake agent'],
  ['driver helper <adapter>|off', DRIVER_ROUTES.helper, 'Ask Daoris\'s agent'],
  ['driver retry <quest>', DRIVER_ROUTES.retry, 'the quest\'s *Try again*, which judges from the planner\'s verdict whether to mark or release it'],
  ['driver line', DRIVER_ROUTES.line, 'the repository\'s or the workspace\'s line'],
  ['driver language', DRIVER_ROUTES.language, 'the session language'],
  ['driver landing', DRIVER_ROUTES.landing, 'the landing rule, sent with its key and its squash named so the rule is replaced whole, as the verb replaces it'],
  ['driver across … read', DRIVER_ROUTES.readAcross, 'reading across'],
  ['driver across … write-to', DRIVER_ROUTES.writeAcross, 'writing across'],
  ['driver standing', DRIVER_ROUTES.standing, 'the standing answer'],
  ['driver review', DRIVER_ROUTES.review, 'the review rule; its procedure is looked for by the instance in the registry\'s checkouts'],
  ['driver opinion', DRIVER_ROUTES.opinion, 'the second-opinion rule'],
  ['driver helpers', DRIVER_ROUTES.helpers, 'the helpers rule'],
  ['driver commits', DRIVER_ROUTES.commits, 'the commit rule'],
  ['driver cap <n>', null, 'alone: no route sets the cap; the window shows it and has no press for it'],
  ['driver timeout <minutes>', null, 'alone: no route sets a session\'s timeout'],
  ['driver adapter <name>', null, 'alone: no route sets the working agent; the window names it from the roster'],
  ['driver retry <quest> --at <n>', null, 'alone: RETRY_QUEST marks at the failures its verdict counts, never at a number the person gives'],
  ['driver retry <quest> --session <id>', null, 'alone: RETRY_QUEST releases the stop its verdict names, never a session the person names'],
  ['driver landing … --cycle <file>|--no-cycle', null, 'alone: SET_LANDING has no field for the cycle, and keeps the one the rule holds'],
  ['driver work-cycle', null, 'alone: no route sets a quest\'s or an ask\'s cycle of work'],
  ['driver brainstorms', null, 'alone: no route sets the brainstorm policy'],
  ['driver workflow new|edit|apply|import|use|kind', null, 'alone: no route writes a named workflow or chooses one'],
];

/** What a verb hands its door: the route and payload it sends where Daoris runs, and what it does where none does. */
export interface VerbPress {
  /** The verb as a lease names it (`driver drive`), never its arguments. */
  readonly what: string;
  /** The route it sends while Daoris runs, or null where none takes it: the verb then acts alone. */
  readonly route: InstanceRoute | null;
  /** The route's payload, as a window's press sends it. */
  readonly payload?: unknown;
  /** The verb acting from its own process, as it does where no Daoris runs. */
  readonly alone: () => ExitCode | Promise<ExitCode>;
  /** The route's answer (its `data`) in the verb's own words, the alone path's text, returning its exit code (§7.3). */
  readonly words: (answer: unknown) => ExitCode | Promise<ExitCode>;
}

/** A verb's door: the switch of §7.2, or a test's. */
export type VerbDoor = (press: VerbPress) => ExitCode | Promise<ExitCode>;

/**
 * No door handed in: the verb acts alone, holding nothing, as it did before RPC1f. A test's, and a `driver.json` the person names by
 * `DAORIS_DRIVER_CONFIG`, which is no instance's file.
 */
export const ALONE: VerbDoor = (press) => press.alone();
