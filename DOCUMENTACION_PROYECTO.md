# DocumentaciÃ³n del Proyecto VannaLight

**Ãšltima actualizaciÃ³n:** 3 de abril de 2026

---

## DescripciÃ³n General

VannaLight es un asistente de IA industrial para consultas SQL en lenguaje natural. Utiliza un LLM local (LLamaSharp) para generar consultas SQL a partir de preguntas en espaÃ±ol, con validaciÃ³n de seguridad, sistema RAG hÃ­brido, soporte para predicciÃ³n de scrap (ML.NET) y arquitectura multi-tenant.

---

## Estructura de Proyectos

```
VannaLight.slnx
â”œâ”€â”€ VannaLight.Core           # Dominio y lÃ³gica de negocio (sin dependencias externas)
â”œâ”€â”€ VannaLight.Infrastructure # Implementaciones (SQLite, LLM, Retrieval)
â”œâ”€â”€ VannaLight.Api            # Web API + SignalR + Frontend
â”œâ”€â”€ VannaLight.ConsoleApp     # AplicaciÃ³n de consola
â””â”€â”€ .codex-build             # Herramientas de desarrollo (NO integrado)
```

---

## VannaLight.Core

Capa de dominio sin dependencias externas. Contains abstracciones, casos de uso y modelos.

### Abstractions/ (Puertos del sistema)

| Archivo | DescripciÃ³n |
|---------|-------------|
| `Ports.cs` | Interfaces principales: ISchemaIngestor, ISchemaStore, ITrainingStore, IRetriever, ILlmClient, ISqlValidator, ISqlDryRunner, IReviewStore |
| `IJobStore.cs` | Interfaz para persistir trabajos/tareas del asistente |
| `ILlmProfileStore.cs` | Interfaz para persistir perfiles de configuraciÃ³n del LLM |
| `ILlmRuntimeProfileProvider.cs` | Proveedor de perfiles de runtime del LLM |
| `ISqlCacheService.cs` | Interfaz para cachÃ© de resultados SQL |
| `IDocChunkRepository.cs` | Interfaz para repositorio de chunks de documentos |
| `IDocsAnswerService.cs` | Interfaz para el servicio de respuestas de documentaciÃ³n |
| `IPatternMatcherService.cs` | Interfaz para matching de patrones de preguntas |
| `ITemplateSqlBuilder.cs` | Interfaz para construir SQL desde patrones |
| `IQueryPatternStore.cs` | Interfaz para persistir patrones de query |
| `IQueryPatternTermStore.cs` | Interfaz para persistir tÃ©rminos de patrones |
| `IBusinessRuleStore.cs` | Interfaz para almacenar reglas de negocio |
| `ISemanticHintStore.cs` | Interfaz para almacenar hints semÃ¡nticos |
| `IAllowedObjectStore.cs` | Interfaz para almacenar objetos permitidos |
| `IAppSecretStore.cs` | Interfaz para almacenar secretos de la app |
| `ITenantStore.cs` | Interfaz para almacenar tenants |
| `ITenantDomainStore.cs` | Interfaz para almacenar dominios por tenant |
| `IConnectionProfileStore.cs` | Interfaz para almacenar perfiles de conexiÃ³n |
| `ISecretResolver.cs` | Interfaz para resolver secretos (env:, config:) |
| `ISystemConfigStore.cs` | Interfaz para almacenar configuraciÃ³n del sistema |
| `ISystemConfigProvider.cs` | Proveedor de configuraciÃ³n por perfil/entorno |
| `IOperationalConnectionResolver.cs` | Resolvedor de conexiones operacionales |
| `IExecutionContextResolver.cs` | Resuelve contexto de ejecuciÃ³n (tenant, domain, connection) |
| `IPredictionIntentRouter.cs` | Interfaz para parsear intenciones de predicciÃ³n |
| `IPredictionAnswerService.cs` | Interfaz para formatear respuestas de predicciÃ³n |
| `IForecastingService.cs` | Interfaz para el servicio de forecasting (ML.NET) |

### UseCases/ (Casos de uso)

| Archivo | DescripciÃ³n |
|---------|-------------|
| `AskUseCase.cs` | Caso de uso principal: recibe pregunta â†’ recupera contexto â†’ genera SQL â†’ valida â†’ ejecuta. Maneja 3 modos: Data (SQL), Docs (documentaciÃ³n), Predict (ML). Usa ExecutionContextResolver para multi-tenant. |
| `TrainExampleUseCase.cs` | Caso de uso para entrenar con ejemplos pregunta-SQL validados |
| `IngestUseCase.cs` | Caso de uso para ingestar esquema de base de datos desde SQL Server |

### Models/ (Entidades del dominio)

| Archivo | DescripciÃ³n |
|---------|-------------|
| `SchemaModels.cs` | TableSchema, ColumnSchema, ForeignKeyInfo, TableSchemaDoc |
| `TrainingModels.cs` | TrainingExample - ejemplos validados para RAG |
| `QuestionJob.cs` | QuestionJob - trabajo/tarea del asistente |
| `CachedQuestionJobRow.cs` | CachÃ© de resultados de jobs |
| `ReviewModels.cs` | ReviewItem, ReviewStatus, ReviewReason |
| `PredictionIntent.cs` | IntenciÃ³n de predicciÃ³n (entity, horizon, target) |
| `DocsModels.cs` | DocTypeSchema, DocChunk, DocSearchResult, ChunkSection |
| `LlmRuntimeProfile.cs` | Perfil de configuraciÃ³n del LLM |
| `BusinessRule.cs` | Reglas de negocio para el dominio |
| `PatternMatchResult.cs` | Resultado del matching de patrones |
| `PatternMetric.cs` | Enum: ScrapQty, ScrapCost, ProducedQty, DownTimeMinutes, etc. |
| `PatternDimension.cs` | Enum: Press, Mold, Part, Department, Failure, Unknown |
| `PatternTimeScope.cs` | Enum: Today, Yesterday, CurrentWeek, CurrentMonth, CurrentShift, Unknown |
| `QueryPattern.cs` | PatrÃ³n de query configurable |
| `QueryPatternTerm.cs` | TÃ©rmino individual de un patrÃ³n |
| `SemanticHint.cs` | Hint semÃ¡ntico para queries |
| `AllowedObject.cs` | Objeto permitido en SQL (tabla/vista) |
| `AppSecret.cs` | Secreto de la aplicaciÃ³n |
| `ConnectionProfile.cs` | Perfil de conexiÃ³n SQL Server (con mÃºltiples modos) |
| `SystemConfigProfile.cs` | Perfil de configuraciÃ³n por entorno |
| `SystemConfigEntry.cs` | Entrada individual de configuraciÃ³n |
| `Tenant.cs` | Tenant (empresa/cliente) |
| `TenantDomain.cs` | Dominio y conexiÃ³n asociada a un tenant |
| `AskExecutionContext.cs` | Contexto de ejecuciÃ³n: TenantKey, Domain, ConnectionName, SystemProfile |
| `SchemaObjectCandidate.cs` | Candidato de objeto de esquema |
| `AppSecret.cs` | Modelo para secretos |

### Settings/ (ConfiguraciÃ³n)

| Archivo | DescripciÃ³n |
|---------|-------------|
| `Settings.cs` | LlmSettings, RetrievalSettings, SecuritySettings, AppSettings, AppSettingsFactory (MEDIO, ALTO) |
| `SqliteOptions.cs` | Opciones de configuraciÃ³n para SQLite |
| `RuntimeDbOptions.cs` | Opciones para base de datos de runtime |
| `OperationalDbOptions.cs` | Opciones para la base de datos operacional |
| `BootstrapSqlConnectionOptions.cs` | Opciones de conexiÃ³n bootstrap |
| `KpiViewOptions.cs` | **NUEVO:** Nombres configurables de vistas KPI |

---

## VannaLight.Infrastructure

Implementaciones concretas de las abstracciones del Core.

### AI/ (IntegraciÃ³n con LLM)

| Archivo | DescripciÃ³n |
|---------|-------------|
| `LlmClient.cs` | Cliente para LLamaSharp - carga modelo GGUF y genera texto/SQL |
| `SqliteLlmProfileStore.cs` | Persistencia de perfiles LLM en SQLite |
| `SqliteLlmProfileProvider.cs` | Proveedor de perfiles LLM |

### Data/ (Repositorios SQLite)

| Archivo | DescripciÃ³n |
|---------|-------------|
| `SqliteJobStore.cs` | Persistencia de trabajos en SQLite |
| `SqliteTrainingStore.cs` | Persistencia de ejemplos de entrenamiento |
| `SqliteSchemaStore.cs` | Persistencia de documentaciÃ³n del esquema |
| `SqliteReviewStore.cs` | GestiÃ³n de cola de revisiÃ³n |
| `SqliteLlmProfileStore.cs` | Persistencia de perfiles LLM |
| `SqlCacheService.cs` | CachÃ© de resultados SQL |
| `SqliteDocChunkRepository.cs` | Repositorio de chunks de documentos |
| `SqliteBusinessRuleStore.cs` | Persistencia de reglas de negocio |
| `SqliteSemanticHintStore.cs` | Persistencia de hints semÃ¡nticos |
| `SqliteAllowedObjectStore.cs` | Persistencia de objetos permitidos |
| `SqliteAppSecretStore.cs` | Persistencia de secretos |
| `SqliteTenantStore.cs` | **NUEVO:** Persistencia de tenants |
| `SqliteTenantDomainStore.cs` | **NUEVO:** Persistencia de dominios por tenant |
| `SqliteConnectionProfileStore.cs` | **NUEVO:** Persistencia de perfiles de conexiÃ³n |
| `SqliteSystemConfigStore.cs` | **NUEVO:** Persistencia de configuraciÃ³n del sistema |
| `SqliteQueryPatternStore.cs` | **NUEVO:** Persistencia de patrones de query |
| `SqliteQueryPatternTermStore.cs` | **NUEVO:** Persistencia de tÃ©rminos de patrones |

### Retrieval/ (Motor RAG)

| Archivo | DescripciÃ³n |
|---------|-------------|
| `LocalRetriever.cs` | RecuperaciÃ³n hÃ­brida (BM25 + contexto) de ejemplos y schema |
| `PatternMatcherService.cs` | Matching determinÃ­stico de patrones (scrap, producciÃ³n, downtime) |
| `TemplateSqlBuilder.cs` | Construye SQL desde patrones predefinidos usando **KpiViewOptions** |

### Security/ (ValidaciÃ³n de seguridad)

| Archivo | DescripciÃ³n |
|---------|-------------|
| `StaticSqlValidator.cs` | Valida SQL SELECT-only, sin palabras peligrosas, sin SELECT * |
| `SqlServerDryRunner.cs` | Compila SQL sin ejecutar (SET NOEXEC ON) |
| `CompositeSecretResolver.cs` | Resolvedor de secretos (env:, config:) |
| `AppSecretProtection.cs` | ProtecciÃ³n de secretos de la app |

### Configuration/ (ConfiguraciÃ³n)

| Archivo | DescripciÃ³n |
|---------|-------------|
| `SystemConfigProvider.cs` | Proveedor de configuraciÃ³n con perfiles por entorno |
| `ExecutionContextResolver.cs` | **NUEVO:** Resuelve contexto multi-tenant (TenantKey, Domain, Connection) |
| `OperationalConnectionResolver.cs` | **NUEVO:** Resolvedor de conexiones con soporte multi-modo |

### SqlServer/ (IntegraciÃ³n con SQL Server)

| Archivo | DescripciÃ³n |
|---------|-------------|
| `SqlServerSchemaIngestor.cs` | Extrae esquema (tablas, columnas, FKs) desde SQL Server |

---

## VannaLight.Api

Web API con SignalR para tiempo real y frontend estÃ¡tico.

### Controllers/

| Archivo | DescripciÃ³n |
|---------|-------------|
| `AssistantController.cs` | API principal: POST /api/assistant/ask, GET /history, GET /status/{jobId} |
| `AdminController.cs` | API de administraciÃ³n: training, profiles, configs, patterns, hints, rules |

### Services/

| Archivo | DescripciÃ³n |
|---------|-------------|
| `InferenceWorker.cs` | BackgroundService que procesa la cola de trabajos |
| `AskRequestQueue.cs` | Cola de trabajos asÃ­ncrona |
| `WiDocIngestor.cs` | Servicio para ingestar documentos PDF |
| `DocumentIngestor.cs` | **NUEVO:** Motor de ingestiÃ³n de documentos con regex para part numbers |
| `DocsAnswerService.cs` | Servicio principal para modo Docs - busca en documentaciÃ³n, extrae hechos |
| `ExtractionEngine.cs` | Motor de extracciÃ³n de datos desde texto de PDFs |
| `DocsIntentRouterLlm.cs` | Parser de intenciones para Docs usando LLM |

### Services/Predictions/ (ML.NET)

| Archivo | DescripciÃ³n |
|---------|-------------|
| `ForecastingService.cs` | Forecasting - carga modelo ML, hace predicciones de scrap |
| `MlModelTrainer.cs` | Entrenador del modelo ML.NET (FastTree) |
| `ModelInput.cs` | Schema de datos para ML.NET input |
| `PredictionIntentRouterLlm.cs` | Parser de intenciones de predicciÃ³n usando LLM |
| `PredictionAnswerService.cs` | Formateador de respuestas de predicciÃ³n |

### Services/Docs/

| Archivo | DescripciÃ³n |
|---------|-------------|
| `WiAnswerBuilder.cs` | Constructor de respuestas para Work Instructions |

### Hubs/

| Archivo | DescripciÃ³n |
|---------|-------------|
| `AssistantHub.cs` | SignalR Hub para comunicaciÃ³n en tiempo real |

### Contracts/ (DTOs de API)

| Archivo | DescripciÃ³n |
|---------|-------------|
| `AskMode.cs` | Enum: Data, Docs, Predict |
| `DocsContracts.cs` | Contratos/DTOs para documentaciÃ³n |
| `DocsIntent.cs` | Clase DocsIntent - intenciÃ³n parseada |
| `DomainPackContracts.cs` | Contratos para domain packs |

### wwwroot/

| Archivo | DescripciÃ³n |
|---------|-------------|
| `index.html` | Frontend principal - chat UI con tabs SQL/Docs/Predict |
| `admin.html` | Panel de administraciÃ³n |
| `css/admin.css` | Estilos del panel admin |
| `js/index.js` | LÃ³gica del frontend principal |
| `js/admin.js` | LÃ³gica del panel admin |

---

## VannaLight.ConsoleApp

AplicaciÃ³n de consola para tareas administrativas.

| Archivo | DescripciÃ³n |
|---------|-------------|
| `Program.cs` | CLI para ejecutar ingest o review |

---


Proyecto separado para integraciÃ³n WhatsApp via UltraMsg API.

| Archivo | DescripciÃ³n |
|---------|-------------|
| `Program.cs` | Punto de entrada del webhook |
| `Services/UltraMsgClient.cs` | Cliente HTTP para UltraMsg API |
| `Services/UltraMsgPollingService.cs` | Servicio de polling |
| `Services/UltraMsgPollingWorker.cs` | Background worker para polling |
| `Services/UltraMsgPollingState.cs` | Estado del polling |
| `Services/RequestInspector.cs` | Inspector de requests |
| `Models/*.cs` | Modelos de mensajes, resultados, etc. |
| `Options/UltraMsgOptions.cs` | Opciones de configuraciÃ³n |

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

### Flujo de ResoluciÃ³n

```
Solicitud API
     â”‚
     â”œâ”€â”€ /api/assistant/ask?tenant=xyz&domain=kpi
     â”‚         â”‚
     â”‚         â–¼
     â”‚   ExecutionContextResolver.ResolveAsync()
     â”‚         â”‚
     â”‚         â”œâ”€â”€â–º Busca Tenant en SQLite
     â”‚         â”œâ”€â”€â–º Busca TenantDomain mapping
     â”‚         â”œâ”€â”€â–º Resuelve ConnectionString correcto
     â”‚         â””â”€â”€â–º Retorna AskExecutionContext
     â”‚                   â”‚
     â”‚                   â–¼
     â”‚         Usa connection del tenant especÃ­fico
     â”‚
     â””â”€â”€ /api/assistant/ask (sin params)
              â”‚
              â–¼
        Usa defaults del sistema
```

---

## Flujo Principal de una Consulta

```
1. Usuario envÃ­a pregunta â†’ AssistantController
2. ExecutionContextResolver resuelve Tenant/Domain/Connection
3. Se crea QuestionJob en SQLite (JobStore)
4. Se encola trabajo en AskRequestQueue
5. InferenceWorker procesa:
   a) Si modo Data:
      - PatternMatcherService detecta patrÃ³n predefinido
      - TemplateSqlBuilder genera SQL usando KpiViewOptions
      - LocalRetriever recupera contexto (ejemplos + schema)
      - LlmClient genera SQL desde prompt
      - StaticSqlValidator valida seguridad
      - SqlServerDryRunner compila sin ejecutar
      - Ejecuta contra SQL Server (del tenant)
      - Guarda en cachÃ© (SqlCacheService)
   b) Si modo Predict:
      - PredictionIntentRouterLlm parsea intenciÃ³n
      - ForecastingService predice con ML.NET
      - PredictionAnswerService formatea respuesta
   c) Si modo Docs:
      - DocsIntentRouterLlm parsea intenciÃ³n
      - DocsAnswerService busca en documentos
      - ExtractionEngine extrae datos
      - WiAnswerBuilder formatea respuesta
6. Notifica al cliente via SignalR
```

---

## ConfiguraciÃ³n de Vistas KPI

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
- SchemaDocs: documentaciÃ³n del esquema
- ReviewQueue: cola de revisiÃ³n
- LlmRuntimeProfiles: perfiles de configuraciÃ³n
- SystemConfigProfiles: perfiles de configuraciÃ³n
- SystemConfigEntries: entradas de configuraciÃ³n
- ConnectionProfiles: perfiles de conexiÃ³n SQL Server
- TenantDomains: dominios por tenant
- Tenants: tenants
- BusinessRules: reglas de negocio
- SemanticHints: hints semÃ¡nticos
- AllowedObjects: objetos permitidos
- AppSecrets: secretos
- QueryPatterns: patrones de query
- QueryPatternTerms: tÃ©rminos de patrones
- DocChunks: chunks de documentos

### SQLite (vanna_runtime.db)
- SqlCache: cachÃ© de resultados SQL

### SQL Server (Operacional - configurable)
- Vistas KPI configurables (Production, Scrap, Downtime)
- Turnos: configuraciÃ³n de turnos

---

## TecnologÃ­as

- **.NET 10.0** - Framework principal
- **LLamaSharp 0.26.0** - LLM local (modelos GGUF)
- **LLamaSharp.Backend.Cuda12 0.26.0** - Soporte CUDA
- **ML.NET 5.0.0** - Forecasting
- **Microsoft.ML.FastTree 5.0.0** - Algoritmo de predicciÃ³n
- **SQLite** - Metadatos y cachÃ©
- **Microsoft.Data.Sqlite 10.0.3** - Driver SQLite
- **Microsoft.Data.SqlClient 6.1.4** - Driver SQL Server
- **Dapper 2.1.66** - Acceso a datos
- **SignalR** - Tiempo real
- **UglyToad.PdfPig 1.7.0-custom-5** - Parsing PDFs âš ï¸ Pre-release
- **Swashbuckle.AspNetCore 10.1.4** - Swagger/OpenAPI

---

## Proyectos Externos (NO integrados)

| Proyecto | PropÃ³sito | Estado |
|----------|-----------|--------|
| .codex-build/RuntimeJobAuditor | AuditorÃ­a de jobs | No integrado |

---

## Estado del Proyecto

| Ãrea | Estado |
|------|--------|
| Core functionality | âœ… Funcionando |
| Multi-tenancy | âœ… Implementado |
| Vistas KPI configurables | âœ… Implementado |
| AutenticaciÃ³n | âŒ Pendiente |
| Tests | âŒ No existen |

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

