import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { App } from './App';
import { PageBoundary } from './PageBoundary';
import { SecondaryWindowRoot } from './SecondaryWindowRoot';
import { WorkspaceScopeProvider } from '../data/scope';
import type { SecondaryWindow } from '../work/window';

/**
 * The page as `main.tsx` mounts it, in both roots: the application's main window, or a secondary window (SURF8), which is a ROUTE
 * into the same bundle — the same bytes, the same components, a different root — so a monitor on a second screen costs the
 * components it already has instead of a second frontend to keep in step.
 *
 * The error screen is outermost (PAGELOOP1), outside the query client and every provider, so a failure in any of them is caught as
 * one in a view is; a render that threw anywhere left the window blank.
 */
export function PageRoot({ client, window: which }: { client: QueryClient; window: SecondaryWindow | null }) {
  return (
    <PageBoundary>
      <QueryClientProvider client={client}>
        <Tooltip.Provider delayDuration={300}>
          {/* The workspace scope wraps the whole shell (WSP5): one scope per query, everywhere —
              including a secondary window, which follows the window that opened it (MAINWINDOWS3). A
              monitor scoped to a different circle than the window that opened it would be the worst
              kind of wrong. Each main window holds its own. */}
          <WorkspaceScopeProvider>
            {which ? <SecondaryWindowRoot window={which} /> : <App />}
          </WorkspaceScopeProvider>
        </Tooltip.Provider>
      </QueryClientProvider>
    </PageBoundary>
  );
}
