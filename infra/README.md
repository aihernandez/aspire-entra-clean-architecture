# Infraestructura Azure del template

Esta carpeta define una instancia independiente por ambiente (`dev`, `staging`, `prod`) con Bicep. No crea recursos en una suscripción hasta ejecutar un despliegue. Cada ambiente debe usar un resource group propio y sus registros de aplicación de Entra ID.

## Servicios

| Módulo | Base del template | Motivo |
|---|---|---|
| `network` | Sí | VNet y subredes separadas para Container Apps y private endpoints. |
| `identity` | Sí | Identidad administrada para extraer imágenes y consultar Key Vault. |
| `registry` | Sí | Azure Container Registry para las imágenes de API y Angular. |
| `sql` | Sí | Azure SQL Database, auditoría y acceso privado; reemplaza SQL Server local de Aspire. |
| `key-vault` | Sí | Secreto de conexión a SQL, acceso RBAC y private endpoint. |
| `monitor` | Sí | Log Analytics, Application Insights y grupo de alertas; alerta de CPU de SQL cuando se indica correo. |
| `container-apps` | Sí | API .NET y Angular en un entorno interno de Container Apps. |
| `front-door` | Sí | Front Door Premium, Private Link y WAF; rutas `/api/*` y `/*` bajo el mismo dominio. |
| `storage` | Opcional | Blob privado para archivos cuando la aplicación incorpore esa función. |

No se incluyen Redis, Service Bus, API Management, AKS ni Azure Communication Services porque el código actual no depende de ellos. Añadirlos por módulo cuando exista un caso de uso concreto. En producción, considerar también Azure Backup para cargas adicionales, Azure Policy, Defender for Cloud, presupuestos y una segunda región si el objetivo de continuidad lo requiere.

## Diferencias entre ambientes

| Configuración | dev | staging | prod |
|---|---:|---:|---:|
| Modo de escalado | Fijo | HTTP automático | HTTP automático |
| Réplicas efectivas por app | 1 | 1–3 | 2–10 |
| SQL | Basic | S1 | General Purpose 2 vCores |
| Redundancia zonal de Container Apps y SQL | No | No | Sí |
| Retención Log Analytics | 30 días | 30 días | 90 días |
| WAF | Detección | Detección | Prevención |
| Restauración puntual SQL | 7 días | 7 días | 35 días, más retención semanal y mensual |
| Key Vault purge protection | No | No | Sí |
| Storage si se activa | LRS | LRS | GZRS |

Los CIDR de ejemplo son distintos entre ambientes. Ajustarlos antes de conectar la VNet a una red corporativa. Las cuotas, SKU y redundancia zonal dependen de la región. Front Door Premium y Private Link tienen costo fijo considerable incluso en `dev`; un perfil económico necesitaría un diseño de borde separado.

El escalado se controla por ambiente con `scaleMode`, `minReplicas`, `maxReplicas`, `apiHttpConcurrency` y `webHttpConcurrency`. En `fixed`, el módulo pone el mínimo y el máximo efectivos en `minReplicas`; no basta con quitar la regla HTTP porque Container Apps aplica una regla predeterminada cuando hay ingress. En `auto`, cada app usa una regla HTTP explícita y escala hasta `maxReplicas`. Los umbrales iniciales de 10 solicitudes concurrentes se deben ajustar con pruebas de carga, observando latencia, errores y capacidad de SQL. `maxReplicas` debe ser igual o mayor que `minReplicas` cuando se usa `auto`.

## Despliegue

Se requiere Azure CLI con Bicep CLI 0.35.1 o superior (por las salidas seguras), permisos de creación de recursos y asignación de roles, los proveedores `Microsoft.App`, `Microsoft.Cdn`, `Microsoft.ContainerRegistry`, `Microsoft.KeyVault`, `Microsoft.Network`, `Microsoft.Sql`, `Microsoft.Insights`, `Microsoft.OperationalInsights` y registros Entra para la API y la SPA. La identidad que despliega debe tener permisos de escritura de secretos en Key Vault.

Preparar las imágenes primero. El Dockerfile de la API se construye desde la raíz del repositorio; el de Angular se construye desde `src/frontend` después de generar el cliente Kiota. Como el ACR nuevo todavía no existe en el primer despliegue, publicar inicialmente ambas imágenes en un registro existente accesible. Tras crear ACR, copiarlas allí y actualizar `API_IMAGE` y `WEB_IMAGE` para volver a desplegar.

Variables que leen los archivos `.bicepparam`: `AZURE_WORKLOAD`, `API_IMAGE`, `WEB_IMAGE`, `ENTRA_TENANT_ID`, `ENTRA_API_CLIENT_ID`, `ENTRA_SPA_CLIENT_ID`, `SQL_ADMIN_LOGIN`, `SQL_ADMIN_PASSWORD`, `ALERT_EMAIL`. `AZURE_WORKLOAD` tiene valor por defecto; `ALERT_EMAIL` es obligatorio en prod. Usar una bóveda o variables secretas del pipeline para la contraseña. No guardar valores reales en Git.

Ejemplo, tras configurar las variables y seleccionar la suscripción:

```powershell
az group create --name rg-caeiat-dev --location eastus2
az deployment group what-if --resource-group rg-caeiat-dev --parameters infra/environments/dev.bicepparam
az deployment group create --resource-group rg-caeiat-dev --parameters infra/environments/dev.bicepparam
```

Después de crear Front Door, aprobar las solicitudes de Private Link del entorno de Container Apps en la sección *Networking > Private endpoint connections*; Azure puede generar más de una. El resultado `webUrl` sirve Angular y `apiUrl` sirve la API a través de `/api`.

## Trabajo necesario antes de recibir tráfico real

1. Generar el cliente Kiota, compilar y publicar las imágenes de la API y Angular con un identificador inmutable; la infraestructura no compila código.
2. Registrar cada URL de Front Door como redirect URI de la SPA en Entra ID, asignar App Roles y configurar los identificadores del ambiente. La URL se conoce tras el despliegue inicial.
3. Ejecutar migraciones de EF Core contra Azure SQL desde una red con acceso privado. La API solo las aplica automáticamente en `Development`.
4. Configurar un proveedor de correo SMTP real y credenciales protegidas. MailPit solo existe en el AppHost local; sin proveedor, el sender actual registra los correos y no los entrega.
5. Validar conectividad de Key Vault y Azure SQL, reglas WAF, probes, restauración SQL, alertas y presupuesto antes de abrir prod. El recurso de Application Insights está preparado, pero el exporter Azure Monitor del código permanece comentado; activarlo para trazas y métricas de la API. Los logs de consola de Container Apps sí fluyen a Log Analytics.

## Referencias

- [Front Door Premium con Container Apps y Private Link](https://learn.microsoft.com/azure/container-apps/front-door-custom-virtual-network-private-link)
- [Parámetros Bicep por ambiente](https://learn.microsoft.com/azure/azure-resource-manager/bicep/parameter-files)
- [Azure Front Door WAF](https://learn.microsoft.com/azure/frontdoor/create-front-door-cli)
