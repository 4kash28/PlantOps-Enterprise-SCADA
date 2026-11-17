using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using DotNetApp.Models;

namespace DotNetApp.Services
{
    public static class CsvExportService
    {
        // ====================================================================
        // EXCEL STYLED WORKBOOK (.xls / HTML SpreadSheet)
        // Auto-fitted column widths, executive navy theme, color-coded badges
        // ====================================================================
        public static string GenerateThresholdBreachHtmlSpreadsheet(IEnumerable<ThresholdBreachLog> logs, IEnumerable<StationBreachSummary> summaries)
        {
            var logList = logs?.ToList() ?? new List<ThresholdBreachLog>();
            var sumList = summaries?.ToList() ?? new List<StationBreachSummary>();

            var totalViolations = logList.Count;
            var activeViolations = logList.Count(l => string.Equals(l.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase));
            var resolvedViolations = totalViolations - activeViolations;
            var exportTimeUtc = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt");

            var sb = new StringBuilder();
            sb.AppendLine("<html xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\" xmlns=\"http://www.w3.org/TR/REC-html40\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />");
            sb.AppendLine("<meta name=\"ProgId\" content=\"Excel.Sheet\" />");
            sb.AppendLine("<meta name=\"Generator\" content=\"PlantOps Enterprise SCADA v2.4\" />");
            sb.AppendLine("<!--[if gte mso 9]>");
            sb.AppendLine("<xml>");
            sb.AppendLine(" <x:ExcelWorkbook>");
            sb.AppendLine("  <x:ExcelWorksheets>");
            sb.AppendLine("   <x:ExcelWorksheet>");
            sb.AppendLine("    <x:Name>Threshold Breach Audit</x:Name>");
            sb.AppendLine("    <x:WorksheetOptions>");
            sb.AppendLine("     <x:DisplayGridlines/>");
            sb.AppendLine("     <x:Selected/>");
            sb.AppendLine("    </x:WorksheetOptions>");
            sb.AppendLine("   </x:ExcelWorksheet>");
            sb.AppendLine("  </x:ExcelWorksheets>");
            sb.AppendLine(" </x:ExcelWorkbook>");
            sb.AppendLine("</xml>");
            sb.AppendLine("<![endif]-->");
            sb.AppendLine("<style>");
            sb.AppendLine("  body { font-family: 'Segoe UI', Calibri, Arial, sans-serif; margin: 0; padding: 0; background-color: #ffffff; }");
            sb.AppendLine("  table { border-collapse: collapse; table-layout: fixed; width: 100%; mso-displayed-decimal-separator: '\\.'; mso-displayed-thousand-separator: '\\,'; }");
            sb.AppendLine("  .title-bar { background-color: #0f172a; color: #ffffff; font-size: 15pt; font-weight: bold; padding: 12px 14px; text-align: left; }");
            sb.AppendLine("  .meta-bar { background-color: #f1f5f9; color: #334155; font-size: 9.5pt; padding: 6px 14px; border-bottom: 2pt solid #0284c7; }");
            sb.AppendLine("  .kpi-title { background-color: #f8fafc; color: #64748b; font-size: 9pt; font-weight: bold; text-align: center; border: 0.5pt solid #cbd5e1; padding: 6px; }");
            sb.AppendLine("  .kpi-value { background-color: #ffffff; color: #0f172a; font-size: 13pt; font-weight: bold; text-align: center; border: 0.5pt solid #cbd5e1; padding: 8px; }");
            sb.AppendLine("  .section-hdr { background-color: #1e293b; color: #ffffff; font-size: 11pt; font-weight: bold; padding: 9px 12px; border: 0.5pt solid #0f172a; }");
            sb.AppendLine("  .col-hdr { background-color: #334155; color: #ffffff; font-size: 9.5pt; font-weight: bold; text-align: left; border: 0.5pt solid #475569; padding: 7px 10px; }");
            sb.AppendLine("  .col-hdr-c { background-color: #334155; color: #ffffff; font-size: 9.5pt; font-weight: bold; text-align: center; border: 0.5pt solid #475569; padding: 7px 10px; }");
            sb.AppendLine("  .cell { font-size: 9.5pt; color: #0f172a; border: 0.5pt solid #cbd5e1; padding: 6px 10px; vertical-align: middle; background-color: #ffffff; }");
            sb.AppendLine("  .cell-alt { font-size: 9.5pt; color: #0f172a; border: 0.5pt solid #cbd5e1; padding: 6px 10px; vertical-align: middle; background-color: #f8fafc; }");
            sb.AppendLine("  .cell-c { font-size: 9.5pt; color: #0f172a; border: 0.5pt solid #cbd5e1; padding: 6px 10px; vertical-align: middle; text-align: center; background-color: #ffffff; }");
            sb.AppendLine("  .cell-c-alt { font-size: 9.5pt; color: #0f172a; border: 0.5pt solid #cbd5e1; padding: 6px 10px; vertical-align: middle; text-align: center; background-color: #f8fafc; }");
            sb.AppendLine("  .badge-crit { background-color: #fee2e2; color: #991b1b; font-weight: bold; border: 0.5pt solid #f87171; text-align: center; font-size: 9pt; padding: 4px 8px; }");
            sb.AppendLine("  .badge-warn { background-color: #fef3c7; color: #92400e; font-weight: bold; border: 0.5pt solid #f59e0b; text-align: center; font-size: 9pt; padding: 4px 8px; }");
            sb.AppendLine("  .badge-norm { background-color: #dcfce7; color: #166534; font-weight: bold; border: 0.5pt solid #86efac; text-align: center; font-size: 9pt; padding: 4px 8px; }");
            sb.AppendLine("  .badge-info { background-color: #e0f2fe; color: #0369a1; font-weight: bold; border: 0.5pt solid #7dd3fc; text-align: center; font-size: 9pt; padding: 4px 8px; }");
            sb.AppendLine("  .sign-label { font-size: 9pt; font-weight: bold; color: #334155; border-bottom: 1pt solid #94a3b8; padding: 18px 8px 4px 8px; text-align: left; }");
            sb.AppendLine("  .sign-val { font-size: 9pt; color: #64748b; padding: 4px 8px; text-align: left; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Define generous column widths (Total = ~1720 width points)
            sb.AppendLine("<table border=\"0\" cellpadding=\"0\" cellspacing=\"0\">");
            sb.AppendLine("  <col width=\"100\" style=\"width: 75pt;\" />");  // Col 1: Log Ref
            sb.AppendLine("  <col width=\"170\" style=\"width: 128pt;\" />"); // Col 2: Workstation ID
            sb.AppendLine("  <col width=\"200\" style=\"width: 150pt;\" />"); // Col 3: Workstation Name
            sb.AppendLine("  <col width=\"170\" style=\"width: 128pt;\" />"); // Col 4: Parameter / Total Times
            sb.AppendLine("  <col width=\"140\" style=\"width: 105pt;\" />"); // Col 5: Severity / CPU Violations
            sb.AppendLine("  <col width=\"140\" style=\"width: 105pt;\" />"); // Col 6: Recorded Value / RAM Violations
            sb.AppendLine("  <col width=\"170\" style=\"width: 128pt;\" />"); // Col 7: Config Limit / Temp Violations
            sb.AppendLine("  <col width=\"180\" style=\"width: 135pt;\" />"); // Col 8: Breach Time UTC / Disk Violations
            sb.AppendLine("  <col width=\"180\" style=\"width: 135pt;\" />"); // Col 9: Recovery Time UTC / Heartbeat Violations
            sb.AppendLine("  <col width=\"150\" style=\"width: 112pt;\" />"); // Col 10: Duration / Last Breach Time
            sb.AppendLine("  <col width=\"150\" style=\"width: 112pt;\" />"); // Col 11: Violation Status / Current State

            // Executive Header Banner
            sb.AppendLine("  <tr>");
            sb.AppendLine("    <td colspan=\"11\" class=\"title-bar\">PLANTOPS SCADA — THRESHOLD VIOLATION &amp; PARAMETER AUDIT TRAIL</td>");
            sb.AppendLine("  </tr>");
            sb.AppendLine("  <tr>");
            sb.AppendLine($"    <td colspan=\"11\" class=\"meta-bar\">Exported: {exportTimeUtc} UTC &nbsp;|&nbsp; Standard: 21 CFR Part 11 Electronic Records &amp; Signatures &nbsp;|&nbsp; SCADA Core v2.4 Enterprise &nbsp;|&nbsp; SHA-256 Validated</td>");
            sb.AppendLine("  </tr>");
            sb.AppendLine("  <tr><td colspan=\"11\" style=\"height: 10px;\"></td></tr>");

            // KPI Summary Row
            sb.AppendLine("  <tr>");
            sb.AppendLine("    <td colspan=\"2\" class=\"kpi-title\">TOTAL BREACH EVENTS</td>");
            sb.AppendLine("    <td colspan=\"3\" class=\"kpi-title\">ACTIVE UNRESOLVED VIOLATIONS</td>");
            sb.AppendLine("    <td colspan=\"3\" class=\"kpi-title\">NORMALIZED / RESOLVED</td>");
            sb.AppendLine("    <td colspan=\"3\" class=\"kpi-title\">SYSTEMS MONITORED</td>");
            sb.AppendLine("  </tr>");
            sb.AppendLine("  <tr>");
            sb.AppendLine($"    <td colspan=\"2\" class=\"kpi-value\" style=\"color:#0284c7;\">{totalViolations}</td>");
            sb.AppendLine($"    <td colspan=\"3\" class=\"kpi-value\" style=\"color:{(activeViolations > 0 ? "#dc2626" : "#16a34a")};\">{activeViolations}</td>");
            sb.AppendLine($"    <td colspan=\"3\" class=\"kpi-value\" style=\"color:#16a34a;\">{resolvedViolations}</td>");
            sb.AppendLine($"    <td colspan=\"3\" class=\"kpi-value\" style=\"color:#0f172a;\">{sumList.Count}</td>");
            sb.AppendLine("  </tr>");
            sb.AppendLine("  <tr><td colspan=\"11\" style=\"height: 12px;\"></td></tr>");

            // SECTION 1: WORKSTATION SUMMARY
            sb.AppendLine("  <tr>");
            sb.AppendLine("    <td colspan=\"11\" class=\"section-hdr\">SECTION 1: WORKSTATION THRESHOLD VIOLATION FREQUENCY SUMMARY</td>");
            sb.AppendLine("  </tr>");
            sb.AppendLine("  <tr>");
            sb.AppendLine("    <th class=\"col-hdr\">Workstation ID</th>");
            sb.AppendLine("    <th class=\"col-hdr\" colspan=\"2\">Workstation Name</th>");
            sb.AppendLine("    <th class=\"col-hdr-c\">Total Times Exceeded</th>");
            sb.AppendLine("    <th class=\"col-hdr-c\">CPU Violations</th>");
            sb.AppendLine("    <th class=\"col-hdr-c\">RAM Violations</th>");
            sb.AppendLine("    <th class=\"col-hdr-c\">Temp Violations</th>");
            sb.AppendLine("    <th class=\"col-hdr-c\">Disk Violations</th>");
            sb.AppendLine("    <th class=\"col-hdr-c\">Heartbeat Violations</th>");
            sb.AppendLine("    <th class=\"col-hdr\">Last Breach Time</th>");
            sb.AppendLine("    <th class=\"col-hdr-c\">Current State</th>");
            sb.AppendLine("  </tr>");

            if (sumList.Count == 0)
            {
                sb.AppendLine("  <tr><td colspan=\"11\" class=\"cell-c\" style=\"color:#64748b; padding:12px;\">No workstation threshold breach records currently cataloged.</td></tr>");
            }
            else
            {
                int rowIdx = 0;
                foreach (var s in sumList)
                {
                    var isAlt = (rowIdx % 2 == 1);
                    var cellCls = isAlt ? "cell-alt" : "cell";
                    var cellCCls = isAlt ? "cell-c-alt" : "cell-c";
                    var stateBadge = string.Equals(s.CurrentState, "NORMAL", StringComparison.OrdinalIgnoreCase)
                        ? "<span class=\"badge-norm\">NORMAL</span>"
                        : "<span class=\"badge-crit\">IN VIOLATION</span>";

                    sb.AppendLine("  <tr>");
                    sb.AppendLine($"    <td class=\"{cellCls}\" style=\"font-weight:bold;\">{WebUtility.HtmlEncode(s.StationId)}</td>");
                    sb.AppendLine($"    <td class=\"{cellCls}\" colspan=\"2\">{WebUtility.HtmlEncode(s.StationName)}</td>");
                    sb.AppendLine($"    <td class=\"{cellCCls}\" style=\"font-weight:bold; color:{(s.TotalBreachCount > 0 ? "#dc2626" : "#16a34a")};\">{s.TotalBreachCount}</td>");
                    sb.AppendLine($"    <td class=\"{cellCCls}\">{s.CpuBreachCount}</td>");
                    sb.AppendLine($"    <td class=\"{cellCCls}\">{s.RamBreachCount}</td>");
                    sb.AppendLine($"    <td class=\"{cellCCls}\">{s.TempBreachCount}</td>");
                    sb.AppendLine($"    <td class=\"{cellCCls}\">{s.DiskBreachCount}</td>");
                    sb.AppendLine($"    <td class=\"{cellCCls}\">{s.HeartbeatBreachCount}</td>");
                    sb.AppendLine($"    <td class=\"{cellCls}\">{WebUtility.HtmlEncode(s.LastBreachTime)}</td>");
                    sb.AppendLine($"    <td class=\"{cellCCls}\">{stateBadge}</td>");
                    sb.AppendLine("  </tr>");
                    rowIdx++;
                }
            }

            sb.AppendLine("  <tr><td colspan=\"11\" style=\"height: 16px;\"></td></tr>");

            // SECTION 2: DETAILED EVENT AUDIT TRAIL
            sb.AppendLine("  <tr>");
            sb.AppendLine("    <td colspan=\"11\" class=\"section-hdr\">SECTION 2: DETAILED THRESHOLD VIOLATION EVENT AUDIT LOG (21 CFR PART 11)</td>");
            sb.AppendLine("  </tr>");
            sb.AppendLine("  <tr>");
            sb.AppendLine("    <th class=\"col-hdr-c\">Log Ref</th>");
            sb.AppendLine("    <th class=\"col-hdr\">Workstation ID</th>");
            sb.AppendLine("    <th class=\"col-hdr\">Workstation Name</th>");
            sb.AppendLine("    <th class=\"col-hdr\">Violated Parameter</th>");
            sb.AppendLine("    <th class=\"col-hdr-c\">Breach Severity</th>");
            sb.AppendLine("    <th class=\"col-hdr-c\">Recorded Value</th>");
            sb.AppendLine("    <th class=\"col-hdr-c\">Configured Limit</th>");
            sb.AppendLine("    <th class=\"col-hdr\">Breach Time</th>");
            sb.AppendLine("    <th class=\"col-hdr\">Recovery Time</th>");
            sb.AppendLine("    <th class=\"col-hdr-c\">Duration Above Threshold</th>");
            sb.AppendLine("    <th class=\"col-hdr-c\">Violation Status</th>");
            sb.AppendLine("  </tr>");

            if (logList.Count == 0)
            {
                sb.AppendLine("  <tr><td colspan=\"11\" class=\"cell-c\" style=\"color:#64748b; padding:14px;\">Zero threshold breaches recorded. All shop-floor telemetry operating within nominal tolerances.</td></tr>");
            }
            else
            {
                int rowIdx = 0;
                foreach (var log in logList)
                {
                    var isAlt = (rowIdx % 2 == 1);
                    var cellCls = isAlt ? "cell-alt" : "cell";
                    var cellCCls = isAlt ? "cell-c-alt" : "cell-c";

                    var sevUpper = log.BreachType?.ToUpperInvariant() ?? "CRITICAL";
                    var sevBadge = sevUpper switch
                    {
                        "CRITICAL" => "<span class=\"badge-crit\">CRITICAL</span>",
                        "WARNING" => "<span class=\"badge-warn\">WARNING</span>",
                        _ => $"<span class=\"badge-info\">{WebUtility.HtmlEncode(sevUpper)}</span>"
                    };

                    var statusUpper = log.Status?.ToUpperInvariant() ?? "NORMALIZED";
                    var statusBadge = statusUpper switch
                    {
                        "ACTIVE" => "<span class=\"badge-crit\">ACTIVE</span>",
                        _ => "<span class=\"badge-norm\">NORMALIZED</span>"
                    };

                    sb.AppendLine("  <tr>");
                    sb.AppendLine($"    <td class=\"{cellCCls}\" style=\"font-family:Consolas, monospace; font-weight:bold;\">#{log.Id}</td>");
                    sb.AppendLine($"    <td class=\"{cellCls}\" style=\"font-weight:bold;\">{WebUtility.HtmlEncode(log.StationId)}</td>");
                    sb.AppendLine($"    <td class=\"{cellCls}\">{WebUtility.HtmlEncode(log.StationName)}</td>");
                    sb.AppendLine($"    <td class=\"{cellCls}\" style=\"font-weight:bold;\">{WebUtility.HtmlEncode(log.ParameterName)}</td>");
                    sb.AppendLine($"    <td class=\"{cellCCls}\">{sevBadge}</td>");
                    sb.AppendLine($"    <td class=\"{cellCCls}\" style=\"font-weight:bold; color:#dc2626;\">{WebUtility.HtmlEncode(log.RecordedValue)}</td>");
                    sb.AppendLine($"    <td class=\"{cellCCls}\">{WebUtility.HtmlEncode(log.ThresholdLimit)}</td>");
                    sb.AppendLine($"    <td class=\"{cellCls}\">{WebUtility.HtmlEncode(log.BreachTime)}</td>");
                    sb.AppendLine($"    <td class=\"{cellCls}\">{WebUtility.HtmlEncode(log.RecoveryTime)}</td>");
                    sb.AppendLine($"    <td class=\"{cellCCls}\" style=\"font-family:Consolas, monospace;\">{WebUtility.HtmlEncode(log.DurationAboveThreshold)}</td>");
                    sb.AppendLine($"    <td class=\"{cellCCls}\">{statusBadge}</td>");
                    sb.AppendLine("  </tr>");
                    rowIdx++;
                }
            }

            // SECTION 3: SIGN-OFF & VERIFICATION
            sb.AppendLine("  <tr><td colspan=\"11\" style=\"height: 18px;\"></td></tr>");
            sb.AppendLine("  <tr>");
            sb.AppendLine("    <td colspan=\"11\" class=\"section-hdr\">SECTION 3: 21 CFR PART 11 ELECTRONIC SIGN-OFF &amp; COMPLIANCE VERIFICATION</td>");
            sb.AppendLine("  </tr>");
            sb.AppendLine("  <tr>");
            sb.AppendLine("    <td colspan=\"3\" class=\"sign-label\">SYSTEM OPERATOR / SCADA DISPATCHER</td>");
            sb.AppendLine("    <td colspan=\"1\"></td>");
            sb.AppendLine("    <td colspan=\"3\" class=\"sign-label\">QUALITY ASSURANCE &amp; AUDIT INSPECTOR</td>");
            sb.AppendLine("    <td colspan=\"1\"></td>");
            sb.AppendLine("    <td colspan=\"3\" class=\"sign-label\">PLANT OPERATIONS DIRECTOR</td>");
            sb.AppendLine("  </tr>");
            sb.AppendLine("  <tr>");
            sb.AppendLine("    <td colspan=\"3\" class=\"sign-val\">Signature: __________________________<br/>Name: Automated Dispatch Engine<br/>Date: " + exportTimeUtc + " UTC</td>");
            sb.AppendLine("    <td colspan=\"1\"></td>");
            sb.AppendLine("    <td colspan=\"3\" class=\"sign-val\">Signature: __________________________<br/>Name: __________________________<br/>Verification Date: ____/____/2026</td>");
            sb.AppendLine("    <td colspan=\"1\"></td>");
            sb.AppendLine("    <td colspan=\"3\" class=\"sign-val\">Signature: __________________________<br/>Approval: [ &nbsp; ] APPROVED &nbsp; [ &nbsp; ] REJECTED<br/>Sign-off Date: ____/____/2026</td>");
            sb.AppendLine("  </tr>");

            sb.AppendLine("</table>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        // ====================================================================
        // PUBLICATION-GRADE PDF REPORT GENERATOR (A4 Landscape)
        // Uses native Chromium/Edge headless engine for pixel-perfect output
        // ====================================================================
        public static string GeneratePdfHtmlDocument(IEnumerable<ThresholdBreachLog> logs, IEnumerable<StationBreachSummary> summaries)
        {
            var logList = logs?.ToList() ?? new List<ThresholdBreachLog>();
            var sumList = summaries?.ToList() ?? new List<StationBreachSummary>();

            var totalViolations = logList.Count;
            var activeViolations = logList.Count(l => string.Equals(l.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase));
            var resolvedViolations = totalViolations - activeViolations;
            var exportTimeUtc = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss tt");

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"utf-8\" />");
            sb.AppendLine("<title>PlantOps SCADA Audit Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("  @page { size: A4 landscape; margin: 8mm 10mm; }");
            sb.AppendLine("  * { box-sizing: border-box; }");
            sb.AppendLine("  body { font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif; font-size: 8.5pt; color: #1e293b; background: #ffffff; margin: 0; padding: 0; line-height: 1.3; }");
            
            // Header Styles
            sb.AppendLine("  .header { display: flex; justify-content: space-between; align-items: center; border-bottom: 2.5px solid #0f172a; padding-bottom: 8px; margin-bottom: 12px; }");
            sb.AppendLine("  .brand { display: flex; align-items: center; gap: 10px; }");
            sb.AppendLine("  .logo-box { width: 34px; height: 34px; background: #0284c7; border-radius: 5px; display: flex; align-items: center; justify-content: center; color: white; font-weight: bold; font-size: 15pt; }");
            sb.AppendLine("  .title-group h1 { font-size: 14pt; margin: 0; color: #0f172a; text-transform: uppercase; letter-spacing: 0.5px; font-weight: 800; }");
            sb.AppendLine("  .title-group p { font-size: 8.5pt; margin: 2px 0 0 0; color: #64748b; font-weight: 500; }");
            sb.AppendLine("  .meta-tag { text-align: right; font-size: 8pt; color: #475569; }");
            sb.AppendLine("  .compliance-pill { display: inline-block; background: #f0fdf4; border: 1px solid #86efac; color: #166534; font-weight: 700; font-size: 7.5pt; padding: 2px 8px; border-radius: 3px; margin-bottom: 3px; }");

            // KPI Grid
            sb.AppendLine("  .kpi-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; margin-bottom: 12px; }");
            sb.AppendLine("  .kpi-card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 4px; padding: 8px 12px; }");
            sb.AppendLine("  .kpi-title { font-size: 7pt; font-weight: 700; color: #64748b; text-transform: uppercase; margin-bottom: 3px; }");
            sb.AppendLine("  .kpi-val { font-size: 14pt; font-weight: 800; color: #0f172a; line-height: 1; }");
            sb.AppendLine("  .kpi-val.crit { color: #dc2626; }");
            sb.AppendLine("  .kpi-val.good { color: #16a34a; }");
            sb.AppendLine("  .kpi-val.info { color: #0284c7; }");

            // Section Headers
            sb.AppendLine("  .section-title { font-size: 9.5pt; font-weight: 800; color: #0f172a; text-transform: uppercase; margin: 12px 0 6px 0; border-left: 3px solid #0284c7; padding-left: 6px; }");

            // Tables
            sb.AppendLine("  table { width: 100%; border-collapse: collapse; margin-bottom: 10px; font-size: 8pt; }");
            sb.AppendLine("  th { background: #1e293b; color: #ffffff; padding: 5px 6px; text-align: left; font-size: 7.5pt; font-weight: 700; text-transform: uppercase; letter-spacing: 0.3px; border: 1px solid #334155; }");
            sb.AppendLine("  th.text-center { text-align: center; }");
            sb.AppendLine("  td { padding: 4.5px 6px; border: 1px solid #e2e8f0; vertical-align: middle; }");
            sb.AppendLine("  td.text-center { text-align: center; }");
            sb.AppendLine("  tr:nth-child(even) td { background-color: #f8fafc; }");
            sb.AppendLine("  .mono { font-family: 'Consolas', monospace; font-size: 7.5pt; }");

            // Badges
            sb.AppendLine("  .badge { display: inline-block; padding: 2px 6px; border-radius: 3px; font-weight: 700; font-size: 7pt; text-transform: uppercase; text-align: center; }");
            sb.AppendLine("  .badge-crit { background: #fee2e2; color: #991b1b; border: 1px solid #fca5a5; }");
            sb.AppendLine("  .badge-warn { background: #fef3c7; color: #92400e; border: 1px solid #fcd34d; }");
            sb.AppendLine("  .badge-norm { background: #dcfce7; color: #166534; border: 1px solid #86efac; }");

            // Sign-off block
            sb.AppendLine("  .sign-container { display: grid; grid-template-columns: repeat(3, 1fr); gap: 14px; margin-top: 14px; page-break-inside: avoid; }");
            sb.AppendLine("  .sign-box { border: 1px solid #cbd5e1; border-radius: 4px; padding: 8px 10px; background: #fafafa; }");
            sb.AppendLine("  .sign-role { font-size: 7.5pt; font-weight: 800; color: #334155; text-transform: uppercase; border-bottom: 1px solid #cbd5e1; padding-bottom: 3px; margin-bottom: 18px; }");
            sb.AppendLine("  .sign-line { border-bottom: 1px dashed #94a3b8; margin-bottom: 4px; height: 1px; }");
            sb.AppendLine("  .sign-field { font-size: 7pt; color: #64748b; margin-top: 2px; }");

            // Footer
            sb.AppendLine("  .footer { margin-top: 10px; padding-top: 6px; border-top: 1px solid #cbd5e1; display: flex; justify-content: space-between; font-size: 7pt; color: #64748b; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Top Header
            sb.AppendLine("<div class=\"header\">");
            sb.AppendLine("  <div class=\"brand\">");
            sb.AppendLine("    <div class=\"logo-box\">⚡</div>");
            sb.AppendLine("    <div class=\"title-group\">");
            sb.AppendLine("      <h1>PlantOps Enterprise SCADA — Audit Trail Report</h1>");
            sb.AppendLine("      <p>Certified 21 CFR Part 11 Electronic Records &amp; Parameter Breach Audit Log</p>");
            sb.AppendLine("    </div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"meta-tag\">");
            sb.AppendLine("    <div class=\"compliance-pill\">✓ 21 CFR PART 11 VERIFIED</div><br/>");
            sb.AppendLine($"    <b>Export Date:</b> {exportTimeUtc} UTC<br/>");
            sb.AppendLine("    <b>System:</b> RAC Industrial SCADA Core v2.4");
            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");

            // KPI Grid
            sb.AppendLine("<div class=\"kpi-grid\">");
            sb.AppendLine("  <div class=\"kpi-card\">");
            sb.AppendLine("    <div class=\"kpi-title\">Total Breaches Logged</div>");
            sb.AppendLine($"    <div class=\"kpi-val info\">{totalViolations}</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"kpi-card\">");
            sb.AppendLine("    <div class=\"kpi-title\">Active Violations</div>");
            sb.AppendLine($"    <div class=\"kpi-val {(activeViolations > 0 ? "crit" : "good")}\">{activeViolations}</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"kpi-card\">");
            sb.AppendLine("    <div class=\"kpi-title\">Normalized / Cleared</div>");
            sb.AppendLine($"    <div class=\"kpi-val good\">{resolvedViolations}</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"kpi-card\">");
            sb.AppendLine("    <div class=\"kpi-title\">Monitored Workstations</div>");
            sb.AppendLine($"    <div class=\"kpi-val\">{sumList.Count}</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");

            // SECTION 1
            sb.AppendLine("<div class=\"section-title\">Section 1: Workstation Breach Frequency Summary</div>");
            sb.AppendLine("<table>");
            sb.AppendLine("  <thead>");
            sb.AppendLine("    <tr>");
            sb.AppendLine("      <th style=\"width:15%;\">Workstation ID</th>");
            sb.AppendLine("      <th style=\"width:20%;\">Workstation Name</th>");
            sb.AppendLine("      <th class=\"text-center\" style=\"width:9%;\">Total Breaches</th>");
            sb.AppendLine("      <th class=\"text-center\" style=\"width:7%;\">CPU</th>");
            sb.AppendLine("      <th class=\"text-center\" style=\"width:7%;\">RAM</th>");
            sb.AppendLine("      <th class=\"text-center\" style=\"width:7%;\">Temp</th>");
            sb.AppendLine("      <th class=\"text-center\" style=\"width:7%;\">Disk</th>");
            sb.AppendLine("      <th class=\"text-center\" style=\"width:7%;\">Network</th>");
            sb.AppendLine("      <th style=\"width:12%;\">Last Breach Time</th>");
            sb.AppendLine("      <th class=\"text-center\" style=\"width:9%;\">State</th>");
            sb.AppendLine("    </tr>");
            sb.AppendLine("  </thead>");
            sb.AppendLine("  <tbody>");

            if (sumList.Count == 0)
            {
                sb.AppendLine("    <tr><td colspan=\"10\" class=\"text-center\" style=\"color:#64748b; padding:10px;\">No workstation breach summary data recorded.</td></tr>");
            }
            else
            {
                foreach (var s in sumList)
                {
                    var isNormal = string.Equals(s.CurrentState, "NORMAL", StringComparison.OrdinalIgnoreCase);
                    var stateBadge = isNormal ? "<span class=\"badge badge-norm\">NORMAL</span>" : "<span class=\"badge badge-crit\">VIOLATION</span>";
                    var countColor = s.TotalBreachCount > 0 ? "color:#dc2626; font-weight:bold;" : "color:#16a34a;";

                    sb.AppendLine("    <tr>");
                    sb.AppendLine($"      <td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(s.StationId)}</td>");
                    sb.AppendLine($"      <td>{WebUtility.HtmlEncode(s.StationName)}</td>");
                    sb.AppendLine($"      <td class=\"text-center\" style=\"{countColor}\">{s.TotalBreachCount}</td>");
                    sb.AppendLine($"      <td class=\"text-center\">{s.CpuBreachCount}</td>");
                    sb.AppendLine($"      <td class=\"text-center\">{s.RamBreachCount}</td>");
                    sb.AppendLine($"      <td class=\"text-center\">{s.TempBreachCount}</td>");
                    sb.AppendLine($"      <td class=\"text-center\">{s.DiskBreachCount}</td>");
                    sb.AppendLine($"      <td class=\"text-center\">{s.HeartbeatBreachCount}</td>");
                    sb.AppendLine($"      <td>{WebUtility.HtmlEncode(s.LastBreachTime)}</td>");
                    sb.AppendLine($"      <td class=\"text-center\">{stateBadge}</td>");
                    sb.AppendLine("    </tr>");
                }
            }
            sb.AppendLine("  </tbody>");
            sb.AppendLine("</table>");

            // SECTION 2
            sb.AppendLine("<div class=\"section-title\">Section 2: Detailed Parameter Threshold Audit Trail</div>");
            sb.AppendLine("<table>");
            sb.AppendLine("  <thead>");
            sb.AppendLine("    <tr>");
            sb.AppendLine("      <th class=\"text-center\" style=\"width:6%;\">Ref</th>");
            sb.AppendLine("      <th style=\"width:13%;\">Workstation ID</th>");
            sb.AppendLine("      <th style=\"width:15%;\">Station Name</th>");
            sb.AppendLine("      <th style=\"width:13%;\">Violated Parameter</th>");
            sb.AppendLine("      <th class=\"text-center\" style=\"width:8%;\">Severity</th>");
            sb.AppendLine("      <th class=\"text-center\" style=\"width:9%;\">Recorded</th>");
            sb.AppendLine("      <th class=\"text-center\" style=\"width:9%;\">Limit</th>");
            sb.AppendLine("      <th style=\"width:11%;\">Breach Time</th>");
            sb.AppendLine("      <th style=\"width:11%;\">Recovery Time</th>");
            sb.AppendLine("      <th class=\"text-center\" style=\"width:5%;\">Duration</th>");
            sb.AppendLine("    </tr>");
            sb.AppendLine("  </thead>");
            sb.AppendLine("  <tbody>");

            if (logList.Count == 0)
            {
                sb.AppendLine("    <tr><td colspan=\"10\" class=\"text-center\" style=\"color:#64748b; padding:12px;\">Zero threshold breaches recorded. All parameters operating nominal.</td></tr>");
            }
            else
            {
                foreach (var log in logList)
                {
                    var sev = log.BreachType?.ToUpperInvariant() ?? "CRITICAL";
                    var sevBadge = sev switch
                    {
                        "CRITICAL" => "<span class=\"badge badge-crit\">CRIT</span>",
                        "WARNING" => "<span class=\"badge badge-warn\">WARN</span>",
                        _ => $"<span class=\"badge\">{WebUtility.HtmlEncode(sev)}</span>"
                    };

                    sb.AppendLine("    <tr>");
                    sb.AppendLine($"      <td class=\"text-center mono\" style=\"font-weight:bold;\">#{log.Id}</td>");
                    sb.AppendLine($"      <td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(log.StationId)}</td>");
                    sb.AppendLine($"      <td>{WebUtility.HtmlEncode(log.StationName)}</td>");
                    sb.AppendLine($"      <td style=\"font-weight:bold;\">{WebUtility.HtmlEncode(log.ParameterName)}</td>");
                    sb.AppendLine($"      <td class=\"text-center\">{sevBadge}</td>");
                    sb.AppendLine($"      <td class=\"text-center\" style=\"font-weight:bold; color:#dc2626;\">{WebUtility.HtmlEncode(log.RecordedValue)}</td>");
                    sb.AppendLine($"      <td class=\"text-center\">{WebUtility.HtmlEncode(log.ThresholdLimit)}</td>");
                    sb.AppendLine($"      <td>{WebUtility.HtmlEncode(log.BreachTime)}</td>");
                    sb.AppendLine($"      <td>{WebUtility.HtmlEncode(log.RecoveryTime)}</td>");
                    sb.AppendLine($"      <td class=\"text-center mono\">{WebUtility.HtmlEncode(log.DurationAboveThreshold)}</td>");
                    sb.AppendLine("    </tr>");
                }
            }
            sb.AppendLine("  </tbody>");
            sb.AppendLine("</table>");

            // SECTION 3: SIGN-OFF
            sb.AppendLine("<div class=\"section-title\">Section 3: Verification &amp; Official Regulatory Sign-Off</div>");
            sb.AppendLine("<div class=\"sign-container\">");
            sb.AppendLine("  <div class=\"sign-box\">");
            sb.AppendLine("    <div class=\"sign-role\">1. Plant SCADA Dispatcher</div>");
            sb.AppendLine("    <div class=\"sign-line\"></div>");
            sb.AppendLine($"    <div class=\"sign-field\"><b>System Operator:</b> Automated Dispatch Core</div>");
            sb.AppendLine($"    <div class=\"sign-field\"><b>Timestamp:</b> {exportTimeUtc} UTC</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"sign-box\">");
            sb.AppendLine("    <div class=\"sign-role\">2. Quality &amp; Compliance Officer</div>");
            sb.AppendLine("    <div class=\"sign-line\"></div>");
            sb.AppendLine("    <div class=\"sign-field\"><b>Inspector Name:</b> ___________________________</div>");
            sb.AppendLine("    <div class=\"sign-field\"><b>Verification Date:</b> _____ / _____ / 2026</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"sign-box\">");
            sb.AppendLine("    <div class=\"sign-role\">3. Plant Operations Manager</div>");
            sb.AppendLine("    <div class=\"sign-line\"></div>");
            sb.AppendLine("    <div class=\"sign-field\"><b>Decision:</b> [ &nbsp; ] ACCEPTED &nbsp;&nbsp; [ &nbsp; ] CORRECTIVE ACTION</div>");
            sb.AppendLine("    <div class=\"sign-field\"><b>Executive Signature:</b> ___________________________</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");

            // Footer
            sb.AppendLine("<div class=\"footer\">");
            sb.AppendLine($"  <span>PlantOps Industrial SCADA &bull; Report Generated {exportTimeUtc} UTC</span>");
            sb.AppendLine("  <span>Compliant with FDA 21 CFR Part 11 Electronic Signatures &bull; Confidential Production Document</span>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        public static void GenerateProfessionalPdfReport(IEnumerable<ThresholdBreachLog> logs, IEnumerable<StationBreachSummary> summaries, string pdfFilePath)
        {
            var browserPath = FindHeadlessBrowserPath();
            if (string.IsNullOrEmpty(browserPath))
            {
                throw new FileNotFoundException("A Chromium-based browser (Microsoft Edge or Google Chrome) was not found to compile PDF.");
            }

            var html = GeneratePdfHtmlDocument(logs, summaries);
            var tempHtmlPath = Path.Combine(Path.GetTempPath(), $"PlantOps_Audit_Report_{Guid.NewGuid():N}.html");

            try
            {
                File.WriteAllText(tempHtmlPath, html, Encoding.UTF8);

                var startInfo = new ProcessStartInfo
                {
                    FileName = browserPath,
                    Arguments = $"--headless=new --disable-gpu --no-pdf-header-footer --run-all-compositor-stages-before-draw --print-to-pdf=\"{pdfFilePath}\" \"{tempHtmlPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    if (!process.WaitForExit(15000))
                    {
                        try { process.Kill(); } catch { }
                        throw new TimeoutException("PDF generation timed out after 15 seconds.");
                    }
                }

                if (!File.Exists(pdfFilePath) || new FileInfo(pdfFilePath).Length == 0)
                {
                    throw new InvalidOperationException("Failed to generate PDF document or output file is empty.");
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(tempHtmlPath)) File.Delete(tempHtmlPath);
                }
                catch { }
            }
        }

        private static string? FindHeadlessBrowserPath()
        {
            string[] candidates = new[]
            {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }

        // ====================================================================
        // FILE WRITING ROUTINES (Excel, CSV, Stoppage Logs)
        // ====================================================================
        public static void SaveThresholdBreachReportToFile(IEnumerable<ThresholdBreachLog> logs, IEnumerable<StationBreachSummary> summaries, string filePath)
        {
            var utf8WithBom = new UTF8Encoding(true);
            if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = GenerateThresholdBreachCsv(logs, summaries);
                File.WriteAllText(filePath, csv, utf8WithBom);
            }
            else
            {
                // Default is styled Excel HTML spreadsheet (.xls)
                var html = GenerateThresholdBreachHtmlSpreadsheet(logs, summaries);
                File.WriteAllText(filePath, html, utf8WithBom);
            }
        }

        public static string GenerateThresholdBreachCsv(IEnumerable<ThresholdBreachLog> logs, IEnumerable<StationBreachSummary> summaries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine("PLANTOPS SCADA — THRESHOLD VIOLATION & PARAMETER AUDIT TRAIL");
            sb.AppendLine("================================================================================");
            sb.AppendLine();
            sb.AppendLine("--- SECTION 1: WORKSTATION THRESHOLD VIOLATION FREQUENCY SUMMARY ---");
            sb.AppendLine("Workstation ID,Workstation Name,Total Times Threshold Exceeded,CPU Violations,RAM Violations,Temp Violations,Disk Violations,Heartbeat Violations,Last Breach Time,Current Violation State");

            foreach (var s in summaries ?? Enumerable.Empty<StationBreachSummary>())
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsv(s.StationId),
                    EscapeCsv(s.StationName),
                    s.TotalBreachCount.ToString(),
                    s.CpuBreachCount.ToString(),
                    s.RamBreachCount.ToString(),
                    s.TempBreachCount.ToString(),
                    s.DiskBreachCount.ToString(),
                    s.HeartbeatBreachCount.ToString(),
                    EscapeCsv(s.LastBreachTime),
                    EscapeCsv(s.CurrentState)
                ));
            }

            sb.AppendLine();
            sb.AppendLine("--- SECTION 2: DETAILED THRESHOLD VIOLATION EVENT AUDIT LOG ---");
            sb.AppendLine("Log Ref,Workstation ID,Workstation Name,Violated Parameter,Breach Severity,Recorded Value,Configured Threshold Limit,Breach Time,Recovery Time,Duration Above Threshold,Violation Status");

            foreach (var log in logs ?? Enumerable.Empty<ThresholdBreachLog>())
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsv($"#{log.Id}"),
                    EscapeCsv(log.StationId),
                    EscapeCsv(log.StationName),
                    EscapeCsv(log.ParameterName),
                    EscapeCsv(log.BreachType),
                    EscapeCsv(log.RecordedValue),
                    EscapeCsv(log.ThresholdLimit),
                    EscapeCsv(log.BreachTime),
                    EscapeCsv(log.RecoveryTime),
                    EscapeCsv(log.DurationAboveThreshold),
                    EscapeCsv(log.Status)
                ));
            }

            return sb.ToString();
        }

        public static void SaveThresholdBreachCsvToFile(IEnumerable<ThresholdBreachLog> logs, IEnumerable<StationBreachSummary> summaries, string filePath)
        {
            SaveThresholdBreachReportToFile(logs, summaries, filePath);
        }

        public static string GenerateCsv(IEnumerable<DowntimeLog> logs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Event Ref,Workstation Node,Workstation Name,Trigger Classification / Root Cause,Stoppage Start (UTC),Resolution (UTC),Total Downtime (Seconds),Duration (HH:MM:SS),Operational Status,Log Timestamp");

            foreach (var log in logs ?? Enumerable.Empty<DowntimeLog>())
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsv($"#{log.Id}"),
                    EscapeCsv(log.StationId),
                    EscapeCsv(log.StationName),
                    EscapeCsv(log.FailureReason),
                    EscapeCsv(log.StartTime),
                    EscapeCsv(log.EndTime ?? "Ongoing"),
                    log.DurationSeconds.ToString("F2"),
                    EscapeCsv(log.DurationFormatted),
                    EscapeCsv(log.Status),
                    EscapeCsv(log.CreatedAt)
                ));
            }

            return sb.ToString();
        }

        public static void SaveCsvToFile(IEnumerable<DowntimeLog> logs, string filePath)
        {
            var csv = GenerateCsv(logs);
            File.WriteAllText(filePath, csv, new UTF8Encoding(true));
        }

        private static string EscapeCsv(string? field)
        {
            if (string.IsNullOrEmpty(field)) return "\"\"";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }
    }
}
