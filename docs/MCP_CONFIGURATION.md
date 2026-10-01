# Configuración y operabilidad de dcArca.McpServer

## Health

El servidor expone dos señales livianas que no llaman a ARCA:

- `GET /health/live`: confirma que el proceso HTTP está levantado.
- `GET /health/ready`: confirma que el proceso completó el arranque con la configuración esencial válida.

Las respuestas sólo contienen `status`; no incluyen CUIT, certificado, rutas, token/sign ni passwords.

## Configuración requerida

Se requiere:

- `Jwt:Authority` / `Jwt__Authority`;
- `Jwt:Audience` / `Jwt__Audience`;
- `dcArcaConfig:Cuit`;
- `dcArcaConfig:CertificatePath`;
- `dcArcaConfig:CertificatePassword` cuando el PFX lo requiera;
- `dcArcaConfig:WsaaUrl`;
- `dcArcaConfig:WsfeUrl`;
- `dcArcaConfig:PadronUrl`;
- `dcArcaConfig:PuntoVenta`.

El certificado y sus secretos deben provenir del host/secret manager y no del repositorio.

## Development vs Production

En `Development` se permite un Authorization Server HTTP para entornos locales, por ejemplo Keycloak en Docker.

Fuera de `Development`, `Jwt:Authority` debe ser una URI HTTPS. El middleware OIDC mantiene `RequireHttpsMetadata=true`.

Las URLs y credenciales del entorno local son ejemplos de desarrollo y no deben reutilizarse en producción.
