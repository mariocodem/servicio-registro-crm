# ServicioRegistroCrm

Servicio de Windows (.NET 10) que:
1. Pide un token a `Crm:UrlToken` con usuario/clave/servicio.
2. Envía `Solicitudes/solicitud.json` a `Crm:UrlRegistro` con `Authorization: Bearer <token>`.
3. Escribe un `.txt` en `Resultados/` (nombre termina en `_EXITO` o `_FALLO`).
4. Guarda la ejecución como entidad `ResultadoRegistro` en SQLite (`registro_crm.db`, tabla `ResultadosRegistro`).

Se repite cada `Crm:IntervaloMinutos` minutos (0 = una sola vez y termina).

## Publicar
```
dotnet publish -c Release -r win-x64 --self-contained false -o C:\Servicios\RegistroCrm
```

## Instalar (PowerShell como administrador)
```
sc.exe create ServicioRegistroCrm binPath= "C:\Servicios\RegistroCrm\ServicioRegistroCrm.exe" start= auto
sc.exe start ServicioRegistroCrm
```
Detener / desinstalar:
```
sc.exe stop ServicioRegistroCrm
sc.exe delete ServicioRegistroCrm
```

## Probar sin instalar
```
dotnet run
```

## Configuración de credenciales
Las credenciales no están en el repositorio. Copia `appsettings.Local.example.json` a
`appsettings.Local.json` y completa `<CODE>` y `<CLAVE>` (pídelos al equipo).
Importante: `Servicio` debe ser `1`.
