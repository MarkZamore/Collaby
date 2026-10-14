import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import Header from '../components/Header';
import Footer from '../components/Footer';
import ScrollReveal from '../components/ScrollReveal';
import { usePublicPageState } from '../hooks/usePublicPageState';
import '../styles/Contact.css';

const SOCIAL_ICONS = {
  whatsapp: (
    <svg viewBox="0 0 24 24" fill="currentColor">
      <path d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 01-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 01-1.51-5.26c.001-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 012.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0012.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 005.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 00-3.48-8.413z" />
    </svg>
  ),
  telegram: (
    <svg viewBox="0 0 24 24" fill="currentColor">
      <path d="M12 0C5.373 0 0 5.373 0 12s5.373 12 12 12 12-5.373 12-12S18.627 0 12 0zm5.562 8.161c-.18 1.897-.962 6.502-1.359 8.627-.168.9-.5 1.201-.82 1.23-.697.064-1.226-.461-1.901-.903-1.056-.693-1.653-1.124-2.678-1.8-1.185-.781-.417-1.21.258-1.91.177-.184 3.247-2.977 3.307-3.23.007-.032.014-.15-.056-.212s-.174-.041-.249-.024c-.106.024-1.793 1.139-5.062 3.345-.479.329-.913.489-1.302.481-.428-.008-1.252-.241-1.865-.44-.752-.244-1.349-.374-1.297-.789.027-.216.325-.437.893-.663 3.498-1.524 5.831-2.529 6.998-3.014 3.332-1.386 4.025-1.627 4.476-1.635.099-.002.321.023.465.14.121.098.155.23.171.324.016.094.037.308.02.474z" />
    </svg>
  ),
};

const Contact = () => {
  const { t } = useTranslation(['landing', 'common']);
  const { isDark, isScrolled, toggleTheme } = usePublicPageState();
  const [formStatus, setFormStatus] = useState('');
  const [formData, setFormData] = useState({
    name: '',
    email: '',
    subject: '',
    message: '',
  });

  const contact = t('landing:contact', { returnObjects: true });
  const contactInfo = Array.isArray(contact.info) ? contact.info : [];
  const faqItems = Array.isArray(contact.faq) ? contact.faq : [];
  const socialLinks = Array.isArray(contact.socialLinks) ? contact.socialLinks : [];

  useEffect(() => {
    window.scrollTo(0, 0);
  }, []);

  const handleChange = (event) => {
    const { name, value } = event.target;
    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    setFormStatus(contact.demoStatus);
    setFormData({
      name: '',
      email: '',
      subject: '',
      message: '',
    });

    window.setTimeout(() => {
      setFormStatus('');
    }, 5000);
  };

  return (
    <div className="contact-page">
      <div className="liquid-shapes">
        <div className="liquid-shape shape-1" />
        <div className="liquid-shape shape-2" />
        <div className="liquid-shape shape-3" />
      </div>

      <Header isDark={isDark} toggleTheme={toggleTheme} isScrolled={isScrolled} />

      <ScrollReveal animation="fade" duration={1000}>
        <section className="contact-hero">
          <div className="container">
            <div className="contact-hero-content">
              <div className="badge glass-effect">
                <span className="badge-icon" aria-hidden="true">
                  ✉
                </span>
                {contact.badge}
              </div>
              <h1 className="contact-hero-title">{contact.title}</h1>
              <p className="contact-hero-description">{contact.description}</p>
            </div>
          </div>
        </section>
      </ScrollReveal>

      <ScrollReveal animation="slide-up" delay={100}>
        <section className="contact-info-section">
          <div className="container">
            <div className="contact-info-grid">
              {contactInfo.map((item) => (
                <div key={item.title} className="contact-info-card glass-effect">
                  <div className="info-icon">{item.icon}</div>
                  <h3 className="info-title">{item.title}</h3>
                  {item.url ? (
                    <a
                      href={item.url}
                      className="info-content"
                      target={item.url.startsWith('http') ? '_blank' : undefined}
                      rel={item.url.startsWith('http') ? 'noopener noreferrer' : undefined}
                    >
                      {item.info}
                    </a>
                  ) : (
                    <p className="info-content">{item.info}</p>
                  )}
                </div>
              ))}
            </div>
          </div>
        </section>
      </ScrollReveal>

      <ScrollReveal animation="slide-up" delay={150}>
        <section className="contact-form-section">
          <div className="container">
            <div className="contact-form-wrapper">
              <div className="form-container glass-effect">
                <h2 className="form-title">{contact.formTitle}</h2>
                <p className="form-description">{contact.formDescription}</p>

                {formStatus && <div className="form-status glass-effect success">{formStatus}</div>}

                <form onSubmit={handleSubmit} className="contact-form">
                  <div className="form-group">
                    <label htmlFor="name">{t('common:labels.name')} *</label>
                    <input
                      type="text"
                      id="name"
                      name="name"
                      value={formData.name}
                      onChange={handleChange}
                      required
                      placeholder={contact.form.namePlaceholder}
                      className="form-input"
                    />
                  </div>

                  <div className="form-group">
                    <label htmlFor="email">{t('common:labels.email')} *</label>
                    <input
                      type="email"
                      id="email"
                      name="email"
                      value={formData.email}
                      onChange={handleChange}
                      required
                      placeholder={contact.form.emailPlaceholder}
                      className="form-input"
                    />
                  </div>

                  <div className="form-group">
                    <label htmlFor="subject">{t('common:labels.subject')}</label>
                    <input
                      type="text"
                      id="subject"
                      name="subject"
                      value={formData.subject}
                      onChange={handleChange}
                      placeholder={contact.form.subjectPlaceholder}
                      className="form-input"
                    />
                  </div>

                  <div className="form-group">
                    <label htmlFor="message">{t('common:labels.message')} *</label>
                    <textarea
                      id="message"
                      name="message"
                      value={formData.message}
                      onChange={handleChange}
                      required
                      rows="6"
                      placeholder={contact.form.messagePlaceholder}
                      className="form-input form-textarea"
                    />
                  </div>

                  <button type="submit" className="btn-primary btn-large btn-full">
                    <span>{t('common:actions.sendDemo')}</span>
                    <span className="btn-icon">{'->'}</span>
                  </button>
                </form>
              </div>

              <div className="contact-sidebar">
                <div className="sidebar-card glass-effect">
                  <h3 className="sidebar-title">{contact.realLinksTitle}</h3>
                  <p className="sidebar-description">{contact.realLinksDescription}</p>
                  <div className="social-links">
                    {socialLinks.map((social) => (
                      <a
                        key={social.key}
                        href={social.url}
                        className="social-link-large"
                        target="_blank"
                        rel="noopener noreferrer"
                        aria-label={social.label}
                      >
                        <div className="social-icon">{SOCIAL_ICONS[social.key] || '->'}</div>
                        <span className="social-name">{social.label}</span>
                      </a>
                    ))}
                  </div>
                </div>

                <div className="sidebar-card glass-effect">
                  <h3 className="sidebar-title">{contact.importantTitle}</h3>
                  <div className="faq-list">
                    {faqItems.map((item) => (
                      <div key={item.question} className="faq-item-small">
                        <h4 className="faq-question-small">{item.question}</h4>
                        <p className="faq-answer-small">{item.answer}</p>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>
      </ScrollReveal>

      <Footer />
    </div>
  );
};

export default Contact;
