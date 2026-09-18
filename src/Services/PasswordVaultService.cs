using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Easy7ZipModern.Services;

public class PasswordVaultService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Easy7ZipModernVaultSecurityKey2026");

    private readonly List<string> _passwords = new List<string>();

    private static string VaultFilePath
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Easy7ZipModern");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "vault.dat");
        }
    }

    public IReadOnlyList<string> Passwords => _passwords.AsReadOnly();

    public PasswordVaultService()
    {
        Load();
    }

    public bool AddPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }
        password = password.Trim();
        if (!_passwords.Contains(password))
        {
            _passwords.Insert(0, password);
            Save();
            return true;
        }
        return false;
    }

    public bool RemovePassword(string password)
    {
        if (_passwords.Remove(password))
        {
            Save();
            return true;
        }
        return false;
    }

    public void Clear()
    {
        _passwords.Clear();
        Save();
    }

    private void Load()
    {
        _passwords.Clear();
        try
        {
            if (!File.Exists(VaultFilePath))
            {
                return;
            }
            byte[] encrypted = File.ReadAllBytes(VaultFilePath);
            byte[] plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            string content = Encoding.UTF8.GetString(plain);
            string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line) && !_passwords.Contains(line))
                {
                    _passwords.Add(line);
                }
            }
        }
        catch
        {
        }
    }

    private void Save()
    {
        try
        {
            var sb = new StringBuilder();
            foreach (string pwd in _passwords)
            {
                sb.AppendLine(pwd);
            }
            byte[] plain = Encoding.UTF8.GetBytes(sb.ToString());
            byte[] encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(VaultFilePath, encrypted);
        }
        catch
        {
        }
    }
}
