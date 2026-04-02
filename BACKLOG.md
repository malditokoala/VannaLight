# Backlog VannaLight

## Estado del piloto

### Ya logrado
- Onboarding/admin capaz de configurar y validar más de una base de datos.
- Separación de contextos de consulta en runtime:
  - `tenantKey`
  - `domain`
  - `connectionName`
- Soporte inicial para ERP y Northwind con conexiones separadas.
- `index` con selector de contexto para consultas.
- Exportación de resultados a CSV y PDF.
- Wizard de onboarding con mejor UX:
  - copy más claro
  - validación inline
  - pasos guiados
  - persistencia local del workspace actual
  - tour y foco visual por paso
- Admin más context-aware:
  - workspaces
  - contextos runtime
  - filtros por workspace
  - tabs de dominio atadas al contexto activo
- Hardening de cancelaciones por refresh (`499 Client Closed Request`).
- Mitigación de concurrencia del cliente LLM local.
- `TrainingExamples` migrados a memoria por contexto:
  - `TenantKey`
  - `Domain`
  - `ConnectionName`
  - `Question`

## Pendiente para cerrar el piloto

### P0
- Implementar fast path para `TrainingExamples` verificados por contexto.
  - Si hay match exacto verificado, reutilizar SQL sin pasar por LLM.
- Revisar y aislar histórico legado.
  - Definir cómo tratar `QuestionJobs` y `TrainingExamples` previos al soporte multi-contexto.
- Filtrar el editor/historial de entrenamiento RAG por contexto activo.
- Correr validación E2E formal en:
  - ERP
  - Northwind
- Documentar checklist operativo de release:
  - persistencia de `vanna_memory.db`
  - persistencia de `vanna_runtime.db`
  - persistencia de `dpkeys`
  - conexiones requeridas por ambiente

### P1
- Mejorar visibilidad del contexto activo en todas las pantallas de Admin.
- Confirmar que todas las pestañas de Admin queden bloqueadas o vacías si no hay contexto activo.
- Revisar reutilización/caché por pregunta exacta:
  - `UserId`
  - `TenantKey`
  - `Domain`
  - `ConnectionName`
- Añadir estrategia explícita para contexto frío:
  - qué usa cuando no hay preguntas previas
  - qué seeds mínimos conviene tener

## Features WOW aprobadas

### 1. Exploración guiada sobre la última respuesta
Alcance aprobado:
- chips sugeridos sobre la última consulta exitosa
- generación por reglas, no por interpretación libre del LLM
- basada en:
  - métrica
  - intención
  - dimensión
  - tiempo

Ejemplos de chips:
- `Ver tendencia`
- `Comparar con ayer`
- `Ver por turno`
- `Ver detalle`
- `Top 10`

Fuera de alcance por ahora:
- multi-turno libre
- explicación causal automática
- `¿por qué ocurrió esto?`
- segundo modelo para interpretación

### 2. Historial local Top 10 de consultas
Alcance aprobado:
- guardar últimas 10 consultas en `localStorage`
- incluir:
  - pregunta
  - timestamp
  - SQL
  - resumen/contexto mínimo
- acciones:
  - `Ver`
  - `Re-ejecutar`
  - `Usar como base`

Fuera de alcance por ahora:
- historial persistido en backend
- conversaciones completas
- guardar datasets grandes en storage local

## Futuro / siguiente versión
- alertas por reglas creadas por usuario
- dashboards o widgets personales
- consultas fijadas
- RAG de documentos más visible para usuario final
- ML.NET como feature visible de negocio
- multi-canal / WhatsApp
- conversación multi-turno libre
- capacidades causales o interpretativas avanzadas

## Definition of Done del piloto
- Cada contexto consulta su propia base y su propia configuración.
- Admin no mezcla configuraciones entre ERP y Northwind.
- Una pregunta verificada puede reutilizar SQL del contexto correcto.
- El sistema responde bien tanto en contexto frío como en contexto con historial.
- El flujo de onboarding es suficientemente claro para un admin técnico.
- Existe checklist operativo para levantar el piloto sin depender de memoria oral.
