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

let globalDocuments = [];
let globalDocumentChunks = [];
let selectedDocumentId = null;
let adminToastSeq = 0;
let globalMlStatus = null;
let globalPredictionProfiles = [];
let globalPredictionDomainPack = null;
let selectedPredictionProfileId = 0;
let globalSqlAlerts = [];
let globalSqlAlertEvents = [];
let globalSqlAlertCatalog = null;
let selectedSqlAlertId = 0;
let adminSignalRConnection = null;

let globalProfiles = [];
let selectedProfileIndex = -1;

let globalTenants = [];
let globalConnectionProfiles = [];
let globalTenantDomains = [];
let globalOnboardingRuntimeContexts = [];
let globalOnboardingSchemaCandidates = [];
let globalOnboardingStatus = null;
let globalOnboardingValidation = null;
let lastOnboardingCompletionJobId = null;
let selectedOnboardingTenantKey = null;
let globalAdminActiveContext = null;
let onboardingBootstrap = null;
let activeOnboardingTourStep = -1;
let onboardingWizardStepOverride = null;
let lastResolvedOnboardingAutoStepIndex = 0;
const onboardingWorkspaceStateKey = 'vannalight.onboarding.workspace';
const onboardingReferencePanelsStateKey = 'vannalight.onboarding.reference-panels';
const adminScopedTabs = new Set(['allowed', 'business-rules', 'semantics', 'patterns']);
const onboardingWizardPanelIds = ['onboardingStepPanel1', 'onboardingStepPanel2b', 'onboardingStepPanel3', 'onboardingStepPanel4', 'onboardingStepPanel5'];

const promptConfigFields = [
    { key: 'SystemPersona', valueType: 'string', elementId: 'txtPromptSystemPersona', description: 'Persona base del system prompt SQL.' },
    { key: 'TaskInstruction', valueType: 'string', elementId: 'txtPromptTaskInstruction', description: 'InstrucciÃƒÆ’Ã‚Â³n principal del system prompt SQL.' },
    { key: 'ContextInstruction', valueType: 'string', elementId: 'txtPromptContextInstruction', description: 'InstrucciÃƒÆ’Ã‚Â³n de uso de contexto del system prompt SQL.' },
    { key: 'SqlSyntaxRules', valueType: 'string', elementId: 'txtPromptSqlSyntaxRules', description: 'Bloque editable de reglas crÃƒÆ’Ã‚Â­ticas de sintaxis T-SQL.' },
    { key: 'TimeInterpretationRules', valueType: 'string', elementId: 'txtPromptTimeInterpretationRules', description: 'Bloque editable de interpretaciÃƒÆ’Ã‚Â³n temporal para el prompt SQL.' },
    { key: 'BusinessRulesHeader', valueType: 'string', elementId: 'txtPromptBusinessRulesHeader', description: 'Encabezado para el bloque de business rules del prompt SQL.' },
    { key: 'SemanticHintsHeader', valueType: 'string', elementId: 'txtPromptSemanticHintsHeader', description: 'Encabezado para el bloque de pistas semÃƒÆ’Ã‚Â¡nticas del prompt SQL.' },
    { key: 'AllowedObjectsHeader', valueType: 'string', elementId: 'txtPromptAllowedObjectsHeader', description: 'Encabezado para el bloque de objetos permitidos del prompt SQL.' },
    { key: 'SchemasHeader', valueType: 'string', elementId: 'txtPromptSchemasHeader', description: 'Encabezado para el bloque de schema docs del prompt SQL.' },
    { key: 'ExamplesHeader', valueType: 'string', elementId: 'txtPromptExamplesHeader', description: 'Encabezado para el bloque de examples del prompt SQL.' },
    { key: 'QuestionHeader', valueType: 'string', elementId: 'txtPromptQuestionHeader', description: 'Encabezado para la pregunta del usuario en el prompt SQL.' },
    { key: 'MaxPromptChars', valueType: 'int', elementId: 'txtPromptMaxPromptChars', description: 'Presupuesto total del prompt SQL en caracteres.' },
    { key: 'MaxRulesChars', valueType: 'int', elementId: 'txtPromptMaxRulesChars', description: 'Presupuesto mÃƒÆ’Ã‚Â¡ximo para reglas de negocio en el prompt SQL.' },
    { key: 'MaxSemanticHintsChars', valueType: 'int', elementId: 'txtPromptMaxSemanticHintsChars', description: 'Presupuesto mÃƒÆ’Ã‚Â¡ximo para pistas semÃƒÆ’Ã‚Â¡nticas del prompt SQL.' },
    { key: 'MaxSchemasChars', valueType: 'int', elementId: 'txtPromptMaxSchemasChars', description: 'Presupuesto mÃƒÆ’Ã‚Â¡ximo para schema docs en el prompt SQL.' },
    { key: 'MaxExamplesChars', valueType: 'int', elementId: 'txtPromptMaxExamplesChars', description: 'Presupuesto mÃƒÆ’Ã‚Â¡ximo para examples en el prompt SQL.' },
    { key: 'MaxRules', valueType: 'int', elementId: 'txtPromptMaxRules', description: 'Cantidad mÃƒÆ’Ã‚Â¡xima de business rules enviadas al prompt SQL.' },
    { key: 'MaxSemanticHints', valueType: 'int', elementId: 'txtPromptMaxSemanticHints', description: 'Cantidad mÃƒÆ’Ã‚Â¡xima de pistas semÃƒÆ’Ã‚Â¡nticas enviadas al prompt SQL.' },
    { key: 'MaxSchemas', valueType: 'int', elementId: 'txtPromptMaxSchemas', description: 'Cantidad mÃƒÆ’Ã‚Â¡xima de schema docs enviados al prompt SQL.' },
    { key: 'MaxExamples', valueType: 'int', elementId: 'txtPromptMaxExamples', description: 'Cantidad mÃƒÆ’Ã‚Â¡xima de training examples enviados al prompt SQL.' }
];

const retrievalConfigFields = [
    { section: 'Retrieval', key: 'Domain', valueType: 'string', elementId: 'txtRetrievalDomain', description: 'Dominio operativo para retrieval y validaciÃƒÆ’Ã‚Â³n.' },
    { section: 'UiDefaults', key: 'AdminDomain', valueType: 'string', elementId: 'txtUiAdminDomain', description: 'Dominio por defecto para pantallas administrativas.' },
    { section: 'Retrieval', key: 'TopExamples', valueType: 'int', elementId: 'txtRetrievalTopExamples', description: 'Cantidad de training examples candidatos para retrieval.' },
    { section: 'Retrieval', key: 'MinExampleScore', valueType: 'double', elementId: 'txtRetrievalMinExampleScore', description: 'Score mÃƒÆ’Ã‚Â­nimo para considerar un training example relevante.' },
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
        body: 'AquÃ­ defines el workspace, el contexto de datos y la conexiÃ³n que usarÃ¡ todo el flujo. Cuando guardas este paso, habilitas el resto del wizard.'
    },
    {
        targetId: 'onboardingStepPanel2b',
        stepLabel: 'Paso 2',
        title: 'Elige las tablas permitidas',
        body: 'Descubre el schema y deja marcadas solo las tablas o vistas que el motor podrÃ¡ consultar. AquÃ­ defines el perÃ­metro seguro del dominio.'
    },
    {
        targetId: 'onboardingStepPanel3',
        stepLabel: 'Paso 3',
        title: 'Prepara el dominio',
        body: 'Este paso genera el contexto tÃ©cnico del motor: schema docs y pistas del dominio. Cuando sale bien, ya puedes probar una pregunta real.'
    },
    {
        targetId: 'onboardingStepPanel4',
        stepLabel: 'Paso 4',
        title: 'Haz una prueba real',
        body: 'Corre una pregunta guiada contra el pipeline real. El wizard te muestra el SQL generado, el resultado y si el dominio ya respondiÃ³ correctamente.'
    },
    {
        targetId: 'onboardingStepPanel5',
        stepLabel: 'Checklist final',
        title: 'Confirma readiness',
        body: 'Este resumen te dice si el dominio ya estÃ¡ listo para usuarios internos o si todavÃ­a necesita mÃ¡s curaciÃ³n antes de salir a operaciÃ³n.'
    }
];
function getOnboardingDefaults() {
    return onboardingBootstrap?.defaults || onboardingBootstrap?.Defaults || {};
}

function getOnboardingProfile() {
    return onboardingBootstrap?.profile || onboardingBootstrap?.Profile || {};
}

function needsInitialOnboardingSetup() {
    return !!(onboardingBootstrap?.needsInitialSetup ?? onboardingBootstrap?.NeedsInitialSetup);
}

function hasOnboardingConnectionProfile(connectionName) {
    const normalized = String(connectionName || '').trim().toLowerCase();
    if (!normalized) return false;
    return globalConnectionProfiles.some(profile =>
        String(profile.connectionName || profile.ConnectionName || '').trim().toLowerCase() === normalized);
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

function buildRagHistoryQuery() {
    const context = getActiveAdminContext();
    if (!context?.tenantKey || !context?.domain || !context?.connectionName) {
        return null;
    }
    const params = new URLSearchParams();
    if (context?.tenantKey) params.set('tenantKey', context.tenantKey);
    if (context?.domain) params.set('domain', context.domain);
    if (context?.connectionName) params.set('connectionName', context.connectionName);
    const query = params.toString();
    return query ? `?${query}` : '';
}

function renderRagContextBanner() {
    const banner = document.getElementById('ragActiveContextBanner');
    if (!banner) return;

    const context = getActiveAdminContext();
    if (!context?.tenantKey || !context?.domain || !context?.connectionName) {
        banner.textContent = 'Selecciona primero un workspace y un contexto vÃ¡lido en Onboarding.';
        banner.classList.add('is-empty');
        return;
    }

    banner.textContent = `Contexto activo: ${context.tenantDisplayName} / ${context.domain} / ${context.connectionName}`;
    banner.classList.remove('is-empty');
}

function resetRagEditor(message = 'Ninguna consulta seleccionada') {
    setValue('txtJobId', '');
    setValue('txtFeedbackComment', '');
    setValue('txtQuestion', '');
    setValue('txtSql', '');

    const ragMeta = document.getElementById('ragMeta');
    if (ragMeta) {
        ragMeta.innerHTML = `<span class="meta-empty">${escHtml(message)}</span>`;
    }

    const badge = document.getElementById('ragVerifyBadge');
    if (badge) {
        badge.style.display = 'none';
        badge.className = 'status-badge warn';
        badge.textContent = '';
    }

    const btnSave = document.getElementById('btnSave');
    if (btnSave) btnSave.disabled = true;
}

function renderRagEmptyState(message) {
    const list = document.getElementById('historyList');
    const count = document.getElementById('ragCount');
    if (count) count.textContent = '0';
    if (!list) return;

    list.innerHTML = `
        <div class="empty-state">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <circle cx="12" cy="12" r="10"/>
                <line x1="12" y1="8" x2="12" y2="12"></line>
                <line x1="12" y1="16" x2="12.01" y2="16"></line>
            </svg>
            ${escHtml(message)}
        </div>`;
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

function getAvailableAdminDomains(extraValues = []) {
    const domains = new Map();
    const addDomain = value => {
        const domain = String(value || '').trim();
        if (!domain) return;
        const lower = domain.toLowerCase();
        if (!domains.has(lower)) {
            domains.set(lower, domain);
        }
    };

    addDomain(globalAdminActiveContext?.domain);
    addDomain(globalMlStatus?.ProfileDomain || globalMlStatus?.profileDomain);

    globalOnboardingRuntimeContexts.forEach(item => {
        addDomain(item?.domain || item?.Domain);
    });

    globalSystemConfigEntries.forEach(item => {
        const section = String(item.section || item.Section || '').trim().toLowerCase();
        const key = String(item.key || item.Key || '').trim().toLowerCase();
        if ((section === 'docs' && key === 'defaultdomain')
            || (section === 'retrieval' && key === 'domain')
            || (section === 'uidefaults' && key === 'admindomain')) {
            addDomain(item.value || item.Value);
        }
    });

    extraValues.forEach(addDomain);
    return Array.from(domains.values()).sort((a, b) => a.localeCompare(b));
}

function populateDomainSelect(selectId, options = {}) {
    const select = document.getElementById(selectId);
    if (!select) return;

    const currentValue = String(select.value || '').trim();
    const domains = getAvailableAdminDomains(options.extraValues || []);
    const includeBlank = options.includeBlank !== false;
    const blankLabel = options.blankLabel || 'Selecciona un dominio';

    select.innerHTML = '';
    if (includeBlank) {
        const blankOption = document.createElement('option');
        blankOption.value = '';
        blankOption.textContent = blankLabel;
        select.appendChild(blankOption);
    }

    domains.forEach(domain => {
        const option = document.createElement('option');
        option.value = domain;
        option.textContent = domain;
        select.appendChild(option);
    });

    const preferredValue = String(options.preferredValue || currentValue || '').trim();
    const selectedValue = domains.find(domain => domain.toLowerCase() === preferredValue.toLowerCase())
        || (includeBlank ? '' : (domains[0] || ''));
    setValue(selectId, selectedValue);
}

function refreshAdminDomainSelectors() {
    const documentsSelect = document.getElementById('txtDocumentDomainFilter');
    const documentsHint = document.getElementById('documentsDomainHint');
    const activeDocsDomain = getActiveAdminContext()?.domain?.trim() || '';

    if (documentsSelect) {
        if (activeDocsDomain) {
            populateDomainSelect('txtDocumentDomainFilter', {
                includeBlank: false,
                preferredValue: activeDocsDomain,
                extraValues: [activeDocsDomain]
            });
            documentsSelect.disabled = true;
            if (documentsHint) {
                documentsHint.innerHTML = `Para el piloto, el dominio documental queda amarrado al contexto activo: <strong>${escHtml(activeDocsDomain)}</strong>.`;
            }
        } else {
            populateDomainSelect('txtDocumentDomainFilter', {
                includeBlank: true,
                blankLabel: 'Default configurado',
                preferredValue: getValue('txtDocumentDomainFilter')
            });
            documentsSelect.disabled = false;
            if (documentsHint) {
    documentsHint.textContent = 'Si no hay contexto activo, se usa el dominio default configurado o el que selecciones aquÃ­.';
            }
        }
    }

    populateDomainSelect('txtPredictionProfileDomain', {
        includeBlank: false,
        preferredValue: getValue('txtPredictionProfileDomain') || getDefaultPredictionDomain()
    });
}

function syncAdminScopedFiltersToContext(context) {
    const normalized = normalizeAdminContext(context);
    const domain = normalized?.domain || '';
    const tenantDisplayName = normalized?.tenantDisplayName || normalized?.tenantKey || 'sin workspace';
    const connectionName = normalized?.connectionName || 'sin conexiÃ³n';

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
        setScopedTabContextBanner(config, 'Selecciona un workspace y un contexto vÃ¡lido en Onboarding.', true);
        }
    });

    refreshAdminDomainSelectors();
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
            renderContextRequiredState(tabKey, 'Selecciona primero un workspace y un contexto vÃ¡lido en Onboarding.');
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
    renderRagContextBanner();

    const currentScopedTab = getCurrentAdminScopedTabKey();
    if (options.reloadCurrentTab && currentScopedTab) {
        if (normalized) {
            await activateAdminScopedTab(currentScopedTab);
        } else {
        renderContextRequiredState(currentScopedTab, 'Selecciona primero un workspace y un contexto vÃ¡lido en Onboarding.');
        }
    }

    if (document.getElementById('pane-rag')?.classList.contains('active')) {
        await loadHistory();
    }
    if (document.getElementById('pane-sql-alerts')?.classList.contains('active')) {
        await loadSqlAlertsAdmin();
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

    if (t === 'documents') {
        await loadDocumentsAdmin();
    }
    if (t === 'ml') {
        await loadMlAdmin();
    }
    if (t === 'rag') {
        await loadHistory();
    }
    if (t === 'sql-alerts') {
        await loadSqlAlertsAdmin();
    }
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
        globalOnboardingRuntimeContexts = (Array.isArray(onboardingBootstrap?.runtimeContexts) ? onboardingBootstrap.runtimeContexts : (Array.isArray(onboardingBootstrap?.RuntimeContexts) ? onboardingBootstrap.RuntimeContexts : []))
            .filter(item => hasOnboardingConnectionProfile(item?.connectionName || item?.ConnectionName || ''));
        globalTenantDomains = [];

        populateOnboardingSummary();
        populateOnboardingConnectionOptions();
        renderOnboardingRuntimeContexts();
        renderOnboardingTenantList();
        toggleOnboardingReferencePanels(readOnboardingReferencePanelsState());

        try {
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
        } catch (hydrateError) {
            console.error('Onboarding bootstrap hydrated with fallback.', hydrateError);
            resetOnboardingForm();
            showOnboardingBanner('warn', 'Cargamos el onboarding, pero no pudimos restaurar el contexto anterior. ContinÃºa desde el paso 1.');
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
    renderOnboardingFlowSummary();
}

function populateOnboardingConnectionOptions() {
    const select = document.getElementById('txtOnboardingConnectionName');
    if (!select) return;

    if (!globalConnectionProfiles.length) {
        select.innerHTML = '<option value="">Selecciona o crea una conexiÃ³n</option>';
        renderOnboardingConnectionCatalog();
        return;
    }

    const currentConnectionName = getValue('txtOnboardingConnectionName').trim();
    const options = globalConnectionProfiles.map(profile => {
        const connectionName = profile.connectionName || profile.ConnectionName || '';
        const profileKey = profile.profileKey || profile.ProfileKey || 'default';
        const databaseName = profile.databaseName || profile.DatabaseName || 'â€”';
        const isActive = !!(profile.isActive || profile.IsActive);
        const label = `${connectionName} Â· ${databaseName} Â· ${profileKey}${isActive ? ' Â· activo' : ''}`;
        return `<option value="${escHtml(connectionName)}">${escHtml(label)}</option>`;
    });

    select.innerHTML = ['<option value="">Selecciona una conexiÃ³n guardada</option>', ...options].join('');
    if (currentConnectionName && globalConnectionProfiles.some(profile =>
        String(profile.connectionName || profile.ConnectionName || '') === currentConnectionName)) {
        select.value = currentConnectionName;
    } else {
        select.value = '';
    }

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
                ${needsInitialOnboardingSetup() ? 'Empieza creando tu primera conexiÃ³n desde este panel' : 'AÃºn no hay conexiones configuradas'}
            </div>`;
        return;
    }

    const selectedConnectionName = getValue('txtOnboardingConnectionName').trim();
    list.innerHTML = globalConnectionProfiles.map(profile => {
        const connectionName = profile.connectionName || profile.ConnectionName || '';
        const profileKey = profile.profileKey || profile.ProfileKey || 'default';
        const databaseName = profile.databaseName || profile.DatabaseName || 'â€”';
        const serverHost = profile.serverHost || profile.ServerHost || 'â€”';
        const description = profile.description || profile.Description || '';
        const isActive = !!(profile.isActive || profile.IsActive);
        const isSelected = selectedConnectionName && selectedConnectionName === connectionName;
        return `
            <div class="history-item onboarding-connection-card ${isSelected ? 'is-selected' : ''}" onclick="applyOnboardingConnection('${jsString(connectionName)}')">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(connectionName)}</div>
                    <div class="hi-time">${escHtml(databaseName)}</div>
                </div>
                <div class="onboarding-connection-meta">${escHtml(serverHost)} Â· Perfil ${escHtml(profileKey)}</div>
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
        const connectionName = item.connectionName || item.ConnectionName || '';
        const profileKey = item.systemProfileKey || item.SystemProfileKey || 'default';
        const isDefault = !!(item.isDefault ?? item.IsDefault);
        const isSelected = tenantKey === selectedTenantKey && domain === selectedDomain && connectionName === selectedConnectionName;

        return `
            <div class="history-item onboarding-mapping is-clickable ${isSelected ? 'is-selected' : ''}" onclick="applyOnboardingRuntimeContext('${jsString(tenantKey)}','${jsString(domain)}','${jsString(connectionName)}','${jsString(profileKey)}')">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(tenantDisplayName)} Â· ${escHtml(domain)}</div>
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
    const runtimeContexts = Array.isArray(globalOnboardingRuntimeContexts) ? globalOnboardingRuntimeContexts : [];
    const runtimeContextCount = runtimeContexts.length;
    const runtimeTenantIndex = new Map();
    runtimeContexts.forEach(item => {
        const tenantKey = String(item?.tenantKey || item?.TenantKey || '').trim();
        if (!tenantKey) return;
        if (!runtimeTenantIndex.has(tenantKey)) {
            runtimeTenantIndex.set(tenantKey, {
                tenantKey,
                displayName: item?.tenantDisplayName || item?.TenantDisplayName || tenantKey,
                isActive: true
            });
        }
    });
    const tenantSource = globalTenants.length
        ? globalTenants
        : Array.from(runtimeTenantIndex.values()).map(item => ({
            tenantKey: item.tenantKey,
            displayName: item.displayName,
            isActive: item.isActive
        }));
    const effectiveCount = Math.max(tenantSource.length, runtimeContextCount, hasPersistedDraft ? 1 : 0);
    if (count) count.textContent = String(effectiveCount);

    const tenantCards = tenantSource.map(tenant => {
        const tenantKey = tenant.tenantKey || tenant.TenantKey || '';
        const displayName = tenant.displayName || tenant.DisplayName || tenantKey;
        const isActive = !!(tenant.isActive || tenant.IsActive);
        const tenantContextCount = runtimeContexts.filter(x =>
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

    let visibleRuntimeContexts = selectedOnboardingTenantKey
        ? runtimeContexts.filter(item =>
            String(item.tenantKey || item.TenantKey || '') === String(selectedOnboardingTenantKey))
        : runtimeContexts;
    if (!visibleRuntimeContexts.length && runtimeContexts.length) {
        visibleRuntimeContexts = runtimeContexts;
    }

    const runtimeCards = visibleRuntimeContexts.map(item => {
        const tenantKey = item.tenantKey || item.TenantKey || '';
        const tenantDisplayName = item.tenantDisplayName || item.TenantDisplayName || tenantKey;
        const domain = item.domain || item.Domain || '';
        const connectionName = item.connectionName || item.ConnectionName || '';
        const profileKey = item.systemProfileKey || item.SystemProfileKey || 'default';
        const isDefault = !!(item.isDefault ?? item.IsDefault);
        const isSelected =
            tenantKey === getValue('txtOnboardingTenantKey').trim()
            && domain === getValue('txtOnboardingDomain').trim()
            && connectionName === getValue('txtOnboardingConnectionName').trim();

        return `
            <div class="history-item onboarding-context-card ${isSelected ? 'selected' : ''}" onclick="applyOnboardingRuntimeContext('${jsString(tenantKey)}','${jsString(domain)}','${jsString(connectionName)}','${jsString(profileKey)}')">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(tenantDisplayName)} Â· ${escHtml(domain)}</div>
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
                AÃºn no hay workspaces registrados
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

    list.innerHTML = sections.length
        ? sections.join('')
        : `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M12 3v18"></path>
                    <path d="M3 12h18"></path>
                    <circle cx="12" cy="12" r="9"></circle>
                </svg>
                <span class="empty-state-title">No pudimos armar la lista del onboarding</span>
                <span class="empty-state-sub">Bootstrap: ${tenantSource.length} workspaces, ${runtimeContextCount} contextos runtime.</span>
            </div>`;
}

async function selectOnboardingTenant(tenantKey) {
    if (!tenantKey) return;
    selectedOnboardingTenantKey = tenantKey;
    renderOnboardingTenantList();
    toggleOnboardingReferencePanels(readOnboardingReferencePanelsState());

    const tenant = globalTenants.find(x => String(x.tenantKey || x.TenantKey || '') === String(tenantKey))
        || globalOnboardingRuntimeContexts.find(x => String(x.tenantKey || x.TenantKey || '') === String(tenantKey));
    if (!tenant) {
        resetOnboardingForm();
        return;
    }

    setValue('txtOnboardingTenantKey', tenant.tenantKey || tenant.TenantKey || '');
    setValue('txtOnboardingDisplayName', tenant.displayName || tenant.DisplayName || tenant.tenantDisplayName || tenant.TenantDisplayName || '');
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

        globalTenantDomains = (await res.json()).filter(item =>
            hasOnboardingConnectionProfile(item?.connectionName || item?.ConnectionName || ''));
        renderTenantDomains();

        const defaultMapping = globalTenantDomains.find(x => !!(x.isDefault ?? x.IsDefault)) || globalTenantDomains[0];
        if (defaultMapping) {
            setValue('txtOnboardingDomain', defaultMapping.domain || defaultMapping.Domain || '');
            const defaults = getOnboardingDefaults();
            setValue('txtOnboardingConnectionName', defaultMapping.connectionName || defaultMapping.ConnectionName || defaults.connectionName || defaults.ConnectionName || '');
            setValue('txtOnboardingSystemProfileKey', defaultMapping.systemProfileKey || defaultMapping.SystemProfileKey || defaults.systemProfileKey || defaults.SystemProfileKey || 'default');
            setOnboardingMeta(defaultMapping);
        } else {
            const persistedWorkspace = readOnboardingWorkspaceState();
            const defaults = getOnboardingDefaults();
            setValue('txtOnboardingDomain', persistedWorkspace?.domain || defaults.domain || defaults.Domain || defaultAllowedDomain || '');
            setValue('txtOnboardingConnectionName', persistedWorkspace?.connectionName || defaults.connectionName || defaults.ConnectionName || '');
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
                Este workspace aÃºn no tiene mappings
            </div>`;
        return;
    }

    list.innerHTML = globalTenantDomains.map(item => {
        const domain = item.domain || item.Domain || '';
        const connectionName = item.connectionName || item.ConnectionName || '';
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
    setValue('txtOnboardingConnectionName', connectionName || '');
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
    setValue('txtOnboardingConnectionName', connectionName || '');
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

function resetOnboardingForm(options = {}) {
    const useBootstrapDefaults = options.useBootstrapDefaults !== false;
    closeOnboardingTour();
    selectedOnboardingTenantKey = null;
    renderOnboardingTenantList();
        toggleOnboardingReferencePanels(readOnboardingReferencePanelsState());
    globalTenantDomains = [];
    renderTenantDomains();
    globalOnboardingSchemaCandidates = [];
    renderOnboardingSchemaCandidates();
    globalOnboardingStatus = null;
    renderOnboardingStatus();
    resetOnboardingValidation();
    hideOnboardingPackBanner();

    const defaults = getOnboardingDefaults();
    setValue('txtOnboardingTenantKey', useBootstrapDefaults ? (defaults.tenantKey || defaults.TenantKey || defaultAdminTenant || 'default') : '');
    setValue('txtOnboardingDisplayName', '');
    setValue('txtOnboardingDescription', '');
    setValue('txtOnboardingDomain', useBootstrapDefaults ? (defaults.domain || defaults.Domain || defaultAllowedDomain || '') : '');
    setValue('txtOnboardingConnectionName', useBootstrapDefaults ? (defaults.connectionName || defaults.ConnectionName || '') : '');
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

function clearOnboardingWorkspaceState() {
    try {
        localStorage.removeItem(onboardingWorkspaceStateKey);
    } catch {
        // no-op
    }
}

function startNewOnboardingWorkspace() {
    clearOnboardingWorkspaceState();
    resetOnboardingForm({ useBootstrapDefaults: false });
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
        connectionName: 'ConexiÃ³n de base de datos',
        connectionString: 'Connection String',
        systemProfileKey: 'Perfil tÃ©cnico'
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
        .replaceAll('ConnectionName', 'ConexiÃ³n de base de datos')
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
        ['displayName', getValue('txtOnboardingDisplayName').trim(), 'Escribe el nombre visible que verÃ¡n los usuarios.'],
        ['domain', getValue('txtOnboardingDomain').trim(), 'Define el contexto de datos que usarÃ¡ este dominio.'],
        ['connectionName', getValue('txtOnboardingConnectionName').trim(), 'Selecciona una conexiÃ³n de base de datos.']
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
        if (showErrors) setOnboardingFieldError('newConnectionName', 'Escribe un nombre para guardar esta conexiÃ³n.');
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

function readOnboardingReferencePanelsState() {
    try {
        const raw = localStorage.getItem(onboardingReferencePanelsStateKey);
        if (raw === null) return false;
        return raw === '1';
    } catch {
        return false;
    }
}

function persistOnboardingReferencePanelsState(isOpen) {
    try {
        localStorage.setItem(onboardingReferencePanelsStateKey, isOpen ? '1' : '0');
    } catch {
        // no-op
    }
}

function getOnboardingFlowState() {
    const health = globalOnboardingStatus?.health ?? globalOnboardingStatus?.Health ?? null;
    const hasWorkspace = !!getValue('txtOnboardingTenantKey').trim() && !!getValue('txtOnboardingDomain').trim() && !!getValue('txtOnboardingConnectionName').trim();
    const hasAllowedObjects = !!(health?.hasAllowedObjects ?? health?.HasAllowedObjects);
    const hasAllowedSelection = globalOnboardingSchemaCandidates.some(x => !!x.isSelected);
    const hasAllowed = hasAllowedSelection || hasAllowedObjects;
    const hasSchemaDocs = !!(health?.hasSchemaDocs ?? health?.HasSchemaDocs);
    const hasSemanticHints = !!(health?.hasSemanticHints ?? health?.HasSemanticHints);
    const isInitialized = hasSchemaDocs && hasSemanticHints;
    const hasValidation = isOnboardingValidationSuccessful(globalOnboardingValidation);

    let currentStep = 1;
    let currentStepLabel = 'Paso 1 Â· Workspace';
    let currentStepHint = 'Empieza configurando el workspace, el dominio y la conexiÃ³n a la base.';
    let requiredAction = needsInitialOnboardingSetup()
        ? 'Crear o seleccionar una conexiÃ³n vÃ¡lida y guardar el workspace.'
        : 'Completar tenant, dominio y conexiÃ³n, luego guardar el workspace.';
    let nextAction = 'Descubrir el schema para elegir tablas permitidas.';
    let progress = 0;
    let progressHint = 'AÃºn no hay pasos obligatorios completados.';

    if (hasWorkspace) {
        currentStep = 2;
        currentStepLabel = 'Paso 2 Â· Tablas permitidas';
        currentStepHint = 'Ya existe contexto mÃ­nimo; ahora toca definir el perÃ­metro seguro del dominio.';
        requiredAction = hasAllowedSelection
            ? 'Guardar la selecciÃ³n actual de tablas permitidas.'
            : 'Descubrir schema y seleccionar al menos una tabla o vista permitida.';
        nextAction = 'Preparar el dominio para generar schema docs e hints.';
        progress = 1;
        progressHint = 'El workspace base ya quedÃ³ configurado.';
    }

    if (hasAllowed) {
        currentStep = 3;
        currentStepLabel = 'Paso 3 Â· Preparar dominio';
        currentStepHint = 'El perÃ­metro seguro ya existe; ahora falta generar el contexto tÃ©cnico mÃ­nimo.';
        requiredAction = 'Ejecutar la preparaciÃ³n del dominio para generar schema docs y semantic hints base.';
        nextAction = 'Hacer una pregunta real para validar el dominio.';
        progress = 2;
        progressHint = 'El dominio ya sabe quÃ© objetos puede consultar.';
    }

    if (isInitialized) {
        currentStep = 4;
        currentStepLabel = 'Paso 4 Â· Prueba real';
        currentStepHint = 'El motor ya tiene contexto tÃ©cnico suficiente para intentar una consulta de negocio.';
        requiredAction = 'Ejecutar una pregunta real y revisar que el SQL y el resultado sean correctos.';
        nextAction = 'Si responde bien, el dominio ya puede considerarse listo para salida inicial.';
        progress = 3;
        progressHint = 'El dominio ya fue preparado e indexado.';
    }

    if (hasValidation) {
        currentStep = 4;
        currentStepLabel = 'Listo Â· Onboarding base completo';
        currentStepHint = 'El flujo base ya quedÃ³ operativo para una primera salida.';
        requiredAction = 'No hay bloqueos base pendientes; lo siguiente es afinaciÃ³n opcional.';
        nextAction = 'Business Rules, Semantic Hints manuales y Query Patterns pueden refinarse despuÃ©s.';
        progress = 4;
        progressHint = 'Los 4 pasos obligatorios del onboarding base estÃ¡n completos.';
    }

    return {
        hasWorkspace,
        hasAllowedObjects,
        hasAllowedSelection,
        hasAllowed,
        hasSchemaDocs,
        hasSemanticHints,
        isInitialized,
        hasValidation,
        currentStep,
        currentStepLabel,
        currentStepHint,
        requiredAction,
        nextAction,
        progress,
        progressHint
    };
}

function renderOnboardingFlowSummary() {
    const state = getOnboardingFlowState();
    setText('txtOnboardingCurrentStep', state.currentStepLabel);
    setText('txtOnboardingRequiredAction', state.requiredAction);

    const meta = document.getElementById('onboardingFlowSummaryMeta');
    if (!meta) return;

    const selectedDomain = getValue('txtOnboardingDomain').trim() || 'sin-domain';
    const selectedConnection = getValue('txtOnboardingConnectionName').trim() || 'sin-conexion';
    const chips = [
        `<span class="meta-chip training-no">${state.progress} / 4</span>`,
        `<span class="meta-chip training-no">${escHtml(selectedDomain)}</span>`,
        `<span class="meta-chip training-no">${escHtml(selectedConnection)}</span>`
    ];

    if (state.hasValidation) {
        chips.unshift('<span class="meta-chip verify-ok">Operativo</span>');
    } else if (state.isInitialized) {
        chips.unshift('<span class="meta-chip verify-pending">Falta validar</span>');
    } else {
        chips.unshift('<span class="meta-chip training-no">En curso</span>');
    }

    meta.innerHTML = chips.join('');

    const activeStepIndex = resolveActiveOnboardingStepIndex();
    const prevBtn = document.getElementById('btnOnboardingPrevStep');
    const nextBtn = document.getElementById('btnOnboardingNextStep');
    if (prevBtn) prevBtn.disabled = activeStepIndex <= 0;
    if (nextBtn) {
        nextBtn.disabled = activeStepIndex >= getCurrentOnboardingStepIndex();
        nextBtn.textContent = activeStepIndex >= 4 ? 'Cierre' : 'Siguiente';
    }
}

function toggleOnboardingReferencePanels(forceOpen = null) {
    const panel = document.getElementById('onboardingReferencePanels');
    const button = document.getElementById('btnToggleOnboardingReferencePanels');
    if (!panel || !button) return;

    const shouldOpen = forceOpen === null ? panel.classList.contains('is-collapsed') : !!forceOpen;
    panel.classList.toggle('is-collapsed', !shouldOpen);
    button.textContent = shouldOpen ? 'Ocultar apoyo' : 'Mostrar apoyo';
    persistOnboardingReferencePanelsState(shouldOpen);
}

function toggleOnboardingFinalTools(forceOpen = null) {
    const panel = document.getElementById('onboardingFinalTools');
    const button = document.getElementById('btnToggleOnboardingFinalTools');
    if (!panel || !button) return;

    const shouldOpen = forceOpen === null ? panel.classList.contains('is-collapsed') : !!forceOpen;
    panel.classList.toggle('is-collapsed', !shouldOpen);
    button.textContent = shouldOpen ? 'Ocultar herramientas' : 'Mostrar herramientas';
}

function renderOnboardingActionGuidance() {
    renderOnboardingFlowSummary();
    const meta = document.getElementById('onboardingMeta');
    if (!meta) return;

    const state = getOnboardingFlowState();
    let hint = 'Completa este paso y usa la accion principal para avanzar.';

    if (!state.hasWorkspace) {
        hint = needsInitialOnboardingSetup()
            ? 'Guarda el workspace y una conexion valida para habilitar el resto del flujo.'
            : 'Guarda el workspace para habilitar el resto del flujo.';
    } else if (!state.hasAllowed) {
        hint = 'Selecciona solo las tablas o vistas necesarias y guardalas.';
    } else if (!state.isInitialized) {
        hint = 'Prepara el dominio para generar el contexto tecnico base.';
    } else if (!state.hasValidation) {
        hint = 'Haz una prueba real con una pregunta simple y valida el resultado.';
    } else {
        hint = 'Flujo base listo. La afinacion avanzada ya es opcional.';
    }

    meta.innerHTML = `<span class="meta-empty">${escHtml(hint)}</span>`;
    syncOnboardingFooterActions();
}

function scrollToOnboardingPanel(panelId) {
    const stepIndex = onboardingWizardPanelIds.indexOf(panelId);
    if (stepIndex < 0) return;
    setOnboardingWizardStep(stepIndex);
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

function resolveActiveOnboardingStepIndex() {
    const autoStep = getCurrentOnboardingStepIndex();
    if (onboardingWizardStepOverride === null || onboardingWizardStepOverride === undefined) {
        onboardingWizardStepOverride = autoStep;
    } else {
        if (autoStep > lastResolvedOnboardingAutoStepIndex && onboardingWizardStepOverride >= lastResolvedOnboardingAutoStepIndex) {
            onboardingWizardStepOverride = autoStep;
        }
        onboardingWizardStepOverride = Math.max(0, Math.min(onboardingWizardStepOverride, autoStep));
    }

    lastResolvedOnboardingAutoStepIndex = autoStep;
    return onboardingWizardStepOverride;
}

function setOnboardingWizardStep(stepIndex, shouldScroll = true) {
    const maxUnlockedStep = getCurrentOnboardingStepIndex();
    const nextStep = Math.max(0, Math.min(stepIndex, maxUnlockedStep));
    onboardingWizardStepOverride = nextStep;
    updateOnboardingStepper();

    if (!shouldScroll) return;
    const panelId = onboardingWizardPanelIds[nextStep];
    const panel = panelId ? document.getElementById(panelId) : null;
    if (panel) {
        panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
}

function goToPreviousOnboardingStep() {
    setOnboardingWizardStep(resolveActiveOnboardingStepIndex() - 1);
}

function goToNextOnboardingStep() {
    setOnboardingWizardStep(resolveActiveOnboardingStepIndex() + 1);
}

function buildOnboardingTourRuntimeNote(stepIndex) {
    const health = globalOnboardingStatus?.health ?? globalOnboardingStatus?.Health ?? null;
    const selectedTables = globalOnboardingSchemaCandidates.filter(x => !!x.isSelected).length;
    const allowedObjectsCount = globalOnboardingStatus?.allowedObjectsCount ?? globalOnboardingStatus?.AllowedObjectsCount ?? 0;
    const schemaDocsCount = globalOnboardingStatus?.schemaDocsCount ?? globalOnboardingStatus?.SchemaDocsCount ?? 0;
    const semanticHintsCount = globalOnboardingStatus?.semanticHintsCount ?? globalOnboardingStatus?.SemanticHintsCount ?? 0;
    const currentConnection = getValue('txtOnboardingConnectionName').trim() || 'sin conexiÃ³n';

    switch (stepIndex) {
        case 0:
            return currentConnection === 'sin conexiÃ³n'
                ? 'Ahora mismo todavÃ­a falta seleccionar una conexiÃ³n para que el wizard pueda continuar.'
                : `La conexiÃ³n actual del wizard es ${currentConnection}.`;
        case 1:
            if (!globalOnboardingSchemaCandidates.length) {
                return 'TodavÃ­a no hay schema cargado. El siguiente clic Ãºtil aquÃ­ es "Descubrir schema".';
            }
            return `Ahora mismo hay ${selectedTables} objeto(s) seleccionados y ${allowedObjectsCount} ya guardado(s).`;
        case 2:
            return `Estado actual: ${allowedObjectsCount} tablas, ${schemaDocsCount} schema docs y ${semanticHintsCount} pistas del dominio.`;
        case 3:
            return isOnboardingValidationSuccessful(globalOnboardingValidation)
                ? 'La prueba actual ya respondiÃ³ correctamente.'
                : 'AquÃ­ esperamos ver SQL generado, resultado y una respuesta marcada como correcta.';
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

    const currentPanelId = onboardingWizardPanelIds[resolveActiveOnboardingStepIndex()] || 'onboardingStepPanel1';

    Object.entries(states).forEach(([panelId, isComplete]) => {
        const panel = document.getElementById(panelId);
        if (!panel) return;
        panel.classList.toggle('is-complete', !!isComplete);
        panel.classList.toggle('is-current', panelId === currentPanelId);
    });

    document.getElementById('onboardingFinalBreak')?.classList.toggle(
        'is-active',
        currentPanelId === 'onboardingStepPanel5' || hasValidation
    );
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
        toggleOnboardingReferencePanels(readOnboardingReferencePanelsState());
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
    const trigger = document.querySelector('#onboardingStepPanel1 .connection-picker .btn.btn-ghost');
    if (!editor) return;

    const shouldOpen = forceOpen === null ? editor.style.display === 'none' : !!forceOpen;
    editor.style.display = shouldOpen ? 'block' : 'none';
    editor.classList.toggle('is-open', shouldOpen);
    if (trigger) {
        trigger.textContent = shouldOpen ? 'Cerrar editor' : '+ Nueva';
        trigger.classList.toggle('is-active', shouldOpen);
    }

    if (shouldOpen) {
        const currentConnectionName = getValue('txtOnboardingConnectionName').trim();
        if (!getValue('txtOnboardingNewConnectionName').trim()) {
            setValue('txtOnboardingNewConnectionName', currentConnectionName || '');
        }
        initializeOnboardingConnectionWizard();
    }
}

function initializeOnboardingConnectionWizard() {
    if (!getValue('txtOnboardingConnectionMode').trim()) {
        setValue('txtOnboardingConnectionMode', 'wizard');
    }

    if (!getValue('txtOnboardingConnServer').trim()) {
        setValue('txtOnboardingConnServer', 'localhost');
    }

    if (!getValue('txtOnboardingConnPort').trim()) {
        setValue('txtOnboardingConnPort', '1433');
    }

    handleOnboardingConnectionModeChange();
    handleOnboardingAuthModeChange();
    buildOnboardingConnectionString();
}

function handleOnboardingConnectionModeChange() {
    const mode = getValue('txtOnboardingConnectionMode').trim() || 'wizard';
    const wizard = document.getElementById('onboardingConnectionWizard');
    if (wizard) {
        wizard.style.display = mode === 'wizard' ? 'block' : 'none';
    }
}

function handleOnboardingAuthModeChange() {
    const authMode = getValue('txtOnboardingConnAuthMode').trim() || 'sql';
    const userInput = document.getElementById('txtOnboardingConnUser');
    const passwordInput = document.getElementById('txtOnboardingConnPassword');
    const isWindows = authMode === 'windows';

    if (userInput) {
        userInput.disabled = isWindows;
        if (isWindows) userInput.value = '';
    }

    if (passwordInput) {
        passwordInput.disabled = isWindows;
        if (isWindows) passwordInput.value = '';
    }

    buildOnboardingConnectionString();
}

function buildOnboardingConnectionString(forceNotify = false) {
    const mode = getValue('txtOnboardingConnectionMode').trim() || 'wizard';
    if (mode !== 'wizard') return getValue('txtOnboardingNewConnectionString').trim();

    const server = getValue('txtOnboardingConnServer').trim();
    const port = getValue('txtOnboardingConnPort').trim();
    const database = getValue('txtOnboardingConnDatabase').trim();
    const authMode = getValue('txtOnboardingConnAuthMode').trim() || 'sql';
    const user = getValue('txtOnboardingConnUser').trim();
    const password = getValue('txtOnboardingConnPassword').trim();
    const encrypt = getValue('txtOnboardingConnEncrypt').trim() || 'True';
    const trustCert = getValue('txtOnboardingConnTrustCert').trim() || 'True';

    const serverToken = port ? `${server},${port}` : server;
    const parts = [];
    if (serverToken) parts.push(`Server=${serverToken}`);
    if (database) parts.push(`Database=${database}`);

    if (authMode === 'windows') {
        parts.push('Trusted_Connection=True');
    } else {
        if (user) parts.push(`User Id=${user}`);
        if (password) parts.push(`Password=${password}`);
    }

    parts.push(`Encrypt=${encrypt}`);
    parts.push(`TrustServerCertificate=${trustCert}`);

    const connectionString = parts.length ? parts.join(';') + ';' : '';
    setValue('txtOnboardingNewConnectionString', connectionString);

    if (forceNotify && (!server || !database || (authMode === 'sql' && (!user || !password)))) {
        showOnboardingConnectionBanner('warn', 'Completa servidor, base de datos y credenciales antes de generar la cadena completa.');
    }

    return connectionString;
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
    const connectionString = buildOnboardingConnectionString() || getValue('txtOnboardingNewConnectionString').trim();
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
            showOnboardingConnectionBanner('err', humanizeOnboardingErrorMessage(body?.Error || ('Error validando conexiÃ³n. CÃ³digo: ' + res.status)));
            return;
        }

        setOnboardingConnectionMeta(
            `Servidor: ${body?.ServerHost || 'n/d'} Â· Base: ${body?.DatabaseName || 'n/d'} Â· Auth integrada: ${body?.IntegratedSecurity ? 'sÃ­' : 'no'}`,
            'meta-chip training-no');
        showOnboardingConnectionBanner('ok', body?.Message || 'ConexiÃ³n validada correctamente.');
    } catch (e) {
        showOnboardingConnectionBanner('err', 'Error de red validando conexiÃ³n: ' + e.message);
    } finally {
        if (spinner) spinner.style.display = 'none';
    }
}

async function saveOnboardingConnection() {
    const connectionName = getValue('txtOnboardingNewConnectionName').trim();
    const connectionString = buildOnboardingConnectionString() || getValue('txtOnboardingNewConnectionString').trim();
    const description = getValue('txtOnboardingNewConnectionDescription').trim();

    const missing = validateOnboardingConnectionFields(true);
    if (missing.length) {
        showOnboardingConnectionBanner('warn', `Completa ${formatOnboardingMissingFields(missing)} antes de guardar la conexiÃ³n.`);
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
            showOnboardingConnectionBanner('err', humanizeOnboardingErrorMessage(body?.Error || ('Error guardando conexiÃ³n. CÃ³digo: ' + res.status)));
            return;
        }

        persistOnboardingWorkspaceState();
        await loadOnboardingBootstrap();
        setValue('txtOnboardingConnectionName', body?.ConnectionName || connectionName);
        persistOnboardingWorkspaceState();
        setOnboardingConnectionMeta(
            `Guardada: ${body?.ConnectionName || connectionName} Â· ${body?.ServerHost || 'n/d'} Â· ${body?.DatabaseName || 'n/d'}`,
            'meta-chip status-ok');
        showOnboardingConnectionBanner('ok', body?.Message || 'ConexiÃ³n guardada correctamente.');
        toggleOnboardingConnectionEditor(false);
        renderOnboardingActionGuidance();
    } catch (e) {
        showOnboardingConnectionBanner('err', 'Error de red guardando conexiÃ³n: ' + e.message);
    } finally {
        if (spinner) spinner.style.display = 'none';
    }
}

async function loadOnboardingSchemaCandidates() {
    const connectionName = getValue('txtOnboardingConnectionName').trim();
    const domain = getValue('txtOnboardingDomain').trim();

    if (!connectionName || !domain) {
        validateOnboardingStep1Fields(true);
    showOnboardingBanner('warn', 'Primero guarda el workspace con un contexto de datos y una conexiÃ³n.');
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
    const selectedBadge = document.getElementById('txtOnboardingSchemaSelectionCount');
    const visibleBadge = document.getElementById('txtOnboardingSchemaVisibleCount');
    if (!list) return;

    const selectedCount = globalOnboardingSchemaCandidates.filter(x => !!x.isSelected).length;
    const search = String(getValue('txtOnboardingSchemaSearch') || '').trim().toLowerCase();
    const typeFilter = String(getValue('selOnboardingSchemaType') || 'all').trim().toLowerCase();
    const assistFilter = String(getValue('selOnboardingSchemaAssist') || 'all').trim().toLowerCase();

    const visibleItems = globalOnboardingSchemaCandidates
        .map((item, sourceIndex) => ({ item, sourceIndex }))
        .filter(({ item }) => {
            const haystack = [item.schemaName, item.SchemaName, item.objectName, item.ObjectName, item.description, item.Description].join(' ').toLowerCase();
            const objectType = normalizeOnboardingObjectType(item.objectType || item.ObjectType || '');
            const isRecommended = !!(item.isSuggested ?? item.IsSuggested ?? item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed);
            const isSelected = !!item.isSelected;
            const isRisky = isRiskyOnboardingCandidate(item);
            if (search && !haystack.includes(search)) return false;
            if (typeFilter !== 'all' && objectType !== typeFilter) return false;
            if (assistFilter === 'recommended' && !isRecommended) return false;
            if (assistFilter === 'selected' && !isSelected) return false;
            if (assistFilter === 'risky' && !isRisky) return false;
            return true;
        })
        .sort((left, right) => {
            const a = left.item;
            const b = right.item;
            const aAllowed = !!(a.isCurrentlyAllowed ?? a.IsCurrentlyAllowed);
            const bAllowed = !!(b.isCurrentlyAllowed ?? b.IsCurrentlyAllowed);
            const aRecommended = !!(a.isSuggested ?? a.IsSuggested ?? aAllowed);
            const bRecommended = !!(b.isSuggested ?? b.IsSuggested ?? bAllowed);
            const aSelected = !!a.isSelected;
            const bSelected = !!b.isSelected;
            const aRisky = isRiskyOnboardingCandidate(a);
            const bRisky = isRiskyOnboardingCandidate(b);
            const scoreA = (aSelected ? 100 : 0) + (aAllowed ? 70 : 0) + (aRecommended ? 40 : 0) - (aRisky ? 25 : 0);
            const scoreB = (bSelected ? 100 : 0) + (bAllowed ? 70 : 0) + (bRecommended ? 40 : 0) - (bRisky ? 25 : 0);
            if (scoreA !== scoreB) return scoreB - scoreA;

            const aKey = `${a.schemaName || a.SchemaName || ''}.${a.objectName || a.ObjectName || ''}`.toLowerCase();
            const bKey = `${b.schemaName || b.SchemaName || ''}.${b.objectName || b.ObjectName || ''}`.toLowerCase();
            return aKey.localeCompare(bKey);
        });

    if (selectedBadge) {
        selectedBadge.textContent = `${selectedCount} seleccionadas`;
    }
    if (visibleBadge) {
        visibleBadge.textContent = globalOnboardingSchemaCandidates.length
            ? `${visibleItems.length} visibles`
            : 'Sin schema';
    }

    if (!globalOnboardingSchemaCandidates.length) {
        list.innerHTML = '<div class="empty-state"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="4" y="4" width="16" height="16" rx="2"></rect><path d="M8 8h8"></path><path d="M8 12h8"></path><path d="M8 16h5"></path></svg><span class="empty-state-title">Schema no cargado</span><span class="empty-state-sub">Descubre el schema para empezar a seleccionar objetos permitidos.</span></div>';
        if (meta) meta.innerHTML = '<span class="meta-empty">Aun no se ha cargado el schema de la conexion.</span>';
        if (saveBtn) saveBtn.disabled = true;
        renderOnboardingActionGuidance();
        return;
    }

    const riskyCount = globalOnboardingSchemaCandidates.filter(isRiskyOnboardingCandidate).length;
    const allowedCount = globalOnboardingSchemaCandidates.filter(item => !!(item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed)).length;
    if (meta) {
        meta.innerHTML = `<span class="meta-chip status-ok">${selectedCount} seleccionadas</span><span class="meta-chip training-no">${allowedCount} ya permitidas</span><span class="meta-chip training-no">${visibleItems.length} visibles</span>${riskyCount ? `<span class="meta-chip verify-pending">${riskyCount} revisar</span>` : ""}`;
    }

    if (!visibleItems.length) {
        list.innerHTML = '<div class="empty-state"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"></circle><path d="M8 12h8"></path><path d="M12 8v8"></path></svg><span class="empty-state-title">Sin coincidencias</span><span class="empty-state-sub">Ajusta los filtros o limpia la busqueda para ver mas objetos.</span></div>';
        if (saveBtn) saveBtn.disabled = selectedCount === 0;
        return;
    }

    list.innerHTML = visibleItems.map(({ item, sourceIndex }) => {
        const schemaName = item.schemaName || item.SchemaName || '';
        const objectName = item.objectName || item.ObjectName || '';
        const objectType = item.objectType || item.ObjectType || '';
        const desc = item.description || item.Description || '';
        const isRecommended = !!(item.isSuggested ?? item.IsSuggested ?? item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed);
        const isCurrentlyAllowed = !!(item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed);
        const isSelected = !!item.isSelected;
        const columnCount = item.columnCount ?? item.ColumnCount ?? 0;
        const pkCount = item.primaryKeyCount ?? item.PrimaryKeyCount ?? 0;
        const fkCount = item.foreignKeyCount ?? item.ForeignKeyCount ?? 0;
        const risky = isRiskyOnboardingCandidate(item);
        return `<label class="schema-candidate ${isSelected ? 'is-selected' : ''} ${isRecommended ? 'is-recommended' : ''} ${risky ? 'is-risky' : ''} ${isCurrentlyAllowed ? 'is-currently-allowed' : ''}"><input type="checkbox" ${isSelected ? 'checked' : ''} onchange="toggleOnboardingSchemaCandidate(${sourceIndex}, this.checked)" /><div class="schema-candidate-body"><div class="schema-candidate-head"><div><div class="hi-question">${escHtml(schemaName)}.${escHtml(objectName)}</div><div class="schema-candidate-submeta">${escHtml(objectType)} - ${columnCount} cols - ${pkCount} pk - ${fkCount} fk</div></div><div class="schema-candidate-tags">${isCurrentlyAllowed ? '<span class=\"meta-chip training-no\">Ya permitida</span>' : ''}${isSelected && !isCurrentlyAllowed ? '<span class=\"meta-chip verify-ok\">Seleccionada</span>' : ''}${isRecommended && !isCurrentlyAllowed ? '<span class=\"meta-chip status-ok\">Recomendada</span>' : ''}${risky ? '<span class=\"meta-chip verify-pending\">Revisar</span>' : ''}</div></div>${desc ? `<div class="schema-candidate-desc">${escHtml(desc)}</div>` : '<div class=\"schema-candidate-desc\">Sin descripcion disponible. Revisala por nombre, tipo y llaves antes de permitirla.</div>'}</div></label>`;
    }).join("");

    if (saveBtn) saveBtn.disabled = selectedCount === 0;
    syncOnboardingFooterActions();
    renderOnboardingActionGuidance();
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
        meta.innerHTML = '<span class="meta-empty">Ejecuta la inicializaciÃ³n para dejar el dominio listo para pruebas.</span>';
        } else {
            meta.innerHTML = `
                <span class="meta-chip status-ok">${escHtml(domain || '')}</span>
                <span class="meta-chip training-no">${escHtml(connectionName || 'sin-conexion')}</span>
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
    const activeStepIndex = resolveActiveOnboardingStepIndex();

    setStepChipState('stepChip1', hasWorkspace, activeStepIndex === 0);
    setStepChipState('stepChip2', hasAllowed, activeStepIndex === 1);
    setStepChipState('stepChip3', isInitialized, activeStepIndex === 2);
    setStepChipState('stepChip4', hasValidation, activeStepIndex === 3);
    setGuideItemState('guideItem1', hasWorkspace, activeStepIndex === 0);
    setGuideItemState('guideItem2', hasAllowed, activeStepIndex === 1);
    setGuideItemState('guideItem3', isInitialized, activeStepIndex === 2);
    setGuideItemState('guideItem4', hasValidation, activeStepIndex === 3);

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
    el.classList.toggle('is-active', !!isActive);
}

function setGuideItemState(id, isComplete, isActive) {
    const el = document.getElementById(id);
    if (!el) return;
    el.classList.toggle('is-complete', !!isComplete);
    el.classList.toggle('is-active', !!isActive);
    el.classList.toggle('is-pending', !isComplete && !isActive);

    const num = el.querySelector('.guide-item-num');
    if (num) {
        num.textContent = isComplete ? 'OK' : id.replace('guideItem', '');
    }
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
            showOnboardingBanner('err', humanizeOnboardingErrorMessage(body?.Error || ('Error al guardar el workspace. CÃ³digo: ' + res.status)));
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

        meta.innerHTML = '<span class="meta-empty">Workspace sin mapping default aÃºn</span>';
        return;
    }

    meta.innerHTML = `
        <span class="meta-chip status-ok">${escHtml(mapping.domain || mapping.Domain || '')}</span>
        <span class="meta-chip training-no">${escHtml(mapping.connectionName || mapping.ConnectionName || 'sin-conexion')}</span>
        <span class="meta-chip training-no">${escHtml(mapping.systemProfileKey || mapping.SystemProfileKey || 'default')}</span>`;
}

function resetOnboardingValidation(keepQuestion = false) {
    const currentQuestion = getValue('txtOnboardingValidationQuestion').trim();
    globalOnboardingValidation = null;
    lastOnboardingCompletionJobId = null;
    clearOnboardingFieldError('validationQuestion');

    if (!keepQuestion) {
        setValue('txtOnboardingValidationQuestion', '');
    } else if (currentQuestion) {
        setValue('txtOnboardingValidationQuestion', currentQuestion);
    }

    hideOnboardingValidationBanner();
    closeOnboardingCompletionModal();
    renderOnboardingValidation();
}

function renderOnboardingValidation() {
    const suggestionList = document.getElementById('onboardingSuggestionList');
    const meta = document.getElementById('onboardingValidationMeta');
    const runBtn = document.getElementById('btnOnboardingRunValidation');
    const panel = document.getElementById('onboardingStepPanel4');
    const resultCard = document.getElementById('txtOnboardingValidationResult')?.closest('.validation-card');
    const sqlCard = document.getElementById('txtOnboardingValidationSql')?.closest('.validation-card');
    const health = globalOnboardingStatus?.health ?? globalOnboardingStatus?.Health ?? null;
    const isInitialized = !!(health?.hasSchemaDocs ?? health?.HasSchemaDocs) && !!(health?.hasSemanticHints ?? health?.HasSemanticHints);
    const suggestions = buildOnboardingSuggestedQuestions();
    const currentQuestion = getValue('txtOnboardingValidationQuestion').trim();
    const validationOk = isOnboardingValidationSuccessful(globalOnboardingValidation);

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
            meta.innerHTML = '<span class="meta-empty">Completa la preparaciÃ³n del dominio antes de ejecutar una prueba.</span>';
        } else if (!globalOnboardingValidation) {
            meta.innerHTML = `
                <span class="meta-chip training-no">${escHtml(getValue('txtOnboardingDomain').trim() || 'sin-domain')}</span>
                <span class="meta-chip training-no">${escHtml(getValue('txtOnboardingConnectionName').trim() || 'sin-conexion')}</span>
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

        suggestions.push(`MuÃ©strame 5 registros de ${objectName}.`);
        suggestions.push(`Â¿CuÃ¡ntos registros hay en ${objectName}?`);
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
    showOnboardingValidationBanner('warn', 'Completa la preparaciÃ³n del dominio antes de ejecutar una prueba.');
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
        showOnboardingValidationBanner('err', body?.Error || ('Error al iniciar la prueba. CÃ³digo: ' + res.status));
            globalOnboardingValidation.status = 'Failed';
            globalOnboardingValidation.errorText = body?.Error || 'No se pudo encolar la prueba.';
            renderOnboardingValidation();
            return;
        }

        globalOnboardingValidation.jobId = body?.JobId || body?.jobId || null;
        globalOnboardingValidation.status = body?.Status || body?.status || 'Queued';
        renderOnboardingValidation();

        if (!globalOnboardingValidation.jobId) {
        showOnboardingValidationBanner('err', 'La API no devolviÃ³ un JobId para monitorear la prueba.');
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
                showOnboardingValidationBanner('ok', 'La pregunta de prueba respondiÃ³ correctamente. El dominio ya tiene un camino bÃ¡sico funcional.');
                maybeCelebrateOnboardingCompletion(globalOnboardingValidation);
            } else if (String(status).toLowerCase() === 'requiresreview') {
                showOnboardingValidationBanner('warn', 'La prueba llegÃ³ a revisiÃ³n. El dominio necesita curaciÃ³n adicional antes de salir a usuarios.');
            } else {
                showOnboardingValidationBanner('err', 'La prueba fallÃ³. Revisa AllowedObjects, SchemaDocs y SemanticHints antes de continuar.');
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
        return 'AÃºn no se ha ejecutado ninguna prueba.';
    }

    return String(validation.sqlText).trim();
}

function formatOnboardingValidationResult(validation) {
    if (!validation) {
        return 'Ejecuta una pregunta real para confirmar que el dominio responde correctamente.';
    }

    const status = String(validation.status || 'Queued');
    if (!isTerminalOnboardingJobStatus(status)) {
        return `Estado: ${status}\n\nLa prueba sigue ejecutÃ¡ndose.`;
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

        return `Estado: ${status}\n\nLa consulta terminÃ³ sin datos serializados visibles.`;
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

function maybeCelebrateOnboardingCompletion(validation) {
    const jobId = String(validation?.jobId || '').trim();
    if (!jobId || lastOnboardingCompletionJobId === jobId) {
        return;
    }

    lastOnboardingCompletionJobId = jobId;
    openOnboardingCompletionModal(validation);
}

function openOnboardingCompletionModal(validation = globalOnboardingValidation) {
    const overlay = document.getElementById('onboardingCompletionOverlay');
    const modal = document.getElementById('onboardingCompletionModal');
    const title = document.getElementById('onboardingCompletionTitle');
    const body = document.getElementById('onboardingCompletionBody');
    const chips = document.getElementById('onboardingCompletionMeta');
    if (!overlay || !modal || !title || !body || !chips) {
        return;
    }

    const tenantKey = getValue('txtOnboardingTenantKey').trim() || 'sin-tenant';
    const domain = getValue('txtOnboardingDomain').trim() || 'sin-domain';
    const connectionName = getValue('txtOnboardingConnectionName').trim() || 'sin-conexion';
    const question = String(validation?.question || getValue('txtOnboardingValidationQuestion').trim() || '').trim();
    const resultCount = validation?.resultCount;

    title.textContent = 'ConfiguraciÃ³n completada satisfactoriamente';
    body.textContent = question
        ? `La pregunta final respondiÃ³ correctamente y este dominio ya quedÃ³ listo para empezar a usarse. Puedes seguir afinÃ¡ndolo, pero el onboarding bÃ¡sico ya quedÃ³ cerrado con Ã©xito. Pregunta validada: "${question}".`
        : 'La pregunta final respondiÃ³ correctamente y este dominio ya quedÃ³ listo para empezar a usarse. Puedes seguir afinÃ¡ndolo, pero el onboarding bÃ¡sico ya quedÃ³ cerrado con Ã©xito.';
    chips.innerHTML = `
        <span class="meta-chip status-ok">${escHtml(tenantKey)}</span>
        <span class="meta-chip training-no">${escHtml(domain)}</span>
        <span class="meta-chip training-no">${escHtml(connectionName)}</span>
        ${resultCount !== null && resultCount !== undefined ? `<span class="meta-chip verify-ok">${escHtml(String(resultCount))} filas</span>` : '<span class="meta-chip verify-ok">Prueba superada</span>'}`;

    overlay.style.display = 'block';
    modal.style.display = 'flex';
    document.body.classList.add('modal-open');
}

function closeOnboardingCompletionModal() {
    const overlay = document.getElementById('onboardingCompletionOverlay');
    const modal = document.getElementById('onboardingCompletionModal');
    if (overlay) {
        overlay.style.display = 'none';
    }
    if (modal) {
        modal.style.display = 'none';
    }
    document.body.classList.remove('modal-open');
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
            <span class="meta-chip training-no">${escHtml(getValue('txtOnboardingConnectionName').trim() || 'sin-conexion')}</span>
            <span class="meta-chip status-ok">Onboarding bÃƒÆ’Ã‚Â¡sico completado</span>`;
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
            showOnboardingPackBanner('err', body?.Error || ('Error al exportar el pack. CÃƒÆ’Ã‚Â³digo: ' + res.status));
            return;
        }

        const serialized = JSON.stringify(body, null, 2);
        setValue('txtOnboardingDomainPackJson', serialized);
        renderOnboardingPackMeta(body);
        showOnboardingPackBanner('ok', 'Domain pack exportado correctamente. Ya quedÃƒÆ’Ã‚Â³ cargado en el editor y listo para descargarse o copiarse.');
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
        showOnboardingPackBanner('err', 'El JSON del pack no es vÃƒÆ’Ã‚Â¡lido: ' + e.message);
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
            showOnboardingPackBanner('err', body?.Error || ('Error al importar el pack. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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

        populateSystemConfigSectionFilter();
        refreshAdminDomainSelectors();
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

function populateSystemConfigSectionFilter() {
    const select = document.getElementById('txtSystemConfigSectionFilter');
    if (!select) return;

    const currentValue = getValue('txtSystemConfigSectionFilter').trim();
    const preferredOrder = ['Prompting', 'Retrieval', 'UiDefaults', 'Docs'];
    const knownSections = new Map();

    preferredOrder.forEach(section => knownSections.set(section.toLowerCase(), section));
    globalSystemConfigEntries.forEach(item => {
        const section = String(item.section || item.Section || '').trim();
        if (!section) return;
        const lower = section.toLowerCase();
        if (!knownSections.has(lower)) {
            knownSections.set(lower, section);
        }
    });

    const orderedSections = [
        ...preferredOrder.filter(section => knownSections.has(section.toLowerCase())),
        ...Array.from(knownSections.values())
            .filter(section => !preferredOrder.includes(section))
            .sort((a, b) => a.localeCompare(b))
    ];

    select.innerHTML = '<option value="">Todas las secciones</option>';
    orderedSections.forEach(section => {
        const option = document.createElement('option');
        option.value = section;
        option.textContent = section;
        select.appendChild(option);
    });

    const matchedCurrent = orderedSections.find(section => section.toLowerCase() === currentValue.toLowerCase());
    setValue('txtSystemConfigSectionFilter', matchedCurrent || (orderedSections.includes('Prompting') ? 'Prompting' : ''));
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
            showSystemConfigBanner('err', body?.Error || ('Error al guardar. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
                    <span class="hi-status ok"><span class="dot"></span>${escHtml(String(value).slice(0, 48) || '(vacÃƒÆ’Ã‚Â­o)')}</span>
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
            showSystemConfigBanner('err', body?.Error || ('Error al guardar. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
    renderRagContextBanner();

    const historyQuery = buildRagHistoryQuery();
    if (!historyQuery) {
        globalJobs = [];
        hideRagBanner();
        resetRagEditor('Selecciona un contexto en Onboarding para revisar consultas.');
        renderRagEmptyState('Selecciona primero un workspace y un contexto vÃƒÆ’Ã‚Â¡lido en Onboarding.');
        return;
    }

    try {
        const res = await fetch(`/api/admin/history${historyQuery}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        const jobs = await res.json();
        globalJobs = Array.isArray(jobs) ? jobs : [];
        hideRagBanner();
        resetRagEditor(globalJobs.length ? 'Selecciona una consulta del historial.' : 'No hay consultas del contexto activo para revisar.');
        renderHistory();
    } catch (e) {
        globalJobs = [];
        resetRagEditor('No se pudo cargar el historial del contexto activo.');
        renderRagEmptyState('Error al cargar historial');
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
    const parsed = parseAdminDate(raw);
    const stamp = parsed ? parsed.getTime() : NaN;
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

        const parsedTime = parseAdminDate(created);
        const time = parsedTime
            ? parsedTime.toLocaleTimeString('es-MX', { hour12: false, hour: '2-digit', minute: '2-digit' })
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
    const feedbackUtcDate = parseAdminDate(feedbackUtcRaw);
    const feedbackUtc = feedbackUtcDate
        ? feedbackUtcDate.toLocaleString('es-MX', { hour12: false })
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
    const createdDate = parseAdminDate(createdRaw);
    const created = createdDate
        ? createdDate.toLocaleString('es-MX', { hour12: false })
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
            <span class="meta-chip ${trClass}">RAG: ${trained ? 'SÃƒÆ’Ã‚Â­' : 'No'}</span>
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
    const context = getActiveAdminContext();

    if (!jobId || !question || !sql) return;
    if (!context?.tenantKey || !context?.domain || !context?.connectionName) {
        showRagBanner('warn', 'Selecciona primero un contexto vÃƒÆ’Ã‚Â¡lido en Onboarding antes de guardar en memoria RAG.');
        return;
    }

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
            showRagBanner('err', body?.Error || ('Error al guardar. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
            showSchemaBanner('ok', body?.Message || body?.message || 'ReindexaciÃƒÆ’Ã‚Â³n de schema completada correctamente.');
        } else {
            showSchemaBanner(
                'err',
                body?.Error || body?.error || body?.Detail || body?.detail || ('Error al reindexar schema. CÃƒÆ’Ã‚Â³digo: ' + res.status)
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
    if (meta) meta.innerHTML = `<span class="meta-empty">NingÃƒÆ’Ã‚Âºn objeto seleccionado</span>`;

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
            showAllowedBanner('err', body?.Error || ('Error al guardar Allowed Object. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
    if (!currentItem) { showAllowedBanner('err', 'No se encontrÃƒÆ’Ã‚Â³ el Allowed Object seleccionado.'); return; }

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
            showAllowedBanner('err', body?.Error || ('Error al actualizar estatus. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
            showBusinessRuleBanner('err', body?.Error || ('Error al guardar Business Rule. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
    if (!currentItem) { showBusinessRuleBanner('err', 'No se encontrÃƒÆ’Ã‚Â³ la Business Rule seleccionada.'); return; }

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
            showBusinessRuleBanner('err', body?.Error || ('Error al actualizar estatus. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
    if (meta) meta.innerHTML = `<span class="meta-empty">Ninguna pista semÃƒÆ’Ã‚Â¡ntica seleccionada</span>`;

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
            showSemanticHintBanner('err', body?.Error || ('Error al guardar Semantic Hint. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
    if (!id) { showSemanticHintBanner('warn', 'Selecciona una pista semÃƒÆ’Ã‚Â¡ntica antes de cambiar su estado.'); return; }

    const currentItem = globalSemanticHints.find(x => String(x.id ?? x.Id) === String(id));
    if (!currentItem) { showSemanticHintBanner('err', 'No se encontrÃƒÆ’Ã‚Â³ la Semantic Hint seleccionada.'); return; }

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
            showSemanticHintBanner('err', body?.Error || ('Error al actualizar estatus. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
    if (meta) meta.innerHTML = `<span class="meta-empty">NingÃƒÆ’Ã‚Âºn pattern seleccionado</span>`;

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
            showQueryPatternBanner('err', body?.Error || ('Error al guardar Query Pattern. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
    if (!currentItem) { showQueryPatternBanner('err', 'No se encontrÃƒÆ’Ã‚Â³ el Query Pattern seleccionado.'); return; }

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
            showQueryPatternBanner('err', body?.Error || ('Error al actualizar estatus. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
    if (meta) meta.innerHTML = `<span class="meta-empty">NingÃƒÆ’Ã‚Âºn term seleccionado</span>`;

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
            showQueryPatternTermBanner('err', body?.Error || ('Error al guardar Query Pattern Term. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
    if (!currentItem) { showQueryPatternTermBanner('err', 'No se encontrÃƒÆ’Ã‚Â³ el Query Pattern Term seleccionado.'); return; }

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
            showQueryPatternTermBanner('err', body?.Error || ('Error al actualizar estatus. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
    if (meta) meta.innerHTML = `<span class="meta-empty">NingÃƒÆ’Ã‚Âºn term seleccionado</span>`;

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
                    <div class="pi-summary">GPU Layers: ${gpu} Ãƒâ€šÃ‚Â· Context: ${Number(ctx).toLocaleString('es-MX')}</div>
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
            showAlert('err', bodyErr?.Error || ('Error al guardar ajustes. CÃƒÆ’Ã‚Â³digo: ' + res.status));
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
                <div style="font-size:.75rem;color:#fbbf24;opacity:.9">IMPORTANTE: DetÃƒÆ’Ã‚Â©n y vuelve a ejecutar la API para que LLamaSharp cargue los nuevos pesos en la VRAM.</div>
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
// DOCUMENTS TAB
// -----------------------------------------------------------
function getDocumentsDomainFilter() {
    const activeDomain = getActiveAdminContext()?.domain?.trim();
    if (activeDomain) return activeDomain;
    return getValue('txtDocumentDomainFilter').trim();
}

function showDocumentsBanner(type, message) {
    const el = document.getElementById('documentsBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type;
    el.textContent = message;
    el.style.display = 'block';
}

function hideDocumentsBanner() {
    const el = document.getElementById('documentsBanner');
    if (!el) return;
    el.style.display = 'none';
    el.className = 'rag-banner';
    el.textContent = '';
}

function showAdminToast(type, message, title = '', timeoutMs = 4200) {
    const host = document.getElementById('adminToastHost');
    if (!host || !message) return;

    const toast = document.createElement('div');
    toast.className = `admin-toast ${type || 'ok'}`;

    const toastTitle = title || (type === 'ok'
        ? 'OperaciÃƒÆ’Ã‚Â³n completada'
        : type === 'warn'
            ? 'AtenciÃƒÆ’Ã‚Â³n'
            : 'OcurriÃƒÆ’Ã‚Â³ un error');

    toast.innerHTML = `
        <button type="button" class="admin-toast-close" aria-label="Cerrar notificaciÃƒÆ’Ã‚Â³n">ÃƒÆ’Ã¢â‚¬â€</button>
        <div class="admin-toast-title">${escHtml(toastTitle)}</div>
        <div class="admin-toast-body">${escHtml(message)}</div>`;

    const currentToastId = ++adminToastSeq;
    toast.dataset.toastId = String(currentToastId);

    const dismiss = () => {
        if (toast.parentElement) {
            toast.parentElement.removeChild(toast);
        }
    };

    toast.querySelector('.admin-toast-close')?.addEventListener('click', dismiss);
    host.appendChild(toast);

    window.setTimeout(() => {
        if (toast.dataset.toastId === String(currentToastId)) {
            dismiss();
        }
    }, Math.max(1800, timeoutMs || 0));
}

function setDocumentsBusy(isBusy, message = '') {
    const uploadBtn = document.getElementById('btnDocumentsUpload');
    const reindexBtn = document.getElementById('btnDocumentsReindex');
    const uploadSpinner = document.getElementById('documentsUploadSpinner');
    const reindexSpinner = document.getElementById('documentsReindexSpinner');
    const uploadLabel = document.getElementById('txtDocumentsUploadLabel');
    const reindexLabel = document.getElementById('txtDocumentsReindexLabel');
    const activeBanner = document.getElementById('documentsActiveBanner');

    if (uploadBtn) uploadBtn.disabled = isBusy || uploadBtn.dataset.blocked === 'true';
    if (reindexBtn) reindexBtn.disabled = isBusy || reindexBtn.dataset.blocked === 'true';
    if (uploadSpinner) uploadSpinner.style.display = isBusy && message.includes('Subiendo') ? 'block' : 'none';
    if (reindexSpinner) reindexSpinner.style.display = isBusy && message.includes('Reindex') ? 'block' : 'none';
    if (uploadLabel) uploadLabel.textContent = isBusy && message.includes('Subiendo') ? 'Subiendo...' : 'Subir PDFs';
    if (reindexLabel) reindexLabel.textContent = isBusy && message.includes('Reindex') ? 'Reindexando...' : 'Reindex Documents';
    if (activeBanner && message) activeBanner.textContent = message;
}

function applyDocumentsStatusUi(status) {
    const uploadBtn = document.getElementById('btnDocumentsUpload');
    const reindexBtn = document.getElementById('btnDocumentsReindex');
    const activeBanner = document.getElementById('documentsActiveBanner');
    const rootExists = !!(status?.rootExists ?? status?.RootExists);
    const filesOnDisk = Number(status?.filesOnDisk ?? status?.FilesOnDisk ?? 0);
    const indexedDocuments = Number(status?.indexedDocuments ?? status?.IndexedDocuments ?? 0);
    const effectiveDomain =
        status?.effectiveDomain ||
        status?.EffectiveDomain ||
        status?.defaultDomain ||
        status?.DefaultDomain ||
        getDocumentsDomainFilter() ||
        'ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â';

    if (uploadBtn) uploadBtn.dataset.blocked = rootExists ? 'false' : 'true';
    if (reindexBtn) reindexBtn.dataset.blocked = rootExists ? 'false' : 'true';
    if (uploadBtn) uploadBtn.disabled = !rootExists;
    if (reindexBtn) reindexBtn.disabled = !rootExists;

    if (activeBanner) {
        activeBanner.textContent = rootExists
            ? `Contexto documental listo. Dominio activo: ${effectiveDomain}. PDFs en disco: ${filesOnDisk}. Indexados: ${indexedDocuments}.`
            : `Contexto documental activo: ${effectiveDomain}. Falta configurar una carpeta vÃƒÆ’Ã‚Â¡lida en Docs:RootPath antes de subir o reindexar.`;
    }
}

function ensureDocumentsDomainPrefill() {
    refreshAdminDomainSelectors();
}

function parseAdminDate(value) {
    if (!value) return null;
    if (value instanceof Date) return Number.isNaN(value.getTime()) ? null : value;
    const raw = String(value).trim();
    if (!raw) return null;

    const normalized = /z$|[+-]\d{2}:\d{2}$/i.test(raw)
        ? raw
        : raw.includes('T')
            ? `${raw}Z`
            : `${raw.replace(' ', 'T')}Z`;

    const dt = new Date(normalized);
    return Number.isNaN(dt.getTime()) ? null : dt;
}

function formatAdminDateTime(value) {
    if (!value) return '';
    const dt = parseAdminDate(value);
    if (!dt) return String(value);
    return dt.toLocaleString('es-MX', {
        year: 'numeric',
        month: 'short',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function renderDocumentsList() {
    const list = document.getElementById('documentList');
    const count = document.getElementById('documentCount');
    if (count) count.textContent = String(globalDocuments.length);
    if (!list) return;

    if (!globalDocuments.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M8 3h7l5 5v13a1 1 0 0 1-1 1H8a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z"></path>
                    <path d="M15 3v6h6"></path>
                </svg>
                No hay documentos indexados para este dominio todavÃƒÆ’Ã‚Â­a.
            </div>`;
        return;
    }

    list.innerHTML = globalDocuments.map(doc => {
        const docId = String(doc.docId || doc.DocId || '');
        const fileName = String(doc.fileName || doc.FileName || docId || 'Documento');
        const title = String(doc.title || doc.Title || fileName);
        const pageCount = Number(doc.pageCount || doc.PageCount || 0);
        const chunkCount = Number(doc.chunkCount || doc.ChunkCount || 0);
        const documentType = String(doc.documentType || doc.DocumentType || 'pdf');
        const updatedUtc = String(doc.updatedUtc || doc.UpdatedUtc || '').trim();
        const selectedClass = docId === selectedDocumentId ? ' is-selected' : '';

        return `
            <div class="document-item${selectedClass}" onclick="selectDocumentAdmin('${jsString(docId)}')">
                <div class="document-item-head">
                    <div>
                        <div class="document-item-title">${escHtml(title)}</div>
                        <div class="document-item-sub">${escHtml(fileName)}</div>
                    </div>
                    <span class="hi-verify verified">${escHtml(documentType.toUpperCase())}</span>
                </div>
                <div class="document-item-meta">
                    <span class="meta-chip status-ok">${pageCount} pÃƒÆ’Ã‚Â¡gs</span>
                    <span class="meta-chip training-no">${chunkCount} chunks</span>
                    ${updatedUtc ? `<span class="meta-chip training-no">${escHtml(formatAdminDateTime(updatedUtc))}</span>` : ''}
                </div>
            </div>`;
    }).join('');
}

function renderDocumentChunks() {
    const list = document.getElementById('documentChunkList');
    const nameEl = document.getElementById('txtDocumentSelectedName');
    const metaEl = document.getElementById('txtDocumentSelectedMeta');
    const chunkCountEl = document.getElementById('txtDocumentChunkCount');
    const pageCountEl = document.getElementById('txtDocumentPageCount');

    const selected = globalDocuments.find(doc => String(doc.docId || doc.DocId || '') === String(selectedDocumentId || '')) || null;
    if (nameEl) nameEl.textContent = selected ? String(selected.title || selected.Title || selected.fileName || selected.FileName || 'Documento') : 'ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â';
    if (metaEl) {
        metaEl.textContent = selected
            ? `${String(selected.fileName || selected.FileName || 'sin-archivo')} Ãƒâ€šÃ‚Â· ${Number(selected.chunkCount || selected.ChunkCount || 0)} chunks`
            : 'Selecciona un documento de la lista para ver sus chunks.';
    }
    if (chunkCountEl) chunkCountEl.textContent = String(globalDocumentChunks.length);
    if (pageCountEl) pageCountEl.textContent = String(Number(selected?.pageCount || selected?.PageCount || 0));
    if (!list) return;

    if (!selectedDocumentId) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M4 6h16"></path>
                    <path d="M4 12h16"></path>
                    <path d="M4 18h10"></path>
                </svg>
                Selecciona un documento para revisar sus chunks y metadata.
            </div>`;
        return;
    }

    if (!globalDocumentChunks.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M4 6h16"></path>
                    <path d="M4 12h16"></path>
                    <path d="M4 18h10"></path>
                </svg>
                Este documento todavÃƒÆ’Ã‚Â­a no tiene chunks indexados o la carga fallÃƒÆ’Ã‚Â³.
            </div>`;
        return;
    }

    list.innerHTML = globalDocumentChunks.map(chunk => {
        const page = Number(chunk.pageNumber || chunk.PageNumber || 0);
        const order = Number(chunk.chunkOrder || chunk.ChunkOrder || 0);
        const title = String(chunk.chunkTitle || chunk.ChunkTitle || '').trim();
        const section = String(chunk.sectionName || chunk.SectionName || '').trim();
        const partNumbers = String(chunk.partNumbers || chunk.PartNumbers || '').trim();
        const tokenCount = Number(chunk.tokenCount || chunk.TokenCount || 0);
        const normalizedTokens = String(chunk.normalizedTokens || chunk.NormalizedTokens || '').trim();
        const isCover = !!(chunk.isCoverPage ?? chunk.IsCoverPage);
        const text = String(chunk.text || chunk.Text || '').trim();

        return `
            <div class="document-chunk-card">
                <div class="document-chunk-head">
                    <div>
                        <div class="document-chunk-title">${escHtml(title || `PÃƒÆ’Ã‚Â¡gina ${page} Ãƒâ€šÃ‚Â· Chunk ${order}`)}</div>
                        <div class="document-chunk-sub">${section ? escHtml(section) + ' Ãƒâ€šÃ‚Â· ' : ''}PÃƒÆ’Ã‚Â¡gina ${page} Ãƒâ€šÃ‚Â· Orden ${order}</div>
                    </div>
                    <div class="document-chunk-meta">
                        ${isCover ? '<span class="meta-chip verify-ok">Cover</span>' : ''}
                        <span class="meta-chip training-no">${tokenCount} tokens</span>
                    </div>
                </div>
                <div class="document-item-meta">
                    ${partNumbers ? `<span class="meta-chip status-ok">${escHtml(partNumbers)}</span>` : ''}
                    ${normalizedTokens ? `<span class="meta-chip training-no">${escHtml(normalizedTokens.slice(0, 96))}${normalizedTokens.length > 96 ? 'ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦' : ''}</span>` : ''}
                </div>
                <div class="document-chunk-text">${escHtml(text)}</div>
            </div>`;
    }).join('');
}

async function loadDocumentsAdmin(preserveBanner = false) {
    ensureDocumentsDomainPrefill();
    const list = document.getElementById('documentList');
    const domain = getDocumentsDomainFilter();
    if (!preserveBanner) {
        hideDocumentsBanner();
    }
    if (list) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M8 3h7l5 5v13a1 1 0 0 1-1 1H8a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z"></path>
                    <path d="M15 3v6h6"></path>
                </svg>
                Cargando documentos...
            </div>`;
    }

    try {
        await loadDocumentsStatus();
        const suffix = domain ? `?domain=${encodeURIComponent(domain)}` : '';
        const res = await fetch(`/api/admin/documents${suffix}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        globalDocuments = await res.json();

        if (!selectedDocumentId || !globalDocuments.some(doc => String(doc.docId || doc.DocId || '') === String(selectedDocumentId))) {
            selectedDocumentId = globalDocuments[0]?.docId || globalDocuments[0]?.DocId || null;
        }

        renderDocumentsList();

        if (selectedDocumentId) {
            await selectDocumentAdmin(selectedDocumentId, false);
        } else {
            globalDocumentChunks = [];
            renderDocumentChunks();
        }
    } catch (e) {
        globalDocuments = [];
        selectedDocumentId = null;
        globalDocumentChunks = [];
        renderDocumentsList();
        renderDocumentChunks();
        showDocumentsBanner('err', 'Error cargando documentos: ' + e.message);
    }
}

async function loadDocumentsStatus() {
    try {
        const domain = getDocumentsDomainFilter();
        const suffix = domain ? `?domain=${encodeURIComponent(domain)}` : '';
        const res = await fetch(`/api/admin/documents/status${suffix}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const body = await res.json();
        const normalized = {
            rootExists: !!(body?.rootExists ?? body?.RootExists),
            rootPath: body?.rootPath || body?.RootPath || '',
            filesOnDisk: Number(body?.filesOnDisk ?? body?.FilesOnDisk ?? 0),
            indexedDocuments: Number(body?.indexedDocuments ?? body?.IndexedDocuments ?? 0),
            effectiveDomain: body?.effectiveDomain || body?.EffectiveDomain || '',
            defaultDomain: body?.defaultDomain || body?.DefaultDomain || ''
        };

        setText('txtDocumentsRootExists', normalized.rootExists ? 'Lista' : 'Falta');
        setText('txtDocumentsRootPath', normalized.rootPath || 'Sin ruta configurada');
        setText('txtDocumentsFilesOnDisk', String(normalized.filesOnDisk));
        setText('txtDocumentsIndexedCount', String(normalized.indexedDocuments));
        setText('txtDocumentsDefaultDomain', `Dominio documental activo: ${normalized.effectiveDomain || normalized.defaultDomain || 'ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â'}`);
        applyDocumentsStatusUi(normalized);
        return normalized;
    } catch (e) {
        setText('txtDocumentsRootExists', 'Error');
        setText('txtDocumentsRootPath', 'No pude leer la configuraciÃƒÆ’Ã‚Â³n documental.');
        setText('txtDocumentsFilesOnDisk', '0');
        setText('txtDocumentsIndexedCount', '0');
        setText('txtDocumentsDefaultDomain', 'Dominio documental activo: ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â');
        applyDocumentsStatusUi({ RootExists: false, EffectiveDomain: getDocumentsDomainFilter() || 'ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â', FilesOnDisk: 0, IndexedDocuments: 0 });
        return null;
    }
}

async function selectDocumentAdmin(docId, updateList = true) {
    selectedDocumentId = docId ? String(docId) : null;
    if (updateList) renderDocumentsList();

    if (!selectedDocumentId) {
        globalDocumentChunks = [];
        renderDocumentChunks();
        return;
    }

    try {
        const res = await fetch(`/api/admin/documents/${encodeURIComponent(selectedDocumentId)}/chunks`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        globalDocumentChunks = await res.json();
        renderDocumentsList();
        renderDocumentChunks();
    } catch (e) {
        globalDocumentChunks = [];
        renderDocumentsList();
        renderDocumentChunks();
        showDocumentsBanner('err', 'Error cargando chunks: ' + e.message);
    }
}

async function reindexDocumentsAdmin() {
    const domain = getDocumentsDomainFilter();
    const status = await loadDocumentsStatus();
    if (!(status?.rootExists ?? status?.RootExists)) {
        showDocumentsBanner('warn', 'No puedo reindexar porque no hay una carpeta documental vÃƒÆ’Ã‚Â¡lida configurada en Docs:RootPath.');
        showAdminToast('warn', 'Falta una carpeta documental vÃƒÆ’Ã‚Â¡lida antes de reindexar.', 'Reindex no disponible');
        return;
    }

    try {
        setDocumentsBusy(true, `Reindexando documentos del dominio ${domain || 'default'}...`);
        const suffix = domain ? `?domain=${encodeURIComponent(domain)}` : '';
        const res = await fetch(`/api/admin/reindex-docs${suffix}`, { method: 'POST' });
        const body = await safeJson(res);
        if (!res.ok) {
            throw new Error(body?.Error || body?.Message || `HTTP ${res.status}`);
        }

        const resultDomain = body?.domain || body?.Domain || domain || 'ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â';
        const totalFiles = Number(body?.totalFiles ?? body?.TotalFiles ?? 0);
        const indexed = Number(body?.indexed ?? body?.Indexed ?? 0);
        const skipped = Number(body?.skipped ?? body?.Skipped ?? 0);
        const errors = Number(body?.errors ?? body?.Errors ?? 0);
        const message = body?.message || body?.Message || 'Reindex completado.';

        showDocumentsBanner('ok', `${message} Dominio: ${resultDomain} Ãƒâ€šÃ‚Â· Total: ${totalFiles} Ãƒâ€šÃ‚Â· Indexados: ${indexed} Ãƒâ€šÃ‚Â· Omitidos: ${skipped} Ãƒâ€šÃ‚Â· Errores: ${errors}`);
        showAdminToast(
            'ok',
            `Dominio ${resultDomain} Ãƒâ€šÃ‚Â· Total ${totalFiles} Ãƒâ€šÃ‚Â· Indexados ${indexed} Ãƒâ€šÃ‚Â· Omitidos ${skipped} Ãƒâ€šÃ‚Â· Errores ${errors}`,
            'Reindex completado'
        );
        await loadDocumentsAdmin(true);
    } catch (e) {
        showDocumentsBanner('err', 'Error reindexando documentos: ' + e.message);
        showAdminToast('err', String(e.message || e), 'Reindex fallÃƒÆ’Ã‚Â³', 5200);
    } finally {
        setDocumentsBusy(false, '');
    }
}

async function uploadDocumentAdmin() {
    const input = document.getElementById('fileDocumentUpload');
    const files = Array.from(input?.files || []);
    const domain = getDocumentsDomainFilter();

    if (!files.length) {
        showDocumentsBanner('warn', 'Selecciona al menos un archivo PDF antes de subirlo.');
        showAdminToast('warn', 'Selecciona al menos un PDF antes de subirlo.', 'Sin archivos');
        return;
    }

    const status = await loadDocumentsStatus();
    if (!(status?.rootExists ?? status?.RootExists)) {
        showDocumentsBanner('warn', 'No puedo subir PDFs porque no hay una carpeta documental vÃƒÆ’Ã‚Â¡lida configurada en Docs:RootPath.');
        showAdminToast('warn', 'Falta una carpeta documental vÃƒÆ’Ã‚Â¡lida antes de subir PDFs.', 'Carga no disponible');
        return;
    }

    try {
        setDocumentsBusy(true, `Subiendo PDFs al dominio ${domain || 'default'}...`);
        const form = new FormData();
        for (const file of files) {
            form.append(files.length > 1 ? 'files' : 'file', file);
        }

        const endpoint = files.length > 1 ? '/api/admin/documents/upload-bulk' : '/api/admin/documents/upload';
        const res = await fetch(endpoint, {
            method: 'POST',
            body: form
        });

        const body = await safeJson(res);
        if (!res.ok) {
            throw new Error(body?.Error || body?.Message || `HTTP ${res.status}`);
        }

        if (files.length > 1) {
            showDocumentsBanner('ok', `${body?.message || body?.Message || 'Carga masiva completada.'} Se ejecutarÃƒÆ’Ã‚Â¡ reindex para dejar todos los documentos disponibles.`);
            showAdminToast('ok', 'Carga masiva completada. Se ejecutarÃƒÆ’Ã‚Â¡ el reindex a continuaciÃƒÆ’Ã‚Â³n.', 'PDFs cargados');
        } else {
            showDocumentsBanner('ok', `${body?.message || body?.Message || 'Documento cargado.'} Se ejecutarÃƒÆ’Ã‚Â¡ reindex para dejarlo disponible.`);
            showAdminToast('ok', 'Documento cargado correctamente. Se ejecutarÃƒÆ’Ã‚Â¡ el reindex a continuaciÃƒÆ’Ã‚Â³n.', 'PDF cargado');
        }
        if (input) input.value = '';
        await reindexDocumentsAdmin();
    } catch (e) {
        showDocumentsBanner('err', 'Error subiendo documento: ' + e.message);
        showAdminToast('err', String(e.message || e), 'Carga fallÃƒÆ’Ã‚Â³', 5200);
    } finally {
        setDocumentsBusy(false, '');
    }
}

// -----------------------------------------------------------
// ML / FORECASTING TAB
// -----------------------------------------------------------
function showMlBanner(type, message) {
    const el = document.getElementById('mlBanner');
    if (!el) return;
    el.className = 'rag-banner ' + type;
    el.textContent = message;
    el.style.display = 'block';
}

function hideMlBanner() {
    const el = document.getElementById('mlBanner');
    if (!el) return;
    el.style.display = 'none';
    el.className = 'rag-banner';
    el.textContent = '';
}

function formatBytes(value) {
    const bytes = Number(value || 0);
    if (!Number.isFinite(bytes) || bytes <= 0) return '0 B';
    const units = ['B', 'KB', 'MB', 'GB'];
    const power = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
    const scaled = bytes / Math.pow(1024, power);
    return `${scaled.toFixed(power === 0 ? 0 : 1)} ${units[power]}`;
}

function renderConnectionOptionsForSelect(selectId, selected, emptyLabel) {
    const select = document.getElementById(selectId);
    if (!select) return;
    const options = [`<option value="">${escHtml(emptyLabel || '(usar conexiÃƒÆ’Ã‚Â³n operacional por defecto)')}</option>`]
        .concat((globalConnectionProfiles || []).map(profile => {
            const value = profile?.connectionName || profile?.ConnectionName || '';
            const description = profile?.description || profile?.Description || '';
            return `<option value="${escAttr(value)}">${escHtml(description ? `${value} Ãƒâ€šÃ‚Â· ${description}` : value)}</option>`;
        }));
    select.innerHTML = options.join('');
    select.value = selected || '';
}

function renderMlConnectionOptions(selected) {
    renderConnectionOptionsForSelect('selMlConnectionName', selected, '(usar conexiÃƒÆ’Ã‚Â³n operacional por defecto)');
}

function renderPredictionConnectionOptions(selected) {
    renderConnectionOptionsForSelect('selPredictionConnectionName', selected, '(usar conexiÃƒÆ’Ã‚Â³n del domain pack o default)');
}

function getDefaultPredictionDomain() {
    if (globalAdminActiveContext?.domain) return String(globalAdminActiveContext.domain).trim();
    const domainFromProfile = String(globalMlStatus?.ProfileDomain || '').trim();
    if (domainFromProfile) return domainFromProfile;
    const current = getValue('txtPredictionProfileDomain').trim();
    if (current) return current;
    const retrievalInput = document.getElementById('txtRetrievalDomain');
    const retrievalDomain = retrievalInput ? String(retrievalInput.value || '').trim() : '';
    return retrievalDomain || 'erp-kpi-pilot';
}

function getDefaultDomainPackKey(domain) {
    const normalized = String(domain || '').trim().toLowerCase();
    if (normalized.includes('northwind')) return 'northwind-sales';
    return 'industrial-kpi';
}

function getDefaultCalendarProfileKey(domain) {
    const normalized = String(domain || '').trim().toLowerCase();
    if (normalized.includes('northwind')) return 'standard-calendar';
    return 'shift-calendar';
}

function getNorthwindDailyUnitsSql() {
    return `SELECT
    CAST(od.ProductID AS nvarchar(50)) AS SeriesKey,
    CAST(o.OrderDate AS date) AS ObservedOn,
    1 AS BucketKey,
    'Daily' AS BucketLabel,
    CAST(0 AS bigint) AS BucketStartTick,
    CAST(863999999999 AS bigint) AS BucketEndTick,
    CAST(SUM(od.Quantity) AS float) AS TargetValue
FROM dbo.Orders o
JOIN dbo.OrderDetails od ON o.OrderID = od.OrderID
WHERE o.OrderDate IS NOT NULL
GROUP BY od.ProductID, CAST(o.OrderDate AS date)
ORDER BY od.ProductID, CAST(o.OrderDate AS date)`;
}

function getNorthwindMonthlySalesSql() {
    return `SELECT
    CONCAT('country:', ISNULL(o.ShipCountry, 'Unknown')) AS SeriesKey,
    DATEFROMPARTS(YEAR(o.OrderDate), MONTH(o.OrderDate), 1) AS ObservedOn,
    1 AS BucketKey,
    FORMAT(DATEFROMPARTS(YEAR(o.OrderDate), MONTH(o.OrderDate), 1), 'yyyy-MM') AS BucketLabel,
    CAST(0 AS bigint) AS BucketStartTick,
    CAST(2678399999999 AS bigint) AS BucketEndTick,
    CAST(SUM(od.Quantity) AS float) AS TargetValue
FROM dbo.Orders o
JOIN dbo.OrderDetails od ON o.OrderID = od.OrderID
WHERE o.OrderDate IS NOT NULL
GROUP BY o.ShipCountry, DATEFROMPARTS(YEAR(o.OrderDate), MONTH(o.OrderDate), 1)
ORDER BY o.ShipCountry, DATEFROMPARTS(YEAR(o.OrderDate), MONTH(o.OrderDate), 1)`;
}

function renderMlModeNotice() {
    const box = document.getElementById('mlModeNotice');
    if (!box) return;

    const mode = getValue('selMlSourceMode') || 'KpiViews';
    const connectionName = getValue('selMlConnectionName').trim();
    const looksNorthwind = connectionName.toLowerCase().includes('northwind');

    if (mode === 'CustomSql') {
        box.innerHTML = `<strong>Modo genÃƒÆ’Ã‚Â©rico activado.</strong> AquÃƒÆ’Ã‚Â­ ya no trabajamos con scrap, producciÃƒÆ’Ã‚Â³n ni downtime. El modelo se entrena desde una consulta canÃƒÆ’Ã‚Â³nica y puede apuntar a ventas, demanda, tickets o cualquier otra serie temporal siempre que devuelva <code>SeriesKey</code>, <code>ObservedOn</code>, <code>BucketKey</code>, <code>BucketLabel</code>, <code>BucketStartTick</code>, <code>BucketEndTick</code>, <code>TargetValue</code>.`;
        return;
    }

    if (looksNorthwind) {
        box.innerHTML = `<strong>AtenciÃƒÆ’Ã‚Â³n.</strong> Seleccionaste una conexiÃƒÆ’Ã‚Â³n que parece no industrial. Para Northwind o sistemas de ventas conviene cambiar a <code>CustomSql</code> o usar un <code>Prediction Profile</code> del dominio, porque <code>KpiViews</code> asume un esquema de planta con producciÃƒÆ’Ã‚Â³n, scrap, downtime y turnos.`;
        return;
    }

    box.innerHTML = `<strong>Modo industrial transicional.</strong> <code>KpiViews</code> sigue siendo ÃƒÆ’Ã‚Âºtil para plantas ERP con vistas KPI ya existentes. Si quieres un diseÃƒÆ’Ã‚Â±o mÃƒÆ’Ã‚Â¡s profesional y multi-dominio, usa <code>CustomSql</code> para la fuente del dataset y administra la semÃƒÆ’Ã‚Â¡ntica de negocio desde <code>Prediction Profiles</code>.`;
}

function toggleMlSourceModeFields() {
    const mode = getValue('selMlSourceMode') || 'KpiViews';
    const kpiWrap = document.getElementById('mlKpiConfigFields');
    const sqlWrap = document.getElementById('mlCustomSqlFields');
    if (kpiWrap) kpiWrap.style.display = mode === 'CustomSql' ? 'none' : '';
    if (sqlWrap) sqlWrap.style.display = mode === 'CustomSql' ? '' : 'none';
    renderMlModeNotice();
}

function populateMlProfileForm(body) {
    setValue('txtMlProfileName', body?.ProfileName || 'default-forecast');
    setValue('txtMlDisplayName', body?.DisplayName || 'Forecasting Profile');
    setValue('txtMlDescription', body?.Description || '');
    setValue('selMlSourceMode', body?.SourceMode || 'KpiViews');
    renderMlConnectionOptions(body?.ConnectionName || '');
    setValue('txtMlShiftTableName', body?.ShiftTableName || 'dbo.Turnos');
    setValue('txtMlProductionView', body?.ProductionViewName || '');
    setValue('txtMlScrapView', body?.ScrapViewName || '');
    setValue('txtMlDowntimeView', body?.DowntimeViewName || '');
    setValue('txtMlTrainingSql', body?.TrainingSql || '');
    toggleMlSourceModeFields();
}

function applyMlSourcePreset(presetKey) {
    if (presetKey === 'industrial') {
        setValue('selMlSourceMode', 'KpiViews');
        if (!getValue('txtMlProfileName').trim()) setValue('txtMlProfileName', 'industrial-forecast');
        if (!getValue('txtMlDisplayName').trim()) setValue('txtMlDisplayName', 'Industrial KPI Forecast');
        if (!getValue('txtMlDescription').trim()) setValue('txtMlDescription', 'Perfil transicional para forecasting industrial por turnos usando vistas KPI.');
        toggleMlSourceModeFields();
        return;
    }

    setValue('selMlSourceMode', 'CustomSql');
    setValue('selMlConnectionName', 'NorthwindDb');

    if (presetKey === 'northwind-daily-units') {
        setValue('txtMlProfileName', 'northwind-daily-units');
        setValue('txtMlDisplayName', 'Northwind Daily Units Forecast');
        setValue('txtMlDescription', 'Perfil genÃƒÆ’Ã‚Â©rico para forecast de unidades vendidas por producto y por dÃƒÆ’Ã‚Â­a en Northwind.');
        setValue('txtMlTrainingSql', getNorthwindDailyUnitsSql());
    } else if (presetKey === 'northwind-monthly-sales') {
        setValue('txtMlProfileName', 'northwind-monthly-units');
        setValue('txtMlDisplayName', 'Northwind Monthly Units Forecast');
        setValue('txtMlDescription', 'Perfil genÃƒÆ’Ã‚Â©rico para forecast de unidades mensuales por paÃƒÆ’Ã‚Â­s en Northwind.');
        setValue('txtMlTrainingSql', getNorthwindMonthlySalesSql());
    }

    toggleMlSourceModeFields();
}

function renderMlStatus() {
    const body = globalMlStatus || {};
    const modelExists = !!body?.ModelExists;
    const connReady = !!body?.OperationalConnectionReady;
    const aligned = !!body?.ModelAlignedWithProfile;
    const sourceMode = body?.SourceMode || 'KpiViews';

    setText('mlStatusBadge', modelExists ? 'READY' : 'EMPTY');
    setText('txtMlStatusSummary', modelExists
        ? 'Hay un modelo serializado disponible para predicciÃƒÆ’Ã‚Â³n.'
        : 'TodavÃƒÆ’Ã‚Â­a no existe un modelo entrenado o no se pudo encontrar el archivo.');
    setText('txtMlModelExists', modelExists ? 'Disponible' : 'No existe');
    setText('txtMlModelUpdated', `ÃƒÆ’Ã…Â¡ltima actualizaciÃƒÆ’Ã‚Â³n: ${body?.ModelLastWriteUtc ? formatAdminDateTime(body.ModelLastWriteUtc) : 'ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â'}`);
    setText('txtMlModelSize', formatBytes(body?.ModelSizeBytes));
    setText('txtMlConnectionReady', connReady ? 'Lista' : 'Falta');
    setText('txtMlModelPath', body?.ModelPath || 'Sin ruta configurada');
    setText('txtMlModelDirectory', `Directorio de modelos: ${body?.ModelDirectory || 'ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â'}`);
    populateMlProfileForm(body);

    const list = document.getElementById('mlStatusList');
    if (!list) return;

    list.innerHTML = `
        <div class="document-item">
            <div class="document-item-head">
                <div>
                    <div class="document-item-title">Modelo de forecasting</div>
                    <div class="document-item-sub">${escHtml(body?.ModelPath || 'Sin ruta')}</div>
                </div>
                <span class="hi-verify ${modelExists ? 'verified' : 'pending'}">${modelExists ? 'READY' : 'EMPTY'}</span>
            </div>
            <div class="document-item-meta">
                <span class="meta-chip ${connReady ? 'status-ok' : 'verify-pending'}">${connReady ? 'ConexiÃƒÆ’Ã‚Â³n operacional lista' : 'Sin conexiÃƒÆ’Ã‚Â³n operacional'}</span>
                <span class="meta-chip ${aligned ? 'verified' : 'verify-pending'}">${aligned ? 'Modelo alineado con perfil' : 'Modelo desalineado'}</span>
                <span class="meta-chip training-no">${escHtml(formatBytes(body?.ModelSizeBytes))}</span>
                <span class="meta-chip training-no">${escHtml(body?.ModelLastWriteUtc ? formatAdminDateTime(body.ModelLastWriteUtc) : 'Sin entrenamiento')}</span>
            </div>
        </div>
        <div class="document-item">
            <div class="document-item-head">
                <div>
                    <div class="document-item-title">${escHtml(body?.DisplayName || 'Perfil ML')}</div>
                    <div class="document-item-sub">${sourceMode === 'CustomSql' ? 'Fuente canÃƒÆ’Ã‚Â³nica por SQL custom.' : 'Fuente clÃƒÆ’Ã‚Â¡sica por vistas KPI y tabla de turnos.'}</div>
                </div>
            </div>
            <div class="document-item-meta">
                <span class="meta-chip training-no">Modo: ${escHtml(sourceMode)}</span>
                <span class="meta-chip training-no">ConexiÃƒÆ’Ã‚Â³n: ${escHtml(body?.ConnectionName || 'default operacional')}</span>
                <span class="meta-chip training-no">${escHtml(sourceMode === 'CustomSql' ? 'Custom SQL canÃƒÆ’Ã‚Â³nico' : `ShiftTable: ${body?.ShiftTableName || 'ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â'}`)}</span>
            </div>
        </div>`;
}

function showPredictionProfileBanner(kind, message) {
    const el = document.getElementById('predictionProfileBanner');
    if (!el) return;
    el.className = `alert-banner ${kind}`;
    el.style.display = 'block';
    el.textContent = message;
}

function hidePredictionProfileBanner() {
    const el = document.getElementById('predictionProfileBanner');
    if (!el) return;
    el.style.display = 'none';
    el.textContent = '';
}

function normalizeJsonField(value) {
    const raw = String(value || '').trim();
    return raw || null;
}

function parseStringArrayField(value) {
    const raw = String(value || '').trim();
    if (!raw) return [];

    try {
        const parsed = JSON.parse(raw);
        if (Array.isArray(parsed)) {
            return parsed
                .map(x => String(x || '').trim())
                .filter(Boolean);
        }
    } catch {
        // fallback abajo
    }

    return raw
        .split(',')
        .map(x => x.trim())
        .filter(Boolean);
}

function toJsonArrayText(values) {
    const normalized = (values || []).map(x => String(x || '').trim()).filter(Boolean);
    return normalized.length ? JSON.stringify(normalized) : '';
}

function getPredictionPackMetrics() {
    const pack = globalPredictionDomainPack;
    return Array.isArray(pack?.metrics || pack?.Metrics) ? (pack.metrics || pack.Metrics) : [];
}

function getPredictionPackDimensions() {
    const pack = globalPredictionDomainPack;
    return Array.isArray(pack?.dimensions || pack?.Dimensions) ? (pack.dimensions || pack.Dimensions) : [];
}

function renderPredictionGuidedSelector(hostId, items, selectedValues, clickHandlerName) {
    const host = document.getElementById(hostId);
    if (!host) return;

    if (!items.length) {
        host.innerHTML = '<span class="meta-empty">No hay opciones definidas en el Domain Pack.</span>';
        return;
    }

    const selectedSet = new Set((selectedValues || []).map(x => String(x || '').trim().toLowerCase()));
    host.innerHTML = items.map(item => {
        const key = String(item?.key || item?.Key || '').trim();
        const display = String(item?.displayName || item?.DisplayName || key).trim();
        const selected = selectedSet.has(key.toLowerCase()) ? ' is-selected' : '';
        const desc = String(item?.description || item?.Description || '').trim();
        return `<button class="suggestion-chip prediction-selector-chip${selected}" type="button" title="${escAttr(desc || display)}" onclick="${clickHandlerName}('${jsString(key)}')">${escHtml(display)}</button>`;
    }).join('');
}

function renderPredictionGuidedSelectors() {
    const selectedTargetMetric = String(getValue('txtPredictionTargetMetricKey') || '').trim();
    const selectedMetrics = parseStringArrayField(getValue('txtPredictionFeatureSourcesJson'));
    const selectedDimensions = parseStringArrayField(getValue('txtPredictionGroupByJson'));
    renderPredictionGuidedSelector(
        'predictionTargetMetricSelector',
        getPredictionPackMetrics(),
        selectedTargetMetric ? [selectedTargetMetric] : [],
        'selectPredictionTargetMetric');
    renderPredictionGuidedSelector('predictionFeatureSourceSelector', getPredictionPackMetrics(), selectedMetrics, 'togglePredictionFeatureSource');
    renderPredictionGuidedSelector('predictionGroupBySelector', getPredictionPackDimensions(), selectedDimensions, 'togglePredictionGroupBy');
}

function selectPredictionTargetMetric(value) {
    setValue('txtPredictionTargetMetricKey', String(value || '').trim());
    renderPredictionGuidedSelectors();
}

function togglePredictionArrayField(fieldId, value) {
    const current = parseStringArrayField(getValue(fieldId));
    const normalized = String(value || '').trim();
    if (!normalized) return;

    const set = new Set(current.map(x => x.toLowerCase()));
    if (set.has(normalized.toLowerCase())) {
        const next = current.filter(x => String(x || '').trim().toLowerCase() !== normalized.toLowerCase());
        setValue(fieldId, toJsonArrayText(next));
    } else {
        current.push(normalized);
        setValue(fieldId, toJsonArrayText(current));
    }

    renderPredictionGuidedSelectors();
}

function togglePredictionFeatureSource(value) {
    togglePredictionArrayField('txtPredictionFeatureSourcesJson', value);
}

function togglePredictionGroupBy(value) {
    togglePredictionArrayField('txtPredictionGroupByJson', value);
}

function renderPredictionDomainPackSummary() {
    const host = document.getElementById('predictionDomainPackSummary');
    if (!host) return;

    const pack = globalPredictionDomainPack;
    if (!pack) {
        host.innerHTML = '<strong>Sin Domain Pack.</strong> No se pudo resolver el vocabulario analÃƒÆ’Ã‚Â­tico para este dominio.';
        renderPredictionGuidedSelectors();
        return;
    }

    const metrics = Array.isArray(pack.metrics || pack.Metrics) ? (pack.metrics || pack.Metrics) : [];
    const dimensions = Array.isArray(pack.dimensions || pack.Dimensions) ? (pack.dimensions || pack.Dimensions) : [];
    const metricText = metrics.length ? metrics.map(x => x.key || x.Key).filter(Boolean).join(', ') : 'sin mÃƒÆ’Ã‚Â©tricas definidas';
    const dimensionText = dimensions.length ? dimensions.map(x => x.key || x.Key).filter(Boolean).join(', ') : 'sin dimensiones definidas';

    host.innerHTML = `<strong>${escHtml(pack.displayName || pack.DisplayName || pack.key || pack.Key || 'Domain Pack')}</strong><br>MÃƒÆ’Ã‚Â©tricas: ${escHtml(metricText)}<br>Dimensiones: ${escHtml(dimensionText)}`;
    renderPredictionGuidedSelectors();
}

function renderPredictionProfileList() {
    const list = document.getElementById('predictionProfileList');
    if (!list) return;

    if (!globalPredictionProfiles.length) {
        list.innerHTML = `<div class="empty-state" style="min-height:180px">No hay Prediction Profiles para este dominio todavÃƒÆ’Ã‚Â­a.</div>`;
        return;
    }

    list.innerHTML = globalPredictionProfiles.map(profile => {
        const id = Number(profile.id || profile.Id || 0);
        const selected = id === selectedPredictionProfileId ? ' is-selected' : '';
        const sourceMode = profile.sourceMode || profile.SourceMode || 'KpiViews';
        const connection = profile.connectionName || profile.ConnectionName || 'default';
        const horizon = `${profile.horizon || profile.Horizon || 1} ${profile.horizonUnit || profile.HorizonUnit || 'day'}`;
        return `
            <div class="prediction-profile-card${selected}" onclick="selectPredictionProfileAdmin(${id})">
                <div class="prediction-profile-title">${escHtml(profile.displayName || profile.DisplayName || profile.profileKey || profile.ProfileKey || 'Prediction Profile')}</div>
                <div class="prediction-profile-sub">${escHtml(profile.profileKey || profile.ProfileKey || '')}</div>
                <div class="prediction-profile-meta">
                    <span class="meta-chip training-no">${escHtml(profile.targetMetricKey || profile.TargetMetricKey || 'ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â')}</span>
                    <span class="meta-chip training-no">${escHtml(sourceMode)}</span>
                    <span class="meta-chip training-no">${escHtml(horizon)}</span>
                    <span class="meta-chip ${profile.isActive || profile.IsActive ? 'status-ok' : 'verify-pending'}">${profile.isActive || profile.IsActive ? 'Activo' : 'Inactivo'}</span>
                </div>
                <div class="prediction-profile-sub">${escHtml(`ConexiÃƒÆ’Ã‚Â³n: ${connection}`)}</div>
            </div>`;
    }).join('');
}

function populatePredictionProfileForm(profile) {
    const domain = profile?.domain || profile?.Domain || getDefaultPredictionDomain();
    setValue('txtPredictionProfileDomain', domain);
    setValue('txtPredictionProfileKey', profile?.profileKey || profile?.ProfileKey || '');
    setValue('txtPredictionProfileDisplayName', profile?.displayName || profile?.DisplayName || '');
    setValue('txtPredictionDomainPackKey', profile?.domainPackKey || profile?.DomainPackKey || getDefaultDomainPackKey(domain));
    setValue('txtPredictionTargetMetricKey', profile?.targetMetricKey || profile?.TargetMetricKey || '');
    setValue('txtPredictionCalendarProfileKey', profile?.calendarProfileKey || profile?.CalendarProfileKey || getDefaultCalendarProfileKey(domain));
    setValue('selPredictionConnectionName', profile?.connectionName || profile?.ConnectionName || '');
    setValue('selPredictionSourceMode', profile?.sourceMode || profile?.SourceMode || 'CustomSql');
    setValue('txtPredictionModelType', profile?.modelType || profile?.ModelType || 'FastTree');
    setValue('txtPredictionGrain', profile?.grain || profile?.Grain || 'day');
    setValue('txtPredictionHorizon', String(profile?.horizon || profile?.Horizon || 7));
    setValue('txtPredictionHorizonUnit', profile?.horizonUnit || profile?.HorizonUnit || 'day');
    setValue('txtPredictionTargetSeriesSource', profile?.targetSeriesSource || profile?.TargetSeriesSource || '');
    setValue('txtPredictionFeatureSourcesJson', profile?.featureSourcesJson || profile?.FeatureSourcesJson || '');
    setValue('txtPredictionGroupByJson', profile?.groupByJson || profile?.GroupByJson || '');
    setValue('txtPredictionFiltersJson', profile?.filtersJson || profile?.FiltersJson || '');
    setValue('txtPredictionNotes', profile?.notes || profile?.Notes || '');
    const checkbox = document.getElementById('chkPredictionProfileIsActive');
    if (checkbox) checkbox.checked = !!(profile ? (profile.isActive ?? profile.IsActive ?? true) : true);
    renderPredictionGuidedSelectors();
}

function buildPredictionProfileDraft(domain) {
    const normalizedDomain = String(domain || getDefaultPredictionDomain()).trim() || 'erp-kpi-pilot';
    const northwind = normalizedDomain.toLowerCase().includes('northwind');
    return {
        Id: 0,
        Domain: normalizedDomain,
        ProfileKey: northwind ? 'northwind-sales-daily-units' : `${normalizedDomain.replace(/[^a-z0-9]+/gi, '-').toLowerCase()}-forecast`,
        DisplayName: northwind ? 'Northwind Sales Daily Units Forecast' : 'Prediction Profile',
        DomainPackKey: getDefaultDomainPackKey(normalizedDomain),
        TargetMetricKey: northwind ? 'units_sold' : 'scrap_qty',
        CalendarProfileKey: getDefaultCalendarProfileKey(normalizedDomain),
        Grain: northwind ? 'day' : 'shift',
        Horizon: northwind ? 7 : 1,
        HorizonUnit: northwind ? 'day' : 'shift',
        ModelType: 'FastTree',
        ConnectionName: northwind ? 'NorthwindDb' : '',
        SourceMode: northwind ? 'CustomSql' : 'KpiViews',
        TargetSeriesSource: northwind ? getNorthwindDailyUnitsSql() : 'ml:active-profile',
        FeatureSourcesJson: northwind ? '["net_sales","order_count"]' : '["produced_qty","downtime_minutes"]',
        GroupByJson: northwind ? '["product"]' : '["part","shift"]',
        FiltersJson: '',
        Notes: northwind
            ? 'Perfil inicial para forecast de unidades vendidas por producto y por dÃƒÆ’Ã‚Â­a.'
            : 'Perfil inicial para forecasting industrial por turno.',
        IsActive: true
    };
}

function selectPredictionProfileAdmin(id) {
    selectedPredictionProfileId = Number(id || 0);
    const profile = globalPredictionProfiles.find(x => Number(x.id || x.Id || 0) === selectedPredictionProfileId) || null;
    populatePredictionProfileForm(profile || buildPredictionProfileDraft(getDefaultPredictionDomain()));
    renderPredictionProfileList();
}

function newPredictionProfileAdmin() {
    selectedPredictionProfileId = 0;
    populatePredictionProfileForm(buildPredictionProfileDraft(getDefaultPredictionDomain()));
    renderPredictionProfileList();
    hidePredictionProfileBanner();
}

async function loadPredictionProfilesAdmin() {
    const domain = getDefaultPredictionDomain();
    setValue('txtPredictionProfileDomain', domain);

    try {
        const [profilesRes, packRes] = await Promise.all([
            fetch(`/api/admin/prediction-profiles?domain=${encodeURIComponent(domain)}`),
            fetch(`/api/admin/domain-pack-preview?domain=${encodeURIComponent(domain)}`)
        ]);

        const profilesBody = await safeJson(profilesRes);
        const packBody = await safeJson(packRes);

        if (!profilesRes.ok) throw new Error(profilesBody?.Error || `HTTP ${profilesRes.status}`);
        if (!packRes.ok) throw new Error(packBody?.Error || `HTTP ${packRes.status}`);

        globalPredictionProfiles = Array.isArray(profilesBody) ? profilesBody : [];
        globalPredictionDomainPack = packBody || null;
        renderPredictionConnectionOptions('');
        renderPredictionDomainPackSummary();

        if (selectedPredictionProfileId) {
            const selected = globalPredictionProfiles.find(x => Number(x.id || x.Id || 0) === selectedPredictionProfileId);
            if (selected) {
                populatePredictionProfileForm(selected);
            } else {
                newPredictionProfileAdmin();
            }
        } else if (globalPredictionProfiles.length) {
            const selected = globalPredictionProfiles.find(x => x.isActive || x.IsActive) || globalPredictionProfiles[0];
            selectedPredictionProfileId = Number(selected.id || selected.Id || 0);
            populatePredictionProfileForm(selected);
        } else {
            newPredictionProfileAdmin();
        }

        renderPredictionProfileList();
    } catch (e) {
        globalPredictionProfiles = [];
        globalPredictionDomainPack = null;
        renderPredictionProfileList();
        renderPredictionDomainPackSummary();
        renderPredictionGuidedSelectors();
        showPredictionProfileBanner('err', 'Error cargando Prediction Profiles: ' + e.message);
    }
}

async function refreshPredictionProfilesAdmin() {
    hidePredictionProfileBanner();
    selectedPredictionProfileId = 0;
    await loadPredictionProfilesAdmin();
}

async function savePredictionProfileAdmin() {
    const domain = getValue('txtPredictionProfileDomain').trim() || getDefaultPredictionDomain();
    const payload = {
        Id: selectedPredictionProfileId || 0,
        Domain: domain,
        ProfileKey: getValue('txtPredictionProfileKey').trim(),
        DisplayName: getValue('txtPredictionProfileDisplayName').trim(),
        DomainPackKey: getValue('txtPredictionDomainPackKey').trim(),
        TargetMetricKey: getValue('txtPredictionTargetMetricKey').trim(),
        CalendarProfileKey: getValue('txtPredictionCalendarProfileKey').trim(),
        Grain: getValue('txtPredictionGrain').trim(),
        Horizon: Number(getValue('txtPredictionHorizon')) || 1,
        HorizonUnit: getValue('txtPredictionHorizonUnit').trim(),
        ModelType: getValue('txtPredictionModelType').trim(),
        ConnectionName: getValue('selPredictionConnectionName').trim(),
        SourceMode: getValue('selPredictionSourceMode').trim(),
        TargetSeriesSource: getValue('txtPredictionTargetSeriesSource').trim(),
        FeatureSourcesJson: normalizeJsonField(getValue('txtPredictionFeatureSourcesJson')),
        GroupByJson: normalizeJsonField(getValue('txtPredictionGroupByJson')),
        FiltersJson: normalizeJsonField(getValue('txtPredictionFiltersJson')),
        Notes: normalizeJsonField(getValue('txtPredictionNotes')),
        IsActive: !!document.getElementById('chkPredictionProfileIsActive')?.checked
    };

    try {
        const res = await fetch('/api/admin/prediction-profiles', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const body = await safeJson(res);
        if (!res.ok) {
            throw new Error(body?.Error || body?.Message || `HTTP ${res.status}`);
        }

        showPredictionProfileBanner('ok', body?.Message || 'Prediction Profile guardado correctamente.');
        selectedPredictionProfileId = Number(body?.Id || 0);
        await loadPredictionProfilesAdmin();
    } catch (e) {
        showPredictionProfileBanner('err', 'Error guardando Prediction Profile: ' + e.message);
    }
}

async function loadMlAdmin() {
    const list = document.getElementById('mlStatusList');
    if (list) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <polyline points="22 7 13.5 15.5 8.5 10.5 2 17"></polyline>
                    <polyline points="16 7 22 7 22 13"></polyline>
                </svg>
                Cargando estado de ML...
            </div>`;
    }

    try {
        const res = await fetch('/api/admin/ml/status');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        globalMlStatus = await res.json();
        renderMlStatus();
        await loadPredictionProfilesAdmin();
        if (globalMlStatus?.ConnectionError) {
            showMlBanner('warn', `Perfil ML cargado, pero la conexiÃƒÆ’Ã‚Â³n no estÃƒÆ’Ã‚Â¡ lista: ${globalMlStatus.ConnectionError}`);
        } else {
            hideMlBanner();
        }
    } catch (e) {
        globalMlStatus = null;
        renderMlStatus();
        await loadPredictionProfilesAdmin();
        showMlBanner('err', 'Error cargando estado ML: ' + e.message);
    }
}

async function saveMlProfileAdmin() {
    const payload = {
        ProfileName: getValue('txtMlProfileName').trim(),
        DisplayName: getValue('txtMlDisplayName').trim(),
        SourceMode: getValue('selMlSourceMode').trim() || 'KpiViews',
        ConnectionName: getValue('selMlConnectionName').trim(),
        Description: getValue('txtMlDescription').trim(),
        ShiftTableName: getValue('txtMlShiftTableName').trim(),
        ProductionViewName: getValue('txtMlProductionView').trim(),
        ScrapViewName: getValue('txtMlScrapView').trim(),
        DowntimeViewName: getValue('txtMlDowntimeView').trim(),
        TrainingSql: getValue('txtMlTrainingSql').trim()
    };

    try {
        const res = await fetch('/api/admin/ml/profile', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const body = await safeJson(res);
        if (!res.ok) {
            throw new Error(body?.Error || body?.Message || `HTTP ${res.status}`);
        }

        showMlBanner('ok', body?.Message || 'Perfil ML guardado correctamente.');
        await loadMlAdmin();
    } catch (e) {
        showMlBanner('err', 'Error guardando perfil ML: ' + e.message);
    }
}

async function trainMlModelAdmin() {
    const spinner = document.getElementById('mlTrainSpinner');
    if (spinner) spinner.style.display = 'block';

    try {
        const res = await fetch('/api/admin/ml/train', { method: 'POST' });
        const body = await safeJson(res);
        if (!res.ok) {
            throw new Error(body?.Error || body?.Message || `HTTP ${res.status}`);
        }

        globalMlStatus = body;
        renderMlStatus();
        showMlBanner('ok', `${body?.Message || 'Entrenamiento completado.'} Modelo: ${body?.ModelExists ? 'disponible' : 'sin archivo'} Ãƒâ€šÃ‚Â· Actualizado: ${body?.ModelLastWriteUtc ? formatAdminDateTime(body.ModelLastWriteUtc) : 'ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â'}`);
        await loadMlAdmin();
    } catch (e) {
        showMlBanner('err', 'Error entrenando modelo ML: ' + e.message);
    } finally {
        if (spinner) spinner.style.display = 'none';
    }
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
    try {
        await loadSystemConfig();
    } catch (error) {
        console.error('Admin init failed while loading system config.', error);
    }

    try {
        await loadOnboardingBootstrap();
    } catch (error) {
        console.error('Admin init failed while loading onboarding bootstrap.', error);
        setOnboardingSidebarStatus?.('err', `No pudimos cargar el bootstrap del onboarding. ${error?.message || error}`);
    }

    try {
        await ensureAdminSignalR();
    } catch (error) {
        console.error('Admin init failed while starting SignalR.', error);
    }

    try {
        loadHistory();
    } catch (error) {
        console.error('Admin init failed while loading RAG history.', error);
    }

    try {
        loadProfiles();
    } catch (error) {
        console.error('Admin init failed while loading hardware profiles.', error);
    }

    resetSystemConfigForm();
    resetAllowedObjectForm();
    resetBusinessRuleForm();
    resetSemanticHintForm();
    resetQueryPatternForm();

    try {
        await setAdminActiveContext(buildAdminContextFromOnboardingForm(), { reloadCurrentTab: false });
    } catch (error) {
        console.error('Admin init failed while applying the active onboarding context.', error);
    }

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

    ['txtOnboardingConnServer', 'txtOnboardingConnPort', 'txtOnboardingConnDatabase', 'txtOnboardingConnAuthMode', 'txtOnboardingConnUser', 'txtOnboardingConnPassword', 'txtOnboardingConnEncrypt', 'txtOnboardingConnTrustCert', 'txtOnboardingConnectionMode']
        .forEach(id => {
            const el = document.getElementById(id);
            if (!el) return;
            el.addEventListener('input', () => {
                if (id === 'txtOnboardingConnAuthMode') {
                    handleOnboardingAuthModeChange();
                    return;
                }
                if (id === 'txtOnboardingConnectionMode') {
                    handleOnboardingConnectionModeChange();
                    return;
                }
                buildOnboardingConnectionString();
            });
            el.addEventListener('change', () => {
                if (id === 'txtOnboardingConnAuthMode') {
                    handleOnboardingAuthModeChange();
                    return;
                }
                if (id === 'txtOnboardingConnectionMode') {
                    handleOnboardingConnectionModeChange();
                    return;
                }
                buildOnboardingConnectionString();
            });
        });

    const validationQuestion = document.getElementById('txtOnboardingValidationQuestion');
    if (validationQuestion) {
        validationQuestion.addEventListener('input', () => clearOnboardingFieldError('validationQuestion'));
        validationQuestion.addEventListener('change', () => clearOnboardingFieldError('validationQuestion'));
    }

    const documentsDomainFilter = document.getElementById('txtDocumentDomainFilter');
    if (documentsDomainFilter) {
        documentsDomainFilter.addEventListener('change', () => loadDocumentsAdmin());
    }

    const predictionProfileDomain = document.getElementById('txtPredictionProfileDomain');
    if (predictionProfileDomain) {
        predictionProfileDomain.addEventListener('change', () => refreshPredictionProfilesAdmin());
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


// -----------------------------------------------------------
// ONBOARDING UX REFACTOR ENHANCEMENTS
// -----------------------------------------------------------
let onboardingAdvancedAreaOpen = false;

function ensureOnboardingStepGoal(panelId, copy) {
    const panel = document.getElementById(panelId);
    if (!panel || !copy) return;
    if (panel.querySelector('.step-panel-goal')) return;
    const header = panel.querySelector('.step-panel-header');
    if (!header || !header.parentElement) return;
    const goal = document.createElement('div');
    goal.className = 'step-panel-goal';
    goal.textContent = copy;
    header.insertAdjacentElement('afterend', goal);
}

function ensureOnboardingSchemaAssistUI() {
    const panel = document.getElementById('onboardingStepPanel2b');
    if (!panel) return;

    ensureOnboardingStepGoal('onboardingStepPanel2b', 'Exito de este paso: dejas un perimetro de consulta claro y evitas exponer objetos innecesarios.');

    const legacyActions = panel.querySelector('.inline-actions');
    if (legacyActions) {
        legacyActions.classList.add('onboarding-legacy-actions');
        const discoverBtn = legacyActions.querySelector('button');
        if (discoverBtn && !discoverBtn.id) {
            discoverBtn.id = 'btnOnboardingDiscoverSchema';
        }
    }

    if (!panel.querySelector('.onboarding-step-intro')) {
        const intro = document.createElement('div');
        intro.className = 'onboarding-step-intro';
        intro.textContent = 'Elige solo lo necesario para responder el piloto. Prioriza objetos recomendados y evita abrir el dominio mas de lo necesario.';
        legacyActions?.insertAdjacentElement('beforebegin', intro);
    }

    if (!panel.querySelector('.onboarding-schema-toolbar')) {
        const toolbar = document.createElement('div');
        toolbar.className = 'onboarding-schema-toolbar';
        toolbar.innerHTML = `
            <div class="onboarding-schema-toolbar-primary">
                <label class="field-label onboarding-toolbar-label" for="txtOnboardingSchemaSearch">Buscar en el schema</label>
                <input type="text" id="txtOnboardingSchemaSearch" class="field-input onboarding-schema-search" placeholder="Buscar por schema, tabla, vista o descripcion" oninput="handleOnboardingSchemaFilterChange()" />
            </div>
            <div class="onboarding-schema-toolbar-filters">
                <select id="selOnboardingSchemaType" class="field-input onboarding-filter-select" onchange="handleOnboardingSchemaFilterChange()">
                    <option value="all">Todos los tipos</option>
                    <option value="table">Solo tablas</option>
                    <option value="view">Solo vistas</option>
                </select>
                <select id="selOnboardingSchemaAssist" class="field-input onboarding-filter-select" onchange="handleOnboardingSchemaFilterChange()">
                    <option value="all">Todas</option>
                    <option value="recommended">Recomendadas</option>
                    <option value="selected">Ya seleccionadas</option>
                    <option value="risky">Revisar</option>
                </select>
            </div>
            <div class="onboarding-schema-toolbar-status">
                <span class="meta-chip training-no" id="txtOnboardingSchemaSelectionCount">0 seleccionadas</span>
                <span class="meta-chip training-no" id="txtOnboardingSchemaVisibleCount">Sin schema</span>
            </div>`;
        if (legacyActions?.firstElementChild) {
            toolbar.appendChild(legacyActions.firstElementChild);
        }
        const meta = document.getElementById('onboardingSchemaMeta');
        meta?.insertAdjacentElement('beforebegin', toolbar);
    }

    panel.querySelector('.onboarding-schema-helper-grid')?.remove();
}

function ensureOnboardingPreparationUI() {
    const panel = document.getElementById('onboardingStepPanel3');
    if (!panel) return;
    ensureOnboardingStepGoal('onboardingStepPanel3', 'Exito de este paso: el sistema deja indexado el schema, genera contexto tecnico y crea hints base para arrancar con seguridad.');

    panel.querySelector('.onboarding-process-grid')?.remove();

    if (!document.getElementById('txtOnboardingPreparationNarrative')) {
        const statusGrid = panel.querySelector('.onboarding-status-grid');
        if (statusGrid) {
            const note = document.createElement('div');
            note.className = 'onboarding-preparation-summary';
            note.id = 'txtOnboardingPreparationNarrative';
            note.textContent = 'Este paso automatiza schema docs, contexto minimo e hints base. Cuando termine, aqui veras que quedo listo.';
            statusGrid.insertAdjacentElement('afterend', note);
        }
    }
}

function ensureOnboardingValidationUI() {
    const panel = document.getElementById('onboardingStepPanel4');
    if (!panel) return;
    ensureOnboardingStepGoal('onboardingStepPanel4', 'Exito de este paso: ya puedes confiar en que el dominio responde una pregunta real con el pipeline SQL del producto.');

    const firstFieldGroup = panel.querySelector('.field-group');
    if (firstFieldGroup && !panel.querySelector('.onboarding-validation-intro')) {
        const intro = document.createElement('div');
        intro.className = 'onboarding-validation-intro';
        intro.innerHTML = `
            <div class="onboarding-inline-hint-card success-tone">
                <div class="subpanel-title">Criterio de exito</div>
                <div class="subpanel-desc">La prueba cierra bien si el SQL usa objetos permitidos, el resultado tiene sentido y puedes confiar en esa primera respuesta.</div>
            </div>`;
        firstFieldGroup.insertAdjacentElement('beforebegin', intro);
    }

    panel.querySelector('.onboarding-validation-overview')?.remove();

    const questionInput = document.getElementById('txtOnboardingValidationQuestion');
    questionInput?.classList.add('onboarding-validation-input');
}

function ensureOnboardingReadinessAdvancedUI() {
    const panel = document.getElementById('onboardingStepPanel5');
    if (!panel) return;

    if (!document.getElementById('txtOnboardingFinalRecommendation')) {
        const meta = document.getElementById('onboardingReadinessMeta');
        if (meta) {
            const card = document.createElement('div');
            card.className = 'onboarding-inline-hint-card success-tone onboarding-final-recommendation';
            card.innerHTML = '<div class="subpanel-title">Siguiente accion recomendada</div><div class="subpanel-desc" id="txtOnboardingFinalRecommendation">Completa primero el onboarding base. La afinacion avanzada viene despues.</div>';
            meta.insertAdjacentElement('afterend', card);
        }
    }

    if (!document.getElementById('onboardingExpertTools')) {
        const advancedHost = document.createElement('div');
        advancedHost.className = 'subpanel onboarding-advanced-hub';
        advancedHost.innerHTML = `
            <div class="onboarding-advanced-hub-head">
                <div>
                    <div class="subpanel-title">Ajustes avanzados</div>
                    <div class="subpanel-desc">Exporta el pack o mueve el dominio a otro ambiente solo cuando el flujo base ya quedo validado.</div>
                </div>
                <button class="btn btn-ghost" type="button" id="btnToggleOnboardingAdvancedArea" onclick="toggleOnboardingAdvancedArea()" disabled>Disponible al cerrar onboarding</button>
            </div>
            <div id="onboardingExpertTools" style="display:none"></div>`;
        const packField = document.getElementById('txtOnboardingDomainPackJson')?.closest('.field-group');
        const packMeta = document.getElementById('onboardingPackMeta');
        const packBanner = document.getElementById('onboardingPackBanner');
        panel.appendChild(advancedHost);
        const target = document.getElementById('onboardingExpertTools');
        packField && target?.appendChild(packField);
        packMeta && target?.appendChild(packMeta);
        packBanner && target?.appendChild(packBanner);
    }

    const target = document.getElementById('onboardingExpertTools');
    const footer = document.querySelector('#pane-onboarding .editor-actions');
    const newWorkspace = footer?.querySelector('button[onclick="startNewOnboardingWorkspace()"]');
    const exportPack = document.getElementById('btnOnboardingExportPack');
    const importPack = document.getElementById('btnOnboardingImportPack');
    if (target && !target.querySelector('.onboarding-advanced-actions')) {
        const actions = document.createElement('div');
        actions.className = 'onboarding-advanced-actions';
        newWorkspace && actions.appendChild(newWorkspace);
        exportPack && actions.appendChild(exportPack);
        importPack && actions.appendChild(importPack);
        target.appendChild(actions);
    }
}

function toggleOnboardingAdvancedArea(forceOpen = null) {
    const panel = document.getElementById('onboardingExpertTools');
    const buttons = [
        document.getElementById('btnToggleOnboardingAdvancedArea'),
        document.getElementById('btnToggleOnboardingAdvancedAreaFooter'),
        document.getElementById('btnToggleOnboardingAdvancedAreaBottom')
    ].filter(Boolean);
    if (!panel) return;
    if (buttons[0]?.disabled && forceOpen !== false) return;
    onboardingAdvancedAreaOpen = forceOpen === null ? panel.style.display === 'none' : !!forceOpen;
    panel.style.display = onboardingAdvancedAreaOpen ? 'block' : 'none';
    buttons.forEach(button => {
        if (button.disabled) {
            button.textContent = 'Disponible al cerrar onboarding';
            return;
        }
        button.textContent = onboardingAdvancedAreaOpen ? 'Ocultar ajustes avanzados' : 'Mostrar ajustes avanzados';
    });
}

function handleOnboardingSchemaFilterChange() {
    renderOnboardingSchemaCandidates();
}

function normalizeOnboardingObjectType(value) {
    const normalized = String(value || '').trim().toLowerCase();
    if (normalized.includes('view')) return 'view';
    if (normalized.includes('table')) return 'table';
    return normalized || 'other';
}

function isRiskyOnboardingCandidate(item) {
    const columnCount = item.columnCount ?? item.ColumnCount ?? 0;
    const pkCount = item.primaryKeyCount ?? item.PrimaryKeyCount ?? 0;
    const fkCount = item.foreignKeyCount ?? item.ForeignKeyCount ?? 0;
    return columnCount >= 20 && pkCount === 0 && fkCount === 0;
}

function extractOnboardingSqlTablesUsed(sqlText) {
    const sql = String(sqlText || '');
    if (!sql.trim()) return [];
    const matches = [...sql.matchAll(/\b(?:from|join)\s+([\[\]\w\.]+)/gi)];
    return [...new Set(matches.map(match => String(match[1] || '').replace(/[\[\]]/g, '').trim()).filter(Boolean))];
}

function renderOnboardingCompactReadiness() {
    const state = getOnboardingFlowState();
    const selectedCount = globalOnboardingSchemaCandidates.filter(x => !!x.isSelected).length;
    setText('txtOnboardingCompactWorkspace', state.hasWorkspace ? 'Listo' : 'Pendiente');
    setText('txtOnboardingCompactTables', state.hasAllowed ? `${selectedCount || 'OK'} activos` : 'Sin seleccionar');
    setText('txtOnboardingCompactContext', state.isInitialized ? 'Preparado' : 'Sin preparar');
    setText('txtOnboardingCompactValidation', state.hasValidation ? 'Validada' : 'Pendiente');
    setText(
        'txtOnboardingCompactSummary',
        !state.hasWorkspace
            ? 'Empieza por guardar el workspace y la conexion.'
            : !state.hasAllowed
                ? 'El siguiente paso es elegir y guardar las tablas permitidas.'
                : !state.isInitialized
                    ? 'Ya puedes preparar el dominio con el contexto tecnico base.'
                    : state.hasValidation
                        ? 'El flujo base ya quedo completado.'
                        : 'Solo falta correr una prueba real del dominio.'
    );
    updateReadinessCard('compactWorkspaceCard', state.hasWorkspace);
    updateReadinessCard('compactTablesCard', state.hasAllowed);
    updateReadinessCard('compactContextCard', state.isInitialized);
    updateReadinessCard('compactValidationCard', state.hasValidation);
}

function syncOnboardingFooterActions() {
    const state = getOnboardingFlowState();
    const footerHint = document.getElementById('onboardingFooterHint');
    const footerShell = document.querySelector('#pane-onboarding .onboarding-footer-shell');
    const footerActions = footerShell?.querySelector('.inline-actions');
    const actions = [
        document.getElementById('btnOnboardingSaveStep1'),
        document.getElementById('btnOnboardingSaveAllowedObjects'),
        document.getElementById('btnOnboardingInitialize'),
        document.getElementById('btnOnboardingRunValidation')
    ].filter(Boolean);
    actions.forEach(button => {
        button.style.display = 'none';
        button.classList.remove('onboarding-primary-cta-active');
        button.classList.remove('btn-ok', 'btn-amber');
        button.classList.add('btn-primary');
    });

    let activeButton = document.getElementById('btnOnboardingSaveStep1');
    let footerCopy = 'Completa tenant, dominio y conexion para guardar el workspace.';

    if (state.hasWorkspace && !state.hasAllowed) {
        activeButton = document.getElementById('btnOnboardingSaveAllowedObjects');
        footerCopy = 'Cuando guardes las tablas, el sistema ya podra preparar el dominio.';
    } else if (state.hasAllowed && !state.isInitialized) {
        activeButton = document.getElementById('btnOnboardingInitialize');
        footerCopy = 'Este paso automatiza el contexto tecnico minimo para intentar una prueba real.';
    } else if (state.isInitialized) {
        activeButton = document.getElementById('btnOnboardingRunValidation');
        footerCopy = state.hasValidation
            ? 'La prueba ya paso. Puedes cerrar el flujo base o abrir la afinacion avanzada.'
            : 'Ejecuta una pregunta real para confirmar que el dominio ya responde bien.';
    }

    if (activeButton) {
        activeButton.style.display = 'inline-flex';
        activeButton.classList.add('onboarding-primary-cta-active');
    }
    if (footerHint) {
        footerHint.textContent = footerCopy;
    }
    if (footerShell) {
        footerShell.classList.toggle('is-complete', !!state.hasValidation);
        footerShell.style.display = state.hasValidation ? 'grid' : 'none';
    }
    if (footerActions) {
        distributeOnboardingPrimaryActions(activeButton, footerActions);
    }
}

function distributeOnboardingPrimaryActions(activeButton, footerActions) {
    const primaryHosts = {
        btnOnboardingSaveStep1: document.getElementById('onboardingStep1ActionHost'),
        btnOnboardingSaveAllowedObjects: document.getElementById('onboardingStep2ActionHost'),
        btnOnboardingInitialize: document.getElementById('onboardingStep3ActionHost'),
        btnOnboardingRunValidation: document.getElementById('onboardingStep4ActionHost')
    };

    Object.values(primaryHosts).filter(Boolean).forEach(host => {
        host.innerHTML = '';
        host.classList.remove('is-visible');
    });

    if (activeButton && primaryHosts[activeButton.id]) {
        const host = primaryHosts[activeButton.id];
        host.classList.add('is-visible');
        host.appendChild(activeButton);
    }

    const secondaryButtons = [
        document.querySelector('#pane-onboarding button[onclick="startNewOnboardingWorkspace()"]'),
        document.getElementById('btnOnboardingExportPack'),
        document.getElementById('btnOnboardingImportPack')
    ].filter(Boolean);

    secondaryButtons.forEach(button => {
        button.style.display = 'inline-flex';
        if (button.parentElement !== footerActions) {
            footerActions.appendChild(button);
        }
    });
}

function enhanceOnboardingWizardLayout() {
    ensureOnboardingStepGoal('onboardingStepPanel1', 'Exito de este paso: el sistema ya conoce el workspace, el dominio semantico y la conexion con la que descubrira el schema.');
    ensureOnboardingSchemaAssistUI();
    ensureOnboardingPreparationUI();
    ensureOnboardingValidationUI();
    ensureOnboardingReadinessAdvancedUI();
    syncOnboardingFooterActions();
}

const originalLoadOnboardingBootstrap = loadOnboardingBootstrap;
loadOnboardingBootstrap = async function () {
    await originalLoadOnboardingBootstrap();
    enhanceOnboardingWizardLayout();
};

const originalResetOnboardingForm = resetOnboardingForm;
resetOnboardingForm = function (options = {}) {
    originalResetOnboardingForm(options);
    enhanceOnboardingWizardLayout();
};

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

    const narrative = document.getElementById('txtOnboardingPreparationNarrative');
    if (narrative) {
        narrative.textContent = !status
            ? 'Todavia no se ha preparado el dominio. Cuando ejecutes este paso te mostraremos aqui que quedo listo.'
            : `Dominio ${domain || 'sin-domain'} en ${connectionName || 'sin-conexion'}: ${allowedObjectsCount} tablas activas, ${schemaDocsCount} schema docs y ${semanticHintsCount} hints base.`;
    }

    const meta = document.getElementById('onboardingStatusMeta');
    if (meta) {
        meta.innerHTML = !status
            ? '<span class="meta-empty">Ejecuta este paso cuando ya estes conforme con las tablas permitidas.</span>'
            : `<span class="meta-chip status-ok">${escHtml(domain || '')}</span><span class="meta-chip training-no">${escHtml(connectionName || 'sin-conexion')}</span><span class="meta-chip training-no">${allowedObjectsCount} tablas</span><span class="meta-chip training-no">${schemaDocsCount} schema docs</span><span class="meta-chip training-no">${semanticHintsCount} hints</span>`;
    }

    const isInitialized = hasSchemaDocs && hasSemanticHints;
    if (!isInitialized && globalOnboardingValidation?.jobId) {
        resetOnboardingValidation(true);
    } else {
        renderOnboardingValidation();
    }

    renderOnboardingReadiness();
    syncOnboardingFooterActions();
    updateOnboardingStepper();
    renderOnboardingActionGuidance();
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
            suggestionList.innerHTML = suggestions.slice(0, 3).map(question => `<button class="suggestion-chip suggestion-chip-card" type="button" onclick="applyOnboardingSuggestedQuestion('${jsString(question)}')"><span class="suggestion-chip-title">Usar esta</span><span>${escHtml(question)}</span></button>`).join('');
        }
    }

    if (runBtn) runBtn.disabled = !isInitialized;
    setText('txtOnboardingValidationSql', formatOnboardingValidationSql(globalOnboardingValidation));
    setText('txtOnboardingValidationResult', formatOnboardingValidationResult(globalOnboardingValidation));
    setText('txtOnboardingValidationExecutionState', globalOnboardingValidation?.status || 'Sin ejecutar');
    setText('txtOnboardingValidationTablesUsed', extractOnboardingSqlTablesUsed(globalOnboardingValidation?.sqlText).join(', ') || 'Aun no disponible');
    setText('txtOnboardingValidationFeedback', validationOk ? 'Respuesta util y coherente' : (globalOnboardingValidation?.errorText ? 'Requiere ajuste' : 'Esperando prueba'));

    if (meta) {
        if (!isInitialized) {
            meta.innerHTML = '<span class="meta-empty">Completa la preparacion del dominio antes de ejecutar una prueba.</span>';
        } else if (!globalOnboardingValidation) {
            meta.innerHTML = `<span class="meta-chip training-no">${escHtml(getValue('txtOnboardingDomain').trim() || 'sin-domain')}</span><span class="meta-chip training-no">${escHtml(getValue('txtOnboardingConnectionName').trim() || 'sin-conexion')}</span><span class="meta-empty">Ejecuta una pregunta real para cerrar este onboarding.</span>`;
        } else {
            const statusKey = String(globalOnboardingValidation.status || '').toLowerCase();
            const statusClass = statusKey === 'completed' ? 'status-ok' : (statusKey === 'queued' || statusKey === 'processing' ? 'verify-pending' : 'status-err');
            const resultCount = globalOnboardingValidation.resultCount;
            const statusLabel = globalOnboardingValidation.status || 'Queued';
            meta.innerHTML = `<span class="meta-chip ${statusClass}">${escHtml(statusLabel)}</span>${resultCount !== null && resultCount !== undefined ? `<span class="meta-chip training-no">${resultCount} filas</span>` : ''}${validationOk ? '<span class="meta-chip verify-ok">Dominio listo para preguntas reales</span>' : '<span class="meta-chip verify-pending">Ajusta y vuelve a probar si la respuesta no es confiable</span>'}`;
        }
    }

    panel?.classList.toggle('is-success', validationOk);
    resultCard?.classList.toggle('is-success', validationOk);
    sqlCard?.classList.toggle('is-success', validationOk);

    renderOnboardingReadiness();
    syncOnboardingFooterActions();
    updateOnboardingStepper();
}

function renderOnboardingReadiness() {
    const health = globalOnboardingStatus?.health ?? globalOnboardingStatus?.Health ?? null;
    const hasWorkspace = !!getValue('txtOnboardingTenantKey').trim() && !!getValue('txtOnboardingDomain').trim() && !!getValue('txtOnboardingConnectionName').trim();
    const hasContext = !!(health?.hasAllowedObjects ?? health?.HasAllowedObjects) && !!(health?.hasSchemaDocs ?? health?.HasSchemaDocs) && !!(health?.hasSemanticHints ?? health?.HasSemanticHints);
    const hasValidation = isOnboardingValidationSuccessful(globalOnboardingValidation);
    const meta = document.getElementById('onboardingReadinessMeta');
    const recommendation = document.getElementById('txtOnboardingFinalRecommendation');
    const finalPanel = document.getElementById('onboardingStepPanel5');
    const advancedHub = document.querySelector('#onboardingStepPanel5 .onboarding-advanced-hub');
    const advancedButton = document.getElementById('btnToggleOnboardingAdvancedArea');

    updateReadinessCard('readinessConnectionCard', hasWorkspace);
    updateReadinessCard('readinessSchemaCard', hasContext);
    updateReadinessCard('readinessValidationCard', hasValidation);
    finalPanel?.classList.toggle('is-success', hasWorkspace && hasContext && hasValidation);
    finalPanel?.classList.toggle('is-pending', !hasValidation);

    if (meta) {
        if (hasWorkspace && hasContext && hasValidation) {
            meta.innerHTML = `<span class="meta-chip verify-ok">Dominio listo</span><span class="meta-chip training-no">${escHtml(getValue('txtOnboardingDomain').trim() || 'sin-domain')}</span><span class="meta-chip training-no">${escHtml(getValue('txtOnboardingConnectionName').trim() || 'sin-conexion')}</span><span class="meta-chip status-ok">Onboarding base completado</span>`;
        } else {
            const missing = [];
            if (!hasWorkspace) missing.push('workspace');
            if (!hasContext) missing.push('contexto');
            if (!hasValidation) missing.push('prueba');
            meta.innerHTML = `<span class="meta-chip verify-pending">Pendiente</span><span class="meta-empty">Falta cerrar: ${escHtml(missing.join(', '))}</span>`;
        }
    }

    if (recommendation) {
        recommendation.textContent = hasWorkspace && hasContext && hasValidation
            ? 'El onboarding base ya quedo listo. Si quieres, ahora puedes exportar un pack o seguir con afinacion avanzada.'
            : (!hasWorkspace ? 'Primero asegura tenant, dominio y conexion.' : (!hasContext ? 'El siguiente foco es preparar bien el dominio.' : 'Haz una prueba real para confirmar que el dominio ya responde bien.'));
    }

    if (!hasValidation) {
        toggleOnboardingAdvancedArea(false);
    }

    if (advancedHub) {
        advancedHub.classList.toggle('is-available', hasValidation);
    }

    if (advancedButton) {
        advancedButton.disabled = !hasValidation;
        advancedButton.textContent = hasValidation
            ? (onboardingAdvancedAreaOpen ? 'Ocultar ajustes avanzados' : 'Mostrar ajustes avanzados')
            : 'Disponible al cerrar onboarding';
    }

    if (!hasValidation) {
        toggleOnboardingFinalTools(false);
    }

    syncOnboardingFooterActions();
    renderOnboardingActionGuidance();
}
// -----------------------------------------------------------
// ONBOARDING UX REFACTOR ENHANCEMENTS - ROUND 2
// -----------------------------------------------------------
function setOnboardingPrimaryButtonLabel(buttonId, label) {
    const button = document.getElementById(buttonId);
    if (!button) return;
    const spinner = button.querySelector('.btn-spinner');
    button.textContent = '';
    if (spinner) {
        button.appendChild(spinner);
    }
    button.append(document.createTextNode(label));
}

function ensureOnboardingWorkspaceGuidedUI() {
    const panel = document.getElementById('onboardingStepPanel1');
    if (!panel) return;

    ensureOnboardingStepGoal('onboardingStepPanel1', 'Exito de este paso: el sistema ya conoce el workspace, el dominio semantico y la conexion con la que descubrira el schema.');
    panel.querySelector('.onboarding-workspace-intro')?.remove();
    panel.querySelector('.onboarding-workspace-checklist')?.remove();

    const connectionActions = document.getElementById('onboardingConnectionMeta')?.parentElement;
    connectionActions?.classList.add('onboarding-connection-actions');
    const connectionEditor = document.getElementById('onboardingConnectionEditor');
    connectionEditor?.classList.add('onboarding-inline-editor-compact');
}

function ensureOnboardingCompactReadinessUI() {
    document.getElementById('onboardingCompactReadiness')?.remove();
}

function ensureOnboardingFooterBarUI() {
    const footer = document.querySelector('#pane-onboarding .editor-actions');
    if (!footer) return;

    footer.classList.add('onboarding-footer-shell');
    const actions = footer.querySelector('.inline-actions');
    if (!actions) return;

    footer.querySelector('.onboarding-footer-bar')?.remove();
    footer.querySelector('#onboardingPrimaryActionHost')?.remove();
    footer.querySelector('#onboardingSecondaryActions')?.remove();

    let info = document.getElementById('onboardingFooterInfo');
    if (!info) {
        info = document.createElement('div');
        info.id = 'onboardingFooterInfo';
        info.className = 'onboarding-footer-info';
        info.innerHTML = `
            <div class="subpanel-title">Siguiente paso</div>
            <div class="subpanel-desc" id="onboardingFooterHint">Completa tenant, dominio y conexion para guardar el workspace.</div>`;
    }

    if (info.parentElement !== footer) {
        footer.insertBefore(info, footer.firstChild);
    }
    if (actions.parentElement !== footer) {
        footer.appendChild(actions);
    }

    const primaryIds = new Set([
        'btnOnboardingSaveStep1',
        'btnOnboardingSaveAllowedObjects',
        'btnOnboardingInitialize',
        'btnOnboardingRunValidation'
    ]);

    [...actions.children].forEach(button => {
        if (!(button instanceof HTMLElement) || button.tagName !== 'BUTTON') return;
        if (primaryIds.has(button.id)) {
            button.classList.add('onboarding-primary-cta');
            button.classList.remove('onboarding-secondary-cta');
            button.style.display = 'none';
        } else {
            button.classList.remove('onboarding-primary-cta');
            button.classList.add('onboarding-secondary-cta');
            button.style.display = 'inline-flex';
        }
    });

    document.getElementById('btnToggleOnboardingAdvancedAreaFooter')?.remove();

    setOnboardingPrimaryButtonLabel('btnOnboardingSaveStep1', 'Guardar y continuar');
    setOnboardingPrimaryButtonLabel('btnOnboardingSaveAllowedObjects', 'Guardar tablas y continuar');
    setOnboardingPrimaryButtonLabel('btnOnboardingInitialize', 'Preparar dominio');
    setOnboardingPrimaryButtonLabel('btnOnboardingRunValidation', 'Ejecutar prueba');
}


const previousEnhanceOnboardingWizardLayout = enhanceOnboardingWizardLayout;
enhanceOnboardingWizardLayout = function () {
    previousEnhanceOnboardingWizardLayout();
    ensureOnboardingWorkspaceGuidedUI();
    ensureOnboardingCompactReadinessUI();
    ensureOnboardingFooterBarUI();
    syncOnboardingFooterActions();
};

// -----------------------------------------------------------
// SQL ALERTS
// -----------------------------------------------------------
function renderSqlAlertContextBanner(message, isEmpty = false) {
    const banner = document.getElementById('sqlAlertContextBanner');
    if (!banner) return;
    banner.textContent = message;
    banner.classList.toggle('is-empty', !!isEmpty);
}

function showSqlAlertBanner(type, message) {
    const banner = document.getElementById('sqlAlertBanner');
    if (!banner) return;
    banner.className = `rag-banner show ${type}`;
    banner.textContent = message;
}

function hideSqlAlertBanner() {
    const banner = document.getElementById('sqlAlertBanner');
    if (!banner) return;
    banner.className = 'rag-banner';
    banner.textContent = '';
}

function ensureSqlAlertContextDefaults() {
    const context = getActiveAdminContext();
    if (!context) return false;
    if (!getValue('txtSqlAlertTenantKey').trim()) setValue('txtSqlAlertTenantKey', context.tenantKey);
    if (!getValue('txtSqlAlertDomain').trim()) setValue('txtSqlAlertDomain', context.domain);
    if (!getValue('txtSqlAlertConnectionName').trim()) setValue('txtSqlAlertConnectionName', context.connectionName);
    return true;
}

function setSqlAlertMeta(rule = null) {
    const meta = document.getElementById('sqlAlertMeta');
    if (!meta) return;

    if (!rule) {
        meta.innerHTML = '<span class="meta-empty">Ninguna alerta seleccionada</span>';
        return;
    }

    const runtimeState = String(rule.runtimeState || rule.RuntimeState || 'Closed');
    const lastTriggeredUtc = rule.lastTriggeredUtc || rule.LastTriggeredUtc || '';
    meta.innerHTML = `<span class="meta-chip training-no">${escHtml(rule.metricKey || rule.MetricKey || 'metric')}</span><span class="meta-chip ${String(rule.isActive || rule.IsActive) === 'true' || !!(rule.isActive || rule.IsActive) ? 'verify-ok' : 'status-err'}">${String(rule.isActive || rule.IsActive) === 'true' || !!(rule.isActive || rule.IsActive) ? 'Activa' : 'Inactiva'}</span><span class="meta-chip training-no">${escHtml(runtimeState)}</span>${lastTriggeredUtc ? `<span class="meta-empty">ÃƒÅ¡ltimo disparo: ${escHtml(lastTriggeredUtc)}</span>` : '<span class="meta-empty">Sin disparos todavÃƒÂ­a</span>'}`;
}

function syncSqlAlertActionButtons() {
    const hasSelection = selectedSqlAlertId > 0;
    const ackButton = document.getElementById('btnSqlAlertAck');
    const clearButton = document.getElementById('btnSqlAlertClear');
    const toggleButton = document.getElementById('btnSqlAlertToggle');
    if (ackButton) ackButton.disabled = !hasSelection;
    if (clearButton) clearButton.disabled = !hasSelection;
    if (toggleButton) toggleButton.disabled = !hasSelection;
}

function populateSqlAlertCatalogSelectors() {
    const metricSelect = document.getElementById('txtSqlAlertMetricKey');
    const dimensionSelect = document.getElementById('txtSqlAlertDimensionKey');
    if (!metricSelect || !dimensionSelect) return;

    const metrics = Array.isArray(globalSqlAlertCatalog?.metrics) ? globalSqlAlertCatalog.metrics : (Array.isArray(globalSqlAlertCatalog?.Metrics) ? globalSqlAlertCatalog.Metrics : []);
    const dimensions = Array.isArray(globalSqlAlertCatalog?.dimensions) ? globalSqlAlertCatalog.dimensions : (Array.isArray(globalSqlAlertCatalog?.Dimensions) ? globalSqlAlertCatalog.Dimensions : []);
    const currentMetric = getValue('txtSqlAlertMetricKey').trim();
    const currentDimension = getValue('txtSqlAlertDimensionKey').trim();

    metricSelect.innerHTML = metrics.length
        ? metrics.map(item => `<option value="${escHtml(item.key || item.Key || '')}">${escHtml(item.displayName || item.DisplayName || item.key || item.Key || '')}</option>`).join('')
        : '<option value="">Sin mÃƒÂ©tricas disponibles</option>';

    dimensionSelect.innerHTML = ['<option value="">Sin dimensiÃƒÂ³n</option>', ...dimensions.map(item => `<option value="${escHtml(item.key || item.Key || '')}">${escHtml(item.displayName || item.DisplayName || item.key || item.Key || '')}</option>`)].join('');

    if (currentMetric) setValue('txtSqlAlertMetricKey', currentMetric);
    if (currentDimension) setValue('txtSqlAlertDimensionKey', currentDimension);
}

function resetSqlAlertForm() {
    selectedSqlAlertId = 0;
    hideSqlAlertBanner();
    setValue('txtSqlAlertId', '');
    setValue('txtSqlAlertDisplayName', '');
    setValue('txtSqlAlertDimensionValue', '');
    setValue('txtSqlAlertThreshold', '50');
    setValue('txtSqlAlertFrequency', '5');
    setValue('txtSqlAlertCooldown', '30');
    setValue('txtSqlAlertNotes', '');
    setValue('txtSqlAlertPreview', '');
    setValue('txtSqlAlertOperator', '1');
    setValue('txtSqlAlertTimeScope', '1');
    setChecked('chkSqlAlertIsActive', true);
    ensureSqlAlertContextDefaults();
    populateSqlAlertCatalogSelectors();
    setSqlAlertMeta();
    syncSqlAlertActionButtons();
}

function renderSqlAlertList() {
    const list = document.getElementById('sqlAlertList');
    const count = document.getElementById('sqlAlertCount');
    if (!list) return;

    const context = getActiveAdminContext();
    const items = globalSqlAlerts.filter(item => {
        if (!context) return true;
        const domain = String(item.domain || item.Domain || '').trim();
        const connectionName = String(item.connectionName || item.ConnectionName || '').trim();
        return domain === context.domain && connectionName === context.connectionName;
    });

    if (count) count.textContent = String(items.length);

    if (!items.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="12" cy="12" r="10"></circle>
                    <path d="M12 8v4"></path>
                    <path d="M12 16h.01"></path>
                </svg>
                AÃƒÂºn no hay alertas SQL para este contexto.
            </div>`;
        return;
    }

    list.innerHTML = items.map(item => {
        const id = item.id || item.Id || 0;
        const isSelected = Number(id) === Number(selectedSqlAlertId);
        const active = !!(item.isActive || item.IsActive);
        const runtimeState = String(item.runtimeState || item.RuntimeState || 'Closed');
        const metricKey = item.metricKey || item.MetricKey || 'metric';
        const operatorLabel = item.comparisonOperatorLabel || item.ComparisonOperatorLabel || '>';
        const threshold = item.threshold ?? item.Threshold ?? 0;
        const dimensionKey = item.dimensionKey || item.DimensionKey || '';
        const dimensionValue = item.dimensionValue || item.DimensionValue || '';
        const dimensionText = dimensionKey && dimensionValue ? ` Ã‚Â· ${dimensionKey}=${dimensionValue}` : '';
        return `
            <div class="history-item sql-alert-card ${isSelected ? 'is-selected' : ''}" onclick="selectSqlAlert(${id})">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(item.displayName || item.DisplayName || 'Alerta SQL')}</div>
                    <div class="hi-time">${escHtml(metricKey)}</div>
                </div>
                <div class="sql-alert-summary">${escHtml(`${metricKey} ${operatorLabel} ${threshold}${dimensionText}`)}</div>
                <div class="hi-badges">
                    <span class="hi-verify ${active ? 'verified' : 'rejected'}">${active ? 'Activa' : 'Inactiva'}</span>
                    <span class="hi-status ok"><span class="dot"></span>${escHtml(runtimeState)}</span>
                </div>
            </div>`;
    }).join('');
}

function renderSqlAlertEvents() {
    const list = document.getElementById('sqlAlertEventList');
    if (!list) return;

    if (!globalSqlAlertEvents.length) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="12" cy="12" r="10"></circle>
                    <path d="M12 8v4"></path>
                    <path d="M12 16h.01"></path>
                </svg>
                Sin eventos recientes para el contexto actual.
            </div>`;
        return;
    }

    list.innerHTML = globalSqlAlertEvents.map(item => {
        const eventType = String(item.eventType || item.EventType || '');
        const message = item.message || item.Message || '';
        const when = item.eventUtc || item.EventUtc || '';
        const observedValue = item.observedValue ?? item.ObservedValue;
        const errorText = item.errorText || item.ErrorText || '';
        return `
            <div class="history-item sql-alert-event-card">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(eventType)}</div>
                    <div class="hi-time">${escHtml(when)}</div>
                </div>
                <div class="sql-alert-summary">${escHtml(message)}</div>
                <div class="hi-badges">
                    ${observedValue !== null && observedValue !== undefined ? `<span class="hi-status ok"><span class="dot"></span>Observado ${escHtml(String(observedValue))}</span>` : ''}
                    ${errorText ? `<span class="hi-verify rejected">${escHtml(errorText)}</span>` : ''}
                </div>
            </div>`;
    }).join('');
}

function fillSqlAlertForm(rule) {
    if (!rule) return;
    selectedSqlAlertId = Number(rule.id || rule.Id || 0);
    setValue('txtSqlAlertId', String(selectedSqlAlertId || ''));
    setValue('txtSqlAlertTenantKey', rule.tenantKey || rule.TenantKey || '');
    setValue('txtSqlAlertDomain', rule.domain || rule.Domain || '');
    setValue('txtSqlAlertConnectionName', rule.connectionName || rule.ConnectionName || '');
    setValue('txtSqlAlertDisplayName', rule.displayName || rule.DisplayName || '');
    setValue('txtSqlAlertMetricKey', rule.metricKey || rule.MetricKey || '');
    setValue('txtSqlAlertDimensionKey', rule.dimensionKey || rule.DimensionKey || '');
    setValue('txtSqlAlertDimensionValue', rule.dimensionValue || rule.DimensionValue || '');
    setValue('txtSqlAlertOperator', String(rule.comparisonOperator || rule.ComparisonOperator || 1));
    setValue('txtSqlAlertThreshold', String(rule.threshold ?? rule.Threshold ?? 0));
    setValue('txtSqlAlertTimeScope', String(rule.timeScope || rule.TimeScope || 1));
    setValue('txtSqlAlertFrequency', String(rule.evaluationFrequencyMinutes || rule.EvaluationFrequencyMinutes || 5));
    setValue('txtSqlAlertCooldown', String(rule.cooldownMinutes || rule.CooldownMinutes || 30));
    setValue('txtSqlAlertNotes', rule.notes || rule.Notes || '');
    setChecked('chkSqlAlertIsActive', !!(rule.isActive || rule.IsActive));
    setValue('txtSqlAlertPreview', '');
    setSqlAlertMeta(rule);
    syncSqlAlertActionButtons();
}

function selectSqlAlert(id) {
    const rule = globalSqlAlerts.find(item => Number(item.id || item.Id || 0) === Number(id));
    if (!rule) return;
    fillSqlAlertForm(rule);
    renderSqlAlertList();
}

function buildSqlAlertPayload() {
    return {
        id: Number(getValue('txtSqlAlertId') || '0'),
        ruleKey: null,
        tenantKey: getValue('txtSqlAlertTenantKey').trim(),
        domain: getValue('txtSqlAlertDomain').trim(),
        connectionName: getValue('txtSqlAlertConnectionName').trim(),
        displayName: getValue('txtSqlAlertDisplayName').trim(),
        metricKey: getValue('txtSqlAlertMetricKey').trim(),
        dimensionKey: getValue('txtSqlAlertDimensionKey').trim() || null,
        dimensionValue: getValue('txtSqlAlertDimensionValue').trim() || null,
        comparisonOperator: Number(getValue('txtSqlAlertOperator') || '1'),
        threshold: Number(getValue('txtSqlAlertThreshold') || '0'),
        timeScope: Number(getValue('txtSqlAlertTimeScope') || '1'),
        evaluationFrequencyMinutes: Number(getValue('txtSqlAlertFrequency') || '5'),
        cooldownMinutes: Number(getValue('txtSqlAlertCooldown') || '30'),
        isActive: getChecked('chkSqlAlertIsActive'),
        notes: getValue('txtSqlAlertNotes').trim() || null
    };
}

async function previewSqlAlert() {
    const payload = buildSqlAlertPayload();
    try {
        const res = await fetch('/api/admin/sql-alerts/preview', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const body = await safeJson(res);
        if (!res.ok) throw new Error(body?.Error || body?.error || `HTTP ${res.status}`);
        setValue('txtSqlAlertPreview', body?.sql || body?.Sql || '');
        showSqlAlertBanner('ok', body?.summary || body?.Summary || 'Preview SQL generado correctamente.');
    } catch (e) {
        showSqlAlertBanner('err', String(e.message || e));
    }
}

async function saveSqlAlert() {
    const payload = buildSqlAlertPayload();
    const spinner = document.getElementById('sqlAlertSpinner');
    try {
        spinner?.classList.add('show');
        const isUpdate = payload.id > 0;
        const res = await fetch(isUpdate ? `/api/admin/sql-alerts/${payload.id}` : '/api/admin/sql-alerts', {
            method: isUpdate ? 'PUT' : 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const body = await safeJson(res);
        if (!res.ok) throw new Error(body?.Error || body?.error || `HTTP ${res.status}`);
        showSqlAlertBanner('ok', 'Regla de monitoreo guardada correctamente.');
        showAdminToast('ok', payload.displayName || 'Regla guardada.', 'Alert Monitor');
        await loadSqlAlertsAdmin();
        const savedId = Number(body?.id || body?.Id || 0);
        if (savedId > 0) {
            selectSqlAlert(savedId);
        }
    } catch (e) {
        showSqlAlertBanner('err', String(e.message || e));
    } finally {
        spinner?.classList.remove('show');
    }
}

async function toggleSqlAlertStatus() {
    if (!selectedSqlAlertId) return;
    const rule = globalSqlAlerts.find(item => Number(item.id || item.Id || 0) === Number(selectedSqlAlertId));
    if (!rule) return;
    const isActive = !(rule.isActive || rule.IsActive);
    try {
        const res = await fetch(`/api/admin/sql-alerts/${selectedSqlAlertId}/activate`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ isActive })
        });
        const body = await safeJson(res);
        if (!res.ok) throw new Error(body?.Error || body?.error || `HTTP ${res.status}`);
        showAdminToast('ok', isActive ? 'Regla activada.' : 'Regla desactivada.', 'Alert Monitor');
        await loadSqlAlertsAdmin();
    } catch (e) {
        showAdminToast('err', String(e.message || e), 'Alert Monitor');
    }
}

async function ackSqlAlert() {
    if (!selectedSqlAlertId) return;
    try {
        const res = await fetch(`/api/admin/sql-alerts/${selectedSqlAlertId}/ack`, { method: 'POST' });
        const body = await safeJson(res);
        if (!res.ok) throw new Error(body?.Error || body?.error || `HTTP ${res.status}`);
        showAdminToast('ok', 'Evento reconocido manualmente.', 'Alert Monitor');
        await loadSqlAlertsAdmin();
    } catch (e) {
        showAdminToast('err', String(e.message || e), 'Alert Monitor');
    }
}

async function clearSqlAlert() {
    if (!selectedSqlAlertId) return;
    try {
        const res = await fetch(`/api/admin/sql-alerts/${selectedSqlAlertId}/clear`, { method: 'POST' });
        const body = await safeJson(res);
        if (!res.ok) throw new Error(body?.Error || body?.error || `HTTP ${res.status}`);
        showAdminToast('ok', 'Regla limpiada manualmente.', 'Alert Monitor');
        await loadSqlAlertsAdmin();
    } catch (e) {
        showAdminToast('err', String(e.message || e), 'Alert Monitor');
    }
}

async function loadSqlAlertsAdmin() {
    const list = document.getElementById('sqlAlertList');
    if (!list) return;
    hideSqlAlertBanner();

    const context = getActiveAdminContext() || buildAdminContextFromOnboardingForm();
    if (!context?.tenantKey || !context?.domain || !context?.connectionName) {
        renderSqlAlertContextBanner('Selecciona primero un workspace y un contexto vÃƒÂ¡lido en Onboarding.', true);
        globalSqlAlerts = [];
        globalSqlAlertEvents = [];
        globalSqlAlertCatalog = null;
        renderSqlAlertList();
        renderSqlAlertEvents();
        resetSqlAlertForm();
        return;
    }

    renderSqlAlertContextBanner(`Contexto activo: ${context.tenantDisplayName} / ${context.domain} / ${context.connectionName}`);
    setValue('txtSqlAlertTenantKey', context.tenantKey);
    setValue('txtSqlAlertDomain', context.domain);
    setValue('txtSqlAlertConnectionName', context.connectionName);

    try {
        const [catalogRes, rulesRes, eventsRes] = await Promise.all([
            fetch(`/api/admin/sql-alerts/catalog?domain=${encodeURIComponent(context.domain)}&connectionName=${encodeURIComponent(context.connectionName)}`),
            fetch('/api/admin/sql-alerts'),
            fetch(`/api/admin/sql-alerts/events?domain=${encodeURIComponent(context.domain)}&limit=50`)
        ]);

        const [catalogBody, rulesBody, eventsBody] = await Promise.all([
            safeJson(catalogRes),
            safeJson(rulesRes),
            safeJson(eventsRes)
        ]);

        if (!catalogRes.ok) throw new Error(catalogBody?.Error || catalogBody?.error || `HTTP ${catalogRes.status}`);
        if (!rulesRes.ok) throw new Error(rulesBody?.Error || rulesBody?.error || `HTTP ${rulesRes.status}`);
        if (!eventsRes.ok) throw new Error(eventsBody?.Error || eventsBody?.error || `HTTP ${eventsRes.status}`);

        globalSqlAlertCatalog = catalogBody || null;
        globalSqlAlerts = Array.isArray(rulesBody) ? rulesBody : [];
        globalSqlAlertEvents = (Array.isArray(eventsBody) ? eventsBody : []).filter(item =>
            String(item.connectionName || item.ConnectionName || '').trim() === context.connectionName);
        populateSqlAlertCatalogSelectors();
        renderSqlAlertList();
        renderSqlAlertEvents();

        const selectedStillExists = globalSqlAlerts.some(item => Number(item.id || item.Id || 0) === Number(selectedSqlAlertId));
        if (selectedStillExists) {
            selectSqlAlert(selectedSqlAlertId);
        } else {
            resetSqlAlertForm();
        }
    } catch (e) {
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="12" cy="12" r="10"></circle>
                    <path d="M8 12h8"></path>
                    <path d="M12 8v8"></path>
                </svg>
                Error cargando Alert Monitor
            </div>`;
        showSqlAlertBanner('err', String(e.message || e));
    }
}

async function ensureAdminSignalR() {
    if (adminSignalRConnection || typeof signalR === 'undefined') return;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hub/assistant')
        .withAutomaticReconnect()
        .build();

    connection.on('SqlAlertEventRaised', payload => {
        const message = payload?.message || payload?.Message || 'Se disparÃƒÂ³ una alerta SQL.';
        const eventType = String(payload?.eventType || payload?.EventType || '').toLowerCase();
        showAdminToast(eventType === 'resolved' ? 'ok' : 'warn', message, 'Alert Monitor', 5200);
        if (document.getElementById('pane-sql-alerts')?.classList.contains('active')) {
            loadSqlAlertsAdmin();
        }
    });

    try {
        await connection.start();
        adminSignalRConnection = connection;
    } catch (e) {
        console.warn('No se pudo iniciar SignalR en admin:', e);
    }
}




// -----------------------------------------------------------
// ONBOARDING WIZARD HARDENING - SINGLE STEP FLOW + BOOTSTRAP FALLBACKS
// -----------------------------------------------------------
function setOnboardingSidebarStatus(kind = 'loading', message = '') {
    const status = document.getElementById('onboardingSidebarStatus');
    if (!status) return;
    status.classList.remove('is-loading', 'is-ok', 'is-warn', 'is-err');
    status.classList.add(`is-${kind}`);
    status.textContent = message || (kind === 'loading'
        ? 'Cargando workspaces del onboarding…'
        : kind === 'ok'
            ? 'Workspaces cargados correctamente.'
            : kind === 'warn'
                ? 'Se cargó el onboarding con fallback.'
                : 'No se pudo cargar el bootstrap del onboarding.');
}
function ensureOnboardingSummarySupportUI() {
    const summary = document.querySelector('#pane-onboarding .onboarding-flow-summary');
    if (!summary) return;
    let support = summary.querySelector('.onboarding-summary-support');
    if (!support) {
        support = document.createElement('div');
        support.className = 'onboarding-summary-support';
        support.innerHTML = `
            <div class="onboarding-summary-block">
                <span class="onboarding-summary-label">QuÃ© dejas listo</span>
                <span class="onboarding-summary-support-text" id="txtOnboardingCurrentStepHint"></span>
            </div>
            <div class="onboarding-summary-block">
                <span class="onboarding-summary-label">QuÃ© sigue despuÃ©s</span>
                <span class="onboarding-summary-support-text" id="txtOnboardingNextAction"></span>
            </div>`;
        summary.insertBefore(support, summary.querySelector('.onboarding-wizard-nav') || null);
    }
}

const originalSelectOnboardingTenantWithDraftFallback = selectOnboardingTenant;
selectOnboardingTenant = async function (tenantKey) {
    const normalizedTenantKey = String(tenantKey || '').trim();
    if (!normalizedTenantKey) return;

    const knownTenant = globalTenants.find(x => String(x.tenantKey || x.TenantKey || '').trim() === normalizedTenantKey)
        || globalOnboardingRuntimeContexts.find(x => String(x.tenantKey || x.TenantKey || '').trim() === normalizedTenantKey);
    if (knownTenant) {
        return originalSelectOnboardingTenantWithDraftFallback(normalizedTenantKey);
    }

    const draft = getPersistedOnboardingWorkspaceFallback();
    if (draft && String(draft.tenantKey || '').trim() === normalizedTenantKey) {
        selectedOnboardingTenantKey = normalizedTenantKey;
        renderOnboardingTenantList();
        toggleOnboardingReferencePanels(readOnboardingReferencePanelsState());
        await applyPersistedOnboardingWorkspaceState(draft);
        return;
    }

    selectedOnboardingTenantKey = normalizedTenantKey;
    renderOnboardingTenantList();
    resetOnboardingForm();
};

loadTenantDomains = async function (tenantKey) {
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

        globalTenantDomains = ((await res.json()) || []).map(item => ({
            ...item,
            hasKnownConnection: hasOnboardingConnectionProfile(item?.connectionName || item?.ConnectionName || '')
        }));
        renderTenantDomains();

        const defaultMapping = globalTenantDomains.find(x => !!(x.isDefault ?? x.IsDefault)) || globalTenantDomains[0];
        if (defaultMapping) {
            setValue('txtOnboardingDomain', defaultMapping.domain || defaultMapping.Domain || '');
            setValue('txtOnboardingConnectionName', defaultMapping.connectionName || defaultMapping.ConnectionName || '');
            setValue('txtOnboardingSystemProfileKey', defaultMapping.systemProfileKey || defaultMapping.SystemProfileKey || 'default');
            setOnboardingMeta(defaultMapping);
        } else {
            const persistedWorkspace = readOnboardingWorkspaceState();
            const defaults = getOnboardingDefaults();
            setValue('txtOnboardingDomain', persistedWorkspace?.domain || defaults.domain || defaults.Domain || defaultAllowedDomain || '');
            setValue('txtOnboardingConnectionName', persistedWorkspace?.connectionName || defaults.connectionName || defaults.ConnectionName || '');
            setValue('txtOnboardingSystemProfileKey', persistedWorkspace?.systemProfileKey || defaults.systemProfileKey || defaults.SystemProfileKey || 'default');
            setOnboardingMeta(null);
        }

        toggleOnboardingAdvancedOptions((getValue('txtOnboardingSystemProfileKey').trim() || 'default') !== 'default');
        persistOnboardingWorkspaceState();
    } catch (error) {
        console.error('Failed to load onboarding tenant-domain mappings.', error);
        globalTenantDomains = [];
        renderTenantDomains();
        showOnboardingBanner('warn', 'No pudimos refrescar los mappings del workspace. Puedes seguir con el contexto cargado o corregir la conexiÃ³n en el paso 1.');
    }
};

renderTenantDomains = function () {
    const list = document.getElementById('onboardingTenantDomainList');
    if (!list) return;

    if (!globalTenantDomains.length) {
        const currentDomain = getValue('txtOnboardingDomain').trim();
        const currentConnection = getValue('txtOnboardingConnectionName').trim();
        const currentProfile = getValue('txtOnboardingSystemProfileKey').trim() || 'default';
        if (currentDomain || currentConnection) {
            const hasKnownConnection = hasOnboardingConnectionProfile(currentConnection);
            list.innerHTML = `
                <div class="history-item onboarding-mapping draft-workspace">
                    <div class="hi-top">
                        <div class="hi-question">${escHtml(currentDomain || 'sin-domain')}</div>
                        <div class="hi-time">${escHtml(currentConnection || 'sin-conexion')}</div>
                    </div>
                    <div class="hi-badges">
                        <span class="hi-verify pending">Actual</span>
                        <span class="hi-status ${hasKnownConnection ? 'ok' : 'warn'}"><span class="dot"></span>${escHtml(currentProfile)}</span>
                    </div>
                    ${!hasKnownConnection && currentConnection ? '<div class="field-hint onboarding-runtime-warning">La conexiÃ³n actual no estÃ¡ registrada en este ambiente. Puedes seguir con el borrador y corregirla en el paso 1.</div>' : ''}
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
                Este workspace aÃºn no tiene mappings.
            </div>`;
        return;
    }

    list.innerHTML = globalTenantDomains.map(item => {
        const domain = item.domain || item.Domain || '';
        const connectionName = item.connectionName || item.ConnectionName || '';
        const profileKey = item.systemProfileKey || item.SystemProfileKey || 'default';
        const isDefault = !!(item.isDefault ?? item.IsDefault);
        const hasKnownConnection = !!(item.hasKnownConnection ?? item.HasKnownConnection ?? hasOnboardingConnectionProfile(connectionName));
        const isSelected = domain === getValue('txtOnboardingDomain').trim() && connectionName === getValue('txtOnboardingConnectionName').trim();
        return `
            <div class="history-item onboarding-mapping is-clickable ${isSelected ? 'is-selected' : ''}" onclick="applyOnboardingMapping('${jsString(domain)}','${jsString(connectionName)}','${jsString(profileKey)}')">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(domain)}</div>
                    <div class="hi-time">${escHtml(connectionName || 'sin-conexion')}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-verify ${isDefault ? 'verified' : 'pending'}">${isDefault ? 'Default' : 'Mapping'}</span>
                    <span class="hi-status ${hasKnownConnection ? 'ok' : 'warn'}"><span class="dot"></span>${escHtml(profileKey)}</span>
                </div>
                ${!hasKnownConnection && connectionName ? '<div class="field-hint onboarding-runtime-warning">Este mapping sigue siendo utilizable, pero la conexiÃ³n ya no estÃ¡ registrada en este ambiente.</div>' : ''}
            </div>`;
    }).join('');
};

populateOnboardingConnectionOptions = function () {
    const select = document.getElementById('txtOnboardingConnectionName');
    if (!select) return;

    const currentConnectionName = getValue('txtOnboardingConnectionName').trim();
    const profiles = Array.isArray(globalConnectionProfiles) ? globalConnectionProfiles : [];
    const profileMap = new Map();
    profiles.forEach(profile => {
        const connectionName = String(profile.connectionName || profile.ConnectionName || '').trim();
        if (!connectionName) return;
        profileMap.set(connectionName.toLowerCase(), profile);
    });

    const fallbackMap = new Map();
    const addFallback = (connectionName, sourceLabel) => {
        const normalized = String(connectionName || '').trim();
        if (!normalized) return;
        const key = normalized.toLowerCase();
        if (profileMap.has(key) || fallbackMap.has(key)) return;
        fallbackMap.set(key, { connectionName: normalized, sourceLabel });
    };

    addFallback(currentConnectionName, 'ConexiÃ³n actual');
    (Array.isArray(globalOnboardingRuntimeContexts) ? globalOnboardingRuntimeContexts : []).forEach(item => {
        addFallback(item?.connectionName || item?.ConnectionName || '', 'Vista desde runtime');
    });
    const draft = getPersistedOnboardingWorkspaceFallback();
    addFallback(draft?.connectionName || '', 'Borrador local');

    const fallbackOptions = Array.from(fallbackMap.values()).map(item => `<option value="${escHtml(item.connectionName)}">${escHtml(`${item.connectionName} Â· ${item.sourceLabel.toLowerCase()} Â· no registrada`)}</option>`);
    const profileOptions = profiles.map(profile => {
        const connectionName = profile.connectionName || profile.ConnectionName || '';
        const profileKey = profile.profileKey || profile.ProfileKey || 'default';
        const databaseName = profile.databaseName || profile.DatabaseName || '?';
        const isActive = !!(profile.isActive || profile.IsActive);
        const label = `${connectionName} Â· ${databaseName} Â· ${profileKey}${isActive ? ' Â· activa' : ''}`;
        return `<option value="${escHtml(connectionName)}">${escHtml(label)}</option>`;
    });

    select.innerHTML = ['<option value="">Selecciona una conexiÃ³n guardada</option>', ...fallbackOptions, ...profileOptions].join('');
    select.value = currentConnectionName || '';
    renderOnboardingConnectionCatalog();
};
renderOnboardingTenantList = function () {
    const list = document.getElementById('onboardingTenantList');
    const count = document.getElementById('onboardingTenantCount');
    if (!list) return;

    const { entries } = getOnboardingWorkspaceEntries();
    if (count) count.textContent = String(entries.length);

    if (!entries.length) {
        const sidebarStatus = document.getElementById('onboardingSidebarStatus')?.textContent || 'TodavÃ­a no hay workspaces utilizables.';
        list.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M12 3v18"></path>
                    <path d="M3 12h18"></path>
                    <circle cx="12" cy="12" r="9"></circle>
                </svg>
                <span class="empty-state-title">Sin workspaces utilizables</span>
                <span class="empty-state-sub">Reintenta el bootstrap o empieza capturando el workspace base en el paso 1.</span>
                <div class="onboarding-tenant-empty-detail">${escHtml(sidebarStatus)}</div>
            </div>`;
        return;
    }

    list.innerHTML = entries.map(entry => {
        const tenantKey = entry.tenantKey || 'workspace';
        const runtimeCount = Array.isArray(entry.runtimeContexts) ? entry.runtimeContexts.length : 0;
        const runtimeKnownConnections = runtimeCount ? entry.runtimeContexts.filter(ctx => !!ctx.hasKnownConnection).length : 0;
        const hasKnownConnection = runtimeCount
            ? runtimeKnownConnections > 0
            : !!entry.connectionName && hasOnboardingConnectionProfile(entry.connectionName);
        const isSelected = selectedOnboardingTenantKey === tenantKey || getValue('txtOnboardingTenantKey').trim() === tenantKey;
        const sourceLabel = entry.source === 'draft'
            ? 'Borrador local'
            : entry.source === 'runtime'
                ? 'Fallback runtime'
                : (entry.isActive ? 'Workspace guardado' : 'Workspace inactivo');
        const secondary = runtimeCount
            ? `${runtimeCount} contexto${runtimeCount === 1 ? '' : 's'} disponible${runtimeCount === 1 ? '' : 's'}${runtimeKnownConnections && runtimeKnownConnections !== runtimeCount ? ` Â· ${runtimeKnownConnections} con conexiÃ³n reconocida` : ''}`
            : (entry.description || (entry.source === 'draft' ? 'Recuperado desde tu navegador' : 'Sin contextos asociados todavÃ­a'));
        return `
            <div class="history-item ${isSelected ? 'selected' : ''}" id="tenant-${escAttr(tenantKey)}" onclick="selectOnboardingTenant('${jsString(tenantKey)}')">
                <div class="hi-top">
                    <div class="hi-question">${escHtml(entry.displayName || tenantKey)}</div>
                    <div class="hi-time">${escHtml(tenantKey)}</div>
                </div>
                <div class="hi-badges">
                    <span class="hi-verify ${entry.source === 'draft' ? 'pending' : (entry.isActive ? 'verified' : 'rejected')}">${escHtml(sourceLabel)}</span>
                    <span class="hi-status ${hasKnownConnection ? 'ok' : 'warn'}"><span class="dot"></span>${escHtml(secondary)}</span>
                </div>
                ${!hasKnownConnection ? '<div class="field-hint onboarding-runtime-warning">Puedes cargar este workspace y corregir la conexiÃ³n en el paso 1. El wizard no deberÃ­a quedarse bloqueado por eso.</div>' : ''}
            </div>`;
    }).join('');
};

async function hydratePreferredOnboardingWorkspace(entries) {
    const persistedWorkspace = readOnboardingWorkspaceState();
    const defaults = getOnboardingDefaults();
    const preferredTenantKey = String(persistedWorkspace?.tenantKey || defaults.tenantKey || defaults.TenantKey || defaultAdminTenant || '').trim();
    const preferredEntry = entries.find(item => String(item.tenantKey || '').trim() === preferredTenantKey) || entries[0] || null;

    if (preferredEntry?.source === 'draft') {
        await applyPersistedOnboardingWorkspaceState(persistedWorkspace || getPersistedOnboardingWorkspaceFallback());
        return;
    }

    if (preferredEntry?.tenantKey) {
        await selectOnboardingTenant(preferredEntry.tenantKey);
        return;
    }

    if (persistedWorkspace?.tenantKey || persistedWorkspace?.domain || persistedWorkspace?.connectionName) {
        await applyPersistedOnboardingWorkspaceState(persistedWorkspace);
        return;
    }

    resetOnboardingForm();
}

const originalLoadOnboardingBootstrapV3 = loadOnboardingBootstrap;
loadOnboardingBootstrap = async function () {
    const list = document.getElementById('onboardingTenantList');
    if (!list) return;

    setOnboardingSidebarStatus('loading', 'Cargando workspaces, conexiones y contextos del onboardingâ€¦');
    hideOnboardingBanner();

    let payload = null;
    let fetchFailed = null;
    try {
        const res = await fetch('/api/admin/onboarding/bootstrap');
        const body = await safeJson(res);
        if (!res.ok) {
            throw new Error(body?.Error || body?.error || `HTTP ${res.status}`);
        }
        payload = body || {};
    } catch (error) {
        fetchFailed = error;
        console.error('Onboarding bootstrap fetch failed.', error);
    }

    if (payload) {
        onboardingBootstrap = payload;
        globalTenants = normalizeOnboardingBootstrapList(payload, 'tenants', 'Tenants');
        globalConnectionProfiles = normalizeOnboardingBootstrapList(payload, 'connections', 'Connections');
        globalOnboardingRuntimeContexts = normalizeOnboardingBootstrapList(payload, 'runtimeContexts', 'RuntimeContexts')
            .map(normalizeOnboardingRuntimeContext)
            .filter(Boolean)
            .sort((a, b) => Number(b.hasKnownConnection) - Number(a.hasKnownConnection) || String(a.tenantDisplayName).localeCompare(String(b.tenantDisplayName)));
        globalTenantDomains = [];
    } else {
        onboardingBootstrap = onboardingBootstrap || {
            EnvironmentName: 'Development',
            NeedsInitialSetup: true,
            Defaults: {
                TenantKey: defaultAdminTenant || 'default',
                Domain: defaultAllowedDomain || '',
                ConnectionName: '',
                SystemProfileKey: 'default'
            }
        };
        globalTenants = Array.isArray(globalTenants) ? globalTenants : [];
        globalConnectionProfiles = Array.isArray(globalConnectionProfiles) ? globalConnectionProfiles : [];
        globalOnboardingRuntimeContexts = Array.isArray(globalOnboardingRuntimeContexts) ? globalOnboardingRuntimeContexts.map(normalizeOnboardingRuntimeContext).filter(Boolean) : [];
    }

    try {
        ensureOnboardingSummarySupportUI();
        ensureOnboardingSingleStepBriefs();
        populateOnboardingSummary();
        populateOnboardingConnectionOptions();
        renderOnboardingRuntimeContexts();
        renderOnboardingTenantList();
        toggleOnboardingReferencePanels(readOnboardingReferencePanelsState());
        await hydratePreferredOnboardingWorkspace(getOnboardingWorkspaceEntries().entries);
        renderOnboardingFlowSummary();
        updateOnboardingPanelFocus();
        syncOnboardingFooterActions();
    } catch (renderError) {
        console.error('Onboarding bootstrap hydration failed.', renderError);
        renderOnboardingTenantList();
        const message = renderError?.message || String(renderError || 'Error de hidrataciÃ³n de UI');
        setOnboardingSidebarStatus('err', `El bootstrap respondiÃ³, pero el sidebar no pudo hidratarse completo. ${message}`);
        showOnboardingBanner('warn', 'El onboarding cargÃ³ parcialmente. Puedes reintentar el bootstrap y revisar la consola para ver el detalle tÃ©cnico.');
        return;
    }

    const { entries, runtimeContexts, draft } = getOnboardingWorkspaceEntries();
    if (fetchFailed) {
        const hasUsableFallback = entries.length > 0 || !!draft;
        setOnboardingSidebarStatus(hasUsableFallback ? 'warn' : 'err', hasUsableFallback
            ? 'No pudimos refrescar el bootstrap. El sidebar sigue operativo con runtime y borrador local.'
            : 'No pudimos cargar el bootstrap y tampoco hay fallback local utilizable.');
        showOnboardingBanner('warn', hasUsableFallback
            ? 'El backend no respondiÃ³ al bootstrap, pero el wizard conserva workspaces Ãºtiles desde runtime o borrador local.'
            : 'El bootstrap fallÃ³ y no encontramos workspaces de respaldo. Usa Recargar o captura un workspace nuevo en el paso 1.');
        return;
    }

    if (entries.length > 0) {
        const sourceCopy = globalTenants.length > 0
            ? 'Workspaces cargados desde bootstrap.'
            : runtimeContexts.length > 0
                ? 'Sin tenants guardados, usando runtime contexts como fallback.'
                : 'Usando el borrador local como respaldo.';
        setOnboardingSidebarStatus(globalTenants.length > 0 ? 'ok' : 'warn', `${entries.length} workspace${entries.length === 1 ? '' : 's'} listo${entries.length === 1 ? '' : 's'} en el sidebar. ${sourceCopy}`);
    } else {
        setOnboardingSidebarStatus('warn', 'El bootstrap respondiÃ³, pero todavÃ­a no hay workspaces configurados para mostrar.');
    }
};
renderOnboardingSchemaCandidates = function () {
    const list = document.getElementById('onboardingSchemaCandidateList');
    const meta = document.getElementById('onboardingSchemaMeta');
    const saveBtn = document.getElementById('btnOnboardingSaveAllowedObjects');
    const selectedBadge = document.getElementById('txtOnboardingSchemaSelectionCount');
    const visibleBadge = document.getElementById('txtOnboardingSchemaVisibleCount');
    if (!list) return;

    const selectedCount = globalOnboardingSchemaCandidates.filter(x => !!x.isSelected).length;
    const search = String(getValue('txtOnboardingSchemaSearch') || '').trim().toLowerCase();
    const typeFilter = String(getValue('selOnboardingSchemaType') || 'all').trim().toLowerCase();
    const assistFilter = String(getValue('selOnboardingSchemaAssist') || 'all').trim().toLowerCase();

    const visibleItems = globalOnboardingSchemaCandidates
        .map((item, sourceIndex) => ({ item, sourceIndex }))
        .filter(({ item }) => {
            const haystack = [item.schemaName, item.SchemaName, item.objectName, item.ObjectName, item.description, item.Description].join(' ').toLowerCase();
            const objectType = normalizeOnboardingObjectType(item.objectType || item.ObjectType || '');
            const isRecommended = !!(item.isSuggested ?? item.IsSuggested ?? item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed);
            const isSelected = !!item.isSelected;
            const isRisky = isRiskyOnboardingCandidate(item);
            if (search && !haystack.includes(search)) return false;
            if (typeFilter !== 'all' && objectType !== typeFilter) return false;
            if (assistFilter === 'recommended' && !isRecommended) return false;
            if (assistFilter === 'selected' && !isSelected) return false;
            if (assistFilter === 'risky' && !isRisky) return false;
            return true;
        })
        .sort((left, right) => {
            const a = left.item;
            const b = right.item;
            const aAllowed = !!(a.isCurrentlyAllowed ?? a.IsCurrentlyAllowed);
            const bAllowed = !!(b.isCurrentlyAllowed ?? b.IsCurrentlyAllowed);
            const aRecommended = !!(a.isSuggested ?? a.IsSuggested ?? aAllowed);
            const bRecommended = !!(b.isSuggested ?? b.IsSuggested ?? bAllowed);
            const aSelected = !!a.isSelected;
            const bSelected = !!b.isSelected;
            const aRisky = isRiskyOnboardingCandidate(a);
            const bRisky = isRiskyOnboardingCandidate(b);
            const scoreA = (aRecommended ? 120 : 0) + (aSelected ? 90 : 0) + (aAllowed ? 70 : 0) - (aRisky ? 25 : 0);
            const scoreB = (bRecommended ? 120 : 0) + (bSelected ? 90 : 0) + (bAllowed ? 70 : 0) - (bRisky ? 25 : 0);
            if (scoreA !== scoreB) return scoreB - scoreA;
            const aKey = `${a.schemaName || a.SchemaName || ''}.${a.objectName || a.ObjectName || ''}`.toLowerCase();
            const bKey = `${b.schemaName || b.SchemaName || ''}.${b.objectName || b.ObjectName || ''}`.toLowerCase();
            return aKey.localeCompare(bKey);
        });

    if (selectedBadge) selectedBadge.textContent = `${selectedCount} seleccionadas`;
    if (visibleBadge) visibleBadge.textContent = globalOnboardingSchemaCandidates.length ? `${visibleItems.length} visibles` : 'Sin schema';

    if (!globalOnboardingSchemaCandidates.length) {
        list.innerHTML = '<div class="empty-state"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="4" y="4" width="16" height="16" rx="2"></rect><path d="M8 8h8"></path><path d="M8 12h8"></path><path d="M8 16h5"></path></svg><span class="empty-state-title">Schema no cargado</span><span class="empty-state-sub">Descubre el schema para empezar a seleccionar objetos permitidos.</span></div>';
        if (meta) meta.innerHTML = '<span class="meta-empty">Primero descubre el schema. Luego conserva solo las tablas o vistas que realmente necesita el dominio.</span>';
        if (saveBtn) saveBtn.disabled = true;
        renderOnboardingActionGuidance();
        return;
    }

    const riskyCount = globalOnboardingSchemaCandidates.filter(isRiskyOnboardingCandidate).length;
    const allowedCount = globalOnboardingSchemaCandidates.filter(item => !!(item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed)).length;
    if (meta) {
        meta.innerHTML = `<span class="meta-chip status-ok">${selectedCount} seleccionadas</span><span class="meta-chip training-no">${allowedCount} ya permitidas</span><span class="meta-chip training-no">${visibleItems.length} visibles</span>${riskyCount ? `<span class="meta-chip verify-pending">${riskyCount} revisar</span>` : ''}<span class="meta-empty">Empieza por lo recomendado y evita seleccionar todo por defecto.</span>`;
    }

    if (!visibleItems.length) {
        list.innerHTML = '<div class="empty-state"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"></circle><path d="M8 12h8"></path><path d="M12 8v8"></path></svg><span class="empty-state-title">Sin coincidencias</span><span class="empty-state-sub">Ajusta los filtros o limpia la bÃºsqueda para ver mÃ¡s objetos.</span></div>';
        if (saveBtn) saveBtn.disabled = selectedCount === 0;
        return;
    }

    const buildCard = ({ item, sourceIndex }) => {
        const schemaName = item.schemaName || item.SchemaName || '';
        const objectName = item.objectName || item.ObjectName || '';
        const objectType = item.objectType || item.ObjectType || '';
        const desc = item.description || item.Description || '';
        const isRecommended = !!(item.isSuggested ?? item.IsSuggested ?? item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed);
        const isCurrentlyAllowed = !!(item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed);
        const isSelected = !!item.isSelected;
        const columnCount = item.columnCount ?? item.ColumnCount ?? 0;
        const pkCount = item.primaryKeyCount ?? item.PrimaryKeyCount ?? 0;
        const fkCount = item.foreignKeyCount ?? item.ForeignKeyCount ?? 0;
        const risky = isRiskyOnboardingCandidate(item);
        return `<label class="schema-candidate ${isSelected ? 'is-selected' : ''} ${isRecommended ? 'is-recommended' : ''} ${risky ? 'is-risky' : ''} ${isCurrentlyAllowed ? 'is-currently-allowed' : ''}"><input type="checkbox" ${isSelected ? 'checked' : ''} onchange="toggleOnboardingSchemaCandidate(${sourceIndex}, this.checked)" /><div class="schema-candidate-body"><div class="schema-candidate-head"><div><div class="hi-question">${escHtml(schemaName)}.${escHtml(objectName)}</div><div class="schema-candidate-submeta">${escHtml(objectType)} Â· ${columnCount} cols Â· ${pkCount} pk Â· ${fkCount} fk</div></div><div class="schema-candidate-tags">${isCurrentlyAllowed ? '<span class="meta-chip training-no">Ya permitida</span>' : ''}${isSelected && !isCurrentlyAllowed ? '<span class="meta-chip verify-ok">Seleccionada</span>' : ''}${isRecommended && !isCurrentlyAllowed ? '<span class="meta-chip status-ok">Recomendada</span>' : ''}${risky ? '<span class="meta-chip verify-pending">Revisar</span>' : ''}</div></div>${desc ? `<div class="schema-candidate-desc">${escHtml(desc)}</div>` : '<div class="schema-candidate-desc">Sin descripciÃ³n disponible. Revisa el nombre, tipo y llaves antes de permitirla.</div>'}</div></label>`;
    };

    const groups = [
        {
            title: 'Empieza por aquÃ­',
            copy: 'Objetos sugeridos o ya usados por el dominio. Suelen ser el mejor punto de partida.',
            className: 'is-priority',
            items: visibleItems.filter(({ item }) => !!(item.isSuggested ?? item.IsSuggested ?? item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed))
        },
        {
            title: 'Ya seleccionadas',
            copy: 'Lo que ya dejaste dentro del perÃ­metro permitido.',
            className: '',
            items: visibleItems.filter(({ item }) => !!item.isSelected && !(item.isSuggested ?? item.IsSuggested ?? item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed))
        },
        {
            title: 'Revisar con cuidado',
            copy: 'Objetos mÃ¡s pesados o con mÃ¡s llaves. Ãšsalos solo si realmente hacen falta.',
            className: 'is-caution',
            items: visibleItems.filter(({ item }) => isRiskyOnboardingCandidate(item) && !item.isSelected && !(item.isSuggested ?? item.IsSuggested ?? item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed))
        },
        {
            title: 'Resto del schema',
            copy: 'Disponible para completar el dominio, pero no deberÃ­a ser el primer impulso seleccionar todo.',
            className: '',
            items: visibleItems.filter(({ item }) => !item.isSelected && !(item.isSuggested ?? item.IsSuggested ?? item.isCurrentlyAllowed ?? item.IsCurrentlyAllowed) && !isRiskyOnboardingCandidate(item))
        }
    ].filter(group => group.items.length > 0);

    list.innerHTML = `<div class="onboarding-schema-groups">${groups.map(group => `
        <section class="onboarding-schema-group ${group.className}">
            <div class="onboarding-schema-group-header">
                <div class="onboarding-schema-group-title">${escHtml(group.title)}</div>
                <div class="onboarding-schema-group-copy">${escHtml(group.copy)}</div>
            </div>
            <div class="onboarding-schema-group-list">
                ${group.items.map(buildCard).join('')}
            </div>
        </section>`).join('')}</div>`;

    if (saveBtn) saveBtn.disabled = selectedCount === 0;
};

renderOnboardingFlowSummary = function () {
    ensureOnboardingSummarySupportUI();
    const state = getOnboardingFlowState();
    setText('txtOnboardingCurrentStep', state.currentStepLabel);
    setText('txtOnboardingRequiredAction', state.requiredAction);
    setText('txtOnboardingCurrentStepHint', state.currentStepHint);
    setText('txtOnboardingNextAction', state.nextAction);

    const meta = document.getElementById('onboardingFlowSummaryMeta');
    if (!meta) return;

    const selectedDomain = getValue('txtOnboardingDomain').trim() || 'sin-domain';
    const selectedConnection = getValue('txtOnboardingConnectionName').trim() || 'sin-conexion';
    const chips = [
        `<span class="meta-chip training-no">${state.progress} / 4</span>`,
        `<span class="meta-chip training-no">${escHtml(selectedDomain)}</span>`,
        `<span class="meta-chip training-no">${escHtml(selectedConnection)}</span>`
    ];

    if (state.hasValidation) {
        chips.unshift('<span class="meta-chip verify-ok">Operativo</span>');
    } else if (state.isInitialized) {
        chips.unshift('<span class="meta-chip verify-pending">Falta validar</span>');
    } else {
        chips.unshift('<span class="meta-chip training-no">En curso</span>');
    }

    meta.innerHTML = chips.join('');

    const prevBtn = document.getElementById('btnOnboardingPrevStep');
    const nextBtn = document.getElementById('btnOnboardingNextStep');
    const activeStepIndex = resolveActiveOnboardingStepIndex();
    const maxUnlocked = getCurrentOnboardingStepIndex();
    if (prevBtn) prevBtn.disabled = activeStepIndex <= 0;
    if (nextBtn) {
        nextBtn.disabled = activeStepIndex >= maxUnlocked;
        nextBtn.textContent = maxUnlocked >= 4 && activeStepIndex >= 3 ? 'Ver cierre' : 'Siguiente';
    }
};

updateOnboardingPanelFocus = function () {
    const state = getOnboardingFlowState();
    const currentPanelId = onboardingWizardPanelIds[resolveActiveOnboardingStepIndex()] || 'onboardingStepPanel1';
    const panelCompletion = {
        onboardingStepPanel1: state.hasWorkspace,
        onboardingStepPanel2b: state.hasAllowed,
        onboardingStepPanel3: state.isInitialized,
        onboardingStepPanel4: state.hasValidation,
        onboardingStepPanel5: state.hasWorkspace && state.hasAllowed && state.isInitialized && state.hasValidation
    };

    Object.entries(panelCompletion).forEach(([panelId, isComplete]) => {
        const panel = document.getElementById(panelId);
        if (!panel) return;
        const isCurrent = panelId === currentPanelId;
        panel.classList.toggle('is-complete', !!isComplete);
        panel.classList.toggle('is-current', isCurrent);
        panel.style.display = isCurrent ? 'block' : 'none';
    });

    const finalBreak = document.getElementById('onboardingFinalBreak');
    if (finalBreak) finalBreak.classList.toggle('is-active', currentPanelId === 'onboardingStepPanel5');

    const supportToggle = document.querySelector('#pane-onboarding .onboarding-support-toggle');
    const supportPanels = document.getElementById('onboardingReferencePanels');
    const showSupport = currentPanelId === 'onboardingStepPanel1';
    supportToggle?.classList.toggle('is-hidden', !showSupport);
    if (supportPanels) {
        supportPanels.classList.toggle('is-hidden', !showSupport || supportPanels.classList.contains('is-collapsed'));
    }

    const finalToolsToggle = document.querySelector('#pane-onboarding .onboarding-final-tools-toggle');
    const finalTools = document.getElementById('onboardingFinalTools');
    const showFinalTools = currentPanelId === 'onboardingStepPanel5' && state.hasValidation;
    finalToolsToggle?.classList.toggle('is-hidden', !showFinalTools);
    if (finalTools) {
        finalTools.classList.toggle('is-hidden', !showFinalTools || finalTools.classList.contains('is-collapsed'));
    }
};

syncOnboardingFooterActions = function () {
    ensureOnboardingFooterBarUI();
    const state = getOnboardingFlowState();
    const footer = document.querySelector('#pane-onboarding .editor-actions');
    const infoInner = document.getElementById('onboardingFooterInfoInner');
    const stepLabel = document.getElementById('onboardingFooterStepLabel');
    const progress = document.getElementById('onboardingFooterProgress');
    const navHost = document.getElementById('onboardingFooterNav');
    const primaryHost = document.getElementById('onboardingFooterPrimary');
    const secondaryHost = document.getElementById('onboardingFooterSecondary');
    if (!footer || !infoInner || !navHost || !primaryHost || !secondaryHost) return;

    const prevBtn = document.getElementById('btnOnboardingPrevStep');
    const nextBtn = document.getElementById('btnOnboardingNextStep');
    [prevBtn, nextBtn].filter(Boolean).forEach(button => {
        if (button.parentElement !== navHost) navHost.appendChild(button);
        button.style.display = 'inline-flex';
        button.classList.add('btn-ghost');
    });

    const primaryButtons = [
        document.getElementById('btnOnboardingSaveStep1'),
        document.getElementById('btnOnboardingSaveAllowedObjects'),
        document.getElementById('btnOnboardingInitialize'),
        document.getElementById('btnOnboardingRunValidation')
    ].filter(Boolean);

    primaryButtons.forEach(button => {
        button.style.display = 'none';
        button.classList.remove('onboarding-primary-cta-active');
        if (button.parentElement === primaryHost) primaryHost.removeChild(button);
    });

    let activeButton = null;
    let footerCopy = state.requiredAction;
    if (!state.hasWorkspace) {
        activeButton = document.getElementById('btnOnboardingSaveStep1');
    } else if (!state.hasAllowed) {
        activeButton = document.getElementById('btnOnboardingSaveAllowedObjects');
        footerCopy = 'Guarda solo las tablas necesarias. Lo demÃ¡s puede esperar.';
    } else if (!state.isInitialized) {
        activeButton = document.getElementById('btnOnboardingInitialize');
        footerCopy = 'Este paso genera el contexto tÃ©cnico mÃ­nimo para que el motor ya pueda intentar una consulta real.';
    } else if (!state.hasValidation) {
        activeButton = document.getElementById('btnOnboardingRunValidation');
        footerCopy = 'La prueba real es el cierre del flujo base. Si sale bien, el dominio ya queda operativo.';
    } else {
        footerCopy = 'Onboarding base completo. Lo avanzado ya es opcional y no bloquea la salida inicial.';
    }

    if (activeButton) {
        primaryHost.appendChild(activeButton);
        activeButton.style.display = 'inline-flex';
        activeButton.classList.add('onboarding-primary-cta-active');
    }

    const newWorkspaceButton = document.querySelector('#pane-onboarding button[onclick="startNewOnboardingWorkspace()"]');
    const exportPackButton = document.getElementById('btnOnboardingExportPack');
    const importPackButton = document.getElementById('btnOnboardingImportPack');
    [newWorkspaceButton, exportPackButton, importPackButton].filter(Boolean).forEach(button => {
        if (button.parentElement !== secondaryHost) secondaryHost.appendChild(button);
        button.classList.add('onboarding-secondary-cta');
    });
    if (newWorkspaceButton) newWorkspaceButton.style.display = 'inline-flex';
    if (exportPackButton) exportPackButton.style.display = state.hasValidation ? 'inline-flex' : 'none';
    if (importPackButton) importPackButton.style.display = state.hasValidation ? 'inline-flex' : 'none';

    infoInner.innerHTML = `<div class="subpanel-title">AcciÃ³n obligatoria ahora</div><div class="subpanel-desc" id="onboardingFooterHint">${escHtml(footerCopy)}</div>`;
    if (stepLabel) stepLabel.textContent = state.currentStepLabel;
    if (progress) progress.textContent = `${state.progress} / 4 completados`;
    footer.style.display = 'grid';
    navHost.style.display = 'flex';
    secondaryHost.style.display = 'flex';
};

const previousEnhanceOnboardingWizardLayoutFinal = enhanceOnboardingWizardLayout;
enhanceOnboardingWizardLayout = function () {
    previousEnhanceOnboardingWizardLayoutFinal();
    ensureOnboardingSummarySupportUI();
    ensureOnboardingSingleStepBriefs();
    renderOnboardingFlowSummary();
    updateOnboardingPanelFocus();
    syncOnboardingFooterActions();
};





function normalizeOnboardingBootstrapList(payload, camelKey, pascalKey) {
    if (Array.isArray(payload?.[camelKey])) return payload[camelKey];
    if (Array.isArray(payload?.[pascalKey])) return payload[pascalKey];
    return [];
}

function normalizeOnboardingRuntimeContext(item) {
    if (!item) return null;
    const tenantKey = String(item.tenantKey || item.TenantKey || '').trim();
    const tenantDisplayName = String(item.tenantDisplayName || item.TenantDisplayName || item.displayName || item.DisplayName || tenantKey).trim();
    const domain = String(item.domain || item.Domain || '').trim();
    const connectionName = String(item.connectionName || item.ConnectionName || '').trim();
    const systemProfileKey = String(item.systemProfileKey || item.SystemProfileKey || 'default').trim() || 'default';
    if (!tenantKey && !domain && !connectionName) return null;
    return {
        ...item,
        tenantKey,
        tenantDisplayName: tenantDisplayName || tenantKey || 'Workspace sin nombre',
        domain,
        connectionName,
        systemProfileKey,
        hasKnownConnection: connectionName ? hasOnboardingConnectionProfile(connectionName) : false
    };
}

function getPersistedOnboardingWorkspaceFallback() {
    const workspace = readOnboardingWorkspaceState();
    if (!workspace || !(workspace.tenantKey || workspace.domain || workspace.connectionName)) return null;
    return {
        tenantKey: workspace.tenantKey || 'workspace-local',
        displayName: workspace.displayName || workspace.tenantKey || 'Borrador local',
        description: workspace.description || '',
        domain: workspace.domain || '',
        connectionName: workspace.connectionName || '',
        systemProfileKey: workspace.systemProfileKey || 'default',
        isDraft: true,
        hasKnownConnection: workspace.connectionName ? hasOnboardingConnectionProfile(workspace.connectionName) : false
    };
}

function getOnboardingWorkspaceEntries() {
    const runtimeContexts = (Array.isArray(globalOnboardingRuntimeContexts) ? globalOnboardingRuntimeContexts : [])
        .map(normalizeOnboardingRuntimeContext)
        .filter(Boolean);
    const tenantMap = new Map();

    (Array.isArray(globalTenants) ? globalTenants : []).forEach(tenant => {
        const tenantKey = String(tenant.tenantKey || tenant.TenantKey || '').trim();
        if (!tenantKey) return;
        tenantMap.set(tenantKey, {
            tenantKey,
            displayName: String(tenant.displayName || tenant.DisplayName || tenantKey).trim() || tenantKey,
            description: String(tenant.description || tenant.Description || '').trim(),
            isActive: !!(tenant.isActive ?? tenant.IsActive ?? true),
            source: 'tenant',
            runtimeContexts: runtimeContexts.filter(ctx => ctx.tenantKey === tenantKey)
        });
    });

    runtimeContexts.forEach(ctx => {
        if (!tenantMap.has(ctx.tenantKey)) {
            tenantMap.set(ctx.tenantKey, {
                tenantKey: ctx.tenantKey,
                displayName: ctx.tenantDisplayName || ctx.tenantKey,
                description: '',
                isActive: true,
                source: 'runtime',
                runtimeContexts: []
            });
        }
        tenantMap.get(ctx.tenantKey).runtimeContexts.push(ctx);
    });

    const entries = Array.from(tenantMap.values()).sort((a, b) => a.displayName.localeCompare(b.displayName));
    const draft = getPersistedOnboardingWorkspaceFallback();
    if (draft && !entries.some(item => item.tenantKey === draft.tenantKey)) {
        entries.unshift({
            tenantKey: draft.tenantKey,
            displayName: draft.displayName,
            description: draft.description || '',
            isActive: true,
            source: 'draft',
            runtimeContexts: draft.domain || draft.connectionName ? [draft] : []
        });
    }

    return { entries, runtimeContexts, draft };
}

function upsertOnboardingStepBrief(panelId, content) {
    const panel = document.getElementById(panelId);
    if (!panel || !content) return;
    let brief = panel.querySelector('.onboarding-step-brief');
    if (!brief) {
        brief = document.createElement('div');
        brief.className = 'onboarding-step-brief';
        const anchor = panel.querySelector('.step-panel-goal') || panel.querySelector('.step-panel-header');
        anchor?.insertAdjacentElement('afterend', brief);
    }
    const rows = [
        ['Qué harás aquí', content.what],
        ['Qué necesitas completar', content.needs],
        ['Qué dejas listo', content.done],
        ['Qué sigue después', content.next]
    ].filter(([, value]) => !!value);
    brief.innerHTML = rows.map(([label, value]) => `
        <div class="onboarding-step-brief-row">
            <span class="onboarding-step-brief-label">${escHtml(label)}</span>
            <span class="onboarding-step-brief-text">${escHtml(value)}</span>
        </div>`).join('');
}

function ensureOnboardingSingleStepBriefs() {
    upsertOnboardingStepBrief('onboardingStepPanel1', {
        what: 'Configurar el workspace, el dominio y la conexión que habilitan todo el onboarding.',
        needs: 'Tenant, nombre visible, dominio semántico y una conexión válida.',
        done: 'El sistema ya sabe en qué base trabajará este dominio.',
        next: 'Elegir solo las tablas o vistas que el motor podrá consultar.'
    });
    upsertOnboardingStepBrief('onboardingStepPanel2b', {
        what: 'Definir el perímetro seguro del dominio con las tablas mínimas necesarias.',
        needs: 'Descubrir schema, buscar objetos y guardar solo los necesarios.',
        done: 'El dominio queda acotado y listo para preparar contexto técnico.',
        next: 'Preparar el dominio para generar schema docs e hints base.'
    });
    upsertOnboardingStepBrief('onboardingStepPanel3', {
        what: 'Generar el contexto técnico mínimo para que el motor pueda responder.',
        needs: 'Tener tablas permitidas ya guardadas y lanzar la preparación del dominio.',
        done: 'Quedan listos schema docs, hints base y contexto suficiente para probar.',
        next: 'Ejecutar una pregunta real y validar el resultado.'
    });
    upsertOnboardingStepBrief('onboardingStepPanel4', {
        what: 'Comprobar con una pregunta real que el dominio ya responde de forma usable.',
        needs: 'Elegir o escribir una pregunta simple y ejecutar la prueba completa.',
        done: 'Sabes si el dominio ya quedó operativo para usuarios internos.',
        next: 'Cerrar el onboarding base y dejar la afinación avanzada como opcional.'
    });
    upsertOnboardingStepBrief('onboardingStepPanel5', {
        what: 'Confirmar si el dominio ya está listo para operar.',
        needs: 'Revisar que workspace, contexto y prueba final ya estén en estado OK.',
        done: 'El flujo base queda cerrado y lo avanzado deja de ser bloqueante.',
        next: 'Exportar, importar o afinar reglas solo si realmente hace falta.'
    });
}

