using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Emberwake.Core;

namespace Emberwake.Backend;

public sealed partial class Store
{
    public AccountInfo Account(string id)
    {
        lock(gate){using var db=Open();var email=Scalar(db,"SELECT email FROM accounts WHERE player=$id",("$id",id)) as string;return new AccountInfo{linked=email!=null,email=email};}
    }
    public static string CleanEmail(string email)
    {
        email=(email??"").Trim().ToLowerInvariant();
        if(email.Length>254||!MailAddress.TryCreate(email,out var address)||address.Address!=email||!email.Contains('.')||email.Any(char.IsWhiteSpace))throw new ApiError("Enter a valid email address.");
        return email;
    }
    static string CodeHash(string challenge,string code,string key)=>Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(challenge+":"+code)));
    public string CreateEmailCode(string email,string player,string code,string key)
    {
        if(key.Length<32)throw new InvalidOperationException("Account signing key must be at least 32 characters");
        email=CleanEmail(email);long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lock(gate)
        {
            using var db=Open();
            var sent=Scalar(db,"SELECT sent FROM email_codes WHERE email=$email",("$email",email));
            if(sent!=null&&now-Convert.ToInt64(sent)<60)throw new ApiError("Please wait a minute before requesting another code.",429);
            if(!string.IsNullOrEmpty(player)&&Account(player).linked)throw new ApiError("This captain already has an email linked. Use Sign in to switch accounts.",409);
            var challenge=Wire.Secret();
            Exec(db,"DELETE FROM email_codes WHERE expires<$old; INSERT OR REPLACE INTO email_codes VALUES($email,$challenge,$hash,$player,$expires,$now,0)",
                ("$old",now-3600),("$email",email),("$challenge",challenge),("$hash",CodeHash(challenge,code,key)),("$player",player??""),("$expires",now+600),("$now",now));
            return challenge;
        }
    }
    public void RevokeEmailCode(string challenge){lock(gate){using var db=Open();Exec(db,"DELETE FROM email_codes WHERE challenge=$c",("$c",challenge));}}
    public AuthResponse VerifyEmailCode(EmailVerifyRequest request,string key)
    {
        if(request?.challenge?.Length!=64||request.code?.Length!=6||request.code.Any(c=>c<'0'||c>'9'))throw new ApiError("Invalid verification code.",401);
        lock(gate)
        {
            using var db=Open();string email,hash,player;long expires;int attempts;
            using(var cmd=db.CreateCommand())
            {
                cmd.CommandText="SELECT email,hash,player,expires,attempts FROM email_codes WHERE challenge=$c";cmd.Parameters.AddWithValue("$c",request.challenge);
                using var r=cmd.ExecuteReader();if(!r.Read())throw new ApiError("Code is invalid or expired. Request another code.",401);
                email=r.GetString(0);hash=r.GetString(1);player=r.GetString(2);expires=r.GetInt64(3);attempts=r.GetInt32(4);
            }
            if(attempts>=5||expires<DateTimeOffset.UtcNow.ToUnixTimeSeconds())throw new ApiError("Code is expired or has too many attempts. Request another code.",401);
            // Commit failed attempts even though the request is rejected.
            Exec(db,"UPDATE email_codes SET attempts=attempts+1 WHERE challenge=$c",("$c",request.challenge));
            if(!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hash),Encoding.UTF8.GetBytes(CodeHash(request.challenge,request.code,key))))throw new ApiError("Invalid verification code.",401);
            using var tx=db.BeginTransaction();
            string existing=Scalar(db,"SELECT player FROM accounts WHERE email=$email",("$email",email)) as string;
            bool linking=!string.IsNullOrEmpty(player);
            if(!string.IsNullOrEmpty(player))
            {
                if(existing!=null&&existing!=player)throw new ApiError("This email has another captain. Choose Sign in to load it; your guest fleet has not been merged.",409);
                Read(db,player);
                if(Scalar(db,"SELECT email FROM accounts WHERE player=$p",("$p",player))!=null&&existing!=player)throw new ApiError("Captain is already linked.",409);
            }
            else player=existing;
            if(player==null)
            {
                var profile=new Profile{id=Guid.NewGuid().ToString("N"),name="Captain"};profile.Unlock(Catalog());foreach(var card in profile.cards)card.blueprints=20;
                player=profile.id;Exec(db,"INSERT INTO players VALUES($id,$credential,$profile)",("$id",player),("$credential",Wire.Hash(Wire.Secret())),("$profile",Wire.Encode(profile)));
            }
            Exec(db,"INSERT OR IGNORE INTO accounts VALUES($email,$player)",("$email",email),("$player",player));
            // Registration retires the guest credential/session before issuing the linked device credential.
            if(linking)Exec(db,"UPDATE players SET credential=$replacement WHERE id=$player; DELETE FROM sessions WHERE player=$player",("$replacement",Wire.Hash(Wire.Secret())),("$player",player));
            var credential=Wire.Secret();var token=Wire.Secret();
            Exec(db,"INSERT INTO devices VALUES($credential,$player); INSERT INTO sessions VALUES($token,$player,$expires); DELETE FROM email_codes WHERE challenge=$challenge",
                ("$credential",Wire.Hash(credential)),("$player",player),("$token",Wire.Hash(token)),("$expires",DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds()),("$challenge",request.challenge));
            var result=new AuthResponse{credential=credential,token=token,profile=Read(db,player)};tx.Commit();return result;
        }
    }
    public void Logout(string id,string credential,string token)
    {
        lock(gate){using var db=Open();using var tx=db.BeginTransaction();Exec(db,"DELETE FROM sessions WHERE hash=$token AND player=$id; DELETE FROM devices WHERE hash=$credential AND player=$id; UPDATE players SET credential=$replacement WHERE id=$id AND credential=$credential",
            ("$id",id),("$token",Wire.Hash(token)),("$credential",Wire.Hash(credential)),("$replacement",Wire.Hash(Wire.Secret())));tx.Commit();}
    }
}

public sealed class AccountMail
{
    public string Key=>Environment.GetEnvironmentVariable("EMBERWAKE_ACCOUNT_KEY")??"";
    public async Task Send(string email,string code)
    {
        string host=Environment.GetEnvironmentVariable("EMBERWAKE_SMTP_HOST"),from=Environment.GetEnvironmentVariable("EMBERWAKE_SMTP_FROM");
        if(string.IsNullOrWhiteSpace(host)||string.IsNullOrWhiteSpace(from)||Key.Length<32)throw new ApiError("Email sign-in is not configured yet.",503);
        using var smtp=new SmtpClient(host,int.TryParse(Environment.GetEnvironmentVariable("EMBERWAKE_SMTP_PORT"),out int port)?port:587);
        smtp.EnableSsl=true;smtp.Timeout=15000;
        var user=Environment.GetEnvironmentVariable("EMBERWAKE_SMTP_USER");
        if(!string.IsNullOrEmpty(user))smtp.Credentials=new NetworkCredential(user,Environment.GetEnvironmentVariable("EMBERWAKE_SMTP_PASSWORD"));
        using var mail=new MailMessage(from,email,"Your Emberwake sign-in code",$"Your Emberwake verification code is {code}. It expires in 10 minutes. Never share this code. If you did not request it, ignore this message.");
        await smtp.SendMailAsync(mail).WaitAsync(TimeSpan.FromSeconds(8));
    }
}
