import React from 'react';
import { useTranslation } from 'react-i18next';
import { Crown, Medal } from 'lucide-react';

const BADGE_CONFIG = {
  1: {
    icon: Crown,
    color: '#F59E0B',
    labelKey: 'gamificationWidgets.firstPlace',
    bg: 'rgba(245, 158, 11, 0.15)',
  },
  2: {
    icon: Medal,
    color: '#94A3B8',
    labelKey: 'gamificationWidgets.secondPlace',
    bg: 'rgba(148, 163, 184, 0.15)',
  },
  3: {
    icon: Medal,
    color: '#D97706',
    labelKey: 'gamificationWidgets.thirdPlace',
    bg: 'rgba(217, 119, 6, 0.15)',
  },
};

export default function TopBadge({ rank, size = 'sm' }) {
  const { t } = useTranslation('platform');
  const config = BADGE_CONFIG[rank];
  if (!config) return null;

  const Icon = config.icon;
  const iconSize = size === 'lg' ? 20 : 14;

  return (
    <div
      className="inline-flex items-center gap-1 rounded-full"
      style={{
        backgroundColor: config.bg,
        padding: size === 'lg' ? '4px 10px' : '2px 6px',
      }}
    >
      <Icon size={iconSize} style={{ color: config.color }} />
      {size === 'lg' && (
        <span className="text-xs font-bold" style={{ color: config.color }}>
          {t(config.labelKey)}
        </span>
      )}
    </div>
  );
}
