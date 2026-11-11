import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import '../locales/i18n';
import type { PluginEntry } from '../bridge/plugins';
import { systemsOffered } from './ProjectManage';
import { unguardedSystems } from './ProjectsView';
import { SystemChoiceField } from './SystemChoiceField';

// A system added as a department: one an installed adapter declares, named by its id unless the person names it.

const plugin = (over: Partial<PluginEntry>): PluginEntry =>
  ({ id: 'stand-in-system', name: 'Stand-in system', enabled: true, problem: null, systems: [{ id: 'stand-in', name: 'The stand-in store' }], ...over }) as PluginEntry;

describe('a system on the add', () => {
  it('offers the systems the plugins switched on and sound declare, each as <plugin>/<system>', () => {
    expect(systemsOffered([plugin({}), plugin({ id: 'off', enabled: false }), plugin({ id: 'odd', problem: 'refused' })]))
      .toEqual([{ system: 'stand-in-system/stand-in', name: 'The stand-in store', plugin: 'Stand-in system' }]);
  });

  it('names the systems whose adapter declares no precondition, the departments the list says are unguarded', () => {
    const both = plugin({ systems: [{ id: 'stand-in', name: 'Guarded', preconditions: 'etag' }, { id: 'stand-in-unguarded', name: 'Bare', preconditions: 'none' }] });
    expect([...unguardedSystems([both, plugin({ id: 'other' })])]).toEqual(['stand-in-system/stand-in-unguarded']);
  });

  it('hands back the system chosen and the name typed, and says what a system department is', () => {
    const onSystem = vi.fn();
    const onName = vi.fn();
    render(<SystemChoiceField offered={systemsOffered([plugin({})])} system="" name="" onSystem={onSystem} onName={onName} />);

    fireEvent.change(screen.getByLabelText('System'), { target: { value: 'stand-in-system/stand-in' } });
    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'store' } });

    expect(onSystem).toHaveBeenCalledWith('stand-in-system/stand-in');
    expect(onName).toHaveBeenCalledWith('store');
    expect(screen.getByText(/applied after the person accepts it/)).toBeTruthy();
  });

  it('says how to get one where no plugin adapts a system', () => {
    render(<SystemChoiceField offered={[]} system="" name="" onSystem={vi.fn()} onName={vi.fn()} />);
    expect(screen.getByText(/No plugin installed adapts a system/)).toBeTruthy();
  });
});
