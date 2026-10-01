# Changelog

Todos los cambios relevantes de dcARCA se documentan en este archivo.

## [2.0.0] - 2026-10-01

### Added
- Servidor `dcArca.McpServer` como resource server MCP autenticado con JWT/OIDC.
- Scopes independientes `arca:consultar` y `arca:facturar`.
- Emisión MCP con numeración server-side serializada por CUIT, punto de venta y tipo de comprobante.
- Reconciliación de emisiones con resultado incierto antes de cualquier reintento.
- Health/liveness/readiness para operación en contenedores y orquestadores.
- `IWsaaTokenStore`, cache en memoria y store filesystem explícito/endurecido.
- Modelo fiscal WSFE explícito para múltiples alícuotas de IVA, exentos, no gravados, tributos, moneda y cotización.
- Documentación completa del MCP, seguridad, scopes, numeración, reconciliación y configuración.

### Changed
- El cache WSAA deja de persistirse implícitamente en disco; por defecto se usa memoria.
- Las operaciones con IVA positivo deben informar explícitamente la alícuota o el detalle de IVA; ya no se asume 21%.
- La emisión MCP recomendada deja de requerir que el caller calcule el próximo número.
- Los scopes de lectura y escritura son independientes; un cliente que necesita ambas capacidades debe solicitar ambos.
- La configuración Docker pública es genérica y no contiene infraestructura privada.

### Fixed
- DNI y otros documentos dejan de validarse con el algoritmo de CUIT.
- Cache WSAA coordinado entre procesos del mismo host mediante lock por archivo y escritura atómica.
- Invalidación tardía de TA no elimina un token ya renovado por otra instancia.
- Respuestas inciertas de `FECAESolicitar` se reconcilian consultando ARCA antes de decidir el estado.

### Security
- Eliminado el workflow de deploy privado desde el repositorio público.
- Retirada la API REST remota no autenticada de `dcArca.Service`.
- La única superficie remota soportada es MCP autenticado; la utilidad local queda como `dcArca.Cli`.
- Los tokens/sign WSAA no se persisten en texto plano salvo opt-in explícito al store filesystem del host.
- `main` tiene ruleset activo con PR obligatorio, bloqueo de delete y non-fast-forward.

### Breaking changes / migration
- `dcArcaAuthService` usa `MemoryWsaaTokenStore.Shared` por defecto. Para persistencia entre procesos/reinicios, inyectar `FileSystemWsaaTokenStore` o un `IWsaaTokenStore` propio.
- Requests con `ImporteIva > 0` deben informar `AlicuotaIva` o `Iva`; dcARCA ya no infiere 21%.
- `dcArca.Service` fue retirado. Para automatización local usar `dcArca.Cli`; para acceso remoto usar `dcArca.McpServer` con OAuth/JWT.
- `arca:facturar` no implica `arca:consultar`; solicitar ambos scopes cuando el cliente necesite leer y emitir.

## [1.0.0] - 2026-09-24

- Primera release estable de dcARCA.
- Migración a .NET 10.
- Primera suite de tests de Core.
- Correcciones iniciales de CUIT, fechas UTC, escaping SOAP y dependencias vulnerables/no utilizadas.
