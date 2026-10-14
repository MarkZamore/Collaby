import React from 'react';
import Navbar from './Navbar';
import '../styles/Header.css';

const Header = ({ isDark, toggleTheme, isScrolled }) => {
  return (
    <header className={`header ${isScrolled ? 'scrolled' : ''}`}>
      <Navbar isDark={isDark} toggleTheme={toggleTheme} isScrolled={isScrolled} />
    </header>
  );
};

export default Header;
