# Autorización MCP por scopes

dcArca.McpServer trata los scopes de lectura y escritura como capacidades independientes.

- `arca:consultar`: habilita `consultar_ultimo_comprobante`, `consultar_comprobante`, `consultar_condiciones_iva` y `consultar_padron`.
- `arca:facturar`: habilita las operaciones de emisión.

`arca:facturar` no implica `arca:consultar`. Un cliente que necesite ambas capacidades debe solicitar ambos scopes, por ejemplo:

```
scope=arca:consultar arca:facturar
```

El servidor acepta la convención OIDC habitual donde el claim JWT `scope` contiene valores separados por espacios. El parsing se centraliza en el servidor y no depende de un IdP específico.
