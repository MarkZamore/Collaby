import { normalizeLanguage } from '../i18n/config';

const LOCALE_MAP = {
  en: 'en-US',
  ru: 'ru-RU',
  kk: 'kk-KZ',
};

const DATE_PART_KEYS = ['weekday', 'era', 'year', 'month', 'day'];
const TIME_PART_KEYS = ['dayPeriod', 'hour', 'minute', 'second', 'fractionalSecondDigits', 'timeZoneName'];

export const getIntlLocale = (language) => LOCALE_MAP[normalizeLanguage(language)] || LOCALE_MAP.en;

const toDate = (value) => {
  if (!value) {
    return null;
  }

  const date = value instanceof Date ? value : new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
};

const formatWithOptions = (value, language, options) => {
  const date = toDate(value);
  if (!date) {
    return '';
  }

  return new Intl.DateTimeFormat(getIntlLocale(language), options).format(date);
};

const hasAnyOption = (options, keys) => keys.some((key) => Object.prototype.hasOwnProperty.call(options, key));

export const formatDateTime = (value, language, options = {}) => {
  const hasExplicitDateParts = hasAnyOption(options, DATE_PART_KEYS);
  const hasExplicitTimeParts = hasAnyOption(options, TIME_PART_KEYS);

  return formatWithOptions(value, language, {
    ...(hasExplicitDateParts ? {} : { dateStyle: 'medium' }),
    ...(hasExplicitTimeParts ? {} : { timeStyle: 'short' }),
    ...options,
  });
};

export const formatDate = (value, language, options = {}) =>
  formatWithOptions(value, language, {
    ...(hasAnyOption(options, DATE_PART_KEYS) ? {} : { dateStyle: 'medium' }),
    ...options,
  });

export const formatTime = (value, language, options = {}) =>
  formatWithOptions(value, language, {
    ...(hasAnyOption(options, TIME_PART_KEYS) ? {} : { timeStyle: 'short' }),
    ...options,
  });

export const formatNumber = (value, language, options = {}) => {
  if (value == null || Number.isNaN(Number(value))) {
    return '';
  }

  return new Intl.NumberFormat(getIntlLocale(language), options).format(value);
};
