import React from 'react';
import { useTranslation } from 'react-i18next';
import '../styles/ImagePlaceholder.css';

/**
 * ImagePlaceholder — набор декоративных SVG-плейсхолдеров для изображений.
 * 
 * @param {string} type — тип плейсхолдера: 'dashboard', 'profile', 'analytics', 'kanban', 'chat', 'leaderboard', 'skills', 'team'.
 * @param {string} width — ширина.
 * @param {string} height — высота.
 */
const ImagePlaceholder = ({ type = 'dashboard', width = '100%', height = '400px' }) => {
  const { t } = useTranslation('landing');
  const placeholders = {
    dashboard: {
      icon: (
        <>
          <rect x="20" y="20" width="60" height="40" rx="8" fill="url(#grad1)" opacity="0.8" />
          <rect x="90" y="20" width="60" height="40" rx="8" fill="url(#grad1)" opacity="0.6" />
          <rect x="160" y="20" width="60" height="40" rx="8" fill="url(#grad1)" opacity="0.4" />
          <rect x="20" y="70" width="200" height="80" rx="12" fill="url(#grad2)" opacity="0.7" />
          <circle cx="40" cy="180" r="15" fill="url(#grad3)" opacity="0.8" />
          <rect x="65" y="170" width="100" height="8" rx="4" fill="url(#grad1)" opacity="0.5" />
          <rect x="65" y="185" width="60" height="6" rx="3" fill="url(#grad1)" opacity="0.3" />
          <path d="M 180 160 L 200 180 L 220 170 L 240 190 L 260 175" stroke="url(#grad3)" strokeWidth="3" fill="none" strokeLinecap="round" />
        </>
      ),
      label: t('home.imagePlaceholders.dashboard')
    },
    profile: {
      icon: (
        <>
          <circle cx="140" cy="80" r="35" fill="url(#grad1)" opacity="0.8" />
          <rect x="90" y="130" width="100" height="12" rx="6" fill="url(#grad2)" opacity="0.6" />
          <rect x="110" y="150" width="60" height="8" rx="4" fill="url(#grad2)" opacity="0.4" />
          <rect x="50" y="180" width="180" height="30" rx="15" fill="url(#grad3)" opacity="0.5" />
          <circle cx="80" cy="240" r="20" fill="url(#grad1)" opacity="0.6" />
          <circle cx="140" cy="240" r="20" fill="url(#grad2)" opacity="0.6" />
          <circle cx="200" cy="240" r="20" fill="url(#grad3)" opacity="0.6" />
        </>
      ),
      label: t('home.imagePlaceholders.profile')
    },
    analytics: {
      icon: (
        <>
          <rect x="30" y="160" width="30" height="80" rx="6" fill="url(#grad1)" opacity="0.8" />
          <rect x="70" y="120" width="30" height="120" rx="6" fill="url(#grad2)" opacity="0.7" />
          <rect x="110" y="100" width="30" height="140" rx="6" fill="url(#grad3)" opacity="0.6" />
          <rect x="150" y="140" width="30" height="100" rx="6" fill="url(#grad1)" opacity="0.5" />
          <rect x="190" y="80" width="30" height="160" rx="6" fill="url(#grad2)" opacity="0.9" />
          <path d="M 30 60 Q 90 40, 140 60 T 250 50" stroke="url(#grad3)" strokeWidth="3" fill="none" strokeLinecap="round" />
          <circle cx="30" cy="60" r="5" fill="#FF6B35" />
          <circle cx="140" cy="60" r="5" fill="#FF8C42" />
          <circle cx="250" cy="50" r="5" fill="#FFA056" />
        </>
      ),
      label: t('home.imagePlaceholders.analytics')
    },
    kanban: {
      icon: (
        <>
          <rect x="20" y="30" width="70" height="200" rx="12" fill="url(#grad1)" opacity="0.3" />
          <rect x="100" y="30" width="70" height="200" rx="12" fill="url(#grad2)" opacity="0.3" />
          <rect x="180" y="30" width="70" height="200" rx="12" fill="url(#grad3)" opacity="0.3" />
          
          <rect x="30" y="50" width="50" height="40" rx="8" fill="url(#grad1)" opacity="0.8" />
          <rect x="30" y="100" width="50" height="40" rx="8" fill="url(#grad1)" opacity="0.6" />
          
          <rect x="110" y="50" width="50" height="40" rx="8" fill="url(#grad2)" opacity="0.8" />
          <rect x="110" y="100" width="50" height="40" rx="8" fill="url(#grad2)" opacity="0.6" />
          <rect x="110" y="150" width="50" height="40" rx="8" fill="url(#grad2)" opacity="0.4" />
          
          <rect x="190" y="50" width="50" height="40" rx="8" fill="url(#grad3)" opacity="0.8" />
        </>
      ),
      label: t('home.imagePlaceholders.kanban')
    },
    chat: {
      icon: (
        <>
          <rect x="30" y="40" width="120" height="40" rx="20" fill="url(#grad1)" opacity="0.7" />
          <circle cx="50" cy="60" r="12" fill="url(#grad3)" opacity="0.9" />
          
          <rect x="130" y="100" width="120" height="40" rx="20" fill="url(#grad2)" opacity="0.7" />
          <circle cx="230" cy="120" r="12" fill="url(#grad1)" opacity="0.9" />
          
          <rect x="30" y="160" width="120" height="40" rx="20" fill="url(#grad3)" opacity="0.7" />
          <circle cx="50" cy="180" r="12" fill="url(#grad2)" opacity="0.9" />
          
          <circle cx="240" cy="230" r="20" fill="url(#grad1)" opacity="0.6" />
          <path d="M 230 240 L 240 250 L 250 240" stroke="#FF6B35" strokeWidth="2" fill="none" strokeLinecap="round" strokeLinejoin="round" />
        </>
      ),
      label: t('home.imagePlaceholders.chat')
    },
    leaderboard: {
      icon: (
        <>
          <rect x="80" y="60" width="60" height="80" rx="8" fill="url(#grad1)" opacity="0.9" />
          <rect x="30" y="90" width="60" height="80" rx="8" fill="url(#grad2)" opacity="0.7" />
          <rect x="130" y="100" width="60" height="80" rx="8" fill="url(#grad3)" opacity="0.6" />
          
          <circle cx="110" cy="40" r="15" fill="#FFD700" opacity="0.9" />
          <text x="110" y="47" textAnchor="middle" fill="#FFF" fontSize="16" fontWeight="bold">1</text>
          
          <circle cx="60" cy="70" r="12" fill="#C0C0C0" opacity="0.8" />
          <text x="60" y="76" textAnchor="middle" fill="#FFF" fontSize="14" fontWeight="bold">2</text>
          
          <circle cx="160" cy="80" r="10" fill="#CD7F32" opacity="0.8" />
          <text x="160" y="85" textAnchor="middle" fill="#FFF" fontSize="12" fontWeight="bold">3</text>
          
          <path d="M 50 200 L 70 210 L 60 220 L 70 230" stroke="url(#grad1)" strokeWidth="2" fill="none" />
          <path d="M 100 200 L 120 210 L 110 220 L 120 230" stroke="url(#grad2)" strokeWidth="2" fill="none" />
          <path d="M 150 200 L 170 210 L 160 220 L 170 230" stroke="url(#grad3)" strokeWidth="2" fill="none" />
        </>
      ),
      label: t('home.imagePlaceholders.leaderboard')
    },
    skills: {
      icon: (
        <>
          <circle cx="140" cy="120" r="50" fill="none" stroke="url(#grad1)" strokeWidth="8" opacity="0.3" />
          <circle cx="140" cy="120" r="50" fill="none" stroke="url(#grad1)" strokeWidth="8" strokeDasharray="220" strokeDashoffset="55" opacity="0.8" transform="rotate(-90 140 120)" />
          
          <circle cx="80" cy="180" r="35" fill="none" stroke="url(#grad2)" strokeWidth="6" opacity="0.3" />
          <circle cx="80" cy="180" r="35" fill="none" stroke="url(#grad2)" strokeWidth="6" strokeDasharray="154" strokeDashoffset="38" opacity="0.7" transform="rotate(-90 80 180)" />
          
          <circle cx="200" cy="180" r="35" fill="none" stroke="url(#grad3)" strokeWidth="6" opacity="0.3" />
          <circle cx="200" cy="180" r="35" fill="none" stroke="url(#grad3)" strokeWidth="6" strokeDasharray="154" strokeDashoffset="77" opacity="0.6" transform="rotate(-90 200 180)" />
          
          <path d="M 140 50 L 140 70" stroke="url(#grad1)" strokeWidth="3" />
          <path d="M 120 180 L 140 120 L 160 180" stroke="url(#grad2)" strokeWidth="3" fill="none" />
          <circle cx="140" cy="120" r="8" fill="#FF6B35" />
        </>
      ),
      label: t('home.imagePlaceholders.skills')
    },
    team: {
      icon: (
        <>
          <circle cx="90" cy="80" r="25" fill="url(#grad1)" opacity="0.8" />
          <ellipse cx="90" cy="140" rx="35" ry="25" fill="url(#grad1)" opacity="0.6" />
          
          <circle cx="170" cy="80" r="25" fill="url(#grad2)" opacity="0.8" />
          <ellipse cx="170" cy="140" rx="35" ry="25" fill="url(#grad2)" opacity="0.6" />
          
          <circle cx="130" cy="140" r="25" fill="url(#grad3)" opacity="0.8" />
          <ellipse cx="130" cy="200" rx="35" ry="25" fill="url(#grad3)" opacity="0.6" />
          
          <circle cx="70" cy="190" r="20" fill="url(#grad1)" opacity="0.5" />
          <circle cx="190" cy="190" r="20" fill="url(#grad2)" opacity="0.5" />
        </>
      ),
      label: t('home.imagePlaceholders.team')
    }
  };

  const selected = placeholders[type] || placeholders.dashboard;

  return (
    <div className="image-placeholder" style={{ width, height }}>
      <svg viewBox="0 0 280 280" fill="none" xmlns="http://www.w3.org/2000/svg">
        <defs>
          <linearGradient id="grad1" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#FF6B35" />
            <stop offset="100%" stopColor="#FF8C42" />
          </linearGradient>
          <linearGradient id="grad2" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#FF8C42" />
            <stop offset="100%" stopColor="#FFA056" />
          </linearGradient>
          <linearGradient id="grad3" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#FFA056" />
            <stop offset="100%" stopColor="#FFB870" />
          </linearGradient>
          
          <filter id="glow">
            <feGaussianBlur stdDeviation="4" result="coloredBlur"/>
            <feMerge>
              <feMergeNode in="coloredBlur"/>
              <feMergeNode in="SourceGraphic"/>
            </feMerge>
          </filter>
        </defs>
        
        <rect width="280" height="280" rx="20" fill="url(#grad1)" opacity="0.05" />
        
        <g filter="url(#glow)">
          {selected.icon}
        </g>
      </svg>
      <div className="placeholder-label">{selected.label}</div>
    </div>
  );
};

export default ImagePlaceholder;
