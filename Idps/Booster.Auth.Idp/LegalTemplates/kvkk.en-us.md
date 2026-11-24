title: KVKK Privacy Notice
summary: Information under Article 10 of the Turkish Personal Data Protection Law No. 6698 on how your personal data is processed for sign-in and account services.
date: 2026-11-24
---
*This text is a template: as {{organization}}, adapt it to how you actually operate and have your legal counsel review it before publishing. Delete this line after editing.*

This notice explains how your personal data is processed when you use the sign-in, registration and account management service provided by {{organization}}. It is given under Article 10 of the Turkish Personal Data Protection Law No. 6698 (KVKK). It is for your information only; you do not need to accept it.

## Data controller

The data controller for your personal data is **{{organization}}**.

| Item | Value |
|---|---|
| Legal name | {{organization}} |
| Registry / MERSİS | {{registry}} |
| Address | {{address}} |
| E-mail | {{email}} |
| Registered e-mail (KEP) | {{kep}} |
| Phone | {{phone}} |
| Website | {{website}} |

## Categories of personal data processed

What we process depends on the sign-in methods you use and on the features {{organization}} has enabled.

- **Identity:** display name, username, and the unique user number the system assigns to you.
- **Contact:** e-mail address, phone number, and whether each has been verified.
- **Sign-in data:** a one-way hash of your password, hashes of previous passwords (to prevent reuse), the date your password was changed, the number of failed sign-in attempts and any temporary account lock. Your password itself is never stored.
- **Verification codes:** one-time codes sent by e-mail or SMS. They are valid only for a short time.
- **Session and security records:** hashes of session and refresh tokens, the IP address from which a session was opened, browser and device information (user agent), the sign-in method used, and session start, last activity and end times.
- **External account links:** if you sign in with an external provider (for example Google, Microsoft, Apple, LinkedIn, Facebook, GitHub, X, or an organizational OIDC/SAML provider), your user identifier at that provider, your last sign-in time, and information the provider sends us, such as your name and e-mail address.
- **Organizational directory data:** if you sign in with an organizational directory (LDAP / Active Directory), your username, name and contact details taken from the directory.
- **Authorization data:** roles and permissions assigned to you, and which of your details you allowed to be shared with which application.
- **Invitation data:** if an administrator invited you, the e-mail address used for the invitation and its status.

This service does not process special categories of personal data (for example health or biometric data). [If your organization does, replace this sentence and state the applicable processing condition under KVKK Article 6.]

## Purposes and legal grounds

For each activity, the legal ground is given by reference to the relevant item of KVKK Article 5(2).

| Activity | Data | Purpose | Legal ground |
|---|---|---|---|
| Creating an account, accepting an invitation, updating account details | Identity, contact, sign-in data | Opening and managing your account | Art. 5(2)(c): necessary for the conclusion or performance of a contract |
| Signing in and keeping you signed in (including single sign-on) | Identity, sign-in data, session records | Verifying your identity and giving you access to connected applications | Art. 5(2)(c) |
| Verification codes by e-mail or SMS, passwordless sign-in, two-step verification | Contact, verification codes | Confirming that a sign-in is yours; verifying your e-mail and phone | Art. 5(2)(c); to the extent it prevents account takeover, Art. 5(2)(f): legitimate interest |
| Password reset | Contact, verification codes, IP address | Letting you regain access to your account securely | Art. 5(2)(c); to the extent it prevents abuse, Art. 5(2)(f) |
| Sign-in with an external provider or organizational directory | External account links, directory data | Letting you sign in with the method you choose | Art. 5(2)(c) |
| Security records, failed sign-in tracking, temporary lock, password history | Session and security records, sign-in data | Preventing unauthorized access, brute-force attacks and account takeover; investigating security incidents | Art. 5(2)(f): legitimate interest in keeping accounts and systems secure, provided your fundamental rights and freedoms are not harmed |
| Sending information to connected applications | User number, name, e-mail and verification status, roles and permissions | Letting the application you sign in to recognize you | Art. 5(2)(c). The details to be shared are shown on the application's permission screen. Approving that screen is part of providing the service you request; it is not explicit consent under KVKK. |
| Responding to competent authorities | Data within the scope of the request | Complying with legal obligations | Art. 5(2)(ç): legal obligation. [State sector-specific laws that apply to you, e.g. Law No. 5651, banking or capital markets rules; otherwise leave this general.] |
| Resolving disputes | Relevant records | Establishing, exercising or defending legal rights | Art. 5(2)(e) |

[If the service is provided to your employees, add: "For our employees, account and sign-in processing is part of performing the employment contract (Art. 5(2)(c))."]

We do not ask for your explicit consent for the activities above. If there is any optional processing based on explicit consent, it is requested separately with its own text, and your use of the service does not depend on it.

## How we collect your data

- From the electronic forms you fill in on the registration, sign-in, password and account pages.
- Automatically from your browser during sign-in (IP address, browser information, cookies). For cookies, see the [Cookie Policy](/cookies).
- From the external identity provider or organizational directory (LDAP / Active Directory) you choose for sign-in.
- From the administrator who invited you or created your account.

## SMS and e-mail verification codes

Verification codes are sent only for sign-in, registration, password reset or verifying your contact details. Never share a code with anyone: whoever has the code can access your account as you. These codes are not used to obtain marketing permission or consent to data processing.

## Who we share your data with and why

| Recipient group | Data shared | Purpose |
|---|---|---|
| Hosting and infrastructure provider [Name the provider, or delete this row if you host on your own servers] | All data processed by the service (as a data processor) | Running the service |
| E-mail delivery provider [Name the provider] | E-mail address, name, message containing a code or link | Delivering verification, invitation and notification e-mails |
| SMS provider [Name the provider; delete if SMS is not used] | Phone number, message containing a code | Delivering verification codes |
| The external identity provider you choose for sign-in | Technical details of the sign-in request during the redirect | Letting you authenticate with that provider. The provider processes your data under its own privacy policy. |
| Connected applications you sign in to | Your user number and the details shown on the permission screen (name, e-mail, roles, permissions) | Giving you access to the application |
| Competent public authorities and courts | Data within the scope of the request | Complying with legal obligations |
| [Legal and audit advisors; delete if not applicable] | [Data needed] | [Purpose] |

## International transfers

[Complete this section if your hosting, e-mail or SMS provider is outside Türkiye. If there is no transfer abroad, delete this section entirely.]

Your personal data is transferred to [State the hosting country] through [type of provider, e.g. our hosting provider]. This transfer is made under KVKK Article 9 [by signing the standard contract published by the Personal Data Protection Board and notifying the Authority / on the basis of an adequacy decision]. Explicit consent is not used as the ground for such regular transfers.

If you choose to sign in with an external identity provider, some of these providers are based outside Türkiye. [Assess this with your legal counsel and state the result here.]

## Retention periods

| Data | Period |
|---|---|
| Account data | While the account is open; when the account is deleted, identity and contact data are deleted or anonymized. [Add any additional period] |
| Verification codes | Only while the code is valid (minutes) |
| Session records (including IP address and browser information) | [State the period, e.g. 1 year after the session ends] |
| Security and event logs | [State the period, e.g. 2 years; use a longer period if sector rules require it] |
| Password history | [State the period or number of entries] |
| Application permissions | Until you revoke them, they expire, or the account is deleted |

When the period ends, the data is deleted, destroyed or anonymized. [Link to your data retention and destruction policy, if you have one.]

## Your rights under KVKK Article 11

By applying to {{organization}}, you can:

- Find out whether your personal data is processed and, if so, request information about it.
- Find out why your data is processed and whether it is used for that purpose.
- Find out to whom your data is transferred, in Türkiye or abroad.
- Ask for incomplete or inaccurate data to be corrected.
- Ask for data to be deleted or destroyed when the reasons for processing no longer exist.
- Ask that corrections and deletions be notified to those your data was transferred to.
- Object to an outcome against you that results solely from automated analysis.
- Claim compensation if you suffer damage because of unlawful processing.

On your account page you can update your details, end your active sessions and, if {{organization}} has enabled it, delete your account.

## How to apply

You can submit your application in Turkish through one of these channels:

- **In writing:** a signed letter delivered by hand or by post to {{address}}.
- **By registered e-mail (KEP):** to {{kep}}.
- **By e-mail:** from the e-mail address registered in our system to {{email}}.
- **With a secure electronic or mobile signature:** send your signed application to {{email}}.

Your application should include your full name, your signature if written, your Turkish ID number (or, for foreign nationals, nationality and passport number), your address for notifications, your e-mail address or phone number if any, and the subject of your request.

We will conclude your application within **30 days** at the latest. It is free of charge as a rule. If handling it involves an additional cost, a fee may be charged according to the tariff set by the Personal Data Protection Board.

## Changes

We update this notice when our processing changes. The current version and its effective date are published on this page. If a purpose changes materially, you will be informed before the new processing begins.

For more detail, see the [Privacy Policy](/privacy) and the [Cookie Policy](/cookies).
