import { useTranslation } from 'react-i18next';
import { usePlaceCast, useSetPlaceCast } from '../../bridge/repositories/cast';
import { PlaceCastSetting } from '../../settings/place/PlaceCast';
import { shellWord } from '../../lib/shellWord';

/** A place's cast in its Settings, wired: read by `PLACE_CAST`, each choice `SET_PLACE_CAST` with the reason beside it. */
export function PlaceCastSection({ workspace, repository }: { workspace: string; repository?: string }) {
  const { t } = useTranslation();
  const cast = usePlaceCast(workspace, repository ?? null);
  const set = useSetPlaceCast();
  if (!cast.data) return null;
  const twin = repository
    ? t('settings.cast.twin.repository', { workspace: shellWord(workspace, '<workspace>'), repository: shellWord(repository, '<repository>') })
    : t('settings.cast.twin.workspace', { workspace: shellWord(workspace, '<workspace>') });
  return (
    <PlaceCastSetting name={repository ?? workspace} twin={twin} view={cast.data} busy={set.isPending}
      onCast={(change) => set.mutate({ ...change, workspace, repository: repository ?? null })} />
  );
}
