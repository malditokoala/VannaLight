# Propuesta de Mejora - VannaLight

## 1. Resumen ejecutivo

VannaLight ya demostro que puede generar valor real como asistente industrial para consultas operativas en lenguaje natural. El producto hoy resuelve un problema importante: permite consultar informacion de negocio sin depender de que el usuario conozca SQL, la estructura de la base de datos o el detalle tecnico del sistema.

La siguiente mejora propuesta no busca cambiar la esencia del producto, sino llevarlo a un nivel mas robusto, escalable y presentable para piloto y evolucion posterior.

La idea central es esta:

**mover mas comportamiento del dominio desde codigo hacia configuracion gobernada en Admin, simplificar el onboarding como flujo guiado y agregar validacion semantica para reducir respuestas plausibles pero incorrectas.**

En terminos simples:
- menos dependencia de fixes en C# por cada nueva forma de preguntar
- mas capacidad de ajuste desde Admin
- menos carga cognitiva para activar un dominio
- mas confianza en las respuestas que entrega el sistema

---

## 2. Situacion actual

VannaLight ya cuenta con una base funcional fuerte:
- modo `SQL` para consultas operativas en lenguaje natural
- modo `PDF` para consultas documentales
- modo `ML` como extension experimental
- arquitectura multi-tenant y multi-dominio
- validacion de seguridad para SQL
- memoria local con ejemplos, hints, reglas y objetos permitidos
- interfaz de administracion para configurar el comportamiento del sistema

Esto significa que el producto ya no es solo una idea: ya existe una plataforma funcional con capacidades reales.

Sin embargo, hoy el sistema se encuentra en un punto intermedio.

### Fortalezas actuales
- ya existe un flujo util y demostrable para consultas operativas
- ya hay separacion entre `index` para uso y `admin` para configuracion
- ya hay componentes configurables del dominio:
  - objetos permitidos
  - semantic hints
  - business rules
  - query patterns
  - training examples
- el sistema ya puede adaptarse a diferentes contextos

### Debilidades actuales
- el onboarding todavia se siente tecnico y pesado para usuarios no tan expertos
- parte importante del comportamiento del dominio sigue viviendo en codigo
- algunas familias de preguntas aun requieren fixes estructurales en matcher o builder
- falta una capa formal de validacion semantica entre intencion y SQL
- el modulo de Admin existe, pero todavia no tiene autoridad completa sobre el comportamiento del dominio

---

## 3. Problema de negocio que esta mejora ataca

Hoy el principal riesgo no es solo tecnico, sino operativo.

Si cada nueva familia de preguntas de negocio obliga a:
- abrir Visual Studio
- modificar C#
- recompilar
- desplegar
- volver a probar

entonces el sistema no escala bien como plataforma configurable.

Eso provoca varios problemas:
- mayor dependencia del equipo tecnico para ajustes operativos
- menor velocidad para adaptar el sistema a nuevos dominios o nuevas preguntas
- mas riesgo de introducir regresiones por cambios pequenos
- menor autonomia del usuario administrador
- menor credibilidad del concepto de “configuracion desde Admin”

En paralelo, si el onboarding sigue siendo complejo o ambiguo:
- el usuario no sabe por donde empezar
- no entiende que pasos son obligatorios
- no sabe cuando el dominio ya esta realmente operativo
- la activacion del sistema se vuelve mas costosa de lo necesario

---

## 4. Idea de mejora

La mejora propuesta tiene tres ejes principales.

### Eje 1 - Convertir Admin en la fuente principal de comportamiento del dominio

La meta es que los casos de negocio frecuentes se resuelvan principalmente desde configuracion, no desde branching hardcoded en codigo.

Esto implica que Admin sea el lugar donde se gobiernen con mas peso real:
- query patterns
- semantic hints
- business rules
- ejemplos validados
- configuracion de metrica, dimension y tiempo

La idea no es eliminar el codigo, sino reservarlo para lo que verdaderamente le corresponde:
- infraestructura del motor
- resolucion de contexto
- ejecucion segura
- validacion estructural
- compilacion declarativa generica
- fallback minimo

### Eje 2 - Simplificar el onboarding como flujo guiado de activacion

El onboarding debe dejar de sentirse como una pagina administrativa larga y pasar a sentirse como un wizard real.

Objetivo:
- que un usuario entienda rapidamente como activar un dominio
- que sepa que hacer primero
- que sepa que es obligatorio
- que sepa que es opcional
- que sepa cuando ya puede hacer una prueba real

### Eje 3 - Agregar validacion semantica antes de ejecutar SQL

No basta con que el SQL compile o sea seguro.
Tambien debe ser coherente con la pregunta del usuario.

La mejora propone agregar una capa que detecte contradicciones como:
- la pregunta menciona una entidad concreta, pero el SQL no la filtra
- la pregunta pide turno actual, pero el SQL no usa filtro temporal equivalente
- la pregunta pide una sola prensa, pero la respuesta es un agregado global
- la pregunta pide `top N`, pero el SQL no agrupa ni ordena correctamente

Esto permite detectar errores plausibles antes de ejecutar la consulta y antes de entregar una respuesta incorrecta al usuario final.

---

## 5. Vision objetivo

La vision objetivo de VannaLight despues de esta mejora es la siguiente:

### Para el usuario final
- hacer preguntas operativas en lenguaje natural
- recibir respuestas mas confiables y contextualizadas
- no depender de conocer SQL

### Para el administrador funcional
- activar un dominio con un flujo mas claro y guiado
- corregir patrones de preguntas desde Admin
- guardar ejemplos buenos sin depender de deploy
- ajustar hints y reglas de negocio de forma mas directa

### Para el equipo tecnico
- reducir fixes puntuales en matcher y builder por casos de negocio repetibles
- tener una frontera mas limpia entre motor y dominio
- escalar el producto a nuevos contextos con menos deuda tecnica

---

## 6. Alcance de la mejora

### Incluye
- fortalecimiento del onboarding
- refuerzo de Query Patterns como capa principal de configuracion de comportamiento
- reduccion de hardcodeo en matcher y builder
- validacion semantica de SQL antes de ejecutar
- mejora del flujo de correccion desde Admin
- documentacion mas clara de lo que se corrige en Admin vs codigo

### No incluye, por ahora
- una reescritura total del motor
- reemplazo completo del LLM actual
- reconstruccion total del modo ML
- soporte enterprise completo multi-organizacion con permisos avanzados
- motor universal para cualquier dominio sin configuracion inicial

---

## 7. Beneficios esperados

### Beneficios operativos
- menor tiempo para activar un nuevo dominio
- menor tiempo para corregir preguntas frecuentes
- menor dependencia de cambios en codigo para ajustes de negocio
- mas control del comportamiento desde Admin

### Beneficios tecnicos
- menos logica de negocio enterrada en C#
- mayor mantenibilidad
- menor riesgo de regresiones por fixes puntuales
- arquitectura mas declarativa

### Beneficios para el piloto
- mejor experiencia en demos
- mayor claridad del mensaje del producto
- mas confianza en respuestas SQL del carril principal
- mejor posicionamiento de Admin como modulo de gobierno del dominio

### Beneficios a futuro
- mejor base para crecer a nuevos dominios
- mejor soporte para versionar configuraciones por contexto
- mejor capacidad para delegar ajustes al usuario administrador sin tocar codigo

---

## 8. Estrategia de implementacion

### Fase 1 - Contener hardcodeo y definir fronteras
- inventariar reglas de negocio actuales en matcher y builder
- clasificar que debe quedarse en codigo y que debe migrar a Admin
- documentar la frontera formal entre motor y dominio

### Fase 2 - Dar prioridad real a Query Patterns
- hacer que las preguntas repetibles del piloto se resuelvan primero por patrones configurados
- dejar el matcher built-in como fallback minimo

### Fase 3 - Declarativizar mas el builder
- reducir branching por caso especifico
- soportar mas construccion por `Metric + Dimension + TimeScope`
- reforzar templates reutilizables

### Fase 4 - Validacion semantica
- validar coherencia entre pregunta y SQL antes de ejecutar
- frenar o marcar consultas sospechosas

### Fase 5 - Mejorar Admin como centro de correccion
- clarificar roles de:
  - Entrenamiento RAG
  - Query Patterns
  - Semantic Hints
  - Business Rules
- dejar un flujo mas obvio para corregir un caso sin deploy

### Fase 6 - Migrar familias demo del piloto
- produccion por prensa
- scrap por prensa
- scrap por numero de parte
- downtime por falla
- scrap cost por molde

---

## 9. Indicadores de exito sugeridos

La mejora puede medirse con indicadores claros.

### De experiencia
- tiempo promedio para activar un dominio nuevo
- numero de pasos necesarios hasta una prueba real exitosa
- numero de intervenciones manuales del equipo tecnico durante onboarding

### De calidad funcional
- porcentaje de preguntas demo correctas al primer intento
- reduccion de consultas corregidas manualmente en Admin
- reduccion de preguntas que generan SQL semantica o logicamente incorrecto

### De mantenimiento
- numero de fixes puntuales en `PatternMatcherService.cs`
- numero de casos resueltos desde Admin sin tocar codigo
- numero de familias de preguntas ya gobernadas por configuracion declarativa

---

## 10. Riesgos y mitigaciones

### Riesgo 1 - Quedarse a medio camino
Si la mejora se deja solo en buenas intenciones, el sistema puede seguir atrapado en una zona gris donde Admin parece configurable, pero la logica real sigue estando en codigo.

Mitigacion:
- definir entregables concretos por fase
- priorizar migracion real de casos del piloto

### Riesgo 2 - Querer reescribir demasiado
Intentar rehacer toda la arquitectura de una sola vez puede frenar el piloto.

Mitigacion:
- avanzar por capas
- conservar lo que ya funciona
- migrar primero familias concretas de preguntas

### Riesgo 3 - Sobreconfigurar Admin
Si Admin gana demasiado poder sin buena UX, el problema se mueve de C# a una interfaz igual de compleja.

Mitigacion:
- mejorar onboarding y copy
- explicar para que sirve cada modulo
- separar camino base de afinacion avanzada

### Riesgo 4 - Validaciones demasiado agresivas
Una validacion semantica mal calibrada podria bloquear consultas validas.

Mitigacion:
- empezar con reglas simples y de alto valor
- registrar warnings antes de endurecer bloqueos

---

## 11. Mensaje de presentacion sugerido

VannaLight ya prueba que es posible consultar informacion operativa en lenguaje natural con contexto controlado.

La mejora propuesta busca llevar el producto del nivel de piloto funcional a una plataforma mas gobernable, escalable y confiable.

No se trata de cambiar la vision del producto.
Se trata de fortalecer tres cosas clave:
- activacion mas simple
- configuracion de dominio mas autonoma desde Admin
- respuestas SQL mas coherentes con la intencion real del usuario

En una frase:

**la mejora convierte a VannaLight de un asistente prometedor con ajustes tecnicos frecuentes en una plataforma operativa mas configurable, mantenible y confiable para uso real.**

---

## 12. Recomendacion final

Si esta mejora se presenta como iniciativa formal, la recomendacion es venderla no como “refactor tecnico”, sino como:

**una evolucion del piloto para reducir dependencia tecnica, acelerar activacion de dominios y aumentar la confianza del usuario en las respuestas del sistema.**

Ese framing comunica mejor su valor para negocio, para operacion y para continuidad del producto.
