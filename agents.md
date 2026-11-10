# MemByte-IM — Agentes del Sistema

> **Documento vivo.** Este fichero debe actualizarse cada vez que se introduzca un cambio relevante en los roles, permisos, módulos o criterios de éxito del sistema. Ante cualquier modificación que afecte a quién puede hacer qué, este documento es la fuente de verdad. Cada Pull Request que altere permisos, añada módulos o cambie flujos de negocio debe incluir la actualización correspondiente aquí.

---

## Contexto del Negocio

**MemByte** es una empresa de soluciones digitales fundada y gestionada inicialmente por una sola persona, con vocación de crecer con equipo propio. El modelo de negocio consiste en diseñar, desarrollar y mantener soluciones digitales a medida para otros clientes: aplicaciones web, intranets corporativas similares a este mismo proyecto, y cualquier solución informática o digital que se pueda ofrecer.

**MemByte-IM** es la herramienta interna con la que MemByte gestiona su propia operativa y la de sus clientes. El **catálogo de productos** de la empresa incluye:

- Aplicaciones web (portales, dashboards, herramientas SaaS).
- Intranets corporativas (como esta misma plataforma, replicable para otros clientes).
- Cualquier solución informática y digital personalizada según las necesidades del cliente.

La plataforma sirve, por tanto, un doble propósito:
1. **Uso interno:** gestionar los proyectos, el equipo y las operaciones de MemByte.
2. **Producto replicable:** ser la base tecnológica que se despliega y adapta para cada cliente que contrate una intranet similar.

---

## Propósito del Proyecto

MemByte-IM es una intranet empresarial modular diseñada para ser el núcleo operativo digital de cualquier organización. Su objetivo es centralizar la gestión de empleados, el control de acceso, la comunicación interna y los flujos de compras en una única plataforma segura, mantenible y rentable.

La solución está construida sobre **.NET 10 LTS** y **Blazor**, garantizando un soporte extendido hasta noviembre de 2028, y aplicando principios de *Security by Design* y *Clean Architecture* desde el primer día.

---

## Objetivos de Negocio

| # | Objetivo | Descripción |
|---|----------|-------------|
| 1 | **Eficiencia Operativa** | Reducir el tiempo dedicado a tareas administrativas recurrentes mediante flujos digitales claros. |
| 2 | **Seguridad y Cumplimiento** | Garantizar que sólo las personas autorizadas accedan a la información que necesitan (principio de mínimo privilegio). |
| 3 | **Escalabilidad Modular** | Permitir añadir nuevos módulos (RRHH avanzado, ERP, BI) sin reescribir el núcleo de la plataforma. |
| 4 | **Rentabilidad Multi-Cliente** | Ofrecer la plataforma como producto SaaS o instalación on-premise a distintos clientes con configuración mínima por cliente. |
| 5 | **Adopción del Usuario** | Lograr una experiencia de uso fluida que motive la adopción orgánica por parte de los empleados. |

---

## Perfiles de Agentes (Roles)

### Navegación y perfil de usuario

- El menú lateral mantiene las opciones visibles según los roles existentes; estos ajustes no amplían permisos.
- En móvil se abre desde el lateral izquierdo, con fondo superpuesto y cierre mediante el botón, Escape, clic fuera o selección de una opción.
- Los usuarios autenticados ven su nombre y correo en la sección **Mi perfil** del menú, tanto en escritorio como en móvil. El botón **Cerrar sesión** se encuentra en esa sección y mantiene el envío POST con protección antiforgery.

### 1. Administrador (`Admin`)

> Rol de máximo privilegio. Responsable de la configuración global del sistema, la gestión de usuarios y el mantenimiento de la plataforma.

#### Módulo: Gestión de Empleados
- Alta, baja y modificación de fichas de empleados.
- Visualización y edición del organigrama completo.
- Exportación de listados de personal.

#### Módulo: Gestión de Usuarios y Roles
- Crear, editar y eliminar cuentas de usuario.
- Asignar y revocar roles.
- Gestionar políticas de contraseñas y sesiones.
- Auditoría completa de accesos y cambios de rol.

#### Módulo: Mensajería Interna
- Acceso a todos los canales y conversaciones (con fines de auditoría).
- Crear y archivar canales globales.
- Moderar contenido.

#### Módulo: Gestión de Compras
- Visualización de todas las solicitudes de compra.
- Aprobación o rechazo de solicitudes escaladas.
- Configuración de límites de aprobación por rol.
- Generación de reportes de gasto.

#### Módulo: Gestión de Proyectos y Clientes
- Alta, baja y modificación de clientes y proyectos.
- Visibilidad global de todos los proyectos, entregables y horas imputadas de la organización.
- Configuración de tarifas por proyecto/cliente y de reglas de facturación.
- Aprobación final de borradores de factura antes de su emisión.
- Generación de reportes de rentabilidad por cliente/proyecto.

#### Permisos Técnicos
- `admin:*` — acceso total a todos los recursos.
- `client:*` — acceso total a la gestión de clientes.
- `project:*` — acceso total a la gestión de proyectos.
- `billing:*` — acceso total al módulo de facturación.
- Acceso al panel de administración del sistema (`/admin`).

---

### 2. Empleado (`Employee`)

> Usuario base de la plataforma. Puede consultar su propia información, comunicarse con compañeros y generar solicitudes de compra.

#### Módulo: Gestión de Empleados
- Consulta y edición de su propia ficha (datos de contacto, foto de perfil).
- Visualización del organigrama (lectura).

#### Módulo: Gestión de Usuarios y Roles
- Cambio de su propia contraseña.
- Sin acceso a la gestión de otros usuarios.

#### Módulo: Mensajería Interna
- Enviar y recibir mensajes directos.
- Participar en canales a los que ha sido añadido.
- Ver historial propio de mensajes.

#### Módulo: Gestión de Compras
- Crear solicitudes de compra (carrito interno).
- Seguimiento del estado de sus propias solicitudes.
- Cancelación de solicitudes pendientes de aprobación.

#### Módulo: Gestión de Proyectos y Clientes
- Consulta de los proyectos en los que participa.
- Registro de horas imputadas (time tracking) a proyectos asignados.
- Consulta del estado de sus propios registros de horas.

#### Permisos Técnicos
- `employee:read:own` — lectura de sus propios datos.
- `employee:write:own` — edición de sus propios datos.
- `purchase:create` — creación de solicitudes.
- `messaging:send` — envío de mensajes.
- `project:read:own` — lectura de proyectos donde participa.
- `timeentry:create:own` — creación de registros de horas propios.

---

### 3. Jefe de Área (`AreaManager`)

> Supervisor de un equipo o departamento. Tiene capacidades de lectura sobre su equipo y puede aprobar solicitudes dentro de su área.

#### Módulo: Gestión de Empleados
- Consulta de fichas de los empleados de su área.
- Visualización del organigrama de su departamento.
- Sin capacidad de crear o eliminar empleados (responsabilidad del Admin).

#### Módulo: Gestión de Usuarios y Roles
- Consulta de los roles de los miembros de su equipo.
- Sin modificación de roles.

#### Módulo: Mensajería Interna
- Crear canales de equipo dentro de su área.
- Añadir o retirar miembros de sus canales.
- Acceso a mensajes de los canales que gestiona.

#### Módulo: Gestión de Compras
- Visualización de todas las solicitudes de compra de su área.
- Aprobación o rechazo de solicitudes de su equipo (dentro de su límite de gasto configurado).
- Escalado de solicitudes que superan su límite al Agente de Compras o Admin.

#### Módulo: Gestión de Proyectos y Clientes
- Visualización y gestión de los proyectos asignados a su área/equipo.
- Aprobación de horas imputadas por los miembros de su equipo.
- Consulta del estado de entregables de los proyectos de su área.

#### Permisos Técnicos
- `employee:read:team` — lectura de datos del equipo.
- `purchase:approve:team` — aprobación de compras del área.
- `messaging:manage:team` — gestión de canales de equipo.
- `project:read:team` — lectura de proyectos del área.
- `project:manage:team` — gestión de proyectos asignados al área.
- `timeentry:approve:team` — aprobación de horas imputadas por el equipo.

---

### 4. Agente de Compras (`PurchasingAgent`)

> Especialista en el proceso de adquisiciones. Gestiona el flujo completo de compras, proveedores y presupuestos.

#### Módulo: Gestión de Empleados
- Acceso de lectura al directorio de empleados (para asignar solicitudes).

#### Módulo: Gestión de Usuarios y Roles
- Sin acceso especial.

#### Módulo: Mensajería Interna
- Canales específicos de compras para coordinación con proveedores internos y áreas.

#### Módulo: Gestión de Compras
- Visualización de **todas** las solicitudes de compra de la organización.
- Aprobación, rechazo o modificación de cualquier solicitud.
- Gestión del catálogo de productos/servicios internos.
- Gestión del carrito centralizado de la empresa.
- Generación de órdenes de compra formales.
- Seguimiento de estado de pedidos (pendiente, en proceso, entregado).
- Generación de reportes de gasto por área, periodo y categoría.

#### Módulo: Gestión de Proyectos y Clientes
- Acceso de lectura a los proyectos, para correlacionar solicitudes de compra con el proyecto de cliente que las origina.
- Sin capacidad de gestión de clientes, horas o facturación.

#### Permisos Técnicos
- `purchase:*` — acceso total al módulo de compras.
- `catalog:manage` — gestión del catálogo interno.
- `reports:purchase` — generación de reportes de compra.
- `project:read` — lectura de proyectos (sólo lectura, para vincular compras a proyectos).

---

## Matriz de Permisos por Módulo

| Módulo / Acción | Admin | AreaManager | PurchasingAgent | Employee |
|---|:---:|:---:|:---:|:---:|
| **Empleados — Ver propio** | ✅ | ✅ | ✅ | ✅ |
| **Empleados — Ver equipo** | ✅ | ✅ | ✅ | ❌ |
| **Empleados — Ver todos** | ✅ | ❌ | ✅ | ❌ |
| **Empleados — Crear/Eliminar** | ✅ | ❌ | ❌ | ❌ |
| **Usuarios — Gestionar roles** | ✅ | ❌ | ❌ | ❌ |
| **Mensajería — Enviar mensajes** | ✅ | ✅ | ✅ | ✅ |
| **Mensajería — Crear canales** | ✅ | ✅ (equipo) | ✅ (compras) | ❌ |
| **Mensajería — Auditoría** | ✅ | ❌ | ❌ | ❌ |
| **Compras — Crear solicitud** | ✅ | ✅ | ✅ | ✅ |
| **Compras — Aprobar equipo** | ✅ | ✅ | ✅ | ❌ |
| **Compras — Aprobar global** | ✅ | ❌ | ✅ | ❌ |
| **Compras — Gestionar catálogo** | ✅ | ❌ | ✅ | ❌ |
| **Reportes — Compras** | ✅ | ❌ | ✅ | ❌ |
| **Clientes — Gestionar (CRUD)** | ✅ | ❌ | ❌ | ❌ |
| **Proyectos — Ver propios** | ✅ | ✅ (equipo) | ✅ (lectura) | ✅ |
| **Proyectos — Gestionar equipo/área** | ✅ | ✅ | ❌ | ❌ |
| **Proyectos — Ver todos** | ✅ | ❌ | ❌ | ❌ |
| **Horas — Registrar propias** | ✅ | ✅ | ❌ | ✅ |
| **Horas — Aprobar equipo** | ✅ | ✅ | ❌ | ❌ |
| **Facturación — Configurar tarifas** | ✅ | ❌ | ❌ | ❌ |
| **Facturación — Aprobar/Emitir** | ✅ | ❌ | ❌ | ❌ |
| **Panel Admin** | ✅ | ❌ | ❌ | ❌ |

---

## Roadmap de Roles — Próximas Fases

> Los siguientes roles/capacidades están identificados pero **no forman parte del alcance actual**. Se documentan aquí para no perder la traza de la decisión y para que cualquier futura implementación parta de una definición ya acordada.

### 5. Portal de Cliente (`ClientPortalUser`) — Fase 2

> Rol externo reservado para clientes de MemByte. Permitiría acceso restringido, de sólo lectura, al estado de sus propios proyectos, sin visibilidad sobre datos internos de la empresa (empleados, otros clientes, compras, facturación interna).

#### Alcance previsto (no implementado)
- Consulta del estado de sus propios proyectos y entregables.
- Descarga de reportes o borradores de factura relacionados con sus proyectos.
- Sin acceso a mensajería interna, gestión de empleados ni compras.

#### Permisos Técnicos previstos
- `project:read:client-own` — lectura de únicamente los proyectos vinculados a su cuenta de cliente.
- `billing:read:client-own` — lectura de sus propias facturas/borradores.

Este rol **no se añade a la Matriz de Permisos por Módulo** hasta que se apruebe formalmente su desarrollo en un plan posterior (ver `planes-de-desarrollo/01-plan-inicial-arquitectura.md`, sección "Fuera de alcance / Próximas fases").

> **Nota:** el plan **[planes-de-desarrollo/03-plan-gestion-usuarios.md](planes-de-desarrollo/03-plan-gestion-usuarios.md)** introduce una entidad `Client` mínima (agrupador de usuarios) como base de datos previa a este rol, pero `ClientPortalUser` en sí y el portal externo siguen sin implementarse.

---

## Criterios de Éxito — KPIs del Proyecto

### KPIs de Adopción

| KPI | Meta (6 meses) | Medición |
|-----|---------------|----------|
| Tasa de activación de usuarios | ≥ 80 % del total de empleados con sesión activa | Usuarios únicos con login / total de empleados |
| Uso mensual del módulo de mensajería | ≥ 60 % de usuarios activos | Usuarios con ≥ 1 mensaje enviado en el mes |
| Solicitudes de compra digitales | ≥ 90 % del total de solicitudes | Solicitudes por la plataforma / total de solicitudes |

### KPIs de Negocio (Proyectos y Clientes)

| KPI | Meta | Medición |
|-----|------|----------|
| Proyectos de cliente activos gestionados en la plataforma | ≥ 90 % de los proyectos en curso de MemByte | Proyectos registrados en la plataforma / total de proyectos activos |
| Horas facturables registradas a tiempo | ≥ 95 % dentro de las 48 h siguientes a su realización | Registros de horas con timestamp de creación ≤ 48 h desde la fecha trabajada |
| Tiempo desde entregable completado hasta borrador de factura | < 24 horas | Marca de tiempo de cierre de entregable → marca de tiempo de generación del borrador |

### KPIs de Calidad Técnica

| KPI | Meta | Herramienta |
|-----|------|-------------|
| Cobertura de pruebas unitarias | ≥ 70 % en capa de dominio y aplicación | Coverlet + GitHub Actions |
| Tiempo de respuesta promedio de página | < 300 ms (p95) | Application Insights / Azure Monitor |
| Disponibilidad del servicio | ≥ 99.5 % mensual | Azure App Service Health Check |
| Vulnerabilidades críticas en pipeline | 0 en producción | NuGetAudit + GitHub Dependabot |

### KPIs de Seguridad

| KPI | Meta | Herramienta |
|-----|------|-------------|
| Secretos en repositorio | 0 | GitHub Secret Scanning |
| Incidentes de acceso no autorizado | 0 | Logs de ASP.NET Core Identity + Azure AD |
| Tiempo de respuesta ante incidente | < 4 horas | Alertas de Azure Monitor |

### KPIs de Rentabilidad

| KPI | Meta | Frecuencia |
|-----|------|------------|
| Coste mensual de infraestructura por cliente | < $50 USD en arranque | Mensual |
| Tiempo de onboarding de un nuevo cliente | < 2 días | Por despliegue |
| Nuevos módulos entregados | ≥ 1 módulo por trimestre | Trimestral |

---

## Despliegues en Azure — Subagente de Referencia

> Para cualquier tarea de aprovisionamiento, despliegue o troubleshooting en Azure (App Service, Key Vault, CI/CD), consultar primero **[planes-de-desarrollo/02-runbook-despliegue-azure.md](planes-de-desarrollo/02-runbook-despliegue-azure.md)**. La infraestructura se declara en `infra/main.bicep`: App Service Linux con imagen Docker de ACR privado, Key Vault RBAC, identidades administradas y telemetría. GitHub Actions usa OIDC y el despliegue se inicia manualmente con **Run workflow**; solo admite el commit actual de `master` cuyo último CI de push haya terminado correctamente. No utiliza perfiles de publicación. La identidad de ejecución tiene únicamente `AcrPull` y `Key Vault Secrets User` sobre sus recursos; la identidad de despliegue necesita permisos de aprovisionamiento y asignación RBAC limitados al grupo de recursos. No cambian los roles ni permisos de negocio. El runbook documenta bootstrap, migración desde ZIP, persistencia, costes y rollback; debe actualizarse ante cambios en el procedimiento.
