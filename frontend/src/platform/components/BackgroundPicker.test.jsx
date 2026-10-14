import '@testing-library/jest-dom/vitest';
import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import BackgroundPicker from './BackgroundPicker';

const platformState = vi.hoisted(() => ({
  boardBackground: {
    type: 'image',
    value: 'https://images.unsplash.com/photo-1507400492013-162706c8c05e?w=1920&q=80',
  },
  defaultBoardBackground: {
    type: 'image',
    value: 'https://images.unsplash.com/photo-1507400492013-162706c8c05e?w=1920&q=80',
  },
  projectBoardBackgroundOverride: null,
  setBoardBackground: vi.fn(),
  clearProjectBoardBackground: vi.fn(),
  clearDefaultBoardBackground: vi.fn(),
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key) => {
      const dictionary = {
        'backgroundPicker.title': 'Board background',
        'backgroundPicker.reset': 'Reset',
        'backgroundPicker.applyForAll': 'Apply this board background for all users',
        'backgroundPicker.upload': 'Upload your own background',
        'backgroundPicker.searchPlaceholder': 'Search images...',
        'backgroundPicker.search': 'Search',
        'backgroundPicker.examples': 'Examples:',
        'backgroundPicker.searchResults': 'Search results',
        'backgroundPicker.loadMore': 'Load more',
        'backgroundPicker.fromUnsplash': 'From',
        'backgroundPicker.searching': 'Searching...',
        'backgroundPicker.solidColors': 'Solid colors',
        'backgroundPicker.images': 'Images',
      };

      return dictionary[key] ?? key;
    },
  }),
}));

vi.mock('../../shared/context/AuthContext', () => ({
  useAuth: () => ({
    accessToken: 'test-token',
  }),
}));

vi.mock('../../shared/utils/workspaceApi', () => ({
  searchBackgroundImages: vi.fn(),
}));

vi.mock('../context/PlatformContext', () => ({
  usePlatform: () => platformState,
}));

describe('BackgroundPicker', () => {
  beforeEach(() => {
    platformState.projectBoardBackgroundOverride = null;
    platformState.setBoardBackground.mockReset();
    platformState.clearProjectBoardBackground.mockReset();
    platformState.clearDefaultBoardBackground.mockReset();
  });

  it('applies board backgrounds to the current project by default in board settings', () => {
    render(<BackgroundPicker showForAllCheckbox />);

    fireEvent.click(screen.getByAltText('Mountains'));

    expect(platformState.setBoardBackground).toHaveBeenCalledWith(
      {
        type: 'image',
        value: 'https://images.unsplash.com/photo-1519681393784-d120267933ba?w=1920&q=80',
      },
      { forAll: false },
    );
  });

  it('hides project reset when only the account default background is active', () => {
    render(<BackgroundPicker showForAllCheckbox />);

    expect(screen.queryByRole('button', { name: 'Reset' })).not.toBeInTheDocument();
  });
});
