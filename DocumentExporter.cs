using System;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Document = DocumentFormat.OpenXml.Wordprocessing.Document;
using PageSize = iTextSharp.text.PageSize;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;

namespace BrowseJobs;

public abstract class DocumentExporter
{
    protected string OutputPath;
    protected string ResumeText;

    protected DocumentExporter(string outputPath, string resumeText)
    {
        OutputPath = outputPath ?? throw new ArgumentNullException(nameof(outputPath));
        ResumeText = resumeText ?? throw new ArgumentNullException(nameof(resumeText));
    }

    public abstract void Export();

    public static DocumentExporter Create(string type, string outputPath, string resumeText)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Export type cannot be null or empty.", nameof(type));

        switch (type.ToLower())
        {
            case "docx":
                return new DocxExporter(outputPath, resumeText);
            case "pdf":
                return new PdfExporter(outputPath, resumeText);
            default:
                throw new NotSupportedException($"Unsupported export format: {type}");
        }
    }

    private class DocxExporter : DocumentExporter
    {
        public DocxExporter(string outputPath, string resumeText)
            : base(outputPath, resumeText)
        {
        }

        public override void Export()
        {
            try
            {
                // Validate output directory exists
                var directory = Path.GetDirectoryName(OutputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using var doc = WordprocessingDocument.Create(
                    OutputPath, WordprocessingDocumentType.Document);

                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = new Body();

                foreach (var line in ResumeText.Split('\n'))
                {
                    var para = new Paragraph(new Run(new Text(line)));
                    body.Append(para);
                }

                mainPart.Document.Append(body);
                mainPart.Document.Save();
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException($"Access denied when creating DOCX file: {OutputPath}", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new InvalidOperationException($"Directory not found for DOCX file: {OutputPath}", ex);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"IO error when creating DOCX file: {OutputPath}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error when creating DOCX file: {ex.Message}", ex);
            }
        }
    }

    private class PdfExporter : DocumentExporter
    {
        public PdfExporter(string outputPath, string resumeText)
            : base(outputPath, resumeText)
        {
        }

        public override void Export()
        {
            FileStream stream = null;
            iTextSharp.text.Document pdfDoc = null;

            try
            {
                // Validate output directory exists
                var directory = Path.GetDirectoryName(OutputPath) ?? string.Empty;
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                stream = new FileStream(OutputPath, FileMode.Create);
                pdfDoc = new iTextSharp.text.Document(PageSize.A4);
                PdfWriter.GetInstance(pdfDoc, stream);
                pdfDoc.Open();

                var font = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                foreach (var line in ResumeText.Split('\n')) pdfDoc.Add(new iTextSharp.text.Paragraph(line, font));
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException($"Access denied when creating PDF file: {OutputPath}", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new InvalidOperationException($"Directory not found for PDF file: {OutputPath}", ex);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"IO error when creating PDF file: {OutputPath}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Unexpected error when creating PDF file: {ex.Message}", ex);
            }
            finally
            {
                try
                {
                    pdfDoc?.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error closing PDF document: {ex.Message}");
                }

                try
                {
                    stream?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error disposing file stream: {ex.Message}");
                }
            }
        }
    }
}

internal class Program8
{
    private static void Main8()
    {
        try
        {
            const string resumeFilePath = "resume.txt";

            // Check if resume file exists
            if (!File.Exists(resumeFilePath))
            {
                Console.WriteLine($"Error: Resume file '{resumeFilePath}' not found.");
                Console.WriteLine("Please create a resume.txt file in the application directory.");
                return;
            }

            var resumeText = File.ReadAllText(resumeFilePath);

            if (string.IsNullOrWhiteSpace(resumeText)) Console.WriteLine("Warning: Resume file is empty.");

            // Export to DOCX
            try
            {
                var docxExporter = DocumentExporter.Create("docx", "resume.docx", resumeText);
                docxExporter.Export();
                Console.WriteLine("DOCX export completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting to DOCX: {ex.Message}");
            }

            // Export to PDF
            try
            {
                var pdfExporter = DocumentExporter.Create("pdf", "resume.pdf", resumeText);
                pdfExporter.Export();
                Console.WriteLine("PDF export completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting to PDF: {ex.Message}");
            }

            Console.WriteLine("Resume export process completed.");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Access denied when reading resume file: {ex.Message}");
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine($"Resume file not found: {ex.Message}");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"IO error when reading resume file: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}