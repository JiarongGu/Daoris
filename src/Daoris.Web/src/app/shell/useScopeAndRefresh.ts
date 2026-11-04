import { useEffect } from 'react';
import { useWorkspaceHoldings, useWorkspaces } from '../../data/queries';
import { useScope } from '../../data/scope';
import { failure } from '../../design/ui';
import { useWindowTitle } from '../windowChrome';
import { figure } from '../../locales/format';
import { type TFunction } from 'i18next';
import { type UseMutationResult } from '@tanstack/react-query';
import { type RefreshResponse } from '../../data/generated/service';
import { type Notify } from '../../design/atoms/Toasts';

export function useScopeAndRefresh({ t, refresh, notify }: {
  t: TFunction<"translation", undefined>;
  refresh: UseMutationResult<RefreshResponse, Error, void, unknown>;
  notify: Notify;
}) {
  // The scope (WSP5): which circle this window is looking at. The roster comes from the registry
  // unscoped — the one reader that must see every workspace — and a remembered choice the deployment
  // no longer holds falls back to every, out loud (the switcher then says so), rather than scoping
  // every query to a circle nobody is in any more.
  const scope = useScope();
  // Each main window holds its own (MAINWINDOWS3), so the title says which, where two would otherwise read alike.
  useWindowTitle(scope.workspace);
  const workspaces = useWorkspaces();
  const holdings = useWorkspaceHoldings();
  // 🔴 The scope in words, whatever the machine holds (D75 §3): the one chosen, the one there is,
  // every one among several, or none yet. With none registered the top bar and the status bar both
  // said "every workspace", of nothing, and the switcher that would have said more is absent below
  // two by WSP5's rule. That rule still hides the control; it no longer hides the fact.
  const scopeNamed = scope.workspace ?? (workspaces.data === undefined
    ? null
    : workspaces.data.length === 0
      ? t('scope.none')
      : workspaces.data.length === 1
        ? workspaces.data[0]!
        : t('scope.every', { count: workspaces.data.length }));
  useEffect(() => {
    if (workspaces.data && scope.workspace && !workspaces.data.includes(scope.workspace)) {
      scope.setWorkspace(null);
    }
  }, [workspaces.data, scope]);

  const onRefresh = () => refresh.mutate(undefined, {
    onSuccess: (report) => {
      // What the semantic half embedded is said beside the count, in the one notice (SEM3b, D123); a
      // refresh it did not run in says nothing of it, since absent is never zero.
      const counted = t('sidebar.refreshed', report);
      const made = report.embedded;
      notify(made
        ? t('sidebar.join', {
          first: counted,
          second: t('sidebar.embedded', {
            count: made.split,
            split: figure(made.split),
            entries: figure(made.entries),
            pieces: figure(made.pieces),
            window: figure(made.window),
          }),
        })
        : counted);
      // A registered checkout that was not where the registry says contributed nothing, and the count
      // still looks healthy — so it is named, as the failure it is (D48 §3; REV3).
      if (report.absent?.length) notify(t('sidebar.absent', { names: report.absent.join(', ') }), 'error');
      // The semantic half's failure is the service's own sentence — dropped nowhere (D24).
      if (report.semanticError) notify(report.semanticError, 'error');
    },
    onError: failure(notify),
  });
  return { scope, workspaces, holdings, onRefresh, scopeNamed };
}
