# Base de datos local SQLite

La aplicación crea automáticamente `Data/ecobox.db` al iniciarse. La base es local al computador y no requiere internet ni cuenta de Supabase.

La tabla `RegistroResiduo` guarda una fila por fecha y caneca con la cantidad acumulada. Las dos canecas se representan con `APROVECHABLE` y `NO_APROVECHABLE`. No se insertan registros de demostración.

El esquema se crea desde Entity Framework Core con `EnsureCreatedAsync` en el primer arranque. La ruta del archivo está en `.gitignore` para mantener la base local fuera de Git.
