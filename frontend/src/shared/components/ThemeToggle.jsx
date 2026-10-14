import React from 'react';
import { useTranslation } from 'react-i18next';
import { Sun, Moon } from 'lucide-react';
import '../styles/ThemeToggle.css';

const ThemeToggle = ({ isDark, onToggle }) => {
  const { t } = useTranslation('common');

  return (
    <button
      className="theme-toggle"
      onClick={onToggle}
      aria-label={isDark ? t('theme.toggleToLight') : t('theme.toggleToDark')}
    >
      <div className="toggle-track">
        <div className="toggle-thumb">
          <span className="toggle-icon">{isDark ? <Moon size={14} /> : <Sun size={14} />}</span>
        </div>
      </div>
    </button>
  );
};

export default ThemeToggle;
