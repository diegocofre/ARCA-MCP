# Reconciliación de emisiones WSFE

Una caída de transporte después de enviar `FECAESolicitar` no demuestra que ARCA haya descartado la solicitud. El comprobante puede haber sido autorizado aunque la respuesta no haya llegado al cliente.

dcARCA distingue estos estados mediante `dcEmissionOutcome`:

- `Authorized`: respuesta normal autorizada;
- `FiscalRejected`: ARCA respondió y rechazó fiscalmente;
- `RecoveredSuccess`: hubo error de transporte, pero `FECompConsultar` confirmó el comprobante;
- `Uncertain`: hubo error de transporte y la reconciliación no pudo confirmar el estado.

Ante `HttpRequestException` o timeout de transporte durante emisión, dcARCA consulta el mismo tipo/número antes de repetir nada. Si la consulta confirma el comprobante, devuelve el resultado recuperado. Si no puede confirmarlo, no realiza un segundo `FECAESolicitar` automático: el caller recibe un estado incierto y debe resolverlo de forma explícita.

Los rechazos fiscales normales y los errores de validación locales no se tratan como resultados inciertos.
