import { fireEvent, render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { PluginIcon } from './PluginIcon';

// A plugin's icon (PLUGUI2, D140 §3): its own, drawn as an image from the bytes it was handed, or its monogram.

const SVG = `data:image/svg+xml;base64,${btoa('<svg xmlns="http://www.w3.org/2000/svg"/>')}`;

describe('PluginIcon', () => {
  it('draws a declared icon as an image of its own bytes, beside the name it decorates', () => {
    const { container } = render(<PluginIcon id="acme.gate" name="Acme gate" icon={SVG} />);

    const image = container.querySelector('img')!;
    expect(image.getAttribute('src')).toBe(SVG);
    // Decoration: the plugin's name is beside it, so it is no second name for a reader.
    expect(image.getAttribute('alt')).toBe('');
    expect(image.getAttribute('aria-hidden')).toBe('true');
    // 🔴 Never markup in the page's document: an SVG is an image, never an inlined <svg>.
    expect(container.querySelector('svg')).toBeNull();
  });

  it('draws the monogram where the declared icon will not load', () => {
    const { container } = render(<PluginIcon id="acme.gate" name="Acme gate" icon={SVG} />);

    fireEvent.error(container.querySelector('img')!);

    expect(container.querySelector('img')).toBeNull();
    expect(container.textContent).toBe('A');
  });

  it('draws the monogram for anything but an icon\'s bytes, and never shows the path it was handed', () => {
    const path = 'C:/somewhere/data/plugins/acme.gate/icon.svg';
    const { container } = render(<PluginIcon id="acme.gate" name="Acme gate" icon={path} />);

    expect(container.querySelector('img')).toBeNull();
    expect(container.innerHTML).not.toContain('somewhere');
    expect(container.textContent).toBe('A');
  });

  it('is the name\'s first character on the id\'s hue, the same on every render', () => {
    const first = render(<PluginIcon id="acme.gate" name="Acme gate" />).container.firstElementChild!;
    const again = render(<PluginIcon id="acme.gate" name="Acme gate" />).container.firstElementChild!;

    expect(first.getAttribute('data-hue')).toBe('plum');
    expect(first.className).toContain('text-ident-plum');
    expect(first.className).toBe(again.className);
    expect(first.textContent).toBe(again.textContent);
  });

  it('wears a 中文 name\'s first character, as declared', () => {
    const { container } = render(<PluginIcon id="acme.quiet" name="夜间暂停委托" />);
    expect(container.textContent).toBe('夜');
    expect(container.firstElementChild!.getAttribute('data-hue')).toBe('teal');
  });

  it('is drawn at the strip\'s, a row\'s and a page\'s size', () => {
    const size = (at: 'strip' | 'row' | 'page') =>
      render(<PluginIcon id="acme.gate" name="Acme gate" size={at} />).container.firstElementChild!.className;
    expect(size('strip')).toContain('size-5');
    expect(size('row')).toContain('size-8');
    expect(size('page')).toContain('size-12');
  });

  it('is drawn faint for a plugin that is off, whether its own or its monogram', () => {
    expect(render(<PluginIcon id="acme.gate" name="Acme gate" dimmed />).container.firstElementChild!.className).toContain('opacity-55');
    expect(render(<PluginIcon id="acme.gate" name="Acme gate" icon={SVG} dimmed />).container.querySelector('img')!.className).toContain('opacity-55');
  });
});
