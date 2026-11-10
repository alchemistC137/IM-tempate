# Módulo de Catálogo de Productos y Servicios

Base de dominio + aplicación + infraestructura para que cada cliente de la
plantilla gestione su propio catálogo de productos y servicios ofertados a
sus clientes. En el caso de MemByte, este catálogo será el de sus propias
soluciones digitales; en el de cualquier otro cliente de la plantilla, se
adaptará a su naturaleza de negocio.

## Estado de esta iteración

- Implementado: modelo de dominio (`CatalogCategory`, `CatalogItem`,
  `Product`, `Service`, `Price`), servicio de aplicación (`ICatalogService`),
  persistencia EF Core (migración `AddCatalogModule`) y pruebas de dominio e
  infraestructura.
- Pendiente (fuera de alcance de esta iteración): páginas Blazor de gestión
  del catálogo (listado, alta/edición de categorías, productos y servicios).
  Se implementarán en una iteración posterior siguiendo el mismo patrón que
  `Components/Pages/Clients` y `Components/Pages/Employees`.

## Aislamiento por cliente

Todo el catálogo está acotado por `ClientId` (obligatorio): no existe un
catálogo global. Cada método de `ICatalogService` recibe el `clientId` de
forma explícita; no hay un "contexto de usuario" ambiental como en el módulo
de mensajería, porque la gestión del catálogo la realiza habitualmente
personal interno en nombre de cada cliente.

## Extensibilidad

- `Product` y `Service` heredan de `CatalogItem` (mapeados con TPH en
  `CatalogItems`, columna discriminadora `ItemType`). Cada cliente puede
  añadir sus propios atributos específicos creando nuevas propiedades en
  `Product`/`Service`, o incluso nuevas subclases de `CatalogItem` si su
  catálogo requiere un tercer tipo de artículo.
- `Price` es un value object simple (importe + moneda ISO 4217), pensado
  para poder evolucionar hacia tarifas variables (por horas, por rango de
  cantidad, etc.) sin romper la forma en que se expone el precio "base" de
  un artículo del catálogo.
- `CatalogCategory` soporta jerarquía (`ParentCategoryId`); la validación de
  ciclos se aplica en `CatalogService`, no en el dominio.
