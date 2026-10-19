(() => {
    const requires = document.querySelector('[data-requires-specialization]');
    const specialty = document.querySelector('[data-specialization-select]');
    if (requires && specialty) {
        const updateSpecialization = () => {
            document.getElementById('task-specialization').hidden = !requires.checked;
            specialty.required = requires.checked;
            specialty.disabled = !requires.checked;
            if (!requires.checked) specialty.value = '';
        };
        requires.addEventListener('change', updateSpecialization);
        updateSpecialization();
    }
    const mode = document.querySelector('[data-schedule-mode]');
    if (!mode) return;
    const requirement = document.getElementById('Input_Requirement');
    const updatePhotos = () => {
        const limits = document.querySelector('[data-photo-limits]');
        const enabled = ['FOTOGRAFIA', 'CAPTURA_PANTALLA'].includes(requirement.value);
        limits.hidden = !enabled;
        limits.querySelectorAll('input').forEach(input => { input.disabled = !enabled; });
    };
    requirement.addEventListener('change', updatePhotos); updatePhotos();
    const period = document.querySelector('[data-task-period]');
    const repeats = document.getElementById('Input_Repetitions');
    const sunday = document.querySelector('[data-sunday-time]');
    const sundayDay = document.querySelector('input[name="InputDays"][value="0"]');
    function updateSunday() {
        const includesSunday = period.value === 'SEMANAL' && (
            mode.value === 'DIAS_FIJOS' && sundayDay.checked ||
            mode.value === 'DISTRIBUIDA' && Number(repeats.value) === 7);
        sunday.hidden = !includesSunday;
        sunday.querySelector('input').disabled = !includesSunday;
    }
    function update() {
        const occasional = period.value === 'OCASIONAL';
        const monthFields = document.querySelector('[data-month-days]');
        const monthInput = monthFields.querySelector('input');
        const monthEnabled = period.value === 'MENSUAL';
        monthFields.hidden = !monthEnabled; monthInput.disabled = !monthEnabled;
        const fields = document.querySelector('[data-periodic-fields]');
        fields.hidden = occasional;
        fields.querySelectorAll('input,select').forEach(input => { input.disabled = occasional; });
        document.querySelector('[data-occasional-help]').hidden = !occasional;
        if (occasional) { updateSunday(); return; }
        document.querySelector('[data-fixed-days]').hidden = mode.value !== 'DIAS_FIJOS';
        document.querySelector('[data-progress-goal]').hidden = mode.value !== 'PROGRESO';
        const progress = mode.value === 'PROGRESO';
        const active = document.getElementById('Input_Active').checked;
        document.querySelector('[data-fixed-days]').querySelectorAll('input').forEach(i => i.disabled = mode.value !== 'DIAS_FIJOS');
        document.querySelector('[data-progress-goal]').querySelectorAll('input').forEach(i => { i.disabled = !progress; i.required = progress && active; });
        document.querySelector('[data-different-people]').hidden = progress && active;
        document.getElementById('Input_DifferentPeople').disabled = progress && active;
        monthInput.disabled = !monthEnabled;
        repeats.readOnly = mode.value !== 'DISTRIBUIDA' || monthEnabled && monthInput.value.trim() !== '';

        if (mode.value === 'PROGRESO') {
            repeats.value = 1;
            if (active) document.getElementById('Input_DifferentPeople').checked = false;
        } else if (mode.value === 'DIAS_FIJOS') repeats.value = Math.max(1, document.querySelectorAll('input[name="InputDays"]:checked').length);
        if (mode.value !== 'DISTRIBUIDA') document.getElementById('Input_PeriodType').value = 'SEMANAL';
        updateSunday();
    }
    document.getElementById('Input_Active').addEventListener('change', update);
    document.querySelector('[data-month-days] input').addEventListener('input', update);
    mode.addEventListener('change', update);
    period.addEventListener('change', () => {
        if (period.value !== 'SEMANAL') mode.value = 'DISTRIBUIDA';
        update();
    });
    document.querySelectorAll('input[name="InputDays"]').forEach(c => c.addEventListener('change', update));
    repeats.addEventListener('input', updateSunday);
    repeats.addEventListener('change', updateSunday);
    update();
})();
