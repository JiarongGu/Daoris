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

/**
 * The routes `daoris agent` sends while Daoris runs (the Agents and Accounts places, the rules): a trust, an account, a rule's answer
 * and a rule widen what an agent may do in one direction, so each is the person's.
 */
export const AGENT_ROUTES = {
  settings: person('SET_AGENT_SETTINGS'),
  rule: person('RULE_ACTION'),
  proposal: person('RULE_PROPOSAL'),
  account: person('ACCOUNT_USE'),
  harness: person('HARNESS_ACTION'),
} as const satisfies Record<string, InstanceRoute>;

/** Each `daoris agent` verb that changes the home: its route, or null with the reason it acts alone. */
export const AGENT_VERBS: readonly (readonly [verb: string, route: InstanceRoute | null, reason: string])[] = [
  ['agent rules allow|ask|deny|remove|default', AGENT_ROUTES.rule, 'the rules screen\'s edit'],
  ['agent rules accept|decline <id>', AGENT_ROUTES.proposal, 'the person\'s answer to an agent\'s proposal'],
  ['agent settings <agent> … model|effort', AGENT_ROUTES.settings, 'an account\'s model and effort'],
  ['agent profile add|remove|default|rename|join', AGENT_ROUTES.harness, 'the Accounts place\'s `profile-*` actions'],
  ['agent unpin <agent>', AGENT_ROUTES.harness, 'the machine\'s pin taken back'],
  ['agent profile order|use|ready', AGENT_ROUTES.account, 'the Accounts place\'s order, use and *Try now*'],
  ['agent install|update <agent>', null, 'alone: the agent\'s own installer or updater, whose output is this terminal\'s; HARNESS_ACTION runs it in the window\'s console'],
  ['agent login <agent>', null, 'alone: the agent\'s own sign-in, whose prompts are this terminal\'s; HARNESS_ACTION runs it in the window\'s console'],
  ['agent key <agent>', null, 'alone: the key is read from this terminal\'s standard input, and never crosses a wire'],
  ['agent pin <agent> <version>', null, 'alone: a download through this terminal\'s fetcher, its checks said here; HARNESS_ACTION runs it in the window\'s console'],
  ['agent unpin <agent> --workspace <name>', null, 'alone: HARNESS_ACTION\'s unpin names no workspace'],
  ['agent trust <agent> <folder> --yes', null, 'alone: TRUST_FOLDER grants only a folder the driver is holding, and this terminal names any folder'],
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
