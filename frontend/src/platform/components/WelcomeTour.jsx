import React, { useEffect } from 'react';
import { driver } from 'driver.js';
import 'driver.js/dist/driver.css';
import { useTranslation } from 'react-i18next';
import { usePreferences } from '../context/PreferencesContext';

const WelcomeTour = () => {
  const { resolvedTheme } = usePreferences();
  const { t } = useTranslation('platform');
  const isDark = resolvedTheme === 'dark';

  useEffect(() => {
    const hasSeenTour = localStorage.getItem('collaby_has_seen_tour');
    if (hasSeenTour) {
      return undefined;
    }

    const driverObj = driver({
      showProgress: true,
      animate: true,
      allowClose: false,
      doneBtnText: t('welcomeTour.done'),
      nextBtnText: t('welcomeTour.next'),
      prevBtnText: t('welcomeTour.previous'),
      progressText: t('welcomeTour.progress'),
      popoverClass: isDark ? 'driver-popover-dark' : '',
      onDestroyed: () => {
        localStorage.setItem('collaby_has_seen_tour', 'true');
      },
      steps: [
        {
          element: 'body',
          popover: {
            title: t('welcomeTour.steps.welcome.title'),
            description: t('welcomeTour.steps.welcome.description'),
            side: 'left',
            align: 'start',
          },
        },
        {
          element: '.sidebar-nav',
          popover: {
            title: t('welcomeTour.steps.navigation.title'),
            description: t('welcomeTour.steps.navigation.description'),
            side: 'right',
            align: 'start',
          },
        },
        {
          element: '.view-switcher',
          popover: {
            title: t('welcomeTour.steps.views.title'),
            description: t('welcomeTour.steps.views.description'),
            side: 'bottom',
            align: 'start',
          },
        },
        {
          element: '.new-task-btn',
          popover: {
            title: t('welcomeTour.steps.firstTask.title'),
            description: t('welcomeTour.steps.firstTask.description'),
            side: 'bottom',
            align: 'end',
          },
        },
      ],
    });

    const timer = window.setTimeout(() => {
      driverObj.drive();
    }, 1000);

    return () => {
      window.clearTimeout(timer);
      driverObj.destroy();
    };
  }, [isDark, t]);

  return null;
};

export default WelcomeTour;
