import React from 'react';
import { useTranslation } from 'react-i18next';

const ContrastSlider = ({ value, onChange, min = 0, max = 100 }) => {
  const { t } = useTranslation('profile');

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between px-1">
        <span className="text-sm font-medium" style={{ color: 'var(--app-text-main)' }}>
          {t('interface.contrastLabel')}
        </span>
        <span className="text-xs font-mono" style={{ color: 'var(--app-text-secondary)' }}>
          {value}%
        </span>
      </div>
      <div
        className="relative h-3 flex-1 rounded-full"
        style={{ backgroundColor: 'rgba(128,128,128,0.2)' }}
      >
        <input
          type="range"
          min={min}
          max={max}
          value={value}
          onChange={(event) => onChange(parseInt(event.target.value, 10))}
          className="absolute inset-0 z-10 h-full w-full cursor-pointer opacity-0"
        />
        <div
          className="pointer-events-none absolute left-0 top-0 h-full rounded-full"
          style={{ width: `${value}%`, backgroundColor: 'var(--app-accent)' }}
        />
        <div
          className="pointer-events-none absolute top-1/2 h-4 w-4 -translate-y-1/2 rounded-full bg-white shadow-md transition-transform"
          style={{ left: `${value}%`, transform: 'translate(-50%, -50%)' }}
        />
      </div>
    </div>
  );
};

export default ContrastSlider;
