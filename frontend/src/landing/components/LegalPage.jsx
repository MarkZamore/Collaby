import React, { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import Header from './Header';
import Footer from './Footer';
import { usePublicPageState } from '../hooks/usePublicPageState';

const LegalPage = ({ pageKey, className, containerClassName, headerClassName, titleClassName, dateClassName, contentClassName, sectionClassName, contactClassName }) => {
  const { t } = useTranslation('legal');
  const { isDark, isScrolled, toggleTheme } = usePublicPageState();
  const page = t(`${pageKey}`, { returnObjects: true });

  useEffect(() => {
    window.scrollTo(0, 0);
  }, []);

  return (
    <div className={`${className} ${isDark ? 'dark' : ''}`}>
      <Header isDark={isDark} toggleTheme={toggleTheme} isScrolled={isScrolled} />

      <div className={containerClassName}>
        <div className={headerClassName}>
          <h1 className={titleClassName}>{page.title}</h1>
          <p className={dateClassName}>{page.updated}</p>
        </div>

        <div className={contentClassName}>
          {page.sections.map((section) => (
            <div key={section.title} className={sectionClassName}>
              <h2>{section.title}</h2>
              {section.body?.map((paragraph) => (
                <p key={paragraph}>{paragraph}</p>
              ))}
              {section.list ? (
                <ul>
                  {section.list.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              ) : null}
              {section.links ? (
                <div className={contactClassName}>
                  {section.links.map((link) => (
                    <p key={link.href}>
                      {link.label}: <a href={link.href} target={link.href.startsWith('http') ? '_blank' : undefined} rel={link.href.startsWith('http') ? 'noopener noreferrer' : undefined}>{link.text}</a>
                    </p>
                  ))}
                </div>
              ) : null}
            </div>
          ))}
        </div>
      </div>

      <Footer />
    </div>
  );
};

export default LegalPage;
