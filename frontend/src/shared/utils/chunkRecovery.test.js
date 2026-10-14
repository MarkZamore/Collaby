import { describe, expect, it } from 'vitest';

import {
  CHUNK_RECOVERY_STORAGE_KEY,
  isDynamicImportError,
  rememberChunkRecoveryAttempt,
  shouldTriggerChunkRecovery,
} from './chunkRecovery';

const createStorage = () => {
  const values = new Map();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
  };
};

describe('chunk recovery helpers', () => {
  it('recognizes dynamic import and chunk loading failures', () => {
    expect(isDynamicImportError('Failed to fetch dynamically imported module')).toBe(true);
    expect(isDynamicImportError(new Error('Failed to load module script'))).toBe(true);
    expect(isDynamicImportError({ message: 'ChunkLoadError: Loading chunk 15 failed.' })).toBe(true);
    expect(isDynamicImportError('Unexpected token < in JSON')).toBe(false);
  });

  it('allows one recovery attempt per route inside the cooldown window', () => {
    const storage = createStorage();
    const route = '/admin/users';

    expect(shouldTriggerChunkRecovery(storage, route, 1_000)).toBe(true);

    rememberChunkRecoveryAttempt(storage, route, 1_000);

    expect(shouldTriggerChunkRecovery(storage, route, 20_000)).toBe(false);
    expect(shouldTriggerChunkRecovery(storage, '/admin/workspaces', 20_000)).toBe(true);
    expect(shouldTriggerChunkRecovery(storage, route, 70_500)).toBe(true);
  });

  it('stores the recovery timestamp and route key', () => {
    const storage = createStorage();

    rememberChunkRecoveryAttempt(storage, '/dashboard', 42_000);

    expect(JSON.parse(storage.getItem(CHUNK_RECOVERY_STORAGE_KEY))).toEqual({
      locationKey: '/dashboard',
      timestamp: 42_000,
    });
  });
});
