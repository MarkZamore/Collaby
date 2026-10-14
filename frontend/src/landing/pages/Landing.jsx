import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import AnimatedCounter from '../components/AnimatedCounter';
import ScrollReveal from '../components/ScrollReveal';
import Header from '../components/Header';
import Footer from '../components/Footer';
import { usePublicPageState } from '../hooks/usePublicPageState';
import '../styles/Landing.css';

const SectionDivider = () => (
  <div className="section-divider">
    <div className="section-divider__line" />
  </div>
);

const Landing = () => {
  const navigate = useNavigate();
  const { t } = useTranslation(['landing', 'common']);
  const { isDark, isScrolled, toggleTheme } = usePublicPageState();
  const [openFaq, setOpenFaq] = useState(null);

  const home = t('landing:home', { returnObjects: true });
  const features = Array.isArray(home.features) ? home.features : [];
  const benefits = Array.isArray(home.benefits) ? home.benefits : [];
  const faqItems = Array.isArray(home.faq) ? home.faq : [];
  const appName = t('common:appName');
  const localizedHeroTitle = home.title || '';
  const hasEmbeddedAppName = localizedHeroTitle.includes(appName);
  const [heroTitlePrefix, heroTitleSuffix = ''] = hasEmbeddedAppName
    ? localizedHeroTitle.split(appName)
    : [localizedHeroTitle, ''];

  const handleCmsLink = (url) => {
    if (!url) return;
    if (url.startsWith('http://') || url.startsWith('https://')) {
      window.open(url, '_blank', 'noopener,noreferrer');
      return;
    }
    navigate(url);
  };

  const stats = [
    { end: 3, label: home.stats.team },
    { end: 5, label: home.stats.modules },
    { end: 9, label: home.stats.months },
  ];

  return (
    <div className="landing">
      <Header isDark={isDark} toggleTheme={toggleTheme} isScrolled={isScrolled} />

      {/* Hero — Centered */}
      <ScrollReveal animation="fade" duration={600}>
        <section className="hero">
          <div className="container">
            <div className="hero-content">
              <div className="hero-text">
                <div className="badge">
                  <span className="badge-icon" aria-hidden="true">&#10022;</span>
                  {home.badge}
                </div>
                <h1 className="hero-title">
                  {hasEmbeddedAppName ? (
                    <>
                      {heroTitlePrefix}
                      <span className="gradient-text">{appName}</span>
                      {heroTitleSuffix}
                    </>
                  ) : (
                    heroTitlePrefix
                  )}
                </h1>
                <p className="hero-description">{home.description}</p>
                <div className="hero-actions">
                  <button
                    className="btn-primary btn-large"
                    onClick={() => handleCmsLink(home.primaryCta.url)}
                  >
                    <span>{home.primaryCta.label}</span>
                    <span className="btn-icon">&rarr;</span>
                  </button>
                  <button
                    className="btn-outline btn-large"
                    onClick={() => handleCmsLink(home.secondaryCta.url)}
                  >
                    <span>{home.secondaryCta.label}</span>
                  </button>
                </div>
                <div className="hero-stats">
                  {stats.map((stat) => (
                    <AnimatedCounter key={stat.label} end={stat.end} suffix="" label={stat.label} duration={2200} />
                  ))}
                </div>
              </div>
            </div>
          </div>
        </section>
      </ScrollReveal>

      <SectionDivider />

      {/* Features */}
      <ScrollReveal animation="slide-up" delay={100} duration={500}>
        <section id="features" className="features">
          <div className="container">
            <div className="section-header">
              <h2 className="section-title">{home.featuresTitle}</h2>
              <p className="section-description">{home.featuresDescription}</p>
            </div>
            <div className="features-grid">
              {features.map((feature) => (
                <div key={feature.title} className="feature-card">
                  <div className="feature-icon">{feature.icon || '&#9679;'}</div>
                  <h3 className="feature-title">{feature.title}</h3>
                  <p className="feature-description">{feature.description}</p>
                </div>
              ))}
            </div>
          </div>
        </section>
      </ScrollReveal>

      <SectionDivider />

      {/* Benefits */}
      <ScrollReveal animation="slide-up" delay={100} duration={500}>
        <section id="benefits" className="benefits">
          <div className="container">
            <div className="benefits-content">
              <div className="benefits-text">
                <h2 className="section-title">{home.benefitsTitle}</h2>
                <div className="benefits-list">
                  {benefits.map((benefit) => (
                    <div key={benefit.title} className="benefit-item">
                      <div className="benefit-icon">{benefit.icon || '+'}</div>
                      <div className="benefit-text">
                        <h3>{benefit.title}</h3>
                        <p>{benefit.description}</p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </section>
      </ScrollReveal>

      <SectionDivider />

      {/* FAQ — Accordion */}
      <ScrollReveal animation="slide-up" delay={100} duration={500}>
        <section className="faq">
          <div className="container">
            <div className="section-header">
              <h2 className="section-title">{home.faqTitle}</h2>
              <p className="section-description">{home.faqDescription}</p>
            </div>
            <div className="faq-list">
              {faqItems.map((item, index) => (
                <div
                  key={item.question}
                  className={`faq-item${openFaq === index ? ' faq-open' : ''}`}
                  onClick={() => setOpenFaq(openFaq === index ? null : index)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      setOpenFaq(openFaq === index ? null : index);
                    }
                  }}
                >
                  <h3 className="faq-question">
                    <span>{item.question}</span>
                    <span className="faq-toggle">+</span>
                  </h3>
                  <p className="faq-answer">{item.answer}</p>
                </div>
              ))}
            </div>
          </div>
        </section>
      </ScrollReveal>

      {/* CTA */}
      <ScrollReveal animation="fade" duration={600}>
        <section className="landing-cta">
          <div className="container">
            <div className="cta-content">
              <h2 className="cta-title">
                {home.primaryCta.label.includes('free')
                  ? home.primaryCta.label.replace('free', '').trim()
                  : home.primaryCta.label}
              </h2>
              <p className="cta-description">{home.description}</p>
              <div className="cta-actions">
                <button
                  className="btn-primary btn-large"
                  onClick={() => handleCmsLink(home.primaryCta.url)}
                >
                  <span>{home.primaryCta.label}</span>
                  <span className="btn-icon">&rarr;</span>
                </button>
              </div>
            </div>
          </div>
        </section>
      </ScrollReveal>

      <Footer />
    </div>
  );
};

export default Landing;
