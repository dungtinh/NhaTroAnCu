using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Web.Hosting;

namespace NhaTroAnCu.Helpers
{
    public class CustomPdfPageEventHelper : PdfPageEventHelper
    {
        private BaseFont baseFont;
        private PdfContentByte cb;
        private PdfTemplate template;

        public override void OnOpenDocument(PdfWriter writer, Document document)
        {
            try
            {
                string fontPath = HostingEnvironment.MapPath("~/Fonts/times.ttf");
                baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                cb = writer.DirectContent;
                template = cb.CreateTemplate(50, 50);
            }
            catch (DocumentException de)
            {
                // Handle exception
            }
            catch (System.IO.IOException ioe)
            {
                // Handle exception
            }
        }

        public override void OnEndPage(PdfWriter writer, Document document)
        {
            base.OnEndPage(writer, document);

            int pageN = writer.PageNumber;
            string text = "Trang " + pageN + " / ";
            float len = baseFont.GetWidthPoint(text, 10);

            Rectangle pageSize = document.PageSize;

            // Header
            cb.BeginText();
            cb.SetFontAndSize(baseFont, 10);
            cb.SetTextMatrix(40, pageSize.GetTop(40));
            cb.ShowText("NHÀ TRỌ AN CƯ - DANH SÁCH NGƯỜI THUÊ");
            cb.EndText();

            // Footer - Page number
            cb.BeginText();
            cb.SetFontAndSize(baseFont, 10);
            cb.SetTextMatrix(pageSize.GetRight(100), pageSize.GetBottom(30));
            cb.ShowText(text);
            cb.EndText();

            cb.AddTemplate(template, pageSize.GetRight(100) + len, pageSize.GetBottom(30));

            // Footer - Date
            cb.BeginText();
            cb.SetFontAndSize(baseFont, 8);
            cb.SetTextMatrix(pageSize.GetLeft(40), pageSize.GetBottom(30));
            cb.ShowText($"Ngày in: {DateTime.Now:dd/MM/yyyy HH:mm}");
            cb.EndText();

            // Line separator in header
            cb.SetLineWidth(0.5f);
            cb.SetColorStroke(BaseColor.GRAY);
            cb.MoveTo(40, pageSize.GetTop(50));
            cb.LineTo(pageSize.GetRight(40), pageSize.GetTop(50));
            cb.Stroke();

            // Line separator in footer
            cb.MoveTo(40, pageSize.GetBottom(40));
            cb.LineTo(pageSize.GetRight(40), pageSize.GetBottom(40));
            cb.Stroke();
        }

        public override void OnCloseDocument(PdfWriter writer, Document document)
        {
            base.OnCloseDocument(writer, document);

            template.BeginText();
            template.SetFontAndSize(baseFont, 10);
            template.SetTextMatrix(0, 0);
            template.ShowText("" + writer.PageNumber);
            template.EndText();
        }
    }
}