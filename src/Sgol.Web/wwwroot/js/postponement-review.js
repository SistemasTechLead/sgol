(() => {
  const toolbar = document.querySelector('[data-postponement-toolbar]');
  if (!toolbar) return;
  let selected = null;
  const select = item => {
    selected = item;
    document.querySelectorAll('.postponement-item').forEach(row => {
      row.classList.toggle('postponement-item--selected', row === item);
      row.querySelector('[name="selected-postponement"]').checked = row === item;
      if (row !== item) row.querySelector('form').hidden = true;
    });
    toolbar.querySelector('[data-postponement-selection]').textContent = item.querySelector('h3').textContent.trim() + ' · ' + item.querySelector('[data-item-person]').textContent.trim();
    toolbar.querySelectorAll('[data-item-action]').forEach(button => {
      const action = button.dataset.itemAction;
      button.disabled = action === 'APPROVE' ? item.dataset.itemApprove !== 'true' : action === 'REASSIGN' ? item.dataset.itemReassign !== 'true' : action !== 'REJECT' && item.dataset.itemMove !== 'true';
      if (action === 'REASSIGN' && item.querySelector('[id="' + item.id + '-reassign-block"]')) button.setAttribute('aria-describedby', item.id + '-reassign-block');
      else button.removeAttribute('aria-describedby');
      button.setAttribute('aria-controls', item.id + '-editor');
      button.setAttribute('aria-expanded', 'false');
    });
    toolbar.querySelector('[data-item-action="APPROVE"]').textContent = item.dataset.itemDelivered === 'true' ? 'Cerrar sin cambios' : 'Aprobar propuesta';
  };
  toolbar.querySelectorAll('[data-item-action]').forEach(button => button.addEventListener('click', () => {
    if (selected) selected.dispatchEvent(new CustomEvent('postponement-action', { detail: button.dataset.itemAction }));
  }));
  new ResizeObserver(() => {
    document.querySelector('.postponement-review-scope')?.style.setProperty('--postponement-toolbar-height', toolbar.offsetHeight + 'px');
  }).observe(toolbar);
  document.querySelectorAll('.postponement-item').forEach(item => {
    const form = item.querySelector('[data-item-editor]');
    if (!form) return;
    form.id = item.id + '-editor';
    const selector = item.querySelector('[name="selected-postponement"]');
    selector.addEventListener('change', () => select(item));
    item.addEventListener('click', event => {
      if (event.target.closest('a,button,details,label,select,textarea,input')) return;
      if (!selector.disabled) select(item);
    });
    const mode = form.querySelector('[data-item-mode]');
    const dayMode = form.querySelector('[data-item-day-mode]');
    const dayInput = form.querySelector('[name="ItemDecision.Day"]');
    const key = 'sgol-postponement:' + item.id;
    const sync = () => {
      // A saved draft or a failed POST may refer to recipients that no longer qualify.
      const unavailableReassignment = mode.value === 'REASSIGN' && item.dataset.itemReassign !== 'true';
      form.querySelectorAll('button[name="confirm"]').forEach(button => { button.disabled = unavailableReassignment; });
      form.querySelector('[data-item-recipient]').hidden = mode.value !== 'REASSIGN';
      form.querySelector('[name="ItemDecision.Recipient"]').disabled = mode.value !== 'REASSIGN';
      form.querySelector('[name="ItemDecision.Recipient"]').required = mode.value === 'REASSIGN';
      const schedule = ['REASSIGN', 'RESCHEDULE'].includes(mode.value);
      form.querySelector('[data-item-day]').hidden = !schedule;
      form.querySelector('[name="ItemDecision.Day"]').disabled = !schedule;
      form.querySelector('[name="ItemDecision.Day"]').required = schedule;
      if (dayMode) {
        if (dayMode.value === 'same') dayInput.value = dayMode.dataset.requestedDay;
        dayInput.readOnly = dayMode.value === 'same';
        form.querySelector('[data-item-date-label]').hidden = dayMode.value === 'same';
      }
      if (selected === item) toolbar.querySelectorAll('[data-item-action]').forEach(b => b.setAttribute('aria-expanded', String(!form.hidden && b.dataset.itemAction === mode.value)));
    };
    const changed = () => {
      form.querySelector('[data-item-preview]')?.remove();
      sync();
      try { sessionStorage.setItem(key, JSON.stringify(Object.fromEntries([...new FormData(form)].filter(([k]) => ['ItemDecision.Action','ItemDecision.Recipient','ItemDecision.Day','ItemDecision.Comment'].includes(k))))); } catch {}
    };
    if (form.hidden) {
      try { const saved = JSON.parse(sessionStorage.getItem(key) || 'null'); if (saved) for (const [name,value] of Object.entries(saved)) { const field = form.elements.namedItem(name); if (field) field.value = value; } } catch {}
    }
    if (dayMode) dayMode.value = dayInput.value === dayMode.dataset.requestedDay ? 'same' : 'other';
    item.addEventListener('postponement-action', event => {
      if (event.detail === 'REASSIGN' && item.dataset.itemReassign !== 'true') return;
      form.hidden = false;
      if (mode.value !== event.detail) { mode.value = event.detail; changed(); }
      sync();
      form.scrollIntoView({ block: 'center' });
      form.querySelector('[name="ItemDecision.Comment"]').focus({ preventScroll: true });
    });
    form.addEventListener('input', changed);
    form.addEventListener('change', changed);
    form.querySelector('[data-item-close]').addEventListener('click', () => { form.hidden = true; sync(); toolbar.querySelector('[data-item-action="' + mode.value + '"]')?.focus(); });
    form.addEventListener('submit', event => {
      if (mode.value === 'REASSIGN' && item.dataset.itemReassign !== 'true') { event.preventDefault(); return; }
      if (form.dataset.submitting) { event.preventDefault(); return; }
      form.dataset.submitting = 'true';
      form.setAttribute('aria-busy', 'true');
      if (event.submitter?.value === 'true') { try { sessionStorage.removeItem(key); } catch {} }
    });
    if (selector.checked) select(item);
    sync();
  });
  const target = document.querySelector('.postponement-editor:not([hidden])') || document.querySelector('.alerta--exito');
  if (target) { target.setAttribute('tabindex', '-1'); target.focus({ preventScroll: true }); }
})();
