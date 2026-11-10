namespace MemByte.Application.Auth;

/// <summary>
/// Datos necesarios para que el usuario configure un autenticador TOTP:
/// la clave compartida (para introducción manual) y la URI otpauth:// que
/// se codifica en el QR mostrado en la UI (QRCoder u otra librería de
/// renderizado se encarga de convertir esta URI en una imagen).
/// </summary>
/// <param name="SharedKey">Clave compartida en formato legible (Base32, con espacios cada 4 caracteres).</param>
/// <param name="AuthenticatorUri">URI otpauth://totp/... lista para codificar en un QR.</param>
public sealed record MfaSetupInfo(string SharedKey, string AuthenticatorUri);
