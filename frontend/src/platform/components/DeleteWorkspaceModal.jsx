import React, { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export const DeleteWorkspaceModal = ({ workspace, isOpen, onConfirm, onCancel }) => {
  const { t } = useTranslation(['platform', 'common']);
  const [inputValue, setInputValue] = useState('');

  if (!isOpen) return null;

  const isConfirmed = inputValue === workspace?.name;

  const handleConfirm = async () => {
    if (!isConfirmed) return;

    try {
      await onConfirm(workspace.id);
    } catch (error) {
      console.error('Error deleting workspace:', error);
    }

    setInputValue('');
  };

  const handleCancel = () => {
    setInputValue('');
    onCancel();
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-slate-900 rounded-lg shadow-lg border border-slate-700 max-w-md w-full mx-4">
        <div className="flex items-center gap-3 p-6 border-b border-slate-700">
          <AlertTriangle className="w-6 h-6 text-red-500 flex-shrink-0" />
          <h2 className="text-xl font-semibold text-white">{t('platform:deleteWorkspace.title')}</h2>
        </div>

        <div className="p-6 space-y-4">
          <p className="text-sm text-slate-300">{t('platform:deleteWorkspace.warning')}</p>
          <p className="text-sm text-slate-400">
            {t('platform:deleteWorkspace.confirm', { name: workspace?.name || '' })}
          </p>

          <input
            type="text"
            value={inputValue}
            onChange={(event) => setInputValue(event.target.value)}
            placeholder={workspace?.name}
            className="w-full px-3 py-2 bg-slate-800 border border-slate-600 rounded-lg text-white placeholder-slate-500 focus:outline-none focus:border-red-500 focus:ring-1 focus:ring-red-500"
          />
        </div>

        <div className="flex gap-3 p-6 border-t border-slate-700">
          <button
            onClick={handleCancel}
            className="flex-1 px-4 py-2 rounded-lg border border-slate-600 text-slate-300 hover:bg-slate-800 transition-colors"
          >
            {t('common:actions.cancel')}
          </button>
          <button
            onClick={handleConfirm}
            disabled={!isConfirmed}
            className={`flex-1 px-4 py-2 rounded-lg font-medium transition-colors ${
              isConfirmed
                ? 'bg-red-600 text-white hover:bg-red-700'
                : 'bg-slate-700 text-slate-500 cursor-not-allowed'
            }`}
          >
            {t('platform:deleteWorkspace.title')}
          </button>
        </div>
      </div>
    </div>
  );
};
