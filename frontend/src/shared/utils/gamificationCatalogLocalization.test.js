import { describe, expect, it } from 'vitest';
import { localizeShopCategory, localizeShopItem } from './gamificationCatalogLocalization';

describe('gamificationCatalogLocalization', () => {
  const translations = {
    'shop.categories.badges': 'Значки профиля',
    'seededCatalog.shopItems.earlyAccessBadge.name': 'Бейдж раннего доступа',
    'seededCatalog.shopItems.earlyAccessBadge.description': 'Награда для ранних участников',
  };

  const t = (key, options = {}) => translations[key] ?? options.defaultValue ?? key;

  it('localizes uppercase shop categories through shared helper', () => {
    expect(localizeShopCategory('BADGES', t)).toBe('Значки профиля');
  });

  it('localizes consumable categories through shared helper', () => {
    expect(
      localizeShopCategory(
        'consumables',
        (key, options = {}) => (key === 'shop.categories.consumables' ? 'Расходники' : options.defaultValue ?? key),
      ),
    ).toBe('Расходники');
  });

  it('adds localized categoryLabel to non-seeded shop items', () => {
    expect(
      localizeShopItem(
        {
          id: 'custom-item',
          category: 'BADGES',
        },
        t,
      ),
    ).toMatchObject({
      id: 'custom-item',
      category: 'BADGES',
      categoryLabel: 'Значки профиля',
    });
  });

  it('adds localized categoryLabel to seeded shop items', () => {
    expect(
      localizeShopItem(
        {
          id: 'a0000001-0000-0000-0000-000000000005',
          category: 'BADGES',
        },
        t,
      ),
    ).toMatchObject({
      category: 'BADGES',
      categoryLabel: 'Значки профиля',
      name: 'Бейдж раннего доступа',
      description: 'Награда для ранних участников',
    });
  });
});
