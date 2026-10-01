# Numeración y concurrencia de emisión MCP

El flujo recomendado es el tool `emitir_comprobante`. El caller no elige el número fiscal.

Dentro del servidor, dcARCA serializa por `(CUIT emisor, punto de venta, tipo de comprobante)` y ejecuta:

1. `FECompUltimoAutorizado`;
2. calcula `último + 1`;
3. `FECAESolicitar`;
4. libera el lock incluso ante excepción.

Secuencias de distinto CUIT, punto de venta o tipo de comprobante no se bloquean entre sí.

## Operación avanzada

`solicitar_cae` se conserva como operación de bajo nivel para integraciones que ya controlan la numeración externamente. El caller es responsable de evitar carreras y duplicados.

## Límite multi-instancia

El lock MCP es in-process. Una única instancia del servidor queda protegida, pero dos réplicas o sistemas externos que compartan el mismo punto de venta no se coordinan mediante este lock.

Para despliegues con múltiples instancias se debe usar coordinación distribuida o, preferentemente, dedicar un punto de venta a cada flujo que deba numerar de forma independiente.
