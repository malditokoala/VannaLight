# Backlog VannaLight

## Estado del piloto

## Semana de presentacion

### Hecho hoy
- Limpieza del repo principal para sacar artefactos de pruebas que ya no pertenecian a `VannaLight`.
- Separacion del laboratorio de conversion documental a un proyecto independiente fuera de `VannaLight`.
- Preparacion de preguntas sugeridas dentro del chat principal para facilitar demos guiadas por modo:
  - `SQL`
  - `PDF`
  - `ML`
- Correccion del bootstrap de contextos para demo:
  - los seeds validos vuelven a activarse al arrancar
  - los contextos `UserManaged` con conexion activa pueden recuperarse si quedaron dormidos en SQLite local
- Reparacion de estado local para reactivar:
  - `northwind-demo / northwind-sales / NorthwindDb`
  - `zenit-mx / northwind-zenit / NorthwindDB`
- Se agrego handoff operativo para la PC del trabajo en `HANDOFF_TRABAJO.md`.

### Prioridad de esta semana
- Ensayar y validar 5-10 preguntas de demo que respondan bien de forma consistente.
- Confirmar ambiente de demo:
  - contexto activo correcto
  - conexiones correctas
  - datos accesibles
  - documentos indexados
- Pulir el recorrido de presentacion:
  - apertura
  - consulta estrella
  - cambio de contexto
  - admin rapido
  - exportacion
- Preparar plan de contingencia si falla:
  - consulta alternativa
  - contexto alternativo
  - captura o evidencia de respaldo

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
- Reutilizacion exacta de `TrainingExamples` verificados por contexto en el flujo SQL.
- Self-correction con maximo 1 reintento para SQL fallido:
  - usa la pregunta original
  - usa el SQL fallido
  - usa el error exacto de validacion/dry-run
- `index` con UX reforzada:
  - banner visible de estado/error
  - contexto activo mas visible
  - historial local Top 10 de consultas
  - acciones para reutilizar o re-ejecutar consultas recientes
- Modo documental (`PDF`) mas robusto:
  - dominio documental atado al contexto activo del piloto
  - Admin docs con mejor feedback visual de upload/reindex
  - timeout explicito para evitar pendientes eternos en consultas documentales
  - carga lazy del router/modelo documental
  - instrumentacion de tiempos en el pipeline documental (`DocsPerf`)
- Admin/System Config con controles mas seguros:
  - filtro por seccion en dropdown
  - dropdowns de dominio donde antes se tecleaba manualmente
  - assets versionados para evitar frontend viejo en cache
- Contextos locales por maquina mas robustos:
  - `appsettings.Local.json` como override local no versionado
  - poda de contextos/tenants/conexiones ajenos al ambiente actual
  - aislamiento mas claro entre PC de trabajo y PC de casa
- Reindex documental mas consistente por dominio:
  - upload/reindex y status conscientes del dominio activo
  - si un PDF ya existe por hash pero cambia de dominio, ahora se reasigna al dominio nuevo
- Admin con mejor claridad visual:
  - barra/contexto visible en tabs de dominio
  - filtros ligados al contexto activo
  - contextos filtrados por workspace seleccionado
- Perfiles de conexion separados para ERP y Northwind:
  - `ErpDb`
  - `NorthwindDb`

## Pendiente para cerrar el piloto

### P0 - Cierre del piloto esta semana

#### 0. Optimizar primero el carril documental por parseo y prefiltrado
Impacto: muy alto  
Necesidad: critica

- Siguiente sesion: tomar esto como primer frente de trabajo.
- Hipotesis actual:
  - el mayor costo del carril `PDF` probablemente esta en `ParseMs`
  - es decir, en el router/modelo documental local
- Mejora candidata de mayor retorno:
  - prefiltrar chunks por numero de parte antes de pasar al modelo
- Objetivo:
  - bajar latencia de consultas documentales especificas
  - reducir carga innecesaria sobre el router/modelo local
  - mantener precision sin volver mas fragil el flujo
- Validar con logs `DocsPerf`:
  - `RetrieveMs`
  - `ScoreMs`
  - `ParseMs`
  - `ComposeMs`
  - `TotalMs`

#### 1. Validacion E2E formal multi-contexto
Impacto: muy alto  
Necesidad: critica

- Estado actual:
  - pruebas principales corridas el fin de semana con los cambios recientes
  - SQL y Admin multi-contexto validados a nivel funcional
- Remate pendiente:
  - consolidar evidencia minima del carril documental (`PDF`)
  - dejar criterio de tiempo esperado documentado para consultas docs

#### 2. Revisar y aislar historico legado
Impacto: muy alto  
Necesidad: critica

- Estado actual:
  - en progreso
  - ya se saco del camino automatico el reuse/retrieval de registros sin contexto confiable
- Definir como tratar `QuestionJobs` y `TrainingExamples` previos al soporte multi-contexto.
- Evitar que el historico viejo contamine:
  - retrieval
  - fast path
  - entrenamiento
- Decidir si:
  - se marca como `legacy`
  - se excluye del reuse automatico
  - o se migra/manualmente revalida

#### 3. Filtrar el editor/historial de entrenamiento RAG por contexto activo
Impacto: muy alto  
Necesidad: critica

- Estado actual:
  - implementacion principal hecha en Admin
  - sin contexto activo ya no muestra historial SQL global
  - el editor de correccion solo trabaja sobre el contexto activo
- El historial reciente y el editor de correccion deben mostrar solo consultas del contexto activo.
- Evitar mezclar consultas ERP y Northwind en la revision admin.

#### 4. Checklist operativo de release
Impacto: alto  
Necesidad: critica

- Estado actual:
  - en progreso
  - ya existe override local por maquina con `appsettings.Local.json`
  - ya se identifico que `vanna_memory.db` y `vanna_runtime.db` no deben tratarse como fuente compartida entre PCs
- Documentar:
  - persistencia de `vanna_memory.db`
  - persistencia de `vanna_runtime.db`
  - persistencia de `dpkeys`
  - conexiones requeridas por ambiente
  - pasos de arranque
  - recuperacion basica ante cambio de connection string o contexto

### P1 - Estabilidad y robustez inmediata

#### 5. Endurecer la experiencia de error en `index`
Impacto: alto  
Necesidad: alta

- Diferenciar visualmente:
  - `ValidationError`
  - `DryRunError`
  - `ExecutionError`
  - `RequiresReview`
- Extender al carril `PDF`:
  - timeout documental visible al usuario
  - mensaje claro cuando el modelo documental tarda demasiado
  - evitar estados `Pendiente` indefinidos
- Mostrar siguiente accion sugerida cuando falle:
  - ver SQL
  - reintentar
  - revisar en Admin

#### 6. Revisar reutilizacion/cache por pregunta exacta
Impacto: alto  
Necesidad: alta

- Confirmar el comportamiento por:
  - `UserId`
  - `TenantKey`
  - `Domain`
  - `ConnectionName`
- Validar si el criterio actual es demasiado estricto para el piloto.

#### 7. Estrategia explicita para contexto frio
Impacto: medio-alto  
Necesidad: alta

- Definir que usa cuando no hay preguntas previas.
- Definir que seeds minimos conviene tener:
  - examples
  - hints
  - patterns
  - rules

#### 8. Mantener adapters, pero hacer el provider menos rigido
Impacto: medio-alto  
Necesidad: alta

- Mantener `Industrial` y `Northwind` para el piloto.
- Evitar que el provider crezca con branching manual por dominio.
- Hacer que cada adapter exponga algo como:
  - `CanHandle(context)`
  - o `SupportedDomains`
- El provider debe resolver por capacidad/registro y no por cadena codificada.

#### 9. Asegurar que los adapters sean livianos
Impacto: medio  
Necesidad: alta

- Un adapter no debe hacer I/O pesado en constructor.
- Evitar trabajo pesado en startup.
- Mover a carga lazy cualquier inicializacion costosa si existiera.

#### 10. Medir el arranque real antes de optimizar
Impacto: medio  
Necesidad: media-alta

- Medir:
  - tiempo de arranque de la API
  - tiempo de cada `Ensure...`
  - tiempo de primera consulta SQL
  - tiempo de primera consulta Docs
  - tiempo de primera consulta Prediction
- Optimizar con datos y no por intuicion.

#### 11. Medir y ajustar performance del carril documental
Impacto: medio-alto  
Necesidad: media-alta

- Estado actual:
  - ya existe instrumentacion `DocsPerf` en backend
  - ya hay timeout defensivo en parseo/composicion
- Siguiente paso:
  - correr consultas docs reales y capturar:
    - `RetrieveMs`
    - `ScoreMs`
    - `ParseMs`
    - `ComposeMs`
    - `TotalMs`
  - decidir si el primer cuello de botella esta en:
    - retrieval/scoring
    - o router/modelo documental
  - bajar tiempo objetivo del carril docs hacia un rango mas consistente

### P2 - Mejora evolutiva despues del piloto

#### 12. Evolucionar domain packs/adapters hacia un modelo mas declarativo
Impacto: medio-alto  
Necesidad: media

- Mantener los adapters actuales como solucion tactica.
- Llevar gradualmente a configuracion:
  - metricas
  - dimensiones
  - calendarios
  - defaults del dominio
- Dejar en codigo solo la logica realmente especial.

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
Estado: implementacion inicial hecha en `index`  
Pendiente para completarla:
- refinar UX de reutilizacion y contexto
- decidir si se conecta o no con historial backend mas adelante

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
- El historico legado no contamina el contexto actual.
- El sistema responde bien tanto en contexto frio como en contexto con historial.
- El flujo de onboarding es suficientemente claro para un admin tecnico.
- El historial y editor RAG respetan el contexto activo.
- El arranque del sistema es aceptable y entendido con medicion basica.
- Existe checklist operativo para levantar el piloto sin depender de memoria oral.
