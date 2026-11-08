import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../locales/i18n';
import { pluginGroups } from './catalog';
import { PluginList } from './PluginList';

describe('the plugin list', () => {
  it('shows a long plugin name whole, wrapped, never cut with an ellipsis', () => {
    render(
      <Tooltip.Provider>
        <PluginList
          groups={pluginGroups([{
            id: 'acme.gate', name: 'Azure DevOps pull request', version: '2.0.0', description: 'A performed step.',
            enabled: true, problem: null, harnesses: [], points: [], running: true, folder: 'f', data: 'd',
          }], [])}
          chosen={null} onChoose={vi.fn()} onInstall={vi.fn()}
        />
      </Tooltip.Provider>,
    );

    expect(screen.getByText('Azure DevOps pull request').className).not.toContain('truncate');
  });
});
