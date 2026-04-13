# Handoff para PC de trabajo

## Fecha
- 2026-04-12

## Contexto

Durante la preparacion de demo se detecto que `index` mostraba `Sin contexto seleccionado` y en `admin` algunos workspaces aparecian como inactivos.

La causa no fue solo mover las cadenas de conexion a `user-secrets`.

El problema real fue una combinacion de:
- nombres de conexion distintos entre contextos, por ejemplo `NorthwindDB` vs `NorthwindDb`
- ausencia de `appsettings.Local.json` en esta maquina
- estado previo en SQLite local con `TenantDomains.IsActive = 0`
- bootstrap que no reactivaba seeds/contextos validos si ya existian pero estaban dormidos

## Cambios ya realizados

Se actualizo [Program.cs](/C:/Users/edgar/OneDrive/Desktop/VannaLight/VannaLight.Api/Program.cs) para:
- reactivar seeds validos al arrancar
- recuperar contextos `UserManaged` si su `ConnectionProfile` sigue activo

Tambien se reparo el estado local en esta maquina para reactivar:
- `northwind-demo / northwind-sales / NorthwindDb`
- `zenit-mx / northwind-zenit / NorthwindDB`

## Importante para la PC del trabajo

Al hacer `git pull`, los contextos del trabajo no deberian romperse si la maquina de trabajo ya tiene:
- `user-secrets` correctos
- `%LOCALAPPDATA%\\VannaLight\\Data\\vanna_memory.db`
- `%LOCALAPPDATA%\\VannaLight\\Data\\vanna_runtime.db`

El repo ya no debe considerarse la fuente de verdad para esos datos locales.

## Checklist para mañana en el trabajo

1. Ejecutar:
```powershell
dotnet user-secrets list --project .\VannaLight.Api\VannaLight.Api.csproj
```

2. Confirmar que existan los `ConnectionStrings` esperados en esa maquina.

3. Revisar que el nombre del `ConnectionName` coincida exactamente con lo que usan los contextos locales.
Ejemplo:
- `NorthwindDb` no es igual a `NorthwindDB`

4. Levantar la API y validar:
- `index` ya no muestra `Sin contexto seleccionado`
- `admin` ya no muestra los workspaces correctos como inactivos

5. Si algun workspace sigue inactivo, revisar primero el estado local:
- `%LOCALAPPDATA%\\VannaLight\\Data\\vanna_memory.db`

## Instruccion para Codex en la PC del trabajo

Si mañana `index` aparece sin contexto o `admin` muestra workspaces inactivos:

1. revisar `user-secrets`
2. revisar si el `ConnectionName` coincide exactamente
3. revisar `Tenants`, `TenantDomains` y `ConnectionProfiles` en la SQLite local
4. confirmar si el contexto esta inactivo solo por estado local
5. no asumir que el problema esta en git o en el repo

## Objetivo

Llegar a la presentacion con:
- contextos visibles en `index`
- workspaces activos en `admin`
- demo de `Text-to-SQL` estable
