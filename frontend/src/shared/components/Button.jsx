import React from 'react';
import '../styles/Button.css';

/**
 * Button - универсальная кнопка с различными вариантами стилей
 * 
 * @param {string} variant - вариант стиля: 'primary', 'secondary', 'outline', 'ghost'
 * @param {string} size - размер: 'sm', 'md', 'lg'
 * @param {ReactNode} children - содержимое кнопки
 * @param {ReactNode} icon - иконка (слева или справа)
 * @param {string} iconPosition - позиция иконки: 'left', 'right'
 * @param {boolean} fullWidth - растянуть на всю ширину
 * @param {boolean} disabled - заблокировать кнопку
 * @param {function} onClick - обработчик клика
 * @param {string} className - дополнительные классы
 */
const Button = ({ 
  variant = 'primary',
  size = 'md',
  children,
  icon,
  iconPosition = 'right',
  fullWidth = false,
  disabled = false,
  onClick,
  className = '',
  ...props
}) => {
  const classes = `
    btn 
    btn-${variant} 
    btn-${size} 
    ${fullWidth ? 'btn-full' : ''} 
    ${className}
  `.trim();

  return (
    <button 
      className={classes}
      onClick={onClick}
      disabled={disabled}
      {...props}
    >
      {icon && iconPosition === 'left' && (
        <span className="btn-icon btn-icon-left">{icon}</span>
      )}
      <span className="btn-text">{children}</span>
      {icon && iconPosition === 'right' && (
        <span className="btn-icon btn-icon-right">{icon}</span>
      )}
    </button>
  );
};

export default Button;
