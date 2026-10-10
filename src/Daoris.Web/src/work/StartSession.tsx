import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { accountName, accountWho } from '../agents/agents';
import type { Account } from '../tools';
import { Button, CheckField, Inline, SelectField, Tip } from '../ui';

/**
 * "Leave it to the driver", as a select's value: the platform's select reserves the empty value for
 * "nothing chosen", and a default must stay choosable after another choice was made.
 */
const DEFAULT = '*';

export type StartChoice = {
  repository: string;
  adapter?: string;
  profile?: string;
  ownTree?: boolean;
};

/**
 * Starting a session, where the result appears (design §3).
 *
 * @remarks
 * **It moved out of Projects deliberately.** Projects keeps the registry's own controls; a
 * conversation is not a registry act, and starting one from a view that then shows you nothing was
 * the shape of the problem D55 is fixing.
 *
 * **Four choices, and every one of them may be left alone.** Silence resolves the way a driven
 * session resolves — the adapter from `driver.json`, the profile from the workspace's default then
 * the machine's then the harness's own home (D49 §4), the tree from the repository's standing
 * opt-in (D51). A conversation is not a second set of rules, so this offers no default of its own
 * to disagree with the driver's.
 *
 * **A fresh tree's cost is stated where the tree is chosen** (D51 §4), not discovered later by a
 * session whose gates cannot run.
 *
 * A molecule: the rosters arrive as props, which is what makes "no repository has a checkout here",
 * "one harness, no accounts" and "a logged-out profile" reachable without a machine in that state.
 */
export function StartSession({
  repositories, busy = [], harnesses, labels = {}, defaultHarness = '', accounts, scopeOf, pending = false, refusal, onStart,
}: {
  /**
   * The list of accounts a start in this repository may run on, for this harness (D130 §3.2): its workspace's own list, or
   * else this machine's, with the workspace it is, or null for this machine's. Null where neither names a list; then every
   * account is offered alike.
   */
  scopeOf?: (repository: string, harness: string) => { workspace: string | null; list: string[] } | null;
  /** Repositories with a checkout on this machine — there is nowhere else to talk (D48 §7). */
  repositories: string[];
  /**
   * Repositories whose checkout an active session holds (UX5 U68): the form opens on one that is
   * free, marks a busy one, and says a working tree of its own starts beside it.
   */
  busy?: string[];
  /** The harnesses this machine has; empty offers no choice. */
  harnesses: string[];
  /**
   * What a person calls each way in — the tool and the door, as Settings names them (UX5 U67). The
   * value stays the id the driver takes; absent, the id is the name.
   */
  labels?: Record<string, string>;
  /** The driver's own harness — whose accounts are offered while no other is chosen. */
  defaultHarness?: string;
  /**
   * Each harness's accounts, with their login state (D49 §4). By harness, because the accounts follow
   * the harness CHOSEN (REV3): the default's alone offered names another harness does not have.
   */
  accounts: Record<string, Pick<Account, 'name' | 'login' | 'account' | 'displayName' | 'key'>[]>;
  pending?: boolean;
  /**
   * Why the last start was refused, in the driver's own words (UX5 U68): said here, whole, under the
   * form it answers. It was a corner toast, cut mid-sentence, beside a form that said nothing.
   */
  refusal?: string | null;
  onStart: (choice: StartChoice) => void;
}) {
  const { t } = useTranslation();
  const [repository, setRepository] = useState('');
  const [adapter, setAdapter] = useState('');
  const [profile, setProfile] = useState('');
  const profiles = accounts[adapter || defaultHarness] ?? [];
  const [ownTree, setOwnTree] = useState(false);

  if (repositories.length === 0) {
    return <p className="m-0 px-3 py-2 text-small text-ink-faint">{t('work.start.none')}</p>;
  }

  // Where nothing is working, unless the person chose: a busy checkout refuses a second session.
  const chosen = repository || repositories.find((name) => !busy.includes(name)) || repositories[0]!;
  const held = busy.includes(chosen) && !ownTree;
  const named = (id: string) => labels[id] ?? id;
  // The scope's accounts first, in its list's order, then every other under *not in its list* (D130 §3.2): a pick outside
  // the list is the person's own choice, never one made by accident.
  const scope = scopeOf?.(chosen, adapter || defaultHarness) ?? null;
  const listed = scope && scope.list.length > 0 ? scope.list : null;
  const outside = scope?.workspace
    ? t('work.start.outside', { workspace: scope.workspace })
    : t('work.start.outsideMachine');
  const ordered = listed
    ? [
      ...listed.map((name) => profiles.find((choice) => choice.name === name)).filter((choice) => choice !== undefined),
      ...profiles.filter((choice) => !listed.includes(choice.name)),
    ]
    : profiles;

  return (
    <form
      className="grid gap-2 px-3 py-2.5"
      onSubmit={(event) => {
        event.preventDefault();
        onStart({
          repository: chosen,
          adapter: adapter || undefined,
          profile: profile || undefined,
          ownTree: ownTree || undefined,
        });
      }}
    >
      {/* The platform's own controls (UX5 U5): a native select and checkbox wore the OS's look and
          its accent in a palette that draws its own (platform language §4). */}
      <label className="grid gap-1 text-meta text-ink-faint">
        {t('work.start.repository')}
        <SelectField
          value={chosen}
          onChange={setRepository}
          ariaLabel={t('work.start.repository')}
          options={repositories.map((name) => ({
            value: name,
            label: busy.includes(name) ? t('work.start.busyOption', { repository: name }) : name,
          }))}
        />
      </label>
      {/* What lets a busy one start after all, said where it is chosen (UX5 U68). */}
      {held && <p className="m-0 text-small text-ink-soft">{t('work.start.busy', { repository: chosen })}</p>}

      {harnesses.length > 1 && (
        <label className="grid gap-1 text-meta text-ink-faint">
          {t('work.start.harness')}
          <SelectField
            value={adapter || DEFAULT}
            onChange={(next) => {
              setAdapter(next === DEFAULT ? '' : next);
              // An account is a harness's own: one chosen for another harness means nothing here.
              setProfile('');
            }}
            ariaLabel={t('work.start.harness')}
            options={[
              // Which it is, where the machine has one (UX5 U67): "this machine's default" said
              // nothing of what a start would run on.
              {
                value: DEFAULT,
                label: defaultHarness
                  ? t('work.start.harnessDefaultNamed', { name: named(defaultHarness) })
                  : t('work.start.harnessDefault'),
              },
              ...harnesses.map((name) => ({ value: name, label: named(name) })),
            ]}
          />
        </label>
      )}

      {profiles.length > 0 && (
        <label className="grid gap-1 text-meta text-ink-faint">
          {t('work.start.profile')}
          <SelectField
            value={profile || DEFAULT}
            onChange={(next) => setProfile(next === DEFAULT ? '' : next)}
            ariaLabel={t('work.start.profile')}
            options={[
              { value: DEFAULT, label: t('work.start.profileDefault') },
              // A logged-out profile is offered and labelled rather than hidden: the spawn refuses
              // with the sentence that names the login action, which teaches more than a missing row.
              // Named as its row on the agent's page leads with it (ACCTNAME1, D152 §4.2): the person's name, with who
              // signed in beside it where the two differ; the value is still the id, which the spawn takes.
              ...ordered.map((choice) => {
                const who = accountWho(choice);
                const name = who ? `${accountName(choice)} · ${who}` : accountName(choice);
                return {
                  value: choice.name,
                  // ACCTUX1b: a key read signed out is a key refused, repaired by a new key and never by a sign-in.
                  label: choice.login === 'out' ? t(choice.key ? 'harness.profileRefused' : 'harness.profileOut', { name }) : name,
                  ...(listed && !listed.includes(choice.name) ? { group: outside } : {}),
                };
              }),
            ]}
          />
        </label>
      )}

      <Tip content={t('work.start.ownTreeTip')}>
        <span className="w-fit">
          <CheckField checked={ownTree} onChange={setOwnTree} label={t('work.start.ownTree')} />
        </span>
      </Tip>

      {/* Sized to its word (platform language §4): in the form's grid it ran the drawer's width. */}
      <Button variant="primary" type="submit" disabled={pending} className="justify-self-start">{t('work.start.go')}</Button>

      {/* The driver's own sentence, whole, where the start was pressed (UX5 U68): it names what to do,
          and a paraphrase of an instruction is a different instruction (D46 §3). */}
      {refusal && (
        <p role="alert" className="m-0 whitespace-pre-wrap border-l-[3px] border-warn pl-2.5 text-small text-ink-soft">
          {/* Its code marks as code, as the toast drew them: `engine` is a name, not punctuation. */}
          <Inline text={refusal} />
        </p>
      )}
    </form>
  );
}
