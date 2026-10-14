import { useEffect, useRef, useState } from 'react';
import '../styles/ScrollReveal.css';

/**
 * ScrollReveal — компонент для анимации элементов при прокрутке.
 * 
 * @param {ReactNode} children — дочерние элементы.
 * @param {string} animation — тип анимации: 'fade', 'slide-up', 'slide-left', 'slide-right', 'scale', 'rotate'.
 * @param {number} delay — задержка анимации в мс.
 * @param {number} duration — длительность анимации в мс.
 * @param {number} threshold — порог видимости для запуска.
 */
const ScrollReveal = ({ 
  children, 
  animation = 'fade',
  delay = 0,
  duration = 600,
  threshold = 0.1,
  className = ''
}) => {
  const [isVisible, setIsVisible] = useState(false);
  const elementRef = useRef(null);

  useEffect(() => {
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setIsVisible(true);
          // Отключаем наблюдение после появления
          if (elementRef.current) {
            observer.unobserve(elementRef.current);
          }
        }
      },
      { threshold, rootMargin: '0px 0px -50px 0px' }
    );

    const currentElement = elementRef.current;
    if (currentElement) {
      observer.observe(currentElement);
    }

    return () => {
      if (currentElement) {
        observer.unobserve(currentElement);
      }
    };
  }, [threshold]);

  return (
    <div
      ref={elementRef}
      className={`scroll-reveal scroll-reveal-${animation} ${isVisible ? 'is-visible' : ''} ${className}`}
      style={{
        '--reveal-delay': `${delay}ms`,
        '--reveal-duration': `${duration}ms`
      }}
    >
      {children}
    </div>
  );
};

export default ScrollReveal;
