# Plan 04 — Mensajería interna segura

> **Estado:** Implementado · **Versión:** 1.0 · **Fecha:** 2026-09-03
>
> **Depende de:** Plan 01 (arquitectura), Plan 03 (gestión de usuarios y entidad `Client`)

---

## Índice

1. [Contexto y objetivo](#1-contexto-y-objetivo)
2. [Modelo de dominio](#2-modelo-de-dominio)
3. [Diseño por capas](#3-diseño-por-capas)
4. [Reglas de aislamiento y seguridad](#4-reglas-de-aislamiento-y-seguridad)
5. [Adjuntos](#5-adjuntos)
6. [Tiempo real (SignalR)](#6-tiempo-real-signalr)
7. [Retención y purga](#7-retención-y-purga)
8. [Configuración](#8-configuración)
9. [UI y rutas](#9-ui-y-rutas)
10. [Pruebas](#10-pruebas)
11. [Fuera de alcance](#11-fuera-de-alcance)

---

## 1. Contexto y objetivo

La intranet necesita un canal de mensajería que permita a **usuarios de cliente** y a **personal interno** comunicarse, con dos restricciones de negocio:

1. Cada cliente ve **exclusivamente** sus propias conversaciones.
2. No todo el personal puede responder: solo los roles con permiso de respuesta (`Admin`, `Support`).

Esta intranet es a la vez la administración interna de MemByte y la plantilla base de futuras intranets de clientes. Por eso el módulo se diseña **multi-tenant desde el primer día**: el aislamiento no es una capa añadida, es una invariante del modelo.

---

## 2. Modelo de dominio

`MemByte.Domain.Messaging`:

| Entidad | Responsabilidad |
|---|---|
| `Conversation` | Raíz del agregado y **unidad de aislamiento**. Posee `ClientId`, estado, prioridad y los campos de seguimiento (`LastMessageAt`, `LastMessagePreview`, `MessageCount`). |
| `Message` | Mensaje individual con borrado lógico (`IsDeleted`) y edición (`EditedAt`). |
| `MessageAttachment` | Adjunto con nombre saneado y `StorageReference` opaca. |
| `ConversationParticipant` | Pertenencia al hilo, permiso de respuesta, contador de no leídos, anclado y silenciado. |

`Conversation.ClientId` es **nulo para las conversaciones internas de MemByte** y contiene un valor concreto para las de un cliente. Nunca se modifica tras la creación: cambiarlo movería el histórico completo a otro tenant.

Los identificadores se asignan en el constructor del dominio, por lo que en EF Core se declaran con `ValueGeneratedNever()`. Sin esa declaración, EF considera las entidades nuevas como modificadas al adjuntarlas al agregado y el guardado falla.

---

## 3. Diseño por capas

Se sigue el patrón del repositorio: servicios sobre `MemByteDbContext` directamente, sin repositorio genérico.

| Capa | Piezas |
|---|---|
| Domain | Entidades y enums (`ConversationStatus`, `ConversationPriority`, `ConversationParticipantRole`). |
| Application | `IMessagingService`, DTOs de solo lectura, `MessagingOptions`, `MessagingPolicies`, abstracciones `IMessagingAttachmentStorage`, `IMessagingNotifier`, `IMessagingUserContextProvider`. |
| Infrastructure | `MessagingService`, `MessagingAuditLogger`, `AzureBlobMessagingAttachmentStorage`, `LocalMessagingAttachmentStorage`, `MessagingRetentionService`. |
| Web | `MessagingHub`, `SignalRMessagingNotifier`, componentes Blazor, endpoint de descarga de adjuntos. |

---

## 4. Reglas de aislamiento y seguridad

Toda operación del servicio aplica, en este orden:

1. **El usuario se resuelve en servidor** vía `IMessagingUserContextProvider`, a partir de las claims autenticadas. La UI nunca envía un identificador de usuario ni de cliente.
2. **Filtro por `ClientId`** del usuario en la consulta, antes de tocar ningún dato.
3. **Participación activa** en el hilo (`LeftAt == null`).
4. **Permiso de respuesta** comprobado por partida doble: el rol del usuario (`context.CanReply`) y el permiso del participante en ese hilo concreto (`participant.CanReply`).

Los rechazos devuelven **siempre el mismo mensaje**, sin distinguir entre «no existe» y «no tienes acceso»:

> La conversación no existe o no tienes acceso a ella.

Así no se confirma la existencia de conversaciones ajenas mediante sondeo de identificadores.

Al añadir participantes solo se admiten cuentas del **mismo cliente**, de modo que no se pueda «invitar» a un usuario de otro tenant y filtrarle así el contenido.

### Auditoría

`MessagingAuditEntry` es una tabla *append-only*. Se registran las operaciones permitidas y, muy especialmente, **los accesos denegados**: son la señal principal de un intento de acceso cruzado entre clientes.

---

## 5. Adjuntos

- Almacenamiento en **contenedor privado** de Azure Blob Storage (`PublicAccessType.None`). Nunca se generan ni persisten URL públicas.
- La `StorageReference` es una ruta **opaca y no adivinable**, derivada de cliente, conversación y mensaje más un GUID.
- Existe un `LocalMessagingAttachmentStorage` como fallback **solo para desarrollo**, con validación anti *path traversal*.
- Validación por **lista blanca** de tipos MIME (no lista negra), para impedir la subida de contenido ejecutable, más límites de tamaño y número por mensaje.
- La descarga pasa por el endpoint `GET /messaging/attachments/{attachmentId:guid}`, protegido por la política `Messaging.Access`, que **revalida la participación** del usuario y audita la descarga.

---

## 6. Tiempo real (SignalR)

`MessagingHub` está marcado con `[Authorize]` y **no expone ningún método invocable desde el cliente**: es un canal de solo emisión. Esto elimina toda una clase de ataques por invocación directa del hub.

El payload (`ConversationNotification`) es deliberadamente mínimo: identificador de conversación, de mensaje y marca temporal. **No viaja el cuerpo del mensaje ni datos del remitente**, de modo que aunque una suscripción fuese incorrecta no habría contenido que filtrar; el cliente simplemente decide si recarga el hilo, y esa recarga vuelve a pasar por todas las validaciones del servicio.

Se excluye del aviso al propio emisor y a los participantes que hayan silenciado el hilo.

---

## 7. Retención y purga

`MessagingRetentionBackgroundService` es un `IHostedService` que **se levanta con la propia aplicación web**: no requiere instalación, servicio de Windows, tarea programada ni proceso aparte.

Funcionamiento:

1. Un `PeriodicTimer` dispara según `Messaging:RetentionScanInterval` (24 h por defecto).
2. En cada tick crea un *scope* propio (el hosted service es singleton; `MessagingRetentionService` y el `DbContext` son *scoped*).
3. Se seleccionan las conversaciones cuyo `LastMessageAt` supera el umbral de retención.
4. Se **eliminan primero los blobs** de los adjuntos y después las filas, para no dejar ficheros huérfanos accesibles en el almacenamiento. Un fallo al borrar un blob concreto se registra pero no aborta el resto de la purga.
5. Las filas hijas caen por cascada al eliminar la conversación.
6. Se deja traza en la auditoría con el evento `RetentionPurge`.

Si `RetentionEnabled` es `false`, el servicio registra un aviso y termina sin hacer nada.

> **Nota para el escalado futuro:** con varias instancias de la web, cada una ejecutaría su propia purga. La operación es idempotente (borra por umbral de fecha), así que no corrompería datos, pero duplicaría trabajo y entradas de auditoría. Cuando llegue ese escenario habrá que añadir un bloqueo distribuido o mover la purga a un proceso dedicado.

---

## 8. Configuración

Sección `Messaging` de `appsettings.json`:

```json
{
  "Messaging": {
	"RetentionEnabled": true,
	"RetentionYears": 5,
	"RetentionScanInterval": "1.00:00:00",
	"MaxAttachmentSizeBytes": 10485760,
	"MaxAttachmentsPerMessage": 5,
	"MaxMessagesPerPage": 100,
	"AllowedAttachmentContentTypes": [
	  "application/pdf",
	  "image/png",
	  "image/jpeg",
	  "text/plain"
	]
  }
}
```

Almacenamiento de adjuntos (`Messaging:AttachmentStorage`). Si no hay cadena de conexión de Azure configurada, se registra automáticamente el proveedor local de desarrollo:

```json
{
  "Messaging": {
	"AttachmentStorage": {
	  "ConnectionString": "<cadena de Azure Storage>",
	  "ContainerName": "messaging-attachments",
	  "LocalRootPath": ".messaging-attachments"
	}
  }
}
```

La cadena de conexión debe gestionarse mediante *user secrets* en desarrollo y Key Vault o variables de entorno en despliegue; nunca versionada.

---

## 9. UI y rutas

| Ruta / componente | Descripción |
|---|---|
| `/messaging` (`MessagingCenter.razor`) | Página principal: bandeja, hilo y redactor. Se conecta al hub con `HubConnectionBuilder` y reconexión automática. |
| `ConversationList.razor` | Lista reutilizable con búsqueda, filtro por estado, contadores de no leídos, anclado y silenciado. |
| `ConversationView.razor` | Hilo de mensajes, adjuntos, cambio de estado y redactor con `InputFile`. |
| `GET /messaging/attachments/{id}` | Descarga autorizada y auditada de adjuntos. |

Los componentes consumen únicamente DTOs ya filtrados por el servicio, lo que los hace reutilizables sin riesgo de fuga.

---

## 10. Pruebas

**Dominio** (`ConversationTests`): creación válida, asunto inválido, `BelongsToClient` con cliente propio y ajeno, actualización de campos de seguimiento, incremento de no leídos solo para los demás participantes, bloqueo de mensajes en hilos cerrados, idempotencia al añadir participantes y reapertura.

**Servicio** (`MessagingServiceTests`): bandeja vacía sin sesión, bandeja restringida al cliente propio, acceso denegado a conversaciones de otro cliente con mensaje genérico, registro de la denegación en auditoría, imposición del cliente del usuario al crear un hilo, rechazo de respuesta sin permiso, envío correcto con notificación al destinatario y no al emisor, y reseteo del contador de no leídos.

---

## 11. Fuera de alcance

- **API pública de mensajería.** Queda pendiente de decisión: puede variar según el cliente de la plantilla. El servicio interno está diseñado para que un API futuro se apoye en él sin reimplementar las reglas de aislamiento.
- Búsqueda de texto completo sobre el histórico.
- Notificaciones por correo electrónico.
- Bloqueo distribuido de la purga (ver §7).
