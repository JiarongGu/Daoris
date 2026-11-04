import { PUBLISHES } from '../bridge/generated/routes';

/**
 * The shell's state publisher, for a mocked shell: a test that presses a hook reads what the press moved through the state hub,
 * as a window does, since the hooks ask nothing again themselves. What each press declares it changes is its `[Publishes]` mark,
 * generated into `PUBLISHES` by module and route.
 */

type Invoke = (module: string, type: string, options?: { payload?: unknown }) => Promise<unknown>;

/** Where a test's mocked `useShenoraEvent` keeps the page's handler for a Daoris event, by its type. */
export type Hear = (type: 'STATE_CHANGED' | 'UNDER_WAY', payload: unknown) => void;

/**
 * `invoke` answered as the shell answers with `StatePublisher` in its pipeline: a declared press is told under way, whole and
 * numbered, from its send until it answers or throws, and what it changed is published once it answered, a refusal answered as
 * data among it and one thrown not. Each reaches `hear` on a later turn, as the kit's pump delivers it.
 */
export function publishing(invoke: Invoke, hear: Hear): Invoke {
  const underWay: { route: string; session?: string; subject?: string }[] = [];
  let seq = 0;
  const tell = (type: 'STATE_CHANGED' | 'UNDER_WAY', payload: unknown) => { setTimeout(() => hear(type, payload), 0); };
  const telling = () => tell('UNDER_WAY', { seq: ++seq, presses: [...underWay] });
  return async (module, type, options) => {
    const declared = PUBLISHES[module]?.[type];
    if (!declared) return invoke(module, type, options);
    const field = (name?: string) => {
      const named = name ? (options?.payload as Record<string, unknown> | undefined)?.[name] : undefined;
      return typeof named === 'string' && named.length > 0 ? named : undefined;
    };
    const session = field(declared.session);
    const subject = field(declared.subject);
    const press = { route: type, ...(session ? { session } : {}), ...(subject ? { subject } : {}) };
    underWay.push(press);
    telling();
    try {
      const answered = await invoke(module, type, options);
      tell('STATE_CHANGED', session ? { scopes: declared.scopes, sessions: [session] } : { scopes: declared.scopes });
      return answered;
    } finally {
      underWay.splice(underWay.indexOf(press), 1);
      telling();
    }
  };
}
