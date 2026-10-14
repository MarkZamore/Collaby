import React, { createContext, useState, useContext } from 'react';

const FirstWorkspaceModalContext = createContext();

export const FirstWorkspaceModalProvider = ({ children }) => {
  const [isOpen, setIsOpen] = useState(false); // Start closed; Dashboard opens it after bootstrap if needed

  return (
    <FirstWorkspaceModalContext.Provider value={{ isOpen, setIsOpen }}>
      {children}
    </FirstWorkspaceModalContext.Provider>
  );
};

export const useFirstWorkspaceModal = () => {
  const context = useContext(FirstWorkspaceModalContext);
  if (!context) {
    throw new Error('useFirstWorkspaceModal must be used within FirstWorkspaceModalProvider');
  }
  return context;
};
