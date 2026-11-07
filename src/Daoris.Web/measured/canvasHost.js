// The check in the installed window, for the steward:
//  node tools/desktop.mjs eval --file src/Daoris.Web/measured/canvasHost.js
// with a workflow's chart in its canvas on screen (a repository's Workflow, its editor at 80rem). It reads, in the host itself, what the
// browser checks cannot: the reader's one stop and list, a Ctrl+wheel taken by the canvas and the window's zoom left where it stands, a plain
// wheel at the top edge left to the page, and a focus walked to the last step with every scroll at 0 and the step revealed. The host's own
// wheel, pinch and Narrator are still a person's.
(async () => {
  const region = document.querySelector('[role="region"][data-flow-pane]');
  if (!region || !region.querySelector('[data-canvas-world]')) return { ok: false, why: 'no workflow chart in its canvas on screen' };
  const world = region.querySelector('[data-canvas-world]');
  const frame = () => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done)));
  const scale = () => Number(world.getAttribute('data-canvas-scale'));
  const box = region.getBoundingClientRect();
  const at = { clientX: box.left + box.width / 2, clientY: box.top + Math.min(80, box.height / 2) };
  const send = (init) => {
    const event = new WheelEvent('wheel', { bubbles: true, cancelable: true, ...at, ...init });
    (document.elementFromPoint(at.clientX, at.clientY) ?? region).dispatchEvent(event);
    return event.defaultPrevented;
  };

  const reader = {
    roledescription: region.getAttribute('aria-roledescription'),
    stops: [...world.querySelectorAll('[tabindex="0"]')].length,
    steps: world.querySelectorAll('[role="listitem"]').length,
  };

  const rootBefore = getComputedStyle(document.documentElement).fontSize;
  const kBefore = scale();
  const ctrlTaken = send({ ctrlKey: true, deltaY: -100 });
  await frame();
  const wheel = {
    ctrlTaken, canvasZoomed: scale() !== kBefore, windowZoomKept: getComputedStyle(document.documentElement).fontSize === rootBefore,
  };
  send({ ctrlKey: true, deltaY: 100 });
  await frame();
  // At the top edge a plain wheel up is the page's.
  for (let turn = 0; turn < 40; turn += 1) send({ deltaY: -200 });
  await frame();
  wheel.upAtTopLeftToPage = !send({ deltaY: -60 });

  const nodes = [...world.querySelectorAll('[data-flow-node]')];
  nodes[0].focus({ preventScroll: true });
  for (let press = 0; press < nodes.length + 2; press += 1) {
    document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }));
    await frame();
  }
  const focused = document.activeElement;
  let scrolled = 0;
  for (let element = focused; element; element = element.parentElement) if (element.scrollTop || element.scrollLeft) scrolled += 1;
  const area = region.getBoundingClientRect();
  const node = focused.getBoundingClientRect();
  const walk = {
    reachedLast: focused === nodes.at(-1), ancestorsScrolled: scrolled,
    revealed: node.top >= area.top - 1 && node.bottom <= area.bottom + 1 && node.left >= area.left - 1 && node.right <= area.right + 1,
  };
  const ok = reader.stops === 1 && wheel.ctrlTaken && wheel.canvasZoomed && wheel.windowZoomKept && wheel.upAtTopLeftToPage
    && walk.reachedLast && walk.ancestorsScrolled === 0 && walk.revealed;
  return { ok, reader, wheel, walk, renderer: navigator.userAgent };
})()
