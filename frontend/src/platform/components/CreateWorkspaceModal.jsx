import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import '../styles/CreateWorkspaceModal.css';

const CreateWorkspaceModal = ({ isOpen, onClose, onComplete }) => {
  const { t } = useTranslation(['platform', 'common']);
  const [step, setStep] = useState(1);
  const totalSteps = 3;

  const [formData, setFormData] = useState({
    usagePurpose: '',
    managementTypes: [],
    workspaceName: '',
  });

  if (!isOpen) return null;

  const handleNext = () => {
    if (step < totalSteps) {
      setStep(step + 1);
      return;
    }

    onComplete?.(formData);
    onClose();
    setStep(1);
    setFormData({ usagePurpose: '', managementTypes: [], workspaceName: '' });
  };

  const handleClose = () => {
    onClose();
    setStep(1);
    setFormData({ usagePurpose: '', managementTypes: [], workspaceName: '' });
  };

  const handleBack = () => {
    if (step > 1) setStep(step - 1);
  };

  const toggleManagementType = (type) => {
    setFormData((prev) => {
      const types = prev.managementTypes.includes(type)
        ? prev.managementTypes.filter((item) => item !== type)
        : [...prev.managementTypes, type];
      return { ...prev, managementTypes: types };
    });
  };

  const renderStepContent = () => {
    switch (step) {
      case 1:
        return (
          <div className="workspace-modal-content">
            <h2 className="workspace-modal-title">{t('platform:createWorkspace.purpose.title')}</h2>
            <p className="workspace-modal-subtitle">{t('platform:createWorkspace.purpose.subtitle')}</p>
            <div className="workspace-options-grid">
              {[
                { id: 'work', label: t('platform:createWorkspace.purpose.options.work'), icon: '💼' },
                { id: 'personal', label: t('platform:createWorkspace.purpose.options.personal'), icon: '🏠' },
                { id: 'study', label: t('platform:createWorkspace.purpose.options.study'), icon: '📚' },
              ].map((option) => (
                <button
                  key={option.id}
                  className={`workspace-option-card ${formData.usagePurpose === option.id ? 'selected' : ''}`}
                  onClick={() => setFormData({ ...formData, usagePurpose: option.id })}
                >
                  <span className="workspace-option-icon">{option.icon}</span>
                  <span className="workspace-option-label">{option.label}</span>
                </button>
              ))}
            </div>
          </div>
        );
      case 2:
        return (
          <div className="workspace-modal-content">
            <h2 className="workspace-modal-title">{t('platform:createWorkspace.management.title')}</h2>
            <p className="workspace-modal-subtitle">{t('platform:createWorkspace.management.subtitle')}</p>
            <div className="workspace-options-grid multi">
              {[
                { id: 'finance', label: t('platform:createWorkspace.management.options.finance'), icon: '💰' },
                { id: 'personal', label: t('platform:createWorkspace.management.options.personal'), icon: '📋' },
                { id: 'operations', label: t('platform:createWorkspace.management.options.operations'), icon: '⚙️' },
                { id: 'hr', label: t('platform:createWorkspace.management.options.hr'), icon: '👥' },
                { id: 'startup', label: t('platform:createWorkspace.management.options.startup'), icon: '🚀' },
                { id: 'it', label: t('platform:createWorkspace.management.options.it'), icon: '💻' },
                { id: 'marketing', label: t('platform:createWorkspace.management.options.marketing'), icon: '📈' },
                { id: 'design', label: t('platform:createWorkspace.management.options.design'), icon: '🎨' },
                { id: 'sales', label: t('platform:createWorkspace.management.options.sales'), icon: '🤝' },
                { id: 'support', label: t('platform:createWorkspace.management.options.support'), icon: '💬' },
                { id: 'other', label: t('platform:createWorkspace.management.options.other'), icon: '📦' },
              ].map((type) => (
                <button
                  key={type.id}
                  className={`workspace-option-pill ${formData.managementTypes.includes(type.id) ? 'selected' : ''}`}
                  onClick={() => toggleManagementType(type.id)}
                >
                  <span>{type.icon}</span>
                  <span>{type.label}</span>
                </button>
              ))}
            </div>
          </div>
        );
      case 3:
        return (
          <div className="workspace-modal-content">
            <h2 className="workspace-modal-title">{t('platform:createWorkspace.name.title')}</h2>
            <p className="workspace-modal-subtitle">{t('platform:createWorkspace.name.subtitle')}</p>
            <div className="workspace-name-input">
              <input
                type="text"
                placeholder={t('platform:createWorkspace.name.placeholder')}
                value={formData.workspaceName}
                onChange={(event) => setFormData({ ...formData, workspaceName: event.target.value })}
                autoFocus
              />
            </div>
          </div>
        );
      default:
        return null;
    }
  };

  const isNextDisabled =
    (step === 1 && !formData.usagePurpose) ||
    (step === 2 && formData.managementTypes.length === 0) ||
    (step === 3 && !formData.workspaceName.trim());

  return (
    <div className="workspace-modal-overlay" onClick={handleClose}>
      <div className="workspace-modal" onClick={(event) => event.stopPropagation()}>
        <div className="workspace-modal-header">
          <div className="workspace-modal-brand">
            <span className="workspace-modal-icon">✨</span>
            <span>{t('platform:createWorkspace.brand')}</span>
          </div>
          <button className="workspace-modal-close" onClick={handleClose}>
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M18 6L6 18M6 6l12 12" />
            </svg>
          </button>
        </div>

        {renderStepContent()}

        <div className="workspace-progress">
          <div className="workspace-progress-steps">
            {[1, 2, 3].map((currentStep) => (
              <div
                key={currentStep}
                className={`workspace-progress-step ${currentStep <= step ? 'active' : ''} ${currentStep < step ? 'completed' : ''}`}
              />
            ))}
          </div>
          <span className="workspace-progress-text">{t('platform:createWorkspace.progress', { step, total: totalSteps })}</span>
        </div>

        <div className="workspace-modal-footer">
          {step > 1 ? (
            <button className="workspace-btn-back" onClick={handleBack}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M15 18l-6-6 6-6" />
              </svg>
              {t('common:actions.back')}
            </button>
          ) : (
            <button className="workspace-btn-cancel" onClick={handleClose}>
              {t('common:actions.cancel')}
            </button>
          )}

          <button className="workspace-btn-next" onClick={handleNext} disabled={isNextDisabled}>
            {step === totalSteps ? t('platform:createWorkspace.actions.create') : t('common:actions.next')}
            {step < totalSteps && (
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M9 18l6-6-6-6" />
              </svg>
            )}
          </button>
        </div>
      </div>
    </div>
  );
};

export default CreateWorkspaceModal;
