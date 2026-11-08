import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Pill, Prose } from '../../design/ui';
import { PageSection } from '../../work/frame/ViewMain';
import type { FilesLook, FilesPress } from './filesLook';

/**
 * **Your paths read as written**: each path of the person's rules that matches a folder without what lies under it, which git's
 * syntax cannot say, with the two wordings git can state; one press words them all and rewrites the person's rules in git's syntax.
 * Nothing shows where no such path stands.
 */
export function FilesKept({ look, onPress }: { look: FilesLook; onPress?: (press: FilesPress) => void }) {
  const { t } = useTranslation();
  const kept = look.kept ?? [];
  const [chosen, setChosen] = useState<Record<string, boolean>>({});
  if (kept.length === 0) return null;
  const key = (scope: string, path: string) => `${scope}\u0000${path}`;
  const ready = kept.every((each) => chosen[key(each.scope, each.path)] !== undefined);

  return (
    <PageSection title={t('files.view.kept')}>
      <div className="grid gap-3">
        <Prose className="text-small">{t('files.view.keptLead')}</Prose>
        <ul aria-label={t('files.view.kept')} className="m-0 grid list-none gap-2 p-0 text-small">
          {kept.map((each) => {
            const name = key(each.scope, each.path);
            return (
              <li key={name} className="grid gap-1 border-t border-line pt-1 first:border-t-0 first:pt-0">
                <div className="flex min-w-0 flex-wrap items-center gap-2">
                  <Pill>{t(`files.view.kind.${each.kind}`)}</Pill>
                  <span className="min-w-0 flex-1 font-mono [overflow-wrap:anywhere]">{each.path}</span>
                  <span className="text-meta text-ink-faint">{each.scope === 'machine' ? t('files.view.scope.machine') : each.scope}</span>
                </div>
                {[true, false].map((whole) => {
                  const patterns = (whole ? each.whole : each.files).join(', ');
                  return (
                    <label key={String(whole)} className="flex min-w-0 items-center gap-2">
                      <input
                        type="radio" name={name} checked={chosen[name] === whole} disabled={!onPress}
                        onChange={() => setChosen((now) => ({ ...now, [name]: whole }))}
                      />
                      <span className="min-w-0 [overflow-wrap:anywhere]">
                        {whole ? t('files.view.keptWhole', { patterns }) : patterns ? t('files.view.keptFiles', { patterns }) : t('files.view.keptNone')}
                      </span>
                    </label>
                  );
                })}
              </li>
            );
          })}
        </ul>
        {onPress && (
          <div>
            <Button
              disabled={!ready}
              onClick={() => onPress({ act: 'settle', wordings: kept.map((each) => ({ scope: each.scope, path: each.path, whole: chosen[key(each.scope, each.path)] === true })) })}
            >
              {t('files.view.keptSettle')}
            </Button>
          </div>
        )}
      </div>
    </PageSection>
  );
}
