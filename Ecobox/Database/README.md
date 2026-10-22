# PostgreSQL / Supabase

La aplicación lee la cadena de conexión desde `ConnectionStrings:EcoBox`. Configúrala con la variable de entorno `ConnectionStrings__EcoBox` en el entorno local o de despliegue. No guardes credenciales reales en este repositorio.

Con una conexión válida, el arranque crea el esquema si todavía no existe y agrega únicamente los dos tipos de residuo y los dos contenedores iniciales. No inserta residuos de demostración.

Los scripts `schema.sql` y `seed.sql` permiten preparar la misma estructura manualmente en PostgreSQL/Supabase.
