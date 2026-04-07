# Despliegue ARM64 para HoloLens 2

## Prerrequisitos

- Visual Studio 2022 en Windows con el workload `Universal Windows Platform development`.
- SDK de Windows 10/11 compatible con UWP.
- HoloLens 2 en `Developer Mode`.
- El dispositivo y el host de despliegue dentro de la misma red o conectados por USB.

## Preparacion

1. Abre [`VannaLight.slnx`](C:/Users/edgar/OneDrive/Desktop/VannaLight/VannaLight.slnx) en Visual Studio.
2. Restaura paquetes NuGet del proyecto [`VannaLight.HoloLens.csproj`](C:/Users/edgar/OneDrive/Desktop/VannaLight/VannaLight.HoloLens/VannaLight.HoloLens.csproj).
3. Ajusta [`appsettings.json`](C:/Users/edgar/OneDrive/Desktop/VannaLight/VannaLight.HoloLens/appsettings.json) con la IP o URL real del frontend y de la API.
4. Verifica que el frontend permita acceder a la API por CORS y que ambos usen el mismo protocolo cuando sea posible.

## Compilacion

1. Selecciona la configuracion `Release`.
2. Selecciona la plataforma `ARM64`.
3. Establece `VannaLight.HoloLens` como proyecto de inicio.

## Despliegue

1. En el selector de destino, elige:
   - `Device` para un HoloLens 2 fisico.
   - `Remote Machine` si desplegaras por IP.
2. Si usas `Remote Machine`, captura la IP del HoloLens 2 y usa autenticacion `Universal (Unencrypted Protocol)` solo para PoC internas.
3. Ejecuta `Deploy`.

## Validacion en planta

1. Confirma que la shell carga el frontend.
2. Verifica que `window.API_URL` y `window.API_BASE_URL` apunten a la IP esperada.
3. Prueba una llamada `fetch` a la API desde el frontend.
4. Prueba acceso a camara y microfono desde la pagina.

## Diagnostico rapido

- Si la UI carga pero las llamadas a la API fallan, revisa `privateNetworkClientServer` en [`Package.appxmanifest`](C:/Users/edgar/OneDrive/Desktop/VannaLight/VannaLight.HoloLens/Package.appxmanifest).
- Si el navegador bloquea solicitudes, revisa mezcla de `http` y `https`.
- Si la pagina no recibe la configuracion, revisa la inyeccion en [`MainPage.xaml.cs`](C:/Users/edgar/OneDrive/Desktop/VannaLight/VannaLight.HoloLens/MainPage.xaml.cs).
