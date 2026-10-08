import { defineConfig } from 'vitest/config';

// Keep the base suite predictable on small CI runners and developer workstations.
export default defineConfig({
  test: {
    pool: 'threads',
    maxWorkers: 1,
  },
});
