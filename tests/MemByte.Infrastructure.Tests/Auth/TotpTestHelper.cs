using System.Security.Cryptography;

namespace MemByte.Infrastructure.Tests.Auth;

/// <summary>
/// Genera códigos TOTP (RFC 6238) a partir de una clave compartida Base32,
/// simulando lo que haría una app autenticadora real (Google Authenticator,
/// Microsoft Authenticator, etc.). ASP.NET Core Identity sólo *valida*
/// códigos TOTP en el servidor: <c>AuthenticatorTokenProvider.GenerateAsync</c>
/// deliberadamente devuelve una cadena vacía, así que los tests deben calcular
/// el código esperado por su cuenta.
/// </summary>
public static class TotpTestHelper
{
	public static string GenerateCode(string base32Key, DateTimeOffset? timestamp = null)
	{
		var keyBytes = Base32Decode(base32Key);
		var counter = (long)((timestamp ?? DateTimeOffset.UtcNow) - DateTimeOffset.UnixEpoch).TotalSeconds / 30;

		var counterBytes = BitConverter.GetBytes(counter);
		if (BitConverter.IsLittleEndian)
			Array.Reverse(counterBytes);

		using var hmac = new HMACSHA1(keyBytes);
		var hash = hmac.ComputeHash(counterBytes);

		var offset = hash[^1] & 0x0F;
		var binaryCode = ((hash[offset] & 0x7F) << 24)
			| ((hash[offset + 1] & 0xFF) << 16)
			| ((hash[offset + 2] & 0xFF) << 8)
			| (hash[offset + 3] & 0xFF);

		var code = binaryCode % 1_000_000;
		return code.ToString("D6");
	}

	private static byte[] Base32Decode(string input)
	{
		const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
		var cleaned = input.Replace(" ", string.Empty).Replace("-", string.Empty).TrimEnd('=').ToUpperInvariant();

		var bits = 0;
		var value = 0;
		var output = new List<byte>();

		foreach (var c in cleaned)
		{
			var index = alphabet.IndexOf(c);
			if (index < 0)
				continue;

			value = (value << 5) | index;
			bits += 5;

			if (bits >= 8)
			{
				output.Add((byte)((value >> (bits - 8)) & 0xFF));
				bits -= 8;
			}
		}

		return output.ToArray();
	}
}
