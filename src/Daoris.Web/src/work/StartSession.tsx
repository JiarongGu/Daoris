import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CheckField, SelectField, Tip } from '../ui';

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
export function StartSession({ repositories, harnesses, defaultHarness = '', accounts, pending = false, onStart }: {
  /** Repositories with a checkout on this machine — there is nowhere else to talk (D48 §7). */
  repositories: string[];
  /** The harnesses this machine has; empty offers no choice. */
  harnesses: string[];
  /** The driver's own harness — whose accounts are offered while no other is chosen. */
  defaultHarness?: string;
  /**
   * Each harness's accounts, with their login state (D49 §4). By harness, because the accounts follow
   * the harness CHOSEN (REV3): the default's alone offered names another harness does not have.
   */
  accounts: Record<string, { name: string; login: 'in' | 'out' | 'unknown'; account?: string | null }[]>;
  pending?: boolean;
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

  const chosen = repository || repositories[0]!;

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
          options={repositories.map((name) => ({ value: name, label: name }))}
        />
      </label>

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
              { value: DEFAULT, label: t('work.start.harnessDefault') },
              ...harnesses.map((name) => ({ value: name, label: name })),
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
              // Named by who is signed in, where the tool says (D66 §3) — the same name the
              // settings page gives it; the value is still the directory's, which the spawn takes.
              ...profiles.map((choice) => ({
                value: choice.name,
                label: choice.login === 'out'
                  ? t('harness.profileOut', { name: choice.account ?? choice.name })
                  : choice.account ?? choice.name,
              })),
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
    </form>
  );
}
