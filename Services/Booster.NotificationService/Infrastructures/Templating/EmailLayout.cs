namespace Booster.NotificationService.Infrastructures.Templating;

public static class EmailLayout
{
    private const string Ink = "#0f172a";
    private const string Muted = "#64748b";
    private const string Border = "#e2e8f0";
    private const string Surface = "#ffffff";
    private const string Canvas = "#f1f5f9";
    private const string Accent = "#4f46e5";

    private const string FontStack =
        "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif";

    public static string Wrap(string preheader, string heading, string content, string footer) =>
        $"""
        <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background-color:{Canvas};margin:0;padding:0;">
          <tr>
            <td align="center" style="padding:32px 16px;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">{preheader}</div>
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="600" style="width:100%;max-width:600px;background-color:{Surface};border:1px solid {Border};border-radius:12px;">
                <tr>
                  <td style="padding:32px 32px 8px 32px;font-family:{FontStack};">
                    <div style="font-size:13px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;color:{Accent};">Booster</div>
                  </td>
                </tr>
                <tr>
                  <td style="padding:8px 32px 0 32px;font-family:{FontStack};">
                    <h1 style="margin:0;font-size:22px;line-height:30px;font-weight:700;color:{Ink};">{heading}</h1>
                  </td>
                </tr>
                <tr>
                  <td style="padding:16px 32px 32px 32px;font-family:{FontStack};font-size:15px;line-height:24px;color:{Ink};">
        {content}
                  </td>
                </tr>
                <tr>
                  <td style="padding:0 32px;">
                    <div style="border-top:1px solid {Border};"></div>
                  </td>
                </tr>
                <tr>
                  <td style="padding:20px 32px 32px 32px;font-family:{FontStack};font-size:13px;line-height:20px;color:{Muted};">
                    {footer}
                  </td>
                </tr>
              </table>
              <div style="font-family:{FontStack};font-size:12px;line-height:18px;color:{Muted};padding-top:16px;">
                Booster
              </div>
            </td>
          </tr>
        </table>
        """;

    public static string Paragraph(string text) =>
        $"""<p style="margin:0 0 16px 0;">{text}</p>""";

    /// <summary>Metin tabanlı buton; altında düz bağlantı için <see cref="LinkFallback"/> kullanılır.</summary>
    public static string Button(string label, string url) =>
        $"""
        <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:24px 0;">
          <tr>
            <td align="center" bgcolor="{Accent}" style="border-radius:8px;">
              <a href="{url}" style="display:inline-block;padding:13px 28px;font-family:{FontStack};font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:8px;">{label}</a>
            </td>
          </tr>
        </table>
        """;

    /// <summary>Buton çalışmazsa kullanılacak, kırılabilir (word-break) düz bağlantı.</summary>
    public static string LinkFallback(string label, string url) =>
        $"""
        <p style="margin:0 0 8px 0;font-size:13px;line-height:20px;color:{Muted};">{label}</p>
        <p style="margin:0 0 16px 0;font-size:13px;line-height:20px;word-break:break-all;"><a href="{url}" style="color:{Accent};">{url}</a></p>
        """;

    /// <summary>Doğrulama kodu gibi kısa değerler için okunaklı kutu.</summary>
    public static string CodeBox(string value) =>
        $"""
        <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:24px 0;">
          <tr>
            <td align="center" style="background-color:{Canvas};border:1px solid {Border};border-radius:10px;padding:20px;">
              <div style="font-family:'SFMono-Regular',Consolas,'Liberation Mono',Menlo,monospace;font-size:32px;font-weight:700;letter-spacing:10px;color:{Ink};">{value}</div>
            </td>
          </tr>
        </table>
        """;
}
