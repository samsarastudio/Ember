using System.Collections;
using Emberwake.Core;
using UnityEngine;

namespace Emberwake.Client
{
    public sealed partial class EmberwakeApp
    {
        void Accounts(){StartCoroutine(LoadAccount());}
        IEnumerator LoadAccount()
        {
            AccountInfo account=null;
            if(service.Online)yield return service.Request<AccountInfo>("GET","/v1/account",null,a=>account=a);
            Clear("account");Header("Your captain, anywhere.",account?.linked==true?account.email:"Guest captain • Link email to protect your progress");
            var email=Field(page,"","Email address",45,360,810);
            Button(page,"Register / link this captain",45,480,810,85,()=>StartCoroutine(SendEmailCode(email.text,true)),true).interactable=service.Online&&account?.linked!=true;
            Button(page,"Sign in with email",45,590,810,85,()=>StartCoroutine(SendEmailCode(email.text,false)));
            Label(page,"We send a six-digit code to your email. Linking saves this fleet. Signing in loads the fleet already linked to that email; fleets are not merged.",45,725,810,240,28);
            Button(page,"Sign out",45,1040,810,85,()=>StartCoroutine(SignOut())).interactable=service.Online&&account?.linked==true;
            Button(page,"Back",45,1170,810,85,Settings);
        }
        IEnumerator SendEmailCode(string email,bool link)
        {
            EmailCodeReply reply=null;ShowLoading("Sending verification code…");
            yield return service.Request<EmailCodeReply>("POST","/v1/auth/email/code",new EmailCodeRequest{email=email,link=link},r=>reply=r);
            if(reply==null){string error=service.Error;yield return LoadAccount();Toast(error);yield break;}
            Clear("email-code");Header("Check your email.","Code sent to "+email);
            var code=Field(page,"","Six-digit code",45,450,810);
            Label(page,"The code expires in 10 minutes. Never share it with anyone.",45,590,810,150,30);
            Button(page,"Verify and continue",45,800,810,90,()=>StartCoroutine(VerifyEmail(reply.challenge,code.text)),true);
            Button(page,"Back / request another code",45,1040,810,85,Accounts);
        }
        IEnumerator VerifyEmail(string challenge,string code)
        {
            yield return service.Request<AuthResponse>("POST","/v1/auth/email/verify",new EmailVerifyRequest{challenge=challenge,code=code.Trim()},service.AcceptAccount);
            if(service.Error!=null){Toast(service.Error);yield break;}
            art.Reload(service.Catalog);Home();Toast("Signed in. Your fleet is saved to your account.");
        }
        IEnumerator SignOut()
        {
            yield return service.Request<ErrorResponse>("POST","/v1/auth/logout",new AuthRequest{credential=PlayerPrefs.GetString(service.CredentialKey,"")},_=>{});
            if(service.Error!=null){Toast(service.Error);yield break;}
            PlayerPrefs.DeleteKey(service.CredentialKey);PlayerPrefs.Save();service.Token=null;service.Online=false;
            service.Profile=new Profile();service.Profile.Unlock(service.Catalog);Accounts();
        }
    }
}
