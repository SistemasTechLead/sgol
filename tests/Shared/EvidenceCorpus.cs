using System.Text;

namespace Sgol.Testing;

internal static class EvidenceCorpus
{
    internal static byte[] Jpeg()
    {
        return
        [
            0xff, 0xd8,
            0xff, 0xc0, 0x00, 0x0b, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00,
            0xff, 0xda, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3f, 0x00,
            0x00,
            0xff, 0xd9
        ];
    }

    internal static byte[] Png() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    internal static byte[] Pdf()
    {
        const string prefix = "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\n";
        var xrefOffset = Encoding.ASCII.GetByteCount(prefix);
        var document = prefix +
            "xref\n0 2\n0000000000 65535 f \n0000000009 00000 n \n" +
            "trailer\n<< /Size 2 /Root 1 0 R >>\nstartxref\n" +
            xrefOffset.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            "\n%%EOF\n";
        return Encoding.ASCII.GetBytes(document);
    }

    internal static byte[] InvalidSignature() => "synthetic-invalid-signature"u8.ToArray();

    internal static byte[] PdfWithoutEndMarker() => Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\nstartxref\n9\n");

    internal static byte[] PdfWithOutOfRangeXref() => Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\nstartxref\n999999\n%%EOF\n");
}
