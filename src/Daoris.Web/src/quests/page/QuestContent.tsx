import type * as React from 'react';
import { api, fileKept, type Quest } from '../../data/api';
import { isImage } from '../../compose/attachments';
import { size } from '../../locales/format';
import { ExternalLink } from '../../design/links';
import { Icon } from '../../design/ui';
import { QuestRequirements } from '../Requirements';
import { PageSection } from '../../work/frame/ViewMain';
import { type QuestRequirementResponse } from '../../data/generated/service';
import { type TFunction } from 'i18next';

export function QuestContent({ body, request, held, requirements, quest, t }: {
  body: string;
  request: React.RefObject<HTMLParagraphElement | null>;
  held: boolean;
  requirements: QuestRequirementResponse[];
  quest: Quest;
  t: TFunction<"translation", undefined>;
}) {
  return (
    <>
{body && (
        <p
          ref={request}
          id="quest-request"
          tabIndex={-1}
          className="m-0 whitespace-pre-wrap text-body leading-relaxed outline-none"
        >
          {body}
        </p>
      )}

      {!held && requirements.length > 0 && (
        /* What the person required (DRIFT1c, D133 §3), in their words: the measure, where the body above is the intake's
           reading of the ask. Then how its done answered each, and when a departure was accepted. */
        <QuestRequirements
          className="mt-4"
          id={quest.id}
          requirements={requirements}
          answers={quest.answers}
          status={quest.status}
          accepted={quest.accepted}
        />
      )}

      {(quest.links?.length ?? 0) > 0 && (
        /* The asker's addresses, as links — the service accepted only http and https, so each is an address
           and nothing that would run. They open where the person chose, their browser or Daoris's (BRW7). */
        <div className="mt-4">
          <PageSection title={t('quests.detail.links')}>
            <ul className="m-0 grid list-none gap-1 p-0">
              {quest.links!.map((link) => (
                <li key={link} className="min-w-0">
                  <ExternalLink
                    href={link}
                    className="inline-flex max-w-full items-center gap-1.5 text-body text-accent underline-offset-2 hover:underline"
                  >
                    <Icon name="link" size={12} />
                    <span className="truncate">{link}</span>
                  </ExternalLink>
                </li>
              ))}
            </ul>
          </PageSection>
        </div>
      )}

      {(quest.attachments?.length ?? 0) > 0 && (
        /* The files, by name (D65 §2). One this machine keeps opens through the host's own route — never by its
           path, which a page does not show — and a picture is shown as one. One named on the record and kept
           elsewhere says so, rather than looking like a broken link. Kept here is a path pulled or `kept` pushed (PUSH1d3). */
        <div className="mt-4">
          <PageSection title={t('quests.detail.files')}>
            <ul className="m-0 grid list-none gap-2 p-0">
              {quest.attachments!.map((file) => (
                <li key={file.sha256} className="min-w-0 text-body">
                  {fileKept(file) ? (
                    <ExternalLink href={api.attachmentUrl(quest.id, file.sha256)} className="group inline-grid max-w-full gap-1.5 text-accent">
                      <span className="inline-flex min-w-0 items-center gap-1.5">
                        <Icon name="attach" size={12} />
                        <span className="truncate underline-offset-2 group-hover:underline">{file.name}</span>
                        <span className="shrink-0 font-mono text-meta text-ink-faint">{size(file.bytes)}</span>
                      </span>
                      {isImage(file.name) && (
                        <img
                          src={api.attachmentUrl(quest.id, file.sha256)} alt={file.name}
                          className="max-h-60 max-w-full rounded-control border border-line object-contain"
                        />
                      )}
                    </ExternalLink>
                  ) : (
                    <span className="inline-flex min-w-0 flex-wrap items-center gap-1.5 text-ink-soft">
                      <Icon name="attach" size={12} />
                      <span className="truncate">{file.name}</span>
                      <span className="font-mono text-meta text-ink-faint">{size(file.bytes)} · {t('quests.detail.fileElsewhere')}</span>
                    </span>
                  )}
                </li>
              ))}
            </ul>
          </PageSection>
        </div>
      )}

      {/* How its conflicts ended (SYNCMERGE1c2, D178 point 7): history, so below what it carries, with nothing to press. */}
    </>
  );
}
