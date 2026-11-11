import { type Authored, readAuthored } from '../locales/authored';

/** One typed argument of a command, as its manifest declares it: a `choice` with its choices, the others with none. */
export type CommandArgShown = {
  name: string;
  type: 'text' | 'number' | 'boolean' | 'choice';
  title: Authored;
  required: boolean;
  choices: string[];
};

/** A command a plugin runs in its own process, as `PLUGINS` answers it: every word authored, its name where it wrote no title. */
export type CommandShown = {
  name: string;
  title: Authored;
  about: Authored;
  /** It asks the person, so it runs only in the round card and is never an agent's tool. */
  interactive: boolean;
  tool: boolean;
  writes: boolean;
  /** The sentence asked once before it runs; none asks nothing. */
  confirm: Authored | null;
  /** The records whose menus offer it. */
  on: string[];
  args: CommandArgShown[];
  /** The git identity's token it takes once; none where it takes none. */
  adopts: { hosts: string[]; answer: string } | null;
};

/** The plugin's private storage: what it keeps there, and the command a removal asks to sign it out, or why none is asked. */
export type StorageShown = { for: Authored; forget: string | null; forgetDisabled: string | null };

/** A view a plugin shows, as declared: where it may stand, the places it is about and what it is told of the window. */
export type ViewShown = { id: string; title: Authored; where: string[]; places: string[]; context: string[] };

/** An entry refused alone: its interface, its name or id where it has one, and the reader's sentence. */
export type InterfaceRefusal = { interface: string; entry: string | null; problem: string };

/** What a plugin implements that Daoris reaches it through, besides its points; what does not read is refused entry by entry. */
export type InterfacesShown = {
  commands: CommandShown[];
  storage: StorageShown | null;
  views: ViewShown[];
  refused: InterfaceRefusal[];
  /** Interfaces this build does not know, each named once and skipped. */
  unknown: string[];
};

/**
 * One round of a plugin's declared command from the window, as `PLUGIN_COMMAND` takes it: the plugin's id and the command's name, the
 * run's id from its first answer on (none at the first round), the arguments by name, every answer so far, the place in force and
 * the viewer's language.
 */
export type PluginCommandPress = {
  id: string;
  command: string;
  run?: string;
  args: Record<string, string | number | boolean>;
  answers: Record<string, string>;
  place: string;
  language: string;
};

/** What the plugin asks before it can go on: the answer's name, its words for what to type, and whether it is hidden. */
export type CommandAskShown = { name: string; prompt: string; masked: boolean };

/**
 * One round as the plugin answered it: its lines, what it asks (none where it ended), a page to open and a code to enter there, what
 * a view does next, data for an agent, and its exit where it asks nothing.
 */
export type PluginCommandRound = {
  run: string;
  said: string[];
  ask: CommandAskShown | null;
  open: string | null;
  code: string | null;
  then: unknown;
  data: unknown;
  exit: number;
};

/** The person's word on one interface or point at one place: `machine`, `workspace:<name>` or `repository:<id>`. */
export type SwitchWord = { what: string; place: string; on: boolean };

export const NO_INTERFACES: InterfacesShown = { commands: [], storage: null, views: [], refused: [], unknown: [] };

const MACHINE = 'machine';

const list = <T,>(value: unknown): T[] => (Array.isArray(value) ? value as T[] : []);
const object = (value: unknown): Record<string, unknown> | null =>
  value !== null && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : null;
const text = (value: unknown): string | null => (typeof value === 'string' && value !== '' ? value : null);
const words = (value: unknown): string[] => list<unknown>(value).filter((each): each is string => typeof each === 'string');
const TYPES = ['text', 'number', 'boolean', 'choice'] as const;

function argOf(raw: unknown): CommandArgShown | null {
  const arg = object(raw);
  const name = text(arg?.name);
  if (!arg || !name) return null;
  const type = TYPES.find((each) => each === arg.type) ?? 'text';
  return { name, type, title: readAuthored(arg.title) ?? name, required: arg.required === true, choices: words(arg.choices) };
}

function commandOf(raw: unknown): CommandShown | null {
  const command = object(raw);
  const name = text(command?.name);
  if (!command || !name) return null;
  const adopts = object(command.adopts);
  return {
    name,
    title: readAuthored(command.title) ?? name,
    about: readAuthored(command.about) ?? '',
    interactive: command.interactive === true,
    tool: command.tool === true,
    writes: command.writes === true,
    confirm: readAuthored(command.confirm),
    on: words(command.on),
    args: list<unknown>(command.args).map(argOf).filter((arg): arg is CommandArgShown => arg !== null),
    adopts: adopts ? { hosts: words(adopts.hosts), answer: text(adopts.answer) ?? '' } : null,
  };
}

function viewOf(raw: unknown): ViewShown | null {
  const view = object(raw);
  const id = text(view?.id);
  if (!view || !id) return null;
  const where = words(view.where);
  const places = words(view.places);
  return {
    id, title: readAuthored(view.title) ?? id,
    where: where.length > 0 ? where : ['page'], places: places.length > 0 ? places : [MACHINE], context: words(view.context),
  };
}

/**
 * A plugin's `interfaces` as an older or a stranger driver may answer them, read defensively: what it never sent is none, an
 * entry with no name is dropped, and an older driver's answer with no interfaces at all is `NO_INTERFACES`.
 */
export function interfacesOf(raw: unknown): InterfacesShown {
  const declared = object(raw);
  if (!declared) return NO_INTERFACES;
  const storage = object(declared.storage);
  return {
    commands: list<unknown>(declared.commands).map(commandOf).filter((each): each is CommandShown => each !== null),
    storage: storage
      ? { for: readAuthored(storage.for) ?? '', forget: text(storage.forget), forgetDisabled: text(storage.forgetDisabled) }
      : null,
    views: list<unknown>(declared.views).map(viewOf).filter((each): each is ViewShown => each !== null),
    refused: list<unknown>(declared.refused).map(object).filter((each) => each !== null && text(each.problem) !== null)
      .map((each) => ({ interface: text(each!.interface) ?? '', entry: text(each!.entry), problem: text(each!.problem)! })),
    unknown: words(declared.unknown),
  };
}

/** The person's words on a plugin's parts, each kept only where it names what, a place and a yes or no. */
export const switchesOf = (raw: unknown): SwitchWord[] =>
  list<unknown>(raw).map(object)
    .filter((each) => each !== null && text(each.what) !== null && text(each.place) !== null && typeof each.on === 'boolean')
    .map((each) => ({ what: each!.what as string, place: each!.place as string, on: each!.on as boolean }));

/** Whether it declares anything Daoris reaches it through, refused entries and interfaces this build does not know included. */
export const declaresInterfaces = (declared: InterfacesShown): boolean =>
  declared.commands.length > 0 || declared.storage !== null || declared.views.length > 0
  || declared.refused.length > 0 || declared.unknown.length > 0;

/**
 * Whether `what` is on at `place`, as the driver's resolver reads it: the place's own word, else its workspace's where the place
 * is a repository of one, else the machine's, else on. `storage` reads the machine's word alone.
 */
export function switchedOn(words: readonly SwitchWord[], what: string, place: string, workspace?: string | null): boolean {
  const said = (at: string) => words.find((word) => word.what === what && word.place === at)?.on;
  if (what === 'storage') return said(MACHINE) ?? true;
  return said(place) ?? (workspace ? said(`workspace:${workspace}`) : undefined) ?? said(MACHINE) ?? true;
}

/** The places that hold a word of their own on `what`, the machine's first, then as written. */
export const placesWithWords = (words: readonly SwitchWord[], what: string): string[] => [
  MACHINE,
  ...words.filter((word) => word.what === what && word.place !== MACHINE).map((word) => word.place),
];
