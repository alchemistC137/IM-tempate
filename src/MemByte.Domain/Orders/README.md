# Módulo de pedidos (Orders)

Vista genérica de plantilla para la gestión de pedidos/órdenes. Cada cliente
de MemByte-IM ordena artículos de su propio catálogo (`MemByte.Domain.Catalog`)
y este módulo administra las tareas clásicas de gestión de pedidos: alta,
líneas, confirmación, progreso, cierre, cancelación y estado de pago.

En el caso concreto de MemByte (la propia empresa gestionada por esta
intranet), sus clientes ordenan soluciones digitales y podrán contratar
diversos servicios de mantenimiento. Esto es solo un ejemplo de uso: el
modelo (`Order`, `OrderLine`, `OrderStatus`, `OrderPaymentStatus`) se ha
mantenido intencionadamente mínimo y extensible para que cada cliente de la
plantilla lo adapte a su propio flujo de pedidos.

- `OrderStatus`: flujo mínimo (Draft → Confirmed → InProgress → Completed,
  con Cancelled como salida anómala). Ampliable con más estados intermedios.
- `OrderPaymentStatus`: Pending/Paid. Ampliable a pagos parciales o
  reembolsos.
- `OrderLine`: referencia un artículo del catálogo (`CatalogItemId`) y
  conserva una "foto" (`ItemNameSnapshot`/`UnitPriceSnapshot`) del nombre y
  precio en el momento de añadirse a la línea, para que el histórico del
  pedido no cambie si el catálogo se actualiza después.
