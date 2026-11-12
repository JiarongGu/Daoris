import { afterEach, describe, expect, it } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import i18n from '../../../locales/i18n';
import { RecordFileView } from './RecordFileView';

// A file of a folder's or a copy's review, once its row is opened: a Markdown file is read rendered as handed back, with its changes
// one press away; any other file is its changes, line by line.

describe('a record file once its row is opened', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('renders a Markdown file at its after version, and shows its changes on the press', () => {
    render(<RecordFileView path="notes/summary.md" before={'# Old\n'} after={'# Summary\n\n- one\n- two\n'} />);

    expect(screen.getByRole('heading', { name: 'Summary' })).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(2);

    fireEvent.click(screen.getByRole('radio', { name: 'Changes' }));
    expect(screen.queryByRole('heading', { name: 'Summary' })).toBeNull();
    expect(screen.getByText('# Old')).toBeInTheDocument();
  });

  it('draws a Markdown file that was removed as its changes, since it has no rendered version', () => {
    render(<RecordFileView path="notes/gone.markdown" before={'# Gone\n'} after={null} />);

    expect(screen.queryByRole('radio', { name: 'Rendered' })).toBeNull();
    expect(screen.getByText('# Gone')).toBeInTheDocument();
  });

  it('draws any other file as its changes alone', () => {
    render(<RecordFileView path="src/flag.ts" before={'a\n'} after={'b\n'} />);

    expect(screen.queryByRole('radio', { name: 'Changes' })).toBeNull();
    expect(screen.getByText('a')).toBeInTheDocument();
    expect(screen.getByText('b')).toBeInTheDocument();
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<RecordFileView path="notes/summary.md" before={null} after={'# 摘要\n'} />);

    expect(screen.getByRole('radio', { name: '改动' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '摘要' })).toBeInTheDocument();
  });
});
