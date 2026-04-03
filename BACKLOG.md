# Backlog VannaLight

## Estado del piloto

### Ya logrado
- Onboarding/admin capaz de configurar y validar mas de una base de datos.
- Separacion de contextos de consulta en runtime:
  - `tenantKey`
  - `domain`
  - `connectionName`
- Soporte inicial para ERP y Northwind con conexiones separadas.
- `index` con selector de contexto para consultas.
- Exportacion de resultados a CSV y PDF.
- Wizard de onboarding con mejor UX:
  - copy mas claro
  - validacion inline
  - pasos guiados
  - persistencia local del workspace actual
  - tour y foco visual por paso
- Admin mas context-aware:
  - workspaces
  - contextos runtime
  - filtros por workspace
  - tabs de dominio atadas al contexto activo
- Hardening de cancelaciones por refresh (`499 Client Closed Request`).
- Mitigacion de concurrencia del cliente LLM local.
- `TrainingExamples` migrados a memoria por contexto:
  - `TenantKey`
  - `Domain`
  - `ConnectionName`
  - `Question`

## Pendiente para cerrar el piloto

### P0
- Implementar fast path para `TrainingExamples` verificados por contexto.
  - Si hay match exacto verificado, reutilizar SQL sin pasar por LLM.
- Implementar self-correction con maximo 1 reintento para SQL fallido.
  - Aplicar solo en:
    - `ValidationError`
    - `DryRunError`
  - En el segundo intento, no repetir el mismo prompt:
    - reenviar la pregunta original
    - incluir el SQL fallido
    - incluir el error exacto de validacion o compilacion
    - reforzar restricciones:
      - usar solo objetos permitidos
      - no inventar tablas ni columnas
      - conservar la intencion original
  - Mejorar la probabilidad de acierto del reintento con mas contexto:
    - pasar el error exacto al modelo
    - si aplica, inyectar mas y mejores ejemplos del mismo contexto
    - priorizar ejemplos verificados del mismo `tenant/domain/connection`
  - Si vuelve a fallar:
    - responder `no pude`
    - marcar `RequiresReview`
- Revisar y aislar historico legado.
  - Definir como tratar `QuestionJobs` y `TrainingExamples` previos al soporte multi-contexto.
- Filtrar el editor/historial de entrenamiento RAG por contexto activo.
- Correr validacion E2E formal en:
  - ERP
  - Northwind
- Documentar checklist operativo de release:
  - persistencia de `vanna_memory.db`
  - persistencia de `vanna_runtime.db`
  - persistencia de `dpkeys`
  - conexiones requeridas por ambiente

### P1
- Mejorar visibilidad del contexto activo en todas las pantallas de Admin.
- Confirmar que todas las pestanas de Admin queden bloqueadas o vacias si no hay contexto activo.
- Revisar reutilizacion/cache por pregunta exacta:
  - `UserId`
  - `TenantKey`
  - `Domain`
  - `ConnectionName`
- Anadir estrategia explicita para contexto frio:
  - que usa cuando no hay preguntas previas
  - que seeds minimos conviene tener

## Features WOW aprobadas

### 1. Exploracion guiada sobre la ultima respuesta
Alcance aprobado:
- chips sugeridos sobre la ultima consulta exitosa
- generacion por reglas, no por interpretacion libre del LLM
- basada en:
  - metrica
  - intencion
  - dimension
  - tiempo

Ejemplos de chips:
- `Ver tendencia`
- `Comparar con ayer`
- `Ver por turno`
- `Ver detalle`
- `Top 10`

Fuera de alcance por ahora:
- multi-turno libre
- explicacion causal automatica
- `por que ocurrio esto?`
- segundo modelo para interpretacion

### 2. Historial local Top 10 de consultas
Alcance aprobado:
- guardar ultimas 10 consultas en `localStorage`
- incluir:
  - pregunta
  - timestamp
  - SQL
  - resumen/contexto minimo
- acciones:
  - `Ver`
  - `Re-ejecutar`
  - `Usar como base`

Fuera de alcance por ahora:
- historial persistido en backend
- conversaciones completas
- guardar datasets grandes en storage local

## Futuro / siguiente version
- alertas por reglas creadas por usuario
- dashboards o widgets personales
- consultas fijadas
- RAG de documentos mas visible para usuario final
- ML.NET como feature visible de negocio
- multi-canal / WhatsApp
- conversacion multi-turno libre
- capacidades causales o interpretativas avanzadas

## Definition of Done del piloto
- Cada contexto consulta su propia base y su propia configuracion.
- Admin no mezcla configuraciones entre ERP y Northwind.
- Una pregunta verificada puede reutilizar SQL del contexto correcto.
- El sistema responde bien tanto en contexto frio como en contexto con historial.
- El flujo de onboarding es suficientemente claro para un admin tecnico.
- Existe checklist operativo para levantar el piloto sin depender de memoria oral.
