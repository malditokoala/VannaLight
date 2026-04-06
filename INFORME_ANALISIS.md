# Informe de Análisis - VannaLight (v3)

**Fecha:** 26 de marzo de 2026  
**Proyecto:** VannaLight - Asistente Industrial de IA

---

## 1. Resumen Ejecutivo

| Categoría | Estado |
|-----------|--------|
| Archivos sin uso | ✅ 0 |
| Hardcoding crítico | ⚠️ 1 |
| Excepciones genéricas | ✅ 2 |
| Proyecto externo sin integrar | ⚠️ 1 |
| Sin tests | ⚠️ |

---

## 2. Arquitectura - Mejoras Detectadas ✅

Se han agregado nuevas abstracciones para configuración y secrets:

### Nuevos Archivos (Infrastructure/Configuration/)

| Archivo | Descripción |
|---------|-------------|
| `SystemConfigProvider.cs` | Proveedor de configuración con perfiles por entorno |
| `OperationalConnectionResolver.cs` | Resolvedor de conexiones SQL Server con soporte multi-modo |

### Nuevas Interfaces (Core/Abstractions/)

| Interfaz | Propósito |
|----------|-----------|
| `ISystemConfigProvider` | Proveedor de configuración centralizado |
| `ISystemConfigStore` | Almacén de perfiles de configuración |
| `IOperationalConnectionResolver` | Resolvedor de conexiones operacionales |
| `IConnectionProfileStore` | Almacén de perfiles de conexión |
| `ISecretResolver` | Resolvedor de secretos (env:, config:) |
| `ISemanticHintStore` | Almacén de hints semánticos |
| `IQueryPatternTermStore` | Almacén de términos de patrones |

### Nuevos Stores (Infrastructure/Data/)

| Store | Propósito |
|-------|-----------|
| `SqliteSystemConfigStore` | Persistencia de configuración del sistema |
| `SqliteConnectionProfileStore` | Persistencia de perfiles de conexión |
| `SqliteSemanticHintStore` | Persistencia de hints semánticos |
| `SqliteQueryPatternStore` | Persistencia de patrones de query |
| `SqliteQueryPatternTermStore` | Persistencia de términos de patrones |

### Nuevos Modelos (Core/Models/)

| Modelo | Propósito |
|--------|-----------|
| `SystemConfigProfile` | Perfil de configuración |
| `SystemConfigEntry` | Entrada individual de configuración |
| `ConnectionProfile` | Perfil de conexión SQL Server |
| `SemanticHint` | Hint semántico para queries |
| `QueryPatternTerm` | Término de patrón de query |

### Nuevos Componentes (Infrastructure/Security/)

| Archivo | Descripción |
|---------|-------------|
| `CompositeSecretResolver.cs` | Resolvedor de secretos soporta `env:` y `config:` |

---

## 3. Hardcoding Encontrado

### 🔴 Crítico (1 осталось)

| Archivo | Línea | Valor | Status |
|---------|-------|-------|--------|
| `Program.cs` | 49 | `C:\Modelos\qwen2.5-coder-7b-instruct-q4_k_m.gguf` | ⚠️ Pendiente |

✅ **Eliminado:** La ruta `C:\VannaLight\WI_DROP` de WiDocIngestor.cs

### 🟡 Vistas SQL Hardcodeadas (18+ lugares)

| Vista | Ubicaciones |
|-------|-------------|
| `vw_KpiProduction_v1` | 6 lugares |
| `vw_KpiScrap_v1` | 6 lugares |
| `vw_KpiDownTime_v1` | 6 lugares |

**Archivos afectados:**
- `TemplateSqlBuilder.cs` - 12 referencias
- `MlModelTrainer.cs` - 3 referencias
- `ForecastingService.cs` - 3 referencias

---

## 4. Proyecto Externo Detectado ⚠️

### `.codex-build/RuntimeJobAuditor/`

| Atributo | Valor |
|----------|-------|
| Líneas de código | ~1957 |
| Estado | **No integrado** en solución |
| Duplicación | Alto (vistas SQL duplicadas) |

**Problemas:**
1. No está referenciado en `VannaLight.slnx`
2. Código duplicado (vistas SQL hardcodeadas)
3. nearly 2000 líneas en un solo archivo
4. Propósito no claro (auditoría de jobs?)

**Recomendación:** Evaluar si integrar o eliminar.

---

## 5. Deuda Técnica

### 5.1 Métodos Largos

| Archivo | Líneas | Problema |
|---------|--------|----------|
| `DocsAnswerService.cs` | 330 | God Object |
| `InferenceWorker.cs` | ~270 | Lógica de 3 modos |
| `AskUseCase.cs` | 405 | Lógica embebida |

### 5.2 Magic Numbers

```csharp
score += 25;   // DocsAnswerService.cs:117
score += 3;    // DocsAnswerService.cs:263
score += 1;    // DocsAnswerService.cs:266
score += 8;    // DocsAnswerService.cs:268
score += 3;    // DocsAnswerService.cs:272
```

### 5.3 Excepciones Genéricas

| Archivo | Línea |
|---------|-------|
| `ForecastingService.cs` | 45 |
| `ForecastingService.cs` | 79 |

### 5.4 TODO Explícito

```csharp
// DocsAnswerService.cs:10
// TODO (Deuda Técnica - Fase de Testing): 
// 1. Extraer lógicas de Dominio
// 2. Mover modelos a Core
// 3. Romper God Object
```

---

## 6. Progreso Total

### ✅ Corregido (desde inicio)

| Categoría | Estado |
|-----------|--------|
| Empty catch blocks | ✅ Eliminado |
| Class1.cs | ✅ Eliminado |
| DocsIntentParser.cs | ✅ Eliminado |
| Hardcoding WiRootPath | ✅ Eliminado |

### ⚠️ Pendiente

| Categoría | Cantidad |
|-----------|----------|
| Rutas absolutas | 1 |
| Vistas SQL | 18+ |
| Excepciones genéricas | 2 |
| Magic numbers | 5 |
| Sin tests | Sí |
| Proyecto .codex-build | Evaluar |

---

## 7. Comparativa Histórica

| Categoría | v1 (14/03) | v2 (20/03) | v3 (26/03) |
|-----------|------------|------------|------------|
| Archivos sin uso | 2 | 0 ✅ | 0 ✅ |
| Empty catch | 1 | 0 ✅ | 0 ✅ |
| Rutas hardcodeadas | 4 | 2 | 1 ✅ |
| Excepciones genéricas | 3+ | 2 | 2 |
| Proyecto externo | 0 | 0 | 1 ⚠️ |
| Arquitectura config | básica | básica | **Mejorada** ✅ |

**Tendencia:** 🟢 **En mejora continua**
