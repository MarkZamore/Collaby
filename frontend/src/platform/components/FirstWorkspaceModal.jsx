import React, { useState } from 'react';
import toast from 'react-hot-toast';
import { ArrowRight, Briefcase, FolderOpen, Sparkles, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';

const FirstWorkspaceModal = ({ onCreateWorkspace, isDark, onClose }) => {
  const { t } = useTranslation(['platform', 'common']);
  const [workspaceName, setWorkspaceName] = useState('');
  const [projectName, setProjectName] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  const handleCreate = async () => {
    if (!workspaceName.trim()) {
      setError(t('platform:firstWorkspace.workspaceRequired'));
      return;
    }

    if (!projectName.trim()) {
      setError(t('platform:firstWorkspace.projectRequired'));
      return;
    }

    setIsLoading(true);
    setError('');

    try {
      if (onCreateWorkspace) {
        await onCreateWorkspace(workspaceName.trim(), projectName.trim());
      }

      toast.success(t('platform:firstWorkspace.created'));
      onClose();
      localStorage.removeItem('collaby_has_seen_tour');
    } catch (err) {
      console.error('Error creating workspace:', err);
      const nextError = t('platform:firstWorkspace.createError');
      setError(nextError);
      toast.error(nextError);
    } finally {
      setIsLoading(false);
    }
  };

  const handleKeyPress = (event) => {
    if (event.key === 'Enter' && workspaceName.trim() && projectName.trim()) {
      handleCreate();
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm"
      data-testid="first-workspace-modal"
    >
      <button
        type="button"
        onClick={onClose}
        className={`absolute right-6 top-6 z-10 inline-flex h-9 w-9 items-center justify-center rounded-full border text-xs shadow-sm transition-all ${
          isDark
            ? 'border-white/10 bg-black/40 text-slate-300 hover:bg-black/70 hover:text-white'
            : 'border-slate-200 bg-white/80 text-slate-500 hover:bg-white hover:text-slate-800'
        }`}
        aria-label={t('common:actions.cancel')}
      >
        <X size={18} />
      </button>

      <div
        className={`glass-panel relative w-full max-w-lg overflow-hidden rounded-[18px] border ${
          isDark ? 'border-white/10' : 'border-white/20'
        }`}
      >
        <div className="absolute right-0 top-0 -z-10 h-96 w-96 rounded-full bg-orange-500/10 blur-3xl" />

        <div className="px-8 pb-4 pt-6 text-center">
          <div className="mb-4 flex justify-center">
            <div className="relative">
              <div className="absolute inset-0 rounded-xl bg-orange-500/20 blur-lg" />
              <div className="relative rounded-lg bg-gradient-to-br from-orange-500 to-orange-600 p-3">
                <Briefcase size={32} className="text-white" />
              </div>
            </div>
          </div>

          <div className="mb-2 flex items-center justify-center gap-2">
            <Sparkles size={20} className="text-orange-500" />
            <h2 className="bg-gradient-to-r from-orange-500 to-orange-600 bg-clip-text text-2xl font-bold text-transparent">
              {t('platform:firstWorkspace.title')}
            </h2>
          </div>

          <p className={`text-sm leading-relaxed ${isDark ? 'text-slate-300' : 'text-slate-600'}`}>
            {t('platform:firstWorkspace.description')}
          </p>
        </div>

        <div className="px-8 py-6">
          <div className="mb-4 grid grid-cols-1 gap-3">
            <div className={`rounded-lg p-3 ${isDark ? 'bg-white/5' : 'bg-orange-50/50'}`}>
              <div className="flex items-start gap-2">
                <Briefcase size={18} className="mt-0.5 shrink-0 text-orange-500" />
                <div className="flex-1">
                  <p className={`text-sm font-semibold ${isDark ? 'text-white' : 'text-slate-900'}`}>
                    {t('platform:firstWorkspace.workspaceLabel')}
                  </p>
                  <p className={`text-xs ${isDark ? 'text-slate-400' : 'text-slate-600'}`}>
                    {t('platform:firstWorkspace.footer')}
                  </p>
                </div>
              </div>
            </div>
          </div>

          <div className="mb-4 space-y-4">
            <div>
              <label className={`mb-2 block text-sm font-semibold ${isDark ? 'text-slate-200' : 'text-slate-700'}`}>
                {t('platform:firstWorkspace.workspaceLabel')}
              </label>
              <input
                type="text"
                data-testid="first-workspace-name"
                value={workspaceName}
                onChange={(event) => setWorkspaceName(event.target.value)}
                onKeyPress={handleKeyPress}
                placeholder={t('platform:firstWorkspace.workspacePlaceholder')}
                className={`w-full rounded-lg border-2 px-4 py-3 transition-all focus:outline-none focus:ring-0 ${
                  isDark
                    ? 'border-white/10 bg-white/5 text-white placeholder-slate-500 focus:border-orange-500/50 focus:bg-white/10'
                    : 'border-orange-100 bg-white text-slate-900 placeholder-slate-400 focus:border-orange-400 focus:bg-orange-50/30'
                }`}
              />
            </div>

            <div>
              <label
                className={`mb-2 flex items-center gap-2 text-sm font-semibold ${
                  isDark ? 'text-slate-200' : 'text-slate-700'
                }`}
              >
                <FolderOpen size={16} className="text-orange-500" />
                {t('platform:firstWorkspace.projectLabel')}
              </label>
              <input
                type="text"
                data-testid="first-project-name"
                value={projectName}
                onChange={(event) => setProjectName(event.target.value)}
                onKeyPress={handleKeyPress}
                placeholder={t('platform:firstWorkspace.projectPlaceholder')}
                className={`w-full rounded-lg border-2 px-4 py-3 transition-all focus:outline-none focus:ring-0 ${
                  isDark
                    ? 'border-white/10 bg-white/5 text-white placeholder-slate-500 focus:border-orange-500/50 focus:bg-white/10'
                    : 'border-orange-100 bg-white text-slate-900 placeholder-slate-400 focus:border-orange-400 focus:bg-orange-50/30'
                }`}
              />
            </div>
          </div>

          {error && (
            <div className="mb-4 rounded-lg border border-red-500/20 bg-red-500/10 p-3">
              <p className="text-sm text-red-500">{error}</p>
            </div>
          )}

          <button
            type="button"
            onClick={handleCreate}
            disabled={isLoading || !workspaceName.trim() || !projectName.trim()}
            data-testid="first-workspace-submit"
            className={`flex w-full items-center justify-center gap-2 rounded-lg px-6 py-3 font-semibold transition-all ${
              isLoading || !workspaceName.trim() || !projectName.trim()
                ? isDark
                  ? 'cursor-not-allowed bg-slate-700/50 text-slate-400'
                  : 'cursor-not-allowed bg-slate-200 text-slate-400'
                : 'bg-gradient-to-r from-orange-500 to-orange-600 text-white hover:shadow-lg hover:shadow-orange-500/20 active:scale-95'
            }`}
          >
            {isLoading ? (
              <>
                <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                <span>{t('platform:firstWorkspace.creating')}</span>
              </>
            ) : (
              <>
                <span>{t('platform:firstWorkspace.submit')}</span>
                <ArrowRight size={18} />
              </>
            )}
          </button>
        </div>

        <div
          className={`border-t px-8 py-3 text-center text-xs ${
            isDark ? 'border-white/10 bg-white/5 text-slate-400' : 'border-orange-100 bg-orange-50/30 text-slate-600'
          }`}
        >
          {t('platform:firstWorkspace.footer')}
        </div>
      </div>
    </div>
  );
};

export default FirstWorkspaceModal;
