# Cache WSAA y concurrencia

El cache filesystem de WSAA se coordina por combinación CUIT + servicio.

## Concurrencia en un host

dcARCA usa dos niveles de sincronización:

1. un `SemaphoreSlim` por clave para evitar renovaciones duplicadas dentro del mismo proceso;
2. un lock de archivo exclusivo por cache para coordinar procesos distintos que comparten el mismo filesystem.

Después de adquirir ambos locks, el servicio relee el cache antes de solicitar un TA nuevo. Si otro proceso ya renovó, reutiliza ese token.

La escritura se realiza en un archivo temporal del mismo directorio y luego se reemplaza el cache mediante rename/move, evitando publicar JSON parcialmente escrito.

## Límite multi-host

Este mecanismo no es un lock distribuido. Sólo coordina procesos que observan el mismo filesystem y cuyo sistema operativo respeta el lock exclusivo del archivo.

Si varias máquinas o réplicas independientes comparten un mismo CUIT/servicio, la coordinación debe quedar a cargo del host mediante un store/lock distribuido o, preferentemente, una estrategia que evite compartir la misma secuencia/credencial entre hosts.
