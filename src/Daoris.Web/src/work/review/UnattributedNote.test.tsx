import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { UnattributedNote } from './UnattributedNote';

// A turn in place whose change set holds paths no tool of its run named says them above its files, in each language; none says nothing.

describe('the paths changed while a session ran', () => {
  it('names each path no tool of the turn named, in English and in Chinese', async () => {
    const { container, rerender } = render(<UnattributedNote paths={['plan.md', 'notes/b.md']} />);
    expect(container.textContent).toContain('Changed while this session ran, by none of its tools');
    expect(container.textContent).toContain('plan.md');
    expect(container.textContent).toContain('notes/b.md');

    await i18n.changeLanguage('zh');
    rerender(<UnattributedNote paths={['plan.md']} />);
    expect(container.textContent).toContain('本会话运行期间');
    await i18n.changeLanguage('en');
  });

  it('draws nothing where every path was its own', () => {
    const { container } = render(<UnattributedNote paths={null} />);
    expect(container.textContent).toBe('');
  });
});
