# Auditoria Tecnica - VannaLight (v4)

**Fecha:** 14 de abril de 2026  
**Auditoria:** actualizada contra el estado real del repo y el backlog operativo  
**Proyecto:** VannaLight - asistente industrial local-first para SQL, documentos PDF y ML

---

## 1. Resumen Ejecutivo

VannaLight ya no esta en fase de prototipo basico. El proyecto entro en una etapa de piloto funcional con:

- consultas SQL multi-contexto
- admin operativo para onboarding, reglas, hints, patterns y entrenamiento
- carril documental PDF funcional
- primeros flujos de ML / forecasting

La direccion tecnica general es buena, pero el sistema todavia depende demasiado de:

- estado local mutable por maquina
- memoria operativa sembrada correctamente
- rutas demo especiales para lograr respuestas consistentes

La conclusion de esta auditoria es:

**el piloto esta bien encaminado y es demostrable, pero su estabilidad sigue dependiendo de disciplina operativa y de reducir acoplamientos residuales.**

### Puntuacion actual

| Area | Estado | Puntuacion | Comentario |
|------|--------|------------|------------|
| Arquitectura | Bien encaminada | 7.5/10 | Mejor separacion por contexto, pero todavia hay piezas tacticas duras |
| Operacion local | Fragil | 5.5/10 | `%LOCALAPPDATA%`, seeds y DBs locales requieren mas blindaje |
| SQL / Text-to-SQL | Funcional con deuda | 7/10 | Ya hay patterns, reuse y self-correction, pero el contexto frio sigue pesando |
| Prompt grounding | Mejorado | 6.5/10 | Mejor que antes, pero depende de schema docs / hints bien sembrados |
| RAG documental | Funcional | 6.5/10 | Ya responde y tiene timeouts, pero falta terminar performance/UX |
| Admin / UX operativa | Buena | 8/10 | Mucho mas usable y context-aware |
| Observabilidad | Aceptable | 6.5/10 | Ya hay `LlmPerf`, `SqlPerf`, `DocsPerf`, pero faltan health checks mas visibles |
| Testing | Bajo | 2/10 | Hay validacion manual fuerte, pero casi nada automatizado |
| Mantenibilidad | Media | 6/10 | Va mejorando, pero quedan zonas con hardcodeo tactico |

**Puntuacion global estimada:** **6.6/10**

---

## 2. Estado Actual del Proyecto

### 2.1 Lo que ya esta bien resuelto

- multi-contexto real por:
  - `TenantKey`
  - `Domain`
  - `ConnectionName`
- admin context-aware
- separacion ERP / Northwind
- soporte de memoria local por contexto
- `TrainingExamples` verificados por contexto
- self-correction SQL con maximo 1 reintento
- historial local Top 10 en `index`
- carril PDF funcional con timeout defensivo
- perfiles de hardware LLM editables desde Admin
- logging de performance:
  - `LlmPerf`
  - `SqlPerf`
  - `DocsPerf`

### 2.2 Lo que sigue siendo sensible

- arranque frio de SQL cuando la memoria local no trae enough:
  - `TrainingExamples`
  - `SchemaDocs`
  - `SemanticHints`
  - `QueryPatterns`
- variaciones entre PC de trabajo y PC de casa
- dependencia de semillas operativas locales
- compilacion / despliegue local trabado por binarios abiertos en Visual Studio

---

## 3. Hallazgos Principales

### 3.1 [Alta] El piloto sigue dependiendo de memoria local mutable

El cambio a `%LOCALAPPDATA%\VannaLight\Data` fue correcto como direccion arquitectonica, pero dejo claro que el sistema puede verse "roto" si la memoria local de una maquina queda vacia o incompleta.

Impacto observado:

- dominios activos sin `AllowedObjects`
- prompt con hints genericos, pero sin grounding suficiente
- historial RAG inconsistente entre maquinas
- consultas demo cayendo al LLM en frio

Estado actual:

- ya existe `appsettings.Local.json`
- ya hay warnings al arranque
- ya se reconstruyo memoria minima para la PC de trabajo

Falta:

- recovery guiado
- seed minimo garantizado por dominio
- diagnostico visible en UI y no solo en logs

### 3.2 [Alta] El carril SQL mejora mucho con rutas deterministicas, pero todavia no esta del todo desacoplado

Durante esta etapa se confirmo que preguntas demo de alto valor:

- `Que prensa lleva mas scrap en el turno actual?`
- `Cuales son los 5 numeros de parte con mas scrap?`

no conviene dejarlas al LLM puro.

Ya se corrigio bastante:

- patterns demo para scrap
- schema docs forzados para `vw_KpiScrap_v1`
- semantic hints de columna:
  - `ScrapQty`
  - `PartNumber`
  - `OperationDate`
  - `ShiftId`

Sin embargo, el sistema todavia conserva dependencia de rutas tacticas y seeds especiales.

### 3.3 [Media-Alta] El prompt SQL tenia estructura correcta, pero podia quedar "ciego"

Se confirmo con prompts reales que en algunos casos entraban:

- `PISTAS SEMANTICAS DEL DOMINIO`
- `OBJETOS SQL PERMITIDOS`

pero no necesariamente:

- `ESQUEMAS RELEVANTES RECUPERADOS`
- `EJEMPLOS RELEVANTES`

Eso llevo a columnas inventadas como:

- `ScrapQuantity`
- `Qty`

Estado actual:

- ya se reforzo el grounding
- ya se siembran hints de columna
- ya se fuerza `SchemaDocs` de `dbo.vw_KpiScrap_v1` en preguntas sensibles

Falta:

- documentar minimos obligatorios por dominio
- seguir reduciendo dependencia del LLM para consultas demo

### 3.4 [Media] `TemplateSqlBuilder` tenia hardcodeo excesivo

El builder cumplio bien como solucion tactica del piloto, pero estaba creciendo con demasiada logica por `PatternKey`.

Estado actual:

- ya se migro una parte a templates declarativos
- el startup siembra `SqlTemplate` reutilizable para varias rutas demo
- el builder ya puede resolver tokens por:
  - metrica
  - dimension
  - tiempo
- ya existe fallback generico para:
  - consultas agrupadas/top
  - consultas de total simple

Concluson:

**ya se corrigio el riesgo principal, pero todavia no esta terminada la migracion completa a modelo declarativo.**

### 3.5 [Media] El carril documental ya es usable, pero aun requiere estabilizacion de performance

Progreso real:

- ya indexa por dominio
- ya tiene timeout
- ya evita pendientes infinitos
- ya instrumenta tiempos por etapa

Lo pendiente no es "hacerlo funcionar", sino:

- reducir latencia
- mejorar prefiltrado por numero de parte
- terminar feedback de usuario final

### 3.6 [Alta] La automatizacion de pruebas sigue siendo muy baja

El proyecto mejoro mucho por validacion manual dirigida, pero sigue con una brecha seria:

- casi no hay pruebas automatizadas
- no hay regresion fuerte sobre carriles demo
- mucho depende de pruebas interactivas y memoria operativa del equipo

Esto es el principal riesgo tecnico si el piloto se sigue moviendo o crece de alcance.

---

## 4. Cambios Positivos Relevantes Detectados

### 4.1 SQL / Prompt / Retrieval

- reuse exacto por `TrainingExamples` verificados
- self-correction con un reintento
- logging `SqlPerf`
- logging `LlmPerf`
- schema grounding mas rico
- hints de columna para KPI scrap
- patterns demo reforzados

### 4.2 Runtime local

- soporte real de perfiles Hardware LLM
- timeout SQL configurable
- chequeos defensivos de memoria local al arranque
- separacion mas clara entre PC trabajo y PC casa

### 4.3 Admin

- filtro de secciones en dropdown
- dominios y contextos mas seguros
- docs PDF mucho mas claro
- feedback visual de reindex y upload
- editor RAG filtrado por contexto activo

### 4.4 Declaratividad

- primer paso serio para salir del hardcodeo del builder SQL
- templates reutilizables por:
  - metrica
  - dimension
  - tiempo

---

## 5. Riesgos Actuales para el Piloto

### Riesgo 1. Contexto frio o memoria local incompleta

Probabilidad: alta  
Impacto: alto

Mitigacion actual:

- warnings en startup
- reconstruccion minima por onboarding
- backlog con minimos obligatorios por dominio

### Riesgo 2. Dependencia del LLM para preguntas demo que deberian ser deterministicas

Probabilidad: media  
Impacto: alto

Mitigacion actual:

- patterns demo
- templates declarativos
- hints de columna

### Riesgo 3. Diferencias entre entornos de trabajo y casa

Probabilidad: alta  
Impacto: medio-alto

Mitigacion actual:

- `appsettings.Local.json`
- prune de contexts por maquina
- handoff de trabajo

### Riesgo 4. Poca cobertura automatizada

Probabilidad: alta  
Impacto: alto

Mitigacion actual:

- ninguna fuerte

Este sigue siendo el riesgo mas grande de mediano plazo.

---

## 6. Recomendaciones Prioritarias

### Fase inmediata

- consolidar los templates declarativos del carril SQL
- documentar minimos obligatorios por dominio:
  - `AllowedObjects`
  - `SchemaDocs`
  - `SemanticHints`
  - `TrainingExamples`
- cerrar checklist operativo de recovery local
- terminar performance del carril documental

### Fase corta posterior al piloto

- pruebas automatizadas de rutas demo SQL
- health check visible para:
  - contexto activo
  - memoria minima
  - perfil LLM
  - docs indexados
- seguir migrando `TemplateSqlBuilder` hacia configuracion declarativa

### Fase de estabilizacion

- menos dependencia de seeds desde startup
- export/import formal de conocimiento por dominio
- smoke tests automatizados por carril:
  - SQL
  - PDF
  - ML

---

## 7. Veredicto Final

VannaLight esta en una posicion mucho mejor que la que reflejaba la auditoria anterior.

No estamos frente a un prototipo caotico. Estamos frente a un piloto:

- funcional
- demostrable
- con buena direccion arquitectonica

pero todavia con deuda clara en:

- operacion local
- cobertura automatizada
- y reduccion completa de acoplamientos tacticos

### Veredicto

**El proyecto esta listo para cerrar el piloto con confianza razonable, siempre que se mantenga la disciplina operativa y se completen los guardarrailes ya identificados en backlog.**

---

## 8. Anexos

### Evidencia tecnica reciente considerada

- [BACKLOG.md](C:/Users/edggom/source/repos/malditokoala/VannaLight/BACKLOG.md)
- [HANDOFF_TRABAJO.md](C:/Users/edggom/source/repos/malditokoala/VannaLight/HANDOFF_TRABAJO.md)
- [Program.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Program.cs)
- [AskUseCase.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Core/UseCases/AskUseCase.cs)
- [TemplateSqlBuilder.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure/Retrieval/TemplateSqlBuilder.cs)
- [PatternMatcherService.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure/Retrieval/PatternMatcherService.cs)

### Nota metodologica

Esta auditoria se enfoca en:

- estado real del piloto
- comportamiento observado en trabajo reciente
- arquitectura y deuda tecnica efectiva

No intenta ser un inventario exhaustivo de cada archivo del repo, sino una fotografia accionable para toma de decisiones tecnica.
