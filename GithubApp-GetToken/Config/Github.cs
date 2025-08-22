using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace GithubApp_GetToken.Config;

public partial class Github
{
    public string ClientId { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;

    private RSA? _privateKeyInstance;

    public RSA RSAPrivateKey
    {
        get
        {
            if (_privateKeyInstance is null)
            {
                switch (PrivateKey.Trim())
                {
                    case var pk when pk.StartsWith("-----BEGIN RSA PRIVATE KEY-----"):
                        _privateKeyInstance = RSA.Create();
                        // INLINE PKCS#1 PEM or PKCS#8 PEM WITH HEADERS
                        _privateKeyInstance.ImportFromPem(PrivateKey);
                        break;

                    case var pk when pk.StartsWith("MII"):
                        _privateKeyInstance = RSA.Create();
                        try
                        {
                            // INLINE PKCS#1 PEM WITHOUT HEADERS
                            _privateKeyInstance.ImportRSAPrivateKey(Convert.FromBase64String(PrivateKey), out int _);
                        }
                        catch
                        {
                            // PKCS#8 PEM WITHOUT HEADERS
                            _privateKeyInstance.ImportPkcs8PrivateKey(Convert.FromBase64String(PrivateKey), out int _);
                        }
                        break;

                    case var thumbprint when ValidCertificateThumbprint().IsMatch(thumbprint):
                        // Certificate thumbprint in Windows Certificate Store (My store, CurrentUser or LocalMachine)
                        _privateKeyInstance = GetCertificateByThumbprint(thumbprint)?.GetRSAPrivateKey() 
                            ?? throw new InvalidOperationException($"Certificate with thumbprint {thumbprint} not found or without private key.");
                        break;

                     default:
                         _privateKeyInstance = RSA.Create();
                        try
                        {
                            // PEM file (PKCS#1 or PKCS#8)
                            _privateKeyInstance.ImportFromPem(File.ReadAllText(PrivateKey));
                        } 
                        catch
                        {
                            try 
                            {
                                // DER file (PKCS#1)
                                _privateKeyInstance.ImportRSAPrivateKey(File.ReadAllBytes(PrivateKey), out int _);
                            } 
                            catch
                            {
                                // DER file (PKCS#8)
                                _privateKeyInstance.ImportPkcs8PrivateKey(File.ReadAllBytes(PrivateKey), out int _);
                            }
                        }
                        break;

                }
            }
            return _privateKeyInstance;
        }
    }


    [GeneratedRegex("^[0-9a-fA-F]{40}$")]
    private static partial Regex ValidCertificateThumbprint();

    private static X509Certificate2? GetCertificateByThumbprint(string thumbprint)
    {
        X509Certificate2? GetCertByThumbprint(StoreName sn, StoreLocation sl)
        {
            using var store = new X509Store(sn, sl);
            store.Open(OpenFlags.ReadOnly);
            var certs = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
            return certs.FirstOrDefault();
        }

        return GetCertByThumbprint(StoreName.My, StoreLocation.CurrentUser)
            ?? GetCertByThumbprint(StoreName.My, StoreLocation.LocalMachine);
    }
}