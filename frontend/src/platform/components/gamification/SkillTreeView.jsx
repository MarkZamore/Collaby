import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Check, ChevronRight, GitBranch, Lock } from 'lucide-react';
import { useGamification } from '../../../shared/context/GamificationContext';
import { localizeSkillTreeNode } from '../../../shared/utils/gamificationCatalogLocalization';

export default function SkillTreeView({ projectId }) {
  const { t } = useTranslation('platform');
  const { api, accessToken } = useGamification();
  const [nodes, setNodes] = useState([]);
  const [loading, setLoading] = useState(true);

  const fetchTree = async () => {
    if (!accessToken || !projectId) {
      setLoading(false);
      return;
    }

    try {
      const data = await api.getSkillTree(accessToken, projectId);
      setNodes(data || []);
    } catch {
      setNodes([]);
    }

    setLoading(false);
  };

  useEffect(() => {
    fetchTree();
  }, [accessToken, projectId]);

  const handleChoose = async (nodeId) => {
    try {
      await api.chooseSkillNode(accessToken, projectId, nodeId);
      await fetchTree();
    } catch {}
  };

  const tiers = [...new Set(nodes.map((node) => node.tier))].sort();

  return (
    <div className="glass-card rounded-xl p-4">
      <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>
        <GitBranch size={16} className="text-purple-400" />
        {t('skillTree.title')}
      </h3>
      {loading ? (
        <div className="py-4 text-center text-xs" style={{ color: 'var(--app-text-secondary)' }}>
          {t('skillTree.loading')}
        </div>
      ) : nodes.length === 0 ? (
        <div className="py-4 text-center text-xs" style={{ color: 'var(--app-text-secondary)' }}>
          {t('skillTree.empty')}
        </div>
      ) : (
        <div className="space-y-3">
          {tiers.map((tier) => (
            <div key={tier}>
              <div className="mb-1 text-[10px] font-medium" style={{ color: 'var(--app-text-secondary)' }}>
                {t('skillTree.tier', { tier })}
              </div>
              <div className="flex gap-2">
                {nodes
                  .filter((node) => node.tier === tier)
                  .map((node) => {
                    const localizedNode = localizeSkillTreeNode(node, t);

                    return (
                      <button
                        key={localizedNode.id}
                        onClick={() => localizedNode.isAvailable && handleChoose(localizedNode.id)}
                        disabled={!localizedNode.isAvailable}
                        className="flex-1 rounded-lg border p-2 text-left transition-all"
                        style={{
                          borderColor: localizedNode.isChosen ? 'var(--app-accent)' : 'var(--app-border)',
                          background: localizedNode.isChosen ? 'var(--app-accent-subtle)' : 'var(--app-bg-secondary)',
                          opacity: localizedNode.isAvailable || localizedNode.isChosen ? 1 : 0.5,
                          cursor: localizedNode.isAvailable ? 'pointer' : 'default',
                        }}
                      >
                        <div className="mb-0.5 flex items-center gap-1">
                          {localizedNode.isChosen ? (
                            <Check size={12} className="text-green-500" />
                          ) : !localizedNode.isAvailable ? (
                            <Lock size={12} style={{ color: 'var(--app-text-secondary)' }} />
                          ) : (
                            <ChevronRight size={12} style={{ color: 'var(--app-accent)' }} />
                          )}
                          <span className="text-xs font-medium" style={{ color: 'var(--app-text-main)' }}>
                            {localizedNode.title}
                          </span>
                        </div>
                        <p className="line-clamp-2 text-[10px]" style={{ color: 'var(--app-text-secondary)' }}>
                          {localizedNode.description}
                        </p>
                      </button>
                    );
                  })}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
