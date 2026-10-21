/* Real server-side DataTables: SQL is authoritative; never download an entire register. */
(() => {
  'use strict';
  const states = new Map();
  const locale = () => document.documentElement.dataset.language || 'en';
  const t = key => locale() === 'en' ? key : window.FarmSphereTranslations?.[key]?.[locale()] || key;
  const text = value => { const node = document.createElement('span'); node.textContent = value == null ? '' : String(value); return node; };
  function labels() {
    return { search: t('Search') + ':', searchPlaceholder: t('Search up to 120 characters'), lengthMenu: t('Rows per page') + ' _MENU_',
      info: t('Showing _START_ to _END_ of _TOTAL_ records'), infoEmpty: t('No records to display'),
      infoFiltered: t('(filtered from _MAX_ records)'), zeroRecords: t('No matching records'), emptyTable: t('No records to display'),
      loadingRecords: t('Loading records'), processing: t('Loading records'),
      paginate: { first: t('First'), previous: t('Previous'), next: t('Next'), last: t('Last') } };
  }
  function start(panel) {
    if (states.has(panel) || !window.DataTable) return;
    const enclosing = panel.closest('details'); if (enclosing && !enclosing.open) return;
    const state = { controller: null, table: null, request: 0, disposed: false }; states.set(panel, state);
    const feedback = panel.querySelector('.grid-feedback');
    const renderer = (value, type) => type === 'display' ? text(value) : value;
    state.table = new DataTable(panel.querySelector('table'), {
      serverSide: true, processing: true, deferRender: true, searchDelay: 350,
      pageLength: 25, lengthMenu: [10, 25, 50, 100], order: [[0, 'desc']], orderMulti: false,
      language: labels(),
      ajax: async (request, callback) => {
        state.controller?.abort(); const controller = new AbortController(); state.controller = controller;
        const serial = ++state.request; feedback.textContent = '';
        const query = new URLSearchParams({ draw: String(request.draw), start: String(request.start), length: String(request.length),
          'search[value]': request.search.value, 'search[regex]': 'false',
          'order[0][column]': String(request.order[0]?.column ?? 0), 'order[0][dir]': request.order[0]?.dir ?? 'desc',
          status: panel.querySelector('.grid-status').value, language: locale() });
        try {
          const response = await fetch(panel.dataset.gridUrl + '?' + query, { credentials: 'same-origin', cache: 'no-store', signal: controller.signal, headers: { Accept: 'application/json' } });
          if (!response.ok) throw new Error(response.status === 401 || response.status === 403 ? 'Your session or access changed. Reload and sign in again.' : response.status === 429 ? 'Too many table requests. Wait a minute and try again.' : 'The table could not be loaded. Use the existing workflow or retry.');
          const page = await response.json();
          if (!Number.isInteger(page.draw) || !Number.isInteger(page.recordsTotal) || !Number.isInteger(page.recordsFiltered) || !Array.isArray(page.data) || page.data.length > 100) throw new Error('The table could not be loaded. Use the existing workflow or retry.');
          if (serial === state.request && !state.disposed) callback(page);
        } catch (error) {
          if (error.name === 'AbortError' || serial !== state.request || state.disposed) return;
          feedback.textContent = t(error.message);
          callback({ draw: request.draw, recordsTotal: 0, recordsFiltered: 0, data: [] });
        }
      },
      columns: [
        { data: 'id', render: renderer }, { data: 'reference', render: renderer }, { data: 'description', render: renderer },
        { data: 'status', render: renderer }, { data: 'date', orderable: false, render: renderer },
        { data: 'value', orderable: false, render: renderer },
        { data: 'url', orderable: false, searchable: false, render: (value, type) => {
          if (type !== 'display') return '';
          const link = document.createElement('a'); link.textContent = t('Open');
          // Only local relative application paths may be used, even if a cached payload is damaged.
          if (typeof value === 'string' && /^\/(?!\/)[A-Za-z0-9/_-]*$/.test(value)) link.href = value;
          return link;
        } }
      ]
    });
    const search = panel.querySelector('input[type="search"]'); if (search) search.maxLength = 120;
    panel.querySelector('.grid-filter').onclick = () => state.table.ajax.reload();
  }
  document.querySelectorAll('.server-grid').forEach(start);
  document.querySelectorAll('details.server-grid-panel').forEach(details => details.addEventListener('toggle', () => { if (details.open) details.querySelectorAll('.server-grid').forEach(start); }));
  document.addEventListener('farmsphere:language', () => {
    for (const [panel, state] of [...states]) {
      state.disposed = true; state.controller?.abort(); state.table.destroy(); states.delete(panel);
      start(panel);
    }
  });
})();
