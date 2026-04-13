# Documentación del Proyecto VannaLight

**Última actualización:** 3 de abril de 2026

---

## Descripción General

VannaLight es un asistente de IA industrial para consultas SQL en lenguaje natural. Utiliza un LLM local (LLamaSharp) para generar consultas SQL a partir de preguntas en español, con validación de seguridad, sistema RAG híbrido, soporte para predicción de scrap (ML.NET) y arquitectura multi-tenant.

---

## Estructura de Proyectos

```
VannaLight.slnx
├── VannaLight.Core           # Dominio y lógica de negocio (sin dependencias externas)
├── VannaLight.Infrastructure # Implementaciones (SQLite, LLM, Retrieval)
├── VannaLight.Api            # Web API + SignalR + Frontend
├── VannaLight.ConsoleApp     # Aplicación de consola
├── UltraMsgWebhookSpike     # Integración WhatsApp (NO integrado en solución)
└── .codex-build             # Herramientas de desarrollo (NO integrado)
```

---

## VannaLight.Core

Capa de dominio sin dependencias externas. Contains abstracciones, casos de uso y modelos.

### Abstractions/ (Puertos del sistema)

| Archivo | Descripción |
|---------|-------------|
| `Ports.cs` | Interfaces principales: ISchemaIngestor, ISchemaStore, ITrainingStore, IRetriever, ILlmClient, ISqlValidator, ISqlDryRunner, IReviewStore |
| `IJobStore.cs` | Interfaz para persistir trabajos/tareas del asistente |
| `ILlmProfileStore.cs` | Interfaz para persistir perfiles de configuración del LLM |
| `ILlmRuntimeProfileProvider.cs` | Proveedor de perfiles de runtime del LLM |
| `ISqlCacheService.cs` | Interfaz para caché de resultados SQL |
| `IDocChunkRepository.cs` | Interfaz para repositorio de chunks de documentos |
| `IDocsAnswerService.cs` | Interfaz para el servicio de respuestas de documentación |
| `IPatternMatcherService.cs` | Interfaz para matching de patrones de preguntas |
| `ITemplateSqlBuilder.cs` | Interfaz para construir SQL desde patrones |
| `IQueryPatternStore.cs` | Interfaz para persistir patrones de query |
| `IQueryPatternTermStore.cs` | Interfaz para persistir términos de patrones |
| `IBusinessRuleStore.cs` | Interfaz para almacenar reglas de negocio |
| `ISemanticHintStore.cs` | Interfaz para almacenar hints semánticos |
| `IAllowedObjectStore.cs` | Interfaz para almacenar objetos permitidos |
| `IAppSecretStore.cs` | Interfaz para almacenar secretos de la app |
| `ITenantStore.cs` | Interfaz para almacenar tenants |
| `ITenantDomainStore.cs` | Interfaz para almacenar dominios por tenant |
| `IConnectionProfileStore.cs` | Interfaz para almacenar perfiles de conexión |
| `ISecretResolver.cs` | Interfaz para resolver secretos (env:, config:) |
| `ISystemConfigStore.cs` | Interfaz para almacenar configuración del sistema |
| `ISystemConfigProvider.cs` | Proveedor de configuración por perfil/entorno |
| `IOperationalConnectionResolver.cs` | Resolvedor de conexiones operacionales |
| `IExecutionContextResolver.cs` | Resuelve contexto de ejecución (tenant, domain, connection) |
| `IPredictionIntentRouter.cs` | Interfaz para parsear intenciones de predicción |
| `IPredictionAnswerService.cs` | Interfaz para formatear respuestas de predicción |
| `IForecastingService.cs` | Interfaz para el servicio de forecasting (ML.NET) |

### UseCases/ (Casos de uso)

| Archivo | Descripción |
|---------|-------------|
| `AskUseCase.cs` | Caso de uso principal: recibe pregunta → recupera contexto → genera SQL → valida → ejecuta. Maneja 3 modos: Data (SQL), Docs (documentación), Predict (ML). Usa ExecutionContextResolver para multi-tenant. |
| `TrainExampleUseCase.cs` | Caso de uso para entrenar con ejemplos pregunta-SQL validados |
| `IngestUseCase.cs` | Caso de uso para ingestar esquema de base de datos desde SQL Server |

### Models/ (Entidades del dominio)

| Archivo | Descripción |
|---------|-------------|
| `SchemaModels.cs` | TableSchema, ColumnSchema, ForeignKeyInfo, TableSchemaDoc |
| `TrainingModels.cs` | TrainingExample - ejemplos validados para RAG |
| `QuestionJob.cs` | QuestionJob - trabajo/tarea del asistente |
| `CachedQuestionJobRow.cs` | Caché de resultados de jobs |
| `ReviewModels.cs` | ReviewItem, ReviewStatus, ReviewReason |
| `PredictionIntent.cs` | Intención de predicción (entity, horizon, target) |
| `DocsModels.cs` | DocTypeSchema, DocChunk, DocSearchResult, ChunkSection |
| `LlmRuntimeProfile.cs` | Perfil de configuración del LLM |
| `BusinessRule.cs` | Reglas de negocio para el dominio |
| `PatternMatchResult.cs` | Resultado del matching de patrones |
| `PatternMetric.cs` | Enum: ScrapQty, ScrapCost, ProducedQty, DownTimeMinutes, etc. |
| `PatternDimension.cs` | Enum: Press, Mold, Part, Department, Failure, Unknown |
| `PatternTimeScope.cs` | Enum: Today, Yesterday, CurrentWeek, CurrentMonth, CurrentShift, Unknown |
| `QueryPattern.cs` | Patrón de query configurable |
| `QueryPatternTerm.cs` | Término individual de un patrón |
| `SemanticHint.cs` | Hint semántico para queries |
| `AllowedObject.cs` | Objeto permitido en SQL (tabla/vista) |
| `AppSecret.cs` | Secreto de la aplicación |
| `ConnectionProfile.cs` | Perfil de conexión SQL Server (con múltiples modos) |
| `SystemConfigProfile.cs` | Perfil de configuración por entorno |
| `SystemConfigEntry.cs` | Entrada individual de configuración |
| `Tenant.cs` | Tenant (empresa/cliente) |
| `TenantDomain.cs` | Dominio y conexión asociada a un tenant |
| `AskExecutionContext.cs` | Contexto de ejecución: TenantKey, Domain, ConnectionName, SystemProfile |
| `SchemaObjectCandidate.cs` | Candidato de objeto de esquema |
| `AppSecret.cs` | Modelo para secretos |

### Settings/ (Configuración)

| Archivo | Descripción |
|---------|-------------|
| `Settings.cs` | LlmSettings, RetrievalSettings, SecuritySettings, AppSettings, AppSettingsFactory (MEDIO, ALTO) |
| `SqliteOptions.cs` | Opciones de configuración para SQLite |
| `RuntimeDbOptions.cs` | Opciones para base de datos de runtime |
| `OperationalDbOptions.cs` | Opciones para la base de datos operacional |
| `BootstrapSqlConnectionOptions.cs` | Opciones de conexión bootstrap |
| `KpiViewOptions.cs` | **NUEVO:** Nombres configurables de vistas KPI |

---

## VannaLight.Infrastructure

Implementaciones concretas de las abstracciones del Core.

### AI/ (Integración con LLM)

| Archivo | Descripción |
|---------|-------------|
| `LlmClient.cs` | Cliente para LLamaSharp - carga modelo GGUF y genera texto/SQL |
| `SqliteLlmProfileStore.cs` | Persistencia de perfiles LLM en SQLite |
| `SqliteLlmProfileProvider.cs` | Proveedor de perfiles LLM |

### Data/ (Repositorios SQLite)

| Archivo | Descripción |
|---------|-------------|
| `SqliteJobStore.cs` | Persistencia de trabajos en SQLite |
| `SqliteTrainingStore.cs` | Persistencia de ejemplos de entrenamiento |
| `SqliteSchemaStore.cs` | Persistencia de documentación del esquema |
| `SqliteReviewStore.cs` | Gestión de cola de revisión |
| `SqliteLlmProfileStore.cs` | Persistencia de perfiles LLM |
| `SqlCacheService.cs` | Caché de resultados SQL |
| `SqliteDocChunkRepository.cs` | Repositorio de chunks de documentos |
| `SqliteBusinessRuleStore.cs` | Persistencia de reglas de negocio |
| `SqliteSemanticHintStore.cs` | Persistencia de hints semánticos |
| `SqliteAllowedObjectStore.cs` | Persistencia de objetos permitidos |
| `SqliteAppSecretStore.cs` | Persistencia de secretos |
| `SqliteTenantStore.cs` | **NUEVO:** Persistencia de tenants |
| `SqliteTenantDomainStore.cs` | **NUEVO:** Persistencia de dominios por tenant |
| `SqliteConnectionProfileStore.cs` | **NUEVO:** Persistencia de perfiles de conexión |
| `SqliteSystemConfigStore.cs` | **NUEVO:** Persistencia de configuración del sistema |
| `SqliteQueryPatternStore.cs` | **NUEVO:** Persistencia de patrones de query |
| `SqliteQueryPatternTermStore.cs` | **NUEVO:** Persistencia de términos de patrones |

### Retrieval/ (Motor RAG)

| Archivo | Descripción |
|---------|-------------|
| `LocalRetriever.cs` | Recuperación híbrida (BM25 + contexto) de ejemplos y schema |
| `PatternMatcherService.cs` | Matching determinístico de patrones (scrap, producción, downtime) |
| `TemplateSqlBuilder.cs` | Construye SQL desde patrones predefinidos usando **KpiViewOptions** |

### Security/ (Validación de seguridad)

| Archivo | Descripción |
|---------|-------------|
| `StaticSqlValidator.cs` | Valida SQL SELECT-only, sin palabras peligrosas, sin SELECT * |
| `SqlServerDryRunner.cs` | Compila SQL sin ejecutar (SET NOEXEC ON) |
| `CompositeSecretResolver.cs` | Resolvedor de secretos (env:, config:) |
| `AppSecretProtection.cs` | Protección de secretos de la app |

### Configuration/ (Configuración)

| Archivo | Descripción |
|---------|-------------|
| `SystemConfigProvider.cs` | Proveedor de configuración con perfiles por entorno |
| `ExecutionContextResolver.cs` | **NUEVO:** Resuelve contexto multi-tenant (TenantKey, Domain, Connection) |
| `OperationalConnectionResolver.cs` | **NUEVO:** Resolvedor de conexiones con soporte multi-modo |

### SqlServer/ (Integración con SQL Server)

| Archivo | Descripción |
|---------|-------------|
| `SqlServerSchemaIngestor.cs` | Extrae esquema (tablas, columnas, FKs) desde SQL Server |

---

## VannaLight.Api

Web API con SignalR para tiempo real y frontend estático.

### Controllers/

| Archivo | Descripción |
|---------|-------------|
| `AssistantController.cs` | API principal: POST /api/assistant/ask, GET /history, GET /status/{jobId} |
| `AdminController.cs` | API de administración: training, profiles, configs, patterns, hints, rules |

### Services/

| Archivo | Descripción |
|---------|-------------|
| `InferenceWorker.cs` | BackgroundService que procesa la cola de trabajos |
| `AskRequestQueue.cs` | Cola de trabajos asíncrona |
| `WiDocIngestor.cs` | Servicio para ingestar documentos PDF |
| `DocumentIngestor.cs` | **NUEVO:** Motor de ingestión de documentos con regex para part numbers |
| `DocsAnswerService.cs` | Servicio principal para modo Docs - busca en documentación, extrae hechos |
| `ExtractionEngine.cs` | Motor de extracción de datos desde texto de PDFs |
| `DocsIntentRouterLlm.cs` | Parser de intenciones para Docs usando LLM |

### Services/Predictions/ (ML.NET)

| Archivo | Descripción |
|---------|-------------|
| `ForecastingService.cs` | Forecasting - carga modelo ML, hace predicciones de scrap |
| `MlModelTrainer.cs` | Entrenador del modelo ML.NET (FastTree) |
| `ModelInput.cs` | Schema de datos para ML.NET input |
| `PredictionIntentRouterLlm.cs` | Parser de intenciones de predicción usando LLM |
| `PredictionAnswerService.cs` | Formateador de respuestas de predicción |

### Services/Docs/

| Archivo | Descripción |
|---------|-------------|
| `WiAnswerBuilder.cs` | Constructor de respuestas para Work Instructions |

### Hubs/

| Archivo | Descripción |
|---------|-------------|
| `AssistantHub.cs` | SignalR Hub para comunicación en tiempo real |

### Contracts/ (DTOs de API)

| Archivo | Descripción |
|---------|-------------|
| `AskMode.cs` | Enum: Data, Docs, Predict |
| `DocsContracts.cs` | Contratos/DTOs para documentación |
| `DocsIntent.cs` | Clase DocsIntent - intención parseada |
| `DomainPackContracts.cs` | Contratos para domain packs |

### wwwroot/

| Archivo | Descripción |
|---------|-------------|
| `index.html` | Frontend principal - chat UI con tabs SQL/Docs/Predict |
| `admin.html` | Panel de administración |
| `css/admin.css` | Estilos del panel admin |
| `js/index.js` | Lógica del frontend principal |
| `js/admin.js` | Lógica del panel admin |

---

## VannaLight.ConsoleApp

Aplicación de consola para tareas administrativas.

| Archivo | Descripción |
|---------|-------------|
| `Program.cs` | CLI para ejecutar ingest o review |

---

## UltraMsgWebhookSpike (NO integrado)

Proyecto separado para integración WhatsApp via UltraMsg API.

| Archivo | Descripción |
|---------|-------------|
| `Program.cs` | Punto de entrada del webhook |
| `Services/UltraMsgClient.cs` | Cliente HTTP para UltraMsg API |
| `Services/UltraMsgPollingService.cs` | Servicio de polling |
| `Services/UltraMsgPollingWorker.cs` | Background worker para polling |
| `Services/UltraMsgPollingState.cs` | Estado del polling |
| `Services/RequestInspector.cs` | Inspector de requests |
| `Models/*.cs` | Modelos de mensajes, resultados, etc. |
| `Options/UltraMsgOptions.cs` | Opciones de configuración |

---

## Arquitectura Multi-Tenant

### AskExecutionContext

```csharp
public class AskExecutionContext
{
    public string TenantKey { get; set; }      // "empresa_xyz"
    public string Domain { get; set; }         // "erp-kpi-pilot"
    public string ConnectionName { get; set; }   // "OperationalDb"
    public string SystemProfileKey { get; set; } // "default"
}
```

### Flujo de Resolución

```
Solicitud API
     │
     ├── /api/assistant/ask?tenant=xyz&domain=kpi
     │         │
     │         ▼
     │   ExecutionContextResolver.ResolveAsync()
     │         │
     │         ├──► Busca Tenant en SQLite
     │         ├──► Busca TenantDomain mapping
     │         ├──► Resuelve ConnectionString correcto
     │         └──► Retorna AskExecutionContext
     │                   │
     │                   ▼
     │         Usa connection del tenant específico
     │
     └── /api/assistant/ask (sin params)
              │
              ▼
        Usa defaults del sistema
```

---

## Flujo Principal de una Consulta

```
1. Usuario envía pregunta → AssistantController
2. ExecutionContextResolver resuelve Tenant/Domain/Connection
3. Se crea QuestionJob en SQLite (JobStore)
4. Se encola trabajo en AskRequestQueue
5. InferenceWorker procesa:
   a) Si modo Data:
      - PatternMatcherService detecta patrón predefinido
      - TemplateSqlBuilder genera SQL usando KpiViewOptions
      - LocalRetriever recupera contexto (ejemplos + schema)
      - LlmClient genera SQL desde prompt
      - StaticSqlValidator valida seguridad
      - SqlServerDryRunner compila sin ejecutar
      - Ejecuta contra SQL Server (del tenant)
      - Guarda en caché (SqlCacheService)
   b) Si modo Predict:
      - PredictionIntentRouterLlm parsea intención
      - ForecastingService predice con ML.NET
      - PredictionAnswerService formatea respuesta
   c) Si modo Docs:
      - DocsIntentRouterLlm parsea intención
      - DocsAnswerService busca en documentos
      - ExtractionEngine extrae datos
      - WiAnswerBuilder formatea respuesta
6. Notifica al cliente via SignalR
```

---

## Configuración de Vistas KPI

```json
{
  "KpiViews": {
    "ProductionViewName": "dbo.vw_KpiProduction_v1",
    "ScrapViewName": "dbo.vw_KpiScrap_v1",
    "DowntimeViewName": "dbo.vw_KpiDownTime_v1"
  }
}
```

---

## Base de Datos

### SQLite (vanna_memory.db)
- QuestionJobs: historial de trabajos
- TrainingExamples: ejemplos pregunta-SQL validados
- SchemaDocs: documentación del esquema
- ReviewQueue: cola de revisión
- LlmRuntimeProfiles: perfiles de configuración
- SystemConfigProfiles: perfiles de configuración
- SystemConfigEntries: entradas de configuración
- ConnectionProfiles: perfiles de conexión SQL Server
- TenantDomains: dominios por tenant
- Tenants: tenants
- BusinessRules: reglas de negocio
- SemanticHints: hints semánticos
- AllowedObjects: objetos permitidos
- AppSecrets: secretos
- QueryPatterns: patrones de query
- QueryPatternTerms: términos de patrones
- DocChunks: chunks de documentos

### SQLite (vanna_runtime.db)
- SqlCache: caché de resultados SQL

### SQL Server (Operacional - configurable)
- Vistas KPI configurables (Production, Scrap, Downtime)
- Turnos: configuración de turnos

---

## Tecnologías

- **.NET 10.0** - Framework principal
- **LLamaSharp 0.26.0** - LLM local (modelos GGUF)
- **LLamaSharp.Backend.Cuda12 0.26.0** - Soporte CUDA
- **ML.NET 5.0.0** - Forecasting
- **Microsoft.ML.FastTree 5.0.0** - Algoritmo de predicción
- **SQLite** - Metadatos y caché
- **Microsoft.Data.Sqlite 10.0.3** - Driver SQLite
- **Microsoft.Data.SqlClient 6.1.4** - Driver SQL Server
- **Dapper 2.1.66** - Acceso a datos
- **SignalR** - Tiempo real
- **UglyToad.PdfPig 1.7.0-custom-5** - Parsing PDFs ⚠️ Pre-release
- **Swashbuckle.AspNetCore 10.1.4** - Swagger/OpenAPI

---

## Proyectos Externos (NO integrados)

| Proyecto | Propósito | Estado |
|----------|-----------|--------|
| UltraMsgWebhookSpike | Integración WhatsApp | No integrado |
| .codex-build/RuntimeJobAuditor | Auditoría de jobs | No integrado |

---

## Estado del Proyecto

| Área | Estado |
|------|--------|
| Core functionality | ✅ Funcionando |
| Multi-tenancy | ✅ Implementado |
| Vistas KPI configurables | ✅ Implementado |
| Autenticación | ❌ Pendiente |
| Tests | ❌ No existen |

---

## Actualizacion de almacenamiento local y secretos (abril 2026)

La estrategia vigente del proyecto es:

- `appsettings.json`: solo defaults seguros y compartidos.
- `dotnet user-secrets`: `ConnectionStrings` reales por maquina en desarrollo.
- `VannaLight.Api/appsettings.Local.json`: override local opcional, ignorado por git.
- SQLite local y `dpkeys`: fuera del repo y fuera de OneDrive.

### Ubicaciones recomendadas

- `vanna_memory.db`: `%LOCALAPPDATA%\\VannaLight\\Data\\vanna_memory.db`
- `vanna_runtime.db`: `%LOCALAPPDATA%\\VannaLight\\Data\\vanna_runtime.db`
- `dpkeys`: `%LOCALAPPDATA%\\VannaLight\\Data\\dpkeys`

### Regla operativa

No compartir entre computadoras:

- `vanna_memory.db`
- `vanna_runtime.db`
- `dpkeys`
- `appsettings.Local.json` con secretos reales

### Objetivo

Evitar contaminacion entre PC de trabajo y PC de casa cuando el repo vive en git y/o en carpetas sincronizadas como OneDrive.
