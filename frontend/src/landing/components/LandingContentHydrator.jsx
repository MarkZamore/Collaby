import { useEffect } from 'react';
import i18n, { LANDING_NAMESPACE_DEFAULTS, normalizeLanguage } from '../../shared/i18n/config';
import { usePreferences } from '../../platform/context/PreferencesContext';
import { buildApiUrl, publicRequest } from '../../shared/utils/apiClient';

const LANDING_PUBLIC_BASE = buildApiUrl('/management', '/landing');

const cloneValue = (value) => JSON.parse(JSON.stringify(value));

const isPlainObject = (value) => value && typeof value === 'object' && !Array.isArray(value);

const deepMerge = (base, patch) => {
  if (Array.isArray(base)) {
    return Array.isArray(patch) ? patch.map(cloneValue) : base.map(cloneValue);
  }

  if (!isPlainObject(base)) {
    return patch === undefined ? cloneValue(base) : cloneValue(patch);
  }

  const result = {};
  const keys = new Set([
    ...Object.keys(base || {}),
    ...Object.keys(isPlainObject(patch) ? patch : {}),
  ]);

  keys.forEach((key) => {
    const baseValue = base?.[key];
    const patchValue = patch?.[key];

    if (patchValue === undefined) {
      result[key] = cloneValue(baseValue);
      return;
    }

    if (Array.isArray(baseValue) || Array.isArray(patchValue)) {
      result[key] = Array.isArray(patchValue) ? patchValue.map(cloneValue) : cloneValue(baseValue);
      return;
    }

    if (isPlainObject(baseValue) && isPlainObject(patchValue)) {
      result[key] = deepMerge(baseValue, patchValue);
      return;
    }

    result[key] = cloneValue(patchValue);
  });

  return result;
};

const applyLandingBundle = async (locale, payload) => {
  const normalizedLocale = normalizeLanguage(locale);
  const defaults = cloneValue(LANDING_NAMESPACE_DEFAULTS[normalizedLocale] || LANDING_NAMESPACE_DEFAULTS.en);
  const merged = deepMerge(defaults, payload || {});

  i18n.addResourceBundle(normalizedLocale, 'landing', merged, true, true);
  if (i18n.language !== normalizedLocale) {
    await i18n.changeLanguage(normalizedLocale);
  }
};

const LandingContentHydrator = ({ active }) => {
  const { language } = usePreferences();

  useEffect(() => {
    if (!active) {
      return undefined;
    }

    const locale = normalizeLanguage(language);
    let cancelled = false;

    const hydrate = async () => {
      try {
        const payload = await publicRequest(`${LANDING_PUBLIC_BASE}/${locale}`);
        if (!cancelled) {
          await applyLandingBundle(locale, payload);
        }
      } catch {
        if (!cancelled) {
          await applyLandingBundle(locale, null);
        }
      }
    };

    hydrate();

    return () => {
      cancelled = true;
    };
  }, [active, language]);

  return null;
};

export default LandingContentHydrator;
