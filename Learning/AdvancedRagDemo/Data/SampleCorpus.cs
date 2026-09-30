using AIMLAPP.Learning.AdvancedRagDemo.Models;

namespace AIMLAPP.Learning.AdvancedRagDemo.Data;

// 15 realistic-looking documents about a fictional "Acme Corp" internal
// knowledge base. Rich enough to demonstrate every retrieval technique.
//
// Try queries like:
//   - "vacation policy"                            (HR match)
//   - "SKU-2287"                                   (keyword-only, wins hybrid)
//   - "how do I fix login errors"                  (query rewriting shines)
//   - "security policy"     with dept=Engineering  (metadata filter)
public static class SampleCorpus
{
    public static readonly List<SourceDoc> Documents = new()
    {
        new SourceDoc("hr-vacation", @"
Acme Corp Vacation Policy

Full-time employees accrue 15 days of paid vacation per year during their first two years, then 20 days per year thereafter. Vacation time accrues monthly and can be carried over up to a maximum of 10 days into the following year.

To request vacation, submit a request in Workday at least two weeks in advance. Approval is at your manager's discretion. During peak business periods (last two weeks of December, first week of January) additional restrictions may apply.
",
            "policy", "HR", new DateTime(2026, 1, 15)),

        new SourceDoc("hr-parental-leave", @"
Parental Leave

All full-time employees are eligible for 12 weeks of paid parental leave following the birth or adoption of a child. Leave must be taken within 12 months of the qualifying event.

To initiate parental leave, notify your manager at least 30 days in advance and submit the request through the HR portal. Extensions of unpaid leave up to 6 additional months may be requested.
",
            "policy", "HR", new DateTime(2026, 3, 22)),

        new SourceDoc("it-password-reset", @"
Password Reset Procedure

If you cannot log in to your Acme account, first verify that Caps Lock is off and you are using the correct email address (firstname.lastname@acme.com).

To reset your password, navigate to https://login.acme.com and click 'Forgot Password'. You will receive a reset link via email within 5 minutes. The link expires after 30 minutes.

If you do not receive the email, check your spam folder. Persistent login failures should be reported to the IT helpdesk (ext. 4200 or helpdesk@acme.com).
",
            "howto", "IT", new DateTime(2026, 2, 8)),

        new SourceDoc("it-vpn", @"
VPN Access

Remote workers must connect to the Acme VPN to access internal systems. Download the Acme VPN Client from the IT self-service portal.

To connect: launch the client, enter your Acme credentials, and approve the MFA push notification on your registered device. Sessions time out after 8 hours of inactivity.

VPN is required for accessing GitLab, Confluence, Jira, and the internal wiki when working outside the office. If VPN access is denied, contact the IT security team.
",
            "howto", "IT", new DateTime(2026, 4, 2)),

        new SourceDoc("sec-data-classification", @"
Data Classification Policy

Acme classifies all data into four tiers: Public, Internal, Confidential, and Restricted.

Public data (marketing materials, published docs) may be shared freely. Internal data (org charts, project plans) may be shared with any employee. Confidential data (customer PII, financials) requires need-to-know access. Restricted data (source code, security keys) requires explicit approval from the CISO.

Never store Confidential or Restricted data in personal drives, personal email, or unapproved cloud services. All violations must be reported to security@acme.com.
",
            "policy", "Engineering", new DateTime(2026, 1, 5)),

        new SourceDoc("sec-incident-response", @"
Security Incident Response

If you observe suspicious activity — unauthorized access, phishing emails, malware, or data leaks — report it immediately to the Security Operations Center (SOC) at soc@acme.com or by calling ext. 9911.

Do not attempt to investigate or remediate the incident yourself. Preserve evidence: do not delete emails, log files, or malicious attachments. The SOC will guide you through next steps.

Response times: Critical incidents (active breach) — 15 minutes. High (targeted attack) — 1 hour. Medium (suspicious activity) — 4 hours.
",
            "policy", "Engineering", new DateTime(2026, 5, 18)),

        new SourceDoc("eng-code-review", @"
Code Review Standards

All code merged to the main branch requires at least one approving review from another engineer. Reviews should focus on correctness, security, performance, and readability — in that order.

Use the pull request template. Include a description of what changed and why, a list of test scenarios, and links to relevant Jira tickets. PRs with more than 400 lines of changes should be split into smaller reviews.

Response time expectation: within one business day. Blocked PRs should be escalated to the tech lead.
",
            "howto", "Engineering", new DateTime(2026, 3, 12)),

        new SourceDoc("eng-deployment", @"
Deployment Process

Deployments to production run through the Acme CD pipeline in GitLab. Merging to main triggers automatic staging deployment; production requires an additional manual approval from an on-call engineer.

Deploy windows: Monday-Thursday, 09:00 to 16:00 local time. No deploys on Fridays or holidays without VP approval. Emergency hotfixes may bypass these windows but require an incident retrospective.

Roll back with the 'acme-cli deploy rollback' command. Rollbacks always take priority over new deploys.
",
            "howto", "Engineering", new DateTime(2026, 6, 30)),

        new SourceDoc("product-quantum-widget", @"
Quantum Widget (SKU-2287)

The Quantum Widget is Acme's flagship consumer product for Q3 2026. It measures ambient temperature, humidity, and air quality, transmitting data over Wi-Fi or Bluetooth.

Specifications: dimensions 8.2 x 8.2 x 2.4 cm, weight 145 g, battery life 6 months (2 x AA), operating temperature -20°C to +50°C. IP54 water resistance. Compatible with iOS 17+ and Android 12+.

The Quantum Widget retails for $89.99 and is available through acme.com, Amazon, and Best Buy.
",
            "product", "Sales", new DateTime(2026, 7, 1)),

        new SourceDoc("product-nova-sensor", @"
Nova Sensor Pro (SKU-3145)

The Nova Sensor Pro is a professional-grade environmental sensor designed for industrial and scientific use. It captures 40+ metrics including VOCs, particulate matter (PM1/PM2.5/PM10), CO2, and radon.

Specifications: rack-mount 1U form factor, PoE+ powered, gigabit Ethernet, Modbus/BACnet support. Certified for ISO 17025 laboratories. Comes with 3-year warranty.

MSRP $2,499. Sold exclusively through Acme's authorized reseller network.
",
            "product", "Sales", new DateTime(2026, 5, 20)),

        new SourceDoc("sales-discount-policy", @"
Sales Discount Approval

Standard discounts up to 10% may be granted by any sales rep. Discounts of 11-20% require sales manager approval. Discounts of 21-30% require director approval. Anything above 30% requires VP of Sales approval.

For volume orders (100+ units), a separate contract-pricing process applies. Contact the deal desk at dealdesk@acme.com to initiate a custom quote.

Never promise a discount before it is approved in Salesforce.
",
            "policy", "Sales", new DateTime(2026, 4, 25)),

        new SourceDoc("hr-remote-work", @"
Remote Work Guidelines

Acme supports a hybrid work model. Full-time employees may work remotely up to 3 days per week; the remaining 2 days should be in the office. Certain roles (customer-facing, hardware lab, security-sensitive) may have stricter in-office requirements.

Ensure your home workspace is ergonomic, has reliable internet (minimum 25 Mbps), and is free of confidential-conversation privacy risks. Reimbursement of up to $500 for home office equipment is available through the Concur portal.
",
            "policy", "All", new DateTime(2026, 2, 14)),

        new SourceDoc("it-mfa", @"
Multi-Factor Authentication (MFA)

All Acme accounts require MFA. Approved second factors are: 1) the Acme Authenticator app (preferred), 2) a hardware YubiKey, or 3) SMS to a registered phone (least secure — being phased out by end of 2026).

To enrol, log in to https://mfa.acme.com and follow the setup wizard. Backup codes are generated during enrolment — store them in a password manager, NOT in email or plaintext files.

Lost your device? Contact the IT helpdesk immediately to prevent unauthorized access.
",
            "howto", "IT", new DateTime(2026, 6, 10)),

        new SourceDoc("hr-expenses", @"
Expense Reimbursement

Business expenses under $500 can be submitted through Concur without pre-approval. Expenses $500-$2000 require manager pre-approval. Above $2000 requires director pre-approval AND a business justification form.

Submit receipts within 30 days of the expense. Reimbursements are processed twice monthly (on the 15th and last day of each month). Personal expenses accidentally charged to a corporate card must be repaid within one pay cycle.

Meal per diems: $60 domestic, $85 international, $120 for high-cost cities (SF, NYC, London, Tokyo).
",
            "policy", "All", new DateTime(2026, 3, 30)),

        new SourceDoc("eng-oncall", @"
On-Call Rotation

All backend engineers participate in the on-call rotation. Each rotation is one week long, Monday 09:00 to the following Monday 09:00 local time.

Primary on-call responds to PagerDuty alerts within 15 minutes and coordinates the incident response. Secondary on-call is a backup — engage them if the primary is unavailable or the incident requires more than one engineer.

Compensation: $150 per weekday on-call, $300 per weekend day. Time-off-in-lieu is also available at 1:1 for hours worked outside normal business hours.
",
            "policy", "Engineering", new DateTime(2026, 4, 8)),
    };
}
