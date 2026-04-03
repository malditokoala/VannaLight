// -----------------------------------------------------------
// STATE
// -----------------------------------------------------------
let globalJobs = [];
let activeFilter = 'all'; // 'all' | 'pending' | 'negative' | 'verified'

let globalSystemConfigEntries = [];
let globalSystemConfigProfile = null;
let selectedSystemConfigEntryId = null;
let defaultAdminTenant = '';

let globalAllowedObjects = [];
let selectedAllowedObjectId = null;
let defaultAllowedDomain = '';

let globalBusinessRules = [];
let selectedBusinessRuleId = null;
let defaultBusinessRuleDomain = '';

let globalQueryPatterns = [];
let selectedQueryPatternId = null;
let defaultQueryPatternDomain = '';

let globalSemanticHints = [];
let selectedSemanticHintId = null;
let defaultSemanticHintDomain = '';

let globalQueryPatternTerms = [];
let selectedQueryPatternTermId = null;

let globalProfiles = [];
let selectedProfileIndex = -1;

let globalTenants = [];
let globalConnectionProfiles = [];
let globalTenantDomains = [];
let globalOnboardingRuntimeContexts = [];
let globalOnboardingSchemaCandidates = [];
let globalOnboardingStatus = null;
let globalOnboardingValidation = null;
let selectedOnboardingTenantKey = null;
let globalAdminActiveContext = null;
let onboardingBootstrap = null;
let activeOnboardingTourStep = -1;
const onboardingWorkspaceStateKey = 'vannalight.onboarding.workspace';
const adminScopedTabs = new Set(['allowed', 'business-rules', 'semantics', 'patterns']);

const promptConfigFields = [
    { key: 'SystemPersona', valueType: 'string', elementId: 'txtPromptSystemPersona', description: 'Persona base del system prompt SQL.' },
    { key: 'TaskInstruction', valueType: 'string', elementId: 'txtPromptTaskInstruction', description: 'Instrucción principal del system prompt SQL.' },
    { key: 'ContextInstruction', valueType: 'string', elementId: 'txtPromptContextInstruction', description: 'Instrucción de uso de contexto del system prompt SQL.' },
    { key: 'SqlSyntaxRules', valueType: 'string', elementId: 'txtPromptSqlSyntaxRules', description: 'Bloque editable de reglas críticas de sintaxis T-SQL.' },
    { key: 'TimeInterpretationRules', valueType: 'string', elementId: 'txtPromptTimeInterpretationRules', description: 'Bloque editable de interpretación temporal para el prompt SQL.' },
    { key: 'BusinessRulesHeader', valueType: 'string', elementId: 'txtPromptBusinessRulesHeader', description: 'Encabezado para el bloque de business rules del prompt SQL.' },
    { key: 'SemanticHintsHeader', valueType: 'string', elementId: 'txtPromptSemanticHintsHeader', description: 'Encabezado para el bloque de pistas semánticas del prompt SQL.' },
    { key: 'AllowedObjectsHeader', valueType: 'string', elementId: 'txtPromptAllowedObjectsHeader', description: 'Encabezado para el bloque de objetos permitidos del prompt SQL.' },
    { key: 'SchemasHeader', valueType: 'string', elementId: 'txtPromptSchemasHeader', description: 'Encabezado para el bloque de schema docs del prompt SQL.' },
    { key: 'ExamplesHeader', valueType: 'string', elementId: 'txtPromptExamplesHeader', description: 'Encabezado para el bloque de examples del prompt SQL.' },
    { key: 'QuestionHeader', valueType: 'string', elementId: 'txtPromptQuestionHeader', description: 'Encabezado para la pregunta del usuario en el prompt SQL.' },
    { key: 'MaxPromptChars', valueType: 'int', elementId: 'txtPromptMaxPromptChars', description: 'Presupuesto total del prompt SQL en caracteres.' },
    { key: 'MaxRulesChars', valueType: 'int', elementId: 'txtPromptMaxRulesChars', description: 'Presupuesto máximo para reglas de negocio en el prompt SQL.' },
    { key: 'MaxSemanticHintsChars', valueType: 'int', elementId: 'txtPromptMaxSemanticHintsChars', description: 'Presupuesto máximo para pistas semánticas del prompt SQL.' },
    { key: 'MaxSchemasChars', valueType: 'int', elementId: 'txtPromptMaxSchemasChars', description: 'Presupuesto máximo para schema docs en el prompt SQL.' },
    { key: 'MaxExamplesChars', valueType: 'int', elementId: 'txtPromptMaxExamplesChars', description: 'Presupuesto máximo para examples en el prompt SQL.' },
    { key: 'MaxRules', valueType: 'int', elementId: 'txtPromptMaxRules', description: 'Cantidad máxima de business rules enviadas al prompt SQL.' },
    { key: 'MaxSemanticHints', valueType: 'int', elementId: 'txtPromptMaxSemanticHints', description: 'Cantidad máxima de pistas semánticas enviadas al prompt SQL.' },
    { key: 'MaxSchemas', valueType: 'int', elementId: 'txtPromptMaxSchemas', description: 'Cantidad máxima de schema docs enviados al prompt SQL.' },
    { key: 'MaxExamples', valueType: 'int', elementId: 'txtPromptMaxExamples', description: 'Cantidad máxima de training examples enviados al prompt SQL.' }
];

const retrievalConfigFields = [
    { section: 'Retrieval', key: 'Domain', valueType: 'string', elementId: 'txtRetrievalDomain', description: 'Dominio operativo para retrieval y validación.' },
    { section: 'UiDefaults', key: 'AdminDomain', valueType: 'string', elementId: 'txtUiAdminDomain', description: 'Dominio por defecto para pantallas administrativas.' },
    { section: 'Retrieval', key: 'TopExamples', valueType: 'int', elementId: 'txtRetrievalTopExamples', description: 'Cantidad de training examples candidatos para retrieval.' },
    { section: 'Retrieval', key: 'MinExampleScore', valueType: 'double', elementId: 'txtRetrievalMinExampleScore', description: 'Score mínimo para considerar un training example relevante.' },
    { section: 'Retrieval', key: 'TopSchemaDocs', valueType: 'int', elementId: 'txtRetrievalTopSchemaDocs', description: 'Cantidad de schema docs relevantes a incluir.' },
    { section: 'Retrieval', key: 'FallbackSchemaDocs', valueType: 'int', elementId: 'txtRetrievalFallbackSchemaDocs', description: 'Cantidad de schema docs de fallback cuando no hay match fuerte.' }
];

const timeScopeTermGroups = new Set([
    'time_scope_today',
    'time_scope_yesterday',
    'time_scope_current_week',
    'time_scope_current_month',
    'time_scope_current_shift'
]);

const onboardingTourSteps = [
    {
        targetId: 'onboardingStepPanel1',
        stepLabel: 'Paso 1',
        title: 'Configura el workspace',
        body: 'Aquí defines el workspace, el contexto de datos y la conexión que usará todo el flujo. Cuando guardas este paso, habilitas el resto del wizard.'
    },
    {
        targetId: 'onboardingStepPanel2b',
        stepLabel: 'Paso 2',
        title: 'Elige las tablas permitidas',
        body: 'Descubre el schema y deja marcadas solo las tablas o vistas que el motor podrá consultar. Aquí defines el perímetro seguro del dominio.'
    },
    {
        targetId: 'onboardingStepPanel3',
        stepLabel: 'Paso 3',
        title: 'Prepara el dominio',
        body: 'Este paso genera el contexto técnico del motor: schema docs y pistas del dominio. Cuando sale bien, ya puedes probar una pregunta real.'
    },
    {
        targetId: 'onboardingStepPanel4',
        stepLabel: 'Paso 4',
        title: 'Haz una prueba real',
        body: 'Corre una pregunta guiada contra el pipeline real. El wizard te muestra el SQL generado, el resultado y si el dominio ya respondió correctamente.'
    },
    {
        targetId: 'onboardingStepPanel5',
        stepLabel: 'Checklist final',
        title: 'Confirma readiness',
        body: 'Este resumen te dice si el dominio ya está listo para usuarios internos o si todavía necesita más curación antes de salir a operación.'
    }
];

function getOnboardingDefaults() {
    return onboardingBootstrap?.defaults || onboardingBootstrap?.Defaults || {};
}

function getOnboardingProfile() {
    return onboardingBootstrap?.profile || onboardingBootstrap?.Profile || {};
}

function getAdminScopedTabConfig(tabKey) {
    const configs = {
        'allowed': {
            label: 'Allowed Objects',
            listId: 'allowedList',
            countId: 'allowedCount',
            filterId: 'txtAllowedDomainFilter',
            editorDomainId: 'txtAllowedDomain',
            hintId: 'allowedContextHint',
            contextBannerId: 'allowedActiveContextBanner',
            bannerId: 'allowedBanner',
            load: loadAllowedObjects,
            reset: resetAllowedObjectForm
        },
        'business-rules': {
            label: 'Business Rules',
            listId: 'businessRuleList',
            countId: 'businessRuleCount',
            filterId: 'txtBusinessRuleDomainFilter',
            editorDomainId: 'txtBusinessRuleDomain',
            hintId: 'businessRuleContextHint',
            contextBannerId: 'businessRuleActiveContextBanner',
            bannerId: 'businessRuleBanner',
            load: loadBusinessRules,
            reset: resetBusinessRuleForm
        },
        'semantics': {
            label: 'Semantic Hints',
            listId: 'semanticHintList',
            countId: 'semanticHintCount',
            filterId: 'txtSemanticHintDomainFilter',
            editorDomainId: 'txtSemanticHintDomain',
            hintId: 'semanticHintContextHint',
            contextBannerId: 'semanticHintActiveContextBanner',
            bannerId: 'semanticHintBanner',
            load: loadSemanticHints,
            reset: resetSemanticHintForm
        },
        'patterns': {
            label: 'Query Patterns',
            listId: 'queryPatternList',
            countId: 'queryPatternCount',
            filterId: 'txtQueryPatternDomainFilter',
            editorDomainId: 'txtQueryPatternDomain',
            hintId: 'queryPatternContextHint',
            contextBannerId: 'queryPatternActiveContextBanner',
            bannerId: 'queryPatternBanner',
            load: loadQueryPatterns,
            reset: resetQueryPatternForm
        }
    };

    return configs[tabKey] || null;
}

function normalizeAdminContext(rawContext) {
    if (!rawContext) return null;

    const tenantKey = String(rawContext.tenantKey || rawContext.TenantKey || '').trim();
    const tenantDisplayName = String(rawContext.tenantDisplayName || rawContext.TenantDisplayName || rawContext.displayName || rawContext.DisplayName || tenantKey).trim();
    const domain = String(rawContext.domain || rawContext.Domain || '').trim();
    const connectionName = String(rawContext.connectionName || rawContext.ConnectionName || '').trim();
    const systemProfileKey = String(rawContext.systemProfileKey || rawContext.SystemProfileKey || 'default').trim() || 'default';

    if (!tenantKey || !domain || !connectionName) return null;

    return {
        tenantKey,
        tenantDisplayName: tenantDisplayName || tenantKey,
        domain,
        connectionName,
        systemProfileKey
    };
}

function getCurrentAdminScopedTabKey() {
    if (document.getElementById('pane-allowed')?.classList.contains('active')) return 'allowed';
    if (document.getElementById('pane-business-rules')?.classList.contains('active')) return 'business-rules';
    if (document.getElementById('pane-semantics')?.classList.contains('active')) return 'semantics';
    if (document.getElementById('pane-patterns')?.classList.contains('active')) return 'patterns';
    return null;
}

function setScopedTabHint(config, message) {
    if (!config?.hintId) return;
    const hint = document.getElementById(config.hintId);
    if (hint) hint.textContent = message;
}

function setScopedTabContextBanner(config, message, isEmpty = false) {
    if (!config?.contextBannerId) return;
    const banner = document.getElementById(config.contextBannerId);
    if (!banner) return;
    banner.textContent = message;
    banner.classList.toggle('is-empty', !!isEmpty);
}

function renderScopedTabEmptyState(config, message) {
    const list = document.getElementById(config.listId);
    const count = document.getElementById(config.countId);
    if (count) count.textContent = '0';
    if (!list) return;

    list.innerHTML = `
        <div class="empty-state">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <path d="M4 12h16"></path>
                <path d="M12 4v16"></path>
                <circle cx="12" cy="12" r="9"></circle>
            </svg>
            ${escHtml(message)}
        </div>`;
}

function clearAdminScopedTabCollections(tabKey) {
    if (tabKey === 'allowed') globalAllowedObjects = [];
    if (tabKey === 'business-rules') globalBusinessRules = [];
    if (tabKey === 'semantics') globalSemanticHints = [];
    if (tabKey === 'patterns') {
        globalQueryPatterns = [];
        globalQueryPatternTerms = [];
        selectedQueryPatternTermId = null;
    }
}

function getActiveAdminContext() {
    return globalAdminActiveContext;
}

function syncAdminScopedFiltersToContext(context) {
    const normalized = normalizeAdminContext(context);
    const domain = normalized?.domain || '';
    const tenantDisplayName = normalized?.tenantDisplayName || normalized?.tenantKey || 'sin workspace';
    const connectionName = normalized?.connectionName || 'sin conexión';

    defaultAllowedDomain = domain;
    defaultBusinessRuleDomain = domain;
    defaultSemanticHintDomain = domain;
    defaultQueryPatternDomain = domain;

    const configs = ['allowed', 'business-rules', 'semantics', 'patterns']
        .map(getAdminScopedTabConfig)
        .filter(Boolean);

    configs.forEach(config => {
        setValue(config.filterId, domain);
        if (!getValue(config.editorDomainId).trim()) {
            setValue(config.editorDomainId, domain);
        }

        if (domain) {
            setScopedTabHint(config, `Contexto activo: ${tenantDisplayName} / ${domain} / ${connectionName}`);
            setScopedTabContextBanner(config, `${tenantDisplayName} / ${domain} / ${connectionName}`);
        } else {
            setScopedTabHint(config, `Selecciona un contexto en Onboarding para ver ${config.label}.`);
            setScopedTabContextBanner(config, 'Selecciona un workspace y un contexto válido en Onboarding.', true);
        }
    });
}

function renderContextRequiredState(tabKey, message = 'Selecciona primero un contexto en Onboarding.') {
    const config = getAdminScopedTabConfig(tabKey);
    if (!config) return;

    clearAdminScopedTabCollections(tabKey);
    config.reset?.();
    setValue(config.filterId, '');
    setValue(config.editorDomainId, '');
    setScopedTabHint(config, message);
    setScopedTabContextBanner(config, message, true);
    renderScopedTabEmptyState(config, message);
}

async function activateAdminScopedTab(tabKey) {
    if (!adminScopedTabs.has(tabKey)) return;

    const context = getActiveAdminContext();
    if (!context?.domain) {
        renderContextRequiredState(tabKey, 'Selecciona primero un workspace y un contexto válido en Onboarding.');
        return;
    }

    syncAdminScopedFiltersToContext(context);
    const config = getAdminScopedTabConfig(tabKey);
    if (config?.load) {
        await config.load();
    }
}

async function setAdminActiveContext(rawContext, options = {}) {
    const normalized = normalizeAdminContext(rawContext);
    globalAdminActiveContext = normalized;
    syncAdminScopedFiltersToContext(normalized);

    const currentScopedTab = getCurrentAdminScopedTabKey();
    if (options.reloadCurrentTab && currentScopedTab) {
        if (normalized) {
            await activateAdminScopedTab(currentScopedTab);
        } else {
            renderContextRequiredState(currentScopedTab, 'Selecciona primero un workspace y un contexto válido en Onboarding.');
        }
    }
}

function buildAdminContextFromOnboardingForm() {
    return normalizeAdminContext({
        tenantKey: getValue('txtOnboardingTenantKey').trim(),
        tenantDisplayName: getValue('txtOnboardingDisplayName').trim(),
        domain: getValue('txtOnboardingDomain').trim(),
        connectionName: getValue('txtOnboardingConnectionName').trim(),
        systemProfileKey: getValue('txtOnboardingSystemProfileKey').trim()
    });
}

// -----------------------------------------------------------
// TAB SWITCHING
// -----------------------------------------------------------
async function switchTab(t) {
    if (t !== 'onboarding') {
        closeOnboardingTour();
    }

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

    await activateAdminScopedTab(t);
}

// -----------------------------------------------------------
// ONBOARDING TAB
// -----------------------------------------------------------
async function loadOnboardingBootstrap() {
    const list = document.getElementById('onboardingTenantList');
    if (!list) return;

    try {
        const res = await fetch('/api/admin/onboarding/bootstrap');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        onboardingBootstrap = await res.json();
        globalTenants = Array.isArray(onboardingBootstrap?.tenants) ? onboardingBootstrap.tenants : (Array.isArray(onboardingBootstrap?.Tenants) ? onboardingBootstrap.Tenants : []);
        globalConnectionProfiles = Array.isArray(onboardingBootstrap?.connections) ? onboardingBootstrap.connections : (Array.isArray(onboardingBootstrap?.Connections) ? onboardingBootstrap.Connections : []);
        globalOnboardingRuntimeContexts = Array.isArray(onboardingBootstrap?.runtimeContexts) ? onboardingBootstrap.runtimeContexts : (Array.isArray(onboardingBootstrap?.RuntimeContexts) ? onboardingBootstrap.RuntimeContexts : []);
        globalTenantDomains = [];

        populateOnboardingSummary();
        populateOnboardingConnectionOptions();
        renderOnboardingRuntimeContexts();
        renderOnboardingTenantList();

        const persistedWorkspace = readOnboardingWorkspaceState();
        const defaults = getOnboardingDefaults();
        const defaultTenantKey = persistedWorkspace?.tenantKey || defaults.tenantKey || defaults.TenantKey || defaultAdminTenant || 'default';
        const hasTenantInBootstrap = !!globalTenants.find(x => String(x.tenantKey || x.TenantKey || '') === String(defaultTenantKey));
        if (defaultTenantKey && hasTenantInBootstrap) {
            await selectOnboardingTenant(defaultTenantKey);
        } else if (persistedWorkspace?.tenantKey || persistedWorkspace?.domain || persistedWorkspace?.connectionName) {
            await applyPersistedOnboardingWorkspaceState(persistedWorkspace);
        } else {
            resetOnboardingForm();
        }
    } catch (e) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="12" cy="12" r="10"></circle>
                    <path d="M8 12h8"></path>
                    <path d="M12 8v8"></path>
                </svg>
                Error al cargar onboarding bootstrap
            </div>`;
    }
}

function populateOnboardingSummary() {
    const defaults = getOnboardingDefaults();
    const profile = getOnboardingProfile();
    setText('txtOnboardingEnvironment', onboardingBootstrap?.environmentName || onboardingBootstrap?.EnvironmentName || 'Development');
    setText('txtOnboardingSystemProfile', profile.profileKey || profile.ProfileKey || defaults.systemProfileKey || defaults.SystemProfileKey || 'default');
    setText('txtOnboardingConnectionCount', String(globalConnectionProfiles.length || 0));
}

function populateOnboardingConnectionOptions() {
    const select = document.getElementById('txtOnboardingConnectionName');
    if (!select) return;

    if (!globalConnectionProfiles.length) {
        select.innerHTML = '<option value="OperationalDb">OperationalDb</option>';
        renderOnboardingConnectionCatalog();
        return;
    }

    select.innerHTML = globalConnectionProfiles.map(profile => {
        const connectionName = profile.connectionName || profile.ConnectionName || 'OperationalDb';
        const profileKey = profile.profileKey || profile.ProfileKey || 'default';
        const databaseName = profile.databaseName || profile.DatabaseName || '—';
        const isActive = !!(profile.isActive || profile.IsActive);
        const label = `${connectionName} · ${databaseName} · ${profileKey}${isActive ? ' · activo' : ''}`;
        return `<option value="${escHtml(connectionName)}">${escHtml(label)}</option>`;
    }).join('');

    renderOnboardingConnectionCatalog();
}

function renderOnboardingConnectionCatalog() {
    const list = document.getElementById('onboardingConnectionCatalog');
    if (!list) return;

    if (!globalConnectionProfiles.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <ellipse cx="12" cy="5" rx="7" ry="3"></ellipse>
                    <path d="M5 5v14c0 1.66 3.1 3 7 3s7-1.34 7-3V5"></path>
                </svg>
                Aún no hay conexiones configuradas
            </div>`;
        return;
    }

    const selectedConnectionName = getValue('txtOnboardingConnectionName').trim();
    list.innerHTML = globalConnectionProfiles.map(profile => {
        const connectionName = profile.connectionName || profile.ConnectionName || 'OperationalDb';
        const profileKey = profile.profileKey || profile.ProfileKey || 'default';
        const databaseName = profile.databaseName || profile.DatabaseName || '—';
        const serverHost = profile.serverHost || profile.ServerHost || '—';
        const description = profile.description || profile.Description || '';
        const isActive = !!(profile.isActive || profile.IsActive);
        const isSelected = selectedConnectionName && selectedConnectionName === connectionName;
        return `
            <div class="history-item onboarding-connection-card ${isSelected ? 'is-selected' : ''}" onclick="applyOnboardingConnection('${jsString(connectionName)}')">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(connectionName)}</div>
                    <div class="hi-time">${escHtml(databaseName)}</div>
                </div>
                <div class="onboarding-connection-meta">${escHtml(serverHost)} · Perfil ${escHtml(profileKey)}</div>
                <div class="hi-badges">
                    <span class="hi-verify ${isActive ? 'verified' : 'rejected'}">${isActive ? 'Activa' : 'Inactiva'}</span>
                    ${description ? `<span class="hi-status ok"><span class="dot"></span>${escHtml(description)}</span>` : ''}
                </div>
            </div>`;
    }).join('');
}

function renderOnboardingRuntimeContexts() {
    const list = document.getElementById('onboardingRuntimeContextList');
    if (!list) return;

    const selectedTenantKey = getValue('txtOnboardingTenantKey').trim() || selectedOnboardingTenantKey || '';
    const visibleRuntimeContexts = selectedTenantKey
        ? globalOnboardingRuntimeContexts.filter(item =>
            String(item.tenantKey || item.TenantKey || '') === String(selectedTenantKey))
        : globalOnboardingRuntimeContexts;

    if (!visibleRuntimeContexts.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M4 6h16"></path>
                    <path d="M4 12h16"></path>
                    <path d="M4 18h16"></path>
                </svg>
                ${selectedTenantKey ? 'Este workspace no tiene contextos runtime disponibles' : 'No hay contextos disponibles en runtime'}
            </div>`;
        return;
    }

    const selectedDomain = getValue('txtOnboardingDomain').trim();
    const selectedConnectionName = getValue('txtOnboardingConnectionName').trim();

    list.innerHTML = visibleRuntimeContexts.map(item => {
        const tenantKey = item.tenantKey || item.TenantKey || '';
        const tenantDisplayName = item.tenantDisplayName || item.TenantDisplayName || tenantKey;
        const domain = item.domain || item.Domain || '';
        const connectionName = item.connectionName || item.ConnectionName || 'OperationalDb';
        const profileKey = item.systemProfileKey || item.SystemProfileKey || 'default';
        const isDefault = !!(item.isDefault ?? item.IsDefault);
        const isSelected = tenantKey === selectedTenantKey && domain === selectedDomain && connectionName === selectedConnectionName;

        return `
            <div class="history-item onboarding-mapping is-clickable ${isSelected ? 'is-selected' : ''}" onclick="applyOnboardingRuntimeContext('${jsString(tenantKey)}','${jsString(domain)}','${jsString(connectionName)}','${jsString(profileKey)}')">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(tenantDisplayName)} · ${escHtml(domain)}</div>
                    <div class="hi-time">${escHtml(connectionName)}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-verify ${isDefault ? 'verified' : 'pending'}">${isDefault ? 'Default runtime' : 'Runtime'}</span>
                    <span class="hi-status ok"><span class="dot"></span>${escHtml(profileKey)}</span>
                </div>
            </div>`;
    }).join('');
}

function applyOnboardingConnection(connectionName) {
    if (!connectionName) return;
    setValue('txtOnboardingConnectionName', connectionName);
    persistOnboardingWorkspaceState();
    setOnboardingMeta(null);
    renderTenantDomains();
    renderOnboardingConnectionCatalog();
    renderOnboardingRuntimeContexts();
    renderOnboardingActionGuidance();
    updateOnboardingStepper();
    hideOnboardingBanner();
}

function renderOnboardingTenantList() {
    const list = document.getElementById('onboardingTenantList');
    const count = document.getElementById('onboardingTenantCount');
    if (!list) return;

    const persistedWorkspace = readOnboardingWorkspaceState();
    const hasPersistedDraft = !!(persistedWorkspace?.tenantKey || persistedWorkspace?.domain || persistedWorkspace?.connectionName);
    const runtimeContextCount = Array.isArray(globalOnboardingRuntimeContexts) ? globalOnboardingRuntimeContexts.length : 0;
    const effectiveCount = Math.max(globalTenants.length, runtimeContextCount, hasPersistedDraft ? 1 : 0);
    if (count) count.textContent = String(effectiveCount);

    const tenantCards = globalTenants.map(tenant => {
        const tenantKey = tenant.tenantKey || tenant.TenantKey || '';
        const displayName = tenant.displayName || tenant.DisplayName || tenantKey;
        const isActive = !!(tenant.isActive || tenant.IsActive);
        const tenantContextCount = globalOnboardingRuntimeContexts.filter(x =>
            String(x.tenantKey || x.TenantKey || '') === String(tenantKey)).length;

        return `
            <div class="history-item ${selectedOnboardingTenantKey === tenantKey ? 'selected' : ''}" id="tenant-${escAttr(tenantKey)}" onclick="selectOnboardingTenant('${jsString(tenantKey)}')">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(displayName)}</div>
                    <div class="hi-time">${escHtml(tenantKey)}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-verify ${isActive ? 'verified' : 'rejected'}">${isActive ? 'Workspace activo' : 'Workspace inactivo'}</span>
                    ${tenantContextCount ? `<span class="hi-status ok"><span class="dot"></span>${tenantContextCount} contexto${tenantContextCount === 1 ? '' : 's'}</span>` : ''}
                </div>
            </div>`;
    });

    const visibleRuntimeContexts = selectedOnboardingTenantKey
        ? globalOnboardingRuntimeContexts.filter(item =>
            String(item.tenantKey || item.TenantKey || '') === String(selectedOnboardingTenantKey))
        : globalOnboardingRuntimeContexts;

    const runtimeCards = visibleRuntimeContexts.map(item => {
        const tenantKey = item.tenantKey || item.TenantKey || '';
        const tenantDisplayName = item.tenantDisplayName || item.TenantDisplayName || tenantKey;
        const domain = item.domain || item.Domain || '';
        const connectionName = item.connectionName || item.ConnectionName || 'OperationalDb';
        const profileKey = item.systemProfileKey || item.SystemProfileKey || 'default';
        const isDefault = !!(item.isDefault ?? item.IsDefault);
        const isSelected =
            tenantKey === getValue('txtOnboardingTenantKey').trim()
            && domain === getValue('txtOnboardingDomain').trim()
            && connectionName === getValue('txtOnboardingConnectionName').trim();

        return `
            <div class="history-item onboarding-context-card ${isSelected ? 'selected' : ''}" onclick="applyOnboardingRuntimeContext('${jsString(tenantKey)}','${jsString(domain)}','${jsString(connectionName)}','${jsString(profileKey)}')">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(tenantDisplayName)} · ${escHtml(domain)}</div>
                    <div class="hi-time">${escHtml(connectionName)}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-verify ${isDefault ? 'verified' : 'pending'}">${isDefault ? 'Contexto default' : 'Contexto runtime'}</span>
                    <span class="hi-status ok"><span class="dot"></span>${escHtml(profileKey)}</span>
                </div>
            </div>`;
    });

    if (!tenantCards.length && !runtimeCards.length) {
        if (hasPersistedDraft) {
            const tenantKey = persistedWorkspace.tenantKey || 'workspace-actual';
            const displayName = persistedWorkspace.displayName || persistedWorkspace.tenantKey || 'Workspace actual';
            list.innerHTML = `
                <div class="history-item draft-workspace ${selectedOnboardingTenantKey === tenantKey ? 'selected' : ''}" onclick="applyPersistedOnboardingWorkspaceState(readOnboardingWorkspaceState())">
                    <div class="hi-top">
                        <div class="hi-question">${escHtml(displayName)}</div>
                        <div class="hi-time">${escHtml(tenantKey)}</div>
                    </div>
                    <div class="hi-badges">
                        <span class="hi-verify pending">Actual</span>
                        <span class="hi-status ok"><span class="dot"></span>Borrador local</span>
                    </div>
                </div>`;
            return;
        }

        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M12 3v18"></path>
                    <path d="M3 12h18"></path>
                    <circle cx="12" cy="12" r="9"></circle>
                </svg>
                Aún no hay workspaces registrados
            </div>`;
        return;
    }

    const sections = [];
    if (tenantCards.length) {
        sections.push(`<div class="onboarding-list-heading">Workspaces</div>${tenantCards.join('')}`);
    }
    if (runtimeCards.length) {
        sections.push(`<div class="onboarding-list-heading">${selectedOnboardingTenantKey ? 'Contextos del workspace seleccionado' : 'Contextos disponibles'}</div>${runtimeCards.join('')}`);
    }

    list.innerHTML = sections.join('');
}

async function selectOnboardingTenant(tenantKey) {
    if (!tenantKey) return;
    selectedOnboardingTenantKey = tenantKey;
    renderOnboardingTenantList();

    const tenant = globalTenants.find(x => String(x.tenantKey || x.TenantKey || '') === String(tenantKey));
    if (!tenant) {
        resetOnboardingForm();
        return;
    }

    setValue('txtOnboardingTenantKey', tenant.tenantKey || tenant.TenantKey || '');
    setValue('txtOnboardingDisplayName', tenant.displayName || tenant.DisplayName || '');
    setValue('txtOnboardingDescription', tenant.description || tenant.Description || '');
    const defaults = getOnboardingDefaults();
    setValue('txtOnboardingSystemProfileKey', defaults.systemProfileKey || defaults.SystemProfileKey || 'default');
    toggleOnboardingAdvancedOptions((getValue('txtOnboardingSystemProfileKey').trim() || 'default') !== 'default');

    await loadTenantDomains(tenantKey);
    await loadOnboardingStatus();
    persistOnboardingWorkspaceState();
    renderOnboardingConnectionCatalog();

    if (getValue('txtOnboardingDomain').trim() && getValue('txtOnboardingConnectionName').trim()) {
        await loadOnboardingSchemaCandidates();
    }

    await setAdminActiveContext(buildAdminContextFromOnboardingForm(), { reloadCurrentTab: true });
}

async function loadTenantDomains(tenantKey) {
    const list = document.getElementById('onboardingTenantDomainList');
    if (!list || !tenantKey) return;

    list.innerHTML = `
        <div class="empty-state">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <path d="M4 6h16"></path>
                <path d="M4 12h16"></path>
                <path d="M4 18h16"></path>
            </svg>
            Cargando mappings...
        </div>`;

    try {
        const res = await fetch(`/api/admin/tenant-domains?tenantKey=${encodeURIComponent(tenantKey)}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        globalTenantDomains = await res.json();
        renderTenantDomains();

        const defaultMapping = globalTenantDomains.find(x => !!(x.isDefault ?? x.IsDefault)) || globalTenantDomains[0];
        if (defaultMapping) {
            setValue('txtOnboardingDomain', defaultMapping.domain || defaultMapping.Domain || '');
            const defaults = getOnboardingDefaults();
            setValue('txtOnboardingConnectionName', defaultMapping.connectionName || defaultMapping.ConnectionName || defaults.connectionName || defaults.ConnectionName || 'OperationalDb');
            setValue('txtOnboardingSystemProfileKey', defaultMapping.systemProfileKey || defaultMapping.SystemProfileKey || defaults.systemProfileKey || defaults.SystemProfileKey || 'default');
            setOnboardingMeta(defaultMapping);
        } else {
            const persistedWorkspace = readOnboardingWorkspaceState();
            const defaults = getOnboardingDefaults();
            setValue('txtOnboardingDomain', persistedWorkspace?.domain || defaults.domain || defaults.Domain || defaultAllowedDomain || '');
            setValue('txtOnboardingConnectionName', persistedWorkspace?.connectionName || defaults.connectionName || defaults.ConnectionName || 'OperationalDb');
            setValue('txtOnboardingSystemProfileKey', persistedWorkspace?.systemProfileKey || defaults.systemProfileKey || defaults.SystemProfileKey || 'default');
            setOnboardingMeta(null);
        }
        toggleOnboardingAdvancedOptions((getValue('txtOnboardingSystemProfileKey').trim() || 'default') !== 'default');
        persistOnboardingWorkspaceState();
    } catch (e) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="12" cy="12" r="10"></circle>
                    <path d="M8 12h8"></path>
                    <path d="M12 8v8"></path>
                </svg>
                Error al cargar mappings
            </div>`;
    }
}

function renderTenantDomains() {
    const list = document.getElementById('onboardingTenantDomainList');
    if (!list) return;

    if (!globalTenantDomains.length) {
        const currentDomain = getValue('txtOnboardingDomain').trim();
        const currentConnection = getValue('txtOnboardingConnectionName').trim();
        const currentProfile = getValue('txtOnboardingSystemProfileKey').trim() || 'default';
        if (currentDomain || currentConnection) {
            list.innerHTML = `
                <div class="history-item onboarding-mapping draft-workspace">
                    <div class="hi-top">
                        <div class="hi-question">${escHtml(currentDomain || 'sin-domain')}</div>
                        <div class="hi-time">${escHtml(currentConnection || 'sin-conexion')}</div>
                    </div>
                    <div class="hi-badges">
                        <span class="hi-verify pending">Actual</span>
                        <span class="hi-status ok"><span class="dot"></span>${escHtml(currentProfile)}</span>
                    </div>
                </div>`;
            return;
        }

        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M4 6h16"></path>
                    <path d="M4 12h16"></path>
                    <path d="M4 18h16"></path>
                </svg>
                Este workspace aún no tiene mappings
            </div>`;
        return;
    }

    list.innerHTML = globalTenantDomains.map(item => {
        const domain = item.domain || item.Domain || '';
        const connectionName = item.connectionName || item.ConnectionName || 'OperationalDb';
        const profileKey = item.systemProfileKey || item.SystemProfileKey || 'default';
        const isDefault = !!(item.isDefault ?? item.IsDefault);
        const isSelected =
            domain === getValue('txtOnboardingDomain').trim()
            && connectionName === getValue('txtOnboardingConnectionName').trim();
        return `
            <div class="history-item onboarding-mapping is-clickable ${isSelected ? 'is-selected' : ''}" onclick="applyOnboardingMapping('${jsString(domain)}','${jsString(connectionName)}','${jsString(profileKey)}')">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(domain)}</div>
                    <div class="hi-time">${escHtml(connectionName)}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-verify ${isDefault ? 'verified' : 'pending'}">${isDefault ? 'Default' : 'Mapping'}</span>
                    <span class="hi-status ok"><span class="dot"></span>${escHtml(profileKey)}</span>
                </div>
            </div>`;
    }).join('');
}

function applyOnboardingMapping(domain, connectionName, profileKey) {
    setValue('txtOnboardingDomain', domain || '');
    setValue('txtOnboardingConnectionName', connectionName || 'OperationalDb');
    setValue('txtOnboardingSystemProfileKey', profileKey || 'default');
    toggleOnboardingAdvancedOptions((getValue('txtOnboardingSystemProfileKey').trim() || 'default') !== 'default');
    persistOnboardingWorkspaceState();
    setOnboardingMeta({
        Domain: domain,
        ConnectionName: connectionName,
        SystemProfileKey: profileKey
    });
    renderTenantDomains();
    renderOnboardingConnectionCatalog();
    renderOnboardingRuntimeContexts();
    renderOnboardingActionGuidance();
    updateOnboardingStepper();
    hideOnboardingBanner();
    setAdminActiveContext(buildAdminContextFromOnboardingForm(), { reloadCurrentTab: true });
}

async function applyOnboardingRuntimeContext(tenantKey, domain, connectionName, profileKey) {
    const tenantExists = globalTenants.some(x => String(x.tenantKey || x.TenantKey || '') === String(tenantKey));

    if (tenantExists) {
        await selectOnboardingTenant(tenantKey);
    } else {
        setValue('txtOnboardingTenantKey', tenantKey || '');
        setValue('txtOnboardingDisplayName', tenantKey || '');
    }

    setValue('txtOnboardingDomain', domain || '');
    setValue('txtOnboardingConnectionName', connectionName || 'OperationalDb');
    setValue('txtOnboardingSystemProfileKey', profileKey || 'default');
    toggleOnboardingAdvancedOptions((getValue('txtOnboardingSystemProfileKey').trim() || 'default') !== 'default');
    persistOnboardingWorkspaceState();
    setOnboardingMeta({
        Domain: domain,
        ConnectionName: connectionName,
        SystemProfileKey: profileKey
    });
    resetOnboardingValidation();

    if (getValue('txtOnboardingDomain').trim() && getValue('txtOnboardingConnectionName').trim()) {
        await loadOnboardingStatus();
        await loadOnboardingSchemaCandidates();
    } else {
        globalOnboardingSchemaCandidates = [];
        renderOnboardingSchemaCandidates();
        globalOnboardingStatus = null;
        renderOnboardingStatus();
    }

    renderTenantDomains();
    renderOnboardingConnectionCatalog();
    renderOnboardingRuntimeContexts();
    renderOnboardingActionGuidance();
    updateOnboardingStepper();
    hideOnboardingBanner();
    await setAdminActiveContext(buildAdminContextFromOnboardingForm(), { reloadCurrentTab: true });
}

function resetOnboardingForm() {
    closeOnboardingTour();
    selectedOnboardingTenantKey = null;
    renderOnboardingTenantList();
    globalTenantDomains = [];
    renderTenantDomains();
    globalOnboardingSchemaCandidates = [];
    renderOnboardingSchemaCandidates();
    globalOnboardingStatus = null;
    renderOnboardingStatus();
    resetOnboardingValidation();
    hideOnboardingPackBanner();

    const defaults = getOnboardingDefaults();
    setValue('txtOnboardingTenantKey', defaults.tenantKey || defaults.TenantKey || defaultAdminTenant || 'default');
    setValue('txtOnboardingDisplayName', '');
    setValue('txtOnboardingDescription', '');
    setValue('txtOnboardingDomain', defaults.domain || defaults.Domain || defaultAllowedDomain || '');
    setValue('txtOnboardingConnectionName', defaults.connectionName || defaults.ConnectionName || 'OperationalDb');
    setValue('txtOnboardingSystemProfileKey', defaults.systemProfileKey || defaults.SystemProfileKey || 'default');
    setValue('txtOnboardingDomainPackJson', '');
    toggleOnboardingAdvancedOptions(false);
    renderOnboardingConnectionCatalog();
    renderOnboardingRuntimeContexts();

    const meta = document.getElementById('onboardingMeta');
    if (meta) {
        meta.innerHTML = '<span class="meta-empty">Empieza guardando el workspace. Eso habilita el resto del wizard.</span>';
    }

    const packMeta = document.getElementById('onboardingPackMeta');
    if (packMeta) {
        packMeta.innerHTML = '<span class="meta-empty">Usa este bloque para mover el dominio a otro ambiente sin rehacer el onboarding completo.</span>';
    }

    hideOnboardingBanner();
    clearOnboardingStep1Errors();
    clearOnboardingConnectionErrors();
    clearOnboardingFieldError('validationQuestion');
    renderOnboardingActionGuidance();
    setAdminActiveContext(null, { reloadCurrentTab: true });
}

function persistOnboardingWorkspaceState() {
    try {
        const payload = {
            tenantKey: getValue('txtOnboardingTenantKey').trim() || null,
            displayName: getValue('txtOnboardingDisplayName').trim() || null,
            domain: getValue('txtOnboardingDomain').trim() || null,
            connectionName: getValue('txtOnboardingConnectionName').trim() || null,
            systemProfileKey: getValue('txtOnboardingSystemProfileKey').trim() || null,
            description: getValue('txtOnboardingDescription').trim() || null
        };

        localStorage.setItem(onboardingWorkspaceStateKey, JSON.stringify(payload));
    } catch {
        // no-op
    }
}

function readOnboardingWorkspaceState() {
    try {
        const raw = localStorage.getItem(onboardingWorkspaceStateKey);
        if (!raw) return null;
        return JSON.parse(raw);
    } catch {
        return null;
    }
}

function humanizeOnboardingFieldName(field) {
    const map = {
        tenantKey: 'Workspace / Tenant',
        displayName: 'Nombre visible',
        domain: 'Contexto de datos / Domain',
        connectionName: 'Conexión de base de datos',
        connectionString: 'Connection String',
        systemProfileKey: 'Perfil técnico'
    };

    return map[field] || field;
}

function getOnboardingStep1MissingFields() {
    const missing = [];
    if (!getValue('txtOnboardingTenantKey').trim()) missing.push('tenantKey');
    if (!getValue('txtOnboardingDisplayName').trim()) missing.push('displayName');
    if (!getValue('txtOnboardingDomain').trim()) missing.push('domain');
    if (!getValue('txtOnboardingConnectionName').trim()) missing.push('connectionName');
    return missing;
}

function formatOnboardingMissingFields(fields) {
    const labels = fields.map(humanizeOnboardingFieldName);
    if (!labels.length) return '';
    if (labels.length === 1) return labels[0];
    if (labels.length === 2) return `${labels[0]} y ${labels[1]}`;
    return `${labels.slice(0, -1).join(', ')} y ${labels.at(-1)}`;
}

function humanizeOnboardingErrorMessage(message) {
    if (!message) return message;

    return String(message)
        .replaceAll('TenantKey', 'Workspace / Tenant')
        .replaceAll('DisplayName', 'Nombre visible')
        .replaceAll('Domain', 'Contexto de datos / Domain')
        .replaceAll('ConnectionName', 'Conexión de base de datos')
        .replaceAll('ConnectionString', 'Connection String')
        .replaceAll('AllowedObjects', 'tablas permitidas')
        .replaceAll('SemanticHints', 'pistas del dominio')
        .replaceAll('SchemaDocs', 'contexto del schema');
}

function getOnboardingFieldErrorId(field) {
    const map = {
        tenantKey: 'errOnboardingTenantKey',
        displayName: 'errOnboardingDisplayName',
        domain: 'errOnboardingDomain',
        connectionName: 'errOnboardingConnectionName',
        newConnectionName: 'errOnboardingNewConnectionName',
        connectionString: 'errOnboardingNewConnectionString',
        validationQuestion: 'errOnboardingValidationQuestion'
    };
    return map[field] || null;
}

function getOnboardingFieldInputId(field) {
    const map = {
        tenantKey: 'txtOnboardingTenantKey',
        displayName: 'txtOnboardingDisplayName',
        domain: 'txtOnboardingDomain',
        connectionName: 'txtOnboardingConnectionName',
        newConnectionName: 'txtOnboardingNewConnectionName',
        connectionString: 'txtOnboardingNewConnectionString',
        validationQuestion: 'txtOnboardingValidationQuestion'
    };
    return map[field] || null;
}

function setOnboardingFieldError(field, message) {
    const errorId = getOnboardingFieldErrorId(field);
    const inputId = getOnboardingFieldInputId(field);
    const errorEl = errorId ? document.getElementById(errorId) : null;
    const inputEl = inputId ? document.getElementById(inputId) : null;

    if (errorEl) {
        errorEl.textContent = message || '';
        errorEl.classList.toggle('is-visible', !!message);
    }

    if (inputEl) {
        inputEl.classList.toggle('input-error', !!message);
    }
}

function clearOnboardingFieldError(field) {
    setOnboardingFieldError(field, '');
}

function clearOnboardingStep1Errors() {
    ['tenantKey', 'displayName', 'domain', 'connectionName'].forEach(clearOnboardingFieldError);
}

function validateOnboardingStep1Fields(showErrors = true) {
    const validations = [
        ['tenantKey', getValue('txtOnboardingTenantKey').trim(), 'Escribe una clave para identificar este workspace.'],
        ['displayName', getValue('txtOnboardingDisplayName').trim(), 'Escribe el nombre visible que verán los usuarios.'],
        ['domain', getValue('txtOnboardingDomain').trim(), 'Define el contexto de datos que usará este dominio.'],
        ['connectionName', getValue('txtOnboardingConnectionName').trim(), 'Selecciona una conexión de base de datos.']
    ];

    const missing = [];
    validations.forEach(([field, value, message]) => {
        if (!value) {
            missing.push(field);
            if (showErrors) setOnboardingFieldError(field, message);
        } else if (showErrors) {
            clearOnboardingFieldError(field);
        }
    });

    return missing;
}

function clearOnboardingConnectionErrors() {
    ['newConnectionName', 'connectionString'].forEach(clearOnboardingFieldError);
}

function validateOnboardingConnectionFields(showErrors = true) {
    const missing = [];
    const connectionName = getValue('txtOnboardingNewConnectionName').trim();
    const connectionString = getValue('txtOnboardingNewConnectionString').trim();

    if (!connectionName) {
        missing.push('connectionName');
        if (showErrors) setOnboardingFieldError('newConnectionName', 'Escribe un nombre para guardar esta conexión.');
    } else if (showErrors) {
        clearOnboardingFieldError('newConnectionName');
    }

    if (!connectionString) {
        missing.push('connectionString');
        if (showErrors) setOnboardingFieldError('connectionString', 'Pega la connection string completa antes de continuar.');
    } else if (showErrors) {
        clearOnboardingFieldError('connectionString');
    }

    return missing;
}

function validateOnboardingValidationQuestion(showErrors = true) {
    const question = getValue('txtOnboardingValidationQuestion').trim();
    if (!question) {
        if (showErrors) setOnboardingFieldError('validationQuestion', 'Escribe o selecciona una pregunta de prueba.');
        return false;
    }

    if (showErrors) clearOnboardingFieldError('validationQuestion');
    return true;
}

function renderOnboardingActionGuidance() {
    const meta = document.getElementById('onboardingMeta');
    if (!meta) return;

    const health = globalOnboardingStatus?.health ?? globalOnboardingStatus?.Health ?? null;
    const hasWorkspace = !!getValue('txtOnboardingTenantKey').trim() && !!getValue('txtOnboardingDomain').trim() && !!getValue('txtOnboardingConnectionName').trim();
    const hasAllowedObjects = !!(health?.hasAllowedObjects ?? health?.HasAllowedObjects);
    const hasSchemaDocs = !!(health?.hasSchemaDocs ?? health?.HasSchemaDocs);
    const hasSemanticHints = !!(health?.hasSemanticHints ?? health?.HasSemanticHints);
    const hasAllowedSelection = globalOnboardingSchemaCandidates.some(x => !!x.isSelected) || hasAllowedObjects;
    const isInitialized = hasSchemaDocs && hasSemanticHints;
    const hasValidation = isOnboardingValidationSuccessful(globalOnboardingValidation);

    if (!hasWorkspace) {
        meta.innerHTML = '<span class="meta-empty">Empieza guardando el workspace. Eso habilita el resto del wizard.</span>';
        return;
    }

    if (!globalOnboardingSchemaCandidates.length && !hasAllowedObjects) {
        meta.innerHTML = '<span class="meta-empty">Siguiente paso: descubre el schema para elegir las tablas que vas a permitir.</span>';
        return;
    }

    if (!hasAllowedSelection) {
        meta.innerHTML = '<span class="meta-empty">Siguiente paso: selecciona al menos una tabla o vista y guarda las tablas permitidas.</span>';
        return;
    }

    if (!isInitialized) {
        meta.innerHTML = '<span class="meta-empty">Siguiente paso: prepara el dominio para generar el contexto técnico del motor.</span>';
        return;
    }

    if (!hasValidation) {
        meta.innerHTML = '<span class="meta-empty">Siguiente paso: ejecuta una pregunta real para validar que el dominio responde correctamente.</span>';
        return;
    }

    meta.innerHTML = `
        <span class="meta-chip verify-ok">Onboarding básico completado</span>
        <span class="meta-chip training-no">${escHtml(getValue('txtOnboardingDomain').trim() || 'sin-domain')}</span>
        <span class="meta-chip training-no">${escHtml(getValue('txtOnboardingConnectionName').trim() || 'sin-conexion')}</span>`;
}

function scrollToOnboardingPanel(panelId) {
    const panel = document.getElementById(panelId);
    if (!panel) return;
    panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

function getCurrentOnboardingStepIndex() {
    const health = globalOnboardingStatus?.health ?? globalOnboardingStatus?.Health ?? null;
    const hasWorkspace = !!getValue('txtOnboardingTenantKey').trim() && !!getValue('txtOnboardingDomain').trim() && !!getValue('txtOnboardingConnectionName').trim();
    const hasAllowed = globalOnboardingSchemaCandidates.some(x => !!x.isSelected) || !!(health?.hasAllowedObjects ?? health?.HasAllowedObjects);
    const isInitialized = !!(health?.hasSchemaDocs ?? health?.HasSchemaDocs) && !!(health?.hasSemanticHints ?? health?.HasSemanticHints);
    const hasValidation = isOnboardingValidationSuccessful(globalOnboardingValidation);

    if (!hasWorkspace) return 0;
    if (!hasAllowed) return 1;
    if (!isInitialized) return 2;
    if (!hasValidation) return 3;
    return 4;
}

function buildOnboardingTourRuntimeNote(stepIndex) {
    const health = globalOnboardingStatus?.health ?? globalOnboardingStatus?.Health ?? null;
    const selectedTables = globalOnboardingSchemaCandidates.filter(x => !!x.isSelected).length;
    const allowedObjectsCount = globalOnboardingStatus?.allowedObjectsCount ?? globalOnboardingStatus?.AllowedObjectsCount ?? 0;
    const schemaDocsCount = globalOnboardingStatus?.schemaDocsCount ?? globalOnboardingStatus?.SchemaDocsCount ?? 0;
    const semanticHintsCount = globalOnboardingStatus?.semanticHintsCount ?? globalOnboardingStatus?.SemanticHintsCount ?? 0;
    const currentConnection = getValue('txtOnboardingConnectionName').trim() || 'sin conexión';

    switch (stepIndex) {
        case 0:
            return currentConnection === 'sin conexión'
                ? 'Ahora mismo todavía falta seleccionar una conexión para que el wizard pueda continuar.'
                : `La conexión actual del wizard es ${currentConnection}.`;
        case 1:
            if (!globalOnboardingSchemaCandidates.length) {
                return 'Todavía no hay schema cargado. El siguiente clic útil aquí es “Descubrir schema”.';
            }
            return `Ahora mismo hay ${selectedTables} objeto(s) seleccionados y ${allowedObjectsCount} ya guardado(s).`;
        case 2:
            return `Estado actual: ${allowedObjectsCount} tablas, ${schemaDocsCount} schema docs y ${semanticHintsCount} pistas del dominio.`;
        case 3:
            return isOnboardingValidationSuccessful(globalOnboardingValidation)
                ? 'La prueba actual ya respondió correctamente.'
                : 'Aquí esperamos ver SQL generado, resultado y una respuesta marcada como correcta.';
        case 4:
            return !!(health?.hasAllowedObjects ?? health?.HasAllowedObjects)
                ? 'Usa este cierre para decidir si el dominio ya puede pasar a usuarios internos.'
                : 'Este resumen se completa solo cuando los pasos anteriores ya quedaron bien.';
        default:
            return '';
    }
}

function updateOnboardingPanelFocus() {
    const health = globalOnboardingStatus?.health ?? globalOnboardingStatus?.Health ?? null;
    const hasWorkspace = !!getValue('txtOnboardingTenantKey').trim() && !!getValue('txtOnboardingDomain').trim() && !!getValue('txtOnboardingConnectionName').trim();
    const hasAllowed = globalOnboardingSchemaCandidates.some(x => !!x.isSelected) || !!(health?.hasAllowedObjects ?? health?.HasAllowedObjects);
    const isInitialized = !!(health?.hasSchemaDocs ?? health?.HasSchemaDocs) && !!(health?.hasSemanticHints ?? health?.HasSemanticHints);
    const hasValidation = isOnboardingValidationSuccessful(globalOnboardingValidation);

    const states = {
        onboardingStepPanel1: hasWorkspace,
        onboardingStepPanel2b: hasAllowed,
        onboardingStepPanel3: isInitialized,
        onboardingStepPanel4: hasValidation,
        onboardingStepPanel5: hasWorkspace && hasAllowed && isInitialized && hasValidation
    };

    let currentPanelId = 'onboardingStepPanel1';
    if (hasWorkspace && !hasAllowed) currentPanelId = 'onboardingStepPanel2b';
    else if (hasAllowed && !isInitialized) currentPanelId = 'onboardingStepPanel3';
    else if (isInitialized && !hasValidation) currentPanelId = 'onboardingStepPanel4';
    else if (hasValidation) currentPanelId = 'onboardingStepPanel5';

    Object.entries(states).forEach(([panelId, isComplete]) => {
        const panel = document.getElementById(panelId);
        if (!panel) return;
        panel.classList.toggle('is-complete', !!isComplete);
        panel.classList.toggle('is-current', panelId === currentPanelId);
    });
}

async function applyPersistedOnboardingWorkspaceState(workspace) {
    if (!workspace) return;

    setValue('txtOnboardingTenantKey', workspace.tenantKey || '');
    setValue('txtOnboardingDisplayName', workspace.displayName || '');
    setValue('txtOnboardingDomain', workspace.domain || '');
    setValue('txtOnboardingConnectionName', workspace.connectionName || '');
    const defaults = getOnboardingDefaults();
    setValue('txtOnboardingSystemProfileKey', workspace.systemProfileKey || defaults.systemProfileKey || defaults.SystemProfileKey || 'default');
    setValue('txtOnboardingDescription', workspace.description || '');
    toggleOnboardingAdvancedOptions((workspace.systemProfileKey || 'default') !== 'default');

    selectedOnboardingTenantKey = workspace.tenantKey || null;
    renderOnboardingTenantList();
    populateOnboardingConnectionOptions();

    if (workspace.tenantKey) {
        await loadTenantDomains(workspace.tenantKey);
    }

    if (workspace.domain) {
        await loadOnboardingStatus();
    }

    if (workspace.domain && workspace.connectionName) {
        await loadOnboardingSchemaCandidates();
    }

    setOnboardingMeta(null);
    renderOnboardingActionGuidance();
    await setAdminActiveContext(buildAdminContextFromOnboardingForm(), { reloadCurrentTab: true });
}

function toggleOnboardingConnectionEditor(forceOpen = null) {
    const editor = document.getElementById('onboardingConnectionEditor');
    if (!editor) return;

    const shouldOpen = forceOpen === null ? editor.style.display === 'none' : !!forceOpen;
    editor.style.display = shouldOpen ? 'block' : 'none';

    if (shouldOpen) {
        const currentConnectionName = getValue('txtOnboardingConnectionName').trim();
        if (!getValue('txtOnboardingNewConnectionName').trim()) {
            setValue('txtOnboardingNewConnectionName', currentConnectionName && currentConnectionName !== 'OperationalDb'
                ? currentConnectionName
                : '');
        }
    }
}

function showOnboardingConnectionBanner(type, message) {
    const el = document.getElementById('onboardingConnectionBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type;
    el.textContent = message;
    el.style.display = 'block';
}

function hideOnboardingConnectionBanner() {
    const el = document.getElementById('onboardingConnectionBanner');
    if (!el) return;
    el.style.display = 'none';
    el.className = 'rag-banner';
}

function setOnboardingConnectionMeta(message, cssClass = 'meta-empty') {
    const meta = document.getElementById('onboardingConnectionMeta');
    if (!meta) return;
    meta.innerHTML = `<span class="${cssClass}">${escHtml(message)}</span>`;
}

async function validateOnboardingConnection() {
    const connectionString = getValue('txtOnboardingNewConnectionString').trim();
    clearOnboardingConnectionErrors();
    if (!connectionString) {
        setOnboardingFieldError('connectionString', 'Pega la connection string completa antes de validarla.');
        showOnboardingConnectionBanner('warn', 'Pega una connection string para poder validarla.');
        return;
    }

    hideOnboardingConnectionBanner();
    const spinner = document.getElementById('onboardingConnectionValidateSpinner');
    if (spinner) spinner.style.display = 'block';

    try {
        const res = await fetch('/api/admin/connections/validate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ConnectionString: connectionString })
        });

        const body = await safeJson(res);
        if (!res.ok) {
            showOnboardingConnectionBanner('err', humanizeOnboardingErrorMessage(body?.Error || ('Error validando conexión. Código: ' + res.status)));
            return;
        }

        setOnboardingConnectionMeta(
            `Servidor: ${body?.ServerHost || 'n/d'} · Base: ${body?.DatabaseName || 'n/d'} · Auth integrada: ${body?.IntegratedSecurity ? 'sí' : 'no'}`,
            'meta-chip training-no');
        showOnboardingConnectionBanner('ok', body?.Message || 'Conexión validada correctamente.');
    } catch (e) {
        showOnboardingConnectionBanner('err', 'Error de red validando conexión: ' + e.message);
    } finally {
        if (spinner) spinner.style.display = 'none';
    }
}

async function saveOnboardingConnection() {
    const connectionName = getValue('txtOnboardingNewConnectionName').trim();
    const connectionString = getValue('txtOnboardingNewConnectionString').trim();
    const description = getValue('txtOnboardingNewConnectionDescription').trim();

    const missing = validateOnboardingConnectionFields(true);
    if (missing.length) {
        showOnboardingConnectionBanner('warn', `Completa ${formatOnboardingMissingFields(missing)} antes de guardar la conexión.`);
        return;
    }

    hideOnboardingConnectionBanner();
    const spinner = document.getElementById('onboardingConnectionSaveSpinner');
    if (spinner) spinner.style.display = 'block';

    try {
        const res = await fetch('/api/admin/connections', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                ConnectionName: connectionName,
                ConnectionString: connectionString,
                Description: description || null
            })
        });

        const body = await safeJson(res);
        if (!res.ok) {
            showOnboardingConnectionBanner('err', humanizeOnboardingErrorMessage(body?.Error || ('Error guardando conexión. Código: ' + res.status)));
            return;
        }

        persistOnboardingWorkspaceState();
        await loadOnboardingBootstrap();
        setValue('txtOnboardingConnectionName', body?.ConnectionName || connectionName);
        persistOnboardingWorkspaceState();
        setOnboardingConnectionMeta(
            `Guardada: ${body?.ConnectionName || connectionName} · ${body?.ServerHost || 'n/d'} · ${body?.DatabaseName || 'n/d'}`,
            'meta-chip status-ok');
        showOnboardingConnectionBanner('ok', body?.Message || 'Conexión guardada correctamente.');
        toggleOnboardingConnectionEditor(false);
        renderOnboardingActionGuidance();
    } catch (e) {
        showOnboardingConnectionBanner('err', 'Error de red guardando conexión: ' + e.message);
    } finally {
        if (spinner) spinner.style.display = 'none';
    }
}

async function loadOnboardingSchemaCandidates() {
    const connectionName = getValue('txtOnboardingConnectionName').trim();
    const domain = getValue('txtOnboardingDomain').trim();

    if (!connectionName || !domain) {
        validateOnboardingStep1Fields(true);
        showOnboardingBanner('warn', 'Primero guarda el workspace con un contexto de datos y una conexión.');
        return;
    }

    const list = document.getElementById('onboardingSchemaCandidateList');
    if (list) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <rect x="4" y="4" width="16" height="16" rx="2"></rect>
                    <path d="M8 8h8"></path>
                    <path d="M8 12h8"></path>
                    <path d="M8 16h5"></path>
                </svg>
                Descubriendo schema...
            </div>`;
    }

    try {
        const res = await fetch(`/api/admin/onboarding/schema-candidates?connectionName=${encodeURIComponent(connectionName)}&domain=${encodeURIComponent(domain)}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        globalOnboardingSchemaCandidates = (await res.json()).map(item => ({
            ...item,
            isSelected: !!(item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed ?? item.isSuggested ?? item.IsSuggested)
        }));
        renderOnboardingSchemaCandidates();
        renderOnboardingValidation();
        updateOnboardingStepper();
        hideOnboardingBanner();
    } catch (e) {
        if (list) {
            list.innerHTML = `
                <div class="empty-state">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                        <circle cx="12" cy="12" r="10"></circle>
                        <path d="M8 12h8"></path>
                        <path d="M12 8v8"></path>
                    </svg>
                    Error al descubrir schema
                </div>`;
        }
        showOnboardingBanner('err', 'No se pudo descubrir el schema: ' + e.message);
    }
}

function renderOnboardingSchemaCandidates() {
    const list = document.getElementById('onboardingSchemaCandidateList');
    const meta = document.getElementById('onboardingSchemaMeta');
    const saveBtn = document.getElementById('btnOnboardingSaveAllowedObjects');
    if (!list) return;

    if (!globalOnboardingSchemaCandidates.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <rect x="4" y="4" width="16" height="16" rx="2"></rect>
                    <path d="M8 8h8"></path>
                    <path d="M8 12h8"></path>
                    <path d="M8 16h5"></path>
                </svg>
                Descubre el schema para seleccionar los objetos permitidos
            </div>`;

        if (meta) meta.innerHTML = '<span class="meta-empty">Aún no se ha cargado el schema de la conexión.</span>';
        if (saveBtn) saveBtn.disabled = true;
        renderOnboardingActionGuidance();
        return;
    }

    const selectedCount = globalOnboardingSchemaCandidates.filter(x => !!x.isSelected).length;
    const existingCount = globalOnboardingSchemaCandidates.filter(x => !!(x.isCurrentlyAllowed ?? x.IsCurrentlyAllowed)).length;

    if (meta) {
        meta.innerHTML = `
            <span class="meta-chip status-ok">${selectedCount} seleccionados</span>
            <span class="meta-chip training-no">${globalOnboardingSchemaCandidates.length} detectados</span>
            <span class="meta-chip training-no">${existingCount} ya guardados</span>`;
    }

    list.innerHTML = globalOnboardingSchemaCandidates.map((item, idx) => {
        const schemaName = item.schemaName || item.SchemaName || '';
        const objectName = item.objectName || item.ObjectName || '';
        const objectType = item.objectType || item.ObjectType || '';
        const desc = item.description || item.Description || '';
        const isCurrentlyAllowed = !!(item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed);
        const isSelected = !!item.isSelected;
        const columnCount = item.columnCount ?? item.ColumnCount ?? 0;
        const pkCount = item.primaryKeyCount ?? item.PrimaryKeyCount ?? 0;
        const fkCount = item.foreignKeyCount ?? item.ForeignKeyCount ?? 0;

        return `
            <label class="schema-candidate ${isSelected ? 'is-selected' : ''}">
                <input type="checkbox" ${isSelected ? 'checked' : ''} onchange="toggleOnboardingSchemaCandidate(${idx}, this.checked)" />
                <div class="schema-candidate-body">
                    <div class="hi-top">
                        <div class="hi-question">${escHtml(schemaName)}.${escHtml(objectName)}</div>
                        <div class="hi-time">${escHtml(objectType)}</div>
                    </div>
                    <div class="hi-badges">
                        <span class="hi-verify ${isCurrentlyAllowed ? 'verified' : 'pending'}">${isCurrentlyAllowed ? 'Ya permitido' : 'Nuevo'}</span>
                        <span class="hi-status ok"><span class="dot"></span>${columnCount} cols</span>
                        <span class="hi-status ok"><span class="dot"></span>${pkCount} pk</span>
                        <span class="hi-status ok"><span class="dot"></span>${fkCount} fk</span>
                    </div>
                    ${desc ? `<div class="schema-candidate-desc">${escHtml(desc)}</div>` : ''}
                </div>
            </label>`;
    }).join('');

    if (saveBtn) saveBtn.disabled = selectedCount === 0;
    updateOnboardingStepper();
    renderOnboardingActionGuidance();
}

function toggleOnboardingSchemaCandidate(idx, isSelected) {
    const item = globalOnboardingSchemaCandidates[idx];
    if (!item) return;
    item.isSelected = !!isSelected;
    renderOnboardingSchemaCandidates();
}

function setOnboardingSchemaSelection(isSelected) {
    if (!globalOnboardingSchemaCandidates.length) return;
    globalOnboardingSchemaCandidates.forEach(item => { item.isSelected = !!isSelected; });
    renderOnboardingSchemaCandidates();
}

async function saveOnboardingAllowedObjects() {
    const domain = getValue('txtOnboardingDomain').trim();
    if (!domain) {
        validateOnboardingStep1Fields(true);
        showOnboardingBanner('warn', 'Primero guarda el workspace para definir el contexto de datos.');
        return;
    }

    if (!globalOnboardingSchemaCandidates.length) {
        showOnboardingBanner('warn', 'Primero descubre el schema antes de guardar las tablas permitidas.');
        return;
    }

    const btn = document.getElementById('btnOnboardingSaveAllowedObjects');
    const spin = document.getElementById('onboardingAllowedSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch('/api/admin/onboarding/allowed-objects', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                Domain: domain,
                Items: globalOnboardingSchemaCandidates.map(item => ({
                    SchemaName: item.schemaName || item.SchemaName,
                    ObjectName: item.objectName || item.ObjectName,
                    ObjectType: item.objectType || item.ObjectType,
                    IsSelected: !!item.isSelected
                }))
            })
        });

        if (res.ok) {
            const body = await safeJson(res);
            showOnboardingBanner('ok', body?.Message || 'Tablas permitidas guardadas correctamente. Ya puedes preparar el dominio.');
            loadAllowedObjects();
            await loadOnboardingSchemaCandidates();
            await loadOnboardingStatus();
        } else {
            const body = await safeJson(res);
            showOnboardingBanner('err', humanizeOnboardingErrorMessage(body?.Error || ('Error al guardar tablas permitidas. Código: ' + res.status)));
        }
    } catch (e) {
        showOnboardingBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (spin) spin.style.display = 'none';
        renderOnboardingSchemaCandidates();
        renderOnboardingActionGuidance();
    }
}

async function initializeOnboardingDomain() {
    const domain = getValue('txtOnboardingDomain').trim();
    const connectionName = getValue('txtOnboardingConnectionName').trim();

    if (!domain || !connectionName) {
        validateOnboardingStep1Fields(true);
        showOnboardingBanner('warn', 'Primero guarda el workspace y asegúrate de tener una conexión válida.');
        return;
    }

    const btn = document.getElementById('btnOnboardingInitialize');
    const spin = document.getElementById('onboardingInitializeSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch('/api/admin/onboarding/initialize', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                Domain: domain,
                ConnectionName: connectionName
            })
        });

        if (res.ok) {
            const body = await safeJson(res);
            globalOnboardingStatus = body?.Status || null;
            renderOnboardingStatus();
            renderOnboardingValidation();
            showOnboardingBanner('ok', body?.Message || 'Dominio preparado correctamente. Ya puedes ejecutar una prueba.');
            loadSemanticHints();
        } else {
            const body = await safeJson(res);
            showOnboardingBanner('err', humanizeOnboardingErrorMessage(body?.Error || ('Error al preparar el dominio. Código: ' + res.status)));
        }
    } catch (e) {
        showOnboardingBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
        updateOnboardingStepper();
        renderOnboardingActionGuidance();
    }
}

async function loadOnboardingStatus() {
    const domain = getValue('txtOnboardingDomain').trim();
    const connectionName = getValue('txtOnboardingConnectionName').trim();

    if (!domain) {
        globalOnboardingStatus = null;
        renderOnboardingStatus();
        return;
    }

    try {
        const res = await fetch(`/api/admin/onboarding/status?domain=${encodeURIComponent(domain)}&connectionName=${encodeURIComponent(connectionName || 'OperationalDb')}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        globalOnboardingStatus = await res.json();
    } catch {
        globalOnboardingStatus = null;
    }

    renderOnboardingStatus();
}

function renderOnboardingStatus() {
    const status = globalOnboardingStatus;
    const allowedObjectsCount = status?.allowedObjectsCount ?? status?.AllowedObjectsCount ?? 0;
    const schemaDocsCount = status?.schemaDocsCount ?? status?.SchemaDocsCount ?? 0;
    const semanticHintsCount = status?.semanticHintsCount ?? status?.SemanticHintsCount ?? 0;
    const health = status?.health ?? status?.Health ?? null;
    const hasAllowedObjects = !!(health?.hasAllowedObjects ?? health?.HasAllowedObjects);
    const hasSchemaDocs = !!(health?.hasSchemaDocs ?? health?.HasSchemaDocs);
    const hasSemanticHints = !!(health?.hasSemanticHints ?? health?.HasSemanticHints);
    const domain = status?.domain ?? status?.Domain ?? getValue('txtOnboardingDomain').trim();
    const connectionName = status?.connectionName ?? status?.ConnectionName ?? getValue('txtOnboardingConnectionName').trim();

    setText('txtOnboardingAllowedCount', String(allowedObjectsCount));
    setText('txtOnboardingSchemaDocCount', String(schemaDocsCount));
    setText('txtOnboardingHintCount', String(semanticHintsCount));

    updateStatusTile('tileAllowedObjects', hasAllowedObjects);
    updateStatusTile('tileSchemaDocs', hasSchemaDocs);
    updateStatusTile('tileSemanticHints', hasSemanticHints);

    const meta = document.getElementById('onboardingStatusMeta');
    if (meta) {
        if (!status) {
            meta.innerHTML = '<span class="meta-empty">Ejecuta la inicialización para dejar el dominio listo para pruebas.</span>';
        } else {
            meta.innerHTML = `
                <span class="meta-chip status-ok">${escHtml(domain || '')}</span>
                <span class="meta-chip training-no">${escHtml(connectionName || '')}</span>
                <span class="meta-chip training-no">${allowedObjectsCount} tablas</span>
                <span class="meta-chip training-no">${schemaDocsCount} schema docs</span>
                <span class="meta-chip training-no">${semanticHintsCount} hints</span>`;
        }
    }

    const isInitialized = hasSchemaDocs && hasSemanticHints;
    if (!isInitialized && globalOnboardingValidation?.jobId) {
        resetOnboardingValidation(true);
    } else {
        renderOnboardingValidation();
    }

    renderOnboardingReadiness();
    updateOnboardingStepper();
    renderOnboardingActionGuidance();
}

function updateStatusTile(id, isOk) {
    const el = document.getElementById(id);
    if (!el) return;
    el.classList.toggle('is-ok', !!isOk);
    el.classList.toggle('is-pending', !isOk);
}

function updateOnboardingStepper() {
    const health = globalOnboardingStatus?.health ?? globalOnboardingStatus?.Health ?? null;
    const hasWorkspace = !!getValue('txtOnboardingTenantKey').trim() && !!getValue('txtOnboardingDomain').trim() && !!getValue('txtOnboardingConnectionName').trim();
    const hasAllowed = globalOnboardingSchemaCandidates.filter(x => !!x.isSelected).length > 0 || !!(health?.hasAllowedObjects ?? health?.HasAllowedObjects);
    const isInitialized = !!(health?.hasSchemaDocs ?? health?.HasSchemaDocs) && !!(health?.hasSemanticHints ?? health?.HasSemanticHints);
    const hasValidation = isOnboardingValidationSuccessful(globalOnboardingValidation);

    setStepChipState('stepChip1', hasWorkspace, true);
    setStepChipState('stepChip2', hasAllowed, hasWorkspace);
    setStepChipState('stepChip3', isInitialized, hasAllowed);
    setStepChipState('stepChip4', hasValidation, isInitialized);

    const discoverBtn = document.getElementById('btnOnboardingDiscoverSchema');
    const saveAllowedBtn = document.getElementById('btnOnboardingSaveAllowedObjects');
    const initializeBtn = document.getElementById('btnOnboardingInitialize');
    const runBtn = document.getElementById('btnOnboardingRunValidation');

    if (discoverBtn) discoverBtn.disabled = !hasWorkspace;
    if (saveAllowedBtn) saveAllowedBtn.disabled = !hasWorkspace || !globalOnboardingSchemaCandidates.length;
    if (initializeBtn) initializeBtn.disabled = !hasAllowed;
    if (runBtn) runBtn.disabled = !isInitialized;

    renderOnboardingReadiness();
    renderOnboardingActionGuidance();
    updateOnboardingPanelFocus();
}

function setStepChipState(id, isComplete, isActive) {
    const el = document.getElementById(id);
    if (!el) return;
    el.classList.toggle('is-complete', !!isComplete);
    el.classList.toggle('is-active', !isComplete && !!isActive);
}

async function saveOnboardingStep1() {
    const tenantKey = getValue('txtOnboardingTenantKey').trim();
    const displayName = getValue('txtOnboardingDisplayName').trim();
    const domain = getValue('txtOnboardingDomain').trim();
    const connectionName = getValue('txtOnboardingConnectionName').trim();
    const description = getValue('txtOnboardingDescription').trim();
    const systemProfileKey = getValue('txtOnboardingSystemProfileKey').trim();

    const missing = validateOnboardingStep1Fields(true);
    if (missing.length) {
        showOnboardingBanner('warn', `Completa ${formatOnboardingMissingFields(missing)} antes de guardar el workspace.`);
        return;
    }

    const btn = document.getElementById('btnOnboardingSaveStep1');
    const spin = document.getElementById('onboardingSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch('/api/admin/onboarding/step1', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                TenantKey: tenantKey,
                DisplayName: displayName,
                Domain: domain,
                ConnectionName: connectionName,
                Description: description || null,
                SystemProfileKey: systemProfileKey || null
            })
        });

        if (res.ok) {
            const body = await safeJson(res);
            showOnboardingBanner('ok', body?.Message || 'Paso 1 guardado correctamente.');
            persistOnboardingWorkspaceState();
            await loadSystemConfig();
            await loadOnboardingBootstrap();
            await selectOnboardingTenant(tenantKey);
        } else {
            const body = await safeJson(res);
            showOnboardingBanner('err', humanizeOnboardingErrorMessage(body?.Error || ('Error al guardar el workspace. Código: ' + res.status)));
        }
    } catch (e) {
        showOnboardingBanner('err', 'Error de red al guardar el workspace: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
        renderOnboardingActionGuidance();
    }
}

function setOnboardingMeta(mapping) {
    const meta = document.getElementById('onboardingMeta');
    if (!meta) return;

    if (!mapping) {
        const currentDomain = getValue('txtOnboardingDomain').trim();
        const currentConnection = getValue('txtOnboardingConnectionName').trim();
        const currentProfile = getValue('txtOnboardingSystemProfileKey').trim() || 'default';
        if (currentDomain || currentConnection) {
            meta.innerHTML = `
                <span class="meta-chip verify-pending">Workspace actual</span>
                <span class="meta-chip status-ok">${escHtml(currentDomain || 'sin-domain')}</span>
                <span class="meta-chip training-no">${escHtml(currentConnection || 'sin-conexion')}</span>
                <span class="meta-chip training-no">${escHtml(currentProfile)}</span>`;
            return;
        }

        meta.innerHTML = '<span class="meta-empty">Workspace sin mapping default aún</span>';
        return;
    }

    meta.innerHTML = `
        <span class="meta-chip status-ok">${escHtml(mapping.domain || mapping.Domain || '')}</span>
        <span class="meta-chip training-no">${escHtml(mapping.connectionName || mapping.ConnectionName || 'OperationalDb')}</span>
        <span class="meta-chip training-no">${escHtml(mapping.systemProfileKey || mapping.SystemProfileKey || 'default')}</span>`;
}

function resetOnboardingValidation(keepQuestion = false) {
    const currentQuestion = getValue('txtOnboardingValidationQuestion').trim();
    globalOnboardingValidation = null;
    clearOnboardingFieldError('validationQuestion');

    if (!keepQuestion) {
        setValue('txtOnboardingValidationQuestion', '');
    } else if (currentQuestion) {
        setValue('txtOnboardingValidationQuestion', currentQuestion);
    }

    hideOnboardingValidationBanner();
    renderOnboardingValidation();
}

function renderOnboardingValidation() {
    const suggestionList = document.getElementById('onboardingSuggestionList');
    const meta = document.getElementById('onboardingValidationMeta');
    const runBtn = document.getElementById('btnOnboardingRunValidation');
    const health = globalOnboardingStatus?.health ?? globalOnboardingStatus?.Health ?? null;
    const isInitialized = !!(health?.hasSchemaDocs ?? health?.HasSchemaDocs) && !!(health?.hasSemanticHints ?? health?.HasSemanticHints);
    const suggestions = buildOnboardingSuggestedQuestions();
    const currentQuestion = getValue('txtOnboardingValidationQuestion').trim();

    if (!currentQuestion && suggestions.length) {
        setValue('txtOnboardingValidationQuestion', suggestions[0]);
    }

    if (suggestionList) {
        if (!isInitialized) {
            suggestionList.innerHTML = '<span class="meta-empty">Prepara el dominio para generar preguntas sugeridas.</span>';
        } else if (!suggestions.length) {
            suggestionList.innerHTML = '<span class="meta-empty">No hay objetos suficientes para sugerir preguntas; escribe una manualmente.</span>';
        } else {
            suggestionList.innerHTML = suggestions.map(question => `
                <button class="suggestion-chip" type="button" onclick="applyOnboardingSuggestedQuestion('${jsString(question)}')">
                    ${escHtml(question)}
                </button>`).join('');
        }
    }

    if (runBtn) {
        runBtn.disabled = !isInitialized;
    }

    setText('txtOnboardingValidationSql', formatOnboardingValidationSql(globalOnboardingValidation));
    setText('txtOnboardingValidationResult', formatOnboardingValidationResult(globalOnboardingValidation));

    if (meta) {
        if (!isInitialized) {
            meta.innerHTML = '<span class="meta-empty">Completa la preparación del dominio antes de ejecutar una prueba.</span>';
        } else if (!globalOnboardingValidation) {
            meta.innerHTML = `
                <span class="meta-chip training-no">${escHtml(getValue('txtOnboardingDomain').trim() || 'sin-domain')}</span>
                <span class="meta-chip training-no">${escHtml(getValue('txtOnboardingConnectionName').trim() || 'OperationalDb')}</span>
                <span class="meta-empty">Ejecuta una pregunta real para cerrar este onboarding.</span>`;
        } else {
            const statusKey = String(globalOnboardingValidation.status || '').toLowerCase();
            const statusClass = statusKey === 'completed'
                ? 'status-ok'
                : (statusKey === 'queued' || statusKey === 'processing' ? 'verify-pending' : 'status-err');
            const resultCount = globalOnboardingValidation.resultCount;
            const statusLabel = globalOnboardingValidation.status || 'Queued';
            const jobShort = String(globalOnboardingValidation.jobId || '').slice(0, 8);

            meta.innerHTML = `
                <span class="meta-chip ${statusClass}">${escHtml(statusLabel)}</span>
                ${jobShort ? `<span class="meta-chip training-no">Job ${escHtml(jobShort)}</span>` : ''}
                ${resultCount !== null && resultCount !== undefined ? `<span class="meta-chip training-no">${resultCount} filas</span>` : ''}
                <span class="meta-chip training-no">${escHtml(getValue('txtOnboardingDomain').trim() || 'sin-domain')}</span>
                ${isOnboardingValidationSuccessful(globalOnboardingValidation)
                    ? '<span class="meta-chip verify-ok">Dominio listo para preguntas reales</span>'
                    : '<span class="meta-chip verify-pending">Ajusta y vuelve a probar si la respuesta no es confiable</span>'}`;
        }
    }

    renderOnboardingReadiness();
    updateOnboardingStepper();
}

function buildOnboardingSuggestedQuestions() {
    const selectedObjects = globalOnboardingSchemaCandidates
        .filter(item => !!item.isSelected)
        .slice(0, 2);
    const fallbackObjects = globalOnboardingSchemaCandidates.slice(0, 2);
    const sourceObjects = selectedObjects.length ? selectedObjects : fallbackObjects;
    const suggestions = [];

    sourceObjects.forEach(item => {
        const objectName = String(item.objectName || item.ObjectName || '').trim();
        if (!objectName) return;

        suggestions.push(`Muéstrame 5 registros de ${objectName}.`);
        suggestions.push(`¿Cuántos registros hay en ${objectName}?`);
    });

    return [...new Set(suggestions)].slice(0, 4);
}

function applyOnboardingSuggestedQuestion(question) {
    setValue('txtOnboardingValidationQuestion', question || '');
    hideOnboardingValidationBanner();
}

async function runOnboardingValidationQuestion() {
    const question = getValue('txtOnboardingValidationQuestion').trim();
    const tenantKey = getValue('txtOnboardingTenantKey').trim();
    const domain = getValue('txtOnboardingDomain').trim();
    const connectionName = getValue('txtOnboardingConnectionName').trim();
    const health = globalOnboardingStatus?.health ?? globalOnboardingStatus?.Health ?? null;
    const isInitialized = !!(health?.hasSchemaDocs ?? health?.HasSchemaDocs) && !!(health?.hasSemanticHints ?? health?.HasSemanticHints);

    if (!isInitialized) {
        showOnboardingValidationBanner('warn', 'Completa la preparación del dominio antes de ejecutar una prueba.');
        return;
    }

    if (!validateOnboardingValidationQuestion(true)) {
        showOnboardingValidationBanner('warn', 'Escribe o selecciona una pregunta de prueba.');
        return;
    }

    const btn = document.getElementById('btnOnboardingRunValidation');
    const spin = document.getElementById('onboardingValidationSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    globalOnboardingValidation = {
        jobId: null,
        question,
        status: 'Queued',
        sqlText: null,
        resultJson: null,
        errorText: null,
        resultCount: null
    };
    renderOnboardingValidation();

    try {
        const res = await fetch('/api/assistant/ask', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                Question: question,
                UserId: 'AdminOnboarding',
                TenantKey: tenantKey || null,
                Domain: domain || null,
                ConnectionName: connectionName || null,
                Mode: 0
            })
        });

        const body = await safeJson(res);
        if (!res.ok) {
            showOnboardingValidationBanner('err', body?.Error || ('Error al iniciar la prueba. Código: ' + res.status));
            globalOnboardingValidation.status = 'Failed';
            globalOnboardingValidation.errorText = body?.Error || 'No se pudo encolar la prueba.';
            renderOnboardingValidation();
            return;
        }

        globalOnboardingValidation.jobId = body?.JobId || body?.jobId || null;
        globalOnboardingValidation.status = body?.Status || body?.status || 'Queued';
        renderOnboardingValidation();

        if (!globalOnboardingValidation.jobId) {
            showOnboardingValidationBanner('err', 'La API no devolvió un JobId para monitorear la prueba.');
            return;
        }

        await pollOnboardingValidationJob(globalOnboardingValidation.jobId);
    } catch (e) {
        showOnboardingValidationBanner('err', 'Error de red ejecutando la prueba: ' + e.message);
        globalOnboardingValidation.status = 'Failed';
        globalOnboardingValidation.errorText = e.message;
        renderOnboardingValidation();
    } finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
    }
}

async function pollOnboardingValidationJob(jobId) {
    const maxAttempts = 45;

    for (let attempt = 0; attempt < maxAttempts; attempt++) {
        if (attempt > 0) {
            await delay(1000);
        }

        const res = await fetch(`/api/assistant/status/${encodeURIComponent(jobId)}`);
        const body = await safeJson(res);
        if (!res.ok || !body) {
            continue;
        }

        const status = body.Status || body.status || 'Queued';
        const resultJson = body.ResultJson || body.resultJson || null;
        const parsedResult = tryParseJson(resultJson);

        globalOnboardingValidation = {
            jobId: body.JobId || body.jobId || jobId,
            question: body.Question || body.question || globalOnboardingValidation?.question || getValue('txtOnboardingValidationQuestion').trim(),
            status,
            sqlText: body.SqlText || body.sqlText || null,
            resultJson,
            parsedResult,
            errorText: body.ErrorText || body.errorText || null,
            resultCount: inferResultCount(parsedResult)
        };
        renderOnboardingValidation();

        if (isTerminalOnboardingJobStatus(status)) {
            if (isOnboardingValidationSuccessful(globalOnboardingValidation)) {
                showOnboardingValidationBanner('ok', 'La pregunta de prueba respondió correctamente. El dominio ya tiene un camino básico funcional.');
            } else if (String(status).toLowerCase() === 'requiresreview') {
                showOnboardingValidationBanner('warn', 'La prueba llegó a revisión. El dominio necesita curación adicional antes de salir a usuarios.');
            } else {
                showOnboardingValidationBanner('err', 'La prueba falló. Revisa AllowedObjects, SchemaDocs y SemanticHints antes de continuar.');
            }
            return;
        }
    }

    showOnboardingValidationBanner('warn', 'La prueba sigue en proceso. Puedes revisar el historial o volver a consultar el estado en unos segundos.');
}

function isTerminalOnboardingJobStatus(status) {
    const key = String(status || '').toLowerCase();
    return key === 'completed' || key === 'failed' || key === 'requiresreview';
}

function isOnboardingValidationSuccessful(validation) {
    if (!validation) return false;
    const key = String(validation.status || '').toLowerCase();
    return key === 'completed' && !!validation.sqlText && !validation.errorText;
}

function formatOnboardingValidationSql(validation) {
    if (!validation?.sqlText) {
        return 'Aún no se ha ejecutado ninguna prueba.';
    }

    return String(validation.sqlText).trim();
}

function formatOnboardingValidationResult(validation) {
    if (!validation) {
        return 'Ejecuta una pregunta real para confirmar que el dominio responde correctamente.';
    }

    const status = String(validation.status || 'Queued');
    if (!isTerminalOnboardingJobStatus(status)) {
        return `Estado: ${status}\n\nLa prueba sigue ejecutándose.`;
    }

    if (validation.errorText) {
        return `Estado: ${status}\n\n${String(validation.errorText).trim()}`;
    }

    const parsed = validation.parsedResult ?? tryParseJson(validation.resultJson);
    if (Array.isArray(parsed)) {
        const sample = parsed.slice(0, 5);
        return `Estado: ${status}\nFilas: ${parsed.length}\n\n${JSON.stringify(sample, null, 2)}`;
    }

    if (parsed && typeof parsed === 'object') {
        return `Estado: ${status}\n\n${JSON.stringify(parsed, null, 2)}`;
    }

    if (validation.resultJson) {
        return `Estado: ${status}\n\n${String(validation.resultJson).slice(0, 4000)}`;
    }

    return `Estado: ${status}\n\nLa consulta terminó sin datos serializados visibles.`;
}

function inferResultCount(parsedResult) {
    if (Array.isArray(parsedResult)) {
        return parsedResult.length;
    }

    if (parsedResult && Array.isArray(parsedResult.rows)) {
        return parsedResult.rows.length;
    }

    return null;
}

function tryParseJson(value) {
    if (!value || typeof value !== 'string') return value ?? null;
    try {
        return JSON.parse(value);
    } catch {
        return null;
    }
}

function showOnboardingValidationBanner(type, message) {
    const el = document.getElementById('onboardingValidationBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type;
    el.textContent = message;
    el.style.display = 'block';
}

function hideOnboardingValidationBanner() {
    const el = document.getElementById('onboardingValidationBanner');
    if (!el) return;
    el.style.display = 'none';
    el.className = 'rag-banner';
}

function renderOnboardingReadiness() {
    const health = globalOnboardingStatus?.health ?? globalOnboardingStatus?.Health ?? null;
    const hasWorkspace = !!getValue('txtOnboardingTenantKey').trim() && !!getValue('txtOnboardingDomain').trim() && !!getValue('txtOnboardingConnectionName').trim();
    const hasContext = !!(health?.hasAllowedObjects ?? health?.HasAllowedObjects)
        && !!(health?.hasSchemaDocs ?? health?.HasSchemaDocs)
        && !!(health?.hasSemanticHints ?? health?.HasSemanticHints);
    const hasValidation = isOnboardingValidationSuccessful(globalOnboardingValidation);
    const meta = document.getElementById('onboardingReadinessMeta');

    updateReadinessCard('readinessConnectionCard', hasWorkspace);
    updateReadinessCard('readinessSchemaCard', hasContext);
    updateReadinessCard('readinessValidationCard', hasValidation);

    if (!meta) return;

    if (hasWorkspace && hasContext && hasValidation) {
        meta.innerHTML = `
            <span class="meta-chip verify-ok">Dominio listo</span>
            <span class="meta-chip training-no">${escHtml(getValue('txtOnboardingDomain').trim() || 'sin-domain')}</span>
            <span class="meta-chip training-no">${escHtml(getValue('txtOnboardingConnectionName').trim() || 'OperationalDb')}</span>
            <span class="meta-chip status-ok">Onboarding básico completado</span>`;
        return;
    }

    const missing = [];
    if (!hasWorkspace) missing.push('workspace');
    if (!hasContext) missing.push('contexto');
    if (!hasValidation) missing.push('prueba');

    meta.innerHTML = `
        <span class="meta-chip verify-pending">Pendiente</span>
        <span class="meta-empty">Falta cerrar: ${escHtml(missing.join(', '))}</span>`;

    renderOnboardingActionGuidance();
}

function toggleOnboardingAdvancedOptions(forceOpen = null) {
    const panel = document.getElementById('onboardingAdvancedOptions');
    const button = document.getElementById('btnToggleOnboardingAdvanced');
    if (!panel || !button) return;

    const shouldOpen = forceOpen === null ? panel.style.display === 'none' : !!forceOpen;
    panel.style.display = shouldOpen ? 'block' : 'none';
    button.textContent = shouldOpen ? 'Ocultar opciones avanzadas' : 'Mostrar opciones avanzadas';
}

function updateReadinessCard(id, isOk) {
    const el = document.getElementById(id);
    if (!el) return;
    el.classList.toggle('is-ok', !!isOk);
    el.classList.toggle('is-pending', !isOk);
}

function startOnboardingTour() {
    switchTab('onboarding');
    ensureOnboardingTourLayer();
    activeOnboardingTourStep = getCurrentOnboardingStepIndex();
    renderOnboardingTour();
}

function nextOnboardingTourStep() {
    if (activeOnboardingTourStep < 0) return;
    if (activeOnboardingTourStep >= onboardingTourSteps.length - 1) {
        closeOnboardingTour();
        return;
    }

    activeOnboardingTourStep += 1;
    renderOnboardingTour();
}

function prevOnboardingTourStep() {
    if (activeOnboardingTourStep <= 0) return;
    activeOnboardingTourStep -= 1;
    renderOnboardingTour();
}

function closeOnboardingTour() {
    ensureOnboardingTourLayer();
    activeOnboardingTourStep = -1;
    const overlay = document.getElementById('onboardingTourOverlay');
    const popover = document.getElementById('onboardingTourPopover');
    if (overlay) {
        overlay.style.display = 'none';
    }
    if (popover) {
        popover.style.display = 'none';
        popover.style.visibility = '';
        popover.style.left = '';
        popover.style.right = '';
        popover.style.top = '';
        popover.style.bottom = '';
    }

    document.querySelectorAll('.tour-target').forEach(el => el.classList.remove('tour-target'));
}

function renderOnboardingTour() {
    ensureOnboardingTourLayer();
    const step = onboardingTourSteps[activeOnboardingTourStep];
    const overlay = document.getElementById('onboardingTourOverlay');
    const popover = document.getElementById('onboardingTourPopover');
    const prevBtn = document.getElementById('btnOnboardingTourPrev');
    const nextBtn = document.getElementById('btnOnboardingTourNext');
    if (!step || !overlay || !popover) {
        closeOnboardingTour();
        return;
    }

    document.querySelectorAll('.tour-target').forEach(el => el.classList.remove('tour-target'));

    const target = document.getElementById(step.targetId);
    if (target) {
        target.classList.add('tour-target');
        target.scrollIntoView({ behavior: 'smooth', block: activeOnboardingTourStep === 4 ? 'start' : 'center' });
    }

    setText('onboardingTourStepLabel', `${step.stepLabel} de ${onboardingTourSteps.length}`);
    setText('onboardingTourTitle', step.title);
    const runtimeNote = buildOnboardingTourRuntimeNote(activeOnboardingTourStep);
    setText('onboardingTourBody', runtimeNote ? `${step.body} ${runtimeNote}` : step.body);

    if (prevBtn) {
        prevBtn.disabled = activeOnboardingTourStep === 0;
    }

    if (nextBtn) {
        nextBtn.textContent = activeOnboardingTourStep >= onboardingTourSteps.length - 1 ? 'Terminar' : 'Siguiente';
    }

    overlay.style.display = 'block';
    popover.style.display = 'flex';
    positionOnboardingTourPopover(target, popover);
}

function positionOnboardingTourPopover(target, popover) {
    if (!popover) {
        return;
    }

    const padding = 24;
    const topRailOffset = 88;
    const gap = 18;
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    popover.style.visibility = 'hidden';
    popover.style.left = '';
    popover.style.right = '';
    popover.style.top = '';
    popover.style.bottom = '';

    const popoverRect = popover.getBoundingClientRect();
    if (!target) {
        popover.style.left = `${viewportWidth - popoverRect.width - padding}px`;
        popover.style.top = `${topRailOffset}px`;
        popover.style.visibility = '';
        return;
    }

    const targetRect = target.getBoundingClientRect();
    const candidates = [
        {
            left: viewportWidth - popoverRect.width - padding,
            top: topRailOffset
        },
        {
            left: padding,
            top: topRailOffset
        },
        {
            left: clamp(targetRect.left - popoverRect.width - gap, padding, viewportWidth - popoverRect.width - padding),
            top: clamp(targetRect.top, topRailOffset, viewportHeight - popoverRect.height - padding)
        },
        {
            left: clamp(targetRect.left, padding, viewportWidth - popoverRect.width - padding),
            top: clamp(targetRect.top - popoverRect.height - gap, topRailOffset, viewportHeight - popoverRect.height - padding)
        },
        {
            left: viewportWidth - popoverRect.width - padding,
            top: viewportHeight - popoverRect.height - padding
        }
    ];

    let selected = candidates[0];
    for (const candidate of candidates) {
        const candidateRect = {
            left: candidate.left,
            top: candidate.top,
            right: candidate.left + popoverRect.width,
            bottom: candidate.top + popoverRect.height
        };

        if (!rectsOverlap(candidateRect, targetRect)) {
            selected = candidate;
            break;
        }
    }

    popover.style.left = `${Math.round(selected.left)}px`;
    popover.style.top = `${Math.round(selected.top)}px`;
    popover.style.visibility = '';
}

function ensureOnboardingTourLayer() {
    const body = document.body;
    const overlay = document.getElementById('onboardingTourOverlay');
    const popover = document.getElementById('onboardingTourPopover');
    if (!body) {
        return;
    }

    if (overlay && overlay.parentElement !== body) {
        body.appendChild(overlay);
    }

    if (popover && popover.parentElement !== body) {
        body.appendChild(popover);
    }
}

function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}

function rectsOverlap(a, b) {
    return !(a.right <= b.left || a.left >= b.right || a.bottom <= b.top || a.top >= b.bottom);
}

async function exportOnboardingDomainPack() {
    const tenantKey = getValue('txtOnboardingTenantKey').trim();
    const domain = getValue('txtOnboardingDomain').trim();

    if (!tenantKey || !domain) {
        showOnboardingPackBanner('warn', 'Primero define TenantKey y Domain en el wizard.');
        return;
    }

    const btn = document.getElementById('btnOnboardingExportPack');
    const spin = document.getElementById('onboardingExportPackSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch(`/api/admin/domain-pack/export?tenantKey=${encodeURIComponent(tenantKey)}&domain=${encodeURIComponent(domain)}`);
        const body = await safeJson(res);
        if (!res.ok || !body) {
            showOnboardingPackBanner('err', body?.Error || ('Error al exportar el pack. Código: ' + res.status));
            return;
        }

        const serialized = JSON.stringify(body, null, 2);
        setValue('txtOnboardingDomainPackJson', serialized);
        renderOnboardingPackMeta(body);
        showOnboardingPackBanner('ok', 'Domain pack exportado correctamente. Ya quedó cargado en el editor y listo para descargarse o copiarse.');
        downloadTextFile(`${sanitizeFileName(tenantKey)}-${sanitizeFileName(domain)}-domain-pack.json`, serialized);
    } catch (e) {
        showOnboardingPackBanner('err', 'Error de red exportando el pack: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
    }
}

async function importOnboardingDomainPack() {
    const raw = getValue('txtOnboardingDomainPackJson').trim();
    if (!raw) {
        showOnboardingPackBanner('warn', 'Pega o exporta primero un JSON de domain pack.');
        return;
    }

    let pack = null;
    try {
        pack = JSON.parse(raw);
    } catch (e) {
        showOnboardingPackBanner('err', 'El JSON del pack no es válido: ' + e.message);
        return;
    }

    const btn = document.getElementById('btnOnboardingImportPack');
    const spin = document.getElementById('onboardingImportPackSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch('/api/admin/domain-pack/import', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(pack)
        });
        const body = await safeJson(res);
        if (!res.ok) {
            showOnboardingPackBanner('err', body?.Error || ('Error al importar el pack. Código: ' + res.status));
            return;
        }

        showOnboardingPackBanner('ok', body?.Message || 'Domain pack importado correctamente.');
        renderOnboardingPackMeta({
            TenantKey: body?.TenantKey || body?.tenantKey || pack?.TenantKey || pack?.tenantKey || '',
            Domain: body?.Domain || body?.domain || pack?.Domain || pack?.domain || '',
            ConnectionName: body?.ConnectionName || body?.connectionName || pack?.ConnectionName || pack?.connectionName || '',
            Imported: body?.Imported || body?.imported || null
        });

        await loadSystemConfig();
        await loadOnboardingBootstrap();

        const importedTenantKey = body?.TenantKey || body?.tenantKey || pack?.TenantKey || pack?.tenantKey;
        if (importedTenantKey) {
            await selectOnboardingTenant(importedTenantKey);
        }

        if (pack?.Domain || pack?.domain) {
            setValue('txtOnboardingDomain', pack?.Domain || pack?.domain || '');
        }

        if (pack?.ConnectionName || pack?.connectionName) {
            setValue('txtOnboardingConnectionName', pack?.ConnectionName || pack?.connectionName || '');
        }

        await loadOnboardingSchemaCandidates();
        await loadOnboardingStatus();
    } catch (e) {
        showOnboardingPackBanner('err', 'Error de red importando el pack: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
    }
}

function renderOnboardingPackMeta(pack) {
    const meta = document.getElementById('onboardingPackMeta');
    if (!meta) return;

    const tenantKey = pack?.TenantKey || pack?.tenantKey || 'sin-tenant';
    const domain = pack?.Domain || pack?.domain || 'sin-domain';
    const connectionName = pack?.ConnectionName || pack?.connectionName || 'sin-connection';
    const imported = pack?.Imported || pack?.imported || null;
    const counts = imported
        ? [
            `${imported.SystemConfigEntries ?? imported.systemConfigEntries ?? 0} cfg`,
            `${imported.AllowedObjects ?? imported.allowedObjects ?? 0} AO`,
            `${imported.BusinessRules ?? imported.businessRules ?? 0} BR`,
            `${imported.SemanticHints ?? imported.semanticHints ?? 0} SH`,
            `${imported.QueryPatterns ?? imported.queryPatterns ?? 0} QP`,
            `${imported.TrainingExamples ?? imported.trainingExamples ?? 0} TE`
        ]
        : [
            `${((pack?.SystemConfigEntries || pack?.systemConfigEntries) || []).length} cfg`,
            `${((pack?.AllowedObjects || pack?.allowedObjects) || []).length} AO`,
            `${((pack?.BusinessRules || pack?.businessRules) || []).length} BR`,
            `${((pack?.SemanticHints || pack?.semanticHints) || []).length} SH`,
            `${((pack?.QueryPatterns || pack?.queryPatterns) || []).length} QP`,
            `${((pack?.TrainingExamples || pack?.trainingExamples) || []).length} TE`
        ];

    meta.innerHTML = `
        <span class="meta-chip status-ok">${escHtml(tenantKey)}</span>
        <span class="meta-chip training-no">${escHtml(domain)}</span>
        <span class="meta-chip training-no">${escHtml(connectionName)}</span>
        ${counts.map(item => `<span class="meta-chip training-no">${escHtml(item)}</span>`).join('')}`;
}

function showOnboardingPackBanner(type, message) {
    const el = document.getElementById('onboardingPackBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type;
    el.textContent = message;
    el.style.display = 'block';
}

function hideOnboardingPackBanner() {
    const el = document.getElementById('onboardingPackBanner');
    if (!el) return;
    el.style.display = 'none';
    el.className = 'rag-banner';
}

function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

function showOnboardingBanner(type, message) {
    const el = document.getElementById('onboardingBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type;
    el.textContent = message;
    el.style.display = 'block';
}

function hideOnboardingBanner() {
    const el = document.getElementById('onboardingBanner');
    if (!el) return;
    el.style.display = 'none';
    el.className = 'rag-banner';
}

// -----------------------------------------------------------
// SYSTEM CONFIG TAB
// -----------------------------------------------------------
async function loadSystemConfig() {
    const list = document.getElementById('systemConfigList');
    if (!list) return;

    try {
        const res = await fetch('/api/admin/system-config');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        const payload = await res.json();
        globalSystemConfigProfile = payload?.Profile || payload?.profile || null;
        globalSystemConfigEntries = Array.isArray(payload?.Entries || payload?.entries)
            ? (payload.Entries || payload.entries)
            : [];

        applyAdminDomainDefault();
        populatePromptConfigEditor();
        populateRetrievalConfigEditor();
        renderSystemConfig();
    } catch (e) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="12" cy="12" r="10"/>
                    <line x1="15" y1="9" x2="9" y2="15"></line>
                    <line x1="9" y1="9" x2="15" y2="15"></line>
                </svg>
                Error al cargar system config
            </div>`;
    }
}

function getSystemConfigEntry(section, key) {
    return globalSystemConfigEntries.find(item =>
        String(item.section || item.Section || '').toLowerCase() === String(section || '').toLowerCase()
        && String(item.key || item.Key || '').toLowerCase() === String(key || '').toLowerCase());
}

function buildSystemConfigEntry(section, field, value) {
    const existing = getSystemConfigEntry(section, field.key);
    return {
        Id: existing?.id ?? existing?.Id ?? 0,
        Section: section,
        Key: field.key,
        Value: value,
        ValueType: field.valueType,
        IsEditableInUi: true,
        ValidationRule: null,
        Description: field.description || null
    };
}

function populatePromptConfigEditor() {
    promptConfigFields.forEach(field => {
        const entry = getSystemConfigEntry('Prompting', field.key);
        setValue(field.elementId, entry?.value || entry?.Value || '');
    });
}

function populateRetrievalConfigEditor() {
    retrievalConfigFields.forEach(field => {
        const entry = getSystemConfigEntry(field.section, field.key);
        setValue(field.elementId, entry?.value || entry?.Value || '');
    });
}

async function savePromptConfig() {
    const entries = promptConfigFields.map(field =>
        buildSystemConfigEntry('Prompting', field, getValue(field.elementId)));

    await saveSystemConfigEntriesBulk(entries, 'promptConfigSpinner', 'Prompting guardado correctamente.');
}

async function saveRetrievalConfig() {
    const entries = retrievalConfigFields.map(field =>
        buildSystemConfigEntry(field.section, field, getValue(field.elementId)));

    await saveSystemConfigEntriesBulk(entries, 'retrievalConfigSpinner', 'Retrieval y defaults guardados correctamente.');
}

async function saveSystemConfigEntriesBulk(entries, spinnerId, successMessage) {
    if (!Array.isArray(entries) || entries.length === 0) {
        showSystemConfigBanner('warn', 'No hay entradas para guardar.');
        return;
    }

    const spin = document.getElementById(spinnerId);
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch('/api/admin/system-config/bulk', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ Entries: entries })
        });

        if (res.ok) {
            showSystemConfigBanner('ok', successMessage);
            await loadSystemConfig();
        } else {
            const body = await safeJson(res);
            showSystemConfigBanner('err', body?.Error || ('Error al guardar. Código: ' + res.status));
        }
    } catch (e) {
        showSystemConfigBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (spin) spin.style.display = 'none';
    }
}

function renderSystemConfig() {
    const list = document.getElementById('systemConfigList');
    const count = document.getElementById('systemConfigCount');
    if (!list) return;

    const sectionFilter = getValue('txtSystemConfigSectionFilter').trim().toLowerCase();
    const visibleEntries = sectionFilter
        ? globalSystemConfigEntries.filter(item =>
            String(item.section || item.Section || '').toLowerCase() === sectionFilter)
        : globalSystemConfigEntries;

    if (count) count.textContent = visibleEntries.length;

    if (!visibleEntries.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="12" cy="12" r="10"/>
                    <path d="M8 12h8"></path>
                </svg>
                Sin entradas para este filtro
            </div>`;
        return;
    }

    list.innerHTML = visibleEntries.map(item => {
        const realIdx = globalSystemConfigEntries.indexOf(item);
        const section = item.section || item.Section || '';
        const key = item.key || item.Key || '';
        const value = item.value || item.Value || '';
        const valueType = item.valueType || item.ValueType || 'string';
        const editable = item.isEditableInUi ?? item.IsEditableInUi;

        return `
            <div class="history-item" id="syscfg-${realIdx}" onclick="loadSystemConfigEditor(${realIdx})">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(section)} / ${escHtml(key)}</div>
                    <div class="hi-time">${escHtml(valueType)}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-verify verified">${editable ? 'UI' : 'LOCK'}</span>
                    <span class="hi-status ok"><span class="dot"></span>${escHtml(String(value).slice(0, 48) || '(vacío)')}</span>
                </div>
            </div>`;
    }).join('');
}

function loadSystemConfigEditor(i) {
    const item = globalSystemConfigEntries[i];
    if (!item) return;

    selectedSystemConfigEntryId = item.id ?? item.Id ?? null;

    document.querySelectorAll('#systemConfigList .history-item').forEach(el => el.classList.remove('selected'));
    const el = document.getElementById('syscfg-' + i);
    if (el) el.classList.add('selected');

    const profile = globalSystemConfigProfile;
    const profileText = profile
        ? `${profile.environmentName || profile.EnvironmentName || 'Development'} / ${profile.profileKey || profile.ProfileKey || 'default'}`
        : '';

    setValue('txtSystemConfigId', selectedSystemConfigEntryId || '');
    setValue('txtSystemConfigProfile', profileText);
    setValue('txtSystemConfigSection', item.section || item.Section || '');
    setValue('txtSystemConfigKey', item.key || item.Key || '');
    setValue('txtSystemConfigValue', item.value || item.Value || '');
    setValue('txtSystemConfigValueType', item.valueType || item.ValueType || 'string');
    setValue('txtSystemConfigDescription', item.description || item.Description || '');
    setValue('txtSystemConfigValidationRule', item.validationRule || item.ValidationRule || '');
    setChecked('chkSystemConfigIsEditable', item.isEditableInUi ?? item.IsEditableInUi ?? true);

    const meta = document.getElementById('systemConfigMeta');
    if (meta) {
        meta.innerHTML = `
            <span class="meta-chip status-ok">${escHtml(item.section || item.Section || '')}</span>
            <span class="meta-chip training-yes">${escHtml(item.key || item.Key || '')}</span>
            <span class="meta-chip training-no">${escHtml(item.valueType || item.ValueType || 'string')}</span>`;
    }

    hideSystemConfigBanner();
}

function resetSystemConfigForm() {
    selectedSystemConfigEntryId = null;
    setValue('txtSystemConfigId', '');
    setValue('txtSystemConfigProfile', globalSystemConfigProfile
        ? `${globalSystemConfigProfile.environmentName || globalSystemConfigProfile.EnvironmentName || 'Development'} / ${globalSystemConfigProfile.profileKey || globalSystemConfigProfile.ProfileKey || 'default'}`
        : '');
    setValue('txtSystemConfigSection', getValue('txtSystemConfigSectionFilter').trim() || 'Prompting');
    setValue('txtSystemConfigKey', '');
    setValue('txtSystemConfigValue', '');
    setValue('txtSystemConfigValueType', 'string');
    setValue('txtSystemConfigDescription', '');
    setValue('txtSystemConfigValidationRule', '');
    setChecked('chkSystemConfigIsEditable', true);

    document.querySelectorAll('#systemConfigList .history-item').forEach(el => el.classList.remove('selected'));

    const meta = document.getElementById('systemConfigMeta');
    if (meta) {
        meta.innerHTML = '<span class="meta-empty">Nueva entrada lista para guardarse</span>';
    }

    hideSystemConfigBanner();
}

async function saveSystemConfigEntry() {
    const section = getValue('txtSystemConfigSection').trim();
    const key = getValue('txtSystemConfigKey').trim();
    const value = getValue('txtSystemConfigValue');
    const valueType = getValue('txtSystemConfigValueType').trim();
    const description = getValue('txtSystemConfigDescription').trim();
    const validationRule = getValue('txtSystemConfigValidationRule').trim();
    const isEditableInUi = getChecked('chkSystemConfigIsEditable');

    if (!section || !key || !valueType) {
        showSystemConfigBanner('warn', 'Section, Key y ValueType son requeridos.');
        return;
    }

    const btn = document.getElementById('btnSystemConfigSave');
    const spin = document.getElementById('systemConfigSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch('/api/admin/system-config', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                Id: Number(getValue('txtSystemConfigId') || 0),
                Section: section,
                Key: key,
                Value: value,
                ValueType: valueType,
                IsEditableInUi: isEditableInUi,
                ValidationRule: validationRule || null,
                Description: description || null
            })
        });

        if (res.ok) {
            showSystemConfigBanner('ok', 'System config guardado correctamente.');
            await loadSystemConfig();

            const refreshedIdx = globalSystemConfigEntries.findIndex(x =>
                (x.section || x.Section) === section && (x.key || x.Key) === key);
            if (refreshedIdx >= 0) loadSystemConfigEditor(refreshedIdx);
        } else {
            const body = await safeJson(res);
            showSystemConfigBanner('err', body?.Error || ('Error al guardar. Código: ' + res.status));
        }
    } catch (e) {
        showSystemConfigBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
    }
}

function showSystemConfigBanner(type, message) {
    const el = document.getElementById('systemConfigBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type;
    el.textContent = message;
    el.style.display = 'block';
}

function hideSystemConfigBanner() {
    const el = document.getElementById('systemConfigBanner');
    if (!el) return;
    el.style.display = 'none';
    el.className = 'rag-banner';
}

function applyAdminDomainDefault() {
    const adminDomainEntry = globalSystemConfigEntries.find(item =>
        String(item.section || item.Section || '').toLowerCase() === 'uidefaults'
        && String(item.key || item.Key || '').toLowerCase() === 'admindomain');
    const adminTenantEntry = globalSystemConfigEntries.find(item =>
        String(item.section || item.Section || '').toLowerCase() === 'uidefaults'
        && String(item.key || item.Key || '').toLowerCase() === 'admintenant');

    const configuredDomain = (adminDomainEntry?.value || adminDomainEntry?.Value || '').trim();
    const configuredTenant = (adminTenantEntry?.value || adminTenantEntry?.Value || '').trim();

    defaultAdminTenant = configuredTenant || defaultAdminTenant;
    if (!configuredDomain) return;
    defaultAllowedDomain = configuredDomain;
    defaultBusinessRuleDomain = configuredDomain;
    defaultSemanticHintDomain = configuredDomain;
    defaultQueryPatternDomain = configuredDomain;
}

// -----------------------------------------------------------
// RAG TAB
// -----------------------------------------------------------
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

function normalizeUserFeedback(value) {
    const key = String(value || '').toLowerCase();
    if (key === 'up') return 'up';
    if (key === 'down') return 'down';
    return '';
}

function isPendingReview(job) {
    const status = String(job.status || job.Status || '').toLowerCase();
    const vs = normalizeVerificationClass(job.verificationStatus || job.VerificationStatus);
    return status === 'requiresreview' || vs === 'pending';
}

function getReviewPriority(job) {
    const feedback = normalizeUserFeedback(job.userFeedback || job.UserFeedback);
    const pending = isPendingReview(job);

    if (feedback === 'down') return 0;
    if (pending) return 1;
    if (feedback === 'up') return 3;
    return 2;
}

function getCreatedTime(job) {
    const raw = job.createdUtc || job.CreatedUtc;
    const stamp = raw ? Date.parse(raw) : NaN;
    return Number.isFinite(stamp) ? stamp : 0;
}

function setFilter(f) {
    activeFilter = f;

    ['all', 'pending', 'negative', 'verified'].forEach(id => {
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
        const feedback = normalizeUserFeedback(job.userFeedback || job.UserFeedback);
        if (activeFilter === 'pending') return isPendingReview(job);
        if (activeFilter === 'negative') return feedback === 'down';
        if (activeFilter === 'verified') return vs === 'verified';
        return true;
    });

    filtered.sort((a, b) => {
        const priorityDiff = getReviewPriority(a) - getReviewPriority(b);
        if (priorityDiff !== 0) return priorityDiff;
        return getCreatedTime(b) - getCreatedTime(a);
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
        const feedback = normalizeUserFeedback(job.userFeedback || job.UserFeedback);
        const needsAttention = feedback === 'down';

        const time = created
            ? new Date(created).toLocaleTimeString('es-MX', { hour12: false, hour: '2-digit', minute: '2-digit' })
            : '--:--';

        return `
            <div class="history-item${needsAttention ? ' feedback-down' : ''}" id="hi-${realIdx}" onclick="loadEditor(${realIdx})">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(question)}</div>
                    <div class="hi-time">${time}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-status ${opOk ? 'ok' : 'err'}">
                        <span class="dot"></span>${escHtml(status)}
                    </span>
                    <span class="hi-verify ${vsKey}">${escHtml(vs)}</span>
                    ${feedback ? `<span class="hi-feedback ${feedback}">${feedback === 'down' ? 'Revision usuario: negativa' : 'Revision usuario: positiva'}</span>` : ''}
                    ${trained ? '<span class="hi-verify verified">RAG validado</span>' : ''}
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
    const feedback = normalizeUserFeedback(job.userFeedback || job.UserFeedback);
    const feedbackUtcRaw = job.feedbackUtc || job.FeedbackUtc;
    const feedbackUtc = feedbackUtcRaw
        ? new Date(feedbackUtcRaw).toLocaleString('es-MX', { hour12: false })
        : '';

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
    const feedbackChip = feedback
        ? `<span class="meta-chip ${feedback === 'down' ? 'feedback-down' : 'feedback-up'}">Usuario: ${feedback === 'down' ? 'Incorrecta' : 'Correcta'}</span>`
        : '';
    const feedbackTime = feedbackUtc
        ? `<span class="meta-time">Feedback: ${escHtml(feedbackUtc)}</span>`
        : '';

    const ragMeta = document.getElementById('ragMeta');
    if (ragMeta) {
        ragMeta.innerHTML = `
            <span class="meta-chip ${opClass}">${escHtml(status)}</span>
            <span class="meta-chip ${vsClass}">Verif: ${escHtml(vs)}</span>
            <span class="meta-chip ${trClass}">RAG: ${trained ? 'Sí' : 'No'}</span>
            ${feedbackChip}
            <span class="meta-time">${escHtml(created)}</span>
            ${feedbackTime}`;
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
// -----------------------------------------------------------
// ALLOWED OBJECTS TAB
// -----------------------------------------------------------
async function loadAllowedObjects() {
    const context = getActiveAdminContext();
    const domain = context?.domain?.trim() || '';
    const list = document.getElementById('allowedList');

    if (!domain) {
        renderContextRequiredState('allowed', 'Selecciona primero un contexto en Onboarding para cargar Allowed Objects.');
        showAllowedBanner('warn', 'Selecciona primero un contexto en Onboarding.');
        return;
    }

    setValue('txtAllowedDomainFilter', domain);

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

// -----------------------------------------------------------
// BUSINESS RULES TAB
// -----------------------------------------------------------
async function loadBusinessRules() {
    const context = getActiveAdminContext();
    const domain = context?.domain?.trim() || '';
    const list = document.getElementById('businessRuleList');

    if (!domain) {
        renderContextRequiredState('business-rules', 'Selecciona primero un contexto en Onboarding para cargar Business Rules.');
        showBusinessRuleBanner('warn', 'Selecciona primero un contexto en Onboarding.');
        return;
    }

    setValue('txtBusinessRuleDomainFilter', domain);

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

// -----------------------------------------------------------
// SEMANTIC HINTS TAB
// -----------------------------------------------------------
async function loadSemanticHints() {
    const context = getActiveAdminContext();
    const domain = context?.domain?.trim() || '';
    const list = document.getElementById('semanticHintList');

    if (!domain) {
        renderContextRequiredState('semantics', 'Selecciona primero un contexto en Onboarding para cargar Semantic Hints.');
        showSemanticHintBanner('warn', 'Selecciona primero un contexto en Onboarding.');
        return;
    }

    setValue('txtSemanticHintDomainFilter', domain);

    try {
        const res = await fetch(`/api/admin/semantic-hints?domain=${encodeURIComponent(domain)}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        globalSemanticHints = await res.json();
        renderSemanticHints();
        setValue('txtSemanticHintDomain', domain);
        hideSemanticHintBanner();
    } catch (e) {
        if (list) list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="8" cy="8" r="2"></circle>
                    <circle cx="16" cy="8" r="2"></circle>
                    <circle cx="12" cy="16" r="2"></circle>
                    <path d="M10 9.5l1.5 4"></path>
                    <path d="M14 9.5l-1.5 4"></path>
                </svg>
                Error al cargar semantic hints
            </div>`;
        showSemanticHintBanner('err', 'Error al cargar Semantic Hints: ' + e.message);
    }
}

function renderSemanticHints() {
    const list = document.getElementById('semanticHintList');
    const count = document.getElementById('semanticHintCount');
    if (!list) return;

    if (count) count.textContent = globalSemanticHints.length;

    if (!globalSemanticHints.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="8" cy="8" r="2"></circle>
                    <circle cx="16" cy="8" r="2"></circle>
                    <circle cx="12" cy="16" r="2"></circle>
                    <path d="M10 9.5l1.5 4"></path>
                    <path d="M14 9.5l-1.5 4"></path>
                </svg>
                Sin semantic hints para este dominio
            </div>`;
        return;
    }

    list.innerHTML = globalSemanticHints.map(item => {
        const id = item.id ?? item.Id;
        const hintKey = item.hintKey || item.HintKey || '';
        const hintType = item.hintType || item.HintType || '';
        const displayName = item.displayName || item.DisplayName || '';
        const priority = item.priority ?? item.Priority ?? 100;
        const isActive = !!(item.isActive ?? item.IsActive);

        return `
            <div class="history-item" id="semantic-${id}" onclick="selectSemanticHint(${id})">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(displayName || hintKey)}</div>
                    <div class="hi-time">P${escHtml(String(priority))}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-verify ${isActive ? 'verified' : 'rejected'}">${isActive ? 'Activo' : 'Inactivo'}</span>
                    <span class="hi-status ok meta-muted"><span class="dot dot-muted"></span>${escHtml(hintType)}</span>
                </div>
                <div class="meta-muted" style="margin-top:6px;font-family:var(--mono);font-size:.62rem;line-height:1.4;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden">${escHtml(hintKey)}</div>
            </div>`;
    }).join('');

    if (selectedSemanticHintId !== null) {
        const el = document.getElementById('semantic-' + selectedSemanticHintId);
        if (el) el.classList.add('selected');
    }
}

function selectSemanticHint(id) {
    const item = globalSemanticHints.find(x => (x.id ?? x.Id) === id);
    if (!item) return;

    selectedSemanticHintId = id;
    document.querySelectorAll('#semanticHintList .history-item').forEach(el => el.classList.remove('selected'));

    const target = document.getElementById('semantic-' + id);
    if (target) target.classList.add('selected');

    const domain = item.domain || item.Domain || '';
    const hintKey = item.hintKey || item.HintKey || '';
    const hintType = item.hintType || item.HintType || '';
    const displayName = item.displayName || item.DisplayName || '';
    const objectName = item.objectName || item.ObjectName || '';
    const columnName = item.columnName || item.ColumnName || '';
    const hintText = item.hintText || item.HintText || '';
    const priority = item.priority ?? item.Priority ?? 100;
    const isActive = !!(item.isActive ?? item.IsActive);

    setValue('txtSemanticHintId', id);
    setValue('txtSemanticHintDomain', domain);
    setValue('txtSemanticHintKey', hintKey);
    setValue('txtSemanticHintType', hintType);
    setValue('txtSemanticHintDisplayName', displayName);
    setValue('txtSemanticHintObjectName', objectName);
    setValue('txtSemanticHintColumnName', columnName);
    setValue('txtSemanticHintText', hintText);
    setValue('txtSemanticHintPriority', priority);
    setChecked('chkSemanticHintIsActive', isActive);

    const toggleBtn = document.getElementById('btnSemanticHintToggle');
    if (toggleBtn) { toggleBtn.disabled = false; toggleBtn.textContent = isActive ? 'Desactivar' : 'Activar'; }

    const meta = document.getElementById('semanticHintMeta');
    if (meta) meta.innerHTML = `
        <span class="meta-chip ${isActive ? 'verify-ok' : 'verify-err'}">${isActive ? 'Activo' : 'Inactivo'}</span>
        <span class="meta-chip training-no">Id: ${id}</span>
        <span class="meta-chip training-no">${escHtml(hintType)}</span>
        <span class="meta-time">${escHtml(domain)}</span>`;

    syncSemanticHintActionButtons();
    hideSemanticHintBanner();
}

function resetSemanticHintForm() {
    selectedSemanticHintId = null;
    document.querySelectorAll('#semanticHintList .history-item').forEach(el => el.classList.remove('selected'));

    setValue('txtSemanticHintId', '');
    setValue('txtSemanticHintDomain', (getValue('txtSemanticHintDomainFilter').trim() || defaultSemanticHintDomain));
    setValue('txtSemanticHintKey', '');
    setValue('txtSemanticHintType', '');
    setValue('txtSemanticHintDisplayName', '');
    setValue('txtSemanticHintObjectName', '');
    setValue('txtSemanticHintColumnName', '');
    setValue('txtSemanticHintText', '');
    setValue('txtSemanticHintPriority', '100');
    setChecked('chkSemanticHintIsActive', true);

    const toggleBtn = document.getElementById('btnSemanticHintToggle');
    if (toggleBtn) { toggleBtn.disabled = true; toggleBtn.textContent = 'Activar / Desactivar'; }

    const meta = document.getElementById('semanticHintMeta');
    if (meta) meta.innerHTML = `<span class="meta-empty">Ninguna pista semántica seleccionada</span>`;

    syncSemanticHintActionButtons();
    hideSemanticHintBanner();
}

function syncSemanticHintActionButtons() {
    const hasSelection = !!getValue('txtSemanticHintId');
    const saveBtn = document.getElementById('btnSemanticHintSave');
    const toggleBtn = document.getElementById('btnSemanticHintToggle');

    if (saveBtn) saveBtn.innerHTML = `
        <div class="btn-spinner" id="semanticHintSpinner" style="display:none"></div>
        ${hasSelection ? 'Actualizar Semantic Hint' : 'Crear Semantic Hint'}`;

    if (toggleBtn) { toggleBtn.disabled = !hasSelection; if (!hasSelection) toggleBtn.textContent = 'Activar / Desactivar'; }
}

async function saveSemanticHint() {
    const id = getValue('txtSemanticHintId');
    const domain = getValue('txtSemanticHintDomain').trim();
    const hintKey = getValue('txtSemanticHintKey').trim();
    const hintType = getValue('txtSemanticHintType').trim();
    const displayName = getValue('txtSemanticHintDisplayName').trim();
    const objectName = getValue('txtSemanticHintObjectName').trim();
    const columnName = getValue('txtSemanticHintColumnName').trim();
    const hintText = getValue('txtSemanticHintText').trim();
    const priorityRaw = getValue('txtSemanticHintPriority').trim();
    const isActive = getChecked('chkSemanticHintIsActive');

    if (!domain || !hintKey || !hintType || !hintText) {
        showSemanticHintBanner('warn', 'Domain, Hint Key, Hint Type y Hint Text son requeridos.'); return;
    }

    const priority = priorityRaw === '' ? 100 : parseInt(priorityRaw, 10);
    if (Number.isNaN(priority) || priority < 0) {
        showSemanticHintBanner('warn', 'Priority debe ser un entero mayor o igual a 0.'); return;
    }

    const btn = document.getElementById('btnSemanticHintSave');
    const spin = document.getElementById('semanticHintSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch('/api/admin/semantic-hints', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                Id: id ? Number(id) : 0,
                Domain: domain,
                HintKey: hintKey,
                HintType: hintType,
                DisplayName: displayName || null,
                ObjectName: objectName || null,
                ColumnName: columnName || null,
                HintText: hintText,
                Priority: priority,
                IsActive: isActive
            })
        });

        if (res.ok) {
            const body = await safeJson(res);
            showSemanticHintBanner('ok', body?.Message || 'Semantic Hint guardada correctamente.');
            setValue('txtSemanticHintDomainFilter', domain);
            await loadSemanticHints();
            const savedId = body?.Id ?? null;
            if (savedId !== null) selectSemanticHint(savedId);
        } else {
            const body = await safeJson(res);
            showSemanticHintBanner('err', body?.Error || ('Error al guardar Semantic Hint. Código: ' + res.status));
        }
    } catch (e) {
        showSemanticHintBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
    }
}

async function toggleSemanticHintStatus() {
    const id = getValue('txtSemanticHintId');
    if (!id) { showSemanticHintBanner('warn', 'Selecciona una pista semántica antes de cambiar su estado.'); return; }

    const currentItem = globalSemanticHints.find(x => String(x.id ?? x.Id) === String(id));
    if (!currentItem) { showSemanticHintBanner('err', 'No se encontró la Semantic Hint seleccionada.'); return; }

    const nextIsActive = !(currentItem.isActive ?? currentItem.IsActive);
    const btn = document.getElementById('btnSemanticHintToggle');
    if (btn) btn.disabled = true;

    try {
        const res = await fetch(`/api/admin/semantic-hints/${id}/status`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ IsActive: nextIsActive })
        });

        if (res.ok) {
            showSemanticHintBanner('ok', `Semantic Hint ${nextIsActive ? 'activada' : 'desactivada'} correctamente.`);
            await loadSemanticHints();
            selectSemanticHint(Number(id));
        } else {
            const body = await safeJson(res);
            showSemanticHintBanner('err', body?.Error || ('Error al actualizar estatus. Código: ' + res.status));
        }
    } catch (e) {
        showSemanticHintBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
    }
}

function showSemanticHintBanner(type, message) {
    const el = document.getElementById('semanticHintBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type; el.textContent = message; el.style.display = 'block';
}
function hideSemanticHintBanner() {
    const el = document.getElementById('semanticHintBanner');
    if (!el) return;
    el.style.display = 'none'; el.className = 'rag-banner';
}

// -----------------------------------------------------------
// QUERY PATTERNS TAB
// -----------------------------------------------------------
async function loadQueryPatterns() {
    const context = getActiveAdminContext();
    const domain = context?.domain?.trim() || '';
    const list = document.getElementById('queryPatternList');

    if (!domain) {
        renderContextRequiredState('patterns', 'Selecciona primero un contexto en Onboarding para cargar Query Patterns.');
        showQueryPatternBanner('warn', 'Selecciona primero un contexto en Onboarding.');
        return;
    }

    setValue('txtQueryPatternDomainFilter', domain);

    try {
        const res = await fetch(`/api/admin/query-patterns?domain=${encodeURIComponent(domain)}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        globalQueryPatterns = await res.json();
        renderQueryPatterns();
        setValue('txtQueryPatternDomain', domain);
        hideQueryPatternBanner();
    } catch (e) {
        if (list) list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M4 19h16"></path>
                    <path d="M4 5h16"></path>
                    <path d="M9 9h6"></path>
                    <path d="M7 15h10"></path>
                </svg>
                Error al cargar Query Patterns
            </div>`;
        showQueryPatternBanner('err', 'Error al cargar Query Patterns: ' + e.message);
    }
}

function renderQueryPatterns() {
    const list = document.getElementById('queryPatternList');
    const count = document.getElementById('queryPatternCount');
    if (!list) return;

    if (count) count.textContent = globalQueryPatterns.length;

    if (!globalQueryPatterns.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M4 19h16"></path>
                    <path d="M4 5h16"></path>
                    <path d="M9 9h6"></path>
                    <path d="M7 15h10"></path>
                </svg>
                Sin Query Patterns para este dominio
            </div>`;
        return;
    }

    list.innerHTML = globalQueryPatterns.map(item => {
        const id = item.id ?? item.Id;
        const patternKey = item.patternKey || item.PatternKey || '';
        const intentName = item.intentName || item.IntentName || '';
        const priority = item.priority ?? item.Priority ?? 100;
        const isActive = !!(item.isActive ?? item.IsActive);

        return `
            <div class="history-item" id="qp-${id}" onclick="selectQueryPattern(${id})">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(patternKey)}</div>
                    <div class="hi-time">P${escHtml(String(priority))}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-verify ${isActive ? 'verified' : 'rejected'}">${isActive ? 'Activo' : 'Inactivo'}</span>
                </div>
                <div class="meta-muted" style="margin-top:6px;font-family:var(--mono);font-size:.62rem;line-height:1.4;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden">${escHtml(intentName)}</div>
            </div>`;
    }).join('');

    if (selectedQueryPatternId !== null) {
        const el = document.getElementById('qp-' + selectedQueryPatternId);
        if (el) el.classList.add('selected');
    }
}

async function selectQueryPattern(id) {
    const item = globalQueryPatterns.find(x => (x.id ?? x.Id) === id);
    if (!item) return;

    selectedQueryPatternId = id;
    selectedQueryPatternTermId = null;
    document.querySelectorAll('#queryPatternList .history-item').forEach(el => el.classList.remove('selected'));

    const target = document.getElementById('qp-' + id);
    if (target) target.classList.add('selected');

    const domain = item.domain || item.Domain || '';
    const patternKey = item.patternKey || item.PatternKey || '';
    const intentName = item.intentName || item.IntentName || '';
    const description = item.description || item.Description || '';
    const sqlTemplate = item.sqlTemplate || item.SqlTemplate || '';
    const defaultTopN = item.defaultTopN ?? item.DefaultTopN ?? '';
    const metricKey = item.metricKey || item.MetricKey || '';
    const dimensionKey = item.dimensionKey || item.DimensionKey || '';
    const defaultTimeScopeKey = item.defaultTimeScopeKey || item.DefaultTimeScopeKey || '';
    const priority = item.priority ?? item.Priority ?? 100;
    const isActive = !!(item.isActive ?? item.IsActive);

    setValue('txtQueryPatternId', id);
    setValue('txtQueryPatternDomain', domain);
    setValue('txtQueryPatternKey', patternKey);
    setValue('txtQueryPatternIntentName', intentName);
    setValue('txtQueryPatternDescription', description);
    setValue('txtQueryPatternSqlTemplate', sqlTemplate);
    setValue('txtQueryPatternDefaultTopN', defaultTopN);
    setValue('txtQueryPatternMetricKey', metricKey);
    setValue('txtQueryPatternDimensionKey', dimensionKey);
    setValue('txtQueryPatternDefaultTimeScopeKey', defaultTimeScopeKey);
    setValue('txtQueryPatternPriority', priority);
    setChecked('chkQueryPatternIsActive', isActive);

    const toggleBtn = document.getElementById('btnQueryPatternToggle');
    if (toggleBtn) { toggleBtn.disabled = false; toggleBtn.textContent = isActive ? 'Desactivar' : 'Activar'; }

    const meta = document.getElementById('queryPatternMeta');
    if (meta) meta.innerHTML = `
        <span class="meta-chip ${isActive ? 'verify-ok' : 'verify-err'}">${isActive ? 'Activo' : 'Inactivo'}</span>
        <span class="meta-chip training-no">Id: ${id}</span>
        <span class="meta-chip training-no">Priority: ${priority}</span>
        <span class="meta-time">${escHtml(domain)}</span>`;

    syncQueryPatternActionButtons();
    resetQueryPatternTermForm();
    await loadQueryPatternTerms(id);
    hideQueryPatternBanner();
}

function resetQueryPatternForm() {
    selectedQueryPatternId = null;
    document.querySelectorAll('#queryPatternList .history-item').forEach(el => el.classList.remove('selected'));

    setValue('txtQueryPatternId', '');
    setValue('txtQueryPatternDomain', (getValue('txtQueryPatternDomainFilter').trim() || defaultQueryPatternDomain));
    setValue('txtQueryPatternKey', '');
    setValue('txtQueryPatternIntentName', '');
    setValue('txtQueryPatternDescription', '');
    setValue('txtQueryPatternSqlTemplate', '');
    setValue('txtQueryPatternDefaultTopN', '');
    setValue('txtQueryPatternMetricKey', '');
    setValue('txtQueryPatternDimensionKey', '');
    setValue('txtQueryPatternDefaultTimeScopeKey', '');
    setValue('txtQueryPatternPriority', '100');
    setChecked('chkQueryPatternIsActive', true);

    const toggleBtn = document.getElementById('btnQueryPatternToggle');
    if (toggleBtn) { toggleBtn.disabled = true; toggleBtn.textContent = 'Activar / Desactivar'; }

    const meta = document.getElementById('queryPatternMeta');
    if (meta) meta.innerHTML = `<span class="meta-empty">Ningún pattern seleccionado</span>`;

    syncQueryPatternActionButtons();
    hideQueryPatternBanner();
    clearQueryPatternTermsState();
}

function syncQueryPatternActionButtons() {
    const hasSelection = !!getValue('txtQueryPatternId');
    const saveBtn = document.getElementById('btnQueryPatternSave');
    const toggleBtn = document.getElementById('btnQueryPatternToggle');

    if (saveBtn) saveBtn.innerHTML = `
        <div class="btn-spinner" id="queryPatternSpinner" style="display:none"></div>
        ${hasSelection ? 'Actualizar Query Pattern' : 'Crear Query Pattern'}`;

    if (toggleBtn) { toggleBtn.disabled = !hasSelection; if (!hasSelection) toggleBtn.textContent = 'Activar / Desactivar'; }
}

async function saveQueryPattern() {
    const id = getValue('txtQueryPatternId');
    const domain = getValue('txtQueryPatternDomain').trim();
    const patternKey = getValue('txtQueryPatternKey').trim();
    const intentName = getValue('txtQueryPatternIntentName').trim();
    const description = getValue('txtQueryPatternDescription').trim();
    const sqlTemplate = getValue('txtQueryPatternSqlTemplate').trim();
    const defaultTopNRaw = getValue('txtQueryPatternDefaultTopN').trim();
    const metricKey = getValue('txtQueryPatternMetricKey').trim();
    const dimensionKey = getValue('txtQueryPatternDimensionKey').trim();
    const defaultTimeScopeKey = getValue('txtQueryPatternDefaultTimeScopeKey').trim();
    const priorityRaw = getValue('txtQueryPatternPriority').trim();
    const isActive = getChecked('chkQueryPatternIsActive');

    if (!domain || !patternKey || !intentName || !sqlTemplate) {
        showQueryPatternBanner('warn', 'Domain, PatternKey, IntentName y SqlTemplate son requeridos.'); return;
    }

    const priority = priorityRaw === '' ? 100 : parseInt(priorityRaw, 10);
    if (Number.isNaN(priority) || priority < 0) {
        showQueryPatternBanner('warn', 'Priority debe ser un entero mayor o igual a 0.'); return;
    }

    let defaultTopN = null;
    if (defaultTopNRaw !== '') {
        defaultTopN = parseInt(defaultTopNRaw, 10);
        if (Number.isNaN(defaultTopN) || defaultTopN < 0) {
            showQueryPatternBanner('warn', 'DefaultTopN debe ser un entero mayor o igual a 0.'); return;
        }
    }

    const btn = document.getElementById('btnQueryPatternSave');
    const spin = document.getElementById('queryPatternSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch('/api/admin/query-patterns', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                Id: id ? Number(id) : 0,
                Domain: domain,
                PatternKey: patternKey,
                IntentName: intentName,
                Description: description || null,
                SqlTemplate: sqlTemplate,
                DefaultTopN: defaultTopN,
                MetricKey: metricKey || null,
                DimensionKey: dimensionKey || null,
                DefaultTimeScopeKey: defaultTimeScopeKey || null,
                Priority: priority,
                IsActive: isActive
            })
        });

        if (res.ok) {
            const body = await safeJson(res);
            showQueryPatternBanner('ok', body?.Message || 'Query Pattern guardado correctamente.');
            setValue('txtQueryPatternDomainFilter', domain);
            await loadQueryPatterns();
            const savedId = body?.Id ?? null;
            if (savedId !== null) await selectQueryPattern(savedId);
        } else {
            const body = await safeJson(res);
            showQueryPatternBanner('err', body?.Error || ('Error al guardar Query Pattern. Código: ' + res.status));
        }
    } catch (e) {
        showQueryPatternBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
    }
}

async function toggleQueryPatternStatus() {
    const id = getValue('txtQueryPatternId');
    if (!id) { showQueryPatternBanner('warn', 'Selecciona un Query Pattern antes de cambiar su estado.'); return; }

    const currentItem = globalQueryPatterns.find(x => String(x.id ?? x.Id) === String(id));
    if (!currentItem) { showQueryPatternBanner('err', 'No se encontró el Query Pattern seleccionado.'); return; }

    const nextIsActive = !(currentItem.isActive ?? currentItem.IsActive);
    const btn = document.getElementById('btnQueryPatternToggle');
    if (btn) btn.disabled = true;

    try {
        const res = await fetch(`/api/admin/query-patterns/${id}/status`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ IsActive: nextIsActive })
        });

        if (res.ok) {
            showQueryPatternBanner('ok', `Query Pattern ${nextIsActive ? 'activado' : 'desactivado'} correctamente.`);
            await loadQueryPatterns();
            await selectQueryPattern(Number(id));
        } else {
            const body = await safeJson(res);
            showQueryPatternBanner('err', body?.Error || ('Error al actualizar estatus. Código: ' + res.status));
        }
    } catch (e) {
        showQueryPatternBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
    }
}

async function loadQueryPatternTerms(patternId) {
    const targetPatternId = patternId || Number(getValue('txtQueryPatternId'));
    const list = document.getElementById('queryPatternTermList');

    if (!targetPatternId) {
        clearQueryPatternTermsState();
        return;
    }

    try {
        const res = await fetch(`/api/admin/query-patterns/${targetPatternId}/terms`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        globalQueryPatternTerms = await res.json();
        renderQueryPatternTerms();
        setValue('txtQueryPatternTermPatternId', targetPatternId);
        hideQueryPatternTermBanner();
    } catch (e) {
        if (list) list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="12" cy="12" r="10"></circle>
                    <path d="M8 12h8"></path>
                    <path d="M12 8v8"></path>
                </svg>
                Error al cargar terms del pattern
            </div>`;
        showQueryPatternTermBanner('err', 'Error al cargar Query Pattern Terms: ' + e.message);
    }
}

function renderQueryPatternTerms() {
    const list = document.getElementById('queryPatternTermList');
    const count = document.getElementById('queryPatternTermCount');
    if (!list) return;

    if (count) count.textContent = globalQueryPatternTerms.length;

    if (!globalQueryPatternTerms.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="12" cy="12" r="10"></circle>
                    <path d="M8 12h8"></path>
                    <path d="M12 8v8"></path>
                </svg>
                Sin terms para este pattern
            </div>`;
        return;
    }

    list.innerHTML = globalQueryPatternTerms.map(item => {
        const id = item.id ?? item.Id;
        const term = item.term || item.Term || '';
        const termGroup = item.termGroup || item.TermGroup || '';
        const matchMode = item.matchMode || item.MatchMode || '';
        const isRequired = !!(item.isRequired ?? item.IsRequired);
        const isActive = !!(item.isActive ?? item.IsActive);
        const groupLabel = formatQueryPatternTermGroup(termGroup);
        const isTimeScope = isTimeScopeTermGroup(termGroup);

        return `
            <div class="history-item" id="qpt-${id}" onclick="selectQueryPatternTerm(${id})">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(term)}</div>
                    <div class="hi-time">${escHtml(matchMode)}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-verify ${isActive ? 'verified' : 'rejected'}">${isActive ? 'Activo' : 'Inactivo'}</span>
                    <span class="hi-status ok meta-muted"><span class="dot dot-muted"></span>${escHtml(groupLabel)}</span>
                    <span class="hi-status ok meta-muted"><span class="dot dot-muted"></span>${isRequired ? 'Required' : 'Optional'}</span>
                    ${isTimeScope ? '<span class="hi-status ok"><span class="dot"></span>TimeScope</span>' : ''}
                </div>
            </div>`;
    }).join('');

    if (selectedQueryPatternTermId !== null) {
        const el = document.getElementById('qpt-' + selectedQueryPatternTermId);
        if (el) el.classList.add('selected');
    }
}

function selectQueryPatternTerm(id) {
    const item = globalQueryPatternTerms.find(x => (x.id ?? x.Id) === id);
    if (!item) return;

    selectedQueryPatternTermId = id;
    document.querySelectorAll('#queryPatternTermList .history-item').forEach(el => el.classList.remove('selected'));

    const target = document.getElementById('qpt-' + id);
    if (target) target.classList.add('selected');

    const patternId = item.patternId ?? item.PatternId ?? '';
    const term = item.term || item.Term || '';
    const termGroup = item.termGroup || item.TermGroup || '';
    const matchMode = item.matchMode || item.MatchMode || 'contains';
    const isRequired = !!(item.isRequired ?? item.IsRequired);
    const isActive = !!(item.isActive ?? item.IsActive);

    setValue('txtQueryPatternTermId', id);
    setValue('txtQueryPatternTermPatternId', patternId);
    setValue('txtQueryPatternTerm', term);
    setValue('txtQueryPatternTermGroup', termGroup);
    setValue('txtQueryPatternTermMatchMode', matchMode);
    setChecked('chkQueryPatternTermIsRequired', isRequired);
    setChecked('chkQueryPatternTermIsActive', isActive);

    const toggleBtn = document.getElementById('btnQueryPatternTermToggle');
    if (toggleBtn) { toggleBtn.disabled = false; toggleBtn.textContent = isActive ? 'Desactivar' : 'Activar'; }

    const meta = document.getElementById('queryPatternTermMeta');
    if (meta) meta.innerHTML = `
        <span class="meta-chip ${isActive ? 'verify-ok' : 'verify-err'}">${isActive ? 'Activo' : 'Inactivo'}</span>
        <span class="meta-chip training-no">Id: ${id}</span>
        <span class="meta-chip training-no">${isRequired ? 'Required' : 'Optional'}</span>
        <span class="meta-chip training-no">${escHtml(formatQueryPatternTermGroup(termGroup))}</span>`;

    syncQueryPatternTermActionButtons();
    hideQueryPatternTermBanner();
}

function resetQueryPatternTermForm() {
    selectedQueryPatternTermId = null;
    document.querySelectorAll('#queryPatternTermList .history-item').forEach(el => el.classList.remove('selected'));

    setValue('txtQueryPatternTermId', '');
    setValue('txtQueryPatternTermPatternId', getValue('txtQueryPatternId'));
    setValue('txtQueryPatternTerm', '');
    setValue('txtQueryPatternTermGroup', '');
    setValue('txtQueryPatternTermMatchMode', 'contains');
    setChecked('chkQueryPatternTermIsRequired', true);
    setChecked('chkQueryPatternTermIsActive', true);

    const toggleBtn = document.getElementById('btnQueryPatternTermToggle');
    if (toggleBtn) { toggleBtn.disabled = true; toggleBtn.textContent = 'Activar / Desactivar'; }

    const meta = document.getElementById('queryPatternTermMeta');
    if (meta) meta.innerHTML = `<span class="meta-empty">Ningún term seleccionado</span>`;

    syncQueryPatternTermActionButtons();
    hideQueryPatternTermBanner();
}

function syncQueryPatternTermActionButtons() {
    const hasPattern = !!getValue('txtQueryPatternId');
    const hasSelection = !!getValue('txtQueryPatternTermId');
    const saveBtn = document.getElementById('btnQueryPatternTermSave');
    const toggleBtn = document.getElementById('btnQueryPatternTermToggle');

    if (saveBtn) {
        saveBtn.disabled = !hasPattern;
        saveBtn.innerHTML = `
            <div class="btn-spinner" id="queryPatternTermSpinner" style="display:none"></div>
            ${hasSelection ? 'Actualizar Term' : 'Agregar Term'}`;
    }

    if (toggleBtn) {
        toggleBtn.disabled = !hasSelection;
        if (!hasSelection) toggleBtn.textContent = 'Activar / Desactivar';
    }
}

async function saveQueryPatternTerm() {
    const termId = getValue('txtQueryPatternTermId');
    const patternId = Number(getValue('txtQueryPatternId'));
    const term = getValue('txtQueryPatternTerm').trim();
    const termGroup = getValue('txtQueryPatternTermGroup').trim();
    const matchMode = getValue('txtQueryPatternTermMatchMode').trim();
    const isRequired = getChecked('chkQueryPatternTermIsRequired');
    const isActive = getChecked('chkQueryPatternTermIsActive');

    if (!patternId) {
        showQueryPatternTermBanner('warn', 'Selecciona primero un Query Pattern.'); return;
    }

    if (!term || !termGroup || !matchMode) {
        showQueryPatternTermBanner('warn', 'Term, TermGroup y MatchMode son requeridos.'); return;
    }

    const btn = document.getElementById('btnQueryPatternTermSave');
    const spin = document.getElementById('queryPatternTermSpinner');
    if (btn) btn.disabled = true;
    if (spin) spin.style.display = 'block';

    try {
        const res = await fetch('/api/admin/query-pattern-terms', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                Id: termId ? Number(termId) : 0,
                PatternId: patternId,
                Term: term,
                TermGroup: termGroup,
                MatchMode: matchMode,
                IsRequired: isRequired,
                IsActive: isActive
            })
        });

        if (res.ok) {
            const body = await safeJson(res);
            showQueryPatternTermBanner('ok', body?.Message || 'Query Pattern Term guardado correctamente.');
            await loadQueryPatternTerms(patternId);
            const savedId = body?.Id ?? null;
            if (savedId !== null) selectQueryPatternTerm(savedId);
        } else {
            const body = await safeJson(res);
            showQueryPatternTermBanner('err', body?.Error || ('Error al guardar Query Pattern Term. Código: ' + res.status));
        }
    } catch (e) {
        showQueryPatternTermBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
        if (spin) spin.style.display = 'none';
    }
}

async function toggleQueryPatternTermStatus() {
    const id = getValue('txtQueryPatternTermId');
    if (!id) { showQueryPatternTermBanner('warn', 'Selecciona un term antes de cambiar su estado.'); return; }

    const currentItem = globalQueryPatternTerms.find(x => String(x.id ?? x.Id) === String(id));
    if (!currentItem) { showQueryPatternTermBanner('err', 'No se encontró el Query Pattern Term seleccionado.'); return; }

    const nextIsActive = !(currentItem.isActive ?? currentItem.IsActive);
    const btn = document.getElementById('btnQueryPatternTermToggle');
    if (btn) btn.disabled = true;

    try {
        const res = await fetch(`/api/admin/query-pattern-terms/${id}/status`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ IsActive: nextIsActive })
        });

        if (res.ok) {
            showQueryPatternTermBanner('ok', `Query Pattern Term ${nextIsActive ? 'activado' : 'desactivado'} correctamente.`);
            await loadQueryPatternTerms(Number(getValue('txtQueryPatternId')));
            selectQueryPatternTerm(Number(id));
        } else {
            const body = await safeJson(res);
            showQueryPatternTermBanner('err', body?.Error || ('Error al actualizar estatus. Código: ' + res.status));
        }
    } catch (e) {
        showQueryPatternTermBanner('err', 'Error de red: ' + e.message);
    } finally {
        if (btn) btn.disabled = false;
    }
}

function clearQueryPatternTermsState() {
    globalQueryPatternTerms = [];
    selectedQueryPatternTermId = null;
    setValue('txtQueryPatternTermId', '');
    setValue('txtQueryPatternTermPatternId', '');
    setValue('txtQueryPatternTerm', '');
    setValue('txtQueryPatternTermGroup', '');
    setValue('txtQueryPatternTermMatchMode', 'contains');
    setChecked('chkQueryPatternTermIsRequired', true);
    setChecked('chkQueryPatternTermIsActive', true);

    const list = document.getElementById('queryPatternTermList');
    if (list) list.innerHTML = `
        <div class="empty-state">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <circle cx="12" cy="12" r="10"></circle>
                <path d="M8 12h8"></path>
                <path d="M12 8v8"></path>
            </svg>
            Selecciona un Query Pattern para operar sus terms
        </div>`;

    const count = document.getElementById('queryPatternTermCount');
    if (count) count.textContent = '0';

    const meta = document.getElementById('queryPatternTermMeta');
    if (meta) meta.innerHTML = `<span class="meta-empty">Ningún term seleccionado</span>`;

    syncQueryPatternTermActionButtons();
    hideQueryPatternTermBanner();
}

function isTimeScopeTermGroup(termGroup) {
    return timeScopeTermGroups.has(String(termGroup || '').trim().toLowerCase());
}

function formatQueryPatternTermGroup(termGroup) {
    const normalized = String(termGroup || '').trim().toLowerCase();
    switch (normalized) {
        case 'time_scope_today': return 'time_scope: today';
        case 'time_scope_yesterday': return 'time_scope: yesterday';
        case 'time_scope_current_week': return 'time_scope: current_week';
        case 'time_scope_current_month': return 'time_scope: current_month';
        case 'time_scope_current_shift': return 'time_scope: current_shift';
        default: return termGroup || '';
    }
}

function showQueryPatternBanner(type, message) {
    const el = document.getElementById('queryPatternBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type; el.textContent = message; el.style.display = 'block';
}
function hideQueryPatternBanner() {
    const el = document.getElementById('queryPatternBanner');
    if (!el) return;
    el.style.display = 'none'; el.className = 'rag-banner';
}
function showQueryPatternTermBanner(type, message) {
    const el = document.getElementById('queryPatternTermBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type; el.textContent = message; el.style.display = 'block';
}
function hideQueryPatternTermBanner() {
    const el = document.getElementById('queryPatternTermBanner');
    if (!el) return;
    el.style.display = 'none'; el.className = 'rag-banner';
}

// -----------------------------------------------------------
// LLM TAB
// -----------------------------------------------------------
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

// -----------------------------------------------------------
// UTILS
// -----------------------------------------------------------
function escHtml(s) {
    return String(s ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;')
        .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function escAttr(s) {
    return escHtml(s).replace(/'/g, '&#39;');
}

function jsString(s) {
    return String(s ?? '').replace(/\\/g, '\\\\').replace(/'/g, "\\'");
}

function sanitizeFileName(value) {
    return String(value ?? 'domain-pack')
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9-_]+/g, '-')
        .replace(/^-+|-+$/g, '') || 'domain-pack';
}

function downloadTextFile(fileName, content) {
    const blob = new Blob([content], { type: 'application/json;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
}

async function safeJson(res) {
    try { return await res.json(); } catch { return null; }
}

function getValue(id) { const el = document.getElementById(id); return el ? el.value : ''; }
function setValue(id, value) { const el = document.getElementById(id); if (el) el.value = value ?? ''; }
function getChecked(id) { const el = document.getElementById(id); return !!(el && el.checked); }
function setChecked(id, value) { const el = document.getElementById(id); if (el) el.checked = !!value; }
function setText(id, value) { const el = document.getElementById(id); if (el) el.textContent = value ?? ''; }

// -----------------------------------------------------------
// INIT
// -----------------------------------------------------------
document.addEventListener('DOMContentLoaded', async () => {
    await loadSystemConfig();
    await loadOnboardingBootstrap();
    loadHistory();
    loadProfiles();
    resetSystemConfigForm();
    resetAllowedObjectForm();
    resetBusinessRuleForm();
    resetSemanticHintForm();
    resetQueryPatternForm();
    await setAdminActiveContext(buildAdminContextFromOnboardingForm(), { reloadCurrentTab: false });

    ['txtOnboardingTenantKey', 'txtOnboardingDisplayName', 'txtOnboardingDomain', 'txtOnboardingConnectionName', 'txtOnboardingDescription', 'txtOnboardingSystemProfileKey']
        .forEach(id => {
            const el = document.getElementById(id);
            if (!el) return;
            el.addEventListener('input', () => {
                const fieldMap = {
                    txtOnboardingTenantKey: 'tenantKey',
                    txtOnboardingDisplayName: 'displayName',
                    txtOnboardingDomain: 'domain',
                    txtOnboardingConnectionName: 'connectionName'
                };
                if (fieldMap[id]) {
                    clearOnboardingFieldError(fieldMap[id]);
                }
                if (id === 'txtOnboardingSystemProfileKey') {
                    toggleOnboardingAdvancedOptions((getValue('txtOnboardingSystemProfileKey').trim() || 'default') !== 'default');
                }
                persistOnboardingWorkspaceState();
                setOnboardingMeta(null);
                renderOnboardingActionGuidance();
                renderTenantDomains();
                renderOnboardingConnectionCatalog();
                renderOnboardingRuntimeContexts();
                updateOnboardingStepper();
            });
            el.addEventListener('change', () => {
                const fieldMap = {
                    txtOnboardingTenantKey: 'tenantKey',
                    txtOnboardingDisplayName: 'displayName',
                    txtOnboardingDomain: 'domain',
                    txtOnboardingConnectionName: 'connectionName'
                };
                if (fieldMap[id]) {
                    clearOnboardingFieldError(fieldMap[id]);
                }
                if (id === 'txtOnboardingSystemProfileKey') {
                    toggleOnboardingAdvancedOptions((getValue('txtOnboardingSystemProfileKey').trim() || 'default') !== 'default');
                }
                persistOnboardingWorkspaceState();
                setOnboardingMeta(null);
                renderOnboardingActionGuidance();
                renderTenantDomains();
                renderOnboardingConnectionCatalog();
                renderOnboardingRuntimeContexts();
                updateOnboardingStepper();
            });
        });

    ['txtOnboardingNewConnectionName', 'txtOnboardingNewConnectionString']
        .forEach(id => {
            const el = document.getElementById(id);
            if (!el) return;
            el.addEventListener('input', () => {
                if (id === 'txtOnboardingNewConnectionName') clearOnboardingFieldError('newConnectionName');
                if (id === 'txtOnboardingNewConnectionString') clearOnboardingFieldError('connectionString');
            });
            el.addEventListener('change', () => {
                if (id === 'txtOnboardingNewConnectionName') clearOnboardingFieldError('newConnectionName');
                if (id === 'txtOnboardingNewConnectionString') clearOnboardingFieldError('connectionString');
            });
        });

    const validationQuestion = document.getElementById('txtOnboardingValidationQuestion');
    if (validationQuestion) {
        validationQuestion.addEventListener('input', () => clearOnboardingFieldError('validationQuestion'));
        validationQuestion.addEventListener('change', () => clearOnboardingFieldError('validationQuestion'));
    }
});

window.addEventListener('resize', () => {
    if (activeOnboardingTourStep >= 0) {
        renderOnboardingTour();
    }
});

window.addEventListener('scroll', () => {
    if (activeOnboardingTourStep >= 0) {
        const popover = document.getElementById('onboardingTourPopover');
        const step = onboardingTourSteps[activeOnboardingTourStep];
        if (!popover || !step) return;
        positionOnboardingTourPopover(document.getElementById(step.targetId), popover);
    }
}, true);

document.addEventListener('keydown', (event) => {
    if (activeOnboardingTourStep < 0) return;
    if (event.key === 'Escape') {
        closeOnboardingTour();
        return;
    }

    if (event.key === 'ArrowRight' || event.key === 'Enter') {
        event.preventDefault();
        nextOnboardingTourStep();
        return;
    }

    if (event.key === 'ArrowLeft') {
        event.preventDefault();
        prevOnboardingTourStep();
    }
});

