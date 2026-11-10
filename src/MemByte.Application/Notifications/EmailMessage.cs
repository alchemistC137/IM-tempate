namespace MemByte.Application.Notifications;

/// <summary>
/// Mensaje de correo saliente, agnóstico del proveedor de envío real
/// (SMTP/SendGrid/Azure Communication Services). Se usa para las
/// invitaciones de alta de usuario y los reseteos de contraseña iniciados
/// por un Admin (ver planes-de-desarrollo/03-plan-gestion-usuarios.md, PR3).
/// </summary>
public sealed record EmailMessage(string To, string Subject, string Body);
