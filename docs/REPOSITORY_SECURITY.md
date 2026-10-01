# Seguridad del repositorio público

dcARCA es un proyecto open source y no debe actuar como punto de despliegue hacia infraestructura privada.

## Límites del repositorio

- Los merges a `main` sólo deben compilar y ejecutar tests.
- El repositorio no debe contener workflows de deploy productivo, hosts privados, redes Docker privadas, dominios operativos, certificados, PFX, claves SSH ni credenciales.
- `docker-compose.yml` es únicamente un ejemplo local/genérico. TLS, reverse proxy, redes privadas y secretos pertenecen al host que despliega la aplicación.
- Los certificados ARCA y sus passwords deben permanecer fuera del repositorio y montarse o inyectarse en runtime.

## Configuración administrativa recomendada

En GitHub, configurar un ruleset o protección de `main` que:

- requiera Pull Request para mergear;
- requiera el check de CI verde;
- bloquee push directo;
- bloquee force-push;
- bloquee borrado de la rama;
- aplique las reglas también a administradores cuando sea viable.

Revisar periódicamente colaboradores con permiso `write` o superior. Para contribuciones externas al proyecto público, preferir fork + Pull Request.

## Secretos de despliegue históricos

Si alguna vez existieron secrets de deploy vinculados a este repositorio, deben retirarse del repositorio público. Cualquier clave SSH que se reutilice en infraestructura privada debe rotarse antes de volver a utilizarse.

La rotación es preventiva y no implica evidencia de exposición.
