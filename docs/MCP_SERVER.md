# dcArca.McpServer

`dcArca.McpServer` expone las operaciones de facturación electrónica de `dcArca.Core` como tools MCP sobre HTTP, protegidas con OAuth/OIDC y JWT Bearer.

## Arquitectura

```text
cliente MCP
   |
   | obtiene JWT
   v
Authorization Server externo
   |
   | Bearer token
   v
dcArca.McpServer
   |
   v
dcArca.Core
   |
   +--> WSAA
   +--> WSFEv1
   +--> Padrón ARCA
```

El MCP es un **resource server**. Valida JWT emitidos por un Authorization Server externo; **no emite tokens, client secrets ni credenciales propias**.

## Requisitos

- .NET 10 SDK para compilar desde código;
- certificado ARCA/PFX válido para el ambiente elegido;
- servicios ARCA correspondientes autorizados para ese certificado;
- un Authorization Server OIDC/JWT;
- un punto de venta habilitado para WSFE.

## Compilar

```bash
dotnet restore dcArca.McpServer/dcArca.McpServer.csproj
dotnet build dcArca.McpServer/dcArca.McpServer.csproj -c Release
```

Docker:

```bash
docker build -f dcArca.McpServer/Dockerfile -t dcarca-mcpserver .
```

El `docker-compose.yml` de la raíz es deliberadamente genérico y no contiene dominios, redes privadas ni secretos de producción.

## Configuración

Partir de `dcArca.McpServer/appsettings.example.json`.

Configuración principal:

```json
{
  "dcArcaConfig": {
    "Cuit": "TU_CUIT",
    "CertificatePath": "/certs/certificado.pfx",
    "CertificatePassword": "DESDE_SECRET_MANAGER",
    "WsaaUrl": "https://wsaahomo.afip.gov.ar/ws/services/LoginCms",
    "WsfeUrl": "https://wswhomo.afip.gov.ar/wsfev1/service.asmx",
    "PadronUrl": "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5",
    "PuntoVenta": 1
  },
  "Jwt": {
    "Authority": "https://idp.example.com/",
    "Audience": "dcarca-mcp"
  }
}
```

Variables de entorno equivalentes usan `__` como separador:

```bash
Jwt__Authority=https://idp.example.com/
Jwt__Audience=dcarca-mcp
dcArcaConfig__Cuit=...
dcArcaConfig__CertificatePath=/certs/certificado.pfx
dcArcaConfig__CertificatePassword=...
dcArcaConfig__WsaaUrl=...
dcArcaConfig__WsfeUrl=...
dcArcaConfig__PadronUrl=...
dcArcaConfig__PuntoVenta=1
```

No guardar PFX, passwords, tokens, client secrets ni configuración privada en el repositorio.

### Homologación vs producción

Use certificado y endpoints del mismo ambiente. No mezcle un certificado de homologación con endpoints productivos.

`Development` permite un `Jwt:Authority` HTTP para un IdP local. Fuera de Development, `Jwt:Authority` debe usar HTTPS y la metadata OIDC también se exige por HTTPS.

## OAuth y scopes

Scopes soportados:

- `arca:consultar`: lectura;
- `arca:facturar`: emisión.

Son independientes. `arca:facturar` **no implica** `arca:consultar`. Un cliente que necesite ambas capacidades debe pedir:

```text
arca:consultar arca:facturar
```

El JWT debe tener issuer, audience, lifetime y firma válidos para `Jwt:Authority` / `Jwt:Audience`.

El claim `scope` se interpreta según la convención OIDC habitual de valores separados por espacios.

### Client credentials / M2M

Para una integración servidor-a-servidor, el cliente obtiene un token del IdP mediante client credentials y luego lo usa como Bearer token contra el MCP.

El `client_secret` pertenece al backend/secret manager. Nunca debe entregarse a un frontend, a un LLM o a un usuario final.

## Keycloak local de desarrollo

El repositorio incluye `deploy/local-keycloak/` para probar OAuth sin depender de infraestructura privada.

```bash
cd deploy/local-keycloak
docker compose up -d
```

Ejemplo de token local:

```bash
curl -s -X POST \
  http://localhost:8081/realms/dcarca/protocol/openid-connect/token \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=client_credentials' \
  -d 'client_id=dcarca-mcp' \
  -d 'client_secret=dcarca-mcp-local-dev-secret' \
  -d 'scope=arca:consultar arca:facturar'
```

Ese secret es exclusivamente de desarrollo local y no debe reutilizarse en producción.

## Endpoint MCP

El transporte HTTP es stateless y se publica en la raíz `/`.

Ejemplo `tools/list`:

```bash
curl -s http://localhost:8080/ \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  --data '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

Sin token el endpoint MCP devuelve 401. Cuando un tool requiere un scope que el token no posee, el SDK MCP lo oculta de `tools/list` y rechaza llamadas directas.

## Tools

### Lectura — scope `arca:consultar`

#### `consultar_ultimo_comprobante`

Consulta el último número autorizado para el tipo indicado y el punto de venta configurado.

Parámetro principal: `tipoComprobante`.

#### `consultar_comprobante`

Consulta un comprobante ya emitido.

Parámetros: `numeroComprobante`, `tipoComprobante`.

La respuesta puede incluir CAE, importes, moneda/cotización, detalle de IVA, tributos, fechas y observaciones.

#### `consultar_condiciones_iva`

Consulta condiciones de IVA válidas para un receptor y tipo de comprobante.

Parámetros: `docTipo`, `docNro`, `tipoComprobante`.

#### `consultar_padron`

Consulta información registral de un CUIT en el padrón autorizado.

Parámetro: `cuit`.

### Emisión — scope `arca:facturar`

#### `emitir_comprobante` — recomendado para el caso simple

No recibe número de comprobante. El servidor consulta el último autorizado y asigna el siguiente dentro de un lock por CUIT/punto de venta/tipo.

Soporta el caso simple de una alícuota mediante `alicuotaIva`, además de fechas de servicio y comprobante/período asociado para notas.

Si `importeIva > 0`, la alícuota debe informarse explícitamente; dcARCA no infiere 21%.

#### `emitir_comprobante_avanzado` — modelo fiscal completo

Recibe un `dcFacturaRequest` completo y también asigna el número server-side; cualquier `NumeroComprobante` recibido es ignorado.

Úselo para:

- cero, una o múltiples alícuotas de IVA;
- importes exentos;
- importes no gravados;
- tributos/percepciones;
- moneda y cotización;
- servicios;
- notas con comprobante o período asociado.

Ejemplo conceptual del objeto `factura`:

```json
{
  "tipoComprobante": 1,
  "concepto": 1,
  "cuitReceptor": 20123456786,
  "tipoDocReceptor": 80,
  "condicionIvaReceptor": 1,
  "importeNeto": 200.00,
  "importeIva": 31.50,
  "importeNoGravado": 0,
  "importeExento": 0,
  "importeTotal": 231.50,
  "iva": [
    { "alicuota": 5, "baseImponible": 100.00, "importe": 21.00 },
    { "alicuota": 4, "baseImponible": 100.00, "importe": 10.50 }
  ],
  "tributos": [],
  "monedaId": "PES",
  "monedaCotizacion": 1,
  "fechaComprobante": "20260930"
}
```

#### `solicitar_cae` — bajo nivel

Permite elegir manualmente el número fiscal. Se conserva para integraciones avanzadas que coordinan la secuencia fuera del MCP.

No es el flujo recomendado para callers generales: dos consumidores que calculen el mismo siguiente número pueden colisionar.

## Numeración y concurrencia

`emitir_comprobante` y `emitir_comprobante_avanzado` serializan la secuencia por:

```text
(CUIT emisor, punto de venta, tipo de comprobante)
```

Dentro del lock:

1. consulta último autorizado;
2. calcula siguiente;
3. emite;
4. libera el lock incluso ante excepción.

Este lock es **in-process**. Si existen varias réplicas o sistemas externos compartiendo el mismo punto de venta, se requiere coordinación distribuida o un punto de venta dedicado.

## Resultado incierto y reconciliación

Si el transporte falla durante `FECAESolicitar`, el servidor no repite la emisión a ciegas. Consulta primero el mismo comprobante con `FECompConsultar`.

`dcFacturaResponse.EmissionOutcome` puede indicar:

- `Authorized`;
- `FiscalRejected`;
- `RecoveredSuccess`;
- `Uncertain`.

Si la consulta confirma que ARCA ya autorizó el comprobante, se devuelve el éxito recuperado. Si no puede confirmarse el estado, se devuelve `Uncertain` y no se hace un segundo envío automático.

## Cache WSAA

Por defecto el TA se mantiene en memoria. No se persisten `token/sign` silenciosamente.

Si el host necesita persistencia entre procesos del mismo equipo, puede inyectar `FileSystemWsaaTokenStore`, que usa lock inter-proceso y escritura atómica. Para protección cifrada o coordinación multi-host, implemente `IWsaaTokenStore` en el host.

## Health

- `GET /health/live`: proceso vivo.
- `GET /health/ready`: configuración esencial cargada y servidor listo.

No llaman a ARCA y no devuelven secretos ni CUIT.

## Seguridad

- TLS obligatorio en producción.
- No commitear certificados, PFX, passwords, JWT ni client secrets.
- No usar el Keycloak/local secret de ejemplo en producción.
- No entregar client secrets a frontend, LLM o usuario final.
- Aplicar mínimo privilegio en scopes.
- Preferir `emitir_comprobante` sobre numeración manual.
- Mantener certificados y secretos en mecanismos provistos por el host.
- El repositorio público no despliega infraestructura privada.

## Referencias internas

- `docs/MCP_AUTHORIZATION.md`
- `docs/MCP_NUMBERING.md`
- `docs/MCP_CONFIGURATION.md`
- `docs/WSFE_RECONCILIATION.md`
- `docs/WSAA_CACHE.md`
- `docs/REPOSITORY_SECURITY.md`
