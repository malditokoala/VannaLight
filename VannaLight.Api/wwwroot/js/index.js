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
            hideResult();
            resetFeedbackPanel();
            updateRuntimeContextState();
        }

        function getContextStorageKey(item) {
            if (!item) return '';
            return [item.tenantKey, item.domain, item.connectionName].join('|');
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

            state.textContent = `Activo: ${currentRuntimeContext.tenantDisplayName || currentRuntimeContext.tenantKey} · ${currentRuntimeContext.domain} · ${currentRuntimeContext.connectionName}`;
        }

        function applyRuntimeContext(item, shouldLog = true) {
            currentRuntimeContext = item || null;

            if (currentRuntimeContext) {
                window.localStorage.setItem(RUNTIME_CONTEXT_STORAGE_KEY, getContextStorageKey(currentRuntimeContext));
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

            const savedKey = window.localStorage.getItem(RUNTIME_CONTEXT_STORAGE_KEY);
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

        // ═══════════════════════════════════════════════════════════
        // SIGNALR
        // ═══════════════════════════════════════════════════════════
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hub/assistant').withAutomaticReconnect().build();

        connection.on('JobStatusUpdated', d => logLine(`⏳ ${d.status}`, 'sys'));
        connection.on('JobFailed', d => { logLine(`❌ ${d.error}`, 'err'); resetUI(); resetFeedbackPanel(); });
        connection.on('JobCompleted', payload => {
            logLine('Completado', 'ok');
            resetUI();

            let data = payload;
            if (payload.resultJson) {
                try { data = JSON.parse(payload.resultJson); } catch { }
            }

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
                return;
            }

            lastRequestMode = currentMode;
            document.getElementById('loadingIcon').style.display = 'block';
            document.getElementById('btnSend').disabled = true;
            hideResult();
            resetFeedbackPanel();
            logLine(`→ ${q}`, 'user');
            lastAskedQuestion = q;
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
            docsCard.textContent = String(answer);
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
                    <thead><tr>${h.map(c => `<th>${c}</th>`).join('')}</tr></thead>
                    <tbody>${data.map(r => `<tr>${h.map(c => `<td>${fmtVal(r[c])}</td>`).join('')}</tr>`).join('')}</tbody>
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
                '<div class="block">',
                '<div class="label">Resultado</div>',
                tableHtml,
                '</div>',
                '</body>',
                '</html>'
            ].join('');
            win.document.write(html);
            win.document.close();
            win.focus();
            win.print();
            logLine('Vista de impresión PDF abierta.', 'ok');
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

        // INIT
        document.getElementById('runtimeContextSelect')?.addEventListener('change', onRuntimeContextChange);
        setMode('sql');
