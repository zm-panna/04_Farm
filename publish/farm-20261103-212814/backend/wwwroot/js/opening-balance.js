// More input rows only; balances and permissions are validated again by the server.
(() => {
 document.querySelectorAll('[data-opening-add]').forEach(button => button.addEventListener('click', () => {
  const body=document.getElementById(button.dataset.openingAdd); if(!body || body.rows.length>=100)return;
  const index=body.rows.length; const row=body.rows[0]?.cloneNode(true); if(!row)return;
  row.querySelectorAll('input,select').forEach(control=>{
   control.name=control.name.replace(/\[\d+\]/,`[${index}]`);
   control.id=control.id.replace(/_\d+__/,`_${index}__`);
   control.value='0';control.setCustomValidity('');
  });
  body.appendChild(row);row.querySelector('select')?.focus();
  if(body.rows.length>=100)button.disabled=true;
 }));
})();
