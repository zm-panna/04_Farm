(() => {
  'use strict';
  const toggle = document.getElementById('nav-toggle');
  const shell = document.getElementById('shell');
  if (!toggle || !shell) return;
  toggle.addEventListener('click', () => {
    toggle.setAttribute('aria-expanded', String(shell.classList.toggle('nav-open')));
  });
  document.querySelectorAll('.side nav a').forEach(link => link.addEventListener('click', () => {
    shell.classList.remove('nav-open');
    toggle.setAttribute('aria-expanded', 'false');
  }));
})();
