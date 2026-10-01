# dcARCA - Facturación Electrónica Argentina

**dcARCA** es un componente .NET 10 para implementar facturación electrónica Argentina utilizando el web service **WSFEv1 de AFIP (ARCA)** y consultar el padrón oficial **ws_sr_constancia_inscripcion** (antes ws_sr_padron_a5) para validar CUIT. 
Esta librería NO ES un producto oficial de ARCA ni del Gobierno Argentino,sino una implementación independiente desarrollada por **Diego Cofré Sistemas**. [www.diegocofre.com.ar](http://www.diegocofre.com.ar)

## Licencia
Este proyecto está licenciado bajo **Apache License 2.0**.  
Copyright (c) 2025 Diego Cofré Sistemas www.diegocofre.com.ar

## MCP Server

Para instalar, configurar y consumir `dcArca.McpServer` con OAuth/OIDC, scopes, numeración segura, reconciliación y ejemplos de homologación, ver **[Documentación completa del MCP](docs/MCP_SERVER.md)**.

## � Quick Start (5 minutos)

### 1. Requisitos previos
- .NET 10 SDK ([Descargar](https://dotnet.microsoft.com/download/dotnet/10.0))
- Certificado digital ARCA (.pfx)

### 2. Clonar y compilar
```powershell
cd c:\ruta\a\tu\proyecto
dotnet build
```

### 3. Configurar
Copia `appsettings.example.json` a `appsettings.json` y edita:
```json
{
  "dcArcaConfig": {
    "Cuit": "TU_CUIT",
    "CertificatePath": "C:\\Ruta\\Certificado.pfx",
    "CertificatePassword": "TU_PASSWORD" /* Puede dejarse vacío si el .pfx no tiene contraseña */,
    "WsaaUrl": "https://wsaahomo.afip.gov.ar/ws/services/LoginCms",
    "WsfeUrl": "https://wswhomo.afip.gov.ar/wsfev1/service.asmx",
    "PadronUrl": "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5",
    "PuntoVenta": 1
  }
}
```

### 4. Ejecutar
```powershell
dotnet run --project dcArca.TestApp\dcArca.TestApp.csproj
```

### 5. Probar
- Selecciona "Facturación Electrónica" en el menú
- Ingresa CUIT receptor: `20123456789`
- Importe neto: `1000.00`
- Clic "Calcular IVA y Total" → "Autorizar Factura"
- Abre "Consultar Comprobante" y verificá cualquier CAE autorizado con `FECompConsultar` (nuevo en esta versión)

## 🆕 Novedades de esta versión
- Nuevo formulario **Consultar Comprobante (FECompConsultar)** dentro de la app WinForms para leer cualquier comprobante autorizado y mostrar todo el payload que devuelve AFIP (CAE, importes, tributos, fechas, IVA detallado, observaciones y eventos).
- Traducción automática de los códigos AFIP más comunes (concepto, tipo de comprobante, tipo de documento, condición IVA y alícuotas) mediante `dcAfipEnumExtensions`, disponible tanto para la UI como para logs/servicios.
- El modelo `dcFacturaResponse` ahora expone propiedades enriquecidas (moneda, cotización, períodos de servicio/vencimiento, IVA desglosado y condición IVA del receptor) que podés reutilizar en tus propios flujos.

## 📦 Instalación

### Requisitos del Sistema
- **.NET 10 SDK** o superior
- **Windows 10/11** (para WinForms)
- **Certificado digital ARCA** válido

### Compilación
```powershell
# Clonar repositorio
git clone https://github.com/tu-repo/dcArca.git
cd dcArca

# Compilar
dotnet build

# Ejecutar aplicación de prueba
dotnet run --project dcArca.TestApp
```

## 🔐 Configuración del Certificado ARCA - Homologación

Esta sección describe el proceso para obtener un certificado digital en el ambiente de **Testing/Homologación** de ARCA. Para producción, el procedimiento es diferente (ver documentación oficial de AFIP).

### Requisitos previos
- OpenSSL instalado ([Descargar](https://slproweb.com/products/Win32OpenSSL.html) para Windows o usar `choco install openssl`)
- Acceso al portal WSASS con Clave Fiscal
- CUIT activo en ARCA

### Paso 1: Generar clave privada y CSR

```powershell
# Generar clave privada (2048 bits)
openssl genrsa -out certificado.key 2048

# Crear CSR (Certificate Signing Request)
# IMPORTANTE: Reemplazá TU_CUIT por tu CUIT sin guiones
openssl req -new -key certificado.key -out certificado.csr -subj "/C=AR/O=Sistema/OU=Servicios/CN=TU_CUIT"
```

**Nota**: El CUIT debe ir en el campo `CN` (Common Name) del certificado.

### Paso 2: Crear certificado en WSASS (Homologación)

1. Ingresa a **[https://auth.afip.gob.ar/](https://auth.afip.gob.ar/)** con tu CUIT y Clave Fiscal
2. Andá a la sección **"WSASS Autoservicio de Acceso a WebServices (TESTING/HOMOLOGACIÓN)"**
3. En el menú lateral, hace clic en **"Nuevo Certificado"**
4. Completa el formulario:
   - **Nombre simbólico del DN** (alias): elegí un nombre identificable, ej: `arcahomo01`
   - **CUIT del contribuyente**: tu CUIT (se completa automáticamente)
   - **Solicitud de certificado en formato PKCS#10**: abrí `certificado.csr` con un editor de texto, copiá **todo el contenido** (incluyendo `-----BEGIN CERTIFICATE REQUEST-----` y `-----END CERTIFICATE REQUEST-----`) y pegalo en el campo
5. Hace clic en **"Crear DN y obtener certificado"**

### Paso 3: Obtener el certificado firmado


1. Copiá **todo el contenido** del certificado que aparece en pantalla, incluyendo:
```
-----BEGIN CERTIFICATE-----
MIIDbTC...
-----END CERTIFICATE-----
```

2. Guardalo en un archivo de texto llamado `certificado.crt`

### Paso 4: Convertir a formato .pfx

```powershell
# Con contraseña (recomendado para producción)
openssl pkcs12 -export -out certificado.pfx -inkey certificado.key -in certificado.crt -password pass:TU_PASSWORD

# Sin contraseña (útil para testing, menos seguro)
openssl pkcs12 -export -nodes -out certificado.pfx -inkey certificado.key -in certificado.crt
```

**Importante**: Guardá `certificado.pfx` en un lugar seguro y **nunca lo subas al repositorio**.

### Paso 5: Autorizar servicios para el certificado

1. En WSASS, andá a **"Servicios"** en el menú lateral
2. Buscá y hace clic en el servicio que necesitás (ej: `wsfe` para facturación, `ws_sr_constancia_inscripcion` para padrón)
3. En la pantalla de información del servicio, hace clic en **"Crear autorización para acceder a este servicio"**
4. Seleccioná:
   - **Nombre simbólico del DN a autorizar**: elegí el alias que creaste antes (ej: `arcahomo01`)
   - **CUIT del DN a autorizar**: tu CUIT
   - **CUIT representado**: tu CUIT
   - **Servicio**: el servicio correspondiente
5. Hace clic en **"Crear autorización de acceso"**

**Servicios necesarios para dcARCA**:
- `wsfe` (Facturación Electrónica - obligatorio)
- `ws_sr_constancia_inscripcion` (Consulta de Padrón - opcional, pero recomendado)


### Paso 6: Verificar autorizaciones

1. En el menú lateral, andá a **"Autorizaciones"**
2. Verificá que aparezcan las autorizaciones creadas para tu alias
3. Si todo está OK, el certificado ya está listo para usar

### Resumen de archivos generados

- `certificado.key` → Clave privada (mantener segura, nunca compartir)
- `certificado.csr` → Solicitud de certificado (solo se usa una vez)
- `certificado.crt` → Certificado público firmado por ARCA
- `certificado.pfx` → Certificado + clave privada en formato Windows/NET (el que usás en `appsettings.json`)

## ⚙️ Configuración

Edita `appsettings.json`:

```json
{
  "dcArcaConfig": {
    "Cuit": "20123456789",
    "CertificatePath": "C:\\Certificados\\certificado.pfx",
    "CertificatePassword": "password123",
    "WsaaUrl": "https://wsaahomo.afip.gov.ar/ws/services/LoginCms",
    "WsfeUrl": "https://wswhomo.afip.gov.ar/wsfev1/service.asmx",
    "PadronUrl": "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5",
    "PuntoVenta": 1
  }
}
```

## 🔐 Gestión del Certificado en Producción

El proceso de producción difiere del ambiente de homologación. En lugar de WSASS, debés utilizar el **Administrador de Certificados Digitales** de ARCA/AFIP.

### Paso 1: Generar CSR (igual que homologación)

```powershell
# Generar clave privada
openssl genrsa -out certificado_prod.key 2048

# Crear CSR con tu CUIT en el campo CN
openssl req -new -key certificado_prod.key -out certificado_prod.csr -subj "/C=AR/O=Sistema/OU=Servicios/CN=TU_CUIT"
```

### Paso 2: Solicitar certificado en producción

1. Ingresa al **Administrador de Certificados Digitales** desde [https://www.arca.gob.ar](https://www.arca.gob.ar) con tu CUIT y Clave Fiscal
2. Subí el archivo `certificado_prod.csr` en el formulario de solicitud
3. El sistema generará y **mostrará el certificado X.509 en pantalla** (no hay descarga automática)
4. Copiá todo el contenido, incluyendo:
```
-----BEGIN CERTIFICATE-----
...
-----END CERTIFICATE-----
```
5. Guardalo como `certificado_prod.crt`

### Paso 3: Convertir a .pfx

```powershell
# Con contraseña (recomendado)
openssl pkcs12 -export -out certificado_prod.pfx -inkey certificado_prod.key -in certificado_prod.crt -password pass:TU_PASSWORD_SEGURO
```

### Paso 4: Autorizar servicios en producción

**Importante**: En producción NO se usa WSASS. Debés utilizar el **Administrador de Relaciones de Clave Fiscal**.

1. Ingresa al **Administrador de Relaciones de Clave Fiscal** desde [https://www.arca.gob.ar](https://www.arca.gob.ar) con tu CUIT
2. Creá una **nueva relación** para autorizar servicios web
3. Selecciona y habilita los servicios necesarios:
   - `wsfe` (Factura Electrónica)
   - `ws_sr_constancia_inscripcion` (Consulta de Padrón / Constancia de Inscripción)
   - **Nota**: Si no lo encontrás, buscá por `ws_sr_padron_a5` (nombre antiguo)
4. Confirmá la autorización del certificado para cada servicio

**Nota**: Asegurate de asociar correctamente el alias/computador fiscal a los servicios habilitados para evitar errores de "certificado no autorizado".

### Paso 5: Actualizar configuración

Modificá `appsettings.json` con las URLs de producción y la ruta al nuevo certificado:

```json
{
  "dcArcaConfig": {
    "Cuit": "TU_CUIT",
    "CertificatePath": "C:\\Certificados\\certificado_prod.pfx",
    "CertificatePassword": "TU_PASSWORD_SEGURO",
    "WsaaUrl": "https://wsaa.afip.gov.ar/ws/services/LoginCms",
    "WsfeUrl": "https://servicios1.afip.gov.ar/wsfev1/service.asmx",
    "PadronUrl": "https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA5",
    "PuntoVenta": TU_PUNTO_VENTA_PRODUCTIVO
  }
}
```

**⚠️ Recordatorios importantes**:
- **No mezcles certificados**: el certificado de testing NO funciona con endpoints de producción
- Cada ambiente (homologación/producción) usa su propio certificado y autorización
- Los endpoints de WSAA y servicios de negocio (Factura, Padrón) son diferentes según el ambiente

**URLs de Producción**:
- WSAA: `https://wsaa.afip.gov.ar/ws/services/LoginCms`
- WSFEv1: `https://servicios1.afip.gov.ar/wsfev1/service.asmx`
- Padrón: `https://aws.afip.gov.ar/sr-padron/webservices/personaServiceA5`

## 🧪 Ejemplos de Uso

### Factura Básica
```csharp
using dcArca.Core.Models;
using dcArca.Core.Services;

var config = dcConfigurationHelper.LoadFromJson("appsettings.json");
var client = new dcWsfeClient(config);

var factura = new dcFacturaRequest
{
    CuitReceptor = 20123456789,
    TipoDocReceptor = 80,
    Concepto = dcConcepto.Productos,
    TipoComprobante = dcTipoComprobante.FacturaB,
    NumeroComprobante = 1,
    ImporteNeto = 1000.00m,
    ImporteIva = 210.00m,
    ImporteTotal = 1210.00m,
    FechaComprobante = "20251119"
};

var resultado = await client.FECAESolicitarAsync(factura);
if (resultado.Success)
{
    Console.WriteLine($"CAE: {resultado.Cae}");
}
```

### Nota de Crédito/Débito
Para notas de crédito o débito, debes especificar el comprobante asociado (CbteAsociado*) para cumplir con la regla 10197 de ARCA:
```csharp
var nota = new dcFacturaRequest
{
    // ... campos básicos ...
    TipoComprobante = dcTipoComprobante.NotaCreditoB, // 8
    // Comprobante asociado (factura original)
    CbteAsociadoTipo = 6, // Tipo de la factura base (FacturaB)
    CbteAsociadoPtoVta = 1, // Punto de venta de la factura base
    CbteAsociadoNro = 123, // Número de la factura base
    // Opcional: CbteAsociadoFecha = "20251119"
};
```

### Factura de Servicios
```csharp
var factura = new dcFacturaRequest
{
    // ... campos básicos ...
    Concepto = dcConcepto.Servicios,
    FechaServicioDesde = "20251101",
    FechaServicioHasta = "20251130",
    FechaVencimiento = "20251215"
};
```

### Validar CUIT
```csharp
var padron = new dcPadronClient(config);
var persona = await padron.GetPersonaAsync(20123456789);
if (persona.Success && persona.Existe)
{
    Console.WriteLine($"Estado: {persona.EstadoClave}");
}
```

### Manejo de Errores
```csharp
var resultado = await client.FECAESolicitarAsync(factura);
if (!resultado.Success)
{
    Console.WriteLine($"Error: {resultado.Mensaje}");
    foreach (var err in resultado.Errores) Console.WriteLine($"- {err}");
}
```

## 📚 API Reference

### Consultar comprobante (FECompConsultar)

La WinForms Test App incluye un formulario dedicado para consultar cualquier comprobante emitido desde tu CUIT:

1. Ejecuta `dcArca.TestApp` y abre **"Consultar Comprobante"** desde el menú principal.
2. Elegí el tipo de comprobante desde la lista (los textos son generados automáticamente con las nuevas extensiones de enums).
3. Ingresá el número a consultar y presioná **Consultar**. La pantalla formatea los datos clave y muestra el detalle completo (incluye IVA por alícuota, fechas de servicio, moneda, observaciones y errores).
4. Toda la información proviene del nuevo método `FECompConsultarAsync`, por lo que es la misma estructura que podés consumir en tus propias apps.

### dcWsfeClient
- `FECAESolicitarAsync(dcFacturaRequest)`: Solicita CAE
- `FECompUltimoAutorizadoAsync(dcTipoComprobante)`: Último número
- `FECompConsultarAsync(long numeroComprobante, dcTipoComprobante tipo)`: Recupera un comprobante existente y devuelve un `dcFacturaResponse` enriquecido.
- `GetCondicionesIVAReceptorAsync(int docTipo, long docNro, dcTipoComprobante tipo)`: Wrapper para `FEParamGetCondicionIvaReceptor` (útil para validar RG 5616).

### dcPadronClient
- `GetPersonaAsync(long cuit)`: Consulta padrón A5

### dcFacturaRequest
- Propiedades: CuitReceptor, ImporteNeto, ImporteIva, etc.
- Para notas: CbteAsociadoTipo, CbteAsociadoPtoVta, CbteAsociadoNro (obligatorios)
- `ValidarCuit()`: Valida formato CUIT
- `EsNota()`: Indica si es nota de crédito/débito
- `CumpleReglaNotas10197()`: Valida que notas tengan comprobante asociado

## 🖥️ CLI local (Linux/Windows/macOS)

`dcArca.Cli` conserva las operaciones de consola para administración y pruebas locales. No abre puertos ni expone una API HTTP.

```bash
cp dcArca.Cli/appsettings.example.json dcArca.Cli/appsettings.json
dotnet run --project dcArca.Cli -- padron 20123456789
dotnet run --project dcArca.Cli -- ultimo-autorizado 6
dotnet run --project dcArca.Cli -- consultar 123 6
dotnet run --project dcArca.Cli -- facturar factura.json
```

Comandos disponibles: `facturar [archivo.json]`, `ultimo-autorizado <tipoComprobante>`, `consultar <numero> <tipoComprobante>`, `padron <cuit>` y `condiciones-iva <docTipo> <docNro> <tipoComprobante>`.

Para acceso remoto autenticado use `dcArca.McpServer`. El proyecto público no incluye una API REST de facturación sin autenticación.

## 🐛 Troubleshooting

### Certificado no encontrado
- Verifica ruta en `appsettings.json` (usa `\\`)
- Asegura que el archivo .pfx exista

### Token inválido
- Autoriza el certificado para `wsfe` en ARCA
- Para padrón: autoriza para `ws_sr_constancia_inscripcion` (o `ws_sr_padron_a5` si aparece con el nombre antiguo en tu panel)

### CUIT no autorizado
- Verifica que tu CUIT tenga permisos en ARCA

### Punto de venta no habilitado
- Configura el punto de venta en ARCA

### Errores comunes ARCA
- 10016: CUIT inválido
- 10048: Comprobante duplicado
- 10197: Notas de crédito/débito requieren comprobante asociado (CbteAsoc)
- 1217: Certificado no autorizado

## 🔒 Seguridad

- Nunca subas .pfx al repositorio
- Agrega `appsettings.json` a `.gitignore`
- Usa variables de entorno o Key Vault en producción
- Rota certificados antes del vencimiento

Nota sobre .pfx sin contraseña:
- La librería soporta `.pfx` sin contraseña. Si tu archivo `.pfx` no está protegido por password puedes dejar `CertificatePassword` vacío (`""`) o eliminar la clave del `appsettings.json`. 
- Advertencia: los `.pfx` sin contraseña son menos seguros; evita su uso en producción y protege el archivo con permisos de sistema o almacén seguro (Key Vault, etc.).

## 📖 Referencias

- [ARCA WSFEv1](https://www.afip.gob.ar/ws/WSFE/documentacion.asp)
- [Padrón - Constancia de Inscripción](https://www.afip.gob.ar/ws/documentacion/ws-padron-a5.asp) (servicio `ws_sr_constancia_inscripcion`)
- [WSAA](https://www.afip.gob.ar/ws/WSAA/wsaa.html)

**diego cofré sistemas** http://www.diegocofre.com.ar - Soluciones en .NET
