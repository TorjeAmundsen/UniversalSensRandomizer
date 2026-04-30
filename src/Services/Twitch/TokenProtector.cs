using System;
using System.Security.Cryptography;
using System.Text;

namespace UniversalSensRandomizer.Services.Twitch;

public static class TokenProtector
{
    public static string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return "";
        }
        byte[] data = Encoding.UTF8.GetBytes(plaintext);
        byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public static string? Unprotect(string base64)
    {
        if (string.IsNullOrEmpty(base64))
        {
            return null;
        }
        try
        {
            byte[] encrypted = Convert.FromBase64String(base64);
            byte[] data = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return null;
        }
    }
}
