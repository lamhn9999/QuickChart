using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LQ.Services.Implementations;

// Server-side SVG silhouette generator. Walks the canvas Document JSON and
// stamps a tiny abstract preview that hints at the canvas's shape (text bars,
// chart silhouettes) without rendering real data. Output is a self-contained
// <svg> string suitable for inline injection.
public class CanvasPreviewService
{
    private const int Width  = 320;
    private const int Height = 240;

    // The first chart we encounter gets the salmon accent so previews have one
    // visual anchor — everything else stays in the neutral silhouette tone.
    private const string Accent     = "#E9967A";
    private const string Silhouette = "rgba(24,24,27,0.18)";

    public string BuildSvg(string? documentJson)
    {
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {Width} {Height}\" preserveAspectRatio=\"xMidYMid slice\">");
        sb.Append($"<rect width=\"100%\" height=\"100%\" fill=\"#FBFBFD\"/>");

        var blocks = ParseBlocks(documentJson);
        if (blocks.Count == 0)
        {
            sb.Append($"<text x=\"{Width / 2}\" y=\"{Height / 2 + 4}\" text-anchor=\"middle\" font-family=\"DM Sans, sans-serif\" font-size=\"24\" font-weight=\"300\" fill=\"{Silhouette}\">—</text>");
            sb.Append("</svg>");
            return sb.ToString();
        }

        int cursorY = 20;
        bool accentUsed = false;
        const int padX = 16;
        const int gap = 10;

        foreach (var block in blocks)
        {
            if (cursorY > Height - 20) break;
            int x = padX;
            int w = Width - 2 * padX;
            string color = Silhouette;
            int consumed;

            switch (block.Kind)
            {
                case "h1": case "heading1":
                    sb.Append($"<rect x=\"{x}\" y=\"{cursorY}\" width=\"{w * 0.6:F0}\" height=\"10\" rx=\"2\" fill=\"{Silhouette}\"/>");
                    consumed = 18;
                    break;
                case "h2": case "heading2":
                    sb.Append($"<rect x=\"{x}\" y=\"{cursorY}\" width=\"{w * 0.45:F0}\" height=\"8\" rx=\"2\" fill=\"{Silhouette}\"/>");
                    consumed = 14;
                    break;
                case "h3": case "heading3":
                    sb.Append($"<rect x=\"{x}\" y=\"{cursorY}\" width=\"{w * 0.35:F0}\" height=\"6\" rx=\"2\" fill=\"{Silhouette}\"/>");
                    consumed = 12;
                    break;
                case "paragraph": case "text": case "textCard": case "callout":
                    consumed = AppendTextLines(sb, x, cursorY, w, lines: 3);
                    break;
                case "divider":
                    sb.Append($"<line x1=\"{x}\" y1=\"{cursorY + 4}\" x2=\"{x + w}\" y2=\"{cursorY + 4}\" stroke=\"{Silhouette}\" stroke-width=\"1\"/>");
                    consumed = 10;
                    break;
                case "code":
                    sb.Append($"<rect x=\"{x}\" y=\"{cursorY}\" width=\"{w}\" height=\"36\" rx=\"3\" fill=\"rgba(24,24,27,0.05)\"/>");
                    for (int li = 0; li < 3; li++)
                        sb.Append($"<rect x=\"{x + 6}\" y=\"{cursorY + 6 + li * 10}\" width=\"{w * (0.5 + li * 0.15):F0}\" height=\"4\" rx=\"1\" fill=\"{Silhouette}\"/>");
                    consumed = 44;
                    break;
                case "chart":
                    color = accentUsed ? Silhouette : Accent;
                    accentUsed = true;
                    consumed = AppendChartSilhouette(sb, x, cursorY, w, height: 56, chartType: block.ChartType, color);
                    break;
                case "filter":
                    sb.Append($"<rect x=\"{x}\" y=\"{cursorY}\" width=\"{w * 0.5:F0}\" height=\"14\" rx=\"7\" fill=\"none\" stroke=\"{Silhouette}\" stroke-width=\"1\"/>");
                    sb.Append($"<circle cx=\"{x + 10}\" cy=\"{cursorY + 7}\" r=\"2\" fill=\"{Silhouette}\"/>");
                    sb.Append($"<circle cx=\"{x + 20}\" cy=\"{cursorY + 7}\" r=\"2\" fill=\"{Silhouette}\"/>");
                    consumed = 22;
                    break;
                default:
                    consumed = AppendTextLines(sb, x, cursorY, w, lines: 1);
                    break;
            }
            cursorY += consumed + gap;
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static int AppendTextLines(StringBuilder sb, int x, int y, int w, int lines)
    {
        double[] widths = lines switch
        {
            1 => new[] { 0.72 },
            2 => new[] { 0.92, 0.6 },
            _ => new[] { 0.92, 0.86, 0.55 }
        };
        for (int i = 0; i < widths.Length; i++)
            sb.Append($"<rect x=\"{x}\" y=\"{y + i * 7}\" width=\"{w * widths[i]:F0}\" height=\"4\" rx=\"1\" fill=\"{Silhouette}\"/>");
        return widths.Length * 7;
    }

    // ChartType enum integers map (in chart.js):
    // 0 pie, 1 bar, 2 line, 3 bar, 4 scatter, 5 polarArea, 6 bar, 7 mixed,
    // 8 matrix, 9 mixedBar, 10 bubble.
    private static int AppendChartSilhouette(StringBuilder sb, int x, int y, int w, int height, int? chartType, string color)
    {
        // Light card background so the silhouette reads as a chart card.
        sb.Append($"<rect x=\"{x}\" y=\"{y}\" width=\"{w}\" height=\"{height}\" rx=\"4\" fill=\"none\" stroke=\"{Silhouette}\" stroke-width=\"1\"/>");
        int ix = x + 6, iy = y + 6, iw = w - 12, ih = height - 12;

        switch (chartType)
        {
            case 0: // pie
            case 5: // polarArea
                {
                    int cx = ix + iw / 2;
                    int cy = iy + ih / 2;
                    int r = Math.Min(iw, ih) / 2 - 2;
                    sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{color}\"/>");
                    sb.Append($"<line x1=\"{cx}\" y1=\"{cy}\" x2=\"{cx + r}\" y2=\"{cy}\" stroke=\"#FBFBFD\" stroke-width=\"1\"/>");
                    sb.Append($"<line x1=\"{cx}\" y1=\"{cy}\" x2=\"{cx - r / 2}\" y2=\"{cy - (int)(r * 0.87)}\" stroke=\"#FBFBFD\" stroke-width=\"1\"/>");
                    break;
                }
            case 2: // line
            case 7: // mixed
                {
                    var pts = new[] { 0.0, 0.4, 0.25, 0.65, 0.5, 0.85, 0.7 };
                    var path = new StringBuilder();
                    for (int i = 0; i < pts.Length; i++)
                    {
                        var px = ix + (int)(iw * (double)i / (pts.Length - 1));
                        var py = iy + ih - (int)(ih * pts[i]);
                        path.Append(i == 0 ? "M" : "L").Append(px.ToString(CultureInfo.InvariantCulture))
                            .Append(' ').Append(py.ToString(CultureInfo.InvariantCulture)).Append(' ');
                    }
                    sb.Append($"<path d=\"{path}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"1.5\" stroke-linejoin=\"round\"/>");
                    break;
                }
            case 4: // scatter
            case 10: // bubble
                {
                    var rand = new[] { (0.15, 0.7), (0.3, 0.4), (0.45, 0.55), (0.55, 0.25), (0.7, 0.6), (0.85, 0.35) };
                    foreach (var (rx, ry) in rand)
                    {
                        var cx = ix + (int)(iw * rx);
                        var cy = iy + (int)(ih * ry);
                        sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"2\" fill=\"{color}\"/>");
                    }
                    break;
                }
            case 8: // matrix
                {
                    int cols = 5, rows = 3;
                    int cellW = iw / cols, cellH = ih / rows;
                    var opacities = new[] { 0.3, 0.5, 0.7, 0.45, 0.6, 0.4, 0.55, 0.35, 0.8, 0.5, 0.6, 0.4, 0.3, 0.7, 0.5 };
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                        {
                            int idx = r * cols + c;
                            sb.Append($"<rect x=\"{ix + c * cellW}\" y=\"{iy + r * cellH}\" width=\"{cellW - 1}\" height=\"{cellH - 1}\" fill=\"{color}\" fill-opacity=\"{opacities[idx % opacities.Length].ToString(CultureInfo.InvariantCulture)}\"/>");
                        }
                    break;
                }
            default: // bar / sideBar / mixedBar / unknown → bars
                {
                    var heights = new[] { 0.4, 0.7, 0.55, 0.85, 0.5, 0.65 };
                    int barW = (iw - (heights.Length - 1) * 2) / heights.Length;
                    for (int i = 0; i < heights.Length; i++)
                    {
                        int bh = (int)(ih * heights[i]);
                        int bx = ix + i * (barW + 2);
                        int by = iy + ih - bh;
                        sb.Append($"<rect x=\"{bx}\" y=\"{by}\" width=\"{barW}\" height=\"{bh}\" fill=\"{color}\"/>");
                    }
                    break;
                }
        }
        return height;
    }

    private record BlockHint(string Kind, int? ChartType);

    private static List<BlockHint> ParseBlocks(string? documentJson)
    {
        if (string.IsNullOrWhiteSpace(documentJson) || documentJson == "null")
            return new List<BlockHint>();

        try
        {
            using var doc = JsonDocument.Parse(documentJson);
            if (!doc.RootElement.TryGetProperty("blocks", out var blocks)
                || blocks.ValueKind != JsonValueKind.Array)
                return new List<BlockHint>();

            var result = new List<BlockHint>();
            foreach (var el in blocks.EnumerateArray())
            {
                var kind = el.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String
                    ? k.GetString() ?? "text" : "text";
                int? chartType = null;
                if (kind == "chart" && el.TryGetProperty("chartType", out var ct)
                                    && ct.ValueKind == JsonValueKind.Number
                                    && ct.TryGetInt32(out var cti))
                    chartType = cti;
                result.Add(new BlockHint(kind, chartType));
            }
            return result;
        }
        catch (JsonException)
        {
            return new List<BlockHint>();
        }
    }
}
