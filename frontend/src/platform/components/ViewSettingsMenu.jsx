import React from 'react';
import { createPortal } from 'react-dom';
import { X, Eye, Calendar, Hash, User, Tag, AlertCircle } from 'lucide-react';
import { useTranslation } from 'react-i18next';

const ViewSettingsMenu = ({ viewSettings, onViewSettingChange, onClose, style }) => {
  const { t } = useTranslation('platform');

  const settings = [
    { id: 'showTaskId', label: t('viewSettings.fields.showTaskId'), icon: Hash },
    { id: 'showAssignee', label: t('viewSettings.fields.showAssignee'), icon: User },
    { id: 'showTags', label: t('viewSettings.fields.showTags'), icon: Tag },
    { id: 'showPriority', label: t('viewSettings.fields.showPriority'), icon: AlertCircle },
    { id: 'showDueDate', label: t('viewSettings.fields.showDueDate'), icon: Calendar },
  ];

  return createPortal(
    <>
      <div className="fixed inset-0 z-[9998]" onClick={onClose} />
      <div
        style={{
          ...style,
          zIndex: 9999,
          backgroundColor: 'var(--app-layer-overlay)',
          borderColor: 'var(--app-layer-border)',
          borderWidth: '1px',
          borderStyle: 'solid',
          color: 'var(--app-text-main)',
        }}
        className="w-64 rounded-xl shadow-xl overflow-hidden"
        onClick={(event) => event.stopPropagation()}
      >
        <div
          className="p-4 border-b flex items-center justify-between"
          style={{ borderColor: 'var(--app-layer-border)' }}
        >
          <h3 className="font-bold flex items-center gap-2">
            <Eye size={16} style={{ color: 'var(--app-text-secondary)' }} />
            {t('viewSettings.title')}
          </h3>
          <button
            type="button"
            onClick={onClose}
            style={{ color: 'var(--app-text-secondary)' }}
            className="hover:opacity-80 transition-opacity"
          >
            <X size={16} />
          </button>
        </div>

        <div className="p-2">
          {settings.map((setting) => (
            <label
              key={setting.id}
              className="flex items-center justify-between p-2 rounded-lg cursor-pointer transition-colors group"
            >
              <div
                className="flex items-center gap-3 text-sm group-hover:text-orange-500 transition-colors"
                style={{ color: 'var(--app-text-secondary)' }}
              >
                <setting.icon
                  size={16}
                  style={{ color: 'var(--app-text-secondary)' }}
                  className="group-hover:text-orange-500 transition-colors"
                />
                {setting.label}
              </div>
              <div className="relative inline-flex items-center cursor-pointer">
                <input
                  type="checkbox"
                  className="sr-only peer"
                  checked={viewSettings[setting.id]}
                  onChange={() => onViewSettingChange(setting.id)}
                />
                <div
                  className="w-9 h-5 rounded-full peer peer-checked:after:translate-x-full after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-[var(--app-accent)]"
                  style={{ backgroundColor: 'var(--app-layer-border)' }}
                />
              </div>
            </label>
          ))}
        </div>
      </div>
    </>,
    document.body
  );
};

export default ViewSettingsMenu;
