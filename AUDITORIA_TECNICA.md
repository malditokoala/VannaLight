# Auditoría Técnica - VannaLight (v6)

**Fecha:** 18 de abril de 2026  
**Versión:** 6.0 - Auditoría de Cierre de Piloto  
**Proyecto:** VannaLight - Asistente Industrial de IA

---

## 1. Resumen Ejecutivo

VannaLight es un asistente de IA industrial que permite consultas SQL en lenguaje natural, búsqueda documental PDF y forecasting de series temporales. Actualmente opera en modo local-first con SQLite y LLMs locales.

### Estado del Proyecto

El piloto está **operativo y demostrable** con las siguientes capacidades:

| Módulo | Estado | Puntuación |
|--------|--------|------------|
| SQL (Text-to-SQL) | ✅ Funcional | 7.5/10 |
| PDF (RAG) | ✅ Funcional | 6.5/10 |
| ML (Forecasting) | ✅ Funcional | 6.5/10 |
| Alertas SQL | ✅ **NUEVO** | 7.0/10 |
| Admin UI | ✅ Funcional | 8.5/10 |
| Cacheo | ✅ Funcional | 7.5/10 |
| Multi-tenancy | ✅ Funcional | 7.5/10 |
| Observabilidad | ✅ Disponible | 7.0/10 |

**Puntuación Global: 7.3/10**

---

## 2. Arquitectura del Sistema

### 2.1 Diagrama de Componentes

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              CLIENTE (Browser)                              │
├─────────────────────────────────────────────────────────────────────────────┤
│  index.html (Chat UI)     │    admin.html (Panel Admin)                      │
│  - Modo SQL              │    - Onboarding Wizard                          │
│  - Modo PDF             │    - System Config                           │
│  - Modo ML             │    - Allowed Objects                        │
│  - Alertas Strip       │    - Business Rules                       │
└─────────────────────────┴───────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         VANNA LIGHT API (ASP.NET Core)                        │
├─────────────────────────────────────────────────────────────────────────────┤
│  Controllers:                                                            │
│  ├── AssistantController.cs    → /api/assistant/* (ask, history, feedback)   │
│  ├── AdminController.cs       → /api/admin/* (config, docs, training)       │
│  ├── SqlAlertsController.cs  → /api/sql-alerts/* (CRUD, ack, clear)    │
│  └── SqlAlertsAdminController.cs → /api/sql-alerts-admin/*              │
├─────────────────────────────────────────────────────────────────────────────┤
│  Use Cases (Core):                                                       │
│  ├── AskUseCase.cs          → Orquestación de consulta SQL                 │
│  ├── TrainExampleUseCase.cs → Guardar ejemplos verificados              │
│  ├── IngestUseCase.cs       → Indexación de documentos PDF                │
│  ├── UpsertSqlAlertRuleUseCase.cs                                      │
│  ├── AcknowledgeSqlAlertUseCase.cs                                   │
│  └── ClearSqlAlertUseCase.cs                                         │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              SERVICIOS (Capa API)                           │
├─────────────────────────────────────────────────────────────────────────────┤
│  SQL Pipeline:                                                           │
│  ├── InferenceWorker.cs      → Background worker para procesamiento            │
│  ├── PatternMatcherService.cs → Matcher de Query Patterns               │
│  ├── TemplateSqlBuilder.cs    → Constructor de SQL declarativo              │
│  ├── StaticSqlValidator.cs  → Validación básica de SQL                  │
│  ├── SqlServerDryRunner.cs → Prueba de SQL antes de ejecución            │
│  ├── SqlCacheService.cs    → Cacheo de resultados                    │
│  └── LocalRetriever.cs    → Retrieval de contexto (hints, docs, examples)│
├─────────────────────────────────────────────────────────────────────────────┤
│  PDF Pipeline:                                                          │
│  ├── DocumentIngestor.cs      → Indexación de PDFs                     │
│  ├── DocsIntentRouterLlm.cs → Router intents PDF                    │
│  ├── DocChunkScorer.cs       → Scoring de chunks RAG                  │
│  ├── DocAnswerComposer.cs   → Composición de respuesta PDF         │
│  └── DocsAnswerService.cs   → Servicio de respuesta PDF              │
├─────────────────────────────────────────────────────────────────────────────┤
│  ML Pipeline:                                                           │
│  ├── PredictionIntentRouterLlm.cs                                    │
│  ├── ForecastingService.cs  → ML.NET forecasting                   │
│  ├── MlModelTrainer.cs      → Entrenamiento de modelos                │
│  ├── IndustrialDomainPackAdapter.cs                                  │
│  └── NorthwindSalesDomainPackAdapter.cs                            │
├─────────────────────────────────────────────────────────────────────────────┤
│  Alerts Pipeline:                                                        │
│  ├── SqlAlertEvaluationWorker.cs → Evaluador en background           │
│  ├── SqlAlertEvaluator.cs  → Evaluaci��n de reglas                   │
│  ├── SqlAlertQueryBuilder.cs → Constructor de queries                │
│  └── SqlAlertMetricCatalog.cs → Catálogo de métricas                  │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         INFRAESTRUCTURA (Data Layer)                        │
├─────────────────────────────────────────────────────────────────────────────┤
│  vanna_memory.db (SQLite):                                               │
│  ├── SystemConfigProfiles / SystemConfigEntries                        │
│  ├── ConnectionProfiles                                             │
│  ├── AppSecrets                                                   │
│  ├── Tenants / TenantDomains                                      │
│  ├── TrainingExamples                                             │
│  ├── SchemaDocs                                                  │
│  ├── SemanticHints                                              │
│  ├── QueryPatterns / QueryPatternTerms                           │
│  ├── BusinessRules                                              │
│  ├── AllowedObjects                                            │
│  ├── DocDocuments / DocChunks                                  │
│  ├── PredictionProfiles                                       │
│  └── SqlAlertRules ← **NUEVO**                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│  vanna_runtime.db (SQLite):                                          │
│  ├── QuestionJobs                                                │
│  ├── LlmRuntimeProfile                                         │
│  ├── SqlAlertStates ← **NUEVO**                                  │
│  └── SqlAlertEvents ← **NUEVO**                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│  SQL Server (Datos del cliente):                                        │
│  └── Bases de datos configuradas por contexto                         │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Flujo de una Consulta SQL

```
Usuario pregunta
       │
       ▼
┌──────────────────┐
│ AssistantController│
│ POST /api/ask   │
└────────┬─────────┘
         │
    ┌────┴────┐
    │ Verifica │
    │ conexión │
    └────┬────┘
         │
         ▼
┌──────────────────┐     ┌───────────���─���─┐
│ SqlCacheService   │────▶│ Hit en cache? │
│ TryGetCached...  │     └───────┬───────┘
└────────┬─────────┘            │
         │              No     │
         ▼              ▼     ▼
┌──────────────────┐     ┌──────────────┐
│   Enqueue async  │     │ Return cached│
│   AskWorkItem    │     │ result       │
└────────┬─────────┘     └──────────────┘
         │
         ▼
┌──────────────────┐
│ InferenceWorker  │ (Background)
│ BackgroundService│
└────────┬─────────┘
         │
    ┌────┴────┐
    │ 1) Route │
    │ Intent  │
    └────┬────┘
         │
         ▼
┌──────────────────┐
│ PatternMatcher    │
│ Service          │
└────────┬─────────┘
         │
    ┌────┴────┐
    │ Pattern  │──────────────┐
    │ found?  │              │
    └────┬────┘              │
         │ No               │ Sí
         ▼                 ▼
┌──────────────────┐   ┌──────────────────┐
│   Retrieval      │   │TemplateSqlBuilder │
│ Docs/Examples    │   │ Build from pattern │
└────────┬─────────┘   └────────┬─────────┘
         │                    │
         ▼                    ▼
┌──────────────────┐   ┌──────────────────┐
│   Build Prompt   │   │   Static Validate │
│ (LLM + context)  │   │   SQL             │
└────────┬─────────┘   └────────┬─────────┘
         │                      │
         ▼                      ▼
┌──────────────────┐   ┌──────────────────┐
│   LLM Client      │   │   Execute on SQL   │
│   InvokeAsync     │   │   Server          │
└────────┬─────────┘   └────────┬─────────┘
         │                      │
    ┌────┴────┐               │
    │Success?│               │
    └────┬────┘               │
     No │                    │
     ▼  ▼                ┌───┴────────┐
┌──────────────────┐    │           │
│  Self-correction  │    │    Return  │
│ (1 retry max)     │    │   result  │
└────────┬─────────┘    │           │
         │            └───────────┘
         ▼
┌──────────────────┐
│   SqlCacheService  │
│   SetCached...    │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│   SignalR Hub    │
│   JobCompleted   │
└──────────────────┘
```

---

## 3. Análisis Detallado por Módulo

### 3.1 Módulo SQL (Text-to-SQL)

**Descripción:** Convierte preguntas en lenguaje natural a consultas SQL válidas para SQL Server.

**Componentes clave:**
- `AssistantController.cs` (líneas 1-265) - Endpoint REST
- `PatternMatcherService.cs` - Matcher de patrones declarativos
- `TemplateSqlBuilder.cs` - Constructor de SQL desde templates
- `StaticSqlValidator.cs` - Validación de SQL peligroso
- `SqlServerDryRunner.cs` - Prueba de SQL antes de ejecutar

**Características implementadas:**

| Característica | Estado | Notas |
|--------------|--------|-------|
| Pattern matching | ✅ | Keywords: "scrap", "producción", "top N" |
| Schema grounding | ✅ | SchemaDocs sembrados para KPIs |
| Semantic hints | ✅ | Columnas: ScrapQty, PartNumber, etc. |
| Self-correction | ✅ | 1 reintento automático |
| Cacheo | ✅ | Por pregunta + contexto |
| Timeout configurable | ✅ | CommandTimeoutSec |
| Validación SQL | ✅ | Bloqueo de DROP, DELETE, etc. |

**Puntuación:** 7.5/10

---

### 3.2 Módulo PDF (RAG)

**Descripción:** Búsqueda en documentos técnicos PDF con retrieval aumentado.

**Componentes clave:**
- `DocumentIngestor.cs` - Indexación de PDFs
- `DocsIntentRouterLlm.cs` - Clasificación de intent
- `DocChunkScorer.cs` - Scoring de relevancia
- `DocAnswerComposer.cs` - Composición de respuesta

**Características implementadas:**

| Característica | Estado | Notas |
|----------------|--------|-------|
| Indexación PDF | ✅ | UglyToad.PdfPig |
| Chunking | ✅ | Por páginas |
| Embeddings | ✅ | Vectorización de texto |
| Retrieval | ✅ | Top-K por similitud |
| Timeout | ✅ | Configurable |
| Citations | ✅ | Página + snippet |

**Puntuación:** 6.5/10

---

### 3.3 Módulo ML (Forecasting)

**Descripción:** Predicción de series temporales usando Microsoft.ML.

**Componentes clave:**
- `ForecastingService.cs` (líneas 1-336) - Motor de forecasting
- `MlModelTrainer.cs` - Entrenamiento de modelos
- `PredictionIntentRouterLlm.cs` - Clasificación de intent
- `IndustrialDomainPackAdapter.cs` - Adapter industrial

**Modelo técnico:**
- **Framework:** Microsoft.ML (FastTree)
- **Features:** Lag1Value, Avg3Value, DayOfWeekIso, BucketKey
- **Horizontes:** EndOfCurrentShift, NextShift, Tomorrow, NextMonth
- **Perfiles:** industrial-scrap-shift, northwind-sales-daily-units

**Nota de terminología:** Usar "pronóstico" en lugar de "predicción" para comunicar incertidumbre.

**Puntuación:** 6.5/10

---

### 3.4 Módulo Alertas SQL (NUEVO)

**Descripción:** Sistema de monitoreo operativo que evalúa reglas SQL periódicamente.

**Componentes clave:**
- `SqlAlertsController.cs` (líneas 1-245) - API REST
- `SqlAlertEvaluationWorker.cs` - Worker background
- `SqlAlertEvaluator.cs` - Evaluación de reglas
- `SqlAlertQueryBuilder.cs` - Constructor de queries

**Arquitectura de tablas:**

```
SqlAlertRules (definición):
├── Id (PK)
├── RuleKey (unique)
├── TenantKey, Domain, ConnectionName (contexto)
├── DisplayName (visible)
├── MetricKey (scrap_qty, produced_qty, etc.)
├── DimensionKey, DimensionValue (opcional)
├── ComparisonOperator (> < >= <= = !=)
├── Threshold (valor numérico)
├── TimeScope (CurrentShift, Today, ThisWeek)
├── EvaluationFrequencyMinutes (cada cuanto evaluar)
├── CooldownMinutes (tiempo entre dispara)
├── IsActive
├── Notes
└── CreatedUtc, UpdatedUtc

SqlAlertStates (estado actual):
├── RuleId (FK)
├── LifecycleState (0=Closed, 1=Triggered, 2=Acknowledged, 3=Resolved)
├── LastObservedValue
├── LastEvaluationUtc
├── LastTriggeredUtc
├── LastAcknowledgedUtc
├── LastResolvedUtc
├── LastErrorUtc, LastErrorMessage

SqlAlertEvents (historial):
├── Id
├── RuleId, RuleKey, TenantKey, Domain, ConnectionName
├── EventType (0=Triggered, 1=Acknowledged, 2=Resolved, 3=Error)
├── LifecycleState
├── ObservedValue, Threshold, ComparisonOperator
├── Message
├── QuerySummary, SqlPreview
├── ErrorText
└── EventUtc
```

**Ciclo de vida de una alerta:**

```
1) Evaluación programada (cada X minutos)
         │
         ▼
   ┌─────────────┐
   │ Ejecuta    │─────────────────────────┐
   │ Query SQL  │                        │
   └────┬──────┘                        │
        │                           │
   ┌────┴────┐                    │
   │ Observed │                    │
   │ > Threshold?                    │
   └────┬────┘                    │
     No │                         │
     ▼  │Sí                      │
   ┌─────────────┐               │
   │ Cooldown?  │               │
   └────┬──────┘               │
     Yes│                         │
     ▼  │No                      │
   ┌─────────────┐               │
   │ New Event  │               │
   │ (Triggered)│               │
   └─────────────┘               │
         │                       │
         ▼                       ▼
   ┌─────────────────────────────┐
   │      SignalR / UI           │ NOTIFICAR
   └─────────────────────────────┘
         │
         ▼
   ──── Estado: Triggered ────
   
   Usuario ve la alerta
         │
         ▼
   [Acknowledge] o [Clear]
         │
         ▼
   Event: Acknowledged/Resolved
   Estado: Closed
```

**API Endpoints:**

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/sql-alerts` | Listar alertas del contexto |
| GET | `/api/sql-alerts/catalog` | Catálogo de métricas disponibles |
| GET | `/api/sql-alerts/events` | Historial de eventos |
| POST | `/api/sql-alerts` | Crear nueva alerta |
| PUT | `/api/sql-alerts/{id}` | Actualizar alerta |
| POST | `/api/sql-alerts/preview` | Previsualizar query |
| POST | `/api/sql-alerts/{id}/activate` | Activar/desactivar |
| POST | `/api/sql-alerts/{id}/ack` | Acknowledgegear |
| POST | `/api/sql-alerts/{id}/clear` | Clear y cerrar |

**UI integrada:** index.html líneas ~1984-2008 (user-alerts-strip)

**Puntuación:** 7.0/10

---

### 3.5 Admin UI

**Descripción:** Panel administrativo para configuración del sistema.

**Tabs:**

| Tab | Funcionalidad | Estado |
|-----|--------------|--------|
| Workspaces | Gestión de tenants | ✅ |
| Onboarding | Wizard 4 pasos | ✅ |
| System Config | Configuración operativa | ✅ |
| Allowed Objects | Tablas permitidas por dominio | ✅ |
| Business Rules | Reglas de negocio | ✅ |
| Semantic Hints | Hints semánticos | ✅ |
| Query Patterns | Templates SQL | ✅ |

**Puntuación:** 8.5/10

---

## 4. Datos de Configuración

### 4.1 Tablas de Configuración

**System Config (vanna_memory.db):**

```sql
-- Perfiles operativos
CREATE TABLE SystemConfigProfiles (
    Id INTEGER PRIMARY KEY,
    EnvironmentName TEXT,
    ProfileKey TEXT UNIQUE,
    DisplayName TEXT,
    Description TEXT,
    IsActive INTEGER,
    IsReadOnly INTEGER,
    CreatedUtc TEXT,
    UpdatedUtc TEXT
);

-- Entradas de configuración
CREATE TABLE SystemConfigEntries (
    Id INTEGER PRIMARY KEY,
    ProfileId INTEGER,
    Section TEXT,      -- Prompting, Retrieval, UiDefaults, Docs
    Key TEXT,        -- MaxPromptChars, SystemPersona, etc.
    Value TEXT,      -- Valor
    ValueType TEXT, -- string, int, double, bool
    IsEditableInUi INTEGER,
    Description TEXT
);
```

**Secciones configurables:**
- `Prompting` - System persona, task instruction, syntax rules
- `Retrieval` - Top examples, min score, top schema docs
- `UiDefaults` - Admin domain, admin tenant
- `Docs` - Root path, default domain, top K

### 4.2 Training Examples

```sql
CREATE TABLE TrainingExamples (
    Id INTEGER PRIMARY KEY,
    Question TEXT,
    Sql TEXT,
    TenantKey TEXT,
    Domain TEXT,
    ConnectionName TEXT,
    IntentName TEXT,
    IsVerified INTEGER,  -- 1 = verificado por admin
    Priority INTEGER,
    UseCount INTEGER,
    CreatedUtc DATETIME,
    LastUsedUtc DATETIME
);
```

### 4.3 Query Patterns

```sql
CREATE TABLE QueryPatterns (
    Id INTEGER PRIMARY KEY,
    Domain TEXT,
    PatternKey TEXT,
    IntentName TEXT,
    Description TEXT,
    SqlTemplate TEXT,       -- Template con {TopN}, {TimeScopeFilter}
    DefaultTopN INTEGER,
    MetricKey TEXT,
    DimensionKey TEXT,
    DefaultTimeScopeKey TEXT,
    Priority INTEGER,
    IsActive INTEGER
);

CREATE TABLE QueryPatternTerms (
    Id INTEGER PRIMARY KEY,
    PatternId INTEGER,
    Term TEXT,           -- "scrap", "producción"
    TermGroup TEXT,     -- "metric", "dimension", "intent"
    MatchMode TEXT,     -- "contains" | "exact"
    IsRequired INTEGER,
    IsActive INTEGER
);
```

---

## 5. Endpoints de API

### 5.1 AssistantController

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/assistant/contexts` | Listar contextos disponibles |
| POST | `/api/assistant/ask` | Ejecutar consulta |
| GET | `/api/assistant/history?mode=` | Historial de consultas |
| POST | `/api/assistant/feedback` | Enviar feedback |
| GET | `/api/assistant/status/{jobId}` | Status de job |

**Códigos de modo:**
- `Data` - Consulta SQL
- `Docs` - Búsqueda PDF
- `Predict` - Forecasting ML

### 5.2 SqlAlertsController

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/sql-alerts` | Listar alertas |
| GET | `/api/sql-alerts/catalog` | Catálogo de métricas |
| GET | `/api/sql-alerts/events` | Historial de eventos |
| POST | `/api/sql-alerts` | Crear alerta |
| PUT | `/api/sql-alerts/{id}` | Actualizar alerta |
| POST | `/api/sql-alerts/preview` | Previsualizar query |
| POST | `/api/sql-alerts/{id}/activate` | Activar/desactivar |
| POST | `/api/sql-alerts/{id}/ack` | Acknowledge |
| POST | `/api/sql-alerts/{id}/clear` | Clear |

### 5.3 AdminController

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/admin/system-config` | Obtener config |
| PUT | `/api/admin/system-config` | Actualizar config |
| POST | `/api/admin/docs/upload` | Subir PDF |
| POST | `/api/admin/docs/index` | Indexar PDFs |
| GET | `/api/admin/training-examples` | Listar ejemplos |
| POST | `/api/admin/training-examples` | Agregar ejemplo |

---

## 6. Dependencias

### 6.1 Paquetes NuGet principales

| Paquete | Versión | Uso |
|--------|--------|-----|
| Microsoft.AspNetCore.App | (ASP.NET 10) | Framework |
| Microsoft.ML | 3.0+ | ML Forecasting |
| LLamaSharp | 0.26.0 | LLM local |
| Microsoft.Data.SqlClient | 5.x | SQL Server |
| Microsoft.Data.Sqlite | 8.x | SQLite |
| Dapper | 2.x | ORM |
| UglyToad.PdfPig | 1.7.0-custom-5 | PDF parsing |
| Chart.js (CDN) | 4.x | Gráficos |

### 6.2 Runtimes

- **.NET:** 10.0 (estable 2026)
- **LLM:** Qwen2.5-Coder-7B (configurable)
- **SQL Server:** 2019+ (SQL Server autenticación)

---

## 7. Métricas de Observabilidad

### 7.1 Logging

El sistema implementa logging por carriles:

```csharp
// SQL Pipeline logging
Log.LogInformation("[SqlPerf][{TenantKey}] Query generated in {ElapsedMs}ms", tenantKey, elapsedMs);
Log.LogInformation("[LlmPerf][{TenantKey}] Prompt sent in {ElapsedMs}ms", tenantKey, elapsedMs);

// Docs Pipeline
Log.LogInformation("[DocsPerf][{Domain}] Retrieved {ChunkCount} chunks", domain, chunkCount);
Log.LogInformation("[DocsPerf][{Domain}] Answer composed in {ElapsedMs}ms", domain, elapsedMs);

// ML Pipeline
Log.LogInformation("[MlPerf][{Domain}] Forecast generated in {ElapsedMs}ms", domain, elapsedMs);
```

### 7.2 Health Check

Endpoint disponible: `GET /health`

```json
{
  "status": "ok",
  "service": "VannaLight.Api",
  "utc": "2026-04-18T12:00:00Z",
  "scheme": "https",
  "host": "localhost:5001"
}
```

---

## 8. Riesgos y Recomendaciones

### 8.1 Riesgos Identificados

| Riesgo | Probabilidad | Impacto | Severidad | Mitigación |
|-------|--------------|---------|----------|------------|
| Memoria local incompleta | Media | Alto | 7/10 | Seeds en startup, warnings |
| Contexto frío sin grounding | Baja | Medio | 4/10 | Schema docs sembrados |
| Sin autenticación | Alta* | Alto | 8/10 | Pendiente para venta |
| Sin pruebas automatizadas | Alta | Medio | 6/10 | Validación manual |
| Diferencias entre PCs | Media | Medio | 5/10 | appsettings.Local.json |

*Para entorno de producción multi-usuario.

### 8.2 Recomendaciones

**Inmediatas (Cierre de pilote):**
1. ✅ Documentar las queries demo funcionando
2. ✅ Verificar seed de memoria en diferentes PCs
3. ✅ Probar flujo completo de alertas

**Corto plazo (Post-piloto):**
1. Autenticación multi-usuario
2. Health check visible en UI
3. Pruebas automatizadas básicas

**Mediano plazo:**
1. Soporte multi-tenant real
2. Métricas de uso
3. Backup/export de conocimiento

---

## 9. Checklist de Cierre

| Item | Estado | Evidencia |
|------|--------|----------|
| SQL multi-contexto | ✅ | TenantKey + Domain + ConnectionName |
| Pattern matching | ✅ | QueryPatternTerms |
| Schema grounding | ✅ | SchemaDocs sembrados |
| Semantic hints | ✅ | SemanticHints sembrados |
| Self-correction | ✅ | 1 retry en AskUseCase |
| Cacheo SQL | ✅ | SqlCacheService |
| PDF RAG | ✅ | DocumentIngestor |
| ML Forecasting | ✅ | ForecastingService |
| Alertas SQL | ✅ | SqlAlertEvaluationWorker |
| Admin UI | ✅ | admin.html |
| Onboarding | ✅ | Wizard 4 pasos |
| Health endpoint | ✅ | /health |

---

## 10. Veredicto Final

**Puntuación: 7.3/10**

El proyecto VannaLight está **listo para entregar el piloto** con confianza:

- ✅ Sistema funcional y demostrable
- ✅ Código mantenible
- ✅ UI operativa
- ✅ Alertas SQL implementadas
- ✅ Forecasting ML operativo

**Pendiente para siguiente fase:**
- Autenticación (para venta)
- Pruebas automatizadas
- Health check visible

---

## Anexo: Rutas de Archivos Clave

```
VannaLight.Api/
├── Controllers/
│   ├── AssistantController.cs     (265 líneas)
│   ├── AdminController.cs
│   ├── SqlAlertsController.cs      (245 líneas)
│   └── SqlAlertsAdminController.cs
├── Services/
│   ├── InferenceWorker.cs
│   ├── SqlCacheService.cs
│   ├── Predictions/
│   │   ├── ForecastingService.cs
│   │   └── MlModelTrainer.cs
│   ├── Docs/
│   │   └── DocsAnswerService.cs
│   └── SqlAlerts/
│       ├── SqlAlertEvaluationWorker.cs
│       └── SqlAlertEvaluator.cs
├── wwwroot/
│   ├── index.html               (~2312 líneas)
│   ├── admin.html              (~2399 líneas)
│   └── css/index.css
└── Program.cs                 (~1200+ líneas)

VannaLight.Core/
├── UseCases/
│   ├── AskUseCase.cs
│   └── TrainExampleUseCase.cs
└── Models/
    └── QuestionJob.cs

VannaLight.Infrastructure/
├── Retrieval/
│   ├── PatternMatcherService.cs
│   └── TemplateSqlBuilder.cs
└── Data/
    ├── SqliteJobStore.cs
    └── SqliteTrainingStore.cs
```