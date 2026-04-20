// ═══════════════════════════════════════════════════════════
        // MODES
        // ═══════════════════════════════════════════════════════════
        const MODES = {
            sql: { key: 'sql', modeVal: 0, label: 'SQL', sub: 'datos', title: 'MODO DATOS — SQL', desc: 'Consultas sobre bases de datos estructuradas KPI', ph: '¿Cuáles son los 5 números de parte con más scrap?', badge: 'DATOS · SQL', icon: '<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/>' },
            docs: { key: 'docs', modeVal: 1, label: 'PDF', sub: 'documentos', title: 'MODO DOCUMENTOS — PDF', desc: 'Work Instructions y procedimientos de planta', ph: '¿Cuál es el empaque del N/P 421084-0006?', badge: 'DOCUMENTOS · PDF', icon: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/>' },
            pred: { key: 'pred', modeVal: 2, label: 'ML', sub: 'predicción', title: 'MODO PREDICCIÓN — ML.NET', desc: 'Pronósticos de series de negocio por entidad y horizonte', ph: 'Ej.: pronóstico de ventas del producto "SKU-001" para la próxima semana', badge: 'PREDICCIÓN · ML', icon: '<polyline points="22 7 13.5 15.5 8.5 10.5 2 17"/><polyline points="16 7 22 7 22 13"/>' }
        };

        const DEMO_PROMPTS = {
            sql: [
                '¿Qué prensa lleva más scrap en el turno actual?',
                '¿Cuáles son los 5 números de parte con más scrap?',
                'Muéstrame 5 registros de Orders.'
            ],
            docs: [
                '¿Cuál es el empaque del N/P 421084-0006?',
                '¿Qué indica la instrucción para cambio de molde?',
                'Resume el procedimiento principal del documento activo.'
            ],
            pred: [
                '¿Cuál es el pronóstico de scrap del N/P "ABC123" para el cierre de este turno?',
                '¿Cómo viene la tendencia de scrap de la prensa "P01" para mañana?',
                '¿Qué valor proyectado de producción tiene el producto "SKU-001" para el siguiente turno?'
            ]
        };

        function predictionFallbackNeedsEntity(source) {
            return currentMode === 'pred' && source === 'fallback';
        }

        let currentMode = 'sql';
        let lastRequestMode = 'sql';
        let myConnectionId = '';
        let currentChart = null;
        let chartVisible = true;
        let lastCompletedJobId = '';
        let submittedFeedback = null;
        let lastAskedQuestion = '';
        let lastSqlExportState = null;
        let runtimeContexts = [];
        let currentRuntimeContext = null;
        const RUNTIME_CONTEXT_STORAGE_KEY = 'vannalight.runtimeContext';
        const QUERY_HISTORY_STORAGE_KEY = 'vannalight.queryHistory';
        const HISTORY_SIDEBAR_COLLAPSED_KEY = 'vannalight.historySidebarCollapsed';
        const TTS_ENABLED_STORAGE_KEY = 'vannalight.ttsEnabled';
        const LOG_PANEL_COLLAPSED_KEY = 'vannalight.logCollapsed';
        const QUERY_HISTORY_LIMIT = 10;
        let queryHistory = [];
        let activeHistoryEntryId = '';
        let previewHistoryEntryId = '';
        let ttsEnabled = false;
        let userSqlAlertCatalog = null;
        let userSqlAlerts = [];
        let userSqlAlertEvents = [];
        let lastSqlPayload = null;
        let lastSqlAlertSuggestion = null;
        let lastSqlAlertEligibility = null;
        let indexSqlAlertNameTouched = false;

        // datos del último chart para poder redibujar al cambiar tipo
        let lastChartModel = null;   // { labels, values, labelColumn, valueColumn }
        let activeChartType = 'bar';  // tipo actualmente renderizado

        // ═══════════════════════════════════════════════════════════
        // MODE SWITCHING
        // ═══════════════════════════════════════════════════════════
        function setMode(m) {
            currentMode = m;
            const c = MODES[m];
            document.documentElement.style.setProperty('--a', `var(--${m}-c)`);
            ['sql', 'docs', 'pred'].forEach(k =>
                document.getElementById('sb-' + k).className = 'sb-btn' + (k === m ? ' active-' + k : ''));
            document.getElementById('tbModeText').textContent = c.badge;
            document.getElementById('mcTitle').textContent = c.title;
            document.getElementById('mcDesc').textContent = c.desc;
            document.getElementById('mcSvg').innerHTML = c.icon;
            document.getElementById('inpKey').textContent = c.label;
            document.getElementById('inpSub').textContent = c.sub;
            document.getElementById('txtQuestion').placeholder = c.ph;
            logLine(`Modo → ${c.title}`, 'sys');
            renderQueryHistory();
            renderDemoPrompts();
            renderModeHelper();
            renderUserSqlAlerts();
            hideResult();
            resetFeedbackPanel();
            updateRuntimeContextState();
        }

        function renderModeHelper() {
            const helper = document.getElementById('modeHelper');
            const copy = document.getElementById('modeHelperCopy');
            if (!helper || !copy) return;

            if (currentMode !== 'pred') {
                helper.classList.remove('is-visible');
                return;
            }

            helper.classList.add('is-visible');
            copy.textContent = 'ML funciona mejor si defines entidad y horizonte. Usa una plantilla y ajusta los valores entre comillas.';
        }

        function applyModeHelperTemplate(template) {
            const txt = document.getElementById('txtQuestion');
            if (!txt) return;

            const templates = {
                producto: '¿Cuál es el pronóstico de unidades del producto "SKU-001" para la próxima semana?',
                cliente: '¿Cuál es el pronóstico de órdenes del cliente "ALFKI" para mañana?',
                pais: '¿Cómo viene la proyección de ventas para el país "USA" el próximo mes?',
                manana: '¿Cuál es el pronóstico para mañana de la entidad "ABC123"?',
                semana: '¿Cuál es el pronóstico semanal para la entidad "ABC123"?'
            };

            const nextValue = templates[template] || templates.producto;
            txt.value = nextValue;
            txt.focus();
            txt.setSelectionRange(txt.value.length, txt.value.length);
        }

        function getTopContextPrompts(limit = 3) {
            const fallback = DEMO_PROMPTS[currentMode] || [];
            const contextKey = currentRuntimeContext ? getContextStorageKey(currentRuntimeContext) : '';
            const scoped = queryHistory.filter(entry => {
                if ((entry.mode || 'sql') !== currentMode) return false;
                if (!contextKey) return true;
                return (entry.contextKey || '') === contextKey;
            });

            if (!scoped.length) {
                return {
                    prompts: fallback.slice(0, limit),
                    source: 'fallback'
                };
            }

            const buckets = new Map();
            scoped.forEach(entry => {
                const question = String(entry.question || '').trim();
                if (!question) return;

                const key = question.toLowerCase();
                const current = buckets.get(key) || {
                    question,
                    count: 0,
                    bestRowCount: 0,
                    latestTs: 0
                };

                current.count += 1;
                current.bestRowCount = Math.max(current.bestRowCount, Number(entry.rowCount || 0));
                const ts = Date.parse(entry.timestamp || '') || 0;
                current.latestTs = Math.max(current.latestTs, ts);
                buckets.set(key, current);
            });

            const ranked = Array.from(buckets.values())
                .sort((a, b) =>
                    b.count - a.count ||
                    b.bestRowCount - a.bestRowCount ||
                    b.latestTs - a.latestTs)
                .slice(0, limit)
                .map(x => x.question);

            return {
                prompts: ranked.length ? ranked : fallback.slice(0, limit),
                source: ranked.length ? 'history' : 'fallback'
            };
        }

        function renderDemoPrompts() {
            const actions = document.getElementById('demoActions');
            const subtitle = document.getElementById('demoStripSubtitle');
            if (!actions || !subtitle) return;

            const { prompts, source } = getTopContextPrompts(3);
            const label = MODES[currentMode]?.label || currentMode;
            const contextLabel = currentRuntimeContext ? getContextDisplayLabel(currentRuntimeContext) : 'sin contexto seleccionado';
            if (source === 'history') {
                subtitle.textContent = `Top 3 del historial para ${label} en ${contextLabel}. Puedes cargarlas o ejecutarlas directo.`;
            } else if (predictionFallbackNeedsEntity(source)) {
                subtitle.textContent = 'ML necesita una entidad concreta para pronosticar. Carga una sugerencia, reemplaza el ejemplo entre comillas por tu N/P, producto, prensa o cliente, y luego ejecútala.';
            } else {
                subtitle.textContent = `Sugerencias base para ${label}. Aún no hay suficiente historial en este contexto.`;
            }
            actions.innerHTML = '';

            prompts.forEach((prompt, index) => {
                const fillBtn = document.createElement('button');
                fillBtn.type = 'button';
                fillBtn.className = 'demo-chip';
                fillBtn.textContent = prompt;
                fillBtn.onclick = () => loadDemoPrompt(true, index);
                actions.appendChild(fillBtn);
            });

            const runBtn = document.createElement('button');
            runBtn.type = 'button';
            runBtn.className = 'demo-chip is-secondary';
            if (predictionFallbackNeedsEntity(source)) {
                runBtn.textContent = 'Reemplaza la entidad antes de ejecutar';
                runBtn.disabled = true;
                runBtn.title = 'Modo ML requiere una entidad concreta como N/P, producto, prensa o cliente.';
            } else {
                runBtn.textContent = 'Ejecutar sugerencia aleatoria';
                runBtn.onclick = () => loadDemoPrompt(false);
            }
            actions.appendChild(runBtn);
        }

        function loadDemoPrompt(fillOnly = true, specificIndex = null) {
            const prompts = getTopContextPrompts(3).prompts;
            if (!prompts.length) return;

            const questionBox = document.getElementById('txtQuestion');
            if (!questionBox) return;

            const selectedPrompt = Number.isInteger(specificIndex) && prompts[specificIndex]
                ? prompts[specificIndex]
                : prompts[Math.floor(Math.random() * prompts.length)];

            questionBox.value = selectedPrompt;
            questionBox.focus();
            logLine(`Pregunta sugerida cargada para ${MODES[currentMode]?.label || currentMode}.`, 'sys');

            if (!fillOnly) {
                sendQuestion();
            }
        }

        function getContextStorageKey(item) {
            if (!item) return '';
            return [item.tenantKey, item.domain, item.connectionName].join('|');
        }

        function getContextDisplayLabel(item) {
            if (!item) return 'Sin contexto';
            return item.label || [
                item.tenantDisplayName || item.tenantKey,
                item.domain,
                item.connectionName
            ].filter(Boolean).join(' · ');
        }

        function getContextHeroMeta(item) {
            if (!item) return 'Selecciona una base antes de consultar.';
            return `${item.tenantDisplayName || item.tenantKey} / ${item.domain || 'Sin dominio'} / ${item.connectionName || 'Sin conexion'}`;
        }

        function safeStorageGet(key) {
            try {
                return window.localStorage.getItem(key);
            } catch {
                return null;
            }
        }

        function safeStorageSet(key, value) {
            try {
                window.localStorage.setItem(key, value);
            } catch {
                // ignorar restricciones del navegador
            }
        }

        function safeStorageRemove(key) {
            try {
                window.localStorage.removeItem(key);
            } catch {
                // ignorar restricciones del navegador
            }
        }

        function renderRuntimeContextHero(message) {
            return;
        }

        function setRuntimeContextErrorState(isError) {
            document.getElementById('runtimeContextSelect')?.classList.toggle('is-error', !!isError);
        }

        function updateRuntimeContextState(message) {
            const state = document.getElementById('runtimeContextState');
            if (!state) return;

            if (message) {
                state.textContent = message;
                return;
            }

            if (!currentRuntimeContext) {
                state.textContent = currentMode === 'sql'
                    ? 'Selecciona una base antes de consultar.'
                    : 'El contexto seleccionado se reutiliza cuando aplique.';
                return;
            }

            state.textContent = `Activo: ${getContextDisplayLabel(currentRuntimeContext)} · ${getContextHeroMeta(currentRuntimeContext)}`;
        }

        function applyRuntimeContext(item, shouldLog = true) {
            currentRuntimeContext = item || null;
            setRuntimeContextErrorState(false);

            if (currentRuntimeContext) {
                safeStorageSet(RUNTIME_CONTEXT_STORAGE_KEY, getContextStorageKey(currentRuntimeContext));
                if (shouldLog) {
                    logLine(`Contexto activo → ${currentRuntimeContext.label}`, 'sys');
                }
            }

            updateRuntimeContextState();
        }

        function renderRuntimeContextOptions() {
            const select = document.getElementById('runtimeContextSelect');
            if (!select) return;

            select.innerHTML = '';

            if (!runtimeContexts.length) {
                const option = document.createElement('option');
                option.value = '';
                option.textContent = 'No hay contextos disponibles';
                select.appendChild(option);
                select.disabled = true;
                setRuntimeContextErrorState(false);
                applyRuntimeContext(null, false);
                updateRuntimeContextState('No se encontraron contextos activos.');
                return;
            }

            runtimeContexts.forEach(item => {
                const option = document.createElement('option');
                option.value = getContextStorageKey(item);
                option.textContent = item.label || `${item.tenantKey} · ${item.domain}`;
                select.appendChild(option);
            });

            select.disabled = false;
            setRuntimeContextErrorState(false);

            const savedKey = safeStorageGet(RUNTIME_CONTEXT_STORAGE_KEY);
            const initialContext = runtimeContexts.find(item => getContextStorageKey(item) === savedKey)
                || runtimeContexts.find(item => item.isDefault)
                || runtimeContexts[0];

            if (initialContext) {
                select.value = getContextStorageKey(initialContext);
                applyRuntimeContext(initialContext, false);
                return;
            }

            applyRuntimeContext(null, false);
            updateRuntimeContextState('No se pudo resolver un contexto por defecto.');
        }

        function onRuntimeContextChange() {
            const select = document.getElementById('runtimeContextSelect');
            if (!select) return;

            const selected = runtimeContexts.find(item => getContextStorageKey(item) === select.value) || null;
            applyRuntimeContext(selected, true);
            loadUserSqlAlerts();
        }

        async function loadRuntimeContexts() {
            try {
                updateRuntimeContextState('Cargando contextos...');
                const response = await fetch('/api/assistant/contexts');
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                runtimeContexts = await response.json();
                renderRuntimeContextOptions();
                await loadUserSqlAlerts();
            } catch (error) {
                runtimeContexts = [];
                renderRuntimeContextOptions();
                updateRuntimeContextState('No se pudieron cargar los contextos.');
                logLine(`Error cargando contextos: ${error.message}`, 'err');
                renderUserSqlAlerts();
            }
        }

        async function reloadRuntimeContexts() {
            await loadRuntimeContexts();
            logLine('Lista de contextos actualizada.', 'sys');
        }

        function getCurrentSqlAlertContext() {
            if (!currentRuntimeContext) return null;
            return {
                tenantKey: currentRuntimeContext.tenantKey || '',
                tenantDisplayName: currentRuntimeContext.tenantDisplayName || currentRuntimeContext.tenantKey || '',
                domain: currentRuntimeContext.domain || '',
                connectionName: currentRuntimeContext.connectionName || ''
            };
        }

        function getCurrentSqlAlertContextQuery() {
            const context = getCurrentSqlAlertContext();
            if (!context?.tenantKey || !context?.domain || !context?.connectionName) return '';
            return `tenantKey=${encodeURIComponent(context.tenantKey)}&domain=${encodeURIComponent(context.domain)}&connectionName=${encodeURIComponent(context.connectionName)}`;
        }

        function canCreateSqlAlertFromCurrentResult() {
            return currentMode === 'sql'
                && !!currentRuntimeContext
                && !!lastSqlPayload
                && !!lastSqlAlertEligibility?.isAlertable;
        }

        async function safeJson(response) {
            try {
                return await response.json();
            } catch {
                return null;
            }
        }

        function showSqlAlertToast(message, kind = 'warning', title = 'SQL Alert') {
            const host = document.getElementById('sqlAlertToastHost');
            if (!host) return;

            const toast = document.createElement('div');
            toast.className = `sql-alert-toast ${kind === 'ok' ? 'is-ok' : 'is-warning'}`;
            toast.innerHTML = `
                <div class="sql-alert-toast-kicker">${escapeHtml(title)}</div>
                <div class="sql-alert-toast-message">${escapeHtml(message)}</div>`;
            host.appendChild(toast);
            window.setTimeout(() => toast.remove(), 4600);
        }

        function mapAlertStatusClass(rule) {
            const runtimeState = String(rule.runtimeState || rule.RuntimeState || 'Closed').toLowerCase();
            if (runtimeState === 'open') return 'is-open';
            if (runtimeState === 'acknowledged') return 'is-ack';
            return 'is-closed';
        }

        function buildAlertSummary(rule) {
            const metricKey = rule.metricKey || rule.MetricKey || 'metric';
            const operator = rule.comparisonOperatorLabel || rule.ComparisonOperatorLabel || '>';
            const threshold = rule.threshold ?? rule.Threshold ?? 0;
            const dimensionValue = rule.dimensionValue || rule.DimensionValue || '';
            const timeScope = rule.timeScopeLabel || rule.TimeScopeLabel || '';
            const dimensionText = dimensionValue ? ` · ${dimensionValue}` : '';
            return `${metricKey} ${operator} ${threshold}${dimensionText} · ${timeScope}`;
        }

        function renderUserSqlAlerts() {
            const strip = document.getElementById('userAlertsStrip');
            const list = document.getElementById('userAlertsList');
            const eventsList = document.getElementById('userAlertEventsList');
            const count = document.getElementById('userAlertsCount');
            const subtitle = document.getElementById('userAlertsSubtitle');
            const createHint = document.getElementById('userAlertsCreateHint');
            const createButton = document.getElementById('btnCreateAlertFromResult');
            const sqlBadge = document.getElementById('sbSqlAlertBadge');
            const eventsMeta = document.getElementById('userAlertEventsMeta');
            if (!strip || !list || !eventsList || !count || !subtitle) return;

            const context = getCurrentSqlAlertContext();
            const isSqlContext = currentMode === 'sql' && !!context?.tenantKey;
            strip.style.display = isSqlContext ? 'flex' : 'none';

            if (!isSqlContext) {
                if (sqlBadge) sqlBadge.style.display = 'none';
                if (createButton) createButton.style.display = 'none';
                return;
            }

            if (!context) {
                count.textContent = '0';
                subtitle.textContent = 'Selecciona un contexto SQL para trabajar con alertas operativas.';
                if (createHint) createHint.textContent = 'Primero selecciona un contexto SQL.';
                if (createButton) {
                    createButton.style.display = 'inline-flex';
                    createButton.disabled = true;
                    createButton.textContent = 'CREAR ALERTA';
                    createButton.title = 'Primero selecciona un contexto SQL.';
                }
                list.innerHTML = '<div class="user-alert-empty">Selecciona un contexto SQL para ver o crear alertas operativas.</div>';
                eventsList.innerHTML = '<div class="user-alert-empty">Aquí aparecerán disparos, resoluciones y acknowledges del contexto activo.</div>';
                return;
            }

            count.textContent = String(userSqlAlerts.length || 0);
            const openCount = userSqlAlerts.filter(item => String(item.runtimeState || item.RuntimeState || '').toLowerCase() === 'open').length;
            if (sqlBadge) {
                if (openCount > 0) {
                    sqlBadge.textContent = String(openCount);
                    sqlBadge.style.display = 'inline-flex';
                } else {
                    sqlBadge.style.display = 'none';
                }
            }
            subtitle.textContent = `Monitorea ${context.tenantDisplayName} / ${context.domain} sin salir del flujo operativo.`;
            if (createHint) {
                createHint.textContent = canCreateSqlAlertFromCurrentResult()
                    ? 'Ya puedes crear una alerta desde el último resultado SQL validado.'
                    : (lastSqlAlertEligibility?.reason || 'La creación nace desde un resultado SQL válido y usable.');
            }
            if (createButton) {
                createButton.style.display = 'inline-flex';
                createButton.disabled = !canCreateSqlAlertFromCurrentResult();
                createButton.textContent = canCreateSqlAlertFromCurrentResult() ? 'CREAR ALERTA' : 'ALERTA NO DISPONIBLE';
                createButton.title = canCreateSqlAlertFromCurrentResult()
                    ? 'Crear una alerta a partir del último resultado SQL validado.'
                    : (lastSqlAlertEligibility?.reason || 'La creación nace desde un resultado SQL válido y usable.');
            }
            if (eventsMeta) {
                eventsMeta.textContent = userSqlAlertEvents.length
                    ? `${userSqlAlertEvents.length} eventos recientes en este contexto`
                    : 'Sin eventos recientes en este contexto';
            }

            if (!userSqlAlerts.length) {
                list.innerHTML = '<div class="user-alert-empty">No hay alertas activas todavía en este contexto. Cuando valides un resultado SQL apto, podrás convertirlo en alerta desde ese mismo resultado.</div>';
            } else {
                list.innerHTML = userSqlAlerts.slice(0, 4).map(rule => {
                    const id = Number(rule.id || rule.Id || 0);
                    const statusClass = mapAlertStatusClass(rule);
                    const active = !!(rule.isActive || rule.IsActive);
                    const runtimeState = String(rule.runtimeState || rule.RuntimeState || 'Closed');
                    const lastObserved = rule.lastObservedValue ?? rule.LastObservedValue;
                    const lastTriggered = rule.lastTriggeredUtc || rule.LastTriggeredUtc || rule.lastAcknowledgedUtc || rule.LastAcknowledgedUtc || '';

                    return `
                        <div class="user-alert-card ${statusClass} ${active ? '' : 'is-inactive'}">
                            <div class="user-alert-card-top">
                                <div class="user-alert-card-name">${escapeHtml(rule.displayName || rule.DisplayName || 'Alerta SQL')}</div>
                                <span class="user-alert-status ${statusClass}">${escapeHtml(runtimeState)}</span>
                            </div>
                            <div class="user-alert-card-summary">${escapeHtml(buildAlertSummary(rule))}</div>
                            <div class="user-alert-card-meta">${active ? 'Activa' : 'Pausada'}${lastObserved !== null && lastObserved !== undefined ? ` · observado ${escapeHtml(fmtVal(lastObserved))}` : ''}</div>
                            <div class="user-alert-card-time">${lastTriggered ? `Último cambio: ${escapeHtml(lastTriggered)}` : 'Sin eventos recientes'}</div>
                            <div class="user-alert-actions">
                                <button class="user-alert-mini-btn" type="button" onclick="toggleUserSqlAlert(${id}, ${active ? 'false' : 'true'})">${active ? 'PAUSAR' : 'REANUDAR'}</button>
                                <button class="user-alert-mini-btn" type="button" onclick="ackUserSqlAlert(${id})">ACK</button>
                                <button class="user-alert-mini-btn" type="button" onclick="clearUserSqlAlert(${id})">CLEAR</button>
                            </div>
                        </div>`;
                }).join('');
            }

            if (!userSqlAlertEvents.length) {
                eventsList.innerHTML = '<div class="user-alert-empty">Aquí aparecerán disparos, resoluciones y acknowledges del contexto activo.</div>';
            } else {
                eventsList.innerHTML = userSqlAlertEvents.slice(0, 3).map(item => {
                    const eventType = item.eventType || item.EventType || 'Evento';
                    const message = item.message || item.Message || '';
                    const observed = item.observedValue ?? item.ObservedValue;
                    return `
                        <div class="user-alert-event-card">
                            <div class="user-alert-card-top">
                                <div class="user-alert-card-name">${escapeHtml(String(eventType))}</div>
                                <span class="user-alert-status is-closed">${escapeHtml(item.eventUtc || item.EventUtc || '')}</span>
                            </div>
                            <div class="user-alert-card-summary">${escapeHtml(message)}</div>
                            <div class="user-alert-card-meta">${observed !== null && observed !== undefined ? `Observado ${escapeHtml(fmtVal(observed))}` : 'Sin valor observado'}</div>
                        </div>`;
                }).join('');
            }
        }

        async function loadUserSqlAlerts() {
            const context = getCurrentSqlAlertContext();
            if (!context?.tenantKey || !context?.domain || !context?.connectionName) {
                userSqlAlertCatalog = null;
                userSqlAlerts = [];
                userSqlAlertEvents = [];
                renderUserSqlAlerts();
                return;
            }

            try {
                const query = getCurrentSqlAlertContextQuery();
                const [catalogRes, rulesRes, eventsRes] = await Promise.all([
                    fetch(`/api/sql-alerts/catalog?domain=${encodeURIComponent(context.domain)}&connectionName=${encodeURIComponent(context.connectionName)}`),
                    fetch(`/api/sql-alerts?${query}`),
                    fetch(`/api/sql-alerts/events?${query}&limit=12`)
                ]);

                const [catalogBody, rulesBody, eventsBody] = await Promise.all([
                    safeJson(catalogRes),
                    safeJson(rulesRes),
                    safeJson(eventsRes)
                ]);

                if (!catalogRes.ok) throw new Error(catalogBody?.Error || catalogBody?.error || `HTTP ${catalogRes.status}`);
                if (!rulesRes.ok) throw new Error(rulesBody?.Error || rulesBody?.error || `HTTP ${rulesRes.status}`);
                if (!eventsRes.ok) throw new Error(eventsBody?.Error || eventsBody?.error || `HTTP ${eventsRes.status}`);

                userSqlAlertCatalog = catalogBody || null;
                userSqlAlerts = Array.isArray(rulesBody) ? rulesBody : [];
                userSqlAlertEvents = Array.isArray(eventsBody) ? eventsBody : [];
                renderUserSqlAlerts();
            } catch (error) {
                logLine(`Error cargando alertas SQL del contexto: ${error.message}`, 'err');
                userSqlAlerts = [];
                userSqlAlertEvents = [];
                renderUserSqlAlerts();
            }
        }

        function getModeLabel(modeKey) {
            return MODES[modeKey]?.label || String(modeKey || '').toUpperCase();
        }

        function formatHistoryTime(timestamp) {
            if (!timestamp) return 'Sin fecha';
            const date = new Date(timestamp);
            if (Number.isNaN(date.getTime())) return 'Sin fecha';

            const diffMs = Date.now() - date.getTime();
            const diffMinutes = Math.max(0, Math.round(diffMs / 60000));
            if (diffMinutes < 1) return 'Hace un momento';
            if (diffMinutes < 60) return `Hace ${diffMinutes} min`;
            const diffHours = Math.round(diffMinutes / 60);
            if (diffHours < 24) return `Hace ${diffHours} h`;
            return date.toLocaleString('es-MX', {
                month: 'short',
                day: 'numeric',
                hour: '2-digit',
                minute: '2-digit'
            });
        }

        function getHistoryStatusLabel(status) {
            switch (status) {
                case 'completed':
                    return 'completada';
                case 'review':
                    return 'revision';
                case 'failed':
                    return 'fallida';
                default:
                    return 'pendiente';
            }
        }

        function getHistorySignature(entry) {
            return [
                entry.mode || '',
                entry.question || '',
                entry.tenantKey || '',
                entry.domain || '',
                entry.connectionName || ''
            ].join('|').toLowerCase();
        }

        function persistQueryHistory() {
            safeStorageSet(QUERY_HISTORY_STORAGE_KEY, JSON.stringify(queryHistory.slice(0, QUERY_HISTORY_LIMIT)));
        }

        function setHistorySidebarCollapsed(isCollapsed) {
            document.body.classList.toggle('history-collapsed', !!isCollapsed);
            const btn = document.getElementById('historyCollapseBtn');
            if (btn) {
                btn.setAttribute('aria-label', isCollapsed ? 'Expandir historial' : 'Colapsar historial');
                btn.title = isCollapsed ? 'Expandir historial' : 'Colapsar historial';
                btn.setAttribute('aria-expanded', isCollapsed ? 'false' : 'true');
            }
            safeStorageSet(HISTORY_SIDEBAR_COLLAPSED_KEY, isCollapsed ? '1' : '0');
        }

        function toggleHistorySidebar() {
            const next = !document.body.classList.contains('history-collapsed');
            setHistorySidebarCollapsed(next);
        }

        function loadHistorySidebarPreference() {
            setHistorySidebarCollapsed(safeStorageGet(HISTORY_SIDEBAR_COLLAPSED_KEY) === '1');
        }

        function renderQueryHistory() {
            const list = document.getElementById('queryHistoryList');
            const modeLabel = document.getElementById('historySidebarModeLabel');
            if (!list) return;
            if (modeLabel) {
                modeLabel.textContent = `Historial ${getModeLabel(currentMode)} · Top ${QUERY_HISTORY_LIMIT}`;
            }

            list.innerHTML = '';
            const modeHistory = queryHistory.filter(entry => (entry.mode || 'sql') === currentMode);

            if (!modeHistory.length) {
                const empty = document.createElement('div');
                empty.className = 'history-empty';
                empty.textContent = `Aun no hay consultas guardadas para ${getModeLabel(currentMode)} en este navegador.`;
                list.appendChild(empty);
                return;
            }

            modeHistory.forEach(entry => {
                const item = document.createElement('div');
                item.className = 'history-item' + (entry.id === previewHistoryEntryId ? ' is-active' : '');

                const main = document.createElement('div');
                main.className = 'history-main';

                const question = document.createElement('div');
                question.className = 'history-question';
                question.textContent = entry.question || 'Consulta sin texto';

                const meta = document.createElement('div');
                meta.className = 'history-meta';

                const status = document.createElement('span');
                status.className = `history-status ${entry.status || 'pending'}`;
                status.textContent = getHistoryStatusLabel(entry.status);

                const mode = document.createElement('span');
                mode.className = 'history-chip';
                mode.textContent = getModeLabel(entry.mode);

                const context = document.createElement('span');
                context.className = 'history-context';
                context.title = entry.contextLabel || 'Sin contexto';
                context.textContent = entry.contextLabel || 'Sin contexto';

                const time = document.createElement('span');
                time.className = 'history-time';
                time.textContent = formatHistoryTime(entry.timestamp);

                meta.appendChild(status);
                meta.appendChild(mode);
                meta.appendChild(context);
                meta.appendChild(time);

                main.appendChild(question);
                main.appendChild(meta);

                const actions = document.createElement('div');
                actions.className = 'history-item-actions';

                const viewBtn = document.createElement('button');
                viewBtn.type = 'button';
                viewBtn.className = 'history-action-btn';
                viewBtn.textContent = 'ABRIR';
                viewBtn.onclick = () => previewHistoryEntry(entry.id);

                const useBtn = document.createElement('button');
                useBtn.type = 'button';
                useBtn.className = 'history-action-btn';
                useBtn.textContent = 'USAR';
                useBtn.onclick = () => useHistoryEntryAsBase(entry.id);

                const rerunBtn = document.createElement('button');
                rerunBtn.type = 'button';
                rerunBtn.className = 'history-action-btn';
                rerunBtn.textContent = 'REPETIR';
                rerunBtn.onclick = () => rerunHistoryEntry(entry.id);

                actions.appendChild(viewBtn);
                actions.appendChild(useBtn);
                actions.appendChild(rerunBtn);

                item.appendChild(main);
                item.appendChild(actions);
                list.appendChild(item);
            });
        }

        function loadQueryHistory() {
            const raw = safeStorageGet(QUERY_HISTORY_STORAGE_KEY);
            if (!raw) {
                queryHistory = [];
                renderQueryHistory();
                renderDemoPrompts();
                return;
            }

            try {
                const parsed = JSON.parse(raw);
                queryHistory = Array.isArray(parsed) ? parsed.slice(0, QUERY_HISTORY_LIMIT) : [];
            } catch {
                queryHistory = [];
            }

            renderQueryHistory();
            renderDemoPrompts();
        }

        function updateQueryHistoryEntry(entryId, patch) {
            if (!entryId) return;
            const index = queryHistory.findIndex(item => item.id === entryId);
            if (index < 0) return;

            queryHistory[index] = {
                ...queryHistory[index],
                ...patch
            };

            persistQueryHistory();
            renderQueryHistory();
            renderDemoPrompts();
        }

        function beginQueryHistoryEntry(question) {
            const entry = {
                id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
                question: question,
                mode: currentMode,
                timestamp: new Date().toISOString(),
                status: 'pending',
                tenantKey: currentRuntimeContext?.tenantKey || '',
                domain: currentRuntimeContext?.domain || '',
                connectionName: currentRuntimeContext?.connectionName || '',
                contextKey: currentRuntimeContext ? getContextStorageKey(currentRuntimeContext) : '',
                contextLabel: currentRuntimeContext ? getContextDisplayLabel(currentRuntimeContext) : 'Sin contexto',
                sql: '',
                rowCount: 0,
                error: ''
            };

            const signature = getHistorySignature(entry);
            queryHistory = queryHistory.filter(item => getHistorySignature(item) !== signature);
            queryHistory.unshift(entry);
            queryHistory = queryHistory.slice(0, QUERY_HISTORY_LIMIT);
            activeHistoryEntryId = entry.id;
            previewHistoryEntryId = entry.id;
            persistQueryHistory();
            renderQueryHistory();
            renderDemoPrompts();
        }

        function finalizeActiveHistoryEntry(status, patch = {}) {
            if (!activeHistoryEntryId) return;
            updateQueryHistoryEntry(activeHistoryEntryId, {
                status,
                ...patch
            });
            activeHistoryEntryId = '';
        }

        function clearQueryHistory() {
            queryHistory = queryHistory.filter(entry => (entry.mode || 'sql') !== currentMode);
            activeHistoryEntryId = '';
            previewHistoryEntryId = '';
            if (queryHistory.length) {
                persistQueryHistory();
            } else {
                safeStorageRemove(QUERY_HISTORY_STORAGE_KEY);
            }
            renderQueryHistory();
            renderDemoPrompts();
            logLine(`Historial local limpiado para ${getModeLabel(currentMode)}.`, 'sys');
        }

        function previewHistoryEntry(entryId) {
            const entry = queryHistory.find(item => item.id === entryId);
            if (!entry) return;

            previewHistoryEntryId = entry.id;
            renderQueryHistory();

            const summary = entry.error
                ? `Ultimo estado: ${getHistoryStatusLabel(entry.status)}. Error: ${entry.error}`
                : entry.rowCount !== undefined && entry.rowCount !== null
                    ? `Ultimo estado: ${getHistoryStatusLabel(entry.status)}. Filas: ${entry.rowCount}.`
                    : `Ultimo estado: ${getHistoryStatusLabel(entry.status)}.`;

            setQueryStatus('info', 'Vista previa de historial', `${summary} Contexto: ${entry.contextLabel || 'Sin contexto'}.`);
        }

        function reuseHistoryEntry(entryId, shouldRun) {
            const entry = queryHistory.find(item => item.id === entryId);
            if (!entry) return;

            previewHistoryEntryId = entry.id;
            renderQueryHistory();

            if (entry.mode && MODES[entry.mode]) {
                setMode(entry.mode);
            }

            if (entry.mode === 'sql' && entry.contextKey) {
                const select = document.getElementById('runtimeContextSelect');
                const selectedContext = runtimeContexts.find(item => getContextStorageKey(item) === entry.contextKey);
                if (selectedContext && select) {
                    select.value = entry.contextKey;
                    applyRuntimeContext(selectedContext, true);
                } else {
                    applyRuntimeContext(null, false);
                    if (select) {
                        select.value = '';
                    }
                    updateRuntimeContextState('El contexto guardado ya no esta disponible. Revisa la base activa antes de consultar.');
                }
            }

            const txt = document.getElementById('txtQuestion');
            if (txt) {
                txt.value = entry.question || '';
                txt.focus();
                txt.setSelectionRange(txt.value.length, txt.value.length);
            }

            if (shouldRun) {
                sendQuestion();
            }
        }

        function useHistoryEntryAsBase(entryId) {
            reuseHistoryEntry(entryId, false);
        }

        function rerunHistoryEntry(entryId) {
            reuseHistoryEntry(entryId, true);
        }

        function setQueryStatus(type, title, message) {
            const banner = document.getElementById('queryStatusBanner');
            const kicker = document.getElementById('queryStatusKicker');
            const titleNode = document.getElementById('queryStatusTitle');
            const messageNode = document.getElementById('queryStatusMessage');
            if (!banner || !kicker || !titleNode || !messageNode) return;

            banner.className = `query-status-banner is-visible ${type || 'info'}`;
            kicker.textContent = type === 'error'
                ? 'Error de consulta'
                : type === 'warning'
                    ? 'Consulta requiere revision'
                    : type === 'success'
                        ? 'Consulta completada'
                        : 'Estado de consulta';
            titleNode.textContent = title || 'Estado de consulta';
            messageNode.textContent = message || '';
        }

        function clearQueryStatus() {
            const banner = document.getElementById('queryStatusBanner');
            if (!banner) return;
            banner.className = 'query-status-banner';
        }

        function dismissQueryStatus() {
            clearQueryStatus();
        }

        function getPayloadRows(payload) {
            if (Array.isArray(payload?.data)) return payload.data;
            if (Array.isArray(payload?.rows)) return payload.rows;
            return [];
        }

        // ═══════════════════════════════════════════════════════════
        // SIGNALR
        // ═══════════════════════════════════════════════════════════
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hub/assistant').withAutomaticReconnect().build();

        connection.onreconnecting(() => {
            document.getElementById('statusText').textContent = 'Reconectando...';
            const dot = document.getElementById('statusDot');
            if (dot) {
                dot.style.background = '#fbbf24';
                dot.style.boxShadow = '0 0 7px #fbbf24';
            }
            logLine('Reconectando con el servidor...', 'sys');
        });

        connection.onreconnected(() => {
            document.getElementById('statusText').textContent = 'Vanna Neural Active';
            const dot = document.getElementById('statusDot');
            if (dot) {
                dot.style.background = '#4ade80';
                dot.style.boxShadow = '0 0 7px #4ade80';
            }
            logLine('Conexion restablecida.', 'ok');
            loadUserSqlAlerts();
        });

        connection.onclose(() => {
            document.getElementById('statusText').textContent = 'Conexion inactiva';
            const dot = document.getElementById('statusDot');
            if (dot) {
                dot.style.background = '#f87171';
                dot.style.boxShadow = '0 0 7px #f87171';
            }
        });

        connection.on('JobStatusUpdated', d => {
            logLine(`⏳ ${d.status}`, 'sys');
            if (String(d?.status || '').toLowerCase().includes('analy')) {
                setQueryStatus('info', 'Analizando consulta', `Estamos procesando la pregunta en ${getContextDisplayLabel(currentRuntimeContext)}.`);
            }
        });
        connection.on('JobFailed', d => {
            const errorMessage = d?.error || 'No se pudo procesar la consulta.';
            const status = String(d?.status || '').toLowerCase();
            const requiresReview = status.includes('review');
            logLine(`❌ ${errorMessage}`, 'err');
            setQueryStatus(
                requiresReview ? 'warning' : 'error',
                requiresReview ? 'La consulta requiere revision' : 'La consulta fallo',
                errorMessage
            );
            finalizeActiveHistoryEntry(requiresReview ? 'review' : 'failed', {
                error: errorMessage
            });
            resetUI();
            resetFeedbackPanel();
        });
        connection.on('JobCompleted', payload => {
            logLine('Completado', 'ok');
            resetUI();

            let data = payload;
            if (payload.resultJson) {
                try { data = JSON.parse(payload.resultJson); } catch { }
            }

            const rows = getPayloadRows(data);
            finalizeActiveHistoryEntry('completed', {
                sql: data?.sql || data?.sqlText || '',
                rowCount: rows.length,
                error: ''
            });
            clearQueryStatus();

            if (data?.IsPredictionRequest === true || data?.type === 'prediction') {
                renderPred(data);
            } else if (lastRequestMode === 'docs') {
                renderDocs(data);
            } else {
                renderSql(data);
            }

            prepareFeedbackPanel(payload?.JobId || payload?.jobId);
        });

        connection.on('SqlAlertEventRaised', payload => {
            const message = payload?.message || payload?.Message || 'Se disparó una alerta SQL.';
            const eventType = String(payload?.eventType || payload?.EventType || '').toLowerCase();
            logLine(`⚠ ${message}`, eventType === 'resolved' ? 'ok' : 'warn');
            setQueryStatus(
                eventType === 'resolved' ? 'info' : 'warning',
                eventType === 'resolved' ? 'Alerta resuelta' : 'SQL Alert',
                message
            );
            showSqlAlertToast(message, eventType === 'resolved' ? 'ok' : 'warning', eventType === 'resolved' ? 'Alerta resuelta' : 'SQL Alert');
            loadUserSqlAlerts();
        });

        async function startConnection() {
            try {
                await connection.start();
                myConnectionId = connection.connectionId;
                document.getElementById('statusText').textContent = 'Vanna Neural Active';
                const dot = document.getElementById('statusDot');
                if (dot) {
                    dot.style.background = '#4ade80';
                    dot.style.boxShadow = '0 0 7px #4ade80';
                }
                logLine('Sistema listo.', 'ok');
                await loadRuntimeContexts();
            } catch { setTimeout(startConnection, 5000); }
        }
        startConnection();

        // ═══════════════════════════════════════════════════════════
        // SEND
        // ═══════════════════════════════════════════════════════════
        async function sendQuestion() {
            const txt = document.getElementById('txtQuestion');
            const q = txt.value.trim();
            if (!q || !myConnectionId) return;
            if (currentMode === 'sql' && !currentRuntimeContext) {
                logLine('Selecciona una base activa antes de consultar.', 'err');
                setRuntimeContextErrorState(true);
                updateRuntimeContextState('Selecciona una base antes de consultar.');
                setQueryStatus('warning', 'Selecciona una base activa', 'Necesitamos un contexto SQL activo antes de enviar la consulta.');
                return;
            }

            lastRequestMode = currentMode;
            document.getElementById('loadingIcon').style.display = 'block';
            document.getElementById('btnSend').disabled = true;
            hideResult();
            resetFeedbackPanel();
            logLine(`→ ${q}`, 'user');
            lastAskedQuestion = q;
            beginQueryHistoryEntry(q);
            setQueryStatus('info', 'Consulta enviada', `Trabajando en ${currentMode === 'sql' ? getContextDisplayLabel(currentRuntimeContext) : getModeLabel(currentMode)}.`);
            txt.value = '';
            txt.focus();

            try {
                const response = await fetch('/api/assistant/ask', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        question: q,
                        userId: 'user_01',
                        connectionId: myConnectionId,
                        tenantKey: currentRuntimeContext?.tenantKey || null,
                        domain: currentRuntimeContext?.domain || null,
                        connectionName: currentRuntimeContext?.connectionName || null,
                        mode: MODES[currentMode].modeVal
                    })
                });
                if (!response.ok) {
                    const errorText = await response.text();
                    throw new Error(`HTTP ${response.status}: ${errorText || 'No se pudo procesar la solicitud.'}`);
                }
            } catch (e) {
                logLine(`Error red: ${e.message}`, 'err');
                setQueryStatus('error', 'No se pudo enviar la consulta', e.message);
                finalizeActiveHistoryEntry('failed', {
                    error: e.message
                });
                resetUI();
                txt.value = q;
            }
        }

        function handleKeyPress(e) { if (e.key === 'Enter') sendQuestion(); }
        function resetUI() { document.getElementById('loadingIcon').style.display = 'none'; document.getElementById('btnSend').disabled = false; }
        function hideResult() {
            document.getElementById('resultArea').style.display = 'none';
            document.body.classList.remove('has-result');
            const headerActions = document.getElementById('resultHeaderActions');
            if (headerActions) headerActions.style.display = 'none';
            lastSqlPayload = null;
            lastSqlAlertSuggestion = null;
            lastSqlAlertEligibility = null;
            renderUserSqlAlerts();
        }

        function resetFeedbackPanel() {
            lastCompletedJobId = '';
            submittedFeedback = null;

            const panel = document.getElementById('feedbackPanel');
            const status = document.getElementById('feedbackStatus');
            const upBtn = document.getElementById('feedbackUpBtn');
            const downBtn = document.getElementById('feedbackDownBtn');

            if (panel) panel.style.display = 'none';
            if (status) status.textContent = 'Indica si esta respuesta fue correcta o incorrecta.';

            [upBtn, downBtn].forEach(btn => {
                if (!btn) return;
                btn.disabled = false;
                btn.classList.remove('active');
            });
        }

        function prepareFeedbackPanel(jobId) {
            const panel = document.getElementById('feedbackPanel');
            const status = document.getElementById('feedbackStatus');
            const upBtn = document.getElementById('feedbackUpBtn');
            const downBtn = document.getElementById('feedbackDownBtn');

            lastCompletedJobId = typeof jobId === 'string' ? jobId : (jobId ? String(jobId) : '');
            submittedFeedback = null;

            if (!panel || !status || !upBtn || !downBtn) return;

            if (!lastCompletedJobId) {
                panel.style.display = 'none';
                return;
            }

            panel.style.display = 'flex';
            status.textContent = 'Indica si esta respuesta fue correcta o incorrecta.';
            upBtn.disabled = false;
            downBtn.disabled = false;
            upBtn.classList.remove('active');
            downBtn.classList.remove('active');
        }

        async function submitFeedback(feedback) {
            if (!lastCompletedJobId || submittedFeedback) return;

            const status = document.getElementById('feedbackStatus');
            const upBtn = document.getElementById('feedbackUpBtn');
            const downBtn = document.getElementById('feedbackDownBtn');
            if (!status || !upBtn || !downBtn) return;

            upBtn.disabled = true;
            downBtn.disabled = true;
            status.textContent = 'Guardando feedback...';

            try {
                const response = await fetch('/api/assistant/feedback', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        jobId: lastCompletedJobId,
                        feedback: feedback
                    })
                });

                if (!response.ok) {
                    const errorText = await response.text();
                    throw new Error(`HTTP ${response.status}: ${errorText || 'No se pudo guardar el feedback.'}`);
                }

                submittedFeedback = feedback;
                upBtn.classList.toggle('active', feedback === 'Up');
                downBtn.classList.toggle('active', feedback === 'Down');
                status.textContent = feedback === 'Up'
                    ? 'Gracias. Marcaste esta respuesta como correcta.'
                    : 'Gracias. Marcaste esta respuesta para revisión.';
                logLine(`Feedback usuario → ${feedback}`, 'sys');
            } catch (e) {
                status.textContent = 'No se pudo guardar el feedback. Intenta nuevamente.';
                upBtn.disabled = false;
                downBtn.disabled = false;
                logLine(`Error feedback: ${e.message}`, 'err');
            }
        }

        function setIndexSqlAlertPreviewBanner(kind = '', message = '') {
            const banner = document.getElementById('idxSqlAlertPreviewBanner');
            if (!banner) return;
            banner.className = 'sql-alert-preview-banner';
            banner.textContent = '';
            if (!message) return;
            banner.classList.add('is-visible');
            banner.classList.add(kind === 'err' ? 'is-err' : 'is-ok');
            banner.textContent = message;
        }

        function getIndexSqlAlertDimensionLabel(value) {
            const lookup = {
                '': 'Todo',
                part: 'Un número de parte',
                product: 'Un producto',
                customer: 'Un cliente',
                ship_country: 'Un país',
                country: 'Un país',
                press: 'Una prensa',
                department: 'Un departamento',
                shift: 'Un turno',
                category: 'Una categoría',
                employee: 'Un empleado'
            };
            return lookup[String(value || '').toLowerCase()] || 'Un elemento';
        }

        function getIndexSqlAlertDimensionValueLabel(value) {
            const lookup = {
                '': 'Elemento',
                part: 'Número de parte',
                product: 'Producto',
                customer: 'Cliente',
                ship_country: 'País',
                country: 'País',
                press: 'Prensa',
                department: 'Departamento',
                shift: 'Turno',
                category: 'Categoría',
                employee: 'Empleado'
            };
            return lookup[String(value || '').toLowerCase()] || 'Elemento';
        }

        function getIndexSqlAlertOperatorLabel(value) {
            const lookup = {
                '1': 'supera',
                '2': 'llega o supera',
                '3': 'cae por debajo de',
                '4': 'llega o cae por debajo de',
                '5': 'llega a',
                '6': 'cambia respecto a'
            };
            return lookup[String(value || '1')] || 'supera';
        }

        function getIndexSqlAlertPeriodLabel(value) {
            const lookup = {
                '1': 'hoy',
                '2': 'en las últimas 24 horas',
                '3': 'en el turno actual',
                '4': 'en la semana actual',
                '5': 'en el mes actual'
            };
            return lookup[String(value || '1')] || 'hoy';
        }

        function getSelectedOptionText(id) {
            const select = document.getElementById(id);
            if (!select) return '';
            const option = select.options[select.selectedIndex];
            return option ? option.textContent.trim() : '';
        }

        function buildAutoIndexSqlAlertName() {
            const metricLabel = getSelectedOptionText('idxSqlAlertMetricKey') || 'Indicador';
            const applyToKey = document.getElementById('idxSqlAlertDimensionKey')?.value || '';
            const candidateSelect = document.getElementById('idxSqlAlertDimensionCandidate');
            const candidateValue = candidateSelect && candidateSelect.style.display !== 'none'
                ? candidateSelect.value.trim()
                : '';
            const elementLabel = candidateValue || document.getElementById('idxSqlAlertDimensionValue')?.value.trim() || '';
            const periodLabel = getSelectedOptionText('idxSqlAlertTimeScope') || 'Hoy';
            const threshold = document.getElementById('idxSqlAlertThreshold')?.value || '0';
            const operatorLabel = getIndexSqlAlertOperatorLabel(document.getElementById('idxSqlAlertOperator')?.value || '1');
            const shortOperator = operatorLabel.includes('debajo') ? '<' : operatorLabel.includes('llega a') ? '=' : '>';
            const entityText = applyToKey && elementLabel ? ` · ${elementLabel}` : '';
            return `${metricLabel}${entityText} · ${periodLabel} ${shortOperator} ${threshold}`;
        }

        function syncIndexSqlAlertEntityPicker(suggestion = null) {
            const applyToKey = document.getElementById('idxSqlAlertDimensionKey')?.value || '';
            const candidateSelect = document.getElementById('idxSqlAlertDimensionCandidate');
            const manualInput = document.getElementById('idxSqlAlertDimensionValue');
            const manualToggle = document.getElementById('idxSqlAlertManualToggle');
            const picker = document.getElementById('idxSqlAlertEntityPicker');
            const hint = document.getElementById('idxSqlAlertEntityHint');
            if (!candidateSelect || !manualInput || !manualToggle || !picker) return;

            const sourceSuggestion = suggestion || lastSqlAlertSuggestion || {};
            const suggestionApplyTo = sourceSuggestion.dimensionKey || '';
            const candidates = applyToKey && applyToKey === suggestionApplyTo
                ? (Array.isArray(sourceSuggestion.dimensionCandidates) ? sourceSuggestion.dimensionCandidates : [])
                : [];
            const uniqueCandidates = Array.from(new Set(candidates
                .map(item => String(item || '').trim())
                .filter(Boolean)));
            const currentManualValue = manualInput.value.trim();
            const currentCandidateValue = candidateSelect.value.trim();

            picker.classList.toggle('is-guided', !!applyToKey && uniqueCandidates.length > 1);

            if (!applyToKey) {
                candidateSelect.innerHTML = '';
                candidateSelect.style.display = 'none';
                manualToggle.style.display = 'none';
                manualInput.style.display = 'block';
                manualInput.disabled = true;
                manualInput.value = '';
                if (hint) hint.textContent = 'Esta alerta vigilará el indicador a nivel global dentro del contexto activo.';
                return;
            }

            manualInput.disabled = false;

            if (!uniqueCandidates.length) {
                candidateSelect.innerHTML = '';
                candidateSelect.style.display = 'none';
                manualToggle.style.display = 'none';
                manualInput.style.display = 'block';
                if (hint) hint.textContent = 'No detectamos elementos claros en el resultado. Puedes escribir uno manualmente como fallback.';
                return;
            }

            if (uniqueCandidates.length === 1) {
                manualInput.value = currentManualValue || uniqueCandidates[0];
                candidateSelect.innerHTML = '';
                candidateSelect.style.display = 'none';
                manualToggle.style.display = 'none';
                manualInput.style.display = 'block';
                if (hint) hint.textContent = 'Detectamos un único elemento claro en el resultado y ya lo dejamos preseleccionado.';
                return;
            }

            candidateSelect.innerHTML = ['<option value="">Selecciona un elemento detectado en el resultado</option>']
                .concat(uniqueCandidates.map(value => `<option value="${escapeHtml(value)}">${escapeHtml(value)}</option>`))
                .join('');
            candidateSelect.style.display = 'block';
            manualToggle.style.display = 'inline-flex';
            const preferredValue = currentCandidateValue || currentManualValue || uniqueCandidates[0];
            candidateSelect.value = uniqueCandidates.includes(preferredValue) ? preferredValue : '';
            manualInput.style.display = currentManualValue && !uniqueCandidates.includes(currentManualValue) ? 'block' : 'none';
            if (manualInput.style.display === 'none') {
                manualInput.value = '';
            }
            if (hint) hint.textContent = 'Usa primero los elementos detectados en el resultado. Solo si hace falta, escribe otro manualmente.';
        }

        function toggleIndexSqlAlertManualValue() {
            const candidateSelect = document.getElementById('idxSqlAlertDimensionCandidate');
            const manualInput = document.getElementById('idxSqlAlertDimensionValue');
            if (!candidateSelect || !manualInput) return;
            const nextVisible = manualInput.style.display === 'none';
            manualInput.style.display = nextVisible ? 'block' : 'none';
            if (!nextVisible) {
                manualInput.value = '';
            } else {
                manualInput.focus();
            }
            updateIndexSqlAlertSummary();
        }

        function updateIndexSqlAlertSummary() {
            const metricLabel = getSelectedOptionText('idxSqlAlertMetricKey') || 'este indicador';
            const applyToKey = document.getElementById('idxSqlAlertDimensionKey')?.value || '';
            const elementInput = document.getElementById('idxSqlAlertDimensionValue');
            const candidateSelect = document.getElementById('idxSqlAlertDimensionCandidate');
            const candidateValue = candidateSelect && candidateSelect.style.display !== 'none'
                ? candidateSelect.value.trim()
                : '';
            const elementValue = candidateValue || elementInput?.value.trim() || '';
            const operatorLabel = getIndexSqlAlertOperatorLabel(document.getElementById('idxSqlAlertOperator')?.value || '1');
            const threshold = document.getElementById('idxSqlAlertThreshold')?.value || '0';
            const periodLabel = getIndexSqlAlertPeriodLabel(document.getElementById('idxSqlAlertTimeScope')?.value || '1');
            const everyMinutes = document.getElementById('idxSqlAlertFrequency')?.value || '5';
            const summaryNode = document.getElementById('idxSqlAlertSummaryText');
            const dimensionLabelNode = document.getElementById('idxSqlAlertDimensionValueLabel');
            const saveBtn = document.getElementById('idxSqlAlertSaveBtn');

            if (dimensionLabelNode) {
                dimensionLabelNode.textContent = getIndexSqlAlertDimensionValueLabel(applyToKey);
            }
            if (elementInput) {
                const contextualLabel = getIndexSqlAlertDimensionValueLabel(applyToKey);
                elementInput.disabled = !applyToKey;
                elementInput.placeholder = applyToKey ? `${contextualLabel} a vigilar` : 'Todo el contexto';
                if (!applyToKey) {
                    elementInput.value = '';
                }
            }

            syncIndexSqlAlertEntityPicker();

            const scopeText = applyToKey && elementValue
                ? `${metricLabel} de ${elementValue}`
                : metricLabel;
            const sentence = `Te avisaremos si ${scopeText} ${operatorLabel} ${threshold} ${periodLabel}, revisando cada ${everyMinutes} minutos.`;
            if (summaryNode) summaryNode.textContent = sentence;

            if (!indexSqlAlertNameTouched) {
                const nameInput = document.getElementById('idxSqlAlertDisplayName');
                if (nameInput) nameInput.value = buildAutoIndexSqlAlertName();
            }

            if (saveBtn) {
                const isActive = !!document.getElementById('idxSqlAlertIsActive')?.checked;
                saveBtn.textContent = isActive ? 'CREAR Y ACTIVAR ALERTA' : 'GUARDAR ALERTA';
            }
        }

        function toggleIndexSqlAlertAdvanced(forceOpen = null) {
            const body = document.getElementById('idxSqlAlertAdvancedBody');
            const label = document.getElementById('idxSqlAlertAdvancedToggleLabel');
            if (!body || !label) return;
            const nextOpen = forceOpen === null ? body.style.display === 'none' : !!forceOpen;
            body.style.display = nextOpen ? 'block' : 'none';
            label.textContent = nextOpen ? 'Ocultar' : 'Mostrar';
        }

        function toggleIndexSqlAlertPreview(forceOpen = null) {
            const wrap = document.getElementById('idxSqlAlertPreviewWrap');
            if (!wrap) return;
            const nextOpen = forceOpen === null ? wrap.style.display === 'none' : !!forceOpen;
            wrap.style.display = nextOpen ? 'block' : 'none';
        }

        function populateIndexSqlAlertCatalog() {
            const metricSelect = document.getElementById('idxSqlAlertMetricKey');
            const dimensionSelect = document.getElementById('idxSqlAlertDimensionKey');
            if (!metricSelect || !dimensionSelect) return;

            const metrics = Array.isArray(userSqlAlertCatalog?.metrics) ? userSqlAlertCatalog.metrics : (Array.isArray(userSqlAlertCatalog?.Metrics) ? userSqlAlertCatalog.Metrics : []);
            const dimensions = Array.isArray(userSqlAlertCatalog?.dimensions) ? userSqlAlertCatalog.dimensions : (Array.isArray(userSqlAlertCatalog?.Dimensions) ? userSqlAlertCatalog.Dimensions : []);

            metricSelect.innerHTML = metrics.length
                ? metrics.map(item => `<option value="${escapeHtml(item.key || item.Key || '')}">${escapeHtml(item.displayName || item.DisplayName || item.key || item.Key || '')}</option>`).join('')
                : '<option value="">Sin métricas disponibles</option>';

            dimensionSelect.innerHTML = ['<option value="">Todo</option>']
                .concat(dimensions.map(item => {
                    const key = item.key || item.Key || '';
                    return `<option value="${escapeHtml(key)}">${escapeHtml(getIndexSqlAlertDimensionLabel(key))}</option>`;
                }))
                .join('');
        }

        function fillIndexSqlAlertFormFromSuggestion(forceFromResult = false) {
            const context = getCurrentSqlAlertContext();
            const suggestion = (forceFromResult ? lastSqlAlertSuggestion : null) || lastSqlAlertSuggestion || {};
            const pills = document.getElementById('idxSqlAlertContextPills');
            const suggestionStrip = document.getElementById('idxSqlAlertSuggestionStrip');
            const modalSubtitle = document.getElementById('sqlAlertModalSubtitle');
            if (pills) {
                pills.innerHTML = context
                    ? [
                        `<span class="sql-alert-context-pill">${escapeHtml(context.tenantDisplayName || context.tenantKey)}</span>`,
                        `<span class="sql-alert-context-pill">${escapeHtml(context.domain)}</span>`,
                        `<span class="sql-alert-context-pill">${escapeHtml(context.connectionName)}</span>`
                    ].join('')
                    : '<span class="sql-alert-context-pill">Sin contexto activo</span>';
            }
            if (suggestionStrip) {
                const suggestionText = suggestion.summary || '';
                suggestionStrip.style.display = suggestionText ? 'block' : 'none';
                suggestionStrip.textContent = suggestionText;
            }
            if (modalSubtitle) {
                modalSubtitle.textContent = suggestion.summary
                    ? `Tomé como base la última consulta SQL para acelerar la creación. Puedes ajustar la regla antes de guardarla.`
                    : 'Te avisaremos cuando este indicador se salga del rango esperado en este contexto.';
            }

            indexSqlAlertNameTouched = false;
            document.getElementById('idxSqlAlertId').value = '';
            document.getElementById('idxSqlAlertDisplayName').value = suggestion.displayName || '';
            document.getElementById('idxSqlAlertDimensionValue').value = suggestion.dimensionValue || '';
            document.getElementById('idxSqlAlertDimensionCandidate').innerHTML = '';
            document.getElementById('idxSqlAlertDimensionCandidate').style.display = 'none';
            document.getElementById('idxSqlAlertManualToggle').style.display = 'none';
            document.getElementById('idxSqlAlertDimensionValue').style.display = 'block';
            document.getElementById('idxSqlAlertThreshold').value = String(suggestion.threshold ?? 50);
            document.getElementById('idxSqlAlertTimeScope').value = String(suggestion.timeScope ?? 1);
            document.getElementById('idxSqlAlertFrequency').value = String(suggestion.evaluationFrequencyMinutes ?? 5);
            document.getElementById('idxSqlAlertCooldown').value = String(suggestion.cooldownMinutes ?? 30);
            document.getElementById('idxSqlAlertNotes').value = suggestion.notes || '';
            document.getElementById('idxSqlAlertOperator').value = String(suggestion.comparisonOperator ?? 1);
            document.getElementById('idxSqlAlertIsActive').checked = true;
            document.getElementById('idxSqlAlertPreview').value = '';
            setIndexSqlAlertPreviewBanner();
            populateIndexSqlAlertCatalog();
            if (suggestion.metricKey) document.getElementById('idxSqlAlertMetricKey').value = suggestion.metricKey;
            if (suggestion.dimensionKey) document.getElementById('idxSqlAlertDimensionKey').value = suggestion.dimensionKey;
            toggleIndexSqlAlertAdvanced(false);
            toggleIndexSqlAlertPreview(false);
            syncIndexSqlAlertEntityPicker(suggestion);
            updateIndexSqlAlertSummary();
        }

        async function openSqlAlertComposer(forceFromResult = false) {
            const context = getCurrentSqlAlertContext();
            if (!context?.tenantKey || !context?.domain || !context?.connectionName) {
                setRuntimeContextErrorState(true);
                setQueryStatus('warning', 'Selecciona una base activa', 'Necesitamos un contexto SQL activo antes de crear una alerta.');
                return;
            }

            if (!canCreateSqlAlertFromCurrentResult()) {
                setQueryStatus('warning', 'Primero valida un resultado SQL', 'Primero realiza una consulta SQL válida para crear una alerta sobre ese resultado.');
                return;
            }

            if (!userSqlAlertCatalog) {
                await loadUserSqlAlerts();
            }

            const modal = document.getElementById('sqlAlertModal');
            if (!modal) return;
            fillIndexSqlAlertFormFromSuggestion(forceFromResult);
            modal.classList.add('is-open');
            modal.setAttribute('aria-hidden', 'false');
        }

        function closeSqlAlertComposer() {
            const modal = document.getElementById('sqlAlertModal');
            if (!modal) return;
            modal.classList.remove('is-open');
            modal.setAttribute('aria-hidden', 'true');
            toggleIndexSqlAlertAdvanced(false);
            toggleIndexSqlAlertPreview(false);
        }

        function buildIndexSqlAlertPayload() {
            const context = getCurrentSqlAlertContext();
            if (!context?.tenantKey || !context?.domain || !context?.connectionName) {
                throw new Error('No hay un contexto SQL activo para crear la alerta.');
            }

            if (!canCreateSqlAlertFromCurrentResult()) {
                throw new Error('Primero valida un resultado SQL apto para monitoreo antes de crear una alerta.');
            }

            const candidateSelect = document.getElementById('idxSqlAlertDimensionCandidate');
            const manualValue = document.getElementById('idxSqlAlertDimensionValue').value.trim();
            const candidateValue = candidateSelect && candidateSelect.style.display !== 'none'
                ? candidateSelect.value.trim()
                : '';
            const dimensionValue = candidateValue || manualValue || null;

            return {
                id: Number(document.getElementById('idxSqlAlertId').value || '0'),
                ruleKey: null,
                tenantKey: context.tenantKey,
                domain: context.domain,
                connectionName: context.connectionName,
                displayName: document.getElementById('idxSqlAlertDisplayName').value.trim(),
                metricKey: document.getElementById('idxSqlAlertMetricKey').value.trim(),
                dimensionKey: document.getElementById('idxSqlAlertDimensionKey').value.trim() || null,
                dimensionValue,
                comparisonOperator: Number(document.getElementById('idxSqlAlertOperator').value || '1'),
                threshold: Number(document.getElementById('idxSqlAlertThreshold').value || '0'),
                timeScope: Number(document.getElementById('idxSqlAlertTimeScope').value || '1'),
                evaluationFrequencyMinutes: Number(document.getElementById('idxSqlAlertFrequency').value || '5'),
                cooldownMinutes: Number(document.getElementById('idxSqlAlertCooldown').value || '30'),
                isActive: document.getElementById('idxSqlAlertIsActive').checked,
                notes: document.getElementById('idxSqlAlertNotes').value.trim() || null
            };
        }

        async function previewIndexSqlAlert() {
            try {
                const payload = buildIndexSqlAlertPayload();
                const response = await fetch('/api/sql-alerts/preview', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                const body = await safeJson(response);
                if (!response.ok) throw new Error(body?.Error || body?.error || `HTTP ${response.status}`);
                document.getElementById('idxSqlAlertPreview').value = body?.sql || body?.Sql || '';
                setIndexSqlAlertPreviewBanner('ok', body?.summary || body?.Summary || 'Preview SQL generado correctamente.');
            } catch (error) {
                setIndexSqlAlertPreviewBanner('err', String(error.message || error));
            }
        }

        async function saveIndexSqlAlert() {
            const button = document.getElementById('idxSqlAlertSaveBtn');
            try {
                const payload = buildIndexSqlAlertPayload();
                if (!payload.displayName) {
                    payload.displayName = buildAutoIndexSqlAlertName();
                    const displayNameInput = document.getElementById('idxSqlAlertDisplayName');
                    if (displayNameInput) displayNameInput.value = payload.displayName;
                }
                if (!payload.metricKey) throw new Error('Selecciona una métrica gobernada.');

                button.disabled = true;
                const isUpdate = payload.id > 0;
                const response = await fetch(isUpdate ? `/api/sql-alerts/${payload.id}` : '/api/sql-alerts', {
                    method: isUpdate ? 'PUT' : 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                const body = await safeJson(response);
                if (!response.ok) throw new Error(body?.Error || body?.error || `HTTP ${response.status}`);

                closeSqlAlertComposer();
                showSqlAlertToast(payload.displayName || 'Alerta SQL guardada.', 'ok', 'Alerta operativa');
                await loadUserSqlAlerts();
            } catch (error) {
                setIndexSqlAlertPreviewBanner('err', String(error.message || error));
            } finally {
                button.disabled = false;
            }
        }

        async function toggleUserSqlAlert(id, nextIsActive) {
            try {
                const response = await fetch(`/api/sql-alerts/${id}/activate`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ isActive: !!nextIsActive })
                });
                const body = await safeJson(response);
                if (!response.ok) throw new Error(body?.Error || body?.error || `HTTP ${response.status}`);
                await loadUserSqlAlerts();
            } catch (error) {
                showSqlAlertToast(String(error.message || error), 'warning', 'Error de alerta');
            }
        }

        async function ackUserSqlAlert(id) {
            try {
                const response = await fetch(`/api/sql-alerts/${id}/ack`, { method: 'POST' });
                const body = await safeJson(response);
                if (!response.ok) throw new Error(body?.Error || body?.error || `HTTP ${response.status}`);
                await loadUserSqlAlerts();
            } catch (error) {
                showSqlAlertToast(String(error.message || error), 'warning', 'Error de alerta');
            }
        }

        async function clearUserSqlAlert(id) {
            try {
                const response = await fetch(`/api/sql-alerts/${id}/clear`, { method: 'POST' });
                const body = await safeJson(response);
                if (!response.ok) throw new Error(body?.Error || body?.error || `HTTP ${response.status}`);
                await loadUserSqlAlerts();
            } catch (error) {
                showSqlAlertToast(String(error.message || error), 'warning', 'Error de alerta');
            }
        }

        // ═══════════════════════════════════════════════════════════
        // PRE-RENDER CLEANUP
        // ═══════════════════════════════════════════════════════════
        function preRender() {
            document.getElementById('resultArea').style.display = 'flex';
            document.body.classList.add('has-result');
            document.getElementById('kpiContainer').innerHTML = '';
            document.getElementById('chartContainer').style.display = 'none';
            document.getElementById('tableContainer').style.display = 'none';
            document.getElementById('predGrid').style.display = 'none';
            document.getElementById('sqlFooter').style.display = 'none';
            document.getElementById('docsCard').style.display = 'none';
            document.getElementById('sqlContainer').style.display = 'none';
            document.getElementById('exportActions').style.display = 'none';
            document.getElementById('resultHeaderActions').style.display = 'none';

            // reset chart toolbar
            document.getElementById('chartToolbar').style.display = 'none';
            document.getElementById('chartToggleBtn').textContent = 'OCULTAR GRÁFICA';
            chartVisible = true;
            lastChartModel = null;
            lastSqlExportState = null;

            // reset tipo activo a bar
            setActiveChartTypeBtn('bar');
            activeChartType = 'bar';

            if (currentChart) { currentChart.destroy(); currentChart = null; }
        }

        // ═══════════════════════════════════════════════════════════
        // RENDER DOCS
        // ═══════════════════════════════════════════════════════════
        function renderDocs(payload) {
            preRender();
            document.getElementById('resultLabel').textContent = 'RESULTADO — DOCUMENTOS';
            const docsCard = document.getElementById('docsCard');
            docsCard.style.display = 'block';
            const answer =
                payload?.HumanizedMessage || payload?.answer ||
                payload?.text || payload?.message || 'Documento analizado correctamente.';
            const confidence = payload?.confidence ?? payload?.Confidence ?? null;
            const citations = Array.isArray(payload?.citations)
                ? payload.citations
                : Array.isArray(payload?.Citations)
                    ? payload.Citations
                    : [];

            docsCard.innerHTML = `
                <div class="docs-answer-text">${escapeHtml(String(answer))}</div>
                ${confidence !== null && confidence !== undefined ? `<div class="docs-confidence">Confianza estimada: ${escapeHtml(Number(confidence).toFixed(2))}</div>` : ''}
                ${citations.length ? `
                    <div class="docs-citations">
                        ${citations.map(c => `
                            <div class="docs-citation">
                                <div class="docs-citation-head">
                                    <span>${escapeHtml(c.fileName || c.FileName || 'Documento')}</span>
                                    <span>p. ${escapeHtml(c.pageNumber || c.PageNumber || '?')}</span>
                                </div>
                                ${c.section || c.Section ? `<div class="docs-citation-section">${escapeHtml(c.section || c.Section)}</div>` : ''}
                                ${c.snippet || c.Snippet ? `<div class="docs-citation-snippet">${escapeHtml(c.snippet || c.Snippet)}</div>` : ''}
                            </div>`).join('')}
                    </div>` : ''}
            `;
            speakSummary(String(answer));
        }

        // ═══════════════════════════════════════════════════════════
        // RENDER PRED
        // ═══════════════════════════════════════════════════════════
        function renderPred(payload) {
            preRender();
            document.getElementById('resultLabel').textContent = 'RESULTADO — PRONÓSTICO';
            const grid = document.getElementById('predGrid');
            grid.style.display = 'grid';
            const p = payload.data || payload;

            if (p && p.PredictedValue !== undefined) {
                const metricKey = String(p.MetricKey || p.TargetMetricKey || '').trim().toLowerCase();
                const seriesType = String(p.SeriesType || p.GroupByKey || '').trim().toLowerCase();
                const entityName = escapeHtml(String(p.EntityName || 'General'));
                const periodLabel = escapeHtml(String(p.ForecastPeriodLabel || 'Turno'));
                const analysisText = escapeHtml(String(p.HumanizedMessage || payload.explanation || 'Predicción de cierre de turno generada.'));
                const metricMap = {
                    scrap_qty: { category: 'Pronóstico de scrap', unit: 'piezas', baseline: 'Serie temporal observada' },
                    produced_qty: { category: 'Pronóstico de producción', unit: 'piezas', baseline: 'Serie temporal observada' },
                    downtime_minutes: { category: 'Pronóstico de downtime', unit: 'min', baseline: 'Serie temporal observada' },
                    units_sold: { category: 'Pronóstico de unidades', unit: 'unidades', baseline: 'Serie temporal observada' },
                    net_sales: { category: 'Pronóstico de ventas', unit: 'monto', baseline: 'Serie temporal observada' },
                    order_count: { category: 'Pronóstico de órdenes', unit: 'órdenes', baseline: 'Serie temporal observada' }
                };
                const seriesMap = {
                    part: 'Número de parte',
                    product: 'Producto',
                    category: 'Categoría',
                    customer: 'Cliente',
                    ship_country: 'País',
                    employee: 'Empleado',
                    press: 'Prensa',
                    department: 'Departamento'
                };
                const metricInfo = metricMap[metricKey] || { category: 'Pronóstico estimado', unit: 'valor', baseline: 'Serie temporal analizada' };
                const seriesLabel = seriesMap[seriesType] || 'Entidad';
                const historyLabel = p.HistoryShiftsUsed || p.HistoryPointsUsed || p.HistoryBucketsUsed || 0;
                grid.innerHTML = `
                <div class="pred-card">
                    <div class="pred-category">${escapeHtml(metricInfo.category)}</div>
                    <div class="pred-value">${fmtVal(p.PredictedValue)}<span style="font-size:1rem;font-weight:400;margin-left:4px;color:var(--muted)">${escapeHtml(metricInfo.unit)}</span></div>
                    <div class="pred-label">${escapeHtml(seriesLabel)}: <strong style="color:var(--text)">${entityName}</strong><br><span style="color:var(--dim);font-size:.75rem">Horizonte: ${periodLabel}</span></div>
                    <div class="pred-confidence">
                        <div class="pred-bar-bg"><div class="pred-bar-fill" style="width:100%"></div></div>
                        <span class="pred-conf-val">🛡️ ${historyLabel} puntos históricos</span>
                    </div>
                    <div class="pred-trend flat">${escapeHtml(metricInfo.baseline)}</div>
                </div>
                <div style="grid-column:1/-1;margin-top:10px;font-size:.85rem;padding:12px;background:var(--panel);border:1px solid var(--border);border-radius:var(--radius);color:var(--text)">
                    <span style="color:var(--pred-c)">🤖 Análisis:</span> ${analysisText}
                </div>`;
                speakSummary(p.HumanizedMessage || payload.explanation);
            } else {
                grid.innerHTML = `<div style="color:var(--dim);font-family:var(--mono);padding:20px">No se recibieron datos de predicción válidos.</div>`;
            }
        }

        // ═══════════════════════════════════════════════════════════
        // RENDER SQL
        // ═══════════════════════════════════════════════════════════
        function renderSql(payload) {
            preRender();
            document.getElementById('resultLabel').textContent = 'RESULTADO — DATOS';
            document.getElementById('sqlFooter').style.display = 'block';
            const sqlText = payload.sql || payload.sqlText || '';
            document.getElementById('sqlContainer').textContent = sqlText;

            const data = Array.isArray(payload.data)
                ? payload.data
                : Array.isArray(payload.rows)
                    ? payload.rows
                    : [];

            lastSqlExportState = {
                question: lastAskedQuestion,
                sql: sqlText,
                rows: Array.isArray(data) ? data : []
            };
            lastSqlPayload = payload;
            lastSqlAlertSuggestion = inferSqlAlertSuggestion(payload, data);
            lastSqlAlertEligibility = lastSqlAlertSuggestion?.eligibility || null;
            renderUserSqlAlerts();
            updateExportActions(lastSqlExportState);

            if (!data || data.length === 0) {
                document.getElementById('kpiContainer').innerHTML =
                    '<div class="kpi-card"><div class="kpi-label">Resultado</div><div class="kpi-value">Sin datos</div></div>';
                return;
            }

            // Intentar renderizar chart; si hay modelo válido, guardarlo
            const chartRendered = renderChartFromRows(data, activeChartType);

            if (data.length === 1) {
                document.getElementById('kpiContainer').innerHTML = Object.entries(data[0])
                    .map(([k, v]) => `<div class="kpi-card"><div class="kpi-label">${k}</div><div class="kpi-value">${fmtVal(v)}</div></div>`)
                    .join('');
            } else {
                document.getElementById('tableContainer').style.display = 'block';
                const h = Object.keys(data[0]);
                document.getElementById('tableContainer').innerHTML =
                    `<table class="data-table">
                    <thead><tr>${h.map(c => `<th class="${getTableColumnClass(c, data)}" title="${escapeHtml(c)}">${escapeHtml(c)}</th>`).join('')}</tr></thead>
                    <tbody>${data.map(r => `<tr>${h.map(c => renderTableCell(c, r[c], data)).join('')}</tr>`).join('')}</tbody>
                </table>`;
            }

            if (!chartRendered) {
                document.getElementById('chartContainer').style.display = 'none';
                document.getElementById('chartToolbar').style.display = 'none';
            }
        }

        function isCategoricalAlertCandidateColumn(columnName, rows) {
            if (!Array.isArray(rows) || !rows.length) return false;
            if (isMostlyNumericColumn(columnName, rows)) return false;
            if (isTechnicalIdColumn(columnName)) return false;

            const values = rows
                .map(row => row?.[columnName])
                .filter(value => value !== null && value !== undefined)
                .map(value => String(value).trim())
                .filter(Boolean);

            if (!values.length) return false;

            const uniqueCount = new Set(values).size;
            if (uniqueCount <= 1) return false;
            if (uniqueCount > Math.max(20, rows.length + 4)) return false;

            const averageLength = values.reduce((sum, value) => sum + value.length, 0) / values.length;
            return averageLength >= 2 && averageLength <= 48;
        }

        function findBestCategoricalAlertColumn(headers, rows) {
            if (!Array.isArray(headers) || !Array.isArray(rows) || !rows.length) return '';
            const scoringHints = {
                part: ['partnumber', 'part number', 'part_number', 'part no', 'part_no', 'numero parte', 'numero de parte', 'np', 'sku', 'itemcode', 'item code'],
                product: ['product', 'producto', 'sku', 'item', 'itemcode'],
                press: ['press', 'prensa', 'machine'],
                customer: ['customer', 'cliente'],
                ship_country: ['country', 'pais', 'ship country'],
                category: ['category', 'categoria'],
                shift: ['shift', 'turno']
            };

            const candidates = headers
                .filter(header => isCategoricalAlertCandidateColumn(header, rows))
                .map(header => {
                    const normalized = normalizeColumnName(header).replace(/[_\-\s\/]+/g, ' ');
                    let score = 0;
                    Object.values(scoringHints).forEach(hints => {
                        if (hints.some(hint => normalized.includes(hint))) score += 4;
                    });
                    if (normalized.includes('name') || normalized.includes('nombre')) score += 2;
                    if (normalized.includes('part') || normalized.includes('sku') || normalized.includes('item')) score += 3;
                    if (normalized.includes('desc') || normalized.includes('description')) score -= 1;
                    return { header, score };
                })
                .sort((a, b) => b.score - a.score);

            return candidates[0]?.header || '';
        }

        function findMatchingColumnName(headers, dimensionKey) {
            const key = String(dimensionKey || '').toLowerCase();
            if (!key || !Array.isArray(headers)) return '';
            const aliases = {
                press: ['press', 'prensa', 'machine'],
                part: ['part', 'parte', 'partnumber', 'part number', 'part_number', 'part no', 'part_no', 'n/p', 'np', 'numero parte', 'numero_de_parte', 'numero de parte', 'número de parte', 'itemcode', 'item code', 'sku'],
                product: ['product', 'producto', 'item', 'itemcode', 'item code', 'sku'],
                customer: ['customer', 'cliente'],
                ship_country: ['country', 'pais', 'shipcountry', 'ship country'],
                category: ['category', 'categoria'],
                shift: ['shift', 'turno']
            };
            const hints = aliases[key] || [key];
            const match = headers.find(header => {
                const normalized = normalizeColumnName(header).replace(/[_\-\s\/]+/g, ' ');
                return hints.some(hint => normalized.includes(normalizeColumnName(hint).replace(/[_\-\s\/]+/g, ' ')));
            });
            return match || '';
        }

        function extractAlertDimensionCandidates(rows, dimensionKey) {
            if (!Array.isArray(rows) || !rows.length) return [];
            const headers = Object.keys(rows[0] || {});
            const candidateColumn = findMatchingColumnName(headers, dimensionKey) || findBestCategoricalAlertColumn(headers, rows);
            if (!candidateColumn) return [];
            return Array.from(new Set(rows
                .map(row => row?.[candidateColumn])
                .filter(value => value !== null && value !== undefined)
                .map(value => String(value).trim())
                .filter(Boolean)))
                .slice(0, 20);
        }

        function evaluateSqlAlertEligibility(rows, metric, dimensionKey, candidates) {
            const reasonBase = 'La alerta debe nacer de un resultado SQL exitoso, interpretable y útil para monitoreo.';
            if (!currentRuntimeContext?.tenantKey || currentMode !== 'sql') {
                return { isAlertable: false, reason: 'Necesitas un contexto SQL activo para crear una alerta.' };
            }
            if (!lastSqlPayload) {
                return { isAlertable: false, reason: 'Primero realiza una consulta SQL válida para crear una alerta sobre ese resultado.' };
            }
            if (!Array.isArray(rows) || !rows.length) {
                return { isAlertable: false, reason: `${reasonBase} El resultado no devolvió datos monitoreables.` };
            }

            const headers = Object.keys(rows[0] || {});
            const numericHeaders = headers.filter(header => isMostlyNumericColumn(header, rows));
            if (!numericHeaders.length) {
                return { isAlertable: false, reason: `${reasonBase} No encontramos una columna numérica clara para vigilar.` };
            }

            if (rows.length > 50) {
                return { isAlertable: false, reason: `${reasonBase} El resultado es demasiado amplio; conviene refinar la consulta primero.` };
            }

            const categoricalColumn = findMatchingColumnName(headers, dimensionKey) || findBestCategoricalAlertColumn(headers, rows);
            const hasCategoricalColumn = !!categoricalColumn;
            const hasCandidates = Array.isArray(candidates) && candidates.length > 0;
            const isCompactRanking = rows.length >= 2 && rows.length <= 20 && numericHeaders.length >= 1 && hasCategoricalColumn;

            if (!dimensionKey && !hasCategoricalColumn) {
                return {
                    isAlertable: true,
                    reason: metric
                        ? 'Resultado agregado global listo para convertirse en alerta.'
                        : 'Resultado agregado global apto para alerta aunque la métrica todavía no se haya identificado con nombre gobernado.'
                };
            }

            if (hasCandidates) {
                return {
                    isAlertable: true,
                    reason: candidates.length === 1
                        ? (metric ? 'Se detectó una entidad clara para vigilar.' : 'Se detectó una entidad clara y una métrica numérica interpretable para vigilar.')
                        : (metric ? 'Se detectaron varias entidades claras para elegir.' : 'Se detectaron varias entidades claras y una métrica numérica interpretable para elegir.')
                };
            }

            if (isCompactRanking) {
                return {
                    isAlertable: true,
                    reason: metric
                        ? 'Se detectó un ranking por entidad con métrica numérica clara.'
                        : 'Resultado agrupado apto para alerta con una dimensión categórica y una métrica numérica claras.'
                };
            }

            if (!metric && hasCategoricalColumn && numericHeaders.length === 1 && rows.length <= 20) {
                return {
                    isAlertable: true,
                    reason: 'Se detectó un resultado agrupado interpretable que puede vigilarse como alerta operativa.'
                };
            }

            return {
                isAlertable: false,
                reason: metric
                    ? `${reasonBase} No pudimos identificar una entidad o agrupación clara para monitorear.`
                    : `${reasonBase} No detectamos una métrica gobernada ni una agrupación clara suficiente para crear la alerta.`
            };
        }

        function inferSqlAlertSuggestion(payload, rows) {
            const catalogMetrics = Array.isArray(userSqlAlertCatalog?.metrics) ? userSqlAlertCatalog.metrics : (Array.isArray(userSqlAlertCatalog?.Metrics) ? userSqlAlertCatalog.Metrics : []);
            const catalogDimensions = Array.isArray(userSqlAlertCatalog?.dimensions) ? userSqlAlertCatalog.dimensions : (Array.isArray(userSqlAlertCatalog?.Dimensions) ? userSqlAlertCatalog.Dimensions : []);
            const question = String(lastAskedQuestion || '').toLowerCase();
            const sql = String(payload?.sql || payload?.sqlText || '').toLowerCase();
            const headers = Array.isArray(rows) && rows.length ? Object.keys(rows[0]) : [];
            const normalizedHeaders = headers.map(normalizeColumnName);

            const pickMetric = () => {
                const findKey = key => catalogMetrics.find(item => String(item.key || item.Key || '').toLowerCase() === key);
                const metricHints = {
                    scrap_qty: ['scrap', 'rechazo', 'merma'],
                    downtime_minutes: ['downtime', 'paro', 'tiempo muerto'],
                    produced_qty: ['produc', 'piezas', 'unidades', 'cantidad producida'],
                    net_sales: ['sales', 'ventas', 'venta neta', 'net sales', 'importe', 'revenue', 'ingresos'],
                    units_sold: ['units sold', 'unidades vendidas', 'qty sold', 'cantidad vendida'],
                    order_count: ['orders', 'ordenes', 'pedidos', 'order count', 'conteo de ordenes']
                };

                for (const [metricKey, hints] of Object.entries(metricHints)) {
                    if (hints.some(hint => question.includes(hint) || sql.includes(hint) || normalizedHeaders.some(header => header.includes(hint)))) {
                        return findKey(metricKey);
                    }
                }

                const scoredMetrics = catalogMetrics
                    .map(item => {
                        const key = String(item.key || item.Key || '').toLowerCase();
                        const displayName = String(item.displayName || item.DisplayName || key).toLowerCase();
                        const tokens = Array.from(new Set(
                            `${key} ${displayName}`
                                .replace(/[_\-\/]+/g, ' ')
                                .split(/\s+/)
                                .map(token => token.trim())
                                .filter(token => token.length >= 3)
                        ));
                        let score = 0;
                        tokens.forEach(token => {
                            if (question.includes(token)) score += 3;
                            if (sql.includes(token)) score += 2;
                            if (normalizedHeaders.some(header => header.includes(token))) score += 4;
                        });
                        return { item, score };
                    })
                    .filter(entry => entry.score > 0)
                    .sort((a, b) => b.score - a.score);

                if (scoredMetrics.length) {
                    return scoredMetrics[0].item;
                }

                return null;
            };

            const pickDimension = () => {
                const scoring = {
                    press: ['press', 'prensa'],
                    part: ['part', 'parte', 'partnumber', 'numero_de_parte'],
                    product: ['product', 'producto'],
                    customer: ['customer', 'cliente'],
                    ship_country: ['country', 'pais'],
                    category: ['category', 'categoria'],
                    shift: ['shift', 'turno']
                };

                for (const dimension of catalogDimensions) {
                    const key = String(dimension.key || dimension.Key || '').toLowerCase();
                    const hints = scoring[key] || [key];
                    if (hints.some(hint => question.includes(hint) || normalizedHeaders.some(header => header.includes(hint)))) {
                        return dimension;
                    }
                }

                return null;
            };

            const metric = pickMetric();
            const dimension = pickDimension();
            const metricKey = metric ? String(metric.key || metric.Key || '') : '';
            const inferredDimensionKey = dimension ? String(dimension.key || dimension.Key || '') : '';
            const fallbackDimensionColumn = findBestCategoricalAlertColumn(headers, rows);
            const fallbackDimensionKey = normalizedHeaders.some(header => header.includes('part') || header.includes('sku') || header.includes('item'))
                ? 'part'
                : normalizedHeaders.some(header => header.includes('press') || header.includes('prensa'))
                    ? 'press'
                    : normalizedHeaders.some(header => header.includes('product') || header.includes('producto'))
                        ? 'product'
                        : normalizedHeaders.some(header => header.includes('customer') || header.includes('cliente'))
                            ? 'customer'
                            : normalizedHeaders.some(header => header.includes('country') || header.includes('pais'))
                                ? 'ship_country'
                                : normalizedHeaders.some(header => header.includes('category') || header.includes('categoria'))
                                    ? 'category'
                                    : normalizedHeaders.some(header => header.includes('shift') || header.includes('turno'))
                                        ? 'shift'
                                        : '';
            const dimensionKey = inferredDimensionKey || (fallbackDimensionColumn ? fallbackDimensionKey : '');
            const dimensionCandidates = extractAlertDimensionCandidates(rows, dimensionKey);
            const dimensionValue = dimensionCandidates.length === 1 ? dimensionCandidates[0] : '';
            const metricLabel = metric ? String(metric.displayName || metric.DisplayName || metricKey || 'métrica') : 'métrica';
            const scopeLabel = question.includes('turno') ? 'turno actual' : 'periodo actual';
            const eligibility = evaluateSqlAlertEligibility(rows, metric, dimensionKey, dimensionCandidates);

            return {
                displayName: buildSuggestedAlertName(metric, dimensionValue),
                metricKey,
                dimensionKey,
                dimensionValue,
                dimensionCandidates,
                comparisonOperator: 1,
                threshold: metric && String(metric.key || metric.Key || '') === 'downtime_minutes' ? 30 : 50,
                timeScope: question.includes('turno') ? 3 : 1,
                evaluationFrequencyMinutes: 5,
                cooldownMinutes: 30,
                notes: lastAskedQuestion ? `Creada desde la consulta: ${lastAskedQuestion}` : '',
                eligibility,
                summary: dimensionValue
                    ? `Sugerencia detectada: vigilar ${metricLabel} para ${dimensionValue} en ${scopeLabel}.`
                    : dimensionCandidates.length > 1
                        ? `Sugerencia detectada: vigilar ${metricLabel} para una de las entidades del resultado en ${scopeLabel}.`
                    : `Sugerencia detectada: vigilar ${metricLabel} en ${scopeLabel}.`
            };
        }

        function buildSuggestedAlertName(metric, dimensionValue) {
            const metricLabel = metric ? String(metric.displayName || metric.DisplayName || metric.key || metric.Key || 'Métrica') : 'Métrica';
            return dimensionValue
                ? `${metricLabel} de ${dimensionValue} fuera de rango`
                : `${metricLabel} fuera de rango`;
        }

        // ═══════════════════════════════════════════════════════════
        // CHART UTILS
        // ═══════════════════════════════════════════════════════════
        function toNumberOrNull(value) {
            if (typeof value === 'number' && Number.isFinite(value)) return value;
            if (typeof value === 'string') {
                const cleaned = value.replace(/,/g, '').trim();
                if (cleaned === '') return null;
                const n = Number(cleaned);
                if (Number.isFinite(n)) return n;
            }
            return null;
        }

        function normalizeColumnName(name) {
            return String(name || '')
                .normalize('NFD')
                .replace(/[\u0300-\u036f]/g, '')
                .toLowerCase();
        }

        function isTechnicalIdColumn(name) {
            const normalized = normalizeColumnName(name);
            return normalized === 'id'
                || normalized.endsWith('id')
                || normalized.endsWith('_id')
                || normalized.includes('identifier')
                || normalized.includes('folio')
                || normalized.includes('consecutivo')
                || normalized.includes('codigo')
                || normalized.includes('code')
                || normalized.includes('clave')
                || normalized.includes('key');
        }

        function scoreNumericColumn(name, rows) {
            const normalized = normalizeColumnName(name);
            let score = 0;

            const metricHints = [
                'total', 'sum', 'qty', 'quantity', 'count', 'avg', 'average', 'mean',
                'min', 'max', 'amount', 'value', 'metric', 'score', 'rate',
                'scrap', 'production', 'produccion', 'pieces', 'piezas', 'hours', 'horas'
            ];

            const groupingHints = ['turno', 'shift', 'fecha', 'date', 'dia', 'day', 'mes', 'month', 'ano', 'year'];

            if (metricHints.some(hint => normalized.includes(hint))) score += 12;
            if (groupingHints.some(hint => normalized.includes(hint))) score += 3;
            if (isTechnicalIdColumn(name)) score -= 20;

            const numericValues = rows
                .map(r => toNumberOrNull(r[name]))
                .filter(v => v !== null);

            const distinctValues = new Set(numericValues).size;
            if (distinctValues <= 1) score -= 2;
            if (distinctValues < numericValues.length) score += 1;

            return score;
        }

        function scoreLabelColumn(name) {
            const normalized = normalizeColumnName(name);
            let score = 0;

            if (!isTechnicalIdColumn(name)) score += 8;
            if (normalized.includes('name') || normalized.includes('nombre')) score += 6;
            if (normalized.includes('operator') || normalized.includes('operador')) score += 5;
            if (normalized.includes('line') || normalized.includes('linea')) score += 4;
            if (normalized.includes('part') || normalized.includes('parte')) score += 4;
            if (normalized.includes('shift') || normalized.includes('turno')) score += 3;
            if (normalized.includes('date') || normalized.includes('fecha')) score += 3;
            if (normalized.includes('group') || normalized.includes('grupo')) score += 2;
            if (normalized.includes('type') || normalized.includes('tipo')) score += 2;
            if (normalized.includes('status') || normalized.includes('estado')) score += 1;

            return score;
        }

        function buildChartModel(rows) {
            if (!Array.isArray(rows) || rows.length < 2) return null;
            const columns = Object.keys(rows[0] || {});
            if (columns.length < 2) return null;

            const numericColumns = columns.filter(col => rows.some(r => toNumberOrNull(r[col]) !== null));
            if (numericColumns.length === 0) return null;

            const labelCandidates = columns.filter(col => !numericColumns.includes(col));
            const labelColumn = (labelCandidates.length > 0
                ? labelCandidates.slice().sort((a, b) => scoreLabelColumn(b) - scoreLabelColumn(a))[0]
                : columns.find(col => !isTechnicalIdColumn(col)) || columns[0]);

            const rankedNumericColumns = numericColumns
                .filter(col => col !== labelColumn)
                .slice()
                .sort((a, b) => scoreNumericColumn(b, rows) - scoreNumericColumn(a, rows));

            const valueColumn = rankedNumericColumns[0] || numericColumns[0];
            if (!labelColumn || !valueColumn) return null;
            if (isTechnicalIdColumn(valueColumn) && rankedNumericColumns.length === 1) return null;

            const limitedRows = rows.slice(0, 20);
            const labels = limitedRows.map(r => String(r[labelColumn] ?? '—'));
            const values = limitedRows.map(r => toNumberOrNull(r[valueColumn]) ?? 0);
            if (!values.some(v => v !== 0)) return null;

            return { labelColumn, valueColumn, labels, values };
        }

        // paleta de colores para pie/scatter
        const CHART_COLORS = [
            'rgba(34,211,238,.75)', 'rgba(52,211,153,.75)', 'rgba(167,139,250,.75)',
            'rgba(251,191,36,.75)', 'rgba(248,113,113,.75)', 'rgba(96,165,250,.75)',
            'rgba(244,114,182,.75)', 'rgba(34,197,94,.75)', 'rgba(251,146,60,.75)',
            'rgba(129,140,248,.75)'
        ];

        /**
         * Renderiza (o rerenderiza) el chart con el tipo indicado.
         * Si chartModel es null intenta construirlo desde rows.
         */
        function renderChartFromRows(rows, chartType) {
            const model = buildChartModel(rows);
            if (!model) return false;
            lastChartModel = model;
            drawChart(model, chartType);
            return true;
        }

        function drawChart(model, chartType) {
            const chartContainer = document.getElementById('chartContainer');
            const chartToolbar = document.getElementById('chartToolbar');
            const ctx = document.getElementById('liaChart').getContext('2d');

            chartContainer.style.display = 'block';
            chartToolbar.style.display = 'flex';

            if (currentChart) { currentChart.destroy(); currentChart = null; }

            const accentColor = getComputedStyle(document.documentElement)
                .getPropertyValue('--sql-c').trim() || '#22d3ee';

            // Preparar dataset según tipo
            let dataset;
            let chartOptions = {
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: 400 },
                plugins: {
                    legend: { display: chartType === 'pie', labels: { color: '#dde4f0', font: { family: "'IBM Plex Mono'" } } },
                    tooltip: {
                        backgroundColor: '#161b25',
                        borderColor: '#1e2535',
                        borderWidth: 1,
                        titleColor: '#dde4f0',
                        bodyColor: '#7a8a9e',
                        titleFont: { family: "'IBM Plex Mono'", size: 11 },
                        bodyFont: { family: "'IBM Plex Mono'", size: 11 },
                    }
                }
            };

            if (chartType === 'pie') {
                dataset = {
                    label: model.valueColumn,
                    data: model.values,
                    backgroundColor: model.values.map((_, i) => CHART_COLORS[i % CHART_COLORS.length]),
                    borderColor: '#09090c',
                    borderWidth: 2,
                    hoverOffset: 8,
                };
                // pie no usa scales
            } else if (chartType === 'scatter') {
                // scatter necesita {x, y} — mapeamos índice → valor
                dataset = {
                    label: model.valueColumn,
                    data: model.values.map((v, i) => ({ x: i, y: v })),
                    backgroundColor: CHART_COLORS[0],
                    borderColor: CHART_COLORS[0],
                    pointRadius: 5,
                    pointHoverRadius: 7,
                };
                chartOptions.scales = {
                    x: {
                        ticks: { color: '#7a8a9e', callback: (val) => model.labels[val] ?? val },
                        grid: { color: 'rgba(255,255,255,0.06)' }
                    },
                    y: { beginAtZero: true, ticks: { color: '#7a8a9e' }, grid: { color: 'rgba(255,255,255,0.06)' } }
                };
            } else {
                // bar / line
                dataset = {
                    label: model.valueColumn,
                    data: model.values,
                    backgroundColor: chartType === 'bar'
                        ? model.values.map((_, i) => CHART_COLORS[i % CHART_COLORS.length])
                        : 'transparent',
                    borderColor: chartType === 'line' ? CHART_COLORS[0] : undefined,
                    borderWidth: chartType === 'line' ? 2 : 0,
                    borderRadius: chartType === 'bar' ? 4 : 0,
                    pointBackgroundColor: chartType === 'line' ? CHART_COLORS[0] : undefined,
                    pointRadius: chartType === 'line' ? 3 : undefined,
                    tension: chartType === 'line' ? 0.35 : undefined,
                    fill: chartType === 'line'
                        ? { target: 'origin', above: 'rgba(34,211,238,0.06)' }
                        : false,
                };
                chartOptions.scales = {
                    x: { ticks: { color: '#7a8a9e', font: { family: "'IBM Plex Mono'", size: 10 } }, grid: { color: 'rgba(255,255,255,0.06)' } },
                    y: { beginAtZero: true, ticks: { color: '#7a8a9e', font: { family: "'IBM Plex Mono'", size: 10 } }, grid: { color: 'rgba(255,255,255,0.06)' } }
                };
            }

            currentChart = new Chart(ctx, {
                type: chartType === 'scatter' ? 'scatter' : chartType,
                data: {
                    labels: chartType === 'scatter' ? undefined : model.labels,
                    datasets: [dataset]
                },
                options: chartOptions
            });
        }

        // ── Cambio manual de tipo de gráfica ──
        function switchChartType(type) {
            if (!lastChartModel) return;        // no hay datos, ignorar
            activeChartType = type;
            setActiveChartTypeBtn(type);
            drawChart(lastChartModel, type);

            // si el chart estaba oculto, mostrarlo al cambiar tipo
            if (!chartVisible) {
                chartVisible = true;
                document.getElementById('chartContainer').style.display = 'block';
                document.getElementById('chartToggleBtn').textContent = 'OCULTAR GRÁFICA';
            }
        }

        function setActiveChartTypeBtn(type) {
            ['bar', 'line', 'scatter', 'pie'].forEach(t => {
                const btn = document.getElementById('ctype-' + t);
                if (btn) btn.className = 'chart-type-btn' + (t === type ? ' active' : '');
            });
        }

        function toggleChart() {
            chartVisible = !chartVisible;
            document.getElementById('chartContainer').style.display = chartVisible ? 'block' : 'none';
            document.getElementById('chartToggleBtn').textContent = chartVisible ? 'OCULTAR GRÁFICA' : 'VER GRÁFICA';
        }

        function toggleSql() {
            const c = document.getElementById('sqlContainer');
            c.style.display = c.style.display === 'none' ? 'block' : 'none';
        }

        function updateExportActions(state) {
            const wrap = document.getElementById('exportActions');
            if (!wrap) return;

            const hasRows = !!(state && Array.isArray(state.rows) && state.rows.length);
            wrap.style.display = hasRows ? 'flex' : 'none';
        }

        function sanitizeExportFileName(value) {
            return String(value || 'resultado-sql')
                .normalize('NFD')
                .replace(/[\u0300-\u036f]/g, '')
                .replace(/[^a-zA-Z0-9-_]+/g, '-')
                .replace(/-+/g, '-')
                .replace(/^-|-$/g, '')
                .toLowerCase() || 'resultado-sql';
        }

        function escapeHtml(value) {
            return String(value ?? '')
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#39;');
        }

        function downloadBlob(fileName, blob) {
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(url), 1000);
        }

        function buildCsvContent(rows) {
            if (!Array.isArray(rows) || rows.length === 0) return '';

            const headers = Object.keys(rows[0] || {});
            const escapeCsv = value => {
                const text = value == null ? '' : String(value);
                if (/[",\n\r]/.test(text)) {
                    return `"${text.replace(/"/g, '""')}"`;
                }
                return text;
            };

            return '\uFEFF' + [
                headers.map(escapeCsv).join(','),
                ...rows.map(row => headers.map(header => escapeCsv(row[header])).join(','))
            ].join('\r\n');
        }

        function exportCurrentResultCsv() {
            if (!lastSqlExportState?.rows?.length) {
                logLine('No hay datos tabulares para exportar a CSV.', 'err');
                return;
            }

            const baseName = sanitizeExportFileName(lastSqlExportState.question || 'resultado-sql');
            const csv = buildCsvContent(lastSqlExportState.rows);
            downloadBlob(`${baseName}.csv`, new Blob([csv], { type: 'text/csv;charset=utf-8;' }));
            logLine('Resultado exportado a CSV.', 'ok');
        }

        function exportCurrentResultPdf() {
            if (!lastSqlExportState?.rows?.length) {
                logLine('No hay datos tabulares para exportar a PDF.', 'err');
                return;
            }

            const rows = lastSqlExportState.rows;
            const chartImageDataUrl = getCurrentChartSnapshotDataUrl();
            const chartSummary = lastChartModel
                ? `${lastChartModel.labelColumn} vs ${lastChartModel.valueColumn} · ${String(activeChartType || 'bar').toUpperCase()}`
                : '';
            const headers = Object.keys(rows[0] || {});
            const headerHtml = headers
                .map(h => '<th style="border:1px solid #d1d5db;padding:8px;background:#f3f4f6;text-align:left;">' + escapeHtml(h) + '</th>')
                .join('');
            const bodyHtml = rows
                .map(row => {
                    const cells = headers
                        .map(h => '<td style="border:1px solid #e5e7eb;padding:8px;vertical-align:top;">' + escapeHtml(fmtVal(row[h])) + '</td>')
                        .join('');
                    return '<tr>' + cells + '</tr>';
                })
                .join('');
            const tableHtml = [
                '<table style="width:100%;border-collapse:collapse;font-family:Arial,sans-serif;font-size:12px;">',
                '<thead><tr>',
                headerHtml,
                '</tr></thead>',
                '<tbody>',
                bodyHtml,
                '</tbody>',
                '</table>'
            ].join('');

            const win = window.open('', '_blank', 'width=1200,height=900');
            if (!win) {
                logLine('El navegador bloqueó la ventana de impresión.', 'err');
                return;
            }

            const printedAt = new Date().toLocaleString('es-MX');
            const html = [
                '<!doctype html>',
                '<html>',
                '<head>',
                '<meta charset="utf-8" />',
                '<title>Resultado VannaLight</title>',
                '<style>',
                'body { font-family: Arial, sans-serif; margin: 24px; color: #111827; }',
                'h1 { margin: 0 0 8px; font-size: 22px; }',
                '.meta { margin-bottom: 18px; color: #4b5563; font-size: 12px; }',
                '.block { margin-bottom: 20px; }',
                '.label { font-weight: 700; margin-bottom: 6px; font-size: 12px; text-transform: uppercase; color: #2563eb; }',
                'pre { background: #f9fafb; border: 1px solid #e5e7eb; padding: 12px; white-space: pre-wrap; word-break: break-word; }',
                '</style>',
                '</head>',
                '<body>',
                '<h1>Resultado exportado de VannaLight</h1>',
                '<div class="meta">Generado: ' + escapeHtml(printedAt) + ' | Filas: ' + rows.length + '</div>',
                '<div class="block">',
                '<div class="label">Pregunta</div>',
                '<pre>' + escapeHtml(lastSqlExportState.question || 'Sin pregunta registrada') + '</pre>',
                '</div>',
                '<div class="block">',
                '<div class="label">SQL generado</div>',
                '<pre>' + escapeHtml(lastSqlExportState.sql || 'Sin SQL registrado') + '</pre>',
                '</div>',
                chartImageDataUrl ? [
                    '<div class="block">',
                    '<div class="label">Grafica</div>',
                    '<div style="margin-bottom:8px;color:#4b5563;font-size:12px;">' + escapeHtml(chartSummary || 'Visualizacion generada desde el resultado actual.') + '</div>',
                    '<div style="border:1px solid #e5e7eb;border-radius:12px;padding:12px;background:#0b1020;">',
                    '<img src="' + chartImageDataUrl + '" alt="Grafica del resultado" style="display:block;width:100%;height:auto;border-radius:8px;" />',
                    '</div>',
                    '</div>'
                ].join('') : '',
                '<div class="block">',
                '<div class="label">Resultado</div>',
                tableHtml,
                '</div>',
                '<script>',
                'window.addEventListener("load", function () {',
                '  setTimeout(function () { window.focus(); window.print(); }, 320);',
                '});',
                '<\/script>',
                '</body>',
                '</html>'
            ].join('');
            win.document.write(html);
            win.document.close();
            win.focus();
            logLine('Vista de impresión PDF abierta.', 'ok');
        }

        function getCurrentChartSnapshotDataUrl() {
            if (!currentChart || !lastChartModel) {
                return null;
            }

            const canvas = document.getElementById('liaChart');
            if (!canvas) {
                return null;
            }

            try {
                currentChart.update('none');
                return canvas.toDataURL('image/png', 1);
            } catch {
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════
        // LOG
        // ═══════════════════════════════════════════════════════════
        function logLine(msg, type = 'ok') {
            const la = document.getElementById('logArea');
            if (!la) return;
            const div = document.createElement('div');
            div.className = 'log-line';

            const time = document.createElement('span');
            time.className = 'log-time';
            time.textContent = new Date().toLocaleTimeString();

            const text = document.createElement('span');
            text.className = `c-${type}`;
            text.textContent = msg;

            div.appendChild(time);
            div.appendChild(text);
            la.appendChild(div);
            la.scrollTop = la.scrollHeight;
        }

        // ═══════════════════════════════════════════════════════════
        // VOICE
        // ═══════════════════════════════════════════════════════════
        function startDictation() {
            if (!('webkitSpeechRecognition' in window) && !('SpeechRecognition' in window)) {
                logLine('Voz no soportada en este navegador.', 'err'); return;
            }
            const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
            const recognition = new SpeechRecognition();
            recognition.lang = 'es-MX';
            recognition.interimResults = false;
            document.getElementById('btnMic').classList.add('mic-active');
            logLine('Escuchando...', 'sys');

            recognition.onresult = function (event) {
                const transcript = event.results[0][0].transcript;
                document.getElementById('txtQuestion').value = transcript;
                logLine(`Voz detectada: "${transcript}"`, 'ok');
                sendQuestion();
            };
            recognition.onerror = function (event) {
                logLine('Error en dictado: ' + event.error, 'err');
                document.getElementById('btnMic').classList.remove('mic-active');
            };
            recognition.onend = function () { document.getElementById('btnMic').classList.remove('mic-active'); };
            recognition.start();
        }

        function speakSummary(text) {
            if (!ttsEnabled || !window.speechSynthesis || !text) return;
            window.speechSynthesis.cancel();
            const sanitizedText = String(text).split('🤖').join('').replace(/\s+/g, ' ').trim().slice(0, 220);
            if (!sanitizedText) return;
            const u = new SpeechSynthesisUtterance(sanitizedText);
            u.lang = 'es-MX'; u.rate = 1.0;
            window.speechSynthesis.speak(u);
        }

        function updateTtsToggle() {
            const button = document.getElementById('sb-tts');
            if (!button) return;
            button.classList.toggle('is-toggled', !!ttsEnabled);
            button.setAttribute('aria-pressed', ttsEnabled ? 'true' : 'false');
            button.title = ttsEnabled ? 'Desactivar voz automática' : 'Activar voz automática';
        }

        function toggleTts() {
            ttsEnabled = !ttsEnabled;
            safeStorageSet(TTS_ENABLED_STORAGE_KEY, ttsEnabled ? '1' : '0');
            updateTtsToggle();
            logLine(ttsEnabled ? 'Voz automática activada.' : 'Voz automática desactivada.', 'sys');
            if (!ttsEnabled && window.speechSynthesis) {
                window.speechSynthesis.cancel();
            }
        }

        function setLogPanelCollapsed(isCollapsed) {
            const wrap = document.querySelector('.log-wrap');
            const button = document.getElementById('logToggleBtn');
            if (!wrap || !button) return;
            wrap.classList.toggle('is-collapsed', !!isCollapsed);
            button.textContent = isCollapsed ? 'Ver consola' : 'Ocultar consola';
            safeStorageSet(LOG_PANEL_COLLAPSED_KEY, isCollapsed ? '1' : '0');
        }

        function toggleLogPanel() {
            const wrap = document.querySelector('.log-wrap');
            if (!wrap) return;
            setLogPanelCollapsed(!wrap.classList.contains('is-collapsed'));
        }

        function loadChatDisplayPreferences() {
            ttsEnabled = safeStorageGet(TTS_ENABLED_STORAGE_KEY) === '1';
            updateTtsToggle();
            setLogPanelCollapsed(safeStorageGet(LOG_PANEL_COLLAPSED_KEY) !== '0');
        }

        // ═══════════════════════════════════════════════════════════
        // UTILS
        // ═══════════════════════════════════════════════════════════
        function fmtVal(v) {
            if (typeof v === 'number') return v.toLocaleString('es-MX', { maximumFractionDigits: 2 });
            return v ?? '—';
        }

        function isNumericLikeValue(value) {
            return toNumberOrNull(value) !== null;
        }

        function isMostlyNumericColumn(columnName, rows) {
            if (!Array.isArray(rows) || !rows.length) return false;
            const sample = rows.slice(0, 12).map(row => row?.[columnName]);
            const nonEmpty = sample.filter(value => value !== null && value !== undefined && String(value).trim() !== '');
            if (!nonEmpty.length) return false;
            const numericCount = nonEmpty.filter(isNumericLikeValue).length;
            return numericCount / nonEmpty.length >= 0.7;
        }

        function getTableColumnClass(columnName, rows) {
            return isMostlyNumericColumn(columnName, rows) ? 'is-numeric' : '';
        }

        function renderTableCell(columnName, value, rows) {
            const formatted = fmtVal(value);
            const cellClass = isMostlyNumericColumn(columnName, rows) ? 'table-cell is-numeric' : 'table-cell is-text';
            const title = escapeHtml(formatted);
            return `<td title="${title}"><span class="${cellClass}">${escapeHtml(formatted)}</span></td>`;
        }

        // INIT
        loadHistorySidebarPreference();
        loadChatDisplayPreferences();
        loadQueryHistory();
        renderUserSqlAlerts();
        window.toggleHistorySidebar = toggleHistorySidebar;
        document.getElementById('historyCollapseBtn')?.addEventListener('click', (event) => {
            event.preventDefault();
            toggleHistorySidebar();
        });
        document.getElementById('runtimeContextSelect')?.addEventListener('change', onRuntimeContextChange);
        document.getElementById('idxSqlAlertMetricKey')?.addEventListener('change', updateIndexSqlAlertSummary);
        document.getElementById('idxSqlAlertDimensionKey')?.addEventListener('change', updateIndexSqlAlertSummary);
        document.getElementById('idxSqlAlertDimensionCandidate')?.addEventListener('change', updateIndexSqlAlertSummary);
        document.getElementById('idxSqlAlertDimensionValue')?.addEventListener('input', updateIndexSqlAlertSummary);
        document.getElementById('idxSqlAlertOperator')?.addEventListener('change', updateIndexSqlAlertSummary);
        document.getElementById('idxSqlAlertThreshold')?.addEventListener('input', updateIndexSqlAlertSummary);
        document.getElementById('idxSqlAlertTimeScope')?.addEventListener('change', updateIndexSqlAlertSummary);
        document.getElementById('idxSqlAlertFrequency')?.addEventListener('input', updateIndexSqlAlertSummary);
        document.getElementById('idxSqlAlertIsActive')?.addEventListener('change', updateIndexSqlAlertSummary);
        document.getElementById('idxSqlAlertDisplayName')?.addEventListener('input', () => {
            indexSqlAlertNameTouched = true;
        });
        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape') {
                closeSqlAlertComposer();
            }
        });
        setMode('sql');
