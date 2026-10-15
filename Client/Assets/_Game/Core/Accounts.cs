using System;
namespace Emberwake.Core
{
    [Serializable] public sealed class EmailCodeRequest { public string email; public bool link; }
    [Serializable] public sealed class EmailCodeReply { public string challenge; }
    [Serializable] public sealed class EmailVerifyRequest { public string challenge, code; }
    [Serializable] public sealed class AccountInfo { public bool linked; public string email; }
}
