# ARCA-MCP — Facturación electrónica argentina en .NET + MCP

**ARCA-MCP** es un proyecto open source en **.NET 10** para integrar aplicaciones y agentes con los servicios de facturación electrónica de **ARCA (ex AFIP)**.

El repositorio nació como **dcARCA**. La solución evolucionó desde una librería embebible y una aplicación de prueba hasta incorporar un **servidor MCP autenticado**, una **CLI multiplataforma**, hardening de seguridad, tests y mecanismos específicos para operar WSFE de forma más segura. Los proyectos, namespaces y tipos internos conservan el prefijo **dcArca** por continuidad y compatibilidad.

Actualmente integra:

- **WSAA** para autenticación con certificado digital.
- **WSFEv1** para emisión y consulta de comprobantes.
- **ws_sr_constancia_inscripcion** para consulta de padrón.
- **Model Context Protocol (MCP)** sobre HTTP con OAuth/OIDC y JWT Bearer.

> ARCA-MCP **no es un producto oficial de ARCA ni del Gobierno Argentino**. Es una implementación independiente desarrollada por Diego Cofré Sistemas.

## Estado actual

La arquitectura actual está compuesta por varias superficies con responsabilidades distintas:

| Proyecto | Función |
| --- | --- |
| **dcArca.Core** | Librería embebible con WSAA, WSFEv1, padrón, validaciones y modelo fiscal. |
| **dcArca.McpServer** | Servidor MCP HTTP autenticado. Es la superficie remota soportada por el proyecto público. |
| **dcArca.Cli** | Utilidad de consola local para administración, pruebas e integraciones sin servidor HTTP. |
| **dcArca.TestApp** | Aplicación WinForms de referencia y diagnóstico para Windows. |
| **dcArca.Core.Tests** | Tests del Core, validaciones fiscales, cache WSAA y reconciliación. |
| **dcArca.McpServer.Tests** | Tests de autenticación, scopes, health y secuenciación MCP. |

La antigua API REST remota no autenticada fue retirada. Para acceso remoto debe utilizarse **dcArca.McpServer**; para automatización local puede utilizarse **dcArca.Cli** o integrar directamente **dcArca.Core**.

## Capacidades principales

- Emisión de Facturas, Notas de Crédito y Notas de Débito A, B, C y M mediante WSFEv1.
- Conceptos de Productos, Servicios y Productos + Servicios.
- Consulta de último comprobante autorizado con FECompUltimoAutorizado.
- Consulta completa de comprobantes existentes con FECompConsultar.
- Consulta de condiciones de IVA del receptor.
- Consulta de padrón por CUIT.
- Modelo fiscal explícito con:
  - una o múltiples alícuotas de IVA;
  - importes exentos y no gravados;
  - tributos y percepciones;
  - moneda y cotización;
  - fechas de servicio;
  - comprobante o período asociado para notas.
- Validación diferenciada de CUIT, DNI y otros tipos de documento.
- Reconciliación de emisiones con resultado incierto.
- Cache WSAA desacoplado mediante IWsaaTokenStore.
- Health checks para ejecución en contenedores.
- Dockerfile y docker-compose genéricos, sin infraestructura privada ni secretos.

## Arquitectura MCP

~~~text
Cliente MCP
    |
    | obtiene JWT
    v
Authorization Server externo
    |
    | Bearer token
    v
dcArca.McpServer
    |
    +--> dcArca.Core
            |
            +--> WSAA
            +--> WSFEv1
            +--> Padrón ARCA
~~~

**dcArca.McpServer es un resource server**: valida tokens emitidos por un Authorization Server externo. No genera usuarios, client secrets ni tokens propios.

Los scopes son independientes:

- **arca:consultar**: operaciones de lectura.
- **arca:facturar**: operaciones de emisión.

Un cliente que necesite ambas capacidades debe solicitar ambos scopes.

La documentación completa está en [docs/MCP_SERVER.md](docs/MCP_SERVER.md).

## Quick Start — servidor MCP

### Requisitos

- .NET 10 SDK.
- Certificado ARCA válido para el ambiente elegido.
- Servicio wsfe autorizado para el certificado.
- Servicio ws_sr_constancia_inscripcion autorizado si se utilizará padrón.
- Punto de venta habilitado para WSFE.
- Authorization Server compatible con OIDC/JWT.

### 1. Clonar

~~~bash
git clone https://github.com/diegocofre/ARCA-MCP.git
cd ARCA-MCP
~~~

### 2. Configurar

Partí del archivo de ejemplo:

~~~bash
cp dcArca.McpServer/appsettings.example.json dcArca.McpServer/appsettings.json
~~~

Configuración mínima:

~~~json
{
  "dcArcaConfig": {
    "Cuit": "TU_CUIT",
    "CertificatePath": "/ruta/certificado.pfx",
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
~~~

También pueden utilizarse variables de entorno con doble guion bajo, por ejemplo:

~~~bash
Jwt__Authority=https://idp.example.com/
Jwt__Audience=dcarca-mcp
dcArcaConfig__Cuit=...
dcArcaConfig__CertificatePath=/certs/certificado.pfx
dcArcaConfig__CertificatePassword=...
dcArcaConfig__WsaaUrl=...
dcArcaConfig__WsfeUrl=...
dcArcaConfig__PadronUrl=...
dcArcaConfig__PuntoVenta=1
~~~

### 3. Compilar y ejecutar

~~~bash
dotnet restore dcArca.McpServer/dcArca.McpServer.csproj
dotnet build dcArca.McpServer/dcArca.McpServer.csproj -c Release
dotnet run --project dcArca.McpServer/dcArca.McpServer.csproj
~~~

El endpoint MCP se publica en la raíz HTTP del servidor.

Health checks:

- **GET /health/live**
- **GET /health/ready**

Ambos son livianos y no llaman a ARCA.

### Desarrollo local con Keycloak

El repositorio incluye un entorno Keycloak de desarrollo en **deploy/local-keycloak/** para probar OAuth/OIDC sin depender de infraestructura privada.

~~~bash
cd deploy/local-keycloak
docker compose up -d
~~~

Ver [docs/MCP_SERVER.md](docs/MCP_SERVER.md) para el flujo completo de obtención del token y llamada a tools/list.

## Tools MCP

### Scope arca:consultar

- **consultar_ultimo_comprobante**
- **consultar_comprobante**
- **consultar_condiciones_iva**
- **consultar_padron**

### Scope arca:facturar

- **emitir_comprobante** — flujo recomendado para casos simples.
- **emitir_comprobante_avanzado** — modelo fiscal completo.
- **solicitar_cae** — operación de bajo nivel con numeración elegida por el caller.

### Numeración segura

Los tools **emitir_comprobante** y **emitir_comprobante_avanzado** no requieren que el cliente calcule el próximo número.

El servidor serializa la secuencia por:

~~~text
(CUIT emisor, punto de venta, tipo de comprobante)
~~~

Dentro del lock:

1. consulta el último autorizado;
2. calcula el siguiente;
3. emite;
4. libera el lock incluso ante excepción.

El lock es **in-process**. Si varias réplicas o sistemas externos comparten un mismo punto de venta, debe utilizarse coordinación distribuida o un punto de venta dedicado.

Más detalle: [docs/MCP_NUMBERING.md](docs/MCP_NUMBERING.md).

## Reconciliación de emisiones inciertas

Un timeout o corte de transporte después de enviar FECAESolicitar no garantiza que ARCA haya descartado la operación.

ARCA-MCP intenta reconciliar el mismo comprobante mediante FECompConsultar antes de decidir el resultado. El modelo puede informar:

- **Authorized**
- **FiscalRejected**
- **RecoveredSuccess**
- **Uncertain**

No se realiza un segundo FECAESolicitar automático cuando el estado fiscal no puede confirmarse.

Más detalle: [docs/WSFE_RECONCILIATION.md](docs/WSFE_RECONCILIATION.md).

## Uso directo de dcArca.Core

El Core puede integrarse sin MCP dentro de cualquier aplicación .NET 10.

~~~csharp
using dcArca.Core;
using dcArca.Core.Models;
using dcArca.Core.Services;

var config = dcConfigurationHelper.LoadFromJson("appsettings.json");
using var client = new dcWsfeClient(config);

var factura = new dcFacturaRequest
{
    TipoComprobante = dcTipoComprobante.FacturaA,
    NumeroComprobante = 1,
    Concepto = dcConcepto.Productos,
    CuitReceptor = 20123456786,
    TipoDocReceptor = (int)dcTipoDocumento.CUIT,
    CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
    ImporteNeto = 1000.00m,
    ImporteIva = 210.00m,
    ImporteTotal = 1210.00m,
    AlicuotaIva = dcAlicuotaIva.Veintiuno,
    FechaComprobante = DateTime.Today.ToString("yyyyMMdd")
};

var resultado = await client.FECAESolicitarAsync(factura);

if (resultado.Success)
{
    Console.WriteLine($"CAE: {resultado.Cae}");
}
else
{
    Console.WriteLine($"{resultado.Codigo}: {resultado.Mensaje}");
}
~~~

En integración directa con **dcArca.Core**, el caller es responsable de coordinar correctamente la numeración. Para consumidores remotos generales se recomienda utilizar los tools de emisión server-side de **dcArca.McpServer**.

### API principal de Core

**dcWsfeClient**

- FECAESolicitarAsync(dcFacturaRequest): solicita CAE.
- FECompUltimoAutorizadoAsync(dcTipoComprobante): consulta el último número autorizado.
- FECompConsultarAsync(long numeroComprobante, dcTipoComprobante tipo): recupera un comprobante existente.
- GetCondicionesIVAReceptorAsync(int docTipo, long docNro, dcTipoComprobante tipo): consulta condiciones de IVA válidas para el receptor.

**dcPadronClient**

- GetPersonaAsync(long cuit): consulta datos registrales del padrón.

El modelo dcFacturaResponse expone CAE, importes, fechas, moneda/cotización, IVA detallado, tributos, observaciones, errores y EmissionOutcome.

### Múltiples alícuotas y modelo fiscal avanzado

Cuando se utiliza detalle de IVA explícito, debe dejarse AlicuotaIva en null y completar la colección Iva.

El request también admite:

- ImporteNoGravado
- ImporteExento
- Tributos
- MonedaId
- MonedaCotizacion

Si ImporteIva es mayor que cero, la alícuota o el detalle de IVA deben informarse explícitamente. ARCA-MCP **no infiere automáticamente un 21%**.

## Notas de Crédito y Débito

Las notas pueden asociarse a un comprobante original:

~~~csharp
var nota = new dcFacturaRequest
{
    TipoComprobante = dcTipoComprobante.NotaCreditoB,

    CbteAsociadoTipo = (int)dcTipoComprobante.FacturaB,
    CbteAsociadoPtoVta = 1,
    CbteAsociadoNro = 123

    // ... resto de los campos fiscales ...
};
~~~

También puede utilizarse un período asociado mediante PeriodoAsocDesde y PeriodoAsocHasta cuando corresponda.

## Facturas de servicios

Para concepto Servicios o ProductosYServicios deben informarse las fechas fiscales correspondientes:

~~~csharp
var factura = new dcFacturaRequest
{
    Concepto = dcConcepto.Servicios,
    FechaServicioDesde = "20261001",
    FechaServicioHasta = "20261031",
    FechaVencimiento = "20261110"

    // ... resto de los campos fiscales ...
};
~~~

## Consulta de padrón

~~~csharp
var config = dcConfigurationHelper.LoadFromJson("appsettings.json");
using var padron = new dcPadronClient(config);

var persona = await padron.GetPersonaAsync(20123456786);

if (persona.Success && persona.Existe)
{
    Console.WriteLine(persona.EstadoClave);
}
~~~

## CLI local

**dcArca.Cli** funciona en Linux, Windows y macOS y no abre sockets ni expone endpoints HTTP.

~~~bash
cp dcArca.Cli/appsettings.example.json dcArca.Cli/appsettings.json

dotnet run --project dcArca.Cli -- padron 20123456786
dotnet run --project dcArca.Cli -- ultimo-autorizado 6
dotnet run --project dcArca.Cli -- consultar 123 6
dotnet run --project dcArca.Cli -- facturar factura.json
~~~

Comandos disponibles:

- facturar [archivo.json]
- ultimo-autorizado <tipoComprobante>
- consultar <numero> <tipoComprobante>
- padron <cuit>
- condiciones-iva <docTipo> <docNro> <tipoComprobante>

## Aplicación WinForms de prueba

**dcArca.TestApp** sigue disponible como herramienta de referencia y diagnóstico en Windows.

Incluye, entre otras funciones:

- emisión de comprobantes;
- consulta de comprobantes mediante FECompConsultar;
- consulta de CUIT/padrón;
- visualización de códigos AFIP con descripciones amigables.

~~~bash
dotnet run --project dcArca.TestApp/dcArca.TestApp.csproj
~~~

La TestApp ya no representa la arquitectura principal del repositorio: para integración reusable utilizar **dcArca.Core**, y para acceso remoto utilizar **dcArca.McpServer**.

## Certificados ARCA

ARCA-MCP trabaja con certificados digitales configurados por el host.

Reglas importantes:

- homologación y producción usan certificados y endpoints diferentes;
- no mezclar un certificado de homologación con servicios productivos;
- el certificado debe estar autorizado para cada Web Service utilizado;
- wsfe es necesario para facturación;
- ws_sr_constancia_inscripcion es necesario para padrón;
- el PFX, su password, claves privadas y tokens nunca deben almacenarse en el repositorio.

Para generar un PFX a partir de una clave privada y un certificado ya emitido:

~~~bash
openssl pkcs12 -export -out certificado.pfx -inkey certificado.key -in certificado.crt
~~~

Los PFX sin contraseña también son soportados por la librería, pero no se recomiendan para producción. Si se utilizan, deben protegerse mediante permisos de filesystem o un almacén seguro del host.

Los procedimientos del portal de ARCA para emitir y relacionar certificados pueden cambiar. Para altas nuevas conviene seguir la documentación oficial vigente del organismo y conservar en este repositorio solamente la configuración técnica estable que necesita la aplicación.

## Cache WSAA

Por defecto los Ticket de Acceso se almacenan en memoria mediante **MemoryWsaaTokenStore.Shared**.

Si un host necesita persistencia entre procesos del mismo equipo puede optar explícitamente por **FileSystemWsaaTokenStore**.

También puede implementarse **IWsaaTokenStore** para integrar un secret manager, base de datos o mecanismo distribuido.

Los token/sign de WSAA son credenciales sensibles y no deben escribirse en logs ni persistirse de forma insegura.

Más detalle: [docs/WSAA_CACHE.md](docs/WSAA_CACHE.md).

## Docker

El repositorio incluye Dockerfile y un docker-compose deliberadamente genérico.

~~~bash
docker build -f dcArca.McpServer/Dockerfile -t dcarca-mcpserver .
~~~

El compose de la raíz monta la configuración y el certificado desde el host. No incluye dominios, TLS, reverse proxy, redes privadas ni secretos productivos.

Ese límite es intencional: la infraestructura privada pertenece al entorno que despliega ARCA-MCP, no al repositorio público.

## Tests y CI

La CI ejecuta sobre Linux:

- build de Core tests;
- build de MCP tests;
- build de CLI;
- auditoría de paquetes vulnerables;
- tests de Core;
- tests de MCP;
- control para impedir que certificados o claves privadas queden versionados.

Ejecución local:

~~~bash
dotnet test dcArca.Core.Tests/dcArca.Core.Tests.csproj -c Release
dotnet test dcArca.McpServer.Tests/dcArca.McpServer.Tests.csproj -c Release
~~~

La solución también contiene la TestApp WinForms, que requiere Windows para su ejecución normal.

## Seguridad

- Nunca commitear PFX, P12, PEM, KEY, passwords, JWT, client secrets ni Ticket de Acceso.
- Utilizar TLS en producción.
- Mantener la metadata OIDC sobre HTTPS fuera de Development.
- Aplicar mínimo privilegio en los scopes.
- No entregar client secrets a un frontend, un LLM o un usuario final.
- Preferir la numeración server-side del MCP frente a coordinación manual desde consumidores remotos.
- Utilizar mecanismos de secretos y permisos del sistema operativo o plataforma de despliegue.
- El repositorio público no debe contener infraestructura ni credenciales privadas.

Ver [docs/REPOSITORY_SECURITY.md](docs/REPOSITORY_SECURITY.md).

## Troubleshooting

### Certificado no encontrado

- Verificá CertificatePath.
- Confirmá que el archivo PFX esté montado o disponible para el proceso.
- En contenedores, revisá que la ruta interna coincida con la configuración.

### Certificado o servicio no autorizado

- Confirmá que el certificado corresponda al ambiente.
- Verificá la autorización del servicio wsfe.
- Para padrón, verificá ws_sr_constancia_inscripcion.

### Punto de venta no habilitado

- Confirmá que PuntoVenta exista y esté habilitado para el servicio de facturación electrónica utilizado.

### IVA informado sin alícuota

Desde la versión 2.0, si ImporteIva es mayor que cero debe informarse AlicuotaIva o el detalle Iva. Ya no se supone automáticamente una alícuota del 21%.

### Resultado incierto

Si una emisión devuelve EmissionOutcome = Uncertain, no repitas automáticamente el mismo FECAESolicitar. Primero debe resolverse el estado fiscal del comprobante.

## Documentación

- [Servidor MCP](docs/MCP_SERVER.md)
- [Autorización y scopes](docs/MCP_AUTHORIZATION.md)
- [Numeración y concurrencia](docs/MCP_NUMBERING.md)
- [Configuración y health](docs/MCP_CONFIGURATION.md)
- [Reconciliación WSFE](docs/WSFE_RECONCILIATION.md)
- [Cache WSAA](docs/WSAA_CACHE.md)
- [Superficies soportadas](docs/SERVICE_SURFACES.md)
- [Seguridad del repositorio](docs/REPOSITORY_SECURITY.md)
- [Changelog](CHANGELOG.md)
- [Notas de v2.0.0](docs/releases/v2.0.0.md)

## Licencia

Apache License 2.0.

Copyright (c) 2025 Diego Cofré Sistemas  
https://www.diegocofre.com.ar
