import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Zap, Coins } from 'lucide-react';
import { cn } from '../../shared/utils/cn';

const RewardToast = ({ show, xp, coins, onClose }) => {
  const { t } = useTranslation('platform');
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    if (show) {
      setVisible(true);
      const timer = setTimeout(() => {
        setVisible(false);
        setTimeout(onClose, 300); // Даём завершиться анимации выхода.
      }, 3000);
      return () => clearTimeout(timer);
    }
  }, [show, onClose]);

  if (!show && !visible) return null;

  return (
    <div className={cn(
      "fixed bottom-8 left-1/2 -translate-x-1/2 z-50 flex items-center gap-4 bg-slate-900/90 dark:bg-white/90 backdrop-blur-md text-white dark:text-slate-900 px-6 py-3 rounded-full shadow-2xl border border-white/10 dark:border-slate-900/10 transition-all duration-500 transform",
      visible ? "translate-y-0 opacity-100 scale-100" : "translate-y-8 opacity-0 scale-95"
    )}>
      <div className="flex items-center gap-2 font-bold text-yellow-400 dark:text-yellow-600 animate-bounce-short">
        <Zap size={20} fill="currentColor" />
        <span>+{xp} {t('analytics.table.xp')}</span>
      </div>
      <div className="w-px h-4 bg-white/20 dark:bg-slate-900/20" />
      <div className="flex items-center gap-2 font-bold text-orange-400 dark:text-orange-600 animate-bounce-short delay-100">
        <Coins size={20} fill="currentColor" />
        <span>+{coins} {t('shop.balance')}</span>
      </div>
    </div>
  );
};

export default RewardToast;
