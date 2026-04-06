# Auditoría Técnica Completa - VannaLight (v3)

**Fecha:** 3 de abril de 2026  
**Auditor:** opencode  
**Proyecto:** VannaLight - Asistente Industrial de IA

---

## 1. Resumen Ejecutivo

| Área | Estado | Puntuación | Cambio vs v2 |
|------|--------|------------|--------------|
| Arquitectura | 🟡 Necesita mejora | 7/10 | - |
| Seguridad | 🟢 Bien | 8/10 | - |
| Dependencias | 🟢 Mayormente al día | 8/10 | - |
| Código | 🟡 Deuda técnica | 5/10 | - |
| Logging/Monitoring | 🔴 Deficiente | 3/10 | - |
| Testing | 🔴 No existe | 0/10 | - |
| Documentación | 🟡 Mejorada | 6/10 | - |
| **Multi-tenancy** | 🟡 En desarrollo | 5/10 | **🆕** |
| **WhatsApp Integration** | 🟡 Spike | 5/10 | **🆕** |

**Puntuación Global:** 5.8/10 🟡

---

## 2. Cambios Detectados desde v2

### 2.1 Nuevos Componentes ✅

| Componente | Descripción | Estado |
|------------|-------------|--------|
| `Tenant` / `TenantDomain` | Modelos para multi-tenancy | ✅ Implementado |
| `ITenantStore` / `SqliteTenantStore` | Persistencia de tenants | ✅ Implementado |
| `IExecutionContextResolver` | Resuelve contexto de ejecución | ✅ Implementado |
| `AskExecutionContext` | Contexto con TenantKey, Domain, Connection | ✅ Implementado |

### 2.2 Nuevo Proyecto Detectado ⚠️

| Proyecto | Descripción | Integración |
|----------|-------------|-------------|
| `UltraMsgWebhookSpike` | Integración WhatsApp (UltraMsg API) | ❌ No integrado |

### 2.3 Progreso General

| Ítem | Estado |
|------|--------|
| Archivos sin uso | ✅ 0 |
| Empty catch blocks | ✅ 0 |
| Vistas SQL hardcodeadas | ⚠️ 18+ (sin cambios) |
| Excepciones genéricas | ⚠️ 2 (sin cambios) |
| Magic numbers | ⚠️ 5 (sin cambios) |
| Autenticación | ❌ Pendiente |
| Tests | ❌ No existe |

---

## 3. Arquitectura Actualizada

### 3.1 Estructura de Proyectos

```
VannaLight.slnx
├── VannaLight.Api           (Web API + Frontend)
├── VannaLight.Core         (Dominio - Sin dependencias)
├── VannaLight.Infrastructure (Implementaciones)
├── VannaLight.ConsoleApp   (CLI)
├── UltraMsgWebhookSpike    (⚠️ No integrado en solución)
└── .codex-build/RuntimeJobAuditor (⚠️ No integrado)
```

### 3.2 Arquitectura Multi-Tenant (Nueva)

```
┌─────────────────────────────────────────────────────┐
│                  AskExecutionContext                 │
├─────────────────────────────────────────────────────┤
│  TenantKey      → "empresa_xyz"                    │
│  Domain         → "erp-kpi-pilot"                  │
│  ConnectionName → "OperationalDb"                   │
│  SystemProfile  → "default"                         │
└─────────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────┐
│           ExecutionContextResolver                   │
├─────────────────────────────────────────────────────┤
│  1. Resolve tenant (from DB or default)            │
│  2. Resolve domain (from DB or config)              │
│  3. Resolve connection (from DB or config)           │
│  4. Resolve system profile (from DB or config)      │
└─────────────────────────────────────────────────────┘
```

### 3.3 Flujo Multi-Tenant

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

## 4. Nuevas Funcionalidades

### 4.1 Multi-Tenancy

| Feature | Implementado | Funcional |
|---------|-------------|-----------|
| Tenant model | ✅ | ✅ |
| TenantDomain mapping | ✅ | ✅ |
| ExecutionContextResolver | ✅ | ✅ |
| Tenant-specific connections | ✅ | ✅ |
| Tenant-specific configs | ✅ | ✅ |

**Beneficio:** Un deployment puede servir a múltiples clientes/empresas.

### 4.2 WhatsApp Integration (UltraMsgWebhookSpike)

| Feature | Estado |
|---------|--------|
| Polling Worker | ✅ |
| Webhook endpoint | ✅ |
| UltraMsg API client | ✅ |
| Request inspector | ✅ |

**Nota:** Proyecto separado, no integrado en solución principal.

---

## 5. Deuda Técnica Actual

### 5.1 Sin Cambios desde v2

| Problema | Ubicación | Severidad |
|---------|-----------|-----------|
| Vistas SQL hardcodeadas | 18+ lugares | Alta |
| Magic numbers | DocsAnswerService.cs | Media |
| Excepciones genéricas | ForecastingService.cs:45,79 | Baja |
| TODO explícito | DocsAnswerService.cs:10 | Baja |

### 5.2 Code Smells Principales

| # | Problema | Severidad | Ubicación |
|---|----------|-----------|-----------|
| 1 | Vistas SQL hardcodeadas | Alta | 18+ ubicaciones |
| 2 | God Object | Media | DocsAnswerService.cs (330 líneas) |
| 3 | Lógica de dominio embebida | Alta | PatternMatcherService.cs |
| 4 | Sin tests unitarios | Alta | Proyecto completo |
| 5 | Logging insuficiente | Alta | Servicios críticos sin log |

---

## 6. Proyectos Externos Detectados

### 6.1 UltraMsgWebhookSpike ⚠️

| Atributo | Valor |
|----------|-------|
| Propósito | Integración WhatsApp via UltraMsg API |
| Líneas | ~135 |
| Estado | **No integrado** en VannaLight.slnx |
| ¿Se usará? | Desconocido |

**Preguntas:**
- ¿Es para permitir chatting via WhatsApp?
- ¿Se integrará con VannaLight.Api?
- ¿Es un experimento que se descartará?

### 6.2 .codex-build/RuntimeJobAuditor ⚠️

| Atributo | Valor |
|----------|-------|
| Propósito | Auditoría de jobs runtime |
| Líneas | ~1957 |
| Estado | **No integrado** en VannaLight.slnx |

---

## 7. Recomendaciones Actualizadas

### Fase 1: Crítico
- [ ] Implementar autenticación (Propuesta 1)
- [ ] Mover vistas SQL a configuración

### Fase 2: Importante
- [ ] Evaluar destino de UltraMsgWebhookSpike
  - ¿Integrar con VannaLight.Api?
  - ¿Descartar?
- [ ] Evaluar destino de .codex-build
- [ ] Agregar tests unitarios
- [ ] Implementar SymSpell para corrección de typos

### Fase 3: Mejora
- [ ] Evaluar embeddings vectoriales (lazy)
- [ ] Documentar arquitectura multi-tenant
- [ ] Actualizar UglyToad.PdfPig a versión estable (0.1.9+)

---

## 8. Preguntas Abiertas

| # | Pregunta | Prioridad |
|---|----------|-----------|
| 1 | ¿UltraMsgWebhookSpike se integrará o se descarta? | 🔴 |
| 2 | ¿El soporte multi-tenant está listo para producción? | 🟡 |
| 3 | ¿Se implementará autenticación antes de clientes externos? | 🔴 |
| 4 | ¿Cuál es el roadmap para los proyectos externos? | 🟡 |

---

## 9. Comparativa Histórica

| Versión | Fecha | Puntuación | Cambios |
|---------|-------|------------|---------|
| v1 | 14/03 | 4.9/10 | Baseline |
| v2 | 26/03 | 5.4/10 | +Configuración, -Empty catch |
| **v3** | 03/04 | **5.8/10** | +Multi-tenant, +WhatsApp spike, +Dependencias corregidas |

**Tendencia:** 🟢 En evolución positiva, pero sin cambios críticos resueltos.

---

## 10. Conclusión

El proyecto ha evolucionado con **multi-tenancy** y **WhatsApp integration**, mostrando maduración hacia un producto comercial. Sin embargo:

**Pendiente crítico:**
- ⚠️ Autenticación (necesario para vender)
- ⚠️ Vistas SQL configurables (necesario para multi-cliente)

**Nuevo:**
- ✅ Arquitectura multi-tenant preparada
- ⚠️ WhatsApp integration en progreso

**Recomendación:** Priorizar autenticación y configuración de BD antes de nuevas features.

---

*Informe generado automáticamente. Última actualización: 2026-04-03*
