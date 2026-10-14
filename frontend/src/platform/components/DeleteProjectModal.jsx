import React, { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export const DeleteProjectModal = ({ project, isOpen, onConfirm, onCancel }) => {
  const { t } = useTranslation(['platform', 'common']);
  const [inputValue, setInputValue] = useState('');

  if (!isOpen) return null;

  const isConfirmed = inputValue === project?.name;

  const handleConfirm = async () => {
    if (!isConfirmed) return;

    try {
      await onConfirm(project.id);
    } catch (error) {
      console.error('Error deleting project:', error);
    }

    setInputValue('');
  };

  const handleCancel = () => {
    setInputValue('');
    onCancel();
  };

  return (
    <div className="app-modal-overlay z-50">
      <div className="app-modal-shell">
        <div className="app-modal-header">
          <AlertTriangle className="w-6 h-6 text-red-500 flex-shrink-0" />
          <h2 className="app-modal-title">{t('platform:deleteProject.title')}</h2>
        </div>

        <div className="app-modal-content">
          <p className="app-modal-copy">{t('platform:deleteProject.warning')}</p>
          <p className="app-modal-copy">
            {t('platform:deleteProject.confirm', { name: project?.name || '' })}
          </p>

          <input
            type="text"
            value={inputValue}
            onChange={(event) => setInputValue(event.target.value)}
            placeholder={project?.name}
            className="app-modal-input"
          />
        </div>

        <div className="app-modal-footer">
          <button
            onClick={handleCancel}
            className="app-modal-button app-modal-button--secondary flex-1"
          >
            {t('common:actions.cancel')}
          </button>
          <button
            onClick={handleConfirm}
            disabled={!isConfirmed}
            className="app-modal-button app-modal-button--danger flex-1"
          >
            {t('platform:deleteProject.title')}
          </button>
        </div>
      </div>
    </div>
  );
};
