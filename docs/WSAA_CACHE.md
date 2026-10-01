# Cache WSAA: seguridad y concurrencia

Los valores `token` y `sign` del Ticket de Acceso (TA) son credenciales sensibles mientras permanezcan vigentes. dcARCA no los registra en logs y, desde este diseño, tampoco los persiste en disco de forma implícita.

## Comportamiento por defecto

`dcArcaAuthService` usa `MemoryWsaaTokenStore.Shared` si el host no inyecta otro store.

Esto mantiene el TA reutilizable entre instancias del servicio dentro del mismo proceso, pero desaparece al finalizar el proceso. Es la opción más segura y no requiere configuración.

## Persistencia filesystem explícita

Si el host necesita compartir/reutilizar el TA entre procesos del mismo equipo, debe opt-in explícitamente:

```csharp
var tokenStore = new FileSystemWsaaTokenStore();

var auth = new dcArcaAuthService(
    config.WsaaUrl,
    config.CertificatePath,
    config.CertificatePassword,
    config.Cuit,
    tokenStore: tokenStore);
```

También puede indicarse un directorio dedicado:

```csharp
var tokenStore = new FileSystemWsaaTokenStore("/ruta/privada/dcarca-cache");
```

El store filesystem:

- coordina procesos mediante lock exclusivo por CUIT + servicio;
- relee el cache dentro de la sección crítica;
- publica escrituras mediante temporal + rename/replace atómico;
- intenta aplicar permisos `0700` al directorio y `0600` al archivo en Unix;
- en Windows depende de las ACL del directorio configuradas por el host.

No coloque este directorio dentro del repositorio, un volumen público o una ruta servida por HTTP.

## Store protegido/cifrado del host

`IWsaaTokenStore` permite implementar almacenamiento cifrado, secret managers, base de datos o coordinación distribuida sin modificar `dcArca.Core`.

La librería no incluye una clave de cifrado propia: almacenar una clave junto al cache sólo trasladaría el mismo problema. Si el host cifra en reposo, la clave debe provenir de su infraestructura de secretos.

Además de `ReadAsync`, `WriteAsync` y `RemoveAsync`, el contrato expone `AcquireLockAsync` para que un store remoto pueda implementar coordinación adecuada a su backend.

## Límite multi-host

`FileSystemWsaaTokenStore` sólo coordina procesos que observan el mismo filesystem y cuyo sistema operativo respeta el lock exclusivo del archivo. No es un lock distribuido.

Si varias máquinas o réplicas independientes comparten un mismo CUIT/servicio, el host debe proveer un `IWsaaTokenStore` con coordinación distribuida o evitar compartir la misma credencial entre hosts.

## Migración desde versiones anteriores

Versiones anteriores de dcARCA escribían automáticamente un JSON bajo el directorio local de la aplicación. Ese comportamiento deja de ser implícito.

- Si no necesita persistencia entre reinicios: no haga nada; se usará memoria.
- Si necesita conservar el comportamiento anterior: inyecte `FileSystemWsaaTokenStore` explícitamente.
- Si los tokens requieren protección adicional en reposo: implemente/injecte un `IWsaaTokenStore` protegido por el host.

Los archivos de cache históricos pueden eliminarse una vez confirmado que ya no son necesarios.
