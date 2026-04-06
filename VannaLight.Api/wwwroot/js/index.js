// ═══════════════════════════════════════════════════════════
        // MODES
        // ═══════════════════════════════════════════════════════════
        const MODES = {
            sql: { key: 'sql', modeVal: 0, label: 'SQL', sub: 'datos', title: 'MODO DATOS — SQL', desc: 'Consultas sobre bases de datos estructuradas KPI', ph: '¿Cuáles son los 5 números de parte con más scrap?', badge: 'DATOS · SQL', icon: '<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/>' },
            docs: { key: 'docs', modeVal: 1, label: 'PDF', sub: 'documentos', title: 'MODO DOCUMENTOS — PDF', desc: 'Work Instructions y procedimientos de planta', ph: '¿Cuál es el empaque del N/P 421084-0006?', badge: 'DOCUMENTOS · PDF', icon: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/>' },
            pred: { key: 'pred', modeVal: 2, label: 'ML', sub: 'predicción', title: 'MODO PREDICCIÓN — ML.NET', desc: 'Pronósticos de scrap a nivel turno', ph: '¿Cuál es el pronóstico de scrap para el cierre de este turno?', badge: 'PREDICCIÓN · ML', icon: '<polyline points="22 7 13.5 15.5 8.5 10.5 2 17"/><polyline points="16 7 22 7 22 13"/>' }
        };

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
        const QUERY_HISTORY_LIMIT = 10;
        let queryHistory = [];
        let activeHistoryEntryId = '';
        let previewHistoryEntryId = '';

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
            hideResult();
            resetFeedbackPanel();
            updateRuntimeContextState();
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
            const hero = document.getElementById('runtimeContextHero');
            const value = document.getElementById('runtimeContextHeroValue');
            const meta = document.getElementById('runtimeContextHeroMeta');
            if (!hero || !value || !meta) return;

            if (!currentRuntimeContext) {
                hero.classList.add('is-empty');
                value.textContent = 'Sin contexto seleccionado';
                meta.textContent = message || 'Selecciona una base antes de consultar.';
                return;
            }

            hero.classList.remove('is-empty');
            value.textContent = getContextHeroMeta(currentRuntimeContext);
            meta.textContent = message || `Etiqueta de contexto: ${getContextDisplayLabel(currentRuntimeContext)}`;
        }

        function updateRuntimeContextState(message) {
            const state = document.getElementById('runtimeContextState');
            if (!state) return;

            if (message) {
                state.textContent = message;
                renderRuntimeContextHero(message);
                return;
            }

            if (!currentRuntimeContext) {
                state.textContent = currentMode === 'sql'
                    ? 'Selecciona una base antes de consultar.'
                    : 'El contexto seleccionado se reutiliza cuando aplique.';
                renderRuntimeContextHero();
                return;
            }

            state.textContent = `Activo: ${getContextDisplayLabel(currentRuntimeContext)}`;
            renderRuntimeContextHero();
        }

        function applyRuntimeContext(item, shouldLog = true) {
            currentRuntimeContext = item || null;

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
            } catch (error) {
                runtimeContexts = [];
                renderRuntimeContextOptions();
                updateRuntimeContextState('No se pudieron cargar los contextos.');
                logLine(`Error cargando contextos: ${error.message}`, 'err');
            }
        }

        async function reloadRuntimeContexts() {
            await loadRuntimeContexts();
            logLine('Lista de contextos actualizada.', 'sys');
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
                viewBtn.textContent = 'VER';
                viewBtn.onclick = () => previewHistoryEntry(entry.id);

                const useBtn = document.createElement('button');
                useBtn.type = 'button';
                useBtn.className = 'history-action-btn';
                useBtn.textContent = 'USAR COMO BASE';
                useBtn.onclick = () => useHistoryEntryAsBase(entry.id);

                const rerunBtn = document.createElement('button');
                rerunBtn.type = 'button';
                rerunBtn.className = 'history-action-btn is-wide';
                rerunBtn.textContent = 'RE-EJECUTAR';
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
                return;
            }

            try {
                const parsed = JSON.parse(raw);
                queryHistory = Array.isArray(parsed) ? parsed.slice(0, QUERY_HISTORY_LIMIT) : [];
            } catch {
                queryHistory = [];
            }

            renderQueryHistory();
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
            setQueryStatus(
                'success',
                'Consulta completada',
                rows.length
                    ? `Se recuperaron ${rows.length} filas en ${getContextDisplayLabel(currentRuntimeContext)}.`
                    : `La consulta termino correctamente en ${getContextDisplayLabel(currentRuntimeContext)}.`
            );

            if (data?.IsPredictionRequest === true || data?.type === 'prediction') {
                renderPred(data);
            } else if (lastRequestMode === 'docs') {
                renderDocs(data);
            } else {
                renderSql(data);
            }

            prepareFeedbackPanel(payload?.JobId || payload?.jobId);
        });

        async function startConnection() {
            try {
                await connection.start();
                myConnectionId = connection.connectionId;
                document.getElementById('statusText').textContent = 'Vanna Neural Active';
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
        function hideResult() { document.getElementById('resultArea').style.display = 'none'; }

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

        // ═══════════════════════════════════════════════════════════
        // PRE-RENDER CLEANUP
        // ═══════════════════════════════════════════════════════════
        function preRender() {
            document.getElementById('resultArea').style.display = 'flex';
            document.getElementById('kpiContainer').innerHTML = '';
            document.getElementById('chartContainer').style.display = 'none';
            document.getElementById('tableContainer').style.display = 'none';
            document.getElementById('predGrid').style.display = 'none';
            document.getElementById('sqlFooter').style.display = 'none';
            document.getElementById('docsCard').style.display = 'none';
            document.getElementById('sqlContainer').style.display = 'none';
            document.getElementById('exportActions').style.display = 'none';

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
            document.getElementById('resultLabel').textContent = 'RESULTADO — PRONÓSTICO INDUSTRIAL';
            const grid = document.getElementById('predGrid');
            grid.style.display = 'grid';
            const p = payload.data || payload;

            if (p && p.PredictedValue !== undefined) {
                grid.innerHTML = `
                <div class="pred-card">
                    <div class="pred-category">PRONÓSTICO DE SCRAP (CIERRE)</div>
                    <div class="pred-value">${fmtVal(p.PredictedValue)}<span style="font-size:1rem;font-weight:400;margin-left:4px;color:var(--muted)">piezas</span></div>
                    <div class="pred-label">N/P: <strong style="color:var(--text)">${p.EntityName || 'General'}</strong><br><span style="color:var(--dim);font-size:.75rem">Periodo: ${p.ForecastPeriodLabel || 'Turno'}</span></div>
                    <div class="pred-confidence">
                        <div class="pred-bar-bg"><div class="pred-bar-fill" style="width:100%"></div></div>
                        <span class="pred-conf-val">🛡️ ${p.HistoryShiftsUsed || 0} turnos históricos</span>
                    </div>
                    <div class="pred-trend flat">Baseline ML.NET v1.0</div>
                </div>
                <div style="grid-column:1/-1;margin-top:10px;font-size:.85rem;padding:12px;background:var(--panel);border:1px solid var(--border);border-radius:var(--radius);color:var(--text)">
                    <span style="color:var(--pred-c)">🤖 Análisis:</span> ${p.HumanizedMessage || payload.explanation || 'Predicción de cierre de turno generada.'}
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
            if (!window.speechSynthesis || !text) return;
            window.speechSynthesis.cancel();
            const sanitizedText = String(text).split('🤖').join('');
            const u = new SpeechSynthesisUtterance(sanitizedText);
            u.lang = 'es-MX'; u.rate = 1.0;
            window.speechSynthesis.speak(u);
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
        loadQueryHistory();
        window.toggleHistorySidebar = toggleHistorySidebar;
        document.getElementById('historyCollapseBtn')?.addEventListener('click', (event) => {
            event.preventDefault();
            toggleHistorySidebar();
        });
        document.getElementById('runtimeContextSelect')?.addEventListener('change', onRuntimeContextChange);
        setMode('sql');
