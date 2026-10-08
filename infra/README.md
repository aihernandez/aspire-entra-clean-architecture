# Infraestructura Azure del template

Bicep compone módulos reutilizables por servicio. `environments/dev.bicepparam`, `staging.bicepparam`
y `prod.bicepparam` configuran instancias independientes; usar un resource group y registros de
aplicación Entra propios por ambiente. Los archivos no crean recursos hasta ejecutar un despliegue.

## Servicios y archivos

| Módulo en `modules/` | Incluido | Función |
|---|---|---|
| `network.bicep` | Sí | VNet, subred Container Apps y subred de private endpoints. |
| `identity.bicep` | Dos instancias | Identidades administradas distintas para API y web. |
| `registry.bicep` | Sí | ACR y AcrPull para ambas identidades. |
| `sql.bicep` | Sí | Azure SQL privado, autenticación solo Entra, auditoría y copias. |
| `key-vault.bicep` | Sí | Bóveda privada RBAC preparada para futuros consumidores de secretos. |
| `monitor.bicep` | Sí | Log Analytics, Application Insights, grupo de acciones y alerta SQL si hay correo. |
| `container-apps.bicep` | Sí | API .NET y Angular en un entorno interno, sondas y escalado. |
| `front-door.bicep` | Sí | Front Door Premium, WAF, Private Link y rutas por servicio. |
| `storage.bicep` | Opcional | Blob privado y acceso de datos para la API si `enableStorage = true`. |

`main.bicep` conecta los módulos y exporta URLs, nombres SQL e identificadores de las identidades.
No se incorporan Redis, Service Bus, AKS, API Management ni un proveedor de correo sin una necesidad
funcional. Policy, Defender for Cloud, presupuestos, continuidad y una segunda región se deben
ajustar a los requisitos de la organización.

## Entornos, escalado y balanceo

| Configuración | dev | staging | prod |
|---|---:|---:|---:|
| `scaleMode` | fixed | auto | auto |
| Réplicas por app | 1 | 1–3 | 2–10 |
| SQL | Basic | S1 | General Purpose 2 vCores |
| Redundancia zonal de apps y SQL | No | No | Sí |
| Log Analytics | 30 días | 30 días | 90 días |
| WAF | Detección | Detección | Prevención |
| Restauración puntual SQL | 7 días | 7 días | 35 días + retención semanal/mensual |
| Purge protection de Key Vault | No | No | Sí |
| Storage opcional | LRS | LRS | GZRS |

Azure Container Apps utiliza reglas HTTP de KEDA. `scaleMode = 'fixed'` fija mínimo y máximo en
`minReplicas`; `auto` añade una regla HTTP por app y permite crecer hasta `maxReplicas`.
`apiHttpConcurrency` y `webHttpConcurrency` controlan la concurrencia objetivo (inicialmente 10).
En automático, `maxReplicas >= minReplicas`. Ajustar capacidad con pruebas de carga y métricas de
latencia, errores y SQL. Front Door dirige solicitudes al origen saludable y el ingress de Container
Apps distribuye el tráfico entre réplicas. El rate limiting de ASP.NET Core sigue siendo por instancia;
no representa una cuota compartida entre réplicas.

Container Apps usa `/alive` para liveness y `/ready` para readiness. Front Door comprueba `/ready`.
Los sondeos son públicos, no aprovisionan usuarios y no consumen cuota. SQL se comprueba con una
conexión aislada y tiempos acotados; una caída no debe reiniciar una API cuyo proceso sigue vivo.

Los CIDR son ejemplos y deben revisarse antes de conectar redes corporativas. SKU, cuotas y zonas
dependen de la región. Front Door Premium y Private Link tienen costo fijo incluso en dev.

## Rutas y WAF

La ruta API usa `patternsToMatch: ['/api/*']` y `originPath: '/'` para sustituir el prefijo coincidente.
No añade una regla URL rewrite redundante. La ruta web usa `/*`. Ambas conservan HTTPS y WAF.

| Solicitud externa | Destino esperado | Comprobación desplegada |
|---|---|---|
| GET `/api/auth-config` | API `/auth-config` | 200 con configuración válida. |
| GET `/api/users/me` | API `/users/me` | 401 sin token; 200 para persona asignada. |
| GET `/api/users?pageNumber=1&pageSize=20` | API `/users`, consulta conservada | 200 Admin; 403 Member. |
| POST `/api/todos` | API `/todos`, método/cuerpo conservados | Alta autenticada, sin redirección a Angular. |
| GET `/profile` | Web `/profile` | Fallback SPA y navegación Angular. |
| Probe `/ready` | API `/ready` directamente | 200 saludable; 503 si SQL no está disponible. |

WAF usa reglas administradas; revisar logs en detección y probar prevención antes de recibir tráfico.
Compilar la plantilla verifica sintaxis y tipos; no demuestra que las rutas, reglas o Private Link funcionen.

## Matriz de privilegios

| Identidad | ACR | SQL | Key Vault | Storage |
|---|---|---|---|---|
| API administrada (`apiIdentityPrincipalId`) | AcrPull | Usuario contenido: CONNECT + SELECT/INSERT/UPDATE/DELETE en `dbo.Users` y `dbo.TodoItems` | Sin roles por defecto | Blob Data Contributor solo si se activa |
| Web administrada (`webIdentityPrincipalId`) | AcrPull | Ninguno | Ninguno | Ninguno |
| Grupo administrador Entra SQL | Según proceso de publicación | Administración de SQL y migraciones; distinto de la API | Según proceso corporativo | Ninguno requerido |
| Ejecutor ARM | Publicación de imágenes según pipeline | Crea recursos ARM, no implica acceso de datos SQL | Crea bóveda; sin necesidad de escribir contraseña SQL | Crea recursos |

La conexión API usa `Authentication=Active Directory Managed Identity` y el **client id** de su
identidad. No contiene contraseña. El usuario SQL se crea con el **object/principal id**, que es
un identificador diferente. La web no recibe la conexión SQL ni acceso a secretos. La bóveda queda
preparada; conceder acceso únicamente cuando exista un consumidor real. El módulo acepta un
principal opcional para ese caso.

La cuenta API no recibe `db_owner`, `db_ddladmin`, administración del servidor, ALTER ni acceso
al historial de migraciones. Añadir permisos sobre tablas nuevas de forma explícita al ampliar el dominio.

## Preparación y despliegue

Requisitos: Azure CLI, Bicep CLI 0.35.1+, suscripción/tenant, permisos ARM de creación de recursos y
asignación de roles, y proveedores Microsoft.App, Microsoft.Cdn, Microsoft.ContainerRegistry,
Microsoft.KeyVault, Microsoft.Network, Microsoft.Sql, Microsoft.Insights y Microsoft.OperationalInsights.
Preparar registros Entra para API/SPA con scope delegado `access_as_user`, roles Admin/Member y
asignaciones de usuarios/grupos. `SQL_ENTRA_ADMIN_OBJECT_ID` identifica preferentemente un grupo
Entra de administradores/migraciones; no debe ser la identidad API ni la web.

Variables de los `.bicepparam`:

- `AZURE_WORKLOAD` (valor predeterminado disponible), `API_IMAGE`, `WEB_IMAGE`.
- `ENTRA_TENANT_ID`, `ENTRA_API_CLIENT_ID`, `ENTRA_SPA_CLIENT_ID`.
- `SQL_ENTRA_ADMIN_OBJECT_ID`, `SQL_ENTRA_ADMIN_NAME`.
- `ALERT_EMAIL` (obligatorio en prod).

Ya no se utilizan `SQL_ADMIN_LOGIN` ni `SQL_ADMIN_PASSWORD`. No guardar valores reales de entorno
en Git. Las imágenes deben estar publicadas con una etiqueta inmutable: Dockerfile API desde la raíz,
Dockerfile Angular desde `src/frontend`, después de generar Kiota. Para el primer despliegue puede
usarse un registro existente accesible; después copiar al nuevo ACR y actualizar las imágenes.

```powershell
az group create --name rg-caeiat-dev --location eastus2
az deployment group validate --resource-group rg-caeiat-dev --parameters infra/environments/dev.bicepparam
az deployment group what-if --resource-group rg-caeiat-dev --parameters infra/environments/dev.bicepparam
az deployment group create --resource-group rg-caeiat-dev --parameters infra/environments/dev.bicepparam
```

Tras crear Front Door, aprobar las conexiones Private Link del entorno Container Apps en
Networking > Private endpoint connections. Azure puede generar varias. Configurar la URL `webUrl`
como redirect URI de la SPA. `apiUrl` publica la API mediante `/api`.

### Orden de bootstrap de SQL

1. Desplegar red, SQL, identidades y apps. La API puede no estar lista hasta completar el esquema y permisos.
2. Desde un runner con acceso privado, autenticado como administrador/migrador Entra distinto de la API,
   ejecutar migraciones EF Core. La API solo aplica migraciones automáticamente en Development local.
3. Ejecutar `scripts/azure/initialize-sql-runtime.ps1` con los outputs SQL y `apiIdentityPrincipalId`.
4. Comprobar `/ready`, CRUD con la identidad API y denegación de operaciones DDL. Comprobar que la web
   no pueda conectarse a SQL ni leer Key Vault/Storage.

El runner de migraciones necesita un SDK .NET 10 y EF CLI 10 compatible con el proyecto. Ejemplo para
un runner Azure con una identidad administrada propia, miembro del grupo administrador SQL:

```powershell
# Instalar EF CLI 10 compatible si el runner no lo tiene.
dotnet tool install --global dotnet-ef --version 10.0.11
$migrationConnection = 'Server=tcp:<server>.database.windows.net,1433;Database=app;Authentication=Active Directory Managed Identity;User Id=<migration-identity-client-id>;Encrypt=True;TrustServerCertificate=False'
dotnet ef database update --project src/backend/Infrastructure --startup-project src/backend/Web.Api --connection $migrationConnection
if ($LASTEXITCODE -ne 0) { throw 'Migration failed' }
az login --identity --client-id <migration-identity-client-id>
./scripts/azure/initialize-sql-runtime.ps1 -Server <server>.database.windows.net -Database app -ApiPrincipalId <api-identity-object-id>
```

El bootstrap requiere Azure CLI y el módulo PowerShell SqlServer 22+. Usa un token de la sesión Azure
CLI actual, no lo imprime ni lo persiste. El ejecutor debe ser miembro del administrador Entra SQL y
resolver/acceder al private endpoint. Crea un usuario `TYPE=E` mediante SID para evitar búsquedas de
directorio; comprueba identidad, roles y grants antes de conceder CRUD sobre las dos tablas. Es
idempotente y rechaza usuarios existentes con otra identidad o privilegios inesperados.

Para revisar el SQL sin Azure ni conexión:

```powershell
./scripts/azure/initialize-sql-runtime.ps1 -Server example.database.windows.net -ApiPrincipalId 44444444-4444-4444-4444-444444444444 -RenderOnly
```

### Actualizar un entorno anterior

El despliegue incremental no elimina identidades, roles o secretos omitidos en la plantilla nueva.
Antes de cambiar un servidor existente a Entra-only, preparar su administrador y migrador Entra y
probar su acceso. Sustituir las identidades de los contenedores y comprobar la nueva revisión antes
de retirar la identidad compartida anterior. Inventariar y retirar expresamente su AcrPull, Key Vault
Secrets User y Blob Data Contributor cuando ya no esté en uso, así como usuarios/grants SQL antiguos
y el secreto de conexión SQL obsoleto. No borrar identidades compartidas con otras cargas. Deshabilitar
SQL authentication solo después de comprobar migraciones y ejecución con las identidades nuevas.

## Comprobaciones antes de recibir tráfico real

- Ejecutar validate/what-if en el ambiente elegido y comprobar cuotas, región, DNS y Private Link.
- Recorrer la matriz de rutas con tráfico real y revisar WAF, TLS y sondas.
- Probar Entra delegado Admin/Member, no asignados y app-only; firmas, audiencia y emisor requieren
  tokens reales. Las pruebas sintéticas locales solo comprueban políticas y claims.
- Probar permisos mínimos, restauración SQL y alertas. No extrapolar pruebas de dev a staging/prod.
- Configurar SMTP real con credenciales protegidas. MailPit solo existe localmente; el correo de
  bienvenida es de mejor esfuerzo y no tiene entrega duradera.
- Activar el exporter Azure Monitor de la API al usar Application Insights; actualmente está comentado.
  Los logs de consola de Container Apps sí llegan a Log Analytics.
- Antes de múltiples réplicas, definir consistencia e invalidación de la caché; añadir Redis solo
  no garantiza invalidación de copias locales. Definir presupuesto y objetivos de recuperación.

El workflow compila Bicep y los parámetros de los tres ambientes con valores ficticios, sin login ni
despliegue. La validación externa requiere entorno real y conserva evidencia separada por ambiente.

## Referencias

- [Front Door Premium y Container Apps mediante Private Link](https://learn.microsoft.com/azure/container-apps/front-door-custom-virtual-network-private-link)
- [Front Door: rutas y URL rewrite](https://learn.microsoft.com/azure/frontdoor/front-door-url-rewrite)
- [Parámetros Bicep](https://learn.microsoft.com/azure/azure-resource-manager/bicep/parameter-files)
- [Azure SQL: autenticación de Microsoft Entra en SqlClient](https://learn.microsoft.com/sql/connect/ado-net/sql/azure-active-directory-authentication)
- [CREATE USER: SID y TYPE](https://learn.microsoft.com/sql/t-sql/statements/create-user-transact-sql)
