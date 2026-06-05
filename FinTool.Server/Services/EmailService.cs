using DnsClient;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

class EmailService(AppDbContext db)
{
    public async Task<bool> IsConfiguredAsync()
    {
        var cfg = await db.EmailConfig.FindAsync(1);
        return cfg != null && !string.IsNullOrWhiteSpace(cfg.FromAddress);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string toName, string username, string setPasswordUrl)
    {
        var cfg = await db.EmailConfig.FindAsync(1)
            ?? throw new InvalidOperationException("Email is not configured.");
        await SendDirectAsync(cfg, toEmail, toName,
            subject: "Welcome to Family Finance — set your password",
            html: BuildWelcomeHtml(toName, username, setPasswordUrl));
    }

    public async Task SendTestEmailAsync(string toEmail)
    {
        var cfg = await db.EmailConfig.FindAsync(1)
            ?? throw new InvalidOperationException("Email is not configured.");
        await SendDirectAsync(cfg, toEmail, toEmail,
            subject: "Family Finance — direct delivery test",
            html: BuildTestHtml());
    }

    private static async Task SendDirectAsync(EmailConfigEntity cfg, string toEmail, string toName, string subject, string html)
    {
        var domain = toEmail.Split('@').Last();
        var mxHosts = await LookupMxAsync(domain);
        if (mxHosts.Count == 0)
            throw new InvalidOperationException($"No MX records found for domain '{domain}'.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(cfg.FromName, cfg.FromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body    = new TextPart("html") { Text = html };

        Exception? lastEx = null;
        foreach (var host in mxHosts)
        {
            try
            {
                using var client = new SmtpClient();
                await client.ConnectAsync(host, 25, SecureSocketOptions.None);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                return;
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }
        }

        throw new InvalidOperationException(
            $"Failed to deliver to any MX host for '{domain}'. Last error: {lastEx?.Message}");
    }

    private static async Task<List<string>> LookupMxAsync(string domain)
    {
        var lookup = new LookupClient();
        var result = await lookup.QueryAsync(domain, QueryType.MX);
        return result.Answers
            .MxRecords()
            .OrderBy(r => r.Preference)
            .Select(r => r.Exchange?.Value?.TrimEnd('.') ?? "")
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();
    }

    private static string BuildWelcomeHtml(string name, string username, string url) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>Welcome to Family Finance</title>
        </head>
        <body style="margin:0;padding:0;background-color:#f5f5f5;font-family:'Helvetica Neue',Helvetica,Arial,sans-serif;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f5f5f5;">
            <tr>
              <td align="center" style="padding:48px 16px;">
                <table role="presentation" cellpadding="0" cellspacing="0" style="width:100%;max-width:520px;">
                  <tr>
                    <td style="background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                        <tr>
                          <td style="background:#0a0a0a;padding:28px 40px;text-align:center;">
                            <span style="font-size:20px;font-weight:700;color:#ffffff;letter-spacing:-0.3px;">Family Finance</span>
                          </td>
                        </tr>
                      </table>
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                        <tr>
                          <td style="padding:40px 40px 36px;">
                            <p style="margin:0 0 8px;font-size:26px;font-weight:700;color:#0a0a0a;letter-spacing:-0.4px;">
                              Welcome, {{name}}!
                            </p>
                            <p style="margin:0 0 24px;font-size:15px;color:#5c5c5c;line-height:1.65;">
                              Your account has been created on Family Finance.
                              Your username is&nbsp;<strong style="color:#0a0a0a;">{{username}}</strong>.
                            </p>
                            <p style="margin:0 0 32px;font-size:15px;color:#5c5c5c;line-height:1.65;">
                              Click below to set your password and get started.
                              This link expires in&nbsp;<strong style="color:#0a0a0a;">48&nbsp;hours</strong>.
                            </p>
                            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 36px;">
                              <tr>
                                <td style="background:#0a0a0a;border-radius:10px;">
                                  <a href="{{url}}"
                                     style="display:inline-block;padding:15px 32px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;letter-spacing:-0.1px;">
                                    Set your password &rarr;
                                  </a>
                                </td>
                              </tr>
                            </table>
                            <p style="margin:0;font-size:13px;color:#9e9e9e;line-height:1.65;">
                              Button not working? Copy and paste this link into your browser:<br>
                              <a href="{{url}}" style="color:#5c5c5c;word-break:break-all;">{{url}}</a>
                            </p>
                          </td>
                        </tr>
                      </table>
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                        <tr>
                          <td style="background:#f9f9f9;padding:20px 40px;border-top:1px solid #f0f0f0;text-align:center;">
                            <p style="margin:0;font-size:12px;color:#a0a0a0;line-height:1.6;">
                              If you weren&rsquo;t expecting this, you can safely ignore it.<br>
                              Sent by your Family Finance server.
                            </p>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    private static string BuildTestHtml() => """
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Direct Delivery Test</title></head>
        <body style="margin:0;padding:0;background:#f5f5f5;font-family:'Helvetica Neue',Helvetica,Arial,sans-serif;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f5f5f5;">
            <tr>
              <td align="center" style="padding:48px 16px;">
                <table role="presentation" cellpadding="0" cellspacing="0" style="width:100%;max-width:520px;">
                  <tr>
                    <td style="background:#fff;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                        <tr>
                          <td style="background:#0a0a0a;padding:28px 40px;text-align:center;">
                            <span style="font-size:20px;font-weight:700;color:#fff;letter-spacing:-0.3px;">Family Finance</span>
                          </td>
                        </tr>
                      </table>
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                        <tr>
                          <td style="padding:40px;">
                            <p style="margin:0 0 12px;font-size:22px;font-weight:700;color:#0a0a0a;">Direct delivery test</p>
                            <p style="margin:0;font-size:15px;color:#5c5c5c;line-height:1.65;">
                              Email was delivered directly via MX record lookup — no SMTP relay needed.
                              Check your spam folder if it didn't arrive in your inbox.
                            </p>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
}
