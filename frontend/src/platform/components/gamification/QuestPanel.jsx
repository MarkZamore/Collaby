import React from 'react';
import { useTranslation } from 'react-i18next';
import { CheckCircle2, Gift, Target } from 'lucide-react';
import { useGamification } from '../../../shared/context/GamificationContext';
import { localizeQuest } from '../../../shared/utils/gamificationCatalogLocalization';

export default function QuestPanel() {
  const { t } = useTranslation('platform');
  const { quests, claimQuest } = useGamification();
  if (quests.length === 0) return null;

  return (
    <div className="glass-card rounded-xl p-4">
      <h3
        className="mb-3 flex items-center gap-2 text-sm font-semibold"
        style={{ color: 'var(--app-text-main)' }}
      >
        <Target size={16} className="text-purple-400" />
        {t('gamificationWidgets.questsTitle')}
      </h3>
      <div className="space-y-2">
        {quests.map((quest) => {
          const localizedQuest = localizeQuest(quest, t);
          return (
            <div
              key={localizedQuest.id}
              className="flex items-center justify-between rounded-lg p-2"
              style={{ background: 'var(--app-bg-secondary)' }}
            >
              <div className="min-w-0 flex-1">
                <p className="truncate text-xs font-medium" style={{ color: 'var(--app-text-main)' }}>
                  {localizedQuest.title}
                </p>
                <div className="mt-1 flex items-center gap-2">
                  <div
                    className="h-1 flex-1 overflow-hidden rounded-full"
                    style={{ background: 'var(--app-bg-tertiary)' }}
                  >
                    <div
                      className="h-full rounded-full transition-all"
                      style={{
                        width: `${Math.min((localizedQuest.progress / Math.max(localizedQuest.requiredCount || 1, 1)) * 100, 100)}%`,
                        background: localizedQuest.isCompleted ? '#22c55e' : 'var(--app-accent)',
                      }}
                    />
                  </div>
                  <span className="whitespace-nowrap text-[10px]" style={{ color: 'var(--app-text-secondary)' }}>
                    {localizedQuest.progress}/{localizedQuest.requiredCount}
                  </span>
                </div>
              </div>
              {localizedQuest.isCompleted && !localizedQuest.isClaimed ? (
                <button
                  onClick={() => claimQuest(localizedQuest.id)}
                  className="ml-2 rounded-md p-1 transition-opacity hover:opacity-80"
                  style={{ background: 'var(--app-accent)', color: '#fff' }}
                  type="button"
                >
                  <Gift size={14} />
                </button>
              ) : localizedQuest.isClaimed ? (
                <CheckCircle2 size={16} className="ml-2 text-green-500" />
              ) : null}
            </div>
          );
        })}
      </div>
    </div>
  );
}
