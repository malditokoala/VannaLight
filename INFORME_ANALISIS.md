# Informe de Análisis de Código - VannaLight

**Fecha:** 14 de marzo de 2026  
**Proyecto:** VannaLight - Asistente Industrial de IA para Consultas SQL

---

## 1. Resumen del Proyecto

VannaLight es un asistente industrial de IA que convierte preguntas en lenguaje natural a consultas SQL de forma segura, usando un modelo local de lenguaje (LLamaSharp) y un sistema de revisión para validar las consultas antes de ejecutarse.

### Características principales:
- Interfaz web con chat para hacer preguntas en lenguaje natural
- Genera consultas SQL usando LLM local
- Tres modos: SQL, Docs (documentación), Predicción (forecasting)
- Sistema RAG híbrido para recuperación de contexto
- Validación de seguridad SQL estática
- Cola de revisión para validar queries
- Almacenamiento en SQLite
- Soporte para extraer esquema de SQL Server

### Arquitectura:
- **VannaLight.Core** - Abstracciones y casos de uso
- **VannaLight.Infrastructure** - Implementaciones (SQLite, LLamaSharp, Retrieval)
- **VannaLight.Api** - API web + frontend
- **VannaLight.ConsoleApp** - Aplicación de consola

---

## 2. Archivos Sin Uso Real

| Archivo | Problema |
|---------|----------|
| `VannaLight.Infrastructure/Class1.cs` | Clase vacía de placeholder, sin referencias |
| `VannaLight.Api/Services/DocsIntentParser.cs` | Parser determinístico legacy no utilizado (el sistema usa `DocsIntentRouterLlm`) |

**Estado:** ✅ Eliminados

---

## 3. Hardcoding Encontrado

| Archivo | Línea | Valor | Severidad |
|---------|-------|-------|-----------|
| `Program.cs` | 32 | `C:\Modelos\qwen2.5-coder-7b-instruct-q4_k_m.gguf` | Alta |
| `WiDocIngestor.cs` | 23 | `C:\VannaLight\WI_DROP` | Alta |
| `Program.cs` | 26, 29 | `Data/vanna_memory.db`, `Data/vanna_runtime.db` | Baja (fallbacks) |

---

## 4. Deuda Técnica

### 4.1 God Object
**Ubicación:** `DocsAnswerService.cs:14` (330 líneas)

Esta clase tiene múltiples responsabilidades:
- Extracción de documentos
- Scoring y ranking
- Construcción de respuestas
- Enrutamiento de intents
- Gestión de configuración

**Recomendación:** Extraer a servicios separados (DocExtractor, DocScorer, AnswerBuilder)

### 4.2 Magic Numbers
**Ubicación:** `DocsAnswerService.cs`

```csharp
score += 25;           // Línea 117
score += 3;            // Línea 263
score += 1;            // Línea 266
score += 8;            // Línea 268
score += 3;            // Línea 272
```

**Recomendación:** Crear constantes con nombres descriptivos

### 4.3 Empty Catch Block
**Ubicación:** `InferenceWorker.cs:205`

```csharp
catch { /* ignorar errores en el manejo de errores */ }
```

**Problema:** Oculta errores críticos y hace difícil el debugging.

### 4.4 TODO Explícito
**Ubicación:** `DocsAnswerService.cs:10-13`

```csharp
// TODO (Deuda Técnica - Fase de Testing): 
// 1. Extraer lógicas de Dominio (Extracción/Scoring) a servicios de Core.
// 2. Mover modelos (DocTypeSchema, DocsIntent) a VannaLight.Core.Models.
// 3. Romper este God Object para facilitar testing unitario.
```

### 4.5 Código Comentado
**Ubicación:** `DocsAnswerService.cs:3`
```csharp
// VannaLight.Api.Contracts;
```

---

## 5. Recomendaciones Prioritarias

| Prioridad | Acción |
|-----------|--------|
| 🔴 Alta | Mover rutas hardcodeadas a configuración (appsettings.json) |
| 🔴 Alta | Eliminar empty catch en InferenceWorker.cs |
| 🟡 Media | Refactorizar DocsAnswerService (extraer responsabilidades) |
| 🟡 Media | Reemplazar magic numbers por constantes |
| 🟢 Baja | Eliminar comentarios comentados |

---

## 6. Métricas del Proyecto

- **Total archivos .cs:** ~60
- **Líneas de código (aprox):** ~5000
- **Proyectos:** 4 (Core, Infrastructure, Api, ConsoleApp)
- **Frameworks:** .NET 10, LLamaSharp, SQLite, SignalR, ML.NET

---

## 7. Nuevos Hallazgos (Análisis Extensivo)

### 7.1 Vistas de Base de Datos Hardcodeadas

| Vista | Ubicaciones |
|-------|-------------|
| `dbo.vw_KpiProduction_v1` | 7 lugares |
| `dbo.vw_KpiScrap_v1` | 6 lugares |
| `dbo.vw_KpiDownTime_v1` | 5 lugares |

**Archivos afectados:**
- `TemplateSqlBuilder.cs:34,45,58,73,88,140-143`
- `AskUseCase.cs:269,279-281`
- `MlModelTrainer.cs:34,39,46`
- `ForecastingService.cs:215,218,222`

### 7.2 Descripciones de Esquema Hardcodeadas

**Ubicación:** `AskUseCase.cs:279-281`

```csharp
@"Tabla: dbo.vw_KpiProduction_v1. Columnas clave: OperationDate, YearNumber..."
```

Estas descripciones de columnas están embebidas en el código y no se actualizan automáticamente.

### 7.3 Lógica de Negocio del Dominio Hardcodeada

**Archivo:** `PatternMatcherService.cs`

Patrones de negocio específicos del dominio industrial:
- "scrap", "prensa", "prensas", "más", "mayor", "top"
- "producción total", "produccion total"
- "downtime", "tiempo caído", "falla"
- "molde", "moldes", "scrap cost"

**Problema:** Si cambia el dominio (otro tipo de fábrica), hay que reescribir el código.

### 7.4 Sin Tests Unitarios

No existe ningún archivo de tests en el proyecto (`*Test*.cs`). Esto impide:
- Refactoring seguro
- Detección de regresiones
- Validación de lógica de negocio

### 7.5 Duplicación de Cadenas de Conexión SQLite

**19 lugares** usan `new SqliteConnection($"Data Source=...")` con diferentes variaciones:
- Con/sin punto y coma final
- Con diferentes modos (ReadWriteCreate, Cache, Timeout)
- Con y sin rutas normalizadas

### 7.6 Excepciones Genéricas

| Archivo | Línea | Problema |
|---------|-------|----------|
| `ForecastingService.cs` | 45, 79 | `throw new Exception("...")` en vez de excepciones tipadas |
| `ExtractionEngine.cs` | 150 | `throw new InvalidOperationException` |

### 7.7 Métodos Largos

- `InferenceWorker.cs`: ~268 líneas (BackgroundService con múltiples responsabilidades)
- `DocsAnswerService.cs`: 330 líneas (God Object)
- `AskUseCase.cs`: 405 líneas (caso de uso con lógica embebida)

---

## 8. Resumen de Deuda Técnica Total

| Categoría | Cantidad | Severidad |
|-----------|----------|-----------|
| Archivos sin uso | 2 | Baja |
| Hardcoding paths | 4 | Alta |
| Hardcoding vistas BD | 3 vistas × 10+ ubicaciones | Alta |
| Magic numbers | 5+ | Media |
| God Objects | 2 | Media |
| Empty catch | 1 | Alta |
| Código duplicado (conexiones) | 19 | Media |
| Sin tests | 1 proyecto completo | Alta |
| Excepciones genéricas | 3+ | Baja |
| Lógica de dominio hardcodeada | 1 archivo completo | Alta |
