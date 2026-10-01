# Superficies ejecutables de dcARCA

## dcArca.Core

Librería embebible con WSAA, WSFE y padrón.

## dcArca.Cli

Utilidad local de consola. Ejecuta una operación y termina. No abre sockets ni expone endpoints HTTP.

## dcArca.McpServer

Única superficie remota soportada por el proyecto público. Requiere autenticación JWT/OIDC y autorización por scopes.

## Decisión sobre la antigua dcArca.Service

La API REST no autenticada fue retirada antes de la próxima release pública. Aunque la CLI compartía originalmente el mismo ejecutable, mantener un modo `serve` sin autenticación permitía publicar accidentalmente operaciones fiscales con la identidad configurada en el host.

La funcionalidad de consola se conserva en `dcArca.Cli`; para acceso remoto debe usarse el servidor MCP autenticado.
