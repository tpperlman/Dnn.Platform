using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DotNetNuke.Entities.Controllers;

namespace OAuth.ResourceServer.Core.Server
{
    // Responsible for providing the key to verify the token came from the authorization server
    public class AuthorizationServerKeyManager
    {
        public RSACryptoServiceProvider GetSignatureVerifier()
        {
            var verifier = new RSACryptoServiceProvider();
            var base64EncodedKey = HostController.Instance.GetString("AuthorizationServerVerificationKey");
            verifier.FromXmlString(Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedKey)));
            return verifier;
        }
    }
}
