import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Tip } from '../ui';

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
export function StartSession({ repositories, harnesses, profiles, pending = false, onStart }: {
  /** Repositories with a checkout on this machine — there is nowhere else to talk (D48 §7). */
  repositories: string[];
  /** The harnesses this machine has; the first is the driver's own, and empty offers no choice. */
  harnesses: string[];
  /** The accounts the chosen harness holds, with their login state (D49 §4). */
  profiles: { name: string; login: 'in' | 'out' | 'unknown' }[];
  pending?: boolean;
  onStart: (choice: StartChoice) => void;
}) {
  const { t } = useTranslation();
  const [repository, setRepository] = useState('');
  const [adapter, setAdapter] = useState('');
  const [profile, setProfile] = useState('');
  const [ownTree, setOwnTree] = useState(false);

  if (repositories.length === 0) {
    return <p className="m-0 px-3 py-2 text-small text-ink-faint">{t('work.start.none')}</p>;
  }

  const chosen = repository || repositories[0];
  const field = 'rounded-control border border-line-strong bg-raised px-2 py-1 text-small text-ink';

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
      <label className="grid gap-1 text-meta text-ink-faint">
        {t('work.start.repository')}
        <select value={chosen} onChange={(event) => setRepository(event.target.value)} className={field}>
          {repositories.map((name) => <option key={name} value={name}>{name}</option>)}
        </select>
      </label>

      {harnesses.length > 1 && (
        <label className="grid gap-1 text-meta text-ink-faint">
          {t('work.start.harness')}
          <select value={adapter} onChange={(event) => setAdapter(event.target.value)} className={field}>
            <option value="">{t('work.start.harnessDefault')}</option>
            {harnesses.map((name) => <option key={name} value={name}>{name}</option>)}
          </select>
        </label>
      )}

      {profiles.length > 0 && (
        <label className="grid gap-1 text-meta text-ink-faint">
          {t('work.start.profile')}
          <select value={profile} onChange={(event) => setProfile(event.target.value)} className={field}>
            <option value="">{t('work.start.profileDefault')}</option>
            {profiles.map((choice) => (
              // A logged-out profile is offered and labelled rather than hidden: the spawn refuses
              // with the sentence that names the login action, which teaches more than a missing row.
              <option key={choice.name} value={choice.name}>
                {choice.login === 'out' ? t('harness.profileOut', { name: choice.name }) : choice.name}
              </option>
            ))}
          </select>
        </label>
      )}

      <Tip content={t('work.start.ownTreeTip')}>
        <label className="flex items-center gap-1.5 text-small text-ink-soft">
          <input
            type="checkbox"
            checked={ownTree}
            onChange={(event) => setOwnTree(event.target.checked)}
            className="accent-accent"
          />
          {t('work.start.ownTree')}
        </label>
      </Tip>

      <Button variant="primary" type="submit" disabled={pending}>{t('work.start.go')}</Button>
    </form>
  );
}
