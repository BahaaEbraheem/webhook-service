using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebhookService.Core.Interfaces
{
    public interface ISignatureService
    {
        string GenerateSignature(string version, long timestamp, Guid eventId, string body, string secret);
        bool ValidateSignature(string signature, string version, long timestamp, Guid eventId, string body, string secret);
        string EncryptSecret(string secret);
        string DecryptSecret(string encryptedSecret);
    }

}
