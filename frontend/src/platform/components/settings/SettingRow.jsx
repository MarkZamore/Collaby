// Строка настройки Collaby в линейном layout-стиле.
import React from 'react';
import { cn } from '../../../shared/utils/cn';

const SettingRow = ({ label, description, children, className }) => {
  return (
    <div 
        className={cn("flex flex-col sm:flex-row justify-between sm:items-center gap-4 py-4 border-b last:border-0", className)}
        style={{ borderColor: 'var(--app-border)' }}
    >
      <div className="flex-1">
        <div className="text-sm font-medium" style={{ color: 'var(--app-text-main)' }}>{label}</div>
        {description && <div className="text-xs mt-0.5 max-w-md" style={{ color: 'var(--app-text-secondary)' }}>{description}</div>}
      </div>
      <div className="flex-shrink-0">
        {children}
      </div>
    </div>
  );
};

export default SettingRow;
