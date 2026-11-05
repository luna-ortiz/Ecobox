# Base de datos local SQLite

La aplicación crea automáticamente `Data/ecobox.db` al iniciarse. La base es local al computador y no requiere internet ni cuenta de Supabase.

La tabla `RegistroResiduo` guarda una fila por fecha y caneca con la cantidad acumulada. Las dos canecas se representan con `APROVECHABLE` y `NO_APROVECHABLE`. No se insertan registros de demostración.

El esquema se crea desde Entity Framework Core con `EnsureCreatedAsync` en el primer arranque. La ruta del archivo está en `.gitignore` para mantener la base local fuera de Git.

## Capacidad física

`EstadoCaneca` persiste la ocupación actual (0–50), ciclo activo, última actualización y último vaciado. `CicloCaneca` conserva el inicio/cierre de cada ciclo y `RegistroResiduo.CicloId` vincula cada fila con él. Vaciar cierra el ciclo actual y abre uno nuevo dentro de la misma transacción; los registros anteriores permanecen consultables en Historial.

La inicialización aplica cambios aditivos e idempotentes. Los registros de una base previa, que no tenía identificadores de ciclo, se agrupan en un ciclo cerrado de legado por caneca; no es posible reconstruir vaciados pasados que nunca se guardaron. Al migrar una base antigua sin datos de ocupación, las canecas se inicializan vacías y el operador debe confirmar el estado físico real.

La acción `Dashboard/VaciarCaneca` requiere POST y antiforgery, pero esta aplicación todavía no tiene autenticación ni roles. En instalaciones accesibles desde una red, se debe restringir el acceso al panel hasta integrar una autorización administrativa.
