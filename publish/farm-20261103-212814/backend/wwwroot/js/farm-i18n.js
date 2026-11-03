/* Display localization only. The server remains authoritative for permissions, dates, quantities and posting. */
(() => {
  'use strict';
  const dictionary = window.FarmSphereTranslations || {};
  const supported = ['en', 'bn', 'banglish', 'ar'];
  const key = 'farmsphere-language';
  let language = 'en';
  const originals = new WeakMap();
  function savedLanguage() {
    try { const value = localStorage.getItem(key); if (supported.includes(value)) return value; } catch (_) { }
    try { const value = document.cookie.split('; ').find(item => item.startsWith(key + '='))?.split('=')[1]; if (supported.includes(value)) return value; } catch (_) { }
    return 'en';
  }
  function translate(text) { return language === 'en' ? text : dictionary[text]?.[language] || text; }
  // Only application-owned enum columns are marked. Names, references and arbitrary text never use this path.
  const enums = {
    tax: ['Standard', 'Exempt', 'OutOfScope'],
    asset: ['Draft', 'Approved', 'Active', 'Disposed', 'Cancelled', 'Posted'],
    document: ['Purchase', 'Sale', 'AnimalSale', 'AnimalPurchase'], order: ['Draft', 'Approved', 'PartiallyReceived', 'Received', 'Cancelled'],
    party: ['Supplier', 'Customer'], unassigned: ['Unassigned'], payment: ['Supplier', 'Customer', 'SupplierRefund', 'CustomerRefund'],
    trigger: ['Manual', 'LowStock', 'HealthDue'], action: ['InAppAlert'],
    job: ['Pending', 'Rejected', 'ReviewRequired', 'Completed'], approval: ['Pending', 'Approved'], subscription: ['Current', 'Suspended', 'Alert', 'Notice'],
    cycle: ['Bred', 'Pregnant', 'NotPregnant', 'Calved', 'Aborted'], balance: ['refund', 'due'],
    milk: ['Morning', 'Evening'], ticket: ['Open', 'InProgress', 'WaitingCustomer', 'Resolved', 'Closed'],
    priority: ['Low', 'Normal', 'High', 'Critical'], activity: ['Created', 'Updated', 'Reopened', 'StatusChanged']
  };
  function enumText(group, value) { return Object.hasOwn(enums, group) && enums[group].includes(value) ? translate(value) : value; }
  const templates = {
    'ecommerce-tax-policy': { text: '{code} v{version} · {rate}% · {treatment}', fields: ['code', 'version', 'rate', 'treatment'], enums: { treatment: 'tax' } },
    'stock-availability': { text: '{sku} — {name} (available {quantity})', fields: ['sku', 'name', 'quantity'] },
    'feed-availability': { text: '{sku} - {name} ({quantity} available)', fields: ['sku', 'name', 'quantity'] },
    'original-document': { text: '{reference} — original {amount}', fields: ['reference', 'amount'] },
    'document-settlement': { text: '{reference} ({kind}, {party}) — {amount} {balance}', fields: ['reference', 'kind', 'party', 'amount', 'balance'], enums: { kind: 'document', balance: 'balance' } },
    'unassigned-settlement': { text: '{reference} ({kind}, {party}) — {amount} {balance}', fields: ['reference', 'kind', 'party', 'amount', 'balance'], enums: { kind: 'document', party: 'unassigned', balance: 'balance' } },
    'invoice-balance': { text: '{reference} - {amount} due', fields: ['reference', 'amount'] },
    'breeding-cycle': { text: '{tag} · {reference} ({status})', fields: ['tag', 'reference', 'status'], enums: { status: 'cycle' } },
    'automation-rule': { text: '{name} ({trigger})', fields: ['name', 'trigger'], enums: { trigger: 'trigger' } },
    'approval-request': { text: '#{id} · rule {rule} · requester {requester}', fields: ['id', 'rule', 'requester'] },
    'support-transition': { text: '{previous} → {next}: {note}', fields: ['previous', 'next', 'note'], enums: { previous: 'ticket', next: 'ticket' } }
  };
  function formatTemplate(name, values) {
    if (!Object.hasOwn(templates, name) || !values || typeof values !== 'object' || Array.isArray(values)) return null;
    const template = templates[name];
    if (Object.keys(values).length !== template.fields.length || !template.fields.every(key => Object.hasOwn(values, key) && typeof values[key] === 'string')) return null;
    // A malformed translation cannot drop/duplicate a record value; fall back to the source template.
    let display = translate(template.text);
    const tokens = [...display.matchAll(/\{([a-z]+)\}/g)].map(match => match[1]);
    if (tokens.length !== template.fields.length || !template.fields.every(key => tokens.filter(token => token === key).length === 1)) display = template.text;
    // One replacement pass; literal placeholders/HTML in user data remain literal text. Never use innerHTML.
    return display.replace(/\{([a-z]+)\}/g, (_, key) =>
      template.enums?.[key] ? enumText(template.enums[key], values[key]) : values[key]);
  }
  const fieldNames = {
    AnimalId: 'Animal', StockItemId: 'Stock item', Amount: 'Amount', Reference: 'Reference', Litres: 'Litres',
    WeightKg: 'Weight (kg)', InitialWeightKg: 'Starting weight (kg)', TargetWeightKg: 'Target weight (kg)',
    ExpectedSaleAmount: 'Expected sale amount', ExpenseDate: 'Expense date', WeighedOn: 'Weighing date',
    ServiceDate: 'Service date', EventDate: 'Event date', Date: 'Date', Quantity: 'Quantity', UnitCost: 'Unit cost',
    SireOrSemen: 'Sire / semen batch identifier', CycleId: 'Open cycle', Description: 'Description', Notes: 'Notes',
    Tag: 'Tag', Breed: 'Breed', Session: 'Session', Category: 'Category', Method: 'Method', Outcome: 'Outcome',
    Password: 'Password', ConfirmPassword: 'Confirm new password', NewPassword: 'New password', Email: 'Email',
    CurrentPassword: 'Current password', Code: 'Authenticator code', RecoveryCode: 'Recovery code',
    PurchaseCost: 'Purchase cost', CurrentWeightKg: 'Weight (kg)', ItemId: 'Stock item', Price: 'Price',
    SourceItemId: 'Source product', DestinationItemId: 'Destination product', DestinationBranchId: 'Destination branch',
    Note: 'Notes', UnitOfMeasure: 'Stock unit', EventType: 'Event type', NextDueDate: 'Due date',
    ExpectedCalvingDate: 'Expected calving date', OpeningDate: 'Opening date', ReviewNote: 'Opening review note',
    AccountCode: 'Account code', Debit: 'Debit', Credit: 'Credit', Value: 'Value', CompanyName: 'Company',
    FarmName: 'Farm', BranchName: 'Branch', NewFarmName: 'New farm', InitialPassword: 'Password',
    Status: 'Status', Details: 'Details', CompletedDate: 'Completion date', AssignedTo: 'Assignee'
  };
  const businessFields = new Set([
    ...Object.values(fieldNames), 'SKU', 'Name', 'Account', 'Calculated amount', 'Total', 'Voucher total',
    'Stock balance', 'Inventory value', 'Average unit cost', 'Transfer note', 'Transfer quantity', 'Return quantity', 'Return amount',
    'Party code', 'Party name', 'Phone', 'Address', 'Order quantity', 'Agreed unit price', 'Order total',
    'Receipt quantity', 'Receipt total', 'Reorder point', 'Target quantity', 'Close reason', 'Feed quantity',
    'Weight', 'Movement', 'Adjustment value', 'Opening quantity', 'Opening stock value', 'Opening animal value',
    'Opening inventory total', 'Opening livestock total', 'Bank amount', 'Rule name'
  ]);
  function knownField(text) {
    if (businessFields.has(text)) return text;
    // Accept only real model-path syntax, never interpolated values or arbitrary provider error text.
    if (!/^[A-Za-z]+(?:\[\d+\])?(?:\.[A-Za-z]+(?:\[\d+\])?)*$/.test(text)) return null;
    if (text.includes('.') && !/^(Entry|Breeding|OutcomeEntry|Plan|Weight|Expense|Opening|Input|Update|NewCompany|NewBranch|NewStaff|ExistingStaff|Revoke|FollowUp)\./.test(text)) return null;
    const name = text.split('.').pop();
    return Object.hasOwn(fieldNames, name) ? fieldNames[name] : null;
  }
  function field(text) { return translate(knownField(text) || text); }
  function validation(text) {
    const exact = translate(text);
    if (exact !== text || language === 'en') return exact;
    let m;
    if ((m = /^The (.+) field is required\.$/.exec(text)) && knownField(m[1]))
      return language === 'ar' ? `حقل ${field(m[1])} مطلوب.` : language === 'bn' ? `${field(m[1])} পূরণ করুন।` : `${field(m[1])} puron korun.`;
    if ((m = /^The field (.+) must be between (-?\d+(?:\.\d+)?) and (-?\d+(?:\.\d+)?)\.$/.exec(text)) && knownField(m[1]))
      return language === 'ar' ? `يجب أن تكون قيمة ${field(m[1])} بين ${m[2]} و${m[3]}.` : language === 'bn' ? `${field(m[1])} ${m[2]} থেকে ${m[3]} এর মধ্যে হতে হবে।` : `${field(m[1])} ${m[2]} theke ${m[3]} er moddhe hote hobe.`;
    if ((m = /^The field (.+) must be a string with a maximum length of '?(\d+)'?\.$/.exec(text)) && knownField(m[1]))
      return language === 'ar' ? `يجب ألا يتجاوز ${field(m[1])} ${m[2]} حرفًا.` : language === 'bn' ? `${field(m[1])} সর্বোচ্চ ${m[2]} অক্ষরের হতে হবে।` : `${field(m[1])} shorbochcho ${m[2]} okkhorer hote hobe.`;
    if ((m = /^The field (.+) must be a string with a minimum length of (\d+) and a maximum length of (\d+)\.$/.exec(text)) && knownField(m[1]))
      return language === 'ar' ? `يجب أن يتراوح طول ${field(m[1])} بين ${m[2]} و${m[3]} حرفًا.` : language === 'bn' ? `${field(m[1])} ${m[2]} থেকে ${m[3]} অক্ষরের হতে হবে।` : `${field(m[1])} ${m[2]} theke ${m[3]} okkhorer hote hobe.`;
    if ((m = /^(.+) must (be positive and )?have at most 3 decimal places and be within the allowed range\.$/.exec(text)) && knownField(m[1]))
      return language === 'ar' ? `يجب أن تكون قيمة ${field(m[1])} ${m[2] ? 'موجبة، و' : ''}ضمن النطاق المسموح وبحد أقصى 3 منازل عشرية.` : language === 'bn' ? `${field(m[1])} ${m[2] ? 'ধনাত্মক হতে হবে, ' : ''}সর্বোচ্চ ৩ দশমিক ঘর ও অনুমোদিত সীমার মধ্যে হতে হবে।` : `${field(m[1])} ${m[2] ? 'dhonattok hote hobe, ' : ''}shorbochcho 3 doshomik ghor o onumodito shimar moddhe hote hobe.`;
    if ((m = /^(.+) is required and must be at most (\d+) characters( without control characters)?\.$/.exec(text)) && knownField(m[1]))
      return language === 'ar' ? `حقل ${field(m[1])} مطلوب وبحد أقصى ${m[2]} حرفًا${m[3] ? ' دون محارف تحكم' : ''}.` : language === 'bn' ? `${field(m[1])} পূরণ করুন, সর্বোচ্চ ${m[2]} অক্ষর${m[3] ? ', নিয়ন্ত্রণ অক্ষর ছাড়া' : ''}।` : `${field(m[1])} puron korun, shorbochcho ${m[2]} okkhor${m[3] ? ', control okkhor chara' : ''}.`;
    if ((m = /^(.+) exceeds the supported range\.$/.exec(text)) && businessFields.has(m[1]))
      return language === 'ar' ? `${field(m[1])} يتجاوز النطاق المدعوم.` : language === 'bn' ? `${field(m[1])} সমর্থিত সীমা ছাড়িয়েছে।` : `${field(m[1])} shomorthito shima chariyeche.`;
    if ((m = /^(Company|Farm|Branch|New farm) name must contain 1–120 characters\.$/.exec(text)))
      return language === 'ar' ? `يجب أن يتكون اسم ${translate(m[1])} من 1–120 حرفًا.` : language === 'bn' ? `${translate(m[1])} নাম ১–১২০ অক্ষরের হতে হবে।` : `${translate(m[1])} nam 1–120 okkhorer hote hobe.`;
    if ((m = /^Passwords must be at least (\d+) characters\.$/.exec(text)))
      return language === 'ar' ? `يجب ألا تقل كلمة المرور عن ${m[1]} حرفًا.` : language === 'bn' ? `পাসওয়ার্ড কমপক্ষে ${m[1]} অক্ষরের হতে হবে।` : `Password kompokkhe ${m[1]} okkhorer hote hobe.`;
    // Unknown messages, invalid-value echoes, credentials and provider errors stay unchanged.
    return text;
  }
  function systemMessage(text) {
    let m;
    if ((m = /^(\d+) stock items are at or below their reorder point\. Review purchasing needs\.$/.exec(text)))
      return translate('{count} stock items are at or below their reorder point. Review purchasing needs.').replace('{count}', m[1]);
    if ((m = /^(\d+) recorded health follow-ups are due\. Staff review is required\.$/.exec(text)))
      return translate('{count} recorded health follow-ups are due. Staff review is required.').replace('{count}', m[1]);
    if (text.startsWith('Scheduled review: '))
      return translate('Scheduled review: {name}').replace('{name}', () => text.slice('Scheduled review: '.length));
    return text;
  }
  function setText(element, text) { if (element.textContent !== text) element.textContent = text; }
  function refreshMessage(element, render) {
    const previous = originals.get(element);
    const current = element.textContent;
    const source = previous && current === previous.rendered ? previous.source : current;
    const rendered = render(source);
    originals.set(element, { source, rendered });
    setText(element, rendered);
  }
  function refresh() {
    // Razor-generated labels are application metadata, not user-entered field values.
    document.querySelectorAll('label[for]').forEach(element => {
      if (element.children.length || element.dataset.i18n || element.dataset.businessI18n || element.dataset.transferI18n) return;
      const original = element.textContent.trim();
      const label = fieldNames[original] || original;
      if (dictionary[label]) element.dataset.i18n = label;
    });
    document.querySelectorAll('[data-i18n], [data-business-i18n], [data-transfer-i18n]').forEach(element => {
      const text = element.dataset.i18n || element.dataset.businessI18n || element.dataset.transferI18n;
      if (text) setText(element, translate(text));
    });
    document.querySelectorAll('[data-i18n-placeholder]').forEach(element => element.setAttribute('placeholder', translate(element.dataset.i18nPlaceholder)));
    document.querySelectorAll('[data-i18n-aria-label]').forEach(element => element.setAttribute('aria-label', translate(element.dataset.i18nAriaLabel)));
    document.querySelectorAll('[data-i18n-enum]').forEach(element => {
      if (!element.children.length && typeof element.dataset.i18nValue === 'string') setText(element, enumText(element.dataset.i18nEnum, element.dataset.i18nValue));
    });
    document.querySelectorAll('[data-i18n-template]').forEach(element => {
      if (element.children.length) return; // Never replace controls or nested content.
      try {
        const text = formatTemplate(element.dataset.i18nTemplate, JSON.parse(element.dataset.i18nValues));
        if (text !== null) setText(element, text);
      } catch (_) { /* Invalid metadata leaves the server-rendered fallback intact. */ }
    });
    document.querySelectorAll('[data-i18n-system-message="notification"]').forEach(element => {
      if (!element.children.length) refreshMessage(element, systemMessage);
    });
    document.querySelectorAll('.validation-summary-errors li, .field-validation-error, [role="alert"], [role="status"]').forEach(element => {
      if (element.children.length || element.dataset.i18n || element.dataset.businessI18n || element.dataset.transferI18n || element.id === 'language-announcement') return;
      if (element.dataset.i18nTemplate || element.dataset.i18nEnum || element.dataset.i18nSystemMessage) return;
      refreshMessage(element, validation);
    });
  }
  function apply(value, announce = false) {
    language = supported.includes(value) ? value : 'en';
    document.documentElement.lang = language === 'bn' ? 'bn' : language === 'banglish' ? 'bn-Latn' : language === 'ar' ? 'ar' : 'en';
    document.documentElement.dir = language === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.dataset.language = language;
    const chooser = document.getElementById('erp-language'); if (chooser) chooser.value = language;
    try { localStorage.setItem(key, language); } catch (_) { }
    try { document.cookie = `${key}=${language};path=/;max-age=31536000;SameSite=Lax${location.protocol === 'https:' ? ';Secure' : ''}`; } catch (_) { }
    document.querySelectorAll("input, select, textarea").forEach(element => { if (element.setCustomValidity) element.setCustomValidity(""); });
    refresh();
    if (announce) {
      const status = document.getElementById('language-announcement');
      if (status) status.textContent = language === 'ar' ? 'تم تغيير اللغة إلى العربية.' : language === 'bn' ? 'ভাষা বাংলা করা হয়েছে।' : language === 'banglish' ? 'Bhasha Banglish kora hoyeche.' : 'Language changed to English.';
    }
    document.dispatchEvent(new CustomEvent('farmsphere:language', { detail: { language } }));
  }
  // Native form errors follow the selected UI language. These messages never replace server validation.
  document.addEventListener('invalid', event => {
    const element = event.target; if (!element.validity || !element.setCustomValidity) return;
    element.setCustomValidity('');
    if (language === 'en') return;
    const v = element.validity;
    let text = '';
    if (v.valueMissing) text = language === 'ar' ? 'يرجى ملء هذا الحقل.' : language === 'bn' ? 'এই ঘরটি পূরণ করুন।' : 'Ei ghor-ti puron korun.';
    else if (v.rangeUnderflow) text = language === 'ar' ? `أدخل قيمة لا تقل عن ${element.min}.` : language === 'bn' ? `সর্বনিম্ন ${element.min} লিখুন।` : `Shorbonimno ${element.min} likhun.`;
    else if (v.rangeOverflow) text = language === 'ar' ? `أدخل قيمة لا تزيد عن ${element.max}.` : language === 'bn' ? `সর্বোচ্চ ${element.max} লিখুন।` : `Shorbochcho ${element.max} likhun.`;
    else if (v.stepMismatch) text = language === 'ar' ? `استخدم الزيادة المسموحة ${element.step}.` : language === 'bn' ? `অনুমোদিত ধাপ ${element.step} অনুসরণ করুন।` : `Onumodito dhap ${element.step} onushoron korun.`;
    else if (v.typeMismatch || v.badInput || v.patternMismatch) text = language === 'ar' ? 'أدخل قيمة بالنوع والتنسيق الصحيحين.' : language === 'bn' ? 'সঠিক ধরনের মান লিখুন।' : 'Shothik dhoroner man likhun.';
    else if (v.tooLong) text = language === 'ar' ? `أدخل ${element.maxLength} حرفًا على الأكثر.` : language === 'bn' ? `সর্বোচ্চ ${element.maxLength} অক্ষর লিখুন।` : `Shorbochcho ${element.maxLength} okkhor likhun.`;
    if (text) element.setCustomValidity(text);
  }, true);
  document.addEventListener('input', event => { if (event.target.setCustomValidity) event.target.setCustomValidity(''); });
  document.getElementById('erp-language')?.addEventListener('change', event => apply(event.target.value, true));
  window.addEventListener('storage', event => { if (event.key === key) apply(event.newValue); });
  window.FarmSphereLanguage = { set: value => apply(value, true), translate, validation, refresh, formatTemplate, enumText,
    register: values => { Object.entries(values).forEach(([text, translations]) => { dictionary[text] = { ...(dictionary[text] || {}), ...translations }; }); refresh(); } };
  apply(savedLanguage());
  if (typeof MutationObserver !== 'undefined') {
    let queued = false;
    new MutationObserver(() => {
      if (queued) return; queued = true;
      queueMicrotask(() => { queued = false; refresh(); });
    }).observe(document.getElementById('main') || document.body, { childList: true, subtree: true });
  }
})();
