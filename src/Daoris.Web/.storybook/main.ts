import type { StorybookConfig } from '@storybook/react-vite';

// The design tool (D42): stories import the SHIPPED components and the SHIPPED tokens, so a
// divergence between design and product is a build error, not a discovery.
const config: StorybookConfig = {
  framework: '@storybook/react-vite',
  stories: ['../src/**/*.stories.tsx'],
};

export default config;
