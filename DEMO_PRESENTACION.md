# Demo de presentacion - VannaLight

## Objetivo

Presentar `VannaLight` como un piloto funcional enfocado en manufactura, con valor claro en consultas operativas en lenguaje natural.

## Mensaje principal

`VannaLight permite consultar informacion operativa en lenguaje natural, con contexto controlado y sin depender de que el usuario sepa SQL.`

## Recorrido recomendado de 10 minutos

1. Problema
- La informacion existe, pero esta fragmentada.
- El usuario operativo no siempre conoce SQL ni la estructura del sistema.

2. Flujo estrella
- Abrir el chat principal.
- Ejecutar una pregunta fuerte de negocio en `SQL`.
- Mostrar resultado tabular o KPI.

3. Cambio de contexto
- Cambiar de dominio o base activa.
- Ejecutar una pregunta simple para demostrar aislamiento por contexto.

4. Administracion
- Mostrar brevemente `admin.html`.
- Enseñar que se puede configurar:
  - workspace
  - dominio
  - conexion
  - objetos permitidos
  - hints o reglas

5. Exportacion
- Exportar un resultado para aterrizar utilidad operativa.

6. Cierre
- Reforzar que hoy el foco es `Text-to-SQL`.
- Documentos y prediccion se presentan como extensiones del piloto.

## Que si mostrar
- Chat principal
- Selector de contexto
- Una o dos consultas muy fuertes
- Historial local
- Exportacion

## Que no mostrar
- Compilacion en vivo
- Setup tecnico completo
- Rutas experimentales
- Configuracion profunda del LLM

## Checklist antes de presentar
- Verificar que la API levante.
- Verificar conexion a base.
- Verificar contextos visibles en el selector.
- Verificar al menos 3 preguntas demo por modo.
- Verificar exportacion.
- Dejar abiertas las pantallas necesarias:
  - `index.html`
  - `admin.html`

## Notas
- Si el flujo documental o ML no esta totalmente estable, no usarlo como pieza central.
- La demo debe sentirse controlada, rapida y clara.
