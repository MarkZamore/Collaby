import React, { useState } from 'react';
import { createPortal } from 'react-dom';
import { X, FolderKanban, Layout, Users, Globe, Lock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../shared/utils/cn';
import { usePlatform } from '../context/PlatformContext';

const CreateProjectModal = ({ isOpen, onClose, workspaceId }) => {
  const { t } = useTranslation(['platform', 'common']);
  const { createProject } = usePlatform();
  const [step, setStep] = useState(1);
  const [formData, setFormData] = useState({
    name: '',
    description: '',
    visibility: 'private',
    template: 'kanban',
  });

  if (!isOpen) return null;

  const handleSubmit = (event) => {
    event.preventDefault();
    createProject(workspaceId, formData.name, {
      description: formData.description,
      visibility: formData.visibility,
      template: formData.template,
    });
    onClose();
    setStep(1);
    setFormData({
      name: '',
      description: '',
      visibility: 'private',
      template: 'kanban',
    });
  };

  return createPortal(
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 sm:p-6">
      <div className="absolute inset-0 bg-slate-900/20 dark:bg-black/40 backdrop-blur-sm transition-opacity pointer-events-none" />

      <div
        className="relative w-full max-w-2xl rounded-2xl shadow-2xl flex flex-col overflow-hidden animate-in fade-in zoom-in-95 duration-200 z-10"
        style={{
          backgroundColor: 'var(--app-layer-overlay)',
          borderColor: 'var(--app-layer-border)',
          borderWidth: '1px',
          borderStyle: 'solid',
          color: 'var(--app-text-main)',
        }}
      >
        <div
          className="flex items-center justify-between px-6 py-4 border-b"
          style={{ borderColor: 'var(--app-layer-border)' }}
        >
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center text-blue-600 dark:text-blue-400">
              <FolderKanban size={20} />
            </div>
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">{t('platform:createProject.title')}</h2>
          </div>
          <button
            onClick={onClose}
            className="p-2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
          >
            <X size={20} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto">
          {step === 1 ? (
            <div className="p-6 space-y-6">
              <div className="space-y-2">
                <label className="text-sm font-medium text-slate-700 dark:text-slate-300">
                  {t('platform:createProject.nameLabel')}
                </label>
                <input
                  type="text"
                  required
                  value={formData.name}
                  onChange={(event) => setFormData({ ...formData, name: event.target.value })}
                  placeholder={t('platform:createProject.namePlaceholder')}
                  className="w-full px-4 py-2.5 bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 rounded-xl text-sm focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500 outline-none transition-all"
                  autoFocus
                />
              </div>

              <div className="space-y-2">
                <label className="text-sm font-medium text-slate-700 dark:text-slate-300">
                  {t('platform:createProject.descriptionLabel')}
                </label>
                <textarea
                  value={formData.description}
                  onChange={(event) => setFormData({ ...formData, description: event.target.value })}
                  placeholder={t('platform:createProject.descriptionPlaceholder')}
                  rows={3}
                  className="w-full px-4 py-2.5 bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 rounded-xl text-sm focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500 outline-none transition-all resize-none"
                />
              </div>

              <div className="space-y-3">
                <label className="text-sm font-medium text-slate-700 dark:text-slate-300">
                  {t('platform:createProject.visibilityLabel')}
                </label>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  {[
                    {
                      id: 'private',
                      icon: Lock,
                      label: t('platform:createProject.visibility.private.label'),
                      desc: t('platform:createProject.visibility.private.description'),
                    },
                    {
                      id: 'team',
                      icon: Users,
                      label: t('platform:createProject.visibility.team.label'),
                      desc: t('platform:createProject.visibility.team.description'),
                    },
                    {
                      id: 'public',
                      icon: Globe,
                      label: t('platform:createProject.visibility.public.label'),
                      desc: t('platform:createProject.visibility.public.description'),
                    },
                  ].map((option) => (
                    <div
                      key={option.id}
                      onClick={() => setFormData({ ...formData, visibility: option.id })}
                      className={cn(
                        'p-3 rounded-xl border cursor-pointer transition-all flex flex-col gap-2',
                        formData.visibility === option.id
                          ? 'bg-blue-50 dark:bg-blue-900/20 border-blue-200 dark:border-blue-800 ring-1 ring-blue-500/20'
                          : 'bg-white dark:bg-slate-800/30 border-slate-200 dark:border-slate-700 hover:border-blue-300 dark:hover:border-blue-700',
                      )}
                    >
                      <option.icon
                        size={18}
                        className={formData.visibility === option.id ? 'text-blue-600 dark:text-blue-400' : 'text-slate-400'}
                      />
                      <div>
                        <div className="text-sm font-medium text-slate-900 dark:text-white">{option.label}</div>
                        <div className="text-[10px] text-slate-500">{option.desc}</div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          ) : (
            <div className="p-6 space-y-6">
              <div className="space-y-3">
                <label className="text-sm font-medium text-slate-700 dark:text-slate-300">
                  {t('platform:createProject.templateLabel')}
                </label>
                <div className="grid grid-cols-1 gap-3">
                  {[
                    {
                      id: 'kanban',
                      icon: Layout,
                      label: t('platform:createProject.templates.kanban.label'),
                      desc: t('platform:createProject.templates.kanban.description'),
                    },
                    {
                      id: 'scrum',
                      icon: Users,
                      label: t('platform:createProject.templates.scrum.label'),
                      desc: t('platform:createProject.templates.scrum.description'),
                    },
                    {
                      id: 'bug',
                      icon: Globe,
                      label: t('platform:createProject.templates.bug.label'),
                      desc: t('platform:createProject.templates.bug.description'),
                    },
                  ].map((option) => (
                    <div
                      key={option.id}
                      onClick={() => setFormData({ ...formData, template: option.id })}
                      className={cn(
                        'p-4 rounded-xl border cursor-pointer transition-all flex items-center gap-4',
                        formData.template === option.id
                          ? 'bg-blue-50 dark:bg-blue-900/20 border-blue-200 dark:border-blue-800 ring-1 ring-blue-500/20'
                          : 'bg-white dark:bg-slate-800/30 border-slate-200 dark:border-slate-700 hover:border-blue-300 dark:hover:border-blue-700',
                      )}
                    >
                      <div
                        className={cn(
                          'w-10 h-10 rounded-lg flex items-center justify-center',
                          formData.template === option.id
                            ? 'bg-blue-100 dark:bg-blue-900/50 text-blue-600'
                            : 'bg-slate-100 dark:bg-slate-800 text-slate-500',
                        )}
                      >
                        <option.icon size={20} />
                      </div>
                      <div>
                        <div className="text-sm font-medium text-slate-900 dark:text-white">{option.label}</div>
                        <div className="text-xs text-slate-500">{option.desc}</div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}

          <div className="flex items-center justify-between px-6 py-4 border-t border-slate-100 dark:border-slate-800/50 bg-slate-50/50 dark:bg-slate-900/20">
            {step === 2 ? (
              <button
                type="button"
                onClick={() => setStep(1)}
                className="px-4 py-2 text-sm font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
              >
                {t('common:actions.back')}
              </button>
            ) : (
              <button
                type="button"
                onClick={onClose}
                className="px-4 py-2 text-sm font-medium text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
              >
                {t('common:actions.cancel')}
              </button>
            )}

            {step === 1 ? (
              <button
                type="button"
                onClick={() => formData.name && setStep(2)}
                disabled={!formData.name}
                className="px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 disabled:bg-slate-300 dark:disabled:bg-slate-700 rounded-lg shadow-sm shadow-blue-500/20 transition-all"
              >
                {t('common:actions.next')}
              </button>
            ) : (
              <button
                type="submit"
                className="px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg shadow-sm shadow-blue-500/20 transition-all"
              >
                {t('platform:createProject.actions.create')}
              </button>
            )}
          </div>
        </form>
      </div>
    </div>,
    document.body,
  );
};

export default CreateProjectModal;
