// Карточка-обёртка для блоков настроек Collaby.
import React from 'react';

const SettingCard = ({ title, children }) => {
  return (
    <div className="space-y-3">
        {title && (
             <h3 
                className="text-sm font-bold uppercase tracking-wider pl-1"
                style={{ color: 'var(--app-text-main)' }}
             >
                {title}
             </h3>
        )}
        <div 
            className="rounded-xl border backdrop-blur-sm overflow-hidden px-5"
            style={{ backgroundColor: 'var(--app-bg)', borderColor: 'var(--app-border)' }}
        >
            {children}
        </div>
    </div>
  );
};

export default SettingCard;
