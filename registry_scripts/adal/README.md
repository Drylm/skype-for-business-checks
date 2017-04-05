What is ADAL?
==

ADAL is the acronym for the *Active Directory Authentication Library*, and, along with OAuth 2.0, it is an underpinning of Modern Authentication. This code library is designed to make secured resources in your directory available to client applications (like Skype for Business) via security tokens. ADAL works with OAuth 2.0 to enable more authentication and authorization scenarios, like Multi-factor Authentication (MFA), and more forms of SAML Auth.

A variety of apps that act as clients can leverage Modern Authentication for help in getting to secured resources. In Skype for Business Server 2015, this technology is used between on-premises clients and on-premises servers in order to give users a proper level of authorization to resources.

Modern Authentication conversations (which are based on ADAL and OAuth 2.0) have some elements in common.

 * There is a client making a request for a resource, in this case, the client is Skype for Business.
 * There is a resource to which the client needs a specific level of access, and this resource is secured by a directory service, in this case the resource is Skype for Business Server 2015.
 * There is an OAuth connection, in other words, a connection that is dedicated to authorizing a user to access a resource. (OAuth is also known by the more descriptive name, 'Server-to-Server' auth, and is often abbreviated as S2S.)

In Skype for Business Server 2015 Modern Authentication (ADAL) conversations, Skype for Business Server 2015 communicates through ADFS (ADFS 3.0 in Windows Server 2012 R2). The authentication may happen using some other Identity Provider (IdP), but Skype for Business server needs to be configured to communicate with ADFS, directly. If you haven't configured ADFS to work with Skype for Business Server 2015 please complete ADFS installation.

ADAL is included in the March 2016 Cumulative Update for Skype for Business Server 2015, and the March 2016 Cumulative Update for Skype for Business must be installed and is needed for successful configuration.
noteNote:
During the initial release, Modern Authentication in an on-premises environment is supported only if there is no mixed Skype topology involved. For example, if the environment is purely Skype for Business Server 2015. This statement may be subject to change.
 

Set these registry keys for every device or computer on which you want to enable Modern Authentication. 
     
|Registry |Key Type |Value|
|---------|---------|-----|
|HKCU\SOFTWARE\Microsoft\Office\15.0\Common\Identity\EnableADAL | REG_DWORD | 1 |
|HKCU\SOFTWARE\Microsoft\Office\15.0\Common\Identity\Version    | REG_DWORD | 1 |

Once these keys are set, set your Office 2013 apps to use Multifactor Authentication (MFA) with Office 365.
    
To disable Modern Authentication on devices for Office 2013, set the *HKCU\SOFTWARE\Microsoft\Office\15.0\Common\Identity\EnableADAL* registry key to a value of zero.
Be aware that a similiar Registry key ( *HKCU\SOFTWARE\Microsoft\Office\16.0\Common\Identity\EnableADAL*) can also be used to disable Modern Authentication on devices for Office 2016.

Clients where Modern Authentication / ADAL isn't Supported
Some client versions don't support OAuth. You can check your version of Office client in Control Panel where you Add and Remove programs in order to compare your version number to the versions (or ranges of versions) listed here:

 - Office Client 15.0.[0000-4766].*
 - Office Client 16.0.[0000-4293].*
 - Office Client 16.0.6001.[0000-1032]
 - Office Client 16.0.[6000-6224].*
