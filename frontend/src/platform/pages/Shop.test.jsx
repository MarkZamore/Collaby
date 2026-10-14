import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import Shop from './Shop';
import i18n from '../../shared/i18n/config';

const gamificationState = vi.hoisted(() => ({
  value: null,
}));

vi.mock('../../shared/context/GamificationContext', () => ({
  useGamification: () => gamificationState.value,
}));

describe('Shop purchase availability', () => {
  it('disables award-only items instead of calling purchase', async () => {
    const purchaseItem = vi.fn();

    gamificationState.value = {
      coins: 500,
      inventory: [],
      purchaseItem,
      accessToken: 'token',
      api: {
        getShopItems: vi.fn().mockResolvedValue([
          {
            id: 'a0000001-0000-0000-0000-000000000010',
            name: 'Bronze',
            category: 'badges',
            price: 0,
            description: 'Award-only item',
            isPurchasable: false,
          },
        ]),
      },
    };

    render(<Shop />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: i18n.t('platform:shop.awardOnly') })).toBeDisabled();
    });

    fireEvent.click(screen.getByRole('button', { name: i18n.t('platform:shop.awardOnly') }));

    expect(purchaseItem).not.toHaveBeenCalled();
  });
});
