import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import '../styles/Footer.css';

const Footer = () => {
  const navigate = useNavigate();
  const { t } = useTranslation(['landing', 'common']);
  const footer = t('landing:footer', { returnObjects: true });

  const renderLink = (link) => {
    const href = link.url?.startsWith('#')
      ? `/${link.url}`
      : link.url || '#';

    const isExternal = href.startsWith('http://') || href.startsWith('https://');
    const hasHash = href.includes('#');
    if (isExternal) {
      return (
        <a href={href} target="_blank" rel="noopener noreferrer">
          {link.label}
        </a>
      );
    }

    if (hasHash) {
      return <a href={href}>{link.label}</a>;
    }

    return (
      <a
        href={href}
        onClick={(event) => {
          event.preventDefault();
          navigate(href);
        }}
      >
        {link.label}
      </a>
    );
  };

  return (
    <footer className="footer">
      <div className="container">
        <div className="footer-content">
          <div className="footer-section footer-brand">
            <div className="footer-logo">
              <span className="logo-icon">🎮</span>
              <span className="logo-text">{t('common:appName')}</span>
            </div>
            <p className="footer-description">{footer.description}</p>
          </div>

          <div className="footer-section">
            <h4 className="footer-title">{footer.project}</h4>
            <ul className="footer-links">
              {footer.projectLinks.map((link) => (
                <li key={`${footer.project}-${link.label}`}>{renderLink(link)}</li>
              ))}
            </ul>
          </div>

          <div className="footer-section">
            <h4 className="footer-title">{footer.pages}</h4>
            <ul className="footer-links">
              {footer.pageLinks.map((link) => (
                <li key={`${footer.pages}-${link.label}`}>{renderLink(link)}</li>
              ))}
            </ul>
          </div>

          <div className="footer-section">
            <h4 className="footer-title">{footer.documents}</h4>
            <ul className="footer-links">
              {footer.documentLinks.map((link) => (
                <li key={`${footer.documents}-${link.label}`}>{renderLink(link)}</li>
              ))}
            </ul>
          </div>

          <div className="footer-section">
            <h4 className="footer-title">{footer.follow}</h4>
            <p className="footer-newsletter-text">{footer.followDescription}</p>
            <ul className="footer-links">
              {footer.followLinks.map((link) => (
                <li key={`${footer.follow}-${link.label}`}>{renderLink(link)}</li>
              ))}
            </ul>
          </div>
        </div>

        <div className="footer-bottom">
          <p className="footer-copyright">
            &copy; 2026 {t('common:appName')}. {footer.copyright}
          </p>
          <div className="footer-legal">
            {footer.legalLinks.map((link, index) => (
              <React.Fragment key={`${footer.follow}-${link.label}`}>
                {index > 0 ? <span className="separator">·</span> : null}
                {renderLink(link)}
              </React.Fragment>
            ))}
          </div>
        </div>
      </div>
    </footer>
  );
};

export default Footer;
