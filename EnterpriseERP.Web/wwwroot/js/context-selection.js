(() => {
  'use strict';
  const company = document.getElementById('companyId');
  const branch = document.getElementById('branchId');
  if (!company || !branch) return;
  const options = Array.from(branch.options);
  function sync() {
    for (const option of options) {
      option.hidden = option.dataset.company !== company.value;
      option.disabled = option.hidden;
    }
    const selected = options.find(option => !option.hidden && option.value === branch.value);
    const first = selected || options.find(option => !option.hidden);
    if (first) branch.value = first.value;
    else branch.selectedIndex = -1;
  }
  company.addEventListener('change', sync);
  sync();
})();
