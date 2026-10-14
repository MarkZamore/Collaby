import React, { useEffect, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import Header from '../components/Header';
import Footer from '../components/Footer';
import ScrollReveal from '../components/ScrollReveal';
import AnimatedCounter from '../components/AnimatedCounter';
import LandingMedia from '../components/LandingMedia';
import { usePublicPageState } from '../hooks/usePublicPageState';
import '../styles/About.css';

const TEAM_VISUALS = [
  {
    gradient: 'linear-gradient(135deg, #FF6B35, #FF8C42)',
    avatar: 'https://avatars.githubusercontent.com/u/187307610?v=4',
    github: 'https://github.com/TemirlanOspanov',
  },
  {
    gradient: 'linear-gradient(135deg, #FF8C42, #FFA056)',
    avatar: 'https://avatars.githubusercontent.com/u/167680253?v=4',
    github: 'https://github.com/Dildar05',
  },
  {
    gradient: 'linear-gradient(135deg, #FFA056, #FFB870)',
    avatar: 'https://avatars.githubusercontent.com/u/187437301?v=4',
    github: 'https://github.com/OscarSalnikov',
  },
];

const About = () => {
  const navigate = useNavigate();
  const { t } = useTranslation(['landing', 'common']);
  const { isDark, isScrolled, toggleTheme } = usePublicPageState();
  const about = t('landing:about', { returnObjects: true });

  const values = Array.isArray(about.values) ? about.values : [];
  const story = Array.isArray(about.story) ? about.story : [];
  const team = Array.isArray(about.team) ? about.team : [];

  useEffect(() => {
    window.scrollTo(0, 0);
  }, []);

  const openCmsLink = (url) => {
    if (!url) {
      return;
    }

    if (url.startsWith('http://') || url.startsWith('https://')) {
      window.open(url, '_blank', 'noopener,noreferrer');
      return;
    }

    navigate(url);
  };

  const stats = useMemo(
    () => [
      { end: 3, label: about.stats.team },
      { end: 5, label: about.stats.scenarios },
      { end: 9, label: about.stats.months },
      { end: 1, label: about.stats.goal },
    ],
    [about.stats.goal, about.stats.months, about.stats.scenarios, about.stats.team]
  );

  const titleSegments = (about.title || '').split('Collaby');

  return (
    <div className="about-page">
      <div className="liquid-shapes">
        <div className="liquid-shape shape-1" />
        <div className="liquid-shape shape-2" />
        <div className="liquid-shape shape-3" />
      </div>

      <Header isDark={isDark} toggleTheme={toggleTheme} isScrolled={isScrolled} />

      <ScrollReveal animation="fade" duration={1000}>
        <section className="about-hero">
          <div className="container">
            <div className="about-hero-content">
              <div className="badge glass-effect">
                <span className="badge-icon" aria-hidden="true">
                  ✦
                </span>
                {about.badge}
              </div>
              <h1 className="about-hero-title">
                {titleSegments[0]}
                <span className="gradient-text">Collaby</span>
                {titleSegments[1] || ''}
              </h1>
              <p className="about-hero-description">{about.description}</p>
              <div className="about-hero-stats">
                {stats.map((stat) => (
                  <AnimatedCounter key={stat.label} end={stat.end} suffix="" label={stat.label} duration={2200} />
                ))}
              </div>
            </div>
          </div>
        </section>
      </ScrollReveal>

      <ScrollReveal animation="slide-up" delay={200}>
        <section className="mission-vision">
          <div className="container">
            <div className="mission-vision-grid">
              <div className="mission-card glass-effect">
                <div className="mission-icon" aria-hidden="true">
                  🧭
                </div>
                <h2 className="mission-title">{about.missionTitle}</h2>
                <p className="mission-description">{about.missionDescription}</p>
              </div>
              <div className="vision-card glass-effect">
                <div className="vision-icon" aria-hidden="true">
                  🔭
                </div>
                <h2 className="vision-title">{about.visionTitle}</h2>
                <p className="vision-description">{about.visionDescription}</p>
              </div>
            </div>
          </div>
        </section>
      </ScrollReveal>

      <ScrollReveal animation="slide-up" delay={100}>
        <section className="values">
          <div className="container">
            <div className="section-header">
              <h2 className="section-title">{about.valuesTitle}</h2>
              <p className="section-description">{about.valuesDescription}</p>
            </div>
            <div className="values-grid">
              {values.map((value) => (
                <div key={value.title} className="value-card glass-effect">
                  <div className="value-icon">{value.icon}</div>
                  <h3 className="value-title">{value.title}</h3>
                  <p className="value-description">{value.description}</p>
                </div>
              ))}
            </div>
          </div>
        </section>
      </ScrollReveal>

      <ScrollReveal animation="slide-left" delay={100}>
        <section className="story">
          <div className="container">
            <div className="story-content">
              <div className="story-text">
                <h2 className="section-title">{about.storyTitle}</h2>
                {story.map((paragraph) => (
                  <p key={paragraph} className="story-paragraph">
                    {paragraph}
                  </p>
                ))}
                <div className="story-quotes">
                  {(Array.isArray(about.quotes) ? about.quotes : []).map((q) => (
                    <div key={q.author} className="story-quote glass-effect">
                      <p className="quote-text">"{q.text}"</p>
                      <p className="quote-author">— {q.author}, <span className="quote-role">{q.role}</span></p>
                    </div>
                  ))}
                </div>
              </div>
              <div className="story-visual">
                <div className="story-image-card glass-effect">
                  <LandingMedia
                    asset={about.storyMedia}
                    fallbackType="team"
                    width="100%"
                    height="500px"
                    imageClassName="landing-media-image"
                    captionClassName="screenshot-label"
                  />
                </div>
              </div>
            </div>
          </div>
        </section>
      </ScrollReveal>

      <ScrollReveal animation="scale" delay={150}>
        <section className="team">
          <div className="container">
            <div className="section-header">
              <h2 className="section-title">{about.teamTitle}</h2>
              <p className="section-description">{about.teamDescription}</p>
            </div>
            <div className="team-grid">
              {team.map((member, index) => {
                const visual = TEAM_VISUALS[index] || TEAM_VISUALS[0];
                const avatarUrl = member.avatarUrl || visual.avatar;
                const profileUrl = member.profileUrl || visual.github;
                return (
                  <div key={member.name} className="team-card glass-effect">
                    <div
                      className="team-avatar"
                      style={{
                        background: avatarUrl ? 'none' : visual.gradient,
                        backgroundImage: avatarUrl ? `url(${avatarUrl})` : 'none',
                        backgroundSize: 'cover',
                        backgroundPosition: 'center',
                      }}
                    >
                      {!avatarUrl ? member.initials : null}
                    </div>
                    <h3 className="team-name">{member.name}</h3>
                    <p className="team-position">{member.position}</p>
                    <p className="team-description">{member.description}</p>
                    <div className="team-social">
                      <a
                        href={profileUrl}
                        className="team-social-link"
                        aria-label={member.name}
                        target="_blank"
                        rel="noopener noreferrer"
                      >
                        <svg viewBox="0 0 24 24" fill="currentColor">
                          <path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0024 12c0-6.63-5.37-12-12-12z" />
                        </svg>
                      </a>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </section>
      </ScrollReveal>

      <ScrollReveal animation="scale" delay={100}>
        <section className="about-cta">
          <div className="container">
            <div className="about-cta-content glass-effect">
              <h2 className="about-cta-title">{about.ctaTitle}</h2>
              <p className="about-cta-description">{about.ctaDescription}</p>
              <div className="about-cta-actions">
                <button
                  type="button"
                  className="btn-primary btn-large"
                  onClick={() => openCmsLink(about.primaryCta.url)}
                >
                  <span>{about.primaryCta.label}</span>
                  <span className="btn-icon">{'->'}</span>
                </button>
                <button
                  type="button"
                  className="btn-outline btn-large"
                  onClick={() => openCmsLink(about.secondaryCta.url)}
                >
                  <span>{about.secondaryCta.label}</span>
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

export default About;
