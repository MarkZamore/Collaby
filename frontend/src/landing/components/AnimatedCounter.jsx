import React, { useState, useEffect, useRef } from 'react';
import '../styles/AnimatedCounter.css';

/**
 * AnimatedCounter — счётчик, который плавно увеличивается до целевого значения.
 * 
 * @param {number} end — конечное значение.
 * @param {number} duration — продолжительность анимации в мс.
 * @param {string} suffix — суффикс, например '+', '%' или 'K'.
 * @param {string} label — подпись под числом.
 */
const AnimatedCounter = ({ end, duration = 2000, suffix = '', label = '' }) => {
  const [count, setCount] = useState(0);
  const [isVisible, setIsVisible] = useState(false);
  const counterRef = useRef(null);

  useEffect(() => {
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting && !isVisible) {
          setIsVisible(true);
        }
      },
      { threshold: 0.3 }
    );

    const currentRef = counterRef.current;
    if (currentRef) {
      observer.observe(currentRef);
    }

    return () => {
      if (currentRef) {
        observer.unobserve(currentRef);
      }
    };
  }, [isVisible]);

  useEffect(() => {
    if (!isVisible) return;

    let startTime;
    let animationFrame;

    const animate = (timestamp) => {
      if (!startTime) startTime = timestamp;
      const progress = Math.min((timestamp - startTime) / duration, 1);

      // Используем easing-функцию, чтобы анимация выглядела мягче.
      const easeOutQuart = 1 - Math.pow(1 - progress, 4);
      
      setCount(Math.floor(end * easeOutQuart));

      if (progress < 1) {
        animationFrame = requestAnimationFrame(animate);
      }
    };

    animationFrame = requestAnimationFrame(animate);

    return () => {
      if (animationFrame) {
        cancelAnimationFrame(animationFrame);
      }
    };
  }, [isVisible, end, duration]);

  return (
    <div ref={counterRef} className="animated-counter glass-effect">
      <div className="counter-value gradient-text">
        {count}{suffix}
      </div>
      {label && <div className="counter-label">{label}</div>}
    </div>
  );
};

export default AnimatedCounter;
