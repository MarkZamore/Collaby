import React from 'react';
import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import ChunkRecoveryBoundary from './ChunkRecoveryBoundary';

const ThrowChunkError = () => {
  throw new Error('Failed to fetch dynamically imported module');
};

describe('ChunkRecoveryBoundary', () => {
  afterEach(() => {
    window.sessionStorage.clear();
    vi.restoreAllMocks();
  });

  it('renders fallback and triggers one recovery attempt for chunk errors', () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    const onRecover = vi.fn();

    render(
      <ChunkRecoveryBoundary fallback={<div>Recovering...</div>} onRecover={onRecover}>
        <ThrowChunkError />
      </ChunkRecoveryBoundary>,
    );

    expect(screen.getByText('Recovering...')).toBeInTheDocument();
    expect(onRecover).toHaveBeenCalledTimes(1);
    expect(consoleError).toHaveBeenCalled();
  });
});
