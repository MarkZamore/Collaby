import { describe, expect, it } from 'vitest';

import { detectBrowserLanguage, getInitialLanguage, normalizeLanguage } from './config';

describe('i18n language helpers', () => {
  it('normalizes region-specific locales to supported language codes', () => {
    expect(normalizeLanguage('ru-RU')).toBe('ru');
    expect(normalizeLanguage('kk_KZ')).toBe('kk');
    expect(normalizeLanguage('en-US')).toBe('en');
  });

  it('falls back to en for unsupported locales', () => {
    expect(normalizeLanguage('es-ES')).toBe('en');
    expect(normalizeLanguage(null)).toBe('en');
  });

  it('detects the browser language from navigator.languages', () => {
    expect(detectBrowserLanguage({ languages: ['ru-RU', 'en-US'] })).toBe('ru');
    expect(detectBrowserLanguage({ languages: ['kk-KZ'] })).toBe('kk');
  });

  it('prefers stored UI preferences over browser locale', () => {
    const storage = {
      getItem: (key) => {
        if (key !== 'collaby-ui-preferences') {
          return null;
        }

        return JSON.stringify({ language: 'kk' });
      },
    };

    expect(getInitialLanguage({
      storage,
      browserNavigator: { languages: ['ru-RU', 'en-US'] },
    })).toBe('kk');
  });
});
