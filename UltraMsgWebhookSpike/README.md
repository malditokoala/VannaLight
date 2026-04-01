# UltraMsgWebhookSpike

Spike aislado en .NET para validar recepcion de webhooks de UltraMsg, captura del payload real, extraccion del texto del usuario y respuesta basica por WhatsApp usando `HttpClient`.

Tambien incluye un modo opcional de `polling` solo para pruebas, util cuando no tienes acceso para configurar `webhook_url` en la instancia.

## Que hace

- Expone `POST /webhook/whatsapp`
- Expone `GET /health`
- Loguea en consola headers, query params, form fields, body raw y body parseado
- Intenta aceptar payloads JSON, form-data, x-www-form-urlencoded y query string
- Extrae `from`, `text/body`, `messageId` y `chatId`
- Responde con un mensaje fijo: `Recibi tu mensaje: <texto>`
- Evita responder si `fromMe=true`
- Puede consultar chats y mensajes por polling para pruebas sin webhook
- Expone `POST /polling/run-once` para disparar una corrida manual de polling

## Estructura minima

- `Program.cs`: endpoints y flujo principal del webhook
- `Options/UltraMsgOptions.cs`: configuracion
- `Services/RequestInspector.cs`: logging y parseo tolerante
- `Services/UltraMsgClient.cs`: llamada saliente a UltraMsg con `HttpClient`
- `Services/UltraMsgPollingService.cs`: polling manual y deduplicacion basica
- `Services/UltraMsgPollingWorker.cs`: temporizador opcional para pruebas
- `Models/*`: DTOs minimos para inspeccion y resultado

## Configuracion

### Opcion 1: user-secrets

Desde la raiz del repo:

```powershell
dotnet user-secrets set "UltraMsg:InstanceId" "instance12345" --project .\UltraMsgWebhookSpike\UltraMsgWebhookSpike.csproj
dotnet user-secrets set "UltraMsg:Token" "tu_token" --project .\UltraMsgWebhookSpike\UltraMsgWebhookSpike.csproj
dotnet user-secrets set "UltraMsg:WebhookUrl" "https://tu-url-publica.ngrok-free.app/webhook/whatsapp" --project .\UltraMsgWebhookSpike\UltraMsgWebhookSpike.csproj
dotnet user-secrets set "UltraMsg:EnablePollingForTests" "true" --project .\UltraMsgWebhookSpike\UltraMsgWebhookSpike.csproj
dotnet user-secrets set "UltraMsg:PollingTargetChatId" "526221524507@c.us" --project .\UltraMsgWebhookSpike\UltraMsgWebhookSpike.csproj
```

### Opcion 2: variables de entorno

```powershell
$env:UltraMsg__InstanceId="instance12345"
$env:UltraMsg__Token="tu_token"
$env:UltraMsg__WebhookUrl="https://tu-url-publica.ngrok-free.app/webhook/whatsapp"
$env:UltraMsg__EnablePollingForTests="true"
$env:UltraMsg__PollingTargetChatId="526221524507@c.us"
```

### Opcion 3: appsettings para pruebas locales

Puedes editar `appsettings.Development.json`, pero no es la opcion recomendada para secretos.

## Como correrlo localmente

1. Ve a la raiz del repo.
2. Configura `InstanceId` y `Token` por user-secrets o variables de entorno.
3. Ejecuta:

```powershell
dotnet run --project .\UltraMsgWebhookSpike\UltraMsgWebhookSpike.csproj
```

4. Verifica salud:

```powershell
Invoke-RestMethod -Method Get -Uri http://localhost:5187/health
```

## Modo polling solo para pruebas

Si no tienes acceso para configurar el webhook en UltraMsg, puedes probar la idea tecnica con polling.

El polling hace esto:

- consulta un chat especifico (`PollingTargetChatId`) o intenta listar chats
- lee los ultimos mensajes del chat
- detecta mensajes nuevos que no vengan de la misma instancia (`fromMe != true`)
- extrae `body`
- responde con `Recibi tu mensaje: <texto>`

Configuracion minima recomendada:

```powershell
dotnet user-secrets set "UltraMsg:EnablePollingForTests" "true" --project .\UltraMsgWebhookSpike\UltraMsgWebhookSpike.csproj
dotnet user-secrets set "UltraMsg:PollingTargetChatId" "526221524507@c.us" --project .\UltraMsgWebhookSpike\UltraMsgWebhookSpike.csproj
```

Luego puedes dejarlo correr automaticamente cada `PollingIntervalSeconds` o dispararlo manualmente con:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5187/polling/run-once
```

Nota importante:

- Este modo valida captura de la pregunta y round-trip basico.
- No valida que UltraMsg envie webhooks.
- El parser de respuestas de `chats/ids` y `chats/messages` se dejo tolerante apoyandose en el ejemplo de C# que compartiste y en la estructura habitual de UltraMsg.

## Como exponerlo a UltraMsg

Para que UltraMsg te mande webhooks reales, tu endpoint local necesita una URL publica temporal. Lo normal para este spike es usar ngrok o una alternativa similar.

Ejemplo con ngrok:

```powershell
ngrok http 5187
```

Luego toma la URL publica, por ejemplo:

`https://abc123.ngrok-free.app/webhook/whatsapp`

Y configuralo en UltraMsg como `webhook_url`.

En UltraMsg tambien debes asegurarte de habilitar la notificacion de mensajes recibidos (`webhook_message_received=true`). En la documentacion publica de UltraMsg eso se configura en los settings de la instancia junto con `webhook_url`.

## Como configurar instanceId, token y webhookUrl

- `InstanceId`: identificador de tu instancia UltraMsg
- `Token`: token de API de esa instancia
- `WebhookUrl`: URL publica que apunta a `POST /webhook/whatsapp`

La llamada saliente de respuesta usa:

`POST https://api.ultramsg.com/{instanceId}/messages/chat`

Con campos `token`, `to` y `body`.

## Como probarlo manualmente sin WhatsApp

### JSON

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5187/webhook/whatsapp -ContentType "application/json" -Body '{"event_type":"message_received","data":{"id":"wamid.demo-1","from":"5215512345678@c.us","body":"Hola mundo","fromMe":false}}'
```

### Form-urlencoded

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5187/webhook/whatsapp -ContentType "application/x-www-form-urlencoded" -Body "from=5215512345678@c.us&body=Hola+desde+form&id=form-1"
```

### Query params

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:5187/webhook/whatsapp?from=5215512345678@c.us&body=Hola+desde+query&id=query-1"
```

## Como validar el round-trip

1. Arranca la app y deja la consola visible.
2. Exponla con ngrok.
3. Configura esa URL como webhook en UltraMsg.
4. Manda un mensaje de WhatsApp al numero conectado a la instancia.
5. Valida en consola:
   - que llego el request
   - que se loguearon headers, query y body
   - que `From` y `Text` quedaron extraidos
   - que el endpoint intento llamar a UltraMsg
   - que UltraMsg respondio con `2xx`
6. Valida en el telefono que el contacto recibio `Recibi tu mensaje: <texto>`

Si el mensaje entra pero no se responde, revisa:

- `UltraMsg:InstanceId`
- `UltraMsg:Token`
- que la instancia este autenticada
- que `fromMe` no venga en `true`
- que el payload realmente traiga `body` o `text`

## Como validar con polling sin webhook

1. Configura `InstanceId`, `Token` y `EnablePollingForTests=true`.
2. Si quieres reducir ruido, configura `PollingTargetChatId` con tu chat, por ejemplo `526221524507@c.us`.
3. Arranca la app.
4. La primera corrida hace `warmup` y registra mensajes existentes sin responderlos.
5. Manda un mensaje nuevo desde tu WhatsApp.
6. Espera el siguiente ciclo o llama a `POST /polling/run-once`.
7. Valida en consola que el polling detecto un mensaje nuevo y que intento responderlo.
8. Valida en tu telefono que llego `Recibi tu mensaje: <texto>`.

## Peculiaridades y limitaciones a considerar con UltraMsg

- El formato del webhook puede variar segun evento o configuracion; por eso el spike intenta leer varios nombres de campo.
- Puede haber eventos que no sean mensajes entrantes reales. No conviene asumir que todo webhook trae texto util.
- Algunos campos como `msgId` para reply pueden variar segun el tipo de mensaje o endpoint. Por eso `IncludeReplyToMessageId` queda en `false` por defecto.
- Si respondes sin filtrar `fromMe`, puedes generar loops.
- Si UltraMsg o WhatsApp demoran, el webhook puede llegar antes de que tengas todo el contexto de negocio.
- Es un servicio tercero: debes contemplar timeouts, reintentos y caidas externas antes de llevarlo a produccion.

## Por que webhook es mejor que polling aqui

- Recibes el mensaje casi en tiempo real.
- Evitas estar consultando mensajes cada pocos segundos.
- Gastas menos llamadas a la API.
- La trazabilidad del evento entrante es mas simple.
- Para round-trip basico, el flujo se vuelve mucho mas directo: llega evento, extraes texto, respondes.
- Polling sigue siendo util para pruebas cuando no tienes permisos de admin sobre la instancia.

## Como evolucionaria despues

Cuando validemos este spike, el siguiente paso natural seria:

1. mover la logica de parseo a un servicio reutilizable dentro del backend principal
2. agregar persistencia de mensajes entrantes y salientes
3. validar firma/autenticacion del webhook si UltraMsg ofrece un mecanismo
4. agregar idempotencia para evitar reprocesar eventos repetidos
5. desacoplar la respuesta con una cola o background worker
6. conectar el texto del usuario con tu backend principal o tu motor de respuestas

## Notas de diseno

- Este proyecto es intencionalmente independiente y no esta agregado al `VannaLight.slnx`
- Se priorizo simplicidad y observabilidad sobre arquitectura avanzada
- No usa RestSharp
