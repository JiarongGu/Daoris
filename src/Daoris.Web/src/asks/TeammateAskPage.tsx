import { useTranslation } from 'react-i18next';
import { ago, stamp } from '../locales/format';
import { Pill } from '../design/ui';
import type { Channel, ElsewhereAsk } from '../work/channel';
import { PageHead, PageSection, ViewMain } from '../work/frame/ViewMain';
import { ChannelSection } from './ChannelSection';

/**
 * **A teammate's ask's page** (SESSONE2h; the co-working design §2.4, D167's SESSONE2g note): an ask held on another machine, of
 * which this one has only the channel messages the sync mirrored. There is no ask record here, so the page has no request, no
 * proposal, no quests and no work, and offers none of an ask's acts (publish, close, delete, pause): it names the ask by its id,
 * says it is a teammate's and held on their machine, gives the circle, the machines its messages came from and the latest, and
 * reads the channel with no composer, since a post to it is refused (`held-elsewhere`) until the co-working design says how its
 * holder takes one (SESSONE2i).
 *
 * @remarks
 * A molecule over `ChannelSection`: the channel arrives as the shell answered it (null while it is read). The unread count is
 * against the person's mark, which the service keeps on a teammate's ask its mirror holds (NOTIFY2g): reading the page moves it
 * through the newest item once (`onRead`), as an ask's own page does, so its item leaves *Messages*. Absent, no mark moves.
 */
export function TeammateAskPage({ ask, channel, onRead }: {
  ask: ElsewhereAsk;
  channel: Channel | null;
  /** The person's mark moved through the newest item shown (`CHANNEL_MARK`), once what was unread is on the page. */
  onRead?: (through: string) => void;
}) {
  const { t } = useTranslation();
  const head = (
    <PageHead
      lead={<Pill tone="neutral">{t('asks.elsewhere.pill')}</Pill>}
      title={t('asks.elsewhere.title', { id: ask.id })}
      facts={[
        ...(ask.workspace ? [t('asks.card.workspace', { workspace: ask.workspace })] : []),
        t('asks.elsewhere.latest', { ago: ago(ask.latest) }),
      ]}
    />
  );
  return (
    <ViewMain header={head}>
      <p className="m-0 mb-4 text-body leading-relaxed">{t('asks.elsewhere.note')}</p>
      <dl className="m-0 mb-4 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1.5 text-body">
        <dt className="text-ink-faint">{t('work.head.id')}</dt><dd className="m-0 font-mono text-meta">#{ask.id}</dd>
        {ask.workspace && <><dt className="text-ink-faint">{t('asks.record.circle')}</dt><dd className="m-0">{ask.workspace}</dd></>}
        {ask.origins.length > 0 && (
          <><dt className="text-ink-faint">{t('asks.elsewhere.from')}</dt><dd className="m-0 wrap-anywhere">{ask.origins.join(', ')}</dd></>
        )}
        <dt className="text-ink-faint">{t('asks.elsewhere.last')}</dt>
        <dd className="m-0">{stamp(ask.latest)} · {ago(ask.latest)}</dd>
      </dl>
      <PageSection title={t('asks.channel.title')}>
        <ChannelSection channel={channel} readOnly onPost={() => {}} onRead={onRead} />
      </PageSection>
    </ViewMain>
  );
}
