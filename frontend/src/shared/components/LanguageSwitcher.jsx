import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Globe, Check, ChevronDown } from 'lucide-react';
import { usePreferences } from '../../platform/context/PreferencesContext';
import { LANGUAGE_META, SUPPORTED_LANGUAGES } from '../i18n/config';
import './LanguageSwitcher.css';

const LanguageSwitcher = ({ className = '', compact = false }) => {
  const { language, setLanguage } = usePreferences();
  const [isOpen, setIsOpen] = useState(false);
  const ref = useRef(null);
  const current = LANGUAGE_META[language] || LANGUAGE_META.en;

  const handleSelect = useCallback((locale) => {
    setLanguage(locale);
    setIsOpen(false);
  }, [setLanguage]);

  useEffect(() => {
    const handleClickOutside = (e) => {
      if (ref.current && !ref.current.contains(e.target)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  useEffect(() => {
    const handleEsc = (e) => {
      if (e.key === 'Escape') setIsOpen(false);
    };
    if (isOpen) {
      document.addEventListener('keydown', handleEsc);
      return () => document.removeEventListener('keydown', handleEsc);
    }
  }, [isOpen]);

  return (
    <div className={`lang-switcher ${compact ? 'lang-switcher--compact' : ''} ${className}`.trim()} ref={ref}>
      <button
        type="button"
        className={`lang-switcher__trigger ${isOpen ? 'lang-switcher__trigger--open' : ''}`}
        onClick={() => setIsOpen((prev) => !prev)}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-label="Select language"
      >
        <Globe size={15} className="lang-switcher__globe" />
        <span className="lang-switcher__label">{current.nativeName}</span>
        <ChevronDown size={14} className={`lang-switcher__chevron ${isOpen ? 'lang-switcher__chevron--open' : ''}`} />
      </button>

      {isOpen && (
        <div className="lang-switcher__dropdown" role="listbox" aria-label="Languages">
          {SUPPORTED_LANGUAGES.map((locale) => {
            const meta = LANGUAGE_META[locale];
            const isActive = locale === language;
            return (
              <button
                key={locale}
                type="button"
                role="option"
                aria-selected={isActive}
                className={`lang-switcher__option ${isActive ? 'lang-switcher__option--active' : ''}`}
                onClick={() => handleSelect(locale)}
              >
                <span className="lang-switcher__option-name">{meta.nativeName}</span>
                {isActive && (
                  <span className="lang-switcher__check-wrap">
                    <Check size={14} />
                  </span>
                )}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
};

export default LanguageSwitcher;
