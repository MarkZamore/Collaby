import React, { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import ThemeToggle from '../../shared/components/ThemeToggle';
import LanguageSwitcher from '../../shared/components/LanguageSwitcher';
import '../styles/Navbar.css';

const Navbar = ({ isDark, toggleTheme, isScrolled }) => {
  const navigate = useNavigate();
  const location = useLocation();
  const { t } = useTranslation(['landing', 'common']);
  const [isCompanyDropdownOpen, setIsCompanyDropdownOpen] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const navbar = t('landing:navbar', { returnObjects: true });

  const scrollToSection = (sectionId) => {
    const element = document.getElementById(sectionId);
    if (!element) {
      return;
    }

    const headerHeight = 80;
    const elementPosition = element.getBoundingClientRect().top + window.pageYOffset;
    const offsetPosition = elementPosition - headerHeight;

    window.scrollTo({
      top: offsetPosition,
      behavior: 'smooth',
    });
  };

  const handleSectionClick = (sectionId) => {
    setIsMobileMenuOpen(false);
    if (location.pathname !== '/') {
      navigate('/');
      window.setTimeout(() => {
        scrollToSection(sectionId);
      }, 100);
      return;
    }

    scrollToSection(sectionId);
  };

  const navigateByCmsLink = (url) => {
    if (!url) {
      return;
    }

    setIsMobileMenuOpen(false);

    if (url.startsWith('http://') || url.startsWith('https://')) {
      window.open(url, '_blank', 'noopener,noreferrer');
      return;
    }

    if (url.startsWith('#')) {
      handleSectionClick(url.slice(1));
      return;
    }

    if (url.startsWith('/#')) {
      handleSectionClick(url.slice(2));
      return;
    }

    navigate(url);
  };

  const renderCmsLink = (item, className) => (
    <span className={className} onClick={() => navigateByCmsLink(item.url)}>
      {item.label}
    </span>
  );

  return (
    <div className={`navbar ${isScrolled ? 'scrolled' : ''}`}>
      <div className="navbar-container">
        <div className="navbar-logo" onClick={() => navigate('/')}>
          <span className="navbar-logo-icon">🎮</span>
          <span className="navbar-logo-text">{t('common:appName')}</span>
        </div>

        <ul className="navbar-links">
          <li
            className="navbar-item dropdown"
            onMouseEnter={() => setIsCompanyDropdownOpen(true)}
            onMouseLeave={() => setIsCompanyDropdownOpen(false)}
          >
            <span className="navbar-link dropdown-trigger">
              {navbar.aboutMenu}
              <svg
                className={`dropdown-arrow ${isCompanyDropdownOpen ? 'open' : ''}`}
                width="12"
                height="12"
                viewBox="0 0 12 12"
                fill="currentColor"
              >
                <path d="M6 8L2 4h8L6 8z" />
              </svg>
            </span>
            <ul className={`dropdown-menu ${isCompanyDropdownOpen ? 'open' : ''}`}>
              <li>{renderCmsLink(navbar.aboutTeam, undefined)}</li>
              <li>{renderCmsLink(navbar.contact, undefined)}</li>
            </ul>
          </li>
          <li>{renderCmsLink(navbar.features, 'navbar-link')}</li>
          <li>{renderCmsLink(navbar.benefits, 'navbar-link')}</li>
        </ul>

        <div className="navbar-actions">
          <LanguageSwitcher />
          <ThemeToggle isDark={isDark} onToggle={toggleTheme} />
          <button
            className="navbar-btn navbar-btn-secondary"
            onClick={() => navigateByCmsLink(navbar.login.url)}
          >
            {navbar.login.label}
          </button>
          <button
            className="navbar-btn navbar-btn-primary"
            onClick={() => navigateByCmsLink(navbar.register.url)}
          >
            {navbar.register.label}
          </button>
        </div>

        <button
          className={`navbar-burger ${isMobileMenuOpen ? 'open' : ''}`}
          onClick={() => setIsMobileMenuOpen((prev) => !prev)}
        >
          <span></span>
          <span></span>
          <span></span>
        </button>
      </div>

      <div
        className={`navbar-mobile-overlay ${isMobileMenuOpen ? 'open' : ''}`}
        onClick={() => setIsMobileMenuOpen(false)}
      ></div>

      <div className={`navbar-mobile ${isMobileMenuOpen ? 'open' : ''}`}>
        <div className="navbar-mobile-header">
          <button className="navbar-mobile-close" onClick={() => setIsMobileMenuOpen(false)}>
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M18 6L6 18M6 6l12 12" />
            </svg>
          </button>
        </div>

        <ul className="navbar-mobile-links">
          <li>
            <span className="navbar-mobile-link" onClick={() => setIsCompanyDropdownOpen((prev) => !prev)}>
              {navbar.aboutMenu}
              <svg
                className={`dropdown-arrow ${isCompanyDropdownOpen ? 'open' : ''}`}
                width="12"
                height="12"
                viewBox="0 0 12 12"
                fill="currentColor"
              >
                <path d="M6 8L2 4h8L6 8z" />
              </svg>
            </span>
            <ul className={`navbar-mobile-dropdown ${isCompanyDropdownOpen ? 'open' : ''}`}>
              <li>{renderCmsLink(navbar.aboutTeam, undefined)}</li>
              <li>{renderCmsLink(navbar.contact, undefined)}</li>
            </ul>
          </li>
          <li>{renderCmsLink(navbar.features, 'navbar-mobile-link')}</li>
          <li>{renderCmsLink(navbar.benefits, 'navbar-mobile-link')}</li>
        </ul>

        <div className="navbar-mobile-actions">
          <div className="navbar-mobile-theme">
            <span>{navbar.theme}</span>
            <ThemeToggle isDark={isDark} onToggle={toggleTheme} />
          </div>
          <LanguageSwitcher compact />
          <button
            className="navbar-btn navbar-btn-secondary navbar-btn-full"
            onClick={() => navigateByCmsLink(navbar.login.url)}
          >
            {navbar.login.label}
          </button>
          <button
            className="navbar-btn navbar-btn-primary navbar-btn-full"
            onClick={() => navigateByCmsLink(navbar.register.url)}
          >
            {navbar.register.label}
          </button>
        </div>
      </div>
    </div>
  );
};

export default Navbar;
