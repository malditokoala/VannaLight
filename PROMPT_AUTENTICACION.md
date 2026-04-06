# Prompt: Diseño de Sistema de Autenticación Configurable

---

## CONTEXTO DEL PROYECTO

### Nombre del Proyecto
**VannaLight** - Asistente de IA Industrial para Consultas SQL

### Descripción General
Aplicación que convierte preguntas en lenguaje natural a consultas SQL usando un LLM local (LLamaSharp). Incluye:
- Chat UI para hacer preguntas en lenguaje natural
- Motor RAG para recuperación de contexto
- Sistema de predicción con ML.NET
- Validación de seguridad SQL
- Cola de revisión para queries

### Arquitectura Actual
```
VannaLight.slnx
├── VannaLight.Core           (Dominio - Sin dependencias)
├── VannaLight.Infrastructure (Implementaciones)
├── VannaLight.Api           (Web API + Frontend)
└── VannaLight.ConsoleApp   (CLI)
```

### Stack Tecnológico
- .NET 10
- LLamaSharp 0.26.0 (LLM local)
- SQLite (metadatos)
- SQL Server (datos operacionales del cliente)
- SignalR (tiempo real)
- ML.NET (predicciones)

---

## EL PROBLEMA ESPECÍFICO

### Situación Actual
El proyecto **no tiene sistema de autenticación integrado**. Cada cliente/usuario se identifica solo con un "UserId" simple.

### Requisitos del Usuario

**Para su empresa (ya implementado):**
- Usar las tablas existentes del ERP de la empresa (catalogo de usuarios y permisos)
- No duplicar usuarios ni contraseñas
- Aprovechar los permisos existentes del ERP

**Para clientes externos (planeado):**
- Cada cliente tiene su propia fuente de autenticación:
  - Tablas de su propia base de datos
  - Active Directory / Azure AD
  - Tabla local simple
- Necesitan una solución flexible sin código nuevo por cliente

### Modelo de Negocio
| Aspecto | Detalle |
|---------|---------|
| Target | PYMES que necesitan consultas SQL |
| Precio | $500-1000 USD implementación / $200-500 USD/mes hosting |
| Diferenciador | Sin subscriptions, datos locales, privacy-first |
| Modelo | Producto + implementación + soporte |

---

## RESTRICCIÓN IMPORTANTE

El usuario NO quiere sobre-ingeniería. Quiere algo simple que funcione para su fase "garage" pero que no le cierre puertas para el futuro.

**Fases planeadas:**
1. **Ahora:** Su empresa → usa tablas ERP ✅
2. **Fase garage:** 1-2 clientes → tabla configurable simple
3. **Fase comercial:** Refactorizar si el negocio crece

---

## PREGUNTA PARA EL ARQUITECTO

Diseñar una solución de autenticación que:

1. **Soporte múltiples proveedores de identidad:**
   - Tablas de base de datos del cliente (ERP)
   - Active Directory / Azure AD (futuro)
   - Base de datos local simple (futuro)

2. **Sea configurable sin recompilar:**
   - El cliente debe poder configurar su fuente de autenticación via JSON/config
   - No requiere cambios de código por cliente

3. **No sea sobre-ingeniería:**
   - Solución práctica para la fase actual
   - Base para escalar después si es necesario

4. **Consideraciones adicionales:**
   - El proyecto usa .NET 10
   - Ya tiene una arquitectura de "Providers" (ISystemConfigProvider, IConnectionProfileStore)
   - No tiene tests unitarios todavía
   - El usuario es el único desarrollador

---

## FORMATO DE RESPUESTA DESEADO

1. **Patrón de diseño recomendado** (con justificación)
2. **Diagrama de arquitectura propuesto**
3. **Código de ejemplo** para:
   - Interfaz/contrato
   - Implementaciones concretas
   - Configuración JSON
4. **Pasos de implementación** priorizados
5. **Cómo hacer la migración gradual** (si aplica)
6. **Riesgos y mitigaciones**
7. **Tiempo estimado** de implementación

---

## REFERENCIAS EXISTENTES EN EL PROYECTO

El proyecto YA usa un patrón similar para configuración:

```csharp
// Ejemplo de cómo ya funciona la configuración:
builder.Services.AddSingleton<IOperationalConnectionResolver, OperationalConnectionResolver>();
builder.Services.AddSingleton<ISecretResolver, CompositeSecretResolver>();
```

```csharp
// Modelo de ConnectionProfile ya existente:
public class ConnectionProfile
{
    public string ProviderKind { get; set; }  // "SqlServer"
    public string ConnectionMode { get; set; } // "CompositeSqlServer", "FullStringRef"
    public string? SecretRef { get; set; }   // "env:", "config:"
}
```

---

¿Puedes diseñar una solución completa para este problema de autenticación configurable?
