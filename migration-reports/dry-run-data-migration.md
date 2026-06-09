# Reporte migracion Azure SQL a PostgreSQL

- Inicio UTC: 2026-04-27T19:45:51.6475655+00:00
- Fin UTC: 
- Origen: sql-espencasa.database.windows.net / espencasa-intranet
- Destino: pg-espencasa-intranet / espencasa_intranet
- Dry run: True
- Truncate destino: False

## Tablas migradas

| Tabla | Origen | Insertados | Destino | Diferencia | Error |
| --- | ---: | ---: | ---: | ---: | --- |

## Tablas grandes

No se detectaron tablas sobre el umbral configurado.

## Diferencias y pendientes

### Tablas origen no representadas en PostgreSQL
- Ninguno

### Tablas PostgreSQL no encontradas en origen
- Ninguno

### Errores
- El destino ya tiene datos. Ejecute nuevamente con --truncate-destination si desea reemplazar el seed/datos de prueba en PostgreSQL.

