/**
 * A `daoris` verb's door to the running Daoris (RPC1f, D171 point 3, the terminal RPC design §7 and §9): the shapes a verb hands
 * its door, and the routes the management verbs send. The door itself, which reaches the pipe, is `service.ts`'s `instanceDoor`,
 * handed in by each verb's row as the records and the fetcher are, so the module that edits a file reaches no network (D50).
 *
 * A verb that changes the home goes through its door, a read never: its route named in the tables below with the class the census
 * gives it (`door.test.ts` reads `PipeCensus.cs`), or null with why it acts alone; its own checks made before the send; and its
 * words one function both paths call, so the terminal's text is one (§7.3). Its tests hand a door that records the press and answers
 * as the instance would, and hold the client path's text to the alone path's.
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

// `daoris driver`'s routes and its verbs that act alone are Daoris's driver program's since PUSH1f2b (D194 point 7): the driver's
// `ChoiceRoutes`, held to the census by the modules' `PipeCensusTests`.

/**
 * The routes `daoris plugin` sends while Daoris runs (the Plugins place's presses): code that runs in the driver, so each is the
 * person's. `PLUGIN_UPDATE` is classed by its payload, and is the person's with `apply: true`, the only form a verb sends.
 */
export const PLUGIN_ROUTES = {
  add: person('PLUGIN_ADD'),
  install: person('PLUGIN_INSTALL'),
  update: person('PLUGIN_UPDATE'),
  action: person('PLUGIN_ACTION'),
} as const satisfies Record<string, InstanceRoute>;

/** Each `daoris plugin` verb that changes the home: its route, or null with the reason it acts alone. */
export const PLUGIN_VERBS: readonly (readonly [verb: string, route: InstanceRoute | null, reason: string])[] = [
  ['plugin add <folder>', PLUGIN_ROUTES.add, 'the Plugins place\'s install from a folder, for a plugin not installed here'],
  ['plugin add --offer <id>', PLUGIN_ROUTES.install, 'the Install beside one of the install\'s offers, for one not installed here'],
  ['plugin update <id> --yes', PLUGIN_ROUTES.update, 'the update\'s press, sent with `apply: true`; the plan without --yes is read here'],
  ['plugin remove|enable|disable <id>', PLUGIN_ROUTES.action, 'the row\'s switch, and its remove, which stops the plugin\'s process before the folder goes'],
  ['plugin add … (installed already)', null, 'alone: PLUGIN_ADD and PLUGIN_INSTALL add and never replace; a replace is the terminal\'s'],
];

// `daoris tool`'s routes and verbs are the driver program's since PUSH1f2c (D194): its `ToolRoutes`, held to the census there.

// `daoris agent rules|trust|settings`'s routes and verbs are the driver program's since PUSH1f2d (D194): its `AgentRoutes`, held to the
// census there, with which home each writes.

// `daoris agent`'s routes are the driver program's since PUSH1f2e3: `HARNESS_ACTION` (PUSH1f2e2) and `ACCOUNT_USE`, its `AgentRoutes`.

/**
 * Each `daoris agent` verb still here that changes the home: none takes a route, each with the reason it acts alone. `install` and `key`
 * are the driver program's since PUSH1f2e5, their rows its `AgentRoutes`'.
 */
export const AGENT_VERBS: readonly (readonly [verb: string, route: InstanceRoute | null, reason: string])[] = [
  ['agent update <agent>', null, 'alone: the agent\'s own updater, or a pin\'s move, whose output is this terminal\'s; HARNESS_ACTION runs it in the window\'s console'],
  ['agent login <agent>', null, 'alone: the agent\'s own sign-in, whose prompts are this terminal\'s; HARNESS_ACTION runs it in the window\'s console'],
  ['agent pin <agent> <version>', null, 'alone: a download through this terminal\'s fetcher, its checks said here; HARNESS_ACTION runs it in the window\'s console'],
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
 * No door handed in: the verb acts alone, holding nothing, as it did before RPC1f. A test's, and a harness file the person names by
 * `DAORIS_HARNESS_CONFIG`, which is no instance's file.
 */
export const ALONE: VerbDoor = (press) => press.alone();
