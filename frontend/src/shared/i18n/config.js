import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

import enAuth from '../locales/en/auth.json';
import enCommon from '../locales/en/common.json';
import enErrors from '../locales/en/errors.json';
import enLanding from '../locales/en/landing.json';
import enLegal from '../locales/en/legal.json';
import enPlatform from '../locales/en/platform.json';
import enProfile from '../locales/en/profile.json';

import kkAuth from '../locales/kk/auth.json';
import kkCommon from '../locales/kk/common.json';
import kkErrors from '../locales/kk/errors.json';
import kkLanding from '../locales/kk/landing.json';
import kkLegal from '../locales/kk/legal.json';
import kkPlatform from '../locales/kk/platform.json';
import kkProfile from '../locales/kk/profile.json';

import ruAuth from '../locales/ru/auth.json';
import ruCommon from '../locales/ru/common.json';
import ruErrors from '../locales/ru/errors.json';
import ruLanding from '../locales/ru/landing.json';
import ruLegal from '../locales/ru/legal.json';
import ruPlatform from '../locales/ru/platform.json';
import ruProfile from '../locales/ru/profile.json';

export const SUPPORTED_LANGUAGES = ['en', 'ru', 'kk'];
export const DEFAULT_LANGUAGE = 'en';
export const LANGUAGE_LABELS = {
  en: 'EN',
  ru: 'RU',
  kk: 'KZ',
};

export const LANGUAGE_META = {
  en: { code: 'en', label: 'EN', nativeName: 'English', flag: '🇬🇧' },
  ru: { code: 'ru', label: 'RU', nativeName: 'Русский', flag: '🇷🇺' },
  kk: { code: 'kk', label: 'KZ', nativeName: 'Қазақша', flag: '🇰🇿' },
};

export const SUPPORTED_THEMES = ['light', 'dark', 'system'];
export const LANDING_NAMESPACE_DEFAULTS = {
  en: enLanding,
  ru: ruLanding,
  kk: kkLanding,
};

const normalizeLanguageCandidate = (value) => {
  if (!value) {
    return null;
  }

  const candidate = String(value).trim().toLowerCase().replace(/_/g, '-');
  if (!candidate) {
    return null;
  }

  if (SUPPORTED_LANGUAGES.includes(candidate)) {
    return candidate;
  }

  const [baseLanguage] = candidate.split('-');
  return SUPPORTED_LANGUAGES.includes(baseLanguage) ? baseLanguage : null;
};

export const normalizeLanguage = (value) => normalizeLanguageCandidate(value) || DEFAULT_LANGUAGE;

export const normalizeTheme = (value) => {
  if (!value) {
    return 'dark';
  }

  const candidate = String(value).trim().toLowerCase();
  return SUPPORTED_THEMES.includes(candidate) ? candidate : 'dark';
};

const readStoredLanguage = (storage) => {
  if (!storage) {
    return null;
  }

  try {
    const storedPreferences = storage.getItem('collaby-ui-preferences');
    if (storedPreferences) {
      const parsed = JSON.parse(storedPreferences);
      const persistedLanguage = normalizeLanguageCandidate(parsed?.language);
      if (persistedLanguage) {
        return persistedLanguage;
      }
    }

    return normalizeLanguageCandidate(storage.getItem('i18nextLng'));
  } catch {
    return null;
  }
};

export const detectBrowserLanguage = (browserNavigator = typeof navigator !== 'undefined' ? navigator : null) => {
  const candidates = [
    ...(Array.isArray(browserNavigator?.languages) ? browserNavigator.languages : []),
    browserNavigator?.language,
    browserNavigator?.userLanguage,
    browserNavigator?.browserLanguage,
  ];

  for (const candidate of candidates) {
    const normalized = normalizeLanguageCandidate(candidate);
    if (normalized) {
      return normalized;
    }
  }

  return DEFAULT_LANGUAGE;
};

export const getInitialLanguage = ({
  storage = typeof window !== 'undefined' ? window.localStorage : null,
  browserNavigator = typeof navigator !== 'undefined' ? navigator : null,
} = {}) => readStoredLanguage(storage) || detectBrowserLanguage(browserNavigator);

const resources = {
  en: {
    common: enCommon,
    landing: enLanding,
    auth: enAuth,
    platform: enPlatform,
    profile: enProfile,
    legal: enLegal,
    errors: enErrors,
  },
  ru: {
    common: ruCommon,
    landing: ruLanding,
    auth: ruAuth,
    platform: ruPlatform,
    profile: ruProfile,
    legal: ruLegal,
    errors: ruErrors,
  },
  kk: {
    common: kkCommon,
    landing: kkLanding,
    auth: kkAuth,
    platform: kkPlatform,
    profile: kkProfile,
    legal: kkLegal,
    errors: kkErrors,
  },
};

if (!i18n.isInitialized) {
  const initialLanguage = getInitialLanguage();
  i18n.use(initReactI18next).init({
    resources,
    lng: initialLanguage,
    fallbackLng: DEFAULT_LANGUAGE,
    supportedLngs: SUPPORTED_LANGUAGES,
    defaultNS: 'common',
    ns: ['common', 'landing', 'auth', 'platform', 'profile', 'legal', 'errors'],
    interpolation: {
      escapeValue: false,
    },
    returnObjects: true,
  });
} else {
  // Sync resources on HMR so locale edits take effect without full restart.
  Object.entries(resources).forEach(([lng, namespaces]) => {
    Object.entries(namespaces).forEach(([ns, bundle]) => {
      i18n.addResourceBundle(lng, ns, bundle, true, true);
    });
  });
}

export default i18n;
