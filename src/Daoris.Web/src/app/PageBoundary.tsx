import { Component, type ErrorInfo, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { getBridge } from '@shenora/react';
import { Button, Icon, Inline, Prose } from '../design/ui';
import { errorMessage, reportPageStopped } from '../bridge/log';
import { liftSplash } from './splash';

// The page's last stand (PAGELOOP1). With no boundary, a render that threw anywhere unmounted the whole tree, and the window was
// blank: on the install React's update-depth guard (#185) did it twice, seconds before a session ended. The window has no reload of
// its own and a restart of Daoris ends every running session, so the page catches it here, says so in the window's words, and
// offers a reload of the page alone. Outside every provider: a failure inside the query client, the scope or the tooltips is caught
// too, so nothing here reads a query, a scope or a bridge hook, only the bridge itself where it asks whether a shell is here.

const HEADING = 'page-stopped';

/** Whether a shell is here, and so whether the machine log heard the failure. A broken bridge is no shell. */
function shellHere(): boolean {
  try {
    return getBridge().isAvailable;
  } catch {
    return false;
  }
}

/** The page reloaded alone: the window, the driver and its sessions keep running behind it (the kit's handshake starts again). */
const reloadPage = () => window.location.reload();

/**
 * The screen a stopped page shows: that it stopped, what broke in one line, that nothing behind it did, and a reload. The message is
 * the error's own, shown as written: React's in a production build, which names its error by number.
 */
export function PageStopped({ message, logged, onReload }: {
  /** The error's message, shown on one line; empty says none. */
  message: string;
  /** Whether the machine log heard it: a browser has no log, and is not told of one. */
  logged: boolean;
  onReload: () => void;
}) {
  const { t } = useTranslation();
  return (
    <main aria-labelledby={HEADING} className="grid min-h-screen place-items-center bg-page px-6 py-10 text-ink">
      {/* A reading column centred in the window, as the start prompt's is (D141 §4): the screen has no pane to wrap at. */}
      <div className="flex w-full max-w-[34rem] flex-col items-start gap-3">
        <span className="text-ink-danger"><Icon name="failure" size={26} /></span>
        <h1 id={HEADING} className="text-view font-[650] tracking-[-0.01em]">{t('stopped.headline')}</h1>
        {message && <p title={message} className="m-0 w-full truncate font-mono text-small text-ink-soft">{message}</p>}
        <Prose>{t('stopped.behind')}</Prose>
        {logged && <Prose><Inline text={t('stopped.logged')} /></Prose>}
        <Button variant="primary" className="mt-2" onClick={onReload}>
          <Icon name="refresh" size={14} />
          {t('stopped.reload')}
        </Button>
      </div>
    </main>
  );
}

/**
 * Catches a render that threw below it, reports it once (`page.error`, `where` `render`), lifts a splash still up, and draws
 * {@link PageStopped} in its place until the page is reloaded. `reload` is the test's seam; the window's location by default.
 */
export class PageBoundary extends Component<{ children: ReactNode; reload?: () => void }, { stopped: string | null }> {
  override state: { stopped: string | null } = { stopped: null };

  static getDerivedStateFromError(error: unknown): { stopped: string } {
    return { stopped: errorMessage(error) };
  }

  override componentDidCatch(error: unknown, _info: ErrorInfo): void {
    reportPageStopped(error);
    liftSplash();
  }

  override render(): ReactNode {
    if (this.state.stopped === null) return this.props.children;
    return <PageStopped message={this.state.stopped} logged={shellHere()} onReload={this.props.reload ?? reloadPage} />;
  }
}
