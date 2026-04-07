# VannaLight.HoloLens

Wrapper UWP para HoloLens 2 que aloja el frontend web dentro de `WebView2` y le inyecta la URL base de la API ASP.NET Core.

La configuracion por defecto apunta a `hololens-sql.html`, una version minima enfocada solo en Text-to-SQL.

## Ajustes rápidos

1. Edita `appsettings.json` para apuntar a la IP real de la planta:
   - `FrontendUrl`
   - `ApiBaseUrl`
2. Si quieres usar la UI minima de PoC, publica la API y abre `http://TU_IP:5122/hololens-sql.html`.
3. Abre la solución en Visual Studio 2022 con el workload de UWP.
4. Restaura paquetes NuGet.
5. Selecciona `ARM64` y despliega en HoloLens 2 o emulador.

## Notas de red

- `privateNetworkClientServer` es obligatorio para hablar con una API dentro de la misma subred.
- Si el frontend se sirve por `https://` y la API por `http://`, el navegador puede bloquear tráfico por contenido mixto. En PoC conviene usar el mismo protocolo en ambos extremos.
- El shell inyecta `window.API_URL`, `window.API_BASE_URL` y `window.VANNALIGHT_CONFIG` antes de que cargue el frontend.
