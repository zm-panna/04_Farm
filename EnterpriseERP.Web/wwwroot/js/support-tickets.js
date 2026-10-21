(() => {
 'use strict';
 const form = document.querySelector('#newTicket');
 const list = document.querySelector('#tickets');
 const message = document.querySelector('#supportMessage');
 const status = document.querySelector('#status');
 const previous = document.querySelector('#previousTickets');
 const next = document.querySelector('#nextTickets');
 const pageLabel = document.querySelector('#ticketPage');
 const refresh = document.querySelector('#refresh');
 if (!list || !message || !status || !previous || !next || !pageLabel || !refresh) return;
 let page = 1, pending, currentData = null;
 const text = key => window.FarmSphereLanguage?.translate(key) || key;
 const translated = key => { const span = document.createElement('span'); span.dataset.i18n = key; span.textContent = text(key); return span; };
 const number = value => { const span = document.createElement('bdi'); span.textContent = String(value); return span; };
 const notice = (target, key, detail) => target.replaceChildren(translated(key), ...(detail ? [document.createTextNode(' ' + detail)] : []));
 const knownEnums = new Set(['Low', 'Normal', 'High', 'Critical', 'Open', 'InProgress', 'WaitingCustomer', 'Resolved', 'Closed']);
 function enumLabel(value) { if (knownEnums.has(value)) return translated(value); const span = document.createElement('span'); span.textContent = value ?? ''; return span; }
 function render(data) {
  list.replaceChildren();
  for (const ticket of data.rows || []) {
   const row = document.createElement('article'); row.className = 'ticket-row';
   const link = document.createElement('a'); link.href = '/support/tickets/' + encodeURIComponent(ticket.id);
   const title = document.createElement('strong'); title.textContent = ticket.ticketNumber ?? ''; link.append(title);
   const detail = document.createElement('small'); detail.append(enumLabel(ticket.priority), document.createTextNode(' · '), enumLabel(ticket.status));
   // Ticket subjects, numbers and other customer text are never interpreted as HTML or translation keys.
   row.append(link, document.createTextNode(' — ' + (ticket.subject ?? '') + ' '), detail); list.append(row);
  }
  if (!data.rows?.length) notice(list, 'No tickets found');
  const total = Math.max(0, Number(data.total) || 0);
  pageLabel.replaceChildren(translated('Page'), document.createTextNode(' '), number(page), document.createTextNode(' / '), number(Math.max(1, Math.ceil(total / 25))), document.createTextNode(' ('), number(total), document.createTextNode(' '), translated('tickets'), document.createTextNode(')'));
  previous.disabled = page <= 1; next.disabled = page * 25 >= total;
 }
 async function load() {
  pending?.abort();
  const request = new AbortController(); pending = request; currentData = null;
  notice(list, 'Loading...'); list.setAttribute('aria-busy', 'true'); previous.disabled = true; next.disabled = true; pageLabel.replaceChildren();
  try {
   const response = await fetch('/api/support/tickets?status=' + encodeURIComponent(status.value) + '&page=' + page, { credentials: 'same-origin', signal: request.signal });
   if (!response.ok) throw Error('HTTP ' + response.status);
   const data = await response.json();
   if (pending !== request) return;
   currentData = data; render(data);
  } catch (error) {
   if (pending === request && error.name !== 'AbortError') notice(list, 'Unable to load tickets.', error.message);
  } finally { if (pending === request) list.setAttribute('aria-busy', 'false'); }
 }
 form?.addEventListener('submit', async event => {
  event.preventDefault(); message.replaceChildren();
  const submit = form.querySelector('button[type="submit"]');
  if (!submit || submit.disabled) return;
  submit.disabled = true;
  const data = Object.fromEntries(new FormData(form)); delete data.__RequestVerificationToken;
  try {
   const response = await fetch('/api/support/tickets', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': form.querySelector('[name="__RequestVerificationToken"]').value }, body: JSON.stringify(data) });
   if (!response.ok) { const problem = await response.json().catch(() => ({})); throw Error(problem.detail || 'HTTP ' + response.status); }
   const created = await response.json();
   form.reset(); notice(message, 'Ticket created.');
   const link = document.createElement('a'); link.href = '/support/tickets/' + encodeURIComponent(created.id); link.append(translated('Open ticket')); message.append(document.createTextNode(' '), link);
   page = 1; status.value = ''; await load();
  } catch (error) { notice(message, 'Unable to create ticket.', window.FarmSphereLanguage?.validation(error.message) || error.message); }
  finally { submit.disabled = false; }
 });
 status.addEventListener('change', () => { page = 1; return load(); });
 previous.addEventListener('click', () => { if (!previous.disabled && page > 1) { page--; return load(); } });
 next.addEventListener('click', () => { if (!next.disabled) { page++; return load(); } });
 refresh.addEventListener('click', load);
 document.addEventListener('farmsphere:language', () => { if (currentData) render(currentData); });
 load();
})();
