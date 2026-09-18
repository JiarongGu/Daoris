// The same tokens and the same catalogs the product loads — the stories run on the shipped theme.
// Dark mode follows the OS (`prefers-color-scheme`), exactly as the product does.
import '../src/tokens.css';
import '../src/i18n';

const preview = {
  parameters: {
    layout: 'padded',
  },
};

export default preview;
