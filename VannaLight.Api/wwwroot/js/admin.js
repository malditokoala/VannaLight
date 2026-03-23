// ═══════════════════════════════════════════════════════════
// STATE
// ═══════════════════════════════════════════════════════════
let globalJobs = [];
let activeFilter = 'all'; // 'all' | 'pending' | 'verified'

let globalAllowedObjects = [];
let selectedAllowedObjectId = null;
const defaultAllowedDomain = 'erp-kpi-pilot';

let globalBusinessRules = [];
let selectedBusinessRuleId = null;
const defaultBusinessRuleDomain = 'erp-kpi-pilot';

let globalProfiles = [];
let selectedProfileIndex = -1;

// ═══════════════════════════════════════════════════════════
// TAB SWITCHING
// ═══════════════════════════════════════════════════════════
function switchTab(t) {
    document.querySelectorAll('.tab-btn').forEach(b => {
        b.className = 'tab-btn';
    });

    document.querySelectorAll('.tab-content').forEach(p => {
        p.classList.remove('active');
    });

    const tab = document.getElementById('tab-' + t);
    const pane = document.getElementById('pane-' + t);

    if (tab) tab.className = 'tab-btn active-' + t;
    if (pane) pane.classList.add('active');
}

// ═══════════════════════════════════════════════════════════
// RAG TAB
// ═══════════════════════════════════════════════════════════
async function loadHistory() {
    const list = document.getElementById('historyList');
    if (!list) return;

    try {
        const res = await fetch('/api/admin/history');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        const jobs = await res.json();
        globalJobs = Array.isArray(jobs) ? jobs : [];
        renderHistory();
    } catch (e) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="12" cy="12" r="10"/>
                    <line x1="15" y1="9" x2="9" y2="15"></line>
                    <line x1="9" y1="9" x2="15" y2="15"></line>
                </svg>
                Error al cargar historial
            </div>`;
    }
}

function normalizeVerificationClass(vs) {
    const key = String(vs || '').toLowerCase();
    if (key === 'verified') return 'verified';
    if (key === 'rejected') return 'rejected';
    return 'pending';
}

function setFilter(f) {
    activeFilter = f;

    ['all', 'pending', 'verified'].forEach(id => {
        const btn = document.getElementById('filter-' + id);
        if (!btn) return;
        btn.className = 'filter-btn' + (id === f ? ' active-' + id : '');
    });

    renderHistory();
}

function renderHistory() {
    const list = document.getElementById('historyList');
    const count = document.getElementById('ragCount');
    if (!list) return;

    const filtered = globalJobs.filter(job => {
        const vs = normalizeVerificationClass(job.verificationStatus || job.VerificationStatus);
        if (activeFilter === 'pending') return vs === 'pending';
        if (activeFilter === 'verified') return vs === 'verified';
        return true;
    });

    if (count) count.textContent = filtered.length;

    if (!filtered.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="12" cy="12" r="10"/>
                    <line x1="12" y1="8" x2="12" y2="12"></line>
                    <line x1="12" y1="16" x2="12.01" y2="16"></line>
                </svg>
                Sin resultados para este filtro
            </div>`;
        return;
    }

    list.innerHTML = filtered.map(job => {
        const realIdx = globalJobs.indexOf(job);
        const status = job.status || job.Status || 'Unknown';
        const vs = job.verificationStatus || job.VerificationStatus || 'Pending';
        const question = job.question || job.Question || '';
        const created = job.createdUtc || job.CreatedUtc;
        const trained = !!(job.trainingExampleSaved || job.TrainingExampleSaved);
        const opOk = status === 'Completed';
        const vsKey = normalizeVerificationClass(vs);

        const time = created
            ? new Date(created).toLocaleTimeString('es-MX', { hour12: false, hour: '2-digit', minute: '2-digit' })
            : '--:--';

        return `
            <div class="history-item" id="hi-${realIdx}" onclick="loadEditor(${realIdx})">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(question)}</div>
                    <div class="hi-time">${time}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-status ${opOk ? 'ok' : 'err'}">
                        <span class="dot"></span>${escHtml(status)}
                    </span>
                    <span class="hi-verify ${vsKey}">${escHtml(vs)}</span>
                    ${trained ? '<span class="hi-verify verified">RAG ✓</span>' : ''}
                </div>
            </div>`;
    }).join('');
}

function loadEditor(i, options = {}) {
    const preserveBanner = options.preserveBanner === true;
    const job = globalJobs[i];
    if (!job) return;

    document.querySelectorAll('.history-item').forEach(el => el.classList.remove('selected'));
    const el = document.getElementById('hi-' + i);
    if (el) el.classList.add('selected');

    setValue('txtJobId', job.jobId || job.JobId || '');
    setValue('txtFeedbackComment', job.feedbackComment || job.FeedbackComment || '');
    setValue('txtQuestion', job.question || job.Question || '');
    setValue('txtSql', job.sqlText || job.SqlText || '');

    const status = job.status || job.Status || 'Unknown';
    const vs = job.verificationStatus || job.VerificationStatus || 'Pending';
    const trained = !!(job.trainingExampleSaved || job.TrainingExampleSaved);
    const vsLow = normalizeVerificationClass(vs);

    const badge = document.getElementById('ragVerifyBadge');
    if (badge) {
        badge.style.display = 'inline-flex';
        badge.className = 'status-badge ' + (vsLow === 'verified' ? 'ok' : vsLow === 'rejected' ? 'err' : 'warn');
        badge.textContent = vs;
    }

    if (!preserveBanner) {
        const error = job.errorText || job.ErrorText;
        if (error && error !== 'null') {
            showRagBanner('warn', `Error reportado: ${error}`);
        } else {
            hideRagBanner();
        }
    }

    const createdRaw = job.createdUtc || job.CreatedUtc;
    const created = createdRaw
        ? new Date(createdRaw).toLocaleString('es-MX', { hour12: false })
        : '';

    const opClass = status === 'Completed' ? 'status-ok' : 'status-err';
    const vsClass = vsLow === 'verified' ? 'verify-ok' : vsLow === 'rejected' ? 'verify-err' : 'verify-pending';
    const trClass = trained ? 'training-yes' : 'training-no';

    const ragMeta = document.getElementById('ragMeta');
    if (ragMeta) {
        ragMeta.innerHTML = `
            <span class="meta-chip ${opClass}">${escHtml(status)}</span>
            <span class="meta-chip ${vsClass}">Verif: ${escHtml(vs)}</span>
            <span class="meta-chip ${trClass}">RAG: ${trained ? 'Sí' : 'No'}</span>
            <span class="meta-time">${escHtml(created)}</span>`;
    }

    const btnSave = document.getElementById('btnSave');
    if (btnSave) btnSave.disabled = false;
}

function showRagBanner(type, message) {
    const el = document.getElementById('ragBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type;
    el.textContent = message;
    el.style.display = 'block';
}

function hideRagBanner() {
    const el = document.getElementById('ragBanner');
    if (!el) return;
    el.style.display = 'none';
    el.className = 'rag-banner';
}

async function saveCorrection() {
    const jobId = getValue('txtJobId');
    const question = getValue('txtQuestion');
    const sql = getValue('txtSql');
    const feedbackComment = getValue('txtFeedbackComment');

    if (!jobId || !question || !sql) return;

    const btn = document.getElementById('btnSave');
    const spin = document.getElementById('ragSpinner');

    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch('/api/admin/train', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                JobId: jobId,
                Question: question,
                SqlText: sql,
                FeedbackComment: feedbackComment || null
            })
        });

        if (res.ok) {
            showRagBanner('ok', 'Guardado correctamente en memoria RAG y runtime actualizado.');
            setFilter('all');
            await loadHistory();

            const refreshedIdx = globalJobs.findIndex(j => (j.jobId || j.JobId) === jobId);
            if (refreshedIdx >= 0) loadEditor(refreshedIdx, { preserveBanner: true });
        } else {
            const body = await safeJson(res);
            showRagBanner('err', body?.Error || ('Error al guardar. Código: ' + res.status));
        }
    } catch (e) {
        showRagBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
    }
}

async function reindexSchema() {
    const btn = document.getElementById('btnReindexSchema');
    const spin = document.getElementById('schemaSpinner');

    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    hideSchemaBanner();

    try {
        const res = await fetch('/api/admin/reindex-schema', {
            method: 'POST'
        });

        const body = await safeJson(res);

        if (res.ok) {
            showSchemaBanner('ok', body?.Message || body?.message || 'Reindexación de schema completada correctamente.');
        } else {
            showSchemaBanner(
                'err',
                body?.Error || body?.error || body?.Detail || body?.detail || ('Error al reindexar schema. Código: ' + res.status)
            );
        }
    } catch (e) {
        showSchemaBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
    }
}

function showSchemaBanner(type, message) {
    const el = document.getElementById('schemaBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type;
    el.textContent = message;
    el.style.display = 'block';
}

function hideSchemaBanner() {
    const el = document.getElementById('schemaBanner');
    if (!el) return;
    el.style.display = 'none';
    el.className = 'rag-banner';
}
// ═══════════════════════════════════════════════════════════
// ALLOWED OBJECTS TAB
// ═══════════════════════════════════════════════════════════
async function loadAllowedObjects() {
    const domainInput = document.getElementById('txtAllowedDomainFilter');
    const domain = (domainInput?.value || defaultAllowedDomain).trim();
    const list = document.getElementById('allowedList');

    if (!domain) { showAllowedBanner('warn', 'Debes indicar un dominio para cargar Allowed Objects.'); return; }

    try {
        const res = await fetch(`/api/admin/allowed-objects?domain=${encodeURIComponent(domain)}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        globalAllowedObjects = await res.json();
        renderAllowedObjects();

        const editDomain = document.getElementById('txtAllowedDomain');
        if (editDomain) editDomain.value = domain;

        hideAllowedBanner();
    } catch (e) {
        if (list) list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <ellipse cx="12" cy="5" rx="7" ry="3"></ellipse>
                    <path d="M5 5v6c0 1.7 3.1 3 7 3s7-1.3 7-3V5"></path>
                    <path d="M5 11v6c0 1.7 3.1 3 7 3s7-1.3 7-3v-6"></path>
                </svg>
                Error al cargar Allowed Objects
            </div>`;
        showAllowedBanner('err', 'Error al cargar Allowed Objects: ' + e.message);
    }
}

function renderAllowedObjects() {
    const list = document.getElementById('allowedList');
    const count = document.getElementById('allowedCount');
    if (!list) return;

    if (count) count.textContent = globalAllowedObjects.length;

    if (!globalAllowedObjects.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <ellipse cx="12" cy="5" rx="7" ry="3"></ellipse>
                    <path d="M5 5v6c0 1.7 3.1 3 7 3s7-1.3 7-3V5"></path>
                    <path d="M5 11v6c0 1.7 3.1 3 7 3s7-1.3 7-3v-6"></path>
                </svg>
                Sin Allowed Objects para este dominio
            </div>`;
        return;
    }

    list.innerHTML = globalAllowedObjects.map(item => {
        const id = item.id ?? item.Id;
        const schemaName = item.schemaName || item.SchemaName || '';
        const objectName = item.objectName || item.ObjectName || '';
        const objectType = item.objectType || item.ObjectType || '';
        const isActive = !!(item.isActive ?? item.IsActive);
        const notes = item.notes || item.Notes || '';

        return `
            <div class="history-item" id="ao-${id}" onclick="selectAllowedObject(${id})">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(schemaName)}.${escHtml(objectName)}</div>
                    <div class="hi-time">${escHtml(objectType)}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-verify ${isActive ? 'verified' : 'rejected'}">
                        ${isActive ? 'Activo' : 'Inactivo'}
                    </span>
                    ${notes ? `<span class="hi-status ok meta-muted"><span class="dot dot-muted"></span>${escHtml(notes)}</span>` : ''}
                </div>
            </div>`;
    }).join('');

    if (selectedAllowedObjectId !== null) {
        const el = document.getElementById('ao-' + selectedAllowedObjectId);
        if (el) el.classList.add('selected');
    }
}

function selectAllowedObject(id) {
    const item = globalAllowedObjects.find(x => (x.id ?? x.Id) === id);
    if (!item) return;

    selectedAllowedObjectId = id;
    document.querySelectorAll('#allowedList .history-item').forEach(el => el.classList.remove('selected'));

    const target = document.getElementById('ao-' + id);
    if (target) target.classList.add('selected');

    const domain = item.domain || item.Domain || '';
    const schemaName = item.schemaName || item.SchemaName || '';
    const objectName = item.objectName || item.ObjectName || '';
    const objectType = item.objectType || item.ObjectType || '';
    const isActive = !!(item.isActive ?? item.IsActive);
    const notes = item.notes || item.Notes || '';

    setValue('txtAllowedId', id);
    setValue('txtAllowedDomain', domain);
    setValue('txtAllowedSchemaName', schemaName);
    setValue('txtAllowedObjectName', objectName);
    setValue('txtAllowedObjectType', objectType);
    setChecked('chkAllowedIsActive', isActive);
    setValue('txtAllowedNotes', notes);

    const toggleBtn = document.getElementById('btnAllowedToggle');
    if (toggleBtn) { toggleBtn.disabled = false; toggleBtn.textContent = isActive ? 'Desactivar' : 'Activar'; }

    const meta = document.getElementById('allowedMeta');
    if (meta) meta.innerHTML = `
        <span class="meta-chip ${isActive ? 'verify-ok' : 'verify-err'}">${isActive ? 'Activo' : 'Inactivo'}</span>
        <span class="meta-chip training-no">Id: ${id}</span>
        <span class="meta-time">${escHtml(domain)}</span>`;

    syncAllowedActionButtons();
    hideAllowedBanner();
}

function resetAllowedObjectForm() {
    selectedAllowedObjectId = null;
    document.querySelectorAll('#allowedList .history-item').forEach(el => el.classList.remove('selected'));

    setValue('txtAllowedId', '');
    setValue('txtAllowedDomain', (getValue('txtAllowedDomainFilter').trim() || defaultAllowedDomain));
    setValue('txtAllowedSchemaName', '');
    setValue('txtAllowedObjectName', '');
    setValue('txtAllowedObjectType', '');
    setChecked('chkAllowedIsActive', true);
    setValue('txtAllowedNotes', '');

    const toggleBtn = document.getElementById('btnAllowedToggle');
    if (toggleBtn) { toggleBtn.disabled = true; toggleBtn.textContent = 'Activar / Desactivar'; }

    const meta = document.getElementById('allowedMeta');
    if (meta) meta.innerHTML = `<span class="meta-empty">Ningún objeto seleccionado</span>`;

    syncAllowedActionButtons();
    hideAllowedBanner();
}

function syncAllowedActionButtons() {
    const hasSelection = !!getValue('txtAllowedId');
    const saveBtn = document.getElementById('btnAllowedSave');
    const toggleBtn = document.getElementById('btnAllowedToggle');

    if (saveBtn) saveBtn.innerHTML = `
        <div class="btn-spinner" id="allowedSpinner" style="display:none"></div>
        ${hasSelection ? 'Actualizar Allowed Object' : 'Crear Allowed Object'}`;

    if (toggleBtn) { toggleBtn.disabled = !hasSelection; if (!hasSelection) toggleBtn.textContent = 'Activar / Desactivar'; }
}

async function saveAllowedObject() {
    const id = getValue('txtAllowedId');
    const domain = getValue('txtAllowedDomain').trim();
    const schemaName = getValue('txtAllowedSchemaName').trim();
    const objectName = getValue('txtAllowedObjectName').trim();
    const objectType = getValue('txtAllowedObjectType').trim();
    const isActive = getChecked('chkAllowedIsActive');
    const notes = getValue('txtAllowedNotes').trim();

    if (!domain || !schemaName || !objectName || !objectType) {
        showAllowedBanner('warn', 'Domain, Schema Name, Object Name y Object Type son requeridos.'); return;
    }

    const btn = document.getElementById('btnAllowedSave');
    const spin = document.getElementById('allowedSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch('/api/admin/allowed-objects', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ Id: id ? Number(id) : 0, Domain: domain, SchemaName: schemaName, ObjectName: objectName, ObjectType: objectType, IsActive: isActive, Notes: notes || null })
        });

        if (res.ok) {
            const body = await safeJson(res);
            showAllowedBanner('ok', body?.Message || 'Allowed Object guardado correctamente.');
            setValue('txtAllowedDomainFilter', domain);
            await loadAllowedObjects();
            const savedId = body?.Id ?? null;
            if (savedId !== null) selectAllowedObject(savedId);
            else { resetAllowedObjectForm(); setValue('txtAllowedDomain', domain); }
        } else {
            const body = await safeJson(res);
            showAllowedBanner('err', body?.Error || ('Error al guardar Allowed Object. Código: ' + res.status));
        }
    } catch (e) {
        showAllowedBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
    }
}

async function toggleAllowedObjectStatus() {
    const id = getValue('txtAllowedId');
    if (!id) { showAllowedBanner('warn', 'Selecciona un Allowed Object antes de cambiar su estado.'); return; }

    const currentItem = globalAllowedObjects.find(x => String(x.id ?? x.Id) === String(id));
    if (!currentItem) { showAllowedBanner('err', 'No se encontró el Allowed Object seleccionado.'); return; }

    const nextIsActive = !(currentItem.isActive ?? currentItem.IsActive);
    const btn = document.getElementById('btnAllowedToggle');
    if (btn) btn.disabled = true;

    try {
        const res = await fetch(`/api/admin/allowed-objects/${id}/status`, {
            method: 'PATCH', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ IsActive: nextIsActive })
        });
        if (res.ok) {
            showAllowedBanner('ok', `Allowed Object ${nextIsActive ? 'activado' : 'desactivado'} correctamente.`);
            await loadAllowedObjects();
            selectAllowedObject(Number(id));
        } else {
            const body = await safeJson(res);
            showAllowedBanner('err', body?.Error || ('Error al actualizar estatus. Código: ' + res.status));
        }
    } catch (e) {
        showAllowedBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
    }
}

function showAllowedBanner(type, message) {
    const el = document.getElementById('allowedBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type; el.textContent = message; el.style.display = 'block';
}
function hideAllowedBanner() {
    const el = document.getElementById('allowedBanner');
    if (!el) return;
    el.style.display = 'none'; el.className = 'rag-banner';
}

// ═══════════════════════════════════════════════════════════
// BUSINESS RULES TAB
// ═══════════════════════════════════════════════════════════
async function loadBusinessRules() {
    const domainInput = document.getElementById('txtBusinessRuleDomainFilter');
    const domain = (domainInput?.value || defaultBusinessRuleDomain).trim();
    const list = document.getElementById('businessRuleList');

    if (!domain) { showBusinessRuleBanner('warn', 'Debes indicar un dominio para cargar Business Rules.'); return; }

    try {
        const res = await fetch(`/api/admin/business-rules?domain=${encodeURIComponent(domain)}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        globalBusinessRules = await res.json();
        renderBusinessRules();
        setValue('txtBusinessRuleDomain', domain);
        hideBusinessRuleBanner();
    } catch (e) {
        if (list) list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <rect x="4" y="4" width="16" height="16" rx="2"></rect>
                    <path d="M8 9h8"></path><path d="M8 13h8"></path><path d="M8 17h5"></path>
                </svg>
                Error al cargar Business Rules
            </div>`;
        showBusinessRuleBanner('err', 'Error al cargar Business Rules: ' + e.message);
    }
}

function renderBusinessRules() {
    const list = document.getElementById('businessRuleList');
    const count = document.getElementById('businessRuleCount');
    if (!list) return;

    if (count) count.textContent = globalBusinessRules.length;

    if (!globalBusinessRules.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <rect x="4" y="4" width="16" height="16" rx="2"></rect>
                    <path d="M8 9h8"></path><path d="M8 13h8"></path><path d="M8 17h5"></path>
                </svg>
                Sin Business Rules para este dominio
            </div>`;
        return;
    }

    list.innerHTML = globalBusinessRules.map(item => {
        const id = item.id ?? item.Id;
        const ruleKey = item.ruleKey || item.RuleKey || '';
        const ruleText = item.ruleText || item.RuleText || '';
        const priority = item.priority ?? item.Priority ?? 0;
        const isActive = !!(item.isActive ?? item.IsActive);

        return `
            <div class="history-item" id="br-${id}" onclick="selectBusinessRule(${id})">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(ruleKey)}</div>
                    <div class="hi-time">P${escHtml(String(priority))}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-verify ${isActive ? 'verified' : 'rejected'}">${isActive ? 'Activa' : 'Inactiva'}</span>
                </div>
                <div class="meta-muted" style="margin-top:6px;font-family:var(--mono);font-size:.62rem;line-height:1.4;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden">${escHtml(ruleText)}</div>
            </div>`;
    }).join('');

    if (selectedBusinessRuleId !== null) {
        const el = document.getElementById('br-' + selectedBusinessRuleId);
        if (el) el.classList.add('selected');
    }
}

function selectBusinessRule(id) {
    const item = globalBusinessRules.find(x => (x.id ?? x.Id) === id);
    if (!item) return;

    selectedBusinessRuleId = id;
    document.querySelectorAll('#businessRuleList .history-item').forEach(el => el.classList.remove('selected'));

    const target = document.getElementById('br-' + id);
    if (target) target.classList.add('selected');

    const domain = item.domain || item.Domain || '';
    const ruleKey = item.ruleKey || item.RuleKey || '';
    const ruleText = item.ruleText || item.RuleText || '';
    const priority = item.priority ?? item.Priority ?? 0;
    const isActive = !!(item.isActive ?? item.IsActive);

    setValue('txtBusinessRuleId', id);
    setValue('txtBusinessRuleDomain', domain);
    setValue('txtBusinessRuleKey', ruleKey);
    setValue('txtBusinessRuleText', ruleText);
    setValue('txtBusinessRulePriority', priority);
    setChecked('chkBusinessRuleIsActive', isActive);

    const toggleBtn = document.getElementById('btnBusinessRuleToggle');
    if (toggleBtn) { toggleBtn.disabled = false; toggleBtn.textContent = isActive ? 'Desactivar' : 'Activar'; }

    const meta = document.getElementById('businessRuleMeta');
    if (meta) meta.innerHTML = `
        <span class="meta-chip ${isActive ? 'verify-ok' : 'verify-err'}">${isActive ? 'Activa' : 'Inactiva'}</span>
        <span class="meta-chip training-no">Id: ${id}</span>
        <span class="meta-chip training-no">Priority: ${priority}</span>
        <span class="meta-time">${escHtml(domain)}</span>`;

    syncBusinessRuleActionButtons();
    hideBusinessRuleBanner();
}

function resetBusinessRuleForm() {
    selectedBusinessRuleId = null;
    document.querySelectorAll('#businessRuleList .history-item').forEach(el => el.classList.remove('selected'));

    setValue('txtBusinessRuleId', '');
    setValue('txtBusinessRuleDomain', (getValue('txtBusinessRuleDomainFilter').trim() || defaultBusinessRuleDomain));
    setValue('txtBusinessRuleKey', '');
    setValue('txtBusinessRuleText', '');
    setValue('txtBusinessRulePriority', '0');
    setChecked('chkBusinessRuleIsActive', true);

    const toggleBtn = document.getElementById('btnBusinessRuleToggle');
    if (toggleBtn) { toggleBtn.disabled = true; toggleBtn.textContent = 'Activar / Desactivar'; }

    const meta = document.getElementById('businessRuleMeta');
    if (meta) meta.innerHTML = `<span class="meta-empty">Ninguna regla seleccionada</span>`;

    syncBusinessRuleActionButtons();
    hideBusinessRuleBanner();
}

function syncBusinessRuleActionButtons() {
    const hasSelection = !!getValue('txtBusinessRuleId');
    const saveBtn = document.getElementById('btnBusinessRuleSave');
    const toggleBtn = document.getElementById('btnBusinessRuleToggle');

    if (saveBtn) saveBtn.innerHTML = `
        <div class="btn-spinner" id="businessRuleSpinner" style="display:none"></div>
        ${hasSelection ? 'Actualizar Business Rule' : 'Crear Business Rule'}`;

    if (toggleBtn) { toggleBtn.disabled = !hasSelection; if (!hasSelection) toggleBtn.textContent = 'Activar / Desactivar'; }
}

async function saveBusinessRule() {
    const id = getValue('txtBusinessRuleId');
    const domain = getValue('txtBusinessRuleDomain').trim();
    const ruleKey = getValue('txtBusinessRuleKey').trim();
    const ruleText = getValue('txtBusinessRuleText').trim();
    const priorityRaw = getValue('txtBusinessRulePriority').trim();
    const isActive = getChecked('chkBusinessRuleIsActive');

    if (!domain || !ruleKey || !ruleText) {
        showBusinessRuleBanner('warn', 'Domain, Rule Key y Rule Text son requeridos.'); return;
    }

    const priority = priorityRaw === '' ? 0 : parseInt(priorityRaw, 10);
    if (Number.isNaN(priority) || priority < 0) {
        showBusinessRuleBanner('warn', 'Priority debe ser un entero mayor o igual a 0.'); return;
    }

    const btn = document.getElementById('btnBusinessRuleSave');
    const spin = document.getElementById('businessRuleSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch('/api/admin/business-rules', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ Id: id ? Number(id) : 0, Domain: domain, RuleKey: ruleKey, RuleText: ruleText, Priority: priority, IsActive: isActive })
        });

        if (res.ok) {
            const body = await safeJson(res);
            showBusinessRuleBanner('ok', body?.Message || 'Business Rule guardada correctamente.');
            setValue('txtBusinessRuleDomainFilter', domain);
            await loadBusinessRules();
            const savedId = body?.Id ?? null;
            if (savedId !== null) selectBusinessRule(savedId);
            else { resetBusinessRuleForm(); setValue('txtBusinessRuleDomain', domain); }
        } else {
            const body = await safeJson(res);
            showBusinessRuleBanner('err', body?.Error || ('Error al guardar Business Rule. Código: ' + res.status));
        }
    } catch (e) {
        showBusinessRuleBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
    }
}

async function toggleBusinessRuleStatus() {
    const id = getValue('txtBusinessRuleId');
    if (!id) { showBusinessRuleBanner('warn', 'Selecciona una Business Rule antes de cambiar su estado.'); return; }

    const currentItem = globalBusinessRules.find(x => String(x.id ?? x.Id) === String(id));
    if (!currentItem) { showBusinessRuleBanner('err', 'No se encontró la Business Rule seleccionada.'); return; }

    const nextIsActive = !(currentItem.isActive ?? currentItem.IsActive);
    const btn = document.getElementById('btnBusinessRuleToggle');
    if (btn) btn.disabled = true;

    try {
        const res = await fetch(`/api/admin/business-rules/${id}/status`, {
            method: 'PATCH', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ IsActive: nextIsActive })
        });
        if (res.ok) {
            showBusinessRuleBanner('ok', `Business Rule ${nextIsActive ? 'activada' : 'desactivada'} correctamente.`);
            await loadBusinessRules();
            selectBusinessRule(Number(id));
        } else {
            const body = await safeJson(res);
            showBusinessRuleBanner('err', body?.Error || ('Error al actualizar estatus. Código: ' + res.status));
        }
    } catch (e) {
        showBusinessRuleBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
    }
}

function showBusinessRuleBanner(type, message) {
    const el = document.getElementById('businessRuleBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type; el.textContent = message; el.style.display = 'block';
}
function hideBusinessRuleBanner() {
    const el = document.getElementById('businessRuleBanner');
    if (!el) return;
    el.style.display = 'none'; el.className = 'rag-banner';
}

// ═══════════════════════════════════════════════════════════
// LLM TAB
// ═══════════════════════════════════════════════════════════
async function loadProfiles() {
    const list = document.getElementById('profileList');
    if (!list) return;

    try {
        const res = await fetch('/api/admin/llm-profiles');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        globalProfiles = await res.json();

        if (!globalProfiles.length) {
            list.innerHTML = `
                <div class="empty-state">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                        <rect x="4" y="4" width="16" height="16" rx="2"></rect>
                        <rect x="9" y="9" width="6" height="6"></rect>
                    </svg>
                    Sin perfiles disponibles
                </div>`;
            return;
        }

        list.innerHTML = globalProfiles.map((p, i) => {
            const name = p.name || p.Name;
            const isActive = !!(p.isActive || p.IsActive);
            const gpu = p.gpuLayerCount ?? p.GpuLayerCount ?? 0;
            const ctx = p.contextSize ?? p.ContextSize ?? 0;
            return `
                <div class="profile-item ${isActive ? 'is-active' : ''}" data-idx="${i}" onclick="selectProfile(${i})">
                    <div class="pi-name">
                        ${escHtml(name)}
                        ${isActive ? '<span class="pi-active-pill">ACTIVO</span>' : ''}
                    </div>
                    <div class="pi-summary">GPU Layers: ${gpu} · Context: ${Number(ctx).toLocaleString('es-MX')}</div>
                </div>`;
        }).join('');

        if (selectedProfileIndex >= 0 && selectedProfileIndex < globalProfiles.length) {
            const target = document.querySelector(`.profile-item[data-idx="${selectedProfileIndex}"]`);
            if (target) target.classList.add('selected');
        }
    } catch (e) {
        list.innerHTML = `<div class="empty-state">Error al cargar perfiles (${escHtml(e.message)})</div>`;
    }
}

function selectProfile(idx) {
    const p = globalProfiles[idx];
    if (!p) return;

    selectedProfileIndex = idx;
    const isActive = !!(p.isActive || p.IsActive);

    document.querySelectorAll('.profile-item').forEach(el => el.classList.remove('selected'));
    const target = document.querySelector(`.profile-item[data-idx="${idx}"]`);
    if (target) target.classList.add('selected');

    setValue('txtProfileId', p.id ?? p.Id ?? '');
    setValue('txtProfileName', p.name || p.Name || '');
    setValue('txtGpuLayers', p.gpuLayerCount ?? p.GpuLayerCount ?? '');
    setValue('txtContextSize', p.contextSize ?? p.ContextSize ?? '');
    setValue('txtBatchSize', p.batchSize ?? p.BatchSize ?? '');
    setValue('txtUBatchSize', p.uBatchSize ?? p.UBatchSize ?? '');
    setValue('txtThreads', p.threads ?? p.Threads ?? '');

    const btnSave = document.getElementById('btnSaveProfile');
    const btnActivate = document.getElementById('btnActivate');
    const vramMeter = document.getElementById('vramMeter');

    if (btnSave) btnSave.disabled = false;
    if (btnActivate) btnActivate.disabled = isActive;
    if (vramMeter) vramMeter.classList.remove('hidden');

    hideAlert();
    updateVram();
}

function updateVram() {
    const layers = parseInt(getValue('txtGpuLayers'), 10) || 0;
    const gb = (layers * 0.18 + 0.5).toFixed(1);
    const pct = Math.min((parseFloat(gb) / 8) * 100, 100);

    setText('vramVal', gb + ' GB');
    const vramFill = document.getElementById('vramFill');
    if (vramFill) vramFill.style.width = pct + '%';
}

function getIntOrNull(elementId) {
    const el = document.getElementById(elementId);
    if (!el) return null;
    const val = el.value;
    if (val === '') return null;
    const parsed = parseInt(val, 10);
    return Number.isNaN(parsed) ? null : parsed;
}

async function saveProfileSettings() {
    if (selectedProfileIndex < 0) return;
    const profileId = globalProfiles[selectedProfileIndex].id ?? globalProfiles[selectedProfileIndex].Id;
    const btn = document.getElementById('btnSaveProfile');
    const spin = document.getElementById('profileSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    const body = {
        GpuLayerCount: getIntOrNull('txtGpuLayers'),
        ContextSize: getIntOrNull('txtContextSize'),
        BatchSize: getIntOrNull('txtBatchSize'),
        UBatchSize: getIntOrNull('txtUBatchSize'),
        Threads: getIntOrNull('txtThreads')
    };

    try {
        const res = await fetch(`/api/admin/llm-profiles/${profileId}`, {
            method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body)
        });
        if (res.ok) { showAlert('ok', 'Ajustes guardados correctamente.'); await loadProfiles(); }
        else {
            const bodyErr = await safeJson(res);
            showAlert('err', bodyErr?.Error || ('Error al guardar ajustes. Código: ' + res.status));
        }
    } catch (e) { showAlert('err', 'Error de red: ' + e.message); }
    finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
    }
}

async function activateSelectedProfile() {
    if (selectedProfileIndex < 0) return;
    const profileId = globalProfiles[selectedProfileIndex].id ?? globalProfiles[selectedProfileIndex].Id;
    const btn = document.getElementById('btnActivate');
    const spin = document.getElementById('activateSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch(`/api/admin/llm-profiles/${profileId}/activate`, { method: 'POST' });
        if (res.ok) {
            showAlert('warn', `
                <div style="font-size:.85rem;font-weight:600;margin-bottom:4px;color:#f8fafc">Perfil activado correctamente</div>
                <div style="font-size:.75rem;color:#fbbf24;opacity:.9">IMPORTANTE: Detén y vuelve a ejecutar la API para que LLamaSharp cargue los nuevos pesos en la VRAM.</div>
            `);
            await loadProfiles();
            const activateBtn = document.getElementById('btnActivate');
            if (activateBtn) activateBtn.disabled = true;
        } else {
            const bodyErr = await safeJson(res);
            showAlert('err', bodyErr?.Error || 'Error al activar perfil.');
            if (btn) btn.disabled = false;
        }
    } catch (e) { showAlert('err', 'Error de red: ' + e.message); if (btn) btn.disabled = false; }
    finally { if (spin) spin.style.display = 'none'; }
}

function showAlert(type, msg) {
    const el = document.getElementById('llmAlert');
    if (!el) return;
    el.className = 'alert-banner ' + type; el.innerHTML = msg; el.style.display = 'block';
}
function hideAlert() {
    const el = document.getElementById('llmAlert');
    if (!el) return;
    el.style.display = 'none';
}

// ═══════════════════════════════════════════════════════════
// UTILS
// ═══════════════════════════════════════════════════════════
function escHtml(s) {
    return String(s ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;')
        .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

async function safeJson(res) {
    try { return await res.json(); } catch { return null; }
}

function getValue(id) { const el = document.getElementById(id); return el ? el.value : ''; }
function setValue(id, value) { const el = document.getElementById(id); if (el) el.value = value ?? ''; }
function getChecked(id) { const el = document.getElementById(id); return !!(el && el.checked); }
function setChecked(id, value) { const el = document.getElementById(id); if (el) el.checked = !!value; }
function setText(id, value) { const el = document.getElementById(id); if (el) el.textContent = value ?? ''; }

// ═══════════════════════════════════════════════════════════
// INIT
// ═══════════════════════════════════════════════════════════
document.addEventListener('DOMContentLoaded', () => {
    loadHistory();
    loadAllowedObjects();
    loadBusinessRules();
    loadProfiles();
    resetAllowedObjectForm();
    resetBusinessRuleForm();
});