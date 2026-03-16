# Auditoría Técnica Completa - VannaLight

**Fecha:** 14 de marzo de 2026  
**Auditor:** opencode  
**Proyecto:** VannaLight - Asistente Industrial de IA

---

## 1. Resumen Ejecutivo

| Área | Estado | Puntuación |
|------|--------|------------|
| Arquitectura | 🟡 Necesita mejora | 6/10 |
| Seguridad | 🟢 Bien | 8/10 |
| Dependencias | 🟡 Actualizar | 5/10 |
| Código | 🟡 Deuda técnica | 5/10 |
| Logging/Monitoring | 🔴 Deficiente | 3/10 |
| Testing | 🔴 No existe | 0/10 |
| Documentación | 🔴 Incompleta | 2/10 |

**Puntuación Global:** 4.9/10 🔴

---

## 2. Arquitectura

### 2.1 Estructura de Proyectos ✅
```
VannaLight.slnx
├── VannaLight.Core           (Dominio - Sin dependencias)
├── VannaLight.Infrastructure (Implementaciones)
├── VannaLight.Api            (Web API + Frontend)
└── VannaLight.ConsoleApp    (CLI)
```

**优点 (Pros):**
- Separación clara de responsabilidades
- Core sin dependencias externas
- Inyección de dependencias configurada

**改善点 (A mejorar):**
- Mezcla de responsabilidades en `DocsAnswerService` (extracción, scoring, respuesta)
- `InferenceWorker` con ~270 líneas maneja múltiples modos (SQL, Docs, Predict)
- Lógica de dominio en capa de infraestructura

### 2.2 Patrones Utilizados

| Patrón | Uso | Estado |
|--------|-----|--------|
| Repository | SqliteXxxStore | ✅ |
| Unit of Work | No | ❌ |
| CQRS | No | ❌ |
| Mediator | No | ❌ |
| Factory | AppSettingsFactory | ✅ |

---

## 3. Dependencias y Versiones

### 3.1 Paquetes NuGet

| Paquete | Versión Actual | Latest | Riesgo |
|---------|----------------|--------|--------|
| **LLamaSharp** | 0.26.0 | 17.x | 🔴 Obsoleto |
| **Microsoft.ML** | 5.0.0 | 5.0.1 | 🟢 |
| **Dapper** | 2.1.66 | 2.1.35 | 🟡 |
| **Microsoft.Data.Sqlite** | 10.0.3 | 10.0.x | 🟢 |
| **UglyToad.PdfPig** | 1.7.0-custom-5 | 0.1.9 | 🔴 Pre-release |
| **Microsoft.Data.SqlClient** | 6.1.4 | 6.1.4 | 🟢 |

### 3.2 Problemas Críticos

1. **LLamaSharp 0.26.0** está muy desactualizado (latest: 17.x)
   - Sin soporte para nuevos modelos
   - Posibles bugs de memoria
   - Recomendación: Migrar a 0.17.x

2. **UglyToad.PdfPig 1.7.0-custom-5** es una versión custom/pre-release
   - Inestable para producción
   - Usar versión estable 0.1.9

3. **Framework .NET 10** (preview)
   - Usar .NET 8 LTS para producción

---

## 4. Seguridad

### 4.1 Validaciones SQL ✅

| Archivo | Implementación |
|---------|----------------|
| `StaticSqlValidator.cs` | ✅ Bloquea INSERT/UPDATE/DELETE/DROP/etc |
| `SqlServerDryRunner.cs` | ✅ Dry-run con NOEXEC |

### 4.2 Mejores Prácticas

| Aspecto | Estado |
|---------|--------|
| Secrets en appsettings | ✅ UserSecretsId configurado |
| Parámetros en queries | ✅ Dapper previene SQL injection |
| Validación de entrada | 🟡 Parcial |
| Rate limiting | ❌ No implementado |
| CORS | ❌ No configurado |

---

## 5. Calidad de Código

### 5.1 Métricas

| Métrica | Valor | Umbral |
|---------|-------|--------|
| Líneas de código (~) | 5000 | - |
| Archivos .cs | 60 | - |
| Complejidad ciclomática | Alta | - |
| Acoplamiento | Alto | - |

### 5.2 Code Smells

| # | Problema | Severidad | Ubicación |
|---|----------|-----------|-----------|
| 1 | God Object | Alta | `DocsAnswerService.cs` (330 líneas) |
| 2 | Método largo | Alta | `InferenceWorker.cs` (268 líneas) |
| 3 | Método largo | Alta | `AskUseCase.cs` (405 líneas) |
| 4 | Magic numbers | Media | `DocsAnswerService.cs` |
| 5 | Empty catch | Alta | `InferenceWorker.cs:205` |
| 6 | Vistas hardcodeadas | Alta | 10+ ubicaciones |
| 7 | Duplicación | Media | 19x connection strings |

### 5.3 Convenciones de Nombre

| Problema | Ejemplo |
|----------|---------|
| Campos privados sin `_` | `logger`, `_log` (mezclado) |
| Clases no selladas | `DocsAnswerService` podría ser `sealed` |
| Métodos async sin CT | Algunos endpoints ignoran CancellationToken |

---

## 6. Logging y Monitoreo

### 6.1 Estado Actual ❌

```csharp
// Solo 2 servicios usan ILogger:
- InferenceWorker
- DocsAnswerService
```

**Problemas:**
- ❌ Sin logging en servicios críticos:
  - `SqliteJobStore`
  - `SqliteTrainingStore`
  - `SqliteSchemaStore`
  - `LlmClient`
  - `LocalRetriever`
  - `AskUseCase`
- ❌ Sin métricas (Application Insights, Prometheus)
- ❌ Sin health checks
- ❌ Sin distributed tracing

### 6.2 Recomendaciones

1. Agregar `ILogger` a todos los servicios de infraestructura
2. Implementar health checks para:
   - SQL Server
   - SQLite
   - Modelo LLM
3. Agregar métricas de rendimiento

---

## 7. Testing

### 7.1 Estado: NO EXISTE ❌

```
Directorios de test: 0 archivos
```

### 7.2 Prioridades para Tests

| Prioridad | Servicio/Clase | Tipo de Test |
|-----------|----------------|--------------|
| 🔴 Alta | `StaticSqlValidator` | Unit |
| 🔴 Alta | `PatternMatcherService` | Unit |
| 🔴 Alta | `TemplateSqlBuilder` | Unit |
| 🟡 Media | `AskUseCase` | Integration |
| 🟡 Media | `LlmClient` | Integration |
| 🟢 Baja | UI (frontend) | E2E |

---

## 8. Documentación

### 8.1 Estado

| Tipo | Estado |
|------|--------|
| README | ❌ No existe |
| API Docs | ❌ No existe |
| Arquitectura | ❌ No existe |
| Changelog | ❌ No existe |
| Código (XML Docs) | ❌ No existe |

### 8.2 Archivos de Configuración

| Archivo | Contenido |
|---------|-----------|
| `appsettings.json` | ⚠️ No está en repo (contiene secrets) |
| `.gitignore` | ✅ Adecuado |

---

## 9. Performance

### 9.1 Puntos de Atención

| Área | Problema | Impacto |
|------|----------|---------|
| **LLM** | Carga en startup | ~30 segundos |
| **ML.NET** | Entrenamiento en runtime | Si no existe modelo |
| **SQL** | Sin cache de resultados | Alta latencia |
| **PDF** | Parsing línea a línea | Lento para PDFs grandes |

### 9.2 Optimizaciones Sugeridas

1. Lazy loading del modelo LLM
2. Pre-entrenar modelo ML y打包
3. Agregar Redis para caché de queries
4. Procesamiento paralelo de PDFs

---

## 10. Configuración y Despliegue

### 10.1 Hardcoding Encontrado

| Valor | Ubicación | Tipo |
|-------|-----------|------|
| `C:\Modelos\qwen2.5-coder-7b-instruct-q4_k_m.gguf` | Program.cs:45 | Crítico |
| `C:\VannaLight\WI_DROP` | WiDocIngestor.cs:23 | Crítico |
| `Data/vanna_memory.db` | 10+ lugares | Bajo |
| Vistas SQL | 10+ lugares | Alto |

### 10.2 Recomendaciones

1. Mover todo a `appsettings.json`
2. Usar variables de entorno en producción
3. No hardcodear rutas absolutas

---

## 11. Plan de Acción

### Fase 1: Crítico (Semana 1)
- [ ] Actualizar LLamaSharp de 0.26.0 a 0.17.x
- [ ] Reemplazar UglyToad.PdfPig con versión estable
- [ ] Mover paths hardcodeados a configuración
- [ ] Eliminar empty catch
- [ ] Agregar logging a servicios críticos

### Fase 2: Importante (Semana 2-3)
- [ ] Agregar tests unitarios (validator, patterns)
- [ ] Refactorizar DocsAnswerService
- [ ] Implementar health checks
- [ ] Agregar rate limiting
- [ ] Configurar CORS

### Fase 3: Mejora (Semana 4+)
- [ ] Migrar a .NET 8 LTS
- [ ] Agregar caché Redis
- [ ] Documentar API con OpenAPI/Swagger
- [ ] Crear README.md

---

## 12. Conclusión

El proyecto tiene una **arquitectura correcta** pero suffers de:
1. **Deuda técnica acumulada** (hardcoding, magic numbers, god objects)
2. **Dependencias obsoletas** (LLamaSharp, PdfPig)
3. **Falta de testing** (riesgo alto para refactoring)
4. **Logging insuficiente** (difícil debugging)

**Recomendación:** Priorizar fase 1 antes de producción.

---

*Informe generado automáticamente. Última actualización: 2026-03-14*
