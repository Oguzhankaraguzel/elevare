using System.IO.Compression;
using System.Text;
using SharedKernel.Forms;
using Shouldly;

namespace Cms.Tests.Forms;

/// <summary>
/// The one gate on a public upload: what the bytes are, not what the name says.
/// </summary>
public sealed class FormAttachmentPolicyTests
{
    [Fact]
    public void Each_allowed_kind_is_recognised_by_its_leading_bytes()
    {
        FormAttachmentPolicy.Detect("%PDF-1.7\n"u8).ShouldNotBeNull().Extension.ShouldBe(".pdf");
        FormAttachmentPolicy.Detect([0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10]).ShouldNotBeNull().Extension.ShouldBe(".jpg");
        FormAttachmentPolicy.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0]).ShouldNotBeNull().Extension.ShouldBe(".png");
        byte[] webp = [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBPVP8 "u8];
        FormAttachmentPolicy.Detect(webp).ShouldNotBeNull().Extension.ShouldBe(".webp");
        FormAttachmentPolicy.Detect(Docx()).ShouldNotBeNull().Extension.ShouldBe(".docx");
    }

    [Fact]
    public void Anything_else_is_refused_whatever_it_is_called()
    {
        FormAttachmentPolicy.Detect("MZ\u0090\0\u0003"u8).ShouldBeNull();           // an .exe named cv.pdf
        FormAttachmentPolicy.Detect("<svg xmlns="u8).ShouldBeNull();               // SVG carries script
        FormAttachmentPolicy.Detect("GIF89a"u8).ShouldBeNull();
        FormAttachmentPolicy.Detect(Zip("data/x.csv")).ShouldBeNull();            // a ZIP that is not a Word file
        FormAttachmentPolicy.Detect([]).ShouldBeNull();
    }

    [Fact]
    public void The_stored_name_is_the_visitors_name_made_safe_with_the_real_extension()
    {
        FormAttachmentPolicy.Kind pdf = FormAttachmentPolicy.Allowed[0];
        FormAttachmentPolicy.SafeFileName(@"C:\Users\ali\..\Özgeçmiş.exe", pdf).ShouldBe("Özgeçmiş.pdf");
        FormAttachmentPolicy.SafeFileName("", pdf).ShouldBe("dosya.pdf");
        FormAttachmentPolicy.SafeFileName(new string('a', 300) + ".jpg", pdf).Length.ShouldBe(124);
        FormAttachmentPolicy.SafeFileName("a\u0000b\nc.pdf", pdf).ShouldBe("abc.pdf");
    }

    private static byte[] Docx() => Zip("[Content_Types].xml", "_rels/.rels", "word/document.xml");

    private static byte[] Zip(params string[] entries)
    {
        using MemoryStream ms = new();
        using (ZipArchive zip = new(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (string name in entries)
            {
                using Stream s = zip.CreateEntry(name).Open();
                s.Write(Encoding.UTF8.GetBytes("<x/>"));
            }
        }
        return ms.ToArray();
    }
}
