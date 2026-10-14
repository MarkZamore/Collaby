import React, { useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { usePlatform } from '../context/PlatformContext';
import { getDefaultRawStatus, normalizeStatusKey } from '../utils/statuses';

const PRIORITY_OPTIONS = ['low', 'high', 'urgent'];

const FALLBACK_STATUS_OPTIONS = [
  { value: getDefaultRawStatus('backlog'), key: 'backlog' },
  { value: getDefaultRawStatus('todo'), key: 'todo' },
  { value: getDefaultRawStatus('inProgress'), key: 'inProgress' },
  { value: getDefaultRawStatus('review'), key: 'review' },
  { value: getDefaultRawStatus('done'), key: 'done' },
];

const CreateTaskModal = ({ onClose, initialStatus }) => {
  const { t } = useTranslation(['platform', 'common']);
  const { addTask, users, activeProject } = usePlatform();

  const statusOptions = useMemo(() => {
    const columns = Object.values(activeProject?.boardData?.columns || {});
    if (columns.length === 0) {
      return FALLBACK_STATUS_OPTIONS;
    }

    return columns.map((column) => ({
      value: column.rawTitle || column.title,
      key: column.statusKey || normalizeStatusKey(column.rawTitle || column.title),
    }));
  }, [activeProject?.boardData?.columns]);

  const resolvedInitialStatus = useMemo(() => {
    if (!initialStatus) {
      return statusOptions[0]?.value || getDefaultRawStatus('todo');
    }

    const match = statusOptions.find(
      (option) =>
        option.value === initialStatus || option.key === normalizeStatusKey(initialStatus),
    );

    return match?.value || initialStatus;
  }, [initialStatus, statusOptions]);

  const [formData, setFormData] = useState(() => ({
    title: '',
    description: '',
    status: resolvedInitialStatus,
    priority: '',
    assigneeId: '',
    dueDate: '',
    tags: [],
  }));

  useEffect(() => {
    setFormData((prev) =>
      statusOptions.some((option) => option.value === prev.status)
        ? prev
        : { ...prev, status: resolvedInitialStatus },
    );
  }, [resolvedInitialStatus, statusOptions]);

  const handleSubmit = (event) => {
    event.preventDefault();

    const assignee = users.find((user) => user.id === formData.assigneeId);

    addTask({
      ...formData,
      assigneeId: formData.assigneeId || null,
      assignee: assignee ? { id: assignee.id, name: assignee.name, initials: assignee.initials } : null,
      comments: 0,
      attachments: 0,
    });

    onClose();
  };

  return createPortal(
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 sm:p-6">
      <div
        className="absolute inset-0 bg-slate-900/20 backdrop-blur-sm transition-opacity dark:bg-black/40"
        onClick={onClose}
      />

      <div
        className="relative z-10 flex max-h-[600px] w-full max-w-2xl animate-in zoom-in-95 flex-col overflow-hidden rounded-3xl shadow-2xl duration-200 fade-in"
        style={{
          backgroundColor: 'var(--app-layer-overlay)',
          borderColor: 'var(--app-layer-border)',
          borderWidth: '1px',
          borderStyle: 'solid',
          color: 'var(--app-text-main)',
        }}
      >
        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-4 dark:border-slate-800/50">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
            {t('platform:createTask.title')}
          </h2>
          <button
            onClick={onClose}
            className="rounded-lg p-2 text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-slate-800 dark:hover:text-slate-300"
            aria-label={t('common:actions.cancel')}
          >
            <X size={20} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="flex-1 space-y-6 overflow-y-auto p-6">
          <div className="space-y-4">
            <div>
              <input
                type="text"
                placeholder={t('platform:createTask.placeholders.title')}
                required
                value={formData.title}
                onChange={(event) => setFormData({ ...formData, title: event.target.value })}
                className="w-full border-none bg-transparent p-0 text-xl font-semibold text-slate-900 placeholder:text-slate-400 focus:ring-0 dark:text-white"
              />
            </div>

            <div>
              <textarea
                placeholder={t('platform:createTask.placeholders.description')}
                rows={4}
                value={formData.description}
                onChange={(event) => setFormData({ ...formData, description: event.target.value })}
                className="w-full resize-none rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm text-slate-900 placeholder:text-slate-400 transition-all focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 dark:border-slate-700 dark:bg-slate-800/50 dark:text-white"
              />
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="space-y-1">
                <label className="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {t('platform:createTask.fields.status')}
                </label>
                <select
                  value={formData.status}
                  onChange={(event) => setFormData({ ...formData, status: event.target.value })}
                  className="w-full rounded-lg border border-slate-200 bg-slate-50 p-2.5 text-sm text-slate-900 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 dark:border-slate-700 dark:bg-slate-800/50 dark:text-white"
                >
                  {statusOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {t(`platform:createTask.statuses.${option.key}`)}
                    </option>
                  ))}
                </select>
              </div>

              <div className="space-y-1">
                <label className="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {t('platform:createTask.fields.priority')}
                </label>
                <select
                  value={formData.priority}
                  onChange={(event) => setFormData({ ...formData, priority: event.target.value })}
                  className="w-full rounded-lg border border-slate-200 bg-slate-50 p-2.5 text-sm text-slate-900 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 dark:border-slate-700 dark:bg-slate-800/50 dark:text-white"
                >
                  <option value="">{t('platform:createTask.priorities.none', { defaultValue: 'Без приоритета' })}</option>
                  {PRIORITY_OPTIONS.map((priority) => (
                    <option key={priority} value={priority}>
                      {t(`platform:createTask.priorities.${priority}`)}
                    </option>
                  ))}
                </select>
              </div>

              <div className="space-y-1">
                <label className="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {t('platform:createTask.fields.assignee')}
                </label>
                <select
                  value={formData.assigneeId}
                  onChange={(event) => setFormData({ ...formData, assigneeId: event.target.value })}
                  className="w-full rounded-lg border border-slate-200 bg-slate-50 p-2.5 text-sm text-slate-900 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 dark:border-slate-700 dark:bg-slate-800/50 dark:text-white"
                >
                  <option value="">{t('platform:createTask.unassigned')}</option>
                  {users.map((user) => (
                    <option key={user.id} value={user.id}>
                      {user.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="space-y-1">
                <label className="text-xs font-medium uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {t('platform:createTask.fields.dueDate')}
                </label>
                <input
                  type="date"
                  value={formData.dueDate}
                  onChange={(event) => setFormData({ ...formData, dueDate: event.target.value })}
                  className="w-full rounded-lg border border-slate-200 bg-slate-50 p-2.5 text-sm text-slate-900 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 dark:border-slate-700 dark:bg-slate-800/50 dark:text-white"
                />
              </div>
            </div>
          </div>

          <div className="flex justify-end gap-3 border-t border-slate-100 pt-4 dark:border-slate-800/50">
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg px-4 py-2 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
            >
              {t('common:actions.cancel')}
            </button>
            <button
              type="submit"
              className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white shadow-sm shadow-blue-500/20 transition-all hover:bg-blue-700 hover:shadow-blue-500/40"
            >
              {t('platform:createTask.submit')}
            </button>
          </div>
        </form>
      </div>
    </div>,
    document.body,
  );
};

export default CreateTaskModal;
