# Backlog VannaLight

## Estado del piloto

### Regla de mantenimiento del backlog
- Todo cambio funcional o tÃƒÂ©cnico relevante debe reflejarse en este archivo el mismo dÃƒÂ­a.
- Cada actualizaciÃƒÂ³n debe dejar claro si el cambio quedÃƒÂ³ en:
  - `Hecho hoy`
  - `En progreso`
  - `Pendiente`
- Si un pendiente cambia de prioridad o de diagnÃƒÂ³stico, se debe actualizar aquÃƒÂ­ antes de cerrar la sesiÃƒÂ³n.

## Semana de presentacion

### Prioridad 0 - Proxima sesion
- Antes de continuar con cualquier otro cambio pendiente, enfocar la siguiente sesion en:
  - simplificar y redisenar el onboarding como flujo guiado
  - separar claramente lo urgente de lo importante dentro de la activacion del sistema
  - definir un camino visible de inicio a fin para que el usuario sepa:
    - por donde empezar
    - que pasos son obligatorios
    - que pasos son recomendados
    - que impacto tiene configurar cada cosa
    - que sigue despues de cada paso
  - hacer que el sistema indique claramente cuando un dominio/contexto ya esta operativo y que falta para quedar listo
  - mover gradualmente la configuracion especifica de la base de datos al onboarding, evitando seguir dispersandola en menus tecnicos

### Prioridad 0 - Urgente: mover comportamiento de negocio desde codigo hacia Admin
- Objetivo de esta linea de trabajo:
  - dejar de resolver familias de preguntas del dominio entrando a `PatternMatcherService.cs`
  - convertir `Admin` en la fuente principal de configuracion para comportamiento semantico del dominio
  - reservar cambios en codigo para bugs estructurales, seguridad, ejecucion y validacion del motor
- Riesgo actual que justifica esta prioridad:
  - hoy el sistema sigue en estado intermedio
  - `Admin` ya permite configurar una parte del dominio, pero matcher, builder y algunas rutas built-in todavia capturan demasiado comportamiento de negocio
  - esto no escala bien para el piloto si cada nueva familia de preguntas obliga a tocar C# y redeployar

#### Fase 1 - Contener el hardcodeo y definir fronteras
- Hacer inventario completo de comportamiento de negocio que hoy vive en codigo:
  - revisar `PatternMatcherService.cs`
  - revisar `TemplateSqlBuilder.cs`
  - revisar cualquier fallback SQL o branch de intencion embebido en `InferenceWorker` o servicios relacionados
- Clasificar cada regla encontrada en una de estas categorias:
  - infraestructura del motor
  - comportamiento del dominio
  - compatibilidad legacy temporal
- Definir y documentar la frontera oficial:
  - en codigo solo debe quedar:
    - pipeline general
    - resolucion de contexto
    - ejecucion segura
    - validacion estructural
    - fallback minimo
    - compilacion declarativa generica
  - en Admin debe vivir:
    - patrones de intencion
    - metrica
    - dimension
    - aliases semanticos
    - hints de columnas
    - reglas de negocio
    - ejemplos verificados
- Criterio de terminado:
  - lista trazable de ramas hardcoded actuales
  - cada rama marcada como `se queda`, `migra a Admin` o `legacy temporal`
  - documento corto en backlog o auditoria tecnica que deje clara la frontera

#### Fase 2 - Dar prioridad real a Query Patterns sobre matcher built-in
- Revisar el orden de resolucion de preguntas SQL:
  - confirmar si hoy el matcher built-in gana antes que patrones configurados
  - cambiar el orden para que `Query Patterns` configurados del dominio sean la fuente principal en preguntas repetibles del piloto
- Reducir el built-in a modo minimo:
  - deteccion general de intencion
  - fallback minimo para no dejar preguntas criticas sin ruta
  - compatibilidad temporal para casos ya sembrados mientras migran a Admin
- Evitar agregar nuevos casos de negocio al matcher built-in:
  - no seguir creciendo ramas como `production_by_press`, `scrap_by_partnumber`, `downtime_by_failure` salvo que sea un bug base unavoidable
- Criterio de terminado:
  - preguntas configuradas en `Query Patterns` del dominio se resuelven por esa via antes de caer al built-in
  - backlog deja explicitamente prohibido seguir agregando logica de negocio frecuente al matcher salvo excepcion documentada

#### Fase 3 - Declarativizar mas el builder SQL
- Revisar `TemplateSqlBuilder.cs` para reducir branching por `PatternKey` y por casos concretos del dominio
- Llevar el builder a una forma mas declarativa basada en:
  - `Metric`
  - `Dimension`
  - `TimeScope`
  - tokens genericos de template
  - validaciones de shape del resultado
- Estandarizar soporte para:
  - entidad concreta en pregunta -> filtro `WHERE`
  - preguntas `top N` -> `GROUP BY + ORDER BY + TOP`
  - total global -> agregado sin dimension
  - `turno actual` -> filtro temporal consistente con la vista base
- Reducir SQL embebido especifico por dominio cuando se pueda reemplazar por construccion generica
- Criterio de terminado:
  - builder resuelve las familias demo principales con piezas declarativas y menos branches especiales
  - se reduce el numero de casos donde corregir una familia de preguntas obliga a tocar el builder

#### Fase 4 - Validacion semantica formal antes de ejecutar SQL
- Crear una capa nueva de validacion semantica entre intencion detectada y SQL final
- Reglas iniciales obligatorias:
  - si la pregunta menciona una entidad concreta, el SQL debe filtrarla
  - si la pregunta pide `turno actual`, el SQL debe incluir filtro temporal equivalente
  - si la pregunta pide una sola entidad, el SQL no debe responder con agregado global sin `WHERE`
  - si la pregunta pide `top N`, el SQL debe agrupar y ordenar por la dimension esperada
- Hacer que la validacion no solo registre warning:
  - debe marcar consulta sospechosa
  - debe intentar enrutar a correccion/revision cuando contradice claramente la intencion
- Definir salida clara para UI y logs:
  - motivo de rechazo o revision
  - regla semantica violada
  - sugerencia de ajuste
- Criterio de terminado:
  - el sistema detecta automaticamente errores como:
    - pregunta con `prensa E4` pero SQL sin filtro de prensa
    - pregunta de `turno actual` sin `ShiftId`
    - pregunta singular respondida con ranking global

#### Fase 5 - Convertir Admin en el lugar real de correccion del dominio
- Diseñar flujo operativo claro para que una mala respuesta se corrija desde Admin:
  - corregir SQL en `Entrenamiento RAG`
  - guardar ejemplo verificado
  - crear o ajustar `Query Pattern`
  - agregar `Semantic Hint` si falta mapeo de columnas
  - agregar `Business Rule` si falta restriccion de negocio
- Hacer mas explicito en Admin para que sirve cada modulo:
  - `Entrenamiento RAG` = corregir ejemplos reales
  - `Query Patterns` = familias repetibles de preguntas
  - `Semantic Hints` = mapeo semantico del dominio
  - `Business Rules` = restricciones y comportamiento esperado
- Agregar copy/UX en Admin para que el usuario sepa:
  - que se arregla con ejemplo
  - que se arregla con pattern
  - que se arregla con hint o regla
  - cuando un problema sigue siendo bug estructural y no configuracion
- Criterio de terminado:
  - existe un flujo reproducible donde un caso de negocio comun se mejora sin deploy
  - el usuario no tecnico entiende donde intervenir para cada tipo de problema

#### Fase 6 - Migrar primero las familias de preguntas del piloto
- Tomar las familias de preguntas mas frecuentes y moverlas a configuracion declarativa en Admin:
  - produccion por prensa
  - scrap por prensa
  - scrap por numero de parte
  - downtime por falla
  - scrap cost por molde
  - comparativos produccion vs scrap por molde/parte cuando aplique
- Para cada familia:
  - definir `MetricKey`
  - definir `DimensionKey`
  - definir `TimeScope`
  - definir aliases frecuentes del usuario
  - definir SQL template o forma declarativa equivalente
  - guardar al menos un ejemplo verificado en memoria
- Criterio de terminado:
  - las familias demo del piloto ya no dependen principalmente de branches hardcoded en C#
  - al menos los casos mas repetidos se pueden ajustar desde Admin

#### Fase 7 - Documentacion operativa y criterio de escalacion
- Documentar explicitamente:
  - que cosas se corrigen desde Admin
  - que cosas todavia requieren codigo
  - que se considera bug estructural
  - cuando una nueva pregunta debe resolverse con pattern y cuando con ejemplo/hint
- Crear politica interna para nuevas correcciones:
  - si es familia repetible del dominio -> primero intentar en Admin
  - si es falla de infraestructura, validacion o enrutamiento base -> codigo
  - si un fix en codigo se hace por urgencia, debe dejar ticket de migracion a Admin si corresponde
- Criterio de terminado:
  - el equipo tiene una regla clara para no seguir cargando negocio en codigo por costumbre

#### Entregables minimos esperados de esta linea urgente
- Inventario de hardcodeo actual en matcher/builder
- Priorizacion de migracion de casos de negocio a `Query Patterns`
- Primera version de validacion semantica pre-ejecucion
- Flujo de correccion desde Admin mas claro y mas usable
- Migracion de las familias de preguntas demo mas importantes
- Documentacion de frontera entre motor y dominio

#### Definicion de exito
- una nueva familia de preguntas del dominio no deberia requerir tocar C# salvo que exista bug estructural
- `Admin` debe convertirse en la via principal para configurar comportamiento del dominio durante el piloto
- el numero de fixes puntuales en `PatternMatcherService.cs` debe bajar de forma visible
- el sistema debe rechazar o marcar consultas semanticamente contradictorias antes de ejecutarlas

### Hecho hoy
- Migracion adicional de comportamiento demo desde hardcodeo hacia configuracion + memoria del piloto:
  - el matcher built-in se redujo de varias ramas especificas del dominio a un fallback mas generico por:
    - metrica
    - dimension
    - `top N`
    - scope temporal
  - objetivo:
    - dejar de seguir creciendo familias demo una por una dentro de `PatternMatcherService.cs`
    - empujar mas peso hacia `Query Patterns` y `TrainingExamples`
- Refuerzo del piloto ERP con `Query Patterns` seed para familias demo frecuentes:
  - se agrego / reforzo `production_by_press` como pattern declarativo del piloto
  - el arranque del API ya deja sembradas las rutas demo frecuentes en `QueryPatterns` + `QueryPatternTerms`
  - objetivo:
    - que estas familias entren primero por configuracion
    - no por ramas ad-hoc en codigo
- Refuerzo del piloto ERP con `TrainingExamples` verificados por contexto:
  - el arranque del API ahora deja sembrados examples verificados por `tenant/domain/connection` para:
    - `production_by_press`
    - `top_downtime_by_failure`
    - `top_scrap_cost_by_mold`
    - `top_scrap_by_press`
    - `top_scrap_by_partnumber`
    - `downtime_by_department`
    - `total_production`
  - los SQL de seed usan `KpiViewOptions`, por lo que respetan las vistas reales configuradas del ambiente
  - objetivo:
    - mejorar retrieval y fast path contextual del piloto
    - hacer mas probable que la mejora del dominio ocurra desde `Admin` + memoria en lugar de desde C#
- Validacion semantica ampliada a mas rutas del carril SQL:
  - la expectativa semantica inferida ya no protege solo la ruta `Pattern`
  - tambien se reutiliza al validar SQL que llegue por:
    - `VerifiedExample`
    - `Llm`
  - objetivo:
    - evitar que un SQL plausible pero contradictorio se cuele solo porque no salio del builder declarativo
- Refuerzo del onboarding guiado para dejar mas explicito que bloquea la salida inicial del dominio:
  - el estado del flujo ahora expone bloqueos base concretos:
    - `workspace`
    - `tablas`
    - `contexto`
    - `prueba`
  - el resumen superior y el cierre del onboarding ahora distinguen mejor entre:
    - `Bloqueado`
    - `Listo para validar`
    - `Operativo`
  - el copy del wizard se endurecio para separar:
    - `Obligatorio ahora`
    - afinacion opcional posterior
  - objetivo:
    - que el operador sepa exactamente que impide salir a uso interno
    - que no confunda afinacion avanzada con prerequisitos del flujo base
- Prioridad real de `Query Patterns` sobre matcher built-in en el carril SQL:
  - `PatternMatcherService` ya devuelve primero el resultado de patrones configurados cuando existe evidencia suficiente de intencion o de ruta
  - el built-in queda mas cerca de fallback minimo y deja de ser la ruta preferida cuando `Admin` ya trae configuracion util
  - se preserva `DimensionValue` al resolver patrones configurados para no perder filtros concretos como:
    - prensa
    - molde
    - falla
    - departamento
    - numero de parte
  - objetivo:
    - mover mas comportamiento del dominio hacia `Admin`
    - bajar dependencia de branches hardcoded en `PatternMatcherService.cs`
- Primera version de validacion semantica pre-ejecucion en `AskUseCase`:
  - antes de ejecutar dry run o SQL real, el pipeline ya rechaza consultas semanticamente contradictorias con la intencion detectada
  - reglas activas en esta iteracion:
    - si la pregunta pide una entidad concreta, el SQL debe incluir `WHERE`
    - si la pregunta pide una entidad concreta, el SQL debe reflejar el valor concreto solicitado
    - si la pregunta pide una sola entidad, el SQL no debe quedarse en agregado global sin filtro de dimension
    - si la pregunta pide `turno actual`, el SQL debe incluir filtro temporal equivalente con fecha actual y `ShiftId`
    - si la pregunta pide `top N`, el SQL debe incluir `TOP`
    - si la pregunta pide `top N` por dimension, el SQL debe incluir `GROUP BY`
    - si la pregunta pide `top N`, el SQL debe incluir `ORDER BY`
  - comportamiento esperado:
    - errores semanticos graves ya caen como `ValidationError` / `RequiresReview`
    - el sistema deja de aceptar tan facil respuestas plausibles pero contradictorias con la pregunta
  - validacion tecnica:
    - `dotnet build VannaLight.Core/VannaLight.Core.csproj`
- Ajuste adicional del onboarding para compactar el Paso 1 y reforzar el cierre del Paso 4:
  - el editor inline de conexion se hizo visualmente mas compacto y menos invasivo
  - el toggle `+ Nueva` ahora refleja mejor su estado:
    - `+ Nueva`
    - `Cerrar editor`
  - el panel de prueba real ahora refuerza visualmente el exito:
    - borde del paso en tono `ok`
    - tarjetas de SQL/resultado con estado exitoso
  - objetivo:
    - que el paso 1 arranque menos pesado
    - que una prueba correcta realmente se sienta como hito de cierre
- Correccion adicional del onboarding Admin para volverlo mas secuencial y menos redundante:
  - el resumen superior del wizard se colapso a un solo bloque compacto:
    - `Paso actual`
    - `Accion obligatoria ahora`
    - chips de contexto/progreso
  - se elimino la duplicacion visual del summary de 4 cards
  - el CTA principal del onboarding ya no compite desde el footer:
    - ahora se mueve al panel del paso activo
    - cada paso tiene su propio `action host`
    - el footer queda como barra secundaria de soporte
  - la barra inferior se reforzo visualmente para que no se pierda con el fondo:
    - fondo mas solido
    - borde superior mas visible
    - mejor contraste general
  - el Paso 2 gano una toolbar mas guiada:
    - buscador mas protagonista
    - filtros agrupados
    - conteo de `seleccionadas` y `visibles`
    - mejor lectura del estado de seleccion
  - se mantuvo un solo sticky real dentro del onboarding:
    - el `stepper`
  - validacion tecnica:
    - `node --check VannaLight.Api/wwwroot/js/admin.js`
- Refactor correctivo del onboarding en Admin para recuperar estabilidad visual del wizard:
  - se elimino el patron de doble sticky entre resumen superior y footer inferior
  - `onboarding-footer-bar` deja de comportarse como barra flotante y vuelve al flujo normal del layout
  - `onboardingCompactReadiness` se redujo a un resumen breve de 4 indicadores, sin copy largo invasivo
  - la action bar inferior recupera contraste con:
    - fondo mas solido
    - borde visible
    - sombra ligera
    - CTA principal dominante
  - se simplifico el Paso 1 para dejar:
    - titulo
    - subtitulo corto
    - una sola ayuda contextual principal
    - formulario sin checklist redundante encima
  - el Paso 2 se limpio para priorizar:
    - toolbar de filtros
    - lista de schema
    - tags utiles (`Recomendada`, `Revisar`, `Ya permitida`)
  - se redujo el peso visual de ayudas y cards secundarias para que el onboarding vuelva a leerse como flujo secuencial
  - objetivo:
    - evitar superposiciones
    - mejorar scroll largo en schema/tablas permitidas
    - devolver claridad jerarquica al wizard
- Primera iteracion del rediseÃ±o guiado del onboarding:
  - se agrego un bloque visible de `Estado actual del wizard`
  - ahora muestra:
    - paso actual
    - accion obligatoria ahora
    - siguiente accion
    - progreso `0 / 4`
  - objetivo:
    - que el usuario sepa claramente donde va
    - que si bloquea el onboarding base
    - que puede dejar para despues
- Desacople visual del camino principal vs paneles de apoyo en onboarding:
  - `Configuracion actual del workspace`
  - `Conexiones configuradas`
  - `Contextos disponibles en runtime`
  - ahora quedan dentro de un bloque de apoyo colapsable
  - se persiste localmente si el usuario prefiere verlo abierto o cerrado
- Ajuste de copy del wizard para reducir ambiguedad:
  - se explicita que `Business Rules`, `Semantic Hints` manuales y `Query Patterns` son afinacion avanzada
  - ya no se presentan como prerequisito del onboarding base
- Validacion tecnica rapida de esta iteracion:
  - `admin.js` quedo con sintaxis valida (`node --check`)
- Segunda iteracion del rediseño UX del onboarding guiado:
  - se agrego un `checklist rapido` sticky con estado de:
    - workspace
    - tablas permitidas
    - contexto generado
    - prueba real
  - el footer del wizard ahora se comporta como barra guiada:
    - un solo CTA primario visible por estado
    - acciones secundarias degradadas visualmente
    - copy explicito del siguiente paso
  - el paso 1 ahora explica mejor:
    - que se esta configurando
    - cuando se considera completo
    - que sigue despues
  - el paso 2 gana una capa mas clara de asistencia para seleccionar tablas con menor riesgo
  - los CTAs del flujo base quedaron alineados con el camino principal:
    - `Guardar y continuar`
    - `Guardar tablas y continuar`
    - `Preparar dominio`
    - `Ejecutar prueba`
  - se mantienen las capacidades avanzadas, pero se empujan fuera del foco principal del wizard
  - se mejora contraste y legibilidad en helper text, labels secundarios, resumenes compactos y tarjetas de soporte del onboarding
- Correccion del carril SQL por patrones para preguntas con entidad concreta:
  - ahora el matcher extrae valores de dimension como `prensa A4` y los pasa al builder
  - el SQL templated/generico ya no responde con `TOP N` cuando la pregunta pide una entidad especifica
  - para `turno actual` el subquery de `ShiftId` ahora usa la misma vista base de la consulta y evita mezclar `scrap` con `production` por alias generico
- Correccion del modo `ML / Prediccion` para no sugerir preguntas invalidas por default:
  - la UI ya no invita a ejecutar una sugerencia generica de forecast sin entidad concreta
  - las sugerencias base de `ML` ahora muestran ejemplos con entidad explicita entre comillas
  - si el modo `ML` no tiene historial suficiente, el boton de ejecutar sugerencia aleatoria queda deshabilitado hasta que el usuario reemplace la entidad del ejemplo
  - se mejora el copy para indicar claramente que el pronostico necesita una serie concreta como:
    - `N/P`
    - `producto`
    - `prensa`
    - `cliente`
  - el error backend tambien se volvio generalista y accionable:
    - ya no habla solo de `numero de parte`
    - ahora indica que falta una entidad concreta para el pronostico
- Correccion del carril PDF para usar la misma SQLite que Admin e indexacion:
  - `DocsAnswerService` ya no reconstruye por su cuenta `Paths:Sqlite`
  - ahora usa `SqliteOptions.DbPath`, igual que el resto del sistema
  - esto corrige fallos como:
    - `SQLite Error 1: no such table: DocChunks`
- Se agrego ruta heuristica previa al LLM en el router documental:
  - preguntas simples de `Molde`, `Empaque` y `Resina` ya no dependen obligatoriamente del modelo local
  - objetivo:
    - evitar crashes nativos del runtime en consultas documentales simples
    - bajar latencia
    - mejorar estabilidad del modo PDF
- Limpieza del repo para remover proyectos externos que no pertenecen al producto actual:
  - `gotenberg_poc`
  - `UltraMsgWebhookSpike`
  - `VannaLight.HoloLens`
  - se ajusta `.gitignore` para evitar que vuelvan a entrar por accidente
  - se limpia documentacion que todavia los listaba como parte del arbol del repo
- Preguntas sugeridas del chat ahora conscientes del contexto activo:
  - ya no dependen solo de una lista fija por modo
  - el panel intenta mostrar el Top 3 del historial local para:
    - modo activo
    - contexto activo
  - el ranking usa:
    - frecuencia
    - `rowCount` como desempate
    - recencia
  - si no hay suficiente historial, cae a preguntas base por modo
  - objetivo:
    - evitar sugerencias que no aplican al dominio actual
    - mejorar demos en `ERP` y `Northwind`
    - acercar las sugerencias a uso real del operador
- Recuperacion del ambiente de trabajo segun `HANDOFF_TRABAJO.md`:
  - `appsettings.Local.json` vuelve a apuntar a `%LOCALAPPDATA%\\VannaLight\\Data`
  - reconstruccion minima de memoria operativa por onboarding local para:
    - `erp-kpi-pilot`
    - `northwind-sales`
- Chequeo defensivo de memoria al arrancar:
  - el startup ahora reporta por dominio:
    - `AllowedObjects`
    - `SchemaDocs`
    - `SemanticHints`
    - `BusinessRules`
    - `QueryPatterns`
    - `TrainingExamples`
  - si un dominio activo no tiene `AllowedObjects`, deja warning claro en logs
  - el health check ya tolera esquemas SQLite legacy sin columna `Domain` y no rompe el arranque
- Timeout explicito en el worker SQL para evitar que un job atorado deje la cola en:
  - `Analyzing`
  - `Queued`
  - timeout ahora configurable por `Timeouts:SqlGenerationSeconds`
  - default subido a `75s` para dar mas margen al LLM local
- Diagnostico y correccion inicial del runtime LLM para SQL:
  - se confirmo que el cliente real del LLM estaba ignorando el perfil de hardware activo
  - `LlmClient` ahora toma `ContextSize` y `GpuLayerCount` del `LlmRuntimeProfile` activo
  - se agrego logging `LlmPerf` para medir:
    - `PromptChars`
    - `MaxTokens`
    - `OutputChars`
    - `TotalMs`
  - se deja como hipotesis fuerte que el carril SQL de `erp-kpi-pilot` esta operando en frio:
    - sin `TrainingExamples` verificados reutilizables
    - y con poca o nula ayuda de `QueryPatterns`

### Pendiente prioritario
- Redisenar el onboarding para que sea guiado, simple y autoexplicativo:
  - avance ya aplicado en esta sesion:
    - resumen visible del wizard con estado y progreso
    - paneles de apoyo fuera del camino principal
    - copy mas claro sobre que es base vs afinacion avanzada
  - pendiente para la siguiente iteracion:
    - simplificar aun mas el orden visual de las secciones
    - reforzar microcopy de impacto y prerequisitos por paso
    - decidir que paneles tecnicos deben salir definitivamente del flujo base
  - hoy hay demasiadas pestanas, menus y puntos de configuracion
  - el usuario no sabe por donde empezar ni que sigue despues de cada paso
  - faltan explicaciones claras de:
    - que hace cada pantalla
    - que es obligatorio
    - que es opcional
    - que impacto tiene configurar o no configurar algo
  - hoy se puede completar onboarding, business rules o semantic hints sin que el sistema indique claramente si eso es suficiente o que falta para quedar operativo
  - objetivo:
    - convertir el onboarding en un flujo secuencial y visible
    - mostrar progreso, prerequisitos y siguiente paso recomendado
    - reducir dependencia de conocimiento tacito del equipo
    - mover la configuracion especifica de la DB al onboarding en vez de dejarla dispersa por admin
- Evolucionar el contrato del modo `ML / Prediccion` de forma generalista:
  - hoy el modo `ML` esta mejor preparado para pronosticos sobre una serie concreta, no para consultas generales agregadas
  - la entidad puede ser:
    - `N/P`
    - `producto`
    - `prensa`
    - `cliente`
    - otra clave de serie configurada
  - hoy no esta bien resuelto para preguntas generales como:
    - `pronostico total de scrap para el cierre del turno`
    - `pronostico general de produccion de manana`
  - trabajo propuesto:
    - definir formalmente dos rutas:
      - `SeriesForecast`
      - `AggregateForecast`
    - hacer que el router pueda decidir entre:
      - forecast por serie
      - forecast agregado
      - rechazo guiado si no existe perfil compatible
    - mover esta configuracion al onboarding para que no quede amarrada a una DB o dominio especifico
    - mantener la UX honesta mientras tanto:
      - no sugerir prompts de `ML` que el contrato actual no pueda resolver
      - explicar claramente cuando falta una entidad concreta
  - impacto:
    - alto en claridad del producto
    - alto en experiencia de usuario
    - medio/alto en arquitectura del carril `ML`
  - prioridad:
    - `P1`, despues del rediseÃƒÆ’Ã‚Â±o de onboarding y antes de ampliar mas capacidades predictivas
- Correccion del carril SQL demo para preguntas de scrap por numero de parte:
  - se agrego soporte explicito a `PatternDimension.PartNumber`
  - `PatternMatcherService` ya reconoce:
    - `numero de parte`
    - `numeros de parte`
    - `part number`
    - `part numbers`
  - `TemplateSqlBuilder` ahora construye SQL directo para `top_scrap_by_partnumber`
  - el startup deja sembrados patterns demo minimos para dominios ERP:
    - `top_scrap_by_press`
    - `top_scrap_by_partnumber`
  - objetivo:
    - sacar del LLM las preguntas demo mas frecuentes
    - evitar columnas inventadas como `ScrapQuantity`
    - bajar el tiempo total del carril SQL en `erp-kpi-pilot`
- Reduccion de hardcodeo en `TemplateSqlBuilder` para rutas demo SQL:
  - los patterns demo de scrap por prensa y por numero de parte ya no dependen de SQL embebido por `PatternKey`
  - el startup ahora siembra un `SqlTemplate` declarativo comun basado en:
    - `MetricKey`
    - `DimensionKey`
    - `DefaultTimeScopeKey`
  - `TemplateSqlBuilder` ahora resuelve tokens genericos como:
    - `DimensionProjection`
    - `DimensionFilter`
    - `DimensionGroupBy`
    - `DimensionOrderBy`
    - `MetricProjection`
    - `MetricOrderBy`
  - objetivo:
    - reducir acoplamiento a una base/vista especifica
    - dejar que nuevos patterns reutilicen el mismo builder
    - mover comportamiento desde `switch` imperativo hacia configuracion declarativa
  - avance adicional:
    - `TemplateSqlBuilder` ya tiene fallback generico por:
      - `Metric`
      - `Dimension`
      - `TimeScope`
    - esto permite construir consultas agrupadas/top y totales simples aun cuando un pattern nuevo no traiga un metodo dedicado por `PatternKey`
  - cobertura declarativa ampliada en seeds del piloto:
    - `total_production`
    - `top_downtime_by_press`
    - `top_downtime_by_failure`
    - `downtime_by_department`
    - `top_scrap_cost_by_mold`
  - se mantiene el codigo legacy como respaldo mientras validamos que los templates declarativos cubren bien los casos reales del piloto
- Refuerzo del grounding del prompt SQL para scrap por numero de parte:
  - se confirmo que el prompt real estaba entrando con:
    - `PISTAS SEMANTICAS DEL DOMINIO`
    - `OBJETOS SQL PERMITIDOS`
  - pero sin suficiente grounding de columnas para `dbo.vw_KpiScrap_v1`
  - `LocalRetriever` ahora fuerza la inclusion de `SchemaDocs` de `vw_KpiScrap_v1` para preguntas con:
    - `scrap`
    - `numero(s) de parte`
    - `part number(s)`
    - `turno`
  - el startup ahora siembra `SemanticHints` de columna para dominios ERP:
    - `PartNumber`
    - `ScrapQty`
    - `OperationDate`
    - `ShiftId`
  - objetivo:
    - evitar invenciones como `Qty` o `ScrapQuantity`
    - hacer que el LLM vea la metrica y dimensiones reales de la vista KPI
    - mejorar el self-correction con nombres validos del esquema
  - aprendizaje registrado:
    - al cambiar de fuente de memoria local (`%LOCALAPPDATA%`) el prompt puede quedarse con:
      - hints demasiado genericos
      - sin `SchemaDocs` relevantes
      - sin `EJEMPLOS RELEVANTES`
    - eso no rompe la estructura del prompt, pero si deja al LLM sin grounding suficiente y empieza a inventar columnas plausibles
    - este incidente confirma que para `erp-kpi-pilot` no basta con sembrar entidades; hay que sembrar tambien columnas criticas del KPI
- Correccion del guardado de perfiles Hardware LLM:
  - `SqliteLlmProfileStore.UpdateAsync(...)` ahora abre conexion explicita
  - envia parametros con nombres exactos (`Id`, `GpuLayers`, `ContextSize`, `BatchSize`, `UBatchSize`, `Threads`)
  - usa `CommandDefinition` con `CancellationToken`
  - evita el fallo de SQLite:
    - `Must add values for the following parameters...`
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
  - el startup ya deja warnings claros si un dominio activo arranca sin memoria operativa
- Documentar:
  - persistencia de `vanna_memory.db`
  - persistencia de `vanna_runtime.db`
  - persistencia de `dpkeys`
  - conexiones requeridas por ambiente
  - pasos de arranque
  - recuperacion basica ante cambio de connection string o contexto

#### 4.1. Blindar la transicion a `%LOCALAPPDATA%` con recovery guiado
Impacto: alto  
Necesidad: alta

- Agregar un flujo de recuperacion mas guiado cuando:
  - hay contextos activos
  - pero la memoria local del dominio esta vacia
- Objetivo:
  - que el sistema no Ã¢â‚¬Å“parezca rotoÃ¢â‚¬Â
  - y que el operador sepa exactamente si debe:
    - restaurar backup local
    - re-sembrar onboarding
    - o re-inicializar el dominio

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
- Documentar minimos obligatorios por dominio para que el prompt no quede "ciego" despues de una migracion o reconstruccion local:
  - `SemanticHints` de columna criticos
  - `SchemaDocs` obligatorios de vistas KPI
  - `TrainingExamples` demo minimos
- Priorizar en SQL del piloto:
  - sembrar 3-5 preguntas demo como `TrainingExamples` verificados en `erp-kpi-pilot`
  - para que no dependan del LLM frio en cada demostracion
  - dejar checklist minimo para `erp-kpi-pilot`:
    - `dbo.vw_KpiScrap_v1`
    - `PartNumber`
    - `ScrapQty`
    - `OperationDate`
    - `ShiftId`

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

#### 10.1. Medir y ajustar performance del carril SQL
Impacto: alto  
Necesidad: media-alta

- Estado actual:
  - ya existe timeout defensivo en SQL (`75s`)
  - ya existe logging `LlmPerf` para medir inferencia del modelo local
  - se confirmo en una prueba real:
    - `LlmPerf TotalMs` alrededor de `18s`
    - `SqlPerf TotalMs` alrededor de `75s`
  - lectura actual:
    - el LLM responde, pero el pipeline completo se sigue consumiendo el tiempo en:
      - validacion/dry-run
      - self-correction
      - o ruta generativa innecesaria para preguntas demo
- Siguiente paso:
  - capturar en una prueba real:
    - `PromptChars`
    - `MaxTokens`
    - `OutputChars`
    - `TotalMs`
  - confirmar si el costo principal viene de:
    - contexto frio por falta de reuse
    - prompt demasiado largo
    - o hardware/profile mal afinado
  - validar despues de reinicio si las preguntas de scrap por numero de parte ya:
    - reciben `SchemaDocs` forzados de `vw_KpiScrap_v1`
    - reciben hints de columna (`ScrapQty`, `PartNumber`, `OperationDate`, `ShiftId`)
    - dejan de inventar `Qty` y `ScrapQuantity`
  - mantener como check de regresion:
    - inspeccionar el prompt real cuando una pregunta demo empiece a inventar columnas
    - confirmar si faltan:
      - `ESQUEMAS RELEVANTES RECUPERADOS`
      - `EJEMPLOS RELEVANTES`
      - hints de columna

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
- Extender la misma linea al carril SQL:
  - seguir migrando `TemplateSqlBuilder` de SQL hardcodeado por `PatternKey`
  - hacia templates reutilizables basados en:
    - metrica
    - dimension
    - tiempo
    - columnas display/fallback

### Aprendizaje reciente: prompts frios vs rutas declarativas
- Ya confirmamos un patron de falla importante:
  - si una pregunta del piloto cae al LLM sin `SchemaDocs` fuertes y sin `SemanticHints` de columna, el modelo inventa nombres plausibles (`Qty`, `ScrapQuantity`, `FaultName`, etc.).
- Causa observada:
  - memoria local fria o incompleta
  - hints demasiado genericos de entidad
  - falta de schema relevante para la vista exacta
- Mitigacion aplicada:
  - forzar rutas declarativas/pattern-first para preguntas demo de alto valor
  - sembrar hints de columna y no solo de entidad
  - alinear `TemplateSqlBuilder` con columnas reales del dominio industrial
- Minimos obligatorios para `erp-kpi-pilot`:
  - `dbo.vw_KpiScrap_v1`: `ScrapQty`, `PartNumber`, `PressName`, `OperationDate`, `ShiftId`
  - `dbo.vw_KpiProduction_v1`: `ProducedQty`, `OperationDate`, `YearNumber`, `WeekOfYear`
  - `dbo.vw_KpiDownTime_v1`: `DownTimeMinutes`, `DownTimeCost`, `FailureCode`, `Department`, `OperationDate`
- Check de regresion para proximas sesiones:
  - si una pregunta demo vuelve a inventar columnas, revisar primero:
    - si entro por `Pattern`
    - si el dominio tiene `SemanticHints` activos de columna
    - si el `SchemaDoc` relevante realmente entra al prompt
- Incidente corregido:
  - el matcher built-in tenia un corte prematuro por `scrap` y evitaba evaluar rutas declarativas de `production` y `downtime`
  - sintoma: preguntas como `total de produccion` o `downtime por falla` caian al LLM aunque ya existia pattern seed
  - accion aplicada: permitir que el matcher built-in siga evaluando `production` y `downtime` aunque la pregunta no contenga `scrap`
- Incidente corregido:
  - si el store devolvia un pattern debil, el matcher dejaba de considerar la ruta built-in fuerte
  - sintoma: preguntas demo del piloto con built-in valido podian seguir cayendo al LLM
  - accion aplicada: hacer que `PatternMatcherService` prefiera built-ins validos cuando el match persistido no alcanza fuerza de ruta
- Hallazgo importante del dominio industrial:
  - `IndustrialDomainPackAdapter` y el esquema real de `dbo.vw_KpiDownTime_v1` no estaban alineados en dimensiones de falla/departamento
  - en la base probada, las columnas validas para downtime son:
    - `FailureId`
    - `FailureName`
    - `DepartmentId`
    - `DepartmentName`
  - y no:
    - `FailureCode`
    - `Department`
  - accion aplicada:
    - realinear `TemplateSqlBuilder`
    - realinear `SemanticHints` sembrados
  - seguimiento:
    - revisar si `IndustrialDomainPackAdapter` tambien debe alinearse o si representa otro contrato separado
- Mitigacion adicional de hardcodeo:
  - `TemplateSqlBuilder` ya no depende en runtime del `switch` por `PatternKey`
  - flujo actual:
    - primero `SqlTemplate`
    - luego fallback generico por `Metric` + `Dimension` + `TimeScope`
  - las rutas legacy especificas quedan como residuo tecnico, pero ya no son el camino principal de ejecucion
- Limpieza aplicada:
  - se eliminaron del runtime los metodos legacy muertos de `TemplateSqlBuilder`
  - tambien se retiro `Supports(...)` de la interfaz, porque ya no formaba parte del flujo real
  - resultado:
    - menos codigo engaÃƒÂ±oso
    - menos mantenimiento duplicado
    - el builder refleja mejor la arquitectura actual declarativa

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

## Notas de backlog externo - HoloTasks / LayerAudits

### Proxima sesion - Ejecucion de auditoria en piso
- Revisar en `LayerAudits.sln` si cada pregunta ya distingue entre:
  - `documentos de referencia`
  - `ayuda visual`
- Si no existe esa distincion, definir el modelo de datos para separarlos:
  - documentos formales:
    - `WI`
    - `CP`
    - `PFD`
    - `PFMEA`
    - formatos
  - ayudas visuales:
    - imagenes
    - ejemplos
    - laminas explicativas
- Ajustar HoloTasks para mostrar ambos grupos por separado:
  - `Documentos de referencia`
  - `Ayuda visual`
- Disenar y agregar la captura completa del renglon operativo del `F-0009` cuando la respuesta sea `NC`:
  - observacion / accion correctiva
  - responsable
  - plan de reaccion `A/B/C`
  - estatus `Abierto / Cerrado`
  - fotos
- Mostrar claramente el plan de reaccion en HoloTasks:
  - no como seleccion libre
  - sino como valor heredado/predefinido por pregunta
  - capturado junto al hallazgo
- Soportar cierre inmediato cuando aplique:
  - especialmente para plan `A`
  - permitir `Cerrado` si se corrigio en el momento
  - `Abierto` si no
- Agregar validaciones minimas antes de finalizar la auditoria:
  - preguntas respondidas
  - `NC` con observacion
  - `NC` con fotos
  - `NC` con estatus
  - responsable y plan de reaccion presentes
- Revisar y disenar soporte para preguntas especiales de Nivel 3:
  - mostrar/habilitar preguntas exclusivas
  - restringirlas por nivel
  - soportar revision de acciones correctivas y validacion con cliente cuando aplique
- Revisar como quedara la estructura formal del hallazgo en HoloTasks:
  - no solo respuesta y evidencia
  - tambien reglas de cierre
  - completitud
  - trazabilidad minima en ejecucion
- Validar nuevamente la experiencia de evidencia en HoloLens:
  - foto completa en miniatura
  - boton de borrar con icono
  - `Ver / anotar`
  - comportamiento de camara nativa
- Dejar para mejora posterior, no como requisito duro inmediato:
  - modo offline
  - cantidad maxima de fotos configurable
  - campo separado de `documento revisado`
  - filtrado automatico de `donde aplique`
- Construir una matriz final de backlog/gap analysis con:
  - `Funcionalidad`
  - `Estado actual`
  - `Falta`
  - `Prioridad`
  - `Impacto en reemplazo del F-0009`
- Revisar manana la cobertura funcional por proyecto para evitar traslapes o huecos:
  - `LayerAudits`
  - `QrPrensas` (API de HoloTasks)
  - `HoloTasks`
  - objetivo:
    - identificar que puntos del proceso cubre cada proyecto
    - detectar dependencias cruzadas
    - confirmar que funcionalidades deben vivir en cada sistema

### Actualizacion 2026-04-17 - simplificacion correctiva del onboarding admin
- Se elimino la capa superior redundante del onboarding:
  - fuera `hero` de ambiente/perfil/conexiones
  - fuera la guia textual de 4 pasos duplicada
- El resumen global quedo concentrado en:
  - `renderOnboardingFlowSummary()`
  - resumen visual compacto de 4 estados (`Workspace`, `Tablas`, `Contexto`, `Prueba`)
- `renderOnboardingActionGuidance()` se redujo a un hint corto, sin reexplicar todo el wizard.
- El footer del onboarding quedo mas minimal:
  - hint corto
  - solo CTA principal visible
  - acciones secundarias ocultas del foco principal
- Se limpio el paso 3 y 4:
  - fuera la grilla de proceso redundante del paso 3
  - fuera el overview grande del paso 4
  - se dejo una sola definicion compacta de exito para validacion
- El paso 2 se mejoro para decision asistida:
  - candidatos ordenados por prioridad real (`seleccionadas`, `ya permitidas`, `recomendadas`, `riesgosas`)
  - meta de seleccion mas clara
  - tags mas utiles y menos repetitivas
- Se corrigieron residuos del refactor en `admin.js`:
  - renderer duplicado de schema candidates eliminado
  - linea duplicada de `renderOnboardingStatus()` eliminada
  - `admin.js` vuelve a pasar `node --check`
- Se reforzo el cierre del wizard para que no compita antes de tiempo:
  - el panel 5 ya colapsa como el resto mientras todavia no aplica
  - el separador `Estado final del dominio` solo aparece cuando ya entras al cierre real
- La franja inferior del onboarding gano mas contraste visual:
  - borde superior mas claro
  - fondo mas solido
  - sombra mas marcada
  - ya no se pierde con el canvas oscuro
- La zona avanzada del paso final quedo mas subordinada:
  - el boton aparece deshabilitado hasta cerrar el onboarding base
  - export/import y `Domain Pack JSON` quedan explicitamente fuera del foco principal

### Actualizacion 2026-04-17 - limpieza UX del index principal
- Se redujo el ruido tecnico por default en el chat:
  - la consola del sistema ahora arranca colapsada
  - se agrego toggle explicito para verla cuando haga falta
- Se simplifico la franja de contexto:
  - fuera el `context-hero` redundante
  - el estado activo ahora resume contexto y etiqueta en una sola linea
  - el selector recibe estado visual de error si intentas consultar SQL sin contexto
- El historial lateral quedo mas compacto:
  - pregunta truncada a dos lineas
  - contexto resumido en una linea
  - acciones ocultas hasta hover / item activo
  - labels mas simples: `Abrir`, `Usar`, `Repetir`
- El banner de estado ya no compite con resultados exitosos:
  - se limpia al completar correctamente una consulta
  - queda reservado para `info`, `warning` y `error`
- La voz automatica ya es opt-in:
  - se agrego toggle `TTS` en el sidebar
  - `speakSummary()` solo habla si el usuario lo activo
  - el texto hablado se sanea y se limita para evitar lecturas largas
- Se reforzo tambien el estado de conexion del topbar:
  - verde cuando esta activo
  - amarillo al reconectar
  - rojo si la conexion cae
- Se hizo mas ligera la pantalla vacia y el estado con resultado:
  - el `demo-strip` se compacta cuando ya existe resultado
  - el foco vuelve al output en lugar de mantener las sugerencias compitiendo
- El modo `ML` ahora tiene una ayuda minima mas usable:
  - se agrego una guia compacta debajo del input solo para `pred`
  - incluye plantillas rapidas para `producto`, `cliente`, `pais`, `mañana` y `semana`
  - evita depender solo del placeholder libre para explicar como preguntar
- Se reforzo la estabilidad general del layout principal:
  - el `topbar` queda sticky y visible al hacer scroll
  - ya no desaparece mientras recorres resultados largos
- Se redujo aun mas la densidad del historial lateral:
  - items con menos padding
  - pregunta en una sola linea
  - metadata mas compacta
  - acciones mas pequeñas para que entren mas registros visibles
- La tarjeta de resultado en `ML` ya no esta tan hardcodeada a scrap:
  - el copy se vuelve mas genericamente predictivo
  - usa `MetricKey` / `SeriesType` para hablar de metrica, entidad y horizonte
  - ejemplos y placeholder tambien quedaron menos industriales
- Se agrupo mejor el bloque principal de composicion:
  - input, ayuda de modo y sugerencias quedaron como un solo stack visual
  - el chat se siente menos fragmentado antes del primer resultado
- Se ajusto tambien el comportamiento compacto del historial:
  - en pantallas chicas las acciones quedan visibles sin depender de hover
  - en desktop siguen discretas hasta foco / hover

### Actualizacion 2026-04-17 - MVP de SQL Alerts estructuradas
- Se implemento la nueva capability `SQL Alerts` respetando el split actual de VannaLight:
  - `vanna_memory.db` para reglas (`SqlAlertRules`)
  - `vanna_runtime.db` para estado/eventos (`SqlAlertStates`, `SqlAlertEvents`)
- Se agrego el modelo de dominio base para alertas SQL:
  - `SqlAlertRule`
  - `SqlAlertState`
  - `SqlAlertEvent`
  - enums fuertes para operador, ventana temporal, estado lifecycle y tipo de evento
- Se agregaron contratos nuevos en `Core` para:
  - store de reglas
  - store de estado runtime
  - store de eventos
  - catálogo semántico de métricas
  - query builder seguro
  - evaluador de alertas
- Se agregaron casos de uso explícitos para:
  - upsert de regla
  - acknowledge manual
  - clear manual
- Se implementaron stores SQLite nuevos:
  - `SqliteSqlAlertRuleStore`
  - `SqliteSqlAlertStateStore`
  - `SqliteSqlAlertEventStore`
- Se implementó una capa de compilación segura a SQL:
  - `SqlAlertMetricCatalog` apoyado en `DomainPackProvider`
  - `SqlAlertQueryBuilder`
  - validación fail-closed contra `AllowedObjects`
  - parámetros tipados en vez de SQL libre del usuario
- Se implementó el worker periódico:
  - `SqlAlertEvaluationWorker`
  - carga alertas activas
  - evalúa por frecuencia
  - aplica cooldown
  - deduplica por estado `Closed/Open/Acknowledged`
  - persiste `Triggered`, `Resolved` y `EvaluationFailed`
- Se integró SignalR reutilizando `AssistantHub`:
  - evento `SqlAlertEventRaised`
  - toasts en admin
  - notificación visible también en `index`
- Se agregaron endpoints nuevos bajo `api/admin/sql-alerts`:
  - listar alertas
  - catálogo por contexto
  - preview SQL
  - crear / editar
  - activar / desactivar
  - ack
  - clear
  - listar eventos
- Se agregó la nueva tab `SQL Alerts` en admin con:
  - listado de alertas por contexto activo
  - formulario estructurado
  - preview del SQL compilado
  - historial de eventos
  - acciones de ack / clear / toggle
- Se hizo también el cableado de cache busting:
  - `admin.css`
  - `admin.js`
  - `index.js`
- Pendiente siguiente fase:
  - endurecer más el soporte multi-dominio en `CurrentShift`
  - sumar más métricas base además de las industriales iniciales
  - agregar badge/panel lateral de alertas en el chat

### Actualizacion 2026-04-17 - Reposicionamiento de producto de SQL Alerts
- Se movió la experiencia principal de `SQL Alerts` hacia `index` para alinearla con el uso operativo diario y con el roadmap de alertas creadas por usuario final.
- En `index` se agregó:
  - entry point desde resultado SQL exitoso (`Crear alerta`)
  - modal compacto de creación estructurada con contexto ya resuelto
  - banda de alertas del contexto activo
  - lista compacta de actividad reciente
  - acciones rápidas para `pausar/reanudar`, `ack` y `clear`
  - toasts propios en tiempo real por SignalR
- Se agregaron endpoints nuevos de superficie de usuario final en `api/sql-alerts`, reutilizando la lógica backend ya construida para reglas, eventos, stores, worker y SignalR.
- `admin` se reposicionó como `Alert Monitor`:
  - menos lenguaje de flujo primario
  - más énfasis en gobernanza, auditoría, troubleshooting y observabilidad
- Se añadió `wwwroot/css/index.css` para aislar el styling de la nueva experiencia de alertas del chat sin convertir `index` en un mini-admin.

### Actualizacion 2026-04-18 - Pulido de SQL Alerts en index
- Se agregó badge de alertas abiertas en el botón `SQL` del rail lateral para dar visibilidad inmediata a incidentes activos.
- Se reforzó la separación visual entre:
  - alertas activas del contexto
  - actividad reciente de alertas
- El flujo `Crear alerta` desde resultado SQL ahora llega con mejor ayuda contextual:
  - resumen corto de la sugerencia detectada
  - prefill más legible desde la última consulta SQL exitosa
- Se mantuvo la experiencia compacta y operativa, sin arrastrar la complejidad completa de `admin` al chat principal.

### Actualizacion 2026-04-18 - Modo simple para crear SQL Alerts
- Se rediseñó el modal de creación de alertas en `index` para que se sienta como una herramienta operativa y no como configuración técnica.
- Ahora el centro del flujo es un resumen vivo en lenguaje natural que se actualiza conforme el usuario cambia:
  - indicador
  - aplicar a / elemento
  - condición
  - límite
  - periodo
  - frecuencia
- El primer nivel del formulario quedó reducido a tres preguntas:
  - qué quieres vigilar
  - cuándo quieres que te avise
  - cómo lo revisamos
- Se movieron a `Opciones avanzadas`:
  - nombre editable
  - no repetir aviso por
  - notas operativas
  - consulta técnica / preview SQL
- Se reemplazó copy técnico por lenguaje de negocio:
  - `Aplicar a`
  - `Elemento`
  - `Condición`
  - `Límite`
  - `Periodo`
  - `Revisar cada`
  - `No repetir aviso por`
  - `Ver consulta técnica`
- El nombre de la alerta ahora se autogenera y el CTA principal pasó a `Crear y activar alerta` cuando corresponde.
### Actualizacion 2026-04-18 - SQL Alerts solo desde resultados SQL validados
- En `index`, la creación de alertas ya no puede nacer desde cero: solo se habilita cuando existe una última consulta SQL exitosa, con contexto activo y resultado apto para monitoreo.
- Se endureció la validación frontend de `resultado apto para alerta` para aceptar solo salidas con:
  - contexto SQL activo
  - métrica gobernada detectable
  - datos devueltos
  - columna numérica clara
  - volumen razonable y semántica interpretable
- Se extendió la inferencia de alertas para extraer `dimensionCandidates` desde las filas visibles del resultado.
- Comportamiento nuevo según resultado:
  - una entidad clara -> se preselecciona automáticamente
  - varias entidades claras -> se muestran primero como selector guiado
  - agregado global -> la alerta se crea a nivel `Todo`
- El input manual quedó solo como fallback secundario cuando no hay candidatos suficientes o el usuario necesita otro valor.
- La UX del modal ahora refuerza que la alerta significa “avísame si cambia esto que ya consulté y validé”, manteniendo intactos el payload estructurado, los endpoints existentes y SignalR.
### Actualizacion 2026-04-18 - Correccion de falsos negativos en elegibilidad de SQL Alerts
- Se ajustó la heurística de elegibilidad en `index` para aceptar mejor resultados `top N`, rankings y agrupados con:
  - una columna numérica clara
  - una columna categórica razonable
  - tamaño compacto y utilizable para monitoreo
- `findMatchingColumnName(...)` ahora reconoce mejor aliases reales de números de parte y entidades del dominio, incluyendo variantes como:
  - `part number`
  - `part_no`
  - `n/p`
  - `np`
  - `numero de parte`
  - `itemcode`
  - `sku`
- Se agregó fallback para detectar la mejor columna categórica aunque `dimensionKey` no haya sido inferido perfectamente en la primera capa.
- `extractAlertDimensionCandidates(...)` ya no depende rígidamente de una dimensión perfecta y puede derivar candidatos desde la mejor columna categórica del resultado.
- Se reforzó el diagnóstico con razones más útiles cuando un resultado sí es alertable, por ejemplo rankings por entidad con métrica clara, sin volver a permitir creación libre desde cero.
### Actualizacion 2026-04-18 - Cache bust para correcciones de SQL Alerts en index
- Se actualizó el versionado de `index.js` e `index.css` en `index.html` para evitar que el navegador siga sirviendo una versión cacheada anterior de la heurística de elegibilidad y del flujo de creación de alertas.
- Esto asegura que los ajustes recientes de `SQL Alerts` en `index` realmente se reflejen al recargar la app.
### Actualizacion 2026-04-18 - Visibilidad y correccion adicional de elegibilidad en SQL Alerts
- Se eliminó el corte temprano que marcaba falsos negativos cuando aún no se detectaba `metricKey`, permitiendo que la elegibilidad use primero la forma real del resultado (ranking, agrupación, KPI global).
- Se amplió la inferencia de métricas para dominios tipo Northwind con señales como:
  - `sales` / `ventas`
  - `net sales`
  - `units sold`
  - `orders` / `pedidos`
- En el header del resultado SQL, la acción de alerta ahora puede mostrarse deshabilitada con motivo cuando el resultado tiene datos pero aún no es apto, para hacer visible el diagnóstico directamente en la UI.
### Actualizacion 2026-04-18 - Reubicacion visible del CTA de SQL Alerts en index
- Se movió el CTA principal `Crear alerta` al panel `Alertas del contexto activo`, que es donde pertenece dentro del flujo operativo.
- Se eliminó la dependencia visual de la zona de resultado/consola para descubrir la acción de alertas.
- El panel de alertas ahora concentra:
  - estado del contexto
  - motivo de elegibilidad
  - CTA principal visible
- Se reforzó el styling del CTA para que tenga más contraste y jerarquía visual dentro del panel de alertas.
- Se actualizó el versionado de `index.js` e `index.css` para forzar que la nueva ubicación del CTA se refleje al recargar.
### Actualizacion 2026-04-18 - Simplificacion fuerte del onboarding en admin
- Se simplificó el onboarding para que el flujo base se lea como wizard real y no como dashboard de bloques superpuestos.
- Se eliminó del flujo visible el resumen redundante de `estado rápido` inyectado debajo del stepper.
- El bloque superior de estado quedó reducido a una lectura compacta:
  - paso actual
  - acción obligatoria
  - chips de progreso/contexto
- Se escondió el footer de acciones secundarias hasta que el flujo base realmente esté completado, evitando que export/import y acciones accesorias compitan desde el principio.
- El `Domain Pack JSON` salió del foco principal del paso 5 y pasó a una sección colapsable de herramientas posteriores.
- Se compactó el stepper y se redujo el peso visual del hero superior para que el usuario entre más rápido al paso activo.
- Se mantuvo la lógica base del wizard y sus CTAs por paso, pero con menos redundancia visual y menos ruido simultáneo.
- Se actualizó el versionado de `admin.css` y `admin.js` para forzar que el navegador cargue esta limpieza del onboarding.
- 2026-04-18: El onboarding admin paso a wizard secuencial real de un paso a la vez. Ahora el stepper y los botones Anterior/Siguiente controlan un panel activo, los pasos completados quedan colapsados por defecto y el flujo avanza automaticamente si el usuario estaba en el paso vigente.
- 2026-04-18: Se corrigio la visibilidad del stepper del onboarding admin, se limpiaron textos rotos de encoding dentro del flujo visible y el bootstrap ahora hace fallback al paso 1 si falla la restauracion del contexto en vez de dejar un error global engañoso.
- 2026-04-18: Se consolido una capa final de estilos del onboarding admin para que el wizard tenga una sola verdad visual. Se reforzo el stepper, se mejoro la legibilidad de los step chips, se colapsan los pasos no activos y se eliminaron conflictos visuales entre reglas viejas y nuevas.

- 2026-04-18: onboarding admin pulido extra. Se eliminó el bloque fantasma de 'Estado rápido', se reforzó el sidebar de workspaces con fallback desde runtime contexts, se hizo visible el estado vacío/fallback del bootstrap y se limpiaron más cadenas mojibake visibles del flujo de onboarding.

### Actualizacion 2026-04-20 - Correccion operativa de SQL Alerts en CurrentShift
- Ajuste equivalente en `index` para experiencia operativa:
  - las alertas y eventos visibles ya no muestran el ISO UTC crudo
  - `Ultimo cambio` y `Actividad reciente` ahora se renderizan en hora local del navegador
  - el UTC original queda disponible en tooltip para soporte/auditoria
- Ajuste UX adicional en SQL Alerts admin para evitar confusion horaria:
  - las cards de `Actividad reciente` y el metadata de `Ultimo disparo` ahora se muestran en hora local del navegador
  - el valor UTC original se conserva en `title`/tooltip para auditoria
  - decision: persistencia en backend sigue en UTC; solo cambia la presentacion frontend
- Se corrigio el incidente detectado al probar alertas SQL tipo `CurrentShift` en `erp-kpi-pilot`:
  - la evaluacion fallaba con `La tabla de turnos 'dbo.Turnos' no esta permitida para el dominio ...`
  - esto bloqueaba la alerta antes de calcular `ObservedValue`, aunque la metrica y el umbral fueran validos
- Se endurecio `SqlAlertQueryBuilder` para `CurrentShift`:
  - si la tabla de turnos configurada existe y esta permitida, se sigue usando como fuente preferida
  - si no esta permitida, el motor ya no falla duro
  - ahora cae a un fallback generico sobre la propia vista/base object de la metrica, usando el `ShiftId` mas reciente del dia
- Se corrigio tambien el filtrado SQL compilado para dimensiones:
  - ahora se califica con alias `src` para evitar ambiguedad y mantener consistencia en el preview generado
- Se mejoro la UX de `Alert Monitor` en admin:
  - el selector de dimensiones ahora se filtra por metrica elegida
  - el hint contextual explica mejor que dimensiones soporta la metrica
  - para `CurrentShift`, el formulario deja claro que puede usar tabla de turnos o fallback por vista
- Se corrigio la regla local de prueba que habia quedado mal guardada:
  - antes: `scrap_qty` + `shift = A4`
  - ahora: `scrap_qty` + `press = A4`
  - se limpio tambien el estado runtime local para permitir reevaluacion limpia
- Aprendizaje a conservar:
  - `ACK` no corrige la regla ni resuelve fallos de compilacion/evaluacion
  - solo marca la alerta como reconocida manualmente
  - los errores estructurales de configuracion deben corregirse en la regla o en el builder, no con `ACK`

