# MemByte-IM — Runbook de despliegue en Azure (Bicep + Docker)

> **Documento vivo.** Procedimiento de referencia para aprovisionar con Bicep y ejecutar
> la imagen .NET 10 en App Service Linux. Sustituye el despliegue ZIP y los perfiles de
> publicación. El esquema y los datos de PostgreSQL/Supabase siguen siendo externos.
> Las instrucciones describen el nuevo flujo; no implican que se haya aplicado en Azure.

## 1. Arquitectura y costes

| Recurso | Declaración y propósito |
|---|---|
| ACR Basic | `infra/registry.bicep`, registro privado sin usuario administrador; imágenes `membyte-im:<commit-sha>`. |
| App Service Plan Linux | `infra/main.bicep`, B1 por defecto, una instancia; F1 ya no se utiliza. B1, ACR y telemetría tienen coste. Revisar presupuesto y cuotas antes de aplicar. |
| App Service | Docker Linux/amd64, puerto 8080, HTTPS obligatorio, TLS 1.2+, WebSockets y afinidad para Blazor Server. FTP y publicación básica SCM deshabilitados. |
| Identidad administrada de ejecución | `<appName>-runtime`, `AcrPull` solo sobre ACR y `Key Vault Secrets User` solo sobre el vault; sin claves de registro. También se conserva la identidad de sistema del sitio existente. |
| Key Vault | RBAC y soft delete; secretos cargados por operadores fuera de Bicep. App Service resuelve las referencias con la identidad de ejecución. |
| Log Analytics / Application Insights | Workspace con retención de 30 días y telemetría de aplicación; conexión en `ApplicationInsights__ConnectionString`. |
| PostgreSQL / Supabase | No se crea ni modifica con esta plantilla. Migraciones y cuentas de producción se gestionan por separado. |

`infra/production.bicepparam` define el nuevo entorno `production` en el grupo
`rg-membyte-main`, con ubicación explícita `spaincentral` (Spain Central):
App Service `membyte-im-main`, plan `plan-membyte-main` y Key Vault `kv-membyte-main`.
El registro ACR se aprovisiona en la ubicación del grupo y su nombre se deriva de
forma estable del ID del grupo. Mantener el grupo en Spain Central para que coincidan.
Antes de aplicar, comprobar disponibilidad regional y cuota de B1 Linux, ACR y
telemetría, además de la disponibilidad global de los nombres del sitio y del vault.
Estos nombres no se han reservado ni se ha comprobado su disponibilidad en Azure.
El despliegue antiguo (`membyte-im-dev`, `plan-membyte-dev`, `kv-membyte-dev`)
no se modifica ni se traslada automáticamente al nuevo grupo.

El grupo de recursos y la confianza OIDC se preparan una vez como bootstrap.
Las ejecuciones normales son incrementales e idempotentes: no eliminan la base de
datos, secretos ni recursos ajenos. La configuración de aplicación declarada en Bicep
es autoritativa: añadir allí cualquier setting adicional necesario antes de migrar.

## 2. Prerrequisitos y bootstrap

- Azure CLI con Bicep, Docker con motor Linux y .NET 10 SDK para validación local.
- Suscripción Azure, cuota para B1 Linux y permisos para crear recursos/asignar roles.
- GitHub Actions habilitado y un environment `production`; restringir sus ramas de
  despliegue mediante **Selected branches and tags → Branch → master**.
  No usar **Protected branches only** si no existen ramas protegidas.
  Los revisores obligatorios de entornos no están disponibles en repositorios privados
  con planes Free, Pro o Team; el control operativo se realiza con el inicio manual
  del workflow. No equivale a una aprobación independiente ni impide que alguien con
  permisos de escritura cambie el workflow; limitar el acceso al repositorio.

Ejecutar desde la raíz del repositorio en PowerShell:

```powershell
az login --use-device-code
az account set --subscription "<SUBSCRIPTION_ID>"
az bicep version
$rg = "rg-membyte-main"
$location = "spaincentral"
az group create --name $rg --location $location

# Una vez por suscripción. Esperar a registrationState = Registered.
foreach ($provider in @("Microsoft.Web", "Microsoft.ContainerRegistry", "Microsoft.KeyVault", "Microsoft.ManagedIdentity", "Microsoft.OperationalInsights", "Microsoft.Insights")) {
    az provider register --namespace $provider --wait
}
```

Si Spain Central devuelve cuota Linux 0 o un servicio no está disponible, detener el
despliegue y revisar las cuotas/disponibilidad antes de elegir otra región. No cambiar
de región automáticamente. No se puede mover un sitio o plan existente simplemente
cambiando `location`.

### Identidad OIDC para GitHub Actions

Para un bootstrap nuevo, crear una aplicación Entra y su service principal, sin
generar contraseña. Si ya existe `membyte-im-github-production`, reutilizar su Id. de
aplicación (cliente) y su entidad de servicio en lugar de crear duplicados:

```powershell
$clientId = az ad app create --display-name "membyte-im-github-production" --query appId -o tsv
$principalId = az ad sp create --id $clientId --query id -o tsv
$scope = az group show --name $rg --query id -o tsv

az role assignment create --assignee-object-id $principalId --assignee-principal-type ServicePrincipal --role Contributor --scope $scope
az role assignment create --assignee-object-id $principalId --assignee-principal-type ServicePrincipal --role AcrPush --scope $scope
```

`Contributor` no permite crear asignaciones RBAC por sí solo. En el IAM del grupo,
añadir también `Role Based Access Control Administrator` a la misma aplicación.
En Condiciones, elegir **Restringir roles y tipos de entidades de seguridad**:
permitir únicamente los roles `AcrPull` y `Key Vault Secrets User` y el tipo
**Entidades de servicio** (`Service principals`, que incluye identidades administradas).
Esta condición permite asignar los permisos de ejecución definidos en Bicep sin
autorizar la asignación de cualquier rol. Limitar las tres asignaciones al grupo
dedicado; no conceder Owner a nivel de suscripción. `AcrPush` en el grupo permite
publicar en el registro recién creado sin depender de un paso manual posterior.
La identidad de CD no necesita leer los secretos del vault. El bootstrap requiere
permisos Entra además de Azure RBAC; puede hacerlo un administrador distinto.

En Entra → App registrations → esa aplicación → Federated credentials, añadir:

| Campo | Valor |
|---|---|
| Issuer | `https://token.actions.githubusercontent.com` |
| Subject | `repo:alchemistC137@82764907/MemByte-IM@1319419949:environment:production` |
| Audience | `api://AzureADTokenExchange` |

El repositorio utiliza el formato de sujeto OIDC con IDs inmutables. En el formulario
de Entra, el Organization ID (ID del propietario, también para una cuenta personal)
es `82764907` y el Repository ID es `1319419949`. No son los IDs de la aplicación Entra.

En GitHub → Settings → Environments → `production` → Variables:

| Variable | Contenido |
|---|---|
| `AZURE_CLIENT_ID` | `$clientId` |
| `AZURE_TENANT_ID` | Tenant de `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | Suscripción de `az account show --query id -o tsv` |
| `AZURE_RESOURCE_GROUP` | `rg-membyte-main` |

Son identificadores, no contraseñas. Ya no se usan `AZURE_APP_NAME` ni
`AZURE_PUBLISH_PROFILE`; retirar el perfil antiguo de GitHub tras la migración.

## 3. Secretos y primer aprovisionamiento

No introducir connection strings en Bicep, parámetros, workflows ni imágenes.
El secreto requerido es `ConnectionStrings--Default` en `kv-membyte-main`.
El vault nuevo no hereda los secretos del antiguo `kv-membyte-dev`: cargarlos de forma
segura antes de validar el arranque. La base de datos sigue siendo externa; elegir
explícitamente la conexión de destino antes de cargar el secreto.

Para un entorno nuevo, crear primero el registro y publicar una imagen real:

```powershell
az deployment group create --resource-group $rg --name container-registry --template-file infra\registry.bicep
$acr = az deployment group show --resource-group $rg --name container-registry --query properties.outputs.registryName.value -o tsv
$server = az deployment group show --resource-group $rg --name container-registry --query properties.outputs.loginServer.value -o tsv
$tag = git rev-parse HEAD
az acr login --name $acr
docker build --platform linux/amd64 --tag "${server}/membyte-im:${tag}" .
docker push "${server}/membyte-im:${tag}"

az deployment group what-if --resource-group $rg --template-file infra\main.bicep --parameters infra\production.bicepparam imageTag=$tag registryName=$acr
az deployment group create --resource-group $rg --name initial-app --template-file infra\main.bicep --parameters infra\production.bicepparam imageTag=$tag registryName=$acr
```

Revisar el resultado y detenerse ante errores; nunca desplegar el tag de ejemplo
`bootstrap`. La identidad humana necesita `AcrPush` para publicar manualmente.
En un entorno nuevo la app no podrá completar el arranque hasta cargar la conexión:

1. Asignar al operador `Key Vault Secrets Officer` solo sobre el vault recién creado:
   ```powershell
   $vaultId = az keyvault show --name "kv-membyte-main" --resource-group $rg --query id -o tsv
   $me = az ad signed-in-user show --query id -o tsv
   az role assignment create --assignee-object-id $me --assignee-principal-type User --role "Key Vault Secrets Officer" --scope $vaultId
   ```
2. Esperar la propagación RBAC y crear `ConnectionStrings--Default` en el Portal de
   Key Vault con la conexión Npgsql de Supabase, usando SSL y validación de certificado.
   Obtener host/puerto/database/usuario/contraseña desde Supabase → Project Settings
   → Database → Connection string (session pooler si la red no admite la conexión directa).
   No pegar el valor en el historial de la terminal ni imprimirlo en CI.
3. Si se usan adjuntos Azure Blob, guardar su conexión en otro secreto y configurar
   `attachmentConnectionSecretName` en `production.bicepparam`. Sin ese parámetro se
   usa `/home/.messaging-attachments`, almacenamiento privado persistente del sitio.
4. Aplicar las migraciones EF Core mediante el procedimiento operativo existente,
   con copia de seguridad previa. La imagen Production no migra ni siembra usuarios.
5. Reiniciar el sitio después de cargar los secretos y verificar que las referencias
   Key Vault figuren como resueltas en App Service → Environment variables.

Los valores se resuelven mediante referencias `@Microsoft.KeyVault(...)`, no por
`KeyVaultName` (la aplicación actual no carga automáticamente ese setting).
Un secreto ausente o un permiso incorrecto impide el arranque: corregirlo, no sustituir
la conexión por un valor de prueba. RBAC y la caché de referencias pueden tardar
en propagarse; reiniciar o refrescar las referencias tras corregirlos.

## 4. Migrar el sitio existente desde ZIP

El destino actual es un entorno nuevo en otro grupo y región, no una actualización
in situ. No copiar los nombres antiguos a `production.bicepparam`: aprovisionar el
nuevo entorno, trasladar los datos persistentes de forma controlada y validarlo antes
de cambiar dominios o retirar el antiguo. No borrar el entorno antiguo como parte
del primer despliegue.

Los pasos siguientes describen la alternativa de conversión in situ, que solo aplica
si se decide conservar explícitamente el sitio y su región originales:

1. Programar una ventana de mantenimiento: cambiar F1 a B1 y cambiar de runtime a
   contenedor puede reiniciar el sitio. Bicep no crea slots ni garantiza zero downtime.
2. Inventariar settings, secretos, dominios y archivos persistentes; guardar copia de
   seguridad de PostgreSQL y adjuntos. Conservar nombres y región existentes.
3. Trasladar a Bicep los settings adicionales de la aplicación antes de aplicarlo.
   Los antiguos `WEBSITE_RUN_FROM_PACKAGE`, `SCM_DO_BUILD_DURING_DEPLOYMENT` y credenciales
   de registro no se incluyen en el nuevo recurso `appsettings`.
4. Conservar `/home/.dataprotection-keys`. Si hay adjuntos locales bajo
   `/home/site/wwwroot/.messaging-attachments`, copiarlos a `/home/.messaging-attachments`
   antes de cambiar el runtime. Si ya hay Azure Blob, configurar su secreto.
5. Aplicar `what-if`, comprobar que actualiza el sitio/plan/vault esperados y desplegar
   una imagen existente como en la sección 3, o iniciar CD manualmente después de CI.
6. Confirmar `/health`, `/login`, CSS/JS, login/MFA, carga/descarga de adjuntos y conexión
   Blazor/SignalR. Comprobar que cookies y adjuntos sobreviven a un reinicio.
7. Eliminar el perfil de publicación antiguo. El script `scripts/create-publish-zip.ps1`
   queda solo como utilidad histórica; no participa en el nuevo pipeline.

`WEBSITES_ENABLE_APP_SERVICE_STORAGE=true` monta `/home` de forma persistente.
La imagen usa usuario no root y prepara directorios escribibles para ejecución local.
No persistir datos bajo `/app`: se reemplaza con cada imagen.
Se mantiene una instancia y afinidad; ampliar Blazor Server a varias instancias
requiere diseñar la distribución de circuitos/SignalR, no solo subir `capacity`.

## 5. CI/CD y rollback

`ci.yml` compila y ejecuta pruebas .NET, auditoría NuGet y escaneo de secretos.
Además compila Bicep y sus parámetros, construye Docker y prueba `/health`, permisos
del usuario no root y los scripts/estilos que referencia la página, incluido el arranque
de Blazor (`scripts/smoke-test-web-assets.py`). `/health` comprueba que el proceso HTTP responde; no valida
consultas a PostgreSQL ni sustituye las comprobaciones funcionales de login.

`cd.yml` solo se inicia mediante `workflow_dispatch`, no automáticamente tras CI ni
al integrar una PR. Después de integrar el workflow en `master`, abrir
**Actions → CD — Deploy container to Azure → Run workflow**, seleccionar **master**
y confirmar únicamente cuando el entorno esté preparado para desplegar.
El job rechaza otras ramas y hace checkout del SHA de la ejecución manual.
Antes de autenticarse en Azure, comprueba que ese SHA sigue siendo el actual de
`master` y consulta la API de Actions con permiso `actions: read`: la última ejecución
de `ci.yml` para un push de ese SHA, rama y repositorio debe estar completada con
conclusión `success`. CI ausente, pendiente, fallido o cancelado bloquea el despliegue;
esperar o corregir CI e iniciar de nuevo el workflow. No se aceptan ejecuciones de PR.
Después autentica con OIDC, aprovisiona ACR, publica `membyte-im:<SHA>`, aplica Bicep
y espera una respuesta correcta de `/health`. No hay tag `latest`.
Si `master` ha avanzado mientras el inicio manual estaba pendiente, el job rechaza
el SHA obsoleto antes de acceder a Azure para evitar desplegarlo fuera de orden.
Los despliegues al entorno se serializan para no interrumpir una aplicación Bicep
en curso. No borrar imágenes referenciadas por sitios o necesarias para rollback.

Para restaurar una imagen anterior, identificar un SHA de CI correcto que siga
existiendo en ACR, revisar `what-if` y reaplicar la plantilla con ese tag:

```powershell
$stableTag = "<SHA_ESTABLE>"
az deployment group what-if --resource-group $rg --template-file infra\main.bicep --parameters infra\production.bicepparam imageTag=$stableTag registryName=$acr
az deployment group create --resource-group $rg --name rollback --template-file infra\main.bicep --parameters infra\production.bicepparam imageTag=$stableTag registryName=$acr
```

Esto revierte la imagen, no la infraestructura del fichero actual ni el esquema.
Para revertir también código/infraestructura de forma duradera, hacer `git revert`
del cambio en `master`, esperar a que CI termine correctamente e iniciar CD manualmente
para publicar el nuevo SHA.
Un rollback automático de migraciones no es seguro; acordar compatibilidad de esquema
y recuperación de datos antes de aplicar migraciones destructivas.

## 6. Validación local y diagnóstico

```powershell
az bicep build --file infra\main.bicep --stdout
az bicep build --file infra\registry.bicep --stdout
az bicep build-params --file infra\production.bicepparam --stdout
docker build --tag membyte-im:local .
# Usar una conexión de pruebas en una variable de entorno local, nunca en la imagen.
# El montaje conserva claves y adjuntos entre recreaciones del contenedor.
docker run --rm --publish 8080:8080 --env ConnectionStrings__Default --volume membyte-home:/home membyte-im:local
```

- **Docker no conecta:** arrancar Docker Desktop con motor Linux antes de construir.
- **`/_framework/blazor.web.js` devuelve 404 y los botones no responden:** en .NET 10
  los componentes Razor deben estar presentes durante `dotnet restore` para incluir
  `Microsoft.AspNetCore.App.Internal.Assets`. El Dockerfile copia `src` antes de restaurar;
  no sustituirlo por una copia exclusiva de los `.csproj`. Reconstruir y desplegar la
  imagen corregida; no descargar manualmente el script ni deshabilitar su fingerprint.
  Comprobar los assets de la página con `python scripts\smoke-test-web-assets.py <URL>`.
- **Image pull no autorizado:** comprobar `AcrPull`, identidad de ejecución,
  `acrUseManagedIdentityCreds` y propagación RBAC; no habilitar el admin de ACR.
- **Contenedor no responde:** confirmar puerto 8080, logs de contenedor y referencia
  de conexión resuelta; no usar `ASPNETCORE_URLS` que fuerce otro puerto.
- **Redirecciones HTTPS:** App Service termina TLS; Bicep habilita forwarded headers
  para ese proxy. No habilitar esa confianza en despliegues expuestos directamente.
- **Key Vault:** usar roles RBAC, no `az keyvault set-policy`. No desactivar RBAC
  para resolver un error de permisos.
- **Pérdida de cookies/adjuntos:** comprobar montaje `/home`, permisos de escritura
  y rutas; no usar el filesystem efímero de `/app`.

## 7. Telemetría, alertas y rollout

Los eventos `AuthAuditEntry` siguen persistiendo en PostgreSQL y publicándose mediante
`ISecurityTelemetryPublisher`. Bicep conecta Application Insights usando la clave
que realmente consulta `Program.cs`, `ApplicationInsights:ConnectionString`.
Las alertas requieren un action group/canal operativo y se configuran aparte:

| Alerta | Consulta KQL orientativa (`traces`) | Umbral |
|---|---|---|
| Fuerza bruta | `traces \| where customDimensions.EventType == "LoginFailed" \| summarize count() by bin(timestamp, 5m), tostring(customDimensions.Ip)` | >20 fallos/5 min/IP |
| Bloqueos | `traces \| where customDimensions.EventType == "AccountLockedOut" \| summarize count() by bin(timestamp, 15m)` | >5 bloqueos/15 min |
| MFA inválido | `traces \| where customDimensions.EventType == "MfaInvalidCode" \| summarize count() by bin(timestamp, 10m), tostring(customDimensions.UserId)` | >5/10 min/usuario |
| Recuperación inválida | `traces \| where customDimensions.EventType == "InvalidRecoveryCode"` | Cualquier evento |
| Disponibilidad | Availability test sobre `/login`, HTTP 200 | 2 fallos consecutivos |

Aplicar rollout interno → piloto → general, con validación de login/MFA, adjuntos,
auditoría y alertas en cada fase. Ante errores 5xx o regresiones de autenticación,
detener la ampliación y restaurar una imagen estable. Objetivos: disponibilidad
mensual ≥99,5 %, página p95 <300 ms, cero accesos no autorizados y cero vulnerabilidades
críticas en producción; el resto de KPIs se mantiene en `agents.md`.
