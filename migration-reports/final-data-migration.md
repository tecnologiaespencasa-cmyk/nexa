# Reporte migracion Azure SQL a PostgreSQL

- Inicio UTC: 2026-04-27T19:49:08.1812260+00:00
- Fin UTC: 2026-04-27T19:49:31.1147162+00:00
- Origen: sql-espencasa.database.windows.net / espencasa-intranet
- Destino: pg-espencasa-intranet / espencasa_intranet
- Dry run: False
- Truncate destino: True

## Tablas migradas

| Tabla | Origen | Insertados | Destino | Diferencia | Error |
| --- | ---: | ---: | ---: | ---: | --- |
| Permissions | 7 | 7 | 7 | 0 |  |
| Roles | 3 | 3 | 3 | 0 |  |
| Users | 1 | 1 | 1 | 0 |  |
| RolePermissions | 9 | 9 | 9 | 0 |  |
| UserRoles | 1 | 1 | 1 | 0 |  |
| UserPermissions | 5 | 5 | 5 | 0 |  |
| NursingAssistants | 5 | 5 | 5 | 0 |  |
| OpsAssistants | 10 | 10 | 10 | 0 |  |
| censo | 2 | 2 | 2 | 0 |  |
| AuditLogs | 213 | 213 | 213 | 0 |  |

## Tablas grandes

No se detectaron tablas sobre el umbral configurado.

## Diferencias y pendientes

### Tablas origen no representadas en PostgreSQL
- Ninguno

### Tablas PostgreSQL no encontradas en origen
- Ninguno

### Errores
- Ninguno

