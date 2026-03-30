# Manual Técnico de VannaLight

## 1. Propósito de este documento

Este manual explica cómo funciona VannaLight de manera técnica pero fácil de seguir.

La idea es que sirva para:

- entender la arquitectura del sistema
- saber cómo se configura
- ubicar dónde vive cada parte
- entender el flujo de una pregunta
- tener una base estable para actualizar el documento cuando cambie el sistema

Este documento está escrito pensando en alguien junior que necesita entender el proyecto sin perderse.

---

## 2. Qué es VannaLight

VannaLight es un sistema que permite hacer preguntas en lenguaje natural y obtener respuestas desde varias capacidades:

- **SQL / Data**: genera o construye SQL para consultar una base operativa
- **Docs / RAG**: responde usando documentos indexados
- **Predictions / ML.NET**: genera respuestas relacionadas con predicción o forecasting

Hoy el corazón del producto está en el flujo **Text-to-SQL**, con una capa administrativa para configurar el sistema sin depender tanto de hardcodeo.

---

## 3. Arquitectura general

El sistema sigue una estructura tipo Clean Architecture con tres proyectos principales:

- [VannaLight.Core](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Core)
- [VannaLight.Infrastructure](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure)
- [VannaLight.Api](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api)

### 3.1 Qué hace cada capa

#### Core

La capa `Core` contiene:

- modelos de dominio
- contratos (`Interfaces`)
- casos de uso
- reglas de aplicación

Aquí vive la lógica principal de negocio, por ejemplo:

- [AskUseCase.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Core/UseCases/AskUseCase.cs)
- [TrainExampleUseCase.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Core/UseCases/TrainExampleUseCase.cs)

#### Infrastructure

La capa `Infrastructure` implementa los contratos del `Core`.

Aquí vive:

- acceso a SQLite
- acceso a SQL Server
- retrieval
- validación de SQL
- ejecución de dry-run
- integración con LLM

Ejemplos:

- [SqliteTrainingStore.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure/Data/SqliteTrainingStore.cs)
- [PatternMatcherService.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure/Retrieval/PatternMatcherService.cs)
- [TemplateSqlBuilder.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure/Retrieval/TemplateSqlBuilder.cs)
- [StaticSqlValidator.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure/Security/StaticSqlValidator.cs)

#### Api

La capa `Api` expone el sistema al exterior.

Aquí vive:

- controllers HTTP
- SignalR
- worker de inferencia
- UI web (`index.html`, `admin.html`)
- configuración de arranque

Ejemplos:

- [Program.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Program.cs)
- [AssistantController.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Controllers/AssistantController.cs)
- [AdminController.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Controllers/AdminController.cs)
- [InferenceWorker.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Services/InferenceWorker.cs)

---

## 4. Bases de datos del sistema

VannaLight usa dos SQLite principales:

### 4.1 `vanna_memory.db`

Es la base de **memoria/configuración/conocimiento**.

Aquí viven cosas como:

- `SystemConfigProfiles`
- `SystemConfigEntries`
- `ConnectionProfiles`
- `Tenants`
- `TenantDomains`
- `AllowedObjects`
- `BusinessRules`
- `SchemaDocs`
- `TrainingExamples`
- `QueryPatterns`
- `QueryPatternTerms`
- `SemanticHints`

En simple:

> Si algo define cómo piensa o se configura el motor, normalmente vive aquí.

### 4.2 `vanna_runtime.db`

Es la base de **runtime/historial/operación**.

Aquí viven cosas como:

- `QuestionJobs`
- estados de ejecución
- SQL generado
- resultados
- errores
- review
- feedback del usuario

En simple:

> Si algo describe qué pasó durante una ejecución, normalmente vive aquí.

---

## 5. Configuración del sistema

### 5.1 Configuración de arranque

El arranque base está en:

- [appsettings.json](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/appsettings.json)

Claves importantes:

- `SystemStartup:EnvironmentName`
- `SystemStartup:DefaultSystemProfile`
- `Paths:Sqlite`
- `Paths:RuntimeDb`
- `Paths:Model`
- `ConnectionStrings:OperationalDb`

### 5.2 Configuración operativa

La configuración operativa real vive cada vez más en:

- `SystemConfigEntries`

Ejemplos:

- `Prompting:*`
- `Retrieval:*`
- `UiDefaults:*`
- `TenantDefaults:*`

Esto permite cambiar comportamiento desde Admin sin recompilar.

### 5.3 Secrets

Las credenciales sensibles no deberían quedar en texto plano en SQLite si pueden evitarse.

El sistema contempla resolución de secretos vía:

- [CompositeSecretResolver.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure/Configuration/CompositeSecretResolver.cs)

---

## 6. Flujo principal de una pregunta SQL

La mejor forma de entender VannaLight es seguir una pregunta desde que entra hasta que responde.

### Paso 1. El usuario pregunta

El usuario hace una pregunta en el chat de [index.html](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/wwwroot/index.html).

La API la recibe en:

- [AssistantController.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Controllers/AssistantController.cs)

### Paso 2. Se resuelve el contexto

Antes de procesar la pregunta, el sistema resuelve:

- `TenantKey`
- `Domain`
- `ConnectionName`

Esto lo hace:

- [ExecutionContextResolver.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure/Configuration/ExecutionContextResolver.cs)

### Paso 3. Se crea un `QuestionJob`

La pregunta se guarda en `vanna_runtime.db` como un trabajo.

Eso permite auditar:

- quién preguntó
- qué preguntó
- en qué tenant/domain/conexión
- qué SQL se generó
- qué resultado devolvió
- si fue corregido

### Paso 4. La pregunta entra a la cola

La API no resuelve todo directamente. Encola el trabajo en:

- [AskRequestQueue.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Services/AskRequestQueue.cs)

### Paso 5. El worker procesa la solicitud

El worker:

- [InferenceWorker.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Services/InferenceWorker.cs)

toma el job y llama al caso de uso principal:

- [AskUseCase.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Core/UseCases/AskUseCase.cs)

### Paso 6. Primero intenta `pattern-first`

Antes de usar el LLM, el sistema intenta reconocer si la pregunta cae en un patrón conocido.

Esto lo hace:

- [PatternMatcherService.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure/Retrieval/PatternMatcherService.cs)

Usa:

- `QueryPatterns`
- `QueryPatternTerms`

Si encuentra match fuerte, usa:

- [TemplateSqlBuilder.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure/Retrieval/TemplateSqlBuilder.cs)

Hoy este builder ya puede consumir `SqlTemplate` desde DB, con fallback a lógica legacy cuando hace falta.

### Paso 7. Si no hay patrón fuerte, cae al carril LLM

Entonces el sistema recupera contexto desde `vanna_memory.db`:

- `AllowedObjects`
- `BusinessRules`
- `SchemaDocs`
- `TrainingExamples`
- `SemanticHints`

El retrieval principal está en:

- [LocalRetriever.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure/Retrieval/LocalRetriever.cs)

Luego arma el prompt desde configuración externalizada y llama al LLM.

### Paso 8. El SQL se valida

Antes de ejecutar SQL real, el sistema pasa por:

- [StaticSqlValidator.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure/Security/StaticSqlValidator.cs)

Objetivo:

- evitar SQL peligroso
- evitar objetos no permitidos
- mantener un modo fail-closed

### Paso 9. Dry-run

Si el SQL pasó validación, puede compilarse primero sin ejecutarse realmente con:

- [SqlServerDryRunner.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Infrastructure/SqlServer/SqlServerDryRunner.cs)

### Paso 10. Ejecución real y respuesta

Si todo sale bien:

- se ejecuta la consulta
- se guarda el resultado en `QuestionJobs`
- se notifica al frontend por SignalR

---

## 7. Qué hace especial al flujo SQL

La parte importante es esta:

### No todo depende del LLM

VannaLight intenta ser más seguro y más gobernable que un Text-to-SQL completamente libre.

Por eso tiene:

- `pattern-first`
- examples verificados
- validación
- dry-run
- review

Esto ayuda a bajar errores y a mantener control operativo.

---

## 8. Cómo funciona el aprendizaje del sistema

VannaLight **no** aprende solo de forma automática como si fuera un sistema autónomo completo.

Lo que sí hace es:

- guardar preguntas y resultados
- permitir review
- permitir guardar ejemplos buenos en memoria

Cuando un admin corrige una consulta y la guarda en memoria:

- se actualiza `TrainingExamples`
- el sistema podrá recuperar ese ejemplo después
- eso mejora futuras respuestas parecidas

Eso se orquesta principalmente con:

- [TrainExampleUseCase.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Core/UseCases/TrainExampleUseCase.cs)

---

## 9. Flujo de feedback del usuario final

El usuario final hoy puede marcar:

- `👍 Correcta`
- `👎 Incorrecta`

Ese feedback:

- no entrena automáticamente
- no cambia SQL solo
- no modifica patterns directamente

Sirve para:

- priorizar revisión en Admin

Esto vive sobre `QuestionJobs`.

---

## 10. Admin: para qué sirve cada módulo

La UI administrativa principal está en:

- [admin.html](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/wwwroot/admin.html)
- [admin.js](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/wwwroot/js/admin.js)
- [admin.css](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/wwwroot/css/admin.css)

### Módulos importantes

#### Onboarding

Sirve para dar de alta un nuevo dominio/base:

1. definir `Tenant + Domain + Connection`
2. descubrir schema
3. seleccionar `AllowedObjects`
4. inicializar dominio
5. probarlo

#### System Config

Sirve para editar configuración operativa como:

- prompting
- retrieval
- defaults de UI

#### Allowed Objects

Sirve para definir qué tablas/vistas están permitidas para el motor.

#### Business Rules

Sirve para agregar reglas de negocio en lenguaje natural que ayudan al LLM.

#### Semantic Hints

Sirve para agregar contexto semántico útil:

- entidades
- medidas
- dimensiones
- pistas del dominio

#### Query Patterns

Sirve para operar:

- `QueryPatterns`
- `QueryPatternTerms`

Es la base del carril `pattern-first`.

#### Entrenamiento RAG

Sirve para revisar y guardar ejemplos útiles en memoria (`TrainingExamples`).

---

## 11. Multi-domain y multi-tenant

### Estado actual

Hoy VannaLight ya soporta bastante bien:

- múltiples dominios
- múltiples conexiones
- contexto por tenant/domain/conexión en runtime

Ya existen:

- `Tenants`
- `TenantDomains`
- `QuestionJobs` con:
  - `TenantKey`
  - `Domain`
  - `ConnectionName`

### Importante

Eso **no significa** que todo el sistema sea multi-tenant perfecto en todos los módulos, pero sí hay una base clara para llegar ahí.

---

## 12. Onboarding de una nueva base de datos

La visión correcta hoy es:

- no crear una SQLite distinta por cada base operativa
- usar una sola `vanna_memory.db`
- usar una sola `vanna_runtime.db`
- crear nuevos `Tenant/Domain/Connection` y su configuración

### Flujo recomendado

1. registrar o seleccionar conexión
2. crear `Tenant`
3. definir `Domain`
4. mapear `Tenant + Domain + Connection`
5. descubrir schema
6. seleccionar `AllowedObjects`
7. inicializar dominio
8. probar con preguntas reales

---

## 13. RAG de documentos

El sistema también tiene un carril para documentos.

Piezas importantes:

- [WiDocIngestor.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Services/WiDocIngestor.cs)
- [DocsIntentRouterLlm.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Services/DocsIntentRouterLlm.cs)
- [DocsAnswerService.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Services/DocsAnswerService.cs)

Su objetivo es:

- indexar documentos
- enrutar preguntas de tipo documental
- responder con citas relevantes

Este frente todavía debe seguir validándose y cerrándose con pruebas reales.

---

## 14. Forecasting / ML.NET

El sistema también tiene una línea de predicción.

Piezas importantes:

- [ForecastingService.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Services/Predictions/ForecastingService.cs)
- [PredictionIntentRouterLlm.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Services/Predictions/PredictionIntentRouterLlm.cs)
- [PredictionAnswerService.cs](C:/Users/edggom/source/repos/malditokoala/VannaLight/VannaLight.Api/Services/Predictions/PredictionAnswerService.cs)

Este frente también sigue en validación y debe revisarse por:

- configuración
- calidad del dato
- precisión de salida
- bugs de integración

---

## 15. Glosario rápido

### Tenant

Un cliente, workspace o unidad lógica aislada.

### Domain

El contexto semántico del negocio sobre el que opera el motor.

Ejemplo:

- `erp-kpi-pilot`
- `adventureworks-sales`

### ConnectionName

Nombre de una conexión operativa registrada.

Ejemplo:

- `OperationalDb`

### AllowedObjects

Lista de tablas/vistas que el motor sí puede usar.

### BusinessRules

Reglas de negocio en lenguaje natural que orientan al motor.

### SchemaDocs

Documentación estructural del schema SQL para ayudar al retrieval y al LLM.

### TrainingExamples

Ejemplos verificados de `pregunta -> SQL` que ayudan al sistema a recuperar buenos casos.

### QueryPatterns

Patrones de intención de pregunta.

### QueryPatternTerms

Términos que ayudan a reconocer esos patrones.

### SemanticHints

Pistas semánticas del dominio para mejorar prompting y retrieval.

### Pattern-first

Modo donde el sistema resuelve una pregunta usando patrones y templates antes de usar el LLM.

### Dry-run

Compilación/validación previa del SQL antes de ejecutarlo realmente.

### Review

Flujo donde una consulta problemática se revisa manualmente.

---

## 16. Qué revisar cuando algo falla

### Si una pregunta SQL no responde bien

Revisar:

1. si el `Domain` correcto está activo
2. si existen `AllowedObjects`
3. si hay `QueryPatterns`/`Terms` útiles
4. si el prompt tiene configuración válida
5. si el SQL fue bloqueado por el validator
6. si falló el dry-run
7. si el job quedó en review

### Si un dominio nuevo no funciona

Revisar:

1. conexión operativa
2. `TenantDomain` mapping
3. `AllowedObjects`
4. `SchemaDocs`
5. `SemanticHints`
6. preguntas de prueba del onboarding

### Si el frontend Admin se ve raro

Revisar:

1. caché del navegador
2. `admin.js`
3. `admin.css`
4. layout flex y paneles del wizard

---

## 17. Cómo actualizar este documento en el futuro

Cada vez que se haga un cambio importante, conviene actualizar este manual en la misma tarea.

### Regla práctica

Si cambió alguno de estos puntos, el manual debe revisarse:

- arquitectura
- flujo de una pregunta
- configuración
- tablas SQLite
- onboarding
- multi-tenant
- RAG docs
- ML.NET
- Admin UI

### Secciones que normalmente cambian más

- sección 5: configuración
- sección 6: flujo de pregunta SQL
- sección 10: Admin
- sección 11: multi-tenant
- sección 12: onboarding
- sección 13 y 14: docs / forecasting

---

## 18. Resumen ejecutivo para alguien nuevo en el proyecto

Si tuvieras que explicarle VannaLight a un nuevo integrante en 2 minutos, sería algo así:

> VannaLight es un sistema que responde preguntas sobre datos, documentos y predicciones. Su núcleo está en Text-to-SQL. Primero intenta resolver preguntas con patrones conocidos; si no puede, usa retrieval + LLM, pero siempre con validación y control. La configuración operativa vive sobre todo en SQLite y se administra desde Admin. El sistema ya soporta múltiples dominios y una base inicial multi-tenant. El onboarding permite preparar una nueva base sin tocar demasiado código, y el siguiente reto es seguir validando el sistema con bases reales, documentos y forecasting.

